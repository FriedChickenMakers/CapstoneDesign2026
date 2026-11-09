using System;
using System.Linq;
using CapstoneDesign.Runtime.LocalState;
using UnityEngine;
using UnityEngine.UI;

namespace CapstoneDesign.Runtime
{
    public sealed partial class WeekOneQuestDemo
    {
        Action focusAdjustmentCancel;
        Func<bool> focusPlanPersist;

        void ShowFocusAdjustmentPrompt(Transform root, FocusMissionDefinition mission, string title, string detail, string confirm, Func<bool> apply)
        {
            if(focusAdjustmentCancel!=null)return;
            var clock=focusPracticeClock;bool resume=clock!=null&&clock.Running;
            var audio=root.GetComponentInChildren<FocusAudioPlayer>();bool resumeAudio=audio!=null&&audio.Playing;
            if(audio!=null)audio.PausePlayback();
            if(clock!=null)clock.Pause();
            if(!SaveFocusDraft())return;
            var overlay=FigmaMissionUi.Rect(root,"Focus.AdjustmentModal",0,0,390,844);
            // Width-fitted phone/tablet frames do not all have an 844-point height.
            overlay.anchorMin=Vector2.zero;overlay.anchorMax=Vector2.one;
            overlay.offsetMin=overlay.offsetMax=Vector2.zero;
            // The panel stays inside the safe-area design frame; the dimmer
            // also covers the visible canvas beneath Android's system inset.
            var dim=FigmaMissionUi.Box(overlay,"Full canvas dim",0,0,390,844,new Color(0,0,0,.38f),0);
            dim.GetComponent<Image>().raycastTarget=true;
            dim.gameObject.AddComponent<FocusAdjustmentDimmerLayout>().Fit();
            var panel=FigmaMissionUi.Box(overlay,"선택 확인",24,260,342,284,FigmaMissionUi.Background,20);
            panel.anchorMin=panel.anchorMax=new Vector2(.5f,.5f);
            panel.pivot=new Vector2(.5f,.5f);panel.anchoredPosition=Vector2.zero;
            FigmaMissionUi.Label(panel,title,24,24,294,60,22,FigmaMissionUi.Ink,true);
            FigmaMissionUi.Label(panel,detail,24,96,294,80,16,FigmaMissionUi.Muted);
            Action cancel=()=>{focusAdjustmentCancel=null;if(overlay!=null){overlay.gameObject.SetActive(false);if(Application.isPlaying)Destroy(overlay.gameObject);else DestroyImmediate(overlay.gameObject);}if(resume&&clock!=null&&!clock.Running)clock.ToggleRunning();if(resumeAudio&&audio!=null)audio.ResumePlayback();};
            focusAdjustmentCancel=cancel;
            FigmaMissionUi.Button(panel,"취소",24,204,140,56,cancel,Color.white,FigmaMissionUi.Brown).name="Focus.AdjustmentCancel";
            FigmaMissionUi.Button(panel,confirm,178,204,140,56,()=>{focusAdjustmentCancel=null;if(!apply()){focusAdjustmentCancel=cancel;return;}if(overlay!=null){overlay.gameObject.SetActive(false);if(Application.isPlaying)Destroy(overlay.gameObject);else DestroyImmediate(overlay.gameObject);}},FocusAccent(mission)).name="Focus.AdjustmentConfirm";
            overlay.SetAsLastSibling();
        }

        void ShowFocusShortenPrompt(Transform root,FocusMissionDefinition mission,string id,int step)
        {
            var clock=focusPracticeClock;if(clock==null||!clock.HasStarted)return;
            ShowFocusAdjustmentPrompt(root,mission,"여기까지만 진행할까요?","여기까지 수행한 시간을 기록해요. 남은 필수 안내가 있으면 이어서 확인해요.","여기까지 하기",()=>
            {
                var session=ActiveSession();
                if(session?.SessionId!=id)return false;
                if(FocusMissionCatalog.IsGuided(session))
                {
                    if(!FocusSaved(Service.ShortenGuidedFocusPractice(id,guidedChoice,guidedReflection,clock.ElapsedSeconds)))return false;
                    guidedResumeClock=false;focusDirty=false;FinishFocusMission();return true;
                }
                if(!FocusSaved(Service.ShortenFocusStep(id,step,clock.ElapsedSeconds,focusAnswer)))return false;
                basicResumeClock=false;focusDirty=false;ShowFocusPractice();return true;
            });
        }

        void BindFocusAdjustmentClock(Transform root,FocusMissionDefinition mission,string id,int step,Button shorten)
        {
            var clock=focusPracticeClock;
            var record=ActiveSession().FocusStepRecords[step];
            clock.RecommendedSeconds=Service.RecommendedFocusSeconds(id,step);
            clock.PreviouslyStarted=!string.IsNullOrEmpty(record.PracticeStartedUtc);
            clock.StartPractice=()=>FocusSaved(Service.StartFocusPracticeStep(id,step));
            clock.StateChanged=()=>
            {
                var current=ActiveSession();
                shorten.interactable=clock.HasStarted && current?.SessionId==id
                    && current.InstructionStep==step && string.IsNullOrEmpty(current.FocusStepRecords[step].ConfirmedUtc);
            };
        }

        void ShowFocusEndPrompt(Transform root,FocusMissionDefinition mission,string id)
        {
            ShowFocusAdjustmentPrompt(root,mission,"이번 미션을 미뤄둘까요?","완료로 기록하지 않아요. 기본 코스의 현재 회차는 그대로 두고 다른 활동을 고를 수 있어요.","미뤄두기",()=>
            {
                if(!FocusSaved(Service.EndFocusParticipation(id)))return false;
                focusDirty=false;focusPracticeClock=null;focusDrawing=null;ShowFocusHome();return true;
            });
        }

        bool MarkFocusPracticeStarted()
        {
            var session=ActiveSession();var mission=FocusMissionCatalog.FindForSession(session);
            if(session==null||mission==null||session.InstructionStep>=mission.Steps.Length)return true;
            int step=session.InstructionStep;var record=session.FocusStepRecords[step];
            if(!GardenStateService.IsActualFocusPractice(mission.Steps[step])||!string.IsNullOrEmpty(record.PracticeStartedUtc)||!string.IsNullOrEmpty(record.ConfirmedUtc))return true;
            return FocusSaved(Service.StartFocusPracticeStep(session.SessionId,step));
        }

        void BuildFocusPlan(Transform root,FocusMissionDefinition mission)
        {
            var session=ActiveSession();int step=session.InstructionStep;var record=session.FocusStepRecords[step];
            string activity=record.PlanActivity??"",period=record.PlanTimePeriod??"",note=record.PlanNote??"";
            // Retain pre-existing free text; only an explicit activity preset replaces it.
            if(activity.Length==0&&period.Length==0&&note.Length==0&&!string.IsNullOrEmpty(record.Value))note=record.Value;
            var accent=FocusAccent(mission);
            Func<bool> save=()=>
            {
                focusAnswer=FocusMissionAdjustmentPolicy.ComposePlan(activity,period,note);focusDirty=true;
                bool saved=FocusSaved(Service.SaveFocusPlan(session.SessionId,step,activity,period,note));if(saved)focusDirty=false;
                var action=FigmaMissionUi.Find<Button>(root,"Focus.Action");if(action!=null)action.interactable=!string.IsNullOrWhiteSpace(focusAnswer);
                return saved;
            };
            focusPlanPersist=()=>!focusDirty||save();
            FigmaMissionUi.Label(root,"어떤 일을 할까요?",45,238,300,24,15,FocusInk(mission),true);
            var activities=FocusMissionAdjustmentPolicy.PlanActivityOptions;
            for(int i=0;i<activities.Length;i++)
            {
                string option=activities[i];
                FigmaMissionUi.Button(root,option,45+i%2*154,268+i/2*48,146,44,()=>{activity=option;note="";if(save())ShowFocusPractice();},activity==option?accent:Color.white,activity==option?Color.white:accent,14).name="Focus.PlanActivity."+i;
            }
            FigmaMissionUi.Label(root,"편한 시간대",45,370,300,24,15,FocusInk(mission),true);
            var periods=FocusMissionAdjustmentPolicy.PlanTimePeriodOptions;
            for(int i=0;i<periods.Length;i++)
            {
                string option=periods[i];
                FigmaMissionUi.Button(root,option,45+i%2*154,400+i/2*46,146,42,()=>{period=option;if(save())ShowFocusPractice();},period==option?accent:Color.white,period==option?Color.white:accent,14).name="Focus.PlanPeriod."+i;
            }
            FigmaMissionUi.Label(root,"직접 적어도 좋아요 · 선택",45,540,300,24,14,FocusMuted(mission));
            var input=FigmaMissionUi.Input(root,"예: 저녁에 차 마시기",45,572,300,64,450);
            input.name="Focus.PlanNote";input.SetTextWithoutNotify(note);
            input.onValueChanged.AddListener(value=>{note=value;focusAnswer=FocusMissionAdjustmentPolicy.ComposePlan(activity,period,note);focusDirty=true;var action=FigmaMissionUi.Find<Button>(root,"Focus.Action");if(action!=null)action.interactable=!string.IsNullOrWhiteSpace(focusAnswer);});
            input.onEndEdit.AddListener(value=>save());AttachFocusKeyboard(root,input,"작은 계획",FigmaMissionUi.Hex(mission.BackgroundHex),accent);
        }
    }

    [ExecuteAlways]
    public sealed class FocusAdjustmentDimmerLayout : MonoBehaviour
    {
        readonly Vector3[] corners = new Vector3[4];

        void OnEnable() { Canvas.preWillRenderCanvases += Fit; Fit(); }
        void OnDisable() { Canvas.preWillRenderCanvases -= Fit; }

        public void Fit()
        {
            var parent = transform.parent as RectTransform;
            var canvas = GetComponentInParent<Canvas>();
            if (parent == null || canvas == null) return;
            var canvasRect = canvas.rootCanvas.transform as RectTransform;
            if (canvasRect == null) return;
            canvasRect.GetWorldCorners(corners);
            Vector2 min = parent.InverseTransformPoint(corners[0]), max = min;
            for (int i = 1; i < corners.Length; i++)
            {
                Vector2 point = parent.InverseTransformPoint(corners[i]);
                min = Vector2.Min(min, point); max = Vector2.Max(max, point);
            }
            var rect = (RectTransform)transform;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f);
            rect.sizeDelta = max - min;
            Vector2 center = (min + max) * .5f;
            rect.localPosition = new Vector3(center.x, center.y, 0);
        }
    }
}
