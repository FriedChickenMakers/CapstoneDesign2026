using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using CapstoneDesign.Plants;
using CapstoneDesign.Runtime;
using CapstoneDesign.Runtime.LocalState;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace CapstoneDesign.EditorTools
{
    // Uses the already-open product scene three times, with an explicit isolated MOCK
    // save root. An explicit option can reload the unchanged scene after an old
    // fixture reused it; it never saves scenes, changes Game-view presets, closes
    // the Editor, or accesses a device. SessionState survives Play domain reloads.
    [InitializeOnLoad]
    public static class GardenPlantAlbumPlayValidation
    {
        const string SessionKey = "Capstone.PlantAlbumPlayValidation.Session";
        const string ErrorKey = SessionKey + ".FirstError";
        const string ErrorCountKey = SessionKey + ".ErrorCount";
        const string SmokeVariable = "CAPSTONE_SMOKE_SAVE_ROOT";
        const string PersonalizationVariable = "CAPSTONE_PERSONALIZATION_TEST_ROOT";
        static string Root => Path.Combine(Directory.GetCurrentDirectory(), "artifacts", "plant-album-20261105");
        static string RequestPath => Path.Combine(Root, "play-request.json");
        static double nextPoll;

        [Serializable] public sealed class Request { public string runId; public bool restoreCleanSceneAfterLegacyFixture; }
        [Serializable] sealed class Session
        {
            public string runId, utc, output, saveRoot, stage, error, unityVersion, firstRuntimeError;
            public string oldSmoke, oldPersonalization;
            public bool smokeWasSet, personalizationWasSet, originalPaused, environmentRestored, cleanSceneRestored;
            public bool gizmosCaptured, originalDrawGizmos, originalShowGizmos, gizmosRestored;
            public int focusWindow, gameViewWindow, phase, assertions, clicks, raycasts, width, height, runtimeErrors;
            public string gizmoApi = "Unity 6000.3.24f1 UnityEditor.CoreModule metadata verified: GameView.drawGizmos and PlayModeView.showGizmos";
            public double deadline;
            public string expectedState, originalPlant, archivedPhotos, archivedPlantId, newPlantId, finalGrowthRequest, photoSaveRequest;
            public List<string> checks = new List<string>();
            public List<string> captures = new List<string>();
            public List<float> editorFrameMs = new List<float>();
            public string environment = "Actual Unity Editor Play / existing Game view / synthetic MOCK file store / archive to empty, restart to create and grow a new individual, second restart restore; not Android performance";
        }

        static GardenPlantAlbumPlayValidation()
        {
            EditorApplication.update += Tick;
            Application.logMessageReceived += ObserveLog;
        }
        static void ObserveLog(string message, string stack, LogType type)
        {
            if (type != LogType.Error && type != LogType.Exception && type != LogType.Assert) return;
            if (string.IsNullOrEmpty(SessionState.GetString(SessionKey, ""))) return;
            SessionState.SetInt(ErrorCountKey, SessionState.GetInt(ErrorCountKey, 0) + 1);
            if (!string.IsNullOrEmpty(SessionState.GetString(ErrorKey, ""))) return;
            string detail = type + ": " + message + "\n" + stack;
            SessionState.SetString(ErrorKey, detail.Length > 6000 ? detail.Substring(0, 6000) : detail);
        }
        static Session ReadSession()
        {
            string value = SessionState.GetString(SessionKey, "");
            return string.IsNullOrEmpty(value) ? null : JsonUtility.FromJson<Session>(value);
        }
        static void Save(Session session) => SessionState.SetString(SessionKey, JsonUtility.ToJson(session));

        // Verified against the installed 6000.3.24f1 UnityEditor.CoreModule.dll:
        // GameView.drawGizmos wraps m_Gizmos, and OnGUI copies that preference to
        // PlayModeView.showGizmos. Set both before rendering; preserve both values.
        static PropertyInfo GizmoProperty(EditorWindow window, bool draw)
        {
            var owner = draw ? window.GetType() : window.GetType().BaseType;
            var property = owner.GetProperty(draw ? "drawGizmos" : "showGizmos",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
            Assert(property != null && property.PropertyType == typeof(bool) && property.CanRead && property.CanWrite,
                "The installed Game view no longer provides the verified gizmo property.");
            return property;
        }
        static bool ReadGizmos(EditorWindow window, bool draw) => (bool)GizmoProperty(window, draw).GetValue(window);
        static EditorWindow GameView(Session session)
        {
            var window = EditorUtility.InstanceIDToObject(session.gameViewWindow) as EditorWindow;
            Assert(window != null && window.GetType().FullName == "UnityEditor.GameView", "The original Game view was closed during validation.");
            return window;
        }
        static void SuppressGizmos(Session session)
        {
            var window = GameView(session);
            GizmoProperty(window, true).SetValue(window, false);
            GizmoProperty(window, false).SetValue(window, false);
            window.Repaint();
            Assert(!ReadGizmos(window, true) && !ReadGizmos(window, false), "Game-view gizmos could not be suppressed for synthetic captures.");
        }
        static void RestoreGizmos(Session session)
        {
            if (!session.gizmosCaptured) { session.gizmosRestored = true; return; }
            var window = GameView(session);
            GizmoProperty(window, true).SetValue(window, session.originalDrawGizmos);
            GizmoProperty(window, false).SetValue(window, session.originalShowGizmos);
            window.Repaint();
            session.gizmosRestored = ReadGizmos(window, true) == session.originalDrawGizmos
                && ReadGizmos(window, false) == session.originalShowGizmos;
            Assert(session.gizmosRestored, "Original Game-view gizmo settings were not restored.");
        }

        static void Tick()
        {
            var session = ReadSession();
            if (session == null)
            {
                if (EditorApplication.timeSinceStartup < nextPoll) return;
                nextPoll = EditorApplication.timeSinceStartup + 1;
                if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating || !File.Exists(RequestPath)) return;
                var request = JsonUtility.FromJson<Request>(File.ReadAllText(RequestPath));
                File.Delete(RequestPath);
                Begin(request ?? new Request());
                return;
            }
            try
            {
                if (EditorApplication.isCompiling || EditorApplication.isUpdating) return;
                if (EditorApplication.timeSinceStartup > session.deadline && session.stage != "finish")
                { Stop(session, new TimeoutException("Album Play validation exceeded its bounded phase deadline.")); return; }
                if (session.stage == "finish")
                {
                    if (!EditorApplication.isPlayingOrWillChangePlaymode) Finish(session);
                    return;
                }
                if (session.stage == "between")
                {
                    if (EditorApplication.isPlayingOrWillChangePlaymode) return;
                    session.phase++; session.stage = "starting";
                    session.deadline = EditorApplication.timeSinceStartup + 120;
                    Save(session); EditorApplication.EnterPlaymode(); return;
                }
                if (session.stage == "running")
                {
                    if (!EditorApplication.isPlayingOrWillChangePlaymode)
                        Stop(session, new InvalidOperationException("Play mode stopped before the validation finished."));
                    return;
                }
                if (!EditorApplication.isPlaying) return;
                var nav = UnityEngine.Object.FindFirstObjectByType<MockupNavigation>(FindObjectsInactive.Include);
                var ui = nav?.activitiesPanel?.GetComponent<WeekOneQuestDemo>();
                if (ui?.Service == null || !ui.HasOpenState) return;
                Assert(AndroidPlatformBridge.InputMode == PlatformInputMode.Mock, "Synthetic Play must remain MOCK.");
                Assert(Environment.GetEnvironmentVariable(SmokeVariable) == session.saveRoot, "Synthetic save override was lost during reload.");
                session.stage = "running"; session.deadline = EditorApplication.timeSinceStartup + 120;
                Save(session);
                var gameView = GameView(session);
                SuppressGizmos(session);
                gameView.Focus();
                Application.runInBackground = true;
                nav.StartCoroutine(RunGuarded(session, nav, ui));
            }
            catch (Exception error) { Stop(session, error); }
        }

        static void Begin(Request request)
        {
            string runId = request.runId ?? "play-" + DateTimeOffset.UtcNow.ToString("yyyyMMdd-HHmmss");
            Assert(Regex.IsMatch(runId, "^[A-Za-z0-9][A-Za-z0-9_-]{0,80}$"), "Use a simple unique Play run ID.");
            string output = Path.Combine(Root, runId);
            Assert(!Directory.Exists(output), "A fresh Play run directory is required.");
            Directory.CreateDirectory(output);
            var session = new Session {
                runId = runId, utc = DateTimeOffset.UtcNow.ToString("O"), output = output,
                saveRoot = Path.Combine(output, "synthetic-save"), phase = 1, stage = "starting",
                originalPaused = EditorApplication.isPaused, focusWindow = EditorWindow.focusedWindow == null ? 0 : EditorWindow.focusedWindow.GetInstanceID(),
                unityVersion = Application.unityVersion, deadline = EditorApplication.timeSinceStartup + 120,
                oldSmoke = Environment.GetEnvironmentVariable(SmokeVariable), oldPersonalization = Environment.GetEnvironmentVariable(PersonalizationVariable)
            };
            session.smokeWasSet = session.oldSmoke != null;
            session.personalizationWasSet = session.oldPersonalization != null;
            SessionState.EraseString(ErrorKey); SessionState.EraseInt(ErrorCountKey);
            Save(session);
            try
            {
                Assert(!EditorApplication.isPlayingOrWillChangePlaymode, "Begin outside Play mode.");
                Assert(SceneManager.GetActiveScene().path == "Assets/Scenes/MockupMain.unity", "Keep MockupMain open; the validator will not change the user's open scene.");
                if (request.restoreCleanSceneAfterLegacyFixture)
                {
                    // Opt-in repair for the former LocalLoop fixture which reused an
                    // already-open scene. Never save its synthetic UI or discard edits.
                    Assert(!SceneManager.GetActiveScene().isDirty, "Unsaved scene edits prevent legacy-fixture restoration.");
                    EditorSceneManager.OpenScene("Assets/Scenes/MockupMain.unity", OpenSceneMode.Single);
                    session.cleanSceneRestored = true; Save(session);
                }
                var gameView = Resources.FindObjectsOfTypeAll(typeof(Editor).Assembly.GetType("UnityEditor.GameView", true)).FirstOrDefault() as EditorWindow;
                Assert(gameView != null, "Open a Game view before requesting graphical Play validation.");
                session.gameViewWindow = gameView.GetInstanceID();
                session.originalDrawGizmos = ReadGizmos(gameView, true);
                session.originalShowGizmos = ReadGizmos(gameView, false);
                session.gizmosCaptured = true;
                Save(session); SuppressGizmos(session);
                using (var store = new FileStateStore(Path.Combine(session.saveRoot, "garden-mock", "state.xml"))) store.Save(SeedAlbum());
                Environment.SetEnvironmentVariable(SmokeVariable, session.saveRoot);
                // An unrelated personalization fixture override could otherwise
                // defeat the product's explicit smoke-mode collection suppression.
                Environment.SetEnvironmentVariable(PersonalizationVariable, null);
                EditorApplication.isPaused = false;
                EditorApplication.EnterPlaymode();
            }
            catch (Exception error) { Stop(session, error); }
        }

        static GardenState SeedCurrent(string identity, float growth)
        {
            var state = new GardenState { MigrationCompleted = true, InstallationId = "synthetic-album-play-owner", Nutrient = 100,
                LegacyGrowth = growth, MbctProgramSelectionVersion = 1, MbctProgramChosen = true };
            var appearance = new GardenPlantAppearance { Recipe = PlantGenerator.Create(state.InstallationId, identity) };
            appearance.Recipe.Counts = (int[])appearance.BaselineProfileCounts.Clone();
            state.PlantAppearance = appearance; GardenPlantVisuals.Sync(state); StateCodec.Validate(state); return state;
        }
        static GardenState SeedAlbum()
        {
            var photos = new List<GardenPlantPhoto>(); var commands = new List<GardenPlantPhotoCommand>();
            for (int index = 0; index < 3; index++)
            {
                var state = SeedCurrent("synthetic-play-photo-" + index, .1f * (index + 1));
                state.PlantPhotos = photos; state.PhotoSaveCommands = commands;
                var store = new FocusMissionValidation.MemoryStore(); store.Save(state);
                using (var service = new GardenStateService(store, new FocusMissionValidation.SyntheticClock(), new DemoBalanceConfig { VisitorChancePercent = 0 }))
                {
                    Assert(service.Open(), service.LastError);
                    string identity = service.Snapshot.PlantAppearance.Recipe.InstanceId;
                    Assert(service.SetPlantCompleted(identity, true) && service.SavePlantPhoto("synthetic-play-photo-command-" + index, identity), service.LastError);
                    photos = service.Snapshot.PlantPhotos; commands = service.Snapshot.PhotoSaveCommands;
                }
            }
            var current = SeedCurrent("synthetic-play-current", .3f);
            current.PurchaseReceipts.Add("synthetic-historical-growth");
            current.PurchaseCommands.Add(new PurchaseCommand { RequestId = "synthetic-historical-growth", Command = "growth|plant:P06" });
            current.GrowthHistory.Add(new GardenGrowthRecord { RequestId = "synthetic-historical-growth", PlantId = "plant:P06",
                PlantInstanceId = current.PlantAppearance.Recipe.InstanceId, LinkedSessionId = "", CreatedUtc = "2026-11-05T00:00:00Z", BeforeGrowth = .2f, AfterGrowth = .3f });
            current.PlantPhotos = photos; current.PhotoSaveCommands = commands;
            StateCodec.Validate(current); return current;
        }

        static IEnumerator RunGuarded(Session session, MockupNavigation nav, WeekOneQuestDemo ui)
        {
            var steps = Steps(session, nav, ui);
            while (true)
            {
                bool moved = false; object yielded = null; Exception failure = null;
                try { moved = steps.MoveNext(); if (moved) yielded = steps.Current; }
                catch (Exception error) { failure = error; }
                if (failure != null) { Stop(session, failure); yield break; }
                if (!moved) yield break;
                yield return yielded;
            }
        }

        static IEnumerator Steps(Session session, MockupNavigation nav, WeekOneQuestDemo ui)
        {
            yield return null; yield return null; yield return new WaitForEndOfFrame();
            session.width = Screen.width; session.height = Screen.height;
            Check(session, SessionState.GetInt(ErrorCountKey, 0) == 0,
                "Actual scene startup has no logged errors or unhandled exceptions");
            Check(session, !ReadGizmos(GameView(session), true) && !ReadGizmos(GameView(session), false),
                "Game-view debug gizmos stay disabled for clean product captures");
            Check(session, !PersonalizationClient.Instance.Configured && !PersonalizationClient.Instance.Consented,
                "Isolated MOCK Play does not enable research enrollment or transmission");
            Check(session, nav.home != null && nav.home.Planter != null
                && (GardenPlantVisuals.HasCurrentPlant(ui.Service.Snapshot) ? nav.home.RenderedRecipe != null : nav.home.RenderedRecipe == null),
                "The real garden presenter initialized its current plant or empty planter");
            if (session.phase > 1)
            {
                Check(session, StateCodec.Encode(ui.Service.Snapshot) == session.expectedState, "Restart restores every photo, current plant, wallet and record byte-for-byte");
                Check(session, ui.Service.Snapshot.PlantPhotos.Count == 4 && PlantCodec.Encode(ui.Service.Snapshot.PlantPhotos) == session.archivedPhotos
                    && GardenPlantVisuals.HasCurrentPlant(ui.Service.Snapshot) == (session.phase == 3),
                    "Restart restores all four frozen photos and the expected empty or newly growing garden");
                if (session.phase == 3) Check(session, ui.Service.Snapshot.PlantAppearance.Recipe.InstanceId == session.newPlantId
                    && ui.Service.Snapshot.PlantAppearance.Recipe.GrowthStep == 1,
                    "Second restart restores the new individual's identity and its own first growth");
            }
            else
            {
                Check(session, ui.Service.Snapshot.PlantPhotos.Count == 3 && ui.Service.Snapshot.PlantAppearance.IsCompleted,
                    "First Play loaded a synthetic completed plant with its historical paid-growth receipt");
            }
            string before = StateCodec.Encode(ui.Service.Snapshot);
            var gardenTarget = nav.home.gardenCamera.targetTexture;
            int nonGardenMeshes = ProceduralMeshCount() - (nav.home.ProceduralPlant == null ? 0 : 1);
            GardenVisualClock.SetOverrideMinutes(12 * 60);
            Button entry;
            if (session.phase == 1)
            {
                nav.ShowActivities(); ui.ShowShop(); yield return null; yield return new WaitForEndOfFrame();
                var completed = ui.Service.Snapshot;
                Check(session, completed.PlantAppearance.IsCompleted && completed.PlantAppearance.Recipe.Mature
                    && completed.Coins == 100 && completed.GrowthHistory.Count == 1 && completed.PurchaseReceipts.Count == 1,
                    "Completed legacy plant retains its old appearance, wallet and purchase history");
                Check(session, !EnabledButton(ui, "확정") && EnabledButton(ui, "사진첩으로 이동"),
                    "Completed plant offers an enabled album action without a paid-growth control");
                session.originalPlant = PlantCodec.Encode(completed.PlantAppearance.Recipe);
                session.archivedPlantId = completed.PlantAppearance.Recipe.InstanceId;
                session.finalGrowthRequest = completed.GrowthHistory.Single().RequestId;
                before = StateCodec.Encode(completed);
                Capture(session, "phase-1-completed-plant");
                entry = Reveal(ui, "사진첩으로 이동"); yield return null; yield return new WaitForEndOfFrame();
            }
            else
            {
                if (session.phase == 2)
                {
                    Check(session, nav.home.RenderedRecipe == null && nav.home.ProceduralPlant == null && nav.home.Planter != null,
                        "Archived plant stays absent from the real garden after restart while its planter remains");
                    // Presenter lives on UiCanvas; the real garden controls live
                    // under its separate serialized IntegratedHomeCanvas.
                    var create = Reveal(nav.home.homeCanvas, "Garden.StartNewPlant"); yield return null; yield return new WaitForEndOfFrame();
                    var oldCreate = create.onClick;
                    string savePath = Path.Combine(session.saveRoot, "garden-mock", "state.xml");
                    string diskBefore = File.ReadAllText(savePath), stagingPath = savePath + ".tmp";
                    Directory.CreateDirectory(stagingPath);
                    try { Click(session, create); }
                    finally { Directory.Delete(stagingPath); }
                    yield return null; yield return new WaitForEndOfFrame();
                    Check(session, StateCodec.Encode(ui.Service.Snapshot) == before && File.ReadAllText(savePath) == diskBefore
                        && nav.home.ProceduralPlant == null, "Actual new-plant save failure preserves the empty garden, wallet and file");
                    create = Reveal(nav.home.homeCanvas, "Garden.StartNewPlant"); yield return null; yield return new WaitForEndOfFrame();
                    Click(session, create); yield return null; yield return null; yield return new WaitForEndOfFrame();
                    var created = ui.Service.Snapshot;
                    session.newPlantId = created.PlantAppearance.Recipe.InstanceId;
                    Check(session, GardenPlantVisuals.HasCurrentPlant(created) && session.newPlantId != session.archivedPlantId
                        && created.PlantAppearance.Recipe.GrowthStep == 0 && GardenPlantVisuals.TotalGrowth(created) == 0
                        && created.PlantAppearance.Recipe.BaseSeed != created.PlantPhotos.Single(photo => photo.SourcePlantInstanceId == session.archivedPlantId).Recipe.BaseSeed
                        && created.Coins == 100 && PlantCodec.Encode(created.PlantPhotos) == session.archivedPhotos,
                        "Actual new-plant retry creates one independent free seed while preserving every archived photo");
                    string createdState = StateCodec.Encode(created); oldCreate.Invoke();
                    Check(session, StateCodec.Encode(ui.Service.Snapshot) == createdState && ui.Service.SavePlantPhoto(session.photoSaveRequest, session.archivedPlantId)
                        && !ui.Service.StartNewPlant("synthetic-stale-create", session.archivedPlantId) && StateCodec.Encode(ui.Service.Snapshot) == createdState,
                        "Queued old creation and archive requests cannot replace or remove the new seed");
                    Capture(session, "phase-2-new-seed-garden");
                    Check(session, ui.Service.BeginSession("synthetic-new-individual-practice", "free-mission:M03")
                        && ui.Service.StartSession("synthetic-new-individual-practice")
                        && ui.Service.CommitPracticeGrowth("synthetic-new-individual-practice")
                        && ui.Service.CompleteSession("synthetic-new-individual-practice"),
                        "Synthetic practice advances the actual newly created current plant through the production transaction");
                    nav.home.RefreshFromState(); yield return null; yield return new WaitForEndOfFrame();
                    var grown = ui.Service.Snapshot;
                    Check(session, grown.Coins == 100 && grown.PlantAppearance.Recipe.GrowthStep == 1
                        && grown.PlantAppearance.Recipe.InstanceId == session.newPlantId
                        && grown.PracticeGrowthRecords.Last().PlantInstanceId == session.newPlantId && PlantCodec.Encode(grown.PlantPhotos) == session.archivedPhotos,
                        "Production practice growth advances only the new individual and leaves the coin balance and archived appearances unchanged");
                    session.originalPlant = PlantCodec.Encode(grown.PlantAppearance.Recipe);
                    before = StateCodec.Encode(grown);
                }
                nav.ShowCourse(); yield return null; yield return new WaitForEndOfFrame();
                entry = Reveal(ui, "PlantAlbum.Open"); yield return null; yield return new WaitForEndOfFrame();
            }
            Click(session, entry); yield return null; yield return null; yield return new WaitForEndOfFrame();
            Check(session, ui.CurrentScreen == "plant-album", "Actual completion or My course button opens the photo album");
            Check(session, ui.GetComponentsInChildren<GardenPlantPhotoView>(false).Any(view => view.RenderCount > 0),
                "Normal player frames render visible thumbnails without forced RenderNow calls");
            Capture(session, "phase-" + session.phase + "-album");

            if (session.phase == 1)
            {
                var save = Reveal(ui, "PlantAlbum.SaveCurrent"); yield return null; yield return new WaitForEndOfFrame();
                Click(session, save); yield return null; yield return null; yield return new WaitForEndOfFrame();
                Check(session, ui.Service.Snapshot.PlantPhotos.Count == 4 && ui.Service.Snapshot.Coins == 100,
                    "Actual save button archives the completed current plant once without spending nutrients");
                Check(session, !GardenPlantVisuals.HasCurrentPlant(ui.Service.Snapshot) && !EnabledButton(ui, "PlantAlbum.SaveCurrent")
                    && EnabledButton(ui, "PlantAlbum.StartNew"), "Archive save empties the current garden and offers a new plant");
                session.archivedPhotos = PlantCodec.Encode(ui.Service.Snapshot.PlantPhotos);
                session.photoSaveRequest = ui.Service.Snapshot.PhotoSaveCommands.Single(command => command.PlantInstanceId == session.archivedPlantId).RequestId;
                before = StateCodec.Encode(ui.Service.Snapshot);
            }
            Check(session, ui.Service.GrowPlant(session.finalGrowthRequest, "plant:P06")
                && (session.phase != 1 || !ui.Service.GrowPlant("synthetic-post-archive", "plant:P06"))
                && StateCodec.Encode(ui.Service.Snapshot) == before,
                "The archived individual's growth retry cannot charge or grow an empty garden or its new plant");

            string photoId = ui.Service.Snapshot.PlantPhotos[1].Id;
            var photo = Reveal(ui, "PlantAlbum.Photo." + photoId); yield return null; yield return new WaitForEndOfFrame();
            Click(session, photo); yield return null; yield return null; yield return new WaitForEndOfFrame();
            var detail = ui.GetComponentsInChildren<GardenPlantPhotoView>(false).Single();
            Check(session, detail.IsInteractive && detail.RenderCount > 0, "Photo selection renders the interactive saved detail in Play");
            int initialRenders = detail.RenderCount;
            for (int frame = 0; frame < 10; frame++) { yield return null; session.editorFrameMs.Add(Time.unscaledDeltaTime * 1000); }
            Check(session, detail.RenderCount == initialRenders, "Unchanged detail stays cached across ten real player frames");
            Check(session, !ui.GetComponentsInChildren<Button>(false).Any(button => button.name == "PlantAlbum.ZoomIn"
                || button.name == "PlantAlbum.ZoomOut" || button.name == "PlantAlbum.ResetView"), "Photo detail excludes the removed inspection buttons");
            Pinch(session, detail, 24); yield return null; yield return null; yield return new WaitForEndOfFrame();
            Check(session, detail.Zoom > 1 && detail.RenderCount > initialRenders, "Two-finger zoom redraws the detail on a later player frame");
            Pinch(session, detail, -16); yield return null; yield return null; yield return new WaitForEndOfFrame();
            Check(session, Math.Abs(detail.Zoom - 1) < .001f, "Reverse pinch restores default magnification in Play");
            Capture(session, "phase-" + session.phase + "-detail");
            Check(session, StateCodec.Encode(ui.Service.Snapshot) == before, "Detail viewing and zoom leave current plant and all saved data unchanged");

            var back = Reveal(ui, "PlantAlbum.DetailBack"); yield return null; yield return new WaitForEndOfFrame();
            Click(session, back); yield return null; yield return null; yield return new WaitForEndOfFrame();
            Check(session, ui.CurrentScreen == "plant-album", "Actual detail back returns to the album");
            var home = Reveal(ui, "홈"); yield return null; yield return new WaitForEndOfFrame();
            Click(session, home); yield return null; yield return null; yield return null; yield return new WaitForEndOfFrame();
            Check(session, nav.home.IsGardenVisible && nav.home.homeCanvas.gameObject.activeSelf, "Actual home tab restores the existing garden");
            Check(session, (session.phase == 1 ? nav.home.RenderedRecipe == null && nav.home.ProceduralPlant == null && nav.home.Planter != null
                : PlantCodec.Encode(nav.home.RenderedRecipe) == session.originalPlant) && nav.home.gardenCamera.targetTexture == gardenTarget,
                "Empty planter or new current plant and original garden render target survive photo inspection");
            Check(session, GardenPlantPhotoView.ActiveViewCount == 0 && GardenPlantPhotoView.OwnedTextureCount == 0
                && GardenPlantPhotoView.SharedCameraCount == 0
                && ProceduralMeshCount() == nonGardenMeshes + (GardenPlantVisuals.HasCurrentPlant(ui.Service.Snapshot) ? 1 : 0),
                "Leaving the album releases its views, textures, camera and generated mesh after normal Destroy frames");
            Check(session, StateCodec.Encode(ui.Service.Snapshot) == before, "Garden return leaves stored state unchanged");
            Capture(session, "phase-" + session.phase + "-garden-return");
            session.expectedState = before;
            File.WriteAllText(Path.Combine(session.output, "phase-" + session.phase + "-synthetic-state.xml"), before);
            session.stage = session.phase < 3 ? "between" : "finish";
            session.deadline = EditorApplication.timeSinceStartup + 120;
            Save(session); EditorApplication.ExitPlaymode();
        }

        static int ProceduralMeshCount() => Resources.FindObjectsOfTypeAll<Mesh>().Count(mesh => mesh.name.StartsWith("ProceduralPlant_", StringComparison.Ordinal));
        static bool EnabledButton(WeekOneQuestDemo ui, string name) => ui.GetComponentsInChildren<Button>(false).Any(button => button.IsActive() && button.IsInteractable()
            && (button.name == name || button.GetComponentsInChildren<Text>(false).Any(text => text.text.Trim() == name)));
        static Button Reveal(Component ui, string name)
        {
            var button = ui.GetComponentsInChildren<Button>(false).FirstOrDefault(item => item.IsActive() && item.IsInteractable()
                && (item.name == name || item.GetComponentsInChildren<Text>(false).Any(text => text.text.Trim() == name)));
            Assert(button != null, "Visible enabled control missing: " + name);
            foreach (var scroll in button.GetComponentsInParent<ScrollRect>(false))
            {
                if (scroll.content == null || scroll.viewport == null) continue;
                Canvas.ForceUpdateCanvases(); scroll.StopMovement();
                var bounds = RectTransformUtility.CalculateRelativeRectTransformBounds(scroll.viewport, button.transform);
                float hidden = scroll.content.rect.height - scroll.viewport.rect.height;
                if (hidden > 0) scroll.verticalNormalizedPosition = Mathf.Clamp01(scroll.verticalNormalizedPosition
                    + (bounds.center.y - scroll.viewport.rect.center.y) / hidden);
            }
            return button;
        }
        static void Pinch(Session session, GardenPlantPhotoView view, float delta)
        {
            var system = EventSystem.current; Assert(system != null, "Actual EventSystem is absent.");
            var canvas = view.GetComponentInParent<Canvas>().rootCanvas;
            var camera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
            var rect = (RectTransform)view.transform;
            var center = RectTransformUtility.WorldToScreenPoint(camera, rect.TransformPoint(rect.rect.center));
            var points = new[] { center - Vector2.right * 24, center + Vector2.right * 24 };
            var pointers = new PointerEventData[2];
            for (int i = 0; i < pointers.Length; i++)
            {
                var pointer = new PointerEventData(system) { position = points[i], pointerId = i + 31,
                    pressPosition = points[i], button = PointerEventData.InputButton.Left };
                var hits = new List<RaycastResult>(); system.RaycastAll(pointer, hits); session.raycasts++;
                Assert(hits.Count > 0 && hits[0].gameObject == view.gameObject, "Pinch begins on the actual photo surface.");
                pointer.pointerPressRaycast = hits[0]; pointers[i] = pointer;
                ExecuteEvents.Execute(view.gameObject, pointer, ExecuteEvents.pointerDownHandler);
                ExecuteEvents.Execute(view.gameObject, pointer, ExecuteEvents.beginDragHandler);
            }
            pointers[1].position += Vector2.right * delta; pointers[1].delta = Vector2.right * delta;
            ExecuteEvents.Execute(view.gameObject, pointers[1], ExecuteEvents.dragHandler);
            foreach (var pointer in pointers)
            { ExecuteEvents.Execute(view.gameObject, pointer, ExecuteEvents.pointerUpHandler); ExecuteEvents.Execute(view.gameObject, pointer, ExecuteEvents.endDragHandler); }
            Save(session);
        }
        static void Click(Session session, Button button)
        {
            Assert(button != null && button.IsActive() && button.IsInteractable(), "Click target disappeared or became disabled.");
            var system = EventSystem.current; Assert(system != null, "Actual EventSystem is absent.");
            var canvas = button.GetComponentInParent<Canvas>().rootCanvas;
            var camera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
            var rect = (RectTransform)button.transform;
            var point = RectTransformUtility.WorldToScreenPoint(camera, rect.TransformPoint(rect.rect.center));
            Assert(new Rect(0, 0, Screen.width, Screen.height).Contains(point), "Click target is outside the live Game view: " + button.name);
            var pointer = new PointerEventData(system) { position = point, button = PointerEventData.InputButton.Left };
            var hits = new List<RaycastResult>(); system.RaycastAll(pointer, hits);
            Assert(hits.Count > 0 && hits[0].gameObject.GetComponentInParent<Button>() == button,
                "Live input is blocked for " + button.name + "; first hit=" + (hits.Count == 0 ? "none" : hits[0].gameObject.name));
            pointer.pointerPressRaycast = hits[0]; session.raycasts++;
            ExecuteEvents.Execute(button.gameObject, pointer, ExecuteEvents.pointerClickHandler);
            session.clicks++; Save(session);
        }
        static void Capture(Session session, string name)
        {
            Assert(!ReadGizmos(GameView(session), true) && !ReadGizmos(GameView(session), false),
                "Game-view gizmos were re-enabled before capture: " + name);
            var image = ScreenCapture.CaptureScreenshotAsTexture();
            Assert(image != null && image.width > 0 && image.height > 0, "Actual player screenshot is unavailable.");
            try
            {
                string path = Path.Combine(session.output, name + ".png"); File.WriteAllBytes(path, image.EncodeToPNG());
                var pixels = image.GetPixels32(); int min = 765, max = 0;
                for (int index = 0; index < pixels.Length; index += 31)
                {
                    int value = pixels[index].r + pixels[index].g + pixels[index].b;
                    min = Math.Min(min, value); max = Math.Max(max, value);
                }
                Assert(max - min >= 90, "Actual player screenshot is blank.");
                session.captures.Add(path); Save(session);
            }
            finally { UnityEngine.Object.Destroy(image); }
        }
        static void Check(Session session, bool condition, string description)
        {
            Assert(condition, description); session.assertions++; session.checks.Add("Phase " + session.phase + ": " + description); Save(session);
        }
        static void Assert(bool value, string message) { if (!value) throw new InvalidOperationException(message); }

        static void Stop(Session session, Exception error)
        {
            if (session == null) return;
            if (string.IsNullOrEmpty(session.error)) session.error = error.ToString();
            session.stage = "finish"; Save(session);
            if (EditorApplication.isPlayingOrWillChangePlaymode) EditorApplication.ExitPlaymode();
            else Finish(session);
        }
        static void Finish(Session session)
        {
            try
            {
                session.runtimeErrors = SessionState.GetInt(ErrorCountKey, 0);
                session.firstRuntimeError = SessionState.GetString(ErrorKey, "");
                if (session.runtimeErrors > 0 && string.IsNullOrEmpty(session.error))
                    session.error = string.IsNullOrEmpty(session.firstRuntimeError)
                        ? "A runtime error was logged during Play validation." : session.firstRuntimeError;
                Environment.SetEnvironmentVariable(SmokeVariable, session.smokeWasSet ? session.oldSmoke : null);
                Environment.SetEnvironmentVariable(PersonalizationVariable, session.personalizationWasSet ? session.oldPersonalization : null);
                EditorApplication.isPaused = session.originalPaused;
                try { RestoreGizmos(session); }
                catch (Exception restoreError) { session.error = (session.error ?? "") + "\nGame-view preference restoration failed: " + restoreError; }
                var focused = EditorUtility.InstanceIDToObject(session.focusWindow) as EditorWindow;
                if (focused != null) focused.Focus();
                session.environmentRestored = Environment.GetEnvironmentVariable(SmokeVariable) == (session.smokeWasSet ? session.oldSmoke : null)
                    && Environment.GetEnvironmentVariable(PersonalizationVariable) == (session.personalizationWasSet ? session.oldPersonalization : null);
                if (!session.environmentRestored && string.IsNullOrEmpty(session.error)) session.error = "Process environment restoration did not match original values.";
                session.stage = string.IsNullOrEmpty(session.error) ? "pass" : "fail";
            }
            finally
            {
                File.WriteAllText(Path.Combine(session.output, "play-report.json"), JsonUtility.ToJson(session, true));
                SessionState.EraseString(SessionKey);
                SessionState.EraseString(ErrorKey); SessionState.EraseInt(ErrorCountKey);
            }
            Debug.Log("PLANT_ALBUM_PLAY " + session.stage + " phases=" + session.phase + " assertions=" + session.assertions
                + " clicks=" + session.clicks + " output=" + session.output);
        }
    }
}
