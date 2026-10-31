using System;
using System.IO;
using System.Linq;
using CapstoneDesign.Runtime;
using CapstoneDesign.Runtime.LocalState;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;
using Fixture = CapstoneDesign.EditorTools.FocusMissionValidation.Fixture;
using Report = CapstoneDesign.EditorTools.FocusMissionValidation.Report;

namespace CapstoneDesign.EditorTools
{
    // Explicit request only; the shared fixture uses disposable synthetic state and first-hit raycasts.
    [InitializeOnLoad]
    public static class AllCourseValidation
    {
        [Serializable] public sealed class Request { public string runId, mode; public bool capture = true; }
        static bool running;
        static double nextPoll;
        static string Root
        {
            get
            {
                string requested = Environment.GetEnvironmentVariable("CAPSTONE_ARTIFACTS");
                return Path.Combine(string.IsNullOrWhiteSpace(requested)
                    ? Path.Combine(Directory.GetCurrentDirectory(), "artifacts") : Path.GetFullPath(requested), "all-course-validation");
            }
        }
        static string RequestPath => Path.Combine(Root, "request.json");
        static AllCourseValidation() { EditorApplication.update += Poll; }
        static void Poll()
        {
            if (running || EditorApplication.timeSinceStartup < nextPoll) return;
            nextPoll = EditorApplication.timeSinceStartup + 1;
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating || !File.Exists(RequestPath)) return;
            var request = JsonUtility.FromJson<Request>(File.ReadAllText(RequestPath)) ?? new Request();
            File.Delete(RequestPath); Execute(request);
        }
        [MenuItem("Capstone Mockup/Validate All Course Navigation (Synthetic)")]
        public static void Run() => Execute(new Request { runId = "manual-" + DateTimeOffset.UtcNow.ToString("yyyyMMdd-HHmmss") });
        static void Execute(Request request)
        {
            if (running || EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling) return;
            running = true; Directory.CreateDirectory(Root);
            var report = new Report { runId = request.runId, utc = DateTimeOffset.UtcNow.ToString("O"), unityVersion = Application.unityVersion };
            try
            {
                if (GraphicsSettings.currentRenderPipeline != null && RenderPipelineManager.currentPipeline == null) Blitter.Cleanup();
                if (request.mode == "walking") Check(report, "Walking start freezes goal atomically, failed save retries, and story audio is available", () => WalkingStartAndAudio(report, request.capture));
                else if (request.mode == "variants") Check(report, "Input and focus-with-choice visual variants remain readable and actionable", () => VisualVariants(report, request.capture));
                else
                {
                    Check(report, "First mission entry asks course; recommendation draft remains independent", () => Chooser(report, request.capture));
                    Check(report, "All 17 recommendations and 12/24/48 course occurrence links are reachable", () => CatalogNavigation(report, request.capture));
                    Check(report, "Course-grouped history preserves per-step records and cross-course review", () => CourseRecords(report, request.capture));
                    Check(report, "Failed course selection preserves selection; active basic draft blocks switching", () => SelectionFailure(report, request.capture));
                    Check(report, "All 45 mission screens and alternate branches execute visible controls to saved completion", () => EveryMissionFlow(report, request.capture));
                }
            }
            finally { File.WriteAllText(Path.Combine(Root, "report.json"), JsonUtility.ToJson(report, true)); running = false; }
            Debug.Log("ALL_COURSE_VALIDATION passed=" + report.passed + " failed=" + report.failed + " clicks=" + report.buttonClicks + " raycasts=" + report.raycastChecks);
            if (report.failed > 0) throw new InvalidOperationException("AllCourseValidation: " + report.failed + " checks failed; inspect report.json.");
        }
        static void Check(Report report, string name, Action action)
        {
            try { action(); report.passed++; report.checks.Add(new FocusMissionValidation.CheckResult { name = name, status = "pass" }); }
            catch (Exception ex) { report.failed++; report.checks.Add(new FocusMissionValidation.CheckResult { name = name, status = "fail", detail = ex.ToString() }); Debug.LogWarning("ALL_COURSE_VALIDATION " + name + "\n" + ex); }
        }
        static void Assert(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
        static void Pick(Fixture f, string program) { f.Ui.ShowFocusProgramSelection(); f.Click("Focus.SelectProgram." + program); f.Click("Focus.ConfirmProgram." + program); }
        static void Chooser(Report report, bool capture)
        {
            using (var f = new Fixture(report, false, false))
            {
                Assert(f.Ui.CurrentScreen == "focus-programs" && !f.Service.Snapshot.MbctProgramChosen, "An unchosen course did not open selection.");
                foreach (string program in new[] { "2w", "4w", "8w" }) Assert(f.Has("Focus.SelectProgram." + program), "Course choice missing: " + program);
                Assert(f.Has("추천 미션 보기"), "Recommendation disappeared before course choice.");
                f.Capture("allcourse-01-course-choice", capture);
                f.Click("추천 미션 보기"); f.Click("추가 미션 시작하기");
                Assert(!f.Service.Snapshot.MbctProgramChosen && f.Latest.MissionId == "free-mission:M03", "Independent recommendation required a course.");
                string draft = f.Latest.SessionId;
                f.Click("닫기"); Pick(f, "2w");
                Assert(f.Ui.CurrentScreen == "home" && f.Service.Snapshot.MbctProgramId == "2w", "Selecting course with additional draft failed.");
                Assert(f.Service.Snapshot.Sessions.Single(a => a.SessionId == draft).Status == ParticipationState.Selected, "Course choice changed recommendation draft.");
                f.Capture("allcourse-02-selected-course-home", capture);
                f.Click("추가 미션 이어서 하기");
                Assert(f.Latest.SessionId == draft && f.Ui.CurrentScreen == "focus-practice", "Recommendation was not resumed.");
                f.Click("닫기");
                var reloaded = f.Store.Load();
                Assert(reloaded.MbctProgramChosen && reloaded.MbctProgramId == "2w" && reloaded.Sessions.Any(a => a.SessionId == draft), "Course and draft did not persist.");
            }
        }
        static void CatalogNavigation(Report report, bool capture)
        {
            using (var f = new Fixture(report, false, false))
            {
                f.Ui.ShowFocusMissionLibrary();
                Assert(FocusMissionCatalog.Additional.Length == 17, "Additional catalog is incomplete.");
                foreach (var definition in FocusMissionCatalog.All)
                {
                    var texture = Resources.Load<Texture2D>(definition.IconAsset);
                    Assert(texture != null && texture.width > 0 && texture.height > 0, "Catalog icon is missing: " + definition.Code);
                }
                foreach (var mission in FocusMissionCatalog.Additional)
                {
                    f.Click("Focus.FreeMission." + mission.Code);
                    Assert(f.VisibleText.Contains(mission.Title) && f.Has("추가 미션 시작하기"), "Recommendation detail missing: " + mission.Code);
                    f.Click("추천·자유 미션 모두 보기");
                }
                f.Capture("allcourse-03-additional-catalog", capture);
                foreach (string program in new[] { "2w", "4w", "8w" })
                {
                    Pick(f, program); f.Click("내 과정"); f.Click("Focus.CourseRecords." + program);
                    int total = FocusMissionPolicy.TotalPracticesFor(program);
                    Assert(f.VisibleText.Contains("완료한 실습 0 / " + total), "Course total is wrong.");
                    Assert(!f.VisibleText.Contains("1주차 미션"), "History retained weekly groups.");
                    f.Click("Focus.CurriculumToggle");
                    Assert(f.Ui.GetComponentsInChildren<Button>(false).Count(b => b.name.StartsWith("Focus.Occurrence." + program + ".", StringComparison.Ordinal)) == total, "Not all course occurrences are listed.");
                    f.Click("Focus.Occurrence." + program + "." + total);
                    Assert(f.VisibleText.Contains(FocusMissionPolicy.MissionAt(total, program).Title) && f.VisibleText.Contains("아직 저장된 수행 기록이 없어요"), "Future occurrence information is inaccessible.");
                    f.Click("코스 기록으로 돌아가기"); f.Capture("allcourse-04-" + program + "-records", capture); f.Click("내 과정으로 돌아가기");
                }
            }
        }
        static void CourseRecords(Report report, bool capture)
        {
            using (var f = new Fixture(report, false, false))
            {
                Pick(f, "2w"); string two = CompleteSynthetic(f, "2w", "synthetic two-week note");
                f.Clock.UtcNow = f.Clock.UtcNow.AddDays(1); Assert(f.Ui.RefreshCycle(), f.Service.LastError);
                Pick(f, "4w"); string four = CompleteSynthetic(f, "4w", "synthetic four-week note");
                string additional = CompleteSynthetic(f, "additional", "synthetic additional note");
                f.Ui.ShowFocusCourse();
                Assert(f.VisibleText.Contains("완료한 실습 1 / 12") && f.VisibleText.Contains("완료한 실습 1 / 24"), "Course progress was combined across courses.");
                f.Capture("allcourse-05-course-grouped-history", capture);
                f.Click("Focus.CourseRecords.2w"); f.Click("저장된 기록 보기");
                Assert(f.Has("Focus.Record." + two) && !f.Has("Focus.Record." + four) && !f.Has("Focus.Record." + additional), "Course history contains another course or independent recommendation.");
                f.Click("Focus.Record." + two);f.Click("Focus.RecordStepsToggle");
                Assert(f.VisibleText.Contains("synthetic two-week note") && f.VisibleText.Contains("2주 코스") && f.VisibleText.Contains("확인 ·"), "Detailed course response or timestamp was lost.");
                f.Capture("allcourse-06-deep-record", capture);
                f.Click("다시 연습하기 · 추가 보상 없음");
                Assert(f.Latest.FocusReview && f.Latest.FocusProgramId == "2w" && f.Service.Snapshot.MbctProgramId == "4w", "Archived-course review changed selected course or membership.");
                Assert(f.Service.EndParticipation(f.Latest.SessionId), f.Service.LastError);
                f.Ui.ShowFocusCourse(); f.Click("추천·자유 미션 기록");
                Assert(f.Has("Focus.Record." + additional) && !f.Has("Focus.Record." + two), "Independent recommendation history was not separate.");
                var reloaded = f.Store.Load();
                Assert(reloaded.Sessions.Single(a => a.SessionId == two).FocusStepRecords.All(r => !string.IsNullOrEmpty(r.ConfirmedUtc)), "Step confirmations did not survive storage.");
                Assert(FocusMissionPolicy.CompletedCount(reloaded, "2w") == 1 && FocusMissionPolicy.CompletedCount(reloaded, "4w") == 1, "Reload mixed per-course progression.");
            }
        }
        static void SelectionFailure(Report report, bool capture)
        {
            using (var f = new Fixture(report, false, false))
            {
                Pick(f, "2w"); f.Ui.ShowFocusProgramSelection(); f.Click("Focus.SelectProgram.4w");
                f.Store.FailNextSaves = 1; f.Click("Focus.ConfirmProgram.4w");
                Assert(f.Service.Snapshot.MbctProgramId == "2w" && f.Ui.CurrentScreen == "focus-program-confirm" && f.VisibleText.Contains("저장하지 못했어요"), "Failed save changed course or dropped retry.");
                f.Capture("allcourse-07-course-save-failure", capture); f.Click("Focus.ConfirmProgram.4w");
                Assert(f.Service.Snapshot.MbctProgramId == "4w" && f.Ui.CurrentScreen == "home", "Retry did not select course.");
                f.Click("기본 미션 시작하기"); string draft = f.Latest.SessionId; f.Click("닫기");
                f.Ui.ShowFocusProgramSelection(); f.Click("Focus.SelectProgram.8w");
                var confirm = f.Ui.GetComponentsInChildren<Button>(false).Single(b => b.name == "Focus.ConfirmProgram.8w");
                Assert(!confirm.interactable && f.VisibleText.Contains("진행 중인 미션을 먼저 마쳐주세요"), "Course switch is enabled over active basic draft.");
                Assert(f.Service.Snapshot.MbctProgramId == "4w" && f.Service.Snapshot.Sessions.Any(a => a.SessionId == draft), "Blocked switch changed draft or course.");
            }
        }
        static string CompleteSynthetic(Fixture f, string program, string note)
        {
            bool additional = program == "additional"; string id = "synthetic-" + Guid.NewGuid().ToString("N");
            int order = additional ? 0 : FocusMissionPolicy.NextOrder(f.Service.Snapshot, program);
            var mission = additional ? FocusMissionCatalog.Find("M03") : FocusMissionPolicy.MissionAt(order, program);
            Assert(f.Service.BeginFocusSession(id, mission.Id, order, false, additional ? null : program), f.Service.LastError); Assert(f.Service.StartSession(id), f.Service.LastError);
            while (true)
            {
                var current = f.Service.Snapshot.Sessions.Single(a => a.SessionId == id); if (current.InstructionStep >= mission.Steps.Length) break;
                int step = current.InstructionStep; var instruction = mission.Steps[step];
                string answer = instruction.Options.FirstOrDefault() ?? (instruction.OptionalInput ? "synthetic response" : null);
                if (instruction.RecommendedSeconds > 0) Assert(f.Service.SaveFocusElapsed(id, step, instruction.RecommendedSeconds), f.Service.LastError);
                Assert(f.Service.ConfirmFocusStep(id, step, answer), f.Service.LastError);
            }
            Assert(f.Service.CompleteFocusSession(id, "편안함", note), f.Service.LastError); return id;
        }
        static void EveryMissionFlow(Report report, bool capture)
        {
            using (var f = new Fixture(report, false, false))
            {
                Pick(f, "8w");
                foreach (string code in FocusMissionPolicy.CourseCodes("8w").Take(28))
                {
                    f.Ui.ShowHome(); f.Click("기본 미션 시작하기");
                    var mission = FocusMissionCatalog.Find(code);
                    Assert(f.Latest.MissionId == mission.Id, "Wrong scheduled mission before UI traversal: " + code);
                    f.AssertVisibleAssets();
                    f.Click("시작"); f.CompletePractice(mission, capture && (code == "P01" || code == "P19" || code == "P24" || code == "P28"));
                    f.CompleteReflection("synthetic " + code + " record", "", false);
                    Assert(f.Latest.Status == ParticipationState.RewardCommitted, code + " did not complete through its visible controls.");
                    f.AssertVisibleAssets();
                    f.Click("Focus.ConfirmHome"); f.Clock.UtcNow = f.Clock.UtcNow.AddDays(2); Assert(f.Ui.RefreshCycle(), f.Service.LastError);
                }
                foreach (var mission in FocusMissionCatalog.Additional)
                {
                    f.Ui.ShowFocusRecommendationFor(mission.Id); f.Click("추가 미션 시작하기"); f.Click("시작");
                    ActivitySession walking = null;
                    if (mission.Code == "M15")
                    {
                        walking = f.Latest;
                        Assert(walking.WalkingGoalSteps > 0 && !string.IsNullOrEmpty(walking.WalkingQueryStartUtc) && !string.IsNullOrEmpty(walking.WalkingQueryEndUtc), "Walking target and query interval were not frozen before practice.");
                        Assert(f.Service.ObserveMissionSteps(walking.SessionId, walking.WalkingGoalSteps, WalkingPersonalization.AggregateSource), f.Service.LastError);
                        string beforeAuto = StateCodec.Encode(f.Service.Snapshot);
                        typeof(WeekOneQuestDemo).GetMethod("CompleteWalkingGoal", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).Invoke(f.Ui, new object[] { walking.SessionId });
                        Assert(StateCodec.Encode(f.Service.Snapshot) == beforeAuto && f.Ui.CurrentScreen == "focus-practice", "Legacy walking completion bypassed the focused guidance or reflection draft.");
                    }
                    f.CompletePractice(mission, capture && (mission.Code == "M11" || mission.Code == "M15"));
                    f.CompleteReflection("synthetic " + mission.Code + " record", "", false);
                    Assert(f.Latest.Status == ParticipationState.RewardCommitted, mission.Code + " did not save through its visible controls.");
                    f.AssertVisibleAssets();
                    if (walking != null)
                    {
                        var saved = f.Latest;
                        Assert(saved.WalkingGoalSteps == walking.WalkingGoalSteps && saved.WalkingQueryStartUtc == walking.WalkingQueryStartUtc && saved.WalkingQueryEndUtc == walking.WalkingQueryEndUtc, "Walking refresh changed the target or query interval.");
                        f.Ui.ShowFocusRecord(saved.SessionId);
                        Assert(f.VisibleText.Contains("시작할 때 정한 걷기 목표") && f.VisibleText.Contains(saved.WalkingGoalSteps.ToString("N0") + "걸음") && f.VisibleText.Contains("걸음 확인 구간"), "Walking record omitted its saved target and query interval.");
                        f.Capture("allcourse-10-saved-walking-target", capture);
                    }
                    if (mission.Code == "M11")
                    {
                        f.Ui.ShowFocusRecord(f.Latest.SessionId);
                        Assert(f.Ui.GetComponentsInChildren<FocusDrawingSurface>(false).Any(s => s.ReadOnly && !s.raycastTarget && FocusDrawingData.Decode(s.Value).Count > 0), "Saved drawing is not shown as a read-only drawing.");
                        Assert(!f.VisibleText.Contains("DRAW1|"), "Encoded drawing data leaked into the user record.");
                        f.Capture("allcourse-08-saved-drawing", capture);
                    }
                }
                foreach (var branch in new[] { new[] { "P19", "짧은 소리 실습" }, new[] { "P24", "앱의 짧은 이야기" }, new[] { "P26", "아직 없어요" } })
                {
                    var mission = FocusMissionCatalog.Find(branch[0]); int order = Array.IndexOf(FocusMissionPolicy.CourseCodes("8w"), branch[0]) + 1;
                    f.Ui.StartFocusReview(mission.Id, order, "8w"); f.Click("시작"); f.CompletePractice(mission, capture, false, branch[1]);
                    f.CompleteReflection("synthetic alternate " + branch[0], "", false);
                    Assert(f.Latest.FocusReview && f.Latest.RewardAmount == 0, "Branch review changed rewards.");
                    if (branch[0] != "P24") Assert(f.Latest.FocusStepRecords.Any(r => r.Skipped), "Alternative route did not skip inapplicable steps.");
                }
                Assert(f.Service.Snapshot.Sessions.Where(a => a.Status == ParticipationState.RewardCommitted).Select(a => a.MissionId).Distinct().Count() == 45, "UI completion coverage is missing a mission.");
            }
        }
        static void VisualVariants(Report report, bool capture)
        {
            using (var f = new Fixture(report, false, false))
            {
                Pick(f, "8w");
                for (int i = 1; i < 18; i++)
                {
                    CompleteSynthetic(f, "8w", "synthetic setup " + i);
                    f.Clock.UtcNow = f.Clock.UtcNow.AddDays(2); Assert(f.Ui.RefreshCycle(), f.Service.LastError);
                }
                f.Ui.ShowHome(); f.Click("기본 미션 시작하기"); f.Click("시작");
                f.CompletePractice(FocusMissionCatalog.Find("P18"), capture); f.CompleteReflection("synthetic P18 visual", "", false);
                f.Ui.ShowFocusRecommendationFor("M10"); f.Click("추가 미션 시작하기"); f.Click("시작");
                f.CompletePractice(FocusMissionCatalog.Find("M10"), capture); f.CompleteReflection("synthetic M10 visual", "", false);
                f.Ui.ShowFocusRecommendationFor("M11"); f.Click("추가 미션 시작하기"); f.Click("시작");
                f.CompletePractice(FocusMissionCatalog.Find("M11"), capture); f.CompleteReflection("synthetic M11 visual", "", false);
                f.Ui.ShowFocusRecord(f.Latest.SessionId); f.Capture("allcourse-09-saved-drawing-visual", capture);
            }
        }
        static void WalkingStartAndAudio(Report report, bool capture)
        {
            using (var f = new Fixture(report, false, false))
            {
                var clip = Resources.Load<AudioClip>("FocusMissions/Audio/P24-story");
                Assert(clip != null && clip.length > 1 && clip.samples > 0, "The app story audio is missing or empty.");
                f.Ui.ShowFocusRecommendationFor("M15"); f.Click("추가 미션 시작하기");
                f.Store.FailNextSaves = 1; f.Click("시작");
                Assert(f.Latest.Status == ParticipationState.Selected && f.Latest.WalkingGoalSteps == 0 && f.Ui.CurrentScreen == "focus-practice", "Failed walking start partially advanced or froze only the target.");
                f.Capture("allcourse-11-walking-start-failed", capture); f.Click("시작");
                var walking = f.Latest;
                Assert(walking.Status == ParticipationState.InProgress && walking.WalkingGoalSteps > 0 && !string.IsNullOrEmpty(walking.WalkingQueryStartUtc) && !string.IsNullOrEmpty(walking.WalkingQueryEndUtc), "Walking start did not atomically persist the target and query period.");
                f.AssertVisibleAssets(); f.Capture("allcourse-12-walking-started", capture);
                f.Clock.UtcNow = f.Clock.UtcNow.AddDays(1); Assert(f.Ui.RefreshCycle(), f.Service.LastError);
                f.Click("Focus.RefreshSteps");
                Assert(f.Latest.WalkingGoalSteps == walking.WalkingGoalSteps && f.Latest.WalkingQueryStartUtc == walking.WalkingQueryStartUtc && f.Latest.WalkingQueryEndUtc == walking.WalkingQueryEndUtc, "Date change or refresh reinterpreted an already saved walking goal.");
                Assert(f.Service.ObserveMissionSteps(walking.SessionId, walking.WalkingGoalSteps, WalkingPersonalization.AggregateSource), f.Service.LastError);
                string beforeAuto = StateCodec.Encode(f.Service.Snapshot);
                typeof(WeekOneQuestDemo).GetMethod("CompleteWalkingGoal", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).Invoke(f.Ui, new object[] { walking.SessionId });
                Assert(StateCodec.Encode(f.Service.Snapshot) == beforeAuto && f.Ui.CurrentScreen == "focus-practice", "Step achievement bypassed the focus guidance.");
                f.CompletePractice(FocusMissionCatalog.Find("M15"), capture); f.CompleteReflection("synthetic frozen walking target", "", false);
                f.Ui.ShowFocusRecord(f.Latest.SessionId);
                Assert(f.VisibleText.Contains("시작할 때 정한 걷기 목표") && f.VisibleText.Contains(walking.WalkingGoalSteps.ToString("N0") + "걸음") && f.VisibleText.Contains("걸음 확인 구간"), "Walking detail lost its saved target.");
                f.Capture("allcourse-13-walking-record", capture);
            }
        }
    }
}
