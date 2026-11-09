using System;
using System.IO;
using System.Linq;
using CapstoneDesign.Runtime;
using CapstoneDesign.Runtime.LocalState;
class NotificationLogicTests
{
    static int checks;
    static DateTimeOffset now=DateTimeOffset.Parse("2026-10-12T01:30:00+09:00");
    static TimeZoneInfo zone=TimeZoneInfo.CreateCustomTimeZone("Synthetic/Korea",TimeSpan.FromHours(9),"Synthetic/Korea","Synthetic/Korea");
    static void Check(bool ok,string message){checks++;if(!ok)throw new Exception(message);}
    static void Main()
    {
        var cache=new ReminderSyncCache();int calls=0;
        Func<PlatformActionResult> fail=()=>{calls++;return new PlatformActionResult{status="ERROR"};};
        Func<PlatformActionResult> success=()=>{calls++;return new PlatformActionResult{status="AVAILABLE"};};
        Check(!cache.Sync("today",fail) && !cache.Sync("today",fail) && calls==2,"JNI failure does not poison same-state retries");
        Check(cache.Sync("today",success) && cache.Sync("today",success) && calls==3,"confirmed state avoids redundant JNI writes");
        Check(!cache.Sync("changed",()=>null) && cache.Sync("changed",success) && calls==4,"null JNI result remains retryable");
        Check(!cache.Sync("third",()=>{throw new IOException("synthetic bridge failure");}) && cache.Sync("third",success),"bridge exception remains retryable");
        cache.Invalidate();Check(cache.Sync("third",success) && calls==6,"invalidated cache synchronizes unchanged payload again");

        var store=new Store();var clock=new Clock();var service=new GardenStateService(store,clock,new DemoBalanceConfig());
        Check(service.Open() && service.RefreshCycle(new string[0]),"open");
        Check(service.CurrentCycleId=="2026-10-11","fixture intentionally lies before 4am reward reset");
        service.BeginSession("walk","free-mission:M15");service.StartWalkingSession("walk",WalkingPersonalization.Evaluate(null,now,zone));
        var session=service.Snapshot.Sessions.Single();
        var receipt=new WalkingAchievement {sessionId="walk",source=WalkingPersonalization.AggregateSource,
            startEpochMs=WalkingPersonalization.QueryStartFor(session).ToUnixTimeMilliseconds(),
            endEpochMs=DateTimeOffset.Parse(session.WalkingQueryEndUtc).ToUnixTimeMilliseconds(),goalSteps=5000,steps=5000,
            achievedAtEpochMs=now.ToUnixTimeMilliseconds(),queryComplete=true};
        Check(WalkingEvidence.AcceptsAchievement(session,receipt,false,now),"exact native achievement accepted");
        Check(!WalkingEvidence.AcceptsAchievement(session,receipt,true,now),"debug mode cannot consume automatic reward receipt");
        receipt.queryComplete=false;Check(!WalkingEvidence.AcceptsAchievement(session,receipt,false,now),"partial receipt rejected");receipt.queryComplete=true;
        receipt.source="DEBUG_VIRTUAL";Check(!WalkingEvidence.AcceptsAchievement(session,receipt,false,now),"virtual receipt cannot become real goal");
        receipt.source="HEALTH_CONNECT_PREFERRED";Check(!WalkingEvidence.AcceptsAchievement(session,receipt,false,now),"since-start source cannot certify day-total target");
        receipt.source=WalkingPersonalization.AggregateSource;receipt.steps=4999;Check(!WalkingEvidence.AcceptsAchievement(session,receipt,false,now),"below-target receipt rejected");receipt.steps=5000;
        receipt.goalSteps=10000;Check(!WalkingEvidence.AcceptsAchievement(session,receipt,false,now),"changed goal rejected");receipt.goalSteps=5000;
        receipt.startEpochMs++;Check(!WalkingEvidence.AcceptsAchievement(session,receipt,false,now),"changed start rejected");receipt.startEpochMs--;
        receipt.endEpochMs++;Check(!WalkingEvidence.AcceptsAchievement(session,receipt,false,now),"changed end rejected");receipt.endEpochMs--;
        receipt.sessionId="previous";Check(!WalkingEvidence.AcceptsAchievement(session,receipt,false,now),"old session receipt rejected");receipt.sessionId="walk";
        receipt.achievedAtEpochMs=0;Check(!WalkingEvidence.AcceptsAchievement(session,receipt,false,now),"missing achievement time rejected");
        receipt.achievedAtEpochMs=now.AddSeconds(10).ToUnixTimeMilliseconds();Check(!WalkingEvidence.AcceptsAchievement(session,receipt,false,now),"future achievement rejected");
        receipt.achievedAtEpochMs=now.AddSeconds(-1).ToUnixTimeMilliseconds();Check(!WalkingEvidence.AcceptsAchievement(session,receipt,false,now),"pre-session achievement rejected");
        receipt.achievedAtEpochMs=now.ToUnixTimeMilliseconds();
        Check(WalkingEvidence.AcceptsAchievement(session,receipt,false,now.AddDays(3)),"earned fixed-window receipt survives delayed app return");

        var result=new StepRangeSnapshot {requestId="query",queryStartEpochMs=receipt.startEpochMs,queryEndEpochMs=now.ToUnixTimeMilliseconds(),
            lastUpdatedEpochMs=now.ToUnixTimeMilliseconds(),count=5000,status="AVAILABLE",queryComplete=true,hasValue=true,
            source=WalkingPersonalization.AggregateSource};
        long start=result.queryStartEpochMs,end=result.queryEndEpochMs;
        Check(WalkingEvidence.AcceptsMissionQuery(session,false,"query",start,end,true,result,now),"normal day query accepted");
        Check(!WalkingEvidence.AcceptsMissionQuery(session,true,"query",start,end,true,result,now),"normal query arriving after debug toggle ignored");
        Check(!WalkingEvidence.MatchesQuery("old-query",start,end,result,now),"different request id ignored");
        result.queryEndEpochMs++;Check(!WalkingEvidence.MatchesQuery("query",start,end,result,now),"same id with different response interval ignored");result.queryEndEpochMs--;
        result.lastUpdatedEpochMs=end-1;Check(!WalkingEvidence.MatchesQuery("query",start,end,result,now),"stale response ignored");result.lastUpdatedEpochMs=end;
        var later=now.AddMinutes(5);long debugStart=DateTimeOffset.Parse(session.StartedUtc).ToUnixTimeMilliseconds();
        result.queryStartEpochMs=debugStart;result.queryEndEpochMs=later.ToUnixTimeMilliseconds();result.lastUpdatedEpochMs=result.queryEndEpochMs;result.source="synthetic.origin|1";
        Check(WalkingEvidence.AcceptsMissionQuery(session,true,"query",debugStart,result.queryEndEpochMs,false,result,later),"debug since-start query accepted while debug remains enabled");
        Check(!WalkingEvidence.AcceptsMissionQuery(session,false,"query",debugStart,result.queryEndEpochMs,false,result,later),"debug result arriving after normal toggle cannot complete personal target");
        service.ObserveMissionSteps("walk",5000,"synthetic.origin|1");
        Check(!WalkingEvidence.CanAutomaticallyComplete(service.Snapshot.Sessions.Single(),false),"persisted debug source cannot be relabelled as personal goal evidence");
        service.ObserveMissionSteps("walk",5000,receipt.source);
        Check(WalkingEvidence.CanAutomaticallyComplete(service.Snapshot.Sessions.Single(),false),"valid observed target can retry completion without another sensor read");
        Check(!WalkingEvidence.CanAutomaticallyComplete(service.Snapshot.Sessions.Single(),true),"debug virtual completion never becomes automatic");
        string beforeGrowth=StateCodec.Encode(service.Snapshot);store.Fail=true;
        Check(!service.TryCommitWalkingPracticeGrowth("walk") && StateCodec.Encode(service.Snapshot)==beforeGrowth,"automatic practice-end save failure leaves the stage, ledger and wallet uncommitted");
        store.Fail=false;
        Check(service.TryCommitWalkingPracticeGrowth("walk") && service.Snapshot.Coins==0
            && service.Snapshot.Sessions.Single().Status==ParticipationState.InProgress
            && service.GetPracticeGrowth("walk").Outcome==PracticeGrowthOutcome.Grown,"walking goal grows once before optional reflection without coins or final completion");
        string grown=CapstoneDesign.Plants.PlantCodec.Encode(service.Snapshot.PlantAppearance.Recipe);
        Check(service.TryCommitWalkingPracticeGrowth("walk") && CapstoneDesign.Plants.PlantCodec.Encode(service.Snapshot.PlantAppearance.Recipe)==grown,
            "repeated native goal receipt cannot grow the same practice twice");
        store.Fail=true;Check(!service.TryCompleteWalkingGoal("walk","synthetic mood","synthetic reflection draft") && service.Snapshot.Nutrient==0,"final completion save failure remains uncommitted");
        Check(service.Snapshot.Sessions.Single().Mood==null && service.Snapshot.Sessions.Single().Note==null,"failed reward save does not partially publish reflection");
        store.Fail=false;Check(service.TryCompleteWalkingGoal("walk","synthetic mood","synthetic reflection draft") && service.Snapshot.Coins==50
            && service.Snapshot.Sessions.Single().RewardAmount==50
            && CapstoneDesign.Plants.PlantCodec.Encode(service.Snapshot.PlantAppearance.Recipe)==grown,"retry atomically saves reflection and 50 coins without another growth");
        Check(service.Snapshot.Sessions.Single().Mood=="synthetic mood" && service.Snapshot.Sessions.Single().Note=="synthetic reflection draft","automatic completion atomically retains the drafted reflection");
        Check(service.TryCompleteWalkingGoal("walk") && service.Snapshot.Coins==50,"duplicate receipt does not grant more coins");
        Check(service.Snapshot.Sessions.Single().Note=="synthetic reflection draft","duplicate completion with no draft cannot erase saved reflection");
        var reopened=new GardenStateService(store,clock,new DemoBalanceConfig());
        Check(reopened.Open() && reopened.Snapshot.Coins==50 && reopened.Snapshot.Sessions.Single().Note=="synthetic reflection draft" && reopened.Snapshot.Sessions.Single().Mood=="synthetic mood","auto-completed reflection and coin award survive restart");
        reopened.Dispose();
        Check(!WalkingEvidence.AcceptsAchievement(service.Snapshot.Sessions.Single(),receipt,false,now),"completed session rejects stale receipt");
        Check(ActivityNotificationLogic.CompletedCalendarDay(service.Snapshot,now,zone)=="2026-10-12","calendar completion suppresses reminder even before 4am cycle reset");
        Check(ActivityNotificationLogic.CompletedCalendarDay(service.Snapshot,now.AddDays(1),zone)=="","previous-day completion does not suppress following day");
        Check(ActivityNotificationLogic.CompletedCalendarDay(service.Snapshot,now.AddSeconds(-1),zone)=="","future completion not treated as current evidence after clock rollback");
        Check(service.BeginSession("daily-basic","course:P01",1)&&service.StartSession("daily-basic")&&service.CompletePracticeSession("daily-basic"),"Complete independent basic after additional walking mission");
        Check(service.Snapshot.Coins==100,"Independent basic completion adds another 50 coins");
        Check(DailyEmotionPolicy.Availability(service.Snapshot)==null&&ActivityNotificationLogic.CompletedCalendarDay(service.Snapshot,now,zone)=="",
            "P/M completion does not suppress a reminder while the unlocked daily emotion mission remains unfinished");
        string emotionId=DailyEmotionPolicy.RecordId(service.CurrentCycleId);
        Check(service.BeginDailyEmotion(service.CurrentCycleId)&&service.SaveDailyEmotionDraft(emotionId,"평온함","synthetic reminder draft",0),"Prepare daily reminder draft");
        Check(ActivityNotificationLogic.CompletedCalendarDay(service.Snapshot,now,zone)=="","An unfinished daily emotion draft still permits the reminder");
        string beforeEmotion=StateCodec.Encode(service.Snapshot);
        string emotionPlant=CapstoneDesign.Plants.PlantCodec.Encode(service.Snapshot.PlantAppearance.Recipe);
        store.Fail=true;Check(!service.CompleteDailyEmotion(emotionId,"평온함","synthetic reminder draft")&&ActivityNotificationLogic.CompletedCalendarDay(service.Snapshot,now,zone)==""
            && StateCodec.Encode(service.Snapshot)==beforeEmotion,
            "Failed daily completion cannot suppress a pending reminder");store.Fail=false;
        Check(service.CompleteDailyEmotion(emotionId,"평온함","synthetic reminder draft")&&ActivityNotificationLogic.CompletedCalendarDay(service.Snapshot,now,zone)=="2026-10-12",
            "Successful third completion suppresses the current calendar-day reminder even before 04:00");
        Check(service.Snapshot.Coins==200 && service.Snapshot.PlantAppearance.Memos.Count==1 && service.GetPracticeGrowth(emotionId)==null
            && CapstoneDesign.Plants.PlantCodec.Encode(service.Snapshot.PlantAppearance.Recipe)==emotionPlant,
            "Third completion adds 100 coins and one memo without growth");
        string afterEmotion=StateCodec.Encode(service.Snapshot);
        Check(service.CompleteDailyEmotion(emotionId,"ignored","ignored") && StateCodec.Encode(service.Snapshot)==afterEmotion,
            "Repeated completion retains the reminder and exact memo/coin award");
        Console.WriteLine("PASS "+checks+" activity notification integration assertions (synthetic data)");
    }
    class Clock:IClock {public DateTimeOffset UtcNow=>now;public TimeZoneInfo TimeZone=>zone;}
    class Store:IStateStore
    {
        GardenState value;public bool Fail;
        public GardenState Load()=>value==null?null:StateCodec.Clone(value);
        public void Save(GardenState state){if(Fail)throw new IOException("Synthetic save failure");value=StateCodec.Clone(state);}
    }
}
