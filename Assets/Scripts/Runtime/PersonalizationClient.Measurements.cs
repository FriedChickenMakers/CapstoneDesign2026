using System;
using System.Collections;
using System.Linq;
using CapstoneDesign.Runtime.LocalState;
using CapstoneDesign.Runtime.Personalization;
using UnityEngine;
using UnityEngine.Networking;

namespace CapstoneDesign.Runtime
{
    public sealed partial class PersonalizationClient
    {
        [Serializable] sealed class MeasurementResult { public bool ok; }
        [Serializable] sealed class MeasurementId { public string event_id; }
        [Serializable] sealed class MeasurementUpload { public string collection_id; public MeasurementId[] events; }
        float nextMeasurementSync;
        string measurementSyncKey;
        public bool MeasurementConsented => Consented && queue.MeasurementConsentUtcMs>0;
        string MeasurementBinding => enrollment==null?"":EventQueue.Fingerprint(enrollment.endpoint,enrollment.token);
        static string MeasurementCall(string method,params object[] args)
        {
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
            string key=binding+":"+since;
            if(key==measurementSyncKey)return true;
            try
            {
                var result=JsonUtility.FromJson<MeasurementResult>(MeasurementCall("configureMeasurements",binding,enabled,since));
                if(result==null || !result.ok) {status="측정 등록 저장 확인 필요 · 다시 시도해 주세요";return false;}
                measurementSyncKey=key;return true;
            }
            catch(Exception) {return false;}
        }
        public void RecordMeasuredSession(ActivitySession session)
        {
            if(!MeasurementConsented || !DateTimeOffset.TryParse(session.SelectedUtc,out var selected)
                || selected.ToUnixTimeMilliseconds()<queue.MeasurementConsentUtcMs || !SynchronizeMeasurementConsent())return;
            RecordMeasuredActivity(session,"activity_selected",selected);
            if(DateTimeOffset.TryParse(session.StartedUtc,out var started))RecordMeasuredActivity(session,"activity_started",started);
            if((session.Status==ParticipationState.RewardCommitted || session.Status==ParticipationState.Ended)
                && DateTimeOffset.TryParse(session.EndedUtc,out var ended))
                RecordMeasuredActivity(session,session.Status==ParticipationState.RewardCommitted?"activity_completed":"activity_ended",ended);
        }
        void RecordMeasuredActivity(ActivitySession session,string kind,DateTimeOffset at)
        {
            // Catalog ID and a hashed session key only; never mood, note or health records.
            MeasurementCall("recordMeasuredActivity",kind,EventQueue.Fingerprint(session.SessionId,"measurement").Substring(0,32),
                session.MissionId,at.ToUnixTimeMilliseconds(),(int)at.Offset.TotalMinutes);
        }
        IEnumerator SynchronizeMeasurements()
        {
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
