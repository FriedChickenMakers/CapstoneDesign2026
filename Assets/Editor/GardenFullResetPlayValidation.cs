using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using CapstoneDesign.Runtime;
using CapstoneDesign.Runtime.LocalState;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace CapstoneDesign.EditorTools
{
    // Batch-only, copied real scene with a synthetic application identity and
    // isolated state root. No Editor windows, input devices or user saves.
    [InitializeOnLoad]
    public static class GardenFullResetPlayValidation
    {
        const string Key="Capstone.FullResetPlay.Session", Errors=Key+".Errors", FirstError=Key+".FirstError";
        const string Smoke="CAPSTONE_SMOKE_SAVE_ROOT", Personalization="CAPSTONE_PERSONALIZATION_TEST_ROOT";
        [Serializable] sealed class Session
        {
            public string output,saveRoot,stage,error,utc,unityVersion,oldSmoke,oldPersonalization,expectedState,firstRuntimeError;
            public int phase=1,assertions,runtimeErrors;
            public double deadline;
            public bool environmentRestored,smokeWasSet,personalizationWasSet;
            public List<string> checks=new List<string>(),captures=new List<string>();
            public string scope="Hidden copied MockupMain Play; synthetic save; public reset coordinator with simulated native cleanup; actual garden resources and restart. Settings pointer clicks and platform cleanup tested separately.";
        }
        static GardenFullResetPlayValidation(){EditorApplication.update+=Tick;Application.logMessageReceived+=ObserveLog;}
        static Session Read(){var value=SessionState.GetString(Key,"");return value.Length==0?null:JsonUtility.FromJson<Session>(value);}
        static void Save(Session value)=>SessionState.SetString(Key,JsonUtility.ToJson(value));
        static void Assert(bool value,string message){if(!value)throw new InvalidOperationException(message);}
        static void Check(Session session,bool value,string message){Assert(value,message);session.assertions++;session.checks.Add("Phase "+session.phase+": "+message);Save(session);}
        static void ObserveLog(string message,string stack,LogType type)
        {
            if(type!=LogType.Error && type!=LogType.Exception && type!=LogType.Assert)return;
            if(SessionState.GetString(Key,"").Length==0)return;
            SessionState.SetInt(Errors,SessionState.GetInt(Errors,0)+1);
            if(SessionState.GetString(FirstError,"").Length==0)SessionState.SetString(FirstError,type+": "+message+"\n"+stack);
        }
        public static void RunBatch()
        {
            try
            {
                string project=Path.GetFullPath(Directory.GetCurrentDirectory());
                Assert(Application.isBatchMode && Application.companyName=="CapstoneSyntheticValidation"
                    && Application.productName.StartsWith("FullResetPlay-",StringComparison.Ordinal),"Synthetic batch identity is required before Editor startup.");
                Assert(project.IndexOf(Path.DirectorySeparatorChar+"artifacts"+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase)>=0
                    && Path.GetFileName(project)=="project","Only the copied artifacts project is allowed.");
                var args=Environment.GetCommandLineArgs();int index=Array.IndexOf(args,"-fullResetPlayOutput");
                Assert(index>=0 && index+1<args.Length,"Missing isolated output.");string output=Path.GetFullPath(args[index+1]);
                Assert(output==Path.GetFullPath(Path.Combine(project,"..","play")),"Output must belong to this copied project.");
                EditorSceneManager.OpenScene("Assets/Scenes/MockupMain.unity",OpenSceneMode.Single);
                double earliest=EditorApplication.timeSinceStartup+3;
                EditorApplication.CallbackFunction begin=null;
                begin=()=>{if(EditorApplication.timeSinceStartup<earliest || EditorApplication.isCompiling || EditorApplication.isUpdating)return;
                    EditorApplication.update-=begin;Begin(output);};
                EditorApplication.update+=begin;
            }
            catch(Exception error){Debug.LogException(error);EditorApplication.Exit(1);}
        }
        static void Begin(string output)
        {
            Assert(!Directory.Exists(output),"A fresh output directory is required.");Directory.CreateDirectory(output);
            var session=new Session{output=output,saveRoot=Path.Combine(output,"synthetic-save"),stage="starting",utc=DateTimeOffset.UtcNow.ToString("O"),
                unityVersion=Application.unityVersion,deadline=EditorApplication.timeSinceStartup+120,
                oldSmoke=Environment.GetEnvironmentVariable(Smoke),oldPersonalization=Environment.GetEnvironmentVariable(Personalization)};
            session.smokeWasSet=session.oldSmoke!=null;session.personalizationWasSet=session.oldPersonalization!=null;
            SessionState.EraseInt(Errors);SessionState.EraseString(FirstError);Save(session);
            try
            {
                using(var store=new FileStateStore(StatePath(session)))store.Save(RichState());
                Environment.SetEnvironmentVariable(Smoke,session.saveRoot);Environment.SetEnvironmentVariable(Personalization,null);
                EditorApplication.isPaused=false;EditorApplication.EnterPlaymode();
            }
            catch(Exception error){Stop(session,error);}
        }
        static string StatePath(Session session)=>Path.Combine(session.saveRoot,"garden-mock","state.xml");
        static void Tick()
        {
            var session=Read();if(session==null || EditorApplication.isCompiling || EditorApplication.isUpdating)return;
            try
            {
                if(session.stage=="finish"){if(!EditorApplication.isPlayingOrWillChangePlaymode)Finish(session);return;}
                if(EditorApplication.timeSinceStartup>session.deadline){Stop(session,new TimeoutException("Bounded Play phase exceeded."));return;}
                if(session.stage=="between")
                {
                    if(EditorApplication.isPlayingOrWillChangePlaymode)return;
                    session.phase++;session.stage="starting";session.deadline=EditorApplication.timeSinceStartup+120;Save(session);EditorApplication.EnterPlaymode();return;
                }
                if(session.stage=="running")
                {if(!EditorApplication.isPlayingOrWillChangePlaymode)Stop(session,new InvalidOperationException("Play stopped early."));return;}
                if(!EditorApplication.isPlaying)return;
                var nav=UnityEngine.Object.FindFirstObjectByType<MockupNavigation>(FindObjectsInactive.Include);
                var ui=nav?.activitiesPanel?.GetComponent<WeekOneQuestDemo>();if(ui?.Service==null || !ui.HasOpenState)return;
                Assert(AndroidPlatformBridge.InputMode==PlatformInputMode.Mock && Environment.GetEnvironmentVariable(Smoke)==session.saveRoot,"MOCK save isolation was lost.");
                session.stage="running";session.deadline=EditorApplication.timeSinceStartup+120;Save(session);Application.runInBackground=true;
                nav.StartCoroutine(RunGuarded(session,nav,ui));
            }
            catch(Exception error){Stop(session,error);}
        }
        sealed class MemoryStore:IStateStore
        {
            string xml;public GardenState Load()=>xml==null?null:StateCodec.Decode(xml);
            public void Save(GardenState state){xml=StateCodec.Encode(state);}
        }
        static void Complete(GardenStateService service,string id,string code)
        {
            Assert(service.BeginFocusSession(id,code)&&service.StartSession(id),"Fixture start: "+service.LastError);
            var mission=FocusMissionCatalog.FindForSession(service.Snapshot.Sessions.Single(item=>item.SessionId==id));
            for(int i=0;i<mission.Steps.Length;i++)
            {var step=mission.Steps[i];string value=step.RequiredAnswer?step.Options.FirstOrDefault()??"synthetic answer":null;
                Assert(service.ConfirmFocusStep(id,i,value,value==null),"Fixture step: "+service.LastError);}
            Assert(service.CompletePracticeSession(id),"Fixture completion: "+service.LastError);
        }
        static void CompleteDay(GardenStateService service,string suffix,string code)
        {
            Complete(service,"reset-p-"+suffix,code);Complete(service,"reset-m-"+suffix,"M03");
            string id=DailyEmotionPolicy.RecordId(service.CurrentCycleId);
            Assert(service.BeginDailyEmotion(service.CurrentCycleId)&&service.CompleteDailyEmotion(id,"평온함","synthetic memo "+suffix),service.LastError);
        }
        static GardenState RichState()
        {
            var store=new MemoryStore();
            using(var service=new GardenStateService(store,new SystemClock(),new DemoBalanceConfig{VisitorChancePercent=0},
                new LegacySnapshot{Nutrient=1000,GardenXp=93,Growth=.2f}))
            {
                Assert(service.Open()&&service.RefreshCycle(Array.Empty<string>())&&service.SelectMbctProgram("8w"),service.LastError);
                CompleteDay(service,"one","P01");
                Assert(service.PurchaseEnvironment("reset-flower","environment:E01","left")&&service.PurchaseEnvironment("reset-shelter","environment:E02","right"),service.LastError);
                Assert(service.SaveExperience("reset-journal","중립","synthetic event","synthetic body","synthetic feeling","synthetic thought"),service.LastError);
                string first=service.Snapshot.PlantAppearance.Recipe.InstanceId;
                Assert(service.SetPlantCompleted(first,true)&&service.SavePlantPhoto("reset-photo",first)&&service.StartNewPlant("reset-seed",first),service.LastError);
                Assert(service.AdvancePreviewDay(Array.Empty<string>()),service.LastError);CompleteDay(service,"two","P02");
                Assert(service.AdvancePreviewDay(Array.Empty<string>())&&service.BeginFocusSession("reset-pending","P03")
                    &&service.StartGuidedFocusSession("reset-pending","")&&service.SaveGuidedFocusDraft("reset-pending","",FocusMissionCatalog.Find("P03").Steps[1].Options.FirstOrDefault(),18),service.LastError);
                return service.Snapshot;
            }
        }
        static IEnumerator RunGuarded(Session session,MockupNavigation nav,WeekOneQuestDemo ui)
        {
            var steps=Steps(session,nav,ui);
            while(true)
            {
                bool moved=false;object yielded=null;Exception failure=null;
                try{moved=steps.MoveNext();if(moved)yielded=steps.Current;}catch(Exception error){failure=error;}
                if(failure!=null){Stop(session,failure);yield break;}if(!moved)yield break;yield return yielded;
            }
        }
        static Mesh[] Meshes(Component component)=>component==null?Array.Empty<Mesh>():component.GetComponentsInChildren<MeshFilter>(true)
            .Select(item=>item.sharedMesh).Where(mesh=>mesh!=null).Distinct().ToArray();
        static IEnumerator Steps(Session session,MockupNavigation nav,WeekOneQuestDemo ui)
        {
            yield return null;yield return null;
            Check(session,SessionState.GetInt(Errors,0)==0,"Real scene starts without errors");
            Check(session,!PersonalizationClient.Instance.Configured&&!PersonalizationClient.Instance.Consented,"Synthetic MOCK cannot upload research records");
            Check(session,nav.home.RenderedRecipe!=null&&nav.home.gardenCamera.targetTexture!=null,"Real garden renders through its existing camera target");
            if(session.phase==1)
            {
                var before=ui.Service.Snapshot;string oldInstallation=before.InstallationId,oldPlant=before.PlantAppearance.Recipe.InstanceId;
                Check(session,before.Coins>0&&before.PlantPhotos.Count==1&&before.PlantPhotos[0].Memos.Count==1
                    &&before.PlantAppearance.Memos.Count==1&&before.Environments.Count==2&&before.PreviewDayOffset==2&&before.Sessions.Count==5,
                    "Fixture contains coins, completed and draft missions, archived and current memos, photos, scenery and simulated date");
                Check(session,nav.home.MemoView!=null&&nav.home.MemoView.MemoCount==1&&nav.home.MemoView.BatchCount>0,"Current memo has a real rendered mesh");
                var oldMeshes=Meshes(nav.home.ProceduralPlant);var oldMemoMeshes=Meshes(nav.home.MemoView);
                var additions=nav.home.gardenRoot.Find("Saved garden additions");
                Check(session,oldMeshes.Length>0&&oldMemoMeshes.Length>0&&additions!=null&&additions.childCount>=2,"Old plant, memo and purchased scenery are present in the scene");
                Capture(session,nav.home,"before-reset");
                nav.ShowActivities();ui.SelectFocusSession("reset-pending");yield return null;
                var timer=ui.GetComponentsInChildren<FocusPracticeClock>(true).FirstOrDefault(item=>item.isActiveAndEnabled);
                Check(session,timer!=null&&timer.ElapsedSeconds>=18,"Actual guided UI restores the saved timer and draft");
                if(!timer.Running)timer.ToggleRunning();yield return new WaitForSecondsRealtime(.2f);
                Check(session,timer.Running,"Actual practice timer is running before reset");
                string path=StatePath(session);int cleanups=0;
                ui.FullResetCleanupOverride=_=>{cleanups++;return new PlatformActionResult{status="ERROR",message="synthetic cleanup failure"};};
                Check(session,!ui.ResetAllData("synthetic-full-reset",oldInstallation)&&ui.HasPendingFullReset&&cleanups==1&&File.Exists(path+".reset"),
                    "Cleanup failure keeps the authoritative reset pending and reports failure");
                Check(session,!ui.Service.AddTestCoins()&&!ui.Service.SaveGuidedFocusDraft("reset-pending","","stale callback",999),"Pending reset blocks stale wallet and draft writes");
                Check(session,timer==null||!timer.isActiveAndEnabled,"Reset disables the previous live timer immediately");
                ui.FullResetCleanupOverride=_=>{cleanups++;return new PlatformActionResult{status="AVAILABLE"};};
                Check(session,ui.TryContinueFullReset()&&!ui.HasPendingFullReset&&cleanups==2,"Retry finishes the same durable reset without restarting it");
                AssertFresh(session,ui.Service.Snapshot,oldInstallation,oldPlant,true);
                Check(session,!File.Exists(path+".reset")&&!File.Exists(path+".bak")&&!File.Exists(path+".tmp")&&!File.Exists(path+".reset.tmp"),"Reset journal and old backup/staging files are removed after success");
                yield return null;yield return null;nav.ShowIsland();yield return null;
                var fresh=ui.Service.Snapshot;
                Check(session,nav.home.RenderedRecipe.InstanceId==fresh.PlantAppearance.Recipe.InstanceId&&nav.home.RenderedRecipe.GrowthStep==0,
                    "Actual home replaces the old plant with the new seed identity");
                Check(session,nav.home.MemoView.MemoCount==0&&nav.home.MemoView.BatchCount==0&&oldMemoMeshes.All(mesh=>mesh==null),"Old memo meshes are released, not only hidden");
                Check(session,oldMeshes.All(mesh=>mesh==null),"Old generated plant meshes are released");
                Check(session,additions==null&&!nav.home.gardenRoot.GetComponentsInChildren<Transform>(true)
                    .Any(item=>item.name=="environment:E01"||item.name=="environment:E02"),"Old purchased flower bed and shelter scene objects are destroyed");
                Check(session,!ui.GetComponentsInChildren<FocusPracticeClock>(true).Any(item=>item.isActiveAndEnabled),"No old practice timer survives reset");
                string exact=StateCodec.Encode(fresh);
                Check(session,ui.ResetAllData("synthetic-full-reset",oldInstallation)&&StateCodec.Encode(ui.Service.Snapshot)==exact&&cleanups==2,
                    "Completed duplicate reset does not repeat cleanup or change the fresh state");
                Capture(session,nav.home,"after-reset");
                // Normal first startup creates today's decision. Establish it once
                // before comparing the byte-identical state across the next Play.
                Check(session,ui.RefreshCycle(),"Fresh first-day initialization succeeds");
                session.expectedState=StateCodec.Encode(ui.Service.Snapshot);
                File.WriteAllText(Path.Combine(session.output,"expected-fresh-state.xml"),session.expectedState);
                session.stage="between";session.deadline=EditorApplication.timeSinceStartup+120;Save(session);EditorApplication.ExitPlaymode();yield break;
            }
            Check(session,StateCodec.Encode(ui.Service.Snapshot)==session.expectedState,"Second Play restores the exact fresh generation and first-day state");
            AssertFresh(session,ui.Service.Snapshot,null,null,false);
            Check(session,nav.home.RenderedRecipe.GrowthStep==0&&(nav.home.MemoView==null||nav.home.MemoView.MemoCount==0),"Restart renders the initial seed with no former memo");
            Check(session,!ui.Service.Snapshot.Environments.Any()&&nav.home.gardenRoot.Find("Saved garden additions")!=null,"Restart retains no purchased scenery");
            Capture(session,nav.home,"restarted-fresh-garden");
            Check(session,SessionState.GetInt(Errors,0)==0,"Both actual Play lifecycles log no errors or exceptions");
            session.stage="finish";Save(session);EditorApplication.ExitPlaymode();
        }
        static void AssertFresh(Session session,GardenState state,string oldInstallation,string oldPlant,bool strict)
        {
            Check(session,state.MigrationCompleted&&!state.FullResetPending&&state.InstallationId!=oldInstallation
                &&state.PlantAppearance.Recipe.InstanceId!=oldPlant,"Reset uses a migrated new installation and unique new plant identity");
            Check(session,state.Coins==0&&state.GardenXp==0&&state.LegacyGrowth==0&&state.StoredGrowthSteps==0&&state.PreviewDayOffset==0,"Wallet, XP, growth bank and app date reset to zero");
            Check(session,!state.MbctProgramChosen&&state.MbctProgramSelectionVersion==1&&state.NextCourseOrder==1&&state.MbctNextOrder==1,"Course returns to initial unchosen state");
            foreach(var field in typeof(GardenState).GetFields(BindingFlags.Public|BindingFlags.Instance).Where(field=>typeof(IList).IsAssignableFrom(field.FieldType)))
            {
                if(!strict&&field.Name=="DailyDecisions")continue;
                Check(session,((IList)field.GetValue(state)).Count==0,"Old state collection is empty: "+field.Name);
            }
            Check(session,GardenPlantVisuals.HasCurrentPlant(state)&&!GardenPlantVisuals.IsCompleted(state)&&state.PlantAppearance.Recipe.GrowthStep==0
                &&state.PlantAppearance.Memos.Count==0&&state.PlantAppearance.Credits.Count==0,"New seed contains no old completion, memo or appearance credits");
        }
        static void Capture(Session session,GardenHomePresenter home,string name)
        {
            var target=home.gardenCamera.targetTexture;Assert(target!=null&&target.IsCreated(),"Garden render target unavailable.");
            var previous=RenderTexture.active;Texture2D image=null;
            try
            {
                RenderTexture.active=target;image=new Texture2D(target.width,target.height,TextureFormat.RGB24,false);
                image.ReadPixels(new Rect(0,0,target.width,target.height),0,0);image.Apply();
                var values=image.GetPixels32().Where((value,index)=>index%31==0).Select(value=>value.r+value.g+value.b).ToArray();
                Assert(values.Max()-values.Min()>=90,"Garden target is blank.");
                string path=Path.Combine(session.output,name+"-garden-target-only.png");File.WriteAllBytes(path,image.EncodeToPNG());session.captures.Add(path);Save(session);
            }
            finally{RenderTexture.active=previous;if(image!=null)UnityEngine.Object.Destroy(image);}
        }
        static void Stop(Session session,Exception error)
        {if(string.IsNullOrEmpty(session.error))session.error=error.ToString();session.stage="finish";Save(session);if(EditorApplication.isPlayingOrWillChangePlaymode)EditorApplication.ExitPlaymode();else Finish(session);}
        static void Finish(Session session)
        {
            session.runtimeErrors=SessionState.GetInt(Errors,0);session.firstRuntimeError=SessionState.GetString(FirstError,"");
            if(session.runtimeErrors>0&&string.IsNullOrEmpty(session.error))session.error=session.firstRuntimeError;
            // JsonUtility turns absent string values into empty strings across a
            // Play domain reload, so persist presence separately from the value.
            string smoke=session.smokeWasSet?session.oldSmoke:null,personalization=session.personalizationWasSet?session.oldPersonalization:null;
            Environment.SetEnvironmentVariable(Smoke,smoke);Environment.SetEnvironmentVariable(Personalization,personalization);
            session.environmentRestored=Environment.GetEnvironmentVariable(Smoke)==smoke&&Environment.GetEnvironmentVariable(Personalization)==personalization;
            if(!session.environmentRestored&&string.IsNullOrEmpty(session.error))session.error="Environment restoration failed.";
            session.stage=string.IsNullOrEmpty(session.error)?"pass":"fail";
            File.WriteAllText(Path.Combine(session.output,"play-report.json"),JsonUtility.ToJson(session,true));
            SessionState.EraseString(Key);SessionState.EraseInt(Errors);SessionState.EraseString(FirstError);
            Debug.Log("FULL_RESET_PLAY "+session.stage+" phases="+session.phase+" assertions="+session.assertions);
            EditorApplication.Exit(session.stage=="pass"?0:1);
        }
    }
}
