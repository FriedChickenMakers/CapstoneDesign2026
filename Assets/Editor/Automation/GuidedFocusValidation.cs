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
            public List<string> visualMeasurements = new List<string>();
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
                foreach (var size in new[] { new Vector2Int(320, 568), new Vector2Int(360, 640), new Vector2Int(390, 844) })
                    Run("P01 illustrated stages, focus choices and safe input " + size.x + "x" + size.y, () => P01VisualFlow(size.x, size.y));
                foreach (var size in new[] { new Vector2Int(320, 568), new Vector2Int(360, 640), new Vector2Int(390, 844) })
                    Run("P02 sound illustrations, reading and optional reflection " + size.x + "x" + size.y, () => P02VisualFlow(size.x, size.y));
                foreach (var size in new[] { new Vector2Int(320, 568), new Vector2Int(360, 640), new Vector2Int(390, 844) })
                    Run("P01-P07 slides, centered action and durable practice " + size.x + "x" + size.y, () => Flow(size.x, size.y));
                Run("Persisted version-2 page remains the previous runner", VersionTwoPage);
                Run("Original schema-1 guided v3 draft migrates and resumes practice", ActiveVersionThreeResume);
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
        static void P01VisualFlow(int width, int height)
        {
            bool reduced = FigmaMissionUi.ReducedMotion;
            try
            {
                FigmaMissionUi.ReducedMotion = true;
                using (var f = new Fixture(width, height))
                {
                    const string id = "synthetic-p01-illustration";
                    Assert(f.Service.SelectMbctProgram("8w") && f.Service.BeginFocusSession(id, "P01", 1, program: "8w"), f.Service.LastError);
                    f.Ui.SelectFocusSession(id); f.Layout();
                    f.AssertP01Illustration(P01BreathIllustration.Stage.Overview); Rect anchor = f.AssertIcon();
                    var overviewPixels = f.Pixels();
                    Rect overviewArea = f.Bounds(f.Ui.GetComponentInChildren<P01BreathIllustration>(false).rectTransform);
                    f.Capture("P01-visual-overview"); f.AssertIllustrationGestureCancellation("P01");
                    Assert(f.Text("Guided.Overview") != null && f.Session(id).Status == ParticipationState.Selected,
                        "Pointer cancellation cannot advance P01 or begin its practice.");
                    f.Click("Focus.IconAction"); f.AssertP01Illustration(P01BreathIllustration.Stage.Preparation); f.AssertIcon(anchor);
                    var preparationPixels = f.Pixels();
                    Rect preparationArea = f.Bounds(f.Ui.GetComponentInChildren<P01BreathIllustration>(false).rectTransform);
                    f.Capture("P01-visual-preparation");
                    f.Click("Focus.IconAction"); f.AssertP01Illustration(P01BreathIllustration.Stage.Choice); f.AssertIcon(anchor);
                    f.Capture("P01-visual-choice");
                    f.Click("Focus.IconAction"); f.AssertP01Illustration(P01BreathIllustration.Stage.Notice, "");
                    f.Click("Focus.IconAction"); f.Click("Focus.IconAction");
                    f.AssertP01Illustration(P01BreathIllustration.Stage.Return, "");
                    var unchosenReturn = f.Pixels(); f.Capture("P01-visual-return-without-choice");
                    Assert(string.IsNullOrEmpty(f.Session(id).FocusStepRecords[0].Value) && f.Session(id).Status == ParticipationState.Selected,
                        "Skipping the focus choice keeps it unset and does not start practice or choose a body location.");
                    f.Click("Guided.Previous"); f.Click("Guided.Previous"); f.Click("Guided.Previous");
                    f.AssertP01Illustration(P01BreathIllustration.Stage.Choice, "");
                    string beforeChoice = StateCodec.Encode(f.Service.Snapshot); int beforeWrites = f.Store.Saves;
                    f.Store.Fail = true; f.Click("Guided.Choice.코");
                    Assert(StateCodec.Encode(f.Service.Snapshot) == beforeChoice && f.Store.Saves == beforeWrites,
                        "P01 choice storage failure leaves durable state and reward history unchanged.");
                    f.AssertP01Illustration(P01BreathIllustration.Stage.Choice, "코"); f.AssertReadingSeparation();
                    f.Capture("P01-visual-choice-error"); f.Store.Fail = false;
                    var choices = new List<Color32[]>();
                    var backNavigationFailures = new List<string>();
                    foreach (string choice in new[] { "코", "가슴", "배" })
                    {
                        if (choices.Count > 0) f.Click("Guided.Previous");
                        f.Click("Guided.Choice." + choice); f.AssertP01Illustration(P01BreathIllustration.Stage.Notice, choice); f.AssertIcon(anchor);
                        Assert(f.Session(id).FocusStepRecords[0].Value == choice && f.Session(id).Status == ParticipationState.Selected,
                            "Selecting " + choice + " changes the real saved focus area without starting the timer.");
                        choices.Add(f.Pixels()); f.Capture("P01-visual-focus-" + choice);
                        string selectedState = StateCodec.Encode(f.Service.Snapshot); int selectedWrites = f.Store.Saves;
                        // Each reference is a newly built real page, reached through its normal buttons.
                        f.Click("Guided.Previous"); f.AssertP01Illustration(P01BreathIllustration.Stage.Choice, choice);
                        var selectedChoicePixels = f.Pixels();
                        f.Click("Focus.IconAction"); f.AssertP01Illustration(P01BreathIllustration.Stage.Notice, choice);
                        var noticePixels = f.Pixels();
                        f.Click("Focus.IconAction"); f.AssertP01Illustration(P01BreathIllustration.Stage.Observe, choice);
                        var observePixels = f.Pixels();
                        f.Click("Focus.IconAction"); f.AssertP01Illustration(P01BreathIllustration.Stage.Return, choice);
                        var previousStages = new[] { P01BreathIllustration.Stage.Observe, P01BreathIllustration.Stage.Notice, P01BreathIllustration.Stage.Choice };
                        var previousPictures = new[] { observePixels, noticePixels, selectedChoicePixels };
                        for (int previous = 0; previous < previousStages.Length; previous++)
                        {
                            f.Click("Guided.Previous"); f.AssertP01Illustration(previousStages[previous], choice);
                            Rect area = f.Bounds(f.Ui.GetComponentInChildren<P01BreathIllustration>(false).rectTransform);
                            int changed = f.ChangedPixels(previousPictures[previous], f.Pixels(), area, 0);
                            report.visualMeasurements.Add(width + "x" + height + " P01 back " + choice + " " + previousStages[previous] + ": " + changed + " changed pixels");
                            if (changed != 0) backNavigationFailures.Add(choice + " " + previousStages[previous] + "=" + changed);
                        }
                        // Capture both affected pages before the aggregate assertion, including in a failing RED run.
                        f.Click("Guided.Previous"); f.Capture("P01-visual-back-preparation-" + choice);
                        Assert(f.Ui.GetComponentInChildren<P01BreathIllustration>(false).CurrentStage == P01BreathIllustration.Stage.Preparation,
                            "Previous from choice reaches preparation without discarding the chosen focus.");
                        int preparationChanged = f.ChangedPixels(preparationPixels, f.Pixels(), preparationArea, 0);
                        f.Click("Guided.Previous"); f.Capture("P01-visual-back-overview-" + choice);
                        Assert(f.Ui.GetComponentInChildren<P01BreathIllustration>(false).CurrentStage == P01BreathIllustration.Stage.Overview,
                            "Previous from preparation reaches the original overview.");
                        int overviewChanged = f.ChangedPixels(overviewPixels, f.Pixels(), overviewArea, 0);
                        report.visualMeasurements.Add(width + "x" + height + " P01 back " + choice + " preparation=" + preparationChanged + ", overview=" + overviewChanged + " changed pixels");
                        if (preparationChanged != 0 || overviewChanged != 0)
                            backNavigationFailures.Add(choice + " preparation=" + preparationChanged + ", overview=" + overviewChanged);
                        Assert(StateCodec.Encode(f.Service.Snapshot) == selectedState && f.Store.Saves == selectedWrites,
                            "All previous-page navigation preserves the selected draft without saving, starting practice or rewarding.");
                        f.Click("Focus.IconAction"); f.Click("Focus.IconAction");
                        f.AssertP01Illustration(P01BreathIllustration.Stage.Choice, choice);
                        f.Click("Focus.IconAction"); f.AssertP01Illustration(P01BreathIllustration.Stage.Notice, choice);
                        Assert(f.ChangedPixels(noticePixels, f.Pixels(), f.Bounds(f.Ui.GetComponentInChildren<P01BreathIllustration>(false).rectTransform), 0) == 0
                            && StateCodec.Encode(f.Service.Snapshot) == selectedState && f.Store.Saves == selectedWrites,
                            "Advancing again restores the saved focus illustration exactly without changing its draft or rewards.");
                    }
                    Assert(backNavigationFailures.Count == 0, "P01 back navigation must match fresh stage pixels and unselected preparation/overview baselines: "
                        + string.Join("; ", backNavigationFailures));
                    var picture = f.Bounds((RectTransform)f.Button("Focus.IconAction").transform);
                    for (int a = 0; a < choices.Count; a++) for (int b = a + 1; b < choices.Count; b++)
                    {
                        int changed = f.ChangedPixels(choices[a], choices[b], picture, 8);
                        report.visualMeasurements.Add(width + "x" + height + " P01 focus pair " + a + "/" + b + ": " + changed + " changed illustration pixels");
                        Assert(changed >= 24, "Nose, chest and belly choices produce visibly distinct illustration pixels inside the same central target.");
                    }
                    f.Click("Focus.IconAction"); f.AssertP01Illustration(P01BreathIllustration.Stage.Observe, "배"); f.AssertIcon(anchor);
                    f.Capture("P01-visual-observe");
                    f.Click("Focus.IconAction"); f.AssertP01Illustration(P01BreathIllustration.Stage.Return, "배"); f.AssertIcon(anchor);
                    Assert(f.ChangedPixels(unchosenReturn, f.Pixels(), picture, 8) >= 24,
                        "Unchosen return guidance remains visually distinct from a chosen body's directed return cue.");
                    f.Capture("P01-visual-return");
                    string beforeStart = StateCodec.Encode(f.Service.Snapshot);
                    f.Store.Fail = true; f.Click("Focus.IconAction"); f.Store.Fail = false;
                    Assert(StateCodec.Encode(f.Service.Snapshot) == beforeStart && f.Session(id).Status == ParticipationState.Selected,
                        "Failed practice start retains the last illustration and original saved participation.");
                    f.AssertP01Illustration(P01BreathIllustration.Stage.Return, "배"); f.AssertReadingSeparation(); f.Capture("P01-visual-start-error");
                    f.Click("Focus.IconAction"); f.AssertP01Illustration(P01BreathIllustration.Stage.Practice, "배"); f.AssertIcon(anchor);
                    var practiceClock = f.Ui.GetComponentInChildren<FocusPracticeClock>(false);
                    Assert(practiceClock.Running, "P01 starts its existing timer only after the final cue.");
                    practiceClock.ElapsedSeconds = 23; Assert(practiceClock.Flush(), "Persist P01 elapsed time before help");
                    f.Capture("P01-visual-practice");
                    Assert(f.Button("Guided.HelpToggle").GetComponentInChildren<Text>().text == "도움말", "P01 retains the help action rather than relabeling it as a restart.");
                    string beforeHelp = StateCodec.Encode(f.Service.Snapshot);
                    f.Click("Guided.HelpToggle");
                    f.AssertP01Illustration(P01BreathIllustration.Stage.Help, "배", 0);
                    Assert(!practiceClock.Running && f.Ui.GetComponentInChildren<FocusPracticeClock>(false) == null
                        && !f.Text("Guided.Progress").text.Contains("1/3") && f.Text("Guided.Help").text.Split('\n').Length == 3,
                        "P01 single-page help shows its three key statements together, pauses the timer and does not advertise three help pages.");
                    f.Capture("P01-visual-help-single"); f.Click("Focus.IconAction");
                    f.AssertP01Illustration(P01BreathIllustration.Stage.Practice, "배");
                    Assert(StateCodec.Encode(f.Service.Snapshot) == beforeHelp
                        && f.Ui.GetComponentInChildren<FocusPracticeClock>(false).ElapsedSeconds == 23,
                        "One central click leaves P01 help and restores its original practice, focus and elapsed draft without reward.");
                    f.Click("Guided.ReflectionToggle"); f.AssertP01Illustration(P01BreathIllustration.Stage.Reflection, "배"); f.AssertIcon(anchor);
                    foreach (var option in f.Ui.GetComponentsInChildren<Button>(false).Where(b => b.name.StartsWith("Guided.Reflection.", StringComparison.Ordinal)).ToArray())
                    { f.RevealRect((RectTransform)option.transform); f.AssertFullyVisible((RectTransform)option.transform, "Every optional reflection choice remains reachable"); }
                    f.Capture("P01-visual-reflection");
                    string readState = StateCodec.Encode(f.Service.Snapshot); int readWrites = f.Store.Saves;
                    var staticPicture = f.Pixels(); var visual = f.Ui.GetComponentInChildren<P01BreathIllustration>(false);
                    visual.SetAllDirty(); f.Layout();
                    Assert(f.ChangedPixels(staticPicture, f.Pixels(), f.Bounds(visual.rectTransform), 0) == 0,
                        "Reduced-motion re-render retains an identical static P01 illustration.");
                    Assert(StateCodec.Encode(f.Service.Snapshot) == readState && f.Store.Saves == readWrites
                        && f.Service.CoinBalance == 137 && f.Service.GetPracticeGrowth(id) == null,
                        "Illustration stages and optional reading never complete the mission, grow a plant or pay a reward.");
                }
            }
            finally { FigmaMissionUi.ReducedMotion = reduced; }
        }
        static void P02VisualFlow(int width, int height)
        {
            bool reduced = FigmaMissionUi.ReducedMotion;
            try
            {
                FigmaMissionUi.ReducedMotion = true;
                using (var f = new Fixture(width, height))
                {
                    const string predecessor = "synthetic-p02-predecessor", id = "synthetic-p02-illustration";
                    Assert(f.Service.SelectMbctProgram("8w") && f.Service.BeginFocusSession(predecessor, "P01", 1, program: "8w")
                        && f.Service.StartGuidedFocusSession(predecessor, "")
                        && f.Service.CompleteGuidedFocusPractice(predecessor, "", "", 0)
                        && f.Service.CompletePracticeSession(predecessor), f.Service.LastError);
                    f.Clock.Time = f.Clock.Time.AddDays(1);
                    Assert(f.Service.RefreshCycle(Array.Empty<string>()) && f.Service.BeginFocusSession(id, "P02", 2, program: "8w"), f.Service.LastError);
                    f.Ui.SelectFocusSession(id); f.Layout();
                    string beforeReading = StateCodec.Encode(f.Service.Snapshot); int readingWrites = f.Store.Saves;
                    var stages = new[] { P02SoundIllustration.Stage.Overview, P02SoundIllustration.Stage.Preparation,
                        P02SoundIllustration.Stage.Notice, P02SoundIllustration.Stage.Observe, P02SoundIllustration.Stage.Return };
                    var pictures = new List<Color32[]>(); var areas = new List<Rect>();
                    Rect anchor = f.AssertIcon();
                    for (int index = 0; index < stages.Length; index++)
                    {
                        if (index > 0) f.Click("Focus.IconAction");
                        f.AssertP02Illustration(stages[index]); f.AssertIcon(anchor);
                        Assert(f.Text("Guided.ChoiceTitle") == null && f.Ui.GetComponentInChildren<FocusPracticeClock>(false) == null,
                            "P02 reading introduces neither a focus-choice page nor an active practice timer.");
                        pictures.Add(f.Pixels()); areas.Add(f.Bounds(f.Ui.GetComponentInChildren<P02SoundIllustration>(false).rectTransform));
                        f.Capture("P02-visual-" + stages[index]);
                        if (index == 0) f.AssertIllustrationGestureCancellation("P02");
                    }
                    // These three cue pages share the same background, so a color-theme
                    // change alone cannot satisfy the actual illustration difference.
                    for (int a = 2; a < pictures.Count; a++) for (int b = a + 1; b < pictures.Count; b++)
                    {
                        int changed = f.ChangedPixels(pictures[a], pictures[b], anchor, 8);
                        report.visualMeasurements.Add(width + "x" + height + " P02 " + stages[a] + "/" + stages[b] + ": " + changed + " changed illustration pixels");
                        Assert(changed >= 24, "P02 noticing, observing and returning have visibly distinct guidance inside their common illustration area.");
                    }
                    for (int index = stages.Length - 2; index >= 0; index--)
                    {
                        f.Click("Guided.Previous"); f.AssertP02Illustration(stages[index]);
                        f.Capture("P02-visual-back-" + stages[index]);
                        Assert(f.ChangedPixels(pictures[index], f.Pixels(), areas[index], 0) == 0,
                            "P02 previous navigation restores the exact fresh " + stages[index] + " illustration without residual effects.");
                    }
                    Assert(StateCodec.Encode(f.Service.Snapshot) == beforeReading && f.Store.Saves == readingWrites,
                        "P02 reading, dragging and previous navigation preserve the original draft, plant, wallet and save count.");
                    for (int index = 1; index < stages.Length; index++) f.Click("Focus.IconAction");
                    f.Store.Fail = true; f.Click("Focus.IconAction"); f.Store.Fail = false;
                    Assert(StateCodec.Encode(f.Service.Snapshot) == beforeReading && f.Session(id).Status == ParticipationState.Selected,
                        "P02 failed practice start preserves the original selected session and rewards.");
                    f.AssertP02Illustration(P02SoundIllustration.Stage.Return); f.AssertReadingSeparation(); f.Capture("P02-visual-start-error");
                    f.Click("Focus.IconAction"); f.AssertP02Illustration(P02SoundIllustration.Stage.Practice);
                    var clock = f.Ui.GetComponentInChildren<FocusPracticeClock>(false);
                    Assert(clock != null && clock.Running && f.Session(id).Status == ParticipationState.InProgress,
                        "P02 starts its existing practice timer only after the last instruction.");
                    clock.ElapsedSeconds = 29; Assert(clock.Flush(), "Persist P02 synthetic elapsed practice");
                    f.Capture("P02-visual-practice");
                    string beforeHelp = StateCodec.Encode(f.Service.Snapshot); int helpWrites = f.Store.Saves;
                    Assert(f.Button("Guided.HelpToggle").GetComponentInChildren<Text>().text == "도움말", "P02 retains its help entry label.");
                    f.Click("Guided.HelpToggle"); f.AssertP02Illustration(P02SoundIllustration.Stage.Help);
                    Assert(!clock.Running && f.Ui.GetComponentInChildren<FocusPracticeClock>(false) == null
                        && f.Text("Guided.Help").text.Split('\n').Length == 3 && !f.Text("Guided.Progress").text.Contains("1/3"),
                        "P02 help presents three key statements on one page while the practice clock is paused.");
                    f.Capture("P02-visual-help-single"); f.Click("Focus.IconAction");
                    f.AssertP02Illustration(P02SoundIllustration.Stage.Practice);
                    Assert(StateCodec.Encode(f.Service.Snapshot) == beforeHelp && f.Store.Saves == helpWrites
                        && f.Ui.GetComponentInChildren<FocusPracticeClock>(false).ElapsedSeconds == 29,
                        "One click returns from P02 help to its practice without changing elapsed time or the saved draft.");
                    f.Click("Guided.ReflectionToggle"); f.AssertP02Illustration(P02SoundIllustration.Stage.Reflection);
                    foreach (var option in f.Ui.GetComponentsInChildren<Button>(false).Where(b => b.name.StartsWith("Guided.Reflection.", StringComparison.Ordinal)).ToArray())
                    { f.RevealRect((RectTransform)option.transform); f.AssertFullyVisible((RectTransform)option.transform, "P02 optional reflection choice remains reachable"); }
                    var input = f.Ui.GetComponentsInChildren<InputField>(false).Single(i => i.name == "Guided.ReflectionInput");
                    f.RevealRect((RectTransform)input.transform); f.AssertFullyVisible((RectTransform)input.transform, "P02 writing field remains reachable in its local answer scroll");
                    const string note = "창밖에서 들리는 새 소리를 잠시 살폈어요.";
                    input.text = note; input.onEndEdit.Invoke(note);
                    Assert(f.Session(id).FocusStepRecords[1].Value == note && f.Session(id).FocusStepRecords[0].ElapsedSeconds == 29,
                        "P02 optional writing uses the existing durable reflection field and preserves elapsed practice.");
                    f.AssertIcon(anchor); f.Capture("P02-visual-reflection-input");
                    f.Click("Guided.Previous"); f.AssertP02Illustration(P02SoundIllustration.Stage.Practice);
                    f.Click("Guided.ReflectionToggle");
                    Assert(f.Ui.GetComponentsInChildren<InputField>(false).Single(i => i.name == "Guided.ReflectionInput").text == note,
                        "P02 reflection back and reentry retain the user's draft without completing the mission.");
                    string beforeRender = StateCodec.Encode(f.Service.Snapshot); int renderWrites = f.Store.Saves;
                    var visual = f.Ui.GetComponentInChildren<P02SoundIllustration>(false); var staticPixels = f.Pixels();
                    visual.SetAllDirty(); f.Layout();
                    Assert(f.ChangedPixels(staticPixels, f.Pixels(), f.Bounds(visual.rectTransform), 0) == 0,
                        "P02 reduced-motion rendering remains static rather than pacing a breathing or sound animation.");
                    Assert(StateCodec.Encode(f.Service.Snapshot) == beforeRender && f.Store.Saves == renderWrites
                        && f.Service.GetPracticeGrowth(id) == null && f.Service.CoinBalance == 187
                        && f.Ui.GetComponentsInChildren<AudioSource>(true).Length == 0,
                        "P02 illustration and optional reading introduce no audio source, completion, growth, reward or extra save.");
                }
            }
            finally { FigmaMissionUi.ReducedMotion = reduced; }
        }
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
                    var overviewPixels = order >= 3 ? f.Pixels() : null;
                    Rect overviewArea = order >= 3 ? f.Bounds(f.Ui.GetComponentInChildren<FocusPracticeIllustration>(false).rectTransform) : anchor;
                    var oldOverview = f.Button("Focus.IconAction").onClick;
                    f.Ui.gameObject.SetActive(false); oldOverview.Invoke(); f.Ui.gameObject.SetActive(true); f.Layout();
                    f.AssertSlide("Guided.Overview", definition.Overview);
                    f.Click("Focus.IconAction"); oldOverview.Invoke(); f.Layout();
                    f.AssertSlide("Guided.Preparation", FocusGuidedPracticeContent.Preparation(code)); anchor = f.AssertIcon();
                    var preparationPixels = order >= 3 ? f.Pixels() : null;
                    Rect preparationArea = order >= 3 ? f.Bounds(f.Ui.GetComponentInChildren<FocusPracticeIllustration>(false).rectTransform) : anchor;
                    if (order >= 3) Assert(f.ChangedPixels(overviewPixels, preparationPixels, preparationArea, 0) >= 24,
                        code + " preparation visibly differs from its introduction within the actual illustration area");
                    Assert(f.Session(id).Status == ParticipationState.Selected, code + " repeated old overview action cannot skip preparation or start");
                    f.Click("Guided.Previous"); f.AssertSlide("Guided.Overview", definition.Overview);
                    if (order >= 3) Assert(f.ChangedPixels(overviewPixels, f.Pixels(), overviewArea, 0) == 0,
                        code + " previous restores the original mission illustration without residual preparation effects");
                    f.Click("Focus.IconAction"); f.AssertSlide("Guided.Preparation", FocusGuidedPracticeContent.Preparation(code));
                    if (order >= 3) Assert(f.ChangedPixels(preparationPixels, f.Pixels(), preparationArea, 0) == 0,
                        code + " advancing again restores the same preparation illustration");
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
                        f.AssertSlide("Guided.ChoiceTitle", FocusGuidedPracticeContent.ChoiceLabel(code)); f.AssertIcon(anchor);
                        f.Click("Focus.IconAction");
                        f.AssertSlide("Guided.Cue.0", FocusGuidedPracticeContent.Guidance(code, "")[0]);
                        Assert(f.Session(id).FocusStepRecords[0].Value == "", code + " no selection uses default guidance without forced answer");
                        f.Click("Guided.Previous");
                        Color32[] priorChoicePicture = null;
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
                            if (code == "P06")
                            {
                                var currentPicture = f.Pixels(); var art = f.Ui.GetComponentInChildren<FocusPracticeIllustration>(false);
                                if (priorChoicePicture != null) Assert(f.ChangedPixels(priorChoicePicture, currentPicture, f.Bounds(art.rectTransform), 0) >= 24,
                                    "P06 choosing hands or dishes changes the actual task illustration while retaining the same instruction step.");
                                priorChoicePicture = currentPicture; f.Capture(code + "-chosen-" + choice);
                            }
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
                    var help = code == "P01" ? new[] { P01BreathGuide.Body(P01BreathIllustration.Stage.Help, chosen) }
                        : code == "P02" ? new[] { P02SoundGuide.Body(P02SoundIllustration.Stage.Help) }
                        : new[] { FocusGuidedPracticeContent.Help(code) };
                    Assert(help.Length == 1 && help[0].Split('\n').Length == 3 && !f.Text("Guided.Progress").text.Contains("1/3"),
                        code + " displays its three short help statements together on one page");
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
                    f.AssertIcon(anchor);
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
                    if (CameraMissionPolicy.Target(f.Session(id)) != null)
                    {
                        Assert(Screen(f.Ui) == "focus-photo" && f.Session(id).Status != ParticipationState.RewardCommitted
                            && f.Service.GetPracticeGrowth(id) == null && f.Service.CoinBalance == 137 + (order - 1) * 50,
                            code + " optional photo choice precedes rewards and opens no camera automatically");
                        f.Capture(code + "-optional-photo");
                        f.Click("Photo.Skip");
                        Assert(f.Session(id).PhotoPromptHandled && f.Session(id).Photo == null && f.Session(id).PhotoBonusAmount == 0,
                            code + " real skip completes without a photo or photo bonus");
                    }
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
            Store store; string originalRecords, originalStarted;
            using (var f = new Fixture(360, 640))
            {
                Assert(f.Service.SelectMbctProgram("8w") && f.Service.BeginFocusSession("existing-active-v3", "P01", 1, program: "8w"), "Seed original v3 session");
                string answer = FocusMissionCatalog.Find("P01").Steps[1].Options.Last();
                Assert(f.Service.StartGuidedFocusSession("existing-active-v3", "배")
                    && f.Service.SaveGuidedFocusDraft("existing-active-v3", "배", answer, 47), "Persist original active v3 draft");
                // Original integrated saves used schema 1 / guided v3; texture's
                // schema 2 / v3 means a different catalog and must not be faked.
                var old = f.Service.Snapshot; var session = old.Sessions.Single();
                old.SchemaVersion = 1; old.PlantCollection = null; session.FocusVersion = 3;
                originalRecords = CapstoneDesign.Plants.PlantCodec.Encode(session.FocusStepRecords);
                originalStarted = session.StartedUtc;
                store = new Store(); store.Save(old);
            }
            using (var restored = new Fixture(360, 640, store))
            {
                string before = StateCodec.Encode(restored.Service.Snapshot);
                restored.Ui.SelectFocusSession("existing-active-v3"); restored.Layout();
                restored.AssertSlide("Guided.PracticeHint", FocusGuidedPracticeContent.PracticeHint("P01"));
                Assert(restored.Service.Snapshot.SchemaVersion == 3 && restored.Session("existing-active-v3").FocusVersion == 4
                    && restored.Session("existing-active-v3").FocusStepRecords[0].Value == "배"
                    && restored.Session("existing-active-v3").Status == ParticipationState.InProgress
                    && restored.Session("existing-active-v3").StartedUtc == originalStarted
                    && CapstoneDesign.Plants.PlantCodec.Encode(restored.Session("existing-active-v3").FocusStepRecords) == originalRecords
                    && restored.Service.CoinBalance == 137 && restored.Service.GetPracticeGrowth("existing-active-v3") == null
                    && restored.Ui.GetComponentInChildren<FocusPracticeClock>(false).ElapsedSeconds == 47,
                    "Original guided v3 translates to v4 without losing draft, time, participation or awarding rewards");
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
        { string xml; public bool Fail; public int Saves; public GardenState Load() => xml == null ? null : StateCodec.Decode(xml);
          public void Save(GardenState value) { if (Fail) throw new IOException("Synthetic guided UI save failure"); xml = StateCodec.Encode(value); Saves++; } }
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
                var breath = Screen(Ui) == "focus-practice" ? Ui.GetComponentInChildren<P01BreathIllustration>(false) : null;
                if (breath != null)
                {
                    var nodes = new[] { "Guided.Overview", "Guided.Preparation", "Guided.ChoiceTitle", "Guided.Cue.0", "Guided.Cue.1", "Guided.Cue.2", "Guided.PracticeHint", "Guided.ReflectionPrompt", "Guided.Help" };
                    var stages = new[] { P01BreathIllustration.Stage.Overview, P01BreathIllustration.Stage.Preparation, P01BreathIllustration.Stage.Choice,
                        P01BreathIllustration.Stage.Notice, P01BreathIllustration.Stage.Observe, P01BreathIllustration.Stage.Return,
                        P01BreathIllustration.Stage.Practice, P01BreathIllustration.Stage.Reflection, P01BreathIllustration.Stage.Help };
                    int index = Array.IndexOf(nodes, name);
                    Assert(index >= 0 && breath.CurrentStage == stages[index], "P01 picture stage matches the requested guidance page.");
                    expected = P01BreathGuide.Body(stages[index], breath.FocusArea, breath.HelpPage);
                }
                var sound = Screen(Ui) == "focus-practice" ? Ui.GetComponentInChildren<P02SoundIllustration>(false) : null;
                if (sound != null)
                {
                    var nodes = new[] { "Guided.Overview", "Guided.Preparation", "Guided.Cue.0", "Guided.Cue.1", "Guided.Cue.2", "Guided.PracticeHint", "Guided.ReflectionPrompt", "Guided.Help" };
                    int index = Array.IndexOf(nodes, name);
                    Assert(index >= 0 && sound.CurrentStage == (P02SoundIllustration.Stage)index, "P02 picture stage matches its actual guidance page.");
                    expected = P02SoundGuide.Body(sound.CurrentStage);
                }
                var practiceArt = Screen(Ui) == "focus-practice" ? Ui.GetComponentInChildren<FocusPracticeIllustration>(false) : null;
                FocusMissionDefinition genericMission = null;
                if (practiceArt != null)
                {
                    var nodes = new[] { "Guided.Overview", "Guided.Preparation", "Guided.ChoiceTitle", "Guided.Cue.0", "Guided.Cue.1", "Guided.Cue.2", "Guided.PracticeHint", "Guided.ReflectionPrompt", "Guided.Help" };
                    var keys = new[] { "overview", "preparation", "choice", "cue0", "cue1", "cue2", "practice", "reflection", "help" };
                    int index = Array.IndexOf(nodes, name);
                    string currentId = (string)typeof(WeekOneQuestDemo).GetField("sessionId", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(Ui);
                    genericMission = FocusMissionCatalog.FindForSession(Ui.Service.Snapshot.Sessions.Single(s => s.SessionId == currentId));
                    string choice = (string)typeof(WeekOneQuestDemo).GetField("guidedChoice", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(Ui);
                    Assert(index >= 0 && practiceArt.MissionCode == genericMission.Code && practiceArt.StepKey == keys[index]
                        && practiceArt.Choice == choice && !practiceArt.raycastTarget,
                        "The illustrated mission follows its actual mission, slide and current choice without intercepting input.");
                }
                Assert(Screen(Ui) == "focus-practice" && text != null && text.text == expected, "Expected single slide " + name);
                var names = new[] { "Guided.Overview", "Guided.Preparation", "Guided.ChoiceTitle", "Guided.Cue.0", "Guided.Cue.1", "Guided.Cue.2", "Guided.PracticeHint", "Guided.ReflectionPrompt", "Guided.Help" };
                Assert(Ui.GetComponentsInChildren<Text>(false).Count(t => names.Contains(t.name)) == 1, "Exactly one piece of guidance is visible on " + name);
                AssertFullyVisible(text.rectTransform, name);
                Assert(text.preferredHeight <= text.rectTransform.rect.height + .5f, name + " fits its complete wrapped body");
                var centered = new[] { TextAnchor.UpperCenter, TextAnchor.MiddleCenter, TextAnchor.LowerCenter };
                var title = Text("Guided.Title"); var context = Text("Guided.Mission");
                if (breath != null || sound != null || practiceArt != null)
                {
                    Assert(text.fontSize == 22 && centered.Contains(text.alignment), "Every current basic mission keeps its centered 22-point instruction.");
                    if (name == "Guided.Overview")
                    {
                        string missionTitle = breath != null ? "잠깐 멈춰보기" : sound != null ? "주변 소리 만나기" : genericMission.Title;
                        string overview = breath != null ? "숨이 느껴지는 한 곳에\n잠시 주의를 두는 실습이에요."
                            : sound != null ? "주변에서 들리는 소리에\n잠시 귀 기울이는 실습이에요." : genericMission.Overview;
                        Assert(title != null && context != null && title.text == missionTitle && context.text == "기본 미션"
                            && text.text == overview && title.fontSize == 30 && context.fontSize == 16,
                            "The introduction retains its original mission title, context, copy and 30/22/16-point hierarchy.");
                        foreach (var header in new[] { title, context })
                        {
                            Assert(centered.Contains(header.alignment) && header.preferredHeight <= header.rectTransform.rect.height + .5f,
                                header.name + " retains its complete centered introduction text");
                            AssertFullyVisible(header.rectTransform, header.name);
                        }
                        Assert(Bounds(context.rectTransform).yMin >= Bounds(title.rectTransform).yMax - .75f
                            && Bounds(title.rectTransform).yMin >= Bounds(text.rectTransform).yMax - .75f,
                            "Introduction context, title and body remain separate.");
                    }
                    else
                    {
                        var slide = text.GetComponentInParent<FocusGuidedSlideLayout>();
                        Assert(title == null && context == null && slide.Heading == null
                            && !slide.GetComponentsInChildren<Transform>(true).Any(t => t.name == "Guided.Title" || t.name == "Guided.Mission"),
                            "Later current basic pages do not create title or mission-context objects, including hidden placeholders.");
                        string heading = breath != null ? P01BreathGuide.Title(breath.CurrentStage, breath.FocusArea, breath.HelpPage)
                            : sound != null ? P02SoundGuide.Title(sound.CurrentStage) : "";
                        Assert(heading == "" && Mathf.Abs(-FigmaMissionUi.TopLeftPosition(text.rectTransform).y - 126) < .1f
                            && text.rectTransform.rect.height >= 54 - .1f,
                            "Later illustrated pages contain only their concise instruction at the reserved reading position.");
                        var close = (RectTransform)Button("Focus.Close").transform;
                        Assert(Bounds(close).yMin >= Bounds(text.rectTransform).yMax - .75f,
                            "The instruction remains below the exit control.");
                    }
                }
                else Assert(title != null && context != null && centered.Contains(text.alignment) && centered.Contains(title.alignment),
                    "Other guided missions retain their title, context and centered instruction.");
                var body = ScreenRect(text.rectTransform); var icon = ScreenRect((RectTransform)Button("Focus.IconAction").transform);
                Assert(body.yMin >= icon.yMax - .5f, name + " does not overlap the fixed illustration");
                Assert(Button("Focus.Action", false) == null, "Guided progress uses the central icon rather than an old footer action");
                if (practiceArt != null)
                {
                    int painted = ChangedPixels(WithoutGraphicPixels(practiceArt), Pixels(), Bounds(practiceArt.rectTransform), 0);
                    Assert(painted >= 128, practiceArt.MissionCode + "/" + practiceArt.StepKey + " actually paints its mission-specific illustration on the GPU.");
                }
            }
            public Rect AssertIcon(Rect? expected = null)
            {
                Layout(); var rect = (RectTransform)Button("Focus.IconAction").transform;
                AssertFullyVisible(rect, "Fixed central icon");
                var slide = rect.GetComponentInParent<FocusGuidedSlideLayout>();
                bool soundIllustration = Screen(Ui) == "focus-practice" && Ui.GetComponentInChildren<P02SoundIllustration>(false) != null;
                bool measuredIllustration = soundIllustration || Screen(Ui) == "focus-practice" && (Ui.GetComponentInChildren<P01BreathIllustration>(false) != null
                    || Ui.GetComponentInChildren<FocusPracticeIllustration>(false) != null);
                bool practice = Text("Guided.PracticeHint") != null;
                bool constrained = ((RectTransform)slide.transform).rect.height < 672
                    || slide.Notice != null && slide.Notice.gameObject.activeInHierarchy
                    || measuredIllustration && (slide.Heading != null && slide.Heading.preferredHeight > 42.5f || slide.Copy != null && slide.Copy.preferredHeight > 64);
                Assert(Vector2.Distance(rect.pivot, new Vector2(.5f, .5f)) < .001f && Mathf.Abs(rect.rect.width - rect.rect.height) < .01f,
                    "Central icon stays square with a centered press pivot");
                if (measuredIllustration)
                {
                    // Measure the actual text/footer geometry so larger copy and
                    // storage notices retain their space at every output size.
                    float copyBottom = -FigmaMissionUi.TopLeftPosition(slide.Copy.rectTransform).y + slide.Copy.rectTransform.rect.height;
                    float lowerEdge = slide.Notice != null && slide.Notice.gameObject.activeInHierarchy
                        ? -FigmaMissionUi.TopLeftPosition(slide.Notice.rectTransform).y
                        : -FigmaMissionUi.TopLeftPosition(slide.Navigation).y - 8;
                    float designHeight = ((RectTransform)slide.transform).rect.height;
                    float down = Mathf.Lerp(14, 32, Mathf.InverseLerp(692, 844, designHeight));
                    float minimumTop = Mathf.Max(254, copyBottom + 18) + down;
                    // f4a8548 added the 146-point timer/shorten controls. Only
                    // Practice adapts its image to keep this whole footer visible.
                    float minimumAnswers = practice ? 146 : soundIllustration ? 100 : 86;
                    Assert(slide.KeepAnswersVisible == practice && Mathf.Abs(slide.MinimumAnswerHeight - minimumAnswers) < .1f,
                        "Only timed practice reserves its complete 146-point control area; reading slides retain their larger illustration contract.");
                    if (practice)
                        minimumTop = Mathf.Min(minimumTop, Mathf.Max(copyBottom + 18, lowerEdge - 40 - minimumAnswers - 160));
                    float expectedSize = Mathf.Clamp(lowerEdge - minimumTop - 40 - minimumAnswers, 160, 280);
                    Assert(Mathf.Abs(rect.rect.width - expectedSize) < .1f,
                        "Illustrated guidance fills available reading space up to 280 points while retaining its body, action hint and " + minimumAnswers + "-point answer area: expected "
                        + expectedSize + ", actual " + rect.rect.width);
                    float actualTop = -FigmaMissionUi.TopLeftPosition(rect).y;
                    float expectedTop = Mathf.Max(minimumTop, designHeight * .48f - expectedSize / 2 + down);
                    if (practice) expectedTop = Mathf.Min(expectedTop, lowerEdge - minimumAnswers - 40 - expectedSize);
                    Assert(Mathf.Abs(actualTop - expectedTop) < .1f,
                        "Measured guidance moves its central illustration down responsively without taking space from the text or answers.");
                }
                else Assert(constrained ? rect.rect.width >= 180-.01f && rect.rect.width <= 224+.01f : Mathf.Abs(rect.rect.width-224) < .01f,
                    "Other guided icons remain 224 design points, shrinking only to 180 when constrained");
                var bounds = ScreenRect(rect);
                Assert(Mathf.Abs(bounds.center.x - width * .5f) < .75f,"Central icon remains horizontally centered: " + bounds);
                var body = Ui.GetComponentsInChildren<Text>(false).Single(text => new[] { "Guided.Overview", "Guided.Preparation", "Guided.ChoiceTitle", "Guided.Cue.0", "Guided.Cue.1", "Guided.Cue.2", "Guided.PracticeHint", "Guided.ReflectionPrompt", "Guided.Help" }.Contains(text.name));
                Assert(ScreenRect(body.rectTransform).yMin >= bounds.yMax-.75f,"Enlarged icon leaves current guidance unobscured");
                Assert(bounds.width >= 44 && bounds.height >= 44, "Icon target is at least 44 pixels");
                if (practice) AssertPracticeControls(slide);
                if (expected.HasValue && !constrained && !practice) Assert(Vector2.Distance(bounds.center, expected.Value.center) < .75f
                    && Vector2.Distance(bounds.size, expected.Value.size) < .75f, "Reading slides retain the same central icon rectangle");
                return bounds;
            }
            void AssertPracticeControls(FocusGuidedSlideLayout slide)
            {
                // Inspect without RevealRect: scrolling to a clipped control must
                // never make this no-scroll practice-screen check pass.
                Assert(Mathf.Abs(slide.ContentHeight - 146) < .1f && slide.Viewport.rect.height >= 146 - .1f,
                    "Practice reserves all authored timer, reflection and shorten controls in its visible answer area.");
                Assert(slide.Content.rect.height <= slide.Viewport.rect.height + .1f && Mathf.Abs(slide.Content.anchoredPosition.y) < .1f,
                    "Practice controls are fully visible without local answer scrolling.");
                var timerStatus = Text("Focus.TimerStatus");
                Assert(timerStatus != null, "Practice timer status exists.");
                AssertFullyVisible(timerStatus.rectTransform, "Practice timer status");
                foreach (var name in new[] { "Focus.TimerToggle", "Guided.ReflectionToggle", "Focus.TooLong" })
                {
                    var control = Button(name, false);
                    Assert(control != null && control.gameObject.activeInHierarchy, "Practice control exists: " + name);
                    AssertFullyVisible((RectTransform)control.transform, "Practice control " + name);
                }
                var answers = ScreenRect(slide.Viewport);
                var hint = ScreenRect(slide.ActionHint);
                var lower = ScreenRect(slide.Notice != null && slide.Notice.gameObject.activeInHierarchy
                    ? slide.Notice.rectTransform : slide.Navigation);
                Assert(answers.yMax <= hint.yMin + .75f && answers.yMin >= lower.yMax - .75f,
                    "The complete practice controls stay between the action hint and navigation or save notice without overlap.");
            }
            public void AssertP02Illustration(P02SoundIllustration.Stage stage)
            {
                Layout(); var visual = Ui.GetComponentInChildren<P02SoundIllustration>(false);
                Assert(visual != null && visual.CurrentStage == stage && !visual.raycastTarget,
                    "P02 uses the expected non-blocking sound illustration: " + stage);
                var nodes = new[] { "Guided.Overview", "Guided.Preparation", "Guided.Cue.0", "Guided.Cue.1", "Guided.Cue.2", "Guided.PracticeHint", "Guided.ReflectionPrompt", "Guided.Help" };
                AssertSlide(nodes[(int)stage], P02SoundGuide.Body(stage));
                Assert(!string.IsNullOrWhiteSpace(Text(nodes[(int)stage]).text), "P02 retains a readable instruction on every stage.");
                Rect icon = AssertIcon(); var hint = Text("Guided.ActionHint");
                var answers = Ui.GetComponentsInChildren<RectTransform>(false).Single(r => r.name == "Guided.AnswersViewport");
                var navigation = Ui.GetComponentsInChildren<RectTransform>(false).Single(r => r.name == "Guided.Navigation");
                Assert(icon.yMin >= Bounds(hint.rectTransform).yMax - .75f
                    && Bounds(hint.rectTransform).yMin >= Bounds(answers).yMax - .75f
                    && Bounds(answers).yMin >= Bounds(navigation).yMax - .75f,
                    "P02 picture, action hint, local response viewport and navigation do not overlap.");
                AssertFullyVisible(hint.rectTransform, "P02 action hint"); AssertFullyVisible(answers, "P02 local answer viewport");
                int painted = ChangedPixels(WithoutGraphicPixels(visual), Pixels(), icon, 0);
                report.visualMeasurements.Add(width + "x" + height + " P02 " + stage + ": " + painted + " visible illustration pixels; target=" + icon);
                Assert(painted >= 128, "P02 stage is actually visible in the GPU render.");
            }
            public void AssertP01Illustration(P01BreathIllustration.Stage stage, string focus = null, int helpPage = -1)
            {
                Layout(); var visual = Ui.GetComponentInChildren<P01BreathIllustration>(false);
                Assert(visual != null && visual.CurrentStage == stage && !visual.raycastTarget,
                    "P01 uses the expected non-blocking illustration stage: " + stage);
                if (focus != null) Assert(visual.FocusArea == focus, "P01 illustration follows the actual chosen focus area " + focus);
                if (helpPage >= 0) Assert(visual.HelpPage == helpPage, "P01 help picture follows page " + helpPage);
                var body = Ui.GetComponentsInChildren<Text>(false).Single(t => new[] { "Guided.Overview", "Guided.Preparation", "Guided.ChoiceTitle", "Guided.Cue.0", "Guided.Cue.1", "Guided.Cue.2", "Guided.PracticeHint", "Guided.ReflectionPrompt", "Guided.Help" }.Contains(t.name));
                AssertSlide(body.name, P01BreathGuide.Body(stage, visual.FocusArea, visual.HelpPage));
                Assert(!string.IsNullOrWhiteSpace(body.text), "P01 retains a readable instruction on every stage.");
                if (stage == P01BreathIllustration.Stage.Notice && focus != null)
                    Assert(focus.Length > 0 ? body.text.Contains(focus) : new[] { "코", "가슴", "배" }.All(body.text.Contains),
                        "Notice names the chosen body area or preserves all three options when none is chosen.");
                Rect icon = AssertIcon(); var hint = Text("Guided.ActionHint");
                var answers = Ui.GetComponentsInChildren<RectTransform>(false).Single(r => r.name == "Guided.AnswersViewport");
                var navigation = Ui.GetComponentsInChildren<RectTransform>(false).Single(r => r.name == "Guided.Navigation");
                Assert(icon.yMin >= Bounds(hint.rectTransform).yMax - .75f
                    && Bounds(hint.rectTransform).yMin >= Bounds(answers).yMax - .75f
                    && Bounds(answers).yMin >= Bounds(navigation).yMax - .75f,
                    "P01 illustration, action hint, local answers and bottom navigation do not overlap.");
                AssertFullyVisible(hint.rectTransform, "P01 action hint"); AssertFullyVisible(answers, "P01 local answer viewport");
                var shown = Pixels(); var hidden = WithoutGraphicPixels(visual); int painted = ChangedPixels(hidden, shown, icon, 0);
                report.visualMeasurements.Add(width + "x" + height + " P01 " + stage + "/" + focus + "/" + helpPage + ": " + painted + " visible illustration pixels; target=" + icon);
                Assert(painted >= 128, "P01 stage is actually painted on the GPU instead of only exposing a configured component.");
                AssertP01AttentionDots(visual, shown);
            }
            void AssertP01AttentionDots(P01BreathIllustration visual, Color32[] pixels)
            {
                // Sample the three anatomical positions in the authored figure,
                // not the halo circumference or the guide's far endpoint.
                var names = new[] { "코", "가슴", "배" };
                var points = new[] { new Vector2(132, 80), new Vector2(128, 132), new Vector2(128, 164) };
                bool selected = names.Contains(visual.FocusArea);
                bool guide = visual.CurrentStage == P01BreathIllustration.Stage.Choice
                    || visual.CurrentStage == P01BreathIllustration.Stage.Help && visual.HelpPage == 0;
                bool selectedGuide = visual.CurrentStage == P01BreathIllustration.Stage.Notice
                    || visual.CurrentStage == P01BreathIllustration.Stage.Return
                    || visual.CurrentStage == P01BreathIllustration.Stage.Help && visual.HelpPage == 2;
                var rect = visual.rectTransform.rect; float unit = Mathf.Min(rect.width, rect.height) / 232f;
                for (int i = 0; i < points.Length; i++)
                {
                    bool expected = guide ? !selected || visual.FocusArea == names[i] : selected && selectedGuide && visual.FocusArea == names[i];
                    var local = new Vector2(rect.center.x + (points[i].x - 128) * unit, rect.center.y + (128 - points[i].y) * unit);
                    var point = camera.WorldToScreenPoint(visual.rectTransform.TransformPoint(local));
                    int colored = 0;
                    for (int y = Mathf.Max(0, Mathf.FloorToInt(point.y) - 1); y <= Mathf.Min(height - 1, Mathf.FloorToInt(point.y) + 1); y++)
                        for (int x = Mathf.Max(0, Mathf.FloorToInt(point.x) - 1); x <= Mathf.Min(width - 1, Mathf.FloorToInt(point.x) + 1); x++)
                        {
                            var color = pixels[y * width + x];
                            // The dark green dot is distinct from skin, clothing,
                            // ink and a pale translucent halo under this small ROI.
                            if (color.g - color.r > 35 && color.g - color.b > 15 && color.g < 155) colored++;
                        }
                    report.visualMeasurements.Add(width + "x" + height + " P01 dot " + visual.CurrentStage + "/" + visual.FocusArea
                        + "/" + visual.HelpPage + "/" + names[i] + ": pixels=" + colored + ", expected=" + expected);
                    Assert(expected ? colored > 0 : colored == 0,
                        "P01 " + visual.CurrentStage + " shows a centre dot at " + names[i] + " only when its guide or return arrow uses that point.");
                }
            }
            public void AssertIllustrationGestureCancellation(string code)
            {
                var priorEvents = EventSystem.current; bool reduced = FigmaMissionUi.ReducedMotion;
                var flags = BindingFlags.Instance | BindingFlags.NonPublic;
                typeof(EventSystem).GetMethod("OnEnable", flags).Invoke(events, null); EventSystem.current = events;
                try
                {
                    foreach (bool staticMode in new[] { false, true })
                    {
                        FigmaMissionUi.ReducedMotion = staticMode;
                        var button = Button("Focus.IconAction"); var rect = (RectTransform)button.transform;
                        var normal = rect.localScale; Rect area = Bounds(rect); string state = StateCodec.Encode(Service.Snapshot); int saves = Store.Saves;
                        events.SetSelectedGameObject(null); var baseline = Pixels();
                        var data = new PointerEventData(events) { position = area.center, pressPosition = area.center, pointerId = -1,
                            button = PointerEventData.InputButton.Left, eligibleForClick = true };
                        var hits = new List<RaycastResult>(); raycaster.Raycast(data, hits); report.raycasts++;
                        Assert(hits.Count > 0 && hits[0].gameObject.GetComponentInParent<Button>() == button, code + " actual central graphic routes the pointer to its action button.");
                        data.pointerCurrentRaycast = data.pointerPressRaycast = hits[0];
                        ExecuteEvents.Execute(button.gameObject, data, ExecuteEvents.pointerDownHandler); Layout();
                        Assert(events.currentSelectedGameObject == button.gameObject, code + " real pointer down selects its action control.");
                        Assert(Vector2.Distance(Bounds(rect).center, area.center) < .1f
                            && Vector3.Distance(rect.localScale, normal * (staticMode ? 1 : .97f)) < .001f,
                            code + " press stays centered and reduced motion suppresses press scaling.");
                        void NoOpaqueHit(string phase)
                        {
                            var shown = Pixels(); var withoutTarget = WithoutGraphicPixels(button.targetGraphic);
                            int darkened = ChangedPixels(withoutTarget, shown, Bounds(rect), 12, true);
                            report.visualMeasurements.Add(width + "x" + height + " " + code + " " + phase + " reduced=" + staticMode + ": " + darkened + " opaque hit pixels");
                            Assert(darkened <= 8, code + " transparent hit area never becomes an opaque filled panel during " + phase);
                        }
                        NoOpaqueHit("down"); Capture(code + "-visual-pointer-down-" + staticMode);
                        data.dragging = true; data.eligibleForClick = false; data.delta = new Vector2(42, 16); data.position += data.delta;
                        ExecuteEvents.Execute(button.gameObject, data, ExecuteEvents.beginDragHandler); ExecuteEvents.Execute(button.gameObject, data, ExecuteEvents.dragHandler);
                        NoOpaqueHit("drag"); Capture(code + "-visual-pointer-drag-" + staticMode);
                        data.position = new Vector2(area.xMax + 24, area.center.y);
                        ExecuteEvents.Execute(button.gameObject, data, ExecuteEvents.pointerExitHandler);
                        ExecuteEvents.Execute(button.gameObject, data, ExecuteEvents.pointerUpHandler); ExecuteEvents.Execute(button.gameObject, data, ExecuteEvents.endDragHandler); Layout();
                        Assert(rect.localScale == normal && Text("Guided.Overview") != null && StateCodec.Encode(Service.Snapshot) == state && Store.Saves == saves,
                            code + " cancelled drag restores its transform and never changes guidance, participation, saves or rewards.");
                        Assert(ChangedPixels(baseline, Pixels(), area, 0) == 0, code + " cancelled release restores its exact illustration pixels.");
                        Capture(code + "-visual-pointer-cancelled-" + staticMode);
                    }
                }
                finally
                {
                    FigmaMissionUi.ReducedMotion = reduced; events.SetSelectedGameObject(null);
                    typeof(EventSystem).GetMethod("OnDisable", flags).Invoke(events, null);
                    if (priorEvents != null) EventSystem.current = priorEvents;
                }
            }
            public Color32[] Pixels()
            {
                Layout(); camera.Render(); var prior = RenderTexture.active; var image = new Texture2D(width, height, TextureFormat.RGB24, false);
                try { RenderTexture.active = target; image.ReadPixels(new Rect(0, 0, width, height), 0, 0); image.Apply(); return image.GetPixels32(); }
                finally { RenderTexture.active = prior; UnityEngine.Object.DestroyImmediate(image); }
            }
            public Color32[] WithoutGraphicPixels(Graphic graphic)
            {
                bool enabled = graphic.enabled;
                try { graphic.enabled = false; return Pixels(); }
                finally { graphic.enabled = enabled; Layout(); }
            }
            public int ChangedPixels(Color32[] before, Color32[] after, Rect area, float inset, bool darkenedOnly = false)
            {
                int count = 0; inset *= width / 390f;
                for (int y = Mathf.Max(0, Mathf.CeilToInt(area.yMin + inset)); y < Mathf.Min(height, Mathf.FloorToInt(area.yMax - inset)); y++)
                    for (int x = Mathf.Max(0, Mathf.CeilToInt(area.xMin + inset)); x < Mathf.Min(width, Mathf.FloorToInt(area.xMax - inset)); x++)
                    {
                        var a = before[y * width + x]; var b = after[y * width + x];
                        bool different = Math.Abs(a.r - b.r) > 8 || Math.Abs(a.g - b.g) > 8 || Math.Abs(a.b - b.b) > 8;
                        if (different && (!darkenedOnly || a.r - b.r > 28 || a.g - b.g > 28 || a.b - b.b > 28)) count++;
                    }
                return count;
            }
            public Rect Bounds(RectTransform rect) => ScreenRect(rect);
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
