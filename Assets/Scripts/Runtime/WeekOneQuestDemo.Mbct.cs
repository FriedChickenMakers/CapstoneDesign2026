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
        InputField practiceInput;
        string practiceKey, practiceSession;
        int practiceStep;
        string experienceId, experienceType;
        int experienceStep;
        int experiencePage;
        readonly string[] experienceValues=new string[4];
        readonly string[] experienceQuestions={"무슨 일이 있었나요?","몸에서는 무엇이 느껴졌나요?","어떤 감정이 있었나요?","어떤 생각이 떠올랐나요?"};

        void ShowLegacyProgramSelection()
        {
            if(!opened)return;
            EnterPage("programs",ShowProgramSelection);var p=FocusPage("내게 맞는 기간","마음챙김 활동");
            var state=Service.Snapshot;
            Guidance(p,"2주와 4주도 처음부터 마무리까지 이어지는 활동이에요. 하루에 하나씩, 쉬는 날은 자유롭게 가져요.");
            if(state.Sessions.Any(s=>s.Status==ParticipationState.Selected || s.Status==ParticipationState.InProgress || s.Status==ParticipationState.Paused))
            {
                GardenUi.FlowLabel(p,"진행 중인 활동을 마치거나 종료한 뒤 기간을 바꿀 수 있어요.");
                DockAction("활동으로",ShowHome,true);return;
            }
            var programs=ActivityList(p,"Course programs");
            foreach(string program in new[]{"2w","4w","8w"})
            {
                string id=program;
                string title=id=="2w"?"2주 · 가볍게 시작":id=="4w"?"4주 · 차근차근 익히기":"8주 · 충분히 연습하기";
                string detail=MbctPolicy.PracticeCount(id)+"개 활동";
                int completed=MbctPolicy.CompletedIds(state,id).Count;
                if(completed>0)detail+=" · "+completed+"개 완료";
                ActivityListItem(programs,title,detail,()=>ShowProgramConfirmation(id),PrototypeUiIcon.Symbol.Leaf,id!="8w");
            }
            GardenUi.FlowLabel(p,"어느 기간이든 감각 관찰부터 나를 돌보는 계획과 다시 시작하는 방법까지 배워요.",GardenUi.BodySize,GardenUi.Muted);
            if(state.MbctProgramChosen)DockAction("현재 활동으로",ShowHome);
            else DockAction("정원으로",GoGarden);
        }
        void ShowProgramConfirmation(string program)
        {
            var state=Service.Snapshot;int weeks=MbctPolicy.ProgramWeeks(program),completed=MbctPolicy.CompletedIds(state,program).Count;
            EnterPage("program-confirm",()=>ShowProgramConfirmation(program),program);
            var p=FocusPage(weeks+"주 마음챙김",completed>0?completed+"개 활동을 마쳤어요":"하루 한 가지, 내 속도로");
            Guidance(p,program=="2w"?"감각과 몸을 살펴보고, 생각과 거리를 두는 연습을 해요. 마지막에는 나를 돕는 계획과 실습을 이어갈 방법을 정해요.":
                program=="4w"?"몸과 일상에서 시작해 생각을 바라보는 연습을 넓혀요. 나의 변화 신호, 도움을 받을 곳과 유지 계획까지 차근차근 다뤄요.":
                "같은 원리를 다양한 상황에서 반복하며 익혀요. 8주 동안 몸과 생각을 관찰하고 나를 돌보는 계획을 이어가요.");
            GardenUi.FlowLabel(p,MbctPolicy.PracticeCount(program)+"개 활동 · 주 최대 6개\n기간은 권장 일정이에요. 놓친 날에도 진도는 그대로예요.");
            GardenUi.FlowLabel(p,"기간을 바꿔도 지금까지의 기록과 정원은 남아요. 돌아오면 멈춘 진도부터 이어갈 수 있어요.",GardenUi.BodySize,GardenUi.Muted);
            GardenUi.FlowLabel(p,"처음 마친 활동마다 영양제 "+Balance.CompletionNutrient+"개를 받아요. 기간 변경과 복습에는 추가 보상이 없어요.",GardenUi.BodySize,GardenUi.Muted);
            DockAction(completed>=MbctPolicy.PracticeCount(program)?"완료한 코스 보기":completed>0?"이어서 하기":"이 기간으로 시작",()=>
            {
                if(Apply(Service.SelectMbctProgram(program))) { coursePageInitialized=false;freeIndex=0;ShowHome(); }
            },true);
            DockAction("다른 기간 보기",ShowProgramSelection);
        }

        string Remembered(ActivitySession session,string key)
        {
            return session?.Answers.FirstOrDefault(a=>a.Key==key)?.Value
                ?? Service.Snapshot.PracticePreferences.FirstOrDefault(a=>a.Key==key)?.Value ?? "";
        }
        bool SaveActivePracticeDraft()
        {
            if(!SaveFocusDraft())return false;
            if(practiceInput==null || practiceKey==null || practiceSession==null)return true;
            var session=Service.Snapshot.Sessions.FirstOrDefault(s=>s.SessionId==practiceSession);
            if(session?.Status!=ParticipationState.InProgress || session.InstructionStep!=practiceStep)return true;
            return Apply(Service.SavePracticeStep(practiceSession,practiceStep,practiceKey,practiceInput.text));
        }
        void ClearPracticeDraft() { practiceInput=null;practiceKey=null;practiceSession=null; }
        void ShowMbctSession(ActivitySession session,MissionDefinition mission,Transform p)
        {
            ClearPracticeDraft();
            if(session.Status==ParticipationState.Selected)
            {
                Guidance(p,mission.purpose);
                GardenTypography.SetWeight(GardenUi.FlowLabel(p,"권장 "+mission.suggestedDurationLabel,GardenUi.BodySize),true);
                GardenUi.FlowLabel(p,"시간과 입력은 완료 조건이 아니에요.",GardenUi.BodySize,GardenUi.Muted);
                DockAction("시작",()=>{if(Apply(Service.StartSession(session.SessionId)))ShowSession();},true);
                DockAction("선택 취소",()=>CancelSelectedPractice(session));
                return;
            }
            if(session.Status==ParticipationState.Paused)
            {
                Guidance(p,"진행 내용은 저장됐어요. 준비되면 쉬던 단계부터 이어서 할 수 있어요.");
                GardenUi.FlowLabel(p,"여기서 활동을 종료하면 완료 보상 없이 참여 기록만 남아요.",GardenUi.BodySize,GardenUi.Muted);
                DockAction("이어서 하기",()=>{if(Apply(Service.ResumeSession(session.SessionId)))ShowSession();},true);
                DockAction("오늘은 여기까지",()=>EndPractice(session));
                return;
            }
            if(session.InstructionStep>=mission.steps.Length){ShowReflection();return;}
            int index=session.InstructionStep;var step=mission.steps[index];
            activityEyebrow.text=(index+1)+" / "+mission.steps.Length+" · "+step.title;
            Guidance(p,step.guidance);
            if(step.answerKey!=null)
            {
                practiceSession=session.SessionId;practiceStep=index;practiceKey=step.answerKey;
                var choices=new System.Collections.Generic.List<Button>();
                for(int i=0;i<step.options.Length;i+=2)
                {
                    var row=GardenUi.Box(p,"Practice choices",0,0,1,0);
                    var flow=row.AddComponent<HorizontalLayoutGroup>();flow.spacing=16;
                    flow.childControlWidth=flow.childControlHeight=true;flow.childForceExpandWidth=true;
                    for(int j=i;j<Math.Min(i+2,step.options.Length);j++)
                    {
                        string option=step.options[j];
                        var choice=GardenUi.Button(row.transform,option,0,0,1,1,()=>
                        {
                            practiceInput.text=option;
                            foreach(var button in choices)GardenUi.StyleButton(button,selected:button.name==option);
                            SaveActivePracticeDraft();
                        });
                        choice.gameObject.AddComponent<GardenFlowButton>();
                        choices.Add(choice);
                    }
                }
                practiceInput=GardenUi.FlowInput(p,"선택 입력 · 직접 적어도 좋아요");
                practiceInput.text=Remembered(session,step.answerKey);
                practiceInput.onEndEdit.AddListener(_=>SaveActivePracticeDraft());
                foreach(var choice in choices)GardenUi.StyleButton(choice,selected:practiceInput.text==choice.name);
                practiceInput.onValueChanged.AddListener(value=>
                {foreach(var choice in choices)GardenUi.StyleButton(choice,selected:choice.name==value);});
                DockPair("입력 건너뛰기",()=>AdvancePractice(true),index==mission.steps.Length-1?"실습 마치기":"다음",()=>AdvancePractice(false),true);
            }
            else DockAction(index==mission.steps.Length-1?"실습 마치기":"다음",()=>AdvancePractice(false),true);
            if(index>0)DockPair("이전",()=>PreviousPractice(session),"잠시 쉬기",()=>PausePractice(session));
            else DockAction("잠시 쉬기",()=>PausePractice(session));
        }
        public void AdvancePractice(bool skipInput)
        {
            var session=ActiveSession();var mission=MindfulnessContent.FindMission(session.MissionId);
            var step=mission.steps[session.InstructionStep];
            bool saved=skipInput && step.answerKey!=null
                ? Service.SkipPracticeStep(session.SessionId,session.InstructionStep+1,step.answerKey)
                : Service.SavePracticeStep(session.SessionId,session.InstructionStep+1,step.answerKey,
                    practiceInput==null?(step.answerKey==null?null:Remembered(session,step.answerKey)):practiceInput.text);
            if(!Apply(saved))return;
            ClearPracticeDraft();ShowSession();
        }
        public void ShowPracticeAnswers(string id)
        {
            EnterPage("answers",()=>ShowPracticeAnswers(id),id);var session=Service.Snapshot.Sessions.First(s=>s.SessionId==id);var p=FocusPage("실습에서 적은 내용","활동 기록");
            GardenUi.FlowLabel(p,session.Answers.Count==0?"입력을 건너뛰었어요.":string.Join("\n\n",session.Answers.Select(a=>AnswerLabel(a.Key)+"\n"+a.Value)));
            DockAction("기록으로",()=>ShowRecord(id));
        }
        public void ShowExperienceTypes()
        {
            EnterPage("experience-type",ShowExperienceTypes);var p=FocusPage("경험 알아차리기","선택 기록");
            Guidance(p,"최근 경험 하나를 살펴보세요. 감정이나 기록의 내용에 정답은 없어요.");
            GardenTypography.SetWeight(GardenUi.FlowLabel(p,"어떤 경험에 가까웠나요?",GardenUi.HeadingSize),true);
            var types=new[]{"즐거움","불편함","중립","잘 모르겠음"};
            for(int i=0;i<types.Length;i++)
            {
                string type=types[i];var button=GardenUi.Button(p,type,0,0,1,0,()=>
                    {experienceId=Guid.NewGuid().ToString("N");experienceType=type;experienceStep=0;Array.Clear(experienceValues,0,4);ShowExperienceStep();});
                button.gameObject.AddComponent<GardenFlowButton>();
            }
            DockAction("기록 목록",ShowRecords);
        }
        public void ShowExperienceStep()
        {
            EnterPage("experience",ShowExperienceStep);
            var p=FocusPage(experienceQuestions[experienceStep],"경험 돌아보기 · "+experienceType+" · "+(experienceStep+1)+" / 4");
            Guidance(p,experienceStep==0?"가벼운 경험부터 적어보세요. 떠오르는 것이 없으면 건너뛰어도 괜찮아요.":experienceStep==1?"압력, 온도, 긴장 등 느껴진 감각을 적어보세요. 잘 몰라도 괜찮아요.":experienceStep==2?"기존 감정 선택을 쓰거나 직접 적어보세요.":"그때 떠오른 생각을 적어보세요. 해결하거나 분석하지 않아도 돼요.");
            var field=GardenUi.FlowInput(p,"선택 기록 · 기기에 저장");field.text=experienceValues[experienceStep]??"";
            field.onValueChanged.AddListener(value=>experienceValues[experienceStep]=value);
            if(experienceStep==2)
            {
                var row=GardenUi.Box(p,"Experience emotions",0,0,1,0);
                row.AddComponent<LayoutElement>().preferredHeight=208;
                row.transform.SetSiblingIndex(field.transform.GetSiblingIndex());
                var picker=GardenUi.MoodPicker(row.transform,0,0,1,1,field.text,value=>field.text=value??"");
                field.onValueChanged.AddListener(picker.SetValue);
            }
            DockPair("건너뛰기",()=>AdvanceExperience(true),experienceStep==3?"기록 저장":"다음",()=>AdvanceExperience(false),true);
            if(experienceStep>0)DockPair("이전",()=>{experienceStep--;ShowExperienceStep();},"기록 취소",ShowRecords);
            else DockAction("기록 취소",ShowRecords);
        }
        void AdvanceExperience(bool skip)
        {
            if(skip)experienceValues[experienceStep]="";
            if(experienceStep<3){experienceStep++;ShowExperienceStep();return;}
            if(Apply(Service.SaveExperience(experienceId,experienceType,experienceValues[0],experienceValues[1],experienceValues[2],experienceValues[3])))FinishExperienceNavigation();
        }
        public void ShowExperienceHistory()
        {
            EnterPage("experience-history",ShowExperienceHistory);var p=FocusPage("경험 기록","나의 기록");
            var state=Service.Snapshot;
            int pages=Math.Max(1,(state.Experiences.Count+3)/4);
            experiencePage=Math.Min(experiencePage,pages-1);
            var records=state.Experiences.AsEnumerable().Reverse().Skip(experiencePage*4).Take(4).ToArray();
            var history=records.Length>0?ActivityList(p,"Experience history"):null;
            for(int i=0;i<records.Length;i++)
            {var e=records[i];ActivityListItem(history,e.Type,LocalTime(e.CreatedUtc),()=>ShowExperienceRecord(e.Id),PrototypeUiIcon.Symbol.Journal);}
            if(records.Length==0)Guidance(p,"아직 경험 기록이 없어요. 기억하고 싶은 순간을 남겨보세요.");
            var destinations=ActivityList(p,"Experience destinations");
            ActivityListItem(destinations,"경험 기록하기",null,ShowExperienceTypes,PrototypeUiIcon.Symbol.Journal);
            ActivityListItem(destinations,"나의 계획",null,ShowPersonalPlan,PrototypeUiIcon.Symbol.Leaf);
            if(pages>1)
            {
                DockPair("이전 기록",()=>{experiencePage--;ShowExperienceHistory();},"다음 기록",()=>{experiencePage++;ShowExperienceHistory();});
                var buttons=activityActions.GetComponentsInChildren<Button>();
                buttons.First(b=>b.name=="이전 기록").interactable=experiencePage>0;
                buttons.First(b=>b.name=="다음 기록").interactable=experiencePage<pages-1;
            }
            DockAction("참여 기록",ShowRecords);
        }
        public void ShowExperienceRecord(string id)
        {
            EnterPage("experience-record",()=>ShowExperienceRecord(id),id);var e=Service.Snapshot.Experiences.First(r=>r.Id==id);var p=FocusPage("경험 · "+e.Type,"나의 기록");
            GardenUi.FlowLabel(p,"사건\n"+e.Event+"\n\n몸 감각\n"+e.Body+"\n\n감정\n"+e.Emotion+"\n\n생각\n"+e.Thought);
            DockAction("경험 목록",ShowExperienceHistory);
        }
        public void ShowPersonalPlan()
        {
            EnterPage("personal-plan",ShowPersonalPlan);var p=FocusPage("나의 계획","필요할 때 바꿀 수 있어요");
            string[] keys={"warning-sign","warning-action","support","favorite","practice-time","restart-plan"};
            var fields=ActivityList(p,"Plan fields");
            for(int i=0;i<keys.Length;i++)
            {
                string key=keys[i];ActivityListItem(fields,AnswerLabel(key),null,()=>ShowPlanField(key),PrototypeUiIcon.Symbol.Journal);
            }
            DockAction("기록으로",ShowExperienceHistory);
        }
        public void ShowPlanField(string key)
        {
            EnterPage("plan-field",()=>ShowPlanField(key),key);var p=FocusPage(AnswerLabel(key),"나의 계획");
            Guidance(p,"아는 만큼만 적고 나중에 바꿀 수 있어요. 입력은 선택이며 앱의 진단이나 자동 연락이 아니에요.");
            var field=GardenUi.FlowInput(p,"선택 입력",280);
            field.text=planDrafts.TryGetValue(key,out string draft)?draft:Service.Snapshot.PracticePreferences.FirstOrDefault(a=>a.Key==key)?.Value??"";
            field.onValueChanged.AddListener(value=>planDrafts[key]=value);
            DockAction("저장",()=>{if(Apply(Service.SavePreference(key,field.text))){planDrafts.Remove(key);ShowPersonalPlan();}},true);
            DockAction("돌아가기",ShowPersonalPlan);
        }
        static string AnswerLabel(string key)
        {
            switch(key)
            {
                case "routine":return "익숙한 행동";case "food":return "먹기 소재";case "movement":return "움직임";case "warning-sign":return "나의 변화 신호";
                case "warning-action":return "신호에 대한 대응";case "support":return "도움을 받을 곳";case "favorite":return "다시 할 실습";case "practice-time":return "실습할 때";
                case "restart-plan":return "다시 시작하는 방법";case "practice-reminder":return "알림 선호";case "energy-activity":return "최근 활동";case "energy-effect":return "활동 뒤 느낌";case "care-action":return "작은 돌봄 행동";
                case "case-mode":return "연습 사례";case "example-event":case "personal-event":return "관찰한 상황";case "example-thought":case "personal-thought":case "observed-thought":return "알아차린 생각";
                case "example-response":case "personal-response":return "몸과 감정";case "example-next-action":case "personal-next-action":return "다음 행동";default:return "실습 기록";
            }
        }
    }
}
