using System;
using System.Linq;
using CapstoneDesign.Runtime.LocalState;
using UnityEngine;

namespace CapstoneDesign.Runtime
{
    public sealed partial class WeekOneQuestDemo
    {
        public bool IsMissionDebugActive
        {
            get
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                return missionDebugOriginalService != null;
#else
                return false;
#endif
            }
        }

        void BuildMissionDebugLauncher(Transform root)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            var layout = root.GetComponent<FocusMobilePageLayout>();
            if (layout != null) layout.BottomInset = Math.Max(layout.BottomInset, 48);
            var button = FigmaMissionUi.Button(root, "P/M 테스트", 272, 716, 94, 44,
                ShowMissionDebugPicker, Color.white, FigmaMissionUi.Green, 13);
            button.name = "Focus.DebugLauncher";
            var rect = (RectTransform)button.transform;
            rect.anchorMin = rect.anchorMax = new Vector2(1, 0);
            rect.pivot = new Vector2(.5f, .5f);
            rect.anchoredPosition = new Vector2(-71, 108);
#endif
        }

        public void ShowMissionDebugPicker()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (!opened || presentingPracticeGrowthSession != null) return;
            RestoreMissionDebug();
            if (!SaveActivePracticeDraft()) return;
            ClearMissionDebugPresentation();
            var root = BeginFigmaPage("mission-debug-picker", ShowMissionDebugPicker, FigmaMissionUi.Background);
            FigmaHeader(root, "미션 테스트", "개발용 · 실제 기록에는 반영되지 않아요.");
            var body = FigmaScroll(root, 132, 550, 820);
            FigmaMissionUi.Label(body, "기본 미션", 24, 8, 342, 28, 18, null, true);
            BuildMissionDebugGrid(root, body, "P", 28, 48);
            FigmaMissionUi.Label(body, "추가 미션", 24, 448, 342, 28, 18, null, true);
            BuildMissionDebugGrid(root, body, "M", 17, 488);
            FigmaMissionUi.Label(body, "코드를 누르면 해당 실습을 바로 열어요.", 24, 778, 342, 28, 14, FigmaMissionUi.Muted);
            FigmaMissionUi.Button(root, "테스트 닫기", 24, 730, 342, 56, () =>
            {
                if (!MissionDebugPickerIsCurrent(root)) return;
                RestoreMissionDebug();
                ShowHome();
            }).name = "Focus.DebugPickerClose";
            FocusEnter(root);
#endif
        }

        public void StartMissionDebug(string code)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (!opened || presentingPracticeGrowthSession != null) return;
            var mission = FocusMissionCatalog.Find((code ?? "").Trim().ToUpperInvariant());
            if (mission == null) return;
            RestoreMissionDebug();
            if (!SaveActivePracticeDraft()) return;

            GardenStateService candidate = null;
            try
            {
                // Build the complete sandbox before replacing anything owned by the
                // live presenter. No file store or platform provider participates.
                candidate = CreateMissionDebugService(mission, out string debugSession);
                var originalService = Service;
                bool originalPreview = preview;
                string originalMode = mode, originalSession = sessionId, originalError = error;
                var originalHealth = PreviewHealth;
                ClearMissionDebugPresentation();
                missionDebugOriginalService = originalService;
                missionDebugOriginalPreview = originalPreview;
                missionDebugOriginalMode = originalMode;
                missionDebugOriginalHealth = originalHealth;
                missionDebugOriginalSession = originalSession;
                missionDebugOriginalError = originalError;
                Service = candidate;
                candidate = null; // The presenter now owns only this temporary service.
                preview = true;
                mode = "MOCK";
                PreviewHealth = new AndroidPlatformSnapshot
                {
                    inputMode = "MOCK", status = "NO_DATA",
                    message = "Synthetic mission test",
                    generatedAtEpochMs = Service.CurrentUtc.ToUnixTimeMilliseconds()
                };
                sessionId = debugSession;
                error = null;
                var debugEntry = ActiveSession();
                if (FocusMissionCatalog.IsGuided(debugEntry))
                {
                    BindGuidedFocus(debugEntry);
                    guidedResumeClock = false;
                }
                ShowFocusPractice();
            }
            catch (Exception exception)
            {
                candidate?.Dispose();
                RestoreMissionDebug();
                ShowMissionDebugPicker();
                error = exception.Message;
                if (notice != null)
                {
                    notice.text = "테스트를 준비하지 못했어요. 코드를 다시 선택해 주세요.";
                    notice.gameObject.SetActive(true);
                    notice.transform.SetAsLastSibling();
                }
            }
#endif
        }

        public void RestoreMissionDebug(bool refreshGarden = true)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (!IsMissionDebugActive) return;
            var sandbox = Service;
            try { ClearMissionDebugPresentation(); }
            finally
            {
                // Restore ownership before disposing the sandbox, including when a
                // caller is closing or destroying the presenter rather than navigating.
                Service = missionDebugOriginalService;
                preview = missionDebugOriginalPreview;
                mode = missionDebugOriginalMode;
                PreviewHealth = missionDebugOriginalHealth;
                sessionId = missionDebugOriginalSession;
                error = missionDebugOriginalError;
                missionDebugOriginalService = null;
                missionDebugOriginalHealth = null;
                missionDebugOriginalMode = missionDebugOriginalSession = missionDebugOriginalError = null;
                screen = "home";
                sandbox?.Dispose();
            }
            if (refreshGarden && this != null && opened && gameObject.activeInHierarchy) UpdateGarden();
#endif
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        GardenStateService missionDebugOriginalService;
        bool missionDebugOriginalPreview;
        string missionDebugOriginalMode, missionDebugOriginalSession, missionDebugOriginalError;
        AndroidPlatformSnapshot missionDebugOriginalHealth;

        bool MissionDebugPickerIsCurrent(RectTransform page) => page != null && page.gameObject.activeInHierarchy
            && view == page.gameObject && screen == "mission-debug-picker";

        void BuildMissionDebugGrid(RectTransform page, Transform parent, string prefix, int count, float top)
        {
            for (int index = 0; index < count; index++)
            {
                string code = prefix + (index + 1).ToString("00");
                FigmaMissionUi.Button(parent, code, 24 + index % 4 * 87, top + index / 4 * 54, 81, 48,
                    () => { if (MissionDebugPickerIsCurrent(page)) StartMissionDebug(code); }, Color.white,
                    prefix == "P" ? FigmaMissionUi.Green : FigmaMissionUi.Brown, 16).name = "Focus.Debug." + code;
            }
        }

        void ClearMissionDebugPresentation()
        {
            ResetDailyEmotionUiDraft();
            // The active draft was saved before entry. Disable the old clock before
            // destroying its view so a deferred callback cannot save to another service.
            if (focusPracticeClock != null) focusPracticeClock.enabled = false;
            if (activityFrame != null)
                foreach (var source in activityFrame.GetComponentsInChildren<AudioSource>(true)) source.Stop();
            ClearRecordHeartRefresh();
            ClearIntegratedPurchasePreview();
            ClearPageView();
            view = null;
            notice = null;
            ClearPracticeDraft();
            planDrafts.Clear();
            pageHistory.Clear();
            restoringPage = false;
            recordsPage = 0;
            coursePageInitialized = false;
            focusPracticeClock = null;
            focusDrawing = null;
            focusInteractionSession = null;
            focusInteractionStep = -1;
            focusDraftSession = null;
            focusDraftStep = -1;
            focusAnswer = focusMood = focusNote = "";
            focusDirty = focusAllEmotions = false;
            focusAnswerInput = focusNoteInput = null;
            focusNextAction = 0;
            guidedDraftSession = null;
            guidedChoice = guidedReflection = "";
            guidedElapsed = 0;
            guidedResumeClock = false; guidedSlide = guidedHelpReturn = GuidedSlide.Overview;
            guidedHelpIndex = 0; guidedViewRevision++;
            ResetBasicFocusSlides();
            mood = draftNote = null;
            note = null;
            presentingPracticeGrowthSession = null;
            stepQueries.Clear();
            runningStepQuery = null;
            forceClosedStepSync = preferNewestClosedStepPeriod = false;
            nextStepSync = nextStepPoll = nextClosedStepSync = nextWalkingBaselinePoll = 0;
            walkingBaselineRefresh.Request("MOCK", null, DateTimeOffset.UtcNow, TimeZoneInfo.Utc, 0, null);
            walkingBaselineSnapshot = null;
            walkingIntroSession = null;
            walkingIntroGoal = walkingIntroExplanation = null;
            walkingIntroStart = null;
            nextCycleCheck = Time.unscaledTime + 30;
        }

        static GardenStateService CreateMissionDebugService(FocusMissionDefinition target, out string targetSession)
        {
            var clock = new MissionDebugClock();
            var service = new GardenStateService(new MissionDebugMemoryStore(), clock, new DemoBalanceConfig());
            try
            {
                RequireMissionDebug(service.Open(), service);
                RequireMissionDebug(service.RefreshCycle(Array.Empty<string>()), service);
                int targetOrder = 0;
                if (target.Role == FocusMissionCatalog.BasicRole)
                {
                    targetOrder = int.Parse(target.Code.Substring(1), System.Globalization.CultureInfo.InvariantCulture);
                    RequireMissionDebug(service.SelectMbctProgram("8w"), service);
                    // The first 28 occurrences in the existing 8-week course are P01-P28.
                    if (targetOrder < 1 || targetOrder > 28 || FocusMissionPolicy.MissionAt(targetOrder, "8w")?.Id != target.Id)
                        throw new InvalidOperationException("Unsupported synthetic course occurrence.");
                    for (int order = 1; order < targetOrder; order++)
                    {
                        AdvanceMissionDebugToAvailableDay(service, clock);
                        EnsureMissionDebugGrowingPlant(service);
                        var previous = FocusMissionPolicy.MissionAt(order, "8w");
                        string id = "mission-debug-prior-" + order;
                        RequireMissionDebug(service.BeginFocusSession(id, previous.Id, order, false, "8w"), service);
                        RequireMissionDebug(service.StartSession(id), service);
                        CompleteMissionDebugPredecessor(service, id);
                        clock.AdvanceDay();
                        RequireMissionDebug(service.RefreshCycle(Array.Empty<string>()), service);
                    }
                    AdvanceMissionDebugToAvailableDay(service, clock);
                }
                EnsureMissionDebugGrowingPlant(service);
                targetSession = "mission-debug-" + target.Code + "-" + Guid.NewGuid().ToString("N");
                RequireMissionDebug(service.BeginFocusSession(targetSession, target.Id, targetOrder, false,
                    target.Role == FocusMissionCatalog.BasicRole ? "8w" : null), service);
                string createdSessionId = targetSession;
                if (target.Code == "M15")
                    RequireMissionDebug(service.StartWalkingSession(targetSession,
                        WalkingPersonalization.Evaluate(null, service.CurrentUtc, service.CurrentTimeZone)), service);
                else if (!FocusMissionCatalog.IsGuided(service.Snapshot.Sessions.First(item => item.SessionId == createdSessionId))
                    && !UsesBasicFocusSlides(service.Snapshot.Sessions.First(item => item.SessionId == createdSessionId)))
                    RequireMissionDebug(service.StartSession(targetSession), service);
                return service;
            }
            catch { service.Dispose(); throw; }
        }

        static void CompleteMissionDebugPredecessor(GardenStateService service, string id)
        {
            var session = service.Snapshot.Sessions.First(item => item.SessionId == id);
            var definition = FocusMissionCatalog.FindForSession(session);
            while (session.InstructionStep < definition.Steps.Length)
            {
                var step = definition.Steps[session.InstructionStep];
                string answer = step.Options.FirstOrDefault() ?? "";
                RequireMissionDebug(service.ConfirmFocusStep(id, session.InstructionStep, answer), service);
                session = service.Snapshot.Sessions.First(item => item.SessionId == id);
            }
            RequireMissionDebug(service.CompleteFocusSession(id), service);
        }

        static void AdvanceMissionDebugToAvailableDay(GardenStateService service, MissionDebugClock clock)
        {
            // Respect the real daily and weekly limits; the clock is synthetic,
            // so no product quota or stored user date needs a developer bypass.
            for (int skipped = 0; FocusMissionPolicy.Availability(service.Snapshot, "8w") != null; skipped++)
            {
                if (skipped >= 7) throw new InvalidOperationException("Synthetic course did not become available.");
                clock.AdvanceDay();
                RequireMissionDebug(service.RefreshCycle(Array.Empty<string>()), service);
            }
        }

        static void EnsureMissionDebugGrowingPlant(GardenStateService service)
        {
            var state = service.Snapshot;
            if (GardenPlantVisuals.HasCurrentPlant(state) && !GardenPlantVisuals.IsCompleted(state)) return;
            string plant = state.PlantAppearance.Recipe.InstanceId;
            if (GardenPlantVisuals.HasCurrentPlant(state))
                RequireMissionDebug(service.SavePlantPhoto("mission-debug-photo-" + plant, plant), service);
            RequireMissionDebug(service.StartNewPlant("mission-debug-plant-" + plant, plant), service);
        }

        static void RequireMissionDebug(bool succeeded, GardenStateService service)
        {
            if (!succeeded) throw new InvalidOperationException(service.LastError ?? "Synthetic mission setup failed.");
        }

        sealed class MissionDebugClock : IClock
        {
            public DateTimeOffset UtcNow { get; private set; } = new DateTimeOffset(2031, 1, 6, 12, 0, 0, TimeSpan.Zero);
            public TimeZoneInfo TimeZone => TimeZoneInfo.Utc;
            public void AdvanceDay() => UtcNow = UtcNow.AddDays(1);
        }

        sealed class MissionDebugMemoryStore : IStateStore, IDisposable
        {
            GardenState saved;
            public GardenState Load() => saved == null ? null : StateCodec.Clone(saved);
            public void Save(GardenState candidate) => saved = StateCodec.Clone(candidate);
            public void Dispose() => saved = null;
        }
#endif
    }
}
