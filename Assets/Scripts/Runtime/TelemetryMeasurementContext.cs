using System;
using System.Linq;
using CapstoneDesign.Runtime.LocalState;
using UnityEngine;

namespace CapstoneDesign.Runtime
{
    public static class TelemetryMeasurementContext
    {
        [Serializable] public sealed class Context
        {
            public int version=2;
            public bool classified,qa,debug;
            public string visit_id="",source_notification_id="",build_id="unknown";
            public int os_api;
        }
        [Serializable] sealed class BuildMetadata { public string source_id; }
        static string buildId;
        public static string BuildId
        {
            get
            {
                if(buildId!=null)return buildId;
                var asset=Resources.Load<TextAsset>("TelemetryBuildMetadata");
                buildId=asset==null?"unknown":JsonUtility.FromJson<BuildMetadata>(asset.text)?.source_id??"unknown";
                return buildId;
            }
        }
        public static Context Current()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                using(var bridge=new AndroidJavaClass("com.capstonedesign2026.platform.AndroidPlatformBridge"))
                    return JsonUtility.FromJson<Context>(bridge.CallStatic<string>("getMeasurementContext"))??new Context();
            }
            catch(Exception){return new Context();}
#else
            return new Context{classified=true,qa=DebugAccess.Qa,debug=DebugAccess.Enabled,build_id=BuildId};
#endif
        }
        public static MeasurementStamp Capture(bool demo=false)=>new MeasurementStamp {Metadata=JsonUtility.ToJson(Current()),EntrySource=demo?"demo":"direct",RecordedUtcMs=DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()};
        public static string Frozen(MeasurementStamp stamp)=>stamp?.Metadata??JsonUtility.ToJson(new Context());
        [Serializable] public sealed class ActivityDetails
        {
            public int content_version,active_seconds,recommended_seconds,step_index=-1;
            public string role="legacy",entry_source="legacy",completion_mode="none",end_reason="none";
        }
        public static ActivityDetails Details(ActivitySession session,MeasurementStamp stamp,string kind,FocusStepRecord step=null)
        {
            var records=step==null?session.FocusStepRecords:new System.Collections.Generic.List<FocusStepRecord>{step};
            bool finished=kind=="activity_completed"||kind=="activity_ended"||kind=="practice_step";
            bool shortened=records?.Any(r=>r.EndReason=="too_long")==true;
            return new ActivityDetails {content_version=session.FocusVersion,
                role=FocusMissionPolicy.RoleFor(session)==FocusMissionCatalog.BasicRole?"basic":FocusMissionPolicy.RoleFor(session)==FocusMissionCatalog.AdditionalRole?"additional":"legacy",
                entry_source=stamp?.EntrySource??"legacy",step_index=step?.Step??-1,
                active_seconds=finished?records?.Sum(r=>r.ElapsedSeconds)??0:0,
                recommended_seconds=finished?records?.Sum(r=>r.RecommendedSeconds)??0:0,
                completion_mode=kind=="activity_ended"?"ended":finished?(shortened?"too_long":"normal"):"none",
                end_reason=shortened?"too_long":kind=="activity_ended"?"deferred":step?.EndReason=="not_applicable"?"not_applicable":"none"};
        }
    }
}
