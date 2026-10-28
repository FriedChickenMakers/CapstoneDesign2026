using CapstoneDesign.Runtime.LocalState;

namespace CapstoneDesign.Runtime
{
    public sealed partial class WeekOneQuestDemo
    {
        public string MissionDateLabel => opened?Service.Snapshot.LastCycleId.Replace('-','.') : "확인 중";

        public bool ResetMissionProgress()
        {
            if(!opened || !Apply(Service.ResetMissionHistory()))return false;
            // Clear detached UI drafts only after the saved reset succeeds.
            ClearPracticeDraft();planDrafts.Clear();recordsPage=0;sessionId=null;mood=null;draftNote=null;note=null;
            focusDraftSession=null;focusDraftStep=-1;focusAnswer="";focusMood="";focusNote="";focusDirty=false;
            focusAnswerInput=null;focusNoteInput=null;focusNextAction=0;
            stepQueries.Clear();runningStepQuery=null;
            ResetCompletedNavigation();ShowHome();return true;
        }

        public bool AdvanceMissionDay()
        {
            if(!opened || !SaveActivePracticeDraft())return false;
            if(!Apply(Service.AdvancePreviewDay(EligibleCycleAnimals(Service.Snapshot))))return false;
            ShowHome();return true;
        }
    }
}
