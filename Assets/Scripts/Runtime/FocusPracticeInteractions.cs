using System;
using System.Collections.Generic;
using System.Linq;
using CapstoneDesign.Runtime.LocalState;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace CapstoneDesign.Runtime
{
    public sealed class FocusPracticeClock:MonoBehaviour
    {
        public Text Label;public Button Toggle;public int RecommendedSeconds,ElapsedSeconds;
        public Func<int,bool> Persist;
        float fraction,sinceSave;bool running,started;
        public bool Running=>running;
        public void Initialize(){started=ElapsedSeconds>0;if(Toggle!=null)Toggle.onClick.AddListener(ToggleRunning);Refresh();}
        void Update()
        {
            if(!running)return;fraction+=Time.unscaledDeltaTime;sinceSave+=Time.unscaledDeltaTime;
            if(fraction>=1){int whole=(int)fraction;ElapsedSeconds=Math.Min(86400,ElapsedSeconds+whole);fraction-=whole;Refresh();}
            if(sinceSave>=5){sinceSave=0;if(!Flush()){running=false;Refresh();}}
        }
        public void ToggleRunning(){running=!running;if(!Flush())running=false;else if(running)started=true;Refresh();}
        public bool Flush()=>Persist==null||Persist(ElapsedSeconds);
        public void Pause(){running=false;Flush();Refresh();}
        void OnApplicationPause(bool pause){if(pause)Pause();}
        void OnApplicationFocus(bool focused){if(!focused)Pause();}
        void Refresh()
        {
            if(Label!=null)Label.text=(running?"집중 중 · ":started?"잠시 멈춤 · ":"권장 "+Format(RecommendedSeconds)+" · ")+Format(ElapsedSeconds)+(started?" / "+Format(RecommendedSeconds):"");
            if(Toggle!=null){var label=Toggle.GetComponentInChildren<Text>();if(label!=null)label.text=running?"잠시 쉬기":started?"이어서 집중":"집중 시작";}
        }
        static string Format(int seconds)=>seconds/60+":"+(seconds%60).ToString("00");
    }
    public sealed class FocusBreathingPulse:MonoBehaviour
    {
        Vector3 scale;float elapsed;
        void Awake(){scale=transform.localScale;}
        void Update(){if(FigmaMissionUi.ReducedMotion){transform.localScale=scale;return;}elapsed+=Time.unscaledDeltaTime;transform.localScale=scale*(1+Mathf.Sin(elapsed*Mathf.PI/4)*.04f);}
    }
    public sealed class FocusWalkingCounter:MonoBehaviour
    {
        public Text Label;public Func<string> Read;
        float nextRefresh;
        public void Refresh(){if(Label!=null&&Read!=null)Label.text=Read();}
        void Update(){if(Time.unscaledTime<nextRefresh)return;nextRefresh=Time.unscaledTime+.75f;Refresh();}
    }
    [RequireComponent(typeof(RectTransform),typeof(CanvasRenderer))]
    public sealed class FocusDrawingSurface:MaskableGraphic,IPointerDownHandler,IDragHandler,IPointerUpHandler
    {
        public bool ReadOnly;
        public Action<string> Changed;
        public Action StrokeEnded;
        List<List<FocusDrawingPoint>> strokes=new List<List<FocusDrawingPoint>>();
        readonly List<string> undo=new List<string>();
        bool drawing;
        public string Value=>FocusDrawingData.Encode(strokes);
        public void Load(string value){strokes=FocusDrawingData.Decode(value);undo.Clear();SetVerticesDirty();}
        void Remember(){if(undo.Count>=64)undo.RemoveAt(0);undo.Add(Value);}
        public void Clear(){if(ReadOnly||strokes.Count==0)return;Remember();strokes.Clear();drawing=false;Notify();StrokeEnded?.Invoke();}
        public void Undo()
        {
            if(ReadOnly)return;
            if(undo.Count>0){strokes=FocusDrawingData.Decode(undo[undo.Count-1]);undo.RemoveAt(undo.Count-1);}
            else if(strokes.Count>0)strokes.RemoveAt(strokes.Count-1);
            else return;
            drawing=false;Notify();StrokeEnded?.Invoke();
        }
        public void OnPointerDown(PointerEventData data)
        {
            if(ReadOnly||data.button!=PointerEventData.InputButton.Left||strokes.Count>=FocusDrawingData.MaximumStrokes)return;
            Remember();strokes.Add(new List<FocusDrawingPoint>());drawing=true;Add(data);
        }
        public void OnDrag(PointerEventData data){if(drawing)Add(data);}
        public void OnPointerUp(PointerEventData data){if(!drawing)return;drawing=false;StrokeEnded?.Invoke();}
        void Add(PointerEventData data)
        {
            if(strokes.Sum(s=>s.Count)>=FocusDrawingData.MaximumPoints)return;
            Vector2 point;if(!RectTransformUtility.ScreenPointToLocalPointInRectangle(rectTransform,data.position,data.pressEventCamera,out point))return;
            var rect=rectTransform.rect;var value=new FocusDrawingPoint(Mathf.RoundToInt((point.x-rect.xMin)/rect.width*63),Mathf.RoundToInt((point.y-rect.yMin)/rect.height*63));
            var last=strokes[strokes.Count-1];if(last.Count>0&&last[last.Count-1].X==value.X&&last[last.Count-1].Y==value.Y)return;
            last.Add(value);Notify();
        }
        void Notify(){SetVerticesDirty();Changed?.Invoke(Value);}
        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();var rect=rectTransform.rect;
            foreach(var stroke in strokes)
            {
                if(stroke.Count==1){var point=Point(stroke[0],rect);Quad(mesh,point+new Vector2(-1.7f,-1.7f),point+new Vector2(1.7f,-1.7f),point+new Vector2(1.7f,1.7f),point+new Vector2(-1.7f,1.7f));}
                for(int i=1;i<stroke.Count;i++){var a=Point(stroke[i-1],rect);var b=Point(stroke[i],rect);var delta=b-a;if(delta.sqrMagnitude<.001f)continue;var normal=new Vector2(-delta.y,delta.x).normalized*1.7f;Quad(mesh,a-normal,b-normal,b+normal,a+normal);}
            }
        }
        static Vector2 Point(FocusDrawingPoint point,Rect rect)=>new Vector2(rect.xMin+point.X/63f*rect.width,rect.yMin+point.Y/63f*rect.height);
        void Quad(VertexHelper mesh,Vector2 a,Vector2 b,Vector2 c,Vector2 d){int n=mesh.currentVertCount;mesh.AddVert(a,color,Vector2.zero);mesh.AddVert(b,color,Vector2.zero);mesh.AddVert(c,color,Vector2.zero);mesh.AddVert(d,color,Vector2.zero);mesh.AddTriangle(n,n+1,n+2);mesh.AddTriangle(n,n+2,n+3);}
    }
    public sealed class FocusAudioPlayer:MonoBehaviour
    {
        public AudioSource Source;public Text Status,Duration;public Image Progress;public Button Toggle,Replay;public string Transcript;
        bool paused,started,wasPlaying;float nextRefresh;
        public void Initialize()
        {
            if(Toggle!=null)Toggle.onClick.AddListener(TogglePlayback);
            if(Replay!=null)Replay.onClick.AddListener(()=>{if(Source==null||Source.clip==null)return;Source.Stop();Source.Play();paused=false;started=true;Refresh();});Refresh();
        }
        void TogglePlayback()
        {
            if(Source==null||Source.clip==null)return;
            if(Source.isPlaying){Source.Pause();paused=true;}else{if(paused)Source.UnPause();else Source.Play();paused=false;started=true;}Refresh();
        }
        void Update(){if(Time.unscaledTime<nextRefresh)return;nextRefresh=Time.unscaledTime+.15f;Refresh();}
        void OnApplicationPause(bool pause){if(pause&&Source!=null&&Source.isPlaying){Source.Pause();paused=true;Refresh();}}
        void OnApplicationFocus(bool focused){if(!focused&&Source!=null&&Source.isPlaying){Source.Pause();paused=true;Refresh();}}
        void Refresh()
        {
            bool available=Source!=null&&Source.clip!=null,playing=available&&Source.isPlaying;
            bool finished=available&&started&&!playing&&!paused&&(wasPlaying||Source.time<=.01f);
            float total=available?Source.clip.length:0,elapsed=available?(finished?total:Source.time):0;
            if(Status!=null)Status.text=!available?"음성 대신 아래에서 글을 읽어주세요.":playing?"이야기를 듣고 있어요":paused?"잠시 멈췄어요":finished?"이야기를 모두 들었어요":"재생 버튼을 눌러 들어보세요";
            if(Duration!=null)Duration.text=available?Format(elapsed)+" / "+Format(total):"";
            if(Progress!=null)Progress.rectTransform.sizeDelta=new Vector2(total>0?268*Mathf.Clamp01(elapsed/total):0,4);
            if(Toggle!=null){Toggle.interactable=available;var t=Toggle.GetComponentInChildren<Text>();if(t!=null)t.text=playing?"일시정지":paused?"계속 듣기":finished?"다시 듣기":"이야기 듣기";}
            if(Replay!=null)Replay.interactable=available;wasPlaying=playing;
        }
        static string Format(float seconds){int value=Mathf.FloorToInt(seconds);return value/60+":"+(value%60).ToString("00");}
    }
}
