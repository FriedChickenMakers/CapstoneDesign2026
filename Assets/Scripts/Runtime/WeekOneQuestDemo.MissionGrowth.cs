using System;
using CapstoneDesign.Runtime.LocalState;
using UnityEngine;

namespace CapstoneDesign.Runtime
{
    public sealed partial class WeekOneQuestDemo
    {
        string presentingPracticeGrowthSession;

        bool RequireMissionPlant()
        {
            if (!opened) return false;
            if (Service.MissionPlantAvailability == null) return true;
            ShowMissionPlantRequired();
            return false;
        }

        void ShowMissionPlantRequired()
        {
            var state = Service.Snapshot;
            if (GardenPlantVisuals.HasCurrentPlant(state)) { ShowHome(); return; }
            var source = state.PlantAppearance?.Recipe?.InstanceId;
            var root = BeginFigmaPage("mission-plant-required", ShowMissionPlantRequired, FigmaMissionUi.Background);
            FigmaHeader(root, "새 씨앗과 함께 시작해요", "미션을 함께할 식물이 필요해요.");
            FigmaMissionUi.Label(root, "사진첩에 보낸 식물과 메모는 그대로 있어요.\n새 씨앗을 심으면 미션을 이어갈 수 있어요.",
                24, 188, 342, 96, 18, FigmaMissionUi.Ink).name = "Mission.PlantRequired";
            FigmaMissionUi.Button(root, "새 식물 키우기", 24, 340, 342, 56, () =>
            {
                if (root != null && view == root.gameObject && screen == "mission-plant-required") StartNewGardenPlant(source);
            }).name = "Mission.StartNewPlant";
            FigmaMissionUi.Button(root, "오늘의 미션으로", 24, 412, 342, 48, ShowHome, Color.white, FigmaMissionUi.Green);
            FocusEnter(root);
        }

        // The completion, old reflection fields and growth receipt commit together.
        // A failed write leaves the practice resumable without a second reward.
        void HandlePracticeCompletion(string practiceSessionId, Action continueAfterCompletion, Func<bool> commit = null)
        {
            if (!opened || string.IsNullOrEmpty(practiceSessionId) || presentingPracticeGrowthSession != null) return;
            if (!RequireMissionPlant()) return;
            var before = Service.Snapshot;
            var prior = Service.GetPracticeGrowth(practiceSessionId);
            if (!(commit == null ? Service.CompletePracticeSession(practiceSessionId) : commit()))
            {
                error = Service.LastError;
                ShowPracticeCompletionRetry(practiceSessionId, continueAfterCompletion, commit);
                return;
            }
            error = null;
            var after = Service.Snapshot;
            if (prior == null) PlayCommittedGrowth(before, after, Service.GetPracticeGrowth(practiceSessionId), continueAfterCompletion);
            else { UpdateGarden(); continueAfterCompletion(); }
        }

        void PlayCommittedGrowth(GardenState before, GardenState after, PracticeGrowthRecord record, Action continueAfterGrowth)
        {
            bool finished = false;
            string generation = before.InstallationId;
            void Continue()
            {
                if (finished) return;
                finished = true;
                if (this == null || !opened || HasPendingFullReset || Service.Snapshot.InstallationId != generation) return;
                presentingPracticeGrowthSession = null;
                if (this == null || !opened || !isActiveAndEnabled || !gameObject.activeInHierarchy) return;
                pageHistory.RemoveAll(page => page.Key == "practice-complete-error");
                UpdateGarden();
                continueAfterGrowth();
            }
            if (!IsMissionDebugActive && record != null && record.AfterStep > record.BeforeStep)
            {
                presentingPracticeGrowthSession = record.SessionId;
                var home = transform.root.GetComponent<GardenHomePresenter>();
                if (GardenMissionGrowthSequence.TryPlay(home, before, after, Continue)) return;
            }
            Continue();
        }

        void ShowPracticeCompletionRetry(string id, Action continueAfterCompletion, Func<bool> commit = null)
        {
            var root = BeginFigmaPage("practice-complete-error", () => ShowPracticeCompletionRetry(id, continueAfterCompletion, commit), FigmaMissionUi.Background);
            FigmaHeader(root, "실습을 마쳤어요", "미션 완료 저장 확인");
            FigmaMissionUi.Label(root, "아직 완료를 저장하지 못했어요.\n실습 내용은 그대로 남아 있어요.", 24, 184, 342, 72, 18, FigmaMissionUi.Ink);
            FigmaMissionUi.Label(root, "다시 시도해도 식물은 한 번만 자라요.", 24, 280, 342, 48, 16, FigmaMissionUi.Muted);
            FigmaMissionUi.Button(root, "오늘의 미션으로", 24, 686, 342, 48, ShowHome, Color.clear, FigmaMissionUi.Green, 16);
            FigmaMissionUi.Button(root, "다시 시도", 24, 746, 342, 56, () =>
            {
                if (view == root.gameObject && screen == "practice-complete-error" && sessionId == id)
                    HandlePracticeCompletion(id, continueAfterCompletion, commit);
            }).name = "PracticeCompletion.Retry";
            FocusEnter(root);
        }

        string StoredGrowthSummary(GardenState state) => state.StoredGrowthSteps > 0
            ? "보관한 성장 " + state.StoredGrowthSteps + "회" : null;

        void AddStoredGrowthGuide(Transform parent, GardenState state)
        {
            if (state.StoredGrowthSteps <= 0) return;
            GardenUi.FlowLabel(parent, StoredGrowthSummary(state) + "\n새 식물을 시작하면 성장이 이어져요.", GardenUi.CaptionSize, GardenUi.Muted)
                .name = "PlantGrowth.StoredGuide";
        }

        void AddCompletedPlantGuide(Transform parent, GardenState state)
        {
            if (!GardenPlantVisuals.HasCurrentPlant(state) || !GardenPlantVisuals.IsCompleted(state)) return;
            GardenUi.FlowLabel(parent, "식물이 완성됐어요. 기록을 마친 뒤 사진첩에 남겨 보세요.", GardenUi.CaptionSize, GardenUi.Muted)
                .name = "PlantGrowth.CompletedNotice";
        }

    }
}
