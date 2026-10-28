using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using CapstoneDesign.Runtime;
using CapstoneDesign.Runtime.LocalState;
using CapstoneDesign.Prototype;
using TMPro;
using UnityEditor;
using UnityEditor.Compilation;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace CapstoneDesign.EditorTools
{
    /// <summary>
    /// Explicitly requested edit-mode validation, using only synthetic memory state and a preview scene.
    /// Never opens a scene, enters Play mode, changes PlayerPrefs, or reads a user save.
    /// </summary>
    [InitializeOnLoad]
    public static class FocusMissionValidation
    {
        [Serializable] public sealed class Request { public string runId; public bool capture = true; }
        [Serializable] public sealed class CheckResult { public string name, status, detail; }
        [Serializable] public sealed class Report
        {
            public string runId, utc, unityVersion;
            public string environment = "Unity Editor / synthetic memory state / isolated preview scene / scrolled visible button center raycasts and callbacks";
            public int passed, failed, buttonClicks, raycastChecks;
            public List<CheckResult> checks = new List<CheckResult>();
            public List<string> captures = new List<string>();
        }

        static bool running;
        static double nextPoll;
        static readonly List<string> compileErrors = new List<string>();
        static readonly string[] Emotions = {
            "기쁨", "만족감", "설렘", "기대감", "편안함", "안도감", "고마움", "뿌듯함",
            "궁금함", "반가움", "놀람", "무덤덤함", "슬픔", "외로움", "서운함", "아쉬움",
            "불안함", "두려움", "답답함", "막막함", "화남", "짜증", "부끄러움", "미안함"
        };
        static string OutputRoot => Path.Combine(Directory.GetCurrentDirectory(), "artifacts", "focus-validation");
        static string RequestPath => Path.Combine(OutputRoot, "request.json");

        static FocusMissionValidation()
        {
            EditorApplication.update += Poll;
            CompilationPipeline.compilationStarted += _ => compileErrors.Clear();
            CompilationPipeline.assemblyCompilationFinished += (_, messages) => {
                foreach (var message in messages.Where(m => m.type == CompilerMessageType.Error))
                    compileErrors.Add(message.file + ":" + message.line + " " + message.message);
            };
            CompilationPipeline.compilationFinished += _ => {
                if (!File.Exists(RequestPath)) return;
                File.WriteAllText(Path.Combine(OutputRoot, "compilation.json"), JsonUtility.ToJson(
                    new CompilationReport { utc = DateTimeOffset.UtcNow.ToString("O"), errors = compileErrors.ToArray() }, true));
            };
        }

        [Serializable] sealed class CompilationReport { public string utc; public string[] errors; }

        static void Poll()
        {
            if (running || EditorApplication.timeSinceStartup < nextPoll) return;
            nextPoll = EditorApplication.timeSinceStartup + 1;
            if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode
                || !File.Exists(RequestPath)) return;
            Request request;
            try { request = JsonUtility.FromJson<Request>(File.ReadAllText(RequestPath)) ?? new Request(); }
            catch (Exception ex)
            {
                File.Delete(RequestPath);
                var invalid = new Report { runId = "invalid-request", utc = DateTimeOffset.UtcNow.ToString("O"), unityVersion = Application.unityVersion, failed = 1 };
                invalid.checks.Add(new CheckResult { name = "Explicit validation request", status = "fail", detail = ex.Message });
                File.WriteAllText(Path.Combine(OutputRoot, "report.json"), JsonUtility.ToJson(invalid, true));
                Debug.LogError("FOCUS_VALIDATION invalid request: " + ex.Message); return;
            }
            // Consume exactly one explicit request; no retry loop or background modification follows a failure.
            File.Delete(RequestPath);
            if (compileErrors.Count > 0)
            {
                var blocked = new Report { runId = request.runId ?? "requested", utc = DateTimeOffset.UtcNow.ToString("O"), unityVersion = Application.unityVersion, failed = 1 };
                blocked.checks.Add(new CheckResult { name = "Project compilation", status = "fail", detail = string.Join("\n", compileErrors) });
                File.WriteAllText(Path.Combine(OutputRoot, "report.json"), JsonUtility.ToJson(blocked, true));
                return;
            }
            Execute(request);
        }

        [MenuItem("Capstone Mockup/Validate Focus Missions (Synthetic)")]
        public static void Run() => Execute(new Request { runId = "manual-" + DateTimeOffset.UtcNow.ToString("yyyyMMdd-HHmmss") });

        static void Execute(Request request)
        {
            if (running || EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling)
                throw new InvalidOperationException("Run focus validation after compilation, outside Play mode.");
            running = true;
            var report = new Report { runId = request.runId ?? "requested", utc = DateTimeOffset.UtcNow.ToString("O"), unityVersion = Application.unityVersion };
            Directory.CreateDirectory(OutputRoot);
            try
            {
                // A failed URP constructor after a Play-mode assembly reload can leave
                // Blitter resources allocated with no live pipeline. Reclaim only that
                // orphaned state; a running render pipeline must retain its resources.
                if (GraphicsSettings.currentRenderPipeline != null && RenderPipelineManager.currentPipeline == null)
                    Blitter.Cleanup();
                Check(report, "Home, recommendation, course, week and garden navigation", () => Navigation(report, request.capture));
                Check(report, "P23, P10 and P25 sequential visible controls and persisted records", () => BasicFlows(report, request.capture));
                Check(report, "M03 droplet action, 24 emotion choices, optional note and garden return", () => AdditionalFlow(report, request.capture));
                Check(report, "Draft survives close and resume; failed completion preserves input and retries once", () => DraftAndRetry(report, request.capture));
                Check(report, "Completed record inspection and review never grant a second reward", () => HistoryAndReview(report, request.capture));
                Check(report, "Integrated portrait shell hides garden canvas and restores it on return", () => IntegratedShell(report, request.capture));
                Check(report, "Focused-input keyboard geometry keeps entry and done controls visible and separate", KeyboardGeometry);
            }
            finally
            {
                File.WriteAllText(Path.Combine(OutputRoot, "report.json"), JsonUtility.ToJson(report, true));
                running = false;
            }
            Debug.Log("FOCUS_VALIDATION passed=" + report.passed + " failed=" + report.failed + " clicks=" + report.buttonClicks + " raycasts=" + report.raycastChecks
                + " report=" + Path.Combine(OutputRoot, "report.json"));
        }

        static void Check(Report report, string name, Action action)
        {
            try { action(); report.passed++; report.checks.Add(new CheckResult { name = name, status = "pass" }); }
            catch (Exception ex) { report.failed++; report.checks.Add(new CheckResult { name = name, status = "fail", detail = ex.ToString() }); Debug.LogWarning("FOCUS_VALIDATION " + name + "\n" + ex); }
        }

        static void Navigation(Report report, bool capture)
        {
            using (var f = new Fixture(report))
            {
                f.AssertMissionBadge("!");
                f.Capture("01-home", capture);
                f.Click("추천 미션 보기");
                Assert(f.Has("추가 미션 시작하기"), "Recommendation did not expose the additional start action.");
                f.Capture("02-recommendation", capture);
                f.Click("‹");
                f.Click("내 과정");
                Assert(f.Has("미션 창으로 돌아가기"), "Course return action is absent.");
                f.Capture("03-course", capture);
                f.Click("1주");
                Assert(f.Has("내 과정으로 돌아가기"), "Week return action is absent.");
                f.Capture("04-week-one", capture);
                f.Click("내 과정으로 돌아가기");
                f.Click("미션 창으로 돌아가기");
                f.Click("내 과정"); f.Click("2주"); f.Click("미션");
                f.Click("홈");
                Assert(f.Navigation.islandPanel.activeSelf && !f.Ui.gameObject.activeSelf, "Garden tab did not return to the existing navigation panel.");
            }
        }

        static void BasicFlows(Report report, bool capture)
        {
            using (var f = new Fixture(report))
            {
                foreach (var code in new[] { "P23", "P10", "P25" })
                {
                    f.Invoke("ShowHome");
                    f.Click("기본 미션 시작하기");
                    var definition = FocusMissionCatalog.Find(code);
                    Assert(f.Latest.MissionId == definition.Id, "Unexpected basic mission: " + f.Latest.MissionId);
                    f.Capture(code + "-overview", capture);
                    f.Click("시작");
                    f.CompletePractice(definition, capture);
                    f.CompleteReflection(code + " synthetic reflection", "편안함", capture && code == "P23");
                    var completed = f.Latest;
                    Assert(completed.Status == ParticipationState.RewardCommitted, code + " did not persist completion.");
                    Assert(completed.FocusStepRecords.Count(r => !string.IsNullOrEmpty(r.ConfirmedUtc)) == definition.Steps.Length,
                        code + " lost a confirmed step.");
                    Assert(completed.Note == code + " synthetic reflection" && completed.Mood == "편안함", code + " lost reflection.");
                    f.Capture(code + "-completed", capture);
                    f.Click("확인");
                    Assert(f.Ui.CurrentScreen == "home", "Completion confirm did not return to mission home.");
                    f.Clock.UtcNow = f.Clock.UtcNow.AddDays(1);
                    f.Ui.RefreshCycle();
                    Assert(f.Has("기본 미션 시작하기"), "Home did not reopen the basic mission after day rollover without navigation.");
                }
                Assert(FocusMissionPolicy.CompletedCount(f.Service.Snapshot) == 3, "Three scheduled completions were not counted.");
                Assert(f.Service.Snapshot.Nutrient == 15, "Basic reward total differs from 3 × 5.");
                f.Invoke("ShowFocusRecord", f.Latest.SessionId); f.Click("다시 연습하기 · 추가 보상 없음"); f.Click("시작");
                f.CompletePractice(FocusMissionCatalog.Find("P25"), false, true);
                f.CompleteReflection("", "", false);
                Assert(f.Latest.FocusStepRecords.Single(r => r.Key == "message").Skipped, "Think-only action did not preserve optional-input skip.");
                Assert(f.Service.Snapshot.Nutrient == 15, "Think-only review granted another reward.");
            }
        }

        static void AdditionalFlow(Report report, bool capture)
        {
            using (var f = new Fixture(report, true))
            {
                f.Click("추천 미션 보기"); f.Click("추가 미션 시작하기"); f.Click("시작");
                f.Click("준비했어요"); f.ClickBack();
                Assert(f.Latest.InstructionStep == 0, "Practice back did not return to the previous step.");
                f.Click("닫기"); f.Click("추가 미션 이어서 하기");
                Assert(f.Latest.InstructionStep == 0, "Additional resume lost the previous step.");
                f.CompletePractice(FocusMissionCatalog.Find("M03"), capture);
                f.ClickBack(); f.Click("물 마셨어요");
                Assert(f.Has("다음"), "Emotion back and last-step reconfirm did not return to emotion.");
                foreach (var label in Emotions)
                {
                    Assert(f.Has(label), "Missing Figma emotion: " + label);
                    f.Click(label);
                }
                f.Click("미안함"); // Clicking the selected emotion clears it.
                f.Click("다음");
                f.SetInput("synthetic additional note");
                f.Click("미션 완료");
                Assert(f.Latest.Status == ParticipationState.RewardCommitted && string.IsNullOrEmpty(f.Latest.Mood), "Optional emotion/toggle-off was not persisted.");
                Assert(f.Service.Snapshot.Nutrient == 5, "Additional completion did not reward exactly once.");
                f.Capture("M03-completed", capture);
                f.Click("정원으로");
                Assert(f.Navigation.home.homeCanvas.gameObject.activeSelf && !f.Ui.gameObject.activeSelf, "Completion garden action failed.");
                f.Navigation.home.RefreshMissionStatus();
                Assert(f.Navigation.home.NavigationBar.MissionBadge.text == "!", "M03-only completion hid the remaining basic mission on the garden.");
                f.Navigation.ShowActivities(); f.Invoke("ShowHome");
                Assert(f.Has("기본 미션 시작하기") && !f.Has("추천 미션 보기"), "Additional-only completion disabled basic or retained its extra CTA.");
                f.AssertMissionBadge("!");
            }
        }

        static void DraftAndRetry(Report report, bool capture)
        {
            using (var f = new Fixture(report, true))
            {
                f.Click("기본 미션 시작하기"); f.Click("시작"); f.Click("준비했어요");
                string id = f.Latest.SessionId;
                f.Click("닫기");
                Assert(f.Has("기본 미션 이어서 하기"), "Close did not expose resume on home.");
                f.Capture("05-paused-home", capture);
                f.Click("기본 미션 이어서 하기");
                Assert(f.Latest.SessionId == id && f.Latest.InstructionStep == 1, "Resume lost the session or stage.");
                f.CompletePractice(FocusMissionCatalog.Find("P23"), false);
                f.Click("고마움"); f.Click("다음"); f.SetInput("synthetic retained note");
                f.ClickBack();
                Assert(f.Has("고마움"), "Note back did not return to emotion.");
                f.Click("다음");
                Assert(f.Input.text == "synthetic retained note", "Note back erased the draft.");
                f.Store.FailNextSaves = 1;
                f.Click("미션 완료");
                Assert(f.Latest.Status != ParticipationState.RewardCommitted && f.Service.Snapshot.Nutrient == 0,
                    "Failed save partially completed or rewarded the mission.");
                f.Capture("06-save-error", capture);
                if (f.Has("입력 내용 확인하기")) f.Click("입력 내용 확인하기");
                Assert(f.Input != null && f.Input.text == "synthetic retained note", "Failed save erased the note.");
                f.Click("미션 완료");
                Assert(f.Latest.Status == ParticipationState.RewardCommitted && f.Service.Snapshot.Nutrient == 5, "Retry did not commit exactly once.");
                f.Invoke("ShowHome");
                Assert(!f.Has("기본 미션 시작하기") && f.Has("추천 미션 보기"), "Basic-only done home has incorrect CTA state.");
                f.AssertMissionBadge("!");
                f.Click("추천 미션 보기"); f.Click("추가 미션 시작하기"); f.Click("시작");
                f.CompletePractice(FocusMissionCatalog.Find("M03"), false); f.CompleteReflection("", "", false);
                f.Invoke("ShowHome");
                Assert(!f.Has("기본 미션 시작하기") && !f.Has("추천 미션 보기"), "Both-done home retained a start CTA.");
                f.AssertMissionBadge("✓");
                f.Capture("07-both-completed-home", capture);
                f.Click("홈"); f.Navigation.home.RefreshMissionStatus();
                Assert(f.Navigation.home.NavigationBar.MissionBadge.text == "✓", "Garden did not switch to a check when all today's missions were complete.");
                f.Capture("16-garden-all-done", capture); f.Navigation.ShowActivities();
                f.Clock.UtcNow = f.Clock.UtcNow.AddDays(1); f.Ui.RefreshCycle();
                f.AssertMissionBadge("!");
                f.Click("홈"); f.Navigation.home.RefreshMissionStatus();
                Assert(f.Navigation.home.NavigationBar.MissionBadge.text == "!", "Garden kept yesterday's completion check after rollover.");
                f.Capture("17-garden-next-day", capture);
            }
        }

        static void HistoryAndReview(Report report, bool capture)
        {
            using (var f = new Fixture(report))
            {
                f.Click("기본 미션 시작하기"); f.Click("시작");
                f.CompletePractice(FocusMissionCatalog.Find("P23"), false);
                f.CompleteReflection("<size=99>synthetic history detail</size>", "뿌듯함", false);
                f.Invoke("ShowFocusCourse");
                f.Click("저장된 기록 보기");
                f.Capture("08-history", capture);
                f.Click(FocusMissionCatalog.Find("P23").Title);
                var copy = f.VisibleText;
                Assert(copy.Contains("synthetic history detail") && copy.Contains("뿌듯함"), "Saved detail omitted reflection fields.");
                Assert(f.Ui.GetComponentsInChildren<Text>(false).Any(t => t.text.Contains("<size=99>synthetic history detail</size>") && !t.supportRichText),
                    "Saved user markup was interpreted as rich text instead of displayed literally.");
                Assert(copy.Contains("향") && copy.Contains("한입"), "Saved detail omitted step content/choice.");
                f.Capture("09-record-detail", capture);
                f.ScrollBottom(); f.Capture("09b-record-reflection", capture);
                var before = f.Service.Snapshot.Nutrient;
                f.Click("다시 연습하기 · 추가 보상 없음");
                f.Click("시작"); f.CompletePractice(FocusMissionCatalog.Find("P23"), false);
                f.CompleteReflection("synthetic review", "", false);
                Assert(f.Latest.FocusReview && f.Latest.Status == ParticipationState.RewardCommitted, "Review was not saved separately.");
                Assert(f.Service.Snapshot.Nutrient == before && FocusMissionPolicy.CompletedCount(f.Service.Snapshot) == 1,
                    "Review changed course progress or awarded a second reward.");
            }
        }

        static void IntegratedShell(Report report, bool capture)
        {
            using (var f = new Fixture(report, true))
            {
                var panel = (RectTransform)f.Ui.transform;
                Assert(panel.anchorMin == Vector2.zero && panel.anchorMax == Vector2.one && panel.sizeDelta == Vector2.zero,
                    "Integrated mission panel is not full stretch.");
                Assert(panel.parent.name == "Tab design frame", "PreparePresentation did not mount the integrated portrait shell.");
                Assert(!f.Navigation.home.homeCanvas.gameObject.activeSelf, "Garden canvas obscures the mission home.");
                var missionBar = f.Ui.GetComponentInChildren<MissionBottomNavigation>(false);
                Assert(missionBar != null, "Mission home is not using the shared navigation component.");
                var missionGeometry = NavigationGeometry(missionBar);
                f.Capture("12-integrated-shell-home", capture);
                f.Click("기본 미션 시작하기");
                Assert(!f.Navigation.home.homeCanvas.gameObject.activeSelf && !f.Has("내 과정"), "Focused practice retained garden or main tab controls.");
                f.Capture("13-integrated-shell-practice", capture);
                f.Click("닫기"); f.Click("홈");
                Assert(f.Navigation.home.homeCanvas.gameObject.activeSelf && !f.Ui.gameObject.activeSelf, "Garden return did not restore its canvas.");
                var home = f.Navigation.home;
                var gardenBar = home.NavigationBar;
                Assert(gardenBar != null && gardenBar.ActiveTab == "홈", "Garden is not using the shared navigation component with Home selected.");
                Assert(NavigationGeometry(gardenBar).SequenceEqual(missionGeometry), "Garden icon, label or button geometry differs from the shared mission tabs.");
                Assert(gardenBar.HomeButton.GetComponentsInChildren<Text>(false).Any(t => t.text == "홈")
                    && gardenBar.MissionButton.GetComponentsInChildren<Text>(false).Any(t => t.text == "미션")
                    && gardenBar.CourseButton.GetComponentsInChildren<Text>(false).Any(t => t.text == "내 과정"),
                    "Garden bottom destinations do not match the unified three tabs.");
                home.RefreshMissionStatus(); Assert(gardenBar.MissionBadge.text == "!", "Garden does not show outstanding missions.");
                var garden = home.homeCanvas.transform.Find("SafePortraitFrame/MainContent/GardenAndGrowth");
                foreach (var name in new[] { "TodayMissionCard", "TodayMissionCard_Edge", "DailyStatus", "MissionName", "MissionEmblem" })
                    Assert(!garden.Find(name).gameObject.activeSelf, "Removed activity card element is still visible: " + name);
                Assert(home.TemporarySettingsButton != null && home.TemporarySettingsButton.gameObject.activeInHierarchy,
                    "Garden top-right temporary settings action is missing.");
                var settings = home.TemporarySettingsButton;
                home.ConfigureNavigation(); home.ConfigureNavigation();
                Assert(home.TemporarySettingsButton == settings, "Repeated navigation setup duplicated the temporary settings button.");
                f.Capture("14-garden-navigation", capture);
                var homeContent = (RectTransform)home.homeCanvas.transform.Find("SafePortraitFrame/MainContent");
                var settingsBounds = RectTransformUtility.CalculateRelativeRectTransformBounds(homeContent, settings.transform);
                Assert(settingsBounds.center.x > homeContent.rect.center.x && settingsBounds.center.y > homeContent.rect.center.y
                    && settingsBounds.max.x <= homeContent.rect.xMax + 1 && settingsBounds.max.y <= homeContent.rect.yMax + 1,
                    "Temporary settings entry is not contained in the garden's top-right corner.");
                var dateBounds = RectTransformUtility.CalculateRelativeRectTransformBounds(homeContent, home.date.transform);
                Assert(dateBounds.max.x <= settingsBounds.min.x - 8, "Garden date overlaps the temporary settings action.");
                f.ClickControl(gardenBar.MissionButton);
                Assert(!f.Navigation.home.homeCanvas.gameObject.activeSelf && f.Ui.gameObject.activeSelf, "Reopening missions retained the garden overlay.");
                Assert(f.Ui.CurrentScreen == "home" && !settings.gameObject.activeInHierarchy, "Garden mission tab opened the wrong destination or retained settings.");
                f.Click("홈"); f.ClickControl(gardenBar.CourseButton);
                Assert(f.Ui.CurrentScreen == "course" && !f.Navigation.settingsPanel.activeSelf,
                    "Garden third tab still opens settings instead of My Course.");
                f.Click("홈"); f.ClickControl(settings);
                Assert(f.Navigation.settingsPanel.activeSelf && !settings.gameObject.activeInHierarchy,
                    "Settings did not open, or its home-only entry remains visible outside the garden.");
                f.Capture("15-settings-navigation", capture);
                f.ClickControl(gardenBar.CourseButton);
                Assert(f.Ui.CurrentScreen == "course" && !f.Navigation.settingsPanel.activeSelf,
                    "The third bottom tab on settings did not open My Course.");
                f.Click("홈"); f.ClickControl(gardenBar.HomeButton);
                Assert(home.homeCanvas.gameObject.activeSelf && settings.gameObject.activeInHierarchy,
                    "Home tab did not restore the garden and its settings entry.");
            }
        }

        static Vector4[] NavigationGeometry(MissionBottomNavigation bar)
        {
            var result = new List<Vector4>();
            foreach (var button in new[] { bar.HomeButton, bar.MissionButton, bar.CourseButton })
            {
                var rect = (RectTransform)button.transform;
                result.Add(new Vector4(rect.anchoredPosition.x, rect.anchoredPosition.y, rect.sizeDelta.x, rect.sizeDelta.y));
                var icon = button.GetComponentInChildren<RawImage>(false);
                var label = button.GetComponentsInChildren<Text>(false).Single(t => t.text == "홈" || t.text == "미션" || t.text == "내 과정");
                Assert(icon != null && icon.rectTransform.anchoredPosition.y > label.rectTransform.anchoredPosition.y,
                    "Tab icon must sit above its label.");
                result.Add(new Vector4(icon.rectTransform.anchoredPosition.x, icon.rectTransform.anchoredPosition.y, icon.rectTransform.sizeDelta.x, icon.rectTransform.sizeDelta.y));
                result.Add(new Vector4(label.rectTransform.anchoredPosition.x, label.rectTransform.anchoredPosition.y, label.rectTransform.sizeDelta.x, label.rectTransform.sizeDelta.y));
            }
            return result.ToArray();
        }

        static void KeyboardGeometry()
        {
            foreach (float height in new[] { 200f, 320f, 480f, 650f })
            foreach (var input in new[] { new Vector2(558, 60), new Vector2(306, 164), new Vector2(430, 164) })
            {
                var layout = FocusMissionKeyboardAvoidance.CalculateLayout(height, input.x, input.y);
                Assert(layout.Input.yMin >= 0 && layout.Input.height >= 60 && layout.Input.height <= input.y
                    && layout.Input.yMax <= height, "Keyboard obscures or collapses the editable input at available height " + height);
                Assert(layout.Done.yMin >= layout.Input.yMax && layout.Done.height >= 24 && layout.Done.height <= 52
                    && layout.Done.yMax <= height, "Keyboard done action overlaps the input or leaves the visible region at height " + height);
            }
        }

        static void Assert(bool value, string message) { if (!value) throw new InvalidOperationException(message); }

        sealed class MemoryStore : IStateStore
        {
            string data;
            public int FailNextSaves;
            public GardenState Load() => data == null ? null : StateCodec.Decode(data);
            public void Save(GardenState state)
            {
                if (FailNextSaves-- > 0) throw new IOException("Synthetic one-shot storage failure");
                FailNextSaves = 0; data = StateCodec.Encode(state);
            }
        }
        sealed class SyntheticClock : IClock
        {
            public DateTimeOffset UtcNow { get; set; } = DateTimeOffset.Parse("2026-10-26T03:00:00Z");
            public TimeZoneInfo TimeZone => TimeZoneInfo.CreateCustomTimeZone("FocusValidation-Seoul", TimeSpan.FromHours(9), "Synthetic Seoul", "Synthetic Seoul");
        }

        sealed class Fixture : IDisposable
        {
            public readonly MemoryStore Store = new MemoryStore();
            public readonly SyntheticClock Clock = new SyntheticClock();
            public readonly GardenStateService Service;
            public readonly WeekOneQuestDemo Ui;
            public readonly MockupNavigation Navigation;
            readonly Report report;
            readonly Scene scene, previous;
            readonly HashSet<int> previousRoots;
            readonly GameObject root;
            readonly Canvas canvas;
            readonly GraphicRaycaster raycaster;
            readonly EventSystem pointerContext;
            readonly RenderTexture validationTarget;
            readonly Camera camera, existingCamera;
            readonly Light existingLight;
            readonly float lightIntensity;
            readonly Vector3 cameraPosition;
            readonly Color ambient;
            public ActivitySession Latest => Service.Snapshot.Sessions.Last(s => FocusMissionPolicy.IsFocus(s));
            public InputField Input => Ui.GetComponentsInChildren<InputField>(false).FirstOrDefault(i => i.gameObject.activeInHierarchy);
            public string VisibleText => string.Join("\n", Ui.GetComponentsInChildren<Text>(false).Where(t => t.gameObject.activeInHierarchy).Select(t => t.text));

            public Fixture(Report report, bool integratedShell = false)
            {
                this.report = report;
                previous = SceneManager.GetActiveScene();
                previousRoots = new HashSet<int>(previous.GetRootGameObjects().Select(o => o.GetInstanceID()));
                ambient = RenderSettings.ambientLight;
                existingCamera = GameObject.Find("MockupCamera")?.GetComponent<Camera>();
                existingLight = GameObject.Find("MoonLight")?.GetComponent<Light>();
                if (existingCamera != null) cameraPosition = existingCamera.transform.position;
                if (existingLight != null) lightIntensity = existingLight.intensity;
                scene = EditorSceneManager.NewPreviewScene();
                root = new GameObject("Focus validation synthetic root", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
                root.SetActive(false); SceneManager.MoveGameObjectToScene(root, scene);
                canvas = root.GetComponent<Canvas>();
                raycaster = root.GetComponent<GraphicRaycaster>();
                var pointerObject = new GameObject("Synthetic pointer context"); pointerObject.transform.SetParent(root.transform, false);
                pointerContext = pointerObject.AddComponent<EventSystem>(); pointerContext.enabled = false;
                var scaler = root.GetComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(390, 844); scaler.matchWidthOrHeight = .5f;
                var cameraObject = new GameObject("Focus validation synthetic camera", typeof(Camera));
                SceneManager.MoveGameObjectToScene(cameraObject, scene);
                camera = cameraObject.GetComponent<Camera>(); camera.enabled = false; camera.scene = scene;
                camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Color.white;
                camera.orthographic = true; camera.orthographicSize = 5; camera.nearClipPlane = .1f; camera.farClipPlane = 100;
                camera.transform.position = new Vector3(0, 0, -10);
                validationTarget = new RenderTexture(780, 1688, 24);
                camera.targetTexture = validationTarget; camera.aspect = 780f / 1688;
                canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera; canvas.planeDistance = 1;
                Navigation = root.AddComponent<MockupNavigation>();
                Navigation.islandPanel = GardenUi.Box(root.transform, "Synthetic unchanged garden destination", 0, 0, 1, 1);
                Navigation.settingsPanel = GardenUi.Box(root.transform, "Synthetic settings", 0, 0, 1, 1);
                Navigation.activitiesPanel = GardenUi.Box(root.transform, "Synthetic mission panel", 0, 0, 1, 1);
                if (integratedShell)
                {
                    // Model the existing scene's real non-null presenter branch without loading its save or garden assets.
                    Navigation.home = root.AddComponent<GardenHomePresenter>(); Navigation.home.navigation = Navigation;
                    var gardenCanvas = new GameObject("Synthetic preserved garden canvas", typeof(RectTransform), typeof(Canvas));
                    gardenCanvas.transform.SetParent(root.transform, false); Navigation.home.homeCanvas = gardenCanvas.GetComponent<Canvas>();
                    var gardenRect = (RectTransform)gardenCanvas.transform; gardenRect.anchorMin = Vector2.zero; gardenRect.anchorMax = Vector2.one; gardenRect.offsetMin = gardenRect.offsetMax = Vector2.zero;
                    var safeFrame = GardenUi.Box(gardenCanvas.transform, "SafePortraitFrame", 0, 0, 1, 1);
                    var homeContent = GardenUi.Box(safeFrame.transform, "MainContent", 0, 0, 1, 1);
                    var brand = HomeText(homeContent.transform, "Brand", "마음 정원");
                    brand.rectTransform.anchorMin = brand.rectTransform.anchorMax = new Vector2(0, 1);
                    brand.rectTransform.pivot = new Vector2(0, 1); brand.rectTransform.anchoredPosition = new Vector2(48, -32);
                    brand.rectTransform.sizeDelta = new Vector2(280, 48);
                    Navigation.home.date = HomeText(homeContent.transform, "Date", "10월 29일");
                    Navigation.home.date.rectTransform.anchorMin = Navigation.home.date.rectTransform.anchorMax = new Vector2(1, 1);
                    Navigation.home.date.rectTransform.pivot = new Vector2(1, 1); Navigation.home.date.rectTransform.anchoredPosition = new Vector2(-48, -32);
                    Navigation.home.date.rectTransform.sizeDelta = new Vector2(200, 48); Navigation.home.date.alignment = TextAlignmentOptions.Right;
                    Navigation.home.homeTab = HomeTab(homeContent.transform, "HomeTab", 0, out Navigation.home.homeTabLabel);
                    Navigation.home.activityTab = HomeTab(homeContent.transform, "ActivityTab", 1, out Navigation.home.activityTabLabel);
                    Navigation.home.settingsTab = HomeTab(homeContent.transform, "SettingsTab", 2, out Navigation.home.settingsTabLabel);
                    GardenUi.Box(homeContent.transform, "BottomNavigation", 0, 0, 1, .1f, Color.white);
                    var garden = GardenUi.Box(homeContent.transform, "GardenAndGrowth", 0, .1f, 1, .8f);
                    GardenUi.Box(garden.transform, "TodayMissionCard", .05f, .1f, .9f, .1f, Color.white);
                    GardenUi.Box(garden.transform, "TodayMissionCard_Edge", .05f, .1f, .9f, .01f, Color.white);
                    Navigation.home.dailyStatus = HomeText(garden.transform, "DailyStatus", "오늘 완료한 활동");
                    Navigation.home.missionName = HomeText(garden.transform, "MissionName", "Synthetic old activity");
                    GardenUi.Box(garden.transform, "MissionEmblem", .8f, .1f, .1f, .1f, Color.white);
                    Navigation.home.ConfigureNavigation();
                    Navigation.legacyBottomNavigation = GardenUi.Box(root.transform, "Synthetic legacy navigation", 0, 0, 1, .1f);
                }
                var balance = new DemoBalanceConfig { CompletionNutrient = 5, VisitorChancePercent = 0 };
                Service = new GardenStateService(Store, Clock, balance, new LegacySnapshot());
                try
                {
                    Ui = Navigation.activitiesPanel.AddComponent<WeekOneQuestDemo>();
                    Ui.Balance = balance; Ui.Initialize(Service, true);
                    if (Navigation.home != null) Navigation.home.activity = Ui;
                    root.SetActive(true); Navigation.ShowActivities(); Invoke("ShowHome");
                    Assert(ReferenceEquals(Ui.Service, Service), "Fixture did not retain its memory-only service.");
                }
                catch { Dispose(); throw; }
            }

            public void Invoke(string method, params object[] args)
            {
                var target = typeof(WeekOneQuestDemo).GetMethods(BindingFlags.Instance | BindingFlags.Public)
                    .FirstOrDefault(m => m.Name == method && m.GetParameters().Length >= args.Length
                        && m.GetParameters().Skip(args.Length).All(p => p.IsOptional));
                Assert(target != null, "Missing public navigation contract: " + method);
                var parameters = target.GetParameters();
                var full = parameters.Select((p, i) => i < args.Length ? args[i] : p.DefaultValue).ToArray();
                target.Invoke(Ui, full); Layout();
            }

            public bool Has(string label) => Button(label) != null;
            public void AssertMissionBadge(string expected)
            {
                var bar = Ui.GetComponentInChildren<MissionBottomNavigation>(false);
                Assert(bar != null && bar.MissionBadge != null && bar.MissionBadge.gameObject.activeInHierarchy
                    && bar.MissionBadge.text == expected, "Mission status badge differs from today's outstanding work: " + expected);
            }
            static TMP_Text HomeText(Transform parent, string name, string text)
            {
                var caption = GardenUi.Box(parent, name, 0, 0, 1, 1).AddComponent<TextMeshProUGUI>();
                GardenTypography.ApplyBody(caption); caption.text = text; caption.fontSize = 28; caption.color = GardenUi.Ink;
                caption.raycastTarget = false; return caption;
            }
            static Button HomeTab(Transform parent, string name, int index, out TMP_Text label)
            {
                var box = GardenUi.Box(parent, name, .04f + index * .32f, .02f, .28f, .06f, Color.white);
                var button = box.AddComponent<Button>(); button.targetGraphic = box.GetComponent<Image>();
                var caption = GardenUi.Box(box.transform, "Label", 0, 0, 1, 1).AddComponent<TextMeshProUGUI>();
                GardenTypography.ApplyBody(caption); caption.fontSize = 22; caption.alignment = TextAlignmentOptions.Center;
                caption.raycastTarget = false; label = caption; return button;
            }
            public void ClickControl(Button button)
            {
                Assert(button != null && button.gameObject.activeInHierarchy && button.interactable, "Navigation control is not visible and enabled.");
                Reveal(button); AssertRaycastTarget(button); button.onClick.Invoke(); report.buttonClicks++; Layout();
            }
            Button Button(string label) => Ui.GetComponentsInChildren<Button>(false).FirstOrDefault(b =>
                b.gameObject.activeInHierarchy && b.interactable && (b.name == label
                    || b.GetComponentsInChildren<Text>(false).Any(t => t.text.Trim() == label)));
            public void Click(string label)
            {
                var button = Button(label);
                Assert(button != null, "Visible enabled button missing: " + label + " on " + Ui.CurrentScreen + "\n" + VisibleText);
                Reveal(button);
                AssertRaycastTarget(button);
                button.onClick.Invoke(); report.buttonClicks++; Layout();
            }
            void Reveal(Button button)
            {
                // Scroll controls into view before checking the actual uGUI target and callback.
                var scroll = button.GetComponentInParent<ScrollRect>();
                if (scroll == null || scroll.content == null || scroll.viewport == null) return;
                Layout();
                var rect = (RectTransform)button.transform;
                Vector3 center = scroll.content.InverseTransformPoint(rect.TransformPoint(rect.rect.center));
                float y = Mathf.Clamp(-center.y - scroll.viewport.rect.height / 2, 0,
                    Mathf.Max(0, scroll.content.rect.height - scroll.viewport.rect.height));
                scroll.content.anchoredPosition = new Vector2(scroll.content.anchoredPosition.x, y);
                scroll.velocity = Vector2.zero; Layout();
            }
            void AssertRaycastTarget(Button button)
            {
                Layout();
                foreach (var graphic in root.GetComponentsInChildren<Graphic>(false))
                {
                    // Graphic lifecycle is not always dispatched in an edit-mode preview scene.
                    // Register only this fixture's graphics; the user's canvases are untouched.
                    GraphicRegistry.RegisterGraphicForCanvas(canvas, graphic);
                    graphic.SetAllDirty(); graphic.Rebuild(CanvasUpdate.PreRender); graphic.Rebuild(CanvasUpdate.LatePreRender);
                }
                Canvas.ForceUpdateCanvases(); camera.Render();
                var rect = (RectTransform)button.transform;
                var point = RectTransformUtility.WorldToScreenPoint(camera, rect.TransformPoint(rect.rect.center));
                var hits = new List<RaycastResult>();
                // Query only the isolated raycaster, without changing EventSystem.current or the user's input modules.
                raycaster.Raycast(new PointerEventData(pointerContext) { position = point }, hits);
                var hit = hits.OrderByDescending(item => item.depth).FirstOrDefault();
                Assert(hit.gameObject != null && hit.gameObject.GetComponentInParent<Button>() == button,
                    "Button center is blocked or not hittable: " + button.name + " on " + Ui.CurrentScreen
                    + "; first hit=" + (hit.gameObject == null ? "none" : hit.gameObject.name));
                report.raycastChecks++;
            }
            public void ClickBack()
            {
                foreach (var label in new[] { "이전", "뒤로", "돌아가기" }) if (Has(label)) { Click(label); return; }
                var back = Ui.GetComponentsInChildren<Button>(false).FirstOrDefault(b => b.interactable
                    && (b.name.ToLowerInvariant().Contains("back") || b.name.Contains("이전")));
                Assert(back != null, "Visible back button was not found."); Reveal(back); AssertRaycastTarget(back); back.onClick.Invoke(); report.buttonClicks++; Layout();
            }
            public void SetInput(string text) { Assert(Input != null, "Visible input missing on " + Ui.CurrentScreen); Input.text = text; Input.onValueChanged.Invoke(text); }
            public void ScrollBottom()
            {
                foreach (var scroll in Ui.GetComponentsInChildren<ScrollRect>(false)) scroll.verticalNormalizedPosition = 0;
                Layout();
            }
            public void CompletePractice(FocusMissionDefinition definition, bool capture, bool thinkOnly = false)
            {
                int limit = 0;
                while (Latest.InstructionStep < definition.Steps.Length)
                {
                    Assert(++limit <= definition.Steps.Length + 1, "Practice did not advance.");
                    int index = Latest.InstructionStep; var step = definition.Steps[index];
                    if (step.OptionalInput && !thinkOnly)
                    {
                        SetInput("synthetic message before speaking");
                        Click("닫기"); Click("기본 미션 이어서 하기");
                        Assert(Latest.InstructionStep == index && Input != null && Input.text == "synthetic message before speaking",
                            "Optional practice draft did not survive close/resume.");
                    }
                    if (step.Options.Length > 0) Click(step.Options[0]);
                    Capture(definition.Code + "-step-" + (index + 1), capture);
                    Click(step.OptionalInput && thinkOnly ? "속으로 확인했어요" : step.ActionLabel);
                    Assert(Latest.InstructionStep > index, "Practice action did not advance: " + definition.Code + " " + step.Key);
                }
                Assert(Has("다음"), "Practice did not lead to shared emotion.");
            }
            public void CompleteReflection(string value, string mood, bool capture)
            {
                if (!string.IsNullOrEmpty(mood)) Click(mood);
                Capture("10-shared-emotion", capture); Click("다음");
                SetInput(value); Capture("11-shared-note", capture); Click("미션 완료");
            }
            void Layout()
            {
                Canvas.ForceUpdateCanvases();
                foreach (var layout in root.GetComponentsInChildren<RectTransform>(false)) LayoutRebuilder.ForceRebuildLayoutImmediate(layout);
                Canvas.ForceUpdateCanvases();
            }
            public void Capture(string name, bool enabled)
            {
                if (!enabled) return;
                string path = Path.Combine(OutputRoot, name + ".png");
                const int width = 780, height = 1688;
                var target = new RenderTexture(width, height, 24);
                var texture = new Texture2D(width, height, TextureFormat.RGB24, false);
                var previousTarget = RenderTexture.active;
                var previousCameraTarget = camera.targetTexture;
                try
                {
                    camera.targetTexture = target; camera.aspect = (float)width / height;
                    for (int pass = 0; pass < 3; pass++)
                    {
                        Layout();
                        foreach (var text in root.GetComponentsInChildren<Text>(false))
                        { text.font?.RequestCharactersInTexture(text.text, text.fontSize, text.fontStyle); text.cachedTextGenerator.Invalidate(); text.SetAllDirty(); }
                        foreach (var graphic in root.GetComponentsInChildren<Graphic>(false))
                        { graphic.SetAllDirty(); graphic.Rebuild(CanvasUpdate.PreRender); graphic.Rebuild(CanvasUpdate.LatePreRender); }
                        Canvas.ForceUpdateCanvases(); camera.Render();
                    }
                    RenderTexture.active = target; texture.ReadPixels(new Rect(0, 0, width, height), 0, 0); texture.Apply();
                    File.WriteAllBytes(path, texture.EncodeToPNG());
                    int minimum = 765, maximum = 0;
                    var pixels = texture.GetPixels32();
                    for (int index = 0; index < pixels.Length; index += 31)
                    {
                        var pixel = pixels[index]; int brightness = pixel.r + pixel.g + pixel.b;
                        minimum = Math.Min(minimum, brightness); maximum = Math.Max(maximum, brightness);
                    }
                    Assert(maximum - minimum >= 90, "Preview has no visible UI contrast; render validation failed: " + name);
                    report.captures.Add(path);
                }
                finally
                {
                    camera.targetTexture = previousCameraTarget; RenderTexture.active = previousTarget;
                    target.Release(); UnityEngine.Object.DestroyImmediate(target); UnityEngine.Object.DestroyImmediate(texture);
                }
            }
            public void Dispose()
            {
                if (root != null)
                    foreach (var graphic in root.GetComponentsInChildren<Graphic>(true)) GraphicRegistry.UnregisterGraphicForCanvas(canvas, graphic);
                if (camera != null) camera.targetTexture = null;
                if (validationTarget != null) { validationTarget.Release(); UnityEngine.Object.DestroyImmediate(validationTarget); }
                if (previous.IsValid() && previous.isLoaded)
                    foreach (var item in previous.GetRootGameObjects())
                        if (!previousRoots.Contains(item.GetInstanceID())) SceneManager.MoveGameObjectToScene(item, scene);
                if (scene.IsValid()) EditorSceneManager.ClosePreviewScene(scene);
                Service?.Dispose();
                if (previous.IsValid() && previous.isLoaded) SceneManager.SetActiveScene(previous);
                RenderSettings.ambientLight = ambient;
                if (existingCamera != null) existingCamera.transform.position = cameraPosition;
                if (existingLight != null) existingLight.intensity = lightIntensity;
            }
        }
    }
}
