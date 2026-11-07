using System;
using System.IO;
using System.Linq;
using System.Xml;
using CapstoneDesign.Plants;
using CapstoneDesign.Runtime.LocalState;

// Synthetic data only. These operation-contract tests deliberately distinguish
// finishing the practice from the optional reflection/final completion command.
sealed class PracticeClock : IClock
{
    public DateTimeOffset Time = DateTimeOffset.Parse("2026-11-05T12:00:00Z");
    public DateTimeOffset UtcNow => Time;
    public TimeZoneInfo TimeZone => TimeZoneInfo.Utc;
}
sealed class PracticeStore : IStateStore
{
    public GardenState State;
    public bool Fail;
    public int Saves;
    public GardenState Load() => State == null ? null : StateCodec.Clone(State);
    public void Save(GardenState candidate)
    {
        if (Fail) throw new IOException("Synthetic practice-growth storage failure");
        State = StateCodec.Clone(candidate); Saves++;
    }
}
static class MissionPracticeGrowthTests
{
    static int checks;
    static void Assert(bool value, string message) { checks++; if (!value) throw new Exception(message); }
    static GardenStateService Open(IStateStore store, PracticeClock clock, LegacySnapshot legacy = null)
    {
        var service = new GardenStateService(store, clock, new DemoBalanceConfig { VisitorChancePercent = 0 }, legacy);
        Assert(service.Open(), "Open synthetic state: " + service.LastError);
        return service;
    }
    static void CoinCompatibility()
    {
        var clock = new PracticeClock(); var store = new PracticeStore();
        using (var service = Open(store, clock, new LegacySnapshot { Nutrient = 137, GardenXp = 19, Growth = .2f }))
        {
            var state = service.Snapshot;
            Assert(state.Coins == 137 && service.CoinBalance == 137, "Existing nutrient balance becomes exactly the same coin balance");
            Assert(state.GardenXp == 19 && state.LegacyGrowth == .2f, "Currency rename preserves unrelated legacy progression");
            string xml = StateCodec.Encode(state);
            var document = new XmlDocument(); document.LoadXml(xml);
            Assert(document.DocumentElement["Nutrient"] != null && document.DocumentElement["Nutrient"].InnerText == "137"
                && document.DocumentElement["Coins"] == null, "One compatible persisted wallet field; no competing coin wallet");
            var detached = StateCodec.Decode(xml); detached.Coins = 901;
            Assert(detached.Nutrient == 901 && state.Coins == 137 && service.CoinBalance == 137,
                "Coin alias mutates only its detached snapshot and shares the legacy wallet");
            Assert(StateCodec.Encode(StateCodec.Decode(xml)) == xml, "Old wallet XML round-trips without loss or an additional balance");
        }
        using (var reopened = Open(store, clock, new LegacySnapshot { Nutrient = 999 }))
            Assert(reopened.Snapshot.Coins == 137 && reopened.Snapshot.GardenXp == 19, "Reopen never reimports or converts the balance twice");
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        using (var service = Open(store, clock))
        {
            string before = StateCodec.Encode(service.Snapshot); int saves = store.Saves;
            store.Fail = true;
            Assert(!service.AddTestCoins() && StateCodec.Encode(service.Snapshot) == before && StateCodec.Encode(store.State) == before
                && store.Saves == saves, "Failed test coin grant changes neither wallet nor any other persisted field");
            store.Fail = false;
            for (int click = 1; click <= 3; click++)
            {
                Assert(service.AddTestCoins() && service.CoinBalance == 137 + 100 * click && store.Saves == saves + click,
                    "Each deliberate test coin click, including retry, adds exactly 100 in one transaction");
                var copy = service.Snapshot; copy.Coins = 137;
                Assert(StateCodec.Encode(copy) == before, "Test coin grant changes no plant, history, record or reward receipt");
            }
        }
#else
        Assert(typeof(GardenStateService).GetMethod("AddTestCoins") == null
            && typeof(GardenStateService).GetMethod("AddTestNutrients") == null,
            "Both current and compatibility development grants are absent from release builds");
#endif
    }
    static ActivitySession Session(GardenStateService service, string id) => service.Snapshot.Sessions.Single(s => s.SessionId == id);
    static void ReadyPractice(GardenStateService service, string id, string code = "M03", bool review = false)
    {
        Assert(service.RefreshCycle(Array.Empty<string>()), "Refresh synthetic practice date");
        Assert(service.BeginFocusSession(id, code, 0, review), "Select practice: " + service.LastError);
        Assert(service.StartSession(id), "Start practice");
        var definition = FocusMissionCatalog.FindForSession(Session(service, id));
        for (int step = 0; step < definition.Steps.Length; step++)
        {
            var item = definition.Steps[step];
            string value = item.Kind == "draw" ? "DRAW1|AA_B" : item.RequiredAnswer ? "synthetic input" : null;
            Assert(service.ConfirmFocusStep(id, step, value, value == null), "Confirm actual practice step: " + service.LastError);
        }
    }
    static void AtomicPracticeAndReflection()
    {
        var store = new PracticeStore(); var clock = new PracticeClock();
        using (var service = Open(store, clock, new LegacySnapshot { Nutrient = 137 }))
        {
            Assert(service.RefreshCycle(Array.Empty<string>()) && service.BeginFocusSession("unready", "M03")
                && service.StartSession("unready"), "Prepare unfinished practice");
            string unfinished = StateCodec.Encode(service.Snapshot);
            Assert(!service.CommitPracticeGrowth("unready") && StateCodec.Encode(service.Snapshot) == unfinished,
                "Practice growth cannot bypass unfinished practice steps");
            var definition = FocusMissionCatalog.FindForSession(Session(service, "unready"));
            for (int step = 0; step < definition.Steps.Length; step++)
                Assert(service.ConfirmFocusStep("unready", step, null, true), "Confirm unfinished fixture step");
            var before = service.Snapshot; string encoded = StateCodec.Encode(before);
            store.Fail = true;
            Assert(!service.CommitPracticeGrowth("unready") && !string.IsNullOrEmpty(service.LastError), "Practice-end save failure is surfaced");
            Assert(StateCodec.Encode(service.Snapshot) == encoded && StateCodec.Encode(store.State) == encoded,
                "Failed practice-end save preserves the old stage, wallet, session, appearance and receipt atomically");
            store.Fail = false;
            Assert(service.CommitPracticeGrowth("unready"), "Practice-end save retry succeeds: " + service.LastError);
            var after = service.Snapshot; var receipt = service.GetPracticeGrowth("unready");
            Assert(receipt != null && receipt.Outcome == PracticeGrowthOutcome.Grown && receipt.BeforeStep == before.PlantAppearance.Recipe.GrowthStep
                && receipt.AfterStep == receipt.BeforeStep + 1 && receipt.AfterStep == after.PlantAppearance.Recipe.GrowthStep,
                "Successful practice end advances exactly one procedural stage before reflection");
            Assert(after.Coins == 137 && Session(service, "unready").Status == ParticipationState.InProgress
                && after.PracticeGrowthRecords.Count == 1 && after.PurchaseReceipts.Count == 0,
                "Practice growth charges no coins and remains separate from optional reflection and final completion");
            Assert(receipt.PlantInstanceId == before.PlantAppearance.Recipe.InstanceId
                && after.PlantAppearance.Recipe.InstanceId == before.PlantAppearance.Recipe.InstanceId
                && after.PlantAppearance.Recipe.BaseSeed == before.PlantAppearance.Recipe.BaseSeed,
                "Growth targets the same individual and never rerolls the seed");
            string committed = StateCodec.Encode(after); receipt.AfterStep = 999;
            Assert(service.GetPracticeGrowth("unready").AfterStep == before.PlantAppearance.Recipe.GrowthStep + 1,
                "Returned practice receipts are detached from persisted state");
            Assert(service.CommitPracticeGrowth("unready") && StateCodec.Encode(service.Snapshot) == committed,
                "Duplicate practice-end command never grows or charges again");
            using (var reopened = Open(store, clock))
                Assert(reopened.CommitPracticeGrowth("unready") && StateCodec.Encode(reopened.Snapshot) == committed,
                    "Abandoning reflection and restarting never replays or duplicates the committed growth");
            Assert(service.SaveFocusReflection("unready", "평온함", "synthetic reflection", 0)
                && service.SaveFocusReflection("unready", "평온함", "synthetic reflection", 1), "Optional reflection saves after growth");
            string recipe = PlantCodec.Encode(service.Snapshot.PlantAppearance.Recipe); int records = service.Snapshot.PracticeGrowthRecords.Count;
            store.Fail = true;
            Assert(!service.CompleteFocusSession("unready", "평온함", "synthetic reflection"), "Final reflection failure remains retryable");
            Assert(PlantCodec.Encode(service.Snapshot.PlantAppearance.Recipe) == recipe && service.CoinBalance == 137
                && service.Snapshot.PracticeGrowthRecords.Count == records, "Failed final completion does not undo or reissue earlier growth");
            store.Fail = false;
            Assert(service.CompleteFocusSession("unready", "평온함", "synthetic reflection") && service.CompleteFocusSession("unready"),
                "Final completion and duplicate final completion succeed");
            Assert(PlantCodec.Encode(service.Snapshot.PlantAppearance.Recipe) == recipe && service.CoinBalance == 187
                && service.Snapshot.PracticeGrowthRecords.Count == records && Session(service, "unready").RewardAmount == 50,
                "Final completion gives exactly 50 coins and no second growth and retains the existing reflection");
            Assert(!service.GrowPlant("synthetic-new-paid-command", GardenPlantVisuals.GrowthPlantId)
                && service.CoinBalance == 187 && PlantCodec.Encode(service.Snapshot.PlantAppearance.Recipe) == recipe,
                "A new legacy paid-growth command cannot spend coins or bypass mission growth");
            Assert(!service.BeginFocusSession("repeat", "M03") && !service.BeginFocusSession("other-same-day", "M01")
                && PlantCodec.Encode(service.Snapshot.PlantAppearance.Recipe) == recipe,
                "Same-day normal additional practice is blocked across all M identities without a second growth");
            ReadyPractice(service, "review", review: true);
            Assert(service.CommitPracticeGrowth("review") && service.GetPracticeGrowth("review").Outcome == PracticeGrowthOutcome.Ineligible
                && service.CompleteFocusSession("review") && PlantCodec.Encode(service.Snapshot.PlantAppearance.Recipe) == recipe,
                "Explicit review remains a record-only practice with no growth");
            clock.Time = clock.Time.AddDays(1); ReadyPractice(service, "tomorrow");
            int stage = service.Snapshot.PlantAppearance.Recipe.GrowthStep;
            var calls = Enumerable.Range(0, 16).Select(i => System.Threading.Tasks.Task.Run(() => service.CommitPracticeGrowth("tomorrow"))).ToArray();
            System.Threading.Tasks.Task.WaitAll(calls);
            Assert(calls.All(call => call.Result) && service.Snapshot.PlantAppearance.Recipe.GrowthStep == stage + 1
                && service.Snapshot.PracticeGrowthRecords.Count(r => r.SessionId == "tomorrow") == 1 && service.CoinBalance == 187,
                "Concurrent duplicate practice endings produce one next-day growth and no currency change");
        }
    }
    static void RealFileFailureAndReload()
    {
        string directory = Path.Combine(Path.GetTempPath(), "capstone-practice-growth-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory); string path = Path.Combine(directory, "state.xml"), expected;
        var clock = new PracticeClock();
        try
        {
            using (var file = new FileStateStore(path))
            using (var service = Open(file, clock, new LegacySnapshot { Nutrient = 41 }))
            {
                ReadyPractice(service, "file-practice"); string before = StateCodec.Encode(service.Snapshot), disk = File.ReadAllText(path);
                Directory.CreateDirectory(path + ".tmp");
                try { Assert(!service.CommitPracticeGrowth("file-practice") && StateCodec.Encode(service.Snapshot) == before
                    && File.ReadAllText(path) == disk, "Actual staging-file failure leaves committed file and in-memory practice unchanged"); }
                finally { Directory.Delete(path + ".tmp"); }
                Assert(service.CommitPracticeGrowth("file-practice"), "Actual disk retry commits the practice growth");
                expected = StateCodec.Encode(service.Snapshot);
            }
            using (var file = new FileStateStore(path))
            using (var service = Open(file, clock))
                Assert(StateCodec.Encode(service.Snapshot) == expected && service.CommitPracticeGrowth("file-practice")
                    && StateCodec.Encode(service.Snapshot) == expected && service.CoinBalance == 41,
                    "Actual process-store reopen restores exact stage, receipt and old coin balance without replay");
        }
        finally { Directory.Delete(directory, true); }
    }
    static void LegacyCompletionAndReflectionDate()
    {
        var clock = new PracticeClock();
        var legacy = new GardenState { MigrationCompleted = true, InstallationId = "synthetic-legacy-mission-owner", Coins = 97, LegacyGrowth = .2f };
        legacy.LastCycleId = "2026-11-04";
        legacy.DailyDecisions.Add(new DailyDecision { CycleId = legacy.LastCycleId, TimeZoneId = "UTC", VisitorId = "", AppliedUtc = "2026-11-04T12:00:00Z" });
        legacy.Sessions.Add(new ActivitySession { SessionId = "old-complete", MissionId = "free-mission:M01", InstanceId = "2026-11-04",
            Status = ParticipationState.RewardCommitted, SelectedUtc = "2026-11-04T12:00:00Z", StartedUtc = "2026-11-04T12:00:00Z",
            EndedUtc = "2026-11-04T12:01:00Z", Note = "synthetic old reflection" });
        legacy.RewardReceipts.Add("2026-11-04|free-mission:M01");
        var store = new PracticeStore { State = legacy };
        using (var service = Open(store, clock))
        {
            string before = StateCodec.Encode(service.Snapshot); store.Fail = true;
            Assert(service.CommitPracticeGrowth("old-complete") && StateCodec.Encode(service.Snapshot) == before,
                "Old completed session retry is a read-only acknowledgement even while storage is failing");
            store.Fail = false;
            Assert(service.CompleteSession("old-complete") && StateCodec.Encode(service.Snapshot) == before
                && service.GetPracticeGrowth("old-complete") == null && service.CoinBalance == 97,
                "Opening an old rewarded completion never retroactively grants a growth or changes its wallet, note or receipt");
            ReadyPractice(service, "before-midnight");
            Assert(service.CommitPracticeGrowth("before-midnight"), "Practice commits before the date changes");
            var receipt = service.GetPracticeGrowth("before-midnight"); string recipe = PlantCodec.Encode(service.Snapshot.PlantAppearance.Recipe);
            clock.Time = clock.Time.AddDays(1);
            Assert(service.RefreshCycle(Array.Empty<string>()) && service.SaveFocusReflection("before-midnight", "평온함", "synthetic delayed reflection", 1)
                && service.CompleteFocusSession("before-midnight"), "Reflection can finish the following day");
            Assert(service.GetPracticeGrowth("before-midnight").CycleId == receipt.CycleId
                && Session(service, "before-midnight").InstanceId == receipt.CycleId
                && PlantCodec.Encode(service.Snapshot.PlantAppearance.Recipe) == recipe && service.CoinBalance == 147,
                "Delayed reflection retains the practice's original business day and cannot earn another stage and pays its first final 50 coins");
        }
    }
    static void FinalStage()
    {
        var clock = new PracticeClock(); var seedStore = new PracticeStore(); GardenState seed;
        using (var initial = Open(seedStore, clock, new LegacySnapshot { Nutrient = 23, Growth = .2f })) seed = initial.Snapshot;
        var appearance = seed.PlantAppearance;
        while (appearance.Recipe.GrowthStep < appearance.Recipe.MaxGrowthSteps - 1)
            appearance.Recipe = PlantGenerator.Advance(appearance.Recipe);
        appearance.MissionGrowthBaseStep = appearance.Recipe.GrowthStep;
        appearance.MissionGrowthBaseGrowth = .29f;
        appearance.MissionGrowthVersion = 1;
        appearance.IsCompleted = false;
        var store = new PracticeStore { State = seed };
        using (var service = Open(store, clock))
        {
            ReadyPractice(service, "last-stage"); string before = StateCodec.Encode(service.Snapshot); store.Fail = true;
            Assert(!service.CommitPracticeGrowth("last-stage") && StateCodec.Encode(service.Snapshot) == before,
                "Final-stage save failure leaves the plant incomplete without a receipt or currency change");
            store.Fail = false;
            Assert(service.CommitPracticeGrowth("last-stage") && GardenPlantVisuals.IsCompleted(service.Snapshot)
                && service.Snapshot.PlantAppearance.IsCompleted && service.Snapshot.PlantAppearance.Recipe.Mature
                && service.CoinBalance == 23, "Final-stage retry atomically completes the plant without spending coins");
            string completed = StateCodec.Encode(service.Snapshot);
            Assert(service.CommitPracticeGrowth("last-stage") && StateCodec.Encode(service.Snapshot) == completed,
                "The final practice receipt cannot overgrow a completed plant on retry");
        }
    }
    static void StoredGrowthLifecycle()
    {
        var clock = new PracticeClock(); var store = new PracticeStore();
        using (var service = Open(store, clock, new LegacySnapshot { Nutrient = 71, Growth = .2f }))
        {
            string originalId = service.Snapshot.PlantAppearance.Recipe.InstanceId;
            Assert(service.SetPlantCompleted(originalId, true), "Prepare explicitly completed low-stage individual");
            string originalRecipe = PlantCodec.Encode(service.Snapshot.PlantAppearance.Recipe);
            int maximum = service.Snapshot.PlantAppearance.Recipe.MaxGrowthSteps;
            for (int index = 0; index < maximum + 2; index++)
            {
                if (index > 0) clock.Time = clock.Time.AddDays(1);
                string id = "bank-completed-" + index;
                ReadyPractice(service, id);
                if (index == 0)
                {
                    string before = StateCodec.Encode(service.Snapshot); store.Fail = true;
                    Assert(!service.CommitPracticeGrowth(id) && StateCodec.Encode(service.Snapshot) == before
                        && service.Snapshot.StoredGrowthSteps == 0, "Failed bank credit creates neither stored growth nor a practice receipt");
                    store.Fail = false;
                }
                Assert(service.CommitPracticeGrowth(id) && service.GetPracticeGrowth(id).Outcome == PracticeGrowthOutcome.Stored
                    && service.Snapshot.StoredGrowthSteps == index + 1 && service.CoinBalance == 71 + index * 50
                    && PlantCodec.Encode(service.Snapshot.PlantAppearance.Recipe) == originalRecipe,
                    "Completed current plant stores exactly one earned stage without changing its appearance or coins");
                string credited = StateCodec.Encode(service.Snapshot);
                Assert(service.CommitPracticeGrowth(id) && StateCodec.Encode(service.Snapshot) == credited
                    && service.CompleteFocusSession(id), "Duplicate practice and final reflection do not credit the bank twice");
            }
            Assert(service.SavePlantPhoto("bank-photo-first", originalId) && !GardenPlantVisuals.HasCurrentPlant(service.Snapshot), "Archive completed individual while retaining all stored growth");
            string photos = PlantCodec.Encode(service.Snapshot.PlantPhotos);
            clock.Time = clock.Time.AddDays(1); Assert(service.RefreshCycle(Array.Empty<string>()), "Advance empty garden day");
            string blocked = StateCodec.Encode(service.Snapshot);
            Assert(!service.BeginFocusSession("bank-empty", "M03") && StateCodec.Encode(service.Snapshot) == blocked,
                "Empty garden requires a new seed without consuming a mission slot or modifying stored growth");
            string empty = StateCodec.Encode(service.Snapshot); store.Fail = true;
            Assert(!service.StartNewPlant("bank-new-first", originalId) && StateCodec.Encode(service.Snapshot) == empty
                && StateCodec.Encode(store.State) == empty, "Failed creation preserves all bank credits, empty garden and immutable photos");
            store.Fail = false;
            Assert(service.StartNewPlant("bank-new-first", originalId), "Retry creates a new individual and consumes only its available stages");
            var first = service.Snapshot; string firstId = first.PlantAppearance.Recipe.InstanceId;
            Assert(firstId != originalId && first.PlantAppearance.Recipe.GrowthStep == maximum && GardenPlantVisuals.IsCompleted(first)
                && first.StoredGrowthSteps == 2 && first.PlantStartCommands.Single().AppliedGrowthSteps == maximum
                && first.Coins == 71 + (maximum + 2) * 50 && PlantCodec.Encode(first.PlantPhotos) == photos,
                "New plant applies at most its maximum stage, preserves overflow and records exactly how much it consumed");
            string firstEncoded = StateCodec.Encode(first); store.Fail = true;
            Assert(service.StartNewPlant("bank-new-first", originalId) && service.CommitPracticeGrowth("bank-completed-0")
                && StateCodec.Encode(service.Snapshot) == firstEncoded,
                "Old creation and practice acknowledgements cannot consume overflow or advance the replacement plant even during storage failure");
            store.Fail = false;
            using (var reopened = Open(store, clock))
                Assert(StateCodec.Encode(reopened.Snapshot) == firstEncoded, "Restart restores applied and remaining growth without consuming again");
            Assert(service.SavePlantPhoto("bank-photo-second", firstId) && service.StartNewPlant("bank-new-second", firstId),
                "A second archived plant can pass the remaining earned growth to its successor");
            var second = service.Snapshot;
            Assert(second.PlantAppearance.Recipe.InstanceId != firstId && second.PlantAppearance.Recipe.GrowthStep == 2
                && !GardenPlantVisuals.IsCompleted(second) && second.StoredGrowthSteps == 0
                && second.PlantStartCommands.Last().AppliedGrowthSteps == 2 && second.PlantPhotos.Count == 2 && second.Coins == 71 + (maximum + 2) * 50,
                "Overflow advances the next distinct plant exactly two stages and is consumed once");
            string secondEncoded = StateCodec.Encode(second), frozenPhotos = PlantCodec.Encode(second.PlantPhotos);
            Assert(service.CommitPracticeGrowth("bank-completed-0") && service.StartNewPlant("bank-new-first", originalId)
                && StateCodec.Encode(service.Snapshot) == secondEncoded, "Old receipts remain harmless after the successor is growing again");
            clock.Time = clock.Time.AddDays(1); ReadyPractice(service, "new-current-practice");
            Assert(service.CommitPracticeGrowth("new-current-practice") && service.Snapshot.PlantAppearance.Recipe.GrowthStep == 3
                && service.GetPracticeGrowth("new-current-practice").PlantInstanceId == second.PlantAppearance.Recipe.InstanceId
                && service.Snapshot.StoredGrowthSteps == 0 && service.CoinBalance == 71 + (maximum + 2) * 50
                && PlantCodec.Encode(service.Snapshot.PlantPhotos) == frozenPhotos,
                "Fresh practice advances only the current successor while all archived appearances remain frozen");
            var corrupt = second; corrupt.StoredGrowthSteps++;
            bool rejected = false; try { StateCodec.Validate(corrupt); } catch (InvalidDataException) { rejected = true; }
            Assert(rejected, "Persisted stored-growth balance must reconcile with its earned and consumed receipts");
        }
    }
    static void ReadyBasic(GardenStateService service, string id, string program, int order)
    {
        var definition = FocusMissionPolicy.MissionAt(order, program);
        Assert(service.BeginFocusSession(id, definition.Code, order, program: program) && service.StartSession(id),
            "Begin basic practice " + id + ": " + service.LastError);
        while (Session(service, id).InstructionStep < definition.Steps.Length)
        {
            int index = Session(service, id).InstructionStep; var step = definition.Steps[index];
            string value = step.Options.FirstOrDefault() ?? (step.Kind == "draw" ? "DRAW1|AA_B" : step.RequiredAnswer ? "synthetic input" : null);
            Assert(service.ConfirmFocusStep(id, index, value, value == null), "Confirm basic step: " + service.LastError);
        }
    }
    static void PracticeMilestoneQuota()
    {
        var clock = new PracticeClock { Time = DateTimeOffset.Parse("2026-11-02T12:00:00Z") };
        var store = new PracticeStore();
        using (var service = Open(store, clock, new LegacySnapshot { Nutrient = 83, Growth = .3f }))
        {
            Assert(service.RefreshCycle(Array.Empty<string>()) && service.SelectMbctProgram("2w"), "Choose short course for pending-reflection quota fixture");
            ReadyBasic(service, "quota-monday", "2w", 1);
            Assert(service.CommitPracticeGrowth("quota-monday") && service.GetPracticeGrowth("quota-monday").Outcome == PracticeGrowthOutcome.Stored,
                "A completed plant's basic practice reserves its occurrence while storing growth");
            var pending = new PracticeStore { State = service.Snapshot };
            using (var ownCompletion = Open(pending, clock))
                Assert(ownCompletion.CompleteFocusSession("quota-monday") && ownCompletion.Snapshot.StoredGrowthSteps == 1
                    && ownCompletion.CoinBalance == 133, "A practice can finalize its own reserved occurrence without being blocked or credited twice");
            Assert(service.EndParticipation("quota-monday") && service.SelectMbctProgram("4w"), "Abandon optional reflection and switch course");
            string before = StateCodec.Encode(service.Snapshot);
            Assert(!service.BeginFocusSession("quota-bypass", FocusMissionPolicy.MissionAt(1, "4w").Code, 1, program: "4w")
                && StateCodec.Encode(service.Snapshot) == before, "Course switching after earned practice growth cannot bypass the original one-basic-practice-per-day quota");
            for (int order = 1; order <= 5; order++)
            {
                clock.Time = clock.Time.AddDays(1); Assert(service.RefreshCycle(Array.Empty<string>()), "Advance quota fixture day");
                string id = "quota-four-week-" + order; ReadyBasic(service, id, "4w", order);
                Assert(service.CommitPracticeGrowth(id) && service.CompleteFocusSession(id), "Next-day basic practice remains available: " + service.LastError);
            }
            Assert(service.Snapshot.StoredGrowthSteps == 6 && service.CoinBalance == 333,
                "Six actual practice milestones across courses reserve six weekly slots and store six stages");
            clock.Time = clock.Time.AddDays(1); Assert(service.RefreshCycle(Array.Empty<string>()), "Advance to Sunday");
            Assert(!service.BeginFocusSession("quota-seventh", FocusMissionPolicy.MissionAt(6, "4w").Code, 6, program: "4w"),
                "An ended first practice still counts toward the original six-per-week limit across courses");
            clock.Time = clock.Time.AddDays(1); Assert(service.RefreshCycle(Array.Empty<string>()), "Advance to following Monday");
            ReadyBasic(service, "quota-next-week", "4w", 6);
            Assert(service.CommitPracticeGrowth("quota-next-week") && service.CompleteFocusSession("quota-next-week")
                && service.Snapshot.StoredGrowthSteps == 7 && service.CoinBalance == 383,
                "The next calendar week reopens basic practice without losing earlier stored growth");
        }
    }
    static void Main()
    {
        CoinCompatibility();
        AtomicPracticeAndReflection();
        RealFileFailureAndReload();
        LegacyCompletionAndReflectionDate();
        FinalStage();
        StoredGrowthLifecycle();
        PracticeMilestoneQuota();
        Console.WriteLine("PASS " + checks + " mission-practice growth assertions (synthetic data)");
    }
}
