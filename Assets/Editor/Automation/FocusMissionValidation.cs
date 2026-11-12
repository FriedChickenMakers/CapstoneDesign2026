using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using CapstoneDesign.Runtime;
using CapstoneDesign.Runtime.LocalState;
using CapstoneDesign.Prototype;
using CapstoneDesign.Plants;
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
        static string OutputRoot
        {
            get
            {
                string requested = Environment.GetEnvironmentVariable("CAPSTONE_ARTIFACTS");
                return string.IsNullOrWhiteSpace(requested)
                    ? Path.Combine(Directory.GetCurrentDirectory(), "artifacts", "focus-validation")
                    : Path.Combine(Path.GetFullPath(requested), "focus-validation");
            }
        }
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
                Check(report, "P04, P01 and P03 sequential visible controls and persisted records", () => BasicFlows(report, request.capture));
                Check(report, "M03 droplet action, 24 emotion choices, optional note and garden return", () => AdditionalFlow(report, request.capture));
                Check(report, "Draft survives close and resume; failed completion preserves input and retries once", () => DraftAndRetry(report, request.capture));
                Check(report, "Completed record inspection and review never grant a second reward", () => HistoryAndReview(report, request.capture));
                Check(report, "Integrated portrait shell hides garden canvas and restores it on return", () => IntegratedShell(report, request.capture));
                Check(report, "Fresh missions start available and weekly rest never appears as today's completion", () => InitialAndWeeklyState(report, request.capture));
                Check(report, "Settings reset mission records atomically and advance only the app mission date", () => SettingsMissionControls(report, request.capture));
                Check(report, "Development settings add exactly 100 coins per click and preserve all other saved data", () => SettingsTestCoins(report, request.capture));
                Check(report, "Focused-input keyboard geometry keeps entry and done controls visible and separate", KeyboardGeometry);
                Check(report, "Learned MBCT recommendation settings and selected candidate remain reachable", () => LearnedRecommendations(report, request.capture));
                Check(report, "Garden shop wallet and linked history preserve Focus and legacy meanings", () => GardenProgressAndRecordTitles(report, request.capture));
                Check(report, "2/4/8 week course selection preserves its independent progress", () => CourseSelections(report, request.capture));
                Check(report, "All 45 missions retain overview, interaction, completion and restart records", () => AllMissionFlows(report, request.capture));
            }
            finally
            {
                File.WriteAllText(Path.Combine(OutputRoot, "report.json"), JsonUtility.ToJson(report, true));
                running = false;
            }
            Debug.Log("FOCUS_VALIDATION passed=" + report.passed + " failed=" + report.failed + " clicks=" + report.buttonClicks + " raycasts=" + report.raycastChecks
                + " report=" + Path.Combine(OutputRoot, "report.json"));
            // A report with failures must also fail -executeMethod/CI. The report
            // is written first so failures remain inspectable in the fresh run.
            if (report.failed > 0) throw new InvalidOperationException("Focus validation failed: " + report.failed + " checks. See report.json.");
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
                // The compact mobile viewport guarantees actual overflow; the
                // simplified home intentionally fits without scrolling on tall phones.
                f.Resize(360, 640); f.Ui.ShowHome();
                Assert(f.Service.Snapshot.Sessions.Count == 0 && FocusMissionPolicy.CompletedCount(f.Service.Snapshot) == 0,
                    "A fresh preview contains a saved mission or completion.");
                Assert(f.Has("기본 미션 시작하기") && f.Has("추천 미션 보기"), "Fresh basic or additional mission starts unavailable.");
                Assert(!f.VisibleText.Contains("오늘 완료했어요"), "A fresh mission is labeled completed.");
                f.AssertMissionBadge("!");
                f.AssertScrollStartsAtTop("기본 미션");
                f.ScrollToBottomViaBar();
                f.Invoke("ShowHome");
                f.AssertScrollStartsAtTop("기본 미션");
                f.Click("기록");
                f.AssertScrollStartsAtTop("Focus.CourseRecords.2w");
                f.ScrollToBottomViaBar();
                f.Invoke("ShowFocusCourse");
                f.AssertScrollStartsAtTop("Focus.CourseRecords.2w");
                f.Click("미션");
                f.AssertScrollStartsAtTop("기본 미션");
                Assert(f.Ui.GetComponentsInChildren<GardenThemeSurface>(false).Any(surface => surface.style == GardenThemeSurface.Style.Backdrop),
                    "The new mission home lost the shared garden theme background.");
                Assert(f.Ui.GetComponentsInChildren<GardenThemeSurface>(false).Any(surface => surface.style == GardenThemeSurface.Style.Primary),
                    "The new mission home lost its garden theme emphasis surface.");
                f.Capture("01-home", capture);
                f.Click("추천 미션 보기");
                Assert(f.Has("추가 미션 시작하기"), "Recommendation did not expose the additional start action.");
                f.Capture("02-recommendation", capture);
                f.Click("미션 창으로 돌아가기");
                f.Click("기록");
                Assert(f.Has("미션 창으로 돌아가기"), "Course return action is absent.");
                Assert(f.VisibleText.Contains("완료한 실습 0 / 12"), "A fresh course does not start at zero completed practices.");
                f.Capture("03-course", capture);
                f.Click("Focus.CourseRecords.2w");
                Assert(f.Has("기록으로 돌아가기"), "Week return action is absent.");
                f.Capture("04-week-one", capture);
                f.Click("기록으로 돌아가기");
                f.Click("미션 창으로 돌아가기");
                f.Click("기록"); f.Click("Focus.CourseRecords.2w"); f.Click("미션");
                f.Click("홈");
                Assert(f.Navigation.islandPanel.activeSelf && !f.Ui.gameObject.activeSelf, "Garden tab did not return to the existing navigation panel.");
            }
        }

        static void BasicFlows(Report report, bool capture)
        {
            using (var f = new Fixture(report, true))
            {
                foreach (var code in new[] { "P04", "P01", "P03" })
                {
                    f.Invoke("ShowHome");
                    f.Click("기본 미션 시작하기");
                    var definition = FocusMissionCatalog.Find(code);
                    Assert(f.Latest.MissionId == definition.Id, "Unexpected basic mission: " + f.Latest.MissionId);
                    f.Capture(code + "-overview", capture);
                    f.Click("시작");
                    f.CompletePractice(definition, capture);
                    f.CompleteReflection(code + " synthetic reflection", "편안함", capture && code == "P04");
                    var completed = f.Latest;
                    Assert(completed.Status == ParticipationState.RewardCommitted, code + " did not persist completion.");
                    Assert(completed.FocusStepRecords.Count(r => !string.IsNullOrEmpty(r.ConfirmedUtc)) == definition.Steps.Length,
                        code + " lost a confirmed step.");
                    Assert(completed.Note == code + " synthetic reflection" && completed.Mood == "편안함", code + " lost reflection.");
                    f.Capture(code + "-completed", capture);
                    MissionCompletionUiValidation.ReturnToMissions(f);
                    Assert(f.Ui.CurrentScreen == "home", "Garden's mission tab did not return to mission home.");
                    f.Clock.UtcNow = f.Clock.UtcNow.AddDays(1);
                    f.Ui.RefreshCycle();
                    Assert(f.Has("기본 미션 시작하기"), "Home did not reopen the basic mission after day rollover without navigation.");
                }
                Assert(FocusMissionPolicy.CompletedCount(f.Service.Snapshot) == 3, "Three scheduled completions were not counted.");
                Assert(f.Service.Snapshot.Coins == 0 && f.Service.Snapshot.PracticeGrowthRecords.Count(record => record.Outcome == PracticeGrowthOutcome.Grown) == 3
                    && f.Service.Snapshot.PlantAppearance.Recipe.GrowthStep == 3, "Three basic practices did not grow exactly three stages while preserving the wallet.");
                f.Invoke("ShowFocusRecord", f.Latest.SessionId); f.Click("다시 연습하기 · 추가 보상 없음"); f.Click("시작");
                f.CompletePractice(FocusMissionCatalog.Find("P03"), false);
                f.CompleteReflection("", "", false);
                Assert(f.Latest.FocusReview, "Review did not remain a separate practice.");
                Assert(f.Service.Snapshot.Coins == 0 && f.Service.Snapshot.PlantAppearance.Recipe.GrowthStep == 3,
                    "Review granted coins or another growth.");
            }
        }

        static void AdditionalFlow(Report report, bool capture)
        {
            using (var f = new Fixture(report, true))
            {
                f.Ui.ShowFocusRecommendationFor("M03"); f.Click("추가 미션 시작하기"); f.Click("시작");
                f.Click("준비했어요"); f.ClickBack();
                Assert(f.Latest.InstructionStep == 0, "Practice back did not return to the previous step.");
                f.Click("닫기"); f.Click("추가 미션 이어서 하기");
                Assert(f.Latest.InstructionStep == 0, "Additional resume lost the previous step.");
                f.CompletePractice(FocusMissionCatalog.Find("M03"), capture);
                f.ClickBack(); f.Click("물 마셨어요");
                Assert(f.Has("다음"), "Emotion back and last-step reconfirm did not return to emotion.");
                f.Click("Focus.MoreEmotions");
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
                Assert(f.Service.Snapshot.Coins == 0 && f.Service.Snapshot.PlantAppearance.Recipe.GrowthStep == 1
                    && f.Service.Snapshot.PracticeGrowthRecords.Count(record => record.Outcome == PracticeGrowthOutcome.Grown) == 1,
                    "Additional practice/back/reconfirm did not preserve exactly one growth and the coin balance.");
                f.Capture("M03-completed", capture);
                f.Click("정원으로");
                Assert(f.Navigation.home.homeCanvas.gameObject.activeSelf && !f.Ui.gameObject.activeSelf, "Completion garden action failed.");
                f.Navigation.home.RefreshMissionStatus();
                Assert(f.Navigation.home.NavigationBar.MissionBadge.text == "!", "M03-only completion hid the remaining basic mission on the garden.");
                f.Navigation.ShowActivities(); f.Invoke("ShowHome");
                Assert(f.Has("기본 미션 시작하기") && f.Has("추천 미션 보기"), "Additional-only completion disabled basic or hid the next recommendation.");
                f.AssertMissionBadge("!");
            }
        }

        static void DraftAndRetry(Report report, bool capture)
        {
            using (var f = new Fixture(report, true))
            {
                f.Click("기본 미션 시작하기"); f.Click("시작"); f.Click("Focus.Action");
                string id = f.Latest.SessionId;
                f.Click("닫기");
                Assert(f.Has("기본 미션 이어서 하기"), "Close did not expose resume on home.");
                f.Capture("05-paused-home", capture);
                f.Click("기본 미션 이어서 하기");
                Assert(f.Latest.SessionId == id && f.Latest.InstructionStep == 1, "Resume lost the session or stage.");
                f.CompletePractice(FocusMissionCatalog.Find("P04"), false);
                f.Click("Focus.MoreEmotions"); f.Click("고마움"); f.Click("다음"); f.SetInput("synthetic retained note");
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
                Assert(f.Latest.Status == ParticipationState.RewardCommitted && f.Service.Snapshot.Coins == 0
                    && f.Service.Snapshot.PlantAppearance.Recipe.GrowthStep == 1
                    && f.Service.Snapshot.PracticeGrowthRecords.Count == 1, "Final reflection retry duplicated the earlier practice growth or changed coins.");
                f.Invoke("ShowHome");
                Assert(!f.Has("기본 미션 시작하기") && f.Has("추천 미션 보기"), "Basic-only done home has incorrect CTA state.");
                f.AssertMissionBadge("·");
                f.Ui.ShowFocusRecommendationFor("M03"); f.Click("추가 미션 시작하기"); f.Click("시작");
                f.CompletePractice(FocusMissionCatalog.Find("M03"), false); f.CompleteReflection("", "", false);
                f.Invoke("ShowHome");
                Assert(!f.Has("기본 미션 시작하기") && f.Has("추천 미션 보기"), "Completing M03 hid the other available recommendations.");
                f.AssertMissionBadge("·");
                f.Capture("07-both-completed-home", capture);
                f.Click("홈"); f.Navigation.home.RefreshMissionStatus();
                Assert(f.Navigation.home.NavigationBar.MissionBadge.text == "·", "Optional recommendations should use a neutral garden badge.");
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
                f.Click("기본 미션 시작하기"); f.Click("Focus.IconAction");
                string originalId = f.Latest.SessionId;
                CompleteCurrentSavedPractice(f, originalId);
                // Retained saved reflection from an earlier app version remains
                // readable; current practices no longer ask for this extra form.
                Assert(f.Service.UpdateReflection(originalId, "뿌듯함", "<size=99>synthetic history detail</size>"), f.Service.LastError);
                string saved = StateCodec.Encode(f.Service.Snapshot);
                f.Store.FailNextSaves = 1;
                f.Invoke("ShowFocusCourse");
                f.Click("Focus.CourseRecords.2w"); f.Click("Focus.Occurrence.2w.1");
                f.Capture("08-history", capture);
                f.Click("Course.DetailSteps");
                var copy = f.VisibleText;
                Assert(copy.Contains("synthetic history detail") && copy.Contains("뿌듯함"), "Saved detail omitted reflection fields.");
                Assert(f.Ui.GetComponentsInChildren<Text>(false).Any(t => t.text.Contains("<size=99>synthetic history detail</size>") && !t.supportRichText),
                    "Saved user markup was interpreted as rich text instead of displayed literally.");
                Assert(f.Service.Snapshot.Sessions.Single(s => s.SessionId == originalId).FocusStepRecords
                    .All(step => copy.Contains(step.Title) && copy.Contains(step.Prompt)), "Saved detail omitted step content/choice.");
                f.Capture("09-record-detail", capture);
                f.ScrollBottom(); f.Capture("09b-record-reflection", capture);
                f.Click("Course.DetailClose");
                Assert(StateCodec.Encode(f.Service.Snapshot) == saved && f.Store.FailNextSaves == 1,
                    "Course/detail browsing attempted to save or changed a committed record.");
                f.Store.FailNextSaves = 0;
                var before = f.Service.Snapshot.Nutrient;
                // The legacy review entry is retained as a public contract,
                // while current record screens intentionally omit its old button.
                f.Invoke("StartFocusReview", FocusMissionPolicy.MissionAt(1,"2w").Id, 1, "2w");
                f.Click("Focus.IconAction"); CompleteCurrentSavedPractice(f, f.Latest.SessionId);
                Assert(f.Latest.FocusReview && f.Latest.Status == ParticipationState.RewardCommitted, "Review was not saved separately.");
                Assert(f.Service.Snapshot.Nutrient == before && FocusMissionPolicy.CompletedCount(f.Service.Snapshot) == 1,
                    "Review changed course progress or awarded a second reward.");
                string reviewed = StateCodec.Encode(f.Service.Snapshot);
                Assert(f.Service.CompletePracticeSession(f.Latest.SessionId) && StateCodec.Encode(f.Service.Snapshot) == reviewed,
                    "Repeating an already completed review changed its reward or record.");
            }
        }

        static void CompleteCurrentSavedPractice(Fixture f, string id)
        {
            var session = f.Service.Snapshot.Sessions.Single(item => item.SessionId == id);
            if (session.Status == ParticipationState.Selected) Assert(f.Service.StartSession(id), f.Service.LastError);
            var mission = FocusMissionCatalog.FindForSession(session);
            if (mission.Code == "M15") Assert(f.Service.EnsureFocusWalkingTarget(id, WalkingPersonalization.Evaluate(null,f.Clock.UtcNow,f.Clock.TimeZone)), f.Service.LastError);
            while (true)
            {
                session = f.Service.Snapshot.Sessions.Single(item => item.SessionId == id);
                if (session.InstructionStep >= mission.Steps.Length) break;
                int index = session.InstructionStep; var step = mission.Steps[index];
                string value = step.Kind == "draw" ? "DRAW1|AA_B" : step.Options.FirstOrDefault()
                    ?? (step.RequiredAnswer || step.OptionalInput ? "synthetic saved response" : "");
                Assert(f.Service.ConfirmFocusStep(id, index, value), f.Service.LastError);
            }
            if (CameraMissionPolicy.Ready(f.Service.Snapshot.Sessions.Single(item => item.SessionId == id))) Assert(f.Service.SkipPhoto(id), f.Service.LastError);
            Assert(f.Service.CompletePracticeSession(id), f.Service.LastError);
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
                Assert(!f.Navigation.home.homeCanvas.gameObject.activeSelf && !f.Has("기록"), "Focused practice retained garden or main tab controls.");
                f.Capture("13-integrated-shell-practice", capture);
                f.Click("닫기"); f.Click("홈");
                Assert(f.Navigation.home.homeCanvas.gameObject.activeSelf && !f.Ui.gameObject.activeSelf, "Garden return did not restore its canvas.");
                var home = f.Navigation.home;
                var gardenBar = home.NavigationBar;
                Assert(gardenBar != null && gardenBar.ActiveTab == "홈", "Garden is not using the shared navigation component with Home selected.");
                Assert(NavigationGeometry(gardenBar).SequenceEqual(missionGeometry), "Garden icon, label or button geometry differs from the shared mission tabs.");
                Assert(gardenBar.HomeButton.GetComponentsInChildren<Text>(false).Any(t => t.text == "홈")
                    && gardenBar.MissionButton.GetComponentsInChildren<Text>(false).Any(t => t.text == "미션")
                    && gardenBar.CourseButton.GetComponentsInChildren<Text>(false).Any(t => t.text == "기록"),
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
                var label = button.GetComponentsInChildren<Text>(false).Single(t => t.text == "홈" || t.text == "미션" || t.text == "기록");
                Assert(icon != null && icon.rectTransform.anchoredPosition.y > label.rectTransform.anchoredPosition.y,
                    "Tab icon must sit above its label.");
                result.Add(new Vector4(icon.rectTransform.anchoredPosition.x, icon.rectTransform.anchoredPosition.y, icon.rectTransform.sizeDelta.x, icon.rectTransform.sizeDelta.y));
                result.Add(new Vector4(label.rectTransform.anchoredPosition.x, label.rectTransform.anchoredPosition.y, label.rectTransform.sizeDelta.x, label.rectTransform.sizeDelta.y));
            }
            return result.ToArray();
        }

        static void InitialAndWeeklyState(Report report, bool capture)
        {
            using (var f = new Fixture(report))
            {
                // Six real service completions on separate days reach Sunday rest;
                // no screen callback or stored user fixture fabricates the weekly state.
                for (int order = 1; order <= 6; order++)
                {
                    var definition = FocusMissionPolicy.MissionAt(order);
                    string id = "synthetic-weekly-" + order;
                    Assert(f.Service.BeginFocusSession(id, definition.Id, order), "Weekly setup did not begin the scheduled basic mission.");
                    Assert(f.Service.StartSession(id), "Weekly setup did not start practice.");
                    for (int step = 0; step < definition.Steps.Length; step++)
                        Assert(f.Service.ConfirmFocusStep(id, step, "", true), "Weekly setup lost a practice confirmation.");
                    Assert(f.Service.CompleteFocusSession(id, "", ""), "Weekly setup did not commit the practice.");
                    f.Clock.UtcNow = f.Clock.UtcNow.AddDays(1);
                    Assert(f.Ui.RefreshCycle(), "Weekly setup did not advance its synthetic day.");
                }
                f.Invoke("ShowHome");
                Assert(FocusMissionPolicy.CompletedCount(f.Service.Snapshot) == 6 && FocusMissionPolicy.Availability(f.Service.Snapshot) != null,
                    "Sunday rest did not enforce the six-practice weekly limit.");
                Assert(f.VisibleText.Contains("다음 주에 이어가요") && !f.VisibleText.Contains("오늘 완료했어요"),
                    "Weekly rest is mislabeled as a completion today.");
                Assert(!f.Has("기본 미션 시작하기"), "Weekly rest permits another basic start.");
                f.Capture("18-weekly-rest", capture);
                f.Clock.UtcNow = f.Clock.UtcNow.AddDays(1);
                Assert(f.Ui.RefreshCycle(), "Next Monday did not refresh the cycle.");
                Assert(f.Has("기본 미션 시작하기") && f.Has("추천 미션 보기"), "Next week did not reopen both mission start actions.");
                f.AssertMissionBadge("!");
                f.Capture("19-next-week-start", capture);
            }
        }

        static void SettingsMissionControls(Report report, bool capture)
        {
            using(var f=new Fixture(report,true))
            {
                f.Click("기본 미션 시작하기"); f.Click("Focus.IconAction");
                CompleteCurrentSavedPractice(f,f.Latest.SessionId);
                var settings=f.OpenMissionSettings();
                Assert(!settings.MissionRefreshButton.gameObject.activeInHierarchy,"Test tools should start collapsed.");
                f.ClickControl(settings.TestToolsToggleButton);
                Assert(settings.MissionRefreshButton.GetComponentsInChildren<Text>(false).Any(t=>t.text=="전체 초기화")
                    && settings.AdvanceMissionDayButton.GetComponentsInChildren<Text>(false).Any(t=>t.text=="다음 날로 넘기기"),
                    "Development actions must expose full reset and app-only day advance.");
                f.Capture("20-settings-mission-controls",capture);
                var original=f.Service.Snapshot;string originalState=StateCodec.Encode(original);
                string originalDate=settings.MissionDateStatus.text;var rawClock=f.Clock.UtcNow;var effectiveClock=f.Service.CurrentUtc;
                f.Store.FailNextSaves=1;f.ClickControl(settings.AdvanceMissionDayButton);
                Assert(StateCodec.Encode(f.Service.Snapshot)==originalState&&f.Service.CurrentUtc==effectiveClock
                    && settings.MissionDateStatus.text==originalDate,"Failed day advance changed records, date or the settings label.");
                f.ClickControl(settings.AdvanceMissionDayButton);
                var advanced=f.Service.Snapshot;
                Assert(f.Clock.UtcNow==rawClock&&f.Service.CurrentUtc==effectiveClock.AddDays(1)
                    && advanced.LastCycleId!=original.LastCycleId,"Next-day control changed the source clock or failed to advance the mission day.");
                Assert(advanced.Sessions.Count==original.Sessions.Count&&advanced.Nutrient==original.Nutrient
                    && PlantCodec.Encode(advanced.PlantAppearance.Recipe)==PlantCodec.Encode(original.PlantAppearance.Recipe),
                    "Day advance erased mission records, changed rewards or modified the saved plant.");
                Assert(settings.MissionDateStatus.text!=originalDate&&settings.MissionDateStatus.text.Contains(f.Ui.MissionDateLabel),
                    "Settings did not show the newly saved app date.");
                f.Capture("21-settings-next-day",capture);
                f.Navigation.ShowActivities();f.Invoke("ShowHome");
                Assert(f.Has("기본 미션 시작하기")&&f.Has("Focus.AdditionalAction"),"Next day did not reopen both available mission actions.");
                f.AssertMissionBadge("!");

                // Today now uses its own daily-emotion draft after both practices.
                // Populate only this memory store; no former per-mission note UI.
                Assert(f.Service.BeginFocusSession("settings-next-basic",FocusMissionPolicy.MissionAt(2,"2w").Id,2),f.Service.LastError);
                CompleteCurrentSavedPractice(f,"settings-next-basic");
                Assert(f.Service.BeginFocusSession("settings-next-additional","M03"),f.Service.LastError);
                CompleteCurrentSavedPractice(f,"settings-next-additional");
                f.Invoke("ShowDailyEmotion");
                const string draft="synthetic editable note preserved on failed settings save";
                f.SetInput(draft); Assert(f.Ui.SaveDailyEmotionUiDraft(),f.Service.LastError);
                string emotionId=f.Service.Snapshot.DailyEmotionRecords.Last().Id;
                f.Navigation.ShowSettings();
                string beforeReset=StateCodec.Encode(f.Service.Snapshot);var beforeResetClock=f.Service.CurrentUtc;
                f.Store.FailNextSaves=1;f.ClickControl(settings.AdvanceMissionDayButton);
                Assert(StateCodec.Encode(f.Service.Snapshot)==beforeReset&&f.Service.CurrentUtc==beforeResetClock,
                    "Failed day advance changed an editable daily-emotion draft.");
                f.ClickControl(settings.MissionRefreshButton);
                Assert(StateCodec.Encode(f.Service.Snapshot)==beforeReset,"Opening reset confirmation erased data.");
                f.Store.FailNextSaves=1;f.ClickControl(settings.MissionResetConfirmButton);
                Assert(StateCodec.Encode(f.Service.Snapshot)==beforeReset,"Failed full reset partially removed saved detail or progress.");
                f.Navigation.ShowActivities();f.Invoke("ShowDailyEmotion",emotionId);
                Assert(f.Input!=null&&f.Input.text==draft,"Failed settings actions destroyed the editable daily-emotion note.");
                f.Navigation.ShowSettings();var preserved=f.Service.Snapshot;
                f.ClickControl(settings.MissionRefreshButton);f.ClickControl(settings.MissionResetConfirmButton);var cleared=f.Service.Snapshot;
                Assert(cleared.Sessions.Count==0&&FocusMissionPolicy.CompletedCount(cleared)==0
                    && cleared.PracticePreferences.Count==0&&cleared.Experiences.Count==0&&cleared.DailyEmotionRecords.Count==0,
                    "Full reset left saved details, course completions or practice answers behind.");
                Assert(cleared.Nutrient==0&&cleared.GardenXp==0&&cleared.Environments.Count==0&&cleared.PlantPhotos.Count==0
                    && cleared.PlantAppearance.Recipe.GrowthStep==0&&cleared.InstallationId!=preserved.InstallationId
                    && cleared.PreviewDayOffset==0&&!cleared.MbctProgramChosen&&!cleared.FullResetPending,
                    "Full reset did not restore wallet, garden, selected course and app date to their initial state.");
                string reset=StateCodec.Encode(cleared);
                Assert(f.Ui.ResetAllData(cleared.FullResetRequestId,cleared.FullResetSourceInstallationId)
                    && StateCodec.Encode(f.Service.Snapshot)==reset,"Repeating a completed reset altered the new garden.");
                f.Capture("22-settings-reset-complete",capture);
                f.Navigation.ShowActivities();f.Invoke("ShowHome");
                f.Click("Focus.SelectProgram.2w");f.Click("Focus.ConfirmProgram.2w");
                Assert(f.Has("기본 미션 시작하기")&&f.Has("Focus.AdditionalAction"),"Full reset did not restore fresh mission actions after choosing a course.");
                f.Invoke("ShowFocusHistory",null,0,"2w");
                Assert(f.VisibleText.Contains("아직 저장된 수행 기록이 없어요")&&!f.VisibleText.Contains(draft),
                    "Cleared details still appear in history.");
                f.Invoke("ShowHome");f.Click("기본 미션 시작하기");
                Assert(f.Latest.FocusOrder==1&&f.Latest.MissionId==FocusMissionPolicy.MissionAt(1,"2w").Id
                    && f.Latest.Status==ParticipationState.Selected,"Reset retained the previous basic occurrence or practice stage.");
                Assert(f.Service.Snapshot.DailyEmotionRecords.Count==0&&f.Service.Snapshot.Sessions.All(s=>string.IsNullOrEmpty(s.Note)),
                    "A new garden inherited the removed note draft.");
            }
        }

        static void SettingsTestCoins(Report report, bool capture)
        {
            foreach (var size in new[] { new Vector2Int(360, 640), new Vector2Int(390, 844) })
            {
                var seeded = new GardenState { MigrationCompleted = true, MbctProgramSelectionVersion = 1,
                    InstallationId = "synthetic-test-nutrient-ui", Nutrient = 35, GardenXp = 9, LegacyGrowth = .2f };
                GardenPlantVisuals.Sync(seeded);
                var store = new MemoryStore(); store.Save(seeded);
                using (var f = new Fixture(report, true, seededStore: store))
                {
                    f.Resize(size.x, size.y);
                    Assert(f.Service.BeginSession("synthetic-test-credit-record", "free-mission:M01", 0)
                        && f.Service.StartSession("synthetic-test-credit-record")
                        && f.Service.CompleteSession("synthetic-test-credit-record"), "Synthetic test-credit record could not be prepared.");
                    string plantId = f.Service.Snapshot.PlantAppearance.Recipe.InstanceId;
                    Assert(f.Service.SetPlantCompleted(plantId, true) && f.Service.SavePlantPhoto("synthetic-test-credit-photo", plantId),
                        "Synthetic album photo could not be prepared.");
                    var settings = f.OpenMissionSettings();
                    var button = settings.AddTestCoinsButton;
                    Assert(button != null && button.name == "Settings.AddTestCoins" && !button.gameObject.activeInHierarchy,
                        "Test coin action is absent or visible outside the collapsed test-tools disclosure.");
                    f.ClickControl(settings.TestToolsToggleButton);
                    Assert(button.GetComponentsInChildren<Text>(false).Any(text => text.text == "코인 100개 추가"),
                        "Test coin button does not have the requested label.");
                    var before = f.Service.Snapshot; string encoded = StateCodec.Encode(before);
                    Assert(settings.TestCoinStatus != null && settings.TestCoinStatus.text == "보유 코인 · " + before.Coins + "개",
                        "Test tools do not show the existing committed balance.");
                    f.Store.FailNextSaves = 1; f.ClickControl(button);
                    Assert(StateCodec.Encode(f.Service.Snapshot) == encoded && StateCodec.Encode(f.Store.Load()) == encoded,
                        "Failed test credit changed the wallet, current plant, saved photo, growth or activity record.");
                    Assert(settings.TestCoinStatus.text == "보유 코인 · " + before.Coins + "개"
                        && settings.GetComponentsInChildren<Text>(false).Any(text => text.text == "추가하지 못했어요. 다시 시도해 주세요."),
                        "Failed test credit did not retain the balance and show a retry message.");
                    f.Capture("settings-test-coins-failed-" + size.x + "x" + size.y, capture);
                    for (int click = 1; click <= 2; click++)
                    {
                        f.ClickControl(button);
                        var after = f.Service.Snapshot;
                        Assert(after.Coins == before.Coins + 100 * click
                            && settings.TestCoinStatus.text == "보유 코인 · " + after.Coins + "개",
                            "Each deliberate test-tool click must credit exactly 100 and refresh the balance.");
                        Assert(StateCodec.Encode(f.Store.Load()) == StateCodec.Encode(after), "Test credit was not persisted.");
                        after.Coins = before.Coins;
                        Assert(StateCodec.Encode(after) == encoded, "Test credit changed data other than the coin balance.");
                    }
                    Assert(settings.GetComponentsInChildren<Text>(false).Any(text => text.text == "코인 100개를 추가했어요."),
                        "Successful test credit does not show its confirmation.");
                    string credited = StateCodec.Encode(f.Service.Snapshot);
                    f.Navigation.ShowActivities(); f.Invoke("ShowHome");
                    f.OpenMissionSettings();
                    Assert(ReferenceEquals(settings.AddTestCoinsButton, button)
                        && settings.GetComponentsInChildren<Button>(true).Count(item => item.name == "Settings.AddTestCoins") == 1
                        && StateCodec.Encode(f.Service.Snapshot) == credited,
                        "Returning to settings recreated the credit action or granted another 100.");
                    using (var reopened = new GardenStateService(f.Store, f.Clock, new DemoBalanceConfig()))
                        Assert(reopened.Open() && StateCodec.Encode(reopened.Snapshot) == credited,
                            "Reopening the synthetic saved state did not preserve the test balance and existing records.");
                    f.RevealGraphic(button.GetComponent<Image>());
                    f.Capture("settings-test-coins-saved-" + size.x + "x" + size.y, capture);
                }
            }
        }

        static void LearnedRecommendations(Report report, bool capture)
        {
            using (var f = new Fixture(report))
            {
                var learned = MbctContent.ForProgram("2w").First();
                f.Ui.SelectMission(learned);
                string id = f.Service.Snapshot.Sessions.Last().SessionId;
                Assert(f.Service.StartSession(id) && f.Service.SavePracticeStep(id, learned.steps.Length), f.Service.LastError);
                f.Clock.UtcNow = f.Clock.UtcNow.AddMinutes(4);
                f.Ui.Finish(null, null);
                Assert(MbctPolicy.AllCompletedIds(f.Service.Snapshot).Contains(learned.id), "Synthetic legacy lesson did not persist completion.");
                int nutrients = f.Service.Snapshot.Nutrient;
                var completed = f.Service.Snapshot.Sessions.Single(entry => entry.SessionId == id);
                long start = DateTimeOffset.Parse(completed.StartedUtc).ToUnixTimeMilliseconds();
                long end = DateTimeOffset.Parse(completed.EndedUtc).ToUnixTimeMilliseconds();
                f.Ui.PreviewHealth = new AndroidPlatformSnapshot
                {
                    inputMode = "MOCK", healthConnect = new HealthConnectSnapshot
                    {
                        heartRate = new HealthMetricSnapshot
                        {
                            status = "AVAILABLE", queryComplete = true, queryStartEpochMs = start, queryEndEpochMs = end,
                            sampleCount = 2, displaySampleCount = 2,
                            heartRateSamples = new[]
                            {
                                new HeartRateSampleSnapshot { measuredAtEpochMs = start, beatsPerMinute = 72, sourcePackage = "Synthetic UI fixture", segmentId = 0 },
                                new HeartRateSampleSnapshot { measuredAtEpochMs = start + 60000, beatsPerMinute = 74, sourcePackage = "Synthetic UI fixture", segmentId = 0 }
                            }
                        }
                    }
                };
                f.Invoke("ShowFocusCourse");
                f.Click("이전 과정·예시 기록");
                Assert(f.Ui.CurrentScreen == "focus-history" && f.Has("Focus.Record." + id), "The current record hub did not expose retained legacy records.");
                f.Capture("legacy-garden-journal", capture);
                f.Click("Focus.Record." + id);
                var healthFrame=f.Ui.GetComponentInChildren<GardenActivityLayout>();
                Assert(healthFrame!=null && healthFrame.Header.GetComponentsInChildren<Text>().Any(t=>t.text=="활동과 측정 기록"),
                    "Health detail title must remain in its fixed heading.");
                f.AssertScrollStartsAtTop("Activity record overview", "Local garden flow");
                var graph = f.Ui.GetComponentInChildren<HeartSampleGraph>();
                Assert(f.Ui.CurrentScreen == "record" && graph != null && graph.Samples.Length == 2,
                    "Non-Focus record no longer opens its activity health chart.");
                var layer = graph.transform.parent.parent;
                var paper = layer.parent.Find("Garden theme surface");
                Assert(paper == null || layer.GetSiblingIndex() > paper.GetSiblingIndex(), "Paper theme covers the retained health graph.");
                f.Capture("legacy-health-record", capture); // Preserve the untouched initial top/title evidence.
                f.RevealGraphic(graph);
                f.Capture("legacy-health-chart-scrolled", capture);
                f.Click("기록 목록");
                Assert(f.Ui.CurrentScreen == "records" && f.Has(learned.title), "Legacy record back must return to its retained participation journal.");
                // The user removed this legacy hub from the record tab. Validate
                // its retained public entry without restoring an obsolete menu.
                f.Invoke("ShowLearnedPracticeRecommendations");
                Assert(f.Has("다시 해볼 만한 활동") && f.Has("추가 활동 추천 설정"), "Retained learned recommendation entry did not expose its actions.");
                f.Capture("learned-recommendation-hub", capture);
                f.Click("추가 활동 추천 설정");
                f.Click("호흡 관찰");
                f.Click("1분 정도");
                Assert(f.Service.Snapshot.PracticePreferences.Any(p => p.Key == "favorite" && p.Value == "호흡 관찰")
                    && f.Service.Snapshot.PracticePreferences.Any(p => p.Key == "recommendation-minutes" && p.Value == "1"),
                    "Recommendation preferences did not persist through the retained UI.");
                f.Capture("learned-recommendation-preferences", capture);
                f.Click("배운 활동으로");
                var policy = new CapstoneDesign.Runtime.Personalization.ResponsePolicy();
                var decision = CapstoneDesign.Runtime.Personalization.OptionalActivityRecommender.Select(f.Service.Snapshot, policy, DateTimeOffset.UtcNow);
                Assert(decision != null && decision.MissionId == learned.id, "Learned MBCT candidate was reinterpreted as a new P/M mission.");
                f.Click("다시 해볼 만한 활동");
                var selected = f.Service.Snapshot.Sessions.Last();
                Assert(selected.MissionId == decision.MissionId && !FocusMissionPolicy.IsFocus(selected), "Recommendation click selected a different candidate or save format.");
                Assert(f.Service.Snapshot.Nutrient == nutrients && MbctPolicy.AllCompletedIds(f.Service.Snapshot).Contains(learned.id),
                    "Opening a recommendation changed legacy completion or rewards.");
                f.Capture("learned-recommendation-selected", capture);
            }
        }

        static void GardenProgressAndRecordTitles(Report report, bool capture)
        {
            var store = new MemoryStore();
            var clock = new SyntheticClock();
            var balance = new DemoBalanceConfig { CompletionNutrient = 5, VisitorChancePercent = 0 };
            const string savedFocusTitle = "합성 이전 제목 · 한 모금의 감각";
            const string ignoredLegacyFocusTitle = "합성 무시할 Focus 제목";
            var focus = FocusMissionPolicy.MissionAt(1, "2w");
            var legacy = MindfulnessContent.FindMission("free-mission:M03");
            using (var seed = new GardenStateService(store, clock, balance))
            {
                Assert(seed.Open() && seed.RefreshCycle(Array.Empty<string>()) && seed.SelectMbctProgram("2w"), seed.LastError);
                Assert(seed.BeginFocusSession("synthetic-focus-title", focus.Id, 1) && seed.StartSession("synthetic-focus-title"), seed.LastError);
                for (int step = 0; step < focus.Steps.Length; step++)
                {
                    string answer = focus.Steps[step].Options.FirstOrDefault() ?? "";
                    Assert(seed.ConfirmFocusStep("synthetic-focus-title", step, answer, answer.Length == 0), seed.LastError);
                }
                Assert(seed.CompleteFocusSession("synthetic-focus-title"), seed.LastError);
                Assert(seed.BeginSession("synthetic-legacy-title", legacy.id) && seed.StartSession("synthetic-legacy-title")
                    && seed.CompleteSession("synthetic-legacy-title"), seed.LastError);
                // Explicit synthetic saved-content variation: a prior Focus
                // title can differ from today's catalog. A non-Focus record
                // must still use its own legacy content interpretation.
                var saved = seed.Snapshot;
                saved.Sessions.Single(entry => entry.SessionId == "synthetic-focus-title").MissionTitleSnapshot = savedFocusTitle;
                saved.Sessions.Single(entry => entry.SessionId == "synthetic-legacy-title").MissionTitleSnapshot = ignoredLegacyFocusTitle;
                StateCodec.Validate(saved); store.Save(saved);
            }
            using (var f = new Fixture(report, false, true, store))
            {
                Assert(FocusMissionPolicy.CompletedCount(f.Service.Snapshot) == 1 && MbctPolicy.CompletedCount(f.Service.Snapshot) == 0,
                    "Fixture does not distinguish the new Focus course from legacy MBCT progress.");
                string before = StateCodec.Encode(f.Service.Snapshot);
                f.Invoke("ShowShop");
                Assert(f.Ui.GetComponentsInChildren<Text>(false).Any(text => text.name == "Shop.Coins"
                    && text.text == f.Service.CoinBalance.ToString("N0") + " 코인")
                    && !f.Ui.GetComponentsInChildren<Transform>(false).Any(node => node.name == "Course progress"),
                    "Garden shop must show its wallet without an unrelated course-progress block.");
                f.Capture("focus-wallet-in-garden-shop", capture);
                f.Invoke("PreviewGrowth");
                Assert(f.Has("미션으로 이동") && !f.Has("확정")
                    && f.Ui.GetComponentsInChildren<Text>(false).Any(text => text.name == "PlantGrowth.MissionGuidance"),
                    "Read-only growth guidance retained a paid confirmation or omitted the mission entry.");
                f.Capture("mission-growth-guidance", capture);
                f.Invoke("ShowGardenJournal");
                Assert(f.Has(savedFocusTitle) && f.Has(legacy.title) && !f.VisibleText.Contains(ignoredLegacyFocusTitle),
                    "Garden journal renamed a saved Focus or legacy record.");
                f.Capture("garden-journal-versioned-titles", capture);
                Assert(StateCodec.Encode(f.Service.Snapshot) == before,
                    "Inspecting progress, mission growth guidance or saved record titles changed persistent state.");
            }
        }

        static void CourseSelections(Report report, bool capture)
        {
            foreach (string program in new[] { "2w", "4w", "8w" })
            using (var f = new Fixture(report, false, false))
            {
                Assert(!f.Service.Snapshot.MbctProgramChosen, "Course fixture starts with a selected course.");
                f.Capture("program-selection-" + program, capture);
                f.Click("Focus.SelectProgram." + program);
                f.Capture("program-confirm-" + program, capture);
                f.Click("Focus.ConfirmProgram." + program);
                Assert(f.Service.Snapshot.MbctProgramChosen && f.Service.Snapshot.MbctProgramId == program,
                    "Course confirmation did not persist " + program);
                f.Click("기본 미션 시작하기");
                Assert(f.Latest.MissionId == FocusMissionPolicy.MissionAt(1, program).Id && f.Latest.FocusProgramId == program,
                    "First basic mission did not use the chosen program.");
                f.Capture("program-first-mission-" + program, capture);
            }
        }

        static void AllMissionFlows(Report report, bool capture)
        {
            var representative = new HashSet<string> { "M07", "M10", "M11", "M15", "P19", "P24", "P26", "P27", "P28" };
            using (var f = new Fixture(report, true))
            {
                foreach (var mission in FocusMissionCatalog.Additional)
                {
                    f.Invoke("StartFocusAdditionalMission", mission.Id);
                    VerifyMissionFlow(f, mission, representative.Contains(mission.Code), capture);
                }
                Assert(f.Service.SelectMbctProgram("8w"), f.Service.LastError);
                // The first 28 occurrences of the real 8-week curriculum cover
                // all 28 P missions; progress is obtained only by real commands.
                for (int order = 1; order <= 28; order++)
                {
                    while (FocusMissionPolicy.Availability(f.Service.Snapshot, "8w") != null)
                    {
                        f.Clock.UtcNow = f.Clock.UtcNow.AddDays(1);
                        Assert(f.Ui.RefreshCycle(), f.Service.LastError);
                    }
                    var mission = FocusMissionPolicy.MissionAt(order, "8w");
                    f.Invoke("StartFocusBasic");
                    VerifyMissionFlow(f, mission, representative.Contains(mission.Code), capture);
                }
                var completed = f.Service.Snapshot.Sessions.Where(s => s.Status == ParticipationState.RewardCommitted).ToArray();
                Assert(FocusMissionCatalog.All.All(m => completed.Any(s => s.MissionId == m.Id)), "One of the 45 missions was not completed through UI controls.");
                string saved = StateCodec.Encode(f.Service.Snapshot);
                using (var reopened = new GardenStateService(f.Store, f.Clock, new DemoBalanceConfig()))
                {
                    Assert(reopened.Open(), reopened.LastError);
                    Assert(StateCodec.Encode(reopened.Snapshot) == saved, "Reopening changed completed sessions, garden or reward state.");
                }
            }
        }

        static void VerifyMissionFlow(Fixture f, FocusMissionDefinition mission, bool representative, bool capture)
        {
            Assert(f.Latest.MissionId == mission.Id, "Unexpected catalog mission " + mission.Code);
            f.AssertVisibleAssets();
            f.Capture("catalog-" + mission.Code + "-overview", capture);
            f.Click("시작");
            f.CompletePractice(mission, representative && capture);
            f.CompleteReflection("", "", false);
            Assert(f.Latest.Status == ParticipationState.RewardCommitted, "Catalog mission did not complete: " + mission.Code);
            if (representative) f.Capture("catalog-" + mission.Code + "-complete", capture);
            MissionCompletionUiValidation.ReturnToMissions(f);
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

        internal sealed class MemoryStore : IStateStore
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
        internal sealed class SyntheticClock : IClock
        {
            public DateTimeOffset UtcNow { get; set; } = DateTimeOffset.Parse("2026-10-26T03:00:00Z");
            public TimeZoneInfo TimeZone => TimeZoneInfo.CreateCustomTimeZone("FocusValidation-Seoul", TimeSpan.FromHours(9), "Synthetic Seoul", "Synthetic Seoul");
        }

        internal sealed class Fixture : IDisposable
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
            readonly Dictionary<Graphic,Canvas> registeredGraphics = new Dictionary<Graphic,Canvas>();
            RenderTexture validationTarget;
            readonly Camera camera, existingCamera;
            readonly Light existingLight;
            readonly float lightIntensity;
            readonly Vector3 cameraPosition;
            readonly Color ambient;
            SensorRawDisplay missionSettings;
            public ActivitySession Latest => Service.Snapshot.Sessions.Last(s => FocusMissionPolicy.IsFocus(s));
            public InputField Input => Ui.GetComponentsInChildren<InputField>(false).FirstOrDefault(i => i.gameObject.activeInHierarchy);
            public string VisibleText => string.Join("\n", Ui.GetComponentsInChildren<Text>(false).Where(t => t.gameObject.activeInHierarchy).Select(t => t.text));
            public void Resize(int width,int height)
            {
                camera.targetTexture=null;validationTarget.Release();UnityEngine.Object.DestroyImmediate(validationTarget);
                validationTarget=new RenderTexture(width,height,24);camera.targetTexture=validationTarget;camera.aspect=width/(float)height;Layout();
            }

            public Fixture(Report report, bool integratedShell = false, bool chooseCourse = true, MemoryStore seededStore = null)
            {
                this.report = report;
                if (seededStore != null) Store = seededStore;
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
                canvas.sortingOrder = 20;
                Navigation = root.AddComponent<MockupNavigation>();
                Navigation.islandPanel = GardenUi.Box(root.transform, "Synthetic unchanged garden destination", 0, 0, 1, 1);
                Navigation.settingsPanel = GardenUi.Box(root.transform, "Synthetic settings", 0, 0, 1, 1);
                Navigation.activitiesPanel = GardenUi.Box(root.transform, "Synthetic mission panel", 0, 0, 1, 1);
                if (integratedShell)
                {
                    // Model the existing scene's real non-null presenter branch without loading its save or garden assets.
                    Navigation.home = root.AddComponent<GardenHomePresenter>(); Navigation.home.navigation = Navigation;
                    var gardenCanvas = new GameObject("Synthetic preserved garden canvas", typeof(RectTransform), typeof(Canvas), typeof(GraphicRaycaster));
                    gardenCanvas.transform.SetParent(root.transform, false); Navigation.home.homeCanvas = gardenCanvas.GetComponent<Canvas>();
                    // MockupMain renders the preserved home/navigation canvas at
                    // order 30 above the settings/mission root at order 20.
                    // The fixture nests it only for ownership and cleanup.
                    Navigation.home.homeCanvas.worldCamera = camera;
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
                if (chooseCourse)
                {
                    Assert(Service.Open(), Service.LastError);
                    Assert(Service.SelectMbctProgram("2w"), Service.LastError);
                }
                try
                {
                    Ui = Navigation.activitiesPanel.AddComponent<WeekOneQuestDemo>();
                    Ui.Balance = balance; Ui.Initialize(Service, true);
                    if (Navigation.home != null) Navigation.home.activity = Ui;
                    root.SetActive(true);
                    if (Navigation.home != null)
                    {
                        // A cold nested preview Canvas inherits the root order
                        // when it first becomes active. Initialize its native
                        // canvas before applying the production order, once.
                        var homeCanvas = Navigation.home.homeCanvas;
                        homeCanvas.gameObject.SetActive(true); Canvas.ForceUpdateCanvases();
                        homeCanvas.overrideSorting = true; homeCanvas.sortingOrder = 30;
                        Assert(homeCanvas.overrideSorting && homeCanvas.sortingOrder == 30,
                            "The initialized synthetic garden canvas must match MockupMain's order 30.");
                    }
                    Navigation.ShowActivities(); Invoke("ShowHome");
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
            public SensorRawDisplay OpenMissionSettings()
            {
                if(missionSettings==null)
                {
                    Assert(!Navigation.settingsPanel.activeSelf,"Build settings while its synthetic panel is inactive.");
                    var refresh=GardenUi.Button(Navigation.settingsPanel.transform,"Synthetic refresh control",0,0,1,.1f,()=>{});
                    missionSettings=Navigation.settingsPanel.AddComponent<SensorRawDisplay>();
                    missionSettings.refreshHealthButton=refresh;missionSettings.PrepareView();
                }
                Navigation.ShowSettings();Layout();
                Assert(missionSettings.MissionRefreshButton!=null&&missionSettings.AdvanceMissionDayButton!=null
                    && missionSettings.MissionDateStatus!=null,"Mission settings controls were not built.");
                return missionSettings;
            }
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
            void Reveal(Button button) => Reveal((RectTransform)button.transform);
            void Reveal(RectTransform rect)
            {
                // Reveal requested controls/charts in every active scroll ancestor.
                // Initial top-position assertions run before any reveal.
                foreach(var scroll in rect.GetComponentsInParent<ScrollRect>(false))
                {
                    if(!scroll.isActiveAndEnabled||scroll.content==null||scroll.viewport==null)continue;
                    Layout();
                    Vector3 center=scroll.content.InverseTransformPoint(rect.TransformPoint(rect.rect.center));
                    float y=Mathf.Clamp(-center.y-scroll.viewport.rect.height/2,0,Mathf.Max(0,scroll.content.rect.height-scroll.viewport.rect.height));
                    scroll.content.anchoredPosition=new Vector2(scroll.content.anchoredPosition.x,y);
                    scroll.velocity=Vector2.zero;Layout();
                }
            }
            public void RevealGraphic(Graphic graphic)
            {
                Reveal(graphic.rectTransform);
                var scroll = graphic.GetComponentInParent<ScrollRect>();
                Assert(scroll != null, "Chart is not inside the expected record scroll view.");
                var bounds = RectTransformUtility.CalculateRelativeRectTransformBounds(scroll.viewport, graphic.rectTransform);
                Assert(bounds.min.y >= scroll.viewport.rect.yMin - 1 && bounds.max.y <= scroll.viewport.rect.yMax + 1,
                    "Scrolled health graph is still clipped by its viewport.");
            }
            void AssertRaycastTarget(Button button)
            {
                Layout();
                PrepareRaycastGraphics();
                Assert(root.GetComponentsInChildren<GardenThemeSurface>(false).All(surface => !surface.raycastTarget),
                    "A decorative garden theme surface intercepts input.");
                var rect = (RectTransform)button.transform;
                var point = RectTransformUtility.WorldToScreenPoint(camera, rect.TransformPoint(rect.rect.center));
                var hits = RaycastFixture(new PointerEventData(pointerContext) { position = point });
                var hit = hits.FirstOrDefault();
                Assert(hit.gameObject != null && hit.gameObject.GetComponentInParent<Button>() == button,
                    "Button center is blocked or not hittable: " + button.name + " on " + Ui.CurrentScreen
                    + "; first hit=" + (hit.gameObject == null ? "none" : hit.gameObject.name)
                    + (hit.gameObject != null && hit.gameObject.GetComponentInParent<Button>() == button ? "" : RaycastDiagnostic(button,point,hits)));
                report.raycastChecks++;
            }
            string RaycastDiagnostic(Button button,Vector2 point,List<RaycastResult> hits)
            {
                string CameraInfo(Camera value) => value==null ? "null" : value.name+"/pixelRect="+value.pixelRect
                    +"/depth="+value.depth+"/target="+(value.targetTexture==null?"none":value.targetTexture.width+"x"+value.targetTexture.height);
                string CanvasInfo(Canvas value) => value==null ? "null" : value.name+"/active="+value.isActiveAndEnabled
                    +"/mode="+value.renderMode+"/order="+value.sortingOrder+"/override="+value.overrideSorting+"/camera="+CameraInfo(value.worldCamera);
                string RectInfo(RectTransform value)
                {
                    var corners=new Vector3[4];value.GetWorldCorners(corners);
                    return "local="+value.rect+"/anchored="+value.anchoredPosition+"/screen="
                        +RectTransformUtility.WorldToScreenPoint(camera,corners[0])+".."+RectTransformUtility.WorldToScreenPoint(camera,corners[2]);
                }
                string allHits=string.Join(" | ",hits.Take(8).Select(item=>(item.gameObject==null?"null":item.gameObject.name)
                    +"/module="+(item.module==null?"null":item.module.name)+"/order="+item.sortingOrder+"/layer="+item.sortingLayer+"/depth="+item.depth+"/distance="+item.distance));
                string controls=string.Join(" | ",button.GetComponentsInChildren<Graphic>(true).Where(graphic=>graphic.raycastTarget).Select(graphic=>
                    graphic.name+"/active="+graphic.isActiveAndEnabled+"/depth="+graphic.depth+"/culled="+graphic.canvasRenderer.cull
                    +"/canvas="+CanvasInfo(graphic.canvas)+"/registered="+(graphic.canvas!=null&&GraphicRegistry.GetGraphicsForCanvas(graphic.canvas).Contains(graphic))
                    +"/contains="+RectTransformUtility.RectangleContainsScreenPoint(graphic.rectTransform,point,camera)
                    +"/filters="+graphic.Raycast(point,camera)+"/"+RectInfo(graphic.rectTransform)));
                string casters=string.Join(" | ",root.GetComponentsInChildren<GraphicRaycaster>(true).Select(caster=>caster.name
                    +"/active="+caster.IsActive()+"/eventCamera="+CameraInfo(caster.eventCamera)+"/canvas="+CanvasInfo(caster.GetComponent<Canvas>())
                    +"/registeredCount="+GraphicRegistry.GetGraphicsForCanvas(caster.GetComponent<Canvas>()).Count));
                return "; point="+point+" screen="+Screen.width+"x"+Screen.height+" fixtureCamera="+CameraInfo(camera)
                    +" hits=["+allHits+"] target=["+controls+"] casters=["+casters+"]";
            }
            void PrepareRaycastGraphics()
            {
                foreach (var graphic in root.GetComponentsInChildren<Graphic>(false))
                {
                    // Preview-scene lifecycle is incomplete. Register only this
                    // fixture's graphics, against their real owning canvas.
                    var owner = graphic.canvas; if (owner == null) continue;
                    if (registeredGraphics.TryGetValue(graphic, out var previousOwner) && previousOwner != owner)
                        GraphicRegistry.UnregisterGraphicForCanvas(previousOwner, graphic);
                    registeredGraphics[graphic] = owner;
                    GraphicRegistry.RegisterGraphicForCanvas(owner, graphic);
                    graphic.SetAllDirty(); graphic.Rebuild(CanvasUpdate.PreRender); graphic.Rebuild(CanvasUpdate.LatePreRender);
                }
                Canvas.ForceUpdateCanvases(); camera.Render();
                var homeCanvas = Navigation.home?.homeCanvas;
                Assert(canvas.sortingOrder == 20 && (homeCanvas == null || !homeCanvas.isActiveAndEnabled
                    || homeCanvas.overrideSorting && homeCanvas.sortingOrder == 30),
                    "Synthetic canvas ordering changed after navigation: root must stay 20 and active garden must stay 30.");
            }
            List<RaycastResult> RaycastFixture(PointerEventData pointer)
            {
                var hits = new List<RaycastResult>();
                var scratch = typeof(GraphicRaycaster).GetField("s_SortedGraphics", BindingFlags.Static | BindingFlags.NonPublic)?.GetValue(null) as List<Graphic>;
                scratch?.Clear();
                try
                {
                    // Use only this fixture's active canvases, without touching
                    // EventSystem.current or the user's raycaster registration.
                    foreach (var caster in root.GetComponentsInChildren<GraphicRaycaster>(false))
                        if (caster.IsActive()) caster.Raycast(pointer, hits);
                }
                finally { scratch?.Clear(); }
                // Use uGUI's own canvas-priority, sorting-layer/order, depth and
                // distance ordering; depth alone is meaningless across canvases.
                var compare = typeof(EventSystem).GetField("s_RaycastComparer", BindingFlags.Static | BindingFlags.NonPublic)?.GetValue(null) as Comparison<RaycastResult>;
                Assert(compare != null, "The installed uGUI EventSystem raycast ordering is unavailable.");
                hits.Sort(compare); return hits;
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
            public void AssertScrollStartsAtTop(string firstElementName, string scrollName = "스크롤 영역")
            {
                // Deliberately inspect the first real layout. Never repair scroll
                // position in validation; the production scrollbar must start at top.
                Layout();
                var scroll = Ui.GetComponentsInChildren<ScrollRect>(false).Single(item => item.name == scrollName);
                Assert(Mathf.Abs(scroll.verticalNormalizedPosition - 1f) < .001f
                    && Mathf.Abs(scroll.content.anchoredPosition.y) < .5f,
                    "Fresh page starts below its first card: " + Ui.CurrentScreen + "; content y=" + scroll.content.anchoredPosition.y);
                var first = scroll.content.Find(firstElementName) as RectTransform
                    ?? scroll.content.GetComponentsInChildren<Text>(false).SingleOrDefault(label => label.text == firstElementName)?.rectTransform;
                Assert(first != null, "Initial page card/title missing: " + firstElementName);
                var bounds = RectTransformUtility.CalculateRelativeRectTransformBounds(scroll.viewport, first);
                Assert(bounds.min.y >= scroll.viewport.rect.yMin - 1 && bounds.max.y <= scroll.viewport.rect.yMax + 1,
                    "First page card/title is clipped before any scroll: " + firstElementName);
            }
            public void ScrollToBottomViaBar()
            {
                var scroll = Ui.GetComponentsInChildren<ScrollRect>(false).Single(item => item.name == "스크롤 영역");
                Assert(scroll.verticalScrollbar != null, "The themed page has no scrollbar.");
                scroll.verticalScrollbar.value = 0;
                Layout();
                Assert(scroll.content.anchoredPosition.y > 1, "Scrollbar did not move its page content.");
            }
            public void CompletePractice(FocusMissionDefinition definition, bool capture, bool thinkOnly = false, string alternateRoute = null, bool keepPhotoChoice = false)
            {
                int limit = 0;
                while (Latest.InstructionStep < definition.Steps.Length)
                {
                    Assert(++limit <= definition.Steps.Length + 1, "Practice did not advance.");
                    int index = Latest.InstructionStep; var step = definition.Steps[index];
                    AssertVisibleAssets();
                    if (step.Kind == "draw")
                    {
                        DrawSyntheticStroke(); Click("Focus.DrawingUndo");
                        Assert(FocusDrawingData.Decode(Ui.GetComponentInChildren<FocusDrawingSurface>().Value).Count == 0, "Drawing undo did not remove the stroke.");
                        DrawSyntheticStroke(); Click("Focus.DrawingClear"); DrawSyntheticStroke();
                    }
                    else if ((step.OptionalInput || step.RequiredAnswer) && Input != null && !thinkOnly)
                    {
                        SetInput("synthetic message before speaking");
                        Click("닫기"); Click(definition.Role == FocusMissionCatalog.BasicRole ? "기본 미션 이어서 하기" : "추가 미션 이어서 하기");
                        Assert(Latest.InstructionStep == index && Input != null && Input.text == "synthetic message before speaking",
                            "Optional practice draft did not survive close/resume.");
                    }
                    if (step.Options.Length > 0) Click(step.Key == "route" && alternateRoute != null && step.Options.Contains(alternateRoute) ? alternateRoute : step.Options[0]);
                    if (Has("Focus.TimerToggle"))
                    {
                        Click("Focus.TimerToggle");
                        Assert(Ui.GetComponentInChildren<FocusPracticeClock>().Running, "Focus timer did not start.");
                        Click("Focus.TimerToggle");
                        Assert(!Ui.GetComponentInChildren<FocusPracticeClock>().Running, "Focus timer did not pause.");
                    }
                    if (Has("Focus.RefreshSteps")) Click("Focus.RefreshSteps");
                    if (Has("Focus.AudioToggle"))
                    {
                        Click("Focus.AudioToggle"); Click("Focus.AudioToggle"); Click("Focus.AudioReplay");
                    }
                    if (Has("Focus.AudioTranscript"))
                    {
                        Click("Focus.AudioTranscript"); Assert(VisibleText.Contains(FocusMissionCatalog.StoryText), "Story text fallback is missing."); Click("Focus.CloseTranscript");
                    }
                    if (Has("Focus.ContinueLater"))
                    {
                        string before = Latest.SessionId; Click("Focus.ContinueLater"); Click("기본 미션 이어서 하기");
                        Assert(Latest.SessionId == before && Latest.InstructionStep == index, "Continue-later lost the saved practice position.");
                    }
                    Capture(definition.Code + "-step-" + (index + 1), capture);
                    Click(step.OptionalInput && thinkOnly ? (Has("속으로 확인했어요") ? "속으로 확인했어요" : "건너뛰기") : "Focus.Action");
                    Assert(Latest.InstructionStep > index, "Practice action did not advance: " + definition.Code + " " + step.Key);
                }
                if(Has("Photo.Skip")&&!keepPhotoChoice)Click("Photo.Skip");
                Assert(Has("다음")||(keepPhotoChoice&&Has("Photo.Skip")), "Practice did not lead to optional photo or shared emotion.");
            }
            public void AssertVisibleAssets()
            {
                foreach (var image in Ui.GetComponentsInChildren<RawImage>(false))
                {
                    Assert(image.texture != null && image.texture.width > 0 && image.texture.height > 0,
                        "A visible mission asset is missing: " + image.name + " on " + Ui.CurrentScreen);
                    var size = image.rectTransform.rect.size;
                    Assert(size.x > 0 && size.y > 0 && Mathf.Abs(size.x / size.y - image.texture.width / (float)image.texture.height) < .02f,
                        "A visible mission asset is stretched: " + image.name + " on " + Ui.CurrentScreen);
                }
            }
            public void DrawSyntheticStroke()
            {
                Layout(); var surface = Ui.GetComponentsInChildren<FocusDrawingSurface>(false).FirstOrDefault(s => !s.ReadOnly);
                Assert(surface != null, "Editable drawing surface is missing.");
                var rect = surface.rectTransform; var center = rect.TransformPoint(rect.rect.center);
                var data = new PointerEventData(pointerContext) { button = PointerEventData.InputButton.Left,
                    position = RectTransformUtility.WorldToScreenPoint(camera, center), pointerPressRaycast = new RaycastResult { module = raycaster } };
                PrepareRaycastGraphics();
                var first = RaycastFixture(data).FirstOrDefault();
                Assert(first.gameObject != null && first.gameObject.GetComponentInParent<FocusDrawingSurface>() == surface,
                    "An empty drawing canvas does not receive the first stroke; first hit=" + first.gameObject?.name);
                report.raycastChecks++;
                surface.OnPointerDown(data); data.position += new Vector2(24, 24); surface.OnDrag(data); surface.OnPointerUp(data);
                Assert(FocusDrawingData.Decode(surface.Value).Any(s => s.Count > 1), "Drawing pointer action did not persist a stroke.");
            }
            public void CompleteReflection(string value, string mood, bool capture)
            {
                if (!string.IsNullOrEmpty(mood)) {if(!Has(mood))Click("Focus.MoreEmotions");Click(mood);}
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
                // This fixture is also used independently by mobile/course runs.
                Directory.CreateDirectory(OutputRoot);
                string path = Path.Combine(OutputRoot, name + ".png");
                int width=validationTarget.width,height=validationTarget.height;
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
                foreach (var entry in registeredGraphics) GraphicRegistry.UnregisterGraphicForCanvas(entry.Value, entry.Key);
                registeredGraphics.Clear();
                if (root != null)
                    foreach (var graphic in root.GetComponentsInChildren<Graphic>(true)) GraphicRegistry.UnregisterGraphicForCanvas(graphic.canvas, graphic);
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
