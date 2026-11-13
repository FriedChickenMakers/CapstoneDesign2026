using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using CapstoneDesign.Runtime.LocalState;
using CapstoneDesign.Runtime.Personalization;
using UnityEngine;
using UnityEngine.Networking;

namespace CapstoneDesign.Runtime
{
    public sealed partial class PersonalizationClient
    {
        [Serializable] sealed class MeasurementResult { public bool ok; public string failure; }
        [Serializable] sealed class MeasurementId { public string event_id; }
        [Serializable] sealed class MeasurementUpload { public string collection_id; public MeasurementId[] events; }
        [Serializable] sealed class AppVisitObservation { public string visit_key; public bool foreground; }
        float nextMeasurementSync;
        string measurementSyncKey;
        string appVisitKey = Guid.NewGuid().ToString("N"), measuredAppVisit, rejectedAppVisit;
        double appPausedAt = -1;

        void OnApplicationPause(bool paused)
        {
            if (paused) { if(appPausedAt<0)appPausedAt = Time.realtimeSinceStartupAsDouble; return; }
            ResumeMeasuredAppVisit(Time.realtimeSinceStartupAsDouble);
        }

        void ResumeMeasuredAppVisit(double now)
        {
            if (appPausedAt >= 0 && now - appPausedAt >= 30)
                appVisitKey = Guid.NewGuid().ToString("N");
            appPausedAt = -1;
            // Resume promptly after connectivity/Doze; never discard an unsent batch.
            nextAttempt = nextMeasurementSync = 0;
        }

        void RecordMeasuredAppVisit()
        {
            if (!MeasurementConsented || !CollectionContextAllowed) return;
#if UNITY_ANDROID && !UNITY_EDITOR
            // GameActivity HOT returns must not rely on Unity pause callbacks.
            // Android Activity START/STOP is also unaffected by keyboard focus.
            AppVisitObservation visit;
            try { visit=JsonUtility.FromJson<AppVisitObservation>(MeasurementCall("getMeasuredAppVisit")); }
            catch(Exception) { return; }
            if(visit==null || !visit.foreground || visit.visit_key==null || visit.visit_key.Length!=32
                || visit.visit_key.Any(c=>!(c>='a' && c<='f') && !(c>='0' && c<='9')))return;
            appVisitKey=visit.visit_key;
#else
            if(appPausedAt>=0)return;
#endif
            string identity = MeasurementBinding + ":" + queue.MeasurementConsentUtcMs + ":" + appVisitKey;
            if (identity == measuredAppVisit || identity == rejectedAppVisit) return;
            try
            {
                var result = JsonUtility.FromJson<MeasurementResult>(MeasurementCall("recordMeasuredAppSession", appVisitKey));
                if (result?.ok == true) measuredAppVisit = identity;
                // A full outbox has already counted this rejected attempt. Do not
                // inflate its counter or rewrite storage every three seconds.
                // A fresh foreground visit/collection receives a different key;
                // transient storage/bridge failures still retry this same visit.
                else if (result?.failure == "capacity" || result?.failure == "invalid" || result?.failure == "outside-window")
                    rejectedAppVisit = identity;
            }
            catch (Exception) { /* Keep the same random key for a later durable retry. */ }
        }
        sealed class PendingFocusExposure
        {
            public string Binding; public FocusRecommendationExposure Exposure;
            public int Attempts; public float NextAttempt; public bool Terminal;
        }
        readonly Dictionary<string,PendingFocusExposure> pendingFocusExposures = new Dictionary<string,PendingFocusExposure>();
#if UNITY_EDITOR
        // Editor-only fault injection for the real client lifecycle validation.
        static Func<string,object[],string> measurementCallOverride;
#endif
        public bool MeasurementConsented => Consented && queue.MeasurementConsentUtcMs>0;
        string MeasurementBinding => enrollment==null?"":EventQueue.Fingerprint(enrollment.endpoint,enrollment.token);
        static string MeasurementCall(string method,params object[] args)
        {
#if UNITY_EDITOR
            if(measurementCallOverride!=null)return measurementCallOverride(method,args);
#endif
#if UNITY_ANDROID && !UNITY_EDITOR
            try { using(var bridge=new AndroidJavaClass("com.capstonedesign2026.platform.AndroidPlatformBridge"))return bridge.CallStatic<string>(method,args); }
            catch(Exception) {return "{}";}
#else
            return "{\"ok\":true}"; // Editor cannot invent native measurements.
#endif
        }
        bool SynchronizeMeasurementConsent(bool forceDisable=false)
        {
            bool enabled=!forceDisable && MeasurementConsented && CollectionContextAllowed;
            string binding=enabled?MeasurementBinding:"";
            long since=enabled?queue.MeasurementConsentUtcMs:0;
            // Failed/unavailable enrollment is not withdrawal. Stop collection in
            // memory while preserving the durable outbox for the same registration.
            bool suspend=!forceDisable && !enabled;
            string key=suspend?"suspended":forceDisable?"withdrawn":binding+":"+since;
            if(key==measurementSyncKey)return true;
            try
            {
                var result=JsonUtility.FromJson<MeasurementResult>(suspend?MeasurementCall("suspendMeasurements"):
                    MeasurementCall("configureMeasurements",binding,enabled,since));
                if(result==null || !result.ok) {status="측정 등록 저장 확인 필요 · 다시 시도해 주세요";return false;}
                if(forceDisable)pendingFocusExposures.Clear();
                measurementSyncKey=key;return true;
            }
            catch(Exception) {return false;}
        }
        public void RecordMeasuredSession(ActivitySession session)
        {
            if(!MeasurementConsented || !DateTimeOffset.TryParse(session.SelectedUtc,out var selected)
                || selected.ToUnixTimeMilliseconds()<queue.MeasurementConsentUtcMs || !SynchronizeMeasurementConsent())return;
            RecordMeasuredActivity(session,"activity_selected",selected);
            RecordMeasuredFocusSession(session);
            if(DateTimeOffset.TryParse(session.StartedUtc,out var started))RecordMeasuredActivity(session,"activity_started",started);
            if((session.Status==ParticipationState.RewardCommitted || session.Status==ParticipationState.Ended)
                && DateTimeOffset.TryParse(session.EndedUtc,out var ended))
                RecordMeasuredActivity(session,session.Status==ParticipationState.RewardCommitted?"activity_completed":"activity_ended",ended);
        }
        public bool RecordMeasuredFocusExposure(FocusRecommendationExposure exposure)
        {
            if(exposure==null || !MeasurementConsented || !CollectionContextAllowed
                || !DateTimeOffset.TryParse(exposure.ShownUtc,out var shown)
                || shown.ToUnixTimeMilliseconds()<queue.MeasurementConsentUtcMs)return false;
            if(pendingFocusExposures.TryGetValue(exposure.DecisionId,out var pending))
            {
                if(pending.Binding!=MeasurementBinding)pendingFocusExposures.Remove(exposure.DecisionId);
                else if(pending.Terminal || Time.unscaledTime<pending.NextAttempt)return false;
                // Keep the first observed timestamp across rerenders/caller mutation.
                else {exposure=pending.Exposure;shown=DateTimeOffset.Parse(exposure.ShownUtc);}
            }
            else if(pendingFocusExposures.Count>=1024)return false;
            var result=SynchronizeMeasurementConsent()?
                RecordMeasuredFocus(exposure,"focus_recommendation_shown","",exposure.MissionId,shown):null;
            if(result?.ok==true) { pendingFocusExposures.Remove(exposure.DecisionId); return true; }
            if(pending==null || pending.Binding!=MeasurementBinding)
                pending=new PendingFocusExposure {Binding=MeasurementBinding,Exposure=exposure.Copy()};
            pending.Attempts++;
            // Capacity/validation rejection cannot recover through a rapid retry.
            // Retain a bounded tombstone so rerenders/session replay do not inflate
            // native dropped-attempt counters. Other failures back off, at most 12 attempts.
            pending.Terminal=result?.failure=="capacity" || result?.failure=="invalid"
                || result?.failure=="outside-window" || pending.Attempts>=12;
            pending.NextAttempt=Time.unscaledTime+Math.Min(300,3*(1<<Math.Min(pending.Attempts-1,7)));
            pendingFocusExposures[exposure.DecisionId]=pending;
            return false;
        }
        void RetryMeasuredFocusExposures()
        {
            if(!MeasurementConsented || !CollectionContextAllowed)return;
            int attempted=0;
            foreach(var item in pendingFocusExposures.ToArray())
            {
                if(item.Value.Binding!=MeasurementBinding || !DateTimeOffset.TryParse(item.Value.Exposure.ShownUtc,out var shown)
                    || shown.ToUnixTimeMilliseconds()<queue.MeasurementConsentUtcMs || shown<DateTimeOffset.UtcNow.AddDays(-90))
                {pendingFocusExposures.Remove(item.Key);continue;}
                if(item.Value.Terminal || Time.unscaledTime<item.Value.NextAttempt)continue;
                if(attempted++>=8)break; // Bound main-thread JNI/disk work per update.
                RecordMeasuredFocusExposure(item.Value.Exposure);
            }
        }
        void RecordMeasuredFocusSession(ActivitySession session)
        {
            var exposure=session.FocusRecommendationExposure;
            // This snapshot was stored only after an actual visible card. Replaying
            // that exact evidence is safe; never substitute selection/start time.
            if(exposure==null || !DateTimeOffset.TryParse(exposure.ShownUtc,out var shown)
                || shown.ToUnixTimeMilliseconds()<queue.MeasurementConsentUtcMs)return;
            RecordMeasuredFocusExposure(exposure);
            string key=EventQueue.Fingerprint(session.SessionId,"measurement").Substring(0,32);
            if(DateTimeOffset.TryParse(session.SelectedUtc,out var selected))RecordMeasuredFocus(exposure,"focus_recommendation_selected",key,session.MissionId,selected);
            if(DateTimeOffset.TryParse(session.StartedUtc,out var started))RecordMeasuredFocus(exposure,"focus_recommendation_started",key,session.MissionId,started);
            if((session.Status==ParticipationState.RewardCommitted || session.Status==ParticipationState.Ended)
                && DateTimeOffset.TryParse(session.EndedUtc,out var ended))
                RecordMeasuredFocus(exposure,session.Status==ParticipationState.RewardCommitted?"focus_recommendation_completed":"focus_recommendation_ended",key,session.MissionId,ended);
        }
        MeasurementResult RecordMeasuredFocus(FocusRecommendationExposure exposure,string kind,string session,string actual,DateTimeOffset at)
        {
            try
            {
                return JsonUtility.FromJson<MeasurementResult>(MeasurementCall("recordMeasuredFocusRecommendation",kind,exposure.DecisionId,session,exposure.MissionId,actual,
                    exposure.PolicyId,exposure.Reason,at.ToUnixTimeMilliseconds(),(int)at.Offset.TotalMinutes));
            }
            catch(Exception) {return null;}
        }
        void RecordMeasuredActivity(ActivitySession session,string kind,DateTimeOffset at)
        {
            // Catalog ID and a hashed session key only; never mood, note or health records.
            MeasurementCall("recordMeasuredActivity",kind,EventQueue.Fingerprint(session.SessionId,"measurement").Substring(0,32),
                session.MissionId,at.ToUnixTimeMilliseconds(),(int)at.Offset.TotalMinutes);
        }
        IEnumerator SynchronizeMeasurements()
        {
            int generation=localResetGeneration;
            if(!MeasurementConsented || !SynchronizeMeasurementConsent())yield break;
            string binding=MeasurementBinding;
            string body=MeasurementCall("getMeasurementBatch",binding);
            MeasurementUpload batch;
            try {batch=JsonUtility.FromJson<MeasurementUpload>(body);}
            catch(Exception) {yield break;}
            // Empty batches still report loss/expiry counters, so missing clicks are not
            // silently treated as nonresponse when collection was incomplete.
            if(batch?.events==null || string.IsNullOrEmpty(batch.collection_id))yield break;
            using(var request=Request("POST","/measurements/batch",body))
            {
                yield return request.SendWebRequest();
                if(generation!=localResetGeneration || localResetBlocked)yield break;
                if(!MeasurementConsented || binding!=MeasurementBinding)yield break;
                if(request.result!=UnityWebRequest.Result.Success) {status="알림·활동 측정 전송 대기";yield break;}
                try
                {
                    var ack=JsonUtility.FromJson<BehaviorAcknowledgement>(request.downloadHandler.text);
                    var sent=batch.events.Select(e=>e.event_id).ToArray();
                    if(ack?.accepted_event_ids==null || ack.accepted_event_ids.Except(sent).Any())throw new InvalidOperationException();
                    Func<string[],string> ids=values=>"["+string.Join(",",values.Select(v=>"\""+v+"\""))+ "]";
                    var result=JsonUtility.FromJson<MeasurementResult>(MeasurementCall("acknowledgeMeasurements",binding,batch.collection_id,ids(ack.accepted_event_ids),ids(sent)));
                    if(result==null || !result.ok)throw new InvalidOperationException();
                }
                catch(Exception) {status="측정 전송 확인 저장 대기";}
            }
        }
    }
}
