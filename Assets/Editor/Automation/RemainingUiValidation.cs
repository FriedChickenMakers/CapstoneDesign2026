using System;
using System.IO;
using System.Linq;
using System.Reflection;
using CapstoneDesign.Runtime;
using CapstoneDesign.Runtime.LocalState;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace CapstoneDesign.EditorTools
{
    // Explicit isolated fixtures only. No production save, native camera or device input.
    public static class RemainingUiValidation
    {
        static FocusMissionValidation.Report report;
        static int assertions;
        public static void RunBatch()
        {
            string requested = Environment.GetEnvironmentVariable("CAPSTONE_REMAINING_UI_VALIDATION");
            if (!Application.isBatchMode || EditorApplication.isPlayingOrWillChangePlaymode
                || Application.companyName != "CapstoneSyntheticValidation" || string.IsNullOrWhiteSpace(requested))
                throw new InvalidOperationException("An isolated synthetic batch and fresh output directory are required.");
            string output = Path.GetFullPath(requested);
            if (output == Path.GetFullPath(Application.persistentDataPath)
                || Directory.Exists(output) && Directory.EnumerateFileSystemEntries(output).Any())
                throw new InvalidOperationException("Output must be fresh and separate from app data.");
            Directory.CreateDirectory(output);
            string previous = Environment.GetEnvironmentVariable("CAPSTONE_ARTIFACTS");
            Environment.SetEnvironmentVariable("CAPSTONE_ARTIFACTS", output);
            report = new FocusMissionValidation.Report { runId = "remaining-ui", utc = DateTimeOffset.UtcNow.ToString("O"),
                unityVersion = Application.unityVersion, environment = "Memory stores, synthetic preview scenes and rendered UI; no real saves, native permissions or physical input" };
            assertions = 0;
            var debugScope=new DebugValidationScope(true);
            try
            {
                foreach (var size in new[] { new Vector2Int(320, 568), new Vector2Int(390, 844), new Vector2Int(1200, 2000) })
                {
                    var current = size;
                    Run("Course navigation, selection retry and fixed actions " + size, () => Courses(current));
                }
                Run("Remaining settings and time layout", () => RemainingSettingsUiValidation.Run(report, Check, true));
                Run("Emotion, memory and optional photo forms", () => RemainingFormsUiValidation.Run(report, Check, true));
                Run("Legacy history, experience, plans and health", () => RemainingHistoryUiValidation.Run(report, Check, true));
                foreach (string method in new[] { "SettingsMissionControls", "SettingsTestCoins", "LearnedRecommendations", "CourseSelections", "HistoryAndReview" })
                {
                    string target = method;
                    Run("Existing regression: " + target, () => typeof(FocusMissionValidation).GetMethod(target,
                        BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, new object[] { report, false }));
                }
                foreach (string method in new[] { "HeartGraph", "HeartRefreshUi" })
                {
                    string target = method;
                    Run("Existing health regression: " + target, () => typeof(LocalLoopValidation).GetMethod(target,
                        BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, null));
                }
            }
            finally
            {
                debugScope.Dispose();
                report.checks.Add(new FocusMissionValidation.CheckResult { name = "assertions", status = "info", detail = assertions.ToString() });
                File.WriteAllText(Path.Combine(output, "report.json"), JsonUtility.ToJson(report, true));
                Environment.SetEnvironmentVariable("CAPSTONE_ARTIFACTS", previous);
            }
            Debug.Log("REMAINING_UI_VALIDATION passed=" + report.passed + " failed=" + report.failed + " assertions=" + assertions);
            if (report.failed != 0) throw new InvalidOperationException("Remaining UI validation failed; inspect report.json.");
        }

        static void Run(string name, Action action)
        {
            try { action(); report.passed++; report.checks.Add(new FocusMissionValidation.CheckResult { name = name, status = "pass" }); }
            catch (Exception error) { report.failed++; report.checks.Add(new FocusMissionValidation.CheckResult { name = name, status = "fail", detail = error.ToString() }); }
        }
        static void Check(bool value, string message) { assertions++; if (!value) throw new InvalidOperationException(message); }
        static string Saved(FocusMissionValidation.Fixture f) => StateCodec.Encode(f.Service.Snapshot);
        static Button Button(FocusMissionValidation.Fixture f, string name) => f.Ui.GetComponentsInChildren<Button>(false).Single(b => b.name == name);

        static void Courses(Vector2Int size)
        {
            using (var f = new FocusMissionValidation.Fixture(report, true))
            {
                f.Resize(size.x, size.y);
                string before = Saved(f);
                f.Ui.ShowFocusProgramSelection(); CheckPage(f, size);
                Check(f.Ui.GetComponentsInChildren<Button>().Count(b => b.name.StartsWith("Focus.SelectProgram.")) == 3, "All three course options remain available.");
                f.Capture("remaining-courses-" + size, true);
                f.Click("Focus.SelectProgram.4w"); CheckPage(f, size);
                Check(Saved(f) == before, "Browsing course options and confirmation writes no progress or wallet.");
                f.Capture("remaining-course-confirm-" + size, true);
                f.Store.FailNextSaves = 1; f.Click("Focus.ConfirmProgram.4w"); CheckPage(f, size);
                Check(Saved(f) == before && f.Ui.CurrentScreen == "focus-program-confirm", "A failed course change preserves the old course and review screen.");
                var notice = f.Ui.GetComponentsInChildren<Text>(false).Single(t => t.name == "Focus.SaveNotice");
                Check(notice.gameObject.activeSelf && !string.IsNullOrEmpty(notice.text), "Failed selection gives a visible retry message.");
                f.Capture("remaining-course-retry-" + size, true);
                f.Click("Focus.ConfirmProgram.4w");
                Check(f.Service.Snapshot.MbctProgramId == "4w" && f.Ui.CurrentScreen == "home", "Retry selects the intended course and returns to missions.");

                Check(f.Service.BeginFocusSession("remaining-pending", "P01", 1), f.Service.LastError);
                before = Saved(f);
                f.Ui.ShowFocusProgramConfirmation("8w"); CheckPage(f, size);
                Check(!Button(f, "Focus.ConfirmProgram.8w").interactable, "Unfinished practice keeps course change blocked.");
                Check(f.Has("진행하던 기본 미션 이어가기"), "Blocked selection provides the existing practice destination.");
                f.Capture("remaining-course-pending-" + size, true);
                f.Ui.ShowFocusRecord("remaining-pending"); CheckPage(f, size);
                Check(f.Has("저장한 단계 이어서 하기") && f.Has("Focus.RecordBack"), "A saved record keeps both resume and return visible.");
                f.Capture("remaining-record-" + size, true);
                var layout = f.Ui.GetComponentInChildren<GardenPageLayout>();
                layout.Content.anchoredPosition = new Vector2(0, 10000); layout.Refresh();
                float maximum = Mathf.Max(0, layout.Content.rect.height - layout.Viewport.rect.height);
                Check(Mathf.Abs(layout.Content.anchoredPosition.y - maximum) < .1f, "Reflow clamps stale content offsets to the readable range.");
                var backBefore = Bounds(layout.Back); var actionBefore = Bounds(layout.Footer);
                layout.Content.anchoredPosition = Vector2.zero; layout.Refresh();
                Check(Bounds(layout.Back) == backBefore && Bounds(layout.Footer) == actionBefore, "Scrolling never moves return or resume controls.");
                f.Ui.ShowFocusHistory(null, 0, "4w"); CheckPage(f, size);
                f.Click("Focus.HistoryBack");
                Check(f.Ui.CurrentScreen == "course" && Saved(f) == before, "Record viewing and return preserve all saved state.");
            }
        }

        static void CheckPage(FocusMissionValidation.Fixture f, Vector2Int size)
        {
            for (int i = 0; i < 3; i++)
            {
                Canvas.ForceUpdateCanvases();
                foreach (var component in f.Ui.GetComponentsInChildren<GardenPageLayout>(false)) component.Refresh();
            }
            var layout = f.Ui.GetComponentInChildren<GardenPageLayout>();
            Check(layout != null && layout.Viewport.rect.height > 80, "The fixed page leaves a usable reading viewport.");
            var screen = new Rect(0, 0, size.x, size.y);
            foreach (var rect in new[] { layout.Back, layout.Footer, layout.Viewport, layout.Navigation }.Where(r => r != null))
                Check(Contains(screen, Bounds(rect)), "Fixed navigation, actions and body remain inside the responsive screen.");
            if (layout.Footer != null)
                Check(Bounds(layout.Viewport).yMin >= Bounds(layout.Footer).yMax - .5f, "Body cannot scroll underneath its action dock.");
            if (layout.Notice != null && layout.Notice.gameObject.activeSelf)
            {
                Check(Contains(screen, Bounds(layout.Notice.rectTransform)), "Error guidance remains onscreen.");
                Check(Bounds(layout.Viewport).yMin >= Bounds(layout.Notice.rectTransform).yMax - .5f, "Error guidance has its own space below the body.");
                if (layout.Footer != null) Check(Bounds(layout.Notice.rectTransform).yMin >= Bounds(layout.Footer).yMax - .5f, "Error guidance cannot cover retry actions.");
            }
        }
        static Rect Bounds(RectTransform rect)
        {
            var canvas = rect.GetComponentInParent<Canvas>();
            var camera = canvas == null || canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
            var corners = new Vector3[4]; rect.GetWorldCorners(corners);
            var a = RectTransformUtility.WorldToScreenPoint(camera, corners[0]); var b = RectTransformUtility.WorldToScreenPoint(camera, corners[2]);
            return Rect.MinMaxRect(a.x, a.y, b.x, b.y);
        }
        static bool Contains(Rect outer, Rect inner) => inner.xMin >= outer.xMin - .5f && inner.xMax <= outer.xMax + .5f
            && inner.yMin >= outer.yMin - .5f && inner.yMax <= outer.yMax + .5f;
    }
}
