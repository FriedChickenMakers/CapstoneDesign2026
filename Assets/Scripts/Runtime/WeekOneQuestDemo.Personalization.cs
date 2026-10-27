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
        void ObserveOptionalRecommendation()
        {
            if(!opened || preview || !Application.isPlaying || Time.unscaledTime<nextOptionalObservation)return;
            nextOptionalObservation=Time.unscaledTime+3;
            var client=PersonalizationClient.Instance;
            if(!client.Consented)return;
            foreach(var session in Service.Snapshot.Sessions.Where(s=>s.Recommendation!=null))client.RecordSession(session);
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
            DockAction("배운 활동으로",ShowFree);
        }
    }
}
