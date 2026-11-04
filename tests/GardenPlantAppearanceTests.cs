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
        // Represent the original schema-1 writer, not today's optional additions.
        // The course-selection migration intentionally initializes its new fields.
        foreach (var key in new[] { "PlantAppearance", "MbctProgramId", "MbctProgramSelectionVersion",
                                    "MbctProgramChosen", "MbctPrograms", "GrowthHistory", "PlantCollection", "PrivacyRevision" })
        {
            var optional = document.DocumentElement[key];
            if (optional != null) document.DocumentElement.RemoveChild(optional);
        }
        document.DocumentElement["SchemaVersion"].InnerText="1";
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

        // A historical schema-1 fixture still maps identically. Real old APKs reject schema 2.
        var rollback = StateCodec.Decode(OldFields(saved));
        Assert(rollback.Nutrient == 77 && rollback.LegacyGrowth == .3f && rollback.PlantAppearance == null, "rollback keeps old fields independently usable");
        GardenPlantVisuals.Sync(rollback);
        Assert(PlantCodec.Encode(rollback.PlantAppearance.Recipe) == recipe, "re-upgrade without new activity deterministically restores appearance");
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
        Migration(); MappingAndVariation();
        Console.WriteLine("PASS " + checks + " garden appearance integration assertions (synthetic state only)");
    }
}
