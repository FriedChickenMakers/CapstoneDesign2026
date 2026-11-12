using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
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
                BackdropSnapshot firstBackdrop=null;
                MissionBottomNavigation Active()=>f.Ui.GetComponentsInChildren<MissionBottomNavigation>(false).Single();
                void Inspect(MissionBottomNavigation bar,string page)
                {
                    Settle(f.Ui.transform.root);
                    CheckBar(bar,Bounds,check,page+" "+suffix);
                    var backdrop=CheckBackdrop(bar,check,page+" "+suffix);
                    if(firstBackdrop==null)firstBackdrop=backdrop;
                    else CompareBackdrop(firstBackdrop,backdrop,check,page+" "+suffix);
                    f.Capture("qa-navigation-"+page+"-"+suffix,capture);
                }
                var mission=Active();Inspect(mission,"mission");
                check(mission.ActiveTab=="미션","Mission tab is selected.");
                f.ClickControl(mission.CourseButton);
                check(f.Ui.CurrentScreen=="course","Mission record tab opens the same records hub as the garden record tab.");
                Inspect(Active(),"records-from-mission");
                f.Click("DailyEmotion.History");
                check(f.Ui.CurrentScreen=="daily-emotion-history","The records hub retains its explicit activity-calendar entry.");
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
                context+": transparent hit surface preserves button layout independently of the decorative shelf.");
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
                var face=button.GetComponentsInChildren<GardenThemeSurface>(false).Single();
                bool selected=bar.ActiveTab==names[i];
                var expectedStyle=selected?GardenThemeSurface.Style.Home:GardenThemeSurface.Style.NavigationLeaf;
                check(face.style==expectedStyle,context+": selected tabs retain the common selected style while other tab faces retain their existing styles.");
                if(selected&&i==0)
                {
                    check(face.SageAppearance&&face.mainTexture!=Resources.Load<Texture2D>("UI/Textures/LeafSet024LitNavigation")
                        &&face.mainTexture!=Resources.Load<Texture2D>("UI/Textures/LeafSet024UI"),
                        context+": selected garden home uses the shared light sage selection treatment without a leaf texture.");
                    check(caption.color==GardenSagePalette.Ink&&icon.color==GardenSagePalette.Ink,
                        context+": selected home caption and icon use the shared dark ink on the light sage surface.");
                }
                previous=rect;
            }
        }

        sealed class BackdropSnapshot
        {
            public Texture Texture;
            public Rect LocalRect,ScreenRect;
            public readonly List<UIVertex> Triangles=new List<UIVertex>();
            public bool Sample(Vector2 normalized,out Vector2 uv,out Color tint)
            {
                Vector2 point=LocalRect.min+Vector2.Scale(LocalRect.size,normalized);
                for(int i=0;i+2<Triangles.Count;i+=3)
                {
                    var a=Triangles[i];var b=Triangles[i+1];var c=Triangles[i+2];
                    Vector2 ab=(Vector2)(b.position-a.position),ac=(Vector2)(c.position-a.position),ap=point-(Vector2)a.position;
                    float determinant=Cross(ab,ac);if(Mathf.Abs(determinant)<.0001f)continue;
                    float wb=Cross(ap,ac)/determinant,wc=Cross(ab,ap)/determinant,wa=1-wb-wc;
                    if(wa<-.0001f||wb<-.0001f||wc<-.0001f)continue;
                    uv=(Vector2)a.uv0*wa+(Vector2)b.uv0*wb+(Vector2)c.uv0*wc;
                    tint=(Color)a.color*wa+(Color)b.color*wb+(Color)c.color*wc;return true;
                }
                uv=default;tint=default;return false;
            }
            static float Cross(Vector2 a,Vector2 b)=>a.x*b.y-a.y*b.x;
        }
        static readonly Vector2[] SharedWoodPoints={new Vector2(.1f,.5f),new Vector2(.5f,.5f),new Vector2(.9f,.5f),new Vector2(.5f,.16f),new Vector2(.5f,.95f)};
        static BackdropSnapshot CheckBackdrop(MissionBottomNavigation bar,Action<bool,string> check,string context)
        {
            var graphic=bar.transform.Find("Navigation shared backdrop")?.GetComponent<MaskableGraphic>();
            check(graphic!=null&&graphic.isActiveAndEnabled&&!graphic.raycastTarget,
                context+": the shared wood image is visible decoration and does not intercept navigation.");
            var old=bar.transform.Find("Garden theme surface");
            check(old==null||!old.gameObject.activeInHierarchy,context+": an old page-specific shelf cannot tint or obscure the common image.");
            var snapshot=new BackdropSnapshot{Texture=graphic.mainTexture,LocalRect=graphic.rectTransform.rect,ScreenRect=Bounds(graphic.rectTransform)};
            check(snapshot.Texture==Resources.Load<Texture2D>("UI/Textures/LightWoodTitleUI"),context+": all tabs use the same original wood image.");
            var outer=Bounds((RectTransform)bar.transform.parent);float scale=outer.width/390f;
            check(Mathf.Abs(snapshot.ScreenRect.xMin-outer.xMin)<.6f&&Mathf.Abs(snapshot.ScreenRect.xMax-outer.xMax)<.6f
                &&Mathf.Abs(snapshot.ScreenRect.yMin-outer.yMin)<.6f&&Mathf.Abs(snapshot.ScreenRect.height-96*scale)<.6f,
                context+": the image keeps a shared full-width coordinate frame, including the bottom inset.");
            using(var mesh=new VertexHelper())
            {
                graphic.GetType().GetMethod("OnPopulateMesh",BindingFlags.Instance|BindingFlags.NonPublic,null,new[]{typeof(VertexHelper)},null).Invoke(graphic,new object[]{mesh});
                mesh.GetUIVertexStream(snapshot.Triangles);
            }
            foreach(var point in SharedWoodPoints)
                check(snapshot.Sample(point,out _,out var tint)&&tint.a>.99f,context+": the common visible wood remains opaque at "+point+".");
            bool garden=bar.ActiveTab=="홈";
            foreach(var point in new[]{new Vector2(.005f,.5f),new Vector2(.5f,.04f),new Vector2(13f/390f,95f/96f)})
                check(snapshot.Sample(point,out _,out var tint)!=garden&&(!garden?tint.a>.99f:true),
                    context+": only the garden clips the outer margin and rounded corner, revealing the existing camera at "+point+".");
            return snapshot;
        }
        static void CompareBackdrop(BackdropSnapshot baseline,BackdropSnapshot current,Action<bool,string> check,string context)
        {
            check(current.Texture==baseline.Texture,context+": changing tabs retains the same texture object.");
            check(Vector2.Distance(current.ScreenRect.min,baseline.ScreenRect.min)<.8f&&Vector2.Distance(current.ScreenRect.size,baseline.ScreenRect.size)<.8f,
                context+": switching tabs keeps the common image at the same screen coordinates.");
            foreach(var point in SharedWoodPoints)
            {
                bool first=baseline.Sample(point,out var firstUv,out var firstTint),second=current.Sample(point,out var uv,out var tint);
                check(first&&second&&Vector2.Distance(firstUv,uv)<.0002f&&Vector4.Distance(firstTint,tint)<.009f,
                    context+": the actual mesh preserves wood crop and tint through the garden mask at "+point+".");
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
