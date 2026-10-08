using System;
using UnityEngine;
using UnityEngine.UI;
using CapstoneDesign.Prototype;

namespace CapstoneDesign.Runtime
{
    // Shared relative layout: usable at phone and tablet aspect ratios.
    public static class GardenUi
    {
        public static Font Font;
        public static Font ResolveFont()
        {
            if(Font!=null)return Font;
            var home=UnityEngine.Object.FindFirstObjectByType<GardenHomePresenter>(FindObjectsInactive.Include);
            Font=home?.title?.font?.sourceFontFile;
            return Font!=null?Font:(Font=Resources.Load<Font>("Fonts/NanumGothic-Regular"));
        }
        // Same 720 x 1280 design frame and type scale as the integrated home.
        public const int TitleSize=46, HeadingSize=34, BodySize=28, CaptionSize=26, ButtonSize=34;
        public static readonly Color Background = new Color32(247,248,239,255);
        public static readonly Color Ink = new Color32(36,60,47,255);
        public static readonly Color Muted = new Color32(100,116,104,255);
        public static readonly Color Green = new Color32(49,91,72,255);
        public static readonly Color Pale = new Color32(227,238,216,255);
        public static readonly Color Border = new Color32(214,225,207,255);
        public static readonly Color Warning = new Color32(145,79,42,255);
        public const float Spacing = 8f;
        static Sprite roundedSprite;

        public static bool HasSharedHeader(Transform page)
        {
            var home=page.GetComponentInParent<MockupNavigation>()?.home;
            return home!=null && home.homeCanvas!=null;
        }

        public static void ConstrainWidth(GameObject area)
        {
            if(area!=null && area.GetComponent<GardenContentWidth>()==null)
                area.AddComponent<GardenContentWidth>();
        }

        // A small cached nine-slice gives these existing screens their own
        // rounded controls without importing the prototype's UI components.
        static Sprite RoundedSprite()
        {
            if(roundedSprite!=null)return roundedSprite;
            const int size=64;const float radius=22;
            var texture=new Texture2D(size,size,TextureFormat.RGBA32,false){name="Garden rounded controls",hideFlags=HideFlags.HideAndDontSave};
            var pixels=new Color[size*size];
            for(int y=0;y<size;y++)for(int x=0;x<size;x++)
            {
                float dx=Mathf.Max(Mathf.Abs(x+.5f-size/2f)-(size/2f-radius),0);
                float dy=Mathf.Max(Mathf.Abs(y+.5f-size/2f)-(size/2f-radius),0);
                pixels[y*size+x]=new Color(1,1,1,Mathf.Clamp01(radius+.5f-Mathf.Sqrt(dx*dx+dy*dy)));
            }
            texture.SetPixels(pixels);texture.Apply(false,true);
            roundedSprite=Sprite.Create(texture,new Rect(0,0,size,size),new Vector2(.5f,.5f),100,0,SpriteMeshType.FullRect,new Vector4(24,24,24,24));
            roundedSprite.name="Garden rounded controls";roundedSprite.hideFlags=HideFlags.HideAndDontSave;
            return roundedSprite;
        }
        public static void Round(Image image,Color color,bool border=false)
        {
            image.sprite=RoundedSprite();image.type=Image.Type.Sliced;image.color=color;
            var outline=image.GetComponent<Outline>();
            if(border)
            {
                if(outline==null)outline=image.gameObject.AddComponent<Outline>();
                outline.enabled=true;
                outline.effectColor=Border;outline.effectDistance=new Vector2(1,-1);outline.useGraphicAlpha=true;
            }
            else if(outline!=null)outline.enabled=false;
        }
        public static void StyleButton(Button button,bool primary=false,bool selected=false)
        {
            Round(button.GetComponent<Image>(),primary?Green:selected?Pale:Color.white,!primary && !selected);
            var colors=button.colors;colors.normalColor=Color.white;
            colors.highlightedColor=new Color(.96f,.99f,.95f);colors.pressedColor=new Color(.83f,.91f,.82f);
            colors.selectedColor=Color.white;colors.disabledColor=new Color(.95f,.965f,.94f,1);button.colors=colors;
            var text=button.GetComponentInChildren<Text>(true);
            if(text!=null)
            {
                text.color=primary?Color.white:Green;text.font=ResolveFont();text.fontStyle=primary||selected?FontStyle.Bold:FontStyle.Normal;
                // New page labels can rebuild the shared dynamic font atlas.
                // Resubmit unchanged tab labels as well as changed selections.
                text.cachedTextGenerator.Invalidate();text.SetAllDirty();
            }
            button.targetGraphic.SetAllDirty();
        }
        public static GameObject Card(Transform parent,string name,float x,float y,float w,float h,Color? color=null)
        {
            var card=Box(parent,name,x,y,w,h,Color.white);
            Round(card.GetComponent<Image>(),color??Color.white,true);card.GetComponent<Image>().raycastTarget=false;return card;
        }
        public static GameObject Box(Transform parent, string name, float x, float y, float w, float h, Color? color = null)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var r = (RectTransform)go.transform;
            r.anchorMin = new Vector2(x,y); r.anchorMax = new Vector2(x+w,y+h);
            r.offsetMin = Vector2.zero; r.offsetMax = Vector2.zero;
            if (color.HasValue) go.AddComponent<Image>().color = color.Value;
            return go;
        }
        public static Text Label(Transform parent, string text, float x, float y, float w, float h, int size = 28)
        {
            var t = Box(parent,"Label",x,y,w,h).AddComponent<Text>();
            t.font = ResolveFont() ?? Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            t.text = text; t.color = Ink; t.fontSize=Mathf.Max(CaptionSize,size); t.alignment=TextAnchor.MiddleLeft;
            t.horizontalOverflow=HorizontalWrapMode.Wrap; t.verticalOverflow=VerticalWrapMode.Truncate;
            t.raycastTarget=false;
            return t;
        }
        public static Button Button(Transform parent, string label, float x, float y, float w, float h, Action action, bool primary=false)
        {
            var go = Box(parent,label,x,y,w,h,new Color(.15f,.28f,.30f,.98f));
            var b = go.AddComponent<Button>(); b.targetGraphic=go.GetComponent<Image>();
            var t=Label(go.transform,label,.05f,0,.9f,1,ButtonSize); t.alignment=TextAnchor.MiddleCenter;
            StyleButton(b,primary);
            b.navigation=new Navigation {mode=Navigation.Mode.None};
            b.onClick.AddListener(()=>action()); return b;
        }
        public static PrototypeUiIcon Icon(Transform parent,PrototypeUiIcon.Symbol symbol,float x,float y,float w,float h,Color? tint=null)
        {
            var icon=Box(parent,"Icon "+symbol,x,y,w,h).AddComponent<PrototypeUiIcon>();
            icon.symbol=symbol;icon.color=tint??Green;icon.raycastTarget=false;return icon;
        }
        public static void Progress(Transform parent,float value,float x,float y,float w,float h)
        {
            Card(parent,"Progress track",x,y,w,h,Border);
            if(value>0)Card(parent,"Progress fill",x,y,w*Mathf.Clamp01(value),h,Green);
        }
        public static Button MenuButton(Transform parent,string title,string detail,float x,float y,float w,float h,Action action,PrototypeUiIcon.Symbol symbol,bool emphasized=false)
        {
            var button=Button(parent,title,x,y,w,h,action);
            var label=button.GetComponentInChildren<Text>();label.gameObject.SetActive(false);
            if(emphasized)StyleButton(button,selected:true);
            Icon(button.transform,symbol,.03f,.23f,.075f,.54f);
            var heading=Label(button.transform,title,.14f,detail==null?.05f:.43f,.80f,detail==null?.9f:.53f,HeadingSize);heading.fontStyle=FontStyle.Bold;
            if(detail!=null){var subtitle=Label(button.transform,detail,.14f,.035f,.80f,.40f,CaptionSize);subtitle.color=Muted;}
            return button;
        }
        public sealed class MoodSelector
        {
            internal readonly Button[] Buttons=new Button[5];
            internal readonly PrototypeMoodFace[] Faces=new PrototypeMoodFace[5];
            internal readonly GameObject[] Checks=new GameObject[5];
            public string Value {get;private set;}
            public void SetValue(string value)
            {
                Value=value;
                for(int i=0;i<5;i++)
                {
                    bool selected=value==PrototypeDefinition.MoodLabels[i+1];
                    StyleButton(Buttons[i],selected:selected);Faces[i].SetSelected(selected);Checks[i].SetActive(selected);
                }
            }
        }
        // Reuses the merged design demo's five vector faces and selection check.
        public static MoodSelector MoodPicker(Transform parent,float x,float y,float w,float h,string current,Action<string> changed)
        {
            var selector=new MoodSelector();
            for(int i=0;i<5;i++)
            {
                int index=i;string value=PrototypeDefinition.MoodLabels[i+1];
                float cell=w/5;
                var button=Button(parent,value,x+i*cell,y,cell-.01f,h,()=>
                {string next=selector.Value==value?null:value;selector.SetValue(next);changed(next);});
                selector.Buttons[i]=button;
                var label=button.GetComponentInChildren<Text>();label.text=i==3?"지쳐요":value;label.fontSize=BodySize;
                label.rectTransform.anchorMin=new Vector2(.02f,.025f);label.rectTransform.anchorMax=new Vector2(.98f,.28f);
                var face=Box(button.transform,"Design mood face",.12f,.33f,.76f,.60f).AddComponent<PrototypeMoodFace>();
                face.mood=(Mood)(i+1);face.raycastTarget=false;selector.Faces[i]=face;
                selector.Checks[i]=Icon(button.transform,PrototypeUiIcon.Symbol.Check,.76f,.78f,.17f,.18f).gameObject;
            }
            selector.SetValue(current);return selector;
        }
        public static InputField Input(Transform parent, string hint, float x,float y,float w,float h)
        {
            var go=Card(parent,"Optional note",x,y,w,h);
            go.GetComponent<Image>().raycastTarget=true;
            var input=go.AddComponent<GardenInputField>();
            input.targetGraphic=go.GetComponent<Image>();
            var caption=Label(go.transform,hint,0,1,1,0,CaptionSize);
            caption.name="Field label";caption.color=Muted;
            caption.rectTransform.offsetMin=new Vector2(Spacing*2,-Spacing*6);
            caption.rectTransform.offsetMax=new Vector2(-Spacing*2,-Spacing);
            input.textComponent=Label(go.transform,"",0,0,1,1,26);
            input.textComponent.alignment=TextAnchor.UpperLeft;
            input.textComponent.rectTransform.offsetMin=new Vector2(Spacing*2,Spacing);
            input.textComponent.rectTransform.offsetMax=new Vector2(-Spacing*2,-Spacing*7);
            input.placeholder=Label(go.transform,"여기에 적어주세요",0,0,1,1,26);
            var placeholder=(Text)input.placeholder;placeholder.alignment=TextAnchor.UpperLeft;
            placeholder.rectTransform.offsetMin=input.textComponent.rectTransform.offsetMin;
            placeholder.rectTransform.offsetMax=input.textComponent.rectTransform.offsetMax;
            input.placeholder.color=Muted;
            input.customCaretColor=true;input.caretColor=Green;input.selectionColor=Pale;
            input.characterLimit=500; input.lineType=InputField.LineType.MultiLineNewline;
            return input;
        }
        public static void ScrollText(Transform parent,string text,float x,float y,float w,float h)
        {
            var box=Card(parent,"Scrollable record",x,y,w,h);
            var viewport=Box(box.transform,"Viewport",.04f,.03f,.92f,.94f);
            viewport.AddComponent<Image>().color=Color.clear;
            viewport.AddComponent<RectMask2D>();
            var content=Label(viewport.transform,text,0,0,1,1,26);
            content.alignment=TextAnchor.UpperLeft;content.verticalOverflow=VerticalWrapMode.Overflow;
            var rt=content.rectTransform;rt.anchorMin=new Vector2(0,1);rt.anchorMax=Vector2.one;rt.pivot=new Vector2(.5f,1);
            var fit=content.gameObject.AddComponent<ContentSizeFitter>();fit.verticalFit=ContentSizeFitter.FitMode.PreferredSize;
            var scroll=box.AddComponent<ScrollRect>();scroll.viewport=(RectTransform)viewport.transform;scroll.content=rt;
            scroll.horizontal=false;scroll.vertical=true;scroll.movementType=ScrollRect.MovementType.Clamped;
        }
        public static Transform ScrollPage(Transform parent,string name,float bottom,float top,float minimumHeight)
        {
            var viewport=Box(parent,name,0,bottom,1,top-bottom,Background);
            viewport.AddComponent<RectMask2D>();
            var scroll=viewport.AddComponent<ScrollRect>();
            var content=Box(viewport.transform,"Page content",0,1,1,0);
            var rect=(RectTransform)content.transform;
            rect.pivot=new Vector2(.5f,1);rect.sizeDelta=new Vector2(0,minimumHeight);
            scroll.viewport=(RectTransform)viewport.transform;scroll.content=rect;
            scroll.horizontal=false;scroll.movementType=ScrollRect.MovementType.Clamped;scroll.scrollSensitivity=48;
            AddScrollIndicator(scroll);
            return content.transform;
        }
        public static void AddScrollIndicator(ScrollRect scroll)
        {
            var track=Box(scroll.transform,"Scroll indicator",.981f,.02f,.006f,.96f);
            var scrollbar=track.AddComponent<Scrollbar>();
            var handle=Box(track.transform,"Handle",0,0,1,1);
            var image=handle.AddComponent<Image>();Round(image,new Color(.39f,.49f,.41f,.55f));
            scrollbar.handleRect=(RectTransform)handle.transform;scrollbar.targetGraphic=image;
            scrollbar.direction=Scrollbar.Direction.BottomToTop;
            scroll.verticalScrollbar=scrollbar;scroll.verticalScrollbarVisibility=ScrollRect.ScrollbarVisibility.AutoHide;
        }
        public static void FromTop(Transform item,float top,float height)
        {
            var rect=(RectTransform)item;
            rect.anchorMin=new Vector2(rect.anchorMin.x,1);rect.anchorMax=new Vector2(rect.anchorMax.x,1);
            rect.offsetMin=new Vector2(0,-top-height);rect.offsetMax=new Vector2(0,-top);
        }
    }
}
