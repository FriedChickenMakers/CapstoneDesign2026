using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
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
    // Explicit isolated batch only. No poll, automatic refresh, scene asset or real save access.
    public static class GuidedFocusValidation
    {
        [Serializable] sealed class Check { public string name, status, detail; }
        [Serializable] sealed class Report
        {
            public string utc, unityVersion, environment;
            public int passed, failed, assertions, clicks, raycasts;
            public List<Check> checks = new List<Check>();
            public List<string> captures = new List<string>(), errors = new List<string>();
        }
        static Report report;
        static string output;
        static bool capture;
        public static void RunBatch()
        {
            var args = Environment.GetCommandLineArgs(); int index = Array.IndexOf(args, "-guidedReport");
            if (!Application.isBatchMode || Application.companyName != "CapstoneSyntheticValidation"
                || (!Application.productName.StartsWith("GuidedFocus-", StringComparison.Ordinal) && Application.productName != "MemoPopup-v07") || index < 0)
                throw new InvalidOperationException("GuidedFocusValidation requires a separate synthetic batch project.");
            string path = args[index + 1]; output = Path.GetDirectoryName(path); Directory.CreateDirectory(output); capture = args.Contains("-guidedCapture");
            report = new Report { utc = DateTimeOffset.UtcNow.ToString("O"), unityVersion = Application.unityVersion,
                environment = "Hidden isolated Unity edit-mode batch; actual runtime page factories, memory state, pointer raycasts/clicks; capture=" + capture };
            Application.logMessageReceived += Observe;
            try
            {
                foreach (var size in new[] { new Vector2Int(360, 640), new Vector2Int(390, 844) })
                    Run("P01-P07 slides, centered action and durable practice " + size.x + "x" + size.y, () => Flow(size.x, size.y));
                Run("Persisted version-2 page remains the previous runner", VersionTwoPage);
                Run("Existing active v3 saved draft resumes practice", ActiveVersionThreeResume);
                Run("Guided debug entry preserves original service", DebugOverview);
            }
            finally
            {
                Application.logMessageReceived -= Observe;
                if (report.errors.Count > 0) { report.failed++; report.checks.Add(new Check { name = "Unity errors", status = "fail", detail = string.Join("\n", report.errors) }); }
                File.WriteAllText(path, JsonUtility.ToJson(report, true));
            }
            Debug.Log("GUIDED_FOCUS " + (report.failed == 0 ? "PASS" : "FAIL") + " assertions=" + report.assertions);
            EditorApplication.Exit(report.failed == 0 ? 0 : 1);
        }
        static void Observe(string text, string stack, LogType type)
        { if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) report.errors.Add(type + ": " + text + "\n" + stack); }
        static void Run(string name, Action action)
        { try { action(); report.passed++; report.checks.Add(new Check { name = name, status = "pass" }); }
          catch (Exception ex) { report.failed++; report.checks.Add(new Check { name = name, status = "fail", detail = ex.ToString() }); } }
        static void Assert(bool value, string message) { report.assertions++; if (!value) throw new InvalidOperationException(message); }
        static string Screen(WeekOneQuestDemo ui) => (string)typeof(WeekOneQuestDemo).GetField("screen", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(ui);
        static void Flow(int width, int height)
        {
            using (var f = new Fixture(width, height))
            {
                Assert(f.Service.SelectMbctProgram("8w"), f.Service.LastError);
                for (int order = 1; order <= 7; order++)
                {
                    while (FocusMissionPolicy.Availability(f.Service.Snapshot, "8w") != null)
                    { f.Clock.Time = f.Clock.Time.AddDays(1); Assert(f.Service.RefreshCycle(Array.Empty<string>()), f.Service.LastError); }
                    string id = "synthetic-guided-ui-" + order, code = "P" + order.ToString("00");
                    var definition = FocusMissionCatalog.Find(code);
                    Assert(f.Service.BeginFocusSession(id, code, order, program: "8w"), f.Service.LastError);
                    f.Ui.SelectFocusSession(id); f.Layout();
                    f.AssertSlide("Guided.Overview", definition.Overview);
                    Rect anchor = f.AssertIcon(); f.AssertCenteredPress();
                    Assert(f.Button("Focus.Close").GetComponentInChildren<Text>().text == "나가기", code + " exit says 나가기");
                    Assert(f.Button("Guided.Previous", false) == null && f.Button("Focus.TimerToggle", false) == null,
                        code + " overview has no prior slide or running practice clock");
                    f.Capture(code + "-overview");
                    var oldOverview = f.Button("Focus.IconAction").onClick;
                    f.Ui.gameObject.SetActive(false); oldOverview.Invoke(); f.Ui.gameObject.SetActive(true); f.Layout();
                    f.AssertSlide("Guided.Overview", definition.Overview);
                    f.Click("Focus.IconAction"); oldOverview.Invoke(); f.Layout();
                    f.AssertSlide("Guided.Preparation", FocusGuidedPracticeContent.Preparation(code)); f.AssertIcon(anchor);
                    Assert(f.Session(id).Status == ParticipationState.Selected, code + " repeated old overview action cannot skip preparation or start");
                    f.Click("Guided.Previous"); f.AssertSlide("Guided.Overview", definition.Overview);
                    f.Click("Focus.IconAction"); f.AssertSlide("Guided.Preparation", FocusGuidedPracticeContent.Preparation(code));
                    if (order == 1)
                    {
                        string beforeExit = StateCodec.Encode(f.Service.Snapshot);
                        f.Click("Focus.Close");
                        Assert(Screen(f.Ui) == "focus-home" || Screen(f.Ui) == "home", "Exit leaves preparation rather than advancing instruction");
                        Assert(StateCodec.Encode(f.Service.Snapshot) == beforeExit, "Exit before practice neither starts, grows nor rewards");
                        f.Ui.SelectFocusSession(id); f.Layout();
                        if (f.Text("Guided.Overview") != null) f.Click("Focus.IconAction");
                        f.AssertSlide("Guided.Preparation", FocusGuidedPracticeContent.Preparation(code));
                    }
                    f.Click("Focus.IconAction");
                    string chosen = "";
                    if (definition.Steps[0].Options.Length > 0)
                    {
                        f.AssertSlide("Guided.ChoiceTitle", "편한 하나를 골라요."); f.AssertIcon(anchor);
                        f.Click("Focus.IconAction");
                        f.AssertSlide("Guided.Cue.0", FocusGuidedPracticeContent.Guidance(code, "")[0]);
                        Assert(f.Session(id).FocusStepRecords[0].Value == "", code + " no selection uses default guidance without forced answer");
                        f.Click("Guided.Previous");
                        foreach (string choice in definition.Steps[0].Options)
                        {
                            if (chosen.Length > 0) f.Click("Guided.Previous");
                            if (order == 1 && chosen.Length == 0)
                            {
                                string beforeChoice = StateCodec.Encode(f.Service.Snapshot);
                                f.Store.Fail = true; f.Click("Guided.Choice." + choice);
                                Assert(f.Text("Guided.ChoiceTitle") != null && StateCodec.Encode(f.Service.Snapshot) == beforeChoice,
                                    "Failed choice remains on selection with original durable state");
                                Assert((string)typeof(WeekOneQuestDemo).GetField("guidedChoice", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(f.Ui) == choice,
                                    "Failed choice keeps the local selected answer for retry");
                                f.AssertReadingSeparation(); f.Capture("P01-choice-save-failed");
                                f.Click("Guided.HelpToggle"); f.AssertSlide("Guided.Help", FocusGuidedPracticeContent.Help(code).Split('\n')[0]);
                                f.Click("Guided.Previous");
                                Assert(f.Text("Guided.ChoiceTitle") != null && StateCodec.Encode(f.Service.Snapshot) == beforeChoice,
                                    "Help and previous remain readable during failed storage without losing selection");
                                f.Store.Fail = false;
                            }
                            f.Click("Guided.Choice." + choice); chosen = choice;
                            f.AssertSlide("Guided.Cue.0", FocusGuidedPracticeContent.Guidance(code, choice)[0]); f.AssertIcon(anchor);
                            Assert(f.Session(id).FocusStepRecords[0].Value == choice && f.Session(id).Status == ParticipationState.Selected,
                                code + " selected branch immediately opens its first instruction, without practice start");
                        }
                    }
                    for (int cue = 0; cue < 3; cue++)
                    {
                        f.AssertSlide("Guided.Cue." + cue, FocusGuidedPracticeContent.Guidance(code, chosen)[cue]); f.AssertIcon(anchor);
                        Assert(f.Session(id).Status == ParticipationState.Selected && f.Session(id).InstructionStep == 0
                            && f.Service.GetPracticeGrowth(id) == null && f.Service.CoinBalance == 137 + (order - 1) * 50,
                            code + " reading cue " + cue + " has no timer, domain progression or reward");
                        if (cue == 0 || code == "P01") f.Capture(code + "-cue-" + cue);
                        if (cue < 2) f.Click("Focus.IconAction");
                    }
                    string beforeStart = StateCodec.Encode(f.Service.Snapshot);
                    f.Store.Fail = true; f.Click("Focus.IconAction"); f.Store.Fail = false;
                    Assert(f.Text("Guided.Cue.2") != null && f.Session(id).Status == ParticipationState.Selected
                        && StateCodec.Encode(f.Service.Snapshot) == beforeStart, code + " failed start stays on the last instruction and preserves all state");
                    f.AssertReadingSeparation();
                    if (order == 1) f.Capture(code + "-start-save-failed");
                    var oldStart = f.Button("Focus.IconAction").onClick;
                    f.Click("Focus.IconAction"); oldStart.Invoke(); f.Layout();
                    f.AssertSlide("Guided.PracticeHint", FocusGuidedPracticeContent.PracticeHint(code)); f.AssertIcon(anchor);
                    var clock = f.Ui.GetComponentInChildren<FocusPracticeClock>(false);
                    Assert(f.Session(id).Status == ParticipationState.InProgress && clock != null && clock.Running,
                        code + " only the final instruction starts practice and the timer; stale start cannot finish");
                    int expectedElapsed = 17; clock.ElapsedSeconds = expectedElapsed; Assert(clock.Flush(), "Persist synthetic elapsed callback " + code);
                    f.Capture(code + "-practice");
                    string savedDraft = StateCodec.Encode(f.Service.Snapshot);
                    if (order == 1) { f.Store.Fail = true; clock.ElapsedSeconds = 18; expectedElapsed = 18; }
                    f.Click("Guided.HelpToggle");
                    Assert(!clock.Running, code + " leaving practice for help pauses its previous clock");
                    var help = FocusGuidedPracticeContent.Help(code).Split('\n');
                    for (int page = 0; page < help.Length; page++)
                    {
                        f.AssertSlide("Guided.Help", help[page]); f.AssertIcon(anchor);
                        Assert(f.Ui.GetComponentInChildren<FocusPracticeClock>(false) == null, code + " help cannot accumulate elapsed time");
                        if (order == 1) { f.AssertReadingSeparation(); f.Capture(code + "-help-" + page + "-save-failed"); }
                        f.Click("Focus.IconAction");
                    }
                    Assert(f.Text("Guided.PracticeHint") != null && StateCodec.Encode(f.Service.Snapshot) == savedDraft,
                        code + " help returns to practice without domain progression or elapsed growth");
                    f.Store.Fail = false;
                    f.Click("Guided.Previous"); f.AssertSlide("Guided.Cue.2", FocusGuidedPracticeContent.Guidance(code, chosen)[2]);
                    Assert(f.Ui.GetComponentInChildren<FocusPracticeClock>(false) == null, code + " previous reviews instruction with clock paused");
                    f.Click("Focus.IconAction");
                    Assert(f.Session(id).FocusStepRecords[0].ElapsedSeconds == expectedElapsed && f.Text("Guided.PracticeHint") != null,
                        code + " returning from instructions preserves elapsed practice time");
                    f.Click("Guided.ReflectionToggle"); f.AssertSlide("Guided.ReflectionPrompt", FocusGuidedPracticeContent.ReflectionPrompt(code));
                    f.AssertIcon(anchor);
                    string reflection = "";
                    if (code == "P02")
                    {
                        var input = f.Ui.GetComponentsInChildren<InputField>(false).Single(i => i.name == "Guided.ReflectionInput");
                        reflection = "synthetic bird sound"; input.text = reflection; input.onEndEdit.Invoke(reflection);
                        f.RevealRect((RectTransform)input.transform);
                        f.AssertFullyVisible((RectTransform)input.transform, "P02 optional writing area remains reachable through local answer scrolling");
                        f.AssertIcon(anchor);
                    }
                    else if (order % 2 == 1)
                    { reflection = definition.Steps[1].Options.Last(); f.Click("Guided.Reflection." + reflection); }
                    Assert(f.Session(id).FocusStepRecords[1].Value == reflection, code + " optional reflection persists independently of practice choice");
                    f.Capture(code + "-reflection");
                    f.Click("Guided.Previous");
                    Assert(f.Text("Guided.PracticeHint") != null, code + " reflection previous returns to practice without completing");
                    if (order == 1)
                    {
                        expectedElapsed++; f.Ui.GetComponentInChildren<FocusPracticeClock>(false).ElapsedSeconds = expectedElapsed;
                        string beforeClose = StateCodec.Encode(f.Service.Snapshot);
                        f.Store.Fail = true; f.Click("Focus.Close"); f.Store.Fail = false;
                        Assert(Screen(f.Ui) == "focus-practice" && StateCodec.Encode(f.Service.Snapshot) == beforeClose,
                            "Failed exit preserves current page and complete durable draft");
                        f.AssertReadingSeparation();
                    }
                    f.Click("Focus.Close");
                    Assert(Screen(f.Ui) != "focus-practice" && f.Session(id).Status == ParticipationState.InProgress,
                        code + " actual exit leaves practice unfinished");
                    f.Ui.SelectFocusSession(id); f.Layout(); f.AssertSlide("Guided.PracticeHint", FocusGuidedPracticeContent.PracticeHint(code));
                    Assert(f.Session(id).FocusStepRecords[0].Value == chosen && f.Session(id).FocusStepRecords[1].Value == reflection
                        && f.Session(id).FocusStepRecords[0].ElapsedSeconds == expectedElapsed, code + " reentry preserves chosen branch, optional response and elapsed time");
                    var finishClock = f.Ui.GetComponentInChildren<FocusPracticeClock>(false);
                    if (!finishClock.Running) f.Click("Focus.TimerToggle");
                    Assert(finishClock.Running, code + " optional timer can resume after reentry");
                    if (order % 2 == 0) { f.Click("Guided.ReflectionToggle"); Assert(!finishClock.Running, code + " optional reflection pauses practice timer"); }
                    int stage = f.Service.Snapshot.PlantAppearance.Recipe.GrowthStep; string beforeFinish = StateCodec.Encode(f.Service.Snapshot);
                    f.Store.Fail = true; f.Click("Focus.IconAction"); f.Store.Fail = false;
                    Assert(Screen(f.Ui) == "focus-practice" && StateCodec.Encode(f.Service.Snapshot) == beforeFinish,
                        code + " failed finish keeps current practice/reflection page and all durable state");
                    f.AssertReadingSeparation(); f.AssertIcon(anchor);
                    if (order == 1) f.Capture(code + "-finish-save-failed");
                    var finish = f.Button("Focus.IconAction").onClick;
                    f.Click("Focus.IconAction");
                    Assert(!finishClock.Running, code + " successful finish pauses old clock before growth presentation");
                    Assert(Screen(f.Ui) == "focus-complete" && f.Service.Snapshot.PlantAppearance.Recipe.GrowthStep == stage + 1
                        && f.Service.CoinBalance == 137 + order * 50 && f.Service.GetPracticeGrowth(id) != null && f.Session(id).Status == ParticipationState.RewardCommitted,
                        code + " direct or optional-reflection finish commits exactly one growth and 50 coins");
                    string after = StateCodec.Encode(f.Service.Snapshot); finish.Invoke();
                    Assert(StateCodec.Encode(f.Service.Snapshot) == after && Screen(f.Ui) == "focus-complete",
                        code + " stale finish cannot replay growth or replace completion");
                    Assert(f.Button("Focus.SkipReflection", false) == null && f.Button("Focus.ReflectionNext", false) == null,
                        code + " old final mood/note pages remain removed");
                    f.Capture(code + "-complete");
                }
            }
        }
        static void VersionTwoPage()
        {
            using (var f = new Fixture(390, 844))
            {
                Assert(f.Service.SelectMbctProgram("8w") && f.Service.BeginFocusSession("synthetic-v2-ui", "P01", 1, program: "8w"), "Create synthetic old occurrence");
                var old = f.Service.Snapshot; var session = old.Sessions.Single(); var def = FocusMissionCatalog.Find("P01", 2);
                session.FocusVersion = 2; session.MissionTitleSnapshot = def.Title; session.MissionDescriptionSnapshot = def.Overview;
                session.FocusStepRecords = def.Steps.Select((s, i) => new FocusStepRecord { Step = i, Key = s.Key, Title = s.Title, Prompt = s.Body }).ToList();
                var store = new Store(); store.Save(old);
                using (var restored = new Fixture(390, 844, store))
                {
                    restored.Ui.SelectFocusSession(session.SessionId); restored.Layout();
                    Assert(restored.Text("Guided.Cue.0") == null && restored.Session(session.SessionId).FocusVersion == 2,
                        "Version 2 uses its original overview without guided UI reinterpretation");
                    restored.Click("Focus.Action");
                    Assert(restored.Session(session.SessionId).InstructionStep == 0 && restored.Text("Guided.Cue.0") == null,
                        "Version 2 remains the original step runner after start");
                    restored.Capture("v2-original-practice");
                }
            }
        }
        static void ActiveVersionThreeResume()
        {
            Store store;
            using (var f = new Fixture(360, 640))
            {
                store = f.Store;
                Assert(f.Service.SelectMbctProgram("8w") && f.Service.BeginFocusSession("existing-active-v3", "P01", 1, program: "8w"), "Seed original v3 session");
                string answer = FocusMissionCatalog.Find("P01").Steps[1].Options.Last();
                Assert(f.Service.StartGuidedFocusSession("existing-active-v3", "배")
                    && f.Service.SaveGuidedFocusDraft("existing-active-v3", "배", answer, 47), "Persist original active v3 draft");
            }
            using (var restored = new Fixture(360, 640, store))
            {
                string before = StateCodec.Encode(restored.Service.Snapshot);
                restored.Ui.SelectFocusSession("existing-active-v3"); restored.Layout();
                restored.AssertSlide("Guided.PracticeHint", FocusGuidedPracticeContent.PracticeHint("P01"));
                Assert(restored.Session("existing-active-v3").FocusVersion == 3 && restored.Session("existing-active-v3").FocusStepRecords[0].Value == "배"
                    && restored.Ui.GetComponentInChildren<FocusPracticeClock>(false).ElapsedSeconds == 47,
                    "Existing active v3 save resumes practice without forcing overview or changing version");
                Assert(StateCodec.Encode(restored.Service.Snapshot) == before, "Opening existing active draft does not write or reward");
                restored.Capture("v3-existing-active-resume");
            }
        }
        static void DebugOverview()
        {
            using (var f = new Fixture(390, 844))
            {
                string before = StateCodec.Encode(f.Service.Snapshot);
                f.Ui.StartMissionDebug("P07"); f.Layout();
                Assert(f.Ui.IsMissionDebugActive && !ReferenceEquals(f.Ui.Service, f.Service), "Debug uses a separate memory service");
                f.AssertSlide("Guided.Overview", FocusMissionCatalog.Find("P07").Overview);
                Assert(f.Ui.GetComponentInChildren<FocusPracticeClock>(false) == null, "Debug entry begins with overview and no timer");
                f.Click("Focus.IconAction"); f.AssertSlide("Guided.Preparation", FocusGuidedPracticeContent.Preparation("P07"));
                Assert(StateCodec.Encode(f.Service.Snapshot) == before, "Debug reading does not change original progress or coins");
                f.Ui.RestoreMissionDebug(); f.Layout();
                Assert(!f.Ui.IsMissionDebugActive && ReferenceEquals(f.Ui.Service, f.Service) && StateCodec.Encode(f.Service.Snapshot) == before,
                    "Debug restores original service and state");
            }
        }
        sealed class Store : IStateStore
        { string xml; public bool Fail; public GardenState Load() => xml == null ? null : StateCodec.Decode(xml);
          public void Save(GardenState value) { if (Fail) throw new IOException("Synthetic guided UI save failure"); xml = StateCodec.Encode(value); } }
        sealed class Clock : IClock
        { public DateTimeOffset Time = DateTimeOffset.Parse("2026-11-09T03:00:00Z"); public DateTimeOffset UtcNow => Time; public TimeZoneInfo TimeZone => TimeZoneInfo.Utc; }
        sealed class Fixture : IDisposable
        {
            public readonly Store Store; public readonly Clock Clock = new Clock();
            public readonly GardenStateService Service; public readonly WeekOneQuestDemo Ui;
            readonly GameObject root; readonly Scene scene; readonly Camera camera; readonly RenderTexture target;
            readonly EventSystem events; readonly GraphicRaycaster raycaster; readonly int width, height;
            public Fixture(int width, int height, Store store = null)
            {
                this.width = width; this.height = height; Store = store ?? new Store();
                Service = new GardenStateService(Store, Clock, new DemoBalanceConfig { VisitorChancePercent = 0 }, new LegacySnapshot { Nutrient = 137 });
                Assert(Service.Open(), Service.LastError);
                scene = EditorSceneManager.NewPreviewScene();
                root = new GameObject("Synthetic guided UI", typeof(RectTransform), typeof(Canvas), typeof(GraphicRaycaster));
                root.SetActive(false); SceneManager.MoveGameObjectToScene(root, scene);
                var rect = (RectTransform)root.transform; rect.sizeDelta = new Vector2(width, height);
                var canvas = root.GetComponent<Canvas>(); canvas.renderMode = RenderMode.WorldSpace;
                var cameraObject = new GameObject("Synthetic guided capture camera", typeof(Camera)); SceneManager.MoveGameObjectToScene(cameraObject, scene);
                camera = cameraObject.GetComponent<Camera>(); camera.enabled = false; camera.scene = scene;
                camera.orthographic = true; camera.orthographicSize = height / 2f; camera.nearClipPlane = .1f; camera.farClipPlane = 100;
                camera.transform.position = new Vector3(0, 0, -10); camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Color.white;
                target = new RenderTexture(width, height, 24); camera.targetTexture = target; camera.aspect = width / (float)height;
                canvas.worldCamera = camera; raycaster = root.GetComponent<GraphicRaycaster>();
                var eventObject = new GameObject("Synthetic pointer", typeof(EventSystem)); eventObject.transform.SetParent(root.transform, false); events = eventObject.GetComponent<EventSystem>(); events.enabled = false;
                var panel = GardenUi.Box(root.transform, "Synthetic activity panel", 0, 0, 1, 1);
                Ui = panel.AddComponent<WeekOneQuestDemo>(); Ui.Initialize(Service, true);
                root.SetActive(true); Layout();
                Assert(ReferenceEquals(Ui.Service, Service), "Actual page presenter keeps injected synthetic service");
            }
            public ActivitySession Session(string id) => Service.Snapshot.Sessions.Single(s => s.SessionId == id);
            public Text Text(string name) => Ui.GetComponentsInChildren<Text>(false).FirstOrDefault(t => t.name == name);
            public Button Button(string name, bool required = true)
            {
                var result = Ui.GetComponentsInChildren<Button>(false).FirstOrDefault(b => b.name == name && b.gameObject.activeInHierarchy);
                if (required) Assert(result != null && result.IsInteractable(), "Visible enabled button missing: " + name); return result;
            }
            public void Layout()
            {
                for (int i = 0; i < 3; i++)
                {
                    Canvas.ForceUpdateCanvases();
                    foreach (var fit in root.GetComponentsInChildren<FigmaMissionCanvasFit>(false)) fit.Fit();
                    foreach (var page in root.GetComponentsInChildren<FocusMobilePageLayout>(false)) page.Refresh();
                    foreach (var slide in root.GetComponentsInChildren<FocusGuidedSlideLayout>(false)) slide.Refresh();
                    Canvas.ForceUpdateCanvases();
                }
                // Graphic depths/raycast ordering are populated by a real render.
                // Null graphics cannot establish that evidence; capture runs use an owned offscreen GPU target.
                if (capture) camera.Render();
            }
            public void Click(string name)
            {
                var button = Button(name); FocusKeyboardNavigation.Reveal(button); Layout();
                var rect = (RectTransform)button.transform; var point = camera.WorldToScreenPoint(rect.TransformPoint(rect.rect.center));
                Assert(point.x > 0 && point.x < width && point.y > 0 && point.y < height, "Button center is within visible capture: " + name + " " + point);
                var data = new PointerEventData(events) { position = point, button = PointerEventData.InputButton.Left };
                var hits = new List<RaycastResult>(); raycaster.Raycast(data, hits); report.raycasts++;
                Assert(hits.Count > 0 && (hits[0].gameObject == button.gameObject || hits[0].gameObject.transform.IsChildOf(button.transform)),
                    "Actual visible button is first pointer target: " + name + "; hit=" + (hits.Count == 0 ? "none" : hits[0].gameObject.name)
                    + "; graphicDepth=" + button.targetGraphic.depth + "; culled=" + button.targetGraphic.canvasRenderer.cull);
                ExecuteEvents.Execute(button.gameObject, data, ExecuteEvents.pointerDownHandler);
                ExecuteEvents.Execute(button.gameObject, data, ExecuteEvents.pointerUpHandler);
                ExecuteEvents.Execute(button.gameObject, data, ExecuteEvents.pointerClickHandler);
                report.clicks++; Layout();
            }
            public void AssertSlide(string name, string expected)
            {
                Layout(); var text = Text(name);
                Assert(Screen(Ui) == "focus-practice" && text != null && text.text == expected, "Expected single slide " + name);
                var names = new[] { "Guided.Overview", "Guided.Preparation", "Guided.ChoiceTitle", "Guided.Cue.0", "Guided.Cue.1", "Guided.Cue.2", "Guided.PracticeHint", "Guided.ReflectionPrompt", "Guided.Help" };
                Assert(Ui.GetComponentsInChildren<Text>(false).Count(t => names.Contains(t.name)) == 1, "Exactly one piece of guidance is visible on " + name);
                AssertFullyVisible(text.rectTransform, name);
                Assert(text.preferredHeight <= text.rectTransform.rect.height + .5f, name + " fits its complete wrapped body");
                var body = ScreenRect(text.rectTransform); var icon = ScreenRect((RectTransform)Button("Focus.IconAction").transform);
                Assert(body.yMin >= icon.yMax - .5f, name + " does not overlap the fixed illustration");
                Assert(Button("Focus.Action", false) == null, "Guided progress uses the central icon rather than an old footer action");
            }
            public Rect AssertIcon(Rect? expected = null)
            {
                Layout(); var rect = (RectTransform)Button("Focus.IconAction").transform;
                AssertFullyVisible(rect, "Fixed central icon");
                Assert(Vector2.Distance(rect.pivot, new Vector2(.5f, .5f)) < .001f && Vector2.Distance(rect.rect.size, new Vector2(180, 180)) < .01f,
                    "Central icon has 180 design-point size and centered pivot");
                var bounds = ScreenRect(rect);
                Assert(Mathf.Abs(bounds.center.x - width * .5f) < .75f && Mathf.Abs(bounds.center.y - height * .52f) < .75f,
                    "Central icon remains at horizontal center and 48 percent from top: " + bounds);
                Assert(bounds.width >= 44 && bounds.height >= 44, "Icon target is at least 44 pixels");
                if (expected.HasValue) Assert(Vector2.Distance(bounds.center, expected.Value.center) < .75f
                    && Vector2.Distance(bounds.size, expected.Value.size) < .75f, "All slides retain the same central icon rectangle");
                return bounds;
            }
            public void AssertCenteredPress()
            {
                var button = Button("Focus.IconAction"); var rect = (RectTransform)button.transform;
                var before = ScreenRect(rect); Vector3 normal = rect.localScale;
                bool reduced = FigmaMissionUi.ReducedMotion; FigmaMissionUi.ReducedMotion = false;
                var data = new PointerEventData(events) { position = before.center, button = PointerEventData.InputButton.Left };
                try
                {
                    ExecuteEvents.Execute(button.gameObject, data, ExecuteEvents.pointerDownHandler);
                    var pressed = ScreenRect(rect);
                    Assert(Vector2.Distance(pressed.center, before.center) < .01f && Mathf.Abs(pressed.width / before.width - .97f) < .001f,
                        "Press contracts symmetrically around illustration center");
                    ExecuteEvents.Execute(button.gameObject, data, ExecuteEvents.pointerDownHandler);
                    Assert(Vector3.Distance(rect.localScale, normal * .97f) < .001f, "Repeated pointer down does not compound scale");
                    ExecuteEvents.Execute(button.gameObject, data, ExecuteEvents.pointerUpHandler);
                    Assert(Vector3.Distance(rect.localScale, normal) < .001f && Vector2.Distance(ScreenRect(rect).center, before.center) < .01f,
                        "Release restores position and scale without navigation");
                    ExecuteEvents.Execute(button.gameObject, data, ExecuteEvents.pointerDownHandler);
                    ExecuteEvents.Execute(button.gameObject, data, ExecuteEvents.pointerExitHandler);
                    Assert(Vector3.Distance(rect.localScale, normal) < .001f, "Pointer exit restores central scale");
                }
                finally { FigmaMissionUi.ReducedMotion = reduced; }
            }
            public void RevealRect(RectTransform rect)
            {
                foreach (var scroll in rect.GetComponentsInParent<ScrollRect>(false).Where(s => s.enabled && s.content != null && s.viewport != null))
                {
                    var center = scroll.content.InverseTransformPoint(rect.TransformPoint(rect.rect.center));
                    scroll.content.anchoredPosition = new Vector2(0, Mathf.Clamp(-center.y - scroll.viewport.rect.height / 2,
                        0, Mathf.Max(0, scroll.content.rect.height - scroll.viewport.rect.height)));
                }
                Layout();
            }
            Rect ScreenRect(RectTransform rect)
            {
                var points = new Vector3[4]; rect.GetWorldCorners(points); var a = camera.WorldToScreenPoint(points[0]); var b = camera.WorldToScreenPoint(points[2]);
                return Rect.MinMaxRect(a.x, a.y, b.x, b.y);
            }
            public void AssertFullyVisible(RectTransform rect, string label)
            {
                var bounds = ScreenRect(rect); const float tolerance = .5f;
                Assert(bounds.xMin >= -tolerance && bounds.xMax <= width + tolerance && bounds.yMin >= -tolerance && bounds.yMax <= height + tolerance, label + " in output viewport");
                foreach (var mask in rect.GetComponentsInParent<RectMask2D>(false))
                {
                    var visible = ScreenRect(mask.rectTransform);
                    Assert(bounds.xMin >= visible.xMin - tolerance && bounds.xMax <= visible.xMax + tolerance
                        && bounds.yMin >= visible.yMin - tolerance && bounds.yMax <= visible.yMax + tolerance, label + " inside scrolling mask");
                }
            }
            public void RevealAndRead(Text text)
            {
                Assert(text != null, "Required guidance text exists"); RevealRect(text.rectTransform);
                AssertFullyVisible(text.rectTransform, text.name);
                Assert(text.preferredHeight <= text.rectTransform.rect.height + .5f, text.name + " reserves its complete wrapped text height");
            }
            public void AssertReadingSeparation()
            {
                Layout(); var notice = Text("Focus.SaveNotice");
                Assert(notice != null && !string.IsNullOrEmpty(notice.text), "Storage failure notice is visible");
                var viewport = Ui.GetComponentsInChildren<RectTransform>(false).Single(r => r.name == "Guided.AnswersViewport");
                var navigation = Ui.GetComponentsInChildren<RectTransform>(false).Single(r => r.name == "Guided.Navigation");
                var body = ScreenRect(viewport); var bottom = ScreenRect(navigation); var error = ScreenRect(notice.rectTransform);
                Assert(body.yMin >= error.yMax - .75f && error.yMin >= bottom.yMax - .75f,
                    "Local answers, failure notice and navigation have separate visible regions");
                AssertFullyVisible(notice.rectTransform, "Storage failure notice"); AssertIcon();
            }
            public void Capture(string name)
            {
                if (!capture) return; Layout(); camera.Render();
                var prior = RenderTexture.active; var image = new Texture2D(width, height, TextureFormat.RGB24, false);
                try
                {
                    RenderTexture.active = target; image.ReadPixels(new Rect(0, 0, width, height), 0, 0); image.Apply();
                    string filename = name + "-" + width + "x" + height + ".png";
                    File.WriteAllBytes(Path.Combine(output, filename), image.EncodeToPNG()); report.captures.Add(filename);
                }
                finally { RenderTexture.active = prior; UnityEngine.Object.DestroyImmediate(image); }
            }
            public void Dispose()
            {
                camera.targetTexture = null; target.Release(); UnityEngine.Object.DestroyImmediate(target);
                UnityEngine.Object.DestroyImmediate(root); Service.Dispose(); EditorSceneManager.ClosePreviewScene(scene);
            }
        }
    }
}
