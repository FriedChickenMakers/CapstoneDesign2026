using System;
using System.Globalization;
using System.Linq;
using CapstoneDesign.Runtime.LocalState;

namespace CapstoneDesign.Runtime
{
    public sealed class ReminderSyncCache
    {
        string successfulSignature;
        public void Invalidate() { successfulSignature=null; }
        public bool Sync(string signature,Func<PlatformActionResult> send)
        {
            if(signature==successfulSignature)return true;
            PlatformActionResult result;
            try { result=send(); } catch { return false; }
            if(result?.ParsedStatus!=PlatformStatus.Available)return false;
            successfulSignature=signature;
            return true;
        }
    }

    public static class ActivityNotificationLogic
    {
        public static string CompletedCalendarDay(GardenState state,DateTimeOffset now,TimeZoneInfo zone)
        {
            var today=TimeZoneInfo.ConvertTime(now,zone).Date;
            return state.Sessions.Any(s=>s.Status==ParticipationState.RewardCommitted &&
                DateTimeOffset.TryParse(s.EndedUtc,out var ended) && ended<=now &&
                TimeZoneInfo.ConvertTime(ended,zone).Date==today)?today.ToString("yyyy-MM-dd",CultureInfo.InvariantCulture):"";
        }
    }

    public static class WalkingEvidence
    {
        static bool Active(ActivitySession session) => session!=null && session.MissionId=="free-mission:M15" &&
            (session.Status==ParticipationState.InProgress || session.Status==ParticipationState.Paused);
        static bool AggregateSource(string source) => source==WalkingPersonalization.AggregateSource || source=="APP_VERIFIED_HEALTH_CONNECT";
        public static bool CanAutomaticallyComplete(ActivitySession session,bool debug) => !debug && Active(session) &&
            session.RealStepHighWater>=WalkingPersonalization.TargetFor(session) && !string.IsNullOrEmpty(session.StepSource) &&
            session.StepSource!="DEBUG_VIRTUAL" && (session.WalkingGoalSteps==0 || AggregateSource(session.StepSource));

        public static bool AcceptsAchievement(ActivitySession session,WalkingAchievement receipt,bool debug,DateTimeOffset now)
        {
            if(debug || !Active(session) || receipt==null || receipt.sessionId!=session.SessionId || !receipt.queryComplete ||
                receipt.steps<WalkingPersonalization.TargetFor(session) || receipt.source=="DEBUG_VIRTUAL" ||
                string.IsNullOrEmpty(receipt.source) || receipt.achievedAtEpochMs<=0 || receipt.achievedAtEpochMs>now.ToUnixTimeMilliseconds()+5000)
                return false;
            if(session.WalkingGoalSteps>0 ? !AggregateSource(receipt.source)
                : receipt.source!="HEALTH_CONNECT_PREFERRED" && receipt.source!="APP_VERIFIED_HEALTH_CONNECT")return false;
            if(!DateTimeOffset.TryParse(session.StartedUtc,out var started) || receipt.achievedAtEpochMs<started.ToUnixTimeMilliseconds())return false;
            long expectedEnd=session.WalkingGoalSteps>0?DateTimeOffset.Parse(session.WalkingQueryEndUtc,CultureInfo.InvariantCulture).ToUnixTimeMilliseconds():long.MaxValue;
            return receipt.startEpochMs==WalkingPersonalization.QueryStartFor(session).ToUnixTimeMilliseconds() &&
                receipt.endEpochMs==expectedEnd && receipt.goalSteps==WalkingPersonalization.TargetFor(session);
        }

        public static bool MatchesQuery(string requestId,long start,long end,StepRangeSnapshot result,DateTimeOffset now) =>
            result!=null && result.requestId==requestId && result.queryStartEpochMs==start && result.queryEndEpochMs==end &&
            !result.refreshing && result.queryComplete && result.hasValue && result.status=="AVAILABLE" && result.count>=0 &&
            !string.IsNullOrEmpty(result.source) && end>start && end<=now.ToUnixTimeMilliseconds()+5000 &&
            result.lastUpdatedEpochMs>=end && result.lastUpdatedEpochMs<=now.ToUnixTimeMilliseconds()+5000;

        public static bool AcceptsMissionQuery(ActivitySession session,bool debug,string requestId,long start,long end,bool aggregate,
            StepRangeSnapshot result,DateTimeOffset now)
        {
            if(!Active(session) || !MatchesQuery(requestId,start,end,result,now))return false;
            var expectedStart=debug?DateTimeOffset.Parse(session.StartedUtc,CultureInfo.InvariantCulture):WalkingPersonalization.QueryStartFor(session);
            if(start!=expectedStart.ToUnixTimeMilliseconds() || aggregate!=(!debug && session.WalkingGoalSteps>0))return false;
            if(!debug && session.WalkingGoalSteps>0 &&
                (end>DateTimeOffset.Parse(session.WalkingQueryEndUtc,CultureInfo.InvariantCulture).ToUnixTimeMilliseconds() ||
                 result.source!=WalkingPersonalization.AggregateSource))return false;
            return true;
        }
    }
}
