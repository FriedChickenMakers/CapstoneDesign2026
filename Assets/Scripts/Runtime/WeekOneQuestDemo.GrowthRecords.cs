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
            if(records.Length>0)GardenUi.FlowLabel(p,"남긴 성장 · "+records.Length+"개",GardenUi.CaptionSize,GardenUi.Muted);
            if(records.Length==0)Guidance(p,"이전에 남긴 성장 기록이 없어요. 새 활동은 참여 기록에서 돌아볼 수 있어요.");
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
            DockAction("사진첩",ShowPlantAlbum,true);
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
            var change=HistoryCard(p,"Growth change summary");
            GardenUi.Emblem(change,PrototypeUiIcon.Symbol.Leaf,88);
            HistorySection(change,"식물에 남은 변화");
            GardenUi.FlowLabel(change,GrowthRecordTitle(record),GardenUi.BodySize);
            var linked=state.Sessions.FirstOrDefault(session=>session.SessionId==record.LinkedSessionId);
            if(linked==null)Guidance(p,"이 성장에는 연결한 활동 기록이 없어요. 식물의 변화만 남겨두었어요.");
            else
            {
                var activity=HistoryCard(p,"Growth linked activity");
                GardenUi.FlowLabel(activity,"함께 기억하기로 고른 활동",GardenUi.CaptionSize,GardenUi.Muted);
                GardenTypography.SetWeight(GardenUi.FlowLabel(activity,FocusSessionTitle(linked),GardenUi.HeadingSize),true);
                GardenUi.FlowLabel(activity,LocalTime(linked.EndedUtc),GardenUi.BodySize,GardenUi.Muted);
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
            var card=HistoryCard(parent,"Record summary · "+title);
            HistorySection(card,title);
            SummaryCount(card,"활동 완료",summary.Completed,"회");
            SummaryCount(card,"참여 종료",summary.Ended,"회");
            SummaryCount(card,"경험 기록",summary.ExperienceTypes.Values.Sum(),"개");
            foreach(string type in new[]{"즐거움","불편함","중립","잘 모르겠음"})
                if(summary.ExperienceTypes.TryGetValue(type,out int count))
                    GardenUi.FlowLabel(card,type+" · "+count+"개",GardenUi.CaptionSize,GardenUi.Muted);
        }
        static void SummaryCount(Transform parent,string title,int value,string unit)
        {
            var row=GardenUi.Box(parent,"Summary count · "+title,0,0,1,0);
            var flow=row.AddComponent<HorizontalLayoutGroup>();flow.spacing=20;
            flow.childControlWidth=flow.childControlHeight=true;flow.childForceExpandHeight=false;flow.childForceExpandWidth=false;
            flow.childAlignment=TextAnchor.MiddleLeft;
            var label=GardenUi.FlowLabel(row.transform,title,GardenUi.BodySize);
            label.gameObject.AddComponent<LayoutElement>().flexibleWidth=1;
            var number=GardenUi.FlowLabel(row.transform,value+unit,GardenUi.HeadingSize);
            GardenTypography.SetWeight(number,true);number.alignment=TextAnchor.MiddleRight;
            var size=number.gameObject.AddComponent<LayoutElement>();size.minWidth=size.preferredWidth=140;
        }
    }
}
