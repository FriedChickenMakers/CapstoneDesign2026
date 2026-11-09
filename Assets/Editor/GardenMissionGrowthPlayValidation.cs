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
    // RunBatch uses only a separately copied project with a synthetic application
    // identity; it never opens/focuses an Editor window. The optional interactive
    // request uses the already-open product scene with an explicit isolated MOCK
    // save root. An explicit option can reload the unchanged scene after an old
    // fixture reused it; it never saves scenes, changes Game-view presets, closes
    // the Editor, or accesses a device. SessionState survives Play domain reloads.
    [InitializeOnLoad]
    public static class GardenMissionGrowthPlayValidation
    {
        const string SessionKey = "Capstone.MissionGrowthPlayValidation.Session";
        const string ErrorKey = SessionKey + ".FirstError";
        const string ErrorCountKey = SessionKey + ".ErrorCount";
        const string SmokeVariable = "CAPSTONE_SMOKE_SAVE_ROOT";
        const string PersonalizationVariable = "CAPSTONE_PERSONALIZATION_TEST_ROOT";
        static string Root => Path.Combine(Directory.GetCurrentDirectory(), "artifacts", "mission-growth-20261105");
        static string RequestPath => Path.Combine(Root, "play-request.json");
        static double nextPoll;

        [Serializable] public sealed class Request { public string runId, output; public bool restoreCleanSceneAfterLegacyFixture, batch; }
        [Serializable] sealed class Session
        {
            public string runId, utc, output, saveRoot, stage, error, unityVersion, firstRuntimeError;
            public string oldSmoke, oldPersonalization;
            public bool smokeWasSet, personalizationWasSet, originalPaused, environmentRestored, cleanSceneRestored, batch;
            public bool gizmosCaptured, originalDrawGizmos, originalShowGizmos, gizmosRestored;
            public int focusWindow, gameViewWindow, phase, assertions, clicks, raycasts, width, height, runtimeErrors;
            public string gizmoApi = "Unity 6000.3.24f1 UnityEditor.CoreModule metadata verified: GameView.drawGizmos and PlayModeView.showGizmos";
            public double deadline;
            public string expectedState;
            public List<string> checks = new List<string>();
            public List<string> captures = new List<string>();
            public List<float> editorFrameMs = new List<float>();
            public string environment = "Actual Unity Editor Play / isolated synthetic MOCK store / 21-stage automatic archive and next individual, practice fifty coins, emotion one hundred coins and memory leaf without growth, restart and background cleanup; not Android performance";
        }

        static GardenMissionGrowthPlayValidation()
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

        public static void RunBatch()
        {
            try
            {
                string project = Path.GetFullPath(Directory.GetCurrentDirectory());
                var args = Environment.GetCommandLineArgs();
                int outputIndex = Array.IndexOf(args, "-missionPlayOutput");
                Assert(Application.isBatchMode && Application.companyName == "CapstoneSyntheticValidation"
                    && Application.productName.StartsWith("DailyEmotionPlay-", StringComparison.Ordinal),
                    "Batch Play requires the isolated synthetic application identity before Editor startup.");
                Assert(project.IndexOf(Path.DirectorySeparatorChar + "artifacts" + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) >= 0
                    && Path.GetFileName(project) == "project", "Batch Play must run in a copied artifacts project.");
                Assert(outputIndex >= 0 && outputIndex + 1 < args.Length, "Missing isolated Play output path.");
                string output = Path.GetFullPath(args[outputIndex + 1]);
                Assert(output == Path.GetFullPath(Path.Combine(project, "..", "play")), "Play output must belong to this copied project run.");
                EditorSceneManager.OpenScene("Assets/Scenes/MockupMain.unity", OpenSceneMode.Single);
                // A fresh project's Search database initializes in delayed Editor
                // callbacks. Entering Play immediately can interrupt its enumeration
                // with a domain reload. Let those callbacks finish before Play.
                double earliest = EditorApplication.timeSinceStartup + 3;
                EditorApplication.CallbackFunction beginAfterStartup = null;
                beginAfterStartup = () =>
                {
                    if (EditorApplication.timeSinceStartup < earliest || EditorApplication.isCompiling || EditorApplication.isUpdating) return;
                    EditorApplication.update -= beginAfterStartup;
                    Begin(new Request { runId = "isolated-play", output = output, batch = true });
                };
                EditorApplication.update += beginAfterStartup;
            }
            catch (Exception error) { Debug.LogException(error); EditorApplication.Exit(1); }
        }

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
                { Stop(session, new TimeoutException("Mission growth Play validation exceeded its bounded phase deadline.")); return; }
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
                if (!session.batch)
                {
                    var gameView = GameView(session);
                    SuppressGizmos(session);
                    gameView.Focus();
                }
                Application.runInBackground = true;
                nav.StartCoroutine(RunGuarded(session, nav, ui));
            }
            catch (Exception error) { Stop(session, error); }
        }

        static void Begin(Request request)
        {
            string runId = request.runId ?? "play-" + DateTimeOffset.UtcNow.ToString("yyyyMMdd-HHmmss");
            Assert(Regex.IsMatch(runId, "^[A-Za-z0-9][A-Za-z0-9_-]{0,80}$"), "Use a simple unique Play run ID.");
            string output = request.batch ? request.output : Path.Combine(Root, runId);
            Assert(!Directory.Exists(output), "A fresh Play run directory is required.");
            Directory.CreateDirectory(output);
            var session = new Session {
                runId = runId, utc = DateTimeOffset.UtcNow.ToString("O"), output = output, batch = request.batch,
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
                if (!session.batch)
                {
                    var gameView = Resources.FindObjectsOfTypeAll(typeof(Editor).Assembly.GetType("UnityEditor.GameView", true)).FirstOrDefault() as EditorWindow;
                    Assert(gameView != null, "Open a Game view before requesting graphical Play validation.");
                    session.gameViewWindow = gameView.GetInstanceID();
                    session.originalDrawGizmos = ReadGizmos(gameView, true);
                    session.originalShowGizmos = ReadGizmos(gameView, false);
                    session.gizmosCaptured = true;
                    Save(session); SuppressGizmos(session);
                }
                else session.environment += " / hidden batch mode; captures show existing garden camera RenderTexture only, not full UI";
                using (var store = new FileStateStore(Path.Combine(session.saveRoot, "garden-mock", "state.xml"))) store.Save(SeedMission());
                Environment.SetEnvironmentVariable(SmokeVariable, session.saveRoot);
                // An unrelated personalization fixture override could otherwise
                // defeat the product's explicit smoke-mode collection suppression.
                Environment.SetEnvironmentVariable(PersonalizationVariable, null);
                EditorApplication.isPaused = false;
                Save(session);
                EditorApplication.EnterPlaymode();
            }
            catch (Exception error) { Stop(session, error); }
        }

        static GardenState SeedMission()
        {
            var clock=new SystemClock();var created=clock.UtcNow.AddDays(-10);
            string cycle=TimeZoneInfo.ConvertTime(created,clock.TimeZone).DateTime.AddHours(-4).ToString("yyyy-MM-dd");
            var state = new GardenState { MigrationCompleted = true, InstallationId = "synthetic-mission-growth-play-owner",
                Coins = 137, MbctProgramSelectionVersion = 1, MbctProgramChosen = true };
            CombinedPlants.InitializeFresh(state,"synthetic-play-plant-a",created.ToString("O"),cycle);
            var recipe=state.PlantAppearance.Recipe;
            for(int stage=0;stage<20;stage++){recipe.Counts[(int)GrowthCategory.Breath]++;recipe=PlantGenerator.Advance(recipe);}
            // An explicit isolated import baseline tests the last 20 -> 21 stage
            // without generating twenty unrelated UI sessions or modifying real data.
            state.PlantAppearance.Recipe=recipe;state.PlantAppearance.MissionGrowthBaseStep=20;
            state.PlantAppearance.MissionGrowthBaseGrowth=.3f*20/21;
            state.PlantAppearance.BaselineProfileCounts=(int[])recipe.Counts.Clone();
            var plant=WeeklyPlants.Active(state);plant.BaselineStep=20;plant.BaselineCounts=(int[])recipe.Counts.Clone();
            CombinedPlants.CaptureCurrent(state,clock.UtcNow.ToString("O"),cycle);
            var store = new MemoryStore(); store.Save(state);
            using (var service = new GardenStateService(store, clock, new DemoBalanceConfig { VisitorChancePercent = 0 }))
            {
                Assert(service.Open() && service.RefreshCycle(Array.Empty<string>()), service.LastError);
                PrepareLastStep(service, "synthetic-play-full", "M17");
                return service.Snapshot;
            }
        }
        static void CheckStoredGrowthPresentation(Session report)
        {
            var clock=new SystemClock();var state=new GardenState{MigrationCompleted=true,InstallationId="synthetic-stored-presentation",LegacyGrowth=.3f};
            GardenPlantVisuals.Sync(state);
            var missions=new[]{"free-mission:M17",DailyEmotionPolicy.MissionId};
            for(int i=0;i<missions.Length;i++)
            {
                var at=clock.UtcNow.AddDays(i-3);string cycle=TimeZoneInfo.ConvertTime(at,clock.TimeZone).DateTime.AddHours(-4).ToString("yyyy-MM-dd");
                string id=i==1?DailyEmotionPolicy.RecordId(cycle):"synthetic-stored-presentation-"+i;
                state.LastCycleId=cycle;state.DailyDecisions.Add(new DailyDecision{CycleId=cycle,AppliedUtc=at.ToString("O"),TimeZoneId=clock.TimeZone.Id,VisitorId=""});
                state.PracticeGrowthRecords.Add(new PracticeGrowthRecord{SessionId=id,EligibilityKey=i==1?"daily-emotion|"+cycle:cycle+"|"+missions[i],
                    CycleId=cycle,MissionId=missions[i],CommittedUtc=at.ToString("O"),Outcome=PracticeGrowthOutcome.Stored});
                state.StoredGrowthSteps++;
            }
            var store=new MemoryStore();store.Save(state);
            using(var service=new GardenStateService(store,clock,new DemoBalanceConfig{VisitorChancePercent=0}))
            {
                Assert(service.Open()&&service.RefreshCycle(Array.Empty<string>())&&service.BeginSession("synthetic-next-presentation","free-mission:M03",0)
                    &&service.StartSession("synthetic-next-presentation")&&service.CompleteSession("synthetic-next-presentation"),service.LastError);
                var after=service.Snapshot;string saved=StateCodec.Encode(after);
                var method=typeof(GardenMissionGrowthSequence).GetMethod("NextPlantBeforeGrowth",BindingFlags.Static|BindingFlags.NonPublic);
                Assert(method!=null,"New-individual presentation helper is missing.");
                var presented=(PlantRecipe)method.Invoke(null,new object[]{after});
                var expected=PlantGenerator.Create(after.InstallationId,after.PlantAppearance.Recipe.InstanceId,21,PlantGrowthMode.MissionCompletions);
                expected.MeshVersion=PlantGeometry.CurrentMeshVersion;
                foreach(var category in new[]{GrowthCategory.Breath,GrowthCategory.Reflection}){expected.Counts[(int)category]++;expected=PlantGenerator.Advance(expected);}
                Check(report,PlantCodec.Encode(presented)==PlantCodec.Encode(expected)&&presented.GrowthStep==2&&after.PlantAppearance.Recipe.GrowthStep==3,
                    "New-individual cinematic reconstructs historical stored categories in their saved order before only the newly earned stage");
                Check(report,StateCodec.Encode(after)==saved&&StateCodec.Encode(service.Snapshot)==saved,
                    "Preparing the new seed's appearance never changes its persisted recipe, receipts, bank or wallet");
            }
        }
        sealed class MemoryStore : IStateStore
        {
            string xml;
            public GardenState Load() => xml == null ? null : StateCodec.Decode(xml);
            public void Save(GardenState state) { xml = StateCodec.Encode(state); }
        }
        static void PrepareLastStep(GardenStateService service, string id, string code)
        {
            Assert(service.BeginFocusSession(id, code) && service.StartSession(id), "Prepare real practice: " + service.LastError);
            var session = service.Snapshot.Sessions.Single(item => item.SessionId == id);
            var mission = FocusMissionCatalog.FindForSession(session);
            for (int step = 0; step < mission.Steps.Length - 1; step++)
            {
                var item = mission.Steps[step];
                string value = item.Kind == "draw" ? "DRAW1|AA_B" : item.RequiredAnswer ? "synthetic input" : null;
                Assert(service.ConfirmFocusStep(id, step, value, value == null), "Prepare preceding practice step: " + service.LastError);
            }
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

        static GardenMissionGrowthSequence Sequence() => UnityEngine.Object.FindFirstObjectByType<GardenMissionGrowthSequence>(FindObjectsInactive.Include);
        static bool SequencePlaying() { var sequence = Sequence(); return sequence != null && sequence.IsPlaying; }
        static object RenderedFrame(Session session) => session.batch ? null : new WaitForEndOfFrame();
        static IEnumerator Steps(Session session, MockupNavigation nav, WeekOneQuestDemo ui)
        {
            yield return null; yield return null; yield return RenderedFrame(session);
            session.width = Screen.width; session.height = Screen.height;
            Check(session, SessionState.GetInt(ErrorCountKey, 0) == 0, "Actual scene startup logs no errors or exceptions");
            if (!session.batch) Check(session, !ReadGizmos(GameView(session), true) && !ReadGizmos(GameView(session), false), "Actual captures exclude editor gizmos");
            Check(session, !PersonalizationClient.Instance.Configured && !PersonalizationClient.Instance.Consented,
                "Synthetic MOCK run does not enroll research or transmit records");
            Check(session, ui.Service.CoinBalance == (session.phase==1?137:session.phase==2?187:337) && nav.home.RenderedRecipe != null,
                "The real garden restores the exact committed coin balance and current plant");
            if (session.phase > 1)
                Check(session, StateCodec.Encode(ui.Service.Snapshot) == session.expectedState, "Restart restores exact committed growth, wallet, session and records");
            if (session.phase == 1 || session.phase == 3)
                foreach (var frame in CheckSceneReentry(session, nav, ui)) yield return frame;
            nav.ShowActivities();
            if (session.phase == 1)
            {
                CheckStoredGrowthPresentation(session);
                ui.SelectFocusSession("synthetic-play-full");
                // Exercise the real page factory in Play, including AddComponent
                // and Unity's native-null semantics, rather than invoking Awake.
                Canvas.ForceUpdateCanvases();
                var entrance = ui.GetComponentsInChildren<FocusMissionEntrance>(false).SingleOrDefault();
                Check(session, !FigmaMissionUi.ReducedMotion && entrance != null,
                    "Actual practice page creates its entrance animation through the mobile layout");
                var entranceGroup = entrance.GetComponent<CanvasGroup>();
                Check(session, entranceGroup != null && entrance.GetComponents<CanvasGroup>().Length == 1
                    && !float.IsNaN(entranceGroup.alpha) && entranceGroup.alpha >= .65f && entranceGroup.alpha <= 1,
                    "RequireComponent and real Awake retain exactly one live CanvasGroup without fake-null errors");
                yield return new WaitForSecondsRealtime(.25f); yield return RenderedFrame(session);
                Check(session, entrance == null && entranceGroup != null && Mathf.Approximately(entranceGroup.alpha, 1),
                    "Entrance finishes normally and leaves the live page fully visible");
                var finalAction = Reveal(ui, "Focus.Action");
                string before = StateCodec.Encode(ui.Service.Snapshot), path = Path.Combine(session.saveRoot, "garden-mock", "state.xml");
                string disk = File.ReadAllText(path), staging = path + ".tmp";
                Directory.CreateDirectory(staging);
                try { Click(session, finalAction); }
                finally { Directory.Delete(staging); }
                yield return null; yield return RenderedFrame(session);
                Check(session, StateCodec.Encode(ui.Service.Snapshot) == before && File.ReadAllText(path) == disk
                    && !SequencePlaying(), "Failed actual final-step save does not grow, charge, start the effect or alter the committed file");
                Capture(session, "practice-final-step-save-failed");
                yield return new WaitForSecondsRealtime(.25f);
                finalAction = Reveal(ui, "Focus.Action");
                var previousRecipe = ui.Service.Snapshot.PlantAppearance.Recipe;
                var originalShaders = nav.home.ProceduralPlant.GetComponentsInChildren<Renderer>(true)
                    .SelectMany(renderer => renderer.sharedMaterials).Select(material => material.shader.name).Distinct().OrderBy(name => name).ToArray();
                var target = nav.home.gardenCamera.targetTexture;
                var position = nav.home.gardenCamera.transform.position;
                var rotation = nav.home.gardenCamera.transform.rotation;
                int cameraCount = Resources.FindObjectsOfTypeAll<Camera>().Length;
                var originalCanvases = Resources.FindObjectsOfTypeAll<Canvas>().Where(canvas => canvas.gameObject.scene.IsValid())
                    .ToDictionary(canvas => canvas.GetInstanceID(), canvas => canvas.enabled);
                Click(session, finalAction);
                yield return null; yield return RenderedFrame(session);
                Check(session, SequencePlaying() && ui.CurrentScreen != "focus-complete"
                    && ui.Service.Snapshot.Sessions.Single(item => item.SessionId == "synthetic-play-full").Status == ParticipationState.RewardCommitted,
                    "Last practice action atomically completes the mission and starts growth before the completion screen");
                var receipt = ui.Service.GetPracticeGrowth("synthetic-play-full");
                Check(session, receipt != null && receipt.Outcome == PracticeGrowthOutcome.Grown
                    && receipt.AfterStep == previousRecipe.GrowthStep + 1 && ui.Service.CoinBalance == 187,
                    "Exactly one next stage and fifty earned coins are durable before cinematic playback");
                Check(session,receipt.BeforeStep==20&&receipt.AfterStep==21&&ui.Service.Snapshot.PlantAppearance.IsCompleted
                    &&ui.Service.Snapshot.PlantPhotos.Single().SourcePlantInstanceId==previousRecipe.InstanceId
                    &&ui.Service.Snapshot.PlantPhotos.Single().Recipe.GrowthStep==21,
                    "The 21st stage automatically archives plant A while it stays visible through the complete cinematic");
                bool swirl = false, white = false, burst = false, orbit = false; float deadline = Time.realtimeSinceStartup + 12;
                while (SequencePlaying() && Time.realtimeSinceStartup < deadline)
                {
                    var sequence = Sequence();
                    Check(session, sequence.OrdinaryUiHidden && nav.home.gardenCamera.targetTexture == target,
                        "Cinematic hides ordinary UI and reuses the existing garden camera output");
                    orbit |= Quaternion.Angle(rotation, nav.home.gardenCamera.transform.rotation) > 1;
                    if (!swirl && sequence.Phase == "swirl" && sequence.Progress > .1f)
                    { Capture(session, "cinematic-white-whirlwind"); swirl = true; }
                    if (!white && sequence.Phase == "transform" && sequence.Progress >= .46f && sequence.Progress <= .53f)
                    {
                        Check(session, nav.home.RenderedRecipe.GrowthStep == previousRecipe.GrowthStep
                            && nav.home.ProceduralPlant.GetComponentsInChildren<Renderer>(true).SelectMany(renderer => renderer.sharedMaterials)
                                .All(material => material.shader.name == "Capstone/Plant Growth White"),
                            "Before the stage swap the old plant is completely covered by the white glow material");
                        Capture(session, "cinematic-white-before-stage-swap"); white = true;
                    }
                    if (!burst && sequence.Phase == "burst")
                    {
                        Check(session, nav.home.RenderedRecipe.GrowthStep == receipt.AfterStep && sequence.PeakParticleCount > 56,
                            "Burst reveals the committed next stage and emits celebratory particles beyond the swirling motes");
                        Capture(session, "cinematic-next-stage-burst"); burst = true;
                    }
                    yield return new WaitForSecondsRealtime(.12f); yield return RenderedFrame(session);
                }
                yield return null; yield return RenderedFrame(session);
                Check(session, !SequencePlaying() && ui.CurrentScreen == "focus-complete" && swirl && white && burst && orbit,
                    "Slow orbit, whirlwind and new-stage burst finish on the immediate mission completion screen");
                Check(session, Sequence().PeakParticleCount > 0
                    && !Resources.FindObjectsOfTypeAll<GameObject>().Any(item => item.name == "Mission growth effects (temporary)" || item.name == "Mission growth full-screen garden"),
                    "Visible particle effects ran and all temporary cinematic roots were released");
                Check(session, Resources.FindObjectsOfTypeAll<Canvas>().Where(canvas => originalCanvases.ContainsKey(canvas.GetInstanceID()))
                    .All(canvas => canvas.enabled == originalCanvases[canvas.GetInstanceID()]), "Every surviving ordinary canvas restores its original enabled state");
                Check(session, nav.home.ProceduralPlant.GetComponentsInChildren<Renderer>(true).SelectMany(renderer => renderer.sharedMaterials)
                    .Select(material => material.shader.name).Distinct().OrderBy(name => name).SequenceEqual(originalShaders),
                    "Final plant restores its normal shader instead of retaining the white cinematic material");
                Check(session, Vector3.Distance(position, nav.home.gardenCamera.transform.position) < .001f
                    && Quaternion.Angle(rotation, nav.home.gardenCamera.transform.rotation) < .01f
                    && nav.home.gardenCamera.targetTexture == target && Resources.FindObjectsOfTypeAll<Camera>().Length == cameraCount,
                    "Cinematic restores camera transform and target without adding a camera");
                string saved = StateCodec.Encode(ui.Service.Snapshot);
                ui.ShowFocusEmotion(); yield return null;
                Check(session, !SequencePlaying() && StateCodec.Encode(ui.Service.Snapshot) == saved,
                    "Legacy emotion entry redirects to completion without replaying growth");
                Capture(session, "immediate-completion-after-growth");
            }
            else if (session.phase == 2)
            {
                string restored = StateCodec.Encode(ui.Service.Snapshot);
                ui.SelectFocusSession("synthetic-play-full"); yield return null; yield return RenderedFrame(session);
                Check(session, ui.CurrentScreen == "focus-complete" && !SequencePlaying()
                    && StateCodec.Encode(ui.Service.Snapshot) == restored,
                    "Restart restores an already completed additional mission without reflection or another award");
                Check(session, ui.Service.BeginFocusSession("synthetic-play-background", "P01")
                    && ui.Service.StartGuidedFocusSession("synthetic-play-background", "코"),
                    "The same day allows the independent basic mission slot: " + ui.Service.LastError);
                ui.SelectFocusSession("synthetic-play-background"); yield return new WaitForSecondsRealtime(.25f); yield return RenderedFrame(session);
                var action = Reveal(ui, "Focus.IconAction");
                var previous=ui.Service.Snapshot;string archivedA=PlantCodec.Encode(previous.PlantPhotos.Single());
                string previousId=previous.PlantAppearance.Recipe.InstanceId;
                var target = nav.home.gardenCamera.targetTexture; var position = nav.home.gardenCamera.transform.position;
                var rotation = nav.home.gardenCamera.transform.rotation;
                Click(session, action); yield return null; yield return RenderedFrame(session);
                Check(session, SequencePlaying()
                    && ui.Service.Snapshot.Sessions.Single(item => item.SessionId == "synthetic-play-background").Status == ParticipationState.RewardCommitted,
                    "Guided basic practice completes before its distinct one-stage cinematic");
                var nextState=ui.Service.Snapshot;var nextReceipt=ui.Service.GetPracticeGrowth("synthetic-play-background");
                Check(session,nextState.PlantAppearance.Recipe.InstanceId!=previousId&&nextReceipt.BeforeStep==0&&nextReceipt.AfterStep==1
                    &&nav.home.RenderedRecipe.InstanceId==nextState.PlantAppearance.Recipe.InstanceId&&nav.home.RenderedRecipe.GrowthStep==0
                    &&PlantCodec.Encode(nextState.PlantPhotos.Single())==archivedA,
                    "Completed plant A stays archived while the cinematic initially displays new plant B's own seed, never A's mature mesh");
                Check(session,Sequence().OrdinaryUiHidden&&nav.home.gardenCamera.targetTexture==target,
                    "The new-individual cinematic also hides ordinary UI and retains the shared garden RenderTexture");
                yield return new WaitForSecondsRealtime(1.6f); yield return RenderedFrame(session);
                var sequence = Sequence();
                var pause = typeof(GardenMissionGrowthSequence).GetMethod("OnApplicationPause", BindingFlags.Instance | BindingFlags.NonPublic);
                Assert(pause != null, "Cinematic must expose the Unity pause lifecycle callback.");
                pause.Invoke(sequence, new object[] { true });
                yield return null; yield return null; yield return RenderedFrame(session);
                Check(session, !SequencePlaying() && ui.CurrentScreen == "focus-complete"
                    && Vector3.Distance(position, nav.home.gardenCamera.transform.position) < .001f
                    && Quaternion.Angle(rotation, nav.home.gardenCamera.transform.rotation) < .01f
                    && nav.home.gardenCamera.targetTexture == target,
                    "Unity pause callback restores camera and immediately completed mission UI");
                Check(session,nav.home.RenderedRecipe.InstanceId==nextState.PlantAppearance.Recipe.InstanceId&&nav.home.RenderedRecipe.GrowthStep==1
                    &&PlantCodec.Encode(ui.Service.Snapshot.PlantPhotos.Single())==archivedA,
                    "Interrupting new-individual growth restores the committed B stage and leaves archived A unchanged");
                Check(session, !nav.home.ProceduralPlant.GetComponentsInChildren<Renderer>(true).SelectMany(renderer => renderer.sharedMaterials)
                    .Any(material => material.shader.name == "Capstone/Plant Growth White")
                    && !Resources.FindObjectsOfTypeAll<GameObject>().Any(item => item.name == "Mission growth effects (temporary)"),
                    "Interruption while glowing releases the effect and restores normal plant materials");
                string interrupted = StateCodec.Encode(ui.Service.Snapshot);
                ui.ShowFocusEmotion(); yield return null;
                pause.Invoke(sequence, new object[] { true }); yield return null;
                Check(session, StateCodec.Encode(ui.Service.Snapshot) == interrupted && !SequencePlaying()
                    && ui.CurrentScreen == "focus-complete",
                    "Repeated pause and legacy reflection reentry cannot duplicate the completion callback or earned stage");
                Capture(session, "completion-after-background-interruption");
                Check(session, ui.Service.CoinBalance == 237
                    && ui.Service.Snapshot.PracticeGrowthRecords.Count(r => r.Outcome == PracticeGrowthOutcome.Grown) == 2
                    && DailyEmotionPolicy.Availability(ui.Service.Snapshot) == null,
                    "Two earned practice stages and two fifty-coin awards unlock the independent emotion mission");

                ui.ShowDailyEmotion(); yield return null; yield return RenderedFrame(session);
                var emotion = DailyEmotionPolicy.Current(ui.Service.Snapshot);
                Check(session, emotion != null && !emotion.IsCompleted && ui.CurrentScreen == "daily-emotion",
                    "Daily emotion opens only after both real practice completions");
                var note = ui.GetComponentsInChildren<InputField>(false).Single(item => item.name == "DailyEmotion.Note");
                note.text = "synthetic daily emotion PlayMode note";
                yield return null;
                var save = Reveal(ui, "DailyEmotion.Save");
                int beforeStep = ui.Service.Snapshot.PlantAppearance.Recipe.GrowthStep;
                Click(session, save); yield return null; yield return RenderedFrame(session);
                var receipt = ui.Service.GetPracticeGrowth(emotion.Id);
                var completedEmotion=ui.Service.GetDailyEmotion(emotion.Id);
                var memo=ui.Service.Snapshot.PlantAppearance.Memos.Single();
                Check(session, !SequencePlaying() && completedEmotion.IsCompleted && completedEmotion.RewardAmount==100
                    && completedEmotion.MemoId==memo.Id && receipt == null
                    && ui.Service.Snapshot.PlantAppearance.Recipe.GrowthStep==beforeStep && ui.Service.CoinBalance == 337,
                    "Emotion completion atomically saves one hundred coins and one plant memo without growth or cinematic");
                Check(session, memo.PlantInstanceId==ui.Service.Snapshot.PlantAppearance.Recipe.InstanceId
                    && memo.Note=="synthetic daily emotion PlayMode note"
                    &&WeeklyPlants.Active(ui.Service.Snapshot).Memories.Single().RecordId==memo.Id
                    &&nav.home.ProceduralPlant.Geometry.Organs.Single().OrganId==WeeklyPlants.Active(ui.Service.Snapshot).Memories.Single().OrganId
                    &&nav.home.ProceduralPlant.Geometry.Organs.Single().Selectable,
                    "The real garden renders the durable emotion as the current plant's selectable memory leaf");
                string committedEmotion = StateCodec.Encode(ui.Service.Snapshot);
                Check(session, ui.Service.CompleteDailyEmotion(emotion.Id, "ignored duplicate", "ignored duplicate")
                    && StateCodec.Encode(ui.Service.Snapshot) == committedEmotion,
                    "A duplicate emotion command cannot change the memo or award coins twice");
                yield return null; yield return RenderedFrame(session);
                Check(session, !SequencePlaying() && ui.CurrentScreen == "home"
                    && StateCodec.Encode(ui.Service.Snapshot) == committedEmotion
                    && ui.Service.Snapshot.PracticeGrowthRecords.Count(r => r.Outcome == PracticeGrowthOutcome.Grown) == 2,
                    "Emotion completion returns home with only the two practice stages and unchanged committed data");
                Check(session, Vector3.Distance(position, nav.home.gardenCamera.transform.position) < .001f
                    && Quaternion.Angle(rotation, nav.home.gardenCamera.transform.rotation) < .01f
                    && nav.home.gardenCamera.targetTexture == target
                    && !Resources.FindObjectsOfTypeAll<GameObject>().Any(item => item.name == "Mission growth effects (temporary)" || item.name == "Mission growth full-screen garden"),
                    "Saving an emotion memo leaves the existing camera intact and creates no cinematic resources");
                Capture(session, "garden-after-daily-emotion-memo");
            }
            else
            {
                string before = StateCodec.Encode(ui.Service.Snapshot);
                var emotion = DailyEmotionPolicy.Current(ui.Service.Snapshot);
                Check(session, emotion?.IsCompleted == true && ui.Service.GetPracticeGrowth(emotion.Id) == null
                    && emotion.RewardAmount==100 && nav.home.ProceduralPlant.Geometry.Organs.Count==1
                    && WeeklyPlants.Active(ui.Service.Snapshot).Memories.Single().RecordId==emotion.MemoId
                    && nav.home.ProceduralPlant.Geometry.Organs.Single().OrganId==WeeklyPlants.Active(ui.Service.Snapshot).Memories.Single().OrganId,
                    "Second restart restores the emotion coin award and the same rendered memo without a growth receipt");
                ui.ShowDailyEmotion(emotion.Id); yield return null; yield return RenderedFrame(session);
                Check(session, !SequencePlaying() && ui.CurrentScreen == "daily-emotion"
                    && !EnabledButton(ui, "DailyEmotion.Save") && StateCodec.Encode(ui.Service.Snapshot) == before,
                    "Completed emotion reopens read-only without replay, loss or duplicate growth");
                ui.ShowFocusCompletion("synthetic-play-background"); yield return null; yield return RenderedFrame(session);
                Check(session, !SequencePlaying() && ui.CurrentScreen == "focus-complete" && StateCodec.Encode(ui.Service.Snapshot) == before,
                    "Practice completion remains directly accessible after emotion completion and restart");
                var garden = Reveal(ui, "Focus.GoGarden"); Click(session, garden); yield return null; yield return RenderedFrame(session);
                Check(session, nav.home.IsGardenVisible && nav.home.homeCanvas.gameObject.activeSelf
                    && PlantCodec.Encode(nav.home.RenderedRecipe) == PlantCodec.Encode(ui.Service.Snapshot.PlantAppearance.Recipe),
                    "Normal garden navigation, two earned stages and the plant-owned memo survive both restarts");
                Capture(session, "garden-after-mission-and-emotion-restarts");
            }
            Check(session, SessionState.GetInt(ErrorCountKey, 0) == 0, "No runtime errors occurred during the actual cinematic flow");
            session.expectedState = StateCodec.Encode(ui.Service.Snapshot);
            File.WriteAllText(Path.Combine(session.output, "phase-" + session.phase + "-synthetic-state.xml"), session.expectedState);
            session.stage = session.phase < 3 ? "between" : "finish";
            session.deadline = EditorApplication.timeSinceStartup + 120;
            Save(session); EditorApplication.ExitPlaymode();
        }

        static IEnumerable<object> CheckSceneReentry(Session session, MockupNavigation nav, WeekOneQuestDemo ui)
        {
            var home = nav.home;
            Check(session, SceneManager.GetActiveScene().path == "Assets/Scenes/MockupMain.unity"
                && home.IsGardenVisible && ui.HasOpenState, "Reentry starts in the real open-state MockupMain garden");
            var service = ui.Service;
            string saved = StateCodec.Encode(service.Snapshot);
            var camera = home.gardenCamera;
            var target = camera.targetTexture;
            int cameraCount = Resources.FindObjectsOfTypeAll<Camera>().Length;
            var sky = home.gardenVisuals.GetComponent<GardenSkyEnvironment>();
            Check(session, sky != null && sky.Clouds != null && target != null && target.IsCreated(),
                "Actual garden owns an existing render target and cloud particle system");
            var clouds = sky.Clouds;
            for (int round = 1; round <= 2; round++)
            {
                Click(session, home.NavigationBar.MissionButton);
                yield return null; yield return new WaitForSecondsRealtime(.25f);
                Check(session, nav.activitiesPanel.activeInHierarchy && !home.IsGardenVisible
                    && ui.HasOpenState && ui.CurrentScreen == "home", "Home tab pointer opens the real mission page");
                var bar = ui.GetComponentsInChildren<MissionBottomNavigation>(false).Single();
                Check(session, bar.ActiveTab == "미션", "Mission navigation visibly selects the mission tab");
                Click(session, bar.CourseButton);
                yield return null; yield return new WaitForSecondsRealtime(.25f);
                bar = ui.GetComponentsInChildren<MissionBottomNavigation>(false).Single();
                Check(session, ui.HasOpenState && ui.CurrentScreen == "daily-emotion-history" && bar.ActiveTab == "기록"
                    && ReferenceEquals(service, ui.Service)
                    && ui.GetComponentsInChildren<Text>(false).Any(text => text.name == "ActivityJournal.Month"),
                    "Records tab pointer opens the activity journal with the same loaded service");
                Check(session, !home.gardenVisuals.activeInHierarchy,
                    "The garden and its cloud system are actually disabled while viewing records");
                if (round == 2)
                {
                    // Reproduce a stopped particle system after suspension. Only
                    // normal product navigation may restart it on return.
                    clouds.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                    Check(session, !clouds.isPlaying, "Stopped-cloud reentry precondition is established");
                }
                // A retained texture can look valid while containing a stale
                // frame. Poison this synthetic target, then require the normal
                // return/render lifecycle to replace it; never call Camera.Render.
                var oldActive = RenderTexture.active;
                try { RenderTexture.active = target; GL.Clear(true, true, Color.magenta); }
                finally { RenderTexture.active = oldActive; }
                Click(session, bar.HomeButton);
                yield return null; yield return new WaitForSecondsRealtime(.25f); yield return RenderedFrame(session);
                Check(session, home.IsGardenVisible && !nav.activitiesPanel.activeInHierarchy
                    && home.gardenVisuals.activeInHierarchy && ui.HasOpenState && ReferenceEquals(service, ui.Service),
                    "Home pointer restores the actual garden without reopening or losing its state service");
                Check(session, home.gardenCamera == camera && camera.targetTexture == target && target.IsCreated()
                    && Resources.FindObjectsOfTypeAll<Camera>().Length == cameraCount,
                    "Tab reentry keeps the same camera and live RenderTexture without extra cameras");
                var displays = home.homeCanvas.GetComponentsInChildren<RawImage>(false)
                    .Where(image => image.texture == target && image.enabled).ToArray();
                Check(session, displays.Length == 1 && displays[0].rectTransform.rect.width > 0
                    && displays[0].rectTransform.rect.height > 0,
                    "The visible garden RawImage still displays that exact RenderTexture");
                CheckRenderTargetContent(session, target);
                Check(session, clouds == sky.Clouds && clouds.gameObject.activeInHierarchy
                    && clouds.isPlaying && clouds.particleCount > 0,
                    "Cloud particles resume playing and remain populated after returning home");
                var first = new ParticleSystem.Particle[clouds.particleCount]; int count = clouds.GetParticles(first);
                yield return new WaitForSecondsRealtime(.25f);
                var later = new ParticleSystem.Particle[clouds.particleCount]; int laterCount = clouds.GetParticles(later);
                Check(session, count == laterCount && count > 0
                    && Enumerable.Range(0, count).Any(index => (first[index].position - later[index].position).sqrMagnitude > .00000001f),
                    "Returned clouds keep moving across actual Play frames");
                Check(session, StateCodec.Encode(service.Snapshot) == saved,
                    "Home, mission and records navigation preserves exact plant, wallet and record state");
                Capture(session, "phase-" + session.phase + "-scene-reentry-" + round);
            }
            Check(session, SessionState.GetInt(ErrorCountKey, 0) == 0,
                "Repeated actual-scene tab reentry logs no errors, destroyed-component or render-target exceptions");
        }

        static void CheckRenderTargetContent(Session session, RenderTexture target)
        {
            var previous = RenderTexture.active;
            var image = new Texture2D(target.width, target.height, TextureFormat.RGB24, false);
            try
            {
                RenderTexture.active = target;
                image.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0); image.Apply();
                var pixels = image.GetPixels32(); int min = 765, max = 0, samples = 0, refreshed = 0;
                for (int index = 0; index < pixels.Length; index += 31)
                {
                    var pixel = pixels[index]; int value = pixel.r + pixel.g + pixel.b;
                    min = Math.Min(min, value); max = Math.Max(max, value); samples++;
                    if (pixel.r < 245 || pixel.g > 10 || pixel.b < 245) refreshed++;
                }
                Check(session, max - min >= 90 && refreshed > samples / 2,
                    "Normal return redraws varied garden pixels over the sentinel instead of retaining a stale or blank target");
            }
            finally { RenderTexture.active = previous; UnityEngine.Object.Destroy(image); }
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
            ExecuteEvents.Execute(button.gameObject, pointer, ExecuteEvents.pointerDownHandler);
            ExecuteEvents.Execute(button.gameObject, pointer, ExecuteEvents.pointerUpHandler);
            ExecuteEvents.Execute(button.gameObject, pointer, ExecuteEvents.pointerClickHandler);
            session.clicks++; Save(session);
        }
        static void Capture(Session session, string name)
        {
            if (!session.batch) Assert(!ReadGizmos(GameView(session), true) && !ReadGizmos(GameView(session), false),
                "Game-view gizmos were re-enabled before capture: " + name);
            Texture2D image;
            if (session.batch)
            {
                var home = UnityEngine.Object.FindFirstObjectByType<GardenHomePresenter>(FindObjectsInactive.Include);
                var target = home?.gardenCamera?.targetTexture;
                Assert(target != null && target.IsCreated(), "Existing garden render target is unavailable.");
                var previous = RenderTexture.active;
                try
                {
                    RenderTexture.active = target;
                    image = new Texture2D(target.width, target.height, TextureFormat.RGB24, false);
                    image.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0);
                    image.Apply();
                }
                finally { RenderTexture.active = previous; }
                name += "-garden-target-only";
            }
            else image = ScreenCapture.CaptureScreenshotAsTexture();
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
                if (!session.batch && focused != null) focused.Focus();
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
            Debug.Log("MISSION_GROWTH_PLAY " + session.stage + " phases=" + session.phase + " assertions=" + session.assertions
                + " clicks=" + session.clicks + " output=" + session.output);
            if (session.batch) EditorApplication.Exit(session.stage == "pass" ? 0 : 1);
        }
    }
}
