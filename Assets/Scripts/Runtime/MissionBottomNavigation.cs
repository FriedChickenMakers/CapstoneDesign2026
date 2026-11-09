using System;
using UnityEngine;
using UnityEngine.UI;
using CapstoneDesign.Prototype;

namespace CapstoneDesign.Runtime
{
    // One 390-point navigation design for the garden and mission pages.
    [ExecuteAlways]
    public sealed class MissionBottomNavigation : MonoBehaviour
    {
        // Preserve the inset shelf approved before integration. All page layouts
        // reserve its full height, including the space below the visible shelf.
        public const float SideInset=12, BottomInset=12, BarWidth=366, BarHeight=84;
        public const float ReservedHeight=BarHeight+BottomInset;
        const float ButtonWidth=108, ButtonGap=12, ButtonInset=9, IconSize=22;
        public Button HomeButton, MissionButton, CourseButton;
        public Text MissionBadge;
        public string ActiveTab { get; private set; }
        readonly string[] labels={"홈","미션","기록"};
        Button[] buttons;
        Text[] captions;
        RawImage[] icons;
        Image badge;

        public static MissionBottomNavigation Create(Transform design,string active,Action home,Action missions,Action course,bool? hasPending,bool hasOptional=false)
        {
            var rect=FigmaMissionUi.Box(design,"하단 탐색",SideInset,844-ReservedHeight,BarWidth,BarHeight,Color.clear);
            rect.GetComponent<Image>().raycastTarget=true;
            GardenThemeSurface.Apply(rect.gameObject,GardenThemeSurface.Style.Shelf);
            var bar=rect.gameObject.AddComponent<MissionBottomNavigation>();
            bar.Build(home,missions,course);bar.Refresh(active,hasPending,hasOptional);return bar;
        }

        void Build(Action home,Action missions,Action course)
        {
            var actions=new[]{home,missions,course};buttons=new Button[3];captions=new Text[3];icons=new RawImage[3];
            for(int i=0;i<3;i++)
            {
                var button=FigmaMissionUi.Button(transform,labels[i],ButtonInset+(ButtonWidth+ButtonGap)*i,8,ButtonWidth,64,actions[i],Color.white,FigmaMissionUi.Green,14);
                // The themed face owns the corners; avoid a white sprite beneath it.
                button.GetComponent<Image>().color=Color.clear;
                button.GetComponentInChildren<Text>().gameObject.SetActive(false);buttons[i]=button;
                icons[i]=FigmaMissionUi.Asset(button.transform,"FocusMissions/Main/"+new[]{"home","leaf-muted","book"}[i],(ButtonWidth-IconSize)/2,7,IconSize,IconSize);
                captions[i]=FigmaMissionUi.Label(button.transform,labels[i],8,33,ButtonWidth-16,24,14,FigmaMissionUi.Muted,true,TextAnchor.MiddleCenter);
            }
            HomeButton=buttons[0];MissionButton=buttons[1];CourseButton=buttons[2];
            var marker=FigmaMissionUi.Box(MissionButton.transform,"미션 상태",60,0,16,16,FigmaMissionUi.Brown,8);
            badge=marker.GetComponent<Image>();
            MissionBadge=FigmaMissionUi.Label(marker,"!",0,0,16,16,11,Color.white,true,TextAnchor.MiddleCenter);
        }

        public void Refresh(string active,bool? hasPending,bool hasOptional=false)
        {
            if(buttons==null)return;ActiveTab=active;
            string[] paths={"home-white","leaf-muted-white","book-white"};
            for(int i=0;i<3;i++)
            {
                bool selected=active==labels[i];
                buttons[i].GetComponent<Image>().color=Color.clear;
                GardenThemeSurface.Apply(buttons[i].gameObject,selected?GardenThemeSurface.Style.Home:GardenThemeSurface.Style.NavigationLeaf);
                captions[i].color=selected?new Color32(35,76,54,255):Color.white;
                icons[i].color=selected?new Color32(35,76,54,255):Color.white;
                icons[i].texture=Resources.Load<Texture2D>("FocusMissions/Main/"+paths[i]);
            }
            badge.gameObject.SetActive(hasPending.HasValue);
            if(hasPending.HasValue)
            {
                // Keep the established brown/green status palette opaque against
                // both leaf tab faces; the former pale optional badge blended in.
                badge.color=hasPending.Value?FigmaMissionUi.Brown:FigmaMissionUi.Green;
                MissionBadge.color=Color.white;
                MissionBadge.text=hasPending.Value?"!":hasOptional?"·":"✓";
            }
        }
        void OnEnable(){Canvas.preWillRenderCanvases+=Fit;Fit();}
        void OnDisable(){Canvas.preWillRenderCanvases-=Fit;}
        void Fit()
        {
            var parent=transform.parent as RectTransform;if(parent==null)return;
            FitRect((RectTransform)transform,parent.rect.height);
        }
        public static void FitRect(RectTransform rect,float parentHeight)
        {
            if(rect!=null)FigmaMissionUi.SetRect(rect,SideInset,parentHeight-ReservedHeight,BarWidth,BarHeight);
        }
    }
}
