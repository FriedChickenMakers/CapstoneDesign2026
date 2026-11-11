using System;
using System.Linq;
using CapstoneDesign.Runtime.LocalState;
using UnityEngine;
using UnityEngine.UI;

namespace CapstoneDesign.Runtime
{
    public sealed partial class WeekOneQuestDemo
    {
        // Presentation only. A plan's slides still confirm one existing domain step.
        int additionalPlanPage;
        int additionalReviewPage;
        string[] additionalReviewPages = Array.Empty<string>();
        void ResetAdditionalSlidePresentation()
        { additionalPlanPage = additionalReviewPage = 0; additionalReviewPages = Array.Empty<string>(); }

        static float AdditionalAnswerReservation(string code) => code == "M11" ? 208 : code == "M15" ? 174
            : code == "M07" ? 160 : code == "M10" ? 160 : code == "M02" || code == "M12" ? 108 : 100;

        string AdditionalPlanBody() => additionalPlanPage == 0 ? "해볼 작은 일을 골라요.\n직접 적으려면 다음으로 가요."
            : additionalPlanPage == 1 ? "편한 시간대를 골라요.\n정확한 시각은 정하지 않아도 돼요."
            : "직접 정할 계획이 있나요?\n고른 내용이 있다면 넘어가도 돼요.";

        float BuildAdditionalPlan(RectTransform root, Transform parent, FocusMissionDefinition mission,
            ActivitySession session, Button icon, Text hint, Action<Action> act, Func<bool> current)
        {
            int step = session.InstructionStep;
            var record = session.FocusStepRecords[step];
            string activity = record.PlanActivity ?? "", period = record.PlanTimePeriod ?? "", note = record.PlanNote ?? "";
            if (activity.Length == 0 && period.Length == 0 && note.Length == 0 && !string.IsNullOrEmpty(record.Value)) note = record.Value;
            var accent = FocusAccent(mission);
            Action refresh = () =>
            {
                focusAnswer = FocusMissionAdjustmentPolicy.ComposePlan(activity, period, note);
                icon.interactable = additionalPlanPage < 2 || !string.IsNullOrWhiteSpace(focusAnswer);
                hint.text = BasicFocusActionHint(session, mission.Steps[step], step, 0);
            };
            Func<bool> save = () =>
            {
                if (!current()) return false;
                refresh();
                bool saved = FocusSaved(Service.SaveFocusPlan(session.SessionId, step, activity, period, note));
                if (saved) focusDirty = false;
                return saved;
            };
            focusPlanPersist = () => !focusDirty || save();
            if (additionalPlanPage < 2)
            {
                bool activityPage = additionalPlanPage == 0;
                var options = activityPage ? FocusMissionAdjustmentPolicy.PlanActivityOptions : FocusMissionAdjustmentPolicy.PlanTimePeriodOptions;
                var buttons = new Button[options.Length];
                for (int i = 0; i < options.Length; i++)
                {
                    string option = options[i];
                    buttons[i] = FigmaMissionUi.Button(parent, option, i % 2 * 174, i / 2 * 50, 168, 44, () => act(() =>
                    {
                        if (activityPage) { activity = option; note = ""; } else period = option;
                        focusDirty = true; refresh();
                        for (int n = 0; n < buttons.Length; n++)
                            StyleGuidedChoice(buttons[n], options[n] == (activityPage ? activity : period), accent, FocusInk(mission));
                        save(); // Keep the draft on this slide if persistence fails.
                    }), Color.white, FocusInk(mission), 15);
                    buttons[i].name = (activityPage ? "Focus.PlanActivity." : "Focus.PlanPeriod.") + i;
                    StyleGuidedChoice(buttons[i], option == (activityPage ? activity : period), accent, FocusInk(mission));
                }
                refresh(); return Mathf.Ceil(options.Length / 2f) * 50;
            }
            var input = FigmaMissionUi.Input(parent, "예: 저녁에 차 마시기 · 선택", 0, 0, 342, 88, 450);
            input.name = "Focus.PlanNote"; input.SetTextWithoutNotify(note);
            input.onValueChanged.AddListener(value => act(() => { note = value; focusDirty = true; refresh(); }));
            input.onEndEdit.AddListener(value => act(() => save()));
            AttachFocusKeyboard(root, input, "작은 계획", FigmaMissionUi.Hex(mission.BackgroundHex), accent);
            refresh(); return 100;
        }

        float BuildAdditionalDrawing(Transform parent, FocusMissionDefinition mission, Button icon, Text hint,
            Action<Action> act, Func<bool> current)
        {
            var card = FigmaMissionUi.Box(parent, "Focus.DrawingPaper", 0, 0, 342, 156, Color.white, 12);
            card.GetComponent<Image>().raycastTarget = true;
            var rect = FigmaMissionUi.Rect(card, "Focus.DrawingSurface", 8, 8, 326, 140);
            var drawing = rect.gameObject.AddComponent<FocusDrawingSurface>();
            drawing.color = FocusAccent(mission); drawing.Load(focusAnswer); focusDrawing = drawing;
            icon.interactable = FocusDrawingData.Decode(focusAnswer).Count > 0;
            drawing.Changed = value => act(() =>
            {
                focusAnswer = value; focusDirty = true;
                icon.interactable = FocusDrawingData.Decode(value).Count > 0;
                hint.text = icon.interactable ? "아이콘을 눌러 그림 확인" : "아래에 선을 하나 그려요";
            });
            drawing.StrokeEnded = () => act(() =>
            {
                if (FocusDrawingData.Decode(focusAnswer).Count > 0 && !MarkFocusPracticeStarted()) return;
                SaveFocusDraft();
            });
            // A modal blocks physical raycasts; this also blocks a retained drag callback.
            drawing.CanInteract = current;
            FigmaMissionUi.Button(parent, "되돌리기", 0, 164, 168, 44, () => act(drawing.Undo), Color.white, FocusAccent(mission), 15).name = "Focus.DrawingUndo";
            FigmaMissionUi.Button(parent, "모두 지우기", 174, 164, 168, 44, () => act(drawing.Clear), Color.clear, FocusAccent(mission), 15).name = "Focus.DrawingClear";
            return 208;
        }

        float BuildAdditionalReview(RectTransform root, Transform parent, ActivitySession session, int step, FocusStepRecord[] records,
            Action<Action> act, Color ink, Color accent)
        {
            if (FocusMissionCatalog.FindForSession(session).Code != "M11")
            {
                if (records.Length == 0) return 1;
                var record = records[basicReviewPage];
                var text = FigmaMissionUi.Label(parent, "", 12, 0, 318, 96, 18, ink, true);
                text.name = "Basic.ReviewValue";
                // Measured pages preserve the entire user's text without making
                // the mission page scroll, including unusually long newlines.
                var pages = new System.Collections.Generic.List<string>();
                string remaining = record.Value;
                while (remaining.Length > 0)
                {
                    var boundaries = System.Globalization.StringInfo.ParseCombiningCharacters(remaining)
                        .Concat(new[] { remaining.Length }).ToArray();
                    int elements = Math.Min(90, boundaries.Length - 1);
                    int length = boundaries[elements];
                    text.text = remaining.Substring(0, length);
                    while (elements > 1 && text.preferredHeight > 92)
                    { length = boundaries[--elements]; text.text = remaining.Substring(0, length); }
                    pages.Add(text.text); remaining = remaining.Substring(length);
                }
                additionalReviewPages = pages.ToArray();
                additionalReviewPage = Math.Min(additionalReviewPage, Math.Max(0, pages.Count - 1));
                text.text = pages.Count == 0 ? "" : pages[additionalReviewPage];
                FigmaMissionUi.Button(parent, "수정", 114, 104, 114, 44, () => act(() =>
                {
                    if (!SaveFocusDraft()) return;
                    int target = session.FocusStepRecords.FindIndex(item => item.Key == record.Key);
                    if (FocusSaved(Service.MoveFocusStep(session.SessionId, target))) ShowFocusPractice();
                }), Color.white, accent, 15).name = "Basic.ReviewEdit";
                return 148;
            }
            // Review the user's drawing in the central action, not an unrelated
            // example above a second drawing. The read-only preview passes taps through.
            var illustration = FigmaMissionUi.Find<RectTransform>(root, "Focus.Illustration");
            var example = illustration.GetComponentInChildren<AdditionalMissionIllustration>();
            if (example != null) example.enabled = false;
            var paper = FigmaMissionUi.Box(illustration, "Focus.DrawingReviewPaper", 8, 48, 164, 84, Color.white, 10);
            paper.GetComponent<Image>().raycastTarget = false;
            // The saved points are normalized: preserve the drawing canvas ratio.
            var drawing = FigmaMissionUi.Rect(paper, "Focus.DrawingReview", 8, 8, 148, 148 * 140f / 326f).gameObject.AddComponent<FocusDrawingSurface>();
            drawing.ReadOnly = true; drawing.raycastTarget = false; drawing.color = accent;
            drawing.Load(records.FirstOrDefault()?.Value);
            FigmaMissionUi.Button(parent, "더 그리기", 114, 0, 114, 44, () => act(() =>
            {
                if (SaveFocusDraft() && FocusSaved(Service.MoveFocusStep(session.SessionId, 0))) ShowFocusPractice();
            }), Color.white, accent, 15).name = "Basic.ReviewEdit";
            return 44;
        }

        float BuildAdditionalWalking(Transform parent, FocusMissionDefinition mission, ActivitySession session,
            Button icon, Action<Action> act, Func<bool> current)
        {
            string id = session.SessionId;
            bool ready = true;
            if (session.WalkingGoalSteps <= 0)
            {
                var target = WalkingPersonalization.Evaluate(ReadWalkingHealth()?.healthConnect?.personalization, Service.CurrentUtc, Service.CurrentTimeZone);
                ready = FocusSaved(Service.EnsureFocusWalkingTarget(id, target)); session = ActiveSession();
                if (ready) RequestStepSyncSoon();
            }
            icon.interactable = ready;
            var count = FigmaMissionUi.Label(parent, "", 0, 0, 342, 28, 16, FocusInk(mission), true, TextAnchor.MiddleCenter);
            count.name = "Focus.WalkingCount";
            var counter = count.gameObject.AddComponent<FocusWalkingCounter>(); counter.Label = count;
            counter.Read = () =>
            {
                var saved = Service.Snapshot.Sessions.FirstOrDefault(s => s.SessionId == id);
                if (!ready || saved == null) return "목표를 저장하지 못했어요";
                return "목표 " + saved.WalkingGoalSteps.ToString("N0") + "걸음 · " +
                    (string.IsNullOrEmpty(saved.StepSource) ? "동기화 대기" : saved.RealStepHighWater.ToString("N0") + "걸음");
            };
            counter.Refresh();
            FigmaMissionUi.Button(parent, "걸음 새로고침", 58, 34, 226, 44, () => act(() =>
            {
                if (!SaveFocusDraft()) return;
                if (!IsMissionDebugActive) { AndroidPlatformBridge.RefreshHealthData(); RequestStepSyncSoon(); TickStepSync(); }
                ShowFocusPractice();
            }), Color.white, FocusAccent(mission), 15).name = "Focus.RefreshSteps";
            return 78;
        }
    }
}
