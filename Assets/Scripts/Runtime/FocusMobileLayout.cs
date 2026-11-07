using System;
using System.Linq;
using CapstoneDesign.Runtime.LocalState;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace CapstoneDesign.Runtime
{
    // A task badge describes work that needs attention, not the size of the optional catalog.
    public static class FocusNavigationStatus
    {
        public static bool NeedsAttention(GardenState state) => state != null &&
            (state.Sessions.Any(s => FocusMissionPolicy.IsFocus(s) && FocusMissionPolicy.IsActive(s) && !s.FocusReview)
             || state.MbctProgramChosen && FocusMissionPolicy.Availability(state) == null
             || DailyEmotionPolicy.Availability(state) == null);
        public static bool HasOptional(GardenState state) => state != null &&
            FocusMissionPolicy.AdditionalAvailability(state) == null;
    }

    // The 390-point horizontal grid stays stable. Height changes only the scrolling
    // reading area, leaving close controls, errors and bottom actions in separate regions.
    [ExecuteAlways]
    public sealed class FocusMobilePageLayout : MonoBehaviour
    {
        public string Page;
        public bool Navigation;
        // Guided slides own their fixed illustration and local answer viewport.
        public bool FixedContent;
        public float BottomInset;
        public Text Notice;
        RectTransform design,body,content,footer,navigation;
        float bodyTop,footerHeight;
        RectTransform[] flowItems;
        Vector2[] flowPositions;
        float compactGap,naturalHeight;
        bool built;
        static float Top(RectTransform rect) => -FigmaMissionUi.TopLeftPosition(rect).y;
        static float Bottom(RectTransform rect) => Top(rect)+rect.rect.height;
        void OnEnable(){design=(RectTransform)transform;Canvas.preWillRenderCanvases+=Refresh;}
        void OnDisable(){Canvas.preWillRenderCanvases-=Refresh;}
        public void Refresh()
        {
            if(FixedContent)return;
            if(design==null)return;
            if(!built){if(transform.childCount<2)return;Build();}
            float h=design.rect.height;
            float message=Notice!=null&&Notice.gameObject.activeSelf?64:0;
            float bottom=(Navigation?84:footerHeight)+BottomInset;
            if(navigation!=null)navigation.anchoredPosition=new Vector2(0,-(h-84));
            if(footer!=null)footer.anchoredPosition=new Vector2(0,-(h-footerHeight));
            if(Notice!=null){Notice.rectTransform.anchoredPosition=new Vector2(24,-(h-bottom-message));Notice.rectTransform.sizeDelta=new Vector2(342,56);Notice.transform.SetAsLastSibling();}
            if(body!=null)body.sizeDelta=new Vector2(390,Mathf.Max(48,h-bottom-message-bodyTop));
            if(content!=null&&flowItems!=null)
            {
                float shift=Mathf.Min(compactGap,Mathf.Max(0,844-h+message)*.6f);
                for(int i=0;i<flowItems.Length;i++)if(flowItems[i]!=null&&flowItems[i].parent==content)
                    FigmaMissionUi.SetTopLeftPosition(flowItems[i],flowPositions[i]+new Vector2(0,-flowPositions[i].y+bodyTop>=240?shift:0));
                content.sizeDelta=new Vector2(390,Mathf.Max(body.rect.height,naturalHeight-shift));
            }
        }
        void Build()
        {
            built=true;
            var children=transform.Cast<Transform>().OfType<RectTransform>().Where(c=>Notice==null||c!=Notice.transform).ToArray();
            navigation=children.FirstOrDefault(c=>c.GetComponent<MissionBottomNavigation>()!=null);
            var existing=children.FirstOrDefault(c=>c.GetComponent<ScrollRect>()!=null);
            if(existing!=null&&Navigation)
            {
                body=existing;bodyTop=Top(body);
                return;
            }
            // A vertical gesture has one owner. Non-tab pages may author a small
            // reading list, but the mobile body handles its scrolling as a whole.
            foreach(var nested in children.Where(c=>c.GetComponent<ScrollRect>()!=null))
            {
                var inner=nested.GetComponent<ScrollRect>();float oldHeight=nested.sizeDelta.y;
                float fullHeight=Mathf.Max(oldHeight,inner.content.rect.height),delta=fullHeight-oldHeight;
                float previousBottom=Top(nested)+oldHeight;
                inner.enabled=false;if(inner.verticalScrollbar!=null)inner.verticalScrollbar.gameObject.SetActive(false);inner.content.anchoredPosition=Vector2.zero;nested.sizeDelta=new Vector2(390,fullHeight);
                foreach(var sibling in children.Where(c=>c!=nested&&Top(c)>=previousBottom&&Top(c)<650))sibling.anchoredPosition+=new Vector2(0,-delta);
            }
            bodyTop=Page=="focus-practice"?78:Navigation?132:Page=="focus-complete"?68:132;
            var actions=children.Where(c=>c!=navigation&&Top(c)>=650).ToArray();
            if(!Navigation)MoveFooter(actions);
            var items=children.Where(c=>c!=navigation&&c.parent==transform&&Top(c)>=bodyTop).ToArray();
            body=FigmaMissionUi.Box(transform,"Mobile.Viewport",0,bodyTop,390,Mathf.Max(48,design.rect.height-bodyTop-(Navigation?84:footerHeight)),Color.clear);
            body.GetComponent<Image>().raycastTarget=true;body.gameObject.AddComponent<RectMask2D>();
            float height=items.Length==0?1:Mathf.Max(1,items.Max(Bottom)-bodyTop+16);
            content=FigmaMissionUi.Rect(body,"Mobile.Content",0,0,390,height);
            foreach(var item in items){var p=FigmaMissionUi.TopLeftPosition(item);item.SetParent(content,false);FigmaMissionUi.SetTopLeftPosition(item,new Vector2(p.x,p.y+bodyTop));}
            flowItems=items;flowPositions=items.Select(FigmaMissionUi.TopLeftPosition).ToArray();naturalHeight=height;
            var illustrations=items.Where(c=>Top(c)+bodyTop>=240).ToArray();
            var headings=items.Where(c=>Top(c)+bodyTop<240).ToArray();
            if(illustrations.Length>0&&headings.Length>0)
                compactGap=Mathf.Max(0,illustrations.Min(Top)-headings.Max(Bottom)-24);
            var scroll=body.gameObject.AddComponent<ScrollRect>();scroll.content=content;scroll.viewport=body;scroll.horizontal=false;scroll.movementType=ScrollRect.MovementType.Clamped;scroll.scrollSensitivity=30;
            if(footer!=null)footer.SetAsLastSibling();if(navigation!=null)navigation.SetAsLastSibling();
            if(Application.isPlaying&&!FigmaMissionUi.ReducedMotion)content.gameObject.AddComponent<FocusMissionEntrance>();
        }
        void MoveFooter(RectTransform[] items)
        {
            if(items.Length==0){footerHeight=24;return;}
            float top=items.Min(Top);
            footerHeight=844-top;
            var background=transform.parent.GetComponent<Image>();
            footer=FigmaMissionUi.Box(transform,"Mobile.Actions",0,top,390,footerHeight,background==null?FigmaMissionUi.Background:background.color);
            foreach(var item in items){var p=FigmaMissionUi.TopLeftPosition(item);item.SetParent(footer,false);FigmaMissionUi.SetTopLeftPosition(item,new Vector2(p.x,p.y+top));}
        }
    }

    // The illustration is the stable visual and touch anchor across every slide.
    // Only optional answers scroll; text, illustration and navigation never move together.
    [ExecuteAlways]
    public sealed class FocusGuidedSlideLayout : MonoBehaviour
    {
        public RectTransform Icon, ActionHint, Viewport, Content, Navigation;
        public Text Notice;
        public float ContentHeight;
        void OnEnable(){Canvas.preWillRenderCanvases+=Refresh;}
        void OnDisable(){Canvas.preWillRenderCanvases-=Refresh;}
        public void Refresh()
        {
            float height=((RectTransform)transform).rect.height;
            float iconTop=height*.48f-90;
            if(Icon!=null)FigmaMissionUi.SetRect(Icon,105,iconTop,180,180);
            if(ActionHint!=null)FigmaMissionUi.SetRect(ActionHint,24,iconTop+192,342,28);
            float bottom=height-72;
            if(Navigation!=null)FigmaMissionUi.SetRect(Navigation,24,height-64,342,48);
            if(Notice!=null&&Notice.gameObject.activeSelf)
            {
                bottom-=64;
                FigmaMissionUi.SetRect(Notice.rectTransform,24,bottom,342,56);
                Notice.transform.SetAsLastSibling();
            }
            float available=Mathf.Max(36,bottom-(iconTop+230));
            if(Viewport!=null)FigmaMissionUi.SetRect(Viewport,24,iconTop+230,342,available);
            if(Content!=null)
            {
                Content.sizeDelta=new Vector2(342,Mathf.Max(available,ContentHeight));
                Content.anchoredPosition=new Vector2(0,Mathf.Clamp(Content.anchoredPosition.y,0,Mathf.Max(0,ContentHeight-available)));
            }
        }
    }

    public sealed class FocusControlFeedback : MonoBehaviour,IPointerDownHandler,IPointerUpHandler,IPointerExitHandler,ISelectHandler,IDeselectHandler
    {
        public Selectable Control;
        Vector3 normal=Vector3.one;
        Outline ring;
        bool pressed;
        void Awake(){FigmaMissionUi.CenterPivot(transform as RectTransform);normal=transform.localScale;}
        public void OnPointerDown(PointerEventData data)
        {
            if(data.button!=PointerEventData.InputButton.Left || Control==null || !Control.IsInteractable() || pressed)return;
            FigmaMissionUi.CenterPivot(transform as RectTransform);normal=transform.localScale;pressed=true;
            if(!FigmaMissionUi.ReducedMotion)transform.localScale=normal*.97f;
        }
        public void OnPointerUp(PointerEventData data)=>Release();
        public void OnPointerExit(PointerEventData data)=>Release();
        void Release(){if(!pressed)return;pressed=false;transform.localScale=normal;}
        void OnDisable(){if(pressed)Release();}
        public void OnSelect(BaseEventData data)
        {
            if(ring==null){ring=gameObject.AddComponent<Outline>();ring.effectColor=FigmaMissionUi.Green;ring.effectDistance=new Vector2(2,-2);ring.useGraphicAlpha=false;}
            ring.enabled=true;
            if(!(data is PointerEventData))FocusKeyboardNavigation.Reveal(Control);
        }
        public void OnDeselect(BaseEventData data){if(ring!=null)ring.enabled=false;Release();}
    }

    public sealed class FocusKeyboardNavigation : MonoBehaviour
    {
        void Update()
        {
            if (GardenMemoPopup.IsOpen) return;
            bool tab=false,reverse=false,space=false;
#if ENABLE_INPUT_SYSTEM
            if(Keyboard.current!=null){tab=Keyboard.current.tabKey.wasPressedThisFrame;reverse=Keyboard.current.shiftKey.isPressed;space=Keyboard.current.spaceKey.wasPressedThisFrame;}
#elif ENABLE_LEGACY_INPUT_MANAGER
            tab=Input.GetKeyDown(KeyCode.Tab);reverse=Input.GetKey(KeyCode.LeftShift)||Input.GetKey(KeyCode.RightShift);space=Input.GetKeyDown(KeyCode.Space);
#endif
            var events=EventSystem.current;if(events==null)return;
            if(tab)
            {
                var controls=GetComponentsInChildren<Selectable>(false).Where(c=>c.IsInteractable()).ToArray();if(controls.Length==0)return;
                int current=Array.FindIndex(controls,c=>c.gameObject==events.currentSelectedGameObject);
                int index=current<0?(reverse?controls.Length-1:0):(current+(reverse?-1:1)+controls.Length)%controls.Length;
                var next=controls[index];events.SetSelectedGameObject(next.gameObject);Reveal(next);
            }
            else if(space){var button=events.currentSelectedGameObject==null?null:events.currentSelectedGameObject.GetComponent<Button>();if(button!=null&&button.IsInteractable())button.onClick.Invoke();}
        }
        public static void Reveal(Selectable control)
        {
            if(control==null)return;
            foreach(var scroll in control.GetComponentsInParent<ScrollRect>(false).Where(s=>s.isActiveAndEnabled&&s.content!=null&&s.viewport!=null))
            {
                var rt=(RectTransform)control.transform;var center=scroll.content.InverseTransformPoint(rt.TransformPoint(rt.rect.center));
                scroll.content.anchoredPosition=new Vector2(0,Mathf.Clamp(-center.y-scroll.viewport.rect.height/2,0,Mathf.Max(0,scroll.content.rect.height-scroll.viewport.rect.height)));
            }
        }
    }
}
