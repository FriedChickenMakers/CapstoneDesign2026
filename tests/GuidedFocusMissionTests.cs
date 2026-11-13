using System;
using System.IO;
using System.Linq;
using CapstoneDesign.Plants;
using CapstoneDesign.Runtime.LocalState;

sealed class GuidedClock : IClock
{
    public DateTimeOffset Time = DateTimeOffset.Parse("2026-11-09T03:00:00Z");
    public DateTimeOffset UtcNow => Time;
    public TimeZoneInfo TimeZone => TimeZoneInfo.Utc;
}
sealed class GuidedStore : IStateStore
{
    public GardenState State;
    public bool Fail;
    public int Saves;
    public GardenState Load() => State == null ? null : StateCodec.Clone(State);
    public void Save(GardenState candidate)
    {
        if (Fail) throw new IOException("Synthetic guided-practice storage failure");
        State = StateCodec.Clone(candidate); Saves++;
    }
}
static class GuidedFocusMissionTests
{
    static int assertions;
    static void Check(bool value, string message) { assertions++; if (!value) throw new Exception(message); }
    static GardenStateService Open(IStateStore store, GuidedClock clock, LegacySnapshot legacy = null)
    {
        var service = new GardenStateService(store, clock, new DemoBalanceConfig { VisitorChancePercent = 0 }, legacy);
        Check(service.Open(), "Open synthetic state: " + service.LastError); return service;
    }
    static ActivitySession Get(GardenStateService service, string id) => service.Snapshot.Sessions.Single(s => s.SessionId == id);
    static string Encode(GardenStateService service) => StateCodec.Encode(service.Snapshot);
    static void FailedUnchanged(GuidedStore store, GardenStateService service, Func<bool> operation, string label)
    {
        string before = Encode(service), durable = StateCodec.Encode(store.State); int saves = store.Saves;
        store.Fail = true;
        try { Check(!operation(), label + " surfaces storage failure"); }
        finally { store.Fail = false; }
        Check(Encode(service) == before && StateCodec.Encode(store.State) == durable && store.Saves == saves,
            label + " preserves the entire live and durable state");
    }
    static void NextDay(GardenStateService service, GuidedClock clock)
    {
        do { clock.Time = clock.Time.AddDays(1); Check(service.RefreshCycle(Array.Empty<string>()), "Advance synthetic day"); }
        while (FocusMissionPolicy.Availability(service.Snapshot, "8w") != null);
    }
    static void Main()
    {
        CatalogCompatibility(); SevenGuidedMissions(); LegacyVersionTwoResume();
        Console.WriteLine("Guided focus mission tests passed: " + assertions + " assertions.");
    }
    static void CatalogCompatibility()
    {
        Check(FocusMissionCatalog.Version == 5, "New catalog versions purpose/allowance contracts while preserving guided v4");
        Check(FocusMissionCatalog.All.Length == 45, "All 28 basic and 17 additional identities retained");
        foreach (string code in Enumerable.Range(1, 7).Select(n => "P" + n.ToString("00")))
        {
            var current = FocusMissionCatalog.Find(code, FocusMissionCatalog.Version); var previous = FocusMissionCatalog.Find(code, 2);
            Check(current != null && previous != null && current.Id == previous.Id, "Old and new stable mission identity " + code);
            Check(current.Steps.Length == 2 && current.Steps[0].Key == "practice" && current.Steps[1].Key == "notice",
                "Two durable guided milestones " + code);
            Check(current.Steps.All(s => !s.RequiredAnswer), "Reflection remains optional " + code);
            Check(previous.Steps.Length > 2, "Version 2 retains its original multi-step guidance " + code);
        }
        foreach (var current in FocusMissionCatalog.All.Where(m => !Enumerable.Range(1, 7).Any(i => m.Code == "P" + i.ToString("00"))))
        {
            var previous = FocusMissionCatalog.Find(current.Code, 2);
            var incoming = FocusMissionCatalog.Find(current.Code, 4);
            Check(previous != null && incoming.Steps.Length == previous.Steps.Length
                && incoming.Steps.Select(s => s.Key + "|" + s.Body).SequenceEqual(previous.Steps.Select(s => s.Key + "|" + s.Body)),
                "Historical incoming P08-P28 and additional guidance unchanged: " + current.Code);
            Check(current.Steps.Select(s=>s.Key).SequenceEqual(incoming.Steps.Select(s=>s.Key)),"Current purpose changes retain guided step indexes: "+current.Code);
        }
    }
    static void SevenGuidedMissions()
    {
        var clock = new GuidedClock(); var store = new GuidedStore();
        var service = Open(store, clock, new LegacySnapshot { Nutrient = 137 });
        try
        {
            Check(service.SelectMbctProgram("8w"), service.LastError);
            for (int order = 1; order <= 7; order++)
            {
                if (order > 1) NextDay(service, clock);
                string id = "synthetic-guided-" + order, code = "P" + order.ToString("00");
                var definition = FocusMissionCatalog.Find(code, FocusMissionCatalog.Version);
                string choice = definition.Steps[0].Options.LastOrDefault() ?? "";
                string reflection = order % 2 == 0 ? "" : definition.Steps[1].Options.LastOrDefault() ?? "synthetic observation " + order;
                Check(service.BeginFocusSession(id, code, order, program: "8w"), "Begin " + code + ": " + service.LastError);
                Check(Get(service, id).FocusVersion == FocusMissionCatalog.Version && Get(service, id).Status == ParticipationState.Selected
                    && FocusMissionCatalog.IsGuided(Get(service, id)), "Version 4 begins as selected guided practice " + code);
                string beforeStart = Encode(service);
                Check(!service.CompleteGuidedFocusPractice(id, choice, reflection, 3) && Encode(service) == beforeStart,
                    "Finish cannot bypass starting the practice " + code);
                FailedUnchanged(store, service, () => service.SaveGuidedFocusDraft(id, choice, "", 0), "Selected choice draft " + code);
                Check(service.SaveGuidedFocusDraft(id, choice, "", 0), "Save selected draft " + code + ": " + service.LastError);
                FailedUnchanged(store, service, () => service.StartGuidedFocusSession(id, choice), "Start " + code);
                Check(service.StartGuidedFocusSession(id, choice), "Start " + code + ": " + service.LastError);
                Check(Get(service, id).Status == ParticipationState.InProgress, "Started state " + code);
                string started = Encode(service); int startSaves = store.Saves;
                store.Fail = true;
                try { Check(service.StartGuidedFocusSession(id, "stale choice"), "Duplicate start acknowledges its previous success " + code); }
                finally { store.Fail = false; }
                Check(Encode(service) == started && store.Saves == startSaves, "Duplicate start never overwrites stored choice " + code);
                Check(!service.SaveGuidedFocusDraft(id, choice, reflection, -1)
                    && !service.SaveGuidedFocusDraft(id, choice, reflection, 86401)
                    && !service.SaveGuidedFocusDraft(id, choice, new string('x', 501), 0) && Encode(service) == started,
                    "Invalid elapsed/oversized reflection leaves full state unchanged " + code);
                FailedUnchanged(store, service, () => service.SaveGuidedFocusDraft(id, choice, reflection, 17), "Practice draft " + code);
                Check(service.SaveGuidedFocusDraft(id, choice, reflection, 17), "Save practice choice, optional response and time " + code + ": " + service.LastError);
                string draft = Encode(service); service.Dispose(); service = Open(store, clock);
                Check(Encode(service) == draft, "Exact restart restores draft " + code);
                var restored = Get(service, id);
                Check(restored.FocusStepRecords[0].Value == choice && restored.FocusStepRecords[1].Value == reflection,
                    "Independent choice and observation restored " + code);
                Check(restored.FocusStepRecords[0].ElapsedSeconds == 17, "Elapsed practice time restored " + code);
                int coins = service.CoinBalance, step = service.Snapshot.PlantAppearance.Recipe.GrowthStep;
                string instance = service.Snapshot.PlantAppearance.Recipe.InstanceId;
                FailedUnchanged(store, service, () => service.CompleteGuidedFocusPractice(id, choice, reflection, 23), "Atomic practice finish " + code);
                Check(service.GetPracticeGrowth(id) == null, "Failed finish cannot leave a growth receipt " + code);
                Check(service.CompleteGuidedFocusPractice(id, choice, reflection, 23), "Retry practice finish " + code + ": " + service.LastError);
                var finished = Get(service, id); var grown = service.Snapshot;
                Check(finished.FocusStepRecords.All(r => !string.IsNullOrEmpty(r.ConfirmedUtc)) && finished.InstructionStep == 2,
                    "Practice and observation milestones commit together " + code);
                Check(finished.FocusStepRecords[0].ElapsedSeconds == 23 && finished.FocusStepRecords[1].Value == reflection,
                    "Latest elapsed time and optional observation committed " + code);
                Check(grown.PlantAppearance.Recipe.GrowthStep == step && service.GetPracticeGrowth(id) == null,
                    "Practice confirmation waits for the existing separate growth transaction " + code);
                string committed = Encode(service); int saves = store.Saves;
                store.Fail = true;
                try { Check(service.CompleteGuidedFocusPractice(id, choice, "must not replace", 900), "Successful finish retry remains acknowledgeable during storage failure " + code); }
                finally { store.Fail = false; }
                Check(Encode(service) == committed && store.Saves == saves, "Duplicate finish neither rewrites response nor grows/spends again " + code);
                FailedUnchanged(store, service, () => service.CommitPracticeGrowth(id), "Post-practice growth " + code);
                Check(Get(service, id).InstructionStep == 2, "Growth failure retains finished practice for retry " + code);
                Check(service.CommitPracticeGrowth(id), "Retry existing growth transaction " + code + ": " + service.LastError);
                Check(service.Snapshot.PlantAppearance.Recipe.GrowthStep == step + 1 && service.Snapshot.PlantAppearance.Recipe.InstanceId == instance
                    && service.GetPracticeGrowth(id) != null && service.CoinBalance == coins,
                    "Exactly one current-plant stage and no coin reward " + code);
                committed = Encode(service);
                service.Dispose(); service = Open(store, clock);
                Check(Encode(service) == committed, "Finished practice restores exactly before common reflection " + code);
                Check(service.MoveFocusStep(id, 0), "Explicit back permits revisiting guided practice " + code);
                Check(service.SaveGuidedFocusDraft(id, choice, "", 10), "Revisit may clear optional observation " + code);
                Check(Get(service, id).FocusStepRecords[0].ElapsedSeconds == 23, "Elapsed time never decreases on revisit " + code);
                Check(service.CompleteGuidedFocusPractice(id, choice, "", 25) && service.CommitPracticeGrowth(id), "Reconfirm revisited practice " + code);
                Check(service.Snapshot.PlantAppearance.Recipe.GrowthStep == step + 1 && Get(service,id).FocusStepRecords[1].Value == "",
                    "Revisit changes only intended observation, not growth " + code);
                Check(service.SaveFocusReflection(id, "", "synthetic final note", 1), "Common optional reflection " + code);
                Check(service.CompleteFocusSession(id, "", "synthetic final note"), "Final mission completion " + code + ": " + service.LastError);
                Check(service.Snapshot.PlantAppearance.Recipe.GrowthStep == step + 1 && service.CoinBalance == coins + 50
                    && service.Snapshot.PracticeGrowthRecords.Count(r => r.SessionId == id) == 1,
                    "Final completion does not apply a second growth and pays exactly 50 coins " + code);
            }
        }
        finally { service.Dispose(); }
    }
    static void LegacyVersionTwoResume()
    {
        var clock = new GuidedClock(); var store = new GuidedStore();
        using (var service = Open(store, clock, new LegacySnapshot { Nutrient = 42 }))
        {
            Check(service.SelectMbctProgram("8w"), "Choose synthetic old course");
            Check(service.BeginFocusSession("synthetic-v2", "P01", 1, program: "8w"), "Prepare compatible occurrence");
            var state = service.Snapshot; var session = state.Sessions.Single(); var definition = FocusMissionCatalog.Find("P01", 2);
            session.FocusVersion = 2; session.MissionTitleSnapshot = definition.Title; session.MissionDescriptionSnapshot = definition.Overview;
            session.FocusStepRecords = definition.Steps.Select((s, i) => new FocusStepRecord { Step = i, Key = s.Key, Title = s.Title, Prompt = s.Body }).ToList();
            store.State = StateCodec.Clone(state);
        }
        using (var service = Open(store, clock))
        {
            string unchanged = Encode(service);
            Check(Get(service, "synthetic-v2").FocusVersion == 2 && FocusMissionCatalog.FindForSession(Get(service, "synthetic-v2")).Steps.Length > 2,
                "Old saved session retains v2 guidance and step indexes");
            Check(!service.StartGuidedFocusSession("synthetic-v2", "") && !service.SaveGuidedFocusDraft("synthetic-v2", "", "", 0)
                && !service.CompleteGuidedFocusPractice("synthetic-v2", "", "", 0) && Encode(service) == unchanged,
                "Guided APIs cannot reinterpret old saved sessions");
            Check(service.StartSession("synthetic-v2"), "Old session starts through original API");
            var definition = FocusMissionCatalog.FindForSession(Get(service, "synthetic-v2"));
            Check(service.ConfirmFocusStep("synthetic-v2", 0, "", true) && service.PauseSession("synthetic-v2"), "Persist an actually paused old multi-step session");
            string paused = Encode(service);
            using (var reopened = Open(store, clock))
            {
                Check(Encode(reopened) == paused && Get(reopened,"synthetic-v2").InstructionStep == 1,
                    "Restart preserves old paused cursor, original prompts and confirmed step timestamps exactly");
                Check(reopened.ResumeSession("synthetic-v2"), "Resume old saved progress");
                for (int i = 1; i < definition.Steps.Length; i++)
                    Check(reopened.ConfirmFocusStep("synthetic-v2", i, "", true), "Old step remains confirmable " + i + ": " + reopened.LastError);
                Check(reopened.CommitPracticeGrowth("synthetic-v2") && reopened.CompleteFocusSession("synthetic-v2"), "Old flow still completes");
                Check(reopened.CoinBalance == 92 && reopened.Snapshot.PracticeGrowthRecords.Count == 1, "Old unfinished flow completes with one stage and 50 coins");
            }
        }
    }
}
