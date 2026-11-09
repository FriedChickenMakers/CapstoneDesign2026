using System;
using System.Linq;
using CapstoneDesign.Prototype;
using CapstoneDesign.Runtime;
using CapstoneDesign.Runtime.LocalState;
using UnityEngine;
using UnityEngine.UI;

namespace CapstoneDesign.EditorTools
{
    // Measure rendered bounds, not matching copies of the runtime constants.
    // The integrated 122-point, zero-gap buttons and seven-point icon offset fail these checks.
    public static class NavigationLayoutValidation
    {
        public static void Run(FocusMissionValidation.Report report,Action<bool,string> check,bool capture)
        {
            foreach(var size in new[]{new Vector2Int(360,640),new Vector2Int(390,844),new Vector2Int(1200,2000)})
            using(var f=new FocusMissionValidation.Fixture(report,true,chooseCourse:false))
            {
                f.Resize(size.x,size.y);Settle(f.Ui.transform.root);
                var bar=f.Ui.GetComponentsInChildren<MissionBottomNavigation>(false).Single();
                var launcher=f.Ui.GetComponentsInChildren<Button>(false).Single(b=>b.name=="Focus.DebugLauncher");
                CheckBadgeStates(f,bar,check,capture && size==new Vector2Int(390,844));
                CheckBar(bar,Bounds,check,"Initial course picker "+size);
                check(Bounds((RectTransform)launcher.transform).yMin>=Bounds((RectTransform)bar.transform).yMax+1,
                    "Initial course-picker development control stays entirely above the inset shelf.");
                f.Capture("qa-navigation-course-picker-"+size.x+"x"+size.y,capture);
                f.ClickControl(launcher);check(f.Ui.CurrentScreen=="mission-debug-picker","Development launcher remains reachable.");
                f.Click("Focus.DebugPickerClose");check(f.Ui.CurrentScreen=="focus-programs","Closing the picker preserves the unselected course state.");
            }
            foreach(var size in new[]{new Vector2Int(360,640),new Vector2Int(390,844),new Vector2Int(1200,2000)})
            using(var f=new FocusMissionValidation.Fixture(report,true))
            {
                f.Resize(size.x,size.y);
                string before=StateCodec.Encode(f.Service.Snapshot);
                string suffix=size.x+"x"+size.y;
                MissionBottomNavigation Active()=>f.Ui.GetComponentsInChildren<MissionBottomNavigation>(false).Single();
                void Inspect(MissionBottomNavigation bar,string page)
                {
                    Settle(f.Ui.transform.root);
                    CheckBar(bar,Bounds,check,page+" "+suffix);
                    f.Capture("qa-navigation-"+page+"-"+suffix,capture);
                }
                var mission=Active();Inspect(mission,"mission");
                check(mission.ActiveTab=="미션","Mission tab is selected.");
                f.ClickControl(mission.CourseButton);
                check(f.Ui.CurrentScreen=="daily-emotion-history","Mission record tab retains the current daily-calendar destination.");
                Inspect(Active(),"calendar");
                f.ClickControl(Active().HomeButton);
                var home=f.Navigation.home.NavigationBar;
                check(f.Navigation.home.homeCanvas.gameObject.activeSelf&&!f.Ui.gameObject.activeSelf,"Home tab restores the garden canvas.");
                Inspect(home,"home");f.ClickControl(home.HomeButton);
                check(f.Navigation.home.homeCanvas.gameObject.activeSelf,"Home button remains a usable self-navigation target.");
                f.ClickControl(home.CourseButton);
                check(f.Ui.CurrentScreen=="course","Garden record tab retains the records hub.");
                Inspect(Active(),"records");
                f.Click("PlantAlbum.Open");
                check(f.Ui.CurrentScreen=="plant-album","The existing records entry opens the plant album.");
                var album=Active();
                // Exercise the independent detail-layout override without manufacturing
                // completed plants. The full photo/record flow has its own validation suite.
                var detail=album.transform.parent.gameObject.AddComponent<GardenPlantAlbumDetailLayout>();
                try
                {
                    detail.Navigation=(RectTransform)album.transform;
                    Settle(f.Ui.transform.root);detail.Refresh();
                    CheckBar(album,Bounds,check,"Album detail container "+suffix);
                }
                finally {UnityEngine.Object.DestroyImmediate(detail);}
                Inspect(album,"album-container");
                f.ClickControl(album.MissionButton);
                check(f.Ui.CurrentScreen=="home","Album mission tab returns to the current mission home.");
                Inspect(Active(),"mission-return");
                check(StateCodec.Encode(f.Service.Snapshot)==before,"Navigation and layout checks never change saved progress or rewards.");
            }
        }

        static void CheckBadgeStates(FocusMissionValidation.Fixture fixture,MissionBottomNavigation bar,Action<bool,string> check,bool capture)
        {
            var badge=bar.MissionBadge.transform.parent.GetComponent<Image>();
            string originalTab=bar.ActiveTab,originalGlyph=bar.MissionBadge.text;
            bool originalVisible=badge.gameObject.activeSelf;
            bool? originalPending=originalVisible?(bool?)(originalGlyph=="!"):null;
            bool originalOptional=originalGlyph=="·";
            string before=StateCodec.Encode(fixture.Service.Snapshot);
            try
            {
                foreach(string tab in new[]{"홈","미션","기록"})
                {
                    void Inspect(bool? pending,bool optional,string glyph,string label,bool snapshot=false)
                    {
                        bar.Refresh(tab,pending,optional);Settle(fixture.Ui.transform.root);
                        string context="Navigation badge "+tab+" / "+label;
                        check(bar.ActiveTab==tab,context+": status does not change the selected tab.");
                        check(badge.gameObject.activeSelf==pending.HasValue,context+": missing status hides the badge.");
                        check(!badge.raycastTarget&&!bar.MissionBadge.raycastTarget,context+": status graphics do not intercept navigation.");
                        if(!pending.HasValue)return;
                        Color32 background=pending.Value?new Color32(142,93,58,255):new Color32(49,91,72,255);
                        check(((Color32)badge.color).Equals(background),context+": the established opaque brown/green background remains visible on leaf tabs.");
                        check(((Color32)bar.MissionBadge.color).Equals(new Color32(255,255,255,255)),context+": the glyph retains white ink on the dark status background.");
                        check(bar.MissionBadge.text==glyph,context+": status meaning is preserved without an invented count.");
                        check(FigmaMissionUi.Contrast(badge.color,bar.MissionBadge.color)>=4.5f,context+": small status text has readable contrast.");
                        if(snapshot&&tab=="미션")fixture.Capture("qa-navigation-badge-"+label,capture);
                    }
                    Inspect(true,false,"!","pending",true);
                    Inspect(true,true,"!","pending-before-optional");
                    Inspect(false,true,"·","optional",true);
                    Inspect(false,false,"✓","complete",true);
                    Inspect(null,true,null,"hidden");
                }
            }
            finally
            {
                bar.Refresh(originalTab,originalPending,originalOptional);
                // Hidden badges have no status update, so retain their prior text too.
                if(!originalVisible)bar.MissionBadge.text=originalGlyph;
                Settle(fixture.Ui.transform.root);
            }
            check(bar.ActiveTab==originalTab&&badge.gameObject.activeSelf==originalVisible&&bar.MissionBadge.text==originalGlyph,
                "Badge palette inspection restores the original navigation display.");
            check(StateCodec.Encode(fixture.Service.Snapshot)==before,"Badge appearance checks never change progress, rewards or mission availability.");
        }

        internal static void CheckBar(MissionBottomNavigation bar,Func<RectTransform,Rect> bounds,Action<bool,string> check,string context)
        {
            var outer=bounds((RectTransform)bar.transform.parent);var shelf=bounds((RectTransform)bar.transform);
            float scale=outer.width/390f;
            void Near(float actual,float expected,string label)=>check(Mathf.Abs(actual-expected)<=.55f,
                context+": "+label+" (actual "+actual.ToString("F2")+", expected "+expected.ToString("F2")+").");
            check(scale>0&&shelf.height>0,context+": navigation has a measurable rendered area.");
            Near(shelf.xMin-outer.xMin,12*scale,"left inset");
            Near(outer.xMax-shelf.xMax,12*scale,"right inset");
            Near(shelf.yMin-outer.yMin,12*scale,"bottom inset");
            Near(shelf.width,366*scale,"shelf width");Near(shelf.height,84*scale,"shelf height");
            var owner=bar.GetComponent<Image>();
            check(owner!=null&&owner.raycastTarget&&owner.color.a==0,
                context+": transparent rectangular hit surface preserves the floating rounded shelf.");
            var buttons=new[]{bar.HomeButton,bar.MissionButton,bar.CourseButton};
            var names=new[]{"홈","미션","기록"};Rect previous=default;
            for(int i=0;i<buttons.Length;i++)
            {
                var button=buttons[i];var rect=bounds((RectTransform)button.transform);
                Near(rect.width,108*scale,names[i]+" button width");Near(rect.height,64*scale,names[i]+" touch height");
                Near(rect.center.x,shelf.xMin+(63+120*i)*scale,names[i]+" button center");
                if(i>0)Near(rect.xMin-previous.xMax,12*scale,names[i]+" gap to previous button");
                check(rect.xMin>=shelf.xMin-.55f&&rect.xMax<=shelf.xMax+.55f&&rect.yMin>=shelf.yMin-.55f&&rect.yMax<=shelf.yMax+.55f,
                    context+": "+names[i]+" touch rectangle is contained by the shelf.");
                var icon=button.GetComponentsInChildren<RawImage>(false).Single();
                var caption=button.GetComponentsInChildren<Text>(false).Single(text=>text.text==names[i]);
                Near(bounds(icon.rectTransform).center.x,rect.center.x,names[i]+" icon center");
                Near(bounds(caption.rectTransform).center.x,rect.center.x,names[i]+" caption center");
                check(caption.alignment==TextAnchor.MiddleCenter&&caption.preferredHeight<=caption.rectTransform.rect.height+.5f,
                    context+": "+names[i]+" caption stays centered and fits its line.");
                check(button.interactable&&button.targetGraphic!=null&&button.targetGraphic.raycastTarget&&button.GetComponent<Image>().color.a==0,
                    context+": "+names[i]+" retains its full touch rectangle without a square white backing.");
                check(button.GetComponentsInChildren<GardenThemeSurface>(false).All(surface=>!surface.raycastTarget),
                    context+": leaf decoration does not intercept button input.");
                previous=rect;
            }
        }

        static Rect Bounds(RectTransform rect)
        {
            var canvas=rect.GetComponentInParent<Canvas>().rootCanvas;
            var camera=canvas.renderMode==RenderMode.ScreenSpaceOverlay?null:canvas.worldCamera;
            var corners=new Vector3[4];rect.GetWorldCorners(corners);
            var points=corners.Select(point=>RectTransformUtility.WorldToScreenPoint(camera,point)).ToArray();
            return Rect.MinMaxRect(points.Min(p=>p.x),points.Min(p=>p.y),points.Max(p=>p.x),points.Max(p=>p.y));
        }
        static void Settle(Transform root)
        {
            for(int pass=0;pass<3;pass++)
            {
                Canvas.ForceUpdateCanvases();
                foreach(var safe in root.GetComponentsInChildren<PrototypePortraitLayout>(false))safe.Apply();
                foreach(var fit in root.GetComponentsInChildren<FigmaMissionCanvasFit>(false))fit.Fit();
                foreach(var page in root.GetComponentsInChildren<FocusMobilePageLayout>(false))page.Refresh();
                foreach(var rect in root.GetComponentsInChildren<RectTransform>(false))LayoutRebuilder.ForceRebuildLayoutImmediate(rect);
                Canvas.ForceUpdateCanvases();
            }
        }
    }
}
