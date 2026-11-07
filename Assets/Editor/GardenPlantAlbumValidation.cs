using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using CapstoneDesign.Plants;
using CapstoneDesign.Prototype;
using CapstoneDesign.Runtime;
using CapstoneDesign.Runtime.LocalState;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Fixture = CapstoneDesign.EditorTools.FocusMissionValidation.Fixture;
using MemoryStore = CapstoneDesign.EditorTools.FocusMissionValidation.MemoryStore;
using Clock = CapstoneDesign.EditorTools.FocusMissionValidation.SyntheticClock;
using Report = CapstoneDesign.EditorTools.FocusMissionValidation.Report;

namespace CapstoneDesign.EditorTools
{
    // Explicit requests only. The shared fixture renders production UI in a preview
    // scene and owns a synthetic memory store. The file test uses a fresh directory
    // beneath this run's ignored artifacts; no personal save or device is accessed.
    [InitializeOnLoad]
    public static class GardenPlantAlbumValidation
    {
        [Serializable] public sealed class Request { public string runId; public bool capture = true; public bool regressions; }
        static bool running;
        static HashSet<int> nativeCameraBaseline = new HashSet<int>();
        static double nextPoll;
        static string Root => Path.Combine(Directory.GetCurrentDirectory(), "artifacts", "plant-album-20261105");
        static string RequestPath => Path.Combine(Root, "request.json");
        static GardenPlantAlbumValidation() { EditorApplication.update += Poll; }

        static void Poll()
        {
            if (running || EditorApplication.timeSinceStartup < nextPoll) return;
            nextPoll = EditorApplication.timeSinceStartup + 1;
            if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode
                || !File.Exists(RequestPath)) return;
            var request = JsonUtility.FromJson<Request>(File.ReadAllText(RequestPath));
            File.Delete(RequestPath);
            Execute(request ?? new Request());
        }

        [MenuItem("Capstone Mockup/Validate Plant Photo Album (Synthetic)")]
        public static void Run() => Execute(new Request { runId = "manual-" + DateTimeOffset.UtcNow.ToString("yyyyMMdd-HHmmss") });

        static void Execute(Request request)
        {
            if (running || EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling)
                throw new InvalidOperationException("Run the album validation after compilation, outside Play mode.");
            string runId = request.runId ?? "run-" + DateTimeOffset.UtcNow.ToString("yyyyMMdd-HHmmss");
            Assert(Regex.IsMatch(runId, "^[A-Za-z0-9][A-Za-z0-9_-]{0,80}$"), "Use a simple, unique run ID.");
            string output = Path.Combine(Root, runId);
            Assert(!Directory.Exists(output), "Use a fresh run ID; earlier evidence is never overwritten.");
            Directory.CreateDirectory(output);
            var report = new Report {
                runId = runId, utc = DateTimeOffset.UtcNow.ToString("O"), unityVersion = Application.unityVersion,
                environment = "Windows Unity Editor; synthetic memory/file saves; production album UI in isolated preview scene; visible-center raycasts; no Android device"
            };
            var priorCameras = NativeStudioCameras();
            nativeCameraBaseline = new HashSet<int>(priorCameras.Select(camera => camera.GetInstanceID()));
            report.checks.Add(new FocusMissionValidation.CheckResult {
                name = "Pre-existing native album cameras before this run", status = "info",
                detail = "count=" + priorCameras.Length + "; sharedCameraCount=" + GardenPlantPhotoView.SharedCameraCount
                    + (priorCameras.Length == 0 ? "" : "\n" + string.Join("\n", priorCameras.Select(DescribePhotoCamera)))
            });
            string previousArtifacts = Environment.GetEnvironmentVariable("CAPSTONE_ARTIFACTS");
            running = true;
            try
            {
                Environment.SetEnvironmentVariable("CAPSTONE_ARTIFACTS", output);
                if (GraphicsSettings.currentRenderPipeline != null && RenderPipelineManager.currentPipeline == null) Blitter.Cleanup();
                Check(report, "Maximum recipe growth completes automatically and preserves earlier explicit completion", CompletionPolicy);
                Check(report, "Photo save archives the current plant atomically and repeated commands preserve it", SaveRetry);
                Check(report, "Independent plant snapshots and current garden survive a real isolated file reopen", () => FileRoundTrip(output));
                foreach (var size in new[] { new Vector2Int(360, 640), new Vector2Int(390, 844) })
                {
                    var viewport = size;
                    Check(report, "Album grid, detail and return at " + size.x + "x" + size.y,
                        () => GalleryFlow(report, request.capture, viewport));
                }
                Check(report, "Incomplete current plant, failed save and retry use the actual UI", () => UiSaveRetry(report, request.capture));
                Check(report, "A failed current photo stays retryable without an error on saved-photo detail", () => UiFailureDetailNavigation(report));
                Check(report, "Final growth commits once, blocks further purchases and enables the album flow", () => FinalGrowthAlbumFlow(report, request.capture));
                Check(report, "Archive, empty garden and a new individual survive failure, stale callbacks and reopen", () => ArchiveNewPlantFlow(report, request.capture));
                Check(report, "Stored practice growth applies to the next plant with visible overflow and safe retry", () => StoredGrowthAlbumFlow(report, request.capture));
                Check(report, "Saved plants stand on grass under the shared garden clock without changing their recipe", () => GrassAndSky(report, request.capture));
                Check(report, "More than one page keeps every photo reachable with bounded preview views", () => Pagination(report, request.capture));
                Check(report, "Repeated album/detail navigation releases owned graphics resources", () => ResourceLifecycle(report));
                if (request.regressions)
                {
                    Check(report, "Existing local garden loop regression suite", LocalLoopValidation.Run);
                    Check(report, "Existing mobile UI regression suite", MobileUiValidation.Run);
                    Check(report, "Existing Focus mission integration suite", FocusMissionValidation.Run);
                    Check(report, "Android release player script compilation", BuildTools.ValidateAndroidReleaseScripts);
                }
            }
            finally
            {
                Check(report, "Native album cameras created by this run are released", () => {
                    var retained = NativeStudioCameras().Where(camera => !nativeCameraBaseline.Contains(camera.GetInstanceID())).ToArray();
                    Assert(retained.Length == 0, "This run retained native album cameras:\n" + string.Join("\n", retained.Select(DescribePhotoCamera)));
                });
                Environment.SetEnvironmentVariable("CAPSTONE_ARTIFACTS", previousArtifacts);
                File.WriteAllText(Path.Combine(output, "report.json"), JsonUtility.ToJson(report, true));
                running = false;
            }
            Debug.Log("PLANT_ALBUM_VALIDATION passed=" + report.passed + " failed=" + report.failed
                + " clicks=" + report.buttonClicks + " raycasts=" + report.raycastChecks + " output=" + output);
            if (report.failed > 0) throw new InvalidOperationException("Plant album validation failed; inspect the fresh run report.");
        }

        static void Check(Report report, string name, Action action)
        {
            try { action(); report.passed++; report.checks.Add(new FocusMissionValidation.CheckResult { name = name, status = "pass" }); }
            catch (Exception error)
            {
                report.failed++;
                report.checks.Add(new FocusMissionValidation.CheckResult { name = name, status = "fail", detail = error.ToString() });
                Debug.LogWarning("PLANT_ALBUM_VALIDATION " + name + "\n" + error);
            }
        }
        static void Assert(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
        static string Encoded(GardenState state) => StateCodec.Encode(state);
        static Camera[] NativeStudioCameras() => Resources.FindObjectsOfTypeAll<Camera>()
            .Where(camera => camera.name == "Plant album shared camera").ToArray();
        static string DescribePhotoCamera(Camera camera) => "id=" + camera.GetInstanceID()
            + "; scene=" + camera.gameObject.scene.path + " (" + camera.gameObject.scene.name + ")"
            + "; root=" + camera.transform.root.name + "#" + camera.transform.root.GetInstanceID()
            + "; active=" + camera.gameObject.activeInHierarchy + "; flags=" + camera.hideFlags;
        static Camera CurrentStudioCamera()
        {
            var studioField = typeof(GardenPlantPhotoView).GetField("studio", BindingFlags.Static | BindingFlags.NonPublic);
            object studio = studioField?.GetValue(null);
            Assert(studio != null, "Photo view has no current shared studio after rendering.");
            var camera = studio.GetType().GetField("camera", BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(studio) as Camera;
            Assert(camera != null && camera.transform.root.GetComponentsInChildren<Camera>(true).Length == 1,
                "The current shared studio does not own exactly one camera.");
            var native = NativeStudioCameras();
            Assert(native.Contains(camera), "Shared studio camera is absent from Unity's native camera inventory.");
            var created = native.Where(item => !nativeCameraBaseline.Contains(item.GetInstanceID())).ToArray();
            Assert(created.Length <= 1, "This run created multiple native album cameras:\n" + string.Join("\n", created.Select(DescribePhotoCamera)));
            return camera;
        }

        static GardenState Seed(string instanceId, float growth)
        {
            var state = new GardenState {
                MigrationCompleted = true, InstallationId = "synthetic-plant-album-owner", Nutrient = 100,
                LegacyGrowth = growth, MbctProgramSelectionVersion = 1, MbctProgramChosen = true
            };
            var appearance = new GardenPlantAppearance { Recipe = PlantGenerator.Create(state.InstallationId, instanceId) };
            appearance.Recipe.Counts = (int[])appearance.BaselineProfileCounts.Clone();
            state.PlantAppearance = appearance;
            GardenPlantVisuals.Sync(state);
            StateCodec.Validate(state);
            return state;
        }
        static MemoryStore Store(GardenState state)
        {
            var store = new MemoryStore(); store.Save(state); return store;
        }
        static GardenStateService Open(IStateStore store)
        {
            var service = new GardenStateService(store, new Clock(), new DemoBalanceConfig { VisitorChancePercent = 0 });
            Assert(service.Open(), service.LastError); return service;
        }

        static void CompletionPolicy()
        {
            var store = Store(Seed("synthetic-small-completed", .1f));
            using (var service = Open(store))
            {
                string identity = service.Snapshot.PlantAppearance.Recipe.InstanceId;
                string before = Encoded(service.Snapshot);
                Assert(!service.SavePlantPhoto("synthetic-incomplete-photo", identity), "An incomplete plant was stored in the album.");
                Assert(Encoded(service.Snapshot) == before && Encoded(store.Load()) == before, "Rejected photo command changed the garden.");
                Assert(service.SetPlantCompleted(identity, true), service.LastError);
                Assert(!service.Snapshot.PlantAppearance.Recipe.Mature && GardenPlantVisuals.TotalGrowth(service.Snapshot) < GardenPlantVisuals.MaxVisualGrowth,
                    "The small completed fixture is already at a recipe/visible limit.");
                Assert(service.SavePlantPhoto("synthetic-small-photo", identity), service.LastError);
                var photo = service.Snapshot.PlantPhotos.Single();
                Assert(photo.SourcePlantInstanceId == identity && photo.Growth == .1f
                    && PlantCodec.Encode(photo.Recipe) == PlantCodec.Encode(service.Snapshot.PlantAppearance.Recipe)
                    && !GardenPlantVisuals.HasCurrentPlant(service.Snapshot),
                    "Photo did not freeze the explicitly completed small plant.");
                string completed = Encoded(service.Snapshot);
                Assert(!GardenPlantVisuals.CanGrow(service.Snapshot) && !service.GrowPlant("synthetic-early-completed-growth", "plant:P06")
                    && Encoded(service.Snapshot) == completed, "An explicitly completed small plant accepted another paid growth.");
            }
            var legacy = Seed("synthetic-large-incomplete", GardenPlantVisuals.MaxVisualGrowth);
            legacy.PlantAppearance.IsCompleted = false;
            store = Store(legacy);
            using (var service = new GardenStateService(store, new Clock(), new DemoBalanceConfig { VisitorChancePercent = 0 }))
            {
                string before = Encoded(legacy); store.FailNextSaves = 1;
                Assert(!service.Open() && Encoded(store.Load()) == before, "Failed completion migration changed an old save.");
                Assert(service.Open(), service.LastError);
                var completed = service.Snapshot;
                Assert(completed.PlantAppearance.IsCompleted && !GardenPlantVisuals.CanGrow(completed), "Maximum growth did not restore as completed.");
                completed.PlantAppearance.IsCompleted = false;
                Assert(Encoded(completed) == before, "Completion migration changed old appearance, growth scale, wallet or records.");
                Assert(service.SavePlantPhoto("synthetic-max-photo", service.Snapshot.PlantAppearance.Recipe.InstanceId)
                    && service.Snapshot.PlantPhotos.Count == 1, "Restored maximum-growth plant cannot be saved to the album.");
            }
        }

        static GardenState LastStageSeed(string instanceId)
        {
            var state = Seed(instanceId, .2f); var appearance = state.PlantAppearance;
            while (appearance.Recipe.GrowthStep < appearance.Recipe.MaxGrowthSteps - 1) appearance.Recipe = PlantGenerator.Advance(appearance.Recipe);
            appearance.MissionGrowthBaseStep = appearance.Recipe.GrowthStep;
            appearance.MissionGrowthBaseGrowth = .29f; appearance.IsCompleted = false;
            StateCodec.Validate(state); return state;
        }
        static void ReadyLegacyPractice(GardenStateService service, string id, string mission)
        { Assert(service.BeginSession(id, mission) && service.StartSession(id), service.LastError); }
        static void FinalGrowthAlbumFlow(Report report, bool capture)
        {
            foreach (var size in new[] { new Vector2Int(360, 640), new Vector2Int(390, 844) })
            using (var f = new Fixture(report, true, true, Store(LastStageSeed("synthetic-final-growth-" + size.x))))
            {
                f.Resize(size.x, size.y); string original = Encoded(f.Service.Snapshot);
                f.Ui.PreviewGrowth();
                Assert(f.Has("미션으로 이동") && !f.Has("확정") && Encoded(f.Service.Snapshot) == original,
                    "Mission growth guidance retained a paid confirmation or changed the saved plant.");
                ReadyLegacyPractice(f.Service, "synthetic-last-practice", "free-mission:M03");
                string before = Encoded(f.Service.Snapshot);
                f.Store.FailNextSaves = 1;
                Assert(!f.Service.CommitPracticeGrowth("synthetic-last-practice") && Encoded(f.Service.Snapshot) == before
                    && Encoded(f.Store.Load()) == before, "Failed last-practice save changed the plant, balance or growth receipt.");
                Assert(f.Service.CommitPracticeGrowth("synthetic-last-practice") && f.Service.CompleteSession("synthetic-last-practice"), f.Service.LastError);
                var completed = f.Service.Snapshot;
                Assert(completed.PlantAppearance.IsCompleted && completed.PlantAppearance.Recipe.Mature && completed.Coins == 100
                    && completed.PracticeGrowthRecords.Single().AfterStep == completed.PlantAppearance.Recipe.MaxGrowthSteps
                    && completed.PurchaseReceipts.Count == 0, "Final practice did not atomically complete the plant without a payment.");
                f.Ui.PreviewGrowth();
                Assert(f.Ui.CurrentScreen == "growth" && f.Has("사진첩으로 이동") && !f.Has("확정")
                    && f.Ui.GetComponentsInChildren<Text>(false).Any(text => text.name == "PlantGrowth.CompletedNotice"),
                    "Completed mission-grown plant does not offer its photo album action.");
                string finalState = Encoded(completed);
                Assert(f.Service.CommitPracticeGrowth("synthetic-last-practice") && !f.Service.GrowPlant("synthetic-after-completion", "plant:P06")
                    && Encoded(f.Service.Snapshot) == finalState, "Practice retry duplicated growth or a new paid command was accepted.");
                Capture(f, "mission-growth-completed-" + size.x + "x" + size.y, capture);
                f.Click("사진첩으로 이동");
                Assert(f.Ui.CurrentScreen == "plant-album" && f.Has("PlantAlbum.SaveCurrent"), "Completion action did not enable photo saving.");
                f.Click("PlantAlbum.SaveCurrent");
                Assert(f.Service.Snapshot.PlantPhotos.Count == 1 && f.Service.Snapshot.Coins == 100
                    && !GardenPlantVisuals.HasCurrentPlant(f.Service.Snapshot), "Saving the completed plant failed to archive it once or spent coins.");
                string saved = Encoded(f.Service.Snapshot); f.Ui.ShowShop();
                Assert(f.Has("새 식물 키우기") && !f.Has("확정"), "Empty garden still offers paid growth for its archived plant.");
                f.Ui.ShowPlantAlbum();
                Assert(!f.Has("PlantAlbum.SaveCurrent") && f.Has("PlantAlbum.StartNew") && Encoded(f.Service.Snapshot) == saved,
                    "Returning to the archive duplicated its photo or lost the new-plant action.");
                using (var reopened = Open(f.Store)) Assert(Encoded(reopened.Snapshot) == saved, "Final mission growth and its photo did not survive reopen.");
                Capture(f, "mission-growth-completed-album-" + size.x + "x" + size.y, capture);
            }
            using (var f = new Fixture(report, true, true, Store(Seed("synthetic-early-completion-guidance", .1f))))
            {
                f.Resize(360, 640); f.Ui.PreviewGrowth();
                string id = f.Service.Snapshot.PlantAppearance.Recipe.InstanceId;
                Assert(f.Service.SetPlantCompleted(id, true), f.Service.LastError); string completed = Encoded(f.Service.Snapshot);
                f.Ui.PreviewGrowth();
                Assert(Encoded(f.Service.Snapshot) == completed && f.Has("사진첩으로 이동") && !f.Has("확정"),
                    "External early completion did not replace read-only growth guidance with the album action.");
            }
        }

        static void SaveRetry()
        {
            var store = Store(Seed("synthetic-retry-plant", .2f));
            using (var service = Open(store))
            {
                string identity = service.Snapshot.PlantAppearance.Recipe.InstanceId;
                string beforeCompletion = Encoded(service.Snapshot);
                store.FailNextSaves = 1;
                Assert(!service.SetPlantCompleted(identity, true) && Encoded(service.Snapshot) == beforeCompletion
                    && Encoded(store.Load()) == beforeCompletion, "Failed completion leaked into memory or storage.");
                Assert(service.SetPlantCompleted(identity, true), service.LastError);
                string beforePhoto = Encoded(service.Snapshot);
                store.FailNextSaves = 1;
                Assert(!service.SavePlantPhoto("synthetic-retry-photo", identity) && Encoded(service.Snapshot) == beforePhoto
                    && Encoded(store.Load()) == beforePhoto, "Failed photo save changed the wallet, plant or album.");
                Assert(service.SavePlantPhoto("synthetic-retry-photo", identity), service.LastError);
                string saved = Encoded(service.Snapshot);
                Assert(service.SavePlantPhoto("synthetic-retry-photo", identity) && Encoded(service.Snapshot) == saved,
                    "Identical request duplicated or replaced the photo.");
                Assert(service.SavePlantPhoto("synthetic-retry-photo-second-id", identity), service.LastError);
                var acknowledged = service.Snapshot;
                Assert(acknowledged.PhotoSaveCommands.RemoveAll(command => command.RequestId == "synthetic-retry-photo-second-id") == 1
                    && Encoded(acknowledged) == saved, "A new request changed more than its own acknowledgement or duplicated the first photo.");
                string frozen = PlantCodec.Encode(service.Snapshot.PlantPhotos.Single().Recipe);
                string archived = Encoded(service.Snapshot);
                Assert(!service.SetPlantCompleted(identity, false) && Encoded(service.Snapshot) == archived
                    && !GardenPlantVisuals.HasCurrentPlant(service.Snapshot)
                    && service.Snapshot.PlantPhotos.Count == 1 && PlantCodec.Encode(service.Snapshot.PlantPhotos.Single().Recipe) == frozen,
                    "An archived plant could be made current again through its completion flag.");
                Assert(service.Snapshot.Nutrient == 100 && service.Snapshot.PurchaseReceipts.Count == 0,
                    "Album operations charged nutrients or created a purchase.");
            }
            var legacy = store.Load(); legacy.PlantAppearance.IsArchived = false;
            string legacySaved = Encoded(legacy); store.Save(legacy);
            using (var service = new GardenStateService(store, new Clock(), new DemoBalanceConfig()))
            {
                store.FailNextSaves = 1;
                Assert(!service.Open() && Encoded(store.Load()) == legacySaved, "Failed old-photo migration partially removed its garden plant.");
                Assert(service.Open() && !GardenPlantVisuals.HasCurrentPlant(service.Snapshot), "An old already-saved current plant was recreated on reopen.");
                var migrated = service.Snapshot; migrated.PlantAppearance.IsArchived = false;
                Assert(Encoded(migrated) == legacySaved, "Old-photo migration changed its recipe, balance, records or frozen photo.");
            }
        }

        static void ArchiveNewPlantFlow(Report report, bool capture)
        {
            foreach (var size in new[] { new Vector2Int(360, 640), new Vector2Int(390, 844) })
            using (var f = new Fixture(report, true, true, Store(Seed("synthetic-archive-source-" + size.x, .2f))))
            {
                f.Resize(size.x, size.y);
                ReadyLegacyPractice(f.Service, "synthetic-archive-last-practice", "free-mission:M03");
                Assert(f.Service.CommitPracticeGrowth("synthetic-archive-last-practice") && f.Service.CompleteSession("synthetic-archive-last-practice")
                    && f.Service.SetPlantCompleted(f.Service.Snapshot.PlantAppearance.Recipe.InstanceId, true), f.Service.LastError);
                var original = f.Service.Snapshot;
                string source = original.PlantAppearance.Recipe.InstanceId, oldSeed = original.PlantAppearance.Recipe.BaseSeed;
                string recipe = PlantCodec.Encode(original.PlantAppearance.Recipe), before = Encoded(original);
                f.Ui.ShowPlantAlbum();
                var saveButton = f.Ui.GetComponentsInChildren<Button>(false).Single(button => button.name == "PlantAlbum.SaveCurrent");
                var oldSave = saveButton.onClick;
                f.Store.FailNextSaves = 1; f.Click("PlantAlbum.SaveCurrent");
                Assert(Encoded(f.Service.Snapshot) == before && GardenPlantVisuals.HasCurrentPlant(f.Service.Snapshot),
                    "Failed archive save removed the current plant.");
                f.Click("PlantAlbum.SaveCurrent");
                var archived = f.Service.Snapshot;
                string empty = Encoded(archived), photos = PlantCodec.Encode(archived.PlantPhotos);
                string savedRequest = archived.PhotoSaveCommands.Single().RequestId;
                Assert(!GardenPlantVisuals.HasCurrentPlant(archived) && archived.PlantAppearance.IsArchived
                    && archived.Coins == 100 && archived.PlantPhotos.Single().SourcePlantInstanceId == source
                    && PlantCodec.Encode(archived.PlantPhotos.Single().Recipe) == recipe && f.Has("PlantAlbum.StartNew"),
                    "Archive save did not leave the original immutable photo and an actionable empty garden.");
                oldSave.Invoke();
                Assert(Encoded(f.Service.Snapshot) == empty, "Duplicate archive click wrote a second photo or modified the empty garden.");
                using (var reopened = Open(f.Store)) Assert(!GardenPlantVisuals.HasCurrentPlant(reopened.Snapshot)
                    && Encoded(reopened.Snapshot) == empty, "Reopening an empty garden recreated its archived plant.");
                Capture(f, "archived-empty-album-" + size.x + "x" + size.y, capture);
                var newButton = f.Ui.GetComponentsInChildren<Button>(false).Single(button => button.name == "PlantAlbum.StartNew");
                var oldNew = newButton.onClick;
                f.Store.FailNextSaves = 1; f.Click("PlantAlbum.StartNew");
                Assert(Encoded(f.Service.Snapshot) == empty && Encoded(f.Store.Load()) == empty && f.Has("PlantAlbum.StartNew"),
                    "Failed new-plant save altered the empty garden or lost the retry action.");
                f.Click("PlantAlbum.StartNew");
                var next = f.Service.Snapshot;
                string nextId = next.PlantAppearance.Recipe.InstanceId;
                Assert(GardenPlantVisuals.HasCurrentPlant(next) && nextId != source
                    && next.PlantAppearance.Recipe.BaseSeed != oldSeed && next.PlantAppearance.Recipe.GrowthStep == 0
                    && !next.PlantAppearance.IsCompleted && GardenPlantVisuals.TotalGrowth(next) == 0
                    && next.Coins == 100 && PlantCodec.Encode(next.PlantPhotos) == photos,
                    "New plant retry did not create one independent, free seed while retaining the album.");
                string created = Encoded(next);
                oldNew.Invoke(); oldSave.Invoke();
                Assert(Encoded(f.Service.Snapshot) == created && f.Service.SavePlantPhoto(savedRequest, source)
                    && !f.Service.StartNewPlant("synthetic-stale-new-request", source)
                    && Encoded(f.Service.Snapshot) == created,
                    "A stale creation/archive request replaced or archived the new current plant.");
                ReadyLegacyPractice(f.Service, "synthetic-new-individual-growth", "free-mission:M01");
                Assert(f.Service.CommitPracticeGrowth("synthetic-new-individual-growth") && f.Service.CompleteSession("synthetic-new-individual-growth"), f.Service.LastError);
                var grown = f.Service.Snapshot;
                Assert(grown.PlantAppearance.Recipe.GrowthStep == 1 && grown.Coins == 100
                    && grown.PlantAppearance.Recipe.InstanceId == nextId && grown.PracticeGrowthRecords.Last().PlantInstanceId == nextId
                    && grown.PracticeGrowthRecords.First().SessionId == "synthetic-archive-last-practice"
                    && PlantCodec.Encode(grown.PlantPhotos) == photos,
                    "Growth did not target only the new individual or changed its archived predecessor.");
                string grownState = Encoded(grown);
                Assert(f.Service.CommitPracticeGrowth("synthetic-archive-last-practice") && Encoded(f.Service.Snapshot) == grownState,
                    "A retry of the previous individual's last growth charged or grew the new plant.");
                using (var reopened = Open(f.Store)) Assert(Encoded(reopened.Snapshot) == grownState, "New individual identity, seed and growth did not survive reopen.");
                f.Navigation.ShowCourse(); f.Click("PlantAlbum.Open");
                Assert(f.Ui.gameObject.activeInHierarchy && f.Ui.CurrentScreen == "plant-album", "My course did not reopen the active album after planting.");
                Assert(!f.Has("PlantAlbum.StartNew") && !f.Has("PlantAlbum.SaveCurrent"), "A growing new plant exposes another creation or archive command.");
                Capture(f, "new-individual-album-" + size.x + "x" + size.y, capture);
            }
        }

        static void StoredGrowthAlbumFlow(Report report, bool capture)
        {
            using (var f = new Fixture(report, true, true, Store(Seed("synthetic-stored-growth-album", .3f))))
            {
                f.Resize(360, 640);
                string source = f.Service.Snapshot.PlantAppearance.Recipe.InstanceId;
                int maximum = f.Service.Snapshot.PlantAppearance.Recipe.MaxGrowthSteps;
                for (int index = 0; index < maximum + 2; index++)
                {
                    if (index > 0) { f.Clock.UtcNow = f.Clock.UtcNow.AddDays(1); Assert(f.Service.RefreshCycle(Array.Empty<string>()), f.Service.LastError); }
                    string id = "synthetic-stored-album-" + index;
                    ReadyLegacyPractice(f.Service, id, "free-mission:M03");
                    Assert(f.Service.CommitPracticeGrowth(id) && f.Service.CompleteSession(id), f.Service.LastError);
                }
                f.Ui.ShowPlantAlbum(); f.Click("PlantAlbum.SaveCurrent");
                Assert(f.Service.Snapshot.StoredGrowthSteps == maximum + 2 && f.Ui.GetComponentsInChildren<Text>(false)
                    .Any(text => text.name == "PlantAlbum.StoredGrowth" && text.text.Contains((maximum + 2) + "회")),
                    "Empty album does not show the saved practice growth that the next plant will receive.");
                Capture(f, "stored-growth-empty-album-360x640", capture);
                string empty = Encoded(f.Service.Snapshot);
                f.Store.FailNextSaves = 1; f.Click("PlantAlbum.StartNew");
                Assert(Encoded(f.Service.Snapshot) == empty && Encoded(f.Store.Load()) == empty && f.Has("PlantAlbum.StartNew"),
                    "Failed creation consumed stored stages or lost the retry action.");
                f.Click("PlantAlbum.StartNew");
                var next = f.Service.Snapshot;
                Assert(next.PlantAppearance.Recipe.InstanceId != source && next.PlantAppearance.Recipe.GrowthStep == maximum
                    && GardenPlantVisuals.IsCompleted(next) && next.StoredGrowthSteps == 2 && next.Coins == 100
                    && next.PlantStartCommands.Single().AppliedGrowthSteps == maximum,
                    "New-plant click did not atomically apply only the maximum stages and preserve the overflow.");
                f.Navigation.ShowCourse(); f.Click("PlantAlbum.Open");
                Assert(f.Has("PlantAlbum.SaveCurrent"), "A plant completed by stored growth cannot be archived through the real album action.");
                Capture(f, "stored-growth-new-completed-album-360x640", capture);
                string saved = Encoded(next);
                using (var reopened = Open(f.Store)) Assert(Encoded(reopened.Snapshot) == saved, "Applied growth and overflow changed after reopen.");
            }
        }

        static void GrassAndSky(Report report, bool capture)
        {
            double? originalHour = GardenVisualClock.OverrideHour;
            try
            {
                using (var f = new Fixture(report, true, true, Store(AlbumSeed(2))))
                {
                    f.Resize(390, 844); f.Ui.ShowPlantPhoto(f.Service.Snapshot.PlantPhotos[0].Id);
                    var view = f.Ui.GetComponentsInChildren<GardenPlantPhotoView>(false).Single();
                    string saved = Encoded(f.Service.Snapshot);
                    GardenVisualClock.SetOverrideMinutes(12 * 60); RenderViews(f);
                    string day = ImageSignature(view);
                    float dayLight = view.LastRenderedDaylight;
                    Assert(view.UsesGrassSurface && Math.Abs(view.LastRenderedHour - 12) < .0001
                        && Mathf.Abs(view.LastRenderedDaylight - GardenSkyEnvironment.EvaluateTime(12).Daylight) < .0001f,
                        "Album preview is not on grass under the garden's noon clock.");
                    var meshes = Resources.FindObjectsOfTypeAll<Mesh>().Where(mesh => mesh.name.StartsWith("ProceduralPlant_", StringComparison.Ordinal))
                        .Select(mesh => mesh.GetInstanceID()).OrderBy(id => id).ToArray();
                    Capture(f, "photo-grass-noon-390x844", capture);
                    GardenVisualClock.SetOverrideMinutes(0); RenderViews(f);
                    Assert(view.UsesGrassSurface && Math.Abs(view.LastRenderedHour) < .0001 && view.LastRenderedDaylight < dayLight
                        && Mathf.Abs(view.LastRenderedDaylight - GardenSkyEnvironment.EvaluateTime(0).Daylight) < .0001f
                        && ImageSignature(view) != day, "Album grass and sky did not follow the garden's night clock.");
                    Assert(meshes.SequenceEqual(Resources.FindObjectsOfTypeAll<Mesh>()
                        .Where(mesh => mesh.name.StartsWith("ProceduralPlant_", StringComparison.Ordinal))
                        .Select(mesh => mesh.GetInstanceID()).OrderBy(id => id)) && Encoded(f.Service.Snapshot) == saved,
                        "Changing photo time regenerated the plant or altered stored appearance/history.");
                    Capture(f, "photo-grass-night-390x844", capture);

                    // Album dragging follows the garden, including views below soil.
                    GardenVisualClock.SetOverrideMinutes(12 * 60);
                    var lowPhoto = f.Service.Snapshot.PlantPhotos.Single(photo => photo.Growth == .1f);
                    Assert(!lowPhoto.Recipe.Mature, "The upward-drag fixture must be a low-stage plant.");
                    f.Ui.ShowPlantPhoto(lowPhoto.Id); RenderViews(f);
                    var lowView = f.Ui.GetComponentsInChildren<GardenPlantPhotoView>(false).Single();
                    var canvas = lowView.GetComponentInParent<Canvas>();
                    var rect = (RectTransform)lowView.transform;
                    var center = RectTransformUtility.WorldToScreenPoint(canvas.worldCamera, rect.TransformPoint(rect.rect.center));
                    var pointer = new PointerEventData(null) { pointerId = -1, button = PointerEventData.InputButton.Left,
                        position = center, pointerPressRaycast = new RaycastResult { module = canvas.GetComponent<GraphicRaycaster>() } };
                    lowView.OnPointerDown(pointer); lowView.OnBeginDrag(pointer);
                    pointer.position += Vector2.up * 2000;
                    lowView.OnDrag(pointer); lowView.OnEndDrag(pointer);
                    lowView.SetZoom(.75f); RenderViews(f);
                    var photoCamera = CurrentStudioCamera();
                    Assert(Quaternion.Angle(photoCamera.transform.rotation, Quaternion.Euler(-PrototypeIslandDrag.PitchLimit, -18, 0)) < .01f
                        && lowView.UsesGrassSurface && Mathf.Approximately(lowView.LastRenderedEnvironmentOpacity, 0) && Encoded(f.Service.Snapshot) == saved,
                        "Maximum upward photo drag differs from the garden pitch limit or changed the archived plant.");
                    Capture(f, "photo-grass-low-stage-upward-limit-390x844", capture);
                    lowView.ResetView(); RenderViews(f);
                    Assert(Quaternion.Angle(CurrentStudioCamera().transform.rotation, Quaternion.Euler(8, -18, 0)) < .01f
                        && Mathf.Approximately(lowView.Zoom, 1) && Encoded(f.Service.Snapshot) == saved,
                        "Reset did not restore the album's initial view without changing stored data.");
                }
            }
            finally
            {
                if (originalHour.HasValue) GardenVisualClock.SetOverrideMinutes((int)Math.Round(originalHour.Value * 60));
                else GardenVisualClock.UseSystemTime();
            }
        }

        static GardenState AlbumSeed(int photoCount)
        {
            var photos = new List<GardenPlantPhoto>();
            var commands = new List<GardenPlantPhotoCommand>();
            for (int index = 0; index < photoCount; index++)
            {
                var state = Seed("synthetic-album-individual-" + index, new[] { .3f, .1f, .2f }[index % 3]);
                state.PlantPhotos = photos;
                state.PhotoSaveCommands = commands;
                using (var service = Open(Store(state)))
                {
                    string identity = service.Snapshot.PlantAppearance.Recipe.InstanceId;
                    Assert(service.SetPlantCompleted(identity, true), service.LastError);
                    Assert(service.SavePlantPhoto("synthetic-photo-" + index, identity), service.LastError);
                    photos = service.Snapshot.PlantPhotos;
                    commands = service.Snapshot.PhotoSaveCommands;
                }
            }
            var current = Seed("synthetic-current-growing-plant", .2f); current.PlantPhotos = photos; current.PhotoSaveCommands = commands;
            StateCodec.Validate(current); return current;
        }

        static void FileRoundTrip(string output)
        {
            string path = Path.Combine(output, "isolated-state", "state.xml");
            string saved;
            using (var store = new FileStateStore(path))
            {
                store.Save(AlbumSeed(3));
                using (var service = Open(store)) saved = Encoded(service.Snapshot);
            }
            using (var store = new FileStateStore(path))
            using (var service = Open(store))
            {
                var state = service.Snapshot;
                Assert(Encoded(state) == saved, "Real file reopen changed frozen photos or current plant/wallet/history.");
                Assert(state.PlantPhotos.Select(photo => photo.SourcePlantInstanceId).Distinct().Count() == 3
                    && state.PlantPhotos.Select(photo => photo.Recipe.BaseSeed).Distinct().Count() == 3,
                    "Distinct individual identity and seed did not survive persistence.");
                Assert(state.PlantAppearance.Recipe.InstanceId == "synthetic-current-growing-plant" && !state.PlantAppearance.IsCompleted,
                    "Viewing the album changed the current growing plant.");
            }
        }

        static void RenderViews(Fixture fixture)
        {
            Canvas.ForceUpdateCanvases();
            var sun = RenderSettings.sun;
            var skybox = RenderSettings.skybox;
            var ambient = RenderSettings.ambientProbe;
            bool fog = RenderSettings.fog;
            var activeTexture = RenderTexture.active;
            var cameras = Resources.FindObjectsOfTypeAll<Camera>().Where(camera => !camera.name.StartsWith("Plant album", StringComparison.Ordinal))
                .Select(camera => new CameraState(camera)).ToArray();
            foreach (var view in fixture.Ui.GetComponentsInChildren<GardenPlantPhotoView>(false))
            {
                Assert(view.RenderNow(), "Photo renderer declined an active view.");
                CurrentStudioCamera();
                ImageSignature(view);
            }
            Assert(RenderSettings.sun == sun && RenderSettings.skybox == skybox && RenderSettings.fog == fog && RenderTexture.active == activeTexture,
                "Photo rendering changed the garden sun, skybox, fog or active render target.");
            var currentAmbient = RenderSettings.ambientProbe;
            for (int color = 0; color < 3; color++) for (int coefficient = 0; coefficient < 9; coefficient++)
                Assert(ambient[color, coefficient] == currentAmbient[color, coefficient], "Photo rendering changed the garden ambient light.");
            foreach (var camera in cameras) camera.AssertUnchanged();
            Canvas.ForceUpdateCanvases();
        }

        sealed class CameraState
        {
            readonly Camera camera;
            readonly Vector3 position;
            readonly Quaternion rotation;
            readonly RenderTexture target;
            readonly int mask;
            readonly bool enabled;
            public CameraState(Camera value)
            {
                camera = value; position = value.transform.position; rotation = value.transform.rotation;
                target = value.targetTexture; mask = value.cullingMask; enabled = value.enabled;
            }
            public void AssertUnchanged()
            {
                Assert(camera != null && camera.transform.position == position && camera.transform.rotation == rotation
                    && camera.targetTexture == target && camera.cullingMask == mask && camera.enabled == enabled,
                    "Photo rendering modified an existing garden/UI camera.");
            }
        }

        static string ImageSignature(GardenPlantPhotoView view)
        {
            var target = view.GetComponent<RawImage>().texture as RenderTexture;
            Assert(target != null && target.IsCreated(), "Photo view has no live render texture.");
            var previous = RenderTexture.active;
            var image = new Texture2D(target.width, target.height, TextureFormat.RGB24, false);
            try
            {
                RenderTexture.active = target;
                image.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0); image.Apply();
                var pixels = image.GetPixels32();
                int minimum = 765, maximum = 0;
                for (int index = 0; index < pixels.Length; index += 7)
                {
                    int brightness = pixels[index].r + pixels[index].g + pixels[index].b;
                    minimum = Math.Min(minimum, brightness); maximum = Math.Max(maximum, brightness);
                }
                Assert(maximum - minimum >= 60, "Plant photo is blank or has no visible plant/planter contrast.");
                using (var sha = SHA256.Create()) return Convert.ToBase64String(sha.ComputeHash(image.EncodeToPNG()));
            }
            finally { RenderTexture.active = previous; UnityEngine.Object.DestroyImmediate(image); }
        }
        static void Capture(Fixture fixture, string name, bool capture)
        {
            RenderViews(fixture); fixture.Capture("plant-album-" + name, capture);
        }

        static void GalleryFlow(Report report, bool capture, Vector2Int size)
        {
            var store = Store(AlbumSeed(3));
            using (var f = new Fixture(report, true, true, store))
            {
                f.Resize(size.x, size.y);
                f.Invoke("ShowFocusCourse");
                Assert(f.Has("PlantAlbum.Open"), "My course has no album entry.");
                f.Click("PlantAlbum.Open");
                string before = Encoded(f.Service.Snapshot);
                Assert(f.Ui.CurrentScreen == "plant-album", "Album entry opened the wrong screen.");
                var photos = f.Service.Snapshot.PlantPhotos;
                Assert(photos.All(photo => f.Has("PlantAlbum.Photo." + photo.Id)), "The grid omitted a saved individual.");
                RenderViews(f);
                var thumbnails = f.Ui.GetComponentsInChildren<GardenPlantPhotoView>(false)
                    .Where(view => view.name.StartsWith("PlantAlbum.Thumbnail.", StringComparison.Ordinal)).ToArray();
                Assert(thumbnails.Length == 3 && thumbnails.All(view => !view.IsInteractive), "Grid thumbnails compete with scroll/selection gestures.");
                Assert(thumbnails.Select(ImageSignature).Distinct().Count() == 3, "Different saved plants produced identical thumbnail images.");
                foreach (var thumbnail in thumbnails.Take(2))
                {
                    var scroll = thumbnail.GetComponentInParent<ScrollRect>();
                    var bounds = RectTransformUtility.CalculateRelativeRectTransformBounds(scroll.viewport, thumbnail.transform);
                    Assert(bounds.min.y >= scroll.viewport.rect.yMin - 1 && bounds.max.y <= scroll.viewport.rect.yMax + 1,
                        "First-row plant pictures are clipped before scrolling at " + size + ".");
                }
                Assert(f.Ui.GetComponentsInChildren<RawImage>(false).Where(image => image.GetComponent<GardenPlantPhotoView>() != null)
                    .All(image => image.texture != null), "A plant thumbnail has no rendered texture.");
                Assert(GardenPlantPhotoView.SharedCameraCount <= 1, "The album created one camera per plant.");
                Capture(f, "grid-" + size.x + "x" + size.y, capture);
                f.Click("PlantAlbum.Photo." + photos[0].Id);
                Assert(f.Ui.CurrentScreen.StartsWith("plant-photo", StringComparison.Ordinal), "Thumbnail did not open detail.");
                var detail = f.Ui.GetComponentsInChildren<GardenPlantPhotoView>(false).Single();
                RenderViews(f);
                Assert(detail.IsInteractive, "Photo detail does not support the approved rotation/zoom interaction.");
                string initialImage = ImageSignature(detail);
                float initialYaw = detail.Yaw;
                var canvas = detail.GetComponentInParent<Canvas>();
                var raycaster = canvas.GetComponent<GraphicRaycaster>();
                var rect = (RectTransform)detail.transform;
                var center = RectTransformUtility.WorldToScreenPoint(canvas.worldCamera, rect.TransformPoint(rect.rect.center));
                var pointer = new PointerEventData(null) { pointerId = -1, button = PointerEventData.InputButton.Left,
                    position = center, pointerPressRaycast = new RaycastResult { module = raycaster } };
                detail.OnPointerDown(pointer); detail.OnBeginDrag(pointer);
                pointer.position += new Vector2(35, 0); detail.OnDrag(pointer); detail.OnEndDrag(pointer);
                Assert(Mathf.Abs(Mathf.DeltaAngle(detail.Yaw, initialYaw)) > 1, "Dragging the RawImage did not rotate the plant.");
                detail.RenderNow();
                Assert(!f.Ui.GetComponentsInChildren<Button>(false).Any(button => button.name == "PlantAlbum.ZoomIn"
                    || button.name == "PlantAlbum.ZoomOut" || button.name == "PlantAlbum.ResetView"), "Removed inspection buttons remain in the detail.");
                Assert(ImageSignature(detail) != initialImage, "Dragging the photo did not change its rendered appearance.");
                detail.ResetView(); detail.RenderNow();
                Assert(Mathf.Abs(detail.Yaw - initialYaw) < .001f && Mathf.Abs(detail.Zoom - 1) < .001f,
                    "Internal view reset did not restore the gesture fixture's initial composition.");
                var left = new PointerEventData(null) { pointerId = 11, button = PointerEventData.InputButton.Left,
                    position = center - new Vector2(24, 0), pointerPressRaycast = new RaycastResult { module = raycaster } };
                var right = new PointerEventData(null) { pointerId = 12, button = PointerEventData.InputButton.Left,
                    position = center + new Vector2(24, 0), pointerPressRaycast = new RaycastResult { module = raycaster } };
                detail.OnPointerDown(left); detail.OnBeginDrag(left); detail.OnPointerDown(right); detail.OnBeginDrag(right);
                right.position += new Vector2(24, 0); detail.OnDrag(right);
                Assert(detail.Zoom > 1, "Two fingers did not zoom inside the photo surface.");
                detail.OnPointerUp(right); detail.OnEndDrag(right);
                float yawAfterPinch = detail.Yaw;
                left.position -= new Vector2(28, 0); detail.OnDrag(left);
                Assert(Mathf.Abs(Mathf.DeltaAngle(detail.Yaw, yawAfterPinch)) > 1, "Lifting one pinch finger prevented remaining-finger rotation.");
                detail.OnPointerUp(left); detail.OnEndDrag(left); detail.ResetView(); detail.RenderNow();
                Capture(f, "detail-" + size.x + "x" + size.y, capture);
                Assert(Encoded(f.Service.Snapshot) == before && Encoded(store.Load()) == before,
                    "Inspecting a photo changed current growth, rewards, records or frozen appearance.");
                f.Click("PlantAlbum.DetailBack");
                Assert(f.Ui.CurrentScreen == "plant-album", "Detail back did not return to the grid.");
                f.Click("PlantAlbum.Back");
                Assert(f.Ui.CurrentScreen == "course", "Album back did not return to My course.");
                f.Click("홈");
                Assert(f.Navigation.home.homeCanvas.gameObject.activeSelf && !f.Ui.gameObject.activeSelf,
                    "Returning to the garden did not restore the existing garden canvas.");
                Assert(Encoded(f.Service.Snapshot) == before, "Returning to the garden changed the growing individual.");
            }
        }

        static void UiSaveRetry(Report report, bool capture)
        {
            var store = Store(Seed("synthetic-ui-save", .1f));
            using (var f = new Fixture(report, true, true, store))
            {
                f.Resize(360, 640); f.Ui.ShowPlantAlbum();
                Assert(!f.Has("PlantAlbum.SaveCurrent"), "Incomplete current plant exposes an enabled save-photo button.");
                Capture(f, "incomplete-360x640", capture);
                string identity = f.Service.Snapshot.PlantAppearance.Recipe.InstanceId;
                Assert(f.Service.SetPlantCompleted(identity, true), f.Service.LastError);
                f.Ui.ShowPlantAlbum();
                Assert(f.Has("PlantAlbum.SaveCurrent"), "Explicitly completed small plant cannot be saved from the album.");
                string before = Encoded(f.Service.Snapshot);
                store.FailNextSaves = 1; f.Click("PlantAlbum.SaveCurrent");
                Assert(Encoded(f.Service.Snapshot) == before && f.Service.Snapshot.PlantPhotos.Count == 0,
                    "UI failure partially added a photo.");
                Assert(f.Has("PlantAlbum.SaveCurrent"), "Save failure lost the retry action.");
                Capture(f, "save-error-360x640", capture);
                f.Click("PlantAlbum.SaveCurrent");
                Assert(f.Service.Snapshot.PlantPhotos.Count == 1 && f.Service.Snapshot.Nutrient == 100,
                    "UI retry did not save exactly one free photo.");
                f.Ui.ShowPlantAlbum();
                Assert(!f.Has("PlantAlbum.SaveCurrent"), "Already-saved individual remains available for duplicate saving.");
            }
        }

        static void UiFailureDetailNavigation(Report report)
        {
            var store = Store(AlbumSeed(1));
            using (var f = new Fixture(report, true, true, store))
            {
                f.Resize(360, 640);
                string current = f.Service.Snapshot.PlantAppearance.Recipe.InstanceId;
                string savedPhoto = f.Service.Snapshot.PlantPhotos.Single().Id;
                Assert(f.Service.SetPlantCompleted(current, true), f.Service.LastError);
                f.Ui.ShowPlantAlbum();
                string before = Encoded(f.Service.Snapshot);
                store.FailNextSaves = 1;
                f.Click("PlantAlbum.SaveCurrent");
                Assert(Encoded(f.Service.Snapshot) == before, "A failed current photo changed the saved collection.");
                Assert(f.Ui.GetComponentsInChildren<Text>(false).Any(text => text.name == "Focus.SaveNotice"),
                    "Album did not show the pending photo-save error.");
                f.Click("PlantAlbum.Photo." + savedPhoto);
                Assert(!f.Ui.GetComponentsInChildren<Text>(false).Any(text => text.name == "Focus.SaveNotice"),
                    "Viewing a previously saved photo inherited the current-photo save error.");
                f.Click("PlantAlbum.DetailBack");
                Assert(f.Ui.GetComponentsInChildren<Text>(false).Any(text => text.name == "Focus.SaveNotice")
                    && f.Has("PlantAlbum.SaveCurrent"), "Returning from detail lost the pending photo and retry notice.");
                f.Click("PlantAlbum.SaveCurrent");
                Assert(f.Service.Snapshot.PlantPhotos.Count == 2
                    && f.Service.Snapshot.PlantPhotos.Count(photo => photo.Id == savedPhoto) == 1,
                    "Retry changed the old photo or created more than one current-photo snapshot.");
                Assert(!f.Ui.GetComponentsInChildren<Text>(false).Any(text => text.name == "Focus.SaveNotice"),
                    "A successful retry retained its old error banner.");
            }
        }

        static void ResourceLifecycle(Report report)
        {
            int viewsBefore = GardenPlantPhotoView.ActiveViewCount;
            int texturesBefore = GardenPlantPhotoView.OwnedTextureCount;
            int camerasBefore = GardenPlantPhotoView.SharedCameraCount;
            int grassMeshesBefore = Resources.FindObjectsOfTypeAll<Mesh>().Count(mesh => mesh.name.StartsWith("Plant album nature ", StringComparison.Ordinal));
            int studioMaterialsBefore = Resources.FindObjectsOfTypeAll<Material>().Count(material => material.name.StartsWith("Plant album ", StringComparison.Ordinal));
            using (var f = new Fixture(report, true, true, Store(AlbumSeed(3))))
            {
                f.Resize(390, 844);
                string before = Encoded(f.Service.Snapshot);
                for (int index = 0; index < 5; index++)
                {
                    f.Ui.ShowPlantAlbum(); RenderViews(f);
                    Assert(GardenPlantPhotoView.SharedCameraCount <= camerasBefore + 1, "Grid navigation accumulates render cameras.");
                    var id = f.Service.Snapshot.PlantPhotos[index % 3].Id;
                    f.Click("PlantAlbum.Photo." + id); RenderViews(f);
                    Assert(GardenPlantPhotoView.ActiveViewCount <= viewsBefore + 1
                        && GardenPlantPhotoView.OwnedTextureCount <= texturesBefore + 1,
                        "Opening detail retained thumbnail views/textures.");
                    var detail = f.Ui.GetComponentsInChildren<GardenPlantPhotoView>(false).Single();
                    var meshes = Resources.FindObjectsOfTypeAll<Mesh>().Where(mesh => mesh.name.StartsWith("ProceduralPlant_", StringComparison.Ordinal))
                        .Select(mesh => mesh.GetInstanceID()).OrderBy(id => id).ToArray();
                    var texture = detail.GetComponent<RawImage>().texture;
                    detail.RenderNow(); detail.RenderNow();
                    var afterMeshes = Resources.FindObjectsOfTypeAll<Mesh>().Where(mesh => mesh.name.StartsWith("ProceduralPlant_", StringComparison.Ordinal))
                        .Select(mesh => mesh.GetInstanceID()).OrderBy(id => id).ToArray();
                    Assert(meshes.SequenceEqual(afterMeshes) && detail.GetComponent<RawImage>().texture == texture,
                        "Unchanged photo regenerated its plant mesh or allocated a replacement texture.");
                    ((RenderTexture)texture).Release(); detail.RenderNow();
                    Assert(((RenderTexture)texture).IsCreated() && detail.GetComponent<RawImage>().texture == texture,
                        "Lost render texture was not recreated without changing the saved photo.");
                    ImageSignature(detail);
                    f.Click("PlantAlbum.DetailBack");
                    f.Click("PlantAlbum.Back");
                    Assert(GardenPlantPhotoView.ActiveViewCount == viewsBefore && GardenPlantPhotoView.OwnedTextureCount == texturesBefore
                        && Resources.FindObjectsOfTypeAll<Mesh>().Count(mesh => mesh.name.StartsWith("Plant album nature ", StringComparison.Ordinal)) == grassMeshesBefore
                        && Resources.FindObjectsOfTypeAll<Material>().Count(material => material.name.StartsWith("Plant album ", StringComparison.Ordinal)) == studioMaterialsBefore,
                        "Leaving the album leaked owned views, textures, meadow mesh or grass/sky materials.");
                }
                Assert(Encoded(f.Service.Snapshot) == before, "Repeated navigation mutated persisted state.");
            }
            Assert(GardenPlantPhotoView.ActiveViewCount == viewsBefore && GardenPlantPhotoView.OwnedTextureCount == texturesBefore
                && GardenPlantPhotoView.SharedCameraCount <= Math.Max(1, camerasBefore), "Disposed preview retains graphics resources.");
        }

        static void Pagination(Report report, bool capture)
        {
            using (var f = new Fixture(report, true, true, Store(AlbumSeed(13))))
            {
                f.Resize(390, 844); f.Ui.ShowPlantAlbum();
                string before = Encoded(f.Service.Snapshot);
                var seen = new HashSet<string>();
                foreach (var button in f.Ui.GetComponentsInChildren<Button>(false).Where(button => button.name.StartsWith("PlantAlbum.Photo.", StringComparison.Ordinal)))
                    seen.Add(button.name);
                Assert(seen.Count == 12 && f.Ui.GetComponentsInChildren<GardenPlantPhotoView>(false).Length <= 13,
                    "First page did not bound preview allocation to current plant plus twelve photos.");
                Assert(f.Has("PlantAlbum.NextPage") && !f.Has("PlantAlbum.PreviousPage"), "First-page navigation state is incorrect.");
                f.Click("PlantAlbum.NextPage");
                var last = f.Ui.GetComponentsInChildren<Button>(false).Where(button => button.name.StartsWith("PlantAlbum.Photo.", StringComparison.Ordinal)).ToArray();
                Assert(last.Length == 1 && seen.Add(last[0].name) && seen.Count == 13, "The last page lost or repeated a saved photo.");
                Assert(!f.Has("PlantAlbum.NextPage") && f.Has("PlantAlbum.PreviousPage"), "Last-page navigation state is incorrect.");
                Capture(f, "page-two-390x844", capture);
                string lastButtonName = last[0].name;
                f.Click(lastButtonName); f.Click("PlantAlbum.DetailBack");
                Assert(f.Has(lastButtonName), "Returning from detail changed the selected album page.");
                f.Click("PlantAlbum.PreviousPage");
                Assert(Encoded(f.Service.Snapshot) == before, "Pagination changed saved photos or the current garden.");
            }
        }
    }
}
