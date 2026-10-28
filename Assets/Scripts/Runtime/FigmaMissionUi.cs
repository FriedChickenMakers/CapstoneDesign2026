using System;
using UnityEngine;
using UnityEngine.UI;
using CapstoneDesign.Prototype;

namespace CapstoneDesign.Runtime
{
    // Figma points, measured from the top left of a 390 x 844 safe-area canvas.
    public static class FigmaMissionUi
    {
        public static readonly Color Background=Hex("F5F7F3"), Ink=Hex("243C2F"), Muted=Hex("647468"), Green=Hex("315B48"), Soft=Hex("E3EED8"), Border=Hex("D9E1D7"), Warm=Hex("F9EEE1"), Brown=Hex("8E5D3A");
        public static Color Hex(string value){Color c;ColorUtility.TryParseHtmlString("#"+value.TrimStart('#'),out c);return c;}
        public static RectTransform Rect(Transform p,string name,float x,float y,float w,float h)
        {
            var go=new GameObject(name,typeof(RectTransform));var rt=(RectTransform)go.transform;rt.SetParent(p,false);
            rt.anchorMin=rt.anchorMax=new Vector2(0,1);rt.pivot=new Vector2(0,1);rt.anchoredPosition=new Vector2(x,-y);rt.sizeDelta=new Vector2(w,h);return rt;
        }
        public static RectTransform Box(Transform p,string name,float x,float y,float w,float h,Color fill,float radius=0)
        {var rt=Rect(p,name,x,y,w,h);var im=rt.gameObject.AddComponent<Image>();if(radius>0){GardenUi.Round(im,fill);im.pixelsPerUnitMultiplier=22f/Mathf.Min(radius,Mathf.Min(w,h)/2f);}else im.color=fill;im.raycastTarget=false;return rt;}
        public static Text Label(Transform p,string text,float x,float y,float w,float h,int size=16,Color? color=null,bool bold=false,TextAnchor align=TextAnchor.UpperLeft)
        {
            var rt=Rect(p,text.Length>40?text.Substring(0,40):text,x,y,w,h);var t=rt.gameObject.AddComponent<Text>();
            t.font=bold?GardenTypography.Semibold:GardenTypography.Body;if(t.font==null)t.font=GardenUi.ResolveFont();
            t.text=text;t.fontSize=size;t.color=color??Ink;t.alignment=align;t.supportRichText=false;t.horizontalOverflow=HorizontalWrapMode.Wrap;t.verticalOverflow=VerticalWrapMode.Overflow;t.lineSpacing=1.2f;t.raycastTarget=false;return t;
        }
        public static Button Button(Transform p,string label,float x,float y,float w,float h,Action action,Color? fill=null,Color? ink=null,int size=16)
        {
            var rt=Box(p,label,x,y,w,h,fill??Green,16);var im=rt.GetComponent<Image>();im.raycastTarget=true;
            var b=rt.gameObject.AddComponent<Button>();b.targetGraphic=im;b.navigation=new Navigation{mode=Navigation.Mode.None};
            var colors=b.colors;colors.highlightedColor=new Color(.94f,.97f,.94f);colors.pressedColor=new Color(.78f,.86f,.80f);colors.disabledColor=new Color(.80f,.83f,.80f);colors.fadeDuration=.1f;b.colors=colors;
            Label(rt,label,8,0,w-16,h,size,ink??Color.white,true,TextAnchor.MiddleCenter);b.onClick.AddListener(()=>action?.Invoke());return b;
        }
        public static RawImage Asset(Transform p,string resourcesPath,float x,float y,float w,float h)
        {
            var tex=Resources.Load<Texture2D>(resourcesPath);var rt=Rect(p,resourcesPath,x,y,w,h);var im=rt.gameObject.AddComponent<RawImage>();im.texture=tex;im.raycastTarget=false;
            if(tex!=null){float scale=Mathf.Min(w/tex.width,h/tex.height);rt.sizeDelta=new Vector2(tex.width*scale,tex.height*scale);rt.anchoredPosition+=new Vector2((w-rt.sizeDelta.x)/2,-(h-rt.sizeDelta.y)/2);}else Debug.LogError("Missing mission asset: "+resourcesPath);
            return im;
        }
        public static InputField Input(Transform p,string hint,float x,float y,float w,float h,int limit=500)
        {
            var rt=Box(p,"입력 · "+hint,x,y,w,h,Color.white,16);rt.GetComponent<Image>().raycastTarget=true;
            var input=rt.gameObject.AddComponent<GardenInputField>();input.targetGraphic=rt.GetComponent<Image>();
            input.textComponent=Label(rt,"",16,16,w-32,h-32,16);input.placeholder=Label(rt,hint,16,16,w-32,h-32,16,Muted);
            input.lineType=InputField.LineType.MultiLineNewline;input.characterLimit=limit;input.customCaretColor=true;input.caretColor=Green;input.selectionColor=Soft;return input;
        }
    }

    [ExecuteAlways]
    public sealed class FigmaMissionCanvasFit:MonoBehaviour
    {
        public RectTransform Design;
        void OnEnable(){Canvas.preWillRenderCanvases+=Fit;Fit();}
        void OnDisable(){Canvas.preWillRenderCanvases-=Fit;}
        public void Fit(){if(Design==null)return;var r=((RectTransform)transform).rect;float s=Mathf.Min(r.width/390f,r.height/844f);if(s<=0)return;Design.anchorMin=Design.anchorMax=new Vector2(.5f,.5f);Design.pivot=new Vector2(.5f,.5f);Design.anchoredPosition=Vector2.zero;Design.sizeDelta=new Vector2(390,844);Design.localScale=Vector3.one*s;}
    }
}
