using System;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using CapstoneDesign.Runtime.LocalState;

namespace CapstoneDesign.Runtime
{
    public sealed partial class WeekOneQuestDemo
    {
        string GuidedIllustrationKey() => guidedSlide.ToString().ToLowerInvariant();

        static string SavedPracticeIllustrationChoice(FocusMissionDefinition mission, ActivitySession session)
        {
            string value(string key) => session.FocusStepRecords.FirstOrDefault(record => record.Key == key
                && !record.Skipped && !string.IsNullOrEmpty(record.Value))?.Value;
            if (FocusMissionCatalog.IsGuided(session)) return value("practice") ?? "";
            if (mission.Code == "P26") return value("help") ?? value("route") ?? "";
            if (mission.Code == "P18") return value("practice") ?? value("activity") ?? "";
            string key = mission.Code == "P24" || mission.Code == "P19" ? "route" : mission.Code == "P12" ? "experience" : "activity";
            return value(key) ?? "";
        }

        void ApplyPracticeIllustrationTheme(Transform root, FocusMissionDefinition mission, string key)
        {
            var background = FigmaMissionUi.Hex(mission.BackgroundHex);
            if (key == "help" || key == "guide-help" || key == "reflection" || key == "review" || key == "notice")
                background = Color.Lerp(background, FigmaMissionUi.Hex("F7F5EF"), .45f);
            var surface = root.parent.GetComponent<Image>(); if (surface != null) surface.color = background;
            var panel = GetComponent<Image>(); if (panel != null) panel.color = background;
        }

        Button BuildPracticeIllustration(Transform root, FocusMissionDefinition mission, string key,
            string choice, Action action, out RectTransform illustration)
        {
            var button = FigmaMissionUi.Button(root, "", 83, 293, 224, 224, action, Color.clear);
            button.name = "Focus.IconAction"; button.transition = Selectable.Transition.None;
            button.gameObject.AddComponent<FocusMissionTapFeedback>().Button = button;
            illustration = FigmaMissionUi.Rect(button.transform, "Focus.Illustration", 0, 0, 180, 180);
            illustration.anchorMin = illustration.anchorMax = illustration.pivot = new Vector2(.5f, .5f);
            illustration.anchoredPosition = Vector2.zero;
            if (FocusAdditionalSlideContent.Supports(mission.Code))
                FigmaMissionUi.Rect(illustration, "Focus.AdditionalIllustration", 0, 0, 180, 180)
                    .gameObject.AddComponent<AdditionalMissionIllustration>().Configure(mission.Code, key, choice, FocusAccent(mission));
            else
            {
                var art = FigmaMissionUi.Rect(illustration, "Focus.PracticeIllustration", 0, 0, 180, 180)
                    .gameObject.AddComponent<FocusPracticeIllustration>();
                art.Configure(mission.Code, key, choice, FocusAccent(mission));
                ApplyPracticeIllustrationContext(art, mission, ActiveSession());
            }
            if (Application.isPlaying && !FigmaMissionUi.ReducedMotion)
                illustration.gameObject.AddComponent<FocusMissionEntrance>();
            return button;
        }

        void ApplyPracticeIllustrationContext(FocusPracticeIllustration art, FocusMissionDefinition mission, ActivitySession session)
        {
            // A routine and a future plan have independent choices. Passing only
            // the last answer lost the earlier trigger/target in their drawings.
            string value(string key)
            {
                if (session == null) return "";
                int index = Array.FindIndex(mission.Steps, step => step.Key == key);
                if (index >= 0 && focusDraftSession == session.SessionId && focusDraftStep == index) return focusAnswer ?? "";
                return session.FocusStepRecords.FirstOrDefault(record => record.Key == key && !record.Skipped)?.Value ?? "";
            }
            art.ConfigureContext(mission.Code == "P27" ? value("trigger") : "",
                mission.Code == "P27" ? value("activity") : mission.Code == "P28" ? value("next-activity") : "",
                mission.Code == "P28" ? value("time") : "",
                mission.Code == "P28" && session != null && art.StepKey == "review"
                    && BasicReviewRecords(session, session.InstructionStep).ElementAtOrDefault(basicReviewPage)?.Key == "activity");
        }
    }
}
