using System;
using System.Linq;
using CapstoneDesign.Runtime.LocalState;
using CapstoneDesign.Runtime.Personalization;
using CapstoneDesign.Prototype;
using UnityEngine;

namespace CapstoneDesign.Runtime
{
    public sealed partial class WeekOneQuestDemo
    {
        float nextOptionalObservation;

        void AddOptionalRecommendation(Transform parent, GardenState state)
        {
            if (!MbctPolicy.AllCompletedIds(state).Any(id => MbctContent.Find(id) != null)) return;
            bool canCollect=!preview && Application.isPlaying;
            var currentPolicy = canCollect?PersonalizationClient.Instance.Policy:new Personalization.ResponsePolicy();
            var ranked = OptionalActivityRecommender.Select(state, currentPolicy, DateTimeOffset.UtcNow);
            if (ranked == null) return;
            var mission = MbctContent.Find(ranked.MissionId);
            // One decision per actual card presentation, with frozen input features.
            // Core curriculum and rewards remain entirely in the local state service.
            string decision = Guid.NewGuid().ToString("N");
            RecommendationExposure exposure=null;
            var row=ActivityListItem(parent,"다시 해볼 만한 활동",mission.title,()=>
            {
                exposure?.ReportVisible();
                var context=canCollect && PersonalizationClient.Instance.Consented?new RecommendationContext {
                    DecisionId=decision,PolicyId=ranked.PolicyId,AppVersion=Application.version,UtcOffsetMinutes=(int)DateTimeOffset.Now.Offset.TotalMinutes,Features=ranked.Features}:null;
                SelectMission(mission,context);
            },PrototypeUiIcon.Symbol.Leaf);
            exposure=row.gameObject.AddComponent<RecommendationExposure>();
            exposure.OnVisible=()=> {if(canCollect)PersonalizationClient.Instance.Record("recommendation_shown",decision,ranked.MissionId,ranked.Features,ranked.PolicyId);};
        }
        void ObserveOptionalRecommendation(bool force=false)
        {
            if(!opened || preview || !Application.isPlaying || (!force && Time.unscaledTime<nextOptionalObservation))return;
            nextOptionalObservation=Time.unscaledTime+3;
            var client=PersonalizationClient.Instance;
            if(!client.Consented)return;
            foreach(var session in Service.Snapshot.Sessions)
            {
                client.RecordSession(session);
                client.RecordMeasuredSession(session);
            }
        }
        // Keep learned MBCT recommendations reachable after the Focus navigation
        // migration. This does not reinterpret P/M missions as MBCT candidates.
        public void ShowLearnedPracticeRecommendations()
        {
            if(!opened || !SaveActivePracticeDraft())return;
            // Returning from settings must not leave the old settings page behind
            // this hub, otherwise reopening it jumps past the hub on Back.
            pageHistory.RemoveAll(page=>page.Key=="recommendation-preferences");
            EnterPage("learned-recommendations",ShowLearnedPracticeRecommendations);
            var p=FocusPage("이전 활동 복습·추천","저장한 마음챙김 활동을 이어가요");
            var state=Service.Snapshot;
            var choices=ActivityList(p,"Learned practice recommendations");
            AddOptionalRecommendation(choices,state);
            if(!MbctPolicy.AllCompletedIds(state).Any(id=>MbctContent.Find(id)!=null))
                Guidance(p,"이전에 완료한 마음챙김 활동이 있으면 복습을 추천해 드려요.");
            ActivityListItem(choices,"배운 활동 모두 보기",null,ShowLegacyFree,PrototypeUiIcon.Symbol.Journal);
            ActivityListItem(choices,"추가 활동 추천 설정",null,ShowRecommendationPreferences,PrototypeUiIcon.Symbol.Gear);
            DockAction("내 과정으로",ShowFocusCourse);
        }
        public void ShowRecommendationPreferences()
        {
            EnterPage("recommendation-preferences",ShowRecommendationPreferences);
            var p=FocusPage("추가 활동 추천","배운 활동에서 골라드려요");
            var state=Service.Snapshot;
            string preferred=state.PracticePreferences.FirstOrDefault(v=>v.Key=="favorite")?.Value??"";
            GardenUi.FlowLabel(p,"어떤 실습이 편했나요?",GardenUi.HeadingSize);
            var choices=ActivityList(p,"Preferred practices");
            foreach(string option in new[]{"몸 살펴보기","호흡 관찰","일상 행동","잠시 멈추기"})
            {
                string value=option;
                ActivityListItem(choices,value,preferred==value?"선택함":null,()=>
                    {if(Apply(Service.SavePreference("favorite",value)))ShowRecommendationPreferences();},PrototypeUiIcon.Symbol.Leaf);
            }
            GardenUi.FlowLabel(p,"얼마나 시간을 내볼까요?",GardenUi.HeadingSize);
            string minutes=state.PracticePreferences.FirstOrDefault(v=>v.Key=="recommendation-minutes")?.Value??"3";
            var durations=ActivityList(p,"Available practice time");
            foreach(string option in new[]{"1","3","5"})
            {
                string value=option;
                ActivityListItem(durations,value+"분 정도",minutes==value?"선택함":null,()=>
                    {if(Apply(Service.SavePreference("recommendation-minutes",value)))ShowRecommendationPreferences();},PrototypeUiIcon.Symbol.Journal);
            }
            DockAction("배운 활동으로",ShowLearnedPracticeRecommendations);
        }
    }
}
