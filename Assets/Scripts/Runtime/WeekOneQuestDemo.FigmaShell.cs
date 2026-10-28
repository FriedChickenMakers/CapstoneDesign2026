using System;
using UnityEngine;
using UnityEngine.UI;

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
            FigmaMissionUi.Box(p,"하단 탐색",0,760,390,84,Color.white);
            string[] labels={"정원","미션","내 과정"};string[] icons={"home",active=="미션"?"leaf":"leaf-muted",active=="내 과정"?"book-active":"book"};Action[] actions={GoGarden,ShowHome,ShowFocusCourse};
            for(int i=0;i<labels.Length;i++){int j=i;float x=12+122*i;var b=FigmaMissionUi.Button(p,labels[i],x,768,122,64,actions[j],labels[i]==active?FigmaMissionUi.Soft:Color.white,FigmaMissionUi.Green,14);b.GetComponentInChildren<Text>().gameObject.SetActive(false);FigmaMissionUi.Asset(b.transform,"FocusMissions/Main/"+icons[i],50,7,22,22);FigmaMissionUi.Label(b.transform,labels[i],8,33,106,24,14,labels[i]==active?FigmaMissionUi.Green:FigmaMissionUi.Muted,true,TextAnchor.MiddleCenter);}
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
