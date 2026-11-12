using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using CapstoneDesign.Runtime;
using CapstoneDesign.Runtime.LocalState;
using CapstoneDesign.Runtime.Personalization;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.Build.Player;
using Mono.Cecil;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace CapstoneDesign.EditorTools
{
    // Explicit hidden isolated batch project only; no poll and no main-Editor actions.
    public static class FullResetValidation
    {
        [Serializable] sealed class Check { public string name,status,detail; }
        [Serializable] sealed class Report
        {
            public string utc,unityVersion,environment; public int passed,failed,assertions,clicks,raycasts;
            public List<Check> checks=new List<Check>(); public List<string> captures=new List<string>(),errors=new List<string>();
        }
        static Report report;static string output;static bool capture;
        public static void RunBatch()
        {
            var args=Environment.GetCommandLineArgs();int index=Array.IndexOf(args,"-fullResetReport");
            if(!Application.isBatchMode||Application.companyName!="CapstoneSyntheticValidation"||!Application.productName.StartsWith("FullReset-",StringComparison.Ordinal)||index<0)
                throw new InvalidOperationException("Full reset validation requires an isolated synthetic batch project.");
            string path=args[index+1];output=Path.GetDirectoryName(path);capture=true;
            report=new Report{utc=DateTimeOffset.UtcNow.ToString("O"),unityVersion=Application.unityVersion,environment="Hidden isolated GPU Editor fixture; actual settings/page factories, memory save and injected app cleanup; not Android-device execution"};
            Application.logMessageReceived+=Observe;
            try
            {
                Run("Settings cancel, failure, pending cleanup and completion 360x640",()=>SettingsFlow(360,640));
                Run("Settings cancel, failure, pending cleanup and completion 390x844",()=>SettingsFlow(390,844));
                Run("Pending reset survives recreated presenter and resumes cleanup",PendingRestart);
                Run("Guided callbacks and development service cannot revive reset data",StaleCallbacks);
                Run("Same-day daily record identity cannot revive an old save callback",SameDayOldEmotionCallback);
                Run("Inactive activity panel still recovers pending reset before normal polling",InactivePanelRecovery);
                Run("Release compilation excludes test cleanup injection",ReleaseScripts);
                Run("Actual local cleanup uses only isolated app-owned stores and preferences",LocalCleanup);
            }
            finally
            {
                Application.logMessageReceived-=Observe;File.WriteAllText(path,JsonUtility.ToJson(report,true));
                EditorApplication.Exit(report.failed==0&&report.errors.Count==0?0:1);
            }
        }
        static void Observe(string message,string trace,LogType type)
        {if(type==LogType.Error||type==LogType.Exception||type==LogType.Assert)report.errors.Add(message+"\n"+trace);}
        static void Run(string name,Action action)
        {try{action();report.passed++;report.checks.Add(new Check{name=name,status="pass"});}catch(Exception ex){report.failed++;report.checks.Add(new Check{name=name,status="fail",detail=ex.ToString()});}}
        static void Assert(bool value,string message){report.assertions++;if(!value)throw new InvalidOperationException(message);}
        static string Xml(GardenStateService service)=>StateCodec.Encode(service.Snapshot);
        static string Screen(WeekOneQuestDemo ui)=>(string)typeof(WeekOneQuestDemo).GetField("screen",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(ui);
        static void Complete(GardenStateService service,string id,string code,int order=0)
        {
            Assert(service.BeginFocusSession(id,code,order)&&service.StartSession(id),"Create synthetic mission: "+service.LastError);
            var definition=FocusMissionCatalog.FindForSession(service.Snapshot.Sessions.Single(item=>item.SessionId==id));
            for(int index=0;index<definition.Steps.Length;index++)
            {var step=definition.Steps[index];string answer=step.RequiredAnswer?step.Options.FirstOrDefault()??"synthetic answer":null;Assert(service.ConfirmFocusStep(id,index,answer,answer==null),"Confirm synthetic step");}
            Assert(service.CompletePracticeSession(id),"Complete synthetic mission: "+service.LastError);
        }
        static Store Seed()
        {
            var store=new Store();var clock=new Clock();
            using(var service=new GardenStateService(store,clock,new DemoBalanceConfig{VisitorChancePercent=0},new LegacySnapshot{Nutrient=1000,GardenXp=73,Growth=.2f}))
            {
                Assert(service.Open()&&service.RefreshCycle(Array.Empty<string>())&&service.SelectMbctProgram("8w"),"Create rich synthetic reset source");
                Complete(service,"old-p","P01",1);Complete(service,"old-m","M03");
                string id=DailyEmotionPolicy.RecordId(service.CurrentCycleId);
                Assert(service.BeginDailyEmotion(service.CurrentCycleId)&&service.CompleteDailyEmotion(id,"평온함","synthetic archived reset memo"),"Attach old memo");
                Assert(service.PurchaseEnvironment("old-furniture","environment:E01","left")&&service.SaveExperience("old-experience","중립","synthetic event","synthetic body","synthetic mood","synthetic thought"),"Seed scenery and record");
                string source=service.Snapshot.PlantAppearance.Recipe.InstanceId;
                Assert(service.SetPlantCompleted(source,true)&&service.SavePlantPhoto("old-photo",source)&&service.StartNewPlant("old-new-plant",source)&&service.AdvancePreviewDay(),"Archive old individual and use app date offset");
                Complete(service,"today-p","P02",2);Complete(service,"today-m","M03");id=DailyEmotionPolicy.RecordId(service.CurrentCycleId);
                Assert(service.BeginDailyEmotion(service.CurrentCycleId)&&service.SaveDailyEmotionDraft(id,"평온함","synthetic pending reset note",1),"Save pending daily draft");
            }
            return store;
        }
        static void Fresh(GardenState state,string prior)
        {
            Assert(state.InstallationId!=prior&&state.Coins==0&&state.GardenXp==0&&state.LegacyGrowth==0&&state.PreviewDayOffset==0,"Reset changes generation and restores zero economy/date defaults");
            Assert(!state.MbctProgramChosen&&state.Sessions.Count==0&&state.Experiences.Count==0&&state.DailyEmotionRecords.Count==0&&state.PracticePreferences.Count==0,"Reset restores first course selection and clears all written activity detail");
            Assert(state.PlantPhotos.Count==0&&state.PhotoSaveCommands.Count==0&&state.PlantStartCommands.Count==0&&state.StoredGrowthSteps==0&&state.PracticeGrowthRecords.Count==0&&state.RewardReceipts.Count==0,"Reset removes album, rewards and stored growth");
            Assert(state.Environments.Count==0&&state.PurchaseReceipts.Count==0&&state.PurchaseCommands.Count==0&&state.GrowthHistory.Count==0&&state.DiscoveredAnimalIds.Count==0,"Reset removes purchased scenery, growth history and visitor collection");
            Assert(GardenPlantVisuals.HasCurrentPlant(state)&&!GardenPlantVisuals.IsCompleted(state)&&state.PlantAppearance.Recipe.GrowthStep==0&&state.PlantAppearance.Memos.Count==0,"Reset supplies one uncompleted default seed without prior memos");
        }
        static void SettingsFlow(int width,int height)
        {
            var store=Seed();string completed;
            using(var f=new Fixture(width,height,store))
            {
                string id=DailyEmotionPolicy.RecordId(f.Service.CurrentCycleId);f.Ui.ShowDailyEmotion(id);f.Layout();
                var oldDailySave=f.Button("DailyEmotion.Save").onClick;
                f.OpenTools();string before=Xml(f.Service),source=f.Service.Snapshot.InstallationId;
                f.Click("Settings.ResetAllData");Assert(Xml(f.Service)==before,"Opening full-reset confirmation cannot erase or mutate state");
                Assert(f.VisibleText.Contains("사진첩")&&f.VisibleText.Contains("건강")&&f.VisibleText.Contains("되돌릴 수 없어요"),"Confirmation accurately names irreversible app data removal");
                var cancelled=f.Button("Settings.ConfirmFullReset").onClick;f.Capture("settings-reset-confirmation");
                f.Click("Settings.CancelFullReset");cancelled.Invoke();f.Layout();Assert(Xml(f.Service)==before&&f.CleanupCalls.Count==0,"Cancel and old cancelled confirmation are harmless");
                f.Click("Settings.ResetAllData");f.Store.Fail=true;f.Click("Settings.ConfirmFullReset");f.Store.Fail=false;
                Assert(Xml(f.Service)==before&&f.CleanupCalls.Count==0&&f.Text("Settings.FullResetStatus").text.Contains("기존 기록"),"Failure before durable commit retains all records and does not call external cleanup");
                f.RevealAndRead(f.Text("Settings.FullResetStatus"));f.Capture("settings-reset-first-save-failed");
                f.CleanupStatus="ERROR";f.Click("Settings.ConfirmFullReset");
                Assert(f.Service.HasPendingFullReset&&f.Ui.HasPendingFullReset,"Cleanup failure retains the durable pending state");Fresh(f.Service.Snapshot,source);
                string pending=Xml(f.Service),request=f.Service.Snapshot.FullResetRequestId;
                Assert(!f.Button("Settings.CancelFullReset",false).IsInteractable(),"After durable reset commit the UI offers retry instead of cancellation");
                f.RevealAndRead(f.Text("Settings.FullResetStatus"));f.Capture("settings-reset-cleanup-pending");
                oldDailySave.Invoke();Assert(Xml(f.Service)==pending,"Old daily-save callback cannot recreate erased records during cleanup");
                f.Navigation.ShowActivities();f.Ui.ShowHome();f.Layout();
                Assert(Screen(f.Ui)=="full-reset-pending"&&f.Button("FullReset.Retry")!=null,"Mission entry is replaced with recovery while cleanup is pending");
                f.Capture("mission-reset-recovery");f.CleanupStatus="PENDING";f.Click("FullReset.Retry");Assert(Xml(f.Service)==pending,"Asynchronous cleanup continues with the exact pending generation");
                f.CleanupStatus="AVAILABLE";var retry=f.Button("FullReset.Retry").onClick;f.Click("FullReset.Retry");
                Assert(!f.Service.HasPendingFullReset&&f.CleanupCalls.All(value=>value==request),"All cleanup attempts share one durable reset request");Fresh(f.Service.Snapshot,source);
                Assert(f.Ui.GetComponentInChildren<FocusPracticeClock>(true)==null,"No old practice timer survives successful reset");
                completed=Xml(f.Service);retry.Invoke();oldDailySave.Invoke();f.Layout();Assert(Xml(f.Service)==completed,"Old recovery and daily callbacks cannot restore data after reset");
                f.OpenTools();f.RefreshSettings();
                Assert(f.Text("Settings.FullResetStatus").text=="모든 기록과 정원을 초기 상태로 되돌렸어요.","Successful reset settings display the completed status rather than stale cleanup guidance");
                Assert(!f.Settings.MissionResetConfirmButton.gameObject.activeInHierarchy,"Successful reset closes the destructive confirmation and retry controls");
                Assert(f.Settings.AddTestCoinsButton.IsInteractable()&&f.Settings.AdvanceMissionDayButton.IsInteractable(),"Successful reset restores normal development controls");
                f.RevealAndRead(f.Text("Settings.FullResetStatus"));f.Capture("settings-reset-complete");
                f.Navigation.ShowActivities();f.Ui.ShowHome();f.Layout();Assert(!f.Service.Snapshot.MbctProgramChosen&&f.VisibleText.Contains("코스"),"Initial course choice appears after reset");f.Capture("first-mission-after-full-reset");
                completed=Xml(f.Service);
            }
            using(var f=new Fixture(width,height,store))
            {Assert(Xml(f.Service)==completed,"Recreated presenter retains exact reset state without resurrecting prior user detail");f.Ui.ShowPlantAlbum();f.Layout();Assert(f.Service.Snapshot.PlantPhotos.Count==0,"Album remains empty after restart");f.Capture("empty-album-after-reset-restart");}
        }
        static void PendingRestart()
        {
            var store=Seed();string source,pending,request;
            using(var f=new Fixture(360,640,store,cleanupStatus:"ERROR"))
            {
                source=f.Service.Snapshot.InstallationId;Assert(!f.Ui.ResetAllData("synthetic-restart-reset",source)&&f.Service.HasPendingFullReset,"Commit reset with interrupted app cleanup");
                request=f.Service.Snapshot.FullResetRequestId;pending=Xml(f.Service);
            }
            using(var f=new Fixture(360,640,store,cleanupStatus:"ERROR"))
            {
                Assert(f.Service.HasPendingFullReset&&Xml(f.Service)==pending,"Restart keeps authoritative pending state and never old data");
                f.Ui.ShowHome();f.Layout();Assert(Screen(f.Ui)=="full-reset-pending","Restart presents recovery before mission controls");f.Capture("restart-pending-reset");
                f.CleanupStatus="AVAILABLE";f.Click("FullReset.Retry");Fresh(f.Service.Snapshot,source);
                Assert(!f.Service.HasPendingFullReset&&f.Service.Snapshot.FullResetRequestId==request,"Restart finishes the same reset command");
            }
        }
        static void StaleCallbacks()
        {
            using(var f=new Fixture(390,844))
            {
                Assert(f.Service.SelectMbctProgram("8w")&&f.Service.BeginFocusSession("old-guided","P01",1),"Prepare old guided page");f.Ui.SelectFocusSession("old-guided");f.Layout();
                for(int page=0;page<8&&f.Ui.GetComponentInChildren<FocusPracticeClock>(false)==null;page++)f.Click("Focus.IconAction");
                var clock=f.Ui.GetComponentInChildren<FocusPracticeClock>(false);
                Assert(clock!=null&&clock.Running&&f.Service.Snapshot.Sessions.Single(session=>session.SessionId=="old-guided").Status==ParticipationState.InProgress,
                    "Actual guided icon slides start the old practice timer before reset");
                clock.ElapsedSeconds=37;Assert(clock.Flush(),"Save old guided time");var oldFinish=f.Button("Focus.IconAction").onClick;
                string source=f.Service.Snapshot.InstallationId;Assert(f.Ui.ResetAllData("guided-reset",source),"Reset while practice timer exists");
                string saved=Xml(f.Service);oldFinish.Invoke();f.Layout();Assert(Xml(f.Service)==saved&&(clock==null||!clock.enabled),"Old guided action and timer cannot complete or repopulate a reset account");
                Assert(f.Service.SelectMbctProgram("8w"),"Choose course in new account");Complete(f.Service,"new-p","P01",1);
                string progressed=Xml(f.Service);Assert(f.Ui.ResetAllData("guided-reset",source)&&Xml(f.Service)==progressed,"Repeated old reset command cannot erase new earned progress");
                source=f.Service.Snapshot.InstallationId;var original=f.Service;f.Ui.StartMissionDebug("P03");f.Layout();Assert(f.Ui.IsMissionDebugActive&&!ReferenceEquals(f.Ui.Service,original),"Enter separate developer service");
                Assert(f.Ui.ResetSourceInstallationId==source&&f.Ui.ResetAllData("debug-reset",source)&&!f.Ui.IsMissionDebugActive&&ReferenceEquals(f.Ui.Service,original),"Full reset targets the real synthetic account rather than the temporary developer service");Fresh(original.Snapshot,source);
            }
        }
        static void SameDayOldEmotionCallback()
        {
            using(var f=new Fixture(360,640))
            {
                Assert(f.Service.SelectMbctProgram("8w"),"Select course before same-day reset");Complete(f.Service,"before-reset-p","P01",1);Complete(f.Service,"before-reset-m","M03");
                string id=DailyEmotionPolicy.RecordId(f.Service.CurrentCycleId);
                Assert(f.Service.BeginDailyEmotion(f.Service.CurrentCycleId)&&f.Service.SaveDailyEmotionDraft(id,"평온함","synthetic OLD same-day note",1),"Prepare original same-day daily draft");
                f.Ui.ShowDailyEmotion(id);f.Layout();var oldSave=f.Button("DailyEmotion.Save").onClick;
                Assert(f.Ui.ResetAllData("same-day-reset",f.Service.Snapshot.InstallationId)&&f.Service.SelectMbctProgram("8w"),"Reset and restart course on same physical day");
                Complete(f.Service,"after-reset-p","P01",1);Complete(f.Service,"after-reset-m","M03");
                Assert(DailyEmotionPolicy.RecordId(f.Service.CurrentCycleId)==id&&f.Service.BeginDailyEmotion(f.Service.CurrentCycleId)
                    &&f.Service.SaveDailyEmotionDraft(id,"편안함","synthetic NEW same-day note",1),"New generation legitimately reuses daily cycle identity with new content");
                f.Ui.ShowDailyEmotion(id);f.Layout();string before=Xml(f.Service);int calls=f.Store.Saves;
                oldSave.Invoke();f.Layout();Assert(Xml(f.Service)==before&&f.Store.Saves==calls&&!f.Service.GetDailyEmotion(id).IsCompleted,"Previous-generation daily callback cannot complete or overwrite same-ID new record");
                f.Click("DailyEmotion.Save");Assert(f.Service.CoinBalance==180&&f.Service.Snapshot.PlantAppearance.Memos.Single().Note=="synthetic NEW same-day note","Only new daily user action grants 100 coins after 50/30 mission rewards and preserves new-generation memo text");
                f.Capture("new-generation-same-day-emotion-complete");
            }
        }
        static void InactivePanelRecovery()
        {
            using(var f=new Fixture(360,640,Seed(),cleanupStatus:"ERROR"))
            {
                f.OpenSettings();Assert(!f.Ui.gameObject.activeInHierarchy,"Settings makes the activity presenter inactive");
                Assert(!f.Ui.ResetAllData("inactive-navigation-reset",f.Service.Snapshot.InstallationId)&&f.Ui.HasPendingFullReset,"Commit pending cleanup while activity presenter is inactive");
                string pending=Xml(f.Service);
                var healthRefresh=typeof(SensorRawDisplay).GetField("nextHealthRefresh",BindingFlags.Instance|BindingFlags.NonPublic);
                foreach(string method in new[]{"Update","OnSettingsOpened","OnApplicationFocus","RefreshText"})
                {
                    healthRefresh.SetValue(f.Settings,-777f);
                    typeof(SensorRawDisplay).GetMethod(method,BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic).Invoke(f.Settings,method=="OnApplicationFocus"?new object[]{true}:null);
                    Assert((float)healthRefresh.GetValue(f.Settings)==-777f&&Xml(f.Service)==pending,"Pending settings "+method+" does not schedule native health refresh or change saved data");
                    Assert(f.Settings.GetComponentsInChildren<Selectable>(true).Where(control=>control!=f.Settings.MissionResetConfirmButton&&!(control is Scrollbar)).All(control=>!control.interactable),
                        "Pending "+method+" leaves only reset confirmation and scrolling enabled");
                }
                var nextAttempt=typeof(WeekOneQuestDemo).GetField("nextFullResetAttempt",BindingFlags.Instance|BindingFlags.NonPublic);
                var cycle=typeof(MockupNavigation).GetField("nextCycleCheck",BindingFlags.Instance|BindingFlags.NonPublic);
                var update=typeof(MockupNavigation).GetMethod("Update",BindingFlags.Instance|BindingFlags.NonPublic);
                cycle.SetValue(f.Navigation,-123f);nextAttempt.SetValue(f.Ui,0f);int calls=f.CleanupCalls.Count;
                f.CleanupStatus="PENDING";update.Invoke(f.Navigation,null);
                Assert(f.CleanupCalls.Count==calls+1&&Xml(f.Service)==pending&&(float)cycle.GetValue(f.Navigation)==-123f,
                    "Navigation owner retries cleanup while hidden and returns before ordinary daily/health polling");
                f.CleanupStatus="AVAILABLE";nextAttempt.SetValue(f.Ui,0f);update.Invoke(f.Navigation,null);
                Assert(!f.Ui.HasPendingFullReset&&f.Service.CoinBalance==0&&f.Service.Snapshot.Sessions.Count==0,"Navigation owner completes cleanup without enabling the old activity panel");
                f.Settings.OnSettingsOpened();
                // MOCK/Editor returns before native refresh; actual settings Update
                // restores controls on its normal half-second polling boundary.
                f.RefreshSettings();Assert(f.Settings.AddTestCoinsButton.interactable&&f.Settings.AdvanceMissionDayButton.interactable,"Settings controls become usable again on the next settings refresh after cleanup finishes");
                f.Navigation.ShowActivities();f.Ui.ShowHome();f.Layout();Assert(Screen(f.Ui)!="full-reset-pending","Normal navigation resumes after inactive-panel recovery");
            }
        }
        static void LocalCleanup()
        {
            string isolated=Path.Combine(output,"local-cleanup"),profiles=Path.Combine(isolated,"profiles"),personal=Path.Combine(isolated,"personalization");Directory.CreateDirectory(personal);
            string previous=Environment.GetEnvironmentVariable("CAPSTONE_PERSONALIZATION_TEST_ROOT");
            string sourceSentinel=Path.Combine(isolated,"external-health-source-kept.xml");File.WriteAllText(sourceSentinel,"synthetic outside app-reset scope");
            int clients=Resources.FindObjectsOfTypeAll<PersonalizationClient>().Length;
            Environment.SetEnvironmentVariable("CAPSTONE_PERSONALIZATION_TEST_ROOT",personal);
            try
            {
                foreach(string name in new[]{"personalization-enrollment.json","behavior-queue.xml"})foreach(string suffix in new[]{"",".tmp",".bak",".new"})File.WriteAllText(Path.Combine(personal,name+suffix),"synthetic local connection content");
                var rich=Seed().Load();
                foreach(string mode in new[]{"live","mock","replay"})using(var file=new FileStateStore(Path.Combine(profiles,"garden-"+mode,"state.xml"))){file.Save(rich);file.Save(rich);}
                string active=Path.Combine(profiles,"garden-mock","state.xml");
                using(var file=new FileStateStore(active))using(var service=new GardenStateService(file,new Clock(),new DemoBalanceConfig()))
                {
                    Assert(service.Open()&&service.ResetAll("whole-local-cleanup",rich.InstallationId),"Create authoritative active reset before app-file cleanup");
                    string pending=Xml(service);
                    PlayerPrefs.SetInt("capstone.garden.nutrient",999);PlayerPrefs.SetString("synthetic.full-reset.pref","synthetic old preference");PlayerPrefs.Save();
                    foreach(string name in new[]{"platform_snapshots.jsonl","ui-profile.json","capstone-music-probe.jsonl"})File.WriteAllText(Path.Combine(Application.persistentDataPath,name),"synthetic app diagnostic record");
                    string replaySource=Path.Combine(isolated,"external-replay-source.jsonl"),recording=Path.Combine(isolated,"external-recording-output.jsonl");
                    var sample=new AndroidPlatformSnapshot{status="AVAILABLE",generatedAtEpochMs=123456789};
                    sample.healthConnect.status="AVAILABLE";sample.healthConnect.steps=new HealthMetricSnapshot{status="AVAILABLE",hasValue=true,value=4321};
                    sample.healthConnect.heartRate=new HealthMetricSnapshot{status="AVAILABLE",hasValue=true,value=73,heartRateSamples=new[]{new HeartRateSampleSnapshot{recordId="synthetic-heart",beatsPerMinute=73}}};
                    sample.sensorService.sampleCount=1;sample.sensorService.sensors=new[]{new SensorCapabilitySnapshot{key="synthetic-acceleration",status="AVAILABLE",values=new[]{1f,2f,3f}}};
                    File.WriteAllText(replaySource,JsonUtility.ToJson(sample)+Environment.NewLine);byte[] replayBytes=File.ReadAllBytes(replaySource);
                    Assert(AndroidPlatformBridge.UseReplayProvider(replaySource).status=="AVAILABLE"&&AndroidPlatformBridge.InputMode==PlatformInputMode.Replay,"Load only synthetic external replay data");
                    Assert(AndroidPlatformBridge.GetSnapshot().healthConnect.steps.value==4321,"Replay fixture exposes its old health data before reset");
                    Assert(AndroidPlatformBridge.StartSnapshotRecording(recording).status=="AVAILABLE","Wrap replay provider with an active synthetic recording");
                    Assert(AndroidPlatformBridge.GetSettingsSnapshot().healthConnect.heartRate.heartRateSamples.Length==1&&new FileInfo(recording).Length>0,"Settings replay records and exposes the synthetic heart sample before reset");
                    string queuePath=Path.Combine(personal,"behavior-queue.xml");
                    string unreadableQueue=File.ReadAllText(queuePath);
                    var unreadable=AppLocalReset.Cleanup("whole-local-cleanup",active);
                    Assert(unreadable.status=="ERROR"&&unreadable.errorCode=="DELETION_STATE_UNREADABLE"&&File.ReadAllText(queuePath)==unreadableQueue
                        &&Xml(service)==pending&&File.Exists(active+".reset"),"Unreadable deletion state is preserved and leaves the reset journal pending");
                    new FileEventQueueStore(queuePath).Save(new EventQueueState());
                    // An open file does not prevent unlink on Unix. A directory at
                    // an app-owned file path forces a real deletion error on both OSes.
                    string blocked=queuePath+".new";File.Delete(blocked);Directory.CreateDirectory(blocked);
                    try { Assert(AppLocalReset.Cleanup("whole-local-cleanup",active).status=="ERROR"&&Xml(service)==pending&&File.Exists(active+".reset"),
                        "Local file deletion failure leaves active reset journal pending and retryable"); }
                    finally { Directory.Delete(blocked); }
                    Assert(AppLocalReset.Cleanup("whole-local-cleanup",active).status=="AVAILABLE","Actual C# cleanup retry completes synthetic app-local data removal");
                    byte[] stoppedRecording=File.ReadAllBytes(recording);
                    foreach(var empty in new[]{AndroidPlatformBridge.GetSnapshot(),AndroidPlatformBridge.GetSettingsSnapshot()})
                        Assert(empty.ParsedStatus==PlatformStatus.NoData&&empty.inputMode=="REPLAY"&&empty.generatedAtEpochMs==0
                            &&!empty.healthConnect.steps.hasValue&&empty.healthConnect.steps.value==0&&empty.healthConnect.steps.ParsedStatus==PlatformStatus.NoData
                            &&!empty.healthConnect.heartRate.hasValue&&empty.healthConnect.heartRate.heartRateSamples.Length==0
                            &&empty.sensorService.sampleCount==0&&empty.sensorService.sensors.Length==0,"Both replay snapshot paths discard health and sensor caches after reset");
                    Assert(AndroidPlatformBridge.InputMode==PlatformInputMode.Replay,"Full reset retains the existing replay input mode");
                    Assert(File.ReadAllBytes(replaySource).SequenceEqual(replayBytes),"External replay source remains byte-for-byte unchanged");
                    Assert(File.ReadAllBytes(recording).SequenceEqual(stoppedRecording)&&AndroidPlatformBridge.StopSnapshotRecording().status=="NO_DATA","Cleanup stops the recording decorator without deleting caller-owned recording output");
                    Assert(Xml(service)==pending&&service.HasPendingFullReset,"Cleanup never bypasses the active service's explicit durable finish step");
                    Assert(!PlayerPrefs.HasKey("capstone.garden.nutrient")&&!PlayerPrefs.HasKey("synthetic.full-reset.pref"),"App preferences and legacy import values are removed in isolated application identity");
                    Assert(Directory.GetFiles(personal).Select(Path.GetFileName).SequenceEqual(new[]{"personalization-local-reset"}),"Local queue/enrollment backups are removed and privacy tombstone remains");
                    Assert(Resources.FindObjectsOfTypeAll<PersonalizationClient>().Length==clients,"Local reset does not create a personalization singleton or network client");
                    foreach(string name in new[]{"platform_snapshots.jsonl","ui-profile.json","capstone-music-probe.jsonl"})Assert(!File.Exists(Path.Combine(Application.persistentDataPath,name)),"Remove app-owned diagnostic file "+name);
                    foreach(string mode in new[]{"live","replay"})using(var other=new FileStateStore(Path.Combine(profiles,"garden-"+mode,"state.xml")))Fresh(other.Load(),rich.InstallationId);
                    Assert(File.ReadAllText(sourceSentinel)=="synthetic outside app-reset scope","Unrelated external-source sentinel remains untouched");
                    Assert(service.FinishFullReset("whole-local-cleanup")&&!File.Exists(active+".reset"),"Full cleanup finishes active reset only after every app-owned step succeeds");
                }
            }
            finally{AndroidPlatformBridge.StopSnapshotRecording();AndroidPlatformBridge.UseMockProvider();Environment.SetEnvironmentVariable("CAPSTONE_PERSONALIZATION_TEST_ROOT",previous);}
        }
        static void ReleaseScripts()
        {
            string folder=Path.Combine(output,"release-scripts");Directory.CreateDirectory(folder);
            var result=PlayerBuildInterface.CompilePlayerScripts(new ScriptCompilationSettings{target=Application.platform==RuntimePlatform.LinuxEditor?BuildTarget.StandaloneLinux64:BuildTarget.StandaloneWindows64,group=BuildTargetGroup.Standalone,options=ScriptCompilationOptions.None},folder);
            string path=Path.Combine(folder,"Assembly-CSharp.dll");Assert(File.Exists(path)&&result.assemblies.Any(item=>Path.GetFileName(item)=="Assembly-CSharp.dll"),"Actual non-development runtime scripts compile");
            using(var assembly=AssemblyDefinition.ReadAssembly(path))
            {
                var ui=assembly.MainModule.Types.Single(type=>type.FullName=="CapstoneDesign.Runtime.WeekOneQuestDemo");
                Assert(!ui.Fields.Any(field=>field.Name=="FullResetCleanupOverride"),"Release excludes injected cleanup test override");
                var settings=assembly.MainModule.Types.Single(type=>type.FullName=="CapstoneDesign.Runtime.SensorRawDisplay");
                Assert(!settings.Methods.Any(method=>method.Name=="RequestMissionReset"),"Development-only destructive reset launcher is excluded from release settings");
            }
        }
        sealed class Store : IStateStore
        { string xml; public bool Fail; public int Saves; public GardenState Load() => xml == null ? null : StateCodec.Decode(xml);
          public void Save(GardenState value) { if (Fail) throw new IOException("Synthetic daily UI save failure"); xml = StateCodec.Encode(value); Saves++; } }
        sealed class Clock : IClock
        { public DateTimeOffset Time = DateTimeOffset.Parse("2026-11-09T03:00:00Z"); public DateTimeOffset UtcNow => Time; public TimeZoneInfo TimeZone => TimeZoneInfo.Utc; }
        sealed class Fixture : IDisposable
        {
            public readonly Store Store; public readonly Clock Clock = new Clock();
            public readonly GardenStateService Service; public readonly WeekOneQuestDemo Ui;
            public readonly MockupNavigation Navigation; public SensorRawDisplay Settings;
            public string CleanupStatus; public readonly List<string> CleanupCalls=new List<string>();
            readonly GameObject root; readonly Scene scene; readonly Camera camera; readonly RenderTexture target;
            readonly EventSystem events; readonly GraphicRaycaster raycaster; readonly int width, height;
            public Fixture(int width, int height, Store store = null, DateTimeOffset? instant = null, string cleanupStatus = "AVAILABLE")
            {
                this.width = width; this.height = height; Store = store ?? new Store(); CleanupStatus=cleanupStatus;
                if(instant.HasValue)Clock.Time=instant.Value;
                Service = new GardenStateService(Store, Clock, new DemoBalanceConfig { VisitorChancePercent = 0 }, new LegacySnapshot { Nutrient = 137 });
                Assert(Service.Open(), Service.LastError);
                scene = EditorSceneManager.NewPreviewScene();
                root = new GameObject("Synthetic daily UI", typeof(RectTransform), typeof(Canvas), typeof(GraphicRaycaster));
                root.SetActive(false); SceneManager.MoveGameObjectToScene(root, scene);
                var rect = (RectTransform)root.transform; rect.sizeDelta = new Vector2(720, 720f*height/width); root.transform.localScale=Vector3.one*(width/720f);
                var canvas = root.GetComponent<Canvas>(); canvas.renderMode = RenderMode.WorldSpace;
                var cameraObject = new GameObject("Synthetic daily capture camera", typeof(Camera)); SceneManager.MoveGameObjectToScene(cameraObject, scene);
                camera = cameraObject.GetComponent<Camera>(); camera.enabled = false; camera.scene = scene;
                camera.orthographic = true; camera.orthographicSize = height / 2f; camera.nearClipPlane = .1f; camera.farClipPlane = 100;
                camera.transform.position = new Vector3(0, 0, -10); camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Color.white;
                target = new RenderTexture(width, height, 24); camera.targetTexture = target; camera.aspect = width / (float)height;
                canvas.worldCamera = camera; raycaster = root.GetComponent<GraphicRaycaster>();
                var eventObject = new GameObject("Synthetic pointer", typeof(EventSystem)); eventObject.transform.SetParent(root.transform, false); events = eventObject.GetComponent<EventSystem>(); events.enabled = false;
                Navigation=root.AddComponent<MockupNavigation>();
                Navigation.islandPanel=GardenUi.Box(root.transform,"Synthetic garden destination",0,0,1,1);
                Navigation.settingsPanel=GardenUi.Box(root.transform,"Synthetic settings panel",0,0,1,1);Navigation.settingsPanel.SetActive(false);
                Navigation.activitiesPanel=GardenUi.Box(root.transform,"Synthetic activity panel",0,0,1,1);
                Ui=Navigation.activitiesPanel.AddComponent<WeekOneQuestDemo>();
                Ui.FullResetCleanupOverride=request=>{CleanupCalls.Add(request);return new PlatformActionResult{status=CleanupStatus,message="Synthetic cleanup result"};};
                Ui.Initialize(Service,true);
                root.SetActive(true);Navigation.ShowActivities();Layout();
                Assert(ReferenceEquals(Ui.Service, Service), "Actual page presenter keeps injected synthetic service");
            }
            public void OpenSettings()
            {
                if(Settings==null)
                {
                    Navigation.settingsPanel.SetActive(false);Settings=Navigation.settingsPanel.AddComponent<SensorRawDisplay>();Settings.PrepareView();
                }
                Navigation.ShowSettings();Layout();
            }
            public void OpenTools()
            {
                OpenSettings();if(!Settings.MissionRefreshButton.gameObject.activeInHierarchy)Click("Settings.TestTools");
            }
            public string VisibleText=>string.Join("\n",root.GetComponentsInChildren<Text>(false).Select(text=>text.text));
            public ActivitySession Session(string id) => Service.Snapshot.Sessions.Single(s => s.SessionId == id);
            public Text Text(string name) => root.GetComponentsInChildren<Text>(false).FirstOrDefault(t => t.name == name);
            public void RefreshSettings()
            {
                typeof(SensorRawDisplay).GetField("nextRefresh",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(Settings,0f);
                typeof(SensorRawDisplay).GetMethod("Update",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(Settings,null);
                Layout();
            }
            public Button Button(string name, bool required = true)
            {
                var result = root.GetComponentsInChildren<Button>(false).FirstOrDefault(b => b.name == name && b.gameObject.activeInHierarchy);
                if (required) Assert(result != null && result.IsInteractable(), "Visible enabled button missing: " + name); return result;
            }
            public void Layout()
            {
                for (int i = 0; i < 3; i++)
                {
                    Canvas.ForceUpdateCanvases();
                    foreach (var fit in root.GetComponentsInChildren<FigmaMissionCanvasFit>(false)) fit.Fit();
                    foreach (var page in root.GetComponentsInChildren<FocusMobilePageLayout>(false)) page.Refresh();
                    Canvas.ForceUpdateCanvases();
                    if(Settings!=null&&Settings.PrimaryScroll!=null&&Settings.gameObject.activeInHierarchy)LayoutRebuilder.ForceRebuildLayoutImmediate(Settings.PrimaryScroll.content);
                }
                // Graphic depths/raycast ordering are populated by a real render.
                // Null graphics cannot establish that evidence; capture runs use an owned offscreen GPU target.
                if (capture) camera.Render();
            }
            public void Click(string name)
            {
                var button = Button(name); FocusKeyboardNavigation.Reveal(button); Layout();
                var rect = (RectTransform)button.transform; var point = camera.WorldToScreenPoint(rect.TransformPoint(rect.rect.center));
                Assert(point.x > 0 && point.x < width && point.y > 0 && point.y < height, "Button center is within visible capture: " + name + " " + point);
                var data = new PointerEventData(events) { position = point, button = PointerEventData.InputButton.Left };
                var hits = new List<RaycastResult>(); raycaster.Raycast(data, hits); report.raycasts++;
                Assert(hits.Count > 0 && (hits[0].gameObject == button.gameObject || hits[0].gameObject.transform.IsChildOf(button.transform)),
                    "Actual visible button is first pointer target: " + name + "; hit=" + (hits.Count == 0 ? "none" : hits[0].gameObject.name)
                    + "; graphicDepth=" + button.targetGraphic.depth + "; culled=" + button.targetGraphic.canvasRenderer.cull);
                ExecuteEvents.Execute(button.gameObject, data, ExecuteEvents.pointerDownHandler);
                ExecuteEvents.Execute(button.gameObject, data, ExecuteEvents.pointerUpHandler);
                ExecuteEvents.Execute(button.gameObject, data, ExecuteEvents.pointerClickHandler);
                report.clicks++; Layout();
            }
            public void AssertFooter(string name)
            {
                var button = Button(name); var rect = (RectTransform)button.transform;
                Assert(rect.parent.name == "Mobile.Actions", "Main action is in fixed footer");
                var points = new Vector3[4]; rect.GetWorldCorners(points);
                foreach (var corner in points) { var p = camera.WorldToScreenPoint(corner); Assert(p.x >= 0 && p.x <= width && p.y >= 0 && p.y <= height, "Fixed footer button fully in viewport"); }
                Assert(rect.rect.height * rect.lossyScale.y >= 44, "Main action meets 44px height at actual output ratio");
            }
            public void RevealRect(RectTransform rect)
            {
                foreach (var scroll in rect.GetComponentsInParent<ScrollRect>(false).Where(s => s.enabled && s.content != null && s.viewport != null))
                {
                    var center = scroll.content.InverseTransformPoint(rect.TransformPoint(rect.rect.center));
                    scroll.content.anchoredPosition = new Vector2(0, Mathf.Clamp(-center.y - scroll.viewport.rect.height / 2,
                        0, Mathf.Max(0, scroll.content.rect.height - scroll.viewport.rect.height)));
                }
                Layout();
            }
            Rect ScreenRect(RectTransform rect)
            {
                var points = new Vector3[4]; rect.GetWorldCorners(points); var a = camera.WorldToScreenPoint(points[0]); var b = camera.WorldToScreenPoint(points[2]);
                return Rect.MinMaxRect(a.x, a.y, b.x, b.y);
            }
            public Rect Bounds(RectTransform rect) => ScreenRect(rect);
            public void AssertFullyVisible(RectTransform rect, string label)
            {
                var bounds = ScreenRect(rect); const float tolerance = .5f;
                Assert(bounds.xMin >= -tolerance && bounds.xMax <= width + tolerance && bounds.yMin >= -tolerance && bounds.yMax <= height + tolerance, label + " in output viewport");
                foreach (var mask in rect.GetComponentsInParent<RectMask2D>(false))
                {
                    var visible = ScreenRect(mask.rectTransform);
                    Assert(bounds.xMin >= visible.xMin - tolerance && bounds.xMax <= visible.xMax + tolerance
                        && bounds.yMin >= visible.yMin - tolerance && bounds.yMax <= visible.yMax + tolerance, label + " inside scrolling mask");
                }
            }
            public void RevealAndRead(Text text)
            {
                Assert(text != null, "Required guidance text exists"); RevealRect(text.rectTransform);
                AssertFullyVisible(text.rectTransform, text.name);
                Assert(text.preferredHeight <= text.rectTransform.rect.height + .5f, text.name + " reserves its complete wrapped text height");
            }
            public void AssertReadingSeparation()
            {
                var rects = Ui.GetComponentsInChildren<RectTransform>(false);
                var body = rects.Single(r => r.name == "Mobile.Viewport"); var footer = rects.Single(r => r.name == "Mobile.Actions");
                var notice = Text("Focus.SaveNotice"); Assert(notice != null && !string.IsNullOrEmpty(notice.text), "Storage failure notice is visible");
                var bodyBounds = ScreenRect(body); var footerBounds = ScreenRect(footer); var noticeBounds = ScreenRect(notice.rectTransform);
                Assert(bodyBounds.yMin >= noticeBounds.yMax - .5f && noticeBounds.yMin >= footerBounds.yMax - .5f,
                    "Scrollable guidance, storage error notice, and fixed footer have separate visible regions");
                AssertFullyVisible(notice.rectTransform, "Storage failure notice");
            }
            public void Capture(string name)
            {
                if (!capture) return; Layout(); camera.Render();
                var prior = RenderTexture.active; var image = new Texture2D(width, height, TextureFormat.RGB24, false);
                try
                {
                    RenderTexture.active = target; image.ReadPixels(new Rect(0, 0, width, height), 0, 0); image.Apply();
                    string filename = name + "-" + width + "x" + height + ".png";
                    File.WriteAllBytes(Path.Combine(output, filename), image.EncodeToPNG()); report.captures.Add(filename);
                }
                finally { RenderTexture.active = prior; UnityEngine.Object.DestroyImmediate(image); }
            }
            public void Dispose()
            {
                camera.targetTexture = null; target.Release(); UnityEngine.Object.DestroyImmediate(target);
                UnityEngine.Object.DestroyImmediate(root); Service.Dispose(); EditorSceneManager.ClosePreviewScene(scene);
            }
        }
    }
}
