using System;
using UnityEngine;
using UnityEngine.UI;
using CapstoneDesign.Runtime.LocalState;

namespace CapstoneDesign.Runtime
{
    public sealed partial class WeekOneQuestDemo
    {
        RectTransform BeginFigmaPage(string key,Action render,Color background,bool navigation=false,string activeTab="미션")
        {
            EnterPage(key,render);ClearRecordHeartRefresh();ClearIntegratedPurchasePreview();ClearPageView();
            var panel=GetComponent<Image>();if(panel!=null)panel.color=background;
            activityFrame=GardenUi.Box(transform,"Figma mission surface",0,0,1,1,background);
            var design=FigmaMissionUi.Rect(activityFrame.transform,"Figma 390 x 844",0,0,390,844);
            var fit=activityFrame.AddComponent<FigmaMissionCanvasFit>();fit.Design=design;fit.Fit();view=design.gameObject;
            if(navigation)FigmaNavigation(design,activeTab);
            notice=FigmaMissionUi.Label(design,string.IsNullOrEmpty(error)?"":"저장하지 못했어요. 입력을 유지했으니 다시 시도해 주세요.",24,714,342,40,14,GardenUi.Warning);notice.gameObject.SetActive(!string.IsNullOrEmpty(error));
            return design;
        }
        void FigmaHeader(Transform p,string title,string subtitle)
        {FigmaMissionUi.Label(p,title,24,52,342,38,28,null,true);FigmaMissionUi.Label(p,subtitle,24,94,342,30,14,FigmaMissionUi.Muted);}
        void FigmaNavigation(Transform p,string active)
        {
            MissionBottomNavigation.Create(p,active,GoGarden,ShowHome,ShowFocusCourse,opened?(bool?)FocusMissionPolicy.HasPendingToday(Service.Snapshot):null);
        }
        void RefreshMissionNavigation()
        {
            if(!opened)return;
            bool pending=FocusMissionPolicy.HasPendingToday(Service.Snapshot);
            foreach(var bar in transform.root.GetComponentsInChildren<MissionBottomNavigation>(true))bar.Refresh(bar.ActiveTab,pending);
        }
        RectTransform FigmaScroll(Transform p,float y=132,float height=628,float contentHeight=900)
        {
            var viewport=FigmaMissionUi.Box(p,"스크롤 영역",0,y,390,height,Color.clear);viewport.GetComponent<Image>().raycastTarget=true;viewport.gameObject.AddComponent<RectMask2D>();
            var content=FigmaMissionUi.Rect(viewport,"스크롤 내용",0,0,390,Mathf.Max(height,contentHeight));
            var scroll=viewport.gameObject.AddComponent<ScrollRect>();scroll.viewport=viewport;scroll.content=content;scroll.horizontal=false;scroll.movementType=ScrollRect.MovementType.Clamped;scroll.scrollSensitivity=30;
            pageViewport=viewport.gameObject;return content;
        }
    }
}
