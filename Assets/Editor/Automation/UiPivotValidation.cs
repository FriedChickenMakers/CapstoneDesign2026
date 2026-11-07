using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using CapstoneDesign.Prototype;
using CapstoneDesign.Runtime;
using CapstoneDesign.Runtime.LocalState;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace CapstoneDesign.EditorTools
{
    // Explicit batch entry only: no InitializeOnLoad, polling, scene or user-save access.
    public static class UiPivotValidation
    {
        [Serializable] sealed class Check { public string name, status, detail; }
        [Serializable] sealed class Report
        {
            public string unityVersion, utc, projectPath, environment;
            public int passed, failed, assertions;
            public List<Check> checks = new List<Check>();
            public List<string> errors = new List<string>();
        }
        static Report report;
        static readonly Vector2 Center = new Vector2(.5f, .5f);
        const float Tolerance = .015f;

        public static void RunBatch()
        {
            string path = Arg("-pivotReport");
            if (!Application.isBatchMode || Application.companyName != "CapstoneSyntheticValidation"
                || !Application.productName.StartsWith("UiPivot-", StringComparison.Ordinal)
                || string.IsNullOrEmpty(path))
                throw new InvalidOperationException("UiPivotValidation requires its isolated synthetic batch project and explicit report path.");
            report = new Report { unityVersion = Application.unityVersion, utc = DateTimeOffset.UtcNow.ToString("O"),
                projectPath = Directory.GetCurrentDirectory(), environment = "Isolated Unity edit-mode batch / no graphics / real RectTransform / production pointer and explicit lifecycle callback invocation / synthetic memory state" };
            Application.logMessageReceived += ObserveError;
            bool preferenceExisted = PlayerPrefs.HasKey("ui.reduce-motion");
            int originalPreference = PlayerPrefs.GetInt("ui.reduce-motion", 0);
            try
            {
                FigmaMissionUi.ReducedMotion = false;
                Run("Authored button bounds, centered press and lifecycle", ButtonLifecycle);
                Run("Rotated, scaled and stretched pivot compensation", PivotCompensation);
                Run("Layout-driven button retains geometry after rebuild", DrivenLayout);
                Run("Mobile footer classification and resize", MobileLayout);
                Run("Actual timed mission finish button placement", TimedMission);
                Run("Keyboard done placement and input restoration", KeyboardPlacement);
                Run("Breathing illustration centers without shifting bounds", BreathingIllustration);
                Run("Existing GardenThemeSurfaceTests (10 styles)", ExistingThemeTests);
            }
            finally
            {
                if (preferenceExisted) PlayerPrefs.SetInt("ui.reduce-motion", originalPreference);
                else PlayerPrefs.DeleteKey("ui.reduce-motion");
                PlayerPrefs.Save();
                Application.logMessageReceived -= ObserveError;
                if (report.errors.Count > 0)
                {
                    report.failed++;
                    report.checks.Add(new Check { name = "Unexpected Unity errors", status = "fail", detail = string.Join("\n", report.errors) });
                }
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                File.WriteAllText(path, JsonUtility.ToJson(report, true));
            }
            Debug.Log("UI_PIVOTS " + (report.failed == 0 ? "PASS" : "FAIL") + " checks=" + report.passed + " assertions=" + report.assertions);
            EditorApplication.Exit(report.failed == 0 ? 0 : 1);
        }

        static string Arg(string name)
        {
            var args = Environment.GetCommandLineArgs(); int i = Array.IndexOf(args, name);
            return i >= 0 && i + 1 < args.Length ? args[i + 1] : null;
        }
        static void ObserveError(string message, string trace, LogType kind)
        { if (kind == LogType.Error || kind == LogType.Exception || kind == LogType.Assert) report.errors.Add(kind + ": " + message + "\n" + trace); }
        static void Run(string name, Action test)
        {
            try { test(); report.passed++; report.checks.Add(new Check { name = name, status = "pass" }); }
            catch (Exception ex) { report.failed++; report.checks.Add(new Check { name = name, status = "fail", detail = ex.ToString() }); }
        }
        static void Assert(bool value, string message)
        { report.assertions++; if (!value) throw new InvalidOperationException(message); }
        static void Near(Vector3 a, Vector3 b, string context)
        { Assert(Vector3.Distance(a, b) < Tolerance, context + " expected=" + b + " actual=" + a); }
        static Vector3[] Corners(RectTransform rect)
        { var value = new Vector3[4]; rect.GetWorldCorners(value); return value; }
        static void SameCorners(RectTransform rect, Vector3[] expected, string context)
        { var actual = Corners(rect); for (int i = 0; i < 4; i++) Near(actual[i], expected[i], context + " corner " + i); }
        static Vector3 Mid(RectTransform rect) => rect.TransformPoint(rect.rect.center);
        static void Authored(RectTransform rect, float x, float y, float width, float height, string context)
        {
            var tl = new Vector2(rect.anchoredPosition.x - rect.pivot.x * rect.rect.width,
                rect.anchoredPosition.y + (1 - rect.pivot.y) * rect.rect.height);
            Near(tl, new Vector2(x, -y), context + " top-left");
            Near(rect.rect.size, new Vector2(width, height), context + " size");
        }
        static void Invoke(object target, string method, params object[] args)
        {
            var found = target.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            Assert(found != null, "Required production method missing: " + method); found.Invoke(target, args);
        }

        sealed class Scope : IDisposable
        {
            public readonly GameObject Root;
            public readonly RectTransform Rect;
            public readonly EventSystem Events;
            public Scope()
            {
                Root = new GameObject("Synthetic pivot fixture", typeof(RectTransform)); Rect = (RectTransform)Root.transform;
                Rect.sizeDelta = new Vector2(390, 844);
                var e = new GameObject("Synthetic pointer", typeof(EventSystem)); e.transform.SetParent(Root.transform, false);
                Events = e.GetComponent<EventSystem>(); Events.enabled = false;
            }
            public void Dispose() { UnityEngine.Object.DestroyImmediate(Root); }
        }

        static void PressAndRestore(Button button, EventSystem events)
        {
            var rect = (RectTransform)button.transform; var feedback = button.GetComponent<FocusControlFeedback>();
            Assert(feedback != null, "Actual control feedback missing.");
            var before = Corners(rect); var center = Mid(rect); var normal = rect.localScale;
            // Non-ExecuteAlways components receive these messages from Unity in Play;
            // this edit-mode batch invokes the same production lifecycle explicitly.
            Invoke(feedback, "Awake"); SameCorners(rect, before, "Feedback Awake preserves authored corners");
            var data = new PointerEventData(events) { button = PointerEventData.InputButton.Left };
            for (int cycle = 0; cycle < 6; cycle++)
            {
                feedback.OnPointerDown(data); feedback.OnPointerDown(data);
                Near(rect.pivot, Center, "Press pivot"); Near(rect.localScale, normal * .97f, "Repeated down scales once");
                Near(Mid(rect), center, "Press center remains fixed");
                var down = Corners(rect);
                for (int i = 0; i < 4; i++) Near(down[i], center + (before[i] - center) * .97f, "Symmetric press corner " + i);
                if (cycle % 3 == 0) feedback.OnPointerUp(data);
                else if (cycle % 3 == 1) feedback.OnPointerExit(data);
                else { button.gameObject.SetActive(false); Invoke(feedback, "OnDisable"); button.gameObject.SetActive(true); }
                Near(rect.localScale, normal, "Release restores exact external scale");
                SameCorners(rect, before, "Release/exit/disable has no drift");
            }
            rect.localScale = new Vector3(1.13f, .83f, 1);
            feedback.OnPointerUp(data); feedback.OnPointerExit(data);
            Near(rect.localScale, new Vector3(1.13f, .83f, 1), "Release without press preserves external scale");
            var changed = Corners(rect); var changedCenter = Mid(rect);
            feedback.OnPointerDown(data); Near(Mid(rect), changedCenter, "External scale press stays centered");
            feedback.OnPointerUp(data); SameCorners(rect, changed, "External scale restored");
            button.interactable = false; feedback.OnPointerDown(data); SameCorners(rect, changed, "Disabled button does not shrink"); button.interactable = true;
            data.button = PointerEventData.InputButton.Right; feedback.OnPointerDown(data); SameCorners(rect, changed, "Right click does not shrink");
            FigmaMissionUi.ReducedMotion = true; data.button = PointerEventData.InputButton.Left;
            feedback.OnPointerDown(data); SameCorners(rect, changed, "Reduced motion disables scale"); feedback.OnPointerUp(data);
            FigmaMissionUi.ReducedMotion = false; rect.localScale = normal;
        }

        static void ButtonLifecycle()
        {
            using (var f = new Scope())
            {
                int calls = 0;
                var button = FigmaMissionUi.Button(f.Rect, "Synthetic action", 45, 650, 300, 56, () => calls++);
                var rect = (RectTransform)button.transform;
                Near(rect.pivot, Center, "Factory centers pivot"); Authored(rect, 45, 650, 300, 56, "Factory authored rectangle");
                var label = button.GetComponentInChildren<Text>().rectTransform;
                var labelBefore = Corners(label); var before = Corners(rect);
                FigmaMissionUi.CenterPivot(rect); SameCorners(rect, before, "Repeated centering is idempotent"); SameCorners(label, labelBefore, "Label unchanged");
                PressAndRestore(button, f.Events); SameCorners(label, labelBefore, "Label returns after all pointer paths");
                button.onClick.Invoke(); Assert(calls == 1, "Pivot and feedback preserve listeners.");
                FigmaMissionUi.SetRect(rect, 24, 714, 342, 48); Authored(rect, 24, 714, 342, 48, "Dynamic rectangle");
            }
        }

        static void PivotCompensation()
        {
            using (var f = new Scope())
            foreach (bool stretched in new[] { false, true })
            foreach (float rotation in new[] { 0f, 27f })
            {
                var parent = FigmaMissionUi.Rect(f.Rect, "Rotated parent", 30, 80, 340, 500);
                parent.localRotation = Quaternion.Euler(0, 0, rotation); parent.localScale = new Vector3(1.21f, .79f, 1);
                var rect = FigmaMissionUi.Rect(parent, "Authored control", 21, 43, 140, 64);
                rect.localRotation = Quaternion.Euler(0, 0, -13); rect.localScale = new Vector3(.88f, 1.17f, 1);
                if (stretched) { rect.anchorMin = new Vector2(.12f, .2f); rect.anchorMax = new Vector2(.81f, .9f); rect.sizeDelta = new Vector2(-40, -120); }
                var fixedChild = FigmaMissionUi.Rect(rect, "Child label", 7, 12, 55, 23);
                var stretchChild = FigmaMissionUi.Rect(rect, "Child graphic", 0, 0, 0, 0);
                stretchChild.anchorMin = Vector2.zero; stretchChild.anchorMax = Vector2.one; stretchChild.offsetMin = new Vector2(3, 4); stretchChild.offsetMax = new Vector2(-7, -9);
                var before = Corners(rect); var fixedBefore = Corners(fixedChild); var stretchBefore = Corners(stretchChild);
                var min = rect.anchorMin; var max = rect.anchorMax; var size = rect.rect.size;
                FigmaMissionUi.CenterPivot(rect);
                Near(rect.pivot, Center, "Complex rect pivot"); Near(rect.anchorMin, min, "Anchors min retained"); Near(rect.anchorMax, max, "Anchors max retained"); Near(rect.rect.size, size, "Stretch rect size retained");
                SameCorners(rect, before, "Rotated/scaled/stretched control"); SameCorners(fixedChild, fixedBefore, "Fixed child world corners"); SameCorners(stretchChild, stretchBefore, "Stretched child world corners");
                FigmaMissionUi.CenterPivot(rect); SameCorners(rect, before, "Complex repeated centering");
                rect.gameObject.AddComponent<Image>(); var b = rect.gameObject.AddComponent<Button>(); b.targetGraphic = rect.GetComponent<Image>();
                rect.gameObject.AddComponent<FocusControlFeedback>().Control = b; PressAndRestore(b, f.Events);
                UnityEngine.Object.DestroyImmediate(parent.gameObject);
            }
        }

        static void DrivenLayout()
        {
            using (var f = new Scope())
            {
                var parent = FigmaMissionUi.Rect(f.Rect, "Driven layout", 20, 50, 350, 300);
                var group = parent.gameObject.AddComponent<VerticalLayoutGroup>(); group.padding = new RectOffset(12, 12, 15, 15);
                group.spacing = 10; group.childControlWidth = group.childControlHeight = true; group.childForceExpandHeight = false;
                var rect = FigmaMissionUi.Rect(parent, "Authored driven button", 0, 0, 100, 56);
                var element = rect.gameObject.AddComponent<LayoutElement>(); element.preferredHeight = 56;
                rect.gameObject.AddComponent<Image>(); var button = rect.gameObject.AddComponent<Button>(); button.targetGraphic = rect.GetComponent<Image>();
                LayoutRebuilder.ForceRebuildLayoutImmediate(parent); var original = Corners(rect);
                FigmaMissionUi.CenterPivot(rect); LayoutRebuilder.ForceRebuildLayoutImmediate(parent); SameCorners(rect, original, "Layout rebuild preserves authored world rect");
                rect.gameObject.AddComponent<FocusControlFeedback>().Control = button;
                PressAndRestore(button, f.Events); LayoutRebuilder.ForceRebuildLayoutImmediate(parent); SameCorners(rect, original, "Layout after pointer cycle");
            }
        }

        static void MobileLayout()
        {
            using (var f = new Scope())
            {
                var design = FigmaMissionUi.Rect(f.Rect, "Design", 0, 0, 390, 844);
                FigmaMissionUi.Label(design, "Header", 24, 30, 342, 32);
                var body = FigmaMissionUi.Button(design, "Body close to footer", 45, 632, 300, 16, () => { });
                var main = FigmaMissionUi.Button(design, "Footer action", 45, 650, 300, 56, () => { });
                var finish = FigmaMissionUi.Button(design, "Footer secondary", 45, 714, 300, 48, () => { });
                // Its center is below 650 but the authored top is above the footer boundary.
                FigmaMissionUi.SetRect((RectTransform)body.transform, 45, 632, 300, 32);
                var originals = new[] { body, main, finish }.Select(b => Corners((RectTransform)b.transform)).ToArray();
                var mobile = design.gameObject.AddComponent<FocusMobilePageLayout>(); mobile.Page = "focus-practice"; mobile.Refresh();
                Assert(body.transform.parent.name == "Mobile.Content", "A body control whose center crosses 650 must not become a footer action.");
                Assert(main.transform.parent.name == "Mobile.Actions" && finish.transform.parent == main.transform.parent, "Both authored footer controls must share the footer.");
                SameCorners((RectTransform)body.transform, originals[0], "Body unchanged at authoring height");
                SameCorners((RectTransform)main.transform, originals[1], "Footer unchanged at authoring height");
                SameCorners((RectTransform)finish.transform, originals[2], "Secondary unchanged at authoring height");
                foreach (float height in new[] { 693.3333f, 844f, 640f, 844f })
                {
                    design.sizeDelta = new Vector2(390, height); mobile.Refresh(); mobile.Refresh();
                    var top = design.InverseTransformPoint(((RectTransform)main.transform).TransformPoint(((RectTransform)main.transform).rect.center));
                    Near(top, new Vector3(195, -(height - 194 + 28), 0), "Responsive footer center");
                    Authored((RectTransform)finish.transform, 45, 64, 300, 48, "Secondary local footer coordinates");
                    PressAndRestore(main, f.Events); mobile.Refresh();
                    Assert(body.transform.parent.name == "Mobile.Content", "Resize cannot reclassify the reading body.");
                }
                SameCorners((RectTransform)finish.transform, originals[2], "Resize roundtrip has no drift");
            }
        }

        sealed class MemoryStore : IStateStore
        { string data; public GardenState Load() => data == null ? null : StateCodec.Decode(data); public void Save(GardenState value) { data = StateCodec.Encode(value); } }
        sealed class Clock : IClock
        { public DateTimeOffset UtcNow => DateTimeOffset.Parse("2026-11-06T03:00:00Z"); public TimeZoneInfo TimeZone => TimeZoneInfo.Utc; }

        static void TimedMission()
        {
            using (var f = new Scope())
            using (var service = new GardenStateService(new MemoryStore(), new Clock(), new DemoBalanceConfig(), new LegacySnapshot()))
            {
                Assert(service.Open(), service.LastError);
                Assert(service.BeginFocusSession("pivot-timed", "free-mission:M08"), service.LastError);
                Assert(service.StartSession("pivot-timed"), service.LastError);
                Assert(service.ConfirmFocusStep("pivot-timed", 0, "", true), service.LastError);
                var owner = new GameObject("Synthetic actual mission presenter", typeof(RectTransform)); owner.SetActive(false); owner.transform.SetParent(f.Rect, false);
                var ui = owner.AddComponent<WeekOneQuestDemo>();
                // Inject before Awake; this fixture cannot open persistent state.
                typeof(WeekOneQuestDemo).GetField("<Service>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(ui, service);
                typeof(WeekOneQuestDemo).GetField("sessionId", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(ui, "pivot-timed");
                var design = FigmaMissionUi.Rect(f.Rect, "Actual practice content", 0, 0, 390, 844);
                var mission = FocusMissionCatalog.Find("free-mission:M08");
                Invoke(ui, "BuildAllFocusPractice", design, mission, 1, (Action)(() => { }));
                Invoke(ui, "AttachAllFocusClock", design, mission, 1);
                var finish = FigmaMissionUi.Find<Button>(design, "Focus.Action"); var toggle = FigmaMissionUi.Find<Button>(design, "Focus.TimerToggle");
                Assert(finish != null && toggle != null, "Real timed mission must build finish and timer controls.");
                Near(((RectTransform)finish.transform).pivot, Center, "Timed finish pivot");
                Authored((RectTransform)finish.transform, 45, 714, 300, 48, "Actual timed finish relocation");
                Authored((RectTransform)toggle.transform, 45, 650, 300, 56, "Actual timer placement");
                var before = Corners((RectTransform)finish.transform);
                var layout = design.gameObject.AddComponent<FocusMobilePageLayout>(); layout.Page = "focus-practice"; layout.Refresh();
                SameCorners((RectTransform)finish.transform, before, "Timed finish after footer extraction");
                Assert(finish.transform.parent.name == "Mobile.Actions", "Timed finish belongs in fixed footer.");
                PressAndRestore(finish, f.Events);
                design.sizeDelta = new Vector2(390, 693.3333f); layout.Refresh();
                Authored((RectTransform)finish.transform, 45, 64, 300, 48, "Actual finish remains spaced below timer on 360x640 ratio");
            }
        }

        static void KeyboardPlacement()
        {
            using (var f = new Scope())
            {
                var design = FigmaMissionUi.Rect(f.Rect, "Keyboard design", 0, 0, 390, 844);
                var input = FigmaMissionUi.Input(design, "Synthetic reflection", 45, 430, 300, 164);
                var before = Corners((RectTransform)input.transform); var originalParent = input.transform.parent;
                var avoid = design.gameObject.AddComponent<FocusMissionKeyboardAvoidance>(); avoid.Input = input; avoid.Title = "Synthetic form"; avoid.Background = Color.white; avoid.Accent = FigmaMissionUi.Green;
                int saves = 0; avoid.Save = () => saves++;
                Invoke(avoid, "OnEnable");
                try
                {
                foreach (float height in new[] { 200f, 320f, 480f, 650f, 320f })
                {
                    Invoke(avoid, "PresentForm", height);
                    var done = FigmaMissionUi.Find<Button>(design, "Focus.KeyboardDone"); Assert(done != null, "Keyboard done is dynamically created.");
                    var expected = FocusMissionKeyboardAvoidance.CalculateLayout(height, 430, 164);
                    Near(((RectTransform)done.transform).pivot, Center, "Keyboard done centered pivot");
                    Authored((RectTransform)done.transform, expected.Done.x, expected.Done.y, expected.Done.width, expected.Done.height, "Actual keyboard done rectangle");
                    Assert(expected.Done.yMin >= expected.Input.yMax && expected.Done.yMax <= height, "Keyboard done stays below editable input and above keyboard.");
                    PressAndRestore(done, f.Events);
                }
                FigmaMissionUi.Find<Button>(design, "Focus.KeyboardDone").onClick.Invoke();
                Assert(saves == 1 && input.transform.parent == originalParent, "Done restores original input parent and saves once.");
                SameCorners((RectTransform)input.transform, before, "Input returns without drift");
                Assert(FigmaMissionUi.Find<Button>(design, "Focus.KeyboardDone") == null, "Temporary keyboard done is removed.");
                }
                finally { Invoke(avoid, "OnDisable"); }
            }
        }

        static void BreathingIllustration()
        {
            using (var f = new Scope())
            {
                var illustration = FigmaMissionUi.Rect(f.Rect, "Breathing illustration", 69, 302, 252, 252);
                var child = FigmaMissionUi.Rect(illustration, "Icon", 50, 50, 152, 152);
                var before = Corners(illustration); var childBefore = Corners(child);
                var pulse = illustration.gameObject.AddComponent<FocusBreathingPulse>();
                // MonoBehaviour Awake is invoked explicitly in edit mode, matching Play initialization.
                Invoke(pulse, "Awake"); Near(illustration.pivot, Center, "Breathing illustration pivot");
                SameCorners(illustration, before, "Breathing authoring bounds"); SameCorners(child, childBefore, "Breathing icon bounds");
                var center = Mid(illustration); illustration.localScale = Vector3.one * 1.04f; Near(Mid(illustration), center, "Breathing scale remains centered");
            }
        }

        static void ExistingThemeTests()
        {
            var assembly = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(a => a.GetName().Name == "Capstone.Prototype.Tests")
                ?? Assembly.Load("Capstone.Prototype.Tests");
            var type = assembly.GetType("CapstoneDesign.Tests.GardenThemeSurfaceTests", true);
            var test = type.GetMethod("ApplyingAndRestylingDecorationDoesNotAddInputOrLayoutTargets");
            Assert(test != null, "Existing theme regression method must remain available.");
            foreach (GardenThemeSurface.Style style in Enum.GetValues(typeof(GardenThemeSurface.Style)))
            { test.Invoke(Activator.CreateInstance(type), new object[] { style }); Assert(true, "Existing theme assertions: " + style); }
        }
    }
}
