using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using CapstoneDesign.Plants;
using CapstoneDesign.Prototype;
using CapstoneDesign.Runtime;
using CapstoneDesign.Runtime.LocalState;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace CapstoneDesign.EditorTools
{
    // Read the scene asset into a disposable preview scene; never initialize a file-backed app service.
    [InitializeOnLoad]
    public static class GardenOverlayValidation
    {
        [Serializable] sealed class Request { public string runId; public bool capture = true; }
        [Serializable] sealed class CheckResult { public string name, status, detail; }
        [Serializable] sealed class Report
        {
            public string runId, utc, unityVersion;
            public string environment="Isolated scene-asset preview / synthetic memory state / no Play mode or user saves";
            public int passed, failed;
            public List<CheckResult> checks=new List<CheckResult>();
            public List<string> captures=new List<string>();
        }
        static bool running;
        static double nextPoll;
        static string OutputRoot=>Path.Combine(Directory.GetCurrentDirectory(),"artifacts","garden-overlay-validation");
        static string RequestPath=>Path.Combine(OutputRoot,"request.json");

        static GardenOverlayValidation(){EditorApplication.update+=Poll;}
        static void Poll()
        {
            if(running||EditorApplication.timeSinceStartup<nextPoll)return;
            nextPoll=EditorApplication.timeSinceStartup+1;
            if(EditorApplication.isCompiling||EditorApplication.isUpdating||EditorApplication.isPlayingOrWillChangePlaymode||!File.Exists(RequestPath))return;
            Request request;
            try{request=JsonUtility.FromJson<Request>(File.ReadAllText(RequestPath))??new Request();}
            catch{File.Delete(RequestPath);return;}
            File.Delete(RequestPath);Execute(request);
        }
        [MenuItem("Capstone Mockup/Validate Garden Overlay (Synthetic)")]
        public static void Run()=>Execute(new Request{runId="manual-"+DateTimeOffset.UtcNow.ToString("yyyyMMdd-HHmmss")});
        static void Execute(Request request)
        {
            if(running||EditorApplication.isPlayingOrWillChangePlaymode||EditorApplication.isCompiling)return;
            running=true;Directory.CreateDirectory(OutputRoot);
            var report=new Report{runId=request.runId??"requested",utc=DateTimeOffset.UtcNow.ToString("O"),unityVersion=Application.unityVersion};
            try
            {
                if(GraphicsSettings.currentRenderPipeline!=null&&RenderPipelineManager.currentPipeline==null)Blitter.Cleanup();
                using(var fixture=new Fixture())
                {
                    Check(report,"Removed panels and view controls leave transparent plant information",()=>Surfaces(fixture));
                    Check(report,"Night and daylight text follow the garden sky with readable actual-scene frames",()=>DayAndNight(fixture,report,request.capture));
                    Check(report,"Wheel and two-finger zoom change the garden view without rotating or changing state",()=>Zoom(fixture,report,request.capture));
                    Check(report,"Bottom-tab controls intercept scrolling while the garden accepts it",()=>UiInterception(fixture));
                }
            }
            catch(Exception error){report.failed++;report.checks.Add(new CheckResult{name="Isolated preview fixture",status="fail",detail=error.ToString()});}
            finally
            {
                running=false;File.WriteAllText(Path.Combine(OutputRoot,"report.json"),JsonUtility.ToJson(report,true));
                Debug.Log("GARDEN_OVERLAY_VALIDATION passed="+report.passed+" failed="+report.failed+" report="+Path.Combine(OutputRoot,"report.json"));
            }
        }
        static void Check(Report report,string name,Action check)
        {
            try{check();report.passed++;report.checks.Add(new CheckResult{name=name,status="pass",detail=""});}
            catch(Exception error){report.failed++;report.checks.Add(new CheckResult{name=name,status="fail",detail=error.ToString()});}
        }
        static void Require(bool value,string message){if(!value)throw new InvalidOperationException(message);}

        static void Surfaces(Fixture f)
        {
            var home=f.Home;var content=home.homeCanvas.transform.Find("SafePortraitFrame/MainContent");
            foreach(var name in new[]{"Brand","Date","Home overview surface"})
            {var node=content.Find(name);Require(node==null||!node.gameObject.activeInHierarchy,"Removed upper element remains visible: "+name);}
            foreach(var name in new[]{"Growth surface","TodayMissionCard","TodayMissionCard_Edge","DailyStatus","MissionName","MissionEmblem","Garden view controls"})
            {var node=content.Find("GardenAndGrowth/"+name);Require(node==null||!node.gameObject.activeInHierarchy,"Removed lower element remains visible: "+name);}
            Require(home.progressDots==null||home.progressDots.All(dot=>dot==null||!dot.gameObject.activeInHierarchy),"Growth progress dots remain visible.");
            Require(!home.homeCanvas.GetComponentsInChildren<Button>(false).Any(b=>b.GetComponentsInChildren<Text>(false).Any(t=>t.text=="보기 +"||t.text=="보기 −"||t.text=="가까이 보기"||t.text=="정원 전체")),"Retired view controls remain visible.");
            foreach(var label in new[]{home.title,home.stage,home.guide})
            {
                Require(label!=null&&label.gameObject.activeInHierarchy&&!string.IsNullOrWhiteSpace(label.text),"Plant information is missing.");
                Require(!label.raycastTarget,"Plant information blocks garden gestures.");
                for(var parent=label.transform.parent;parent!=null&&parent!=home.homeCanvas.transform;parent=parent.parent)
                {
                    var image=parent.GetComponent<Image>();
                    Require(image==null||!image.enabled||image.color.a<.01f,"Plant information has an opaque image backdrop.");
                    var surface=parent.GetComponent<PrototypeUiSurface>();
                    Require(surface==null||!surface.enabled||(surface.topColor.a<.01f&&surface.bottomColor.a<.01f),"Plant information has an opaque surface backdrop.");
                }
            }
            string species=home.RenderedRecipe.SpeciesId;
            Require(species==null?home.title.text.Contains("식물"):home.title.text.Contains(PlantCatalog.Get(species).Name),"Selected plant name is not shown.");
            Require(home.guide.text.Contains("영양제")&&home.guide.text.Contains("37"),"The nutrient count is not shown.");
            Require(home.guide.fontSize<home.title.fontSize,"The nutrient count is not visually secondary.");
            Require(home.stage.text==new[]{"작은 씨앗","새싹","봉오리","꽃이 피었어요"}[GardenHomePresenter.StageForGrowth(GardenPlantVisuals.TotalGrowth(f.State))],"Growth text does not describe the actual plant state.");
            Require(home.NavigationBar!=null&&home.TemporarySettingsButton!=null&&home.TemporarySettingsButton.gameObject.activeInHierarchy,"Home navigation or temporary settings entry disappeared.");
            Require(home.OverlayTopBoundary!=null,"Transparent camera-framing boundary is missing.");
        }
        static void DayAndNight(Fixture f,Report report,bool capture)
        {
            foreach(var hour in new[]{0d,12d})
            {
                f.Sky.SetTimeForPreview(hour);f.Home.RefreshOverlayTextColor();
                Color expected=hour==0?Color.white:Color.black;
                foreach(var label in new[]{f.Home.title,f.Home.stage,f.Home.guide})
                    Require(Vector4.Distance(label.color,expected)<.01f,"Overlay text has the wrong color at hour "+hour);
                f.Capture(hour==0?"01-garden-night":"02-garden-day",report,capture);
            }
            Require(GardenHomePresenter.OverlayTextColor(.49f)==Color.white&&GardenHomePresenter.OverlayTextColor(.5f)==Color.black,"Day/night overlay threshold is inconsistent.");
        }
        static void Zoom(Fixture f,Report report,bool capture)
        {
            var orbit=f.Layout.orbit;orbit.zoomLayout=f.Layout;
            string stateBefore=StateCodec.Encode(f.Service.Snapshot);
            f.Layout.SetZoom(.85f);f.Layout.Apply();float initial=f.Home.gardenCamera.orthographicSize;var angles=orbit.Angles;
            var middle=new Vector2(f.Size.x*.5f,f.Size.y*.48f);
            orbit.OnScroll(f.Pointer(1,middle,new Vector2(0,1)));
            Require(f.Layout.Zoom>.85f&&f.Home.gardenCamera.orthographicSize<initial,"Mouse wheel up did not visibly zoom in.");
            orbit.OnScroll(f.Pointer(1,middle,new Vector2(0,-1)));
            Require(Mathf.Abs(f.Layout.Zoom-.85f)<.001f,"Opposite wheel movement did not restore zoom.");
            var first=f.Pointer(1,middle+Vector2.left*100);var second=f.Pointer(2,middle+Vector2.right*100);
            orbit.OnPointerDown(first);orbit.OnPointerDown(second);
            Require(orbit.IsPinching,"Two garden touches did not begin a pinch.");
            second.position=middle+Vector2.right*170;orbit.OnDrag(second);
            Require(f.Layout.Zoom>.85f&&f.Home.gardenCamera.orthographicSize<initial,"Spreading two fingers did not zoom in.");
            float spread=f.Layout.Zoom;second.position=middle+Vector2.right*40;orbit.OnDrag(second);
            Require(f.Layout.Zoom<spread,"Closing two fingers did not zoom out.");
            Require(orbit.Angles==angles,"Pinch unexpectedly rotated the garden.");
            orbit.OnPointerUp(second);first.position+=new Vector2(2,1);orbit.OnDrag(first);
            Require(orbit.Angles==angles,"Returning to one finger caused an orbit jump.");
            first.position+=new Vector2(20,4);orbit.OnDrag(first);
            Require((orbit.Angles-angles).sqrMagnitude>.0001f,"Single-finger orbit did not resume after a pinch.");
            orbit.OnPointerUp(first);
            Require(!orbit.IsPinching,"Pinch remained latched after touch release.");
            Require(PrototypeIslandDrag.ZoomFromWheel(1,999)==1&&PrototypeIslandDrag.ZoomFromWheel(0,-999)==0,"Wheel zoom is not bounded.");
            Require(PrototypeIslandDrag.ZoomFromPinch(.5f,0,100)==.5f,"A zero-distance pinch changed zoom.");
            Require(StateCodec.Encode(f.Service.Snapshot)==stateBefore,"View gestures modified saved garden or mission state.");
            f.Layout.SetZoom(0);f.Capture("03-garden-wide",report,capture);
            f.Layout.SetZoom(1);f.Capture("04-garden-close",report,capture);
            f.Layout.SetZoom(.85f);
        }
        static void UiInterception(Fixture f)
        {
            f.Draw();var button=f.Home.NavigationBar.MissionButton;var rect=(RectTransform)button.transform;
            Vector2 point=RectTransformUtility.WorldToScreenPoint(f.Camera,rect.TransformPoint(rect.rect.center));
            var pointer=f.Pointer(1,point,new Vector2(0,1));var hits=new List<RaycastResult>();f.Raycaster.Raycast(pointer,hits);
            var hit=hits.OrderByDescending(h=>h.depth).FirstOrDefault();
            Require(hit.gameObject!=null&&hit.gameObject.GetComponentInParent<Button>()==button,"Garden image intercepts the mission tab.");
            float previous=f.Layout.Zoom;ExecuteEvents.ExecuteHierarchy(hit.gameObject,pointer,ExecuteEvents.scrollHandler);
            Require(f.Layout.Zoom==previous,"Scrolling over a bottom tab zoomed the garden.");
        }

        sealed class Clock:IClock
        {
            public DateTimeOffset UtcNow=>DateTimeOffset.Parse("2026-10-29T03:00:00Z");
            public TimeZoneInfo TimeZone=>TimeZoneInfo.Utc;
        }
        sealed class Store:IStateStore
        {
            GardenState value;
            public GardenState Load()=>value==null?null:StateCodec.Clone(value);
            public void Save(GardenState state){value=StateCodec.Clone(state);}
        }
        sealed class Fixture:IDisposable
        {
            public readonly GardenHomePresenter Home;
            public readonly GardenStateService Service;
            public readonly GardenState State;
            public readonly GardenSkyEnvironment Sky;
            public readonly PrototypeGardenPreviewLayout Layout;
            public readonly Camera Camera;
            public readonly GraphicRaycaster Raycaster;
            public readonly Vector2Int Size=new Vector2Int(900,1600);
            readonly Scene scene;
            readonly Scene originalScene;
            readonly EventSystem pointerContext;
            readonly RenderTexture target;
            readonly Texture2D texture;
            readonly AmbientMode ambientMode;
            readonly Color ambientLight,ambientSky,ambientEquator,ambientGround;
            readonly float reflection;
            readonly Material skybox;
            public Fixture()
            {
                originalScene=SceneManager.GetActiveScene();ambientMode=RenderSettings.ambientMode;ambientLight=RenderSettings.ambientLight;
                ambientSky=RenderSettings.ambientSkyColor;ambientEquator=RenderSettings.ambientEquatorColor;ambientGround=RenderSettings.ambientGroundColor;
                reflection=RenderSettings.reflectionIntensity;skybox=RenderSettings.skybox;
                scene=EditorSceneManager.OpenPreviewScene("Assets/Scenes/MockupMain.unity");
                try
                {
                    Home=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<GardenHomePresenter>(true)).Single();
                    Require(Home.activity.Service==null,"Preview scene unexpectedly initialized a persistent app service.");
                    Service=new GardenStateService(new Store(),new Clock(),new DemoBalanceConfig{VisitorChancePercent=0},new LegacySnapshot{Nutrient=37,Growth=.1f});
                    Require(Service.Open(),"Synthetic memory state did not open.");
                    SetField(Home.activity,"<Service>k__BackingField",Service);SetField(Home.activity,"opened",true);SetField(Home.activity,"preview",true);SetField(Home.activity,"mode","MOCK");
                    Home.navigation.ShowIsland();State=Service.Snapshot;Home.RefreshFromState(State);
                    Layout=Home.homeCanvas.GetComponentInChildren<PrototypeGardenPreviewLayout>(true);
                    Require(Layout!=null&&Layout.orbit!=null,"Actual scene is missing its garden input surface.");
                    Camera=Home.oldCamera;Camera.enabled=false;Camera.scene=scene;Camera.clearFlags=CameraClearFlags.SolidColor;Camera.backgroundColor=Color.black;
                    target=new RenderTexture(Size.x,Size.y,24);texture=new Texture2D(Size.x,Size.y,TextureFormat.RGBA32,false);
                    Camera.targetTexture=target;Camera.aspect=(float)Size.x/Size.y;
                    var canvas=Home.GetComponent<Canvas>();canvas.renderMode=Home.homeCanvas.renderMode=RenderMode.ScreenSpaceCamera;
                    canvas.worldCamera=Home.homeCanvas.worldCamera=Camera;canvas.planeDistance=1;Home.homeCanvas.planeDistance=.75f;
                    Home.gardenCamera.scene=scene;
                    Sky=GardenSkyEnvironment.Ensure(Home.gardenCamera,Home.gardenVisuals.transform);
                    Raycaster=Home.homeCanvas.GetComponent<GraphicRaycaster>()??Home.homeCanvas.gameObject.AddComponent<GraphicRaycaster>();
                    var pointer=new GameObject("Isolated overlay pointer");pointer.SetActive(false);SceneManager.MoveGameObjectToScene(pointer,scene);
                    pointerContext=pointer.AddComponent<EventSystem>();pointerContext.enabled=false;
                    Draw();
                }
                catch{Dispose();throw;}
            }
            static void SetField(object owner,string name,object value)=>owner.GetType().GetField(name,BindingFlags.Instance|BindingFlags.NonPublic).SetValue(owner,value);
            public PointerEventData Pointer(int id,Vector2 point,Vector2 scroll=default)
            {
                return new PointerEventData(pointerContext){pointerId=id,button=PointerEventData.InputButton.Left,position=point,pressPosition=point,scrollDelta=scroll,
                    pointerPressRaycast=new RaycastResult{gameObject=Layout.orbit.gameObject,module=Raycaster,screenPosition=point},
                    pointerCurrentRaycast=new RaycastResult{gameObject=Layout.orbit.gameObject,module=Raycaster,screenPosition=point}};
            }
            public void Draw()
            {
                foreach(var graphic in Home.homeCanvas.GetComponentsInChildren<Graphic>(false))
                {
                    GraphicRegistry.RegisterGraphicForCanvas(Home.homeCanvas,graphic);graphic.SetAllDirty();graphic.Rebuild(CanvasUpdate.PreRender);graphic.Rebuild(CanvasUpdate.LatePreRender);
                }
                foreach(var label in Home.homeCanvas.GetComponentsInChildren<TMP_Text>(false))label.ForceMeshUpdate(false,true);
                Canvas.ForceUpdateCanvases();
                foreach(var fit in Home.homeCanvas.GetComponentsInChildren<PrototypePortraitLayout>(true))fit.Apply();
                Layout.Apply();Sky.UpdateClouds(0);Home.gardenCamera.Render();Canvas.ForceUpdateCanvases();Camera.Render();
            }
            public void Capture(string name,Report report,bool enabled)
            {
                if(!enabled)return;
                for(int pass=0;pass<4;pass++)Draw();
                var previous=RenderTexture.active;
                try{RenderTexture.active=target;texture.ReadPixels(new Rect(0,0,Size.x,Size.y),0,0);texture.Apply();}
                finally{RenderTexture.active=previous;}
                var pixels=texture.GetPixels32();int min=765,max=0;
                foreach(var pixel in pixels){int value=pixel.r+pixel.g+pixel.b;min=Math.Min(min,value);max=Math.Max(max,value);}
                Require(max-min>=90,"Actual garden preview is blank or a solid frame: "+name);
                string path=Path.Combine(OutputRoot,name+".png");File.WriteAllBytes(path,texture.EncodeToPNG());report.captures.Add(path);
            }
            public void Dispose()
            {
                if(Home!=null&&Home.homeCanvas!=null)
                    foreach(var graphic in Home.homeCanvas.GetComponentsInChildren<Graphic>(true))GraphicRegistry.UnregisterGraphicForCanvas(Home.homeCanvas,graphic);
                if(Camera!=null)Camera.targetTexture=null;
                if(target!=null){target.Release();UnityEngine.Object.DestroyImmediate(target);}
                if(texture!=null)UnityEngine.Object.DestroyImmediate(texture);
                if(scene.IsValid())EditorSceneManager.ClosePreviewScene(scene);
                Service?.Dispose();
                if(originalScene.IsValid()&&originalScene.isLoaded)SceneManager.SetActiveScene(originalScene);
                RenderSettings.ambientMode=ambientMode;RenderSettings.ambientLight=ambientLight;RenderSettings.ambientSkyColor=ambientSky;
                RenderSettings.ambientEquatorColor=ambientEquator;RenderSettings.ambientGroundColor=ambientGround;
                RenderSettings.reflectionIntensity=reflection;RenderSettings.skybox=skybox;
            }
        }
    }
}
