using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Text;
using CapstoneDesign.Runtime.Personalization;
using UnityEngine;
using UnityEngine.Networking;

namespace CapstoneDesign.Runtime
{
    // Operators provision an individual enrollment in app-private storage only
    // after written consent. Registered study collection starts without an app
    // consent screen; ordinary unregistered installations do not upload research data.
    public sealed partial class PersonalizationClient : MonoBehaviour
    {
        [Serializable] sealed class Enrollment { public string endpoint, token; }
        static PersonalizationClient instance;
        static bool localResetBlocked;
        UnityWebRequest activeRequest;
        int localResetGeneration;
        EventQueue queue;
        Enrollment enrollment;
        ResponsePolicy policy = new ResponsePolicy();
        bool busy, policyLoaded, initializationAttempted, enrollmentInitializationPending = true;
        long nextPolicyRefresh;
        float nextAttempt, nextActivationAttempt, nextEnrollmentAttempt;
        string status = "연결 설정 없음";
        public static PersonalizationClient Instance
        {
            get
            {
                if (instance == null)
                {
                    var owner = new GameObject("Personalization client");
                    if(Application.isPlaying)DontDestroyOnLoad(owner);
                    else owner.hideFlags=HideFlags.HideAndDontSave;
                    instance = owner.AddComponent<PersonalizationClient>();
                }
                return instance;
            }
        }
        static bool CollectionContextAllowed
        {
            get
            {
                if(!Application.isPlaying)return false;
#if UNITY_EDITOR
                if(!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("CAPSTONE_PERSONALIZATION_TEST_ROOT")))return true;
                if(!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("CAPSTONE_SMOKE_SAVE_ROOT")))return false;
#endif
                return AndroidPlatformBridge.InputMode==PlatformInputMode.Live;
            }
        }
        public bool Configured => !localResetBlocked && CollectionContextAllowed && enrollment != null && queue != null && queue.BoundTo(EventQueue.Fingerprint(enrollment.endpoint, enrollment.token));
        public bool Consented => Configured && queue.Enabled;
        public bool DeletionPending => queue != null && queue.DeletePending;
        public bool EnrollmentRevoked => queue != null && queue.EnrollmentRevoked;
        public int PendingCount => queue?.Count??0;
        public string Status => status;
        public long ConsentStartedUtcMs => Consented ? queue.ConsentStartedUtcMs : long.MaxValue;
        public ResponsePolicy Policy => Consented && policy.Valid(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()) ? policy : new ResponsePolicy();
        void Awake()
        {
            if(!Application.isPlaying)return;
            if (instance != null && instance != this) { Destroy(gameObject); return; }
            instance = this;
            if(CollectionContextAllowed)InitializeEnrollment();
        }
        void InitializeEnrollment()
        {
            initializationAttempted=true;
            enrollmentInitializationPending=true;
            nextEnrollmentAttempt=Time.unscaledTime+3;
            try
            {
                string directory = PrivateDirectory();
                if (localResetBlocked || File.Exists(Path.Combine(directory,"personalization-local-reset")))
                { localResetBlocked=true; status="연결 설정 없음"; return; }
                var loadedQueue = new EventQueue(new FileEventQueueStore(Path.Combine(directory, "behavior-queue.xml")));
                string path = Path.Combine(directory, "personalization-enrollment.json");
                if (!File.Exists(path)) { enrollment=null; status="연결 설정 없음"; return; }
                var candidate = JsonUtility.FromJson<Enrollment>(File.ReadAllText(path));
                if (candidate == null || !ValidEndpoint(candidate.endpoint) || candidate.token == null || candidate.token.Length < 32
                    || candidate.token.Length>512 || candidate.token.Any(c=>c<33 || c>126)) { enrollment=null; status = "연결 설정 확인 필요"; return; }
                candidate.endpoint = candidate.endpoint.TrimEnd('/');
                loadedQueue.Bind(EventQueue.Fingerprint(candidate.endpoint, candidate.token));
                queue = loadedQueue;
                enrollment = candidate;
                enrollmentInitializationPending=false;
                status = queue.DeletePending ? "서버 기록 삭제 대기" : queue.EnrollmentRevoked ? "참가 등록 폐기됨" : "참가 등록 확인됨";
                // Preserve a prior authorized collection boundary and pending data.
                // First activation uses now; never backfill pre-enrollment sessions.
                // Removing the UI must not undo an operator's pending/revoked deletion.
                if (!queue.DeletePending && !queue.EnrollmentRevoked)
                {
                    nextActivationAttempt = Time.unscaledTime + 3;
                    SetConsent(true);
                }
                else SynchronizeMeasurementConsent(forceDisable:true);
            }
            catch (Exception) { status = "저장된 설정 확인 필요"; enrollment = null; }
        }
        static string PrivateDirectory()
        {
#if UNITY_EDITOR
            var isolated=Environment.GetEnvironmentVariable("CAPSTONE_PERSONALIZATION_TEST_ROOT");
            if(!string.IsNullOrEmpty(isolated))return isolated;
#endif
#if UNITY_ANDROID && !UNITY_EDITOR
            using (var unity = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
            using (var activity = unity.GetStatic<AndroidJavaObject>("currentActivity"))
            using (var files = activity.Call<AndroidJavaObject>("getFilesDir"))
                return files.Call<string>("getAbsolutePath");
#else
            return Path.Combine(Application.persistentDataPath, "personalization");
#endif
        }
        // Local app reset is deliberately separate from SetConsent(false), which
        // performs the operator's server-side participant deletion protocol.
        // No singleton is created and no network/native request is issued here.
        public static PlatformActionResult ResetLocalDataForAppReset(string resetId)
        {
            if(string.IsNullOrEmpty(resetId) || resetId.Length>128 || resetId.Any(c=>!(c>='a' && c<='z') && !(c>='A' && c<='Z') && !(c>='0' && c<='9') && c!='-' && c!='_' && c!=':'))
                return new PlatformActionResult {status="ERROR",errorCode="INVALID_RESET_ID",message="초기화 요청을 확인해 주세요."};
            // A local reset must not erase an already committed server-deletion
            // obligation or its credentials. Check durable state even when the
            // client has not started yet. Leave Update alive to finish DELETE.
            try
            {
                string queuePath=Path.Combine(PrivateDirectory(),"behavior-queue.xml");
                var saved=new FileEventQueueStore(queuePath).Load();
                if((instance!=null && instance.DeletionPending) || (saved!=null && saved.DeletePending))
                {
                    if(CollectionContextAllowed)
                    {
                        var client=Instance;
                        client.nextAttempt=0;
                    }
                    return new PlatformActionResult {status="PENDING",errorCode="SERVER_DELETION_PENDING",
                        message="서버 기록 삭제를 확인한 뒤 초기화를 마칠 수 있어요. 연결 후 다시 시도해 주세요."};
                }
            }
            catch(Exception)
            {
                return new PlatformActionResult {status="ERROR",errorCode="DELETION_STATE_UNREADABLE",
                    message="서버 기록 삭제 상태를 확인하지 못했어요. 연결 기록을 보존하고 다시 시도해 주세요."};
            }
            localResetBlocked=true;
            if(instance!=null)
            {
                instance.localResetGeneration++;
                try {instance.activeRequest?.Abort();} catch(Exception) { }
                instance.StopAllCoroutines();
                try {instance.activeRequest?.Dispose();} catch(Exception) { }
                instance.activeRequest=null; instance.queue=null; instance.enrollment=null;
                instance.policy=new ResponsePolicy(); instance.policyLoaded=false; instance.busy=false;
                instance.initializationAttempted=true; instance.nextPolicyRefresh=0;
                instance.enrollmentInitializationPending=false;
                instance.pendingFocusExposures.Clear();
                instance.nextAttempt=instance.nextActivationAttempt=instance.nextMeasurementSync=0;
                instance.measurementSyncKey=null; instance.status="연결 설정 없음";
            }
            try
            {
                string directory=PrivateDirectory(); Directory.CreateDirectory(directory);
                // A crash after the first deletion must never reactivate old enrollment.
                using(var marker=new FileStream(Path.Combine(directory,"personalization-local-reset"),FileMode.Create,FileAccess.Write,FileShare.Read))
                {var bytes=Encoding.UTF8.GetBytes(resetId);marker.Write(bytes,0,bytes.Length);marker.Flush(true);}
                foreach(string name in new[]{"personalization-enrollment.json","behavior-queue.xml"})
                    foreach(string suffix in new[]{"",".tmp",".bak",".new"})
                    {string path=Path.Combine(directory,name+suffix);File.Delete(path);if(File.Exists(path))throw new IOException();}
                return new PlatformActionResult {status="AVAILABLE",message="앱 내부 연결 기록을 초기화했어요."};
            }
            catch(Exception) {return new PlatformActionResult {status="ERROR",errorCode="PERSONALIZATION_RESET_PENDING",message="연결 기록을 지우지 못했어요. 다시 시도해 주세요."};}
        }
        static bool ValidEndpoint(string value)
        {
            Uri uri;
            if (!Uri.TryCreate(value, UriKind.Absolute, out uri) || uri.Query.Length > 0 || uri.Fragment.Length > 0
                || uri.UserInfo.Length > 0 || uri.AbsolutePath != "/") return false;
            return uri.Scheme == "https" || (Debug.isDebugBuild || Application.isEditor) && uri.Scheme == "http" && uri.IsLoopback;
        }
        // Internal lifecycle control for enrollment and operator/test withdrawal.
        // There is deliberately no participant-facing toggle or consent action.
        public bool SetConsent(bool enabled)
        {
            if (!Configured) return false;
            try
            {
                if (!enabled && !SynchronizeMeasurementConsent(forceDisable:true)) throw new IOException();
                queue.SetConsent(enabled,DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
                if (enabled) queue.EnableMeasurements(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
                if (!SynchronizeMeasurementConsent()) throw new IOException();
                if (!enabled) { policy = new ResponsePolicy(); policyLoaded = false; }
                nextAttempt = 0;
                status = enabled ? "추천 개선에 참여 중" : "기기 대기 기록 삭제됨 · 서버 삭제 대기";
                return true;
            }
            catch (Exception) { status = "설정을 저장하지 못했어요"; return false; }
        }
        void ActivateRegisteredCollection()
        {
            nextActivationAttempt = Time.unscaledTime + 3;
            // A transient durable queue write failure must recover without a UI
            // action. Native consent synchronization has its own retry cadence.
            // Nonzero boundaries are idempotent; withdrawal is never reopened.
            if (Configured && !queue.DeletePending && !queue.EnrollmentRevoked
                && (!queue.Enabled || queue.MeasurementConsentUtcMs == 0)) SetConsent(true);
        }
        public bool Record(string kind, string decisionId, string activityId, float[] features, string policyId)
        {
            if (!Consented) return false;
            try
            {
                var now = DateTimeOffset.Now;
                return queue.Add(new BehaviorEvent { event_id=Guid.NewGuid().ToString("N"), decision_id=decisionId, kind=kind,
                    occurred_utc_ms=now.ToUnixTimeMilliseconds(), utc_offset_minutes=(int)now.Offset.TotalMinutes,
                    activity_id=activityId, policy_id=policyId, app_version=Application.version, content_version="courses-v1", features=features });
            }
            catch (Exception) { status = "참여 기록 전송 대기"; return false; }
        }
        public void RecordSession(LocalState.ActivitySession session)
        {
            if(!Consented || session.Recommendation==null || !DateTimeOffset.TryParse(session.SelectedUtc,out var selected)
                || selected.ToUnixTimeMilliseconds()<ConsentStartedUtcMs)return;
            if(DateTimeOffset.TryParse(session.StartedUtc,out var started))RecordSessionEvent(session,"activity_started",started);
            if((session.Status==LocalState.ParticipationState.RewardCommitted || session.Status==LocalState.ParticipationState.Ended)
                && DateTimeOffset.TryParse(session.EndedUtc,out var ended))
                RecordSessionEvent(session,session.Status==LocalState.ParticipationState.RewardCommitted?"activity_completed":"activity_ended",ended);
        }
        void RecordSessionEvent(LocalState.ActivitySession session,string kind,DateTimeOffset occurred)
        {
            if(occurred<DateTimeOffset.UtcNow.AddDays(-90))return;
            var context=session.Recommendation;
            try
            {
                queue.Add(new BehaviorEvent {event_id=EventQueue.Fingerprint(session.SessionId,kind).Substring(0,32),
                    decision_id=context.DecisionId,kind=kind,activity_id=session.MissionId,policy_id=context.PolicyId,
                    features=context.Features,app_version=context.AppVersion,content_version="courses-v1",
                    occurred_utc_ms=occurred.ToUnixTimeMilliseconds(),utc_offset_minutes=context.UtcOffsetMinutes});
            }
            catch(Exception) {status="참여 기록 전송 대기";}
        }
        void Update()
        {
            if(localResetBlocked)return;
            if(CollectionContextAllowed && (!initializationAttempted || enrollmentInitializationPending && Time.unscaledTime>=nextEnrollmentAttempt))InitializeEnrollment();
            if(Time.unscaledTime>=nextMeasurementSync)
            {
                nextMeasurementSync=Time.unscaledTime+3;
                if(SynchronizeMeasurementConsent())RetryMeasuredFocusExposures();
            }
            if(!CollectionContextAllowed)return;
            if(Time.unscaledTime>=nextActivationAttempt)ActivateRegisteredCollection();
            if (!Configured || busy || Time.unscaledTime < nextAttempt || (!queue.Enabled && !queue.DeletePending)) return;
            nextAttempt = Time.unscaledTime + 60;
            StartCoroutine(Synchronize());
        }
        UnityWebRequest Request(string method, string path, string body=null)
        {
            var request = new UnityWebRequest(enrollment.endpoint + path, method);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.timeout = 15;
            request.redirectLimit = 0; // Never forward an enrollment bearer to a redirect target.
            request.SetRequestHeader("Authorization", "Bearer " + enrollment.token);
            if (body != null)
            {
                request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(body));
                request.SetRequestHeader("Content-Type", "application/json");
            }
            activeRequest=request;
            return request;
        }
        IEnumerator Synchronize()
        {
            int generation=localResetGeneration;
            busy = true;
            try
            {
                if (queue.DeletePending)
                {
                    using (var request = Request("DELETE", "/participant"))
                    {
                        yield return request.SendWebRequest();
                        if(generation!=localResetGeneration || localResetBlocked)yield break;
                        if (request.result == UnityWebRequest.Result.Success)
                        {
                            try
                            {
                                var acknowledgement=JsonUtility.FromJson<DeletionAcknowledgement>(request.downloadHandler.text);
                                if(acknowledgement==null || !acknowledgement.deleted || !acknowledgement.enrollment_revoked)throw new InvalidDataException();
                                queue.CompleteDeletion(); status = "참여 기록 삭제 완료";
                            }
                            catch (Exception) { status = "삭제 확인 저장 대기"; }
                        }
                        else status = "서버 기록 삭제 대기 · 연결되면 재시도";
                    }
                    yield break;
                }
                if (!policyLoaded || DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()>=nextPolicyRefresh)
                {
                    using (var request = Request("GET", "/policy/current"))
                    {
                        yield return request.SendWebRequest();
                        if(generation!=localResetGeneration || localResetBlocked)yield break;
                        if (queue.Enabled && request.result == UnityWebRequest.Result.Success)
                        {
                            try
                            {
                                var candidate = JsonUtility.FromJson<ResponsePolicy>(request.downloadHandler.text);
                                if (candidate != null && candidate.Valid(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()))
                                {
                                    policy = candidate; policyLoaded = true;
                                    long now=DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                                    nextPolicyRefresh=candidate.model_type=="logistic"?Math.Min(candidate.expires_utc_ms,now+3600000):now+3600000;
                                }
                            }
                            catch (Exception) { policy = new ResponsePolicy(); }
                        }
                    }
                }
                if (!queue.Enabled || !CollectionContextAllowed) { nextAttempt = 0; yield break; }
                yield return SynchronizeMeasurements();
                if(generation!=localResetGeneration || localResetBlocked)yield break;
                if (!queue.Enabled || !CollectionContextAllowed) { nextAttempt = 0; yield break; }
                BehaviorEvent[] sent;
                try { sent = queue.Batch(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()); }
                catch (Exception) { status = "참여 기록 확인 필요"; yield break; }
                if (sent.Length == 0) yield break;
                using (var request = Request("POST", "/events/batch", JsonUtility.ToJson(new BehaviorBatch { events=sent })))
                {
                    yield return request.SendWebRequest();
                    if(generation!=localResetGeneration || localResetBlocked)yield break;
                    if (!queue.Enabled) { nextAttempt = 0; yield break; }
                    if (request.result == UnityWebRequest.Result.Success)
                    {
                        try
                        {
                            var acknowledgement = JsonUtility.FromJson<BehaviorAcknowledgement>(request.downloadHandler.text);
                            if (acknowledgement?.accepted_event_ids == null) throw new InvalidDataException();
                            queue.Acknowledge(acknowledgement.accepted_event_ids, sent.Select(e => e.event_id));
                            status = "추천 개선에 참여 중";
                        }
                        catch (Exception) { status = "참여 기록 전송 확인 대기"; }
                    }
                    else status = "참여 기록 전송 대기 · 활동은 계속할 수 있어요";
                }
            }
            finally { if(generation==localResetGeneration) {busy = false;activeRequest=null;} }
        }
    }
}
