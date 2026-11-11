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
            float bottom=(Navigation?MissionBottomNavigation.ReservedHeight:footerHeight)+BottomInset;
            MissionBottomNavigation.FitRect(navigation,h);
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
            body=FigmaMissionUi.Box(transform,"Mobile.Viewport",0,bodyTop,390,Mathf.Max(48,design.rect.height-bodyTop-(Navigation?MissionBottomNavigation.ReservedHeight:footerHeight)),Color.clear);
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

    // Today's three actions fit the available safe-area height. Compact cards
    // move the action beside the copy instead of shrinking the whole page.
    [ExecuteAlways]
    public sealed class FocusMissionHomeLayout : MonoBehaviour
    {
        public sealed class Card
        {
            public RectTransform Root, Icon;
            public Text Badge, Title, Detail;
            public Button Action;
            public int TitleSize;
            internal bool Side;
            internal float TextWidth, BadgeHeight, TitleHeight, DetailHeight, CopyHeight, ButtonHeight, Height;
            internal Text ActionText;
        }

        public RectTransform Body, Header, Heading, DebugLauncher;
        public Text Notice;
        public Card[] Cards;
        const float CardWidth = 342, Padding = 12, Gap = 8, SideActionWidth = 120;
        void OnEnable() { Canvas.preWillRenderCanvases += Refresh; }
        void OnDisable() { Canvas.preWillRenderCanvases -= Refresh; }

        static float TextHeight(Text text, float width)
        {
            if (text == null) return 0;
            text.rectTransform.sizeDelta = new Vector2(width, text.rectTransform.sizeDelta.y);
            return Mathf.Ceil(text.preferredHeight);
        }
        static float Measure(Card card, bool side)
        {
            card.Side = side;
            card.TextWidth = CardWidth - Padding * 2 - (side ? SideActionWidth + 12 : 0);
            card.Title.fontSize = side ? Mathf.Min(18, card.TitleSize) : card.TitleSize;
            card.BadgeHeight = card.Badge == null ? 0 : Mathf.Max(24, TextHeight(card.Badge, card.TextWidth - (card.Icon == null ? 0 : 28)));
            card.TitleHeight = TextHeight(card.Title, card.TextWidth);
            card.DetailHeight = TextHeight(card.Detail, card.TextWidth);
            card.CopyHeight = (card.Badge == null ? 0 : card.BadgeHeight + 4) + card.TitleHeight + 4 + card.DetailHeight;
            if (card.ActionText == null) card.ActionText = card.Action.GetComponentInChildren<Text>();
            float actionWidth = side ? SideActionWidth : CardWidth - Padding * 2;
            card.ButtonHeight = Mathf.Max(48, TextHeight(card.ActionText, actionWidth - 16) + 8);
            card.Height = Padding * 2 + (side ? Mathf.Max(card.CopyHeight, card.ButtonHeight) : card.CopyHeight + Gap + card.ButtonHeight);
            return card.Height;
        }

        public void Refresh()
        {
            if (Body == null || Cards == null || Cards.Length == 0) return;
            float height = ((RectTransform)transform).rect.height;
            float headerTop = Mathf.Lerp(16, 36, Mathf.InverseLerp(680, 844, height));
            if (Header != null) FigmaMissionUi.SetRect(Header, 12, headerTop, 366, 48);
            if (Heading != null) FigmaMissionUi.SetRect(Heading, 24, headerTop + 7, 342, 38);
            float top = headerTop + 68;
            bool debug = DebugLauncher != null && DebugLauncher.gameObject.activeSelf;
            bool message = Notice != null && Notice.gameObject.activeSelf;
            float noticeWidth = debug ? 236 : 342;
            float noticeHeight = message ? TextHeight(Notice, noticeWidth) : 0;
            // Error guidance and the development entry share a row, while the
            // navigation bar keeps its inset shelf and bottom margin below them.
            float footer = Mathf.Max(debug ? 52 : 0, message ? noticeHeight + 12 : 0);
            float footerTop = height - MissionBottomNavigation.ReservedHeight - footer;
            if (debug)
            {
                DebugLauncher.anchorMin = DebugLauncher.anchorMax = new Vector2(0, 1);
                FigmaMissionUi.SetRect(DebugLauncher, 272, footerTop + (footer - 44) / 2, 94, 44);
            }
            if (message)
            {
                FigmaMissionUi.SetRect(Notice.rectTransform, 24, footerTop + (footer - noticeHeight) / 2, noticeWidth, noticeHeight);
                Notice.transform.SetAsLastSibling();
            }
            float available = Mathf.Max(1, footerTop - top - Gap);
            FigmaMissionUi.SetRect(Body, 0, top, 390, available);
            float natural = 0;
            // Prefer full-width actions. Short screens compact the lower cards
            // first, keeping the main mission's visual priority when possible.
            for (int sideCount = 0; sideCount <= Cards.Length; sideCount++)
            {
                natural = Gap * (Cards.Length - 1);
                for (int i = 0; i < Cards.Length; i++) natural += Measure(Cards[i], i >= Cards.Length - sideCount);
                if (natural <= available) break;
            }
            float extra = Mathf.Max(0, available - natural) / Cards.Length;
            float y = 0;
            foreach (var card in Cards)
            {
                float cardHeight = card.Height + extra;
                FigmaMissionUi.SetRect(card.Root, 24, y, CardWidth, cardHeight);
                float copyY = card.Side ? (cardHeight - card.CopyHeight) / 2 : Padding;
                if (card.Badge != null)
                {
                    float badgeX = Padding + (card.Icon == null ? 0 : 28);
                    FigmaMissionUi.SetRect(card.Badge.rectTransform, badgeX, copyY, card.TextWidth - (badgeX - Padding), card.BadgeHeight);
                    if (card.Icon != null) FigmaMissionUi.SetRect(card.Icon, Padding, copyY + (card.BadgeHeight - 24) / 2, 24, 24);
                    copyY += card.BadgeHeight + 4;
                }
                FigmaMissionUi.SetRect(card.Title.rectTransform, Padding, copyY, card.TextWidth, card.TitleHeight);
                copyY += card.TitleHeight + 4;
                FigmaMissionUi.SetRect(card.Detail.rectTransform, Padding, copyY, card.TextWidth, card.DetailHeight);
                float actionWidth = card.Side ? SideActionWidth : CardWidth - Padding * 2;
                float actionX = card.Side ? CardWidth - Padding - actionWidth : Padding;
                float actionY = card.Side ? (cardHeight - card.ButtonHeight) / 2 : cardHeight - Padding - card.ButtonHeight;
                FigmaMissionUi.SetRect((RectTransform)card.Action.transform, actionX, actionY, actionWidth, card.ButtonHeight);
                FigmaMissionUi.SetRect(card.ActionText.rectTransform, 8, 0, actionWidth - 16, card.ButtonHeight);
                y += cardHeight + Gap;
            }
        }
    }

    // The illustration is the stable visual and touch anchor across every slide.
    // Only optional answers scroll; text, illustration and navigation never move together.
    [ExecuteAlways]
    public sealed class FocusGuidedSlideLayout : MonoBehaviour
    {
        public RectTransform Icon, Illustration, ActionHint, Viewport, Content, Navigation;
        public Text Notice;
        // Optional larger reading treatment. Other slides retain their authored layout.
        public Text Heading, Copy;
        public float MinimumAnswerHeight = 86;
        public float ContentHeight;
        public bool KeepAnswersVisible;
        void OnEnable(){Canvas.preWillRenderCanvases+=Refresh;}
        void OnDisable(){Canvas.preWillRenderCanvases-=Refresh;}
        public void Refresh()
        {
            float height=((RectTransform)transform).rect.height;
            float bottom=height-72;
            if(Navigation!=null)FigmaMissionUi.SetRect(Navigation,24,height-64,342,48);
            if(Notice!=null&&Notice.gameObject.activeSelf)
            {
                bottom-=64;
                FigmaMissionUi.SetRect(Notice.rectTransform,24,bottom,342,56);
                Notice.transform.SetAsLastSibling();
            }
            // Keep the larger illustration below the copy, including on short
            // phones; reserve answer space when a storage notice is visible.
            bool reading = Copy != null;
            float minTop = 254, actionGap = 50;
            float readingBottom = 0;
            float illustrationOffset = reading ? Mathf.Lerp(14,32,Mathf.InverseLerp(692,844,height)) : 0;
            if (reading)
            {
                float copyTop = 126;
                if (Heading != null)
                {
                    float titleHeight = Mathf.Max(42, Mathf.Ceil(Heading.preferredHeight));
                    FigmaMissionUi.SetRect(Heading.rectTransform,24,122,342,titleHeight);
                    copyTop = 122 + titleHeight + 10;
                }
                float copyHeight = Mathf.Max(54, Mathf.Ceil(Copy.preferredHeight));
                FigmaMissionUi.SetRect(Copy.rectTransform,24,copyTop,342,copyHeight);
                readingBottom = copyTop + copyHeight + 18;
                minTop = Mathf.Max(254, readingBottom) + illustrationOffset;
                actionGap = 40;
            }
            float answerHeight = reading ? MinimumAnswerHeight : 72;
            // Timer actions are always visible. On short/error layouts use the
            // spare gap below the copy, then reduce only this slide's image.
            if (KeepAnswersVisible)
                minTop = Mathf.Min(minTop, Mathf.Max(readingBottom, bottom-actionGap-answerHeight-160));
            float availableSize = bottom-minTop-actionGap-answerHeight;
            float minimumSize = KeepAnswersVisible ? Mathf.Min(160, Mathf.Max(96, availableSize)) : reading ? 160 : 180;
            float size=Mathf.Clamp(availableSize,minimumSize,reading?280:224);
            float iconTop=Mathf.Max(minTop,height*.48f-size/2+illustrationOffset);
            if (KeepAnswersVisible) iconTop = Mathf.Min(iconTop, bottom-answerHeight-actionGap-size);
            if(Icon!=null)FigmaMissionUi.SetRect(Icon,(390-size)/2,iconTop,size,size);
            if(Illustration!=null)Illustration.localScale=Vector3.one*(size/180);
            if(ActionHint!=null)FigmaMissionUi.SetRect(ActionHint,24,iconTop+size+(reading?6:12),342,28);
            float answersTop=iconTop+size+actionGap;
            float available=Mathf.Max(36,bottom-answersTop);
            if(Viewport!=null)FigmaMissionUi.SetRect(Viewport,24,answersTop,342,available);
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
        FocusControlFocusRing ring;
        bool pressed;
        void Awake(){FigmaMissionUi.CenterPivot(transform as RectTransform);normal=transform.localScale;}
        public void OnPointerDown(PointerEventData data)
        {
            if(data.button!=PointerEventData.InputButton.Left || Control==null || !Control.IsInteractable())return;
            // A pointer can press the already keyboard-selected control without
            // another OnSelect event. Keep its transparent hit area unobscured.
            ShowFocus(false);
            if(pressed)return;
            FigmaMissionUi.CenterPivot(transform as RectTransform);normal=transform.localScale;pressed=true;
            if(!FigmaMissionUi.ReducedMotion)transform.localScale=normal*.97f;
        }
        public void OnPointerUp(PointerEventData data)=>Release();
        public void OnPointerExit(PointerEventData data)=>Release();
        void Release(){if(!pressed)return;pressed=false;transform.localScale=normal;}
        void OnDisable(){ShowFocus(false);Release();}
        public void OnSelect(BaseEventData data)
        {
            bool keyboard=!(data is PointerEventData) && Control!=null && Control.IsInteractable();
            ShowFocus(keyboard);
            if(keyboard)FocusKeyboardNavigation.Reveal(Control);
        }
        public void OnDeselect(BaseEventData data){ShowFocus(false);Release();}
        void ShowFocus(bool visible)
        {
            if(!visible){if(ring!=null)ring.gameObject.SetActive(false);return;}
            if(ring==null)
            {
                var child=new GameObject("Keyboard focus border",typeof(RectTransform),typeof(LayoutElement));
                var rect=(RectTransform)child.transform;rect.SetParent(transform,false);
                rect.anchorMin=Vector2.zero;rect.anchorMax=Vector2.one;rect.offsetMin=rect.offsetMax=Vector2.zero;
                child.GetComponent<LayoutElement>().ignoreLayout=true;
                ring=child.AddComponent<FocusControlFocusRing>();ring.color=FigmaMissionUi.Green;ring.raycastTarget=false;
            }
            ring.transform.SetAsLastSibling();ring.gameObject.SetActive(true);
        }
    }

    // Outline duplicates a Graphic's filled triangles, even when its Image is
    // transparent. A separate border leaves the illustration's centre empty.
    [RequireComponent(typeof(RectTransform),typeof(CanvasRenderer))]
    public sealed class FocusControlFocusRing : MaskableGraphic
    {
        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();var r=GetPixelAdjustedRect();
            if(r.width<=0 || r.height<=0)return;
            float thickness=Mathf.Min(2,Mathf.Min(r.width,r.height)/2);
            Edge(mesh,r.xMin,r.yMin,r.xMax,r.yMin+thickness);
            Edge(mesh,r.xMin,r.yMax-thickness,r.xMax,r.yMax);
            Edge(mesh,r.xMin,r.yMin+thickness,r.xMin+thickness,r.yMax-thickness);
            Edge(mesh,r.xMax-thickness,r.yMin+thickness,r.xMax,r.yMax-thickness);
        }
        void Edge(VertexHelper mesh,float left,float bottom,float right,float top)
        {
            if(right<=left || top<=bottom)return;
            int start=mesh.currentVertCount;
            mesh.AddVert(new Vector3(left,bottom),color,Vector2.zero);
            mesh.AddVert(new Vector3(left,top),color,Vector2.zero);
            mesh.AddVert(new Vector3(right,top),color,Vector2.zero);
            mesh.AddVert(new Vector3(right,bottom),color,Vector2.zero);
            mesh.AddTriangle(start,start+1,start+2);mesh.AddTriangle(start,start+2,start+3);
        }
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
