using System;
using System.Collections.Generic;
using System.Linq;
using CapstoneDesign.Runtime.LocalState;
using UnityEngine;
using UnityEngine.UI;

namespace CapstoneDesign.Runtime
{
    public sealed partial class WeekOneQuestDemo
    {
        sealed class DailyEmotionLocalDraft
        {
            public string Mood = "", Note = "";
            public bool Dirty, Expanded;
        }

        static readonly string[] DailyEmotionFirstChoices =
            { "기쁨", "편안함", "뿌듯함", "무덤덤함", "슬픔", "불안함", "답답함", "화남" };
        readonly Dictionary<string, DailyEmotionLocalDraft> dailyEmotionDrafts = new Dictionary<string, DailyEmotionLocalDraft>();
        readonly List<Button> dailyEmotionChoiceButtons = new List<Button>();
        GardenStateService dailyEmotionDraftOwner;
        string dailyEmotionDraftId;
        DailyEmotionLocalDraft dailyEmotionDraft;
        InputField dailyEmotionNoteInput;
        Text dailyEmotionStatus;
        Button dailyEmotionSaveButton;
        bool dailyEmotionCompleting;
        float dailyEmotionScroll;

        static string DailyEmotionRewardSummary(DailyEmotionRecord record)
        {
            if (record == null || record.RewardVersion == 0) return "오늘의 감정을 남겼어요.";
            string memo = string.IsNullOrEmpty(record.MemoId) ? "기록을 저장했어요" : "메모 1장을 남겼어요";
            return memo + (record.RewardAmount > 0 ? " · 코인 +" + record.RewardAmount : "");
        }

        FocusMissionHomeLayout.Card BuildDailyEmotionCard(Transform parent, GardenState state, float y)
        {
            const float height = 136;
            var record = DailyEmotionPolicy.Current(state);
            bool completed = record?.IsCompleted == true;
            bool available = DailyEmotionPolicy.Availability(state) == null;
            var card = FigmaMissionUi.Box(parent, "DailyEmotion.Card", 24, y, 342, height,
                available || completed ? FigmaMissionUi.Soft : Color.white, 20);
            var title = FigmaMissionUi.Label(card, "감정 기록", 16, 12, 310, 28, 18, null, true);
            string description = completed ? DailyEmotionRewardSummary(record)
                : available ? "기억 잎 1개 · 완료 보상 100코인"
                : "모든 미션 완료 후 열려요.";
            var detail = FigmaMissionUi.Label(card, description, 16, 42, 310, 24, 14, FigmaMissionUi.Muted);
            string id = record?.Id;
            var frame = activityFrame;
            var action = FigmaMissionUi.Button(card, completed ? "기록 보기" : "감정 기록하기", 16, 76, 310, 44, () =>
            {
                if (frame == null || frame != activityFrame || !frame.activeInHierarchy) return;
                ShowDailyEmotion(id);
            }, completed || available ? FigmaMissionUi.Green : FigmaMissionUi.Soft,
                completed || available ? Color.white : FigmaMissionUi.Muted, 16);
            action.name = "DailyEmotion.CardAction";
            action.interactable = completed || available;
            return new FocusMissionHomeLayout.Card { Root=card, Title=title, Detail=detail, Action=action, TitleSize=18 };
        }

        public void ShowDailyEmotion(string id = null)
        {
            if (!opened || IsMissionDebugActive || !SaveActivePracticeDraft()) return;
            if (!RefreshCycle()) return;
            if (id == null)
            {
                var state = Service.Snapshot;
                var current = DailyEmotionPolicy.Current(state);
                if (current == null)
                {
                    if (!RequireMissionPlant()) return;
                    if (!Service.BeginDailyEmotion(state.LastCycleId))
                    {
                        string failure = Service.LastError;
                        ShowHome();
                        SetDailyEmotionError(failure);
                        return;
                    }
                    current = DailyEmotionPolicy.Current(Service.Snapshot);
                }
                id = current?.Id;
            }
            var record = string.IsNullOrEmpty(id) ? null : Service.GetDailyEmotion(id);
            if (record == null) { ShowDailyEmotionHistory(); return; }
            if (!record.IsCompleted && !Service.IsDailyEmotionExpired(record.Id) && !RequireMissionPlant()) return;
            if (dailyEmotionDraftId != record.Id) dailyEmotionScroll = 0;
            BindDailyEmotionDraft(record);
            RenderDailyEmotion(record);
        }

        void BindDailyEmotionDraft(DailyEmotionRecord record)
        {
            if (!ReferenceEquals(dailyEmotionDraftOwner, Service))
            {
                dailyEmotionDrafts.Clear();
                dailyEmotionDraftOwner = Service;
            }
            if (!dailyEmotionDrafts.TryGetValue(record.Id, out var draft))
            {
                draft = new DailyEmotionLocalDraft { Mood = record.Mood ?? "", Note = record.Note ?? "" };
                dailyEmotionDrafts.Add(record.Id, draft);
            }
            // Completed records are immutable. Always display the committed value.
            if (record.IsCompleted)
            {
                draft.Mood = record.Mood ?? "";
                draft.Note = record.Note ?? "";
                draft.Dirty = false;
            }
            dailyEmotionDraftId = record.Id;
            dailyEmotionDraft = draft;
        }

        bool DailyEmotionCanEdit(string id)
        {
            if (!opened || IsMissionDebugActive || string.IsNullOrEmpty(id)) return false;
            var record = Service.GetDailyEmotion(id);
            return record != null && !record.IsCompleted && !Service.IsDailyEmotionExpired(id)
                && DailyEmotionPolicy.Availability(Service.Snapshot) == null;
        }

        bool DailyEmotionPageIsCurrent(GameObject frame, string id) => this != null && opened
            && isActiveAndEnabled && gameObject.activeInHierarchy && frame != null && frame.activeInHierarchy
            && activityFrame == frame && screen == "daily-emotion" && dailyEmotionDraftId == id;

        void RenderDailyEmotion(DailyEmotionRecord record)
        {
            bool editable = DailyEmotionCanEdit(record.Id);
            dailyEmotionCompleting = false;
            dailyEmotionNoteInput = null;
            dailyEmotionSaveButton = null;
            dailyEmotionChoiceButtons.Clear();
            var root = BeginFigmaPage("daily-emotion", () => ShowDailyEmotion(record.Id), FigmaMissionUi.Background);
            var frame = activityFrame;
            if (notice != null) notice.name = "DailyEmotion.Error";
            FigmaHeader(root, "감정 기록", record.CycleId.Replace('-', '.') + " · 나의 마음 돌아보기");
            var body = FigmaMissionUi.Rect(root, "DailyEmotion.Body", 24, 146, 342, 1);
            dailyEmotionStatus = FigmaMissionUi.Label(body, "", 0, 0, 342, 64, 15, FigmaMissionUi.Muted);
            dailyEmotionStatus.name = "DailyEmotion.Status";
            float y = 72;
            if (editable)
            {
                y += DailyEmotionText(body, "가까운 감정을 골라요", y, 18, FigmaMissionUi.Ink, true) + 8;
                var emotions = dailyEmotionDraft.Expanded ? FocusEmotions : DailyEmotionFirstChoices;
                for (int i = 0; i < emotions.Length; i++)
                {
                    string emotion = emotions[i];
                    var button = FigmaMissionUi.Button(body, emotion, i % 4 * 88, y + i / 4 * 54, 78, 48, () =>
                    {
                        if (!DailyEmotionPageIsCurrent(frame, record.Id)) return;
                        if (!DailyEmotionCanEdit(record.Id)) { RefreshDailyEmotionUiDate(); return; }
                        dailyEmotionDraft.Mood = dailyEmotionDraft.Mood == emotion ? "" : emotion;
                        dailyEmotionDraft.Dirty = true;
                        SaveDailyEmotionUiDraft();
                        RefreshDailyEmotionUiDate();
                    }, Color.white, FigmaMissionUi.Ink, 14);
                    button.name = "DailyEmotion.Emotion." + emotion;
                    dailyEmotionChoiceButtons.Add(button);
                }
                y += Mathf.CeilToInt(emotions.Length / 4f) * 54;
                var unknown = FigmaMissionUi.Button(body, "잘 모르겠어요", 0, y, 342, 44, () =>
                {
                    if (!DailyEmotionPageIsCurrent(frame, record.Id)) return;
                    if (!DailyEmotionCanEdit(record.Id)) { RefreshDailyEmotionUiDate(); return; }
                    dailyEmotionDraft.Mood = dailyEmotionDraft.Mood == "잘 모르겠어요" ? "" : "잘 모르겠어요";
                    dailyEmotionDraft.Dirty = true;
                    SaveDailyEmotionUiDraft();
                    RefreshDailyEmotionUiDate();
                }, Color.white, FigmaMissionUi.Ink, 14);
                unknown.name = "DailyEmotion.Emotion.잘 모르겠어요";
                dailyEmotionChoiceButtons.Add(unknown);
                y += 50;
                FigmaMissionUi.Button(body, dailyEmotionDraft.Expanded ? "감정 접기" : "감정 더 보기", 0, y, 342, 44, () =>
                {
                    if (!DailyEmotionPageIsCurrent(frame, record.Id)) return;
                    SaveDailyEmotionUiDraft(); // Reading more choices must not depend on storage success.
                    CaptureDailyEmotionScroll(root);
                    dailyEmotionDraft.Expanded = !dailyEmotionDraft.Expanded;
                    RenderDailyEmotion(Service.GetDailyEmotion(record.Id) ?? record);
                }, Color.clear, FigmaMissionUi.Green, 14).name = "DailyEmotion.MoreEmotions";
                y += 58;
                y += DailyEmotionText(body, "한 줄 메모 · 선택", y, 18, FigmaMissionUi.Ink, true) + 8;
                dailyEmotionNoteInput = FigmaMissionUi.Input(body, "지금 떠오르는 생각을 적어보세요.", 0, y, 342, 140, 500);
                dailyEmotionNoteInput.name = "DailyEmotion.Note";
                dailyEmotionNoteInput.SetTextWithoutNotify(dailyEmotionDraft.Note);
                dailyEmotionNoteInput.onValueChanged.AddListener(value =>
                {
                    if (!DailyEmotionPageIsCurrent(frame, record.Id)) return;
                    dailyEmotionDraft.Note = value;
                    dailyEmotionDraft.Dirty = true;
                    RefreshDailyEmotionUiDate();
                });
                dailyEmotionNoteInput.onEndEdit.AddListener(value =>
                {
                    if (DailyEmotionPageIsCurrent(frame, record.Id)) SaveDailyEmotionUiDraft();
                });
                var keyboard = root.gameObject.AddComponent<FocusMissionKeyboardAvoidance>();
                keyboard.Input = dailyEmotionNoteInput;
                keyboard.Title = "한 줄 메모";
                keyboard.Background = FigmaMissionUi.Background;
                keyboard.Accent = FigmaMissionUi.Green;
                keyboard.Save = () => SaveDailyEmotionUiDraft();
                y += 152;
                y += DailyEmotionText(body, "감정이나 메모 중 하나만 남겨도 돼요.\n완료하면 기억 잎에 기록이 담기고 100코인을 받아요.\n다음 날 오전 4시까지 작성할 수 있어요.", y, 14, FigmaMissionUi.Muted);
                dailyEmotionSaveButton = FigmaMissionUi.Button(root, "메모 남기고 완료", 100, 730, 266, 56,
                    () => CompleteDailyEmotionUi(frame, record.Id));
                dailyEmotionSaveButton.name = "DailyEmotion.Save";
                FigmaMissionUi.Button(root, "뒤로", 24, 730, 64, 56, () => TryDailyEmotionBack(), Color.white,
                    FigmaMissionUi.Green, 15).name = "DailyEmotion.Back";
            }
            else
            {
                y += DailyEmotionText(body, "그때의 감정", y, 15, FigmaMissionUi.Muted) + 8;
                y += DailyEmotionText(body, string.IsNullOrEmpty(dailyEmotionDraft.Mood) ? "선택한 감정이 없어요." : dailyEmotionDraft.Mood,
                    y, 22, FigmaMissionUi.Green, true, "DailyEmotion.ReadOnlyMood") + 24;
                y += DailyEmotionText(body, "남긴 메모", y, 15, FigmaMissionUi.Muted) + 8;
                y += DailyEmotionText(body, string.IsNullOrWhiteSpace(dailyEmotionDraft.Note) ? "남긴 메모가 없어요." : dailyEmotionDraft.Note,
                    y, 17, FigmaMissionUi.Ink, name: "DailyEmotion.ReadOnlyNote") + 16;
                FigmaMissionUi.Button(root, "미션으로", 24, 730, 166, 56, ShowHome, Color.white,
                    FigmaMissionUi.Green, 15).name = "DailyEmotion.Back";
                FigmaMissionUi.Button(root, "기록 목록", 200, 730, 166, 56, ShowDailyEmotionHistory)
                    .name = "DailyEmotion.History";
            }
            body.sizeDelta = new Vector2(342, y + 12);
            RefreshDailyEmotionUiDate();
            FocusEnter(root);
            var scroll = root.GetComponentInChildren<ScrollRect>();
            if (scroll != null)
                scroll.content.anchoredPosition = new Vector2(0, Mathf.Clamp(dailyEmotionScroll, 0,
                    Mathf.Max(0, scroll.content.rect.height - scroll.viewport.rect.height)));
        }

        static float DailyEmotionText(Transform parent, string value, float y, int size, Color color,
            bool bold = false, string name = null)
        {
            var label = FigmaMissionUi.Label(parent, value, 0, y, 342, 24, size, color, bold);
            label.lineSpacing = 1.15f;
            float height = Mathf.Max(24, label.preferredHeight + 4);
            label.rectTransform.sizeDelta = new Vector2(342, height);
            if (name != null) label.name = name;
            return height;
        }

        void CaptureDailyEmotionScroll(Transform root)
        {
            var scroll = root.GetComponentInChildren<ScrollRect>();
            dailyEmotionScroll = scroll == null ? 0 : scroll.content.anchoredPosition.y;
        }

        void SetDailyEmotionError(string message)
        {
            error = message;
            if (notice == null) return;
            notice.gameObject.SetActive(!string.IsNullOrEmpty(message));
            notice.text = string.IsNullOrEmpty(message) ? "" : "저장하지 못했어요. 입력은 그대로 남아 있어요.\n다시 시도해 주세요.";
            notice.transform.SetAsLastSibling();
        }

        public bool SaveDailyEmotionUiDraft()
        {
            if (!opened || screen != "daily-emotion" || dailyEmotionDraft == null || dailyEmotionCompleting
                || !ReferenceEquals(dailyEmotionDraftOwner, Service)) return true;
            if (dailyEmotionNoteInput != null) dailyEmotionDraft.Note = dailyEmotionNoteInput.text;
            if (!DailyEmotionCanEdit(dailyEmotionDraftId))
            {
                // The deadline cannot trap Back. Keep unsaved text in this in-memory
                // draft, bound to its old record ID; never move it into a new day.
                SetDailyEmotionError(null);
                RefreshDailyEmotionUiDate();
                return true;
            }
            if (!dailyEmotionDraft.Dirty) return true;
            bool saved = Service.SaveDailyEmotionDraft(dailyEmotionDraftId, dailyEmotionDraft.Mood, dailyEmotionDraft.Note, 0);
            if (saved) { dailyEmotionDraft.Dirty = false; SetDailyEmotionError(null); return true; }
            if (Service.IsDailyEmotionExpired(dailyEmotionDraftId))
            {
                SetDailyEmotionError(null);
                RefreshDailyEmotionUiDate();
                return true;
            }
            SetDailyEmotionError(Service.LastError);
            return false;
        }

        public void RefreshDailyEmotionUiDate()
        {
            if (!opened || screen != "daily-emotion" || dailyEmotionDraft == null
                || !ReferenceEquals(dailyEmotionDraftOwner, Service)) return;
            var record = Service.GetDailyEmotion(dailyEmotionDraftId);
            bool editable = DailyEmotionCanEdit(dailyEmotionDraftId);
            foreach (var button in dailyEmotionChoiceButtons)
            {
                if (button == null) continue;
                bool selected = button.name == "DailyEmotion.Emotion." + dailyEmotionDraft.Mood;
                button.targetGraphic.color = selected ? FigmaMissionUi.Green : Color.white;
                button.GetComponentInChildren<Text>().color = selected ? Color.white : FigmaMissionUi.Ink;
                button.interactable = editable && !dailyEmotionCompleting;
            }
            if (dailyEmotionNoteInput != null)
            {
                dailyEmotionNoteInput.readOnly = !editable || dailyEmotionCompleting;
                dailyEmotionNoteInput.interactable = editable && !dailyEmotionCompleting;
            }
            if (dailyEmotionSaveButton != null)
                dailyEmotionSaveButton.interactable = editable && !dailyEmotionCompleting
                    && (!string.IsNullOrWhiteSpace(dailyEmotionDraft.Mood) || !string.IsNullOrWhiteSpace(dailyEmotionDraft.Note));
            if (dailyEmotionStatus != null)
                dailyEmotionStatus.text = record?.IsCompleted == true ? DailyEmotionRewardSummary(record)
                    : editable ? "감정 또는 메모를 남겨요." + (string.IsNullOrWhiteSpace(dailyEmotionDraft.Mood)
                        ? "" : "\n선택한 감정 · " + dailyEmotionDraft.Mood)
                    : dailyEmotionDraft.Dirty ? "작성 시간이 지났어요. 저장하지 못한 입력은\n이 화면에만 남아 있어요."
                    : "작성 시간이 지났어요.\n지난 기록은 읽기만 할 수 있어요.";
        }

        void CompleteDailyEmotionUi(GameObject frame, string id)
        {
            if (!DailyEmotionPageIsCurrent(frame, id) || dailyEmotionCompleting) return;
            if (!GardenPlantVisuals.HasCurrentPlant(Service.Snapshot))
            {
                if (SaveDailyEmotionUiDraft()) RequireMissionPlant();
                return;
            }
            if (!DailyEmotionCanEdit(id)) { RefreshDailyEmotionUiDate(); return; }
            if (dailyEmotionNoteInput != null) dailyEmotionDraft.Note = dailyEmotionNoteInput.text;
            if (string.IsNullOrWhiteSpace(dailyEmotionDraft.Mood) && string.IsNullOrWhiteSpace(dailyEmotionDraft.Note)) return;
            dailyEmotionCompleting = true;
            if (!Service.CompleteDailyEmotion(id, dailyEmotionDraft.Mood, dailyEmotionDraft.Note))
            {
                dailyEmotionCompleting = false;
                if (Service.IsDailyEmotionExpired(id)) SetDailyEmotionError(null);
                else SetDailyEmotionError(Service.LastError);
                RefreshDailyEmotionUiDate();
                return;
            }
            BindDailyEmotionDraft(Service.GetDailyEmotion(id));
            SetDailyEmotionError(null);
            RefreshDailyEmotionUiDate();
            dailyEmotionCompleting = false;
            UpdateGarden();
            if (DailyEmotionPageIsCurrent(frame, id)) ShowHome();
        }

        public bool TryDailyEmotionBack()
        {
            if (screen != "daily-emotion") return false;
            if (!SaveDailyEmotionUiDraft()) return true;
            bool fromHistory = pageHistory.Count >= 2 && pageHistory[pageHistory.Count - 2].Key == "daily-emotion-history";
            if (fromHistory) ShowDailyEmotionHistory(); else ShowHome();
            return true;
        }

        public void ShowDailyEmotionHistory()
        {
            ShowActivityJournal();
        }

        public void ResetDailyEmotionUiDraft()
        {
            dailyEmotionDrafts.Clear();
            dailyEmotionChoiceButtons.Clear();
            dailyEmotionDraftOwner = null;
            dailyEmotionDraftId = null;
            dailyEmotionDraft = null;
            dailyEmotionNoteInput = null;
            dailyEmotionStatus = null;
            dailyEmotionSaveButton = null;
            dailyEmotionCompleting = false;
            dailyEmotionScroll = 0;
        }
    }
}
