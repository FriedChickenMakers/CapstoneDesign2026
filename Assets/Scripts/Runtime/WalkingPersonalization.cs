using System;
using System.Globalization;
using System.Linq;
using CapstoneDesign.Runtime.LocalState;

namespace CapstoneDesign.Runtime
{
    /// <summary>Product goal tuning, not a clinical sleep assessment or exercise prescription.</summary>
    public static class WalkingPersonalization
    {
        public const int DefaultGoal = 5000;
        public const int MinimumBaselineDays = 5;
        public const string AggregateSource = "HEALTH_CONNECT_AGGREGATE";

        public static WalkingTarget Evaluate(WalkingBaselineSnapshot snapshot, DateTimeOffset now, TimeZoneInfo zone)
        {
            if(zone==null)throw new ArgumentNullException("zone");
            var date=TimeZoneInfo.ConvertTime(now,zone).Date;
            var from=LocalBoundary(date,zone);var until=LocalBoundary(date.AddDays(1),zone);
            var result=new WalkingTarget {GoalSteps=DefaultGoal,QueryStartUtc=from.ToString("O"),
                QueryEndUtc=until.ToString("O"),Basis="DEFAULT"};
            long nowMs=now.ToUnixTimeMilliseconds();
            // A cached result from before today's boundary cannot include yesterday's complete day.
            bool fresh=snapshot!=null && snapshot.queryComplete && snapshot.status=="AVAILABLE" &&
                snapshot.lastUpdatedEpochMs>=from.ToUnixTimeMilliseconds() && snapshot.lastUpdatedEpochMs<=nowMs &&
                nowMs-snapshot.lastUpdatedEpochMs<=TimeSpan.FromHours(6).TotalMilliseconds &&
                snapshot.queryEndEpochMs==from.ToUnixTimeMilliseconds();
            if(fresh)
            {
                // Missing buckets are not zero-step days. Reject duplicates, current-day values,
                // impossible counts and out-of-window samples before choosing a stable median.
                var days=(snapshot.days??Array.Empty<DailyStepBaseline>()).Where(x=>x!=null && x.hasValue &&
                    x.count>=0 && x.count<=100000 && x.endEpochMs<=from.ToUnixTimeMilliseconds() &&
                    x.endEpochMs>LocalBoundary(date.AddDays(-14),zone).ToUnixTimeMilliseconds())
                    .GroupBy(x=>x.endEpochMs).Where(g=>g.Count()==1).Select(g=>g.First()).ToArray();
                if(days.Length>=MinimumBaselineDays && days.Max(x=>x.endEpochMs)>=LocalBoundary(date.AddDays(-2),zone).ToUnixTimeMilliseconds())
                {
                    var counts=days.Select(x=>x.count).OrderBy(x=>x).ToArray();
                    double median=counts.Length%2==1?counts[counts.Length/2]:(counts[counts.Length/2-1]+counts[counts.Length/2])/2.0;
                    result.GoalSteps=(int)Math.Max(5000,Math.Min(10000,Math.Round(median/500.0,MidpointRounding.AwayFromZero)*500));
                    result.UsedBaseline=true;result.Basis="ACTIVITY_BASELINE";
                }
            }
            var sleep=snapshot?.sleep;
            // Sleep duration is a deduplicated recorded session total. No sleep record never means
            // 'zero hours'. Old, partial, denied or implausible records cannot alter intensity.
            bool usableSleep=fresh && sleep!=null && sleep.status=="AVAILABLE" && sleep.hasValue && sleep.queryComplete &&
                sleep.measuredAtEpochMs>0 && sleep.measuredAtEpochMs<=nowMs &&
                nowMs-sleep.measuredAtEpochMs<=TimeSpan.FromHours(36).TotalMilliseconds &&
                sleep.value>=60 && sleep.value<=960;
            if(usableSleep && sleep.value<360)
            {
                result.GoalSteps=(int)(Math.Floor(result.GoalSteps*.8/500)*500);
                result.ReducedForSleep=true;result.Basis+="_SHORT_SLEEP";
            }
            return result;
        }

        // Midnight offsets may change with daylight saving; never assume every day is 24 hours.
        static DateTimeOffset LocalBoundary(DateTime date,TimeZoneInfo zone)
        {
            var local=DateTime.SpecifyKind(date,DateTimeKind.Unspecified);
            while(zone.IsInvalidTime(local))local=local.AddMinutes(1);
            return new DateTimeOffset(local,zone.GetUtcOffset(local)).ToUniversalTime();
        }
        public static int TargetFor(ActivitySession session) => session!=null && session.WalkingGoalSteps>0?session.WalkingGoalSteps:300;
        public static DateTimeOffset QueryStartFor(ActivitySession session) => DateTimeOffset.Parse(
            session.WalkingGoalSteps>0?session.WalkingQueryStartUtc:session.StartedUtc,CultureInfo.InvariantCulture);
        public static DateTimeOffset QueryEndFor(ActivitySession session,DateTimeOffset now)
        {
            if(session.WalkingGoalSteps<=0)return now;
            var end=DateTimeOffset.Parse(session.WalkingQueryEndUtc,CultureInfo.InvariantCulture);
            return now<end?now:end;
        }
        public static string Explanation(WalkingTarget target)
        {
            if(target==null)return "건강 기록이 충분하지 않아 기본 목표로 시작해요.";
            string basis=target.UsedBaseline?"최근 14일의 평소 걸음 수를 반영했어요.":"건강 기록이 충분하지 않아 기본 목표로 시작해요.";
            return target.ReducedForSleep?basis+" 최근 수면 기록이 짧아 목표를 낮췄어요.":basis;
        }
        public static string Explanation(ActivitySession session) => session==null || session.WalkingGoalSteps==0
            ?"이전에 시작한 활동의 300걸음 목표를 유지해요."
            :Explanation(new WalkingTarget {UsedBaseline=(session.WalkingTargetBasis??"").StartsWith("ACTIVITY_BASELINE",StringComparison.Ordinal),ReducedForSleep=session.WalkingSleepReduced});
    }
}
