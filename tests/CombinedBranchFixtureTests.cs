using System;
using System.IO;
using System.Linq;
using CapstoneDesign.Runtime.LocalState;
using CapstoneDesign.Plants;

// Inputs are produced by each original branch's compiled code, never a user save.
public static class CombinedBranchFixtureTests
{
    static int checks;
    sealed class Clock : IClock
    {
        public DateTimeOffset UtcNow => DateTimeOffset.Parse("2026-10-27T12:00:00+09:00");
        public TimeZoneInfo TimeZone => TimeZoneInfo.CreateCustomTimeZone("Synthetic", TimeSpan.FromHours(9), "Synthetic", "Synthetic");
    }
    sealed class Store : IStateStore
    {
        public string Data; public bool Fail;
        public GardenState Load() => StateCodec.Decode(Data);
        public void Save(GardenState state)
        {
            if (Fail) throw new IOException("Synthetic migration write failure");
            Data = StateCodec.Encode(state);
        }
    }
    static void Require(bool value, string message)
    { checks++; if (!value) throw new Exception(message); }
    static GardenStateService Open(Store store) => new GardenStateService(store, new Clock(),
        new DemoBalanceConfig { VisitorChancePercent = 0 }, new LegacySnapshot());
    public static void Main(string[] args)
    {
        Require(args.Length == 2, "Supply original previous and latest synthetic XML fixtures");
        foreach (string path in args) Verify(path);
        Console.WriteLine("COMBINED_BRANCH_FIXTURES_PASS checks=" + checks);
    }
    static void Verify(string path)
    {
        string raw = File.ReadAllText(path);
        var before = StateCodec.Decode(raw);
        int originalSchema = before.SchemaVersion;
        var store = new Store { Data = raw, Fail = true };
        using (var failed = Open(store))
            Require(!failed.Open() && store.Data == raw, "Failed migration must preserve the original bytes");
        store.Fail = false;
        string migrated;
        using (var service = Open(store))
        {
            Require(service.Open(), service.LastError);
            var after = service.Snapshot;
            Require(after.SchemaVersion == 3, "Both original formats reach the combined schema");
            Require(after.InstallationId == before.InstallationId && after.Nutrient == before.Nutrient, "Identity and wallet preserved");
            Require(after.RewardReceipts.SequenceEqual(before.RewardReceipts), "Original reward receipts preserved");
            Require(after.PurchaseReceipts.SequenceEqual(before.PurchaseReceipts), "Original purchase receipts preserved");
            Require(after.Sessions.Count == before.Sessions.Count, "No original mission is dropped or duplicated");
            foreach (var old in before.Sessions)
            {
                var current = after.Sessions.Single(s => s.SessionId == old.SessionId);
                Require(current.Status == old.Status && current.RewardAmount == old.RewardAmount && current.Mood == old.Mood && current.Note == old.Note,
                    "Original mission completion, amount and text preserved");
                Require(PlantCodec.Encode(current.FocusStepRecords) == PlantCodec.Encode(old.FocusStepRecords), "Exact historical steps preserved");
                Require(FocusMissionCatalog.FindForSession(current).Steps.Length == current.FocusStepRecords.Count, "Each saved session has its own compatible definition");
            }
            if (originalSchema == 1)
            {
                Require(after.Sessions.All(s => s.FocusVersion == 4), "Previous guided v3 definitions migrate to v4");
                Require(after.PlantAppearance.Recipe.MaxGrowthSteps == 12, "Existing 12-step plant retains its target");
                Require(PlantCodec.Encode(after.PlantAppearance.Recipe) == PlantCodec.Encode(before.PlantAppearance.Recipe), "Existing active recipe unchanged");
                Require(after.PlantPhotos.Count == before.PlantPhotos.Count, "Existing photos not duplicated");
                foreach (var photo in before.PlantPhotos)
                {
                    var copy = after.PlantPhotos.Single(p => p.Id == photo.Id);
                    Require(PlantCodec.Encode(copy.Recipe) == PlantCodec.Encode(photo.Recipe), "Archived recipe preserved");
                    Require(PlantCodec.Encode(copy.Memos) == PlantCodec.Encode(photo.Memos), "Archived dates, text and P/M references preserved");
                }
                Require(after.DailyEmotionRecords.Count == 1 && after.DailyEmotionRecords.Single().RewardAmount == 100, "Existing daily emotion and reward retained");
            }
            else
            {
                Require(after.Sessions.All(s => s.FocusVersion == 3), "Latest branch v3 definitions are not reinterpreted as guided v3");
                foreach (var plant in before.PlantCollection.Plants)
                {
                    var copy = after.PlantCollection.Plants.Single(p => p.Id == plant.Id);
                    Require(PlantCodec.Encode(copy.Recipe) == PlantCodec.Encode(plant.Recipe), "Latest plant recipe preserved");
                    Require(copy.Memories.Count == plant.Memories.Count, "Latest memory leaves preserved");
                }
            }
            migrated = store.Data;
        }
        using (var service = Open(store))
        {
            Require(service.Open() && store.Data == migrated, "Reopen does not duplicate or rewrite migrated plants");
            var initial = service.Snapshot;
            var active = initial.Sessions.Single(s => s.SessionId == "synthetic-p02-active");
            var definition = FocusMissionCatalog.FindForSession(active);
            for (int i = active.InstructionStep; i < definition.Steps.Length; i++)
                Require(service.ConfirmFocusStep(active.SessionId, i, null, true), service.LastError);
            Require(service.CompletePracticeSession(active.SessionId), service.LastError);
            var finished = service.Snapshot;
            Require(finished.Nutrient == initial.Nutrient + 50, "Resumed historical practice earns the combined 50 coins once");
            Require(finished.PlantAppearance.Recipe.GrowthStep == initial.PlantAppearance.Recipe.GrowthStep + 1, "Resumed practice grows its actual current plant once");
            string committed = store.Data;
            Require(service.CompletePracticeSession(active.SessionId) && store.Data == committed, "Duplicate completion preserves all migrated state");
        }
    }
}
