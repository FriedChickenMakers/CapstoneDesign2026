using System;
using System.Linq;
using CapstoneDesign.Runtime.LocalState;
using UnityEngine;

namespace CapstoneDesign.Runtime
{
    public sealed partial class WeekOneQuestDemo
    {
        float nextNotificationPoll;
        readonly ReminderSyncCache reminderSync = new ReminderSyncCache();

        public void TickMissionNotifications()
        {
            if(!opened || preview || mode!="LIVE" || Time.unscaledTime<nextNotificationPoll)return;
            nextNotificationPoll=Time.unscaledTime+3;
            ConsumeWalkingAchievement();
            // A save may have failed after valid steps were persisted. Retrying that transaction
            // must not depend on another sensor sync or on notification permission.
            var retry=Service.Snapshot.Sessions.LastOrDefault(s=>WalkingEvidence.CanAutomaticallyComplete(s,DebugWalkEnabled));
            if(retry!=null)CompleteWalkingGoal(retry.SessionId);
            SyncDailyReminder();
            var openedNotification=ActivityNotificationBridge.ConsumeOpen();
            if(openedNotification.kind!="daily" && openedNotification.kind!="walking")return;
            transform.root.GetComponent<MockupNavigation>()?.ShowActivities();
            if(openedNotification.kind=="walking")
            {
                var saved=Service.Snapshot.Sessions.FirstOrDefault(s=>s.SessionId==openedNotification.sessionId && s.MissionId=="free-mission:M15");
                if(saved!=null)
                {
                    // Retained notifications open the preserved record, not a retired mission flow.
                    ShowRecord(saved.SessionId);
                    return;
                }
            }
            ShowHome();
        }

        void SyncDailyReminder()
        {
            var state=Service.Snapshot;
            string completed=ActivityNotificationLogic.CompletedCalendarDay(state,Service.CurrentUtc,Service.CurrentTimeZone);
            var current=state.Sessions.LastOrDefault(s=>FocusMissionPolicy.IsFocus(s) && FocusMissionPolicy.IsActive(s));
            string title=current==null?"":current.MissionTitleSnapshot ?? FocusMissionCatalog.Find(current.MissionId)?.Title ?? "";
            string signature=completed+"|"+title+"|"+(current!=null);
            // Store only a confirmed native write. JNI failure or a temporarily unavailable
            // platform bridge must leave the same payload eligible for the next poll.
            reminderSync.Sync(signature,()=>ActivityNotificationBridge.Sync(completed,title,current!=null));
        }

        void ConsumeWalkingAchievement()
        {
            var receipt=ActivityNotificationBridge.Achievement();
            if(string.IsNullOrEmpty(receipt.sessionId))return;
            var session=Service.Snapshot.Sessions.FirstOrDefault(s=>s.SessionId==receipt.sessionId && s.MissionId=="free-mission:M15");
            if(session!=null && session.Status==ParticipationState.RewardCommitted)
            { AndroidPlatformBridge.AcknowledgeMissionAchievement(receipt.sessionId);return; }
            if(session==null || session.Status==ParticipationState.Ended)
            { AndroidPlatformBridge.CancelMissionRewardReminder(receipt.sessionId);return; }
            if(!WalkingEvidence.AcceptsAchievement(session,receipt,DebugWalkEnabled,Service.CurrentUtc))return;
            if(session.StepSource==receipt.source && session.RealStepHighWater>=receipt.steps)
                CompleteWalkingGoal(session.SessionId);
            else if(Apply(Service.ObserveMissionSteps(session.SessionId,receipt.steps,receipt.source)))CompleteWalkingGoal(session.SessionId);
        }

        // Query IDs alone cannot protect a day-total goal from an old debug/since-start query.
        // The caller supplies the immutable query parameters saved when the native read began.
        bool ApplyQueriedMissionSteps(string id,string requestId,long start,long end,bool aggregate,StepRangeSnapshot result)
        {
            var session=Service.Snapshot.Sessions.FirstOrDefault(s=>s.SessionId==id);
            return WalkingEvidence.AcceptsMissionQuery(session,DebugWalkEnabled,requestId,start,end,aggregate,result,Service.CurrentUtc) &&
                Apply(Service.ObserveMissionSteps(id,result.count,result.source));
        }

        void CompleteWalkingGoal(string id)
        {
            var beforeState=Service.Snapshot;
            var before=beforeState.Sessions.FirstOrDefault(s=>s.SessionId==id);
            // Focus missions finish through their own guidance and reflection transaction.
            // A step notification must not submit or discard the user's editable focus draft.
            if(FocusMissionPolicy.IsFocus(before))return;
            if(!WalkingEvidence.CanAutomaticallyComplete(before,DebugWalkEnabled))return;
            // Do not discard a reflection the user is currently composing. Its explicit Finish
            // uses the same atomic completion; leaving the form lets the verified goal finish.
            if(screen=="reflection" && sessionId==id && isActiveAndEnabled)return;
            // Leaving the reflection page may destroy its InputField, while these session-owned
            // drafts remain in memory. Commit them with the reward before clearing UI state.
            string reflectionMood=sessionId==id?mood:null;
            string reflectionNote=sessionId==id?draftNote:null;
            if(!Apply(Service.TryCompleteWalkingGoal(id,reflectionMood,reflectionNote)))return;
            AndroidPlatformBridge.AcknowledgeMissionAchievement(id);
            reminderSync.Invalidate();
            bool showingParticipation=(screen=="session" || screen=="reflection") && sessionId==id;
            if(sessionId==id) {sessionId=null;mood=null;draftNote=null;ClearPracticeDraft();}
            if(showingParticipation)
            {
                ResetCompletedNavigation();
                ShowCompletion(Service.Snapshot.Nutrient>beforeState.Nutrient);
            }
            else pageHistory.RemoveAll(page=>page.Key=="session" || page.Key=="reflection");
        }
    }
}
