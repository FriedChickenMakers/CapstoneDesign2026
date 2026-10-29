using System;
using UnityEngine;
using UnityEngine.UI;
using CapstoneDesign.Prototype;

namespace CapstoneDesign.Runtime
{
    // One 390-point navigation design for the garden and mission pages.
    public sealed class MissionBottomNavigation : MonoBehaviour
    {
        public Button HomeButton, MissionButton, CourseButton;
        public Text MissionBadge;
        public string ActiveTab { get; private set; }
        readonly string[] labels={"홈","미션","내 과정"};
        Button[] buttons;
        Text[] captions;
        RawImage[] icons;
        Image badge;

        public static MissionBottomNavigation Create(Transform design,string active,Action home,Action missions,Action course,bool? hasPending)
        {
            var rect=FigmaMissionUi.Box(design,"하단 탐색",0,760,390,84,Color.white);
            GardenThemeSurface.Apply(rect.gameObject,GardenThemeSurface.Style.Shelf);
            var bar=rect.gameObject.AddComponent<MissionBottomNavigation>();
            bar.Build(home,missions,course);bar.Refresh(active,hasPending);return bar;
        }

        void Build(Action home,Action missions,Action course)
        {
            var actions=new[]{home,missions,course};buttons=new Button[3];captions=new Text[3];icons=new RawImage[3];
            for(int i=0;i<3;i++)
            {
                var button=FigmaMissionUi.Button(transform,labels[i],12+122*i,8,122,64,actions[i],Color.white,FigmaMissionUi.Green,14);
                button.GetComponentInChildren<Text>().gameObject.SetActive(false);buttons[i]=button;
                icons[i]=FigmaMissionUi.Asset(button.transform,"FocusMissions/Main/"+new[]{"home","leaf-muted","book"}[i],50,7,22,22);
                captions[i]=FigmaMissionUi.Label(button.transform,labels[i],8,33,106,24,14,FigmaMissionUi.Muted,true,TextAnchor.MiddleCenter);
            }
            HomeButton=buttons[0];MissionButton=buttons[1];CourseButton=buttons[2];
            var marker=FigmaMissionUi.Box(MissionButton.transform,"미션 상태",67,0,16,16,FigmaMissionUi.Brown,8);
            badge=marker.GetComponent<Image>();
            MissionBadge=FigmaMissionUi.Label(marker,"!",0,0,16,16,11,Color.white,true,TextAnchor.MiddleCenter);
        }

        public void Refresh(string active,bool? hasPending)
        {
            if(buttons==null)return;ActiveTab=active;
            string[] paths={"home",active=="미션"?"leaf":"leaf-muted",active=="내 과정"?"book-active":"book"};
            for(int i=0;i<3;i++)
            {
                bool selected=active==labels[i];FigmaMissionUi.SetSelected(buttons[i],selected);
                captions[i].color=selected?FigmaMissionUi.Green:FigmaMissionUi.Muted;
                icons[i].texture=Resources.Load<Texture2D>("FocusMissions/Main/"+paths[i]);
            }
            badge.gameObject.SetActive(hasPending.HasValue);
            if(hasPending.HasValue)
            {
                badge.color=hasPending.Value?FigmaMissionUi.Brown:FigmaMissionUi.Green;
                MissionBadge.text=hasPending.Value?"!":"✓";
            }
        }
    }
}
