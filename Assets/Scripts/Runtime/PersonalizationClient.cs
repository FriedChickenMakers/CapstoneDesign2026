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
    // Enrollment is provisioned separately in app-private storage. Shipping a
    // universal bearer token or enabling research collection by default is forbidden.
    public sealed partial class PersonalizationClient : MonoBehaviour
    {
        [Serializable] sealed class Enrollment { public string endpoint, token; }
        static PersonalizationClient instance;
        EventQueue queue;
        Enrollment enrollment;
        ResponsePolicy policy = new ResponsePolicy();
        bool busy, policyLoaded, initializationAttempted;
        long nextPolicyRefresh;
        float nextAttempt;
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
        public bool Configured => CollectionContextAllowed && enrollment != null && queue != null && queue.BoundTo(EventQueue.Fingerprint(enrollment.endpoint, enrollment.token));
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
            try
            {
                string directory = PrivateDirectory();
                queue = new EventQueue(new FileEventQueueStore(Path.Combine(directory, "behavior-queue.xml")));
                string path = Path.Combine(directory, "personalization-enrollment.json");
                if (!File.Exists(path)) return;
                var candidate = JsonUtility.FromJson<Enrollment>(File.ReadAllText(path));
                if (candidate == null || !ValidEndpoint(candidate.endpoint) || candidate.token == null || candidate.token.Length < 32
                    || candidate.token.Length>512 || candidate.token.Any(c=>c<33 || c>126)) { status = "연결 설정 확인 필요"; return; }
                candidate.endpoint = candidate.endpoint.TrimEnd('/');
                queue.Bind(EventQueue.Fingerprint(candidate.endpoint, candidate.token));
                enrollment = candidate;
                status = queue.DeletePending ? "서버 기록 삭제 대기" : queue.Enabled ? "추천 개선에 참여 중" : "참여 안 함";
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
        static bool ValidEndpoint(string value)
        {
            Uri uri;
            if (!Uri.TryCreate(value, UriKind.Absolute, out uri) || uri.Query.Length > 0 || uri.Fragment.Length > 0
                || uri.UserInfo.Length > 0 || uri.AbsolutePath != "/") return false;
            return uri.Scheme == "https" || (Debug.isDebugBuild || Application.isEditor) && uri.Scheme == "http" && uri.IsLoopback;
        }
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
            if(Time.unscaledTime>=nextMeasurementSync) {nextMeasurementSync=Time.unscaledTime+3;SynchronizeMeasurementConsent();}
            if(!CollectionContextAllowed)return;
            if(!initializationAttempted)InitializeEnrollment();
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
            return request;
        }
        IEnumerator Synchronize()
        {
            busy = true;
            try
            {
                if (queue.DeletePending)
                {
                    using (var request = Request("DELETE", "/participant"))
                    {
                        yield return request.SendWebRequest();
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
                if (!queue.Enabled || !CollectionContextAllowed) { nextAttempt = 0; yield break; }
                BehaviorEvent[] sent;
                try { sent = queue.Batch(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()); }
                catch (Exception) { status = "참여 기록 확인 필요"; yield break; }
                if (sent.Length == 0) yield break;
                using (var request = Request("POST", "/events/batch", JsonUtility.ToJson(new BehaviorBatch { events=sent })))
                {
                    yield return request.SendWebRequest();
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
            finally { busy = false; }
        }
    }
}
