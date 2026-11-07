#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using CapstoneDesign.Runtime.LocalState;
using UnityEngine;
using UnityEngine.UI;

namespace CapstoneDesign.Runtime
{
    // Explicit audit harness, never self-starting. Both Editor and the isolated
    // audit APK consume this same traversal with a memory-only garden service.
    // Each yield is a presentation boundary: the player must wait at least .25s
    // and settle layout before capturing/continuing (runtime input debounce).
    public static class VisualAuditTraversal
    {
        public sealed class Clock : IClock
        {
            public DateTimeOffset Time = DateTimeOffset.Parse("2026-10-26T12:00:00+09:00");
            public DateTimeOffset UtcNow => Time;
            public TimeZoneInfo TimeZone => TimeZoneInfo.CreateCustomTimeZone("Visual-audit-KST", TimeSpan.FromHours(9), "Synthetic KST", "Synthetic KST");
        }
        public sealed class Store : IStateStore
        {
            string data;
            public int FailNextSaves;
            public GardenState Load() => data == null ? null : StateCodec.Decode(data);
            public void Save(GardenState state)
            {
                if (FailNextSaves > 0) { FailNextSaves--; throw new IOException("Synthetic visual-audit save failure."); }
                data = StateCodec.Encode(state);
            }
        }

        static readonly string[] Programs = { "2w", "4w", "8w" };
        static readonly string[] Emotions = {
            "기쁨", "만족감", "설렘", "기대감", "편안함", "안도감", "고마움", "뿌듯함",
            "궁금함", "반가움", "놀람", "무덤덤함", "슬픔", "외로움", "서운함", "아쉬움",
            "불안함", "두려움", "답답함", "막막함", "화남", "짜증", "부끄러움", "미안함"
        };

        public static IEnumerable<string> Cases(WeekOneQuestDemo ui, GardenStateService service, Clock clock)
        {
            Require(ui.Service == service && ui.HasOpenState, "Audit requires an initialized injected service.");
            Require(AndroidPlatformBridge.InputMode == PlatformInputMode.Mock, "Visual audit requires mocked platform input.");
            ui.ShowFocusProgramSelection();
            foreach (string frame in ScrollPages(ui, "programs-first")) yield return frame;
            foreach (string program in Programs)
            {
                Click(ui, "Focus.SelectProgram." + program); yield return "program-confirm-" + program;
                Click(ui, "Focus.ConfirmProgram." + program);
                foreach (string frame in ScrollPages(ui, "home-" + program)) yield return frame;
                ui.ShowFocusProgramSelection(); yield return null;
            }
            ui.ShowFocusMissionLibrary();
            foreach (string filter in new[] { "전체", "감각", "몸 돌봄", "마음 기록" })
            {
                Click(ui, "Focus.LibraryFilter." + filter);
                int index = Array.IndexOf(new[] { "전체", "감각", "몸 돌봄", "마음 기록" }, filter);
                foreach (string frame in ScrollPages(ui, "library-filter-" + index)) yield return frame;
            }

            bool emotionsCovered = false;
            foreach (var mission in FocusMissionCatalog.Additional)
            {
                ui.ShowFocusRecommendationFor(mission.Id);
                foreach (string frame in ScrollPages(ui, mission.Code + "-recommendation")) yield return frame;
                Click(ui, "추가 미션 시작하기"); yield return mission.Code + "-overview";
                foreach (string frame in Complete(ui, service, mission.Code, null, !emotionsCovered)) yield return frame;
                emotionsCovered = true;
            }
            Require(service.SelectMbctProgram("8w"), service.LastError);
            for (int order = 1; order <= FocusMissionCatalog.Basic.Length; order++)
            {
                int days = 0;
                while (FocusMissionPolicy.Availability(service.Snapshot, "8w") != null)
                {
                    Require(++days <= 8, "Synthetic course failed to become available.");
                    clock.Time = clock.Time.AddDays(1);
                    Require(ui.RefreshCycle(), service.LastError);
                }
                var mission = FocusMissionPolicy.MissionAt(order, "8w");
                ui.ShowHome(); yield return null;
                Click(ui, "기본 미션 시작하기"); yield return mission.Code + "-overview";
                foreach (string frame in Complete(ui, service, mission.Code, null, false)) yield return frame;
            }
            Require(FocusMissionCatalog.All.All(m => service.Snapshot.Sessions.Any(s =>
                s.MissionId == m.Id && s.Status == ParticipationState.RewardCommitted)), "Not every current mission completed.");

            // Each route choice is photographed in the primary run. Replay
            // alternate routes too, because later conditional screens differ.
            foreach (var mission in FocusMissionCatalog.All)
            {
                var route = mission.Steps.FirstOrDefault(step => step.Key == "route" && step.Options.Length > 1);
                if (route == null) continue;
                foreach (string choice in route.Options.Skip(1))
                {
                    int order = mission.Role == FocusMissionCatalog.BasicRole
                        ? Enumerable.Range(1, 28).First(i => FocusMissionPolicy.MissionAt(i, "8w").Id == mission.Id) : 0;
                    ui.StartFocusReview(mission.Id, order, mission.Role == FocusMissionCatalog.BasicRole ? "8w" : null);
                    string prefix = mission.Code + "-alternate-" + Array.IndexOf(route.Options, choice);
                    yield return prefix + "-overview";
                    foreach (string frame in Complete(ui, service, prefix, choice, false)) yield return frame;
                }
            }

            ui.ShowFocusCourse();
            foreach (string frame in ScrollPages(ui, "courses-populated")) yield return frame;
            foreach (string program in Programs)
            {
                ui.ShowFocusProgram(program);
                foreach (string frame in ScrollPages(ui, "course-" + program)) yield return frame;
                Click(ui, "Focus.CurriculumToggle");
                foreach (string frame in ScrollPages(ui, "curriculum-" + program)) yield return frame;
                for (int order = 1; order <= FocusMissionPolicy.TotalPracticesFor(program); order++)
                {
                    Click(ui, "Focus.Occurrence." + program + "." + order);
                    foreach (string frame in ScrollPages(ui, "occurrence-" + program + "-" + order.ToString("00"))) yield return frame;
                    Click(ui, "코스 기록으로 돌아가기"); yield return null;
                }
                ui.ShowFocusHistory(null, 0, program);
                foreach (string frame in ScrollPages(ui, "history-" + program)) yield return frame;
            }
            ui.ShowFocusHistory(null, 0, "additional");
            foreach (string frame in ScrollPages(ui, "history-additional")) yield return frame;
            ui.ShowHome(); yield return "missions-final";
        }

        static ActivitySession Latest(GardenStateService service) => service.Snapshot.Sessions.Last(FocusMissionPolicy.IsFocus);

        // Focused follow-up for selected controls that were below the viewport
        // after a page rebuild. No production save is loaded or changed, and no
        // mission has to be completed merely to revisit its choice widgets.
        public static IEnumerable<string> ChoiceStates(WeekOneQuestDemo ui, GardenStateService service, Clock clock)
        {
            const BindingFlags hidden = BindingFlags.Instance | BindingFlags.NonPublic;
            Require(ui.Service == service && ui.HasOpenState, "Choice capture requires the injected audit service.");
            Require(AndroidPlatformBridge.InputMode == PlatformInputMode.Mock, "Choice capture requires mock input.");
            Require(typeof(GardenStateService).GetField("store", hidden)?.GetValue(service) is Store,
                "Choice capture refuses a persistent state store.");
            var serviceField = typeof(WeekOneQuestDemo).GetField("<Service>k__BackingField", hidden);
            var navigation = ui.transform.root.GetComponent<MockupNavigation>();
            Require(serviceField != null && navigation != null, "Choice fixture injection context is missing.");
            bool wasIsland = navigation.home != null ? navigation.home.IsGardenVisible
                : navigation.islandPanel != null && navigation.islandPanel.activeSelf;
            bool wasSettings = navigation.settingsPanel != null && navigation.settingsPanel.activeSelf;
            var fixtureClock = new Clock { Time = clock.Time };
            GardenState baseline;
            using (var empty = new GardenStateService(new Store(), fixtureClock, new DemoBalanceConfig { VisitorChancePercent = 0 }))
            {
                Require(empty.Open() && empty.RefreshCycle(Array.Empty<string>()) && empty.SelectMbctProgram("8w"), empty.LastError);
                baseline = empty.Snapshot;
            }
            ui.ShowHome();
            try
            {
                navigation.ShowActivities();
                foreach (var target in ChoiceTargets())
                for (int index = 0; index < target.Mission.Steps.Length; index++)
                {
                    var step = target.Mission.Steps[index];
                    bool timerFixture = target.Mission.Code == "M02" && step.RecommendedSeconds > 0;
                    bool inputFixture = step.Kind == "input" || step.Kind == "plan" || !string.IsNullOrEmpty(step.InputHint);
                    if (step.Options.Length == 0 && !inputFixture && !timerFixture) continue;
                    int route = Array.FindIndex(target.Mission.Steps, candidate => candidate.Key == "route");
                    string[] contexts = route >= 0 && route < index
                        ? target.Mission.Steps[route].Options.Where(option =>
                            FocusMissionAdjustmentPolicy.DifferentPurposeAlternative(target.Mission, "route", option) == null).ToArray()
                        : new string[] { null };
                    foreach (string context in contexts)
                    {
                        string prefix = target.Prefix;
                        if (context != null)
                        {
                            int alternative = Array.IndexOf(target.Mission.Steps[route].Options, context);
                            if (alternative > 0) prefix += "-alternate-" + alternative;
                        }
                        var seed = ChoiceSeed(baseline, fixtureClock, target, index, context);
                        var session = seed.Sessions.Last();
                        if (!FocusMissionCatalog.IsApplicable(target.Mission, session.FocusStepRecords, index)) continue;
                        var store = new Store();
                        store.Save(seed); // StateCodec validates and serializes every independent fixture.
                        using (var fixture = new GardenStateService(store, fixtureClock, new DemoBalanceConfig { VisitorChancePercent = 0 }))
                        {
                            Require(fixture.Open(), fixture.LastError);
                            try
                            {
                                serviceField.SetValue(ui, fixture);
                                ui.SelectFocusSession(session.SessionId);
                                Require(ui.CurrentScreen == "focus-practice", "Choice fixture did not open its practice step.");
                                yield return null;
                                string key = prefix + "-step-" + (index + 1).ToString("00") + "-" + step.Key;
                                for (int option = 0; option < step.Options.Length; option++)
                                {
                                    Click(ui, step.Options[option]);
                                    RevealButton(ui, step.Options[option]);
                                    Require(Latest(fixture).FocusStepRecords[index].Value == step.Options[option],
                                        "Choice callback did not persist " + key + " option " + option);
                                    yield return key + "-choice-" + option;
                                }
                                if (step.Kind == "plan")
                                {
                                    for (int option = 0; option < FocusMissionAdjustmentPolicy.PlanActivityOptions.Length; option++)
                                    {
                                        string control = "Focus.PlanActivity." + option;
                                        Click(ui, control); RevealButton(ui, control);
                                        Require(Latest(fixture).FocusStepRecords[index].PlanActivity == FocusMissionAdjustmentPolicy.PlanActivityOptions[option],
                                            "Plan activity callback did not persist its selection.");
                                        yield return key + "-activity-" + option;
                                    }
                                    for (int option = 0; option < FocusMissionAdjustmentPolicy.PlanTimePeriodOptions.Length; option++)
                                    {
                                        string control = "Focus.PlanPeriod." + option;
                                        Click(ui, control); RevealButton(ui, control);
                                        Require(Latest(fixture).FocusStepRecords[index].PlanTimePeriod == FocusMissionAdjustmentPolicy.PlanTimePeriodOptions[option],
                                            "Plan period callback did not persist its selection.");
                                        yield return key + "-period-" + option;
                                    }
                                }
                                if (inputFixture)
                                {
                                    const string syntheticInput = "합성 화면 검토용 기록입니다. 창가에서 잠시 쉬었어요.";
                                    var input = ui.GetComponentsInChildren<InputField>(false).FirstOrDefault(field => field.isActiveAndEnabled);
                                    Require(input != null, "Choice input fixture has no active field: " + key);
                                    input.text = syntheticInput; input.onEndEdit.Invoke(input.text);
                                    input = ui.GetComponentsInChildren<InputField>(false).FirstOrDefault(field => field.isActiveAndEnabled);
                                    Require(input != null, "Input callback removed its active field: " + key);
                                    RevealTransform(input.transform);
                                    var saved = Latest(fixture).FocusStepRecords[index];
                                    Require((step.Kind == "plan" ? saved.PlanNote : saved.Value) == syntheticInput,
                                        "Input callback did not persist " + key);
                                    yield return key + "-input";
                                    if (step.Options.Length > 0)
                                    {
                                        foreach (string option in step.Options)
                                        {
                                            var preset = FindButton(ui, "Focus.Option." + option);
                                            var background = preset == null ? null : preset.GetComponent<Image>();
                                            Require(background != null && Mathf.Abs(background.color.r - 1) < .01f &&
                                                Mathf.Abs(background.color.g - 1) < .01f && Mathf.Abs(background.color.b - 1) < .01f &&
                                                Mathf.Abs(background.color.a - .82f) < .01f,
                                                "Custom input left a preset visually selected: " + key + " / " + option);
                                        }
                                        RevealButton(ui, "Focus.Option." + step.Options[0]);
                                        yield return key + "-input-options";
                                    }
                                }
                                if (timerFixture)
                                {
                                    Click(ui, "Focus.TimerToggle"); yield return null;
                                    Click(ui, "Focus.TooLong"); yield return key + "-shorten-dialog";
                                    Click(ui, "Focus.AdjustmentCancel"); yield return null;
                                }
                                else
                                {
                                    Click(ui, "Focus.EndParticipation"); yield return key + "-end-dialog";
                                    Click(ui, "Focus.AdjustmentCancel"); yield return null;
                                }
                            }
                            finally
                            {
                                try { ui.ShowHome(); }
                                finally { serviceField.SetValue(ui, service); ui.ShowHome(); }
                            }
                        }
                    }
                }
            }
            finally
            {
                serviceField.SetValue(ui, service);
                ui.ShowHome();
                if (wasIsland) navigation.ShowIsland();
                else if (wasSettings) navigation.ShowSettings();
                else navigation.ShowActivities();
            }
        }

        sealed class ChoiceTarget
        {
            public FocusMissionDefinition Mission;
            public string Prefix, Source;
        }
        static IEnumerable<ChoiceTarget> ChoiceTargets()
        {
            foreach (var mission in FocusMissionCatalog.All)
            {
                yield return new ChoiceTarget { Mission = mission, Prefix = mission.Code };
                var route = mission.Steps.FirstOrDefault(step => step.Key == "route");
                if (route == null) continue;
                for (int option = 0; option < route.Options.Length; option++)
                {
                    string alternative = FocusMissionAdjustmentPolicy.DifferentPurposeAlternative(mission, "route", route.Options[option]);
                    if (alternative != null)
                        yield return new ChoiceTarget { Mission = FocusMissionCatalog.Find(alternative),
                            Prefix = mission.Code + "-alternate-" + option, Source = mission.Id };
                }
            }
        }
        static GardenState ChoiceSeed(GardenState baseline, Clock clock, ChoiceTarget target, int index, string context)
        {
            var seed = StateCodec.Clone(baseline);
            var mission = target.Mission;
            bool basic = mission.Role == FocusMissionCatalog.BasicRole && target.Source == null;
            var started = clock.Time.AddMinutes(-1);
            var session = new ActivitySession {
                SessionId = "synthetic-choice-" + target.Prefix + "-" + index + "-" + Guid.NewGuid().ToString("N"),
                MissionId = mission.Id, InstanceId = seed.LastCycleId, SelectedUtc = started.AddSeconds(-1).ToString("O"),
                StartedUtc = started.ToString("O"), Status = ParticipationState.InProgress,
                FocusVersion = FocusMissionCatalog.Version, FocusRole = basic ? FocusMissionCatalog.BasicRole : FocusMissionCatalog.AdditionalRole,
                FocusProgramId = basic ? "8w" : "", FocusOrder = basic ? Array.IndexOf(FocusMissionPolicy.CourseCodes("8w"), mission.Code) + 1 : 0,
                MissionTitleSnapshot = mission.Title, MissionDescriptionSnapshot = mission.Overview, InstructionStep = index,
                FocusSupport = target.Source != null, FocusSourceMissionId = target.Source,
                // A deleted source history is supported by the production schema.
                FocusSourceSessionId = target.Source == null ? null : "synthetic-choice-source-removed",
                FocusStepRecords = mission.Steps.Select((step, number) => new FocusStepRecord {
                    Step = number, Key = step.Key, Title = step.Title, Prompt = step.Body,
                    Value = number < index ? (step.Key == "route" && context != null ? context : step.Options.FirstOrDefault()) ??
                        (step.RequiredAnswer ? "합성 선택 화면 검토용 입력" : "") : ""
                }).ToList()
            };
            for (int previous = 0; previous < index; previous++)
            {
                var step = mission.Steps[previous]; var record = session.FocusStepRecords[previous];
                bool applicable = FocusMissionCatalog.IsApplicable(mission, session.FocusStepRecords, previous);
                if (!applicable) { record.Value = ""; record.Skipped = true; }
                else if (step.Kind == "draw") record.Value = FocusDrawingData.Encode(new[] { new List<FocusDrawingPoint> {
                    new FocusDrawingPoint(10, 10), new FocusDrawingPoint(30, 40) } });
                record.ConfirmedUtc = started.AddTicks(previous + 1).ToString("O");
                record.UpdatedUtc = record.ConfirmedUtc;
                record.EndReason = applicable ? "completed" : "not_applicable";
                if (record.Value.Length > 0) session.Answers.Add(new PracticeAnswer {
                    Key = record.Key, Value = record.Value, UpdatedUtc = record.UpdatedUtc });
            }
            if (mission.Code == "M15")
            {
                var day = new DateTimeOffset(clock.Time.Year, clock.Time.Month, clock.Time.Day, 0, 0, 0, clock.Time.Offset);
                session.WalkingGoalSteps = 5000; session.WalkingQueryStartUtc = day.ToString("O");
                session.WalkingQueryEndUtc = day.AddDays(1).ToString("O"); session.WalkingTargetBasis = "DEFAULT";
            }
            seed.Sessions.Add(session);
            StateCodec.Validate(seed);
            return seed;
        }

        sealed class Variant
        {
            public bool Shorten, SkipOptional, SkipReflection, Keyboard, SaveFailure, IconTap;
            public PhotoProvider Photo;
        }

        // Separate from the catalog pass so the same remaining branches can be
        // repeated after a focused layout fix without repeating 45 missions.
        public static IEnumerable<string> AdditionalStates(WeekOneQuestDemo ui, GardenStateService service, Clock clock)
        {
            foreach (var scenario in new[] {
                new { Code = "M02", Name = "shorten-confirmed", Variant = new Variant { Shorten = true } },
                new { Code = "M07", Name = "skip-optional", Variant = new Variant { SkipOptional = true, SkipReflection = true } },
                new { Code = "M13", Name = "keyboard-save-retry", Variant = new Variant { Keyboard = true, SaveFailure = true } },
                new { Code = "M03", Name = "icon-action", Variant = new Variant { IconTap = true } }
            })
            {
                ui.StartFocusReview(FocusMissionCatalog.Find(scenario.Code).Id, 0); yield return scenario.Name + "-overview";
                foreach (string frame in Complete(ui, service, scenario.Name, null, false, scenario.Variant)) yield return frame;
            }
            ui.StartFocusReview("free-mission:M02", 0); yield return "end-participation-overview";
            Click(ui, "Focus.Action"); yield return null;
            Click(ui, "Focus.EndParticipation"); yield return "end-participation-confirm";
            Click(ui, "Focus.AdjustmentConfirm"); yield return "end-participation-result";
            Require(Latest(service).Status == ParticipationState.Ended, "Confirmed skip did not end participation.");

            var photoField = typeof(WeekOneQuestDemo).GetField("photoService", BindingFlags.Instance | BindingFlags.NonPublic);
            Require(photoField != null, "Photo fixture injection field missing.");
            foreach (string code in new[] { "M03", "M14", "P04" })
            foreach (string status in code == "M03" ? new[] { "matched", "not_found", "unavailable", "cancelled", "capturing", "processing" } : new[] { "matched" })
            {
                string name = "photo-" + code + "-" + status;
                var provider = new PhotoProvider(clock, status);
                photoField.SetValue(ui, new PhotoVerificationService(provider));
                int order = code[0] == 'P' ? Enumerable.Range(1, 28).First(i => FocusMissionPolicy.MissionAt(i, "8w").Code == code) : 0;
                ui.StartFocusReview(FocusMissionCatalog.Find(code).Id, order, order > 0 ? "8w" : null); yield return name + "-overview";
                foreach (string frame in Complete(ui, service, name, null, false, new Variant { Photo = provider })) yield return frame;
            }
            photoField.SetValue(ui, new PhotoVerificationService(new PhotoProvider(clock, "unavailable")));
        }

        sealed class PhotoProvider : IMissionPhotoProvider
        {
            readonly Clock clock;
            public readonly string Status;
            public PhotoVerificationResult Result;
            public string DirectoryPath { get; }
            public PhotoProvider(Clock clock, string status)
            {
                this.clock = clock; Status = status;
                DirectoryPath = Path.Combine(Environment.GetEnvironmentVariable("CAPSTONE_ARTIFACTS") ?? Application.temporaryCachePath, "synthetic-audit-photos");
                Directory.CreateDirectory(DirectoryPath);
            }
            public PhotoVerificationResult Start(string id, string target)
            {
                Result = PhotoVerificationService.Unavailable(id, target); Result.status = Status;
                Result.capturedUtc = clock.Time.ToString("O"); Result.label = Status == "matched" ? target == "book" ? "book" : "cup" : ""; Result.score = Status == "matched" ? .88 : 0;
                if (Status != "cancelled" && Status != "capturing")
                {
                    var image = new Texture2D(320, 240, TextureFormat.RGB24, false);
                    var pixels = new Color32[320 * 240];
                    for (int y = 0; y < 240; y++) for (int x = 0; x < 320; x++)
                        pixels[y * 320 + x] = x > 90 && x < 230 && y > 35 && y < 205 ? new Color32(80, 140, 120, 255) : new Color32(235, 238, 231, 255);
                    image.SetPixels32(pixels); image.Apply(); File.WriteAllBytes(Path.Combine(DirectoryPath, id + ".jpg"), image.EncodeToJPG(80));
                    if (Application.isPlaying) UnityEngine.Object.Destroy(image); else UnityEngine.Object.DestroyImmediate(image);
                }
                return Result;
            }
            public PhotoVerificationResult Poll(string id) => Result;
            public void Delete(string id) { if (CameraMissionPolicy.ValidId(id)) { string path = Path.Combine(DirectoryPath, id + ".jpg"); if (File.Exists(path)) File.Delete(path); } }
            public void Prune(string[] keep) { }
        }

        static IEnumerable<string> Complete(WeekOneQuestDemo ui, GardenStateService service, string prefix, string alternate, bool allEmotions, Variant variant = null)
        {
            Click(ui, variant?.IconTap == true && Has(ui, "Focus.IconAction") ? "Focus.IconAction" : "Focus.Action"); yield return null;
            int guard = 0;
            while (ui.CurrentScreen == "focus-practice")
            {
                Require(++guard < 32, "Mission traversal did not converge: " + prefix);
                var session = Latest(service);
                var mission = FocusMissionCatalog.FindForSession(session);
                if (session.Status == ParticipationState.Selected)
                {
                    yield return prefix + "-support-overview";
                    Click(ui, "Focus.Action"); yield return null;
                    continue;
                }
                int index = session.InstructionStep;
                Require(index < mission.Steps.Length, "Practice stayed open after its final step.");
                var step = mission.Steps[index];
                string key = prefix + "-step-" + (index + 1).ToString("00") + "-" + step.Key;
                foreach (string frame in ScrollPages(ui, key + "-initial")) yield return frame;
                for (int option = 0; option < step.Options.Length; option++)
                {
                    Click(ui, step.Options[option]);
                    // The callback rebuilds this page at its scroll origin.
                    // Capture the selected option itself on shorter displays.
                    RevealButton(ui, step.Options[option]);
                    yield return key + "-choice-" + option;
                }
                if (step.Options.Length > 0)
                {
                    string desired = step.Key == "route" && alternate != null && step.Options.Contains(alternate) ? alternate : step.Options[0];
                    if (Latest(service).FocusStepRecords[index].Value != desired)
                    { Click(ui, desired); yield return null; }
                }
                if (step.Kind == "plan")
                {
                    for (int option = 0; option < FocusMissionAdjustmentPolicy.PlanActivityOptions.Length; option++)
                    { Click(ui, "Focus.PlanActivity." + option); RevealButton(ui, "Focus.PlanActivity." + option); yield return key + "-activity-" + option; }
                    for (int option = 0; option < FocusMissionAdjustmentPolicy.PlanTimePeriodOptions.Length; option++)
                    { Click(ui, "Focus.PlanPeriod." + option); RevealButton(ui, "Focus.PlanPeriod." + option); yield return key + "-period-" + option; }
                }
                var input = ui.GetComponentsInChildren<InputField>(false).FirstOrDefault(i => i.isActiveAndEnabled);
                if (input != null)
                {
                    input.text = "합성 화면 검토용 기록입니다. 창가에서 잠시 쉬었어요.";
                    input.onEndEdit.Invoke(input.text);
                    input = ui.GetComponentsInChildren<InputField>(false).FirstOrDefault(field => field.isActiveAndEnabled);
                    Require(input != null, "Input callback removed its active field: " + key);
                    RevealTransform(input.transform);
                    yield return key + "-input";
                    if (variant?.Keyboard == true)
                    {
                        input.ActivateInputField(); yield return key + "-keyboard-opening";
                        yield return key + "-keyboard-open";
                        input.DeactivateInputField(); yield return null;
                    }
                }
                if (step.Kind == "draw")
                {
                    Draw(ui); yield return key + "-drawing";
                    Click(ui, "Focus.DrawingClear"); yield return key + "-cleared";
                    Click(ui, "Focus.DrawingUndo"); yield return key + "-restored";
                }
                if (Has(ui, "Focus.TimerToggle"))
                {
                    Click(ui, "Focus.TimerToggle"); yield return key + "-timer-running";
                    Click(ui, "Focus.TimerToggle"); yield return key + "-timer-paused";
                    if (Has(ui, "Focus.TooLong"))
                    {
                        Click(ui, "Focus.TooLong"); yield return key + "-shorten-dialog";
                        Click(ui, "Focus.AdjustmentCancel"); yield return null;
                        if (variant?.Shorten == true)
                        {
                            var timer = ui.GetComponentInChildren<FocusPracticeClock>(); timer.ElapsedSeconds = 8;
                            Require(timer.Flush(), "Synthetic timer elapsed could not be saved.");
                            Click(ui, "Focus.TooLong"); yield return key + "-shorten-confirm";
                            Click(ui, "Focus.AdjustmentConfirm"); yield return key + "-shortened";
                            variant.Shorten = false; continue;
                        }
                    }
                }
                if (Has(ui, "Focus.AudioToggle"))
                {
                    Click(ui, "Focus.AudioToggle"); yield return key + "-audio-playing";
                    Click(ui, "Focus.AudioToggle"); yield return key + "-audio-paused";
                    Click(ui, "Focus.AudioReplay"); yield return key + "-audio-replay";
                    Click(ui, "Focus.AudioTranscript");
                    foreach (string frame in ScrollPages(ui, key + "-transcript")) yield return frame;
                    Click(ui, "Focus.CloseTranscript"); yield return null;
                }
                if (Has(ui, "Focus.RefreshSteps")) { Click(ui, "Focus.RefreshSteps"); yield return key + "-walking-refreshed"; }
                if (Has(ui, "Focus.ContinueLater"))
                {
                    Click(ui, "Focus.ContinueLater"); yield return key + "-resume-home";
                    ui.SelectFocusSession(session.SessionId); yield return key + "-resumed";
                }
                if (index == 0)
                {
                    Click(ui, "Focus.EndParticipation"); yield return key + "-end-dialog";
                    Click(ui, "Focus.AdjustmentCancel"); yield return null;
                }
                Click(ui, variant?.SkipOptional == true && Has(ui, "Focus.Skip") ? "Focus.Skip" :
                    variant?.IconTap == true && Has(ui, "Focus.IconAction") ? "Focus.IconAction" : "Focus.Action"); yield return null;
                Require(Latest(service).SessionId != session.SessionId || Latest(service).InstructionStep > index,
                    "Action did not advance " + key);
            }
            if (Has(ui, "Photo.Skip"))
            {
                foreach (string frame in ScrollPages(ui, prefix + "-photo-choice")) yield return frame;
                if (variant?.Photo != null)
                {
                    Click(ui, "Photo.Capture");
                    foreach (string frame in ScrollPages(ui, prefix + "-photo-result")) yield return frame;
                    if (Has(ui, "Photo.Register"))
                    {
                        if (variant.Photo.Status == "matched") { Click(ui, "Photo.Retake"); yield return prefix + "-photo-retaken"; }
                        Click(ui, "Photo.Register"); yield return null;
                    }
                    else { Click(ui, "Photo.Skip"); yield return null; }
                }
                else { Click(ui, "Photo.Skip"); yield return null; }
            }
            Require(ui.CurrentScreen == "focus-emotion", "Missing reflection page for " + prefix);
            yield return prefix + "-emotion";
            if (variant?.SkipReflection == true)
            {
                Click(ui, "Focus.SkipReflection"); yield return prefix + "-complete-without-reflection";
                Require(Latest(service).Status == ParticipationState.RewardCommitted, "Skip-reflection did not complete.");
                ui.ShowHome(); yield return null; yield break;
            }
            if (allEmotions)
            {
                Click(ui, "Focus.MoreEmotions"); yield return "reflection-all-emotions";
                foreach (string frame in ScrollPages(ui, "reflection-all-emotions-visible")) yield return frame;
                for (int index = 0; index < Emotions.Length; index++)
                {
                    string control = "Focus.Emotion." + Emotions[index];
                    Click(ui, control);
                    // The selection callback rebuilds the page at its scroll origin.
                    // Reveal the new selected control, including lower emotion rows.
                    RevealButton(ui, control);
                    yield return "reflection-emotion-" + index.ToString("00");
                }
                Click(ui, "Focus.MoreEmotions"); yield return null;
            }
            Click(ui, "Focus.ReflectionNext"); yield return prefix + "-note-empty";
            var note = ui.GetComponentsInChildren<InputField>(false).First();
            note.text = "합성 화면 검토용 기록입니다."; note.onEndEdit.Invoke(note.text); yield return prefix + "-note-filled";
            if (variant?.Keyboard == true)
            {
                note.ActivateInputField(); yield return prefix + "-note-keyboard-opening";
                yield return prefix + "-note-keyboard-open";
                note.DeactivateInputField(); yield return null;
            }
            if (variant?.SaveFailure == true)
            {
                var store = typeof(GardenStateService).GetField("store", BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(service) as Store;
                Require(store != null, "Failure preview requires the injected audit memory store.");
                store.FailNextSaves = 1;
                Click(ui, "Focus.Complete"); yield return prefix + "-save-error";
                Require(ui.CurrentScreen == "focus-save-error", "Expected failed-save presentation.");
                Click(ui, "Focus.ReviewFailedSave"); yield return prefix + "-retry-input";
            }
            Click(ui, "Focus.Complete"); yield return prefix + "-complete";
            Require(Latest(service).Status == ParticipationState.RewardCommitted, "Mission did not complete: " + prefix);
            Click(ui, "Focus.ViewRecord");
            foreach (string frame in ScrollPages(ui, prefix + "-record")) yield return frame;
            if (Has(ui, "Focus.RecordStepsToggle"))
            {
                Click(ui, "Focus.RecordStepsToggle");
                foreach (string frame in ScrollPages(ui, prefix + "-record-expanded")) yield return frame;
            }
            if (variant?.Photo != null && Has(ui, "Photo.Delete"))
            { Click(ui, "Photo.Delete"); yield return prefix + "-photo-deleted"; }
            ui.ShowHome(); yield return null;
        }

        static void Draw(WeekOneQuestDemo ui)
        {
            var surface = ui.GetComponentsInChildren<FocusDrawingSurface>(false).First(s => !s.ReadOnly);
            string value = FocusDrawingData.Encode(new[] { new List<FocusDrawingPoint> {
                new FocusDrawingPoint(9, 12), new FocusDrawingPoint(20, 36), new FocusDrawingPoint(34, 18), new FocusDrawingPoint(50, 44) } });
            surface.Load(value); surface.Changed?.Invoke(value); surface.StrokeEnded?.Invoke();
        }

        public static bool Has(WeekOneQuestDemo ui, string name) => FindButton(ui, name) != null;
        static Button FindButton(WeekOneQuestDemo ui, string name) => ui.GetComponentsInChildren<Button>(false).FirstOrDefault(button =>
            button.isActiveAndEnabled && button.interactable && (button.name == name || button.GetComponentsInChildren<Text>(false).Any(t => t.text.Trim() == name)));
        public static void Click(WeekOneQuestDemo ui, string name)
        {
            var button = RevealButton(ui, name);
            button.onClick.Invoke();
        }
        static Button RevealButton(WeekOneQuestDemo ui, string name)
        {
            var button = FindButton(ui, name);
            Require(button != null, "Missing enabled control " + name + " on " + ui.CurrentScreen);
            RevealTransform(button.transform);
            return button;
        }
        static void RevealTransform(Transform target)
        {
            Settle();
            foreach (var scroll in target.GetComponentsInParent<ScrollRect>(false))
            {
                if (scroll.content == null || scroll.viewport == null) continue;
                var rect = (RectTransform)target;
                Vector3 center = scroll.content.InverseTransformPoint(rect.TransformPoint(rect.rect.center));
                float y = Mathf.Clamp(-center.y - scroll.viewport.rect.height / 2, 0, Mathf.Max(0, scroll.content.rect.height - scroll.viewport.rect.height));
                scroll.content.anchoredPosition = new Vector2(scroll.content.anchoredPosition.x, y); scroll.velocity = Vector2.zero;
            }
            Settle();
        }
        public static IEnumerable<string> ScrollPages(WeekOneQuestDemo ui, string prefix)
        {
            Settle();
            var scrolls = ui.GetComponentsInChildren<ScrollRect>(false).Where(s => s.isActiveAndEnabled && s.content != null && s.viewport != null && s.vertical).ToArray();
            foreach (var scroll in scrolls) { scroll.verticalNormalizedPosition = 1; scroll.velocity = Vector2.zero; }
            yield return prefix + "-top";
            for (int index = 0; index < scrolls.Length; index++)
            {
                var scroll = scrolls[index]; Settle();
                float overflow = scroll.content.rect.height - scroll.viewport.rect.height;
                if (overflow <= 1) continue;
                int pages = Math.Max(1, Mathf.CeilToInt(overflow / Math.Max(1, scroll.viewport.rect.height * .8f)));
                for (int page = 1; page <= pages; page++)
                { scroll.verticalNormalizedPosition = 1 - page / (float)pages; scroll.velocity = Vector2.zero; yield return prefix + "-scroll" + index + "-" + page.ToString("00"); }
            }
        }
        public static void Settle()
        {
            Canvas.ForceUpdateCanvases();
            foreach (var fit in UnityEngine.Object.FindObjectsByType<FigmaMissionCanvasFit>(FindObjectsSortMode.None)) fit.Fit();
            foreach (var layout in UnityEngine.Object.FindObjectsByType<FocusMobilePageLayout>(FindObjectsSortMode.None)) layout.Refresh();
            Canvas.ForceUpdateCanvases();
        }
        public static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
    }
}
#endif
