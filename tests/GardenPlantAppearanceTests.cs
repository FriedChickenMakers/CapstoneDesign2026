using System;
using System.IO;
using System.Linq;
using System.Xml;
using CapstoneDesign.Plants;
using CapstoneDesign.Runtime;
using CapstoneDesign.Runtime.LocalState;

sealed class AppearanceClock : IClock
{
    public DateTimeOffset Now = DateTimeOffset.Parse("2026-01-05T12:00:00Z");
    public DateTimeOffset UtcNow => Now;
    public TimeZoneInfo TimeZone => TimeZoneInfo.Utc;
}
sealed class AppearanceStore : IStateStore
{
    public GardenState State;
    public bool Fail;
    public int Saves;
    public GardenState Load() => State == null ? null : StateCodec.Clone(State);
    public void Save(GardenState state)
    {
        if (Fail) throw new IOException("Synthetic save failure");
        State = StateCodec.Clone(state); Saves++;
    }
}
static class GardenPlantAppearanceTests
{
    static int checks;
    static void Assert(bool condition, string message) { checks++; if (!condition) throw new Exception(message); }
    static GardenStateService Open(AppearanceStore store, AppearanceClock clock, int nutrient = 100)
    {
        var service = new GardenStateService(store, clock, new DemoBalanceConfig(), new LegacySnapshot { Nutrient = nutrient });
        Assert(service.Open(), "state opens: " + service.LastError);
        return service;
    }
    static void CompleteOnNextDay(GardenStateService service, string id, string mission, int order = 0)
    {
        Assert(service.AdvancePreviewDay(), "advance isolated fixture to another practice day");
        Assert(service.BeginSession(id, mission, order), "select " + mission);
        Assert(service.StartSession(id), "start " + mission);
        if (MbctPolicy.IsMbct(mission)) Assert(service.SavePracticeStep(id, MbctContent.Find(mission).steps.Length), "finish MBCT practice");
        Assert(service.CompleteSession(id), "complete " + mission + ": " + service.LastError);
    }
    static GardenStateService AppendHistoricalAdditional(AppearanceStore store, AppearanceClock clock, string id, string mission)
    {
        // The old app allowed different M missions on one day. Retain an explicit
        // historical fixture to verify its profile cap and duplicate receipts.
        var state=StateCodec.Clone(store.State);string receipt=state.LastCycleId+"|"+mission;
        var session=new ActivitySession {SessionId=id,MissionId=mission,InstanceId=state.LastCycleId,
            SelectedUtc=clock.Now.ToString("O"),StartedUtc=clock.Now.ToString("O"),EndedUtc=clock.Now.ToString("O"),Status=ParticipationState.RewardCommitted};
        state.Sessions.Add(session);
        var growth=new PracticeGrowthRecord {SessionId=id,MissionId=mission,EligibilityKey=receipt,CycleId=state.LastCycleId,
            CommittedUtc=clock.Now.ToString("O"),Outcome=state.RewardReceipts.Contains(receipt)?PracticeGrowthOutcome.AlreadyClaimed:PracticeGrowthOutcome.Grown};
        if(growth.Outcome==PracticeGrowthOutcome.Grown)
        {
            growth.PlantInstanceId=state.PlantAppearance.Recipe.InstanceId;
            growth.BeforeStep=state.PlantAppearance.Recipe.GrowthStep;growth.BeforeGrowth=GardenPlantVisuals.TotalGrowth(state);
            state.PracticeGrowthRecords.Add(growth);GardenPlantVisuals.AdvanceMissionGrowth(state);
            growth.AfterStep=state.PlantAppearance.Recipe.GrowthStep;growth.AfterGrowth=GardenPlantVisuals.TotalGrowth(state);
            state.RewardReceipts.Add(receipt);
        }
        else state.PracticeGrowthRecords.Add(growth);
        GardenPlantVisuals.Sync(state);StateCodec.Validate(state);store.State=state;
        return Open(store,clock,0);
    }
    static string OldFields(GardenState state)
    {
        var document = new XmlDocument(); document.LoadXml(StateCodec.Encode(state));
        // Represent the original schema-1 writer, not today's optional additions.
        // The course-selection migration intentionally initializes its new fields.
        foreach (var key in new[] { "PlantAppearance", "MbctProgramId", "MbctProgramSelectionVersion",
                                    "MbctProgramChosen", "MbctPrograms", "GrowthHistory" })
        {
            var optional = document.DocumentElement[key];
            if (optional != null) document.DocumentElement.RemoveChild(optional);
        }
        return document.OuterXml;
    }
    static void Reject(GardenState state, string message)
    {
        bool rejected = false;
        try { StateCodec.Validate(state); } catch (Exception) { rejected = true; }
        Assert(rejected, message);
    }
    static string MeshFingerprint(PlantRecipe recipe)
    {
        var geometry = PlantGeometry.Build(recipe);
        return StateCodec.Hash(string.Join(";", geometry.Vertices.Select(v =>
            v.X.ToString("R", System.Globalization.CultureInfo.InvariantCulture) + "," +
            v.Y.ToString("R", System.Globalization.CultureInfo.InvariantCulture) + "," +
            v.Z.ToString("R", System.Globalization.CultureInfo.InvariantCulture))));
    }

    static void Migration()
    {
        var clock = new AppearanceClock();
        var old = new GardenState { MigrationCompleted = true, InstallationId = "synthetic-existing-garden", Nutrient = 77, LegacyGrowth = .3f, GardenXp = 9 };
        old.UnlockIds.Add("synthetic-unlock");
        old.LegacyCompletedUnknownDate.Add("course:P01");
        old.Environments.Add(new EnvironmentPlacement { EnvironmentId = "environment:E01", Slot = "left" });
        string originalFields = OldFields(old);
        // The exact schema-1 XML without an appearance element is accepted.
        old = StateCodec.Decode(originalFields);
        Assert(old.PlantAppearance == null, "pre-integration XML retains absent optional field");
        var store = new AppearanceStore { State = old, Fail = true };
        var service = new GardenStateService(store, clock, new DemoBalanceConfig());
        Assert(!service.Open() && store.State.PlantAppearance == null, "failed migration publishes neither shape nor economy change");
        store.Fail = false; Assert(service.Open(), "migration retry");
        var saved = service.Snapshot;
        Assert(OldFields(saved) == originalFields, "migration preserves every pre-existing XML field");
        Assert(saved.MbctProgramChosen && saved.MbctProgramId == "8w", "legacy course migration explicitly preserves the eight-week program");
        Assert(saved.PlantAppearance.Recipe.SpeciesId == "species:chamomile" && saved.PlantAppearance.Recipe.Mature, "legacy flower remains mature chamomile");
        Assert(saved.PlantAppearance.Credits.Count == 0 && saved.RewardReceipts.Count == 0, "compatibility prior never invents completion or reward");
        Assert(saved.PlantAppearance.BaselineProfileCounts.SequenceEqual(new[] { 4, 0, 0, 0, 0, 2 }), "compatibility prior stored separately");
        string recipe = PlantCodec.Encode(saved.PlantAppearance.Recipe), mesh = MeshFingerprint(saved.PlantAppearance.Recipe);
        int writes = store.Saves;
        service = Open(store, clock);
        Assert(store.Saves == writes && PlantCodec.Encode(service.Snapshot.PlantAppearance.Recipe) == recipe, "restart does not migrate, write or regenerate again");
        Assert(MeshFingerprint(service.Snapshot.PlantAppearance.Recipe) == mesh, "restart recreates identical procedural geometry");

        // Simulate rollback's schema-1 writer discarding its unknown optional element.
        var rollback = StateCodec.Decode(OldFields(saved));
        Assert(rollback.Nutrient == 77 && rollback.LegacyGrowth == .3f && rollback.PlantAppearance == null, "rollback keeps old fields independently usable");
        GardenPlantVisuals.Sync(rollback);
        Assert(PlantCodec.Encode(rollback.PlantAppearance.Recipe) == recipe, "re-upgrade without new activity deterministically restores appearance");
    }

    static void GrowthAndPreview()
    {
        var clock = new AppearanceClock(); var store = new AppearanceStore(); var service = Open(store, clock);
        Assert(service.Snapshot.PlantAppearance.Recipe.GrowthStep == 0, "new garden has procedural seed");
        Assert(service.RefreshCycle(new string[0]), "practice growth cycle");
        for (int i = 1; i <= 12; i++)
        {
            if(i>1)Assert(service.AdvancePreviewDay(), "each new practice uses another day");
            string session = "synthetic-step-" + i;
            Assert(service.BeginSession(session, "free-mission:synthetic-step-" + i) && service.StartSession(session), "start one explicit practice");
            string before = StateCodec.Encode(service.Snapshot);
            string expected = PlantCodec.Encode(PlantGenerator.Advance(service.Snapshot.PlantAppearance.Recipe));
            Assert(StateCodec.Encode(service.Snapshot) == before && StateCodec.Encode(store.State) == before, "detached expected next recipe leaves persistence unchanged");
            store.Fail = true;
            Assert(!service.CommitPracticeGrowth(session), "failed practice growth reported");
            Assert(StateCodec.Encode(service.Snapshot) == before, "failed growth is atomic for wallet, shape and receipts");
            store.Fail = false;
            Assert(service.CommitPracticeGrowth(session), "practice growth commits");
            var saved = service.Snapshot;
            Assert(saved.Coins == 100 + (i-1)*50 && saved.PurchaseReceipts.Count == 0 && saved.PracticeGrowthRecords.Count == i, "practice growth preserves coins until final completion");
            Assert(saved.PlantAppearance.Recipe.GrowthStep == i, "each finished practice advances exactly one procedural step");
            Assert(PlantCodec.Encode(saved.PlantAppearance.Recipe) == expected, "confirmed shape exactly matches preview");
            Assert(saved.PlantAppearance.IsCompleted == (i == 12), "only the final procedural step completes the plant");
            string committed = StateCodec.Encode(saved);
            Assert(service.CommitPracticeGrowth(session) && StateCodec.Encode(service.Snapshot) == committed, "retry creates no extra growth or debit");
            Assert(service.CompleteSession(session) && PlantCodec.Encode(service.Snapshot.PlantAppearance.Recipe) == expected
                && service.Snapshot.Coins==100+i*50,
                "final completion adds fifty coins without growing again");
        }
        var mature = service.Snapshot;
        string shape = PlantCodec.Encode(mature.PlantAppearance.Recipe);
        Assert(!service.GrowPlant("synthetic-after-flower", GardenPlantVisuals.GrowthPlantId), "final recipe stage rejects more purchases");
        Assert(service.Snapshot.Coins == 700 && GardenPlantVisuals.TotalGrowth(service.Snapshot) == .3f
            && PlantCodec.Encode(service.Snapshot.PlantAppearance.Recipe) == shape, "rejected extra purchase preserves flower, progress and nutrient");
        var cloned = service.Snapshot;
        cloned.PlantAppearance.BaselineProfileCounts[0] = 999;
        cloned.PlantAppearance.Recipe.Counts[0] = 999;
        cloned.PlantAppearance.Recipe.LockedProfile[0] = 0;
        cloned.PlantAppearance.Recipe.Axes[0].Length = 999;
        cloned.PlantAppearance.Recipe.Leaves[0].Length = 999;
        cloned.PlantAppearance.Recipe.Heads[0].Diameter = 999;
        Assert(PlantCodec.Encode(service.Snapshot.PlantAppearance.Recipe) == shape, "snapshot detaches all mutable recipe arrays and organs");
        Assert(GardenPlantVisuals.StageForGrowth(.098f) == 0 && GardenPlantVisuals.StageForGrowth(.099f) == 1
            && GardenPlantVisuals.StepForGrowth(.199f) == 4 && GardenPlantVisuals.StepForGrowth(.299f) == 12, "legacy tolerance boundaries preserved");
    }

    static void ActivityCredits()
    {
        var clock = new AppearanceClock(); var store = new AppearanceStore(); var service = Open(store, clock, 0);
        Assert(service.RefreshCycle(new string[0]), "activity cycle");
        Assert(service.BeginSession("synthetic-selected", "free-mission:M17") && service.StartSession("synthetic-selected"), "begin breath");
        Assert(service.Snapshot.PlantAppearance.Credits.Count == 0, "selection and start never credit appearance");
        string before = StateCodec.Encode(service.Snapshot); store.Fail = true;
        Assert(!service.CompleteSession("synthetic-selected") && StateCodec.Encode(service.Snapshot) == before, "failed completion cannot credit profile or wallet");
        store.Fail = false; Assert(service.CompleteSession("synthetic-selected"), "complete breath");
        Assert(service.Snapshot.Coins == 50 && service.Snapshot.PlantAppearance.Credits.Count == 1, "new completion grants one appearance credit and fifty coins");
        Assert(service.CompleteSession("synthetic-selected") && service.Snapshot.PlantAppearance.Credits.Count == 1, "completion retry deduplicated");
        string claimed=StateCodec.Encode(service.Snapshot);
        Assert(!service.BeginSession("synthetic-blocked-second", "free-mission:M04") && StateCodec.Encode(service.Snapshot)==claimed,
            "today's app rejects a second additional mission without rewriting historical state");
        service=AppendHistoricalAdditional(store,clock,"synthetic-breath-repeat","free-mission:M17");
        service=AppendHistoricalAdditional(store,clock,"synthetic-body","free-mission:M04");
        service=AppendHistoricalAdditional(store,clock,"synthetic-movement","free-mission:M15");
        service=AppendHistoricalAdditional(store,clock,"synthetic-fourth","free-mission:M02");
        service=AppendHistoricalAdditional(store,clock,"synthetic-unknown","free-mission:synthetic-unknown");
        var saved = service.Snapshot;
        Assert(saved.PlantAppearance.Credits.Count == 3 && saved.PlantAppearance.Recipe.Counts.SequenceEqual(new[] { 5, 1, 1, 0, 0, 2 }), "three recognized unique completions per cycle");
        Assert(saved.Coins == 50 && saved.RewardReceipts.Count == 5 && saved.PlantAppearance.Recipe.GrowthStep == 5,
            "appearance cap remains separate from growth and historical completions receive no retroactive coins");
        var snapshot = service.Snapshot; snapshot.PlantAppearance.Credits[0].Category = 5;
        Assert(service.Snapshot.PlantAppearance.Credits[0].Category == 0, "snapshot detaches activity receipt records");
        clock.Now = clock.Now.AddDays(1); Assert(service.RefreshCycle(new string[0]), "next cycle");
        CompleteOnNextDay(service, "synthetic-next-breath", "free-mission:M17");
        CompleteOnNextDay(service, "synthetic-mbct", "course:MBCT01", 1);
        Assert(service.Snapshot.PlantAppearance.Credits.Count == 5, "next business cycle and MBCT content are credited");
        int credits = service.Snapshot.PlantAppearance.Credits.Count;
        service = Open(store, clock);
        Assert(service.Snapshot.PlantAppearance.Credits.Count == credits, "reopen does not double-count historical completions");
        var bud = service.Snapshot.PlantAppearance.Recipe;
        var axes = PlantCodec.Encode(bud.Axes); string species = bud.SpeciesId;
        CompleteOnNextDay(service, "synthetic-reflection", "free-mission:M07");
        var updated = service.Snapshot.PlantAppearance.Recipe;
        Assert(updated.SpeciesId == species && PlantCodec.Encode(updated.Axes) == axes && updated.BaseSeed == bud.BaseSeed
            && updated.Height == bud.Height && updated.Radius == bud.Radius,
            "later activity cannot reroll locked species or old organs");
        while (!service.Snapshot.PlantAppearance.Recipe.Mature)
        {
            string step = service.Snapshot.PlantAppearance.Recipe.GrowthStep.ToString();
            CompleteOnNextDay(service, "synthetic-finish-" + step, "free-mission:synthetic-finish-" + step);
        }
        string mature = PlantCodec.Encode(service.Snapshot.PlantAppearance.Recipe);
        clock.Now = clock.Now.AddDays(1); service.RefreshCycle(new string[0]);
        CompleteOnNextDay(service, "synthetic-after-mature", "free-mission:M15");
        Assert(PlantCodec.Encode(service.Snapshot.PlantAppearance.Recipe) == mature, "mature plant remains immutable after activities");

        var invalid = service.Snapshot; invalid.PlantAppearance.Credits.Add(invalid.PlantAppearance.Credits[0]); Reject(invalid, "duplicate credits rejected");
        invalid = service.Snapshot; invalid.PlantAppearance.Recipe.Counts[0]++; Reject(invalid, "invented activity count rejected");
        invalid = service.Snapshot; invalid.PlantAppearance.BaselineProfileCounts[0]++; Reject(invalid, "unversioned compatibility prior rejected");
        invalid = service.Snapshot; invalid.PlantAppearance.Credits[0].MissionId = "free-mission:M01"; Reject(invalid, "receipt without real matching completion rejected");
    }

    static void VisualGrowthLimit()
    {
        var clock = new AppearanceClock();
        var state = new GardenState { MigrationCompleted = true, InstallationId = "synthetic-final-growth", Nutrient = 10, LegacyGrowth = .1f };
        state.Plants.Add(new PlantProgress { PlantId = GardenPlantVisuals.GrowthPlantId, Growth = .1f });
        GardenPlantVisuals.Sync(state);
        var store = new AppearanceStore { State = state };
        var service = Open(store, clock);
        Assert(service.RefreshCycle(new string[0]), "prepare final procedural step");
        for (int i = 0; i < 7; i++) CompleteOnNextDay(service, "synthetic-before-final-" + i, "free-mission:synthetic-before-final-" + i);
        Assert(service.AdvancePreviewDay(), "final practice occurs on its own day");
        Assert(service.BeginSession("synthetic-final-practice", "free-mission:synthetic-final-practice")
            && service.StartSession("synthetic-final-practice"), "last practice begins");
        var before = service.Snapshot;
        string original = StateCodec.Encode(before);
        Assert(!before.PlantAppearance.IsCompleted && !before.PlantAppearance.Recipe.Mature && GardenPlantVisuals.CanGrow(before),
            "plant is eligible for its final procedural step");
        Assert(GardenPlantVisuals.ScaleForGrowth(.3f) == 1.9f
            && GardenPlantVisuals.ScaleForGrowth(.4f) > GardenPlantVisuals.ScaleForGrowth(.3f), "existing blossom scale and later size increase preserved");
        var expectedRecipe = PlantGenerator.Advance(before.PlantAppearance.Recipe);
        Assert(expectedRecipe.Mature && expectedRecipe.GrowthStep == before.PlantAppearance.Recipe.GrowthStep + 1,
            "last practice reaches the final recipe stage in one step");
        Assert(StateCodec.Encode(service.Snapshot) == original && StateCodec.Encode(store.State) == original,
            "final preview does not alter saved growth, nutrient or receipts");

        store.Fail = true;
        Assert(!service.CommitPracticeGrowth("synthetic-final-practice")
            && StateCodec.Encode(service.Snapshot) == original && StateCodec.Encode(store.State) == original,
            "failed final practice preserves exact wallet, appearance, progress and receipts");
        store.Fail = false;
        Assert(service.CommitPracticeGrowth("synthetic-final-practice"), "final practice retry succeeds");
        var saved = service.Snapshot;
        Assert(saved.Coins == 360 && saved.PurchaseReceipts.Count == 0 && saved.PracticeGrowthRecords.Count == 8,
            "final practice records one claim without granting or spending coins");
        Assert(Math.Abs(GardenPlantVisuals.TotalGrowth(saved) - .3f) < 1e-6f, "final procedural growth reaches its display scale");
        Assert(PlantCodec.Encode(saved.PlantAppearance.Recipe) == PlantCodec.Encode(expectedRecipe),
            "final recipe exactly matches its preview");
        Assert(saved.PlantAppearance.IsCompleted && saved.GrowthHistory.Count == 0,
            "completion and practice growth commit without fabricating paid growth history");
        string committed = StateCodec.Encode(saved);
        Assert(service.CommitPracticeGrowth("synthetic-final-practice")
            && StateCodec.Encode(service.Snapshot) == committed, "successful request remains idempotent at limit with zero nutrient");
        service = Open(store, clock);
        Assert(!GardenPlantVisuals.CanGrow(service.Snapshot) && StateCodec.Encode(service.Snapshot) == committed,
            "final progress and completion survive reopening");
        Assert(!service.SetPlantCompleted(GardenPlantVisuals.InstanceId, false)
            && StateCodec.Encode(service.Snapshot) == committed, "final-stage completion cannot be reverted");

        foreach (float savedGrowth in new[] { .3f, .4f, GardenPlantVisuals.MaxVisualGrowth, 2f })
        foreach (bool legacyOnly in new[] { false, true })
        {
            var old = new GardenState { MigrationCompleted = true, InstallationId = "synthetic-old-capped-" + legacyOnly,
                Nutrient = 100, LegacyGrowth = legacyOnly ? savedGrowth : .3f,
                // This fixture isolates growth limits after the independently tested course migration.
                MbctProgramSelectionVersion = 1, MbctProgramChosen = true };
            if (!legacyOnly) old.Plants.Add(new PlantProgress { PlantId = GardenPlantVisuals.GrowthPlantId, Growth = savedGrowth - .3f });
            old.PurchaseReceipts.Add("synthetic-old-success");
            old.PurchaseCommands.Add(new PurchaseCommand { RequestId = "synthetic-old-success", Command = "growth|" + GardenPlantVisuals.GrowthPlantId });
            GardenPlantVisuals.Sync(old);
            old.PlantAppearance.IsCompleted = false; // Previous app omitted or left this flag false.
            string oldXml = StateCodec.Encode(old);
            string originalRecipe = PlantCodec.Encode(old.PlantAppearance.Recipe);
            var oldStore = new AppearanceStore { State = old, Fail = true };
            var reopened = new GardenStateService(oldStore, clock, new DemoBalanceConfig());
            Assert(!reopened.Open() && StateCodec.Encode(oldStore.State) == oldXml,
                "failed completion migration leaves old saved flag, appearance, money and records unchanged");
            oldStore.Fail = false;
            Assert(reopened.Open() && reopened.Snapshot.PlantAppearance.IsCompleted && oldStore.Saves == 1,
                "completion migration retries and atomically marks an old fully grown plant complete");
            var normalized = reopened.Snapshot;
            normalized.PlantAppearance.IsCompleted = false;
            Assert(StateCodec.Encode(normalized) == oldXml && PlantCodec.Encode(reopened.Snapshot.PlantAppearance.Recipe) == originalRecipe,
                "migration changes only completion and preserves historical growth, size, seed and every other field");
            string completedXml = StateCodec.Encode(reopened.Snapshot);
            reopened = Open(oldStore, clock);
            Assert(oldStore.Saves == 1 && StateCodec.Encode(reopened.Snapshot) == completedXml,
                "completed migration is not repeated on restart");
            Assert(!GardenPlantVisuals.CanGrow(reopened.Snapshot)
                && GardenPlantVisuals.ScaleForGrowth(GardenPlantVisuals.TotalGrowth(reopened.Snapshot)) == GardenPlantVisuals.ScaleForGrowth(savedGrowth),
                "older completed plant keeps its original rendered scale");
            var noChange = reopened.Snapshot; GardenPlantVisuals.PreviewGrowth(noChange, .1f);
            Assert(StateCodec.Encode(noChange) == completedXml, "completed preview is a no-op and does not reduce historical growth");
            int saves = oldStore.Saves;
            Assert(!reopened.GrowPlant("synthetic-new-at-limit", GardenPlantVisuals.GrowthPlantId)
                && StateCodec.Encode(reopened.Snapshot) == completedXml && StateCodec.Encode(oldStore.State) == completedXml && oldStore.Saves == saves,
                "new completed-plant purchase is rejected with enough nutrient and without any write or charge");
            Assert(reopened.GrowPlant("synthetic-old-success", GardenPlantVisuals.GrowthPlantId)
                && StateCodec.Encode(reopened.Snapshot) == completedXml, "old successful request remains idempotent after completion migration");
            Assert(reopened.SavePlantPhoto("synthetic-migrated-photo", GardenPlantVisuals.InstanceId)
                && reopened.Snapshot.Nutrient == 100, "migrated complete plant can save a photo without another growth purchase");
        }
    }

    static void MappingAndVariation()
    {
        foreach (var mission in MindfulnessContent.Course.Concat(MindfulnessContent.FreeMissions).Concat(MbctContent.Course))
        {
            GrowthCategory category;
            Assert(GardenPlantVisuals.TryCategory(mission.id, out category), "all bundled missions explicitly mapped: " + mission.id);
        }
        foreach (var unknown in new[] { null, "course:MBCT49", "course:P99", "free-mission:M00", "free-mission:M1", "free-mission:M01 ", "free-mission:custom" })
        { GrowthCategory category; Assert(!GardenPlantVisuals.TryCategory(unknown, out category), "unknown/noncanonical content ignored"); }
        var a = new GardenState { MigrationCompleted = true, InstallationId = "synthetic-garden-a", LegacyGrowth = .3f };
        var b = new GardenState { MigrationCompleted = true, InstallationId = "synthetic-garden-b", LegacyGrowth = .3f };
        GardenPlantVisuals.Sync(a); GardenPlantVisuals.Sync(b); StateCodec.Validate(a); StateCodec.Validate(b);
        Assert(a.PlantAppearance.Recipe.SpeciesId == "species:chamomile" && b.PlantAppearance.Recipe.SpeciesId == "species:chamomile", "compatibility species retained for distinct installations");
        Assert(MeshFingerprint(a.PlantAppearance.Recipe) != MeshFingerprint(b.PlantAppearance.Recipe), "stored identity produces distinct procedural geometry");
    }

    static void ExistingRecipeProgressionMigration()
    {
        var clock = new AppearanceClock();
        foreach (float growth in new[] { 0f, .1f, .2f, .3f, .4f })
        {
            var old = new GardenState { MigrationCompleted = true, InstallationId = "synthetic-stage-migration-" + growth,
                Nutrient = 73, GardenXp = 9, LegacyGrowth = growth, MbctProgramSelectionVersion = 1, LastCycleId = "2026-01-05" };
            old.DailyDecisions.Add(new DailyDecision { CycleId = old.LastCycleId, VisitorId = "", TimeZoneId = "UTC", AppliedUtc = clock.Now.ToString("O") });
            old.Sessions.Add(new ActivitySession { SessionId = "synthetic-old-completed", MissionId = "free-mission:M17", InstanceId = old.LastCycleId,
                SelectedUtc = clock.Now.AddMinutes(-2).ToString("O"), StartedUtc = clock.Now.AddMinutes(-1).ToString("O"), EndedUtc = clock.Now.ToString("O"),
                Status = ParticipationState.RewardCommitted });
            old.RewardReceipts.Add(old.LastCycleId + "|free-mission:M17");
            GardenPlantVisuals.Sync(old);
            old.PlantAppearance.MissionGrowthVersion = 0;
            old.PlantAppearance.MissionGrowthBaseStep = 0;
            old.PlantAppearance.MissionGrowthBaseGrowth = 0;
            string original = StateCodec.Encode(old), recipe = PlantCodec.Encode(old.PlantAppearance.Recipe);
            var store = new AppearanceStore { State = old, Fail = true };
            var service = new GardenStateService(store,clock,new DemoBalanceConfig());
            Assert(!service.Open() && StateCodec.Encode(store.State) == original, "failed progression migration leaves every old field intact");
            store.Fail = false;
            Assert(service.Open() && store.Saves == 1, "progression migration retries atomically");
            var migrated = service.Snapshot;
            Assert(migrated.PlantAppearance.MissionGrowthVersion == 1 && migrated.PlantAppearance.MissionGrowthBaseStep == old.PlantAppearance.Recipe.GrowthStep
                && migrated.PlantAppearance.MissionGrowthBaseGrowth == growth && PlantCodec.Encode(migrated.PlantAppearance.Recipe) == recipe,
                "existing stage, recipe, seed and exact prior growth become the immutable migration baseline");
            migrated.PlantAppearance.MissionGrowthVersion = 0; migrated.PlantAppearance.MissionGrowthBaseStep = 0; migrated.PlantAppearance.MissionGrowthBaseGrowth = 0;
            Assert(StateCodec.Encode(migrated) == original, "progression migration changes only its three compatibility baseline fields");
            string committed = StateCodec.Encode(service.Snapshot);
            Assert(service.CommitPracticeGrowth("synthetic-old-completed") && service.Snapshot.PracticeGrowthRecords.Count == 0
                && StateCodec.Encode(service.Snapshot) == committed, "historical completed practice is never replayed as growth or coins");
            service = Open(store,clock);
            Assert(store.Saves == 1 && StateCodec.Encode(service.Snapshot) == committed, "progression migration is idempotent across restart");
            if (growth < .3f)
            {
                CompleteOnNextDay(service,"synthetic-after-migration","free-mission:synthetic-after-migration");
                Assert(service.Snapshot.PlantAppearance.Recipe.GrowthStep == old.PlantAppearance.Recipe.GrowthStep + 1
                    && service.Snapshot.LegacyGrowth == growth && service.Snapshot.Coins == 123,
                    "first new completion advances one real step and adds fifty coins while preserving historical growth");
            }
        }
    }

    static void MissionStagesHonorRecipeLimits()
    {
        var clock = new AppearanceClock();
        var recipe = PlantGenerator.Create("synthetic-future-stage-owner","synthetic-21-step-individual",21,PlantGrowthMode.MissionCompletions);
        recipe.Counts = new[] { 4,0,0,0,0,2 };
        var state = new GardenState { MigrationCompleted = true, MbctProgramSelectionVersion = 1, Nutrient = 88,
            PlantAppearance = new GardenPlantAppearance { Recipe = recipe, MissionGrowthVersion = 1, UsesIndividualGrowth = true } };
        var service = Open(new AppearanceStore { State = state },clock);
        Assert(service.RefreshCycle(new string[0]), "future recipe practice cycle");
        for (int step = 1; step <= 21; step++)
        {
            CompleteOnNextDay(service,"synthetic-21-step-" + step,"free-mission:synthetic-21-step-" + step);
            Assert(service.Snapshot.PlantAppearance.Recipe.GrowthStep == step && service.Snapshot.PlantAppearance.Recipe.MaxGrowthSteps == 21
                && service.Snapshot.PlantAppearance.IsCompleted == (step == 21), "practice progression honors the recipe limit without a fixed twelve-step assumption");
        }
        Assert(service.Snapshot.Coins == 1138 && service.SavePlantPhoto("synthetic-21-step-photo",recipe.InstanceId),
            "completed future-limit recipe can be archived without any coin change");
        Assert(service.Snapshot.PlantPhotos.Single().Recipe.MaxGrowthSteps == 21, "photo preserves the recipe's original growth target");
    }

    static void Main()
    {
        Migration(); GrowthAndPreview(); VisualGrowthLimit(); ActivityCredits(); MappingAndVariation();
        ExistingRecipeProgressionMigration(); MissionStagesHonorRecipeLimits();
        Console.WriteLine("PASS " + checks + " garden appearance integration assertions (synthetic state only)");
    }
}
