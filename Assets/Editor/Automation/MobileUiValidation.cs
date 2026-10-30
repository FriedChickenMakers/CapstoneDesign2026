using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
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
    // Run only by explicit request. Every case owns disposable synthetic state;
    // no installed-app data, preferences, device capture, or scene is changed.
    [InitializeOnLoad]
    public static class MobileUiValidation
    {
        [Serializable] public sealed class Request { public string runId; public bool capture = true; }
        static bool running;
        static double nextPoll;
        static string OutputRoot => Path.Combine(Directory.GetCurrentDirectory(), "artifacts", "mobile-ui-validation");
        static string RequestPath => Path.Combine(OutputRoot, "request.json");
        static MobileUiValidation() { EditorApplication.update += Poll; }
        static void Poll()
        {
            if (running || EditorApplication.timeSinceStartup < nextPoll) return;
            nextPoll = EditorApplication.timeSinceStartup + 1;
            if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode || !File.Exists(RequestPath)) return;
            Request request;
            try { request = JsonUtility.FromJson<Request>(File.ReadAllText(RequestPath)) ?? new Request(); }
            catch (Exception ex)
            {
                File.Delete(RequestPath);
                var invalid = new Report { runId = "invalid-request", utc = DateTimeOffset.UtcNow.ToString("O"), unityVersion = Application.unityVersion, failed = 1 };
                invalid.checks.Add(new FocusMissionValidation.CheckResult { name = "Explicit validation request", status = "fail", detail = ex.Message });
                File.WriteAllText(Path.Combine(OutputRoot, "report.json"), JsonUtility.ToJson(invalid, true)); return;
            }
            File.Delete(RequestPath); Execute(request);
        }

        [MenuItem("Capstone Mockup/Validate Mobile UI (Synthetic)")]
        public static void Run() => Execute(new Request { runId = "manual-" + DateTimeOffset.UtcNow.ToString("yyyyMMdd-HHmmss") });
        static void Execute(Request request)
        {
            if (running || EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling)
                throw new InvalidOperationException("Run mobile validation after compilation, outside Play mode.");
            running = true; Directory.CreateDirectory(OutputRoot);
            var report = new Report { runId = request.runId ?? "requested", utc = DateTimeOffset.UtcNow.ToString("O"), unityVersion = Application.unityVersion };
            try
            {
                if (GraphicsSettings.currentRenderPipeline != null && RenderPipelineManager.currentPipeline == null) Blitter.Cleanup();
                foreach (var size in new[] { new Vector2Int(360, 640), new Vector2Int(390, 844), new Vector2Int(412, 915) })
                {
                    var viewport = size;
                    Check(report, "Mobile viewport " + size.x + "x" + size.y + ": first action, readable regions and retry", () => MobileViewport(report, request.capture, viewport));
                }
                Check(report, "Optional catalog filters retain all 17 missions", () => CatalogFilters(report, request.capture));
                Check(report, "Course history prioritizes current course and saved records; curriculum and audit disclose on demand", () => CourseAndRecordDisclosure(report, request.capture));
                Check(report, "Required inputs explain completion and retain the accepted response", () => RequiredInput(report, request.capture));
                Check(report, "Drawing clear can be undone and restored strokes survive navigation", () => DrawingUndo(report, request.capture));
                Check(report, "Skip optional reflection commits once; failure preserves the draft and retries", () => SkipReflectionRetry(report, request.capture));
                Check(report, "Timer explicitly starts and pauses, permits manual finish at zero and persists elapsed time", () => TimerControls(report, request.capture));
                Check(report, "Story player exposes audio duration, playback controls and readable transcript fallback", () => StoryPlayer(report, request.capture));
                Check(report, "All 45 mission action colors meet small-text contrast", ActionContrast);
            }
            finally { File.WriteAllText(Path.Combine(OutputRoot, "report.json"), JsonUtility.ToJson(report, true)); running = false; }
            Debug.Log("MOBILE_UI_VALIDATION passed=" + report.passed + " failed=" + report.failed + " clicks=" + report.buttonClicks + " raycasts=" + report.raycastChecks);
        }
        static void Check(Report report, string name, Action action)
        {
            try { action(); report.passed++; report.checks.Add(new FocusMissionValidation.CheckResult { name = name, status = "pass" }); }
            catch (Exception ex) { report.failed++; report.checks.Add(new FocusMissionValidation.CheckResult { name = name, status = "fail", detail = ex.ToString() }); Debug.LogWarning("MOBILE_UI_VALIDATION " + name + "\n" + ex); }
        }
        static void Assert(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
        static T Named<T>(Fixture f, string name) where T : Component => f.Ui.GetComponentsInChildren<T>(true).FirstOrDefault(c => c.name == name);
        static Button Button(Fixture f, string name) => Named<Button>(f, name);
        static void Settle(Fixture f)
        {
            Canvas.ForceUpdateCanvases();
            foreach (var fit in f.Ui.GetComponentsInChildren<FigmaMissionCanvasFit>(false)) fit.Fit();
            foreach (var layout in f.Ui.GetComponentsInChildren<FocusMobilePageLayout>(false)) layout.Refresh();
            Canvas.ForceUpdateCanvases();
        }
        static Rect BoundsIn(RectTransform item, RectTransform relative)
        {
            var corners = new Vector3[4]; item.GetWorldCorners(corners);
            var points = corners.Select(p => (Vector2)relative.InverseTransformPoint(p)).ToArray();
            return Rect.MinMaxRect(points.Min(p => p.x), points.Min(p => p.y), points.Max(p => p.x), points.Max(p => p.y));
        }
        static bool Contains(Rect parent, Rect child) => child.xMin >= parent.xMin - 1 && child.xMax <= parent.xMax + 1 && child.yMin >= parent.yMin - 1 && child.yMax <= parent.yMax + 1;
        static bool Overlap(Rect a, Rect b) => Mathf.Min(a.xMax, b.xMax) - Mathf.Max(a.xMin, b.xMin) > 1 && Mathf.Min(a.yMax, b.yMax) - Mathf.Max(a.yMin, b.yMin) > 1;
        static void VisibleWithoutScroll(Fixture f, Button action)
        {
            Settle(f); Assert(action != null && action.gameObject.activeInHierarchy && action.interactable, "Required first action is missing or disabled.");
            var design = f.Ui.GetComponentInChildren<FigmaMissionCanvasFit>().Design;
            var target = BoundsIn((RectTransform)action.transform, design);
            Assert(Contains(design.rect, target), "Required first action is outside the phone surface: " + action.name);
            foreach (var scroll in action.GetComponentsInParent<ScrollRect>())
                Assert(Contains(BoundsIn(scroll.viewport, design), target), "Required first action needs scrolling: " + action.name);
        }
        static void SeparateRegions(Fixture f, bool expectNotice = false)
        {
            Settle(f);
            var layout = f.Ui.GetComponentInChildren<FocusMobilePageLayout>(); Assert(layout != null, "Responsive page layout is missing.");
            var design = (RectTransform)layout.transform;
            var regions = new List<KeyValuePair<string, Rect>>();
            foreach (string name in new[] { "body", "footer", "navigation" })
            {
                var item = typeof(FocusMobilePageLayout).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(layout) as RectTransform;
                if (item == null || !item.gameObject.activeInHierarchy) continue;
                var bounds = BoundsIn(item, design); Assert(Contains(design.rect, bounds), name + " leaves the safe phone surface.");
                regions.Add(new KeyValuePair<string, Rect>(name, bounds));
            }
            bool notice = layout.Notice != null && layout.Notice.gameObject.activeInHierarchy;
            Assert(!expectNotice || notice, "A save failure has no visible retry explanation.");
            if (notice)
            {
                var bounds = BoundsIn(layout.Notice.rectTransform, design); Assert(Contains(design.rect, bounds), "Save notice leaves the phone surface.");
                regions.Add(new KeyValuePair<string, Rect>("notice", bounds));
            }
            foreach (Transform child in design)
            {
                var title = child.GetComponent<Text>(); var close = child.GetComponent<Button>();
                if ((title == null || title.fontSize < 20) && (close == null || close.name != "닫기")) continue;
                var bounds = BoundsIn((RectTransform)child, design);
                Assert(Contains(design.rect, bounds), "Fixed header leaves the phone surface: " + child.name);
                foreach (var region in regions) Assert(!Overlap(bounds, region.Value), "Fixed header overlaps " + region.Key + ": " + child.name);
            }
            for (int i = 0; i < regions.Count; i++) for (int j = i + 1; j < regions.Count; j++)
                Assert(!Overlap(regions[i].Value, regions[j].Value), regions[i].Key + " overlaps " + regions[j].Key + " on " + f.Ui.CurrentScreen);
        }
        static void NoStepCounter(Fixture f)
        {
            Assert(!f.Ui.GetComponentsInChildren<Text>(false).Any(t => Regex.IsMatch(t.text, @"(?<![\d:])\b\d+\s*/\s*\d+\b(?!:)")), "Mission step count is still displayed.");
            Assert(!f.Ui.GetComponentsInChildren<Transform>(false).Any(t => Regex.IsMatch(t.name, @"^단계 \d+$")), "Mission step progress bar is still present.");
        }
        static void MobileViewport(Report report, bool capture, Vector2Int size)
        {
            using (var f = new Fixture(report, false, false))
            {
                f.Resize(size.x, size.y); f.Ui.ShowFocusProgramSelection();
                VisibleWithoutScroll(f, Button(f, "추천 미션 보기")); SeparateRegions(f);
                f.Capture("mobile-" + size.x + "x" + size.y + "-chooser", capture);
                f.Click("추천 미션 보기"); f.Click("추가 미션 시작하기"); f.Click("Focus.Action");
                Assert(!f.Service.Snapshot.MbctProgramChosen && f.Latest.MissionId == "free-mission:M03", "Independent recommendation unexpectedly selected a course.");
                NoStepCounter(f); SeparateRegions(f);
                VisibleWithoutScroll(f, Button(f, "Focus.Action"));
                f.Capture("mobile-" + size.x + "x" + size.y + "-practice", capture);
                f.Click("닫기"); f.Ui.ShowFocusProgramSelection(); f.Click("Focus.SelectProgram.4w");
                f.Store.FailNextSaves = 1; f.Click("Focus.ConfirmProgram.4w");
                Assert(!f.Service.Snapshot.MbctProgramChosen && f.Ui.CurrentScreen == "focus-program-confirm", "Failed selection changed course state.");
                SeparateRegions(f, true); f.Capture("mobile-" + size.x + "x" + size.y + "-error", capture);
                f.Click("Focus.ConfirmProgram.4w"); Assert(f.Service.Snapshot.MbctProgramId == "4w", "Retry did not select the requested course.");
                SeparateRegions(f); f.Capture("mobile-" + size.x + "x" + size.y + "-home", capture);
            }
        }
        static void CatalogFilters(Report report, bool capture)
        {
            using (var f = new Fixture(report, false, false))
            {
                f.Resize(360, 640); f.Ui.ShowFocusMissionLibrary();
                Func<string[]> codes = () => f.Ui.GetComponentsInChildren<Button>(false).Where(b => b.name.StartsWith("Focus.FreeMission.", StringComparison.Ordinal)).Select(b => b.name.Substring("Focus.FreeMission.".Length)).ToArray();
                Assert(codes().Length == 17, "The unfiltered catalog does not expose all 17 additional missions.");
                var union = new HashSet<string>();
                foreach (string filter in new[] { "감각", "몸 돌봄", "마음 기록" })
                {
                    f.Click("Focus.LibraryFilter." + filter); var subset = codes();
                    Assert(subset.Length > 0 && subset.Length < 17, "Category does not narrow the catalog: " + filter);
                    foreach (string code in subset) union.Add(code);
                }
                Assert(union.SetEquals(FocusMissionCatalog.Additional.Select(m => m.Code)), "Category filters hide or invent missions.");
                f.Ui.ShowFocusMissionLibrary(); Assert(codes().Length == 17, "All-missions entry retained a hidden filter.");
                f.Click("Focus.FreeMission.M17"); Assert(f.VisibleText.Contains("안내 따라 호흡"), "A catalog result does not open its mission.");
                f.Ui.ShowFocusMissionLibrary(); f.Capture("mobile-catalog", capture); SeparateRegions(f);
            }
        }
        static void Pick(Fixture f, string program)
        { f.Ui.ShowFocusProgramSelection(); f.Click("Focus.SelectProgram." + program); f.Click("Focus.ConfirmProgram." + program); }
        static string CompleteNext(Fixture f, string program, string note)
        {
            string id = "mobile-synthetic-" + Guid.NewGuid().ToString("N"); int order = FocusMissionPolicy.NextOrder(f.Service.Snapshot, program);
            var mission = FocusMissionPolicy.MissionAt(order, program);
            Assert(f.Service.BeginFocusSession(id, mission.Id, order, false, program), f.Service.LastError); Assert(f.Service.StartSession(id), f.Service.LastError);
            while (true)
            {
                var entry = f.Service.Snapshot.Sessions.Single(s => s.SessionId == id); if (entry.InstructionStep >= mission.Steps.Length) break;
                var step = mission.Steps[entry.InstructionStep]; string answer = step.Options.FirstOrDefault() ?? (step.OptionalInput || step.RequiredAnswer ? "합성 검증 응답" : null);
                Assert(f.Service.ConfirmFocusStep(id, entry.InstructionStep, answer), f.Service.LastError);
            }
            Assert(f.Service.CompleteFocusSession(id, "편안함", note), f.Service.LastError); return id;
        }
        static void CourseAndRecordDisclosure(Report report, bool capture)
        {
            using (var f = new Fixture(report, false, false))
            {
                f.Resize(390, 844); Pick(f, "2w"); string older = CompleteNext(f, "2w", "합성 기록: 창가에서 잠깐 쉬었어요");
                f.Clock.UtcNow = f.Clock.UtcNow.AddDays(2); Assert(f.Ui.RefreshCycle(), f.Service.LastError);
                Pick(f, "8w"); string current = CompleteNext(f, "8w", "합성 기록: 숨의 움직임을 느꼈어요");
                f.Ui.ShowFocusCourse();
                var rows = f.Ui.GetComponentsInChildren<Button>(false).Where(b => b.name.StartsWith("Focus.CourseRecords.", StringComparison.Ordinal)).OrderBy(b => -((RectTransform)b.transform).anchoredPosition.y).ToArray();
                Assert(rows.Length == 3 && rows[0].name == "Focus.CourseRecords.8w", "Current course is not first in My Journey.");
                Assert(!f.VisibleText.Contains("1주차 미션"), "Records reverted to weekly grouping."); f.Capture("mobile-course-groups", capture);
                f.Click("Focus.CourseRecords.8w");
                Assert(f.Has("Focus.Record." + current) && !f.Has("Focus.Record." + older), "Saved course records are missing or mixed.");
                Assert(!f.Ui.GetComponentsInChildren<Button>(false).Any(b => b.name.StartsWith("Focus.Occurrence.", StringComparison.Ordinal)), "Future curriculum is expanded by default ahead of records.");
                f.Capture("mobile-course-records-first", capture); f.Click("Focus.CurriculumToggle");
                Assert(f.Ui.GetComponentsInChildren<Button>(false).Count(b => b.name.StartsWith("Focus.Occurrence.8w.", StringComparison.Ordinal)) == 48, "Expanded 8-week curriculum is incomplete.");
                f.Click("Focus.Occurrence.8w.48"); Assert(f.VisibleText.Contains(FocusMissionPolicy.MissionAt(48, "8w").Title), "Last course occurrence is unreachable.");
                f.Ui.ShowFocusRecord(current);
                Assert(f.VisibleText.Contains("합성 기록: 숨의 움직임을 느꼈어요") && f.VisibleText.Contains("편안함"), "Personal record summary is missing.");
                Assert(!f.VisibleText.Contains("확인 ·"), "Step timestamps are exposed before the audit is expanded.");
                f.Capture("mobile-record-personal-first", capture); f.Click("Focus.RecordStepsToggle");
                Assert(f.VisibleText.Contains("확인 ·") && f.VisibleText.Contains("행동 확인"), "Expanded audit omits saved execution evidence.");
                f.Click("Focus.RecordStepsToggle"); Assert(!f.VisibleText.Contains("확인 ·"), "Audit did not collapse.");
                Assert(f.Store.Load().Sessions.Single(s => s.SessionId == current).Note == "합성 기록: 숨의 움직임을 느꼈어요", "Reading a record changed saved content.");
            }
        }
        static void StartAdditional(Fixture f, string code)
        { f.Ui.ShowFocusRecommendationFor(code); f.Click("추가 미션 시작하기"); f.Click("Focus.Action"); }
        static void RequiredInput(Report report, bool capture)
        {
            foreach (string code in new[] { "M10", "M07" })
            using (var f = new Fixture(report, false, false))
            {
                f.Resize(360, 640); StartAdditional(f, code);
                Assert(f.VisibleText.Contains("필수") && Named<Text>(f, "Focus.InputHelp") != null, "Required input has no persistent label or help: " + code);
                Assert(!Button(f, "Focus.Action").interactable, "Empty required input can advance: " + code);
                f.SetInput("   "); Assert(!Button(f, "Focus.Action").interactable, "Whitespace bypasses required input.");
                if (code == "M07") f.Click("Focus.Option.잘 모르겠어요"); else f.SetInput("합성 계획: 점심 뒤 창문 열기");
                Assert(Button(f, "Focus.Action").interactable && !string.IsNullOrWhiteSpace(f.Input.text), "Valid input does not enable completion.");
                string answer = f.Input.text; SeparateRegions(f); f.Capture("mobile-" + code + "-required-input", capture);
                var design = f.Ui.GetComponentInChildren<FigmaMissionCanvasFit>().Design;
                var inputBounds = BoundsIn((RectTransform)f.Input.transform, design);
                var helpBounds = BoundsIn(Named<Text>(f, "Focus.InputHelp").rectTransform, design);
                Assert(!Overlap(inputBounds, helpBounds) && Mathf.Abs(helpBounds.yMax - inputBounds.yMin) <= 20, "Required-input help is detached from or overlaps the field.");
                if (code == "M10")
                {
                    f.Store.FailNextSaves = 1; f.Click("Focus.Action");
                    Assert(f.Latest.InstructionStep == 0 && f.Input.text == answer, "Failed input confirmation discarded the response or advanced practice.");
                    SeparateRegions(f, true);
                }
                f.Click("Focus.Action"); Assert(f.Latest.FocusStepRecords[0].Value == answer && !string.IsNullOrEmpty(f.Latest.FocusStepRecords[0].ConfirmedUtc), "Accepted required response was not persisted.");
            }
        }
        static void DrawingUndo(Report report, bool capture)
        {
            using (var f = new Fixture(report, false, false))
            {
                f.Resize(360, 640); StartAdditional(f, "M11"); f.DrawSyntheticStroke();
                var drawing = f.Ui.GetComponentInChildren<FocusDrawingSurface>(); string before = drawing.Value;
                f.Click("Focus.DrawingClear"); Assert(FocusDrawingData.Decode(drawing.Value).Count == 0 && !Button(f, "Focus.Action").interactable, "Clear did not empty the drawing or left empty completion enabled.");
                f.Click("Focus.DrawingUndo"); Assert(drawing.Value == before && Button(f, "Focus.Action").interactable, "Undo did not restore the exact pre-clear drawing.");
                Assert(f.Store.Load().Sessions.Single(s => s.SessionId == f.Latest.SessionId).FocusStepRecords[0].Value == before, "Restored drawing was not persisted.");
                f.Capture("mobile-drawing-restored", capture); f.Click("닫기"); f.Ui.ShowFocusRecommendationFor("M11"); f.Click("추가 미션 이어서 하기");
                Assert(f.Ui.GetComponentInChildren<FocusDrawingSurface>().Value == before, "Restored drawing was lost on resume.");
            }
        }
        static void FinishSimplePractice(Fixture f)
        {
            int guard = 0;
            while (f.Ui.CurrentScreen == "focus-practice")
            {
                Assert(++guard < 20, "Simple practice did not finish.");
                f.Click("Focus.Action");
            }
            Assert(f.Ui.CurrentScreen == "focus-emotion", "Practice did not reach shared optional reflection."); NoStepCounter(f);
        }
        static void SkipReflectionRetry(Report report, bool capture)
        {
            using (var f = new Fixture(report, false, false))
            {
                f.Resize(360, 640); StartAdditional(f, "M03"); FinishSimplePractice(f); f.Click("Focus.Emotion.편안함");
                f.Click("Focus.ReflectionNext"); f.SetInput("합성 초안: 한 모금 쉬어갔어요"); f.Click("Focus.Back");
                string id = f.Latest.SessionId; long nutrient = f.Service.Snapshot.Nutrient; int receipts = f.Service.Snapshot.RewardReceipts.Count;
                f.Store.FailNextSaves = 1; f.Click("Focus.SkipReflection");
                Assert(f.Ui.CurrentScreen == "focus-emotion" && f.Latest.Status != ParticipationState.RewardCommitted && f.Latest.Mood == "편안함" && f.Latest.Note == "합성 초안: 한 모금 쉬어갔어요", "Failed skip destroyed the editable reflection draft or advanced completion.");
                Assert(f.Service.Snapshot.Nutrient == nutrient && f.Service.Snapshot.RewardReceipts.Count == receipts, "Failed skip awarded nutrients.");
                SeparateRegions(f, true); f.Capture("mobile-skip-reflection-retry", capture); f.Click("Focus.SkipReflection");
                var saved = f.Store.Load(); var entry = saved.Sessions.Single(s => s.SessionId == id);
                Assert(f.Ui.CurrentScreen == "focus-complete" && entry.Status == ParticipationState.RewardCommitted && string.IsNullOrEmpty(entry.Mood) && string.IsNullOrEmpty(entry.Note), "Skip did not complete directly without optional reflection.");
                Assert(saved.Nutrient == nutrient + entry.RewardAmount && saved.RewardReceipts.Count == receipts + 1, "Completion did not commit one reward atomically.");
                Assert(f.Service.CompleteFocusSession(id, "", ""), f.Service.LastError);
                Assert(f.Service.Snapshot.Nutrient == saved.Nutrient && f.Service.Snapshot.RewardReceipts.Count == saved.RewardReceipts.Count, "Retrying the same completion duplicated rewards.");
                SeparateRegions(f); f.Capture("mobile-completion", capture);
            }
        }
        static void TimerControls(Report report, bool capture)
        {
            using (var f = new Fixture(report, false, false))
            {
                f.Resize(360, 640); StartAdditional(f, "M02"); f.Click("Focus.Action");
                var timer = f.Ui.GetComponentInChildren<FocusPracticeClock>(); Assert(timer != null && !timer.Running && timer.ElapsedSeconds == 0, "Focus timer started automatically.");
                VisibleWithoutScroll(f, Button(f, "Focus.TimerToggle")); f.Click("Focus.TimerToggle"); Assert(timer.Running, "Primary timer control did not start.");
                f.Click("Focus.TimerToggle"); Assert(!timer.Running && f.VisibleText.Contains("이어서 집중"), "Pause did not provide a resume state.");
                Assert(timer.ElapsedSeconds == 0, "Synchronous fixture unexpectedly elapsed real focus time.");
                f.Capture("mobile-timer-paused", capture); int before = f.Latest.InstructionStep; f.Click("Focus.Action");
                Assert(f.Latest.InstructionStep > before && f.Latest.FocusStepRecords[before].ElapsedSeconds == 0, "Manual finish at zero was blocked or invented elapsed time.");
            }
            using (var f = new Fixture(report, false, false))
            {
                StartAdditional(f, "M02"); f.Click("Focus.Action"); var timer = f.Ui.GetComponentInChildren<FocusPracticeClock>();
                timer.ElapsedSeconds = 37; Assert(timer.Flush(), f.Service.LastError); string id = f.Latest.SessionId; f.Click("닫기");
                f.Ui.ShowFocusRecommendationFor("M02"); f.Click("추가 미션 이어서 하기");
                Assert(f.Latest.SessionId == id && f.Ui.GetComponentInChildren<FocusPracticeClock>().ElapsedSeconds == 37, "Saved elapsed time did not survive resume.");
            }
        }
        static void StoryPlayer(Report report, bool capture)
        {
            using (var f = new Fixture(report, false, false))
            {
                f.Resize(390, 844); Pick(f, "8w");
                while (FocusMissionPolicy.NextOrder(f.Service.Snapshot, "8w") < 24)
                {
                    CompleteNext(f, "8w", "합성 오디오 검증 준비"); f.Clock.UtcNow = f.Clock.UtcNow.AddDays(2); Assert(f.Ui.RefreshCycle(), f.Service.LastError);
                }
                f.Ui.ShowFocusHome(); f.Click("기본 미션 시작하기"); f.Click("Focus.Action"); f.Click("Focus.Option.앱의 짧은 이야기"); f.Click("Focus.Action");
                var audio = f.Ui.GetComponentInChildren<FocusAudioPlayer>(); Assert(audio != null && audio.Source.clip != null && audio.Source.clip.samples > 0 && audio.Source.clip.length > 1, "Story audio is absent or empty.");
                Assert(audio.Duration != null && Regex.IsMatch(audio.Duration.text, @"\d+:\d{2}\s*/\s*\d+:\d{2}") && audio.Progress != null, "Story player lacks duration or playback progress.");
                Assert(Button(f, "Focus.AudioToggle").interactable && Button(f, "Focus.AudioReplay").interactable, "Story playback controls are unavailable.");
                f.Click("Focus.AudioToggle"); f.Click("Focus.AudioToggle"); f.Click("Focus.AudioReplay");
                f.Capture("mobile-story-player", capture); f.Click("Focus.AudioTranscript"); Assert(f.VisibleText.Contains(FocusMissionCatalog.StoryText), "Text fallback does not contain the story.");
                SeparateRegions(f); f.Capture("mobile-story-transcript", capture); f.Click("Focus.CloseTranscript");
                Assert(f.Ui.CurrentScreen == "focus-practice" && f.Latest.InstructionStep == 1, "Transcript return lost the current practice position.");
            }
        }
        static void ActionContrast()
        {
            foreach (var mission in FocusMissionCatalog.All)
            {
                Color action = FigmaMissionUi.ActionColor(FigmaMissionUi.Hex(mission.AccentHex));
                // Independent calculation avoids accepting a shared implementation error.
                double r = Linear(action.r), g = Linear(action.g), b = Linear(action.b);
                double contrast = 1.05 / (.2126 * r + .7152 * g + .0722 * b + .05);
                Assert(contrast >= 4.5, mission.Code + " action/white contrast below 4.5:1: " + contrast.ToString("F3"));
            }
        }
        static double Linear(double value) => value <= .04045 ? value / 12.92 : Math.Pow((value + .055) / 1.055, 2.4);
    }
}
