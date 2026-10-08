using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using CapstoneDesign.Runtime;
using CapstoneDesign.Runtime.LocalState;

namespace CapstoneDesign.EditorTools
{
    [InitializeOnLoad]
    public static class LocalLoopPlaySmoke
    {
        const string Key="Capstone.LocalLoopSmoke.Active";
        static LocalLoopPlaySmoke()
        {
            if(SessionState.GetBool(Key,false))EditorApplication.update+=WaitForPlay;
        }
        public static void Start()
        {
            if(string.IsNullOrEmpty(Environment.GetEnvironmentVariable("CAPSTONE_SMOKE_SAVE_ROOT")))throw new Exception("Explicit isolated smoke save directory required");
            EditorSceneManager.OpenScene("Assets/Scenes/MockupMain.unity");
            SessionState.SetBool(Key,true);
            SessionState.SetFloat(Key+".Deadline",(float)EditorApplication.timeSinceStartup+60);
            EditorApplication.update-=WaitForPlay;EditorApplication.update+=WaitForPlay;
            EditorApplication.EnterPlaymode();
        }
        static void WaitForPlay()
        {
            if(EditorApplication.timeSinceStartup>SessionState.GetFloat(Key+".Deadline",0))
            {
                EditorApplication.update-=WaitForPlay;SessionState.SetBool(Key,false);
                Debug.LogError("Play smoke startup timed out");EditorApplication.Exit(1);return;
            }
            if(!EditorApplication.isPlaying)return;
            var loop=UnityEngine.Object.FindFirstObjectByType<WeekOneQuestDemo>(FindObjectsInactive.Include);
            if(loop?.Service==null)return;
            EditorApplication.update-=WaitForPlay;Run();
        }
        static void Run()
        {
            string root=Environment.GetEnvironmentVariable("CAPSTONE_SMOKE_SAVE_ROOT");
            string phase=Environment.GetEnvironmentVariable("CAPSTONE_SMOKE_PHASE") ?? "fresh";
            try
            {
                var nav=UnityEngine.Object.FindFirstObjectByType<MockupNavigation>();
                if(nav==null)throw new Exception("Serialized navigation missing in Play mode");
                if(AndroidPlatformBridge.InputMode!=PlatformInputMode.Mock)throw new Exception("Smoke must remain MOCK");
                nav.ShowActivities();
                var ui=nav.activitiesPanel.GetComponent<WeekOneQuestDemo>();
                if(ui?.Service==null)throw new Exception("Awake did not initialize local state");
                var home=nav.home;
                if(home==null || home.gardenCamera==null || home.gardenCamera.enabled)
                    throw new Exception("Integrated home or on-demand garden camera missing in Play mode");
                if(phase=="fresh")
                {
                    if(ui.Service.Snapshot.Sessions.Count!=0)throw new Exception("Fresh fixture directory already contains sessions");
                    nav.ShowIsland();
                    AssertLegacyRendererParked(home);
                    if(!home.plant.seed.activeSelf || !home.homeCanvas.gameObject.activeSelf)
                        throw new Exception("Fresh home did not show the saved seed");
                    home.activityTab.onClick.Invoke();
                    AssertLegacyRendererParked(home);
                    if(!nav.activitiesPanel.activeSelf || home.gardenVisuals.activeSelf)
                        throw new Exception("Activity tab did not park the garden camera");
                    AssertRaycastTarget(ui.GetComponentsInChildren<Button>().Single(button=>button.name=="추천 활동 안내"));
                    nav.ShowIsland();home.settingsTab.onClick.Invoke();
                    AssertLegacyRendererParked(home);
                    if(!nav.settingsPanel.activeSelf || home.IsGardenVisible || !home.settingsTab.gameObject.activeInHierarchy)
                        throw new Exception("Settings tab did not open existing settings");
                    AssertRaycastTarget(nav.GetComponentInChildren<SensorRawDisplay>(true).GetComponentsInChildren<Button>()
                        .Single(button=>button.name=="심박 차트 보기"));
                    nav.ShowActivities();
                    // Five nutrients of synthetic legacy participation fund the ten-nutrient garden check.
                    if(!ui.Service.BeginSession("synthetic-starter","free-mission:M01",0) || !ui.Service.StartSession("synthetic-starter") || !ui.Service.CompleteSession("synthetic-starter"))throw new Exception(ui.Service.LastError);
                    if(ui.Service.Snapshot.Nutrient!=5)throw new Exception("Default reward must be five");
                    ui.SelectMission(MbctContent.Course[0]);
                    string id=ui.Service.Snapshot.Sessions.Last().SessionId;
                    if(!ui.Service.StartSession(id))throw new Exception(ui.Service.LastError);
                    ui.ShowSession();
                    ui.GetComponentsInChildren<InputField>(true).Single(f=>f.gameObject.activeInHierarchy).text="물";
                    for(int step=0;step<4;step++)ui.AdvancePractice(false);
                    ui.Finish(null,null);
                    if(!ui.Service.SaveExperience("play-record","즐거움","바깥 소리","어깨가 내려감","차분해요","소리가 들린다") || !ui.Service.SavePreference("support","치료진"))throw new Exception(ui.Service.LastError);
                    if(ui.Service.Snapshot.Nutrient!=10 || ui.Service.Snapshot.MbctNextOrder!=2)throw new Exception("Runtime completion mismatch");
                    ui.PreviewEnvironment("environment:E01");nav.ShowIsland();
                    if(GameObject.Find("PREVIEW environment:E01")!=null || ui.Service.Snapshot.Nutrient!=10)throw new Exception("Navigation did not cancel unpaid preview");
                    home.shopButton.onClick.Invoke();
                    if(ui.CurrentScreen!="shop")throw new Exception("Home shop button did not open the existing garden shop");
                    ui.PreviewGrowth();nav.ShowIsland();
                    if(!home.plant.seed.activeSelf || ui.Service.Snapshot.Nutrient!=10)
                        throw new Exception("Cancelled growth changed the saved home");
                    nav.ShowActivities();ui.PreviewGrowth();
                    ui.GetComponentsInChildren<Button>(true).Single(b=>b.gameObject.activeInHierarchy && b.gameObject.name=="확정").onClick.Invoke();
                    if(ui.Service.Snapshot.Nutrient!=0 || !home.plant.sprout.activeSelf)
                        throw new Exception("Growth purchase did not update the saved home");
                }
                else
                {
                    if(ui.Service.Snapshot.Nutrient!=0 || ui.Service.Snapshot.MbctNextOrder!=2 || ui.Service.Snapshot.Sessions.Count!=2
                        || !home.plant.sprout.activeSelf)throw new Exception("Process restart failed to restore committed progress and growth");
                    if(ui.Service.Snapshot.Experiences.Single().Emotion!="차분해요" || ui.Service.Snapshot.PracticePreferences.Single(a=>a.Key=="support").Value!="치료진")throw new Exception("Restart lost journal or plan");
                    string id=ui.Service.Snapshot.Sessions.Single(s=>MbctPolicy.IsMbct(s.MissionId)).SessionId;
                    if(!ui.Service.CompleteSession(id) || ui.Service.Snapshot.Nutrient!=0)throw new Exception("Restart retry duplicated reward");
                    // A step sync may commit rewards while the activity page is hidden.
                    // Reopening that existing page must agree with the main garden.
                    ui.ShowHome();nav.ShowIsland();
                    if(!ui.ToggleDebugWalk())throw new Exception("Could not enable synthetic debug walk");
                    for(int i=0;i<10;i++)if(!ui.AddVirtualDebugStep())throw new Exception("Synthetic step failed");
                    nav.ShowActivities();
                    if(ui.Service.Snapshot.Nutrient!=1 || !home.guide.text.Contains("영양제 1개")
                        || !ui.GetComponentsInChildren<Text>().Any(t=>t.text.Contains("영양제 1  ·  코스")))
                        throw new Exception("Activity header retained stale nutrients after a hidden reward update");
                }
                File.WriteAllText(Path.Combine(root,"play-"+phase+".json"),"{\"status\":\"PASS\",\"mode\":\"MOCK\",\"environment\":\"Unity Editor Play mode\",\"phase\":\""+phase+"\"}");
                Debug.Log("LOCAL_LOOP_PLAY_SMOKE_PASS "+phase);
                SessionState.SetBool(Key,false);EditorApplication.Exit(0);
            }
            catch(Exception ex)
            {
                Directory.CreateDirectory(root);File.WriteAllText(Path.Combine(root,"play-"+phase+".json"),"{\"status\":\"FAIL\"}");
                Debug.LogException(ex);SessionState.SetBool(Key,false);EditorApplication.Exit(1);
            }
        }

        static void AssertLegacyRendererParked(GardenHomePresenter home)
        {
            if(home.oldCamera.enabled || home.oldLight.enabled)
                throw new Exception("Integrated tab rendered the hidden legacy camera or light");
        }

        static void AssertRaycastTarget(Button target)
        {
            // The home Canvas stays mounted to share its navigation. Callback-only
            // tests cannot detect a transparent home Graphic intercepting these taps.
            Canvas.ForceUpdateCanvases();
            var eventSystem=EventSystem.current;
            if(eventSystem==null || target==null || !target.IsActive() || !target.IsInteractable())
                throw new Exception("Raycast smoke requires an active EventSystem and enabled button");
            var rect=(RectTransform)target.transform;
            var canvas=target.GetComponentInParent<Canvas>().rootCanvas;
            Camera camera=canvas.renderMode==RenderMode.ScreenSpaceOverlay?null:canvas.worldCamera;
            Vector2 point=RectTransformUtility.WorldToScreenPoint(camera,rect.TransformPoint(rect.rect.center));
            if(!new Rect(0,0,Screen.width,Screen.height).Contains(point))
                throw new Exception("Raycast target is outside the visible screen: "+target.name+" at "+point);
            var hits=new List<RaycastResult>();
            eventSystem.RaycastAll(new PointerEventData(eventSystem){position=point},hits);
            var reached=hits.Count==0?null:hits[0].gameObject.GetComponentInParent<Button>();
            if(reached!=target)
                throw new Exception("Tap blocked for "+target.name+" at "+point+"; raycast order: "+
                    string.Join(" > ",hits.Take(5).Select(hit=>hit.gameObject.name)));
            Debug.Log("LOCAL_LOOP_RAYCAST_PASS "+target.name+" at "+point);
        }
    }
}
