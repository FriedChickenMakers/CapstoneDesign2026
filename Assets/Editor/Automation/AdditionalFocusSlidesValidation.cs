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
    // Explicit synthetic GPU batch only. Never opens user preferences, saves or devices.
    public static class AdditionalFocusSlidesValidation
    {
        [Serializable] sealed class Check { public string name, status, detail; }
        [Serializable] sealed class Report
        {
            public string utc, environment, unityVersion;
            public int passed, failed, assertions, clicks, raycasts;
            public List<Check> checks = new List<Check>();
            public List<string> captures = new List<string>(), errors = new List<string>(), visualMeasurements = new List<string>();
        }
        static Report report;
        static string output;
        static bool capture = true;
        public static void RunBatch()
        {
            var args = Environment.GetCommandLineArgs(); int index = Array.IndexOf(args, "-additionalSlidesReport");
            if (!Application.isBatchMode || Application.companyName != "CapstoneSyntheticValidation"
                || Application.productName != "MemoPopup-v07" || index < 0)
                throw new InvalidOperationException("Additional slide checks require the isolated synthetic batch project.");
            string path = args[index + 1]; output = Path.GetDirectoryName(path); Directory.CreateDirectory(output);
            report = new Report { utc = DateTimeOffset.UtcNow.ToString("O"), unityVersion = Application.unityVersion,
                environment = "Hidden synthetic Unity GPU rendering; injected memory state; real UI raycasts and pointer events" };
            Application.logMessageReceived += Observe;
            try
            {
                foreach (var size in new[] { new Vector2Int(320, 568), new Vector2Int(360, 640), new Vector2Int(390, 844) })
                foreach (var mission in FocusMissionCatalog.Additional.Where(m => m.Code != "M01"))
                    Run(mission.Code + " complete illustrated flow " + size.x + "x" + size.y,
                        () => Flow(mission.Code, size.x, size.y));
                Run("All M02-M17 final save failures and retries preserve exactly one reward", FinalSaveFailures);
                Run("Additional skip modal blocks stale actions and retries without quota", SkipFailures);
                Run("Timer help, exit and restored presenter retain elapsed practice", TimerRestore);
                Run("Long personal text remains complete through measured review pages", LongReview);
                Run("Plan and drawing drafts survive save retry and presenter restart", DraftRestore);
                Run("M07 and M13 explicit choices retain actual practice markers", ChoiceMarkers);
                Run("Saved catalog versions retain original records and rendering contract", SavedVersions);
                Run("M01 illustration remains the existing single-step contract", M01Smoke);
            }
            finally
            {
                Application.logMessageReceived -= Observe;
                if (report.errors.Count > 0) { report.failed++; report.checks.Add(new Check { name = "Unity errors", status = "fail", detail = string.Join("\n", report.errors) }); }
                File.WriteAllText(path, JsonUtility.ToJson(report, true));
            }
            Debug.Log("ADDITIONAL_SLIDES " + (report.failed == 0 ? "PASS" : "FAIL") + " assertions=" + report.assertions);
            EditorApplication.Exit(report.failed == 0 ? 0 : 1);
        }
        static void Observe(string text, string stack, LogType type)
        { if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) report.errors.Add(type + ": " + text + "\n" + stack); }
        static void Run(string name, Action action)
        { try { action(); report.passed++; report.checks.Add(new Check { name = name, status = "pass" }); }
          catch (Exception ex) { report.failed++; report.checks.Add(new Check { name = name, status = "fail", detail = ex.ToString() }); } }
        static void Assert(bool value, string message) { report.assertions++; if (!value) throw new InvalidOperationException(message); }
        static object Field(WeekOneQuestDemo ui, string name) => typeof(WeekOneQuestDemo).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(ui);
        static string Screen(WeekOneQuestDemo ui) => (string)Field(ui, "screen");
        static string State(Fixture f) => StateCodec.Encode(f.Service.Snapshot);
        static string Start(Fixture f, string code)
        {
            string id = "synthetic-all-m-" + code;
            Assert(f.Service.SelectMbctProgram("8w"), f.Service.LastError);
            Assert(f.Service.BeginFocusSession(id, code), f.Service.LastError);
            f.Ui.SelectFocusSession(id); f.Layout(); return id;
        }
        static void Flow(string code, int width, int height)
        {
            using (var f = new Fixture(width, height))
            {
                string id = Start(f, code); var definition = FocusMissionCatalog.FindForSession(f.Session(id));
                string[] original = f.Session(id).FocusStepRecords.Select(r => r.Key + "|" + r.Title + "|" + r.Prompt).ToArray();
                f.AssertPage("overview"); f.Capture(code + "-overview"); f.CancelledGesture();
                var old = f.Button("Focus.IconAction").onClick;
                string before = State(f); f.Ui.gameObject.SetActive(false); old.Invoke(); f.Ui.gameObject.SetActive(true); f.Layout();
                Assert(State(f) == before, code + " inactive overview callback cannot start or reward");
                f.Click("Focus.IconAction"); old.Invoke(); f.Layout();
                Assert(f.Session(id).Status == ParticipationState.InProgress && f.Session(id).InstructionStep == 0,
                    code + " overview starts only once and stale action cannot consume the first real step");
                int limit = 0;
                while (f.Session(id).Status != ParticipationState.RewardCommitted)
                {
                    Assert(++limit <= 16, code + " finite original steps plus optional plan pages");
                    if (Screen(f.Ui) == "focus-photo") { f.Click("Photo.Skip"); continue; }
                    int step = f.Session(id).InstructionStep; var instruction = definition.Steps[step];
                    f.AssertPage(instruction.Key); f.Capture(code + "-" + instruction.Key + "-" + limit);
                    Assert(f.Service.GetPracticeGrowth(id) == null && f.Service.CoinBalance == 137,
                        code + " unfinished guidance awards no growth or coins");
                    var artBefore = f.Pixels(); var bounds = f.ArtBounds();
                    f.Click("Basic.HelpToggle"); f.AssertPage("guide-help");
                    Assert(f.Text("Basic.Help").text.Split('\n').Length <= 3, code + " help is one concise page");
                    Assert(f.Session(id).InstructionStep == step, code + " help never confirms a real step");
                    if (limit == 1) f.Capture(code + "-help");
                    f.Click("Focus.IconAction"); f.AssertPage(instruction.Key);
                    Assert(f.ChangedPixels(artBefore, f.Pixels(), bounds) == 0, code + " help restores the exact current drawing without residual effects");
                    // Every simple first step can return to the overview without losing its original start.
                    if (limit == 1)
                    {
                        string started = f.Session(id).StartedUtc;
                        f.Click("Basic.Previous"); f.AssertPage("overview"); f.Click("Focus.IconAction");
                        Assert(f.Session(id).StartedUtc == started && f.Session(id).InstructionStep == step,
                            code + " previous and resume do not restart or advance the domain session");
                    }
                    Fill(f, id, instruction);
                    if (f.Session(id).InstructionStep != step || Screen(f.Ui) != "focus-practice") continue;
                    var final = f.Button("Focus.IconAction").onClick;
                    f.Click("Focus.IconAction"); string afterAction = State(f); final.Invoke(); f.Layout();
                    Assert(State(f) == afterAction,
                        code + " duplicate old action cannot skip the following instruction");
                }
                AssertCompleted(f, id);
                Assert(f.Session(id).FocusStepRecords.Select(r => r.Key + "|" + r.Title + "|" + r.Prompt).SequenceEqual(original),
                    code + " presentation preserves original saved step keys and snapshots");
                f.Capture(code + "-complete");
            }
        }
        static void Fill(Fixture f, string id, FocusMissionStep instruction)
        {
            if (instruction.Kind == "plan")
            {
                if (f.Button("Focus.PlanActivity.0", false) != null) f.Click("Focus.PlanActivity.0");
                if (f.Button("Focus.PlanPeriod.0", false) != null) f.Click("Focus.PlanPeriod.0");
                var note = f.Ui.GetComponentsInChildren<InputField>(false).FirstOrDefault(i => i.name == "Focus.PlanNote");
                if (note != null) { note.text = "합성 계획 메모"; note.onEndEdit.Invoke(note.text); f.Layout(); }
            }
            else if (instruction.Kind == "draw")
            {
                Assert(!f.Button("Focus.IconAction", false).interactable, "An empty drawing cannot complete its original required drawing step");
                f.DrawSyntheticStroke();
                Assert(f.Button("Focus.IconAction").interactable, "Actual stroke enables drawing confirmation");
            }
            else
            {
                var input = f.Ui.GetComponentsInChildren<InputField>(false).FirstOrDefault(i => i.name == "Focus.AnswerInput");
                if (input != null)
                {
                    if (instruction.RequiredAnswer) Assert(!f.Button("Focus.IconAction", false).interactable,
                        "An unanswered original required input cannot be bypassed by the icon");
                    input.text = "합성 확인 문장"; input.onEndEdit.Invoke(input.text); f.Layout();
                }
                else if (instruction.Options.Length > 0)
                    f.Click("Focus.Option." + (instruction.Key == "check" ? instruction.Options.Last() : instruction.Options[0]));
            }
            var clock = f.Ui.GetComponentInChildren<FocusPracticeClock>(false);
            if (clock != null && !clock.HasStarted)
            {
                Assert(!clock.Running, "Rendering a practice clock does not invent completed practice");
                f.Click("Focus.TimerToggle"); clock.ElapsedSeconds = 19; f.Clock.Time = f.Clock.Time.AddSeconds(19);
                Assert(clock.Flush(), "Actual elapsed practice persists through the existing service");
                f.Click("Basic.HelpToggle"); Assert(f.Ui.GetComponentInChildren<FocusPracticeClock>(false) == null, "Help has no advancing practice clock");
                f.Click("Focus.IconAction");
                clock = f.Ui.GetComponentInChildren<FocusPracticeClock>(false);
                Assert(clock != null && !clock.Running && clock.ElapsedSeconds == 19, "Help preserves elapsed time and returns paused");
            }
        }
        static void AssertCompleted(Fixture f, string id)
        {
            var receipt = f.Service.GetPracticeGrowth(id);
            Assert(f.Session(id).Status == ParticipationState.RewardCommitted && f.Service.CoinBalance == 167
                && f.Session(id).RewardAmount == 30 && Screen(f.Ui) == "focus-complete", "Final additional completion pays exactly 30 coins once");
            Assert(receipt != null && receipt.AfterStep == receipt.BeforeStep + 1
                && f.Service.Snapshot.PracticeGrowthRecords.Count(r => r.SessionId == id) == 1, "Final completion grows the identified plant exactly once");
        }
        static void FinalSaveFailures()
        {
            foreach (var mission in FocusMissionCatalog.Additional.Where(m => m.Code != "M01"))
            using (var f = new Fixture(320, 568))
            {
                string id = Start(f, mission.Code); f.Click("Focus.IconAction"); bool failed = false; int limit = 0;
                while (f.Session(id).Status != ParticipationState.RewardCommitted)
                {
                    Assert(++limit <= 18, "Bounded failure recovery flow");
                    if (Screen(f.Ui) == "focus-photo") { f.Click("Photo.Skip"); continue; }
                    int step = f.Session(id).InstructionStep; var instruction = mission.Steps[step];
                    // Optional choice is left empty so its final confirmation is under this test's control.
                    if (instruction.Kind != "choice") Fill(f, id, instruction);
                    if (step == mission.Steps.Length - 1 && !failed)
                    {
                        string before = State(f); f.Store.Fail = true; f.Click("Focus.IconAction"); f.Store.Fail = false;
                        Assert(State(f) == before && f.Service.CoinBalance == 137 && f.Service.GetPracticeGrowth(id) == null,
                            mission.Code + " failed final save leaves the same progress, coins and growth");
                        f.AssertPage(instruction.Key); f.AssertSaveNotice(); f.Capture(mission.Code + "-final-save-failed"); failed = true;
                    }
                    f.Click("Focus.IconAction");
                }
                Assert(failed, mission.Code + " actually injected failure at its final step"); AssertCompleted(f, id);
            }
        }
        static void SkipFailures()
        {
            foreach (string code in new[] { "M03", "M07", "M10", "M11", "M15", "M17" })
            using (var f = new Fixture(360, 640))
            {
                string id = Start(f, code); f.Click("Focus.IconAction");
                string before = State(f); var stale = f.Button("Focus.IconAction", false).onClick;
                f.Click("Focus.EndParticipation"); before = State(f); stale.Invoke(); f.Layout();
                Assert(State(f) == before && f.Button("Focus.AdjustmentConfirm") != null, code + " modal blocks stale background actions");
                var cancelledConfirm = f.Button("Focus.AdjustmentConfirm").onClick;
                var cancelledCancel = f.Button("Focus.AdjustmentCancel").onClick;
                f.Click("Focus.AdjustmentCancel"); Assert(State(f) == before, code + " cancelled skip preserves state");
                cancelledConfirm.Invoke(); f.Layout(); Assert(State(f) == before, code + " retained confirmation from a cancelled dialog is inert");
                f.Click("Focus.EndParticipation"); cancelledCancel.Invoke(); f.Layout();
                Assert(f.Button("Focus.AdjustmentConfirm") != null && State(f) == before, code + " old cancel cannot close a newer confirmation dialog");
                f.Store.Fail = true; f.Click("Focus.AdjustmentConfirm"); f.Store.Fail = false;
                stale.Invoke(); f.Layout(); Assert(State(f) == before, code + " failed skip remains retryable without background progress");
                var confirmed = f.Button("Focus.AdjustmentConfirm").onClick;
                f.Click("Focus.AdjustmentConfirm"); string ended = State(f); confirmed.Invoke(); f.Layout();
                Assert(State(f) == ended, code + " repeated confirmation is inert after the dialog is disposed");
                Assert(f.Session(id).Status == ParticipationState.Ended && f.Service.CoinBalance == 137
                    && f.Service.GetPracticeGrowth(id) == null && FocusMissionPolicy.AdditionalAvailability(f.Service.Snapshot) == null,
                    code + " confirmed skip is incomplete, unrewarded and leaves today's slot available");
            }
        }
        static void TimerRestore()
        {
            Store store;
            using (var f = new Fixture(320, 568))
            {
                string id = Start(f, "M17"); f.Click("Focus.IconAction"); f.Click("Focus.IconAction");
                f.Click("Focus.TimerToggle"); var clock = f.Ui.GetComponentInChildren<FocusPracticeClock>(); clock.ElapsedSeconds = 23;
                f.Store.Fail = true; f.Click("Focus.Close"); f.Store.Fail = false;
                Assert(Screen(f.Ui) == "focus-practice" && f.Service.GetPracticeGrowth(id) == null, "Failed elapsed save keeps the timed mission open");
                f.Click("Focus.Close"); Assert(Screen(f.Ui) == "home" && f.Session(id).FocusStepRecords[1].ElapsedSeconds == 23,
                    "Retry exits only after elapsed time is durable"); store = f.Store;
            }
            using (var f = new Fixture(390, 844, store))
            {
                f.Ui.SelectFocusSession("synthetic-all-m-M17"); f.Layout(); f.AssertPage("breath");
                var clock = f.Ui.GetComponentInChildren<FocusPracticeClock>();
                Assert(clock != null && clock.ElapsedSeconds == 23 && !clock.Running, "A fresh presenter restores elapsed time, current step and pause");
                f.Click("Focus.IconAction"); AssertCompleted(f, "synthetic-all-m-M17");
            }
        }
        static void SavedVersions()
        {
            foreach (int version in new[] { 2, 3, 4, 5 })
            foreach (var mission in FocusMissionCatalog.Additional.Where(m => m.Code != "M01"))
            {
                Store store;
                using (var seed = new Fixture(390, 844))
                {
                    string id = Start(seed, mission.Code); var saved = seed.Service.Snapshot;
                    var session = saved.Sessions.Single(s => s.SessionId == id); var def = FocusMissionCatalog.Find(mission.Code, version);
                    session.FocusVersion = version; session.MissionTitleSnapshot = def.Title; session.MissionDescriptionSnapshot = def.Overview;
                    session.FocusStepRecords = def.Steps.Select((s, i) => new FocusStepRecord { Step = i, Key = s.Key, Title = s.Title, Prompt = s.Body }).ToList();
                    store = new Store(); store.Save(saved);
                }
                using (var f = new Fixture(390, 844, store))
                {
                    string id = "synthetic-all-m-" + mission.Code, before = State(f);
                    f.Ui.SelectFocusSession(id); f.Layout();
                    Assert(State(f) == before, "Opening v" + version + " " + mission.Code + " preserves saved catalog and records");
                    bool modern = version >= FocusMissionCatalog.GuidedVersion;
                    Assert((f.Ui.GetComponentInChildren<AdditionalMissionIllustration>(false) != null) == modern,
                        "Only compatible v4/v5 use new additional guide; v2/v3 retain their original renderer");
                    f.Click(modern ? "Focus.IconAction" : "Focus.Action");
                    Assert(f.Session(id).FocusVersion == version && f.Session(id).InstructionStep == 0, "Guide never migrates or consumes a saved first step on start");
                    if (modern) f.AssertPage(FocusMissionCatalog.FindForSession(f.Session(id)).Steps[0].Key);
                }
            }
        }
        static void LongReview()
        {
            foreach (string code in new[] { "M07", "M10" })
            using (var f = new Fixture(320, 568))
            {
                string id = Start(f, code); f.Click("Focus.IconAction");
                if (code == "M10") { f.Click("Focus.IconAction"); f.Click("Focus.IconAction"); }
                var input = f.Ui.GetComponentsInChildren<InputField>(false).Single();
                string value = string.Concat(Enumerable.Repeat("합성 🙂 e\u0301 기록을 줄바꿈과 함께 끝까지 보존해요.\n", 10));
                input.text = value; input.onEndEdit.Invoke(input.text); f.Layout();
                value = f.Session(id).FocusStepRecords[0].Value;
                Assert(value.Length > 200, "Fixture really stores a long synthetic user record");
                f.Click("Focus.IconAction");
                var pages = new List<string>(); int limit = 0;
                while (Screen(f.Ui) == "focus-practice")
                {
                    Assert(++limit < 30, "Bounded full review pagination");
                    var text = f.Text("Basic.ReviewValue");
                    Assert(text != null, "Review exposes an actual slice of saved text");
                    f.AssertFullyVisible(text.rectTransform, "long record page");
                    Assert(text.preferredHeight <= text.rectTransform.rect.height + .75f, "Every long record page is fully readable");
                    for (int c = 0; c < text.text.Length; c++)
                    {
                        if (char.IsHighSurrogate(text.text[c])) Assert(c + 1 < text.text.Length && char.IsLowSurrogate(text.text[++c]), "Review page preserves complete surrogate pairs");
                        else Assert(!char.IsLowSurrogate(text.text[c]), "Review page never starts with an isolated surrogate");
                    }
                    Assert(text.text.Length == 0 || System.Globalization.CharUnicodeInfo.GetUnicodeCategory(text.text, 0) != System.Globalization.UnicodeCategory.NonSpacingMark,
                        "A review page does not split a combining mark from its base character");
                    pages.Add(text.text);
                    if (limit == 2)
                    {
                        string currentPage = text.text;
                        f.Click("Basic.HelpToggle"); f.Click("Focus.IconAction");
                        Assert(f.Text("Basic.ReviewValue").text == currentPage, "Help restores the exact long-review page");
                        f.Click("Basic.Previous"); Assert(f.Text("Basic.ReviewValue").text == pages[0], "Previous moves to the earlier local review page");
                        f.Click("Focus.IconAction"); Assert(f.Text("Basic.ReviewValue").text == currentPage, "Next returns to the same local review page without domain confirmation");
                    }
                    Assert(f.Session(id).FocusStepRecords[0].Value == value && f.Service.CoinBalance == 137,
                        "Paging never rewrites user text or awards before final confirmation");
                    f.Capture(code + "-long-review-" + limit);
                    f.Click("Focus.IconAction");
                }
                Assert(pages.Count > 1 && string.Concat(pages) == value, "Measured pages show every saved character exactly once, including newlines");
                AssertCompleted(f, id);
            }
        }
        static void DraftRestore()
        {
            foreach (string code in new[] { "M10", "M11" })
            {
                Store store; string persisted;
                using (var f = new Fixture(360, 640))
                {
                    string id = Start(f, code); f.Click("Focus.IconAction");
                    if (code == "M10")
                    {
                        f.Store.Fail = true; f.Click("Focus.PlanActivity.0"); f.Store.Fail = false;
                        Assert(string.IsNullOrEmpty(f.Session(id).FocusStepRecords[0].PlanActivity), "Failed plan draft is absent from durable state");
                        f.Click("Focus.IconAction"); // Retry persists the visible draft before changing the presentation page.
                        Assert(!string.IsNullOrEmpty(f.Session(id).FocusStepRecords[0].PlanActivity), "Plan retry preserves the chosen activity");
                        f.Click("Focus.PlanPeriod.0");
                    }
                    else
                    {
                        f.Store.Fail = true; f.DrawSyntheticStroke(); f.Store.Fail = false;
                        Assert(string.IsNullOrEmpty(f.Session(id).FocusStepRecords[0].Value), "Failed drawn draft does not invent a durable stroke");
                    }
                    f.Click("Focus.Close"); Assert(Screen(f.Ui) == "home", "Close retries and stores the draft before exiting");
                    persisted = f.Session(id).FocusStepRecords[0].Value;
                    Assert(!string.IsNullOrEmpty(persisted), "The synthetic draft is durable before presenter replacement"); store = f.Store;
                }
                using (var f = new Fixture(390, 844, store))
                {
                    string id = "synthetic-all-m-" + code; f.Ui.SelectFocusSession(id); f.Layout();
                    Assert(f.Session(id).FocusStepRecords[0].Value == persisted && f.Service.GetPracticeGrowth(id) == null && f.Service.CoinBalance == 137,
                        "Restart retains exact draft without completion or reward");
                    if (code == "M11")
                        Assert(f.Ui.GetComponentsInChildren<FocusDrawingSurface>(false).Single(d => !d.ReadOnly).Value == persisted,
                            "Restored drawing has its exact original stroke geometry");
                    else Assert(!string.IsNullOrEmpty(f.Session(id).FocusStepRecords[0].PlanActivity)
                        && !string.IsNullOrEmpty(f.Session(id).FocusStepRecords[0].PlanTimePeriod), "Restored plan keeps both independent structured choices");
                }
            }
        }
        static void ChoiceMarkers()
        {
            foreach (string code in new[] { "M07", "M13" })
            using (var f = new Fixture(360, 640))
            {
                string id = Start(f, code); f.Click("Focus.IconAction");
                Assert(string.IsNullOrEmpty(f.Session(id).FocusStepRecords[0].PracticeStartedUtc), "Rendering alone does not mark the actual choice practice started");
                f.Click("Focus.Option.잘 모르겠어요");
                Assert(!string.IsNullOrEmpty(f.Session(id).FocusStepRecords[0].PracticeStartedUtc)
                    && f.Session(id).FocusStepRecords[0].Value == "잘 모르겠어요",
                    code + " explicit uncertainty is a valid retained answer with an actual practice-start marker");
                f.Capture(code + "-uncertain-context");
                if (code == "M13")
                {
                    var art = f.Ui.GetComponentInChildren<AdditionalMissionIllustration>(false);
                    Assert(art.Stage == "word" && art.Choice == "잘 모르겠어요",
                        "The neutral recall choice continues into the optional word illustration");
                    f.Click("Basic.Previous"); f.Capture("M13-uncertain-recall");
                }
            }
        }
        static void M01Smoke()
        {
            using (var f = new Fixture(390, 844))
            {
                string id = Start(f, "M01"); Assert(f.Ui.GetComponentInChildren<M01LightIllustration>() != null, "M01 keeps its dedicated illustration");
                f.Click("Focus.IconAction"); f.Click("M01.Choice.Lamp"); f.Click("Focus.IconAction");
                AssertCompleted(f, id); Assert(f.Session(id).FocusStepRecords.Single().Key == "light", "M01 still has exactly one persisted light step");
            }
        }

        // Fixture helpers below are intentionally shared in behavior with the existing
        // basic-slide batch: actual GPU rendering and hit-testing, never direct service completion.
        sealed class Store : IStateStore
        { string xml; public bool Fail; public int FailAfterWrites = -1; public GardenState Load() => xml == null ? null : StateCodec.Decode(xml);
          public void Save(GardenState value) { if (Fail || FailAfterWrites == 0) throw new IOException("Synthetic guided UI save failure");
              if (FailAfterWrites > 0) FailAfterWrites--; xml = StateCodec.Encode(value); } }
        sealed class Clock : IClock
        { public DateTimeOffset Time = DateTimeOffset.Parse("2026-11-11T03:00:00Z"); public DateTimeOffset UtcNow => Time; public TimeZoneInfo TimeZone => TimeZoneInfo.Utc; }
        sealed class Fixture : IDisposable
        {
            public readonly Store Store; public readonly Clock Clock = new Clock();
            public readonly GardenStateService Service; public readonly WeekOneQuestDemo Ui;
            readonly GameObject root; readonly Scene scene; readonly Camera camera; readonly RenderTexture target;
            readonly EventSystem events; readonly GraphicRaycaster raycaster; readonly int width, height;
            readonly HashSet<Rect> stableIconAnchors = new HashSet<Rect>();
            Rect? additionalAnchor;
            public Fixture(int width, int height, Store store = null, DateTimeOffset? time = null)
            {
                this.width = width; this.height = height; Store = store ?? new Store();
                if (time.HasValue) Clock.Time = time.Value;
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
                Ui = panel.AddComponent<WeekOneQuestDemo>(); Ui.PreviewHealth = new AndroidPlatformSnapshot { inputMode = "MOCK", status = "NO_DATA" }; Ui.Initialize(Service, true);
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
            public void DrawSyntheticStroke()
            {
                var surface = Ui.GetComponentsInChildren<FocusDrawingSurface>(false).Single(s => !s.ReadOnly);
                RevealRect(surface.rectTransform); Layout();
                var point = camera.WorldToScreenPoint(surface.rectTransform.TransformPoint(surface.rectTransform.rect.center));
                var data = new PointerEventData(events) { position = point, button = PointerEventData.InputButton.Left,
                    pointerPressRaycast = new RaycastResult { module = raycaster } };
                var hits = new List<RaycastResult>(); raycaster.Raycast(data, hits); report.raycasts++;
                Assert(hits.Count > 0 && hits[0].gameObject.GetComponentInParent<FocusDrawingSurface>() == surface, "Drawing receives actual pointer hit");
                surface.OnPointerDown(data); data.position += new Vector2(24, 24); surface.OnDrag(data); surface.OnPointerUp(data); Layout();
                Assert(FocusDrawingData.Decode(surface.Value).Any(stroke => stroke.Count > 1), "Actual drawing gesture preserves multiple points");
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

            public Color32[] Pixels()
            {
                Layout(); camera.Render(); var prior = RenderTexture.active; var image = new Texture2D(width, height, TextureFormat.RGB24, false);
                try { RenderTexture.active = target; image.ReadPixels(new Rect(0, 0, width, height), 0, 0); image.Apply(); return image.GetPixels32(); }
                finally { RenderTexture.active = prior; UnityEngine.Object.DestroyImmediate(image); }
            }

            public int ChangedPixels(Color32[] before, Color32[] after, Rect area)
            {
                int count = 0;
                for (int y = Mathf.Max(0, Mathf.CeilToInt(area.yMin)); y < Mathf.Min(height, Mathf.FloorToInt(area.yMax)); y++)
                    for (int x = Mathf.Max(0, Mathf.CeilToInt(area.xMin)); x < Mathf.Min(width, Mathf.FloorToInt(area.xMax)); x++)
                    {
                        var a = before[y * width + x]; var b = after[y * width + x];
                        if (Math.Abs(a.r - b.r) > 8 || Math.Abs(a.g - b.g) > 8 || Math.Abs(a.b - b.b) > 8) count++;
                    }
                return count;
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

            public Rect ArtBounds() => ScreenRect(Ui.GetComponentInChildren<AdditionalMissionIllustration>(false).rectTransform);
            public void AssertPage(string stage)
            {
                Layout(); var art = Ui.GetComponentsInChildren<AdditionalMissionIllustration>(false);
                Assert(art.Length == 1 && !art[0].raycastTarget, "Exactly one non-intercepting additional illustration");
                string id = (string)Field(Ui, "sessionId");
                var currentMission = FocusMissionCatalog.FindForSession(Session(id));
                string artStage = stage == "plan" && currentMission.Steps[Session(id).InstructionStep].Kind == "plan"
                    ? "plan-" + Field(Ui, "additionalPlanPage") : stage;
                Assert(art[0].Code == currentMission.Code && art[0].Stage == artStage, "Artwork is configured for the actual current mission and stage: " + art[0].Stage + " expected " + artStage);
                string bodyName = stage == "overview" ? "Basic.Overview" : stage == "guide-help" ? "Basic.Help" : "Basic.Body";
                var body = Text(bodyName);
                Assert(body != null && !string.IsNullOrWhiteSpace(body.text) && body.fontSize == 22
                    && body.alignment == TextAnchor.UpperCenter, "One centered, readable22-point current action: " + stage);
                AssertFullyVisible(body.rectTransform, "current action");
                Assert(body.preferredHeight <= body.rectTransform.rect.height + .75f, "Current action wraps without truncation");
                var title = Text("Basic.Title");
                Assert((title != null) == (stage == "overview"), "Mission heading appears only on overview");
                if (title != null)
                {
                    Assert(title.fontSize == 30 && title.preferredHeight <= title.rectTransform.rect.height + .75f, "Overview title stays large and untruncated");
                    AssertFullyVisible(title.rectTransform, "overview heading");
                    Assert(ScreenRect(title.rectTransform).yMin >= ScreenRect(body.rectTransform).yMax - .75f, "Overview heading and purpose are separate");
                }
                Assert(!Ui.GetComponentsInChildren<Text>(false).Any(t => System.Text.RegularExpressions.Regex.IsMatch(t.text ?? "", @"\b[PM]\d{2}\b")), "Product guidance hides developer mission IDs");
                var icon = Button("Focus.IconAction", false);
                Assert(icon != null && Button("Focus.Action", false) == null, "Only the central icon advances the guide");
                var rect = (RectTransform)icon.transform; var bounds = ScreenRect(rect);
                if (Text("Focus.SaveNotice") == null)
                {
                    if (!additionalAnchor.HasValue) additionalAnchor = bounds;
                    else Assert(Vector2.Distance(additionalAnchor.Value.center, bounds.center) < .75f
                        && Vector2.Distance(additionalAnchor.Value.size, bounds.size) < .75f,
                        "Normal slides retain the same per-mission illustration center and size: " + stage + " original=" + additionalAnchor.Value + " current=" + bounds);
                }
                Assert(Vector2.Distance(rect.pivot, new Vector2(.5f, .5f)) < .001f
                    && Mathf.Abs(bounds.center.x - width / 2f) < .75f && Mathf.Abs(rect.rect.width - rect.rect.height) < .01f,
                    "Central illustration stays square, horizontally centered and centrally pivoted");
                AssertFullyVisible(rect, "central action"); AssertFullyVisible(art[0].rectTransform, "drawn illustration");
                Assert(ScreenRect(body.rectTransform).yMin >= bounds.yMax - .75f, "The current reading area does not overlap the central image");
                foreach (var name in new[] { "Focus.Close", "Basic.Previous", "Basic.HelpToggle", "Focus.EndParticipation", "Focus.TimerToggle", "Focus.TooLong" })
                {
                    var button = Button(name, false);
                    if (button != null) AssertFullyVisible((RectTransform)button.transform, "control " + name);
                }
                Assert(Button("Focus.Close").GetComponentInChildren<Text>().text == "나가기"
                    && Button("Focus.EndParticipation").GetComponentInChildren<Text>().text == "미션 넘기기", "Exit and incomplete skip remain separate");
                var activeButtons = Ui.GetComponentsInChildren<Button>(false).Where(b => b.gameObject.activeInHierarchy).ToArray();
                for (int i = 0; i < activeButtons.Length; i++)
                for (int j = i + 1; j < activeButtons.Length; j++)
                {
                    var a = ScreenRect((RectTransform)activeButtons[i].transform); var b = ScreenRect((RectTransform)activeButtons[j].transform);
                    Assert(Mathf.Min(a.xMax, b.xMax) - Mathf.Max(a.xMin, b.xMin) <= .75f
                        || Mathf.Min(a.yMax, b.yMax) - Mathf.Max(a.yMin, b.yMin) <= .75f,
                        "Separate touch targets: " + activeButtons[i].name + " / " + activeButtons[j].name);
                }
                Assert(!Ui.GetComponentsInChildren<FocusMobilePageLayout>(false).Any(p => !p.FixedContent), "The mission page itself does not require scrolling");
                Assert(!Ui.GetComponentsInChildren<ScrollRect>(false).Any(s => s.enabled && s.vertical && s.content != null && s.viewport != null
                    && s.content.rect.height > s.viewport.rect.height + .75f), "Normal guidance and answer controls fit their local viewport without scrolling");
                if (currentMission.Code == "M11" && stage == "review")
                {
                    var drawing = Ui.GetComponentsInChildren<FocusDrawingSurface>(false).Single(d => d.ReadOnly);
                    Assert(!art[0].enabled && !drawing.raycastTarget && drawing.Value == Session(id).FocusStepRecords[0].Value
                        && drawing.transform.IsChildOf(art[0].transform.parent), "Drawing review centrally shows the exact user's saved strokes, with no competing example or intercepting hit area");
                    Assert(Mathf.Abs(drawing.rectTransform.rect.width / drawing.rectTransform.rect.height - 326f / 140f) < .001f,
                        "Read-only review preserves the input drawing aspect ratio without stretching its strokes");
                }
                var shown = Pixels(); Color32[] hidden;
                var illustrationGroup = art[0].transform.parent.gameObject;
                try { illustrationGroup.SetActive(false); hidden = Pixels(); }
                finally { illustrationGroup.SetActive(true); Layout(); }
                int painted = ChangedPixels(hidden, shown, ArtBounds());
                report.visualMeasurements.Add(width + "x" + height + " " + stage + ": " + painted + " painted pixels");
                Assert(painted >= 256, "Current mission illustration paints a substantial visible GPU area");
            }
            public void AssertSaveNotice()
            {
                var notice = Text("Focus.SaveNotice");
                Assert(notice != null && !string.IsNullOrWhiteSpace(notice.text), "Failed save exposes retry guidance");
                AssertFullyVisible(notice.rectTransform, "save notice");
                Assert(notice.preferredHeight <= notice.rectTransform.rect.height + .75f, "Save notice text fits");
            }
            public void CancelledGesture()
            {
                var button = Button("Focus.IconAction"); var rect = (RectTransform)button.transform;
                var pixels = Pixels(); var area = ArtBounds(); var original = ScreenRect(rect); string state = State(this);
                var point = camera.WorldToScreenPoint(rect.TransformPoint(rect.rect.center));
                var data = new PointerEventData(events) { position = point, pressPosition = point, button = PointerEventData.InputButton.Left };
                var hits = new List<RaycastResult>(); raycaster.Raycast(data, hits); report.raycasts++;
                Assert(hits.Count > 0 && (hits[0].gameObject == button.gameObject || hits[0].gameObject.transform.IsChildOf(button.transform)), "Icon receives the actual gesture hit");
                ExecuteEvents.Execute(button.gameObject, data, ExecuteEvents.pointerDownHandler); Layout();
                var pressed = ScreenRect(rect);
                Assert(Vector2.Distance(pressed.center, original.center) < .01f && button.targetGraphic.color.a <= .13f, "Press stays centered without an opaque background box");
                data.position = new Vector2(width - 1, height - 1); data.dragging = true; data.eligibleForClick = false;
                ExecuteEvents.Execute(button.gameObject, data, ExecuteEvents.beginDragHandler);
                ExecuteEvents.Execute(button.gameObject, data, ExecuteEvents.dragHandler);
                ExecuteEvents.Execute(button.gameObject, data, ExecuteEvents.pointerExitHandler);
                ExecuteEvents.Execute(button.gameObject, data, ExecuteEvents.pointerUpHandler);
                ExecuteEvents.Execute(button.gameObject, data, ExecuteEvents.endDragHandler); Layout();
                Assert(State(this) == state && button.targetGraphic.color.a < .01f
                    && Vector2.Distance(ScreenRect(rect).center, original.center) < .01f
                    && Vector2.Distance(ScreenRect(rect).size, original.size) < .01f
                    && ChangedPixels(pixels, Pixels(), area) == 0,
                    "Cancelled drag restores exact artwork and transform without progress or rewards");
            }
        }
    }
}
