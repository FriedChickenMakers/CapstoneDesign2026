using System;
using UnityEngine;
using UnityEngine.UI;
using CapstoneDesign.Runtime.LocalState;

namespace CapstoneDesign.Runtime
{
    public sealed partial class WeekOneQuestDemo
    {
        enum GuidedSlide { Overview, Preparation, Choice, Cue0, Cue1, Cue2, Practice, Reflection, Help }
        string guidedDraftSession, guidedChoice = "", guidedReflection = "";
        int guidedElapsed, guidedHelpIndex, guidedViewRevision;
        bool guidedResumeClock;
        GuidedSlide guidedSlide, guidedHelpReturn;

        void BindGuidedFocus(ActivitySession session)
        {
            if (guidedDraftSession == session.SessionId) return;
            guidedDraftSession = session.SessionId;
            guidedChoice = session.FocusStepRecords[0].Value ?? "";
            guidedReflection = session.FocusStepRecords[1].Value ?? "";
            guidedElapsed = session.FocusStepRecords[0].ElapsedSeconds;
            guidedResumeClock = false;
            guidedHelpIndex = 0;
            // UI slides do not create domain steps or alter persisted session versions.
            guidedSlide = session.Status == ParticipationState.Selected ? GuidedSlide.Overview : GuidedSlide.Practice;
            guidedHelpReturn = guidedSlide;
        }

        int GuidedElapsed()
        {
            if (focusInteractionSession == guidedDraftSession && focusPracticeClock != null)
                guidedElapsed = Math.Max(guidedElapsed, focusPracticeClock.ElapsedSeconds);
            return guidedElapsed;
        }

        bool SaveGuidedFocusDraft()
        {
            var session = ActiveSession();
            if (!FocusMissionCatalog.IsGuided(session) || guidedDraftSession != session.SessionId
                || session.InstructionStep != 0) return true;
            int elapsed = GuidedElapsed();
            if (string.IsNullOrEmpty(error) && (session.FocusStepRecords[0].Value ?? "") == guidedChoice
                && (session.FocusStepRecords[1].Value ?? "") == guidedReflection
                && session.FocusStepRecords[0].ElapsedSeconds == elapsed) return true;
            return FocusSaved(Service.SaveGuidedFocusDraft(session.SessionId, guidedChoice, guidedReflection, elapsed));
        }

        bool IsGuidedView(string id, int revision) => sessionId == id && screen == "focus-practice"
            && guidedDraftSession == id && guidedViewRevision == revision;

        void BuildGuidedFocusPractice(Transform root, FocusMissionDefinition mission, ActivitySession session)
        {
            BindGuidedFocus(session);
            int revision = ++guidedViewRevision;
            string id = session.SessionId;
            Action<Action> current = action =>
            {
                if (root != null && view == root.gameObject && root.gameObject.activeInHierarchy
                    && gameObject.activeInHierarchy && IsGuidedView(id, revision)) action();
            };
            var ink = FocusInk(mission); var accent = FocusAccent(mission); var muted = FocusMuted(mission);
            root.GetComponent<FocusMobilePageLayout>().FixedContent = true;
            focusPracticeClock = null;
            focusInteractionSession = id;
            focusInteractionStep = 0;
            FigmaMissionUi.Button(root, "나가기", 24, 24, 86, 48, () => current(CloseFocusPractice), Color.clear, ink, 15).name = "Focus.Close";
            FigmaMissionUi.Label(root, FocusPracticeContext(GuidedProgress()), 148, 36, 218, 24, 14, accent,
                false, TextAnchor.MiddleRight).name = "Guided.Progress";
            FigmaMissionUi.Label(root, mission.Code + " · " + (guidedSlide == GuidedSlide.Overview ? "기본 미션" : mission.Title),
                24, 86, 342, 24, 14, accent).name = "Guided.Mission";
            string heading, body, bodyName;
            GuidedSlideText(mission, out heading, out body, out bodyName);
            FigmaMissionUi.Label(root, heading, 24, 122, 342, 42, heading.Length > 14 ? 24 : 27, ink, true).name = "Guided.Title";
            var copy = FigmaMissionUi.Label(root, body, 24, 174, 342, 68, 18, muted);
            copy.name = bodyName; copy.lineSpacing = 1.15f;

            var icon = FigmaMissionUi.Button(root, "", 105, 315, 180, 180,
                () => current(() => AdvanceGuidedSlide(id)), Color.clear, accent);
            icon.name = "Focus.IconAction";
            icon.transition = Selectable.Transition.None;
            icon.gameObject.AddComponent<FocusMissionTapFeedback>().Button = icon;
            FocusCircle(icon.transform, 0, 0, 180, FocusCircleColor(mission));
            AllFocusIcon(icon.transform, mission, 28, 28, 124);
            string hint = guidedSlide == GuidedSlide.Overview ? "아이콘을 눌러 시작"
                : guidedSlide == GuidedSlide.Practice || guidedSlide == GuidedSlide.Reflection ? "아이콘을 눌러 실습 마치기"
                : guidedSlide == GuidedSlide.Cue2 ? "아이콘을 눌러 실습"
                : guidedSlide == GuidedSlide.Choice ? (string.IsNullOrEmpty(guidedChoice) ? "고르지 않고 다음" : "선택한 대로 다음")
                : guidedSlide == GuidedSlide.Help && guidedHelpIndex == GuidedHelp(mission).Length - 1 ? "아이콘을 눌러 돌아가기"
                : "아이콘을 눌러 다음";
            var actionHint = FigmaMissionUi.Label(root, hint, 24, 507, 342, 28, 14, accent, false, TextAnchor.MiddleCenter);
            actionHint.name = "Guided.ActionHint";

            var viewport = FigmaMissionUi.Box(root, "Guided.AnswersViewport", 24, 545, 342, 220, Color.clear);
            viewport.GetComponent<Image>().raycastTarget = true;
            viewport.gameObject.AddComponent<RectMask2D>();
            var content = FigmaMissionUi.Rect(viewport, "Guided.Answers", 0, 0, 342, 1);
            var scroll = viewport.gameObject.AddComponent<ScrollRect>();
            scroll.viewport = viewport; scroll.content = content; scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Clamped; scroll.scrollSensitivity = 30;
            float contentHeight = 1;
            if (guidedSlide == GuidedSlide.Choice)
                contentHeight = BuildGuidedChoices(content, mission, current);
            else if (guidedSlide == GuidedSlide.Practice)
                contentHeight = BuildGuidedClock(root, content, mission, current);
            else if (guidedSlide == GuidedSlide.Reflection)
                contentHeight = BuildGuidedReflection(root, content, mission, current);
            else if (guidedSlide == GuidedSlide.Overview)
            {
                int seconds = mission.Steps[0].RecommendedSeconds;
                FigmaMissionUi.Label(content, "권장 " + (seconds >= 60 ? seconds / 60 + "분" : seconds + "초"),
                    0, 0, 342, 28, 14, muted, false, TextAnchor.MiddleCenter);
                contentHeight = 28;
            }
            var navigation = FigmaMissionUi.Rect(root, "Guided.Navigation", 24, 780, 342, 48);
            if (guidedSlide != GuidedSlide.Overview)
                FigmaMissionUi.Button(navigation, "이전", 0, 0, 88, 48,
                    () => current(() => TryGuidedPrevious()), Color.clear, muted, 15).name = "Guided.Previous";
            if (guidedSlide != GuidedSlide.Overview && guidedSlide != GuidedSlide.Help)
                FigmaMissionUi.Button(navigation, "도움말", 246, 0, 96, 48, () => current(() =>
                {
                    guidedHelpReturn = guidedSlide; guidedHelpIndex = 0;
                    NavigateGuidedSlide(GuidedSlide.Help);
                }), Color.clear, accent, 15).name = "Guided.HelpToggle";
            var layout = root.gameObject.AddComponent<FocusGuidedSlideLayout>();
            layout.Icon = (RectTransform)icon.transform; layout.ActionHint = actionHint.rectTransform;
            layout.Viewport = viewport; layout.Content = content; layout.ContentHeight = contentHeight;
            layout.Navigation = navigation; layout.Notice = notice; layout.Refresh();
        }

        string GuidedProgress()
        {
            if (guidedSlide >= GuidedSlide.Cue0 && guidedSlide <= GuidedSlide.Cue2)
                return "안내 " + ((int)guidedSlide - (int)GuidedSlide.Cue0 + 1) + "/3";
            if (guidedSlide == GuidedSlide.Help) return "도움말 " + (guidedHelpIndex + 1) + "/3";
            return guidedSlide == GuidedSlide.Practice ? "내 속도로 실습" : guidedSlide == GuidedSlide.Reflection ? "돌아보기 · 선택" : "";
        }

        void GuidedSlideText(FocusMissionDefinition mission, out string title, out string body, out string name)
        {
            string code = mission.Code;
            title = mission.Title; body = mission.Overview; name = "Guided.Overview";
            switch (guidedSlide)
            {
                case GuidedSlide.Preparation:
                    title = "준비해요"; body = FocusGuidedPracticeContent.Preparation(code); name = "Guided.Preparation"; break;
                case GuidedSlide.Choice:
                    title = FocusGuidedPracticeContent.ChoiceLabel(code); body = "편한 하나를 골라요."; name = "Guided.ChoiceTitle"; break;
                case GuidedSlide.Cue0: case GuidedSlide.Cue1: case GuidedSlide.Cue2:
                    int cue = (int)guidedSlide - (int)GuidedSlide.Cue0;
                    title = cue == 0 ? "감각을 느껴요" : cue == 1 ? "잠시 살펴봐요" : "천천히 이어가요";
                    body = FocusGuidedPracticeContent.Guidance(code, guidedChoice)[cue]; name = "Guided.Cue." + cue; break;
                case GuidedSlide.Practice:
                    title = FocusGuidedPracticeContent.PracticeTitle(code); body = FocusGuidedPracticeContent.PracticeHint(code); name = "Guided.PracticeHint"; break;
                case GuidedSlide.Reflection:
                    title = "잠깐 돌아보기"; body = FocusGuidedPracticeContent.ReflectionPrompt(code); name = "Guided.ReflectionPrompt"; break;
                case GuidedSlide.Help:
                    title = "괜찮아요"; body = GuidedHelp(mission)[guidedHelpIndex]; name = "Guided.Help"; break;
            }
        }

        static string[] GuidedHelp(FocusMissionDefinition mission) => FocusGuidedPracticeContent.Help(mission.Code).Split('\n');

        float BuildGuidedChoices(Transform parent, FocusMissionDefinition mission, Action<Action> current)
        {
            var choices = mission.Steps[0].Options;
            float width = (342 - (choices.Length - 1) * 6) / choices.Length;
            for (int index = 0; index < choices.Length; index++)
            {
                string option = choices[index];
                bool selected = option == guidedChoice;
                var button = FigmaMissionUi.Button(parent, option, index * (width + 6), 0, width, 52, () => current(() =>
                {
                    guidedChoice = option;
                    if (SaveGuidedFocusDraft()) NavigateGuidedSlide(GuidedSlide.Cue0, false);
                    else ShowFocusPractice(); // Keep the draft choice visible for retry.
                }), selected ? FocusAccent(mission) : Color.white, selected ? Color.white : FocusInk(mission), 15);
                button.name = "Guided.Choice." + option;
            }
            return 52;
        }

        float BuildGuidedClock(Transform root, Transform body, FocusMissionDefinition mission, Action<Action> current)
        {
            var muted = FocusMuted(mission); var accent = FocusAccent(mission);
            var label = FigmaMissionUi.Label(body, "", 0, 0, 342, 28, 14, muted, false, TextAnchor.MiddleCenter);
            label.name = "Focus.TimerStatus";
            var toggle = FigmaMissionUi.Button(body, "집중 시작", 0, 38, 166, 48, () => { }, Color.white, accent, 15);
            toggle.name = "Focus.TimerToggle";
            FigmaMissionUi.Button(body, "돌아보기 · 선택", 176, 38, 166, 48,
                () => current(() => NavigateGuidedSlide(GuidedSlide.Reflection)), Color.clear, accent, 15).name = "Guided.ReflectionToggle";
            focusPracticeClock = root.gameObject.AddComponent<FocusPracticeClock>();
            focusPracticeClock.Label = label; focusPracticeClock.Toggle = toggle;
            focusPracticeClock.RecommendedSeconds = mission.Steps[0].RecommendedSeconds;
            focusPracticeClock.ElapsedSeconds = guidedElapsed;
            focusPracticeClock.Persist = elapsed =>
            {
                if (root == null || view != root.gameObject || screen != "focus-practice") return true;
                guidedElapsed = Math.Max(guidedElapsed, elapsed);
                return SaveGuidedFocusDraft();
            };
            focusPracticeClock.Initialize();
            if (guidedResumeClock) { guidedResumeClock = false; focusPracticeClock.ToggleRunning(); }
            return 86;
        }

        static void StyleGuidedChoice(Button button, bool selected, Color accent, Color ink)
        {
            if (button == null) return;
            button.targetGraphic.color = selected ? accent : Color.white;
            button.GetComponentInChildren<Text>().color = selected ? Color.white : ink;
        }

        float BuildGuidedReflection(Transform page, Transform body, FocusMissionDefinition mission, Action<Action> current)
        {
            var ink = FocusInk(mission); var accent = FocusAccent(mission);
            var options = mission.Steps[1].Options;
            Button[] buttons = new Button[options.Length];
            InputField input = null;
            Action refresh = () => { for (int i = 0; i < buttons.Length; i++) StyleGuidedChoice(buttons[i], guidedReflection == options[i], accent, ink); };
            for (int index = 0; index < options.Length; index++)
            {
                string option = options[index];
                buttons[index] = FigmaMissionUi.Button(body, option, (index % 2) * 174, (index / 2) * 54, 168, 48, () => current(() =>
                {
                    guidedReflection = guidedReflection == option ? "" : option;
                    if (input != null) input.SetTextWithoutNotify(guidedReflection);
                    SaveGuidedFocusDraft(); refresh();
                }), Color.white, ink, 15);
                buttons[index].name = "Guided.Reflection." + option;
            }
            float y = Mathf.CeilToInt(options.Length / 2f) * 54;
            if (mission.Steps[1].OptionalInput)
            {
                input = FigmaMissionUi.Input(body, "짧게 남기기 · 선택", 0, y, 342, 88, 500);
                input.name = "Guided.ReflectionInput"; input.SetTextWithoutNotify(guidedReflection);
                input.onValueChanged.AddListener(value => current(() => { guidedReflection = value; refresh(); }));
                input.onEndEdit.AddListener(value => current(() => SaveGuidedFocusDraft()));
                AttachFocusKeyboard(page, input, "잠깐 돌아보기", FigmaMissionUi.Hex(mission.BackgroundHex), accent);
                y += 100;
            }
            refresh();
            return y;
        }

        void NavigateGuidedSlide(GuidedSlide next, bool save = true)
        {
            GuidedElapsed();
            // A reading page is still reachable after a failed write. In-memory answers
            // survive; no timer runs behind guidance, help or reflection.
            if (focusPracticeClock != null) focusPracticeClock.Pause();
            if (save) SaveGuidedFocusDraft();
            guidedResumeClock = false;
            guidedSlide = next;
            ShowFocusPractice();
        }

        void AdvanceGuidedSlide(string id)
        {
            var mission = FocusMissionCatalog.FindForSession(ActiveSession());
            if (mission == null) return;
            switch (guidedSlide)
            {
                case GuidedSlide.Overview: NavigateGuidedSlide(GuidedSlide.Preparation); break;
                case GuidedSlide.Preparation:
                    NavigateGuidedSlide(mission.Steps[0].Options.Length > 0 ? GuidedSlide.Choice : GuidedSlide.Cue0); break;
                case GuidedSlide.Choice:
                    if (SaveGuidedFocusDraft()) NavigateGuidedSlide(GuidedSlide.Cue0, false); break;
                case GuidedSlide.Cue0: NavigateGuidedSlide(GuidedSlide.Cue1); break;
                case GuidedSlide.Cue1: NavigateGuidedSlide(GuidedSlide.Cue2); break;
                case GuidedSlide.Cue2: StartGuidedFocus(id); break;
                case GuidedSlide.Practice: case GuidedSlide.Reflection: FinishGuidedFocus(id); break;
                case GuidedSlide.Help:
                    if (guidedHelpIndex < GuidedHelp(mission).Length - 1) { guidedHelpIndex++; NavigateGuidedSlide(GuidedSlide.Help); }
                    else NavigateGuidedSlide(guidedHelpReturn);
                    break;
            }
        }

        bool TryGuidedPrevious()
        {
            var mission = FocusMissionCatalog.FindForSession(ActiveSession());
            if (mission == null) return false;
            switch (guidedSlide)
            {
                case GuidedSlide.Overview: CloseFocusPractice(); break;
                case GuidedSlide.Preparation: NavigateGuidedSlide(GuidedSlide.Overview); break;
                case GuidedSlide.Choice: NavigateGuidedSlide(GuidedSlide.Preparation); break;
                case GuidedSlide.Cue0:
                    NavigateGuidedSlide(mission.Steps[0].Options.Length > 0 ? GuidedSlide.Choice : GuidedSlide.Preparation); break;
                case GuidedSlide.Cue1: NavigateGuidedSlide(GuidedSlide.Cue0); break;
                case GuidedSlide.Cue2: NavigateGuidedSlide(GuidedSlide.Cue1); break;
                case GuidedSlide.Practice: NavigateGuidedSlide(GuidedSlide.Cue2); break;
                case GuidedSlide.Reflection: NavigateGuidedSlide(GuidedSlide.Practice); break;
                case GuidedSlide.Help:
                    if (guidedHelpIndex > 0) { guidedHelpIndex--; NavigateGuidedSlide(GuidedSlide.Help); }
                    else NavigateGuidedSlide(guidedHelpReturn);
                    break;
            }
            return true;
        }

        void StartGuidedFocus(string id)
        {
            if (sessionId != id || screen != "focus-practice") return;
            if (!SaveGuidedFocusDraft() || !FocusSaved(Service.StartGuidedFocusSession(id, guidedChoice))) return;
            guidedSlide = GuidedSlide.Practice;
            guidedResumeClock = true;
            ShowFocusPractice();
        }

        void FinishGuidedFocus(string id)
        {
            if (sessionId != id || screen != "focus-practice") return;
            if (!FocusSaved(Service.CompleteGuidedFocusPractice(id, guidedChoice, guidedReflection, GuidedElapsed()))) return;
            if (focusPracticeClock != null) focusPracticeClock.Pause();
            guidedResumeClock = false; focusDirty = false;
            FinishFocusMission();
        }
    }
}
