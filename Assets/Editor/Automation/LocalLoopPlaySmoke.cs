using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
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
                if(phase=="fresh")
                {
                    if(ui.Service.Snapshot.Sessions.Count!=0)throw new Exception("Fresh fixture directory already contains sessions");
                    ui.SelectMission(MindfulnessContent.Course[0]);
                    string id=ui.Service.Snapshot.Sessions.Single().SessionId;
                    if(!ui.Service.StartSession(id))throw new Exception(ui.Service.LastError);
                    ui.Finish(null,null);
                    if(ui.Service.Snapshot.Nutrient!=10 || ui.Service.Snapshot.NextCourseOrder!=2)throw new Exception("Runtime completion mismatch");
                    ui.PreviewEnvironment("environment:E01");nav.ShowIsland();
                    if(GameObject.Find("PREVIEW environment:E01")!=null || ui.Service.Snapshot.Nutrient!=10)throw new Exception("Navigation did not cancel unpaid preview");
                }
                else
                {
                    if(ui.Service.Snapshot.Nutrient!=10 || ui.Service.Snapshot.NextCourseOrder!=2 || ui.Service.Snapshot.Sessions.Count!=1)throw new Exception("Process restart failed to restore committed progress");
                    string id=ui.Service.Snapshot.Sessions.Single().SessionId;
                    if(!ui.Service.CompleteSession(id) || ui.Service.Snapshot.Nutrient!=10)throw new Exception("Restart retry duplicated reward");
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
