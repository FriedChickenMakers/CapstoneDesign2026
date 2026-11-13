using System;
using UnityEngine;
using UnityEngine.UI;

namespace CapstoneDesign.Runtime
{
    // Presentation copy only: existing P01 choices, domain steps and receipts stay intact.
    public static class P01BreathGuide
    {
        public static string Title(P01BreathIllustration.Stage stage, string focus, int helpPage = 0)
            => stage == P01BreathIllustration.Stage.Overview ? "잠깐 멈춰보기" : "";

        public static string Body(P01BreathIllustration.Stage stage, string focus, int helpPage = 0)
        {
            switch (stage)
            {
                case P01BreathIllustration.Stage.Overview: return "숨이 느껴지는 한 곳에\n잠시 주의를 두는 실습이에요.";
                case P01BreathIllustration.Stage.Preparation: return "편하게 앉거나 누워요.";
                case P01BreathIllustration.Stage.Choice: return "숨을 느낄 곳을 골라요.";
                case P01BreathIllustration.Stage.Notice: return focus == "코" ? "코끝의 스침과 온도를 느껴요."
                    : focus == "가슴" ? "가슴의 움직임을 느껴요."
                    : focus == "배" ? "배의 움직임을 느껴요."
                    : "코·가슴·배 중 한 곳을 느껴요.";
                case P01BreathIllustration.Stage.Observe: return "숨은 평소처럼 자연스럽게.";
                case P01BreathIllustration.Stage.Return: return "딴생각을 알아차리면,\n숨의 느낌으로 돌아와요.";
                case P01BreathIllustration.Stage.Practice: return "편한 만큼 숨을 느껴요.";
                case P01BreathIllustration.Stage.Reflection: return "느낀 점을 남겨요.\n건너뛰어도 괜찮아요.";
                default: return "숨은 평소처럼.\n느낄 곳은 바꿔도 돼요.\n불편하면 눈을 뜨거나 쉬어요.";
            }
        }
    }

    public sealed partial class WeekOneQuestDemo
    {
        P01BreathIllustration.Stage P01Stage()
        {
            switch (guidedSlide)
            {
                case GuidedSlide.Overview: return P01BreathIllustration.Stage.Overview;
                case GuidedSlide.Preparation: return P01BreathIllustration.Stage.Preparation;
                case GuidedSlide.Choice: return P01BreathIllustration.Stage.Choice;
                case GuidedSlide.Cue0: return P01BreathIllustration.Stage.Notice;
                case GuidedSlide.Cue1: return P01BreathIllustration.Stage.Observe;
                case GuidedSlide.Cue2: return P01BreathIllustration.Stage.Return;
                case GuidedSlide.Practice: return P01BreathIllustration.Stage.Practice;
                case GuidedSlide.Reflection: return P01BreathIllustration.Stage.Reflection;
                default: return P01BreathIllustration.Stage.Help;
            }
        }

        void ApplyP01BreathTheme(Transform root)
        {
            Color background = FigmaMissionUi.Hex(guidedSlide <= GuidedSlide.Choice ? "F4F7EF"
                : guidedSlide == GuidedSlide.Reflection || guidedSlide == GuidedSlide.Help ? "F7F5EE" : "EDF5F0");
            var surface = root.parent.GetComponent<Image>(); if (surface != null) surface.color = background;
            var panel = GetComponent<Image>(); if (panel != null) panel.color = background;
            // Only the numbered guidance has progress marks; help is a single page.
            int active = guidedSlide >= GuidedSlide.Cue0 && guidedSlide <= GuidedSlide.Cue2
                ? (int)guidedSlide - (int)GuidedSlide.Cue0 : -1;
            if (active < 0) return;
            for (int i = 0; i < 3; i++)
                FigmaMissionUi.Box(root, "P01.Progress." + i, 298 + i * 24, 64, 18, 3,
                    FigmaMissionUi.Hex(i <= active ? "527B68" : "D7E3D8"), 1.5f);
        }

        Button BuildP01BreathIcon(Transform root, Action action, out RectTransform illustration)
        {
            var button = FigmaMissionUi.Button(root, "", 83, 293, 224, 224, action, Color.clear);
            button.name = "Focus.IconAction"; button.transition = Selectable.Transition.None;
            button.gameObject.AddComponent<FocusMissionTapFeedback>().Button = button;
            illustration = FigmaMissionUi.Rect(button.transform, "Focus.Illustration", 0, 0, 180, 180);
            illustration.anchorMin = illustration.anchorMax = illustration.pivot = new Vector2(.5f, .5f);
            illustration.anchoredPosition = Vector2.zero;
            var artRect = FigmaMissionUi.Rect(illustration, "P01.BreathIllustration", 0, 0, 180, 180);
            var art = artRect.gameObject.AddComponent<P01BreathIllustration>();
            art.Configure(P01Stage(), guidedChoice, guidedHelpIndex);
            if (Application.isPlaying && !FigmaMissionUi.ReducedMotion)
                illustration.gameObject.AddComponent<FocusMissionEntrance>();
            return button;
        }
    }
}
