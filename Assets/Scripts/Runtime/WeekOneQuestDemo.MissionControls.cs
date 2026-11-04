using CapstoneDesign.Runtime.LocalState;
using System.Linq;

namespace CapstoneDesign.Runtime
{
    public sealed partial class WeekOneQuestDemo
    {
        public string MissionDateLabel => opened?Service.AppliedCycleId.Replace('-','.') : "확인 중";

        public bool ResetMissionProgress()
        {
            var photoIds=opened?Service.Snapshot.Sessions.SelectMany(s=>new[]{s.Photo?.Id,s.PendingPhotoId}).ToArray():System.Array.Empty<string>();
            if(!opened || !Apply(Service.ResetMissionHistory()))return false;
            foreach(var photoId in photoIds)Photos.Delete(photoId);
            Photos.Prune(Service.Snapshot);
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
