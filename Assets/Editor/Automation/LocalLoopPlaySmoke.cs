using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
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
                    if(!home.plant.seed.activeSelf || !home.homeCanvas.gameObject.activeSelf)
                        throw new Exception("Fresh home did not show the saved seed");
                    home.activityTab.onClick.Invoke();
                    if(!nav.activitiesPanel.activeSelf || home.gardenVisuals.activeSelf)
                        throw new Exception("Activity tab did not park the garden camera");
                    nav.ShowIsland();home.settingsTab.onClick.Invoke();
                    if(!nav.settingsPanel.activeSelf || home.homeCanvas.gameObject.activeSelf)
                        throw new Exception("Settings tab did not open existing settings");
                    nav.ShowActivities();
                    ui.SelectMission(MindfulnessContent.Course[0]);
                    string id=ui.Service.Snapshot.Sessions.Single().SessionId;
                    if(!ui.Service.StartSession(id))throw new Exception(ui.Service.LastError);
                    ui.Finish(null,null);
                    if(ui.Service.Snapshot.Nutrient!=10 || ui.Service.Snapshot.NextCourseOrder!=2)throw new Exception("Runtime completion mismatch");
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
                    if(ui.Service.Snapshot.Nutrient!=0 || ui.Service.Snapshot.NextCourseOrder!=2 || ui.Service.Snapshot.Sessions.Count!=1
                        || !home.plant.sprout.activeSelf)throw new Exception("Process restart failed to restore committed progress and growth");
                    string id=ui.Service.Snapshot.Sessions.Single().SessionId;
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
    }
}
