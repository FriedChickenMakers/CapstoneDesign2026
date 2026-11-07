using System;
using System.IO;
using System.Linq;
using CapstoneDesign.Plants;
using CapstoneDesign.Runtime.LocalState;

sealed class DailyClock : IClock
{
    public DateTimeOffset Time = DateTimeOffset.Parse("2026-11-09T12:00:00Z");
    public DateTimeOffset UtcNow => Time;
    public TimeZoneInfo TimeZone => TimeZoneInfo.Utc;
}
sealed class DailyStore : IStateStore
{
    public GardenState State; public bool Fail; public int Saves;
    public GardenState Load() => State == null ? null : StateCodec.Clone(State);
    public void Save(GardenState candidate)
    {
        if (Fail) throw new IOException("Synthetic daily-policy storage failure");
        State = StateCodec.Clone(candidate); Saves++;
    }
}
static class DailyMissionPolicyTests
{
    static int checks;
    static void Check(bool value, string why) { checks++; if (!value) throw new Exception(why); }
    static GardenStateService Open(DailyStore store, DailyClock clock)
    {
        var service = new GardenStateService(store, clock, new DemoBalanceConfig { VisitorChancePercent = 0 }, new LegacySnapshot { Nutrient = 137 });
        Check(service.Open() && service.RefreshCycle(Array.Empty<string>()), "Open synthetic daily state: " + service.LastError);
        return service;
    }
    static ActivitySession Get(GardenStateService service, string id) => service.Snapshot.Sessions.Single(s => s.SessionId == id);
    static void Advance(GardenStateService service, DailyClock clock, string utc)
    { clock.Time = DateTimeOffset.Parse(utc); Check(service.RefreshCycle(Array.Empty<string>()), "Advance synthetic clock"); }
    static void Select(GardenStateService service, string id, string code = "M03", int order = 0)
    { Check(service.BeginFocusSession(id, code, order), "Select " + id + ": " + service.LastError); }
    static void Ready(GardenStateService service, string id)
    {
        Check(service.StartSession(id), "Start " + id);
        var definition = FocusMissionCatalog.FindForSession(Get(service, id));
        for (int index = Get(service,id).InstructionStep; index < definition.Steps.Length; index++)
        {
            var step = definition.Steps[index]; string value = step.RequiredAnswer ? "synthetic answer" : null;
            Check(service.ConfirmFocusStep(id, index, value, value == null), "Confirm " + id + ": " + service.LastError);
        }
    }
    static void FailUnchanged(DailyStore store, GardenStateService service, Func<bool> operation, string label)
    {
        string before = StateCodec.Encode(service.Snapshot), durable = StateCodec.Encode(store.State); int saves = store.Saves;
        store.Fail = true;
        try { Check(!operation(), label + " returns failure"); } finally { store.Fail = false; }
        Check(StateCodec.Encode(service.Snapshot) == before && StateCodec.Encode(store.State) == durable && store.Saves == saves,
            label + " preserves complete live and durable state");
    }
    static void Main()
    {
        IndependentDailySlots(); CarrySelectedAndStarted(); BoundaryFailure(); ReflectionRetainsPracticeDay(); HistoricMultipleAdditional(); StalePracticeCannotClaim(); LegacyCannotBypass();
        Console.WriteLine("Daily mission policy tests passed: " + checks + " assertions.");
    }
    static void IndependentDailySlots()
    {
        foreach (bool completedPlant in new[] {false, true})
        {
            var clock = new DailyClock(); var store = new DailyStore();
            using (var service = Open(store, clock))
            {
                Check(service.SelectMbctProgram("8w"), "Choose course");
                if (completedPlant) Check(service.SetPlantCompleted(service.Snapshot.PlantAppearance.Recipe.InstanceId, true), "Synthetic completed plant");
                Check(!FocusMissionPolicy.PracticeClaimedToday(service.Snapshot, FocusMissionCatalog.AdditionalRole), "No initial M claim");
                FailUnchanged(store, service, () => service.BeginFocusSession("m", "M03"), "Failed selection");
                Select(service,"m"); Ready(service,"m");
                FailUnchanged(store, service, () => service.CommitPracticeGrowth("m"), "Failed practice end");
                Check(FocusMissionPolicy.AdditionalAvailability(service.Snapshot) == null, "Failed growth does not consume M slot");
                Check(service.CommitPracticeGrowth("m"), "Retry commits M growth");
                var receipt = service.GetPracticeGrowth("m");
                Check(receipt.Outcome == PracticeGrowthOutcome.Grown && service.Snapshot.StoredGrowthSteps==0
                    && (!completedPlant || (service.Snapshot.PlantPhotos.Count==1 && receipt.PlantInstanceId!=service.Snapshot.PlantPhotos[0].SourcePlantInstanceId)),
                    "Growing and automatically replaced plants each consume one daily slot without new bank credits");
                Check(FocusMissionPolicy.PracticeClaimedToday(service.Snapshot, FocusMissionCatalog.AdditionalRole)
                    && FocusMissionPolicy.TodayPractice(service.Snapshot, FocusMissionCatalog.AdditionalRole).SessionId == "m"
                    && !FocusMissionPolicy.AdditionalCompletedToday(service.Snapshot), "Practice claim is visible before optional final completion");
                string after = StateCodec.Encode(service.Snapshot); int saves = store.Saves;
                store.Fail = true; Check(service.CommitPracticeGrowth("m") && store.Saves == saves, "Exact growth retry acknowledges without a write even on failing store"); store.Fail = false;
                Check(StateCodec.Encode(service.Snapshot) == after, "Duplicate growth is byte-identical");
                Check(!service.BeginFocusSession("another-m", "M01"), "Other M denied after practice growth");
                Select(service, "p", "P01", 1); Ready(service,"p"); Check(service.CommitPracticeGrowth("p"), "P allowance independent of M");
                FailUnchanged(store, service, () => service.CompleteFocusSession("m"), "Failed reflection completion");
                Check(service.CompleteFocusSession("m") && service.CompleteFocusSession("m"), "Own final completion and retry allowed despite M quota");
                Check(service.CompleteFocusSession("p"), "Own P final completion allowed despite P quota");
                Check(!service.BeginFocusSession("m-after-final", "M02") && !service.BeginFocusSession("p-after-final", "P02", 2), "Both roles reject second normal mission");
                Check(FocusMissionPolicy.AdditionalCompletedToday(service.Snapshot,"M03") && !FocusMissionPolicy.AdditionalCompletedToday(service.Snapshot,"M01"), "Historical per-M final-completion helper retains its meaning");
                Check(FocusMissionPolicy.HasPendingToday(service.Snapshot) && DailyEmotionPolicy.Availability(service.Snapshot)==null && service.CoinBalance == 237,
                    "Both finished P/M slots unlock the pending third mission with exactly two 50-coin rewards");
                var recipe = PlantCodec.Encode(service.Snapshot.PlantAppearance.Recipe); int bank = service.Snapshot.StoredGrowthSteps;
                Check(service.BeginFocusSession("review","M03",0,true),"Explicit historical review remains eligible for record-only replay");
                Ready(service,"review"); Check(service.CommitPracticeGrowth("review") && service.CompleteFocusSession("review"),"Complete explicit review");
                Check(service.GetPracticeGrowth("review").Outcome==PracticeGrowthOutcome.Ineligible && service.Snapshot.StoredGrowthSteps==bank
                    && PlantCodec.Encode(service.Snapshot.PlantAppearance.Recipe)==recipe,"Review changes neither growth nor stored growth");
            }
        }
    }
    static void CarrySelectedAndStarted()
    {
        foreach (string code in new[] {"M03", "P01"}) foreach (bool started in new[] {false,true})
        {
            var clock = new DailyClock { Time = DateTimeOffset.Parse("2026-11-09T23:59:00Z") }; var store = new DailyStore();
            var service = Open(store,clock); Check(service.SelectMbctProgram("8w"),"Choose carry-over course");
            Select(service,"carry",code,code[0]=='P'?1:0);
            string draft=FocusMissionCatalog.Find(code).Steps[0].Options.FirstOrDefault()??"";
            if(started) { Check(service.StartSession("carry"),"Start carry-over"); Check(service.SaveFocusDraft("carry",0,draft),"Save carry-over draft: "+service.LastError); Check(service.PauseSession("carry"),"Pause carry-over draft"); }
            string selected=Get(service,"carry").SelectedUtc; string cycle=service.CurrentCycleId;
            Advance(service,clock,"2026-11-10T00:01:00Z"); Check(service.CurrentCycleId==cycle,"Midnight does not reset the 04:00 business day");
            Advance(service,clock,"2026-11-10T03:59:00Z"); Check(service.CurrentCycleId==cycle,"03:59 still belongs to previous business day");
            Advance(service,clock,"2026-11-10T04:01:00Z"); Check(service.CurrentCycleId!=cycle,"04:00 starts new business day");
            service.Dispose(); service=Open(store,clock);
            Check(Get(service,"carry").SelectedUtc==selected && Get(service,"carry").InstanceId==cycle
                && Get(service,"carry").Status==(started?ParticipationState.Paused:ParticipationState.Selected),"Unfinished identity, original selection date and paused/selected state survive rollover/reopen");
            if(started) Check(Get(service,"carry").FocusStepRecords[0].Value==draft,"Unfinished answer is retained");
            Check(!service.BeginFocusSession("replace",code=="M03"?"M01":"P01",code[0]=='P'?1:0),"Unfinished role must resume instead of replacement");
            if(started)Check(service.ResumeSession("carry"),"Resume persisted paused practice");
            Ready(service,"carry"); FailUnchanged(store,service,()=>service.CommitPracticeGrowth("carry"),"Carried practice failure");
            Check(service.CommitPracticeGrowth("carry") && service.GetPracticeGrowth("carry").CycleId==service.CurrentCycleId
                && Get(service,"carry").InstanceId==service.CurrentCycleId,"First practice commit consumes the actual new business day");
            Check(service.CompleteFocusSession("carry") && service.Snapshot.PracticeGrowthRecords.Count==1 && service.CoinBalance==187,"Carried practice commits once with one 50-coin completion reward");
            service.Dispose();
        }
    }
    static void ReflectionRetainsPracticeDay()
    {
        var clock=new DailyClock {Time=DateTimeOffset.Parse("2026-11-10T03:59:00Z")}; var store=new DailyStore();
        using(var service=Open(store,clock))
        {
            Select(service,"old","M03"); Ready(service,"old"); Check(service.CommitPracticeGrowth("old"),"Practice before reset");
            string oldCycle=service.CurrentCycleId; string recipe=PlantCodec.Encode(service.Snapshot.PlantAppearance.Recipe);
            Advance(service,clock,"2026-11-10T04:01:00Z");
            Check(!FocusMissionPolicy.PracticeClaimedToday(service.Snapshot,FocusMissionCatalog.AdditionalRole),"Yesterday's practice does not claim today");
            FailUnchanged(store,service,()=>service.CompleteFocusSession("old"),"Late reflection failure");
            Check(service.CompleteFocusSession("old") && Get(service,"old").InstanceId==oldCycle && service.GetPracticeGrowth("old").CycleId==oldCycle
                && PlantCodec.Encode(service.Snapshot.PlantAppearance.Recipe)==recipe,"Late final completion retains original practice day and plant");
            Select(service,"today","M01"); Ready(service,"today"); Check(service.CommitPracticeGrowth("today") && service.CompleteFocusSession("today"),"New business day opens another M");
            Check(service.Snapshot.PracticeGrowthRecords.Count==2 && service.CoinBalance==237,"Two distinct days produce exactly two growth receipts");
        }
    }
    static void BoundaryFailure()
    {
        var clock=new DailyClock{Time=DateTimeOffset.Parse("2026-11-10T03:59:00Z")};var store=new DailyStore();
        using(var service=Open(store,clock))
        {
            Select(service,"boundary","M03");Ready(service,"boundary");string previous=service.Snapshot.LastCycleId;
            clock.Time=DateTimeOffset.Parse("2026-11-10T04:01:00Z");
            FailUnchanged(store,service,()=>service.CommitPracticeGrowth("boundary"),"Boundary growth save failure");
            Check(service.Snapshot.LastCycleId==previous && Get(service,"boundary").InstanceId==previous,"Failed rollover growth leaves both durable daily cycle and draft attribution unchanged");
            Check(service.CommitPracticeGrowth("boundary") && service.Snapshot.LastCycleId=="2026-11-10"
                && Get(service,"boundary").InstanceId=="2026-11-10" && service.GetPracticeGrowth("boundary").CycleId=="2026-11-10",
                "Retry atomically advances business cycle, session attribution and one growth receipt");
            string after=StateCodec.Encode(service.Snapshot);
            Check(service.CommitPracticeGrowth("boundary")&&StateCodec.Encode(service.Snapshot)==after,"Boundary retry cannot repeat growth or redraw daily state");
        }
    }
    static void HistoricMultipleAdditional()
    {
        // Construct a valid pre-policy state using independent old receipts; never overwrite user saves.
        var clock=new DailyClock(); var store=new DailyStore(); GardenState historical;
        using(var service=Open(store,clock))
        {
            Select(service,"historical-m03","M03"); Ready(service,"historical-m03"); Check(service.CompleteFocusSession("historical-m03"),"Seed first historical completion");
            historical=service.Snapshot;
        }
        var otherStore=new DailyStore();
        using(var other=Open(otherStore,clock))
        {
            Select(other,"historical-m01","M01"); Ready(other,"historical-m01"); Check(other.CompleteFocusSession("historical-m01"),"Seed second historical completion");
            var source=other.Snapshot;
            historical.Sessions.Add(source.Sessions.Single());
            historical.RewardReceipts.AddRange(source.RewardReceipts.Where(r=>!historical.RewardReceipts.Contains(r)));
            // Pre-growth saves legitimately have completed reward receipts without the later growth ledger.
        }
        GardenPlantVisuals.Sync(historical); StateCodec.Validate(historical); store.Save(historical); string before=StateCodec.Encode(historical);
        using(var service=Open(store,clock))
        {
            Check(StateCodec.Encode(service.Snapshot)==before && service.Snapshot.Sessions.Count==2,"Historical multiple-M records load and round-trip without rewriting");
            Check(FocusMissionPolicy.AdditionalCompletedToday(service.Snapshot,"M01") && FocusMissionPolicy.AdditionalCompletedToday(service.Snapshot,"M03"),"Both historical per-M records remain available");
            Check(!service.BeginFocusSession("new-m","M02"),"Historical multi-M day blocks further new M");
            Check(service.CompleteFocusSession("historical-m01") && StateCodec.Encode(service.Snapshot)==before,"Old completed command retry neither retroactively grows nor erases history");
        }
    }
    static void LegacyCannotBypass()
    {
        foreach(bool legacyFirst in new[]{false,true})
        {
            var clock=new DailyClock(); var store=new DailyStore();
            using(var service=Open(store,clock))
            {
                if(legacyFirst) Check(service.BeginSession("old","free-mission:M03") && service.StartSession("old") && service.CompleteSession("old"),"Complete old additional path");
                else {Select(service,"new","M01");Ready(service,"new");Check(service.CompleteFocusSession("new"),"Complete new additional path");}
                string before=StateCodec.Encode(service.Snapshot);
                Check(!service.BeginSession("old-bypass","free-mission:M02") && !service.BeginFocusSession("new-bypass","M04")
                    && StateCodec.Encode(service.Snapshot)==before,"Legacy and Focus entry points share one M allowance");
                clock.Time=clock.Time.AddDays(1);
                Check(service.BeginSession("new-day-old","free-mission:M02"),"Legacy BeginSession refreshes its own day before checking quota");
            }
        }
    }
    static void StalePracticeCannotClaim()
    {
        var clock=new DailyClock(); var completedStore=new DailyStore(); GardenState historical;
        using(var first=Open(completedStore,clock))
        {Select(first,"finished","M03");Ready(first,"finished");Check(first.CompleteFocusSession("finished"),"Seed completed M for historical active fixture");historical=first.Snapshot;}
        using(var second=Open(new DailyStore(),clock))
        {Select(second,"stale","M01");Ready(second,"stale");historical.Sessions.Add(second.Snapshot.Sessions.Single());}
        StateCodec.Validate(historical); completedStore.Save(historical);
        using(var service=Open(completedStore,clock))
        {
            string before=StateCodec.Encode(service.Snapshot);
            Check(!service.CommitPracticeGrowth("stale") && !service.CompleteFocusSession("stale")
                && StateCodec.Encode(service.Snapshot)==before,"Historical already-active second M cannot bypass the slot at practice or final fallback");
            clock.Time=clock.Time.AddDays(1);Check(service.CommitPracticeGrowth("stale") && service.CompleteFocusSession("stale"),"Blocked old active M remains resumable on the next business day");
        }
    }
}
