using System;
using System.Linq;
using CapstoneDesign.Runtime.LocalState;
using UnityEngine;
using UnityEngine.UI;

namespace CapstoneDesign.Runtime
{
    public sealed partial class WeekOneQuestDemo
    {
        readonly WalkingBaselineRefresh walkingBaselineRefresh=new WalkingBaselineRefresh();
        Text walkingIntroGoal, walkingIntroExplanation;
        Button walkingIntroStart;
        string walkingIntroSession;
        float nextWalkingBaselinePoll;
        AndroidPlatformSnapshot walkingBaselineSnapshot;

        // Unity serializes the public preview DTO even when the scene is running LIVE.
        // Only an explicit preview may replace the selected platform provider.
        AndroidPlatformSnapshot ReadWalkingHealth() => walkingBaselineSnapshot=
            preview && PreviewHealth!=null?PreviewHealth:AndroidPlatformBridge.GetSnapshot();
        string WalkingInputMode => preview?"MOCK":mode;

        public void OnActivitiesOpened() { RequestWalkingBaselineRefresh(); }

        void RequestWalkingBaselineRefresh()
        {
            if(!opened || WalkingInputMode!="LIVE")return;
            if(walkingBaselineRefresh.IsPending){TickWalkingPersonalization();return;}
            var snapshot=ReadWalkingHealth();
            walkingBaselineRefresh.Request(WalkingInputMode,snapshot?.healthConnect,Service.CurrentUtc,
                Service.CurrentTimeZone,Time.realtimeSinceStartup,AndroidPlatformBridge.RefreshHealthData);
            ApplyWalkingBaselinePreview(snapshot);
        }

        public void TickWalkingPersonalization()
        {
            if(!opened || WalkingInputMode!="LIVE" || Time.realtimeSinceStartup<nextWalkingBaselinePoll)return;
            if(!walkingBaselineRefresh.IsPending && (screen!="session" || walkingIntroStart==null))return;
            nextWalkingBaselinePoll=Time.realtimeSinceStartup+.75f;
            // Poll scalar native status, then deserialize full health history only when its
            // completed refresh changed. Selected introductions need no per-frame bridge work.
            var status=(preview && PreviewHealth!=null?PreviewHealth:AndroidPlatformBridge.GetSettingsSnapshot())?.healthConnect;
            var cached=walkingBaselineSnapshot?.healthConnect;
            if(cached==null || (status?.refreshing!=true &&
                (status?.lastRefreshEpochMs!=cached.lastRefreshEpochMs || status?.status!=cached.status)))
                ReadWalkingHealth();
            var health=walkingBaselineSnapshot?.healthConnect;
            if(status!=null){status.personalization=health?.personalization;health=status;}
            walkingBaselineRefresh.Tick(WalkingInputMode,health,Service.CurrentUtc,
                Service.CurrentTimeZone,Time.realtimeSinceStartup,AndroidPlatformBridge.RefreshHealthData);
            ApplyWalkingBaselinePreview(walkingBaselineSnapshot);
        }

        // Replace only the selected walking introduction's labels/button. Health results must
        // never rebuild a practice/reflection page, move focus, or overwrite a started goal.
        void ApplyWalkingBaselinePreview(AndroidPlatformSnapshot snapshot)
        {
            if(screen!="session" || walkingIntroSession!=sessionId || walkingIntroStart==null ||
                walkingIntroGoal==null || walkingIntroExplanation==null)return;
            var session=Service.Snapshot.Sessions.FirstOrDefault(s=>s.SessionId==sessionId);
            if(session?.MissionId!="free-mission:M15" || session.Status!=ParticipationState.Selected)return;
            var target=WalkingPersonalization.Evaluate(snapshot?.healthConnect?.personalization,Service.CurrentUtc,Service.CurrentTimeZone);
            bool pending=WalkingInputMode=="LIVE" && walkingBaselineRefresh.IsPending;
            walkingIntroGoal.text="오늘 총 "+(DebugWalkEnabled?10:target.GoalSteps).ToString("N0")+"걸음";
            walkingIntroExplanation.text=pending?"최근 활동과 수면 기록을 확인하고 있어요.":
                walkingBaselineRefresh.Failed && !WalkingPersonalization.IsFresh(snapshot?.healthConnect?.personalization,Service.CurrentUtc,Service.CurrentTimeZone)
                ?"건강 기록을 확인하지 못해 기본 목표로 시작해요.":WalkingPersonalization.Explanation(target);
            walkingIntroStart.interactable=!pending;
            var label=walkingIntroStart.GetComponentInChildren<Text>(true);
            if(label!=null)label.text=pending?"확인 중…":"시작";
        }

        void StartSelectedWalking()
        {
            // A user can tap immediately after returning, or leave this introduction open
            // across midnight. Check again before freezing its target and calendar window.
            RequestWalkingBaselineRefresh();
            if(WalkingInputMode=="LIVE" && walkingBaselineRefresh.IsPending)return;
            var target=WalkingPersonalization.Evaluate(ReadWalkingHealth()?.healthConnect?.personalization,
                Service.CurrentUtc,Service.CurrentTimeZone);
            if(Apply(Service.StartWalkingSession(sessionId,target)))
            { RequestStepSyncSoon();ScheduleMissionReminder(); }
            ShowSession();
        }
    }
}
