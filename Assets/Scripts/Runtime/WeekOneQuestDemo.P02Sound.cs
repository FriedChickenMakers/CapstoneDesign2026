using System;
using UnityEngine;
using UnityEngine.UI;

namespace CapstoneDesign.Runtime
{
    // Presentation only. Listening uses the existing optional reflection and timer;
    // the illustration neither captures nor plays sound.
    public static class P02SoundGuide
    {
        public static string Title(P02SoundIllustration.Stage stage)
            => stage == P02SoundIllustration.Stage.Overview ? "주변 소리 만나기" : "";

        public static string Body(P02SoundIllustration.Stage stage)
        {
            switch (stage)
            {
                case P02SoundIllustration.Stage.Overview: return "주변에서 들리는 소리에\n잠시 귀 기울이는 실습이에요.";
                case P02SoundIllustration.Stage.Preparation: return "편하게 앉아 귀 기울여요.";
                case P02SoundIllustration.Stage.Notice: return "가깝거나 먼 소리 하나를 들어요.";
                case P02SoundIllustration.Stage.Observe: return "크기와 높낮이,\n시작과 끝을 살펴요.";
                case P02SoundIllustration.Stage.Return: return "딴생각을 알아차리면,\n소리로 돌아와요.";
                case P02SoundIllustration.Stage.Practice: return "편한 만큼 소리에 귀 기울여요.";
                case P02SoundIllustration.Stage.Reflection: return "기억나는 소리를 남겨요.\n건너뛰어도 괜찮아요.";
                default: return "이름은 몰라도 괜찮아요.\n조용하면 잠시 기다려요.\n생각이 들면 소리로 돌아와요.";
            }
        }
    }

    public sealed partial class WeekOneQuestDemo
    {
        P02SoundIllustration.Stage P02Stage()
        {
            switch (guidedSlide)
            {
                case GuidedSlide.Overview: return P02SoundIllustration.Stage.Overview;
                case GuidedSlide.Preparation: return P02SoundIllustration.Stage.Preparation;
                case GuidedSlide.Cue0: return P02SoundIllustration.Stage.Notice;
                case GuidedSlide.Cue1: return P02SoundIllustration.Stage.Observe;
                case GuidedSlide.Cue2: return P02SoundIllustration.Stage.Return;
                case GuidedSlide.Practice: return P02SoundIllustration.Stage.Practice;
                case GuidedSlide.Reflection: return P02SoundIllustration.Stage.Reflection;
                default: return P02SoundIllustration.Stage.Help;
            }
        }

        void ApplyP02SoundTheme(Transform root)
        {
            Color background = FigmaMissionUi.Hex(guidedSlide <= GuidedSlide.Preparation ? "F0F5F7"
                : guidedSlide == GuidedSlide.Reflection || guidedSlide == GuidedSlide.Help ? "F7F5EF" : "EDF4F5");
            var surface = root.parent.GetComponent<Image>(); if (surface != null) surface.color = background;
            var panel = GetComponent<Image>(); if (panel != null) panel.color = background;
            int active = guidedSlide >= GuidedSlide.Cue0 && guidedSlide <= GuidedSlide.Cue2
                ? (int)guidedSlide - (int)GuidedSlide.Cue0 : -1;
            if (active < 0) return;
            for (int i = 0; i < 3; i++)
                FigmaMissionUi.Box(root, "P02.Progress." + i, 298 + i * 24, 64, 18, 3,
                    FigmaMissionUi.Hex(i <= active ? "50738B" : "D6E3E8"), 1.5f);
        }

        Button BuildP02SoundIcon(Transform root, Action action, out RectTransform illustration)
        {
            var button = FigmaMissionUi.Button(root, "", 83, 293, 224, 224, action, Color.clear);
            button.name = "Focus.IconAction"; button.transition = Selectable.Transition.None;
            button.gameObject.AddComponent<FocusMissionTapFeedback>().Button = button;
            illustration = FigmaMissionUi.Rect(button.transform, "Focus.Illustration", 0, 0, 180, 180);
            illustration.anchorMin = illustration.anchorMax = illustration.pivot = new Vector2(.5f, .5f);
            illustration.anchoredPosition = Vector2.zero;
            var artRect = FigmaMissionUi.Rect(illustration, "P02.SoundIllustration", 0, 0, 180, 180);
            artRect.gameObject.AddComponent<P02SoundIllustration>().Configure(P02Stage());
            if (Application.isPlaying && !FigmaMissionUi.ReducedMotion)
                illustration.gameObject.AddComponent<FocusMissionEntrance>();
            return button;
        }
    }
}
