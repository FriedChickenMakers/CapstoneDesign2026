using System;
using CapstoneDesign.Runtime.LocalState;
using UnityEngine;
using UnityEngine.UI;

namespace CapstoneDesign.Runtime
{
    public sealed partial class WeekOneQuestDemo
    {
        // These are presentation pages within the existing single `light` step.
        // Method selection is optional guidance, never a new saved answer or reward.
        enum LightPage { Overview, Choice, Action, Help }
        string lightGuideSession, lightGuideMethod = "";
        LightPage lightGuidePage, lightHelpReturn;
        int lightGuideRevision;

        static bool UsesM01LightGuide(ActivitySession session)
            => session != null && session.FocusVersion == FocusMissionCatalog.Version
                && FocusMissionCatalog.FindForSession(session)?.Code == "M01";

        void ResetM01LightGuide()
        {
            lightGuideSession = null; lightGuideMethod = "";
            lightGuidePage = lightHelpReturn = LightPage.Overview;
            lightGuideRevision++;
        }

        void BuildM01LightGuide(RectTransform root, FocusMissionDefinition mission, ActivitySession session)
        {
            if (lightGuideSession != session.SessionId)
            {
                ResetM01LightGuide(); lightGuideSession = session.SessionId;
                // An existing unfinished save resumes before the real-world action.
                lightGuidePage = session.Status == ParticipationState.Selected ? LightPage.Overview : LightPage.Choice;
            }
            int revision = ++lightGuideRevision; string id = session.SessionId;
            Func<bool> current = () => root != null && view == root.gameObject && root.gameObject.activeInHierarchy
                && isActiveAndEnabled && screen == "focus-practice" && sessionId == id && lightGuideRevision == revision;
            Action<Action> act = action => { if (current()) action(); };
            bool overview = lightGuidePage == LightPage.Overview;
            bool choice = lightGuidePage == LightPage.Choice;
            bool help = lightGuidePage == LightPage.Help;
            var ink = FigmaMissionUi.Hex("594D37"); var accent = FigmaMissionUi.Hex("866B35");
            var muted = FigmaMissionUi.Hex("756A54");
            var background = FigmaMissionUi.Hex(overview || choice ? "F7F5EC" : help ? "F5F3EB" : "FAF4E5");
            var surface = root.parent.GetComponent<Image>(); if (surface != null) surface.color = background;
            var panel = GetComponent<Image>(); if (panel != null) panel.color = background;
            root.GetComponent<FocusMobilePageLayout>().FixedContent = true;
            focusPracticeClock = null; focusDrawing = null;
            focusInteractionSession = id; focusInteractionStep = overview ? -1 : session.InstructionStep;
            FigmaMissionUi.Button(root, "나가기", 24, 24, 86, 48, () => act(CloseFocusPractice), Color.clear, ink, 15).name = "Focus.Close";
            string context = overview ? "" : choice ? "방법 고르기" : help ? "도움말" : "직접 해보기";
            FigmaMissionUi.Label(root, FocusPracticeContext(context), 148, 36, 218, 24, 14, accent,
                false, TextAnchor.MiddleRight).name = "M01.Context";
            Text heading = null;
            if (overview)
            {
                FigmaMissionUi.Label(root, "추가 미션", 24, 86, 342, 24, 16, accent, false, TextAnchor.UpperCenter).name = "M01.Mission";
                heading = FigmaMissionUi.Label(root, mission.Title, 24, 122, 342, 42, 30, ink, true, TextAnchor.UpperCenter);
                heading.name = "M01.Title";
            }
            string body = overview ? "커튼이나 조명으로\n주변에 빛을 조금 더해요."
                : choice ? "지금 편한 방법 하나를 골라요."
                : help ? "커튼과 조명 중 하나면 돼요.\n눈부시지 않을 만큼만 밝게 해요.\n지금 어렵다면 나갔다 이어해요."
                : lightGuideMethod == "curtain" ? "커튼을 조금 열어\n주변에 빛을 더해요."
                : lightGuideMethod == "lamp" ? "가까운 조명을 켜\n주변에 빛을 더해요."
                : "커튼을 조금 열거나\n조명을 켜요.";
            var copy = FigmaMissionUi.Label(root, body, 24, 174, 342, 68, 22, ink, false, TextAnchor.UpperCenter);
            copy.name = "M01.Body"; copy.lineSpacing = 1.15f;
            var icon = FigmaMissionUi.Button(root, "", 83, 293, 224, 224,
                () => act(() => AdvanceM01LightGuide(id)), Color.clear, accent);
            icon.name = "Focus.IconAction"; icon.transition = Selectable.Transition.None;
            icon.gameObject.AddComponent<FocusMissionTapFeedback>().Button = icon;
            var illustration = FigmaMissionUi.Rect(icon.transform, "Focus.Illustration", 0, 0, 180, 180);
            illustration.anchorMin = illustration.anchorMax = illustration.pivot = new Vector2(.5f, .5f);
            illustration.anchoredPosition = Vector2.zero;
            var art = illustration.gameObject.AddComponent<M01LightIllustration>();
            art.Configure(overview ? M01LightIllustration.Stage.Overview : choice ? M01LightIllustration.Stage.Choice
                : help ? M01LightIllustration.Stage.Help : lightGuideMethod == "curtain" ? M01LightIllustration.Stage.Curtain
                : lightGuideMethod == "lamp" ? M01LightIllustration.Stage.Lamp : M01LightIllustration.Stage.Brighten);
            if (Application.isPlaying && !FigmaMissionUi.ReducedMotion)
                illustration.gameObject.AddComponent<FocusMissionEntrance>();
            string actionHint = overview ? "아이콘을 눌러 " + (session.Status == ParticipationState.Selected ? "시작" : "이어하기")
                : choice ? "고르거나 아이콘을 눌러 다음" : help ? "아이콘을 눌러 돌아가기" : "빛을 더했다면 아이콘을 눌러 완료";
            var hint = FigmaMissionUi.Label(root, actionHint, 24, 507, 342, 28, 14, muted, false, TextAnchor.MiddleCenter);
            hint.name = "M01.ActionHint";
            var answers = FigmaMissionUi.Rect(root, "M01.Choices", 24, 545, 342, 52);
            if (choice)
            {
                AddM01Method(answers, "커튼 열기", "curtain", "M01.Choice.Curtain", 0, act, accent);
                AddM01Method(answers, "조명 켜기", "lamp", "M01.Choice.Lamp", 177, act, accent);
            }
            var navigation = FigmaMissionUi.Rect(root, "M01.Navigation", 24, 780, 342, 48);
            if (!overview)
                FigmaMissionUi.Button(navigation, "이전", 0, 0, 88, 48, () => act(() => TryM01LightBack()),
                    Color.clear, muted, 15).name = "M01.Previous";
            if (!overview && !help)
                FigmaMissionUi.Button(navigation, "도움말", 246, 0, 96, 48, () => act(() =>
                {
                    if (!SaveFocusDraft()) return;
                    lightHelpReturn = lightGuidePage; lightGuidePage = LightPage.Help; ShowFocusPractice();
                }), Color.clear, accent, 15).name = "M01.HelpToggle";
            var layout = root.gameObject.AddComponent<FocusGuidedSlideLayout>();
            layout.Icon = (RectTransform)icon.transform; layout.Illustration = illustration; layout.ActionHint = hint.rectTransform;
            layout.Viewport = answers; layout.Navigation = navigation; layout.Notice = notice;
            layout.Heading = heading; layout.Copy = copy;
            // Equal space on every page keeps the illustration's centre stable.
            layout.MinimumAnswerHeight = 60; layout.Refresh();
        }

        void AddM01Method(Transform root, string label, string method, string name, float x, Action<Action> act, Color accent)
        {
            var button = FigmaMissionUi.Button(root, label, x, 0, 165, 48, () => act(() =>
            {
                lightGuideMethod = method; lightGuidePage = LightPage.Action; ShowFocusPractice();
            }), FigmaMissionUi.Hex(method == lightGuideMethod ? "E9DDBF" : "EEE7D6"), accent, 17);
            button.name = name;
        }

        void AdvanceM01LightGuide(string id)
        {
            var session = ActiveSession();
            if (!UsesM01LightGuide(session) || session.SessionId != id) return;
            if (lightGuidePage == LightPage.Help) lightGuidePage = lightHelpReturn;
            else if (lightGuidePage == LightPage.Overview)
            {
                if (session.Status == ParticipationState.Selected && !FocusSaved(Service.StartSession(id))) return;
                lightGuidePage = LightPage.Choice;
            }
            else if (lightGuidePage == LightPage.Choice) lightGuidePage = LightPage.Action;
            else
            {
                // Preserve the historical light record: no invented answer or extra
                // completion. Existing transactions own growth, coins and retry safety.
                AdvanceFocus(id, session.InstructionStep); return;
            }
            ShowFocusPractice();
        }

        bool TryM01LightBack()
        {
            if (!SaveFocusDraft()) return true;
            if (lightGuidePage == LightPage.Help) lightGuidePage = lightHelpReturn;
            else if (lightGuidePage == LightPage.Action) lightGuidePage = LightPage.Choice;
            else if (lightGuidePage == LightPage.Choice) lightGuidePage = LightPage.Overview;
            else { CloseFocusPractice(); return true; }
            ShowFocusPractice(); return true;
        }
    }
}
