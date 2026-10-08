using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using CapstoneDesign.Runtime.LocalState;

namespace CapstoneDesign.Runtime
{
    public sealed partial class WeekOneQuestDemo
    {
        sealed class ActivityPage
        {
            public string Key;
            public Action Render;
            public float Scroll = 1;
        }

        readonly List<ActivityPage> pageHistory = new List<ActivityPage>();
        readonly Dictionary<string, string> planDrafts = new Dictionary<string, string>();
        bool restoringPage;

        // Rebuilding a live status page or selecting a list's next week is not
        // navigation. Returning to an existing ancestor removes its children.
        void EnterPage(string page, Action render, string parameter = null)
        {
            if (!restoringPage)
            {
                var scroll = pageViewport == null ? null : pageViewport.GetComponent<ScrollRect>();
                if (pageHistory.Count > 0 && scroll != null)
                    pageHistory[pageHistory.Count - 1].Scroll = scroll.verticalNormalizedPosition;
                string key = page + (parameter == null ? "" : ":" + parameter);
                int existing = pageHistory.FindLastIndex(item => item.Key == key);
                if (existing >= 0)
                {
                    pageHistory.RemoveRange(existing + 1, pageHistory.Count - existing - 1);
                    pageHistory[existing].Render = render;
                }
                else pageHistory.Add(new ActivityPage { Key = key, Render = render });
            }
            screen = page;
        }

        public bool TryNavigateBack()
        {
            if (screen == "home") return false;
            // A failed save consumes Back and retains the input and error message.
            if (opened && !SaveActivePracticeDraft()) return true;
            if (screen == "experience" && experienceStep > 0)
            {
                experienceStep--;
                ShowExperienceStep();
                return true;
            }
            if (screen == "session" || screen == "reflection")
            {
                var session = ActiveSession();
                var mission = MindfulnessContent.FindMission(session?.MissionId);
                if (session?.Status == ParticipationState.InProgress && mission?.mbctActivity > 0 &&
                    (session.InstructionStep > 0 || screen == "reflection"))
                {
                    int step = screen == "reflection"
                        ? Math.Min(session.InstructionStep, mission.steps.Length - 1)
                        : session.InstructionStep - 1;
                    if (!Apply(Service.SavePracticeStep(session.SessionId, Math.Max(0, step)))) return true;
                    ClearPracticeDraft();
                    ShowSession();
                    return true;
                }
            }
            ClearPracticeDraft();
            if (screen == "placement" || screen == "growth") UpdateGarden();
            ClearGhost();
            if (pageHistory.Count <= 1) { ShowHome(); return true; }
            pageHistory.RemoveAt(pageHistory.Count - 1);
            var previous = pageHistory[pageHistory.Count - 1];
            restoringPage = true;
            try { previous.Render(); }
            finally { restoringPage = false; }
            var restoredScroll = pageViewport == null ? null : pageViewport.GetComponent<ScrollRect>();
            if (restoredScroll != null)
            {
                Canvas.ForceUpdateCanvases();
                restoredScroll.verticalNormalizedPosition = previous.Scroll;
            }
            return true;
        }

        void FinishExperienceNavigation()
        {
            // A saved form must not reappear as an editable duplicate on Back.
            pageHistory.RemoveAll(page => page.Key == "experience" || page.Key == "experience-type");
            ShowExperienceHistory();
        }

        void ResetCompletedNavigation()
        {
            pageHistory.Clear();
            pageHistory.Add(new ActivityPage { Key = "home", Render = ShowHome });
        }
    }
}
