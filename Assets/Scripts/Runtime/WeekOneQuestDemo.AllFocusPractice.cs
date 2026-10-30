using System;
using System.Linq;
using CapstoneDesign.Runtime.LocalState;
using UnityEngine;
using UnityEngine.UI;

namespace CapstoneDesign.Runtime
{
    public sealed partial class WeekOneQuestDemo
    {
        FocusPracticeClock focusPracticeClock;
        FocusDrawingSurface focusDrawing;
        string focusInteractionSession;
        int focusInteractionStep=-1;

        bool SaveFocusActivity()
        {
            if(!opened||screen!="focus-practice"||focusInteractionSession!=sessionId)return true;
            var active=ActiveSession();
            if(active==null||active.InstructionStep!=focusInteractionStep||active.Status==ParticipationState.Selected||active.Status==ParticipationState.RewardCommitted)return true;
            if(focusDrawing!=null){focusAnswer=focusDrawing.Value;focusDirty=true;}
            return focusPracticeClock==null||focusPracticeClock.Flush();
        }
        void AttachAllFocusClock(Transform root,FocusMissionDefinition mission,int step)
        {
            focusPracticeClock=null;focusInteractionSession=sessionId;focusInteractionStep=step;
            if(mission.Code!="M11"||step!=0)focusDrawing=null;
            if(step<0||mission.Steps[step].RecommendedSeconds<=0)return;
            var session=ActiveSession();string id=session.SessionId;int index=step;
            var selection=FigmaMissionUi.Find<Text>(root,"Focus.SelectionHelp");
            if(selection!=null)selection.rectTransform.anchoredPosition=new Vector2(45,-566);
            var label=FigmaMissionUi.Label(root,"",45,610,300,32,14,FocusMuted(mission),false,TextAnchor.MiddleCenter);
            label.name="Focus.TimerStatus";
            var toggle=FigmaMissionUi.Button(root,"집중 시작",45,650,300,56,()=>{},FocusAccent(mission),Color.white,18);
            toggle.name="Focus.TimerToggle";
            focusPracticeClock=root.gameObject.AddComponent<FocusPracticeClock>();focusPracticeClock.Label=label;focusPracticeClock.Toggle=toggle;
            var finish=FigmaMissionUi.Find<Button>(root,"Focus.Action");
            if(finish!=null)
            {
                var rect=(RectTransform)finish.transform;rect.anchoredPosition=new Vector2(45,-714);rect.sizeDelta=new Vector2(300,48);
                finish.targetGraphic.color=Color.clear;
                var text=finish.GetComponentInChildren<Text>();text.text="실습 마치기";text.color=FocusAccent(mission);text.fontSize=16;text.rectTransform.sizeDelta=new Vector2(284,48);
            }
            focusPracticeClock.RecommendedSeconds=mission.Steps[step].RecommendedSeconds;focusPracticeClock.ElapsedSeconds=session.FocusStepRecords[step].ElapsedSeconds;
            focusPracticeClock.Persist=elapsed=>FocusSaved(Service.SaveFocusElapsed(id,index,elapsed));
            focusPracticeClock.Initialize();
        }
        void BuildAllFocusPractice(Transform root,FocusMissionDefinition mission,int step,Action next)
        {
            focusDrawing=null;var accent=FocusAccent(mission);var instruction=step<0?null:mission.Steps[step];string kind=instruction?.Kind??"overview";bool walkingReady=true;
            var circle=FigmaMissionUi.Hex(mission.CircleHex??"E1EBE4");
            if(kind=="overview")
            {
                FocusCircle(root,85,307,220,circle);AllFocusIcon(root,mission,119,341,152);
                FocusCaption(root,"한 번에 한 단계씩",accent,563);
            }
            else if(kind=="draw") BuildFocusDrawing(root,mission);
            else if(kind=="review") BuildFocusReview(root,mission,step);
            else if(kind=="choice"||kind=="input"||(kind=="focus"&&instruction.Options.Length>0))
            {
                float size=kind=="input"?72:120;AllFocusIcon(root,mission,(390-size)/2,kind=="input"?245:267,size);
                BuildAllFocusOptions(root,mission,instruction,kind=="input"?332:432);
                if(kind=="input")
                {
                    float labelY=instruction.Options.Length>0?340+Mathf.CeilToInt(instruction.Options.Length/2f)*56:340;
                    FigmaMissionUi.Label(root,instruction.Title+" · "+(instruction.RequiredAnswer?"필수":"선택"),45,labelY,300,28,16,FocusInk(mission),true);
                    focusAnswerInput=FigmaMissionUi.Input(root,instruction.InputHint??"짧게 적어주세요",45,labelY+36,300,92,500);
                    focusAnswerInput.name="Focus.AnswerInput";focusAnswerInput.SetTextWithoutNotify(focusAnswer);
                    var helper=FigmaMissionUi.Label(root,instruction.RequiredAnswer?"한 단어 이상 적거나 위에서 골라주세요.":"적지 않아도 계속할 수 있어요.",45,labelY+136,300,42,14,FocusMuted(mission));helper.name="Focus.InputHelp";
                    focusAnswerInput.onValueChanged.AddListener(value=>{focusAnswer=value;focusDirty=true;var action=FigmaMissionUi.Find<Button>(root,"Focus.Action");if(action!=null&&instruction.RequiredAnswer)action.interactable=!string.IsNullOrWhiteSpace(value);if(instruction.RequiredAnswer)helper.text=string.IsNullOrWhiteSpace(value)?(instruction.Options.Length>0?"한 단어 이상 적거나 위에서 골라주세요.":"완료하려면 한 단어 이상 적어주세요."):"입력했어요. 계속할 수 있어요.";});
                    if(instruction.RequiredAnswer&&instruction.Options.Length==0)helper.text=string.IsNullOrWhiteSpace(focusAnswer)?"완료하려면 한 단어 이상 적어주세요.":"입력했어요. 계속할 수 있어요.";
                    focusAnswerInput.onEndEdit.AddListener(value=>SaveFocusDraft());AttachFocusKeyboard(root,focusAnswerInput,instruction.Title,FigmaMissionUi.Hex(mission.BackgroundHex),accent);
                }
                else FigmaMissionUi.Label(root,string.IsNullOrEmpty(focusAnswer)?"선택 없이도 계속할 수 있어요.":"선택 · "+focusAnswer,45,603,300,30,14,FocusMuted(mission),false,TextAnchor.MiddleCenter).name="Focus.SelectionHelp";
            }
            else if(kind=="walk") walkingReady=BuildAllFocusWalking(root,mission);
            else if(kind=="listen") BuildAllFocusListening(root,mission);
            else if(kind=="wait")
            {
                AllFocusIcon(root,mission,135,285,120);AllFocusContext(root,mission,"선택한 생활 계기와 실습을 저장했어요.\n돌아오면 여기서 이어갈 수 있어요.",448,112);
                FigmaMissionUi.Button(root,"나중에 이어하기",45,583,300,48,CloseFocusPractice,Color.white,accent,15).name="Focus.ContinueLater";
            }
            else if(kind=="prepare")
            {
                AllFocusIcon(root,mission,135,285,120);AllFocusContext(root,mission,"준비를 마친 뒤\n아래 버튼을 눌러요.",448,112);
            }
            else
            {
                var circleNode=FigmaMissionUi.Rect(root,"집중 아이콘",69,302,252,252);FocusCircle(circleNode,0,0,252,circle);AllFocusIcon(circleNode,mission,50,50,152);
                if(kind=="breathe")circleNode.gameObject.AddComponent<FocusBreathingPulse>();
                string cue=kind=="breathe"?"숨은 내 속도로 · 억지로 바꾸지 않아요":kind=="confirm"?"해본 순서를 돌아보고 마쳐요":"주의가 벗어나면 부드럽게 돌아와요";
                FigmaMissionUi.Label(root,cue,45,instruction.RecommendedSeconds>0?560:570,300,42,14,FocusMuted(mission),false,TextAnchor.MiddleCenter);
                if(instruction.Options.Length>0)BuildAllFocusOptions(root,mission,instruction,586);
            }
            var mainAction=FigmaMissionUi.Button(root,step<0?"시작":kind=="listen"?"듣기 마치기":instruction.ActionLabel,45,650,300,56,next,kind=="listen"?Color.clear:accent,kind=="listen"?accent:Color.white,18);mainAction.name="Focus.Action";
            if(kind=="draw")mainAction.interactable=FocusDrawingData.Decode(focusAnswer).Count>0;
            if(kind=="walk")mainAction.interactable=walkingReady;
            if(instruction?.RequiredAnswer==true)mainAction.interactable=!string.IsNullOrWhiteSpace(focusAnswer);
            if(step>=0&&!instruction.RequiredAnswer&&(kind=="input"||kind=="choice"))
                FigmaMissionUi.Button(root,"건너뛰기",125,714,140,48,()=>{focusAnswer="";if(focusAnswerInput!=null)focusAnswerInput.SetTextWithoutNotify("");focusDirty=true;next();},Color.clear,accent,14).name="Focus.Skip";
        }
        static void AllFocusIcon(Transform root,FocusMissionDefinition mission,float x,float y,float size)
            =>FigmaMissionUi.Asset(root,mission.IconAsset,x,y,size,size);
        static void AllFocusContext(Transform root,FocusMissionDefinition mission,string text,float y,float height)
        {
            var card=FigmaMissionUi.Box(root,"현재 행동 안내",45,y,300,height,FocusAlpha(Color.white,.78f),20);
            FigmaMissionUi.Label(card,text,20,24,260,height-36,17,FocusAccent(mission),true,TextAnchor.MiddleCenter);
        }
        void BuildAllFocusOptions(Transform root,FocusMissionDefinition mission,FocusMissionStep step,float y)
        {
            for(int i=0;i<step.Options.Length;i++)
            {
                string option=step.Options[i];bool selected=focusAnswer==option;var accent=FocusAccent(mission);
                var button=FigmaMissionUi.Button(root,option,45+(i%2)*154,y+(i/2)*56,146,48,()=>
                {
                    bool resumeClock=focusPracticeClock!=null&&focusPracticeClock.Running;
                    focusAnswer=focusAnswer==option?"":option;if(focusAnswerInput!=null)focusAnswerInput.SetTextWithoutNotify(focusAnswer);focusDirty=true;
                    if(SaveFocusDraft()){ShowFocusPractice();if(resumeClock&&focusPracticeClock!=null)focusPracticeClock.ToggleRunning();}
                },selected?accent:FocusAlpha(Color.white,.82f),selected?Color.white:accent,14);button.name="Focus.Option."+option;
            }
        }
        void BuildFocusDrawing(Transform root,FocusMissionDefinition mission)
        {
            var card=FigmaMissionUi.Box(root,"낙서 캔버스",45,288,300,256,Color.white,16);card.GetComponent<Image>().raycastTarget=true;
            var drawingRect=FigmaMissionUi.Rect(card,"그리기",8,8,284,240);drawingRect.gameObject.AddComponent<CanvasRenderer>();focusDrawing=drawingRect.gameObject.AddComponent<FocusDrawingSurface>();focusDrawing.color=FocusAccent(mission);focusDrawing.Load(focusAnswer);
            focusDrawing.Changed=value=>{focusAnswer=value;focusDirty=true;var action=FigmaMissionUi.Find<Button>(root,"Focus.Action");if(action!=null)action.interactable=FocusDrawingData.Decode(value).Count>0;};focusDrawing.StrokeEnded=()=>SaveFocusDraft();
            FigmaMissionUi.Button(root,"되돌리기",45,558,146,48,()=>focusDrawing.Undo(),Color.white,FocusAccent(mission),14).name="Focus.DrawingUndo";
            FigmaMissionUi.Button(root,"모두 지우기",199,558,146,48,()=>focusDrawing.Clear(),Color.clear,FigmaMissionUi.Hex("923E30"),14).name="Focus.DrawingClear";
            FigmaMissionUi.Label(root,"모두 지워도 되돌리기로 복원할 수 있어요.",45,614,300,28,14,FocusMuted(mission),false,TextAnchor.MiddleCenter);
        }
        void BuildFocusReview(Transform root,FocusMissionDefinition mission,int step)
        {
            var records=ActiveSession().FocusStepRecords.Take(step).Where(r=>!r.Skipped&&!string.IsNullOrEmpty(r.Value)).ToArray();
            var box=FigmaMissionUi.Box(root,"저장할 내용 확인",45,295,300,300,Color.white,22);
            if(mission.Code=="M11")
            {
                var drawingRect=FigmaMissionUi.Rect(box,"저장할 그림",8,8,284,234);drawingRect.gameObject.AddComponent<CanvasRenderer>();var drawing=drawingRect.gameObject.AddComponent<FocusDrawingSurface>();drawing.ReadOnly=true;drawing.raycastTarget=false;drawing.color=FocusAccent(mission);drawing.Load(records.FirstOrDefault()?.Value);
                FigmaMissionUi.Label(box,"이전 단계에서 더 그릴 수 있어요.",16,254,268,30,13,FocusMuted(mission),false,TextAnchor.MiddleCenter);return;
            }
            var viewport=FigmaMissionUi.Rect(box,"내용 스크롤",16,16,268,268);viewport.gameObject.AddComponent<RectMask2D>();var maskImage=viewport.gameObject.AddComponent<Image>();maskImage.color=Color.clear;
            var content=FigmaMissionUi.Rect(viewport,"확인 내용",0,0,268,268);float y=0;
            if(records.Length==0){FigmaMissionUi.Label(content,"입력하지 않은 항목은\n건너뛴 것으로 기록해요.",0,30,268,110,17,FocusMuted(mission),false,TextAnchor.MiddleCenter);y=160;}
            foreach(var record in records)
            {
                FigmaMissionUi.Label(content,record.Title,0,y,268,24,13,FocusMuted(mission));y+=30;
                var text=FigmaMissionUi.Label(content,record.Value,0,y,268,50,17,FocusInk(mission),true);float h=Mathf.Max(48,text.preferredHeight);text.rectTransform.sizeDelta=new Vector2(268,h);y+=h+20;
            }
            content.sizeDelta=new Vector2(268,Mathf.Max(268,y));var scroll=viewport.gameObject.AddComponent<ScrollRect>();scroll.content=content;scroll.viewport=viewport;scroll.horizontal=false;scroll.movementType=ScrollRect.MovementType.Clamped;
        }
        bool BuildAllFocusWalking(Transform root,FocusMissionDefinition mission)
        {
            AllFocusIcon(root,mission,145,250,100);var card=FigmaMissionUi.Box(root,"오늘의 걸음",45,370,300,164,Color.white,22);
            var session=ActiveSession();string id=session.SessionId;bool ready=true;
            if(session.WalkingGoalSteps<=0)
            {
                var target=WalkingPersonalization.Evaluate(ReadWalkingHealth()?.healthConnect?.personalization,Service.CurrentUtc,Service.CurrentTimeZone);
                ready=FocusSaved(Service.EnsureFocusWalkingTarget(id,target));session=ActiveSession();
                if(ready)RequestStepSyncSoon();
            }
            FigmaMissionUi.Label(card,ready?"목표 "+session.WalkingGoalSteps.ToString("N0")+"걸음":"목표를 저장하지 못했어요",20,20,260,28,18,FocusAccent(mission),true);
            var count=FigmaMissionUi.Label(card,"",20,58,260,36,24,FocusInk(mission),true);var counter=card.gameObject.AddComponent<FocusWalkingCounter>();counter.Label=count;
            counter.Read=()=>{var saved=Service.Snapshot.Sessions.FirstOrDefault(s=>s.SessionId==id);return saved!=null&&!string.IsNullOrEmpty(saved.StepSource)?saved.RealStepHighWater.ToString("N0")+"걸음":"동기화된 걸음 없음";};counter.Refresh();
            FigmaMissionUi.Button(card,"걸음 새로고침",20,108,260,48,()=>{if(!SaveFocusDraft())return;AndroidPlatformBridge.RefreshHealthData();RequestStepSyncSoon();TickStepSync();ShowFocusPractice();},FigmaMissionUi.Hex(mission.BackgroundHex),FocusAccent(mission),14).name="Focus.RefreshSteps";
            FigmaMissionUi.Label(root,"움직일 때는 화면을 보지 마세요.\n기록이 늦으면 직접 걸음을 확인해요.",45,560,300,44,14,FocusMuted(mission),false,TextAnchor.MiddleCenter);
            return ready;
        }
        void BuildAllFocusListening(Transform root,FocusMissionDefinition mission)
        {
            AllFocusIcon(root,mission,150,252,90);string route=ActiveSession().FocusStepRecords.FirstOrDefault(r=>r.Key=="route")?.Value;
            if(route=="상대의 이야기")
            {AllFocusContext(root,mission,"상대가 말하는 동안\n답을 준비하지 말고 들어요.",418,150);return;}
            var card=FigmaMissionUi.Box(root,"짧은 이야기 음성",45,366,300,260,Color.white,22);
            var player=card.gameObject.AddComponent<FocusAudioPlayer>();player.Source=card.gameObject.AddComponent<AudioSource>();player.Source.playOnAwake=false;player.Source.clip=Resources.Load<AudioClip>("FocusMissions/Audio/P24-story");
            FigmaMissionUi.Label(card,"창가의 작은 화분",16,16,268,28,18,FocusInk(mission),true);
            player.Status=FigmaMissionUi.Label(card,"",16,52,268,32,14,FocusMuted(mission));
            FigmaMissionUi.Box(card,"음성 재생 구간",16,92,268,4,FigmaMissionUi.Hex(mission.CircleHex));
            player.Progress=FigmaMissionUi.Box(card,"음성 재생 위치",16,92,0,4,FocusAccent(mission)).GetComponent<Image>();
            player.Duration=FigmaMissionUi.Label(card,"",16,104,268,24,14,FocusMuted(mission),false,TextAnchor.MiddleRight);
            player.Toggle=FigmaMissionUi.Button(card,"이야기 듣기",16,140,164,48,()=>{},FocusAccent(mission),Color.white,16);player.Toggle.name="Focus.AudioToggle";
            player.Replay=FigmaMissionUi.Button(card,"다시 듣기",188,140,96,48,()=>{},FigmaMissionUi.Hex(mission.BackgroundHex),FocusAccent(mission),14);player.Replay.name="Focus.AudioReplay";
            FigmaMissionUi.Button(card,"이야기 글 보기",16,196,268,48,()=>ShowFocusStoryTranscript(mission),Color.clear,FocusAccent(mission),14).name="Focus.AudioTranscript";player.Initialize();
        }
        void ShowFocusStoryTranscript(FocusMissionDefinition mission)
        {
            if(!SaveFocusDraft())return;var root=BeginFigmaPage("focus-story",()=>ShowFocusStoryTranscript(mission),FigmaMissionUi.Hex(mission.BackgroundHex));
            FigmaHeader(root,"창가의 작은 화분","P24 · 앱이 제공하는 짧은 이야기");var body=FigmaScroll(root,180,450,560);
            FigmaMissionUi.Label(body,FocusMissionCatalog.StoryText,32,16,326,500,21,FocusInk(mission));
            FigmaMissionUi.Button(root,"듣기 화면으로",45,698,300,56,ShowFocusPractice,FocusAccent(mission)).name="Focus.CloseTranscript";
        }
    }
}
