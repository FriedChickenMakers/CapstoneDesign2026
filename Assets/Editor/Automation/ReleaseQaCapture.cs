using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using CapstoneDesign.Prototype;
using CapstoneDesign.Runtime;
using CapstoneDesign.Runtime.LocalState;
using TMPro;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;

namespace CapstoneDesign.EditorTools
{
    // Current runtime routes, unlike the old 48-practice compatibility gallery.
    // Real scene/prefabs and page factories; synthetic stores and mission sandbox.
    // Does not claim device touch, keyboard, audio or every branching step coverage.
    public static class ReleaseQaCapture
    {
        sealed class Clock : IClock
        {
            public DateTimeOffset UtcNow { get; set; } = DateTimeOffset.Parse("2026-11-10T12:00:00+09:00");
            public TimeZoneInfo TimeZone => TimeZoneInfo.CreateCustomTimeZone("Synthetic-KST", TimeSpan.FromHours(9), "KST", "KST");
        }
        sealed class Store : IStateStore
        {
            string data;
            public GardenState Load() => data == null ? null : StateCodec.Decode(data);
            public void Save(GardenState value) => data = StateCodec.Encode(value);
        }
        [Serializable] sealed class Report
        {
            public string status = "running", renderer;
            public bool synthetic = true, deviceTested;
            public List<string> checks = new List<string>(), images = new List<string>(), errors = new List<string>();
            public int textRegionsMeasured;
            public List<TextRegion> layoutSuspects = new List<TextRegion>();
        }
        [Serializable] sealed class TextRegion
        {
            public string image, path, renderer, text, visibility, overflow, reason;
            public float width, height, preferredWidth, preferredHeight;
            public bool wraps, intentionalTruncation, parentHasMask;
        }
        static Report report;
        static string output;
        static Vector2Int size;
        static StreamWriter metrics;

        public static void RunBatch()
        {
            string root = Environment.GetEnvironmentVariable("CAPSTONE_ARTIFACTS");
            if (!Application.isBatchMode || string.IsNullOrWhiteSpace(root) || SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
                throw new InvalidOperationException("Use explicit batch mode, a renderer and CAPSTONE_ARTIFACTS.");
            output = Path.Combine(root, "release-ui");
            Directory.CreateDirectory(output);
            report = new Report { renderer = SystemInfo.graphicsDeviceName };
            metrics = new StreamWriter(Path.Combine(output, "text-layout-metrics.jsonl"), false);
            string scope = Environment.GetEnvironmentVariable("CAPSTONE_RELEASE_UI_SCOPE") ?? "all";
            bool Includes(string value) => scope == "all" || scope.Split(',').Contains(value);
            try
            {
                foreach (var viewport in Sizes())
                {
                    size = viewport;
                    if (Includes("pages")) Run("Current page inventory " + size, CapturePages);
                    if (Includes("missions")) Run("All 45 mission entry pages " + size, CaptureMissions);
                    if (Includes("albums") && !Includes("pages")) Run("Plant album capture " + size, CaptureAlbum);
                    if (Includes("course-summary")) Run("Chosen/unselected garden course summary " + size, CaptureCourseSummary);
                }
                if (Includes("navigation")) Run("Navigation geometry and input", () =>
                {
                    var navigation = new FocusMissionValidation.Report { runId = "release-navigation", utc = DateTimeOffset.UtcNow.ToString("O") };
                    NavigationLayoutValidation.Run(navigation, (ok, description) => Require(ok, description), true);
                    report.checks.Add("Navigation real-button raycasts=" + navigation.raycastChecks + "; clicks=" + navigation.buttonClicks);
                });
                if (Includes("settings")) Run("Settings on/off geometry and full scroll", SettingsLayoutValidation.Run);
                report.status = report.errors.Count == 0 ? "pass" : "fail";
            }
            finally
            {
                metrics?.Dispose(); metrics = null;
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                AndroidPlatformBridge.UseMockProvider();
                File.WriteAllText(Path.Combine(output, "report.json"), JsonUtility.ToJson(report, true));
            }
            Debug.Log("RELEASE_UI_CAPTURE " + report.status + " images=" + report.images.Count + " errors=" + report.errors.Count
                + " textRegions=" + report.textRegionsMeasured + " layoutSuspects=" + report.layoutSuspects.Count);
            if (report.errors.Count > 0) throw new InvalidOperationException("Release UI capture failed; see release-ui/report.json.");
        }

        static void Run(string name, Action action)
        {
            try { action(); report.checks.Add(name + ": pass"); }
            catch (Exception error) { report.errors.Add(name + ": " + error); Debug.LogWarning("RELEASE_UI_CAPTURE " + name + "\n" + error); }
        }

        static GardenStateService Scene(out MockupNavigation nav, out WeekOneQuestDemo ui, out Clock clock)
        {
            EditorSceneManager.OpenScene("Assets/Scenes/MockupMain.unity");
            AndroidPlatformBridge.UseMockProvider();
            nav = UnityEngine.Object.FindFirstObjectByType<MockupNavigation>(FindObjectsInactive.Include);
            Require(nav != null, "Integrated navigation missing.");
            ui = nav.activitiesPanel.GetComponent<WeekOneQuestDemo>();
            clock = new Clock();
            var service = new GardenStateService(new Store(), clock, new DemoBalanceConfig { VisitorChancePercent = 0 });
            ui.Initialize(service, true);
            nav.ShowActivities();
            return service;
        }

        static void CaptureAlbum()
        {
            using var service = Scene(out _, out var ui, out _);
            Require(service.SelectMbctProgram("8w"), service.LastError);
            ui.ShowPlantAlbum();
            Capture("plant-album-empty", ui, true);
        }

        static void CaptureCourseSummary()
        {
            using var service = Scene(out _, out var ui, out _);
            Require(!service.Snapshot.MbctProgramChosen, "Course summary fixture must start unselected.");
            void Check(string name, Action show, string expected, bool progress)
            {
                string saved = StateCodec.Encode(service.Snapshot);
                show();
                var labels = ui.GetComponentsInChildren<Text>(false);
                Require(labels.Any(label => label.text.Contains(expected)), "Course summary missing: " + name);
                Require(ui.GetComponentsInChildren<Transform>(false).Any(node => node.name == "Course progress") == progress,
                    "Course progress visibility wrong: " + name);
                if (!service.Snapshot.MbctProgramChosen)
                    Require(!labels.Any(label => label.text.Contains("주 코스")), "Unselected page exposes default course: " + name);
                Capture(name, ui, false);
                Require(StateCodec.Encode(service.Snapshot) == saved, "Course display altered stored data: " + name);
                report.checks.Add(name + ": summary/progress/state preserved");
            }
            Check("shop-wallet-unchosen", ui.ShowShop, service.CoinBalance.ToString("N0") + " 코인", false);
            // The incoming shop now uses a purchase sheet; it no longer duplicates course progress.
            foreach (string program in new[] { "2w", "4w", "8w" })
            {
                Require(service.SelectMbctProgram(program), service.LastError);
                int weeks = MbctPolicy.ProgramWeeks(program), total = FocusMissionPolicy.TotalPracticesFor(program);
                string expected = "코인 0  ·  " + weeks + "주 코스 0 / " + total;
                Check("shop-wallet-" + program, ui.ShowShop, service.CoinBalance.ToString("N0") + " 코인", false);

            }
        }

        static void CapturePages()
        {
            using var service = Scene(out var nav, out var ui, out var clock);
            void Page(string name, Action action) { action(); Capture(name, ui, true); }
            Page("course-chooser", ui.ShowFocusProgramSelection);
            foreach (string program in new[] { "2w", "4w", "8w" })
                Page("course-confirm-" + program, () => ui.ShowFocusProgramConfirmation(program));
            Require(service.SelectMbctProgram("8w"), service.LastError);
            Page("mission-home", ui.ShowHome);
            Page("additional-recommendation", ui.ShowFocusRecommendation);
            Page("records-hub-empty", ui.ShowFocusCourse);
            foreach (string program in new[] { "2w", "4w", "8w" })
                Page("course-progress-" + program, () => ui.ShowFocusProgram(program));
            Page("calendar-empty", ui.ShowDailyEmotionHistory);
            Page("garden-shop", ui.ShowShop);
            Page("plant-album-empty", ui.ShowPlantAlbum);
            Page("learned-recommendations", ui.ShowLearnedPracticeRecommendations);
            Page("recommendation-preferences", ui.ShowRecommendationPreferences);
            Page("record-summary-empty", ui.ShowRecordSummary);
            Page("experience-types", ui.ShowExperienceTypes);
            Page("experience-history-empty", ui.ShowExperienceHistory);
            Page("personal-plan", ui.ShowPersonalPlan);
            Page("discoveries-empty", ui.ShowDiscoveries);
            // Use the actual domain completion commands, not fabricated records.
            const string id = "synthetic-release-completed";
            Require(service.BeginFocusSession(id, "M03") && service.StartSession(id), service.LastError);
            var definition = FocusMissionCatalog.FindForSession(service.Snapshot.Sessions.Single(s => s.SessionId == id));
            for (int step = 0; step < definition.Steps.Length; step++)
            {
                var instruction = definition.Steps[step];
                string answer = instruction.Options.FirstOrDefault() ?? (instruction.RequiredAnswer ? "합성 화면 검토 기록" : null);
                Require(service.ConfirmFocusStep(id, step, answer, answer == null), service.LastError);
            }
            clock.UtcNow = clock.UtcNow.AddMinutes(3);
            Require(service.CompletePracticeSession(id), service.LastError);
            Page("mission-complete", () => ui.ShowFocusCompletion(id));
            Page("mission-record", () => ui.ShowFocusRecord(id));
            const string basicId = "synthetic-release-basic-completed";
            Require(service.BeginFocusSession(basicId, "P01", 1, false, "8w")
                && service.StartGuidedFocusSession(basicId, "코")
                && service.CompleteGuidedFocusPractice(basicId, "코", "", 60)
                && service.CompletePracticeSession(basicId), service.LastError);
            Require(DailyEmotionPolicy.Availability(service.Snapshot) == null, "Daily emotion prerequisites are missing.");
            Page("daily-emotion", () => ui.ShowDailyEmotion());
            Require(ui.CurrentScreen == "daily-emotion", "Daily emotion capture opened a fallback page.");
            Page("records-hub-populated", ui.ShowFocusCourse);
            Page("mission-history", () => ui.ShowFocusHistory());
            Page("calendar-populated", ui.ShowDailyEmotionHistory);
            Require(service.SaveExperience("synthetic-release-journal", "즐거움", "합성 화면 검토용 기록입니다. 정원을 보며 잠시 쉬었어요.", "햇살", "편안함", "숨을 골랐어요."), service.LastError);
            Page("experience-history-populated", ui.ShowExperienceHistory);
            Page("experience-detail", () => ui.ShowExperienceRecord("synthetic-release-journal"));
            var plant = WeeklyPlants.Active(service.Snapshot);
            if (plant != null) Page("plant-memories", () => ui.ShowPlantMemories(plant.Id));
            Page("record-summary-populated", ui.ShowRecordSummary);
            // Garden day/night and the interactive 3D orbit are captured by the
            // dedicated two-camera home renderer and the device QA pass.
            report.checks.Add("Real scene page inventory does not replace home two-camera/device captures.");
        }

        static void CaptureMissions()
        {
            using var gate = new DebugValidationScope(true);
            Require(DebugAccess.Enter(), "Synthetic debug gate could not open.");
            using var service = Scene(out var nav, out var ui, out var clock);
            Require(service.SelectMbctProgram("8w"), service.LastError);
            int count = 0;
            foreach (var mission in FocusMissionCatalog.All)
            {
                bool basic = mission.Role == FocusMissionCatalog.BasicRole;
                string saved = StateCodec.Encode(service.Snapshot);
                string entry = "synthetic-release-entry-" + mission.Code;
                try
                {
                    // The development picker deliberately starts additional
                    // missions at step 1, so it cannot supply their overview.
                    // Use normal domain selection in this disposable store for M.
                    if (basic) ui.StartMissionDebug(mission.Code);
                    else
                    {
                        Require(service.BeginFocusSession(entry, mission.Id), service.LastError);
                        ui.SelectFocusSession(entry);
                    }
                    Require(ui.CurrentScreen == "focus-practice" && (!basic || ui.IsMissionDebugActive), "Mission entry failed: " + mission.Code);
                    Capture("mission-" + mission.Code + "-overview", ui, false);
                    var buttons = ui.GetComponentsInChildren<Button>(false);
                    var next = buttons.FirstOrDefault(button => button.interactable && button.name == "Focus.Action")
                        ?? buttons.FirstOrDefault(button => button.interactable && button.name == "Focus.IconAction");
                    Require(next != null, "Mission entry action missing: " + mission.Code);
                    next.onClick.Invoke();
                    Require(ui.CurrentScreen == "focus-practice", "Mission did not enter its first instruction: " + mission.Code);
                    Capture("mission-" + mission.Code + "-first-instruction", ui, true);
                    count++;
                }
                finally
                {
                    if (basic)
                    {
                        ui.RestoreMissionDebug(false);
                        Require(StateCodec.Encode(service.Snapshot) == saved, "Mission sandbox altered its original garden: " + mission.Code);
                    }
                    else
                    {
                        Require(service.EndParticipation(entry), service.LastError);
                        ui.ShowHome();
                    }
                }
            }
            Require(count == 45, "Mission inventory omitted a mission.");
            report.checks.Add("All 45 current P/M overviews and first instructions captured; disposable state only. P sandbox preserves its caller; M uses ordinary select/start/end commands. Full branching gameplay is covered by dedicated suites, not claimed here.");
        }

        static void Capture(string name, WeekOneQuestDemo ui, bool bottom)
        {
            Require(!ui.GetComponentsInChildren<Text>(false).Any(label => label.gameObject.activeInHierarchy
                && label.text.Contains("저장하지 못했어요")), "Normal gallery route has a save/recovery error banner: " + name);
            string Snapshot() => StateCodec.Encode(ui.Service.Snapshot);
            string before = Snapshot();
            CaptureAt(name + "-top", ui, false);
            if (bottom && ui.GetComponentsInChildren<ScrollRect>(false).Any(scroll => scroll.viewport != null && scroll.content != null && scroll.content.rect.height > scroll.viewport.rect.height + 2))
                CaptureAt(name + "-bottom", ui, true);
            Require(Snapshot() == before, "Rendering changed state: " + name);
        }
        static void CaptureAt(string name, WeekOneQuestDemo ui, bool bottom)
        {
            string file = name + "-" + size.x + "x" + size.y + ".png";
            string path = Path.Combine(output, file);
            LocalLoopPreview.Render(size, path);
            foreach (var scroll in ui.GetComponentsInChildren<ScrollRect>(false))
            {
                if (scroll.viewport == null || scroll.content == null) continue;
                scroll.StopMovement(); scroll.verticalNormalizedPosition = bottom ? 0 : 1;
            }
            // Edit-mode batch captures have no intervening LateUpdate. Album
            // images normally render there, after layout has its final size.
            foreach (var photo in ui.GetComponentsInChildren<GardenPlantPhotoView>(false))
            {
                Require(photo.RenderNow(), "Plant album preview did not render: " + name);
                Require(photo.RenderCount > 0 && photo.GetComponent<RawImage>().texture is RenderTexture texture
                    && texture.IsCreated(), "Plant album preview has no live image: " + name);
            }
            LocalLoopPreview.Render(size, path);
            Require(ui.GetComponentsInChildren<GardenThemeSurface>(false).All(surface => !surface.raycastTarget), "Theme intercepts input: " + name);
            MeasureText(ui, file);
            report.images.Add(file);
        }
        static void MeasureText(WeekOneQuestDemo ui, string image)
        {
            foreach (var label in ui.GetComponentsInChildren<Text>(false))
            {
                if (string.IsNullOrWhiteSpace(label.text) || !label.enabled) continue;
                Measure(label.rectTransform, ui, new TextRegion { image = image, renderer = "UGUI Text", text = label.text,
                    width = label.rectTransform.rect.width, height = label.rectTransform.rect.height,
                    preferredWidth = label.preferredWidth, preferredHeight = label.preferredHeight,
                    wraps = label.horizontalOverflow == HorizontalWrapMode.Wrap,
                    overflow = label.horizontalOverflow + "/" + label.verticalOverflow });
            }
            foreach (var label in ui.GetComponentsInChildren<TMP_Text>(false))
            {
                if (string.IsNullOrWhiteSpace(label.text) || !label.enabled) continue;
                Measure(label.rectTransform, ui, new TextRegion { image = image, renderer = "TMP", text = label.text,
                    width = label.rectTransform.rect.width, height = label.rectTransform.rect.height,
                    preferredWidth = label.preferredWidth, preferredHeight = label.preferredHeight,
                    wraps = label.textWrappingMode != TextWrappingModes.NoWrap && label.textWrappingMode != TextWrappingModes.PreserveWhitespaceNoWrap,
                    intentionalTruncation = label.overflowMode == TextOverflowModes.Ellipsis || label.overflowMode == TextOverflowModes.Truncate,
                    overflow = label.overflowMode.ToString() });
            }
        }
        static void Measure(RectTransform label, WeekOneQuestDemo ui, TextRegion region)
        {
            var names = new List<string>();
            for (var node = (Transform)label; node != null && node != ui.transform; node = node.parent) names.Add(node.name);
            names.Reverse(); region.path = string.Join("/", names);
            if (region.text.Length > 160) region.text = region.text.Substring(0, 160) + "…";
            var frame = (RectTransform)ui.transform;
            Rect original = Bounds(label, frame), visible = original;
            foreach (var mask in label.GetComponentsInParent<RectMask2D>(false))
                visible = Intersection(visible, Bounds((RectTransform)mask.transform, frame));
            foreach (var mask in label.GetComponentsInParent<Mask>(false))
                if (mask.enabled) visible = Intersection(visible, Bounds((RectTransform)mask.transform, frame));
            visible = Intersection(visible, frame.rect);
            region.visibility = visible.width <= .5f || visible.height <= .5f ? "scroll-or-mask-hidden"
                : Contains(visible, original) ? "visible" : "partially-clipped-by-scroll-or-mask";
            if (label.parent is RectTransform parent)
            {
                region.parentHasMask = parent.GetComponent<RectMask2D>() != null || parent.GetComponent<Mask>() != null;
                // A ScrollRect intentionally positions content outside its viewport.
                // Its clipping is recorded above rather than called a layout defect.
                if (!region.parentHasMask && region.visibility != "scroll-or-mask-hidden"
                    && !Contains(parent.rect, Bounds(label, parent))) region.reason = "allocated text rectangle leaves immediate parent";
            }
            if (region.visibility != "scroll-or-mask-hidden" && !region.intentionalTruncation)
            {
                if (region.preferredHeight > region.height + 2) region.reason = Join(region.reason, "preferred text height exceeds allocated height");
                if (!region.wraps && region.preferredWidth > region.width + 2) region.reason = Join(region.reason, "unwrapped text width exceeds allocated width");
            }
            report.textRegionsMeasured++;
            metrics.WriteLine(JsonUtility.ToJson(region));
            if (!string.IsNullOrEmpty(region.reason)) report.layoutSuspects.Add(region);
        }
        static Rect Bounds(RectTransform item, RectTransform relative)
        {
            var corners = new Vector3[4]; item.GetWorldCorners(corners);
            var points = corners.Select(point => (Vector2)relative.InverseTransformPoint(point)).ToArray();
            return Rect.MinMaxRect(points.Min(point => point.x), points.Min(point => point.y), points.Max(point => point.x), points.Max(point => point.y));
        }
        static bool Contains(Rect outer, Rect inner) => inner.xMin >= outer.xMin - 1 && inner.xMax <= outer.xMax + 1
            && inner.yMin >= outer.yMin - 1 && inner.yMax <= outer.yMax + 1;
        static Rect Intersection(Rect a, Rect b)
        {
            float left = Mathf.Max(a.xMin, b.xMin), right = Mathf.Min(a.xMax, b.xMax);
            float bottom = Mathf.Max(a.yMin, b.yMin), top = Mathf.Min(a.yMax, b.yMax);
            return new Rect(left, bottom, Mathf.Max(0, right - left), Mathf.Max(0, top - bottom));
        }
        static string Join(string a, string b) => string.IsNullOrEmpty(a) ? b : a + "; " + b;
        static Vector2Int[] Sizes()
        {
            string input = Environment.GetEnvironmentVariable("CAPSTONE_RELEASE_UI_SIZES") ?? "390x844,1200x2000";
            return input.Split(',').Select(item =>
            {
                var fields = item.Trim().Split('x');
                Require(fields.Length == 2, "Use WIDTHxHEIGHT capture sizes.");
                int width = int.Parse(fields[0], CultureInfo.InvariantCulture), height = int.Parse(fields[1], CultureInfo.InvariantCulture);
                Require(width >= 320 && height >= 320 && width <= 2400 && height <= 2400, "Capture size out of range.");
                return new Vector2Int(width, height);
            }).ToArray();
        }
        static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
    }
}
