using System;
using System.IO;
using CapstoneDesign.Runtime.LocalState;
using UnityEngine;

namespace CapstoneDesign.Runtime
{
    public sealed partial class WeekOneQuestDemo
    {
        string persistentStateFile, presentedResetRequest;
        bool fullResetCleanupRunning;
        float nextFullResetAttempt;
        public bool HasPendingFullReset => opened && Service.HasPendingFullReset;
        public string FullResetStatusText { get; private set; }
        public string ResetSourceInstallationId
        {
            get
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                if (IsMissionDebugActive) return missionDebugOriginalService.Snapshot.InstallationId;
#endif
                return opened ? Service.Snapshot.InstallationId : null;
            }
        }
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        // Isolated validation injects cleanup failures without touching app files,
        // native services or the host application's preferences.
        public Func<string, PlatformActionResult> FullResetCleanupOverride;
#endif

        public bool ResetAllData(string requestId, string expectedInstallationId)
        {
            if (!opened || fullResetCleanupRunning) return false;
            RestoreMissionDebug(false);
            if (!Service.ResetAll(requestId, expectedInstallationId))
            {
                FullResetStatusText = "초기화를 시작하지 못했어요. 기존 기록은 그대로예요. 다시 시도해 주세요.";
                return false;
            }
            if (!Service.HasPendingFullReset) return true; // Completed duplicate command.
            return TryContinueFullReset();
        }

        public bool TryContinueFullReset()
        {
            if (!opened || fullResetCleanupRunning) return false;
            if (!Service.HasPendingFullReset) return true;
            string request = Service.Snapshot.FullResetRequestId;
            if (presentedResetRequest != request)
            {
                presentedResetRequest = request;
                ClearFullResetPresentation();
            }
            fullResetCleanupRunning = true;
            try
            {
                PlatformActionResult result;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                if (FullResetCleanupOverride != null) result = FullResetCleanupOverride(request);
                else
#endif
                // Injected preview services never authorize access to real app storage.
                if (string.IsNullOrEmpty(persistentStateFile)) result = new PlatformActionResult { status = "AVAILABLE" };
                else result = AppLocalReset.Cleanup(request, persistentStateFile);
                if (result == null || result.status != "AVAILABLE")
                {
                    FullResetStatusText = result?.status == "PENDING"
                        ? "전체 초기화를 진행하고 있어요. 잠시 기다려 주세요."
                        : "초기화 정리가 아직 끝나지 않았어요. 다시 시도하면 이어서 정리해요.";
                    nextFullResetAttempt = Time.unscaledTime + 2;
                    return false;
                }
                if (!Service.FinishFullReset(request))
                {
                    FullResetStatusText = "초기화 저장을 마무리하지 못했어요. 다시 시도하면 이어서 정리해요.";
                    nextFullResetAttempt = Time.unscaledTime + 2;
                    return false;
                }
                error = null;
                FullResetStatusText = "모든 기록과 정원을 초기 상태로 되돌렸어요.";
                ClearFullResetPresentation();
                UpdateGarden();
                transform.root.GetComponent<GardenHomePresenter>()?.ResetView();
                ShowHome();
                return true;
            }
            catch (Exception)
            {
                FullResetStatusText = "초기화 정리가 아직 끝나지 않았어요. 다시 시도해 주세요.";
                nextFullResetAttempt = Time.unscaledTime + 2;
                return false;
            }
            finally { fullResetCleanupRunning = false; }
        }

        public void PollFullResetRecovery()
        {
            if(!HasPendingFullReset)return;
            if(Time.unscaledTime>=nextFullResetAttempt)TryContinueFullReset();
            if(HasPendingFullReset && isActiveAndEnabled && screen!="full-reset-pending")ShowFullResetRecovery();
            else if(HasPendingFullReset && view!=null)
            {
                foreach(var label in view.GetComponentsInChildren<UnityEngine.UI.Text>(true))
                    if(label.name=="FullReset.PendingStatus")label.text=FullResetStatusText
                        ?? "초기화 정리가 끝나면 새 정원으로 돌아가요.";
            }
        }

        void ShowFullResetRecovery()
        {
            var root = BeginFigmaPage("full-reset-pending", ShowFullResetRecovery, FigmaMissionUi.Background);
            FigmaHeader(root, "전체 초기화 중", "저장된 초기화를 마무리하고 있어요.");
            FigmaMissionUi.Label(root, FullResetStatusText ?? "초기화 정리가 끝나면 새 정원으로 돌아가요.",
                24, 192, 342, 96, 18, FigmaMissionUi.Ink).name = "FullReset.PendingStatus";
            FigmaMissionUi.Button(root, "초기화 다시 시도", 24, 340, 342, 56, () =>
            {
                if (root != null && view == root.gameObject && HasPendingFullReset)
                { if (!TryContinueFullReset()) ShowFullResetRecovery(); }
            }).name = "FullReset.Retry";
            FocusEnter(root);
        }

        void ClearFullResetPresentation()
        {
            ClosePlantMemoPopup();
            // Stop callbacks before discarding their views and detached drafts.
            StopAllCoroutines();
            if (focusPracticeClock != null) focusPracticeClock.enabled = false;
            if (activityFrame != null)
                foreach (var source in activityFrame.GetComponentsInChildren<AudioSource>(true)) source.Stop();
            transform.root.GetComponentInChildren<GardenMissionGrowthSequence>(true)?.Interrupt();
            ClearRecordHeartRefresh(); ClearIntegratedPurchasePreview(); ClearGhost();
            ClearPageView(); view = null; notice = null;
            ClearPracticeDraft(); ResetDailyEmotionUiDraft(); planDrafts.Clear();
            focusAdjustmentCancel = null; focusPlanPersist = null;
            pageHistory.Clear(); restoringPage = false;
            sessionId = mood = draftNote = null; note = null;
            recordsPage = coursePage = freeIndex = experiencePage = experienceStep = 0;
            coursePageInitialized = false;
            experienceId = experienceType = null; Array.Clear(experienceValues, 0, experienceValues.Length);
            focusPracticeClock = null; focusDrawing = null;
            focusInteractionSession = null; focusInteractionStep = -1;
            focusDraftSession = null; focusDraftStep = -1;
            focusAnswer = focusMood = focusNote = ""; focusDirty = focusAllEmotions = false;
            focusAnswerInput = focusNoteInput = null; focusNextAction = 0;
            guidedDraftSession = null; guidedChoice = guidedReflection = ""; guidedElapsed = 0;
            guidedResumeClock = false; guidedSlide = guidedHelpReturn = GuidedSlide.Overview;
            guidedHelpIndex = 0; guidedViewRevision++;
            ResetBasicFocusSlides();
            focusExpandedCurricula.Clear(); focusExpandedRecordSteps.Clear();
            focusVisibleRecommendation = null; focusRecommendationSignature = null;
            photoService = null; photoRenderedStatus = null; nextPhotoPoll = 0;
            presentingPracticeGrowthSession = null;
            pendingEnvironment = pendingPurchase = null;
            plantAlbumPage = 0; pendingPlantPhotoRequest = pendingPlantPhotoSource = null;
            pendingNewPlantRequest = pendingNewPlantSource = null;
            plantAlbumSaveFailed = newPlantStartFailed = false;
            stepQueries.Clear(); runningStepQuery = null;
            forceClosedStepSync = preferNewestClosedStepPeriod = false;
            lastClosedStepQueryPeriodId = "";
            stepQueryStatus = "아직 걸음 구간을 조회하지 않았어요";
            nextStepSync = nextStepPoll = nextClosedStepSync = nextWalkingBaselinePoll = 0;
            walkingBaselineRefresh.Request("MOCK", null, DateTimeOffset.UtcNow, TimeZoneInfo.Utc, 0, null);
            walkingBaselineSnapshot = null; walkingIntroSession = null;
            walkingIntroGoal = walkingIntroExplanation = null; walkingIntroStart = null;
            reminderSync.Invalidate(); nextNotificationPoll = 0;
            nextCycleCheck = Time.unscaledTime + 30;
            GardenVisualClock.UseSystemTime();
            screen = "home";
        }
    }
}
