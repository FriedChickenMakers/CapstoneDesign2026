#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using CapstoneDesign.Prototype;
using CapstoneDesign.Runtime.LocalState;
using UnityEngine;
using UnityEngine.UI;

namespace CapstoneDesign.Runtime
{
    // Opt-in visual audit only. The host must inject a memory-only service and
    // mock platform before enumerating. No actual photos, sensors or preferences
    // are used. The same iterator drives Editor and the separate audit APK.
    public static class VisualAuditShellCases
    {
        const BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;
        const string Sample = "합성 화면 검토용 기록입니다. 정원에서 잠시 쉬며 천천히 숨을 골랐어요.";

        // Seed only a newly-created synthetic state. Retained growth receipts
        // model deleted historical activities without filling the course history.
        // Thus the mission traversal still starts with no completed missions.
        public static void Seed(GardenState state)
        {
            Require(state != null && state.Sessions.Count == 0 && state.PlantCollection == null
                && state.PurchaseReceipts.Count == 0, "Seed requires a new synthetic garden.");
            state.InstallationId = "synthetic-visual-audit";
            state.MigrationCompleted = true;
            state.MbctProgramSelectionVersion = 1;
            state.Nutrient = 50;
            state.LegacyGrowth = .5f;
            var time = DateTimeOffset.Parse("2026-08-01T12:00:00+09:00");
            for (int i = 0; i < 5; i++)
            {
                string id = "synthetic-prior-growth-" + i;
                state.PurchaseReceipts.Add(id);
                state.PurchaseCommands.Add(new PurchaseCommand { RequestId = id, Command = "growth|" + GardenPlantVisuals.GrowthPlantId });
                state.GrowthHistory.Add(new GardenGrowthRecord { RequestId = id, PlantId = GardenPlantVisuals.GrowthPlantId,
                    CreatedUtc = time.AddDays(-5 + i).ToString("O"), LinkedSessionId = "",
                    BeforeGrowth = i * .1f, AfterGrowth = (i + 1) * .1f });
            }
            GardenPlantVisuals.Sync(state);
            WeeklyPlants.Migrate(state, time.ToString("O"), true);
            for (int i = 0; i < 64; i++)
            {
                var day = time.AddDays(i / 3);
                string cycle = day.ToString("yyyy-MM-dd");
                if (state.LastCycleId != cycle)
                {
                    state.DailyDecisions.Add(new DailyDecision { CycleId = cycle, AppliedUtc = day.ToString("O"),
                        TimeZoneId = "Synthetic-KST", VisitorId = "" });
                    state.LastCycleId = cycle;
                }
                WeeklyPlants.Complete(state, new ActivitySession { SessionId = "synthetic-retained-growth-" + i,
                    MissionId = "free-mission:M17", Status = ParticipationState.RewardCommitted,
                    InstanceId = cycle }, day.ToString("O"), 540);
            }
            StateCodec.Validate(state);
        }

        public static IEnumerable<string> Cases(MockupNavigation nav, WeekOneQuestDemo ui,
            GardenStateService service, VisualAuditTraversal.Clock clock)
        {
            Require(ui.Service == service && ui.HasOpenState, "Shell audit needs the injected garden service.");
            Require(AndroidPlatformBridge.InputMode == PlatformInputMode.Mock, "Shell audit requires mock platform input.");
            double? oldHour = GardenVisualClock.OverrideHour;
            try
            {
                foreach (string frame in Garden(nav, ui)) yield return frame;
                foreach (string frame in Shop(nav, ui, service)) yield return frame;
                foreach (string frame in Experiences(nav, ui, service, clock)) yield return frame;
                foreach (string frame in Plants(nav, ui, service)) yield return frame;
                foreach (string frame in Records(ui, service, clock)) yield return frame;
                foreach (string frame in Plans(ui, service)) yield return frame;
                foreach (string frame in Settings(nav, ui, service)) yield return frame;
                nav.ShowIsland();
                yield return "shell-final-garden";
            }
            finally
            {
                if (oldHour.HasValue) GardenVisualClock.SetOverrideMinutes((int)Math.Round(oldHour.Value * 60));
                else GardenVisualClock.UseSystemTime();
            }
        }

        static IEnumerable<string> Garden(MockupNavigation nav, WeekOneQuestDemo ui)
        {
            nav.ShowIsland();
            var sky = GardenSkyEnvironment.Ensure(nav.home.gardenCamera, nav.home.gardenVisuals.transform);
            try
            {
                foreach (int hour in new[] { 12, 0 })
                {
                    GardenVisualClock.SetOverrideMinutes(hour * 60);
                    sky.SetTimeForPreview(hour); nav.home.RefreshFromState();
                    yield return hour == 12 ? "garden-day" : "garden-night";
                }
            }
            // Later settings cases must follow the visual clock, not this
            // temporary capture override (including in the device audit).
            finally { sky.SetTimeForPreview(null); }
            nav.home.SetOverview(false); yield return "garden-plant-close";
            nav.home.SetZoom(1); yield return "garden-plant-zoom";
            nav.home.ResetView(); yield return "garden-view-reset";
            nav.ShowActivities(); ui.ShowPlantCollection();
            foreach (string frame in Pages(ui, "plant-collection-initial")) yield return frame;
        }

        static IEnumerable<string> Shop(MockupNavigation nav, WeekOneQuestDemo ui, GardenStateService service)
        {
            ui.ShowShop();
            foreach (string frame in Pages(ui, "shop")) yield return frame;
            foreach (string id in new[] { "environment:E01", "environment:E02" })
            {
                string label = id.EndsWith("01", StringComparison.Ordinal) ? "flower" : "shelter";
                ui.PreviewEnvironment(id);
                foreach (string frame in Pages(ui, "shop-" + label + "-preview")) yield return frame;
                VisualAuditTraversal.Click(ui, "취소"); yield return "shop-" + label + "-cancel";
                int price = ui.Balance.EnvironmentCost;
                try
                {
                    // Presentation-only unavailable-price fixture; no purchase is
                    // submitted while this temporary balance value is in use.
                    ui.Balance.EnvironmentCost = service.Snapshot.Nutrient + 1;
                    ui.PreviewEnvironment(id);
                    Require(!ui.GetComponentsInChildren<Button>().First(b => b.name == "확정").interactable,
                        "Unaffordable environment still has an enabled confirmation.");
                    foreach (string frame in Pages(ui, "shop-" + label + "-insufficient")) yield return frame;
                }
                finally { ui.Balance.EnvironmentCost = price; }
                ui.PreviewEnvironment(id); yield return null;
                int before = service.Snapshot.Nutrient;
                VisualAuditTraversal.Click(ui, "확정");
                Require(service.Snapshot.Environments.Any(e => e.EnvironmentId == id)
                    && service.Snapshot.Nutrient == before - price, "Synthetic environment purchase failed.");
                yield return "shop-" + label + "-placed";
                nav.ShowActivities(); ui.PreviewEnvironment(id);
                Require(!ui.GetComponentsInChildren<Button>().First(b => b.name == "확정").interactable,
                    "Occupied placement must be unavailable.");
                foreach (string frame in Pages(ui, "shop-" + label + "-occupied")) yield return frame;
                VisualAuditTraversal.Click(ui, "취소"); yield return null;
            }
            VisualAuditTraversal.Click(ui, "식물함 · 성장과 기억");
            foreach (string frame in Pages(ui, "shop-plant-destination")) yield return frame;
        }

        static IEnumerable<string> Experiences(MockupNavigation nav, WeekOneQuestDemo ui,
            GardenStateService service, VisualAuditTraversal.Clock clock)
        {
            ui.ShowExperienceHistory();
            foreach (string frame in Pages(ui, service.Snapshot.Experiences.Count == 0 ? "experience-history-empty" : "experience-history-initial")) yield return frame;
            string[] types = { "즐거움", "불편함", "중립", "잘 모르겠음" };
            for (int kind = 0; kind < types.Length; kind++)
            {
                ui.ShowExperienceTypes(); yield return "experience-type-options-" + kind;
                VisualAuditTraversal.Click(ui, types[kind]); yield return null;
                for (int step = 0; step < 4; step++)
                {
                    string prefix = "experience-" + kind + "-step-" + step;
                    foreach (string frame in Pages(ui, prefix + "-empty")) yield return frame;
                    var field = ui.GetComponentsInChildren<InputField>(false).First();
                    field.text = kind == 3 ? string.Concat(Enumerable.Repeat(Sample + " ", 5)) : Sample;
                    field.onEndEdit.Invoke(field.text);
                    yield return prefix + "-filled";
                    if (step == 2 && kind == 0)
                    {
                        for (int mood = 1; mood <= 5; mood++)
                        {
                            VisualAuditTraversal.Click(ui, PrototypeDefinition.MoodLabels[mood]);
                            yield return prefix + "-mood-" + mood;
                        }
                        VisualAuditTraversal.Click(ui, PrototypeDefinition.MoodLabels[5]);
                        yield return prefix + "-mood-cleared";
                    }
                    if (step > 0 && kind == 0)
                    {
                        VisualAuditTraversal.Click(ui, "이전"); yield return prefix + "-previous";
                        VisualAuditTraversal.Click(ui, "다음"); yield return null;
                    }
                    VisualAuditTraversal.Click(ui, kind == 1 ? "건너뛰기" : step == 3 ? "기록 저장" : "다음");
                    yield return null;
                }
                foreach (string frame in Pages(ui, "experience-" + kind + "-plant-confirm")) yield return frame;
                if (kind == 0)
                {
                    VisualAuditTraversal.Click(ui, "식물함에서 다른 식물 선택");
                    foreach (string frame in Pages(ui, "experience-plant-picker")) yield return frame;
                    if (VisualAuditTraversal.Has(ui, "다음"))
                    {
                        VisualAuditTraversal.Click(ui, "다음");
                        foreach (string frame in Pages(ui, "experience-plant-picker-next")) yield return frame;
                        VisualAuditTraversal.Click(ui, "이전"); yield return "experience-plant-picker-previous";
                    }
                    string selected = WeeklyPlants.Title(service.Snapshot.PlantCollection.Plants.Last(p => !p.Legacy));
                    VisualAuditTraversal.Click(ui, selected); yield return "experience-plant-selected";
                    VisualAuditTraversal.Click(ui, "기록으로 돌아가기"); yield return "experience-plant-back-to-input";
                    VisualAuditTraversal.Click(ui, "기록 저장"); yield return null;
                }
                int count = service.Snapshot.Experiences.Count;
                VisualAuditTraversal.Click(ui, "이 식물에 저장");
                Require(service.Snapshot.Experiences.Count == count + 1, "Synthetic experience was not saved.");
                foreach (string frame in Pages(ui, "experience-" + kind + "-saved")) yield return frame;
                clock.Time = clock.Time.AddMinutes(1);
            }
            ui.ShowExperienceTypes(); VisualAuditTraversal.Click(ui, types[0]); yield return null;
            VisualAuditTraversal.Click(ui, "기록 취소"); yield return "experience-cancelled";

            // More than one history and memory page, with intentional synthetic
            // prose long enough to exercise wrapping and vertical scrolling.
            for (int i = 0; i < 22; i++)
            {
                Require(service.SaveExperience("synthetic-visual-extra-" + i, types[i % types.Length],
                    Sample, "합성 감각", "차분함", "합성 생각"), service.LastError);
                clock.Time = clock.Time.AddMinutes(1);
            }
            ui.ShowExperienceHistory();
            int page = 0;
            do
            {
                foreach (string frame in Pages(ui, "experience-history-page-" + page++)) yield return frame;
                if (!VisualAuditTraversal.Has(ui, "다음 기록")) break;
                VisualAuditTraversal.Click(ui, "다음 기록"); yield return null;
            } while (page < 30);
            if (VisualAuditTraversal.Has(ui, "이전 기록")) { VisualAuditTraversal.Click(ui, "이전 기록"); yield return "experience-history-previous"; }

            foreach (var record in service.Snapshot.Experiences.Take(4))
            {
                int index = Array.IndexOf(types, record.Type);
                ui.ShowExperienceRecord(record.Id);
                foreach (string frame in Pages(ui, "experience-record-" + index)) yield return frame;
                VisualAuditTraversal.Click(ui, "기록 수정");
                foreach (string frame in Pages(ui, "experience-edit-" + index)) yield return frame;
                var field = ui.GetComponentsInChildren<InputField>().First(); field.text = Sample + " 수정한 합성 문장입니다.";
                VisualAuditTraversal.Click(ui, index == 1 ? "취소" : "저장"); yield return "experience-edit-result-" + index;
            }
        }

        static IEnumerable<string> Plants(MockupNavigation nav, WeekOneQuestDemo ui, GardenStateService service)
        {
            ui.ShowPlantCollection();
            int page = 0;
            int pages = Math.Max(1, (service.Snapshot.PlantCollection.Plants.Count + 3) / 4);
            for (; page < pages; page++)
            {
                foreach (string frame in Pages(ui, "plant-collection-page-" + page)) yield return frame;
                if (page + 1 < pages) { VisualAuditTraversal.Click(ui, "다음 식물"); yield return null; }
            }
            if (pages > 1) { VisualAuditTraversal.Click(ui, "이전 식물"); yield return "plant-collection-previous"; }
            foreach (var plant in service.Snapshot.PlantCollection.Plants)
            {
                ui.ShowPlantMemories(plant.Id, 0);
                int memoryPages = Math.Max(1, (plant.Memories.Count + 20) / 21);
                for (int branch = 0; branch < memoryPages; branch++)
                {
                    foreach (string frame in Pages(ui, "plant-" + plant.Ordinal + "-memories-" + branch)) yield return frame;
                    if (branch + 1 < memoryPages) { VisualAuditTraversal.Click(ui, "다음 가지"); yield return null; }
                }
                if (memoryPages > 1) { VisualAuditTraversal.Click(ui, "이전 가지"); yield return "plant-" + plant.Ordinal + "-previous-branch"; }
                VisualAuditTraversal.Click(ui, "정원에서 이 가지 보기"); yield return "garden-archived-plant-" + plant.Ordinal;
                nav.ShowActivities();
            }
            var target = service.Snapshot.PlantCollection.Plants.Last(p => p.Memories.Any(m => m.Kind == MemoryRecordKind.Experience));
            var records = target.Memories.Where(m => m.Kind == MemoryRecordKind.Experience).Take(2).ToArray();
            ui.ShowMemoryChoices(target.Id, records.Select(m => m.OrganId).ToArray());
            foreach (string frame in Pages(ui, "memory-overlap-choices")) yield return frame;
            foreach (var kind in new[] { MemoryRecordKind.SessionReflection, MemoryRecordKind.Experience })
            {
                var owner = service.Snapshot.PlantCollection.Plants.FirstOrDefault(p => p.Memories.Any(m => m.Kind == kind));
                if (owner == null) continue;
                var memory = owner.Memories.First(m => m.Kind == kind);
                ui.ShowMemoryChoices(owner.Id, new[] { memory.OrganId });
                foreach (string frame in Pages(ui, "memory-detail-" + kind)) yield return frame;
                VisualAuditTraversal.Click(ui, kind == MemoryRecordKind.Experience ? "경험 기록 수정" : "감정 기록 수정");
                foreach (string frame in Pages(ui, "memory-edit-" + kind)) yield return frame;
                VisualAuditTraversal.Click(ui, "취소"); yield return "memory-edit-cancel-" + kind;
                ui.ShowMemoryChoices(owner.Id, new[] { memory.OrganId });
                VisualAuditTraversal.Click(ui, "이 기록 삭제");
                foreach (string frame in Pages(ui, "memory-delete-confirm-" + kind)) yield return frame;
                VisualAuditTraversal.Click(ui, "취소"); yield return "memory-delete-cancel-" + kind;
                VisualAuditTraversal.Click(ui, "이 기록 삭제"); yield return null;
                VisualAuditTraversal.Click(ui, "기록 삭제");
                Require(service.Snapshot.PlantCollection.Plants.First(p => p.Id == owner.Id).Memories
                    .First(m => m.OrganId == memory.OrganId).Kind == MemoryRecordKind.None, "Synthetic memory deletion failed.");
                foreach (string frame in Pages(ui, "memory-deleted-" + kind)) yield return frame;
            }
            ui.ShowGrowthHistory();
            foreach (string frame in Pages(ui, "growth-history")) yield return frame;
            if (VisualAuditTraversal.Has(ui, "다음 성장"))
            {
                VisualAuditTraversal.Click(ui, "다음 성장");
                foreach (string frame in Pages(ui, "growth-history-next")) yield return frame;
                VisualAuditTraversal.Click(ui, "이전 성장"); yield return "growth-history-previous";
            }
            foreach (var record in service.Snapshot.GrowthHistory)
            {
                Invoke(ui, "ShowGrowthRecord", record.RequestId);
                foreach (string frame in Pages(ui, "growth-record-" + record.RequestId)) yield return frame;
            }
        }

        static IEnumerable<string> Records(WeekOneQuestDemo ui, GardenStateService service, VisualAuditTraversal.Clock clock)
        {
            // The mission traversal captures every current mission record. Add a
            // historical legacy record to exercise its distinct heart chart shell.
            const string id = "synthetic-visual-legacy-record";
            Require(service.BeginSession(id, "free-mission:M17"), service.LastError);
            Require(service.StartSession(id), service.LastError);
            clock.Time = clock.Time.AddMinutes(4);
            Require(service.CompleteSession(id, "차분함", Sample), service.LastError);
            ui.ShowGardenJournal();
            foreach (string frame in Pages(ui, "participation-history")) yield return frame;
            if (VisualAuditTraversal.Has(ui, "다음 기록"))
            {
                VisualAuditTraversal.Click(ui, "다음 기록");
                foreach (string frame in Pages(ui, "participation-history-next")) yield return frame;
                VisualAuditTraversal.Click(ui, "이전 기록"); yield return "participation-history-previous";
            }
            ui.ShowRecordSummary();
            foreach (string frame in Pages(ui, "record-summary")) yield return frame;
            ui.ShowDiscoveries();
            foreach (string frame in Pages(ui, "animal-discoveries")) yield return frame;
            ui.ShowRecord(id);
            foreach (string frame in Pages(ui, "legacy-record-no-heart")) yield return frame;
            var session = service.Snapshot.Sessions.First(s => s.SessionId == id);
            long start = DateTimeOffset.Parse(session.StartedUtc).ToUnixTimeMilliseconds();
            long end = DateTimeOffset.Parse(session.EndedUtc).ToUnixTimeMilliseconds();
            ui.PreviewHealth = new AndroidPlatformSnapshot { inputMode = "MOCK", healthConnect = new HealthConnectSnapshot {
                heartRate = new HealthMetricSnapshot { status = "AVAILABLE", queryComplete = true,
                    queryStartEpochMs = start, queryEndEpochMs = end, sampleCount = 4, displaySampleCount = 4,
                    heartRateSamples = Enumerable.Range(0, 4).Select(i => new HeartRateSampleSnapshot {
                        measuredAtEpochMs = start + 1000 + i * 60000, beatsPerMinute = new[] { 73, 76, 75, 72 }[i],
                        sourcePackage = "Synthetic source", segmentId = 0 }).ToArray() } } };
            ui.ShowRecord(id);
            foreach (string frame in Pages(ui, "legacy-record-heart")) yield return frame;
            VisualAuditTraversal.Click(ui, "감정 기록 수정");
            foreach (string frame in Pages(ui, "legacy-reflection-edit")) yield return frame;
            VisualAuditTraversal.Click(ui, "저장"); yield return "legacy-reflection-saved";
            ui.PreviewHealth = null;
        }

        static IEnumerable<string> Plans(WeekOneQuestDemo ui, GardenStateService service)
        {
            ui.ShowPersonalPlan();
            foreach (string frame in Pages(ui, "personal-plan")) yield return frame;
            foreach (string key in new[] { "warning-sign", "warning-action", "support", "favorite", "practice-time", "restart-plan" })
            {
                ui.ShowPlanField(key);
                foreach (string frame in Pages(ui, "plan-" + key + "-empty")) yield return frame;
                var field = ui.GetComponentsInChildren<InputField>().First(); field.text = Sample;
                yield return "plan-" + key + "-filled";
                VisualAuditTraversal.Click(ui, "저장");
                Require(service.Snapshot.PracticePreferences.Any(p => p.Key == key && p.Value == Sample), "Synthetic plan save failed.");
                yield return "plan-" + key + "-saved";
            }
            ui.ShowLearnedPracticeRecommendations();
            foreach (string frame in Pages(ui, "learned-recommendations")) yield return frame;
            VisualAuditTraversal.Click(ui, "배운 활동 모두 보기");
            foreach (string frame in Pages(ui, "learned-activities")) yield return frame;
            ui.ShowRecommendationPreferences();
            foreach (string frame in Pages(ui, "recommendation-preferences")) yield return frame;
            int index = 0;
            foreach (string choice in new[] { "몸 살펴보기", "호흡 관찰", "일상 행동", "잠시 멈추기", "1분 정도", "3분 정도", "5분 정도" })
            {
                VisualAuditTraversal.Click(ui, choice);
                foreach (string frame in Pages(ui, "recommendation-preference-choice-" + index++)) yield return frame;
            }
            VisualAuditTraversal.Click(ui, "배운 활동으로"); yield return "recommendation-preferences-return";
        }

        static IEnumerable<string> Settings(MockupNavigation nav, WeekOneQuestDemo ui, GardenStateService service)
        {
            var sensor = nav.GetComponentInChildren<SensorRawDisplay>(true);
            Require(sensor != null, "Settings component is missing.");
            sensor.PrepareView(); nav.ShowSettings(); Invoke(sensor, "RefreshText");
            foreach (string frame in Scroll(sensor.transform, "settings-default")) yield return frame;

            // These rows normally write native/PlayerPrefs settings. Preview only
            // their renderer states and suspend periodic refresh while doing so.
            bool enabled = sensor.enabled; sensor.enabled = false;
            try
            {
                foreach (bool on in new[] { false, true })
                {
                    foreach (string name in new[] { "musicToggle", "reducedMotionToggle", "notificationToggle", "notificationAdaptive" })
                        Invoke(sensor, "UpdateSwitch", Field<Button>(sensor, name), on);
                    Field<GameObject>(sensor, "notificationOptions").SetActive(on);
                    foreach (string frame in Scroll(sensor.transform, "settings-switch-preview-" + (on ? "on" : "off"))) yield return frame;
                }
            }
            finally { sensor.enabled = enabled; Invoke(sensor, "RefreshText"); }

            foreach (string action in new[] { "심박 차트 보기", "건강 기록 동기화", "활동 권한", "건강 데이터 권한" })
            {
                Click(sensor.transform, action);
                foreach (string frame in Scroll(sensor.transform, "settings-mock-action-" + Array.IndexOf(new[] { "심박 차트 보기", "건강 기록 동기화", "활동 권한", "건강 데이터 권한" }, action))) yield return frame;
            }
            sensor.OpenVisualDebug();
            foreach (string frame in Scroll(sensor.transform, "garden-time")) yield return frame;
            foreach (int minute in new[] { 1, 5, 15 })
            {
                Click(sensor.transform, minute + "분"); yield return "garden-time-increment-" + minute;
                Click(sensor.transform, "+ " + minute + "분"); RefreshEditorVisualTime(nav, sensor); yield return "garden-time-forward-" + minute;
                Click(sensor.transform, "− " + minute + "분"); RefreshEditorVisualTime(nav, sensor); yield return "garden-time-back-" + minute;
            }
            Click(sensor.transform, "실제 시간으로"); RefreshEditorVisualTime(nav, sensor); yield return "garden-time-system";
            foreach (string preset in new[] { "낮 12시 보기", "밤 0시 보기" })
            {
                Click(sensor.transform, preset); RefreshEditorVisualTime(nav, sensor);
                yield return preset.StartsWith("낮", StringComparison.Ordinal) ? "garden-time-day-preset" : "garden-time-night-preset";
                nav.ShowSettings(); sensor.OpenVisualDebug(); RefreshEditorVisualTime(nav, sensor); yield return null;
            }
            Click(sensor.transform, "정원 보기"); yield return "garden-time-view";
            nav.ShowSettings(); sensor.OpenVisualDebug(); yield return null;
            Click(sensor.transform, "‹  설정으로"); yield return "garden-time-return";

            sensor.ToggleTestTools(); yield return null;
            foreach (string frame in Scroll(sensor.transform, "settings-test-tools")) yield return frame;
            Click(sensor.transform, "Settings.ResetMissionRecords"); yield return null;
            foreach (string frame in Scroll(sensor.transform, "settings-reset-confirmation")) yield return frame;
            Click(sensor.transform, "취소"); yield return "settings-reset-cancelled";
            int offset = service.Snapshot.PreviewDayOffset;
            sensor.AdvanceMissionDayButton.onClick.Invoke(); yield return null;
            Require(service.Snapshot.PreviewDayOffset == offset + 1, "Synthetic test date did not advance.");
            foreach (string frame in Scroll(sensor.transform, "settings-date-advanced")) yield return frame;
            sensor.ToggleDiagnostics(); yield return null;
            foreach (string frame in Scroll(sensor.transform, "settings-diagnostics")) yield return frame;
            foreach (string action in new[] { "가속도 수집 켜기", "가속도 수집 끄기", "걸음 디버그 OFF", "가상 걸음 +1", "걸음 디버그 ON" })
            {
                Click(sensor.transform, action); yield return null;
                foreach (string frame in Scroll(sensor.transform, "settings-diagnostic-action-" + Array.IndexOf(new[] { "가속도 수집 켜기", "가속도 수집 끄기", "걸음 디버그 OFF", "가상 걸음 +1", "걸음 디버그 ON" }, action))) yield return frame;
            }
            sensor.ToggleDiagnostics(); yield return null;
            // Last data-changing choice: all source records have already been
            // captured. The host runs an isolated APK and an in-memory service.
            Click(sensor.transform, "Settings.ResetMissionRecords"); yield return null;
            int nutrient = service.Snapshot.Nutrient;
            int plants = service.Snapshot.PlantCollection.Plants.Count;
            Click(sensor.transform, "Settings.ConfirmMissionReset"); yield return null;
            Require(service.Snapshot.Sessions.Count == 0 && service.Snapshot.Experiences.Count == 0
                && service.Snapshot.Nutrient == nutrient && service.Snapshot.PlantCollection.Plants.Count == plants,
                "Synthetic reset must remove records while preserving the garden.");
            foreach (string frame in Scroll(sensor.transform, "settings-reset-complete")) yield return frame;
            sensor.ToggleTestTools();
            foreach (string frame in Scroll(sensor.transform, "settings-test-tools-closed")) yield return frame;
            nav.ShowActivities(); ui.ShowGardenJournal();
            foreach (string frame in Pages(ui, "participation-history-reset-empty")) yield return frame;
            ui.ShowExperienceHistory();
            foreach (string frame in Pages(ui, "experience-history-reset-empty")) yield return frame;
        }

        static void RefreshEditorVisualTime(MockupNavigation nav, SensorRawDisplay sensor)
        {
            if (Application.isPlaying) return;
            // Runtime-only components do not receive OnEnable/Changed/Update
            // during Edit-mode captures. Present the already-changed clock.
            var page = sensor.GetComponentInChildren<GardenVisualDebugPage>(true);
            if (page != null) Invoke(page, "Refresh");
            GardenSkyEnvironment.Ensure(nav.home.gardenCamera, nav.home.gardenVisuals.transform).SetTimeForPreview(null);
        }

        static IEnumerable<string> Pages(WeekOneQuestDemo ui, string prefix) => VisualAuditTraversal.ScrollPages(ui, prefix);
        static IEnumerable<string> Scroll(Transform root, string prefix)
        {
            VisualAuditTraversal.Settle();
            var scrolls = root.GetComponentsInChildren<ScrollRect>(false).Where(s => s.isActiveAndEnabled
                && s.vertical && s.content != null && s.viewport != null).ToArray();
            foreach (var scroll in scrolls) { scroll.verticalNormalizedPosition = 1; scroll.velocity = Vector2.zero; }
            yield return prefix + "-top";
            for (int i = 0; i < scrolls.Length; i++)
            {
                var scroll = scrolls[i]; VisualAuditTraversal.Settle();
                float overflow = scroll.content.rect.height - scroll.viewport.rect.height;
                if (overflow <= 1) continue;
                int pages = Math.Max(1, Mathf.CeilToInt(overflow / Math.Max(1, scroll.viewport.rect.height * .8f)));
                for (int page = 1; page <= pages; page++)
                {
                    scroll.verticalNormalizedPosition = 1 - page / (float)pages; scroll.velocity = Vector2.zero;
                    yield return prefix + "-scroll" + i + "-" + page.ToString("00");
                }
            }
        }
        static void Click(Transform root, string name)
        {
            var button = root.GetComponentsInChildren<Button>(false).FirstOrDefault(b => b.isActiveAndEnabled && b.interactable
                && (b.name == name || b.GetComponentsInChildren<Text>(false).Any(t => t.text.Trim() == name)));
            Require(button != null, "Missing enabled shell control: " + name);
            VisualAuditTraversal.Settle(); button.onClick.Invoke();
        }
        static T Field<T>(object target, string name)
        {
            var field = target.GetType().GetField(name, Hidden);
            Require(field != null, "Missing audit renderer field: " + name);
            return (T)field.GetValue(target);
        }
        static void Invoke(object target, string name, params object[] arguments)
        {
            var method = target.GetType().GetMethod(name, Hidden);
            Require(method != null, "Missing audit renderer method: " + name);
            method.Invoke(target, arguments);
        }
        static void Require(bool value, string message) => VisualAuditTraversal.Require(value, message);
    }
}
#endif
