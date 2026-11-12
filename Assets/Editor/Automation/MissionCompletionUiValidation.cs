using System;
using System.Linq;
using System.Reflection;
using CapstoneDesign.Runtime;
using CapstoneDesign.Runtime.LocalState;
using UnityEngine;
using UnityEngine.UI;

namespace CapstoneDesign.EditorTools
{
    // Invoked explicitly by isolated integration validation. All participation,
    // progress and photos belong to the existing in-memory preview fixture.
    internal static class MissionCompletionUiValidation
    {
        public static void AllCompletionActions(FocusMissionValidation.Report report, Action<bool,string> check, bool capture)
        {
            foreach (var size in new[] { new Vector2Int(320,568), new Vector2Int(390,844) })
            using (var f = new FocusMissionValidation.Fixture(report, true))
            {
                f.Resize(size.x, size.y);
                check(f.Service.SelectMbctProgram("8w") && f.Service.SavePreference("favorite", "물 마시기"), f.Service.LastError);
                var basics = FocusMissionPolicy.CourseCodes("8w").Take(28).Select(code => FocusMissionCatalog.Find(code)).ToArray();
                var missions = basics.Concat(FocusMissionCatalog.Additional).ToArray();
                check(basics.Select(mission => mission.Id).Distinct().Count() == FocusMissionCatalog.Basic.Length
                    && missions.Length == FocusMissionCatalog.All.Length, "Completion actions cover every current P/M mission.");
                for (int index = 0; index < missions.Length; index++)
                {
                    var mission = missions[index];
                    f.Clock.UtcNow = f.Clock.UtcNow.AddDays(2);
                    check(f.Service.RefreshCycle(Array.Empty<string>()), f.Service.LastError);
                    string id = "completion-actions-" + mission.Code;
                    check(f.Service.BeginFocusSession(id, mission.Id, index < basics.Length ? index + 1 : 0,
                        program: index < basics.Length ? "8w" : null), f.Service.LastError);
                    Complete(f, id, check);
                    string before = StateCodec.Encode(f.Service.Snapshot);
                    var recommendation = FocusAdditionalRecommendationPolicy.Select(f.Service.Snapshot, f.Clock.UtcNow);
                    f.Store.FailNextSaves = 1;
                    f.Invoke("ShowFocusCompletion", id);
                    string label = mission.Code + " " + size + ": ";
                    check(f.Ui.CurrentScreen == "focus-complete", label + "the committed completion opens.");
                    check(!f.Ui.GetComponentsInChildren<Button>(true).Any(button => button.name == "Focus.ConfirmHome" || button.name == "Focus.ViewRecord"),
                        label + "the two removed navigation controls are absent, including inactive copies.");
                    check(f.Has("Focus.GoGarden"), label + "the garden destination remains available.");
                    check(StateCodec.Encode(f.Service.Snapshot) == before && f.Store.FailNextSaves == 1,
                        label + "opening completion does not save, alter preferences or award again.");
                    f.Capture("qa-completion-actions-" + mission.Code + "-" + size.x + "x" + size.y,
                        capture && new[] { "P01", "P28", "M01", "M17" }.Contains(mission.Code));
                    ReturnToMissions(f);
                    check(f.Ui.CurrentScreen == "home", label + "garden's real mission tab returns to today's missions.");
                    // The record hub remains available through the existing tab,
                    // rather than through a duplicate completion-screen action.
                    f.ClickControl(f.Ui.GetComponentInChildren<MissionBottomNavigation>(false).CourseButton);
                    check(f.Ui.CurrentScreen == "course", label + "the real record tab opens the record hub.");
                    f.ClickControl(f.Ui.GetComponentInChildren<MissionBottomNavigation>(false).MissionButton);
                    var after = FocusAdditionalRecommendationPolicy.Select(f.Service.Snapshot, f.Clock.UtcNow);
                    check(StateCodec.Encode(f.Service.Snapshot) == before && f.Store.FailNextSaves == 1,
                        label + "completion and tab navigation preserve all saved data without attempting a write.");
                    check(after.MissionId == recommendation.MissionId && after.PolicyId == recommendation.PolicyId
                        && after.Reason == recommendation.Reason && after.Score == recommendation.Score
                        && after.Features.SequenceEqual(recommendation.Features),
                        label + "recommendation choice, reasons, score and features are unchanged.");
                    f.Store.FailNextSaves = 0;
                }
            }
        }

        public static void ReturnToMissions(FocusMissionValidation.Fixture fixture)
        {
            fixture.Click("Focus.GoGarden");
            if (fixture.Navigation.home == null || !fixture.Navigation.home.homeCanvas.gameObject.activeSelf || fixture.Ui.gameObject.activeSelf)
                throw new InvalidOperationException("Completion's remaining action must reach the integrated garden.");
            fixture.ClickControl(fixture.Navigation.home.NavigationBar.MissionButton);
        }

        public static void OpenJournalRecordFromCompletion(FocusMissionValidation.Fixture fixture, string sessionId)
        {
            fixture.Click("Focus.GoGarden");
            if (fixture.Navigation.home == null || !fixture.Navigation.home.homeCanvas.gameObject.activeSelf)
                throw new InvalidOperationException("Completion's remaining action must reach the integrated garden.");
            fixture.ClickControl(fixture.Navigation.home.NavigationBar.CourseButton);
            fixture.Click("DailyEmotion.History");
            fixture.Click("ActivityJournal.Mission." + sessionId);
        }

        public static void AdditionalArtwork(FocusMissionValidation.Report report, Action<bool,string> check, bool capture)
        {
            foreach (var size in new[] { new Vector2Int(320,568), new Vector2Int(390,844) })
            foreach (var mission in FocusMissionCatalog.Additional)
            using (var f = new FocusMissionValidation.Fixture(report))
            {
                f.Resize(size.x, size.y);
                string id = "completion-art-" + mission.Code;
                check(f.Service.BeginFocusSession(id,mission.Code),f.Service.LastError);
                Complete(f,id,check);
                string before = StateCodec.Encode(f.Service.Snapshot);
                f.Store.FailNextSaves = 1;
                f.Invoke("ShowFocusCompletion",id);
                check(f.Ui.CurrentScreen == "focus-complete",mission.Code + ": saved completion screen opens");
                var graphic = mission.Code == "M01"
                    ? (MaskableGraphic)f.Ui.GetComponentInChildren<M01LightIllustration>(false)
                    : f.Ui.GetComponentInChildren<AdditionalMissionIllustration>(false);
                check(graphic != null,mission.Code + ": completion owns its correct illustration family");
                if (graphic is AdditionalMissionIllustration additional)
                    check(additional.Code == mission.Code && additional.Stage == "review",mission.Code + ": completion artwork is configured for the saved mission");
                check(!graphic.raycastTarget,mission.Code + ": completion artwork does not intercept buttons");
                using (var vertices = new VertexHelper())
                {
                    graphic.GetType().GetMethod("OnPopulateMesh",BindingFlags.Instance|BindingFlags.NonPublic,
                        null,new[] { typeof(VertexHelper) },null).Invoke(graphic,new object[] { vertices });
                    check(vertices.currentVertCount > 30 && vertices.currentIndexCount > 60,
                        mission.Code + ": real completion geometry is not empty");
                }
                f.Capture("qa-completion-art-" + mission.Code + "-" + size.x + "x" + size.y,
                    capture && (mission.Code == "M01" || mission.Code == "M03" || mission.Code == "M17"));
                check(StateCodec.Encode(f.Service.Snapshot) == before && f.Store.FailNextSaves == 1,
                    mission.Code + ": opening/rendering completion neither writes nor rewards again");
            }
        }

        public static void CompletedCourseReadOnly(FocusMissionValidation.Report report, Action<bool,string> check, bool capture)
        {
            foreach (bool activeOtherCourse in new[] { false,true })
            using (var f = new FocusMissionValidation.Fixture(report))
            {
                f.Resize(390,844);
                for (int order = 1; order <= FocusMissionPolicy.TotalPracticesFor("2w"); order++)
                {
                    f.Clock.UtcNow = f.Clock.UtcNow.AddDays(2);
                    check(f.Service.RefreshCycle(Array.Empty<string>()),f.Service.LastError);
                    string id = "completed-course-readonly-" + order;
                    check(f.Service.BeginFocusSession(id,FocusMissionPolicy.MissionAt(order,"2w").Code,order),f.Service.LastError);
                    Complete(f,id,check);
                }
                f.Clock.UtcNow = f.Clock.UtcNow.AddDays(2);
                check(f.Service.RefreshCycle(Array.Empty<string>()) && f.Service.SelectMbctProgram("8w"),f.Service.LastError);
                if (activeOtherCourse)
                    check(f.Service.BeginFocusSession("active-eight-week",FocusMissionPolicy.MissionAt(1,"8w").Code,1),f.Service.LastError);
                string before = StateCodec.Encode(f.Service.Snapshot);
                f.Invoke("ShowFocusProgramConfirmation","2w");
                var action = f.Ui.GetComponentsInChildren<Button>(false).Single(button => button.name == "Focus.ConfirmProgram.2w");
                check(action.IsInteractable() && action.GetComponentInChildren<Text>().text == "코스 기록 보기",
                    "A finished course offers record reading even while another course has an active practice");
                f.Store.FailNextSaves = 1;
                f.Click("Focus.ConfirmProgram.2w");
                check(f.Ui.CurrentScreen == "focus-course" && f.Ui.GetComponentsInChildren<Text>(false).Any(text => text.text == "현재 : 2주 코스"),
                    "View-completed-course opens that course's record dashboard");
                check(f.Service.Snapshot.MbctProgramId == "8w" && StateCodec.Encode(f.Service.Snapshot) == before && f.Store.FailNextSaves == 1,
                    "Reading a completed course preserves selection, all saved progress and wallet without even attempting a save");
                f.Capture("qa-completed-course-readonly-" + (activeOtherCourse ? "active" : "idle"),capture);
            }
        }

        static void Complete(FocusMissionValidation.Fixture f,string id,Action<bool,string> check)
        {
            check(f.Service.StartSession(id),f.Service.LastError);
            var mission = FocusMissionCatalog.FindForSession(f.Service.Snapshot.Sessions.Single(session => session.SessionId == id));
            if (mission.Code == "M15")
                check(f.Service.EnsureFocusWalkingTarget(id,WalkingPersonalization.Evaluate(null,f.Clock.UtcNow,f.Clock.TimeZone)),f.Service.LastError);
            while (true)
            {
                var session = f.Service.Snapshot.Sessions.Single(item => item.SessionId == id);
                if (session.InstructionStep >= mission.Steps.Length) break;
                int step = session.InstructionStep; var instruction = mission.Steps[step];
                string answer = instruction.Kind == "draw" ? "DRAW1|AA_B" : instruction.Options.FirstOrDefault()
                    ?? (instruction.RequiredAnswer || instruction.OptionalInput ? "synthetic response" : "");
                if (instruction.Kind == "plan")
                {
                    string activity = FocusMissionAdjustmentPolicy.PlanActivityOptions[0];
                    string period = FocusMissionAdjustmentPolicy.PlanTimePeriodOptions[0];
                    check(f.Service.SaveFocusPlan(id,step,activity,period,""),f.Service.LastError);
                    answer = FocusMissionAdjustmentPolicy.ComposePlan(activity,period,"");
                }
                check(f.Service.ConfirmFocusStep(id,step,answer),f.Service.LastError);
            }
            if (CameraMissionPolicy.Ready(f.Service.Snapshot.Sessions.Single(session => session.SessionId == id)))
                check(f.Service.SkipPhoto(id),f.Service.LastError);
            check(f.Service.CompletePracticeSession(id),f.Service.LastError);
        }
    }
}
