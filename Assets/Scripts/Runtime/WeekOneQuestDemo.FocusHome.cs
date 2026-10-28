using System;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using CapstoneDesign.Runtime.LocalState;

namespace CapstoneDesign.Runtime
{
    public sealed partial class WeekOneQuestDemo
    {
        public void ShowHome()=>ShowFocusHome();
        public void ShowCourse()=>ShowFocusCourse();
        public void ShowFree()=>ShowFocusRecommendation();
        public void ShowProgramSelection()=>ShowFocusCourse();
        public void ShowRecords()=>ShowFocusHistory();
        public void ShowRecord(string id)=>ShowFocusRecord(id);

        public void ShowFocusHome()
        {
            if(opened&&!SaveActivePracticeDraft())return;ClearPracticeDraft();
            var p=BeginFigmaPage("home",ShowFocusHome,FigmaMissionUi.Background,true);FigmaHeader(p,"미션","오늘은 한 가지부터, 천천히.");
            if(!opened){FigmaMissionUi.Label(p,"저장된 내용을 불러오지 못했어요.\n기존 기록을 보존하고 있습니다.",24,170,342,100);return;}
            var s=Service.Snapshot;var active=FocusMissionPolicy.Active(s,FocusMissionCatalog.BasicRole);var extra=FocusMissionPolicy.Active(s,FocusMissionCatalog.AdditionalRole);
            string unavailable=FocusMissionPolicy.Availability(s);bool finished=FocusMissionPolicy.CompletedCount(s)>=12;
            bool doneToday=FocusMissionPolicy.Completed(s).Any(a=>a.InstanceId==s.LastCycleId);
            bool waitingForWeek=!finished&&!doneToday&&unavailable!=null;
            var body=FigmaScroll(p,132,628,628);
            var card=FigmaMissionUi.Box(body,"기본 미션",24,8,342,252,FigmaMissionUi.Green,24);
            FigmaMissionUi.Asset(card,"FocusMissions/Main/basic-leaf",24,24,24,24);FigmaMissionUi.Label(card,"MBCT 과정",56,24,238,24,14,Color.white,true);
            FigmaMissionUi.Label(card,"기본 미션",24,61,294,32,22,Color.white,true);
            FigmaMissionUi.Label(card,finished?"차곡차곡 쌓은 실천을\n내 과정에서 돌아봐요.":active!=null?"진행하던 단계부터\n편하게 이어가요.":waitingForWeek?"이번 주 실습을 모두 마쳤어요.\n다음 주에 다시 이어가요.":doneToday?"오늘의 실천을 마쳤어요.\n내일 다시 이어가요.":"오늘의 순서에 따라\n차근차근 이어가요.",24,108,294,58,16,Color.white);
            var basic=FigmaMissionUi.Button(card,active!=null?"기본 미션 이어서 하기":finished?"과정 완료":waitingForWeek?"다음 주에 이어가요":doneToday?"오늘 완료했어요":"기본 미션 시작하기",24,176,294,52,StartFocusBasic,Color.white,FigmaMissionUi.Green);basic.interactable=active!=null||(!finished&&unavailable==null);
            FigmaMissionUi.Label(body,"추가 미션",24,284,240,27,18,null,true);FigmaMissionUi.Label(body,"선택 활동",264,284,102,27,14,FigmaMissionUi.Muted,false,TextAnchor.MiddleRight);
            var additional=FigmaMissionUi.Box(body,"추가 추천 미션",24,322,342,202,FigmaMissionUi.Warm,24);
            FigmaMissionUi.Asset(additional,"FocusMissions/Main/spark",20,20,24,24);FigmaMissionUi.Label(additional,"나를 위한 추천",52,20,260,24,14,FigmaMissionUi.Brown,true);
            FigmaMissionUi.Label(additional,"물 마시기",20,57,302,28,18,null,true);FigmaMissionUi.Label(additional,"물 한 잔 · 원하는 날만 가볍게",20,95,302,24,14,FigmaMissionUi.Brown);
            bool extraDone=FocusMissionPolicy.AdditionalCompletedToday(s);
            var rec=FigmaMissionUi.Button(additional,extra!=null?"추가 미션 이어서 하기":extraDone?"오늘 완료했어요":"추천 미션 보기",20,130,302,52,extra!=null?(Action)(()=>SelectFocusSession(extra.SessionId)):ShowFocusRecommendation,FigmaMissionUi.Brown);
            rec.interactable=extra!=null||!extraDone;
        }
        public void StartFocusBasic()
        {
            if(!opened||!SaveActivePracticeDraft()||!RefreshCycle())return;
            var s=Service.Snapshot;var active=FocusMissionPolicy.Active(s,FocusMissionCatalog.BasicRole);
            if(active!=null){SelectFocusSession(active.SessionId);return;}
            int order=FocusMissionPolicy.NextOrder(s);var d=FocusMissionPolicy.MissionAt(order);if(d==null){ShowFocusCourse();return;}
            BeginFocus(d.Id,order,false);
        }
        public void StartFocusAdditional()
        {
            if(!opened||!SaveActivePracticeDraft()||!RefreshCycle())return;
            var active=FocusMissionPolicy.Active(Service.Snapshot,FocusMissionCatalog.AdditionalRole);
            if(active!=null){SelectFocusSession(active.SessionId);return;}BeginFocus("free-mission:M03",0,false);
        }
        void BeginFocus(string mission,int order,bool review)
        {
            string id=Guid.NewGuid().ToString("N");if(!Apply(Service.BeginFocusSession(id,mission,order,review))){ShowFocusHome();return;}
            sessionId=id;ClearPracticeDraft();ShowFocusPractice();
        }
        public void SelectFocusSession(string id)
        {
            if(!SaveActivePracticeDraft())return;sessionId=id;var s=Service.Snapshot.Sessions.FirstOrDefault(x=>x.SessionId==id);
            if(s==null){ShowFocusHome();return;}if(s.Status==ParticipationState.Paused&&!Apply(Service.ResumeSession(id)))return;ShowFocusPractice();
        }
        public void StartFocusReview(string missionId,int order)
        {
            if(!SaveActivePracticeDraft())return;
            var definition=FocusMissionCatalog.Find(missionId);
            if(definition!=null && FocusMissionPolicy.Active(Service.Snapshot,definition.Role)!=null)
            {
                if(notice!=null){notice.text="진행 중인 "+(definition.Role==FocusMissionCatalog.BasicRole?"기본":"추가")+" 미션을 마치면 복습할 수 있어요.";notice.gameObject.SetActive(true);notice.transform.SetAsLastSibling();}
                return;
            }
            BeginFocus(missionId,order,true);
        }
        public void ShowFocusRecommendation()
        {
            if(!opened||!SaveActivePracticeDraft())return;
            var p=BeginFigmaPage("focus-recommendation",ShowFocusRecommendation,FigmaMissionUi.Background);FigmaHeader(p,"추가 미션","원하는 날, 나에게 맞는 만큼.");
            var box=FigmaMissionUi.Box(p,"추가 미션 안내",24,140,342,266,FigmaMissionUi.Warm,24);
            FigmaMissionUi.Label(box,"가벼운 실천",28,28,270,24,14,FigmaMissionUi.Brown,true);FigmaMissionUi.Asset(box,"FocusMissions/Main/spark",24,72,32,32);
            FigmaMissionUi.Label(box,"물 마시기",24,124,294,32,22,null,true);FigmaMissionUi.Label(box,"물 한 잔만 준비해요.",24,165,294,25,14,FigmaMissionUi.Brown);
            FigmaMissionUi.Label(box,"물을 마신 뒤 물방울을 눌러\n오늘의 작은 실천을 남겨요.",24,200,294,58,16);
            var why=FigmaMissionUi.Box(p,"추천 이유",24,430,342,124,Color.white,24);FigmaMissionUi.Label(why,"이 미션을 추천한 이유",20,20,302,24,14,FigmaMissionUi.Brown,true);FigmaMissionUi.Label(why,"일상에서 짧게 실천할 수 있는\n추가 미션이에요.",20,55,302,55,16);
            FigmaMissionUi.Label(p,"추가 미션은 선택이에요.\n기본 과정의 진도에는 반영되지 않아요.",24,578,342,56,14,FigmaMissionUi.Muted);
            FigmaMissionUi.Button(p,"‹",24,768,48,52,ShowHome,Color.clear,FigmaMissionUi.Brown,30);
            FigmaMissionUi.Button(p,"추가 미션 시작하기",84,768,282,52,StartFocusAdditional,FigmaMissionUi.Brown);
        }
        public void ShowFocusCourse()
        {
            if(!opened||!SaveActivePracticeDraft())return;
            var s=Service.Snapshot;int done=FocusMissionPolicy.CompletedCount(s),next=FocusMissionPolicy.NextOrder(s);var active=FocusMissionPolicy.Active(s,FocusMissionCatalog.BasicRole);var d=active==null?FocusMissionPolicy.MissionAt(next):FocusMissionCatalog.Find(active.MissionId);
            var p=BeginFigmaPage("course",ShowFocusCourse,FigmaMissionUi.Background,true,"내 과정");FigmaHeader(p,"내 과정","내가 어디쯤 왔는지 확인해요.");var body=FigmaScroll(p,132,628,860);
            var summary=FigmaMissionUi.Box(body,"과정 요약",24,8,342,116,Color.white,24);FigmaMissionUi.Asset(summary,"FocusMissions/Main/course-book",20,24,24,24);FigmaMissionUi.Label(summary,"MBCT 2주 과정",52,20,270,30,18,null,true);FigmaMissionUi.Label(summary,"완료한 실습 "+done+" / 12",20,60,302,25,14,FigmaMissionUi.Muted);FigmaMissionUi.Box(summary,"진도",20,92,302,6,FigmaMissionUi.Border,3);if(done>0)FigmaMissionUi.Box(summary,"완료 진도",20,92,302*done/12f,6,FigmaMissionUi.Green,3);
            var current=FigmaMissionUi.Box(body,"현재 위치",24,148,342,250,FigmaMissionUi.Soft,24);
            FigmaMissionUi.Label(current,done==12?"과정 완료":"현재 "+Math.Min(2,(next-1)/6+1)+"주차",28,28,286,24,14,FigmaMissionUi.Green,true);
            FigmaMissionUi.Label(current,done==12?"나의 실천 돌아보기":"일상에서 주의 기울이기",20,70,302,30,18,null,true);
            FigmaMissionUi.Label(current,d==null?"열두 번의 실습을 마쳤어요.":"현재 미션 · "+d.Code+" "+d.Title,20,111,302,52,16);
            FigmaMissionUi.Label(current,active==null?(done==12?"기록은 계속 볼 수 있어요.":"시작 전 · 내 속도로"):(active.Status==ParticipationState.Selected?"시작 전":"저장된 단계 "+(Math.Min(active.InstructionStep,d.Steps.Length)+1)+" · 이어하기 가능"),20,165,302,24,14,FigmaMissionUi.Muted);
            FigmaMissionUi.Button(current,"미션 창으로 돌아가기",20,194,302,52,ShowHome,Color.white,FigmaMissionUi.Green);
            FigmaMissionUi.Label(body,"전체 과정",24,422,240,28,18,null,true);FigmaMissionUi.Label(body,"2주",264,422,102,28,14,FigmaMissionUi.Muted,false,TextAnchor.MiddleRight);
            for(int w=1;w<=2;w++){int week=w;int count=FocusMissionPolicy.Completed(s).Count(a=>(a.FocusOrder-1)/6+1==week);var row=FigmaMissionUi.Button(body,"",24,464+(w-1)*82,342,74,()=>ShowFocusWeek(week),next>=((w-1)*6+1)?FigmaMissionUi.Soft:Color.white,FigmaMissionUi.Ink);FigmaMissionUi.Label(row.transform,w+"주",12,23,42,28,14,FigmaMissionUi.Green,true,TextAnchor.MiddleCenter);FigmaMissionUi.Label(row.transform,"한입 · 숨 · 말하기",56,12,274,27,16);FigmaMissionUi.Label(row.transform,count+" / 6 완료",56,42,274,24,14,FigmaMissionUi.Green);}
            FigmaMissionUi.Button(body,"저장된 기록 보기",24,642,342,52,()=>ShowFocusHistory(),Color.white,FigmaMissionUi.Green);
            FigmaMissionUi.Label(body,"기본 미션과 추가 미션의 수행 기록을\n회차별로 다시 볼 수 있어요.",24,712,342,52,14,FigmaMissionUi.Muted);
        }
        public void ShowFocusWeek(int week)
        {
            if(!opened||!SaveActivePracticeDraft())return;week=Math.Max(1,Math.Min(2,week));int selectedWeek=week;var s=Service.Snapshot;
            var p=BeginFigmaPage("focus-week",()=>ShowFocusWeek(selectedWeek),FigmaMissionUi.Background,true,"내 과정");FigmaHeader(p,week+"주차 미션","한입 · 숨 · 말하기");var body=FigmaScroll(p,132,628,800);
            var info=FigmaMissionUi.Box(body,"과정 안내",24,8,342,100,FigmaMissionUi.Soft,24);FigmaMissionUi.Label(info,"한 번에 하나씩 이어가요.",20,20,302,28,18,null,true);FigmaMissionUi.Label(info,"미션은 미션 창에서 시작할 수 있어요.",20,58,302,26,14,FigmaMissionUi.Muted);
            int next=FocusMissionPolicy.NextOrder(s);for(int i=0;i<6;i++){int order=(week-1)*6+i+1;var d=FocusMissionPolicy.MissionAt(order);bool complete=order<next;var row=FigmaMissionUi.Button(body,"",24,132+i*90,342,74,()=>ShowFocusHistory(d.Id,order),order==next?FigmaMissionUi.Soft:Color.white,FigmaMissionUi.Ink);FigmaMissionUi.Label(row.transform,order.ToString("00"),12,22,34,28,14,FigmaMissionUi.Green,true,TextAnchor.MiddleCenter);FigmaMissionUi.Label(row.transform,d.Code+" · "+d.Title,56,12,270,30,15);FigmaMissionUi.Label(row.transform,complete?"완료 · 기록 보기":order==next?"현재 미션 · 기록 확인":"이후 순서 · 정보 보기",56,44,270,24,14,FigmaMissionUi.Muted);}
            FigmaMissionUi.Button(body,"내 과정으로 돌아가기",24,688,342,52,ShowFocusCourse,Color.white,FigmaMissionUi.Green);
        }
        public void ShowFocusHistory(string missionId=null,int order=0)
        {
            if(!opened||!SaveActivePracticeDraft())return;var s=Service.Snapshot;var def=FocusMissionCatalog.Find(missionId);
            var list=s.Sessions.Where(a=>(missionId==null||a.MissionId==(def?.Id??missionId))&&(order==0||a.FocusOrder==order)).OrderByDescending(a=>a.SelectedUtc,StringComparer.Ordinal).ToArray();
            var p=BeginFigmaPage("focus-history",()=>ShowFocusHistory(missionId,order),FigmaMissionUi.Background,true,"내 과정");FigmaHeader(p,def==null?"저장된 기록":def.Code+" 수행 기록",def?.Title??"기본 미션과 추가 미션을 돌아봐요.");var body=FigmaScroll(p,132,628,Mathf.Max(620,140+list.Length*100));
            if(list.Length==0){FigmaMissionUi.Label(body,"아직 저장된 수행 기록이 없어요.\n미션 창에서 실습한 뒤 돌아와 주세요.",24,40,342,80,16,FigmaMissionUi.Muted);}
            for(int i=0;i<list.Length;i++){var entry=list[i];bool focus=FocusMissionPolicy.IsFocus(entry);var row=FigmaMissionUi.Button(body,"",24,8+i*100,342,88,()=>ShowFocusRecord(entry.SessionId),Color.white,FigmaMissionUi.Ink);FigmaMissionUi.Label(row.transform,entry.MissionTitleSnapshot??MindfulnessContent.FindMission(entry.MissionId)?.title??entry.MissionId,16,12,310,28,16,null,true);FigmaMissionUi.Label(row.transform,(focus?(entry.FocusRole=="basic"?"기본 "+entry.FocusOrder+"회":"추가")+" · ":"이전 기록 · ")+LocalTime(entry.EndedUtc??entry.SelectedUtc),16,42,310,21,13,FigmaMissionUi.Muted);FigmaMissionUi.Label(row.transform,StateLabel(entry.Status)+(entry.FocusReview?" · 복습":""),16,64,310,20,12,FigmaMissionUi.Green);}
            FigmaMissionUi.Button(body,"내 과정으로 돌아가기",24,Mathf.Max(180,20+list.Length*100),342,52,ShowFocusCourse,Color.white,FigmaMissionUi.Green);
        }
        public void ShowFocusRecord(string id)
        {
            if(!opened||!SaveActivePracticeDraft())return;var a=Service.Snapshot.Sessions.FirstOrDefault(s=>s.SessionId==id);if(a==null){ShowFocusHistory();return;}
            bool focus=FocusMissionPolicy.IsFocus(a);var p=BeginFigmaPage("focus-record",()=>ShowFocusRecord(id),FigmaMissionUi.Background,true,"내 과정");FigmaHeader(p,"미션 기록",a.MissionTitleSnapshot??MindfulnessContent.FindMission(a.MissionId)?.title??a.MissionId);
            var parts=new System.Collections.Generic.List<string>{(focus?(a.FocusRole=="basic"?"기본 미션 · "+a.FocusOrder+"회":"추가 미션") : "이전 미션")+" · "+StateLabel(a.Status),"시작 · "+LocalTime(a.StartedUtc)+"\n마침 · "+(a.EndedUtc==null?"진행 중":LocalTime(a.EndedUtc))};
            if(focus)foreach(var step in a.FocusStepRecords.OrderBy(x=>x.Step))parts.Add((step.Step+1)+". "+step.Title+"\n"+step.Prompt+"\n"+(string.IsNullOrEmpty(step.ConfirmedUtc)?"작성 중":"확인 · "+LocalTime(step.ConfirmedUtc))+"\n"+(step.Skipped?"입력 건너뜀":string.IsNullOrEmpty(step.Value)?"행동 확인":step.Value));
            else foreach(var answer in a.Answers??new System.Collections.Generic.List<PracticeAnswer>())parts.Add(AnswerLabel(answer.Key)+"\n"+answer.Value);
            parts.Add("감정 · "+(string.IsNullOrEmpty(a.Mood)?"선택하지 않음":a.Mood));parts.Add("한 줄 기록\n"+(string.IsNullOrEmpty(a.Note)?"남긴 기록 없음":a.Note));if(focus&&a.Status==ParticipationState.RewardCommitted)parts.Add("받은 영양제 · "+a.RewardAmount+(a.FocusReview?" (복습)":""));
            var body=FigmaScroll(p,132,628,1100);float y=8;
            foreach(var text in parts){float h=Mathf.Max(64,28+Mathf.Ceil(text.Length/23f)*23+(text.Count(c=>c=='\n')*15));var card=FigmaMissionUi.Box(body,"기록 항목",24,y,342,h,Color.white,16);var label=FigmaMissionUi.Label(card,text,16,14,310,h-28,15);float measured=Mathf.Max(h,label.preferredHeight+28);card.sizeDelta=new Vector2(342,measured);label.rectTransform.sizeDelta=new Vector2(310,measured-28);y+=measured+12;}
            if(focus&&a.Status==ParticipationState.RewardCommitted){FigmaMissionUi.Button(body,"다시 연습하기 · 추가 보상 없음",24,y,342,52,()=>StartFocusReview(a.MissionId,a.FocusOrder),Color.white,FigmaMissionUi.Green,14);y+=64;}
            FigmaMissionUi.Button(body,"기록 목록",24,y,342,52,()=>ShowFocusHistory(focus?a.MissionId:null),Color.white,FigmaMissionUi.Green);y+=76;
            body.sizeDelta=new Vector2(390,Mathf.Max(628,y));
        }
    }
}
