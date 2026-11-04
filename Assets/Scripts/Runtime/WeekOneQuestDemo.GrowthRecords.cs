using System;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using CapstoneDesign.Runtime.LocalState;
using CapstoneDesign.Prototype;

namespace CapstoneDesign.Runtime
{
    public sealed partial class WeekOneQuestDemo
    {
        int growthHistoryPage;

        public void ShowGrowthHistory()
        {
            EnterPage("growth-history",ShowGrowthHistory);var p=FocusPage("이전 성장 구매 기록","나의 정원");
            var state=Service.Snapshot;var records=state.GrowthHistory.AsEnumerable().Reverse().ToArray();
            int pages=Math.Max(1,(records.Length+3)/4);growthHistoryPage=Mathf.Clamp(growthHistoryPage,0,pages-1);
            if(records.Length==0)Guidance(p,"이전에 영양제로 키운 기록이 없어요. 활동으로 자라는 식물과 기억은 식물함에서 볼 수 있어요.");
            if(state.PurchaseCommands.Any(command=>command.Command.StartsWith("growth|",StringComparison.Ordinal) && !records.Any(record=>record.RequestId==command.RequestId)))
                GardenUi.FlowLabel(p,"이전 버전의 성장은 정원에 그대로 남아 있어요. 당시의 상세 연결 기록은 없어요.",GardenUi.BodySize,GardenUi.Muted);
            if(records.Length>0)
            {
                var list=ActivityList(p,"Plant growth history");
                foreach(var entry in records.Skip(growthHistoryPage*4).Take(4))
                {
                    string id=entry.RequestId;
                    ActivityListItem(list,GrowthRecordTitle(entry),LocalTime(entry.CreatedUtc),()=>ShowGrowthRecord(id),PrototypeUiIcon.Symbol.Leaf);
                }
            }
            if(pages>1)
            {
                DockPair("이전 성장",()=>{growthHistoryPage--;ShowGrowthHistory();},"다음 성장",()=>{growthHistoryPage++;ShowGrowthHistory();});
                var buttons=activityActions.GetComponentsInChildren<Button>();
                buttons.First(button=>button.name=="이전 성장").interactable=growthHistoryPage>0;
                buttons.First(button=>button.name=="다음 성장").interactable=growthHistoryPage<pages-1;
            }
            DockAction("식물함",ShowPlantCollection,true);
            DockAction("참여 기록",ShowGardenJournal);
        }
        static string GrowthRecordTitle(GardenGrowthRecord record)
        {
            var stages=new[]{"씨앗","새싹","봉오리","개화"};
            int before=GardenPlantVisuals.StageForGrowth(record.BeforeGrowth),after=GardenPlantVisuals.StageForGrowth(record.AfterGrowth);
            return before==after?"식물이 조금 더 자랐어요":stages[before]+" → "+stages[after];
        }
        void ShowGrowthRecord(string requestId)
        {
            var state=Service.Snapshot;var record=state.GrowthHistory.FirstOrDefault(entry=>entry.RequestId==requestId);
            if(record==null){ShowGrowthHistory();return;}
            EnterPage("growth-record",()=>ShowGrowthRecord(requestId),requestId);
            var p=FocusPage(GrowthRecordTitle(record),LocalTime(record.CreatedUtc));
            var linked=state.Sessions.FirstOrDefault(session=>session.SessionId==record.LinkedSessionId);
            if(linked==null)Guidance(p,"이 성장에는 연결한 활동 기록이 없어요. 식물의 변화만 남겨두었어요.");
            else
            {
                GardenUi.FlowLabel(p,"함께 기억하기로 고른 활동",GardenUi.CaptionSize,GardenUi.Muted);
                GardenTypography.SetWeight(GardenUi.FlowLabel(p,FocusSessionTitle(linked),GardenUi.HeadingSize),true);
                GardenUi.FlowLabel(p,LocalTime(linked.EndedUtc),GardenUi.BodySize,GardenUi.Muted);
                DockAction("그때의 활동과 감정 보기",()=>ShowRecord(linked.SessionId),true);
            }
            DockAction("성장 기록",ShowGrowthHistory);
        }
        public void ShowRecordSummary()
        {
            EnterPage("record-summary",ShowRecordSummary);var p=FocusPage("이번 주와 지금까지","나의 기록");
            var state=Service.Snapshot;var now=Service.CurrentUtc;
            var local=TimeZoneInfo.ConvertTime(now,Service.CurrentTimeZone).Date;
            var monday=DateTime.SpecifyKind(local.AddDays(-((int)local.DayOfWeek+6)%7),DateTimeKind.Unspecified);
            while(Service.CurrentTimeZone.IsInvalidTime(monday))monday=monday.AddMinutes(1);
            var start=new DateTimeOffset(monday,Service.CurrentTimeZone.GetUtcOffset(monday));
            RenderSummaryGroup(p,"이번 주 · "+monday.ToString("M/d")+"부터",GardenRecordSummary.Calculate(state,start,now.AddTicks(1)));
            RenderSummaryGroup(p,"지금까지",GardenRecordSummary.Calculate(state,DateTimeOffset.MinValue,now.AddTicks(1)));
            GardenUi.FlowLabel(p,"복습도 참여 횟수에 포함돼요. 빈도는 남긴 기록의 수이며, 마음 상태의 좋고 나쁨을 평가하지 않아요.",GardenUi.BodySize,GardenUi.Muted);
            DockAction("참여 기록",ShowGardenJournal);
        }
        static void RenderSummaryGroup(Transform parent,string title,GardenRecordSummary summary)
        {
            GardenTypography.SetWeight(GardenUi.FlowLabel(parent,title,GardenUi.HeadingSize),true);
            GardenUi.FlowLabel(parent,"활동 완료 "+summary.Completed+"회 · 참여 종료 "+summary.Ended+"회");
            GardenUi.FlowLabel(parent,"경험 기록 "+summary.ExperienceTypes.Values.Sum()+"개",GardenUi.BodySize,GardenUi.Muted);
            foreach(string type in new[]{"즐거움","불편함","중립","잘 모르겠음"})
                if(summary.ExperienceTypes.TryGetValue(type,out int count))GardenUi.FlowLabel(parent,type+" · "+count+"개");
        }
    }
}
