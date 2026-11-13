using System;
using System.Linq;
using CapstoneDesign.Runtime;
using CapstoneDesign.Runtime.LocalState;

class WalkingBaselineRefreshTests
{
    static int checks,requests;
    static readonly TimeZoneInfo Zone=TimeZoneInfo.CreateCustomTimeZone("Synthetic/Walking",TimeSpan.FromHours(9),"Synthetic","Synthetic");
    static DateTimeOffset now=DateTimeOffset.Parse("2026-10-12T12:00:00+09:00");
    static void Check(bool condition,string description){checks++;if(!condition)throw new Exception(description);}
    static PlatformActionResult Request(){requests++;return new PlatformActionResult{status="AVAILABLE"};}
    static HealthConnectSnapshot Health(DateTimeOffset at,int steps=10000)
    {
        var midnight=new DateTimeOffset(at.Date,at.Offset);
        return new HealthConnectSnapshot{status="AVAILABLE",lastRefreshEpochMs=at.ToUnixTimeMilliseconds(),
            personalization=new WalkingBaselineSnapshot{status="AVAILABLE",queryComplete=true,lastUpdatedEpochMs=at.ToUnixTimeMilliseconds(),
                queryEndEpochMs=midnight.ToUnixTimeMilliseconds(),days=Enumerable.Range(0,7).Select(i=>new DailyStepBaseline{
                    count=steps,hasValue=true,endEpochMs=midnight.AddDays(-i).ToUnixTimeMilliseconds()}).ToArray(),
                sleep=new HealthMetricSnapshot{status="AVAILABLE",hasValue=true,queryComplete=true,value=300,measuredAtEpochMs=at.AddHours(-1).ToUnixTimeMilliseconds()}}};
    }
    static void Start(WalkingBaselineRefresh refresh,HealthConnectSnapshot health,float seconds=0,string mode="LIVE")=>refresh.Request(mode,health,now,Zone,seconds,Request);
    static void Tick(WalkingBaselineRefresh refresh,HealthConnectSnapshot health,float seconds,string mode="LIVE")=>refresh.Tick(mode,health,now,Zone,seconds,Request);
    static void Main()
    {
        var fresh=Health(now);var refresh=new WalkingBaselineRefresh();Start(refresh,fresh);
        Check(requests==0 && !refresh.IsPending,"fresh baseline starts immediately without native read");
        var stale=Health(now.AddHours(-7));Start(refresh,stale);
        Check(requests==1 && refresh.IsPending,"long background return requests baseline");
        Start(refresh,stale,1);Start(refresh,stale,2);Tick(refresh,stale,3);
        Check(requests==1 && refresh.IsPending,"repeat focus and cached response cannot submit twice or allow stale start");
        Check(WalkingPersonalization.Evaluate(stale.personalization,now,Zone).GoalSteps==5000,"old cached sleep and activity are not used");
        Tick(refresh,fresh,4);
        Check(!refresh.IsPending && !refresh.Failed,"fresh result ends pending wait");
        var adjusted=WalkingPersonalization.Evaluate(fresh.personalization,now,Zone);
        Check(adjusted.GoalSteps==8000 && adjusted.ReducedForSleep,"new result adjusts usual activity and sleep before start");
        var store=new Store();var service=new GardenStateService(store,new Clock(),new DemoBalanceConfig());
        Check(service.Open() && service.RefreshCycle(new string[0]) && service.BeginSession("synthetic-fresh","free-mission:M15") &&
            service.StartWalkingSession("synthetic-fresh",adjusted),"new walking selection persists refreshed target");
        var saved=service.Snapshot.Sessions.Single();string start=saved.WalkingQueryStartUtc,end=saved.WalkingQueryEndUtc;
        var later=Health(now,6000);Tick(refresh,later,5);
        Check(service.StartWalkingSession(saved.SessionId,WalkingPersonalization.Evaluate(later.personalization,now,Zone)) &&
            service.Snapshot.Sessions.Single().WalkingGoalSteps==8000 && service.Snapshot.Sessions.Single().WalkingQueryStartUtc==start &&
            service.Snapshot.Sessions.Single().WalkingQueryEndUtc==end,"later results never rewrite started target or frozen calendar interval");

        var midnightNow=DateTimeOffset.Parse("2026-10-13T00:01:00+09:00");
        Check(!WalkingPersonalization.IsFresh(Health(midnightNow.AddMinutes(-2)).personalization,midnightNow,Zone),"midnight invalidates even a two-minute-old baseline");
        refresh=new WalkingBaselineRefresh();requests=0;
        refresh.Request("LIVE",Health(midnightNow.AddMinutes(-2)),midnightNow,Zone,0,Request);
        refresh.Tick("LIVE",Health(midnightNow),midnightNow,Zone,1,Request);
        Check(requests==1 && !refresh.IsPending && WalkingPersonalization.Evaluate(Health(midnightNow).personalization,midnightNow,Zone).GoalSteps==8000,
            "midnight return refreshes completed-day baseline before new goal");
        refresh=new WalkingBaselineRefresh();requests=0;
        var beforeMidnight=midnightNow.AddMinutes(-2);
        refresh.Request("LIVE",Health(beforeMidnight.AddHours(-7)),beforeMidnight,Zone,0,Request);
        refresh.Tick("LIVE",Health(beforeMidnight),beforeMidnight,Zone,1,Request);
        refresh.Request("LIVE",Health(beforeMidnight),midnightNow,Zone,2,Request);
        Check(requests==2 && refresh.IsPending,"successful refresh cooldown cannot block a new calendar day's immediate Start");

        refresh=new WalkingBaselineRefresh();requests=0;stale.refreshing=true;Start(refresh,stale);
        Check(refresh.IsPending && requests==0,"existing native read is adopted without queueing another");
        Tick(refresh,stale,2);Start(refresh,stale,3);Tick(refresh,fresh,4);
        Check(requests==0 && !refresh.IsPending,"one in-flight initialization read serves repeated focus events");
        refresh=new WalkingBaselineRefresh();Start(refresh,stale);
        var denied=new HealthConnectSnapshot{status="AVAILABLE",lastRefreshEpochMs=now.ToUnixTimeMilliseconds(),
            personalization=new WalkingBaselineSnapshot{status="PERMISSION_DENIED"}};
        Tick(refresh,denied,1);
        Check(refresh.Failed && !refresh.IsPending && requests==0,"adopted permission failure does not repeat native read");
        Start(refresh,denied,2);Check(requests==0 && !refresh.IsPending,"permission fallback observes cooldown");

        refresh=new WalkingBaselineRefresh();stale.refreshing=false;requests=0;
        refresh.Request("LIVE",stale,now,Zone,0,()=>{requests++;return new PlatformActionResult{status="PERMISSION_DENIED"};});
        Check(refresh.Failed && !refresh.IsPending && requests==1,"immediate denied result enables fallback");
        Start(refresh,stale,119);Check(requests==1 && !refresh.IsPending,"failure cooldown resists repeated activity entry");
        Start(refresh,stale,121);Check(requests==2 && refresh.IsPending,"entry can retry after cooldown");
        Tick(refresh,denied,122);Check(!refresh.IsPending && refresh.Failed,"completed denied snapshot releases pending start");
        Check(WalkingPersonalization.Evaluate(denied.personalization,now,Zone).GoalSteps==5000,"denied query retains explicit default");

        refresh=new WalkingBaselineRefresh();requests=0;Start(refresh,stale);
        stale.refreshing=true;Tick(refresh,stale,WalkingBaselineRefresh.TimeoutSeconds-.1f);
        Check(refresh.IsPending,"native busy waits only within bound");
        Tick(refresh,stale,WalkingBaselineRefresh.TimeoutSeconds);
        Check(!refresh.IsPending && refresh.Failed && requests==1,"fifteen-second timeout releases start without another read");
        Start(refresh,stale,16);Check(requests==1 && !refresh.IsPending,"timeout cooldown prevents starting a second busy read");
        var fallback=WalkingPersonalization.Evaluate(stale.personalization,now,Zone);
        service.EndParticipation(saved.SessionId);service.BeginSession("synthetic-fallback","free-mission:M15");
        Check(service.StartWalkingSession("synthetic-fallback",fallback) && service.Snapshot.Sessions.Last().WalkingGoalSteps==5000,
            "timed-out health query still permits default walking participation");
        Start(refresh,fresh,17);Check(!refresh.IsPending && !refresh.Failed && requests==1,"late fresh result is reusable even inside failed-attempt cooldown");
        Check(service.Snapshot.Sessions.Last().WalkingGoalSteps==5000,"late native result cannot mutate already started fallback");

        refresh=new WalkingBaselineRefresh();requests=0;stale.refreshing=false;Start(refresh,stale,0,"MOCK");
        Check(requests==0 && !refresh.IsPending,"mock never calls native refresh");
        Start(refresh,stale);Tick(refresh,fresh,1,"MOCK");
        Check(!refresh.IsPending && requests==1,"mode change cancels UI wait without issuing another query");
        refresh=new WalkingBaselineRefresh();refresh.Request("LIVE",stale,now,Zone,0,()=>{throw new Exception("synthetic JNI failure");});
        Check(refresh.Failed && !refresh.IsPending,"JNI exception cannot strand start");
        refresh=new WalkingBaselineRefresh();refresh.Request("LIVE",stale,now,Zone,0,()=>null);
        Check(refresh.Failed && !refresh.IsPending,"null JNI result enables fallback");
        Console.WriteLine("PASS "+checks+" walking baseline refresh assertions (synthetic data)");
    }
    class Clock:IClock{public DateTimeOffset UtcNow=>now;public TimeZoneInfo TimeZone=>Zone;}
    class Store:IStateStore{GardenState state;public GardenState Load()=>state==null?null:StateCodec.Clone(state);public void Save(GardenState value){state=StateCodec.Clone(value);}}
}
