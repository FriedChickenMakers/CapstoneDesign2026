using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using CapstoneDesign.Prototype;
using CapstoneDesign.Runtime;
using CapstoneDesign.Runtime.LocalState;
using TMPro;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

namespace CapstoneDesign.EditorTools
{
    // Actual integrated home scene with a synthetic in-memory garden only.
    // The comparison changes decoration; it never imports assets or saves the scene.
    public static class GardenActionStyleCapture
    {
        sealed class Clock : IClock
        {
            public DateTimeOffset UtcNow=>new DateTimeOffset(2026,11,9,12,0,0,TimeSpan.FromHours(9));
            public TimeZoneInfo TimeZone=>TimeZoneInfo.CreateCustomTimeZone("Synthetic-KST",TimeSpan.FromHours(9),"KST","KST");
        }
        sealed class Store : IStateStore
        {
            public string Data;
            public GardenState Load()=>Data==null?null:StateCodec.Decode(Data);
            public void Save(GardenState state)=>Data=StateCodec.Encode(state);
        }
        [Serializable] sealed class Report
        {
            public string environment="Actual MockupMain home; synthetic in-memory state; mock platform; Editor only; no device/PlayerPrefs/runtime asset/scene save.";
            public int passed,failed;
            public List<string> checks=new List<string>();
            public List<Frame> frames=new List<Frame>();
        }
        [Serializable] sealed class Frame
        {
            public string file;
            public int width,height;
            public List<Bounds> buttons=new List<Bounds>();
        }
        [Serializable] sealed class Bounds
        {
            public string control;
            public float x,y,width,height;
        }
        sealed class ButtonSnapshot
        {
            public Button Button;
            public Rect Rect;
            public Vector3 Position,Scale;
            public Quaternion Rotation;
            public Transform Parent;
            public bool Interactable;
            public Button.ButtonClickedEvent Click;
            public int PersistentListeners;
            public ButtonSnapshot(Button button)
            {
                Button=button;var r=(RectTransform)button.transform;Rect=r.rect;
                Position=r.position;Scale=r.localScale;Rotation=r.rotation;Parent=r.parent;
                Interactable=button.interactable;Click=button.onClick;PersistentListeners=Click.GetPersistentEventCount();
            }
            public void AssertUnchanged()
            {
                var r=(RectTransform)Button.transform;
                Require(r.rect==Rect && r.position==Position && r.localScale==Scale && r.rotation==Rotation && r.parent==Parent,
                    "Control layout changed: "+Button.name);
                Require(Button.interactable==Interactable && ReferenceEquals(Button.onClick,Click)
                    && Button.onClick.GetPersistentEventCount()==PersistentListeners,"Control event/interactability changed: "+Button.name);
            }
        }

        public static void RunBatch()
        {
            string root=Environment.GetEnvironmentVariable("CAPSTONE_ARTIFACTS");
            string source=Environment.GetEnvironmentVariable("CAPSTONE_NAV_LEAF_SOURCE");
            Require(Application.isBatchMode && !Application.isPlaying && !string.IsNullOrWhiteSpace(root)
                && !string.IsNullOrWhiteSpace(source) && SystemInfo.graphicsDeviceType!=GraphicsDeviceType.Null,
                "Use Editor batch graphics and explicit CAPSTONE_ARTIFACTS/CAPSTONE_NAV_LEAF_SOURCE.");
            string output=Path.Combine(root,"home-style");
            Require(!Directory.Exists(output),"Use a fresh home-style artifact directory.");
            Directory.CreateDirectory(output);
            var report=new Report();var rendering=new List<string>();Texture2D navigation=null;
            double? previousHour=GardenVisualClock.OverrideHour;
            try
            {
                navigation=GardenNavigationLeafBake.Bake(source,output);
                // Opening the real home scene unloads otherwise-unused transient
                // textures. Retain this owned atlas until our explicit finally cleanup.
                navigation.hideFlags=HideFlags.HideAndDontSave;
                foreach(var size in new[]{new Vector2Int(780,1688),new Vector2Int(1200,2000)})
                    Capture(size,output,navigation,report,rendering);
                Debug.Log("GARDEN_ACTION_STYLE_PASS captures="+report.frames.Count+" checks="+report.passed);
            }
            catch(Exception error)
            {
                report.failed++;report.checks.Add("FAIL "+error);throw;
            }
            finally
            {
                if(navigation!=null)UnityEngine.Object.DestroyImmediate(navigation);
                if(previousHour.HasValue)GardenVisualClock.SetOverrideMinutes((int)Math.Round(previousHour.Value*60));
                else GardenVisualClock.UseSystemTime();
                AndroidPlatformBridge.UseMockProvider();
                File.WriteAllText(Path.Combine(output,"home-style-report.json"),JsonUtility.ToJson(report,true));
                File.WriteAllLines(Path.Combine(output,"home-style-rendering.txt"),rendering);
            }
        }

        public static void RunRuntimeBatch()
        {
            string root=Environment.GetEnvironmentVariable("CAPSTONE_ARTIFACTS");
            Require(Application.isBatchMode && !Application.isPlaying && !string.IsNullOrWhiteSpace(root),"Explicit batch artifact directory required.");
            string output=Path.Combine(root,"runtime-home");Directory.CreateDirectory(output);
            var report=new Report();var rendering=new List<string>();double? previousHour=GardenVisualClock.OverrideHour;
            try
            {
                foreach(var size in new[]{new Vector2Int(780,1688),new Vector2Int(1200,2000)})
                foreach(int hour in new[]{12,0})
                {
                    EditorSceneManager.OpenScene("Assets/Scenes/MockupMain.unity",OpenSceneMode.Single);
                    AndroidPlatformBridge.UseMockProvider();GardenVisualClock.SetOverrideMinutes(hour*60);
                    var home=UnityEngine.Object.FindFirstObjectByType<GardenHomePresenter>(FindObjectsInactive.Include);
                    var store=new Store();var balance=new DemoBalanceConfig{VisitorChancePercent=0};
                    using var service=new GardenStateService(store,new Clock(),balance,new LegacySnapshot{Nutrient=50});
                    home.activity.Balance=balance;home.activity.Initialize(service,true);
                    Require(service.SelectMbctProgram("8w"),service.LastError);
                    typeof(GardenHomePresenter).GetMethod("Awake",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(home,null);
                    home.navigation.ShowActivities();home.navigation.ShowIsland();home.RefreshFromState();home.ResetView();
                    var sky=GardenSkyEnvironment.Ensure(home.gardenCamera,home.gardenVisuals.transform);sky.SetTimeForPreview(hour);
                    home.RefreshOverlayTextColor();
                    GameObject.Find("MockupCamera").GetComponent<Camera>().GetUniversalAdditionalCameraData().dithering=false;
                    home.gardenCamera.GetUniversalAdditionalCameraData().dithering=false;
                    var state=StateCodec.Encode(service.Snapshot);var saved=store.Data;
                    var button=home.TemporarySettingsButton;
                    Require(button!=null && button.GetComponent<Image>().color.a==0,"Home settings must be text only with retained touch target.");
                    Require(button.GetComponentInChildren<Text>().color==GardenHomePresenter.OverlayTextColor(sky.Daylight),"Home settings contrast must follow day/night.");
                    Require(!button.GetComponentsInChildren<GardenThemeSurface>().Any(x=>x.isActiveAndEnabled),"Home settings still has a wooden plaque.");
                    string file="home-"+(hour==12?"day":"night")+"-"+size.x+"x"+size.y+".png";
                    using(var capture=new GardenLightingValidation.HomeCapture(home,sky,size))capture.Render(Path.Combine(output,file),rendering);
                    Require(state==StateCodec.Encode(service.Snapshot) && saved==store.Data,"Capture changed saved state.");
                    report.frames.Add(new Frame{file=file,width=size.x,height=size.y});report.passed++;
                }
            }
            catch(Exception error){report.failed++;report.checks.Add(error.ToString());throw;}
            finally
            {
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
                if(previousHour.HasValue)GardenVisualClock.SetOverrideMinutes((int)Math.Round(previousHour.Value*60));else GardenVisualClock.UseSystemTime();
                AndroidPlatformBridge.UseMockProvider();File.WriteAllText(Path.Combine(output,"report.json"),JsonUtility.ToJson(report,true));
                File.WriteAllLines(Path.Combine(output,"rendering.txt"),rendering);
            }
        }

        static void Capture(Vector2Int size,string output,Texture2D navigation,Report report,List<string> rendering)
        {
            GardenStateService service=null;
            try
            {
                EditorSceneManager.OpenScene("Assets/Scenes/MockupMain.unity",OpenSceneMode.Single);
                AndroidPlatformBridge.UseMockProvider();GardenVisualClock.SetOverrideMinutes(720);
                var home=UnityEngine.Object.FindFirstObjectByType<GardenHomePresenter>(FindObjectsInactive.Include);
                Require(home!=null && home.activity!=null && home.shopButton!=null,"Actual integrated home bindings are missing.");
                var store=new Store();var balance=new DemoBalanceConfig {VisitorChancePercent=0};
                service=new GardenStateService(store,new Clock(),balance,new LegacySnapshot {Nutrient=50});
                home.activity.Balance=balance;home.activity.Initialize(service,true);
                Require(service.SelectMbctProgram("8w"),service.LastError);
                // Edit-mode scene opening does not run the presenter's Awake-owned
                // listeners. Exercise that lifecycle once rather than inventing actions.
                var awake=typeof(MonoBehaviour).GetProperty("didAwake",BindingFlags.Instance|BindingFlags.Public);
                Require(awake!=null && !(bool)awake.GetValue(home),"Expected an unawakened edit-mode home presenter.");
                typeof(GardenHomePresenter).GetMethod("Awake",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(home,null);
                home.navigation.ShowActivities();home.navigation.ShowIsland();home.RefreshFromState();home.ResetView();
                var sky=GardenSkyEnvironment.Ensure(home.gardenCamera,home.gardenVisuals.transform);
                sky.SetTimeForPreview(12);
                var camera=GameObject.Find("MockupCamera").GetComponent<Camera>();
                camera.GetUniversalAdditionalCameraData().dithering=false;
                home.gardenCamera.GetUniversalAdditionalCameraData().dithering=false;
                var bar=home.NavigationBar;
                Require(bar!=null && bar.ActiveTab=="홈","Expected selected real home navigation.");
                var inactive=new[]{bar.MissionButton,bar.CourseButton};
                var selected=Surface(bar.HomeButton);
                var primary=Resources.Load<Texture2D>("UI/Textures/LeafSet017UI");
                Require(primary!=null && Surface(home.shopButton).style==GardenThemeSurface.Style.Primary
                    && Surface(home.shopButton).mainTexture==primary,"Garden action does not use the approved Primary/LeafSet017 source.");
                Require(selected.style==GardenThemeSurface.Style.Home,"Selected home style changed before capture.");
                foreach(var button in inactive)
                    GardenTextureComparisonCapture.ApplyNavigation(Surface(button),navigation);
                var controls=new[]{bar.HomeButton,bar.MissionButton,bar.CourseButton,home.shopButton};
                using(var capture=new GardenLightingValidation.HomeCapture(home,sky,size))
                {
                    string beforeName="before-garden-primary-"+size.x+"x"+size.y+".png";
                    capture.Render(Path.Combine(output,beforeName),rendering);
                    report.frames.Add(FrameFor(beforeName,size,controls,camera));
                    string state=StateCodec.Encode(service.Snapshot),stored=store.Data;
                    var snapshots=controls.Select(b=>new ButtonSnapshot(b)).ToArray();
                    // Foreground/image colors and badge are owned by the existing
                    // presenter. Decorative surface backgrounds are deliberately excluded.
                    var foreground=home.homeCanvas.GetComponentsInChildren<Graphic>(true)
                        .Where(g=>g is TMP_Text || g is Text || g is RawImage).ToDictionary(g=>g,g=>g.color);
                    var badge=bar.MissionBadge.transform.parent.GetComponent<Image>();
                    Color badgeColor=badge.color;string badgeText=bar.MissionBadge.text;
                    bool badgeActive=badge.gameObject.activeSelf;var selectedTexture=selected.mainTexture;
                    foreach(var button in inactive)GardenTextureComparisonCapture.PreviewGardenPrimary(button,1f,navigation);
                    GardenTextureComparisonCapture.PreviewGardenPrimary(home.shopButton,2f);
                    string afterName="after-garden-primary-"+size.x+"x"+size.y+".png";
                    capture.Render(Path.Combine(output,afterName),rendering);
                    report.frames.Add(FrameFor(afterName,size,controls,camera));
                    foreach(var snapshot in snapshots)snapshot.AssertUnchanged();
                    foreach(var pair in foreground)Require(pair.Key!=null && pair.Key.color==pair.Value,"Foreground color changed: "+pair.Key.name);
                    Require(badge.color==badgeColor && bar.MissionBadge.text==badgeText && badge.gameObject.activeSelf==badgeActive,
                        "Navigation badge changed.");
                    Require(selected.style==GardenThemeSurface.Style.Home && selected.mainTexture==selectedTexture && selected.enabled,
                        "Selected home style changed.");
                    foreach(var button in inactive)
                    {
                        var face=button.GetComponentInChildren<GardenTextureOptionSurface>();
                        Require(face!=null && face.Source==navigation && face.mainTexture==navigation
                            && face.GardenPrimaryFrame && !face.raycastTarget,
                            "Inactive navigation must retain its original LeafSet024 bake.");
                    }
                    foreach(var button in new[]{home.shopButton})
                    {
                        var surface=Surface(button);
                        Require(surface.style==GardenThemeSurface.Style.Primary && surface.mainTexture==primary && surface.enabled
                            && !surface.raycastTarget,"Expected nonblocking original Primary/LeafSet017 on "+button.name);
                    }
                    Require(home.homeCanvas.GetComponentsInChildren<GardenThemeSurface>(true).All(s=>!s.raycastTarget)
                        && home.homeCanvas.GetComponentsInChildren<GardenTextureOptionSurface>(true).All(s=>!s.raycastTarget),
                        "A decorative preview intercepts input.");
                    Require(StateCodec.Encode(service.Snapshot)==state && store.Data==stored,"Home comparison changed saved state.");
                    report.passed++;
                    report.checks.Add(size+": PASS actual home before/after; original LeafSet024 bake retained on inactive navigation; garden-care Primary/LeafSet017 remains 2x; control rects, parent, foreground, badge, selected tab, event objects/persistent counts and in-memory state unchanged.");
                }
            }
            finally
            {
                // Discard scene-only presentation changes, including preview graphics.
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
                service?.Dispose();
            }
        }

        static GardenThemeSurface Surface(Button button)
        {
            var surface=button==null?null:button.transform.Find("Garden theme surface")?.GetComponent<GardenThemeSurface>();
            Require(surface!=null,"Expected an existing button theme surface.");return surface;
        }
        static Frame FrameFor(string file,Vector2Int size,Button[] controls,Camera camera)
        {
            var frame=new Frame {file=file,width=size.x,height=size.y};
            foreach(var button in controls)
            {
                var points=new Vector3[4];((RectTransform)button.transform).GetWorldCorners(points);
                var a=camera.WorldToViewportPoint(points[0]);var b=camera.WorldToViewportPoint(points[2]);
                frame.buttons.Add(new Bounds {control=button.name,x=a.x*size.x,y=(1-b.y)*size.y,
                    width=(b.x-a.x)*size.x,height=(b.y-a.y)*size.y});
            }
            return frame;
        }
        static void Require(bool condition,string message)
        {if(!condition)throw new InvalidOperationException(message);}
    }
}
