using System;
using System.IO;
using System.Linq;
using CapstoneDesign.Runtime.LocalState;
class Clock : IClock
{
    public DateTimeOffset Time = DateTimeOffset.Parse("2026-09-22T18:59:00Z");
    public TimeZoneInfo Zone = TimeZoneInfo.CreateCustomTimeZone("Korea", TimeSpan.FromHours(9), "Korea", "Korea");
    public DateTimeOffset UtcNow { get { return Time; } }
    public TimeZoneInfo TimeZone { get { return Zone; } }
}
class Store : IStateStore
{
    public GardenState State, LastAttempt; public bool Fail; public int Saves;
    public GardenState Load() { return State == null ? null : StateCodec.Clone(State); }
    public void Save(GardenState s) { LastAttempt=StateCodec.Clone(s); if (Fail) throw new IOException("Synthetic save failure"); State = StateCodec.Clone(s); Saves++; }
}
class Tests
{
    static int checks;
    static void Assert(bool ok, string message) { checks++; if (!ok) throw new Exception(message); }
    static GardenStateService Service(Store store, Clock clock, DemoBalanceConfig config = null, LegacySnapshot legacy = null)
    { var service = new GardenStateService(store, clock, config ?? new DemoBalanceConfig { CompletionNutrient=10 }, legacy); Assert(service.Open(), "open"); return service; }
    static void Session(GardenStateService s, string id, string mission = "course:P01", int order = 1)
    { Assert(s.BeginSession(id, mission, order), "select"); Assert(s.StartSession(id), "start"); }
    static void Main()
    {
        var clock = new Clock(); var store = new Store(); var s = Service(store, clock);
        Assert(s.RefreshCycle(new[] { "animal:A01" }), "cycle");
        Assert(s.Snapshot.LastCycleId == "2026-09-22", "before reset");
        Session(s,"s1"); store.Fail = true;
        Assert(!s.CompleteSession("s1"), "save failure surfaced");
        Assert(s.Snapshot.Nutrient == 0 && s.Snapshot.Sessions[0].Status == ParticipationState.InProgress && s.Snapshot.NextCourseOrder == 1, "atomic failed completion");
        store.Fail = false; Assert(s.CompleteSession("s1"), "complete without reflection/sensor/timer");
        Assert(s.CompleteSession("s1") && s.Snapshot.Nutrient == 10 && s.Snapshot.NextCourseOrder == 2, "duplicate session");
        s = Service(store, clock); Assert(s.CompleteSession("s1") && s.Snapshot.Nutrient == 10, "restart duplicate");
        Session(s,"s2"); Assert(s.CompleteSession("s2") && s.Snapshot.Nutrient == 10, "daily mission receipt");
        clock.Time = clock.Time.AddMinutes(1); Assert(s.RefreshCycle(new string[0]), "boundary");
        Assert(s.Snapshot.LastCycleId == "2026-09-23" && s.Snapshot.DailyDecisions.Count == 2 && s.Snapshot.NextCourseOrder == 2, "new day course independent");
        clock.Time = clock.Time.AddDays(10); Assert(s.RefreshCycle(new string[0]) && s.Snapshot.DailyDecisions.Count == 3 && s.Snapshot.Nutrient == 10, "no catchup");
        clock.Time = clock.Time.AddDays(-8); Assert(s.RefreshCycle(new[] {"animal:A01"}) && s.Snapshot.DailyDecisions.Count == 3, "clock rollback");
        clock.Zone = TimeZoneInfo.CreateCustomTimeZone("West", TimeSpan.FromHours(-12), "West", "West");
        Assert(s.RefreshCycle(new[] {"animal:A02"}) && s.Snapshot.DailyDecisions.Count == 3, "timezone rollback");
        var before = StateCodec.Encode(s.Snapshot);
        Assert(s.CanPurchaseEnvironment("environment:E01", " LEFT "), "preview");
        Assert(StateCodec.Encode(s.Snapshot) == before, "preview/cancel immutable");
        store.Fail = true; Assert(!s.PurchaseEnvironment("p1", "environment:E01", "left") && s.Snapshot.Nutrient == 10 && s.Snapshot.Environments.Count == 0, "atomic purchase failure");
        store.Fail = false; Assert(s.PurchaseEnvironment("p1", "environment:E01", " LEFT "), "purchase");
        Assert(s.PurchaseEnvironment("p1", "environment:E01", "left") && s.Snapshot.Nutrient == 0 && s.Snapshot.Environments.Count == 1, "purchase idempotent");
        Assert(!s.PurchaseEnvironment("p2", "environment:E02", "LEFT"), "normalized overlap");
        Assert(!s.GrowPlant("g1", "plant:P01"), "insufficient funds");
        Session(s,"stopped", "free-mission:M01",0); Assert(s.PauseSession("stopped") && s.ResumeSession("stopped") && s.EndParticipation("stopped"), "pause resume end");
        Assert(!s.CompleteSession("stopped") && s.Snapshot.Nutrient == 0, "participation end no reward");
        Assert(!s.BeginSession("invalid", "course:P29",29), "invalid order");
        Assert(!s.BeginSession("ahead", "course:P03",3) && s.Snapshot.NextCourseOrder==2,"future course cannot be selected");
        var legacy = new LegacySnapshot { Nutrient = 77, GardenXp = 123, Growth = .3f, LastUnlock = "seed" };
        legacy.CompletedMissionIds.Add("free-mission:legacy"); legacy.UnlockIds.Add("seed");
        var migratedStore = new Store(); var migrated = Service(migratedStore,clock,null,legacy);
        Assert(migrated.Snapshot.Nutrient == 77 && migrated.Snapshot.GardenXp == 123 && migrated.Snapshot.LegacyGrowth == .3f && migrated.Snapshot.UnlockIds[0] == "seed" && migrated.Snapshot.NextCourseOrder == 1, "legacy preserved no XP conversion");
        legacy.Nutrient = 900; migrated = Service(migratedStore,clock,null,legacy);
        Assert(migrated.Snapshot.Nutrient == 77 && migrated.Snapshot.LegacyCompletedUnknownDate.Count == 1, "migration once");
        migrated.RefreshCycle(new string[0]); Session(migrated,"legacy", "free-mission:legacy",0); migrated.CompleteSession("legacy");
        Assert(migrated.Snapshot.Nutrient == 77, "legacy completion not rewarded");
        var failedMigration = new Store { Fail = true }; var failed = new GardenStateService(failedMigration,clock,new DemoBalanceConfig { CompletionNutrient=10 },legacy);
        Assert(!failed.Open() && failedMigration.State == null, "migration failure no publication"); failedMigration.Fail=false; Assert(failed.Open() && failed.Snapshot.Nutrient ==900,"migration retry once");
        var visitors = Service(new Store(),clock,new DemoBalanceConfig { VisitorChancePercent = 100 });
        visitors.RefreshCycle(new[] {"animal:A01"}); Assert(visitors.Snapshot.DailyDecisions[0].VisitorId == "animal:A01", "visitor");
        visitors.RefreshCycle(new[] {"animal:A02"}); Assert(visitors.Snapshot.DailyDecisions[0].VisitorId == "animal:A01", "content refresh no redraw");
        clock.Time = clock.Time.AddDays(1); visitors.RefreshCycle(new string[0]); Assert(visitors.Snapshot.DailyDecisions.Last().VisitorId == "" && visitors.Snapshot.DiscoveredAnimalIds.Count == 1, "absence and retained discovery");
        var noVisitor = Service(new Store(),clock,new DemoBalanceConfig { VisitorChancePercent = 0 }); noVisitor.RefreshCycle(new[]{"animal:A01"}); noVisitor.RefreshCycle(new[]{"animal:A02"}); Assert(noVisitor.Snapshot.DailyDecisions.Count ==1 && noVisitor.Snapshot.DailyDecisions[0].VisitorId=="","persisted absence");
        var detached = s.Snapshot; detached.Nutrient=9999; Assert(s.Snapshot.Nutrient != 9999, "snapshot cannot mutate state");
        var retryStore = new Store(); var retry = Service(retryStore,clock,new DemoBalanceConfig { VisitorChancePercent=100 });
        retryStore.Fail=true; Assert(!retry.RefreshCycle(new[]{"animal:A01","animal:A02"}) && retry.Snapshot.DailyDecisions.Count==0,"failed cycle not published");
        retryStore.Fail=false; Assert(retry.RefreshCycle(new[]{"animal:A01","animal:A02"}),"cycle retry");
        var expectedVisitor=retry.Snapshot.DailyDecisions[0].VisitorId; retry=Service(retryStore,clock); retry.RefreshCycle(new[]{"animal:A03"}); Assert(retry.Snapshot.DailyDecisions[0].VisitorId==expectedVisitor,"saved cycle stable after restart");
        var grow = Service(new Store(),clock,null,new LegacySnapshot{Nutrient=20}); Assert(grow.GrowPlant("grow","plant:P01") && grow.GrowPlant("grow","plant:P01") && grow.Snapshot.Nutrient==10 && grow.Snapshot.Plants[0].Growth==.1f,"growth one debit");
        var fuzzStore=new Store(); var fuzz=Service(fuzzStore,clock); fuzz.RefreshCycle(new string[0]);
        for(int i=0;i<60;i++) { Session(fuzz,"repeat-"+i,"free-mission:M01",0); Assert(fuzz.CompleteSession("repeat-"+i),"repeat completion"); if(i%7==0) fuzz=Service(fuzzStore,clock); }
        Assert(fuzz.Snapshot.Nutrient==10 && fuzz.Snapshot.RewardReceipts.Count==1,"sixty completions/restarts one daily reward");
        Assert(!s.BeginSession("s1","course:P02",2),"session id payload conflict");
        Assert(!s.PurchaseEnvironment("p1","environment:E02","right"),"purchase id payload conflict");
        Assert(!s.GrowPlant("p1","plant:P01"),"purchase id cross-action conflict");
        var freezeStore=new Store(); var freeze=Service(freezeStore,clock,new DemoBalanceConfig{VisitorChancePercent=100});
        freezeStore.Fail=true; Assert(!freeze.RefreshCycle(new[]{"animal:A01"}),"visitor freeze failure"); freezeStore.Fail=false;
        Assert(freeze.RefreshCycle(new[]{"animal:A02"}) && freeze.Snapshot.DailyDecisions[0].VisitorId=="animal:A01","failed roll retained despite changed candidates");
        var restartStore=new Store(); var restartRoll=Service(restartStore,clock,new DemoBalanceConfig{VisitorChancePercent=100}); restartStore.Fail=true; restartRoll.RefreshCycle(new[]{"animal:A01","animal:A02"}); var attempted=restartStore.LastAttempt.DailyDecisions[0].VisitorId;
        restartStore.Fail=false; restartRoll=Service(restartStore,clock,new DemoBalanceConfig{VisitorChancePercent=100}); restartRoll.RefreshCycle(new[]{"animal:A01","animal:A02"}); Assert(restartRoll.Snapshot.DailyDecisions[0].VisitorId==attempted,"failed roll reproducible across restart with same inputs");
        var timeStore=new Store(); var timeService=Service(timeStore,clock); timeService.RefreshCycle(new string[0]); Session(timeService,"rollback"); var start=timeService.Snapshot.Sessions[0].StartedUtc;
        clock.Time=clock.Time.AddDays(-1); Assert(timeService.CompleteSession("rollback") && DateTimeOffset.Parse(timeService.Snapshot.Sessions[0].EndedUtc)>=DateTimeOffset.Parse(start),"rollback health interval nonnegative");
        var poisoned=StateCodec.Clone(timeService.Snapshot); poisoned.Sessions=null; var invalidStore=new Store{State=poisoned}; var invalid=new GardenStateService(invalidStore,clock,new DemoBalanceConfig { CompletionNutrient=10 }); Assert(!invalid.Open(),"missing lists fail closed");
        poisoned=StateCodec.Clone(timeService.Snapshot); poisoned.Sessions.Add(poisoned.Sessions[0]); invalidStore.State=poisoned; Assert(!invalid.Open(),"duplicate session corruption fails closed");
        poisoned=StateCodec.Clone(timeService.Snapshot); poisoned.LastCycleId="2099-01-01"; invalidStore.State=poisoned; Assert(!invalid.Open(),"invalid high water fails closed");
        poisoned=StateCodec.Clone(timeService.Snapshot); poisoned.Nutrient=-1; invalidStore.State=poisoned; Assert(!invalid.Open(),"negative nutrient fails closed");
        timeService.Dispose(); Assert(!timeService.Open(),"disposed service cannot reopen");
        var parallelStore=new Store(); var parallelService=Service(parallelStore,clock); parallelService.RefreshCycle(new string[0]);
        var select1=System.Threading.Tasks.Task.Run(()=>parallelService.BeginSession("parallel1","course:P01",1)); var select2=System.Threading.Tasks.Task.Run(()=>parallelService.BeginSession("parallel2","course:P01",1)); System.Threading.Tasks.Task.WaitAll(select1,select2);
        Assert(select1.Result != select2.Result && parallelService.Snapshot.Sessions.Count==1,"concurrent selection one active session");
        var activeId=parallelService.Snapshot.Sessions[0].SessionId; parallelService.StartSession(activeId);
        var completions=Enumerable.Range(0,20).Select(i=>System.Threading.Tasks.Task.Run(()=>parallelService.CompleteSession(activeId))).ToArray(); System.Threading.Tasks.Task.WaitAll(completions);
        Assert(completions.All(t=>t.Result) && parallelService.Snapshot.Nutrient==10 && parallelService.Snapshot.NextCourseOrder==2,"concurrent duplicate completion one transaction");
        var configured=new DemoBalanceConfig{CompletionNutrient=7}; var configuredService=Service(new Store(),clock,configured); configured.CompletionNutrient=999; configuredService.RefreshCycle(new string[0]); Session(configuredService,"config"); configuredService.CompleteSession("config"); Assert(configuredService.Snapshot.Nutrient==7,"configuration frozen at construction");
        var walkStore=new Store(); var walk=Service(walkStore,clock); walk.RefreshCycle(new string[0]);
        Assert(walk.EnableDebugWalk(),"enable walk demo"); var period=walk.Snapshot.DebugWalkPeriods[0].Id;
        Assert(walk.EnableDebugWalk() && walk.Snapshot.DebugWalkPeriods.Count==1,"enabling while on keeps one period");
        walkStore.Fail=true;
        Assert(!walk.ObserveDebugWalk(period,10) && walk.Snapshot.Nutrient==0 && walk.Snapshot.DebugWalkPeriods[0].RealStepHighWater==0,"walk reward save failure is atomic");
        walkStore.Fail=false;
        Assert(walk.ObserveDebugWalk(period,10) && walk.Snapshot.Nutrient==1,"retry actual walk observation once");
        Assert(walk.ObserveDebugWalk(period,9) && walk.ObserveDebugWalk(period,10) && walk.Snapshot.Nutrient==1,"actual callback high-water is idempotent");
        for(int i=0;i<9;i++)Assert(walk.AddVirtualStep(),"virtual step");
        Assert(walk.Snapshot.Nutrient==1 && walk.AddVirtualStep() && walk.Snapshot.Nutrient==2,"ten combined steps award one more nutrient");
        Assert(walk.ObserveDebugWalk(period,10) && walk.Snapshot.Nutrient==2,"repeated actual range does not cross threshold twice");
        walk=Service(walkStore,clock); Assert(walk.ObserveDebugWalk(period,10) && walk.Snapshot.Nutrient==2,"same range after restart no duplicate");
        Session(walk,"walk-m15","free-mission:M15",0); Assert(walk.ObserveMissionSteps("walk-m15",10) && walk.AddVirtualStep() && walk.Snapshot.Sessions.Last().VirtualSteps==1,"M15 actual and virtual progress");
        walkStore.Fail=true;
        Assert(!walk.MarkMissionGoalNotified("walk-m15") && !walk.Snapshot.Sessions.Last().GoalNotificationCommitted,"notification marker save failure is atomic");
        walkStore.Fail=false;
        Assert(walk.MarkMissionGoalNotified("walk-m15") && walk.Snapshot.Sessions.Last().GoalNotificationCommitted,"notification marker commits");
        Assert(!walk.MarkMissionGoalNotified("walk-m15"),"notification marker claims only once");
        Assert(walk.CompleteSession("walk-m15") && walk.Snapshot.Nutrient==12,"M15 completion reward stays separate");
        Assert(walk.DisableDebugWalk() && !walk.AddVirtualStep(),"debug off stops virtual steps");
        Assert(walk.ObserveDebugWalk(period,20) && walk.Snapshot.Nutrient==13,"closed period accepts final interval observation");
        Assert(walk.ObserveDebugWalk(period,20) && walk.ObserveDebugWalk(period,19) && walk.Snapshot.Nutrient==13,"closed period final observations remain idempotent");
        Assert(walk.ObserveDebugWalk(period,5,"watch:Samsung") && walk.Snapshot.DebugWalkPeriods[0].RealStepHighWater==5 &&
            walk.Snapshot.DebugWalkPeriods[0].RewardedUnits==3 && walk.Snapshot.Nutrient==13,
            "preferred-source correction lowers displayed steps without clawing back or reissuing earned rewards");
        Assert(walk.ObserveDebugWalk(period,30,"watch:Samsung") && walk.Snapshot.Nutrient==14,
            "corrected source only pays the next previously uncredited threshold");
        Assert(walk.ObserveMissionSteps("walk-m15",5,"watch:Samsung") &&
            walk.Snapshot.Sessions.Last().RealStepHighWater==5,"mission progress recalibrates to the preferred source");
        walk=Service(walkStore,clock);
        Assert(walk.Snapshot.DebugWalkPeriods[0].StepSource=="watch:Samsung" &&
            walk.Snapshot.Sessions.Last().StepSource=="watch:Samsung","preferred source survives restart");
        var oldXml=StateCodec.Encode(walk.Snapshot).Replace("<StepSource>watch:Samsung</StepSource>","");
        Assert(StateCodec.Decode(oldXml).DebugWalkPeriods[0].StepSource=="",
            "state saved before source tracking remains readable");
        FileTests(clock);
        Console.WriteLine("PASS " + checks + " local-state assertions (synthetic data; no Unity/device required)");
    }
    static void FileTests(Clock clock)
    {
        string dir=Path.Combine(Path.GetTempPath(),"garden-tests-"+Guid.NewGuid().ToString("N")); Directory.CreateDirectory(dir); string path=Path.Combine(dir,"state.xml");
        try
        {
            using(var file=new FileStateStore(path))
            {
                var s=new GardenStateService(file,clock,new DemoBalanceConfig { CompletionNutrient=10 }); Assert(s.Open(),"real file init"); Assert(s.RefreshCycle(new string[0]),"real replace"); Session(s,"persisted"); Assert(s.CompleteSession("persisted"),"real complete");
                Directory.CreateDirectory(path+".tmp"); Assert(!s.GrowPlant("blocked-write","plant:P01") && s.Snapshot.Nutrient==10 && s.Snapshot.Plants.Count==0,"real staging IO failure preserves transaction"); Directory.Delete(path+".tmp");
                Assert(s.GrowPlant("blocked-write","plant:P01") && s.Snapshot.Nutrient==0,"real staging failure retry once");
                bool locked=false; try { using(var duplicate=new FileStateStore(path)) {} } catch(IOException) {locked=true;} Assert(locked,"exclusive writer");
            }
            using(var file=new FileStateStore(path)) {var s=new GardenStateService(file,clock,new DemoBalanceConfig { CompletionNutrient=10 }); Assert(s.Open() && s.Snapshot.Nutrient==0,"real reload"); Assert(s.CompleteSession("persisted") && s.Snapshot.Nutrient==0,"real retry");}
            string validEnvelope=File.ReadAllText(path); string validBody=validEnvelope.Substring(validEnvelope.IndexOf('\n')+1);
            var oldXml=new System.Xml.XmlDocument(); oldXml.LoadXml(validBody);
            oldXml.DocumentElement.RemoveChild(oldXml.DocumentElement["DebugWalkPeriods"]);
            foreach(System.Xml.XmlNode session in oldXml.DocumentElement["Sessions"].ChildNodes)
                foreach(string field in new[]{"RealStepHighWater","VirtualSteps","GoalNotificationCommitted"})
                { var optional=session[field]; if(optional!=null)session.RemoveChild(optional); }
            Assert(StateCodec.Decode(oldXml.OuterXml).DebugWalkPeriods.Count==0,"schema-1 save without walk fields remains readable");
            string missingBody=System.Text.RegularExpressions.Regex.Replace(validBody,@"<Sessions>.*?</Sessions>","",System.Text.RegularExpressions.RegexOptions.Singleline);
            File.WriteAllText(path,StateCodec.Hash(missingBody)+"\n"+missingBody);
            using(var file=new FileStateStore(path)) {var bad=new GardenStateService(file,clock,new DemoBalanceConfig { CompletionNutrient=10 }); Assert(!bad.Open(),"checksummed structurally incomplete XML rejected");}
            File.WriteAllText(path,validEnvelope);
            var released=new FileStateStore(path); released.Dispose(); bool rejected=false; try {released.Save(new GardenState{MigrationCompleted=true});} catch(ObjectDisposedException) {rejected=true;} Assert(rejected,"disposed file writer cannot write");
            Assert(File.Exists(path+".bak"),"backup preserved"); var backup=File.ReadAllBytes(path+".bak"); File.WriteAllText(path,"broken");
            using(var file=new FileStateStore(path)) {var s=new GardenStateService(file,clock,new DemoBalanceConfig { CompletionNutrient=10 },new LegacySnapshot{Nutrient=999}); Assert(!s.Open(),"corruption fails closed");}
            Assert(backup.SequenceEqual(File.ReadAllBytes(path+".bak")),"corruption backup untouched"); File.Delete(path);
            using(var file=new FileStateStore(path)) {var s=new GardenStateService(file,clock,new DemoBalanceConfig { CompletionNutrient=10 }); Assert(!s.Open(),"missing primary backup not first run");}
            File.Delete(path+".bak"); File.WriteAllText(path+".tmp","partial first write");
            using(var file=new FileStateStore(path)) {var s=new GardenStateService(file,clock,new DemoBalanceConfig { CompletionNutrient=10 }); Assert(!s.Open(),"partial initial write fails closed");}
        }
        finally {Directory.Delete(dir,true);}
    }
}
