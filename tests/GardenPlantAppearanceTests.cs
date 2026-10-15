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
    static void Complete(GardenStateService service, string id, string mission, int order = 0)
    {
        Assert(service.BeginSession(id, mission, order), "select " + mission);
        Assert(service.StartSession(id), "start " + mission);
        Assert(service.CompleteSession(id), "complete " + mission + ": " + service.LastError);
    }
    static string OldFields(GardenState state)
    {
        var document = new XmlDocument(); document.LoadXml(StateCodec.Encode(state));
        var appearance = document.DocumentElement["PlantAppearance"];
        if (appearance != null) document.DocumentElement.RemoveChild(appearance);
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
        int[] steps = { 3, 4, 12 };
        for (int i = 0; i < steps.Length; i++)
        {
            string before = StateCodec.Encode(service.Snapshot);
            var preview = service.Snapshot; GardenPlantVisuals.PreviewGrowth(preview, .1f); StateCodec.Validate(preview);
            Assert(preview.PlantAppearance.Recipe.GrowthStep == steps[i], "preview follows legacy visible stage " + i);
            Assert(StateCodec.Encode(service.Snapshot) == before && StateCodec.Encode(store.State) == before, "preview/cancel leaves persistence and balance unchanged");
            string expected = PlantCodec.Encode(preview.PlantAppearance.Recipe);
            store.Fail = true;
            Assert(!service.GrowPlant("synthetic-grow-" + i, GardenPlantVisuals.GrowthPlantId), "failed growth reported");
            Assert(StateCodec.Encode(service.Snapshot) == before, "failed growth is atomic for wallet, shape and receipts");
            store.Fail = false;
            Assert(service.GrowPlant("synthetic-grow-" + i, GardenPlantVisuals.GrowthPlantId), "growth commits");
            var saved = service.Snapshot;
            Assert(saved.Nutrient == 100 - 10 * (i + 1) && saved.PurchaseReceipts.Count == i + 1, "one existing charge per purchase");
            Assert(PlantCodec.Encode(saved.PlantAppearance.Recipe) == expected, "confirmed shape exactly matches preview");
            string committed = StateCodec.Encode(saved);
            Assert(service.GrowPlant("synthetic-grow-" + i, GardenPlantVisuals.GrowthPlantId) && StateCodec.Encode(service.Snapshot) == committed, "retry creates no extra growth or debit");
            if (i == 1) Assert(saved.PlantAppearance.Recipe.Heads.Count > 0 && PlantGenerator.Opening(saved.PlantAppearance.Recipe, 0) == 0, "second purchase remains a closed bud");
        }
        var mature = service.Snapshot;
        string shape = PlantCodec.Encode(mature.PlantAppearance.Recipe);
        Assert(service.GrowPlant("synthetic-after-flower", GardenPlantVisuals.GrowthPlantId), "existing post-blossom purchases remain supported");
        Assert(service.Snapshot.Nutrient == 60 && GardenPlantVisuals.TotalGrowth(service.Snapshot) > .39f
            && PlantCodec.Encode(service.Snapshot.PlantAppearance.Recipe) == shape, "extra purchase retains flower and existing scale input");
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
        Assert(service.Snapshot.Nutrient == 5 && service.Snapshot.PlantAppearance.Credits.Count == 1, "single existing reward and single appearance credit");
        Assert(service.CompleteSession("synthetic-selected") && service.Snapshot.PlantAppearance.Credits.Count == 1, "completion retry deduplicated");
        Complete(service, "synthetic-breath-repeat", "free-mission:M17");
        Complete(service, "synthetic-body", "free-mission:M04");
        Complete(service, "synthetic-movement", "free-mission:M15");
        Complete(service, "synthetic-fourth", "free-mission:M02");
        Complete(service, "synthetic-unknown", "free-mission:synthetic-unknown");
        var saved = service.Snapshot;
        Assert(saved.PlantAppearance.Credits.Count == 3 && saved.PlantAppearance.Recipe.Counts.SequenceEqual(new[] { 5, 1, 1, 0, 0, 2 }), "three recognized unique completions per cycle");
        Assert(saved.Nutrient == 25 && saved.RewardReceipts.Count == 5, "appearance cap/unknown activity never changes existing reward policy");
        var snapshot = service.Snapshot; snapshot.PlantAppearance.Credits[0].Category = 5;
        Assert(service.Snapshot.PlantAppearance.Credits[0].Category == 0, "snapshot detaches activity receipt records");
        clock.Now = clock.Now.AddDays(1); Assert(service.RefreshCycle(new string[0]), "next cycle");
        Complete(service, "synthetic-next-breath", "free-mission:M17");
        Complete(service, "synthetic-mbct", "course:MBCT01", 1);
        Assert(service.Snapshot.PlantAppearance.Credits.Count == 5, "next business cycle and MBCT content are credited");
        int credits = service.Snapshot.PlantAppearance.Credits.Count;
        service = Open(store, clock);
        Assert(service.Snapshot.PlantAppearance.Credits.Count == credits, "reopen does not double-count historical completions");
        Assert(service.GrowPlant("synthetic-sprout", GardenPlantVisuals.GrowthPlantId)
            && service.GrowPlant("synthetic-bud", GardenPlantVisuals.GrowthPlantId), "grow credited profile to bud");
        var bud = service.Snapshot.PlantAppearance.Recipe;
        var axes = PlantCodec.Encode(bud.Axes); var heads = PlantCodec.Encode(bud.Heads); string species = bud.SpeciesId;
        Complete(service, "synthetic-reflection", "free-mission:M07");
        var updated = service.Snapshot.PlantAppearance.Recipe;
        Assert(updated.SpeciesId == species && PlantCodec.Encode(updated.Axes) == axes && PlantCodec.Encode(updated.Heads) == heads,
            "later activity cannot reroll locked species or old organs");
        Assert(service.GrowPlant("synthetic-flower", GardenPlantVisuals.GrowthPlantId), "finish credited flower");
        string mature = PlantCodec.Encode(service.Snapshot.PlantAppearance.Recipe);
        clock.Now = clock.Now.AddDays(1); service.RefreshCycle(new string[0]);
        Complete(service, "synthetic-after-mature", "free-mission:M15");
        Assert(PlantCodec.Encode(service.Snapshot.PlantAppearance.Recipe) == mature, "mature plant remains immutable after activities");

        var invalid = service.Snapshot; invalid.PlantAppearance.Credits.Add(invalid.PlantAppearance.Credits[0]); Reject(invalid, "duplicate credits rejected");
        invalid = service.Snapshot; invalid.PlantAppearance.Recipe.Counts[0]++; Reject(invalid, "invented activity count rejected");
        invalid = service.Snapshot; invalid.PlantAppearance.BaselineProfileCounts[0]++; Reject(invalid, "unversioned compatibility prior rejected");
        invalid = service.Snapshot; invalid.PlantAppearance.Credits[0].MissionId = "free-mission:M01"; Reject(invalid, "receipt without real matching completion rejected");
    }

    static void VisualGrowthLimit()
    {
        var clock = new AppearanceClock();
        var state = new GardenState { MigrationCompleted = true, InstallationId = "synthetic-final-growth", Nutrient = 10, LegacyGrowth = .3f };
        state.Plants.Add(new PlantProgress { PlantId = GardenPlantVisuals.GrowthPlantId, Growth = .7f });
        GardenPlantVisuals.Sync(state);
        var store = new AppearanceStore { State = state };
        var service = Open(store, clock);
        var before = service.Snapshot;
        string original = StateCodec.Encode(before);
        Assert(before.PlantAppearance.Recipe.Mature && GardenPlantVisuals.CanGrow(before), "recipe maturity still permits existing post-blossom visual growth");
        Assert(GardenPlantVisuals.ScaleForGrowth(.3f) == 1.9f
            && GardenPlantVisuals.ScaleForGrowth(.4f) > GardenPlantVisuals.ScaleForGrowth(.3f), "existing blossom scale and later size increase preserved");
        var preview = service.Snapshot;
        GardenPlantVisuals.PreviewGrowth(preview, .1f);
        StateCodec.Validate(preview);
        float finalGrowth = GardenPlantVisuals.TotalGrowth(preview);
        Assert(Math.Abs(finalGrowth - GardenPlantVisuals.MaxVisualGrowth) < 1e-6f
            && finalGrowth - GardenPlantVisuals.TotalGrowth(before) < .1f, "last purchase uses only remaining visible growth");
        Assert(GardenPlantVisuals.ScaleForGrowth(finalGrowth) > GardenPlantVisuals.ScaleForGrowth(GardenPlantVisuals.TotalGrowth(before))
            && !GardenPlantVisuals.CanGrow(preview), "final partial growth reaches shared renderer limit");
        Assert(StateCodec.Encode(service.Snapshot) == original && StateCodec.Encode(store.State) == original,
            "final preview does not alter saved growth, nutrient or receipts");

        store.Fail = true;
        Assert(!service.GrowPlant("synthetic-final-purchase", GardenPlantVisuals.GrowthPlantId)
            && StateCodec.Encode(service.Snapshot) == original && StateCodec.Encode(store.State) == original,
            "failed final purchase preserves exact wallet, appearance, progress and receipts");
        store.Fail = false;
        Assert(service.GrowPlant("synthetic-final-purchase", GardenPlantVisuals.GrowthPlantId), "final purchase retry succeeds");
        var saved = service.Snapshot;
        Assert(saved.Nutrient == 0 && saved.PurchaseReceipts.Count == 1
            && GardenPlantVisuals.TotalGrowth(saved) == finalGrowth
            && PlantCodec.Encode(saved.PlantAppearance.Recipe) == PlantCodec.Encode(preview.PlantAppearance.Recipe),
            "one charge commits exactly the previewed final appearance and progress");
        string committed = StateCodec.Encode(saved);
        Assert(service.GrowPlant("synthetic-final-purchase", GardenPlantVisuals.GrowthPlantId)
            && StateCodec.Encode(service.Snapshot) == committed, "successful request remains idempotent at limit with zero nutrient");
        service = Open(store, clock);
        Assert(!GardenPlantVisuals.CanGrow(service.Snapshot) && StateCodec.Encode(service.Snapshot) == committed,
            "final progress and visible limit survive reopening");

        foreach (float savedGrowth in new[] { GardenPlantVisuals.MaxVisualGrowth, 2f })
        foreach (bool legacyOnly in new[] { false, true })
        {
            var old = new GardenState { MigrationCompleted = true, InstallationId = "synthetic-old-capped-" + legacyOnly,
                Nutrient = 100, LegacyGrowth = legacyOnly ? savedGrowth : .3f };
            if (!legacyOnly) old.Plants.Add(new PlantProgress { PlantId = GardenPlantVisuals.GrowthPlantId, Growth = savedGrowth - .3f });
            old.PurchaseReceipts.Add("synthetic-old-success");
            old.PurchaseCommands.Add(new PurchaseCommand { RequestId = "synthetic-old-success", Command = "growth|" + GardenPlantVisuals.GrowthPlantId });
            GardenPlantVisuals.Sync(old);
            string oldXml = StateCodec.Encode(old);
            var oldStore = new AppearanceStore { State = old };
            var reopened = Open(oldStore, clock);
            Assert(oldStore.Saves == 0 && StateCodec.Encode(reopened.Snapshot) == oldXml,
                "older over-limit progress remains untouched on load, including legacy contribution");
            Assert(!GardenPlantVisuals.CanGrow(reopened.Snapshot)
                && GardenPlantVisuals.ScaleForGrowth(GardenPlantVisuals.TotalGrowth(reopened.Snapshot)) == GardenPlantVisuals.ScaleForGrowth(GardenPlantVisuals.MaxVisualGrowth),
                "older over-limit state uses original maximum renderer scale");
            var noChange = reopened.Snapshot; GardenPlantVisuals.PreviewGrowth(noChange, .1f);
            Assert(StateCodec.Encode(noChange) == oldXml, "over-limit preview is a no-op and does not reduce historical growth");
            int saves = oldStore.Saves;
            Assert(!reopened.GrowPlant("synthetic-new-at-limit", GardenPlantVisuals.GrowthPlantId)
                && StateCodec.Encode(reopened.Snapshot) == oldXml && StateCodec.Encode(oldStore.State) == oldXml && oldStore.Saves == saves,
                "new over-limit purchase is rejected with enough nutrient and without any write or charge");
            Assert(reopened.GrowPlant("synthetic-old-success", GardenPlantVisuals.GrowthPlantId)
                && StateCodec.Encode(reopened.Snapshot) == oldXml, "old successful request remains idempotent beyond new purchase limit");
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

    static void Main()
    {
        Migration(); GrowthAndPreview(); VisualGrowthLimit(); ActivityCredits(); MappingAndVariation();
        Console.WriteLine("PASS " + checks + " garden appearance integration assertions (synthetic state only)");
    }
}
