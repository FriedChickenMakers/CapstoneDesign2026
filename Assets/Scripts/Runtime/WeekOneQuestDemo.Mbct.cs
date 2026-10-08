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

        string Remembered(ActivitySession session,string key)
        {
            return session?.Answers.FirstOrDefault(a=>a.Key==key)?.Value
                ?? Service.Snapshot.PracticePreferences.FirstOrDefault(a=>a.Key==key)?.Value ?? "";
        }
        bool SaveActivePracticeDraft()
        {
            if(practiceInput==null || practiceKey==null || practiceSession==null)return true;
            var session=Service.Snapshot.Sessions.FirstOrDefault(s=>s.SessionId==practiceSession);
            if(session?.Status!=ParticipationState.InProgress)return true;
            return Apply(Service.SavePracticeStep(practiceSession,practiceStep,practiceKey,practiceInput.text));
        }
        void ClearPracticeDraft() { practiceInput=null;practiceKey=null;practiceSession=null; }
        void ShowMbctSession(ActivitySession session,MissionDefinition mission,Transform p)
        {
            ClearPracticeDraft();
            GardenUi.Label(p,mission.purpose,.07f,.735f,.86f,.065f,22);
            if(session.Status==ParticipationState.Selected)
            {
                GardenUi.Label(p,"화면 안내를 따라 편한 만큼 참여해보세요.",.07f,.60f,.86f,.12f,28);
                GardenUi.Label(p,"권장 "+mission.suggestedDurationLabel+" · 시간은 완료 조건이 아니에요.\n입력은 선택이고 중간에 쉬어도 괜찮아요.",.07f,.43f,.86f,.15f,25);
                GardenUi.Button(p,"시작",.07f,.34f,.86f,.075f,()=>{if(Apply(Service.StartSession(session.SessionId)))ShowSession();},true);
                GardenUi.Button(p,"오늘은 여기까지",.07f,.24f,.86f,.075f,()=>{if(Apply(Service.EndParticipation(session.SessionId))){sessionId=null;ShowHome();}});
                GardenUi.Button(p,"목록으로",.07f,.15f,.86f,.075f,ShowHome);return;
            }
            if(session.Status==ParticipationState.Paused)
            {
                GardenUi.Label(p,"잠시 쉬는 중이에요. 이어서 하거나 여기서 마쳐도 괜찮아요.",.07f,.48f,.86f,.22f,28);
                GardenUi.Button(p,"이어서 하기",.07f,.34f,.86f,.075f,()=>{if(Apply(Service.ResumeSession(session.SessionId)))ShowSession();},true);
                GardenUi.Button(p,"오늘은 여기까지",.07f,.24f,.86f,.075f,()=>{if(Apply(Service.EndParticipation(session.SessionId))){sessionId=null;ShowHome();}});
                GardenUi.Button(p,"목록으로",.07f,.15f,.86f,.075f,ShowHome);return;
            }
            if(session.InstructionStep>=mission.steps.Length){ShowReflection();return;}
            int index=session.InstructionStep;var step=mission.steps[index];
            GardenUi.Label(p,(index+1)+" / "+mission.steps.Length+" · "+step.title,.07f,.66f,.86f,.065f,29);
            GardenUi.Card(p,"Practice guidance",.07f,.49f,.86f,.155f,GardenUi.Pale);
            GardenUi.Label(p,step.guidance,.10f,.505f,.80f,.125f,27);
            GardenUi.Progress(p,(index+1)/(float)mission.steps.Length,.07f,.652f,.86f,.004f);
            if(step.answerKey!=null)
            {
                practiceSession=session.SessionId;practiceStep=index;practiceKey=step.answerKey;
                practiceInput=GardenUi.Input(p,"선택하거나 직접 적기 · 건너뛰어도 괜찮아요",.07f,.22f,.86f,.095f);
                practiceInput.text=Remembered(session,step.answerKey);
                practiceInput.onEndEdit.AddListener(_=>SaveActivePracticeDraft());
                var choices=new System.Collections.Generic.List<Button>();
                for(int i=0;i<step.options.Length;i++)
                {
                    string option=step.options[i];int row=i/2,col=i%2;
                    var choice=GardenUi.Button(p,option,.07f+col*.44f,.405f-row*.0775f,.42f,.065f,()=>
                    {
                        practiceInput.text=option;
                        foreach(var button in choices)GardenUi.StyleButton(button,selected:button.name==option);
                        if(!SaveActivePracticeDraft())notice.text="저장하지 못했어요. 다시 시도해 주세요.";
                    });
                    choices.Add(choice);GardenUi.StyleButton(choice,selected:practiceInput.text==option);
                }
                practiceInput.onValueChanged.AddListener(value=>
                {
                    foreach(var choice in choices)GardenUi.StyleButton(choice,selected:choice.name==value);
                });
            }
            else GardenUi.Button(p,"잠시 쉬기",.07f,.30f,.86f,.065f,()=>{if(Apply(Service.PauseSession(session.SessionId)))ShowSession();});
            GardenUi.Button(p,index==mission.steps.Length-1?"실습 마치기":"다음",.52f,.135f,.41f,.07f,()=>AdvancePractice(false),true);
            GardenUi.Button(p,step.answerKey==null?"목록으로":"입력 건너뛰기",.07f,.135f,.41f,.07f,()=>{if(step.answerKey==null)ShowHome();else AdvancePractice(true);});
            if(index>0)GardenUi.Button(p,"이전",.07f,.06f,.26f,.055f,()=>
            {if(SaveActivePracticeDraft() && Apply(Service.SavePracticeStep(session.SessionId,index-1))){ClearPracticeDraft();ShowSession();}});
            if(step.answerKey!=null)GardenUi.Button(p,"잠시 쉬기",.37f,.06f,.26f,.055f,()=>{if(SaveActivePracticeDraft() && Apply(Service.PauseSession(session.SessionId))){ClearPracticeDraft();ShowSession();}});
            GardenUi.Button(p,"여기까지",.67f,.06f,.26f,.055f,()=>{if(SaveActivePracticeDraft() && Apply(Service.EndParticipation(session.SessionId))){ClearPracticeDraft();sessionId=null;ShowHome();}});
        }
        public void AdvancePractice(bool skipInput)
        {
            var session=ActiveSession();var mission=MindfulnessContent.FindMission(session.MissionId);
            var step=mission.steps[session.InstructionStep];
            if(!Apply(Service.SavePracticeStep(session.SessionId,session.InstructionStep+1,
                skipInput?null:step.answerKey,practiceInput==null?(step.answerKey==null?null:Remembered(session,step.answerKey)):practiceInput.text)))return;
            ClearPracticeDraft();ShowSession();
        }
        public void ShowPracticeAnswers(string id)
        {
            EnterPage("answers",()=>ShowPracticeAnswers(id),id);var session=Service.Snapshot.Sessions.First(s=>s.SessionId==id);var p=Page("실습에서 적은 내용");
            GardenUi.ScrollText(p,session.Answers.Count==0?"입력을 건너뛰었어요.":string.Join("\n\n",session.Answers.Select(a=>AnswerLabel(a.Key)+"\n"+a.Value)),.07f,.25f,.86f,.53f);
            GardenUi.Button(p,"기록으로",.07f,.15f,.86f,.075f,()=>ShowRecord(id));
        }
        public void ShowExperienceTypes()
        {
            EnterPage("experience-type",ShowExperienceTypes);var p=Page("경험 알아차리기");
            GardenUi.Icon(p,PrototypeUiIcon.Symbol.Journal,.10f,.63f,.09f,.09f);
            GardenUi.Label(p,"최근 경험 하나를 살펴보세요.\n감정이나 기록의 내용에 정답은 없어요.\n기록은 선택이며 보상과 관계없어요.",.22f,.57f,.68f,.20f,28);
            var types=new[]{"즐거움","불편함","중립","잘 모르겠음"};
            for(int i=0;i<types.Length;i++)
            {string type=types[i];var button=GardenUi.Button(p,type,.07f+(i%2)*.44f,.44f-(i/2)*.10f,.42f,.08f,()=>
                {experienceId=Guid.NewGuid().ToString("N");experienceType=type;experienceStep=0;Array.Clear(experienceValues,0,4);ShowExperienceStep();});
            }
            GardenUi.Button(p,"기록 목록",.07f,.15f,.86f,.075f,ShowRecords);
        }
        public void ShowExperienceStep()
        {
            EnterPage("experience",ShowExperienceStep);var p=Page("경험 돌아보기 · "+experienceType);
            GardenUi.Label(p,(experienceStep+1)+" / 4 · "+experienceQuestions[experienceStep],.07f,.68f,.86f,.09f,28);
            GardenUi.Label(p,experienceStep==0?"가벼운 경험부터 적어보세요. 떠오르는 것이 없으면 건너뛰어도 괜찮아요.":experienceStep==1?"압력, 온도, 긴장 등 느껴진 감각을 적어보세요. 잘 몰라도 괜찮아요.":experienceStep==2?"기존 감정 선택을 쓰거나 직접 적어보세요.":"그때 떠오른 생각을 적어보세요. 해결하거나 분석하지 않아도 돼요.",.07f,.54f,.86f,.115f,25);
            var field=GardenUi.Input(p,"선택 기록 · 기기에 저장",.07f,.28f,.86f,.17f);field.text=experienceValues[experienceStep]??"";
            field.onValueChanged.AddListener(value=>experienceValues[experienceStep]=value);
            if(experienceStep==2)
            {
                var rt=(RectTransform)field.transform;rt.anchorMin=new Vector2(.07f,.22f);rt.anchorMax=new Vector2(.93f,.34f);
                var picker=GardenUi.MoodPicker(p,.07f,.36f,.86f,.16f,field.text,value=>field.text=value??"");
                field.onValueChanged.AddListener(picker.SetValue);
            }
            GardenUi.Button(p,experienceStep==3?"기록 저장":"다음",.52f,.15f,.41f,.065f,()=>AdvanceExperience(false),true);
            GardenUi.Button(p,"건너뛰기",.07f,.15f,.41f,.065f,()=>AdvanceExperience(true));
            if(experienceStep>0)GardenUi.Button(p,"이전",.07f,.075f,.41f,.055f,()=>{experienceStep--;ShowExperienceStep();});
            GardenUi.Button(p,"기록 취소",.52f,.075f,.41f,.055f,ShowRecords);
        }
        void AdvanceExperience(bool skip)
        {
            if(skip)experienceValues[experienceStep]="";
            if(experienceStep<3){experienceStep++;ShowExperienceStep();return;}
            if(Apply(Service.SaveExperience(experienceId,experienceType,experienceValues[0],experienceValues[1],experienceValues[2],experienceValues[3])))FinishExperienceNavigation();
        }
        public void ShowExperienceHistory()
        {
            EnterPage("experience-history",ShowExperienceHistory);var p=Page("경험 기록과 나의 계획");
            GardenUi.Button(p,"경험 기록하기",.07f,.68f,.41f,.075f,ShowExperienceTypes);
            GardenUi.Button(p,"나의 계획",.52f,.68f,.41f,.075f,ShowPersonalPlan);
            var state=Service.Snapshot;
            int pages=Math.Max(1,(state.Experiences.Count+3)/4);
            experiencePage=Math.Min(experiencePage,pages-1);
            var records=state.Experiences.AsEnumerable().Reverse().Skip(experiencePage*4).Take(4).ToArray();
            for(int i=0;i<records.Length;i++)
            {var e=records[i];GardenUi.Button(p,LocalTime(e.CreatedUtc)+" · "+e.Type,.07f,.57f-i*.085f,.86f,.07f,()=>ShowExperienceRecord(e.Id));}
            if(records.Length==0)GardenUi.Label(p,"아직 경험 기록이 없어요.",.07f,.45f,.86f,.15f,28);
            GardenUi.Button(p,"참여 기록",.07f,.15f,.86f,.075f,ShowRecords);
            if(pages>1)
            {
                var previous=GardenUi.Button(p,"이전 기록",.07f,.075f,.41f,.055f,()=>{experiencePage--;ShowExperienceHistory();});previous.interactable=experiencePage>0;
                var next=GardenUi.Button(p,"다음 기록",.52f,.075f,.41f,.055f,()=>{experiencePage++;ShowExperienceHistory();});next.interactable=experiencePage<pages-1;
            }
        }
        public void ShowExperienceRecord(string id)
        {
            EnterPage("experience-record",()=>ShowExperienceRecord(id),id);var e=Service.Snapshot.Experiences.First(r=>r.Id==id);var p=Page("경험 · "+e.Type);
            GardenUi.ScrollText(p,"사건\n"+e.Event+"\n\n몸 감각\n"+e.Body+"\n\n감정\n"+e.Emotion+"\n\n생각\n"+e.Thought,.07f,.25f,.86f,.53f);
            GardenUi.Button(p,"경험 목록",.07f,.15f,.86f,.075f,ShowExperienceHistory);
        }
        public void ShowPersonalPlan()
        {
            EnterPage("personal-plan",ShowPersonalPlan);var p=Page("나의 계획");
            string[] keys={"warning-sign","warning-action","support","favorite","practice-time","restart-plan"};
            for(int i=0;i<keys.Length;i++)
            {string key=keys[i];GardenUi.Button(p,AnswerLabel(key),.07f,.68f-i*.08f,.86f,.065f,()=>ShowPlanField(key));}
            GardenUi.Button(p,"기록으로",.07f,.15f,.86f,.075f,ShowExperienceHistory);
        }
        public void ShowPlanField(string key)
        {
            EnterPage("plan-field",()=>ShowPlanField(key),key);var p=Page(AnswerLabel(key));
            GardenUi.Label(p,"아는 만큼만 적고 나중에 바꿀 수 있어요.\n입력은 선택이며 앱의 진단이나 자동 연락이 아니에요.",.07f,.57f,.86f,.20f,27);
            var field=GardenUi.Input(p,"선택 입력",.07f,.34f,.86f,.19f);
            field.text=planDrafts.TryGetValue(key,out string draft)?draft:Service.Snapshot.PracticePreferences.FirstOrDefault(a=>a.Key==key)?.Value??"";
            field.onValueChanged.AddListener(value=>planDrafts[key]=value);
            GardenUi.Button(p,"저장",.07f,.24f,.86f,.075f,()=>{if(Apply(Service.SavePreference(key,field.text))){planDrafts.Remove(key);ShowPersonalPlan();}},true);
            GardenUi.Button(p,"돌아가기",.07f,.15f,.86f,.075f,ShowPersonalPlan);
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
