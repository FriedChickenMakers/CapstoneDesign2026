using System;
using System.IO;
using System.Linq;
using CapstoneDesign.Runtime;
using CapstoneDesign.Runtime.LocalState;

class WalkingTests
{
    static int checks;
    static void Check(bool ok,string message){checks++;if(!ok)throw new Exception(message);}
    static DateTimeOffset Now=DateTimeOffset.Parse("2026-10-12T12:00:00+09:00");
    static TimeZoneInfo Zone=TimeZoneInfo.CreateCustomTimeZone("Synthetic/Korea",TimeSpan.FromHours(9),"Synthetic/Korea","Synthetic/Korea");
    static WalkingBaselineSnapshot Baseline(params long[] counts)
    {
        var midnight=DateTimeOffset.Parse("2026-10-12T00:00:00+09:00");
        return new WalkingBaselineSnapshot {status="AVAILABLE",queryComplete=true,lastUpdatedEpochMs=Now.ToUnixTimeMilliseconds(),
            queryEndEpochMs=midnight.ToUnixTimeMilliseconds(),days=counts.Select((n,i)=>new DailyStepBaseline {
                count=Math.Max(0,n),hasValue=n>=0,endEpochMs=midnight.AddDays(-i).ToUnixTimeMilliseconds()}).ToArray()};
    }
    static WalkingTarget Evaluate(WalkingBaselineSnapshot data)=>WalkingPersonalization.Evaluate(data,Now,Zone);
    static void Main()
    {
        Check(Evaluate(null).GoalSteps==5000,"missing snapshot uses a gentle default");
        var low=Baseline(2100,2900,2600,2300,2800);
        Check(Evaluate(low).GoalSteps==5000 && Evaluate(low).UsedBaseline,"low baseline gets 5000");
        Check(Evaluate(Baseline(11000,10500,13000,16000,10000)).GoalSteps==10000,"active user capped at 10000");
        Check(Evaluate(Baseline(6200,6400,6800,7300,7500)).GoalSteps==7000,"middle activity gets rounded median");
        Check(Evaluate(Baseline(7000,7000,7000,7000,99999)).GoalSteps==7000,"outlier does not dominate median");
        Check(!Evaluate(Baseline(9000,9000,9000,9000)).UsedBaseline,"at least five observed days");
        Check(!Evaluate(Baseline(9000,9000,9000,9000,-1,-1,-1)).UsedBaseline,"missing days do not count as zero");
        Check(Evaluate(Baseline(0,0,0,0,0)).UsedBaseline,"recorded zero is valid unlike missing");
        var stale=Baseline(10000,10000,10000,10000,10000);stale.lastUpdatedEpochMs-=7*3600000;
        Check(!Evaluate(stale).UsedBaseline,"query older than six hours is not fresh");
        stale.lastUpdatedEpochMs=Now.AddDays(1).ToUnixTimeMilliseconds();Check(!Evaluate(stale).UsedBaseline,"future query rejected");
        var partial=Baseline(10000,10000,10000,10000,10000);partial.queryComplete=false;
        Check(!Evaluate(partial).UsedBaseline,"partial query cannot tune goal");
        partial.queryComplete=true;partial.status="PERMISSION_DENIED";Check(!Evaluate(partial).UsedBaseline,"denied query uses default");
        var noRecent=Baseline(10000,10000,10000,10000,10000);foreach(var day in noRecent.days)day.endEpochMs-=4*86400000L;
        Check(!Evaluate(noRecent).UsedBaseline,"old valid days do not pretend activity is recent");
        var duplicates=Baseline(10000,10000,10000,10000,10000);duplicates.days[4].endEpochMs=duplicates.days[0].endEpochMs;
        Check(!Evaluate(duplicates).UsedBaseline,"duplicate daily buckets cannot fill baseline");
        var sleep=Baseline(10000,10000,10000,10000,10000);
        sleep.sleep=new HealthMetricSnapshot {status="AVAILABLE",hasValue=true,queryComplete=true,value=300,
            measuredAtEpochMs=Now.AddHours(-4).ToUnixTimeMilliseconds()};
        Check(Evaluate(sleep).GoalSteps==8000 && Evaluate(sleep).ReducedForSleep,"short recorded sleep reduces goal 20 percent");
        sleep.sleep.value=360;Check(Evaluate(sleep).GoalSteps==10000,"six hours is not reduced");
        sleep.sleep.value=0;Check(!Evaluate(sleep).ReducedForSleep,"invalid zero sleep not treated as no sleep");
        sleep.sleep.value=300;sleep.sleep.hasValue=false;Check(!Evaluate(sleep).ReducedForSleep,"absent sleep is not short sleep");
        sleep.sleep.hasValue=true;sleep.sleep.status="PARTIAL";Check(!Evaluate(sleep).ReducedForSleep,"partial sleep cannot reduce goal");
        sleep.sleep.status="AVAILABLE";sleep.sleep.measuredAtEpochMs=Now.AddHours(-37).ToUnixTimeMilliseconds();
        Check(!Evaluate(sleep).ReducedForSleep,"stale sleep cannot change goal");
        sleep.sleep.measuredAtEpochMs=Now.AddHours(1).ToUnixTimeMilliseconds();Check(!Evaluate(sleep).ReducedForSleep,"future sleep rejected");
        var target=Evaluate(low);
        Check(DateTimeOffset.Parse(target.QueryStartUtc)==DateTimeOffset.Parse("2026-10-12T00:00:00+09:00"),"today includes pre-start steps");
        var ny=TimeZoneInfo.FindSystemTimeZoneById("America/New_York");
        var spring=WalkingPersonalization.Evaluate(null,DateTimeOffset.Parse("2026-03-08T12:00:00-04:00"),ny);
        var fall=WalkingPersonalization.Evaluate(null,DateTimeOffset.Parse("2026-11-01T12:00:00-05:00"),ny);
        Check((DateTimeOffset.Parse(spring.QueryEndUtc)-DateTimeOffset.Parse(spring.QueryStartUtc)).TotalHours==23,"DST spring day is 23 hours");
        Check((DateTimeOffset.Parse(fall.QueryEndUtc)-DateTimeOffset.Parse(fall.QueryStartUtc)).TotalHours==25,"DST fall day is 25 hours");

        var store=new MemoryStore();var clock=new TestClock();var service=new GardenStateService(store,clock,new DemoBalanceConfig());
        Check(service.Open() && service.RefreshCycle(new string[0]),"open garden");
        Check(service.BeginSession("synthetic-walk","free-mission:M15") && service.StartWalkingSession("synthetic-walk",target),"start personalized activity");
        target.GoalSteps=10000;Check(service.Snapshot.Sessions[0].WalkingGoalSteps==5000,"target detached from caller");
        Check(service.StartWalkingSession("synthetic-walk",target) && service.Snapshot.Sessions[0].WalkingGoalSteps==5000,"active target never changes");
        Check(WalkingPersonalization.QueryStartFor(service.Snapshot.Sessions[0])<DateTimeOffset.Parse(service.Snapshot.Sessions[0].StartedUtc),"query includes steps before starting");
        Check(WalkingPersonalization.QueryEndFor(service.Snapshot.Sessions[0],Now.AddDays(2))==DateTimeOffset.Parse("2026-10-13T00:00:00+09:00"),"next-day steps excluded from frozen target");
        service.ObserveMissionSteps("synthetic-walk",4999,WalkingPersonalization.AggregateSource);
        Check(!service.TryCompleteWalkingGoal("synthetic-walk") && service.Snapshot.Nutrient==0,"below target not completed");
        service.ObserveMissionSteps("synthetic-walk",5000,WalkingPersonalization.AggregateSource);
        store.Fail=true;Check(!service.TryCompleteWalkingGoal("synthetic-walk") && service.Snapshot.Nutrient==0,"failed save never commits reward");
        store.Fail=false;Check(service.TryCompleteWalkingGoal("synthetic-walk") && service.Snapshot.Nutrient==5,"observed target auto-completes and rewards");
        Check(service.Snapshot.Sessions[0].Status==ParticipationState.RewardCommitted,"automatic completion is durable session result");
        Check(service.TryCompleteWalkingGoal("synthetic-walk") && service.Snapshot.Nutrient==5,"duplicate automatic completion idempotent");
        service=new GardenStateService(store,clock,new DemoBalanceConfig());Check(service.Open(),"reopen");
        Check(service.Snapshot.Sessions[0].WalkingGoalSteps==5000 && service.TryCompleteWalkingGoal("synthetic-walk") && service.Snapshot.Nutrient==5,"target and receipt survive restart");
        Check(service.BeginSession("repeat","free-mission:M15") && service.StartWalkingSession("repeat",Evaluate(low)),"second walk same cycle");
        service.ObserveMissionSteps("repeat",7000,WalkingPersonalization.AggregateSource);service.TryCompleteWalkingGoal("repeat");
        Check(service.Snapshot.Nutrient==5,"same daily mission cannot reward twice");
        Check(service.BeginSession("legacy","free-mission:M15") && service.StartSession("legacy"),"legacy session starts through old contract");
        var legacy=service.Snapshot.Sessions.Last();
        Check(WalkingPersonalization.TargetFor(legacy)==300 && WalkingPersonalization.QueryStartFor(legacy)==DateTimeOffset.Parse(legacy.StartedUtc),"legacy keeps old goal and interval");
        service.ObserveMissionSteps("legacy",299);Check(!service.TryCompleteWalkingGoal("legacy"),"legacy threshold preserved");
        service.ObserveMissionSteps("legacy",300);Check(service.TryCompleteWalkingGoal("legacy"),"legacy completes at 300");
        var encoded=StateCodec.Encode(service.Snapshot);var decoded=StateCodec.Decode(encoded);
        Check(decoded.Sessions[0].WalkingGoalSteps==5000 && decoded.Sessions.Last().WalkingGoalSteps==0,"serialization preserves both generations");
        var invalid=Evaluate(low);invalid.GoalSteps=50001;
        Check(service.BeginSession("invalid","free-mission:M15") && !service.StartWalkingSession("invalid",invalid),"invalid goal rejected atomically");
        Check(service.Snapshot.Sessions.Last().Status==ParticipationState.Selected,"invalid start did not modify state");
        int selectionsBefore=service.Snapshot.Sessions.Count;
        int nutrientsBefore=service.Snapshot.Nutrient;
        store.Fail=true;Check(!service.CancelSelection("invalid") && service.Snapshot.Sessions.Count==selectionsBefore,"failed cancellation preserves selected activity");
        store.Fail=false;Check(service.CancelSelection("invalid") && service.Snapshot.Sessions.Count==selectionsBefore-1,"unstarted selection can be discarded");
        Check(service.Snapshot.Nutrient==nutrientsBefore,"selection cancellation grants no reward");
        service=new GardenStateService(store,clock,new DemoBalanceConfig());Check(service.Open() && !service.Snapshot.Sessions.Any(x=>x.SessionId=="invalid"),"selection cancellation survives reload");
        Check(service.BeginSession("different","free-mission:M01"),"discarding introduction unlocks another activity choice");
        Check(service.StartSession("different") && !service.CancelSelection("different"),"started participation cannot be silently discarded");
        Check(service.PauseSession("different") && !service.CancelSelection("different"),"paused participation retains its history");
        Check(service.EndParticipation("different") && !service.CancelSelection("different"),"ended history cannot be discarded");
        Check(!service.CancelSelection("synthetic-walk"),"rewarded history cannot be discarded");
        Console.WriteLine("PASS "+checks+" walking personalization assertions (synthetic data)");
    }
    class TestClock:IClock {public DateTimeOffset UtcNow=>Now;public TimeZoneInfo TimeZone=>Zone;}
    class MemoryStore:IStateStore
    {
        GardenState value;public bool Fail;
        public GardenState Load()=>value==null?null:StateCodec.Clone(value);
        public void Save(GardenState state){if(Fail)throw new IOException("Synthetic save failure");value=StateCodec.Clone(state);}
    }
}
