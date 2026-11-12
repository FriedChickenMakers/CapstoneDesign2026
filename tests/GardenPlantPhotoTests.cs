using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Xml;
using CapstoneDesign.Plants;
using CapstoneDesign.Runtime.LocalState;

sealed class PhotoClock : IClock
{
    public DateTimeOffset Now = DateTimeOffset.Parse("2026-01-05T12:00:00+00:00");
    public DateTimeOffset UtcNow => Now;
    public TimeZoneInfo TimeZone => TimeZoneInfo.Utc;
}
sealed class PhotoStore : IStateStore
{
    public GardenState State;
    public GardenState LastAttempt;
    public bool Fail;
    public int Saves;
    public GardenState Load() => State == null ? null : StateCodec.Clone(State);
    public void Save(GardenState candidate)
    {
        LastAttempt = StateCodec.Clone(candidate);
        if (Fail) throw new IOException("Synthetic photo save failure");
        State = StateCodec.Clone(candidate);
        Saves++;
    }
}
static class GardenPlantPhotoTests
{
    static int checks;
    static void Assert(bool condition, string label)
    { checks++; if (!condition) throw new Exception(label); }
    static GardenState Fixture(float growth, string instance = "garden-main", bool completed = false)
    {
        var state = new GardenState {
            MigrationCompleted = true, MbctProgramSelectionVersion = 1,
            InstallationId = "synthetic-photo-owner", Nutrient = 77, GardenXp = 9,
            LegacyGrowth = growth, LastUnlock = "synthetic-old-unlock"
        };
        state.UnlockIds.Add("synthetic-old-unlock");
        state.LegacyCompletedUnknownDate.Add("free-mission:synthetic-old-completion");
        var recipe = PlantGenerator.Create(state.InstallationId, instance);
        recipe.Counts = new[] { 4, 0, 0, 0, 0, 2 };
        state.PlantAppearance = new GardenPlantAppearance { Recipe = recipe };
        GardenPlantVisuals.Sync(state);
        state.PlantAppearance.IsCompleted |= completed;
        StateCodec.Validate(state);
        return state;
    }
    static GardenStateService Open(IStateStore store, PhotoClock clock)
    {
        var service = new GardenStateService(store, clock, new DemoBalanceConfig());
        Assert(service.Open(), "open synthetic photo state: " + service.LastError);
        return service;
    }
    static string PhotoXml(GardenState state) => PlantCodec.Encode(state.PlantPhotos);
    static bool GrowPractice(GardenStateService service, string id) => service.AdvancePreviewDay()
        && service.BeginSession(id,"free-mission:M17") && service.StartSession(id)
        && service.CommitPracticeGrowth(id) && service.CompleteSession(id);
    static string ExceptAlbum(GardenState state, bool removeCompletion = false)
    {
        var doc = new XmlDocument(); doc.LoadXml(StateCodec.Encode(state));
        foreach (string field in new[] { "PlantPhotos", "PhotoSaveCommands", "PlantStartCommands" })
            doc.DocumentElement.RemoveChild(doc.DocumentElement[field]);
        var current = doc.DocumentElement["PlantAppearance"];
        if (current != null) current.RemoveChild(current["IsArchived"]);
        if (removeCompletion && doc.DocumentElement["PlantAppearance"] != null)
        {
            var appearance = doc.DocumentElement["PlantAppearance"];
            appearance.RemoveChild(appearance["IsCompleted"]);
        }
        return doc.OuterXml;
    }
    static void Reject(GardenState state, string label)
    {
        bool rejected = false;
        try { StateCodec.Validate(state); } catch (Exception) { rejected = true; }
        Assert(rejected, label);
    }

    static void ExplicitCompletion()
    {
        var clock = new PhotoClock();
        foreach (float growth in new[] { 0f, .1f, .2f })
        {
            var store = new PhotoStore { State = Fixture(growth) };
            var service = Open(store, clock);
            string before = StateCodec.Encode(service.Snapshot);
            Assert(!service.SavePlantPhoto("synthetic-uncompleted", "garden-main"), "unfinished plant cannot be photographed");
            Assert(StateCodec.Encode(service.Snapshot) == before && StateCodec.Encode(store.State) == before,
                "rejected photo preserves exact persisted state");
            Assert(!service.SetPlantCompleted("synthetic-wrong-instance", true), "completion targets a current individual, not a species");
            Assert(!service.Snapshot.PlantAppearance.IsCompleted, "invalid completion has no effect");
        }
        foreach (float growth in new[] { .3f, GardenPlantVisuals.MaxVisualGrowth, 2f })
        {
            var old = Fixture(growth);
            old.PlantAppearance.IsCompleted = false;
            var store = new PhotoStore { State = old };
            var service = Open(store, clock);
            Assert(service.Snapshot.PlantAppearance.IsCompleted && !GardenPlantVisuals.CanGrow(service.Snapshot),
                "final-stage plant is normalized to completed on open");
            string before = StateCodec.Encode(service.Snapshot);
            Assert(!service.SetPlantCompleted("garden-main", false) && StateCodec.Encode(service.Snapshot) == before,
                "final-stage plant cannot be changed back to incomplete");
            Assert(service.SavePlantPhoto("synthetic-completed", "garden-main"), "fully grown plant can be photographed immediately");
        }
        var earlyStore = new PhotoStore { State = Fixture(.1f) };
        var early = Open(earlyStore, clock);
        earlyStore.Fail = true;
        Assert(!early.SetPlantCompleted("garden-main", true) && !early.Snapshot.PlantAppearance.IsCompleted,
            "failed completion write cannot authorize a photo");
        earlyStore.Fail = false;
        var beforeCompletion=early.Snapshot;
        string recipe=PlantCodec.Encode(beforeCompletion.PlantAppearance.Recipe);
        Assert(early.SetPlantCompleted("garden-main", true), "explicit completion before maximum stage");
        var completed=early.Snapshot;
        Assert(PlantCodec.Encode(completed.PlantAppearance.Recipe)==recipe&&completed.Coins==beforeCompletion.Coins
            &&completed.GrowthHistory.Count==beforeCompletion.GrowthHistory.Count&&completed.Sessions.Count==beforeCompletion.Sessions.Count,
            "completion changes no recipe, wallet, growth history or participation");
        Assert(completed.PlantPhotos.Count==1&&completed.PlantAppearance.IsCompleted&&!completed.PlantAppearance.IsArchived
            &&GardenPlantVisuals.HasCurrentPlant(completed)&&!GardenPlantVisuals.CanGrow(completed),
            "completion automatically retains an album photo while the completed plant remains visible");
        string frozenPhoto=PhotoXml(completed);
        Assert(early.SavePlantPhoto("synthetic-early-photo","garden-main")&&PhotoXml(early.Snapshot)==frozenPhoto,
            "manual capture acknowledges the original automatic photo without recapture");
        Assert(completed.PlantPhotos[0].Growth==.1f&&completed.PlantPhotos[0].Scale==GardenPlantVisuals.ScaleForGrowth(.1f)
            &&PlantCodec.Encode(completed.PlantPhotos[0].Recipe)==recipe&&!completed.PlantPhotos[0].Recipe.Mature,
            "early photo freezes the exact lower-stage recipe, growth and scale");
        string beforePaid=StateCodec.Encode(early.Snapshot);
        Assert(!early.GrowPlant("synthetic-no-early-growth",GardenPlantVisuals.GrowthPlantId)&&StateCodec.Encode(early.Snapshot)==beforePaid,
            "early completion cannot be bypassed through paid growth");
        Assert(early.RefreshCycle(new string[0])&&early.BeginSession("synthetic-after-completion","free-mission:M17")
            &&early.StartSession("synthetic-after-completion")&&early.CompleteSession("synthetic-after-completion"),
            "next eligible practice starts and grows a new individual atomically");
        var successor=early.Snapshot;
        Assert(successor.PlantAppearance.Recipe.InstanceId!="garden-main"&&successor.PlantAppearance.Recipe.MaxGrowthSteps==21
            &&successor.PlantAppearance.Recipe.GrowthStep==1&&successor.StoredGrowthSteps==0&&successor.Coins==77+30
            &&PhotoXml(successor)==frozenPhoto,"successor receives one growth step and thirty coins without changing the old photo");
        string stable=StateCodec.Encode(successor);
        Assert(early.CompletePracticeSession("synthetic-after-completion")&&StateCodec.Encode(early.Snapshot)==stable,
            "completion retry cannot create or grow a second successor");
        var reopened=Open(earlyStore,clock);
        Assert(StateCodec.Encode(reopened.Snapshot)==stable,"restart preserves successor and frozen completed individual");
    }

    static void FrozenPicturesAndIndividuals()
    {
        var clock = new PhotoClock();
        var store = new PhotoStore { State = Fixture(.3f, "synthetic-individual-one", true) };
        var service = Open(store, clock);
        Assert(service.SavePlantPhoto("synthetic-photo-one", "synthetic-individual-one"), "first completed individual saved");
        var initial = service.Snapshot;
        string photo = PhotoXml(initial), originalRecipe = PlantCodec.Encode(initial.PlantPhotos[0].Recipe);
        var detached = service.Snapshot;
        var recipe = detached.PlantPhotos[0].Recipe;
        recipe.Counts[0] = 999; recipe.LockedProfile[0] = 99; recipe.Axes[0].Length = 999;
        recipe.Leaves[0].Length = 999; recipe.Heads[0].Diameter = 999;
        detached.PlantPhotos[0].Scale = 999; detached.PhotoSaveCommands[0].PlantInstanceId = "synthetic-mutated";
        Assert(PhotoXml(service.Snapshot) == photo && PhotoXml(store.State) == photo,
            "snapshot detaches album recipes, arrays, organs, metadata and commands");
        Assert(PlantCodec.Encode(service.Snapshot.PlantAppearance.Recipe) == originalRecipe,
            "photo snapshot edits cannot mutate the current plant");
        string beforeGrowth = StateCodec.Encode(service.Snapshot);
        Assert(!service.GrowPlant("synthetic-later-growth", GardenPlantVisuals.GrowthPlantId)
            && StateCodec.Encode(service.Snapshot) == beforeGrowth, "finished plant cannot spend nutrients on more growth");
        clock.Now = clock.Now.AddDays(2);
        Assert(service.SavePlantPhoto("synthetic-second-button", "synthetic-individual-one"), "different request acknowledges first photo");
        Assert(PhotoXml(service.Snapshot) == photo && service.Snapshot.PlantPhotos.Count == 1,
            "new request preserves first capture date, recipe, scale and identity");
        Assert(!service.SetPlantCompleted("synthetic-individual-one", false) && PhotoXml(service.Snapshot) == photo,
            "finished stage cannot be reopened or change its album snapshot");

        Assert(service.StartNewPlant("synthetic-start-two", "synthetic-individual-one"), "explicit action creates the next seed");
        string secondId = service.Snapshot.PlantAppearance.Recipe.InstanceId;
        for (int i = 0; i < 21; i++) Assert(GrowPractice(service,"synthetic-second-growth-" + i), "second individual grows one procedural step per practice: "+i+" "+service.LastError);
        Assert(service.SavePlantPhoto("synthetic-photo-two", secondId), "another individual can be saved");
        var pictures = service.Snapshot.PlantPhotos;
        Assert(pictures.Count == 2 && pictures[0].Id != pictures[1].Id
            && pictures[0].Recipe.BaseSeed != pictures[1].Recipe.BaseSeed,
            "different individuals keep independent IDs, seeds and full recipes");
        Assert(PlantCodec.Encode(pictures[0]) == PlantCodec.Encode(initial.PlantPhotos[0]), "new current individual leaves older photo exact");
        int writes = store.Saves;
        Assert(service.SavePlantPhoto("synthetic-photo-one", "synthetic-individual-one") && store.Saves == writes,
            "old committed request retries after current individual changes without a write");
        Assert(!service.SavePlantPhoto("synthetic-photo-one", secondId), "request cannot be retargeted to another individual");
        Assert(!service.SavePlantPhoto("synthetic-unknown", "synthetic-unknown-instance"), "unknown individual cannot be captured");
        string album = PhotoXml(service.Snapshot);
        Assert(service.ResetMissionHistory() && PhotoXml(service.Snapshot) == album,
            "mission history reset preserves all album recipes and capture metadata");
        Assert(service.Snapshot.PlantAppearance.IsCompleted, "mission reset preserves explicit completion");
        service = Open(store, clock);
        Assert(PhotoXml(service.Snapshot) == album, "screen reopen and reload preserve all photos");
    }

    static void FailureRetryAndCommandIdentity()
    {
        var clock = new PhotoClock();
        var funded = Fixture(.2f, completed: true);
        funded.Coins = 400;
        var store = new PhotoStore { State = funded };
        var service = Open(store, clock);
        string before = StateCodec.Encode(service.Snapshot);
        store.Fail = true;
        Assert(!service.SavePlantPhoto("synthetic-retry", "garden-main"), "save failure is reported");
        Assert(StateCodec.Encode(service.Snapshot) == before && StateCodec.Encode(store.State) == before,
            "failed capture leaves recipe, completion, wallet, photo and receipt unchanged");
        store.Fail = false;
        int priorWrites=store.Saves;string originalCapture=service.Snapshot.PlantPhotos.Single().SavedUtc;
        clock.Now = clock.Now.AddMinutes(1);
        var tasks = Enumerable.Range(0, 8).Select(_ => Task.Run(() => service.SavePlantPhoto("synthetic-retry", "garden-main"))).ToArray();
        Task.WaitAll(tasks);
        Assert(tasks.All(task => task.Result) && service.Snapshot.PlantPhotos.Count == 1 && store.Saves == priorWrites+1,
            "concurrent retries commit one photo and one receipt");
        Assert(service.Snapshot.PlantPhotos[0].SavedUtc == originalCapture,
            "manual capture retains the original successful automatic capture time");
        store.Fail = true;
        Assert(service.SavePlantPhoto("synthetic-retry", "garden-main"), "already committed retry succeeds during later write failure");
        store.Fail = false;
        string committed = StateCodec.Encode(service.Snapshot);
        Assert(!service.GrowPlant("synthetic-retry", GardenPlantVisuals.GrowthPlantId), "photo request cannot become a growth purchase");
        Assert(!service.PurchaseEnvironment("synthetic-retry", "environment:E01", "left"), "photo request cannot become an environment purchase");
        Assert(StateCodec.Encode(service.Snapshot) == committed, "cross-action conflicts cannot debit or mutate state");
        Assert(service.PurchaseEnvironment("synthetic-purchase", "environment:E01", "left"), "independent original environment purchase works");
        committed = StateCodec.Encode(service.Snapshot);
        Assert(!service.SavePlantPhoto("synthetic-purchase", "garden-main") && StateCodec.Encode(service.Snapshot) == committed,
            "purchase request cannot become a photo command");
        Assert(!service.SavePlantPhoto("", "garden-main") && !service.SavePlantPhoto("synthetic-blank", " "), "blank identities rejected");
    }

    static void LegacyAndValidation()
    {
        var clock = new PhotoClock();
        var original = Fixture(.4f);
        string oldXml = ExceptAlbum(original, true);
        var old = StateCodec.Decode(oldXml);
        Assert(old.PlantPhotos.Count == 0 && old.PhotoSaveCommands.Count == 0 && !old.PlantAppearance.IsCompleted,
            "missing album and completion fields load empty and false");
        var store = new PhotoStore { State = old };
        var service = Open(store, clock);
        Assert(service.Snapshot.SchemaVersion==3&&store.Saves==1&&service.Snapshot.PlantAppearance.IsCompleted
            &&PlantCodec.Encode(service.Snapshot.PlantAppearance.Recipe)==PlantCodec.Encode(old.PlantAppearance.Recipe)
            &&service.CoinBalance==old.Coins&&service.Snapshot.LegacyGrowth==old.LegacyGrowth
            &&service.Snapshot.UnlockIds.SequenceEqual(old.UnlockIds)&&service.Snapshot.Sessions.Count==old.Sessions.Count
            &&service.Snapshot.PlantPhotos.Count==1,
            "legacy completion migrates once into the canonical collection and automatic album without changing recipe, money or history");
        Assert(service.Snapshot.PlantAppearance.Recipe.InstanceId == "garden-main", "legacy identity stays garden-main");
        Assert(service.SavePlantPhoto("synthetic-validation", "garden-main"), "legacy final-stage plant can be captured after migration");
        var valid = service.Snapshot;
        Assert(PhotoXml(StateCodec.Decode(StateCodec.Encode(valid))) == PhotoXml(valid), "XML roundtrip preserves every frozen recipe value");
        // New individuals use 21 steps; early completion still freezes the actual
        // lower stage instead of silently making the recipe mature.
        using(var future=Open(new PhotoStore(),clock))
        {
            for(int i=0;i<3;i++)Assert(GrowPractice(future,"synthetic-future-"+i),"prepare 21-step future photo");
            string futureRecipe=PlantCodec.Encode(future.Snapshot.PlantAppearance.Recipe);
            Assert(future.SetPlantCompleted(future.Snapshot.PlantAppearance.Recipe.InstanceId,true),"complete a 21-step individual early");
            var restoredFuture=StateCodec.Decode(StateCodec.Encode(future.Snapshot));
            Assert(restoredFuture.PlantPhotos.Single().Recipe.MaxGrowthSteps==21
                &&restoredFuture.PlantPhotos.Single().Recipe.GrowthStep==3&&!restoredFuture.PlantPhotos.Single().Recipe.Mature
                &&PlantCodec.Encode(restoredFuture.PlantPhotos.Single().Recipe)==futureRecipe,
                "album roundtrip preserves an exact low-stage 21-step snapshot without inferring maturity");
        }
        var invalid = StateCodec.Clone(valid); invalid.PlantPhotos.Add(invalid.PlantPhotos[0]); Reject(invalid, "duplicate photo rejected");
        invalid = StateCodec.Clone(valid); invalid.PlantPhotos[0].SourcePlantInstanceId = "synthetic-other"; Reject(invalid, "source/recipe identity mismatch rejected");
        invalid = StateCodec.Clone(valid); invalid.PlantPhotos[0].Scale = float.NaN; Reject(invalid, "nonfinite captured scale rejected");
        invalid = StateCodec.Clone(valid); invalid.PlantPhotos[0].Recipe = null; Reject(invalid, "missing frozen recipe rejected");
        invalid = StateCodec.Clone(valid); invalid.PhotoSaveCommands.Clear(); Reject(invalid, "missing durable save command rejected");
        invalid = StateCodec.Clone(valid); invalid.PhotoSaveCommands[0].PhotoId = "plant-photo:synthetic-missing"; Reject(invalid, "orphan command rejected");
        invalid = StateCodec.Clone(valid); invalid.PlantPhotos[0].Recipe.MeshVersion = 999; Reject(invalid, "unsupported snapshot recipe version rejected");
        invalid=Fixture(.1f);invalid.PlantAppearance.PreviousActivityKeys.AddRange(new[]{"synthetic-duplicate","synthetic-duplicate"});Reject(invalid,"legacy duplicate activity identity remains rejected");
        invalid=Fixture(.1f);invalid.PlantAppearance.IndividualGrowth=float.NaN;Reject(invalid,"legacy nonfinite individual growth remains rejected");
    }

    static string ExceptCurrentAndStarts(GardenState state)
    {
        var doc = new XmlDocument(); doc.LoadXml(StateCodec.Encode(state));
        foreach (string field in new[] { "PlantAppearance", "PlantStartCommands", "PlantCollection" })
            doc.DocumentElement.RemoveChild(doc.DocumentElement[field]);
        return doc.OuterXml;
    }
    static void ArchiveAndStartCycles()
    {
        var clock = new PhotoClock();
        var legacy = Fixture(.2f);
        legacy.LegacyGrowth = .1f;
        legacy.Plants.Add(new PlantProgress { PlantId = GardenPlantVisuals.GrowthPlantId, Growth = .1f });
        legacy.PurchaseReceipts.Add("synthetic-original-growth");
        legacy.PurchaseCommands.Add(new PurchaseCommand { RequestId = "synthetic-original-growth", Command = "growth|" + GardenPlantVisuals.GrowthPlantId });
        legacy.GrowthHistory.Add(new GardenGrowthRecord { RequestId = "synthetic-original-growth", PlantId = GardenPlantVisuals.GrowthPlantId,
            PlantInstanceId = "garden-main", CreatedUtc = clock.Now.ToString("O"), LinkedSessionId = "", BeforeGrowth = .1f, AfterGrowth = .2f });
        var store = new PhotoStore { State = legacy };
        var service = Open(store, clock);
        Assert(!service.StartNewPlant("synthetic-premature-start", "garden-main"), "a growing plant cannot be replaced by a new seed");
        Assert(service.GrowPlant("synthetic-original-growth", GardenPlantVisuals.GrowthPlantId), "historical paid growth receipt remains retryable");
        Assert(service.RefreshCycle(new string[0]) && service.BeginSession("synthetic-old-activity", "free-mission:M17")
            && service.StartSession("synthetic-old-activity") && service.CompleteSession("synthetic-old-activity"), "original completed mission fixture");
        Assert(service.SetPlantCompleted("garden-main", true), "early explicit completion before archive");
        Assert(service.Snapshot.PlantPhotos.Any(photo=>photo.SourcePlantInstanceId=="garden-main"), "explicit completion already preserves a source photo for successor creation");
        Assert(service.SavePlantPhoto("synthetic-cycle-photo", "garden-main"), "manual photo request acknowledges the automatically retained completed plant");
        string empty = StateCodec.Encode(service.Snapshot), album = PhotoXml(service.Snapshot);
        var archived = service.Snapshot;
        Assert(!service.GrowPlant("synthetic-empty-growth", GardenPlantVisuals.GrowthPlantId)
            && !service.SetPlantCompleted("garden-main", false) && StateCodec.Encode(service.Snapshot) == empty,
            "completed photographed individual cannot accept paid growth or reopen its recipe");
        Assert(!service.StartNewPlant("synthetic-wrong-source", "synthetic-other"), "stale source cannot start a seed");
        store.Fail = true;
        Assert(!service.StartNewPlant("synthetic-new-seed", "garden-main"), "new seed write failure is reported");
        string attemptedRecipe = PlantCodec.Encode(store.LastAttempt.PlantAppearance.Recipe);
        Assert(StateCodec.Encode(service.Snapshot) == empty && StateCodec.Encode(store.State) == empty,
            "failed seed creation preserves completed plant, photo, history and economy exactly");
        store.Fail = false;
        service = Open(store, clock);
        int writes = store.Saves;
        var tasks = Enumerable.Range(0, 8).Select(_ => Task.Run(() => service.StartNewPlant("synthetic-new-seed", "garden-main"))).ToArray();
        Task.WaitAll(tasks);
        Assert(tasks.All(task => task.Result) && store.Saves == writes + 1 && service.Snapshot.PlantStartCommands.Count == 1,
            "concurrent starts produce one persisted new individual");
        var seed = service.Snapshot;
        string secondId = seed.PlantAppearance.Recipe.InstanceId;
        Assert(secondId != "garden-main" && seed.PlantAppearance.Recipe.BaseSeed != archived.PlantAppearance.Recipe.BaseSeed
            && PlantCodec.Encode(seed.PlantAppearance.Recipe) == attemptedRecipe,
            "restart retry retains deterministic new identity and seed, distinct from archived plant");
        Assert(GardenPlantVisuals.HasCurrentPlant(seed) && GardenPlantVisuals.CanGrow(seed)
            && !seed.PlantAppearance.IsCompleted && seed.PlantAppearance.Recipe.GrowthStep == 0 && GardenPlantVisuals.TotalGrowth(seed) == 0,
            "explicit next plant begins as a growable zero-progress seed");
        Assert(ExceptCurrentAndStarts(seed) == ExceptCurrentAndStarts(archived)
            &&PlantCodec.Encode(seed.PlantCollection.Plants.Take(archived.PlantCollection.Plants.Count).ToList())==PlantCodec.Encode(archived.PlantCollection.Plants),
            "starting a plant preserves all old growth, mission, reward, balance and album fields exactly");
        Assert(seed.PlantAppearance.Credits.Count == 0 && seed.PlantAppearance.Recipe.Counts.All(count=>count==0),
            "historical activity is excluded from new seed profile");
        var detached = service.Snapshot;
        detached.PlantAppearance.PreviousActivityKeys.Clear(); detached.PlantStartCommands[0].PlantInstanceId = "synthetic-mutated";
        Assert(StateCodec.Encode(service.Snapshot) == StateCodec.Encode(seed), "new lifecycle lists are detached on snapshot reads");
        service = Open(store, clock);
        Assert(StateCodec.Encode(service.Snapshot) == StateCodec.Encode(seed), "reopen cannot replay historical activity into a new seed");
        writes = store.Saves; store.Fail = true;
        Assert(service.StartNewPlant("synthetic-new-seed", "garden-main") && store.Saves == writes,
            "committed seed request acknowledges without writing during a later disk failure");
        store.Fail = false;
        Assert(service.SavePlantPhoto("synthetic-cycle-photo", "garden-main") && StateCodec.Encode(service.Snapshot) == StateCodec.Encode(seed),
            "old exact photo retry cannot remove the new current plant");
        Assert(!service.SavePlantPhoto("synthetic-stale-save", "garden-main")
            && !service.StartNewPlant("synthetic-second-click", "garden-main")
            && !service.StartNewPlant("synthetic-new-seed", secondId), "stale or conflicting lifecycle commands cannot replace current plant");
        Assert(!service.GrowPlant("synthetic-new-seed", GardenPlantVisuals.GrowthPlantId)
            && !service.PurchaseEnvironment("synthetic-new-seed", "environment:E01", "left")
            && !service.SavePlantPhoto("synthetic-new-seed", secondId)
            && !service.StartNewPlant("synthetic-cycle-photo", "garden-main")
            && !service.StartNewPlant("synthetic-original-growth", "garden-main"), "lifecycle and purchase request namespaces cannot overlap");
        Assert(service.GrowPlant("synthetic-original-growth", GardenPlantVisuals.GrowthPlantId)
            && GardenPlantVisuals.TotalGrowth(service.Snapshot) == 0 && service.Snapshot.Nutrient == seed.Nutrient,
            "old growth retry cannot grow or charge the new individual");
        Assert(!service.GrowPlant("synthetic-wrong-growth-target", "plant:P01"), "new individual cannot be bypassed with another species key");
        Assert(service.AdvancePreviewDay(), "new individual practice uses a new daily slot");
        Assert(service.BeginSession("synthetic-current-growth", "free-mission:M17")
            && service.StartSession("synthetic-current-growth"), "new current practice begins");
        string beforeNewGrowth = StateCodec.Encode(service.Snapshot);
        var expectedRecipe=service.Snapshot.PlantAppearance.Recipe;GrowthCategory expectedCategory;
        Assert(GardenPlantVisuals.TryCategory("free-mission:M17",out expectedCategory),"known practice has an explicit growth category");
        expectedRecipe.Counts[(int)expectedCategory]++;expectedRecipe=PlantGenerator.Advance(expectedRecipe);
        store.Fail = true;
        Assert(!service.CommitPracticeGrowth("synthetic-current-growth")
            && StateCodec.Encode(service.Snapshot) == beforeNewGrowth && StateCodec.Encode(store.State) == beforeNewGrowth,
            "new individual growth write failure preserves seed, wallet, photo and all historical records");
        store.Fail = false;
        Assert(service.CommitPracticeGrowth("synthetic-current-growth"), "new current individual grows");
        var grown = service.Snapshot;
        Assert(PlantCodec.Encode(grown.PlantAppearance.Recipe) == PlantCodec.Encode(expectedRecipe),
            "new individual growth retry commits exactly its preview recipe");
        Assert(service.CommitPracticeGrowth("synthetic-current-growth")
            && StateCodec.Encode(service.Snapshot) == StateCodec.Encode(grown), "new individual duplicate growth request cannot charge or advance twice");
        Assert(grown.PlantAppearance.Recipe.GrowthStep == 1 && grown.Coins == seed.Coins
            && grown.LegacyGrowth == archived.LegacyGrowth && PlantCodec.Encode(grown.Plants) == PlantCodec.Encode(archived.Plants)
            && PlantCodec.Encode(grown.GrowthHistory.Take(archived.GrowthHistory.Count).ToList()) == PlantCodec.Encode(archived.GrowthHistory),
            "new practice growth preserves coins and all prior plant progress and historical records");
        var currentGrowth = service.GetPracticeGrowth("synthetic-current-growth");
        Assert(currentGrowth.PlantInstanceId == secondId && currentGrowth.BeforeStep == 0
            && currentGrowth.AfterStep == 1, "new practice growth identifies its individual and independent progress");
        var invalid = StateCodec.Clone(grown); invalid.PracticeGrowthRecords.Last().PlantInstanceId = "synthetic-missing-plant";
        Reject(invalid, "growth history cannot point to a nonexistent individual");
        invalid = StateCodec.Clone(grown); invalid.PlantStartCommands.Add(invalid.PlantStartCommands[0]);
        Reject(invalid, "duplicate seed command rejected");
        invalid = StateCodec.Clone(grown); invalid.PlantStartCommands[0].ArchivedPlantInstanceId = "synthetic-missing-source";
        Reject(invalid, "seed command must reference an archived photo");
        invalid = StateCodec.Clone(grown); invalid.PlantStartCommands[0].PlantInstanceId = "synthetic-missing-current";
        Reject(invalid, "seed command must reference its current or subsequently archived individual");
        invalid = StateCodec.Clone(grown); invalid.PlantCollection.Decisions.Add(invalid.PlantCollection.Decisions.Last());
        Reject(invalid, "duplicate canonical growth receipt rejected");
        invalid = StateCodec.Clone(grown); WeeklyPlants.Active(invalid).RenderScale=float.NaN;
        Reject(invalid, "nonfinite current render scale rejected");
        Assert(service.CompleteSession("synthetic-current-growth"), "finish optional reflection without additional growth");
        Assert(service.AdvancePreviewDay(), "next profile activity uses another daily slot");
        Assert(service.BeginSession("synthetic-new-activity", "free-mission:M01") && service.StartSession("synthetic-new-activity")
            && service.CompleteSession("synthetic-new-activity"), "new activity can still earn normal reward and appearance credit");
        Assert(service.Snapshot.PlantCollection.Decisions.Count(record=>record.PlantId==secondId)==2 && service.Snapshot.PlantAppearance.Recipe.Counts.Sum()==2
            && PhotoXml(service.Snapshot) == album, "only new activity affects current profile; frozen photo remains exact");
        Assert(service.SetPlantCompleted(secondId, true) && service.SavePlantPhoto("synthetic-second-cycle-photo", secondId), "second independent plant can be archived");
        string secondEmpty = StateCodec.Encode(service.Snapshot);
        Assert(service.StartNewPlant("synthetic-new-seed", "garden-main") && StateCodec.Encode(service.Snapshot) == secondEmpty,
            "old start retry cannot refill the garden after its created individual was later archived");
        Assert(!service.StartNewPlant("synthetic-stale-next", "garden-main") && service.StartNewPlant("synthetic-third-seed", secondId),
            "third seed requires the most recently archived source");
        Assert(service.Snapshot.PlantPhotos.Count == 2 && service.Snapshot.PlantStartCommands.Count == 2
            && GardenPlantVisuals.TotalGrowth(service.Snapshot) == 0, "multiple cycles retain both independent photos and reset only current growth");
        string thirdRecipe = PlantCodec.Encode(service.Snapshot.PlantAppearance.Recipe);
        Assert(service.ResetMissionHistory() && PlantCodec.Encode(service.Snapshot.PlantAppearance.Recipe) == thirdRecipe
            && service.Snapshot.PlantPhotos.Count == 2 && service.Snapshot.PlantStartCommands.Count == 2,
            "history reset preserves photos, seed identity and lifecycle receipts");
        service = Open(store, clock);
        Assert(PlantCodec.Encode(service.Snapshot.PlantAppearance.Recipe) == thirdRecipe, "new cycle survives history reset and restart unchanged");
        Assert(service.BeginSession("synthetic-reset-old-activity", "free-mission:M01") && service.StartSession("synthetic-reset-old-activity")
            && service.CompleteSession("synthetic-reset-old-activity"), "reset mission can be completed using existing reward path");
        Assert(service.Snapshot.PlantAppearance.Credits.Count == 0 && PlantCodec.Encode(service.Snapshot.PlantAppearance.Recipe) == thirdRecipe,
            "a reset cannot replay an old cycle/mission appearance credit into a later plant");
        Assert(service.AdvancePreviewDay() && service.BeginSession("synthetic-next-day-activity", "free-mission:M01")
            && service.StartSession("synthetic-next-day-activity") && service.CompleteSession("synthetic-next-day-activity"),
            "a genuinely new day permits the same mission again");
        Assert(service.Snapshot.PlantAppearance.Recipe.GrowthStep==1&&service.Snapshot.PlantAppearance.Recipe.Counts.Sum()==1, "durable prior receipts do not block future occurrences of the same mission");
    }

    static void ArchiveMigration()
    {
        var clock = new PhotoClock();
        var store = new PhotoStore { State = Fixture(.3f) };
        var service = Open(store, clock);
        Assert(service.SavePlantPhoto("synthetic-old-photo", "garden-main"), "old-photo migration fixture");
        var priorPhotoState=service.Snapshot;
        var previousVersion=Fixture(.3f,completed:true);
        previousVersion.PlantPhotos=priorPhotoState.PlantPhotos;previousVersion.PhotoSaveCommands=priorPhotoState.PhotoSaveCommands;
        previousVersion.PlantAppearance.IsArchived=true;
        store.State = previousVersion;
        string old = StateCodec.Encode(previousVersion);
        store.Fail = true;
        var reopening = new GardenStateService(store, clock, new DemoBalanceConfig());
        Assert(!reopening.Open() && StateCodec.Encode(store.State) == old, "failed archive migration leaves prior state exact");
        store.Fail = false; int writes = store.Saves;
        Assert(reopening.Open() && store.Saves == writes + 1 && GardenPlantVisuals.HasCurrentPlant(reopening.Snapshot)
            &&reopening.Snapshot.SchemaVersion==3&&reopening.Snapshot.PlantAppearance.IsCompleted,
            "historical empty planter migrates atomically to a visible completed individual");
        var normalized=reopening.Snapshot;
        Assert(PhotoXml(normalized)==PhotoXml(previousVersion)&&PlantCodec.Encode(normalized.PlantAppearance.Recipe)==PlantCodec.Encode(previousVersion.PlantAppearance.Recipe)
            &&normalized.Coins==previousVersion.Coins&&normalized.GrowthHistory.Count==previousVersion.GrowthHistory.Count,
            "archive migration preserves the original photo, recipe, wallet and growth history");
        writes = store.Saves;
        service = Open(store, clock);
        Assert(store.Saves == writes && service.SavePlantPhoto("synthetic-old-photo", "garden-main") && store.Saves == writes,
            "migration and old photo retry never recreate a plant or duplicate a photo");
        var legacy = Fixture(0); legacy.PlantAppearance = null; legacy.PlantPhotos.Clear(); legacy.PhotoSaveCommands.Clear();
        var legacyService = Open(new PhotoStore { State = legacy }, clock);
        Assert(GardenPlantVisuals.HasCurrentPlant(legacyService.Snapshot) && legacyService.Snapshot.PlantAppearance.Recipe.InstanceId == "garden-main",
            "absent appearance in genuinely old saves still initializes the original legacy seed");
    }

    static void ArchiveBeforeReflection()
    {
        var clock = new PhotoClock();
        var store = new PhotoStore { State = Fixture(.2f) };
        var service = Open(store,clock);
        int remaining=service.Snapshot.PlantAppearance.Recipe.MaxGrowthSteps-service.Snapshot.PlantAppearance.Recipe.GrowthStep;
        for (int i=0;i<remaining-1;i++) Assert(GrowPractice(service,"synthetic-before-reflection-" + i), "prepare last real practice step");
        const string session = "synthetic-final-without-reflection";
        Assert(service.AdvancePreviewDay(), "last plant practice has its own daily slot");
        Assert(service.BeginSession(session,"free-mission:M17") && service.StartSession(session)
            && service.CommitPracticeGrowth(session), "last practice completes the plant before optional reflection");
        Assert(service.Snapshot.Sessions.Last().Status == ParticipationState.InProgress && service.Snapshot.PlantAppearance.IsCompleted,
            "practice growth and final participation status remain independent");
        var receipt = service.GetPracticeGrowth(session);
        Assert(service.SavePlantPhoto("synthetic-before-reflection-photo","garden-main"), "completed practice plant can be archived before old reflection finalization");
        string empty=StateCodec.Encode(service.Snapshot);int coins=service.CoinBalance;
        Assert(service.PauseSession(session)&&service.ResumeSession(session)&&service.CommitPracticeGrowth(session)
            &&StateCodec.Encode(service.Snapshot)==empty&&service.MissionPlantAvailability==null,
            "completed visible plant permits draft pause/resume while the original growth receipt stays idempotent");
        Assert(service.StartNewPlant("synthetic-before-reflection-next","garden-main"), "explicit next seed preserves the original practice receipt");
        string seed = PlantCodec.Encode(service.Snapshot.PlantAppearance.Recipe), album = PhotoXml(service.Snapshot);
        Assert(service.CommitPracticeGrowth(session) && service.GetPracticeGrowth(session).PlantInstanceId == receipt.PlantInstanceId
            && PlantCodec.Encode(service.Snapshot.PlantAppearance.Recipe) == seed,
            "practice reentry remains bound to the previous individual after replacement");
        Assert(service.CompleteSession(session,"synthetic mood","synthetic note") && service.Snapshot.StoredGrowthSteps == 0 && service.CoinBalance==coins+30
            && PhotoXml(service.Snapshot) == album && PlantCodec.Encode(service.Snapshot.PlantAppearance.Recipe) == seed,
            "late reflection changes neither the saved photograph nor successor stage/profile and cannot credit the growth bank");
        service = Open(store,clock);
        Assert(PhotoXml(service.Snapshot) == album && PlantCodec.Encode(service.Snapshot.PlantAppearance.Recipe) == seed,
            "late reflection binding survives restart without replaying old activity into the successor");
    }

    static void RealFileFailureAndRestart()
    {
        string temporaryRoot = Path.GetFullPath(Path.GetTempPath());
        string directory = Path.GetFullPath(Path.Combine(temporaryRoot, "capstone-plant-photo-tests-" + Guid.NewGuid().ToString("N")));
        if (!directory.StartsWith(temporaryRoot, StringComparison.OrdinalIgnoreCase)) throw new Exception("Unsafe synthetic test path");
        Directory.CreateDirectory(directory);
        string path = Path.Combine(directory, "state.xml");
        var clock = new PhotoClock();
        string album, newRecipe = null;
        try
        {
            using (var file = new FileStateStore(path))
            {
                file.Save(Fixture(.2f, completed: true));
                var service = Open(file, clock);
                byte[] original = File.ReadAllBytes(path);
                Directory.CreateDirectory(path + ".tmp");
                Assert(!service.SavePlantPhoto("synthetic-disk-photo", "garden-main")&&service.Snapshot.PlantPhotos.Count==1
                    &&!service.Snapshot.PhotoSaveCommands.Any(command=>command.RequestId=="synthetic-disk-photo"),
                    "real staging-path failure preserves the automatic photo without publishing a manual receipt");
                Assert(original.SequenceEqual(File.ReadAllBytes(path)), "real failed write preserves original file byte-for-byte");
                Directory.Delete(path + ".tmp");
                Assert(service.SavePlantPhoto("synthetic-disk-photo", "garden-main"), "real filesystem retry succeeds");
                album = PhotoXml(service.Snapshot);
            }
            using (var file = new FileStateStore(path))
            {
                var service = Open(file, clock);
                byte[] original = File.ReadAllBytes(path);
                Assert(PhotoXml(service.Snapshot) == album && service.Snapshot.PlantAppearance.IsCompleted,
                    "new service restores exact photos and explicit completion from disk");
                Assert(GardenPlantVisuals.HasCurrentPlant(service.Snapshot)&&service.Snapshot.PlantAppearance.IsCompleted, "real restart restores the completed visible plant and its photo");
                Assert(service.SavePlantPhoto("synthetic-disk-photo", "garden-main") && original.SequenceEqual(File.ReadAllBytes(path)),
                    "restart duplicate acknowledges without rewriting disk");
                Directory.CreateDirectory(path + ".tmp");
                Assert(!service.StartNewPlant("synthetic-disk-seed", "garden-main")
                    && GardenPlantVisuals.HasCurrentPlant(service.Snapshot) && original.SequenceEqual(File.ReadAllBytes(path)),
                    "real seed staging failure leaves photo and completed plant bytes unchanged");
                Directory.Delete(path + ".tmp");
                Assert(service.StartNewPlant("synthetic-disk-seed", "garden-main"), "real seed creation retry succeeds");
                newRecipe = PlantCodec.Encode(service.Snapshot.PlantAppearance.Recipe);
            }
            using (var file = new FileStateStore(path))
            {
                var service = Open(file, clock);
                byte[] original = File.ReadAllBytes(path);
                Assert(GardenPlantVisuals.HasCurrentPlant(service.Snapshot) && GardenPlantVisuals.TotalGrowth(service.Snapshot) == 0
                    && PlantCodec.Encode(service.Snapshot.PlantAppearance.Recipe) == newRecipe && PhotoXml(service.Snapshot) == album,
                    "new process restores new seed identity and full recipe while preserving the old photo");
                Assert(service.StartNewPlant("synthetic-disk-seed", "garden-main")
                    && service.SavePlantPhoto("synthetic-disk-photo", "garden-main") && original.SequenceEqual(File.ReadAllBytes(path)),
                    "old start and archive retries never alter the new seed or rewrite disk after restart");
            }
            byte[] backup = File.ReadAllBytes(path + ".bak");
            File.WriteAllText(path, "synthetic-corruption");
            using (var file = new FileStateStore(path))
            {
                var service = new GardenStateService(file, clock, new DemoBalanceConfig());
                Assert(!service.Open(), "damaged primary does not fall back to an older album or new garden");
            }
            Assert(backup.SequenceEqual(File.ReadAllBytes(path + ".bak")), "failed recovery leaves backup unchanged");
        }
        finally { Directory.Delete(directory, true); }
    }

    static string CompleteMemoDay(GardenStateService service,string prefix,string basicCode)
    {
        Assert(service.SelectMbctProgram("8w") && service.RefreshCycle(new string[0]),"memo fixture chooses a real basic course");
        Assert(service.BeginFocusSession(prefix+"-basic",basicCode)
            && service.StartGuidedFocusSession(prefix+"-basic",basicCode=="P01"?"코":"")
            && service.CompleteGuidedFocusPractice(prefix+"-basic",basicCode=="P01"?"코":"","",60)
            && service.CompletePracticeSession(prefix+"-basic"),"memo fixture completes guided basic practice");
        Assert(service.BeginSession(prefix+"-additional","free-mission:M03") && service.StartSession(prefix+"-additional")
            && service.CompletePracticeSession(prefix+"-additional"),"memo fixture completes the independent additional practice");
        Assert(service.BeginDailyEmotion(service.Snapshot.LastCycleId),"real P/M completions unlock the daily memo");
        return DailyEmotionPolicy.Current(service.Snapshot).Id;
    }

    static void MemoOwnershipAndPhotoSnapshots()
    {
        var clock=new PhotoClock();var store=new PhotoStore {State=Fixture(.1f)};var service=Open(store,clock);
        string emotionId=CompleteMemoDay(service,"synthetic-memo-one","P01");
        Assert(service.CoinBalance==77+50+30,"fresh basic and additional missions add 50 and 30 coins respectively");
        Assert(service.SaveDailyEmotionDraft(emotionId,"기쁨","synthetic first plant note",0),"memo draft is durable before completion");
        string before=StateCodec.Encode(service.Snapshot),recipe=PlantCodec.Encode(service.Snapshot.PlantAppearance.Recipe);
        store.Fail=true;
        Assert(!service.CompleteDailyEmotion(emotionId,"기쁨","synthetic first plant note")
            && StateCodec.Encode(service.Snapshot)==before && StateCodec.Encode(store.State)==before,
            "failed memo completion rolls back coins, text, identity and completed state together");
        string attemptedMemo=store.LastAttempt.PlantAppearance.Memos.Single().Id;
        store.Fail=false;
        Assert(service.CompleteDailyEmotion(emotionId,"기쁨","synthetic first plant note"),"memo completion retry succeeds");
        var memo=service.Snapshot.PlantAppearance.Memos.Single();
        Assert(memo.Id==attemptedMemo && memo.DailyEmotionRecordId==emotionId && memo.PlantInstanceId=="garden-main"
            && memo.Note=="synthetic first plant note" && memo.Mood=="기쁨", "memo retry has stable source, owner and original content");
        Assert(service.CoinBalance==77+50+30+100 && service.GetDailyEmotion(emotionId).RewardVersion==1
            && service.GetDailyEmotion(emotionId).RewardAmount==100 && service.GetDailyEmotion(emotionId).MemoId==memo.Id
            && service.GetPracticeGrowth(emotionId)==null && PlantCodec.Encode(service.Snapshot.PlantAppearance.Recipe)==recipe,
            "emotion completion grants one hundred coins and one memo without changing growth or its bank");
        string memoXml=PlantCodec.Encode(memo),done=StateCodec.Encode(service.Snapshot);
        memo.Note="mutated detached value";service.Snapshot.PlantAppearance.Memos.Clear();
        Assert(StateCodec.Encode(service.Snapshot)==done,"current memo snapshot reads detach lists and each item");
        store.Fail=true;
        Assert(service.CompleteDailyEmotion(emotionId,"different","different") && StateCodec.Encode(service.Snapshot)==done,
            "successful completion retry is read-only even during a later storage failure");
        store.Fail=false;
        Assert(service.SetPlantCompleted("garden-main",true),"memo plant can explicitly complete");
        before=StateCodec.Encode(service.Snapshot);store.Fail=true;
        Assert(!service.SavePlantPhoto("synthetic-memo-photo","garden-main") && StateCodec.Encode(service.Snapshot)==before,
            "failed photo preserves the current memo and plant without creating an archive");
        store.Fail=false;
        Assert(service.SavePlantPhoto("synthetic-memo-photo","garden-main"),"memo and plant archive together");
        string album=PhotoXml(service.Snapshot);
        Assert(PlantCodec.Encode(service.Snapshot.PlantPhotos.Single().Memos.Single())==memoXml,
            "photo freezes all memo data with its original individual");
        var detached=service.Snapshot;detached.PlantPhotos[0].Memos[0].Note="detached photo change";detached.PlantPhotos[0].Memos.Clear();
        Assert(PhotoXml(service.Snapshot)==album,"photo memo list and content are deeply detached");
        before=StateCodec.Encode(service.Snapshot);
        Assert(service.CompleteDailyEmotion(emotionId,"ignored","ignored") && StateCodec.Encode(service.Snapshot)==before,
            "completed emotion retry remains safe after automatic photo retention");
        Assert(service.StartNewPlant("synthetic-memo-next","garden-main") && service.Snapshot.PlantAppearance.Memos.Count==0,
            "next seed starts without moving or copying the archived plant's memos");
        string nextId=service.Snapshot.PlantAppearance.Recipe.InstanceId;
        Assert(service.AdvancePreviewDay(),"second memo uses the next business day");
        string secondEmotion=CompleteMemoDay(service,"synthetic-memo-two","P02");
        Assert(service.CompleteDailyEmotion(secondEmotion,"","synthetic second plant note") && service.CoinBalance==77+2*(50+30+100),
            "another day independently awards its two practices and single note-only emotion completion");
        Assert(service.Snapshot.PlantAppearance.Memos.Single().PlantInstanceId==nextId
            && service.Snapshot.PlantAppearance.Memos.Single().Id!=attemptedMemo && PhotoXml(service.Snapshot)==album,
            "successor memo is independent and never changes an archived photo");
        var invalid=service.Snapshot;invalid.PlantPhotos[0].Memos[0].PlantInstanceId=nextId;
        Reject(invalid,"memo cannot be rebound to another plant instance");
        invalid=service.Snapshot;invalid.PlantAppearance.Memos.Add(invalid.PlantAppearance.Memos[0]);
        Reject(invalid,"duplicate memo IDs are rejected");
        invalid=service.Snapshot;invalid.PlantPhotos[0].Memos=null;
        Reject(invalid,"missing photo memo list is rejected rather than silently dropping it");
        string currentMemos=PlantCodec.Encode(service.Snapshot.PlantAppearance.Memos);
        Assert(service.ResetMissionHistory() && service.CoinBalance==77+2*(50+30+100) && PhotoXml(service.Snapshot)==album
            && PlantCodec.Encode(service.Snapshot.PlantAppearance.Memos)==currentMemos,
            "history reset preserves both plant-owned memos and durable coin claims");
        service=Open(store,clock);
        secondEmotion=CompleteMemoDay(service,"synthetic-memo-reset","P01");
        Assert(service.CompleteDailyEmotion(secondEmotion,"","synthetic duplicate after reset") && service.CoinBalance==77+2*(50+30+100)
            && service.GetDailyEmotion(secondEmotion).RewardAmount==0 && service.GetDailyEmotion(secondEmotion).MemoId==""
            && PlantCodec.Encode(service.Snapshot.PlantAppearance.Memos)==currentMemos && PhotoXml(service.Snapshot)==album,
            "reset and repeat commands cannot grant coins or create another memo for an already claimed day");
    }

    static void PlantPeriodPersistence()
    {
        var clock = new PhotoClock { Now = DateTimeOffset.Parse("2026-01-05T03:59:00Z") };
        var store = new PhotoStore(); var service = Open(store, clock);
        var current = service.Snapshot.PlantAppearance;
        Assert(current.StartedUtc == clock.UtcNow.ToString("O") && current.StartedCycleId == "2026-01-04",
            "First install captures exact first-seed instant and pre-04:00 business date");
        string start = current.StartedUtc, cycle = current.StartedCycleId, source = current.Recipe.InstanceId;
        string capturedUtc=clock.UtcNow.ToString("O");
        Assert(service.SetPlantCompleted(source, true), "Complete synthetic first seed for period archive");
        clock.Now = DateTimeOffset.Parse("2026-01-06T04:00:00Z");
        string before = StateCodec.Encode(service.Snapshot); store.Fail = true;
        Assert(!service.SavePlantPhoto("synthetic-period-photo", source) && StateCodec.Encode(service.Snapshot) == before
            && StateCodec.Encode(store.State) == before, "Failed archive preserves plant and original start period atomically");
        store.Fail = false;
        Assert(service.SavePlantPhoto("synthetic-period-photo", source), "Archive period retry");
        var photo = service.Snapshot.PlantPhotos.Single();
        Assert(photo.StartedUtc == start && photo.StartedCycleId == cycle && photo.SavedCycleId == "2026-01-04"
            && photo.SavedUtc == capturedUtc, "Photo freezes original start and the actual automatic-completion date");
        string album = PhotoXml(service.Snapshot); int writes = store.Saves;
        clock.Now = clock.Now.AddDays(2);
        Assert(service.SavePlantPhoto("synthetic-period-photo", source) && store.Saves == writes && PhotoXml(service.Snapshot) == album,
            "Duplicate archive cannot change dates when the real clock advances");
        before = StateCodec.Encode(service.Snapshot); store.Fail = true;
        Assert(!service.StartNewPlant("synthetic-period-new", source) && StateCodec.Encode(service.Snapshot) == before,
            "Failed successor creation does not replace first plant period or frozen photo");
        store.Fail = false; Assert(service.StartNewPlant("synthetic-period-new", source), "Retry successor creation");
        var successor = service.Snapshot.PlantAppearance;
        Assert(successor.StartedUtc == clock.UtcNow.ToString("O") && successor.StartedCycleId == "2026-01-08"
            && successor.Recipe.InstanceId != source && PhotoXml(service.Snapshot) == album, "Successor has its own actual start while old period remains frozen");
        string nextStart = successor.StartedUtc; writes = store.Saves; clock.Now = clock.Now.AddHours(3);
        Assert(service.StartNewPlant("synthetic-period-new", source) && store.Saves == writes
            && service.Snapshot.PlantAppearance.StartedUtc == nextStart, "Repeated seed command preserves first successful start time");
        service.Dispose(); service = Open(store, clock);
        Assert(service.Snapshot.PlantAppearance.StartedUtc == nextStart && PhotoXml(service.Snapshot) == album, "Restart restores both periods exactly");
        var detached = service.Snapshot; detached.PlantAppearance.StartedUtc = "detached"; detached.PlantPhotos[0].StartedCycleId = "detached";
        Assert(service.Snapshot.PlantAppearance.StartedUtc == nextStart && PhotoXml(service.Snapshot) == album, "Detached period fields cannot mutate saved state");
        service.Dispose();

        var legacy = Fixture(.1f, completed: true); var oldStore = new PhotoStore { State = legacy };
        using (var old = Open(oldStore, clock))
        {
            Assert(old.Snapshot.PlantAppearance.StartedUtc == "" && old.Snapshot.PlantAppearance.StartedCycleId == "",
                "Loaded old recipe does not invent a planting date from current time");
            Assert(old.SavePlantPhoto("synthetic-period-legacy", "garden-main"), "Archive older plant with unknown start");
            var unknown = old.Snapshot.PlantPhotos.Single();
            Assert(unknown.StartedUtc == "" && unknown.StartedCycleId == "" && unknown.SavedCycleId == old.CurrentCycleId,
                "New archive preserves unknown historical start while recording known end date");
        }
        var oldXml = new XmlDocument(); oldXml.LoadXml(StateCodec.Encode(oldStore.State));
        foreach (string name in new[] { "StartedUtc", "StartedCycleId", "SavedCycleId" })
            foreach (XmlNode node in oldXml.SelectNodes("//" + name).Cast<XmlNode>().ToArray()) node.ParentNode.RemoveChild(node);
        oldStore.State = StateCodec.Decode(oldXml.OuterXml); string originalSaved = oldStore.State.PlantPhotos.Single().SavedUtc;
        using (var restoredOld = Open(oldStore, clock))
        {
            var unknown = restoredOld.Snapshot.PlantPhotos.Single();
            Assert(unknown.StartedUtc == "" && unknown.StartedCycleId == "" && unknown.SavedCycleId == "" && unknown.SavedUtc == originalSaved,
                "Old XML without period fields stays unknown and keeps original archive timestamp");
        }
        using (var migrated = new GardenStateService(new PhotoStore(), clock, new DemoBalanceConfig(), new LegacySnapshot { HasExistingState = true }))
        {
            Assert(migrated.Open() && migrated.Snapshot.PlantAppearance.StartedUtc == "" && migrated.Snapshot.PlantAppearance.StartedCycleId == "",
                "Existing legacy installation with empty balances is not mistaken for first install");
        }
    }

    static void Main()
    {
        ExplicitCompletion(); FrozenPicturesAndIndividuals(); FailureRetryAndCommandIdentity();
        LegacyAndValidation(); ArchiveAndStartCycles(); ArchiveMigration(); ArchiveBeforeReflection(); RealFileFailureAndRestart();
        MemoOwnershipAndPhotoSnapshots(); PlantPeriodPersistence();
        Console.WriteLine("PASS " + checks + " plant-photo assertions (synthetic isolated storage; no Unity/device data)");
    }
}
