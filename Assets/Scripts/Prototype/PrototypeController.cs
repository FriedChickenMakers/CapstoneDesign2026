using System;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace CapstoneDesign.Prototype
{
    public enum PrototypePage { Garden, CheckIn, Mission, MoodHistory }

    public sealed class PrototypeController : MonoBehaviour
    {
        public PrototypeDefinition definition;
        public PrototypePlantView plantView;
        public PrototypeMissionPresentation missionPresentation;
        public PrototypeUiSelectionView selectionView;
        public GameObject[] pages;
        public GameObject screenRoot;
        public GameObject modalBackdrop;
        public GameObject growthPopup;
        public GameObject settingsPopup;
        public GameObject resetPopup;
        public GameObject missionCompletedPopup;
        public GameObject developerPanel;
        public TMP_Text dateText;
        public TMP_Text stageText;
        public TMP_Text gardenTitle;
        public TMP_Text gardenGuide;
        public TMP_Text gardenActionLabel;
        public TMP_Text dailyStatus;
        public Button gardenButton;
        public PrototypeUiSurface[] progressDots;
        public TMP_Text questionText;
        public TMP_Text quietLabel;
        public TMP_Text togetherLabel;
        public TMP_Text moodStatus;
        public TMP_Text checkInActionLabel;
        public Button checkInAction;
        public Button quietButton;
        public Button togetherButton;
        public TMP_InputField moodNoteInput;
        public TMP_Text noteCounter;
        public Button[] moodButtons;
        public PrototypeMoodFace[] moodFaces;
        public TMP_Text missionTitle;
        public TMP_Text missionGuide;
        public TMP_Text missionStatus;
        public Button missionAction;
        public TMP_Text growthTitle;
        public TMP_Text growthGuide;
        public TMP_Text growthStage;
        public TMP_Text growthSummary;
        public TMP_Text settingsDate;
        public PrototypeMoodHistoryView moodHistory;
        public PrototypePortraitLayout portraitLayout;
        public TMP_Text historyActionLabel;
        public TMP_Text homeTabLabel;
        public TMP_Text historyTabLabel;

        public PrototypeSession Session { get; private set; }
        public bool IsMissionInProgress => Session != null && Session.IsMissionInProgress;
        // Subscribe for changes, and read IsMissionInProgress for the initial value.
        public event Action<bool> MissionInProgressChanged;
        private bool lastMissionInProgress;
        public PrototypePage CurrentPage { get; private set; }
        public bool DeveloperMode => Debug.isDebugBuild || Application.isEditor;
        private int displayedRound;
        private int demoDays;
        private float nextDateCheck;
        private bool ModalOpen => growthPopup.activeSelf || settingsPopup.activeSelf || resetPopup.activeSelf
            || (missionCompletedPopup != null && missionCompletedPopup.activeSelf);
        private static readonly Color Green = new Color(.31f, .47f, .37f);
        private static readonly Color Pale = new Color(.88f, .92f, .86f);

        private void Awake()
        {
            Application.targetFrameRate = 60;
            Screen.orientation = ScreenOrientation.Portrait;
            BeginSession();
        }
        private void OnDestroy() { if (Session != null) Session.Changed -= Refresh; }
        private void Update()
        {
            if (Time.unscaledTime >= nextDateCheck) { nextDateCheck = Time.unscaledTime + 1; SyncDay(); }
            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame
                && (moodNoteInput == null || !moodNoteInput.isFocused)) Back();
        }
        private void OnApplicationFocus(bool focus) { if (focus && Session != null) SyncDay(); }
        private bool SyncDay()
        {
            if (Session == null || growthPopup.activeSelf || !Session.RefreshDay()) return false;
            ShowGarden(); // Discard stale screen inputs when the date changes.
            return true;
        }
        private void BeginSession()
        {
            if (Session != null) Session.Changed -= Refresh;
            demoDays = 0;
            Session = new PrototypeSession(definition, () => DateTime.Now.AddDays(demoDays));
            Session.Changed += Refresh;
            pages[(int)PrototypePage.Garden].GetComponentInChildren<PrototypeIslandDrag>(true)?.ResetView();
            developerPanel.SetActive(DeveloperMode);
            ShowGarden();
        }
        public void ShowGarden()
        {
            Session.RefreshDay();
            ShowPage(PrototypePage.Garden);
        }
        public void ShowMoodHistory()
        {
            if (ModalOpen) return;
            Session.RefreshDay();
            ShowPage(PrototypePage.MoodHistory);
        }
        public void HistoryAction()
        {
            if (ModalOpen || CurrentPage != PrototypePage.MoodHistory) return;
            ShowGarden();
            if (!Session.RecordCompleted && !Session.IsComplete) GardenAction();
        }
        public void Back() => ShowGarden();
        private void ShowPage(PrototypePage page)
        {
            ClosePopups();
            CurrentPage = page;
            for (int i = 0; i < pages.Length; i++) pages[i].SetActive(i == (int)page);
            RefreshLayout();
            Refresh();
        }
        public void ClosePopups()
        {
            growthPopup.SetActive(false);
            settingsPopup.SetActive(false);
            resetPopup.SetActive(false);
            if (missionCompletedPopup != null) missionCompletedPopup.SetActive(false);
            screenRoot.SetActive(true);
            modalBackdrop.SetActive(false);
            RefreshLayout();
        }
        private void OpenModal(GameObject popup)
        {
            ClosePopups();
            screenRoot.SetActive(false);
            modalBackdrop.SetActive(true);
            popup.SetActive(true);
            RefreshLayout();
        }
        private void RefreshLayout()
        {
            if (portraitLayout == null) return;
            portraitLayout.Apply();
        }
        public void GardenAction()
        {
            if (SyncDay() || ModalOpen || CurrentPage != PrototypePage.Garden) return;
            if (Session.RecordCompleted) { OpenModal(missionCompletedPopup); return; }
            if (Session.IsComplete) { OpenGrowth(); return; }
            if (Session.AwaitingMoodRecord) { OpenCheckIn(); return; }
            var result = Session.StartMission(displayedRound);
            if (result == PrototypeOutcome.Success) ShowPage(PrototypePage.Mission);
            else if (result == PrototypeOutcome.StaleRequest) ShowGarden();
        }
        public void OpenCheckIn()
        {
            if (SyncDay() || ModalOpen) return;
            if (!Session.MissionCompleted) return;
            ShowPage(PrototypePage.CheckIn);
        }
        public void ConfirmCheckIn()
        {
            if (SyncDay() || ModalOpen || CurrentPage != PrototypePage.CheckIn) return;
            if (Session.RecordCompleted) { ShowGarden(); return; }
            var result = Session.RecordMood(displayedRound);
            if (result == PrototypeOutcome.Success) OpenGrowth();
            else if (result == PrototypeOutcome.StaleRequest) ShowGarden();
        }
        public void SelectHappy() => SetMood(Mood.Happy);
        public void SelectCalm() => SetMood(Mood.Calm);
        public void SelectNeutral() => SetMood(Mood.Neutral);
        public void SelectTired() => SetMood(Mood.Tired);
        public void SelectDown() => SetMood(Mood.Down);
        private void SetMood(Mood mood)
        {
            if (SyncDay() || ModalOpen || CurrentPage != PrototypePage.CheckIn) return;
            Session.SetMood(displayedRound, Session.TodayMood == mood ? Mood.None : mood);
        }
        public void EditMoodNote(string text)
        {
            if (SyncDay() || ModalOpen || CurrentPage != PrototypePage.CheckIn) return;
            Session.SetNote(displayedRound, text);
        }
        public void ChooseQuiet() => Choose(PlantChoice.Quiet);
        public void ChooseTogether() => Choose(PlantChoice.Together);
        private void Choose(PlantChoice choice)
        {
            if (SyncDay() || ModalOpen || CurrentPage != PrototypePage.CheckIn) return;
            Session.SetChoice(displayedRound, choice);
        }
        public void MissionAction()
        {
            if (SyncDay() || ModalOpen || CurrentPage != PrototypePage.Mission) return;
            var result = Session.CompleteMission(displayedRound);
            if (result == PrototypeOutcome.Success) OpenCheckIn();
            else if (result == PrototypeOutcome.StaleRequest) ShowGarden();
        }
        private void OpenGrowth() { Refresh(); OpenModal(growthPopup); }
        public void ConfirmGrowth() => ShowGarden();
        public void OpenSettings() { if (DeveloperMode) { Refresh(); OpenModal(settingsPopup); } }
        public void DemoNextDay()
        {
            if (!DeveloperMode || !settingsPopup.activeSelf) return;
            demoDays++;
            Session.RefreshDay();
            ShowGarden();
        }
        public void AskReset() { if (DeveloperMode) OpenModal(resetPopup); }
        public void ConfirmReset() { if (DeveloperMode && resetPopup.activeSelf) BeginSession(); }

        public void Refresh()
        {
            if (Session == null) return;
            displayedRound = Session.Round;
            dateText.text = Session.Day.ToString("M월 d일") + (demoDays > 0 ? " · 시연" : "");
            stageText.text = PrototypeDefinition.StageLabels[Session.Stage];
            gardenTitle.text = Session.IsComplete ? Session.FinalFlower.displayName : "오늘의 식물";
            gardenGuide.text = Session.IsComplete ? Session.FinalFlower.meaning : "섬을 드래그해 천천히 둘러보세요.";
            dailyStatus.text = IsMissionInProgress ? "미션 진행 중" : Session.MissionCompleted ? "오늘의 실천 완료" : Session.IsComplete ? "세 번의 작은 실천" : "오늘의 작은 실천";
            gardenActionLabel.text = Session.RecordCompleted ? "미션 완료" : Session.IsComplete ? "꽃 다시 보기"
                : Session.AwaitingMoodRecord ? "마음 기록 이어하기"
                : Session.MissionStarted ? "미션 이어하기" : "오늘의 미션";
            gardenButton.interactable = true;
            for (int i = 0; i < progressDots.Length; i++)
                progressDots[i].color = i <= Session.Stage ? Green : Pale;
            var question = definition.questions[Session.QuestionIndex];
            questionText.text = question.question;
            quietLabel.text = question.quietLabel;
            togetherLabel.text = question.togetherLabel;
            moodStatus.text = Session.TodayMood == Mood.None ? "감정 선택은 자유예요" : PrototypeDefinition.MoodLabels[(int)Session.TodayMood] + " · 선택됨";
            bool editable = Session.AwaitingMoodRecord && !Session.IsComplete;
            for (int i = 0; i < moodButtons.Length; i++)
            {
                moodButtons[i].interactable = editable;
                moodFaces[i].SetSelected((int)Session.TodayMood == i + 1);
            }
            quietButton.interactable = togetherButton.interactable = editable;
            if (moodNoteInput != null)
            {
                moodNoteInput.readOnly = !editable;
                // Do not reset the caret/IME composition for unrelated refreshes.
                if (moodNoteInput.text != Session.TodayNote) moodNoteInput.SetTextWithoutNotify(Session.TodayNote);
                noteCounter.text = Session.TodayNote.Length + " / " + PrototypeSession.NoteCharacterLimit;
            }
            TintChoice(quietButton, quietLabel, Session.TodayChoice == PlantChoice.Quiet);
            TintChoice(togetherButton, togetherLabel, Session.TodayChoice == PlantChoice.Together);
            checkInAction.interactable = Session.RecordCompleted || (editable && Session.TodayChoice != PlantChoice.None);
            checkInActionLabel.text = Session.RecordCompleted ? "메인으로" : "기록하고 성장 보기";
            missionTitle.text = definition.missionTitle;
            missionGuide.text = definition.missionGuide;
            missionStatus.text = IsMissionInProgress ? "미션 진행 중 · 나의 속도로" : "하루 한 번 · 나의 속도로";
            missionAction.interactable = IsMissionInProgress;
            growthTitle.text = Session.IsComplete ? Session.FinalFlower.displayName : "오늘의 실천이 자랐어요";
            growthGuide.text = Session.IsComplete ? Session.FinalFlower.meaning : "기분과 상관없이, 해낸 행동은 남아요.";
            growthStage.text = (Session.Stage + 1) + " / " + PrototypeDefinition.StageCount + " · " + PrototypeDefinition.StageLabels[Session.Stage];
            growthSummary.text = Session.IsComplete ? $"나만의 시간 {Session.QuietCount} · 연결의 시간 {Session.TogetherCount}" : "한 모금 천천히 · 완료";
            settingsDate.text = Session.Day.ToString("yyyy.MM.dd") + (demoDays > 0 ? " · 시연 날짜" : " · 기기 날짜");
            if (moodHistory != null)
            {
                moodHistory.Render(Session.Completions);
                historyActionLabel.text = Session.RecordCompleted || Session.IsComplete ? "홈 · 정원으로"
                    : Session.AwaitingMoodRecord ? "마음 기록 이어하기" : Session.MissionStarted ? "이어하기" : "오늘 시작하기";
                bool historySelected = CurrentPage == PrototypePage.MoodHistory;
                historyTabLabel.fontStyle = historySelected ? FontStyles.Bold : FontStyles.Normal;
                homeTabLabel.fontStyle = historySelected ? FontStyles.Normal : FontStyles.Bold;
            }
            plantView.Render(Session, definition);
            if (selectionView != null) selectionView.Render(CurrentPage, Session.TodayMood, Session.TodayChoice);
            if (missionPresentation != null) missionPresentation.Render(IsMissionInProgress, Session.RecordCompleted);
            if (lastMissionInProgress != IsMissionInProgress)
            {
                lastMissionInProgress = IsMissionInProgress;
                MissionInProgressChanged?.Invoke(lastMissionInProgress);
            }
        }
        private static void TintChoice(Button button, TMP_Text label, bool selected)
        {
            var surface = (PrototypeUiSurface)button.targetGraphic;
            surface.topColor = surface.bottomColor = selected ? Pale : Color.white;
            surface.SetVerticesDirty();
            label.color = Green;
            label.fontStyle = selected ? FontStyles.Bold : FontStyles.Normal;
        }
    }
}
