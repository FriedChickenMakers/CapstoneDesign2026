using System;
using System.IO;
using System.Linq;
using CapstoneDesign.Plants;
using CapstoneDesign.Runtime;
using CapstoneDesign.Runtime.LocalState;

static class PlantMemoryPresentationTests
{
    static int checks;
    static void Check(bool value, string message) { checks++; if (!value) throw new Exception(message); }
    sealed class Clock : IClock
    {
        public DateTimeOffset Now = DateTimeOffset.Parse("2026-11-09T10:00:00Z");
        public DateTimeOffset UtcNow => Now;
        public TimeZoneInfo TimeZone => TimeZoneInfo.Utc;
    }
    sealed class Store : IStateStore
    {
        public GardenState State;
        public bool Fail;
        public GardenState Load() => State == null ? null : StateCodec.Clone(State);
        public void Save(GardenState state) { if (Fail) throw new IOException("Synthetic write failure"); State = StateCodec.Clone(state); }
    }
    static void Complete(GardenStateService service, string id, string mission)
    {
        Check(service.RefreshCycle(null), service.LastError);
        Check(service.BeginSession(id, mission, mission.StartsWith("course:") ? 1 : 0), service.LastError);
        Check(service.StartSession(id) && service.CompleteSession(id, null, null), service.LastError);
    }

    static void EditedArchivedMemoryStaysWithItsIndividual()
    {
        var clock = new Clock(); var store = new Store();
        using (var service = new GardenStateService(store, clock, new DemoBalanceConfig()))
        {
            Check(service.Open(), service.LastError);
            Complete(service, "synthetic-basic", "course:P01"); Complete(service, "synthetic-additional", "free-mission:M17");
            string plantId = WeeklyPlants.Active(service.Snapshot).Id;
            Check(service.SetPlantCompleted(plantId, true), service.LastError);
            string daily = DailyEmotionPolicy.RecordId(service.Snapshot.LastCycleId);
            Check(service.BeginDailyEmotion(service.Snapshot.LastCycleId), service.LastError);
            Check(service.CompleteDailyEmotion(daily, "편안함", "합성 기억"), service.LastError);
            var state = service.Snapshot; var plant = WeeklyPlants.Active(state); var memory = plant.Memories.Single();
            var photo = state.PlantPhotos.Single(); var memo = PlantAlbumHistory.MemoryRecord(state, plantId, memory.OrganId);
            Check(memo != null && memo.Note == "합성 기억", "Leaf could not resolve its saved memo");
            Check(PlantAlbumHistory.MemoryRecord(state, plantId, memo.Id).Note == memo.Note, "Record-list identity did not resolve the same leaf");
            Check(PlantAlbumHistory.Records(state, photo).Single().Id == memo.Id, "Album and leaf records differ");
            Check(PlantAlbumHistory.MemoryRecord(state, "another-individual", memo.Id) == null, "Memory leaked to another individual");
            memo.Note = "detached edit";
            Check(PlantAlbumHistory.MemoryRecord(state, plantId, memory.OrganId).Note == "합성 기억", "Presentation mutated saved text");
            string recipe = PlantCodec.Encode(plant.Recipe); int coins = state.Coins;
            store.Fail = true;
            Check(!service.UpdateDailyEmotion(memory.RecordId, "고마움", "수정된 합성 기억"), "Failed save was accepted");
            Check(PlantAlbumHistory.Records(service.Snapshot, service.Snapshot.PlantPhotos.Single()).Single().Note == "합성 기억", "Failed edit changed visible album text");
            store.Fail = false;
            Check(service.UpdateDailyEmotion(memory.RecordId, "고마움", "수정된 합성 기억"), service.LastError);
            state = service.Snapshot;
            Check(PlantAlbumHistory.Records(state, state.PlantPhotos.Single()).Single().Note == "수정된 합성 기억", "Album did not reflect atomic edit");
            clock.Now = clock.Now.AddDays(1); Complete(service, "next-plant", "free-mission:M17");
            state = service.Snapshot;
            Check(WeeklyPlants.Active(state).Id != plantId, "Next growth did not create its own individual");
            Check(PlantAlbumHistory.Leaves(state, WeeklyPlants.Active(state).Id).Length == 0, "Old leaf moved to new growing plant");
            Check(PlantAlbumHistory.MemoryRecord(state, plantId, memory.OrganId).Note == "수정된 합성 기억", "Archived leaf became inaccessible");
            string active = WeeklyPlants.Active(state).Id;
            Check(service.DeleteDailyEmotion(memory.RecordId), service.LastError);
            state = service.Snapshot;
            Check(PlantAlbumHistory.Records(state, state.PlantPhotos.Single()).Length == 0, "Deleted memory reappeared in album fallback");
            Check(PlantAlbumHistory.MemoryRecord(state, plantId, memory.OrganId) == null, "Deleted leaf still opened private text");
            var leaves = PlantAlbumHistory.Leaves(state, plantId);
            Check(leaves.Length == 1 && !leaves[0].Linked && leaves[0].OrganId == memory.OrganId && leaves[0].Slot == 0, "Deletion changed a leaf attachment/identity");
            Check(PlantCodec.Encode(state.PlantCollection.Plants.First(p => p.Id == plantId).Recipe) == recipe, "Editing/deleting changed archived plant appearance");
            Check(state.Coins == coins + 30 && WeeklyPlants.Active(state).Id == active, "Memory operations changed wallet or growth target");
        }
    }

    static void AllMemoryPagesAndDetachedResults()
    {
        var state = new GardenState(); state.PlantCollection = new PlantCollectionState { ActiveId = "growing" };
        var plant = new PlantInstanceState { Id = "archived" }; state.PlantCollection.Plants.Add(plant);
        for (int i = 0; i < 60; i++) plant.Memories.Add(new MemoryOrganState {
            OrganId = "archived/memory/" + i, SlotIndex = i, Kind = i % 3 == 0 ? MemoryRecordKind.None : MemoryRecordKind.DailyEmotion,
            RecordId = i % 3 == 0 ? null : "memo-" + i });
        var leaves = PlantAlbumHistory.Leaves(state, plant.Id);
        Check(leaves.Length == 60 && leaves.Select(m => m.OrganId).Distinct().Count() == 60, "Memory projection capped or duplicated the collection");
        Check(leaves.Count(m => m.Slot / 21 == 0) == 21 && leaves.Count(m => m.Slot / 21 == 1) == 21 && leaves.Count(m => m.Slot / 21 == 2) == 18, "Last memory page is unreachable");
        Check(leaves.Count(m => m.Linked) == 40, "Unlinked geometry remained clickable");
        leaves[1].Slot = 700; leaves[1].OrganId = "edited copy";
        Check(plant.Memories[1].SlotIndex == 1 && plant.Memories[1].OrganId == "archived/memory/1", "Renderer inputs mutated domain identity");
        Check(state.PlantCollection.ActiveId == "growing", "Viewing archive changed active growth target");
    }
    public static int Main()
    {
        try { EditedArchivedMemoryStaysWithItsIndividual(); AllMemoryPagesAndDetachedResults(); Console.WriteLine("Plant memory presentation PASS: " + checks); return 0; }
        catch (Exception error) { Console.Error.WriteLine(error); return 1; }
    }
}
