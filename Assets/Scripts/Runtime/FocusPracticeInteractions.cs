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
        float fraction,sinceSave;bool running;
        public bool Running=>running;
        public void Initialize(){if(Toggle!=null)Toggle.onClick.AddListener(ToggleRunning);Refresh();}
        void Update()
        {
            if(!running)return;fraction+=Time.unscaledDeltaTime;sinceSave+=Time.unscaledDeltaTime;
            if(fraction>=1){int whole=(int)fraction;ElapsedSeconds=Math.Min(86400,ElapsedSeconds+whole);fraction-=whole;Refresh();}
            if(sinceSave>=5){sinceSave=0;if(!Flush()){running=false;Refresh();}}
        }
        public void ToggleRunning(){running=!running;if(!Flush())running=false;Refresh();}
        public bool Flush()=>Persist==null||Persist(ElapsedSeconds);
        public void Pause(){running=false;Flush();Refresh();}
        void OnApplicationPause(bool pause){if(pause)Pause();}
        void OnApplicationFocus(bool focused){if(!focused)Pause();}
        void Refresh()
        {
            if(Label!=null)Label.text=Format(ElapsedSeconds)+" / 권장 "+Format(RecommendedSeconds);
            if(Toggle!=null){var label=Toggle.GetComponentInChildren<Text>();if(label!=null)label.text=running?"잠시 쉬기":ElapsedSeconds>0?"이어서 집중":"집중 시작";}
        }
        static string Format(int seconds)=>seconds/60+":"+(seconds%60).ToString("00");
    }
    public sealed class FocusBreathingPulse:MonoBehaviour
    {
        Vector3 scale;float elapsed;
        void Awake(){scale=transform.localScale;}
        void Update(){elapsed+=Time.unscaledDeltaTime;transform.localScale=scale*(1+Mathf.Sin(elapsed*Mathf.PI/4)*.04f);}
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
        bool drawing;
        public string Value=>FocusDrawingData.Encode(strokes);
        public void Load(string value){strokes=FocusDrawingData.Decode(value);SetVerticesDirty();}
        public void Clear(){strokes.Clear();drawing=false;Notify();StrokeEnded?.Invoke();}
        public void Undo(){if(strokes.Count>0)strokes.RemoveAt(strokes.Count-1);drawing=false;Notify();StrokeEnded?.Invoke();}
        public void OnPointerDown(PointerEventData data)
        {
            if(ReadOnly||data.button!=PointerEventData.InputButton.Left||strokes.Count>=FocusDrawingData.MaximumStrokes)return;
            strokes.Add(new List<FocusDrawingPoint>());drawing=true;Add(data);
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
        public AudioSource Source;public Text Status;public Button Toggle,Replay;public string Transcript;
        bool paused;
        public void Initialize()
        {
            if(Toggle!=null)Toggle.onClick.AddListener(TogglePlayback);
            if(Replay!=null)Replay.onClick.AddListener(()=>{if(Source==null||Source.clip==null)return;Source.Stop();Source.Play();paused=false;Refresh();});Refresh();
        }
        void TogglePlayback()
        {
            if(Source==null||Source.clip==null)return;
            if(Source.isPlaying){Source.Pause();paused=true;}else{if(paused)Source.UnPause();else Source.Play();paused=false;}Refresh();
        }
        void Update(){Refresh();}
        void OnApplicationPause(bool pause){if(pause&&Source!=null&&Source.isPlaying){Source.Pause();paused=true;Refresh();}}
        void Refresh()
        {
            if(Status!=null)Status.text=Source==null||Source.clip==null?"음성을 불러오지 못했어요. 글을 읽을 수 있어요.":"앱의 짧은 이야기 · 한국어 합성 음성\n"+(Source.isPlaying?"재생 중":paused?"잠시 멈춤":"재생 버튼으로 시작해요");
            if(Toggle!=null){Toggle.interactable=Source!=null&&Source.clip!=null;var t=Toggle.GetComponentInChildren<Text>();if(t!=null)t.text=Source!=null&&Source.isPlaying?"일시정지":paused?"계속 듣기":"이야기 듣기";}
        }
    }
}
