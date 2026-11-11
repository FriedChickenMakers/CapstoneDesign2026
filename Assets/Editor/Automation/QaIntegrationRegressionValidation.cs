using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using CapstoneDesign.Runtime;
using CapstoneDesign.Runtime.LocalState;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace CapstoneDesign.EditorTools
{
    // Explicit synthetic edit-mode entry only. Never polls, enters Play mode,
    // writes PlayerPrefs, opens a user's scene or reads the normal save location.
    public static class QaIntegrationRegressionValidation
    {
        static FocusMissionValidation.Report report;
        static string output, saveRoot;
        static bool capture;
        static int assertions;
        const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

        public static void RunBatch()
        {
            string artifacts = Environment.GetEnvironmentVariable("CAPSTONE_ARTIFACTS");
            string isolated = Environment.GetEnvironmentVariable("CAPSTONE_SMOKE_SAVE_ROOT");
            if (!Application.isBatchMode || EditorApplication.isPlayingOrWillChangePlaymode
                || string.IsNullOrWhiteSpace(artifacts) || string.IsNullOrWhiteSpace(isolated))
                throw new InvalidOperationException("Explicit batch, CAPSTONE_ARTIFACTS and a fresh CAPSTONE_SMOKE_SAVE_ROOT are required.");
            saveRoot = Path.GetFullPath(isolated);
            output = Path.Combine(Path.GetFullPath(artifacts), "qa-integration-validation");
            if (saveRoot == Path.GetFullPath(Application.persistentDataPath)
                || saveRoot == Path.GetFullPath(Directory.GetCurrentDirectory())
                || Directory.Exists(saveRoot) && Directory.EnumerateFileSystemEntries(saveRoot).Any()
                || Directory.Exists(output) && Directory.EnumerateFileSystemEntries(output).Any())
                throw new InvalidOperationException("Refusing a populated or normal app save/output directory.");
            Directory.CreateDirectory(saveRoot); Directory.CreateDirectory(output);
            capture = Environment.GetCommandLineArgs().Contains("-qaIntegrationCapture");
            report = new FocusMissionValidation.Report { runId="qa-integration", utc=DateTimeOffset.UtcNow.ToString("O"),
                unityVersion=Application.unityVersion, environment="Explicit edit-mode preview; synthetic in-memory/domain state and isolated writer-lock failures; no device, native reminders, networking or real preferences changed" };
            try
            {
                Run("Saved photo has one image, one deletion control and one deletion effect", PhotoRecord);
                Run("Valid base-plus-photo reward is shown and invalid photo-only state is rejected", PhotoRewardContract);
                Run("Guided timer modal pauses, cancel resumes, shortened completion preserves actual time", GuidedAdjustment);
                Run("Guided timer actions fit short phones before scrolling, including a save error", GuidedTimerBounds);
                Run("Skipping basic mission preserves course order and grants no reward", SkipBasic);
                Run("M10 offers separate activity and comfortable-period choices", PlanChoices);
                Run("Existing guided v4 timers keep their original recommendation and remain startable", LegacyGuidedTimer);
                Run("Legacy texture-v3 P25 headers fit their complete original copy", LegacySpeakingHeader);
                Run("Reviewed purpose copy overrides old static prose only for new content", PurposeCopy);
                Run("P21/P28 pause choices retain a v5 observation anchor and preserve v4", PauseChoicePurpose);
                Run("P19/P26 alternatives are labeled additional and consume only the additional slot", SupportActivityRole);
                Run("Navigation preserves inset shelf, centered labels/icons and separated touch targets", () => NavigationLayoutValidation.Run(report,Check,capture));
                Run("Untimed practice retains one completion action without fabricated start times", () => FocusPracticeActionValidation.Untimed(report,Check,capture));
                Run("Timed start and shortening hooks survive untimed UI correction", () => FocusPracticeActionValidation.Timed(report,Check));
                Run("Writer-lock failure recreates service after Retry", () => LoadRecovery(true));
                Run("Allocated service retries failed Open and restores active session/photos", () => LoadRecovery(false));
            }
            finally
            {
                report.checks.Add(new FocusMissionValidation.CheckResult { name="assertions",status="info",detail=assertions.ToString() });
                File.WriteAllText(Path.Combine(output,"report.json"),JsonUtility.ToJson(report,true));
            }
            Debug.Log("QA_INTEGRATION_REGRESSION passed="+report.passed+" failed="+report.failed+" assertions="+assertions);
            if(report.failed>0)throw new InvalidOperationException("QA integration regression failed; inspect its private report.");
        }
        static void Run(string name,Action action)
        {
            try {action();report.passed++;report.checks.Add(new FocusMissionValidation.CheckResult{name=name,status="pass"});}
            catch(Exception error) {report.failed++;report.checks.Add(new FocusMissionValidation.CheckResult{name=name,status="fail",detail=error.ToString()});}
        }
        static void Check(bool ok,string message) {assertions++;if(!ok)throw new InvalidOperationException(message);}
        static T Field<T>(WeekOneQuestDemo ui,string name) => (T)typeof(WeekOneQuestDemo).GetField(name,Private).GetValue(ui);
        static ActivitySession Session(GardenStateService service,string id) => service.Snapshot.Sessions.Single(s=>s.SessionId==id);
        static Button Button(WeekOneQuestDemo ui,string name) => ui.GetComponentsInChildren<Button>(false).Single(b=>b.name==name);
        static Text Text(WeekOneQuestDemo ui,string name) => ui.GetComponentsInChildren<Text>(false).Single(t=>t.name==name);
        static void Ready(FocusMissionValidation.Fixture f,string id,string code="M03")
        {
            Check(f.Service.BeginFocusSession(id,code),f.Service.LastError);
            Check(f.Service.StartSession(id),f.Service.LastError);
            var definition=FocusMissionCatalog.FindForSession(Session(f.Service,id));
            while(Session(f.Service,id).InstructionStep<definition.Steps.Length)
            {
                int step=Session(f.Service,id).InstructionStep;var instruction=definition.Steps[step];
                string answer=instruction.Options.FirstOrDefault()??(instruction.RequiredAnswer?"synthetic fixture":"");
                Check(f.Service.ConfirmFocusStep(id,step,answer),f.Service.LastError);
            }
            f.Clock.UtcNow=f.Clock.UtcNow.AddSeconds(1);
        }
        static MissionPhoto AttachPhoto(FocusMissionValidation.Fixture f,SyntheticPhotos provider,string id)
        {
            var photo=new MissionPhoto {Id=Guid.NewGuid().ToString("N"),Target="container",Outcome="matched",
                CapturedUtc=f.Clock.UtcNow.ToString("O"),ModelVersion=CameraMissionPolicy.ModelVersion,MatchedLabel="cup",Score=.8};
            provider.Write(photo.Id);
            Check(f.Service.BeginPhotoCapture(id,photo.Id,photo.Target),f.Service.LastError);
            Check(f.Service.RegisterPhoto(id,photo),f.Service.LastError);
            return photo;
        }
        static void PhotoRecord()
        {
            using(var f=new FocusMissionValidation.Fixture(report))
            {
                var provider=new SyntheticPhotos(Path.Combine(output,"record-photo"));f.Ui.SetPhotoProviderForValidation(provider);
                Ready(f,"saved-photo");var photo=AttachPhoto(f,provider,"saved-photo");
                Check(f.Service.CompletePracticeSession("saved-photo"),f.Service.LastError);
                f.Invoke("ShowFocusRecord","saved-photo");
                Check(f.Ui.GetComponentsInChildren<RawImage>(false).Count(i=>i.name=="Photo.Preview")==1,"Saved photo must appear exactly once.");
                Check(f.Ui.GetComponentsInChildren<Button>(false).Count(b=>b.name=="Photo.Delete")==1,"Photo deletion control must appear exactly once.");
                f.Capture("qa-photo-record-single",capture);
                int coins=f.Service.CoinBalance,bonus=Session(f.Service,"saved-photo").PhotoBonusAmount;
                f.Click("Photo.Delete");
                Check(Session(f.Service,"saved-photo").Photo==null && !File.Exists(Path.Combine(provider.DirectoryPath,photo.Id+".jpg")),"One click removes the saved photo binding and file.");
                Check(provider.Deletes==1 && f.Service.CoinBalance==coins && Session(f.Service,"saved-photo").PhotoBonusAmount==bonus,"Deleting a photo does not grant or undo its reward.");
            }
        }
        static void PhotoRewardContract()
        {
            using(var f=new FocusMissionValidation.Fixture(report))
            {
                var provider=new SyntheticPhotos(Path.Combine(output,"reward-photo"));
                f.Ui.SetPhotoProviderForValidation(provider);
                Ready(f,"photo-reward");AttachPhoto(f,provider,"photo-reward");
                Check(f.Service.CompletePracticeSession("photo-reward"),f.Service.LastError);
                var saved=Session(f.Service,"photo-reward");
                Check(saved.RewardAmount==50 && saved.PhotoBonusAmount==1,"Actual completion grants the supported 50+1 reward.");
                f.Invoke("ShowFocusCompletion","photo-reward");
                Check(Text(f.Ui,"Focus.CoinReward").text=="코인 +51","Completion displays the actual base-plus-photo total.");
                string original=StateCodec.Encode(f.Service.Snapshot);
                var invalid=f.Service.Snapshot;invalid.Sessions.Single(s=>s.SessionId=="photo-reward").RewardAmount=0;
                bool rejected=false;
                try {StateCodec.Validate(invalid);}catch(InvalidDataException){rejected=true;}
                Check(rejected,"Photo bonus with zero base reward is invalid, not a supported historical display case.");
                Check(StateCodec.Encode(f.Service.Snapshot)==original,"Invalid display fixture never changes the real synthetic service state.");
            }
        }
        static void GuidedAdjustment()
        {
            using(var f=new FocusMissionValidation.Fixture(report))
            {
                Check(f.Service.SelectMbctProgram("8w"),f.Service.LastError);
                Check(f.Service.BeginFocusSession("guided-short","P01",1),f.Service.LastError);
                f.Invoke("SelectFocusSession","guided-short");
                Check(f.Ui.GetComponentInChildren<FocusPracticeClock>(false)==null,"Guide opening must not start a countdown.");
                for(int i=0;i<8 && f.Ui.GetComponentInChildren<FocusPracticeClock>(false)==null;i++)f.Click("Focus.IconAction");
                var clock=f.Ui.GetComponentInChildren<FocusPracticeClock>(false);
                Check(clock!=null && clock.Running && clock.HasStarted,"Last guide cue starts the actual countdown.");
                Check(Button(f.Ui,"Focus.TooLong").interactable,"Too-long control becomes available after practice begins.");
                clock.ElapsedSeconds=12;f.Clock.UtcNow=f.Clock.UtcNow.AddSeconds(12);Check(clock.Flush(),"Actual elapsed time persists.");
                f.Click("Focus.TooLong");
                Check(!clock.Running,"Confirmation modal pauses the countdown.");
                Check(Button(f.Ui,"Focus.AdjustmentConfirm").GetComponentInChildren<Text>().text=="여기까지 하기","Confirmation text matches the agreed action.");
                f.Capture("qa-too-long-paused",capture);
                f.Click("Focus.AdjustmentCancel");Check(clock.Running,"Cancel restores the previous running state.");
                f.Click("Focus.TooLong");f.Click("Focus.AdjustmentConfirm");
                var completed=Session(f.Service,"guided-short");
                Check(completed.FocusStepRecords[0].EndReason=="too_long" && completed.FocusStepRecords[0].ElapsedSeconds==12,"Shortened record retains actual elapsed time and reason.");
                Check(completed.FocusStepRecords.All(s=>!string.IsNullOrEmpty(s.ConfirmedUtc)),"Guided required records remain confirmed.");
                Check(completed.Status==ParticipationState.RewardCommitted && completed.RewardAmount==50,"Valid short completion uses the normal reward.");
            }
        }
        static void GuidedTimerBounds()
        {
            foreach (var size in new[] { new Vector2Int(360,640), new Vector2Int(390,844) })
            using (var f = new FocusMissionValidation.Fixture(report))
            {
                f.Resize(size.x,size.y);
                Check(f.Service.SelectMbctProgram("8w") && f.Service.BeginFocusSession("timer-layout","P01",1),f.Service.LastError);
                f.Invoke("SelectFocusSession","timer-layout");
                for (int i=0;i<8 && f.Ui.GetComponentInChildren<FocusPracticeClock>(false)==null;i++) f.Click("Focus.IconAction");
                var clock=f.Ui.GetComponentInChildren<FocusPracticeClock>(false);
                Check(clock!=null && clock.Running,"Synthetic timer begins through its real guide controls.");
                var layout=f.Ui.GetComponentInChildren<FocusGuidedSlideLayout>(false);
                int coins=f.Service.CoinBalance,growth=f.Service.Snapshot.PracticeGrowthRecords.Count;
                void Inspect(string state)
                {
                    Canvas.ForceUpdateCanvases();layout.Refresh();Canvas.ForceUpdateCanvases();
                    Check(Mathf.Abs(layout.Content.anchoredPosition.y)<.1f,"Timer starts at the top; no reveal/scroll may hide a clipped control.");
                    foreach (string name in new[] {"Focus.TimerToggle","Guided.ReflectionToggle","Focus.TooLong"})
                    {
                        var button=Button(f.Ui,name);
                        var bounds=RectTransformUtility.CalculateRelativeRectTransformBounds(layout.Viewport,button.transform);
                        var visible=layout.Viewport.rect;
                        Check(bounds.min.x>=visible.xMin-.5f && bounds.max.x<=visible.xMax+.5f
                            && bounds.min.y>=visible.yMin-.5f && bounds.max.y<=visible.yMax+.5f,
                            name+" is fully visible before scrolling on "+size+" / "+state);
                        Check(((RectTransform)button.transform).rect.height>=48,"Timer action retains its full touch height.");
                    }
                    var copyBounds=RectTransformUtility.CalculateRelativeRectTransformBounds(layout.transform,layout.Copy.transform);
                    var iconBounds=RectTransformUtility.CalculateRelativeRectTransformBounds(layout.transform,layout.Icon);
                    Check(iconBounds.max.y<=copyBounds.min.y-8,"Compact timer illustration does not overlap the instruction.");
                    if (layout.Notice.gameObject.activeSelf)
                    {
                        var viewportBounds=RectTransformUtility.CalculateRelativeRectTransformBounds(layout.transform,layout.Viewport);
                        var noticeBounds=RectTransformUtility.CalculateRelativeRectTransformBounds(layout.transform,layout.Notice.transform);
                        Check(viewportBounds.min.y>=noticeBounds.max.y-.5f,"Timer actions remain above the save error notice.");
                    }
                    f.Capture("qa-timer-visible-"+state+"-"+size.x+"x"+size.y,capture);
                }
                Inspect("normal");
                clock.ElapsedSeconds=7;f.Clock.UtcNow=f.Clock.UtcNow.AddSeconds(7);f.Store.FailNextSaves=1;
                Check(!clock.Flush() && layout.Notice.gameObject.activeSelf,"An actual synthetic save failure shows its notice.");
                Inspect("save-error");
                f.Click("Focus.TooLong");
                Check(!clock.Running,"The visible short-phone shortening action opens its real pause modal.");
                f.Click("Focus.AdjustmentCancel");
                Check(clock.Running,"Cancel resumes the timer after the failed save is retried.");
                Check(f.Service.CoinBalance==coins && f.Service.Snapshot.PracticeGrowthRecords.Count==growth,
                    "Layout and cancelled shortening do not complete or reward the mission.");
            }
        }
        static void SkipBasic()
        {
            using(var f=new FocusMissionValidation.Fixture(report))
            {
                Check(f.Service.SelectMbctProgram("8w"),f.Service.LastError);
                Check(f.Service.BeginFocusSession("skip-basic","P01",1),f.Service.LastError);
                f.Invoke("SelectFocusSession","skip-basic");int coins=f.Service.CoinBalance,growth=f.Service.Snapshot.PracticeGrowthRecords.Count;
                f.Click("Focus.EndParticipation");f.Click("Focus.AdjustmentConfirm");
                Check(Session(f.Service,"skip-basic").Status==ParticipationState.Ended && FocusMissionPolicy.NextOrder(f.Service.Snapshot)==1,"Skip leaves the basic course's current occurrence available.");
                Check(f.Service.CoinBalance==coins && f.Service.Snapshot.PracticeGrowthRecords.Count==growth,"Incomplete skip gives neither coins nor partial growth.");
                Check(f.Service.BeginFocusSession("additional-after-skip","M03"),"Additional activity remains available after deferring basic practice.");
            }
        }
        static void PlanChoices()
        {
            using(var f=new FocusMissionValidation.Fixture(report))
            {
                Check(f.Service.BeginFocusSession("plan","M10") && f.Service.StartSession("plan"),f.Service.LastError);
                f.Invoke("SelectFocusSession","plan");
                Check(f.Ui.GetComponentsInChildren<Button>(false).Any(b=>b.name=="Focus.PlanActivity.0") && f.Ui.GetComponentsInChildren<Button>(false).Any(b=>b.name=="Focus.PlanPeriod.0"),"M10 exposes activity and time-period options.");
                f.Click("Focus.PlanActivity.0");f.Click("Focus.PlanPeriod.0");
                var plan=Session(f.Service,"plan").FocusStepRecords[0];
                Check(!string.IsNullOrEmpty(plan.PlanActivity) && !string.IsNullOrEmpty(plan.PlanTimePeriod),"Separate selected activity and comfortable period persist.");
                f.Capture("qa-m10-plan-choices",capture);
            }
        }
        static void LegacyGuidedTimer()
        {
            var seed=new FocusMissionValidation.MemoryStore();
            using(var f=new FocusMissionValidation.Fixture(report))
            {
                Check(f.Service.SelectMbctProgram("8w"),f.Service.LastError);
                Check(f.Service.BeginFocusSession("old-guided","P01",1),f.Service.LastError);
                var old=f.Service.Snapshot;old.Sessions.Single(s=>s.SessionId=="old-guided").FocusVersion=FocusMissionCatalog.GuidedVersion;
                StateCodec.Validate(old);seed.Save(old);
            }
            using(var f=new FocusMissionValidation.Fixture(report,chooseCourse:false,seededStore:seed))
            {
                f.Invoke("SelectFocusSession","old-guided");
                for(int i=0;i<8 && f.Ui.GetComponentInChildren<FocusPracticeClock>(false)==null;i++)f.Click("Focus.IconAction");
                var clock=f.Ui.GetComponentInChildren<FocusPracticeClock>(false);
                var session=Session(f.Service,"old-guided");
                Check(session.FocusVersion==FocusMissionCatalog.GuidedVersion && clock!=null && clock.Running,"Existing v4 guide reaches a running timer without migration.");
                Check(clock.RecommendedSeconds==FocusMissionCatalog.FindForSession(session).Steps[0].RecommendedSeconds && clock.RecommendedSeconds>0,"Existing guided recommendation is not overwritten with zero.");
            }
        }
        static void LegacySpeakingHeader()
        {
            var seed=new FocusMissionValidation.MemoryStore();
            var definition=FocusMissionCatalog.Find("P25",FocusMissionCatalog.TextureVersion);
            using(var f=new FocusMissionValidation.Fixture(report))
            {
                Check(f.Service.SelectMbctProgram("8w"),f.Service.LastError);
                Check(f.Service.BeginFocusSession("legacy-speaking","P01",1),f.Service.LastError);
                // Inject one validated historical draft for rendering; this gate
                // does not complete it or claim a course-progression test.
                var old=f.Service.Snapshot;var session=old.Sessions.Single(s=>s.SessionId=="legacy-speaking");
                session.FocusVersion=FocusMissionCatalog.TextureVersion;session.MissionId=definition.Id;session.FocusOrder=25;
                session.MissionTitleSnapshot=definition.Title;session.MissionDescriptionSnapshot=definition.Overview;
                session.FocusStepRecords=definition.Steps.Select((step,index)=>new FocusStepRecord {Step=index,Key=step.Key,Title=step.Title,Prompt=step.Body}).ToList();
                StateCodec.Validate(old);seed.Save(old);
            }
            using(var f=new FocusMissionValidation.Fixture(report,chooseCourse:false,seededStore:seed))
            {
                for(int step=-1;step<definition.Steps.Length;step++)
                {
                    if(step==0)Check(f.Service.StartSession("legacy-speaking"),f.Service.LastError);
                    else if(step>0)
                    {
                        var prior=definition.Steps[step-1];
                        Check(f.Service.ConfirmFocusStep("legacy-speaking",step-1,prior.Options.FirstOrDefault()??""),f.Service.LastError);
                    }
                    f.Invoke("SelectFocusSession","legacy-speaking");
                    Check(Session(f.Service,"legacy-speaking").FocusVersion==FocusMissionCatalog.TextureVersion,"Historical rendering must not migrate the session version.");
                    foreach(var size in new[]{new Vector2Int(780,1688),new Vector2Int(800,1280)})
                    {
                        f.Resize(size.x,size.y);
                        var heading=Text(f.Ui,"Focus.PracticeHeading");var body=Text(f.Ui,"Focus.PracticeBody");
                        Check(heading.text==(step<0?definition.Title:definition.Steps[step].Title)
                            && body.text==(step<0?definition.Overview:definition.Steps[step].Body),"Historical title and full body remain unchanged.");
                        Check(heading.rectTransform.rect.height>=48 && body.rectTransform.rect.height>=84,"Texture P25 retains its original expanded header geometry.");
                        foreach(var text in new[]{heading,body})
                            Check(text.preferredHeight<=text.rectTransform.rect.height+.75f,"Legacy P25 step "+step+" "+text.name+" fits the complete wrapped copy at "+size+".");
                    }
                    if(step==2)f.Capture("qa-texture-v3-p25-header",capture);
                }
            }
        }
        static void PauseChoicePurpose()
        {
            foreach(string code in new[]{"P21","P28"})
            foreach(int version in new[]{FocusMissionCatalog.Version,FocusMissionCatalog.GuidedVersion})
            {
                var definition=FocusMissionCatalog.Find(code,version);
                var current=FocusMissionCatalog.Find(code,FocusMissionCatalog.Version);
                var historical=FocusMissionCatalog.Find(code,FocusMissionCatalog.GuidedVersion);
                Check(current.Steps.Length==historical.Steps.Length,"Purpose copy does not add or remove steps.");
                for(int i=0;i<current.Steps.Length;i++)
                {
                    var a=current.Steps[i];var b=historical.Steps[i];
                    Check(a.Key==b.Key && a.Kind==b.Kind && a.RecommendedSeconds==b.RecommendedSeconds
                        && a.RequiredAnswer==b.RequiredAnswer && a.OptionalInput==b.OptionalInput
                        && a.Options.SequenceEqual(b.Options),"Purpose copy preserves "+code+" step/timing/answer contracts.");
                }
                Check(current.Steps.Single(step=>step.Key=="practice").Body.Contains("몸이 닿는 감각")
                    && !historical.Steps.Single(step=>step.Key=="practice").Body.Contains("몸이 닿는 감각"),"Catalog anchor belongs only to the unpublished current version.");
                var seed=new FocusMissionValidation.MemoryStore();string id="pause-"+code+"-v"+version;
                using(var f=new FocusMissionValidation.Fixture(report))
                {
                    Check(f.Service.SelectMbctProgram("8w"),f.Service.LastError);
                    Check(f.Service.BeginFocusSession(id,"P01",1),f.Service.LastError);
                    // Valid synthetic draft for screen traversal, not a claim
                    // that this gate completes all preceding course occurrences.
                    var saved=f.Service.Snapshot;var session=saved.Sessions.Single(item=>item.SessionId==id);
                    session.FocusVersion=version;session.MissionId=definition.Id;session.FocusOrder=int.Parse(code.Substring(1));
                    session.MissionTitleSnapshot=definition.Title;session.MissionDescriptionSnapshot=definition.Overview;
                    session.FocusStepRecords=definition.Steps.Select((step,index)=>new FocusStepRecord {Step=index,Key=step.Key,Title=step.Title,Prompt=step.Body}).ToList();
                    StateCodec.Validate(saved);seed.Save(saved);
                }
                using(var f=new FocusMissionValidation.Fixture(report,chooseCourse:false,seededStore:seed))
                {
                    Check(f.Service.StartSession(id),f.Service.LastError);f.Invoke("SelectFocusSession",id);
                    // Choice buttons already advance immediately; another icon
                    // tap would finish practice and leave the screen under test.
                    f.Click("잠시 멈춤");
                    var session=Session(f.Service,id);
                    Check(session.FocusVersion==version && session.InstructionStep==1
                        && session.FocusStepRecords[0].Value=="잠시 멈춤","Actual selection reaches "+code+" v"+version+" practice; actual step="+session.InstructionStep+", version="+session.FocusVersion+".");
                    var clock=f.Ui.GetComponentInChildren<FocusPracticeClock>(false);
                    Check(clock!=null && clock.RecommendedSeconds==definition.Steps[1].RecommendedSeconds,"Observation wording does not change the recommended duration.");
                    Check(session.FocusStepRecords[1].Prompt==definition.Steps[1].Body && session.RewardAmount==0,
                        "Saved prompt retains its catalog version and merely viewing practice gives no reward.");
                    string expected=version==FocusMissionCatalog.Version?"잠시 멈춰 몸이 닿는 감각을 살펴요.":"하던 일을 멈추고 잠시 쉬어요.";
                    foreach(var size in new[]{new Vector2Int(780,1688),new Vector2Int(800,1280)})
                    {
                        f.Resize(size.x,size.y);var copy=Text(f.Ui,"Basic.Body");
                        Check(copy.text==expected,"Displayed "+code+" v"+version+" pause copy matches its version contract.");
                        Check(copy.preferredHeight<=copy.rectTransform.rect.height+.75f,"Complete pause instruction fits "+size+".");
                        if(version==FocusMissionCatalog.Version)f.Capture("qa-"+code.ToLowerInvariant()+"-pause-v5-"+size.x+"x"+size.y,capture);
                    }
                }
            }
        }
        static FocusMissionValidation.MemoryStore SeedRoleDraft(string id,string code,int version)
        {
            var seed=new FocusMissionValidation.MemoryStore();var definition=FocusMissionCatalog.Find(code,version);
            using(var f=new FocusMissionValidation.Fixture(report))
            {
                Check(f.Service.SelectMbctProgram("8w"),f.Service.LastError);
                Check(f.Service.BeginFocusSession(id,"P01",1),f.Service.LastError);
                var saved=f.Service.Snapshot;var session=saved.Sessions.Single(item=>item.SessionId==id);
                session.FocusVersion=version;session.MissionId=definition.Id;session.FocusOrder=int.Parse(code.Substring(1));
                session.MissionTitleSnapshot=definition.Title;session.MissionDescriptionSnapshot=definition.Overview;
                session.FocusStepRecords=definition.Steps.Select((step,index)=>new FocusStepRecord {Step=index,Key=step.Key,Title=step.Title,Prompt=step.Body}).ToList();
                StateCodec.Validate(saved);seed.Save(saved);
            }
            return seed;
        }
        static void SupportActivityRole()
        {
            foreach(string code in new[]{"P19","P26"})
            {
                string sourceId="support-source-"+code;
                var seed=SeedRoleDraft(sourceId,code,FocusMissionCatalog.Version);
                using(var f=new FocusMissionValidation.Fixture(report,chooseCourse:false,seededStore:seed))
                {
                    int before=FocusMissionPolicy.NextOrder(f.Service.Snapshot),coins=f.Service.CoinBalance;
                    Check(f.Service.StartSession(sourceId),f.Service.LastError);f.Invoke("SelectFocusSession",sourceId);
                    f.Click(code=="P19"?"짧은 소리 실습":"아직 없어요");
                    var snapshot=f.Service.Snapshot;
                    var source=snapshot.Sessions.Single(item=>item.SessionId==sourceId);
                    var support=snapshot.Sessions.Single(item=>item.FocusSupport && item.FocusSourceSessionId==sourceId);
                    Check(Text(f.Ui,"Guided.Mission").text=="추가 활동","Actual alternative overview identifies the additional activity.");
                    Check(support.MissionId=="course:P02" && support.FocusRole==FocusMissionCatalog.AdditionalRole
                        && FocusMissionCatalog.FindForSession(support).Role==FocusMissionCatalog.AdditionalRole,"Display and domain agree on the separate sound activity role.");
                    Check(source.Status==ParticipationState.Ended && source.FocusEndReason=="alternative" && source.RewardAmount==0
                        && FocusMissionPolicy.NextOrder(snapshot)==before && f.Service.CoinBalance==coins,"Alternative selection does not complete or reward its source course occurrence.");
                    Check(!snapshot.PracticeGrowthRecords.Any(item=>item.SessionId==sourceId),"Original deferred mission receives no growth.");
                    f.Capture("qa-"+code.ToLowerInvariant()+"-additional-p02",capture);
                    void CheckHomeCards()
                    {
                        string saved=StateCodec.Encode(f.Service.Snapshot);
                        f.Invoke("ShowHome");
                        var basic=Button(f.Ui,"Focus.BasicAction");
                        var additional=Button(f.Ui,"Focus.AdditionalAction");
                        var expected=FocusMissionPolicy.MissionAt(before,f.Service.Snapshot.MbctProgramId);
                        Check(basic.transform.parent.GetComponentsInChildren<Text>(false).Any(label=>label.text==expected.Title)
                            && basic.GetComponentInChildren<Text>().text=="기본 미션 시작하기",
                            "Home basic card presents its actual next course mission, not the additional P02 support activity.");
                        Check(additional.transform.parent.GetComponentsInChildren<Text>(false).Any(label=>label.text==support.MissionTitleSnapshot)
                            && additional.GetComponentInChildren<Text>().text=="이어서 하기",
                            "Only the additional card resumes the separate P02 support activity.");
                        Check(StateCodec.Encode(f.Service.Snapshot)==saved,"Home card display does not mutate the source or support session.");
                        f.Capture("qa-"+code.ToLowerInvariant()+"-support-home-"+Session(f.Service,support.SessionId).Status,capture);
                        f.Click("Focus.AdditionalAction");
                        Check(Text(f.Ui,"Guided.Mission").text=="추가 활동" && StateCodec.Encode(f.Service.Snapshot)==saved,
                            "Additional card resumes the same saved support activity without starting a second session.");
                    }
                    CheckHomeCards();
                    // The visible route was selected through UI. Finish the
                    // separate synthetic activity through its real domain APIs
                    // to check the role's quota, not an audio/timer simulation.
                    Check(f.Service.StartGuidedFocusSession(support.SessionId,""),f.Service.LastError);
                    CheckHomeCards();
                    Check(f.Service.StartFocusPracticeStep(support.SessionId,0),f.Service.LastError);
                    f.Clock.UtcNow=f.Clock.UtcNow.AddSeconds(1);
                    Check(f.Service.CompleteGuidedFocusPractice(support.SessionId,"","",1),f.Service.LastError);
                    Check(f.Service.CompletePracticeSession(support.SessionId),f.Service.LastError);
                    snapshot=f.Service.Snapshot;
                    Check(FocusMissionPolicy.NextOrder(snapshot)==before && Session(f.Service,sourceId).RewardAmount==0,
                        "Completing the separate activity leaves the original basic occurrence deferred.");
                    Check(FocusMissionPolicy.PracticeClaimedToday(snapshot,FocusMissionCatalog.AdditionalRole)
                        && !FocusMissionPolicy.PracticeClaimedToday(snapshot,FocusMissionCatalog.BasicRole)
                        && FocusMissionPolicy.AdditionalAvailability(snapshot)!=null,"Completion consumes only the additional daily slot.");
                    Check(!f.Service.BeginFocusSession("second-extra-"+code,"M03"),"Another additional activity cannot bypass the daily quota.");
                }
            }
            foreach(int version in new[]{FocusMissionCatalog.Version,FocusMissionCatalog.GuidedVersion})
            {
                string id="normal-p02-v"+version;
                using(var f=new FocusMissionValidation.Fixture(report,chooseCourse:false,seededStore:SeedRoleDraft(id,"P02",version)))
                {
                    f.Invoke("SelectFocusSession",id);
                    Check(!Session(f.Service,id).FocusSupport && Text(f.Ui,"Guided.Mission").text=="기본 미션",
                        "Ordinary P02 v"+version+" retains its basic-mission overview.");
                }
            }
        }
        static void PurposeCopy()
        {
            var body=typeof(WeekOneQuestDemo).GetMethod("BasicBodyFor",BindingFlags.Static|BindingFlags.NonPublic);
            Check(body!=null,"Actual page-body selection helper exists.");
            foreach(string code in new[]{"P09","P19","P24","P25","P26"})
            {
                var current=FocusMissionCatalog.Find(code,FocusMissionCatalog.Version);
                var old=FocusMissionCatalog.Find(code,FocusMissionCatalog.GuidedVersion);
                foreach(var step in current.Steps)
                {
                    string displayed=(string)body.Invoke(null,new object[]{new ActivitySession{FocusVersion=FocusMissionCatalog.Version},current,step,""});
                    Check(displayed==step.Body,"Current "+code+"/"+step.Key+" preserves reviewed catalog purpose.");
                }
                foreach(var step in old.Steps)
                {
                    string displayed=(string)body.Invoke(null,new object[]{new ActivitySession{FocusVersion=FocusMissionCatalog.GuidedVersion},old,step,""});
                    Check(displayed==FocusBasicSlideContent.Body(code,step.Key,step.Body,""),"Existing v4 "+code+"/"+step.Key+" keeps prior presentation.");
                }
            }
        }
        static void LoadRecovery(bool locked)
        {
            string testRoot=Path.Combine(saveRoot,locked?"writer-lock":"open-failure");Directory.CreateDirectory(testRoot);
            string path=GardenStatePaths.StateFile(Application.persistentDataPath,"MOCK",true,testRoot);
            using(var state=new GardenStateService(new FileStateStore(path),new SystemClock(),new DemoBalanceConfig()))
            {
                Check(state.Open(),state.LastError);Check(state.BeginFocusSession("restored-active","M03"),state.LastError);
            }
            byte[] original=File.ReadAllBytes(path);FileStateStore held=null;
            if(locked)held=new FileStateStore(path);else File.WriteAllText(path,"synthetic invalid XML");
            string originalOverride=Environment.GetEnvironmentVariable("CAPSTONE_SMOKE_SAVE_ROOT");
            var previous=SceneManager.GetActiveScene();var ambient=RenderSettings.ambientLight;
            var existingCamera=GameObject.Find("MockupCamera")?.GetComponent<Camera>();
            var existingLight=GameObject.Find("MoonLight")?.GetComponent<Light>();
            var cameraPosition=existingCamera==null?Vector3.zero:existingCamera.transform.position;
            float lightIntensity=existingLight==null?0:existingLight.intensity;
            var scene=EditorSceneManager.NewPreviewScene();
            var go=new GameObject("Synthetic recovery preview",typeof(RectTransform),typeof(Canvas),typeof(Image));
            go.SetActive(false);SceneManager.MoveGameObjectToScene(go,scene);
            var ui=go.AddComponent<WeekOneQuestDemo>();var provider=new SyntheticPhotos(Path.Combine(output,locked?"locked-photos":"failed-open-photos"));
            string orphan=Guid.NewGuid().ToString("N");provider.Write(orphan);ui.SetPhotoProviderForValidation(provider);
            try
            {
                Environment.SetEnvironmentVariable("CAPSTONE_SMOKE_SAVE_ROOT",testRoot);
                ui.Initialize(null,true);go.SetActive(true);
                Check(!ui.HasOpenState && (locked?ui.Service==null:ui.Service!=null),"Real construction/Open failure reaches the recoverable state.");
                var priorService=ui.Service;
                held?.Dispose();held=null;if(!locked)File.WriteAllBytes(path,original);
                Button(ui,"State.RetryLoad").onClick.Invoke();Canvas.ForceUpdateCanvases();
                Check(ui.HasOpenState && ui.Service!=null,"Retry opens the preserved synthetic save.");
                Check(locked?priorService==null:ReferenceEquals(priorService,ui.Service),"Retry recreates only the missing service.");
                Check(Field<string>(ui,"sessionId")=="restored-active","Active session is restored after retry.");
                Check(provider.Prunes==1 && !File.Exists(Path.Combine(provider.DirectoryPath,orphan+".jpg")),"Successful retry runs photo pruning exactly once.");
            }
            finally
            {
                held?.Dispose();ui.Service?.Dispose();EditorSceneManager.ClosePreviewScene(scene);
                if(previous.IsValid() && previous.isLoaded)SceneManager.SetActiveScene(previous);
                RenderSettings.ambientLight=ambient;
                if(existingCamera!=null)existingCamera.transform.position=cameraPosition;
                if(existingLight!=null)existingLight.intensity=lightIntensity;
                Environment.SetEnvironmentVariable("CAPSTONE_SMOKE_SAVE_ROOT",originalOverride);
            }
        }
        sealed class SyntheticPhotos : IMissionPhotoProvider
        {
            public string DirectoryPath {get;}
            public int Deletes,Prunes;
            public SyntheticPhotos(string path) {DirectoryPath=path;Directory.CreateDirectory(path);}
            public void Write(string id)
            {
                var image=new Texture2D(8,8,TextureFormat.RGB24,false);
                try {image.SetPixels(Enumerable.Repeat(new Color(.3f,.6f,.4f),64).ToArray());image.Apply();File.WriteAllBytes(Path.Combine(DirectoryPath,id+".jpg"),image.EncodeToJPG());}
                finally {UnityEngine.Object.DestroyImmediate(image);}
            }
            public PhotoVerificationResult Start(string id,string target) => PhotoVerificationService.Unavailable(id,target);
            public PhotoVerificationResult Poll(string id) => null;
            public void Delete(string id) {Deletes++;File.Delete(Path.Combine(DirectoryPath,id+".jpg"));}
            public void Prune(string[] keep)
            {
                Prunes++;foreach(string path in Directory.GetFiles(DirectoryPath,"*.jpg"))if(!keep.Contains(Path.GetFileNameWithoutExtension(path)))File.Delete(path);
            }
        }
    }
}
