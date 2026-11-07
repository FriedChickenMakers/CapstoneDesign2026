using System;
using System.IO;
using System.Linq;
using CapstoneDesign.Runtime.LocalState;

sealed class FocusMeasurementClock:IClock
{
    public DateTimeOffset Time=DateTimeOffset.Parse("2030-01-01T12:00:00Z");
    public DateTimeOffset UtcNow=>Time;
    public TimeZoneInfo TimeZone=>TimeZoneInfo.Utc;
}
sealed class FocusMeasurementStore:IStateStore
{
    public string Saved;public bool Fail;
    public GardenState Load()=>Saved==null?null:StateCodec.Decode(Saved);
    public void Save(GardenState state){if(Fail)throw new IOException("Synthetic storage failure");Saved=StateCodec.Encode(state);}
}
static class FocusRecommendationMeasurementTests
{
    static int checks;
    static void Check(bool ok,string name){checks++;if(!ok)throw new Exception(name);}
    static FocusRecommendationExposure Exposure(FocusMeasurementClock clock)=>new FocusRecommendationExposure{
        DecisionId=new string('a',32),MissionId="free-mission:M17",PolicyId="focus-additional-history-preference-v2",
        Reason="focus-method-preference",ShownUtc=clock.Time.AddMinutes(-1).ToString("O")};
    static GardenStateService Open(FocusMeasurementStore store,FocusMeasurementClock clock)
    {var service=new GardenStateService(store,clock,new DemoBalanceConfig());Check(service.Open(),"open");Check(service.RefreshCycle(Array.Empty<string>()),"refresh");return service;}
    static ActivitySession Read(GardenStateService service)=>service.Snapshot.Sessions.Single();
    static void Main()
    {
        var clock=new FocusMeasurementClock();var store=new FocusMeasurementStore();var service=Open(store,clock);
        var exposure=Exposure(clock);
        Check(service.BeginFocusSession("synthetic-other-choice","M02",exposure:exposure),"other activity can follow recommendation");
        Check(Read(service).MissionId=="free-mission:M02"&&Read(service).FocusRecommendationExposure.MissionId=="free-mission:M17","actual and recommended remain distinct");
        exposure.Reason="synthetic caller mutation";
        Check(Read(service).FocusRecommendationExposure.Reason=="focus-method-preference","source snapshot copied");
        var copy=service.Snapshot;copy.Sessions[0].FocusRecommendationExposure.MissionId="free-mission:M01";
        Check(Read(service).FocusRecommendationExposure.MissionId=="free-mission:M17","nested snapshot copied");
        service=Open(store,clock);
        Check(Read(service).FocusRecommendationExposure.DecisionId==new string('a',32),"XML restart preserves decision");
        Check(service.BeginFocusSession("synthetic-other-choice","M02",exposure:Exposure(clock)),"retry same session");
        Check(service.Snapshot.Sessions.Count==1,"retry cannot duplicate selection");
        foreach(var change in new Action<FocusRecommendationExposure>[] {
            e=>e.ShownUtc=clock.Time.AddSeconds(1).ToString("O"), e=>e.ShownUtc=clock.Time.AddDays(-1).AddSeconds(-1).ToString("O"),
            e=>e.DecisionId="synthetic-not-a-random-id",e=>e.Reason="synthetic arbitrary text",e=>e.MissionId="course:P01"})
        {
            var bad=Exposure(clock);change(bad);var isolated=new FocusMeasurementStore();var target=Open(isolated,clock);
            Check(!target.BeginFocusSession("synthetic-invalid","M02",exposure:bad),"invalid attribution rejected");
            Check(target.Snapshot.Sessions.Count==0,"invalid attribution cannot partially create session");
        }
        store=new FocusMeasurementStore();service=Open(store,clock);store.Fail=true;
        Check(!service.BeginFocusSession("synthetic-failed","M02",exposure:Exposure(clock)),"write failure surfaced");
        Check(service.Snapshot.Sessions.Count==0,"failed write does not create measured selection");
        store.Fail=false;Check(service.BeginFocusSession("synthetic-direct","M02"),"direct start still works");
        Check(Read(service).FocusRecommendationExposure==null&&Read(service).FocusRecommendationId==null,"direct start cannot invent exposure");
        service=Open(store,clock);Check(Read(service).FocusRecommendationExposure==null,"older no-attribution saves survive");
        Console.WriteLine("Focus recommendation measurement state: "+checks+" assertions PASS");
    }
}
