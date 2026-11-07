using System;
using System.IO;
using System.Linq;
using System.Reflection;
using CapstoneDesign.Runtime;
using CapstoneDesign.Runtime.LocalState;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;
using Fixture=CapstoneDesign.EditorTools.FocusMissionValidation.Fixture;
using Report=CapstoneDesign.EditorTools.FocusMissionValidation.Report;

namespace CapstoneDesign.EditorTools
{
    // Explicit execution only. The shared fixture uses an isolated preview scene, memory-only
    // synthetic saves and first-hit raycasts. It does not read user records or PlayerPrefs.
    public static class FocusAdjustmentValidation
    {
        static bool running;
        static string OutputRoot
        {
            get
            {
                string requested=Environment.GetEnvironmentVariable("CAPSTONE_ARTIFACTS");
                return Path.Combine(string.IsNullOrWhiteSpace(requested)?Path.Combine(Directory.GetCurrentDirectory(),"artifacts"):Path.GetFullPath(requested),"focus-adjustment-validation");
            }
        }
        [MenuItem("Capstone Mockup/Validate Focus Adjustments (Synthetic)")]
        public static void Run()
        {
            if(running||EditorApplication.isCompiling||EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Run Focus adjustment validation outside Play mode after compilation.");
            running=true;Directory.CreateDirectory(OutputRoot);
            var report=new Report {runId="focus-adjustments-"+DateTimeOffset.UtcNow.ToString("yyyyMMdd-HHmmss"),utc=DateTimeOffset.UtcNow.ToString("O"),unityVersion=Application.unityVersion,
                environment="Unity Editor / isolated synthetic memory fixture / real button raycasts and callbacks / injected elapsed seconds, not real-time timing accuracy or device QA"};
            try
            {
                if(GraphicsSettings.currentRenderPipeline!=null&&RenderPipelineManager.currentPipeline==null)Blitter.Cleanup();
                Check(report,"Timed adjustment starts disabled, pauses modal, cancels to running and ends only current segment",()=>TimedAdjustment(report));
                Check(report,"Whole mission skip retains basic occurrence and gives no nutrients",()=>WholeMissionSkip(report));
                Check(report,"M10 activity and coarse period choices, optional text and draft restart",()=>PlanChoices(report));
                Check(report,"P25 version3 exposes intentional communication choices and requires selection",()=>CommunicationChoices(report));
                Check(report,"P19 alternative becomes actual P02 additional activity and leaves original occurrence pending",()=>ActualAlternative(report));
                Check(report,"Visible Focus recommendation keeps its decision through details and actual selection",()=>RecommendationSelection(report));
                Check(report,"Direct mission library selection cannot fabricate a recommendation exposure",()=>DirectLibrarySelection(report));
                Check(report,"Choosing another activity preserves separate actual and observed recommended mission IDs",()=>DeclinedRecommendation(report));
            }
            finally
            {
                File.WriteAllText(Path.Combine(OutputRoot,"report.json"),JsonUtility.ToJson(report,true));running=false;
            }
            Debug.Log("FOCUS_ADJUSTMENT_VALIDATION passed="+report.passed+" failed="+report.failed+" clicks="+report.buttonClicks+" raycasts="+report.raycastChecks+" report="+Path.Combine(OutputRoot,"report.json"));
            if(report.failed>0)throw new InvalidOperationException("Focus adjustment validation failed; inspect report.json.");
        }
        static void Check(Report report,string name,Action action)
        {
            try{action();report.passed++;report.checks.Add(new FocusMissionValidation.CheckResult{name=name,status="pass"});}
            catch(Exception ex){report.failed++;report.checks.Add(new FocusMissionValidation.CheckResult{name=name,status="fail",detail=ex.ToString()});Debug.LogWarning("FOCUS_ADJUSTMENT_VALIDATION "+name+"\n"+ex);}
        }
        static void Assert(bool value,string message){if(!value)throw new InvalidOperationException(message);}
        static Button Button(Fixture f,string name)=>f.Ui.GetComponentsInChildren<Button>(false).Single(button=>button.name==name&&button.gameObject.activeInHierarchy);
        static ActivitySession Session(Fixture f,string id)=>f.Service.Snapshot.Sessions.Single(session=>session.SessionId==id);
        static void StartAdditional(Fixture f,string code)
        {f.Ui.ShowFocusRecommendationFor(FocusMissionCatalog.Find(code).Id);f.Click("추가 미션 시작하기");f.Click("시작");}
        static void TimedAdjustment(Report report)
        {
            using(var f=new Fixture(report))
            {
                StartAdditional(f,"M02");f.Click("Focus.Action");
                Assert(f.Latest.InstructionStep==1,"Expected the timed observation segment.");
                Assert(!Button(f,"Focus.TooLong").interactable,"Too-long action was enabled before actual timer start.");
                f.Capture("adjust-01-before-timer",true);
                f.Click("Focus.TimerToggle");var clock=f.Ui.GetComponentInChildren<FocusPracticeClock>();
                Assert(clock.Running&&clock.HasStarted&&Button(f,"Focus.TooLong").interactable,"Timer start did not enable shortening.");
                Assert(!string.IsNullOrEmpty(f.Latest.FocusStepRecords[1].PracticeStartedUtc),"Actual timer start was not persisted.");
                // No sleeping or simulated device clock: exercise callback semantics with a known
                // synthetic elapsed value. Mono/Unity timer wall-clock accuracy is a separate check.
                clock.ElapsedSeconds=8;Assert(clock.Flush(),"Synthetic elapsed could not be stored.");
                f.Click("Focus.TooLong");Assert(!clock.Running,"Countdown kept running under the confirmation modal.");
                Assert(f.VisibleText.Contains("여기까지만 진행할까요?"),"Current-segment confirmation missing.");
                f.Capture("adjust-02-paused-shortening-modal",true);
                f.Click("Focus.AdjustmentCancel");
                Assert(clock.Running&&f.Latest.FocusStepRecords[1].ShortenedUtc==null,"Cancel did not resume or recorded false shortening feedback.");
                f.Click("Focus.TooLong");f.Click("Focus.AdjustmentConfirm");
                var saved=f.Latest;
                Assert(saved.InstructionStep==2&&saved.Status==ParticipationState.InProgress&&saved.RewardAmount==0,"Shortening ended entire mission or paid reward prematurely.");
                Assert(saved.FocusStepRecords[1].ElapsedSeconds==8&&saved.FocusStepRecords[1].EndReason=="too_long","Actual elapsed and confirmed ending reason missing.");
                Assert(f.Ui.CurrentScreen=="focus-practice"&&f.VisibleText.Contains("눈에 띈 것"),"Remaining guidance did not appear.");
                f.Capture("adjust-03-after-current-segment",true);
            }
        }
        static void WholeMissionSkip(Report report)
        {
            using(var f=new Fixture(report))
            {
                f.Ui.ShowHome();f.Click("기본 미션 시작하기");string source=f.Latest.SessionId;f.Click("시작");
                f.Click("Focus.Action");Assert(f.Latest.InstructionStep==1,"P04 should reach the hands/cup observation.");
                f.Click("Focus.PracticeStart");f.Click("Focus.EndParticipation");
                f.Capture("adjust-04-whole-mission-skip-modal",true);f.Click("Focus.AdjustmentConfirm");
                Assert(f.Ui.CurrentScreen=="home"&&Session(f,source).Status==ParticipationState.Ended,"Whole skip did not return to mission home.");
                Assert(Session(f,source).FocusEndReason=="skipped"&&FocusMissionPolicy.NextOrder(f.Service.Snapshot,"2w")==1,"Skip changed the original basic occurrence.");
                Assert(f.Service.Snapshot.Nutrient==0,"Incomplete participation granted nutrient.");
                Assert(f.Service.Snapshot.PlantCollection.ParticipationDecisions.Count(row=>row.Units>0)==1,"Actual started participation did not create its small-growth record.");
                f.Capture("adjust-05-pending-course-after-skip",true);
            }
        }
        static void PlanChoices(Report report)
        {
            using(var f=new Fixture(report))
            {
                StartAdditional(f,"M10");Assert(!Button(f,"Focus.Action").interactable,"Empty plan was accepted.");
                f.Click("Focus.PlanActivity.0");Assert(!Button(f,"Focus.Action").interactable,"Activity without period was silently assigned a clock time.");
                f.Click("Focus.PlanPeriod.2");Assert(Button(f,"Focus.Action").interactable,"Activity plus coarse period did not enable plan.");
                Assert(f.Latest.FocusStepRecords[0].Value=="저녁 · 차 마시기","M10 choice did not store its actual plan.");
                f.Capture("adjust-06-plan-choices",true);
                var input=f.Ui.GetComponentsInChildren<InputField>(false).Single(field=>field.name=="Focus.PlanNote");
                input.text="synthetic personal plan";input.onEndEdit.Invoke(input.text);
                Assert(f.Latest.FocusStepRecords[0].Value=="저녁 · synthetic personal plan","Optional plan text did not combine with chosen period.");
                f.Click("닫기");f.Click("추가 미션 이어서 하기");
                input=f.Ui.GetComponentsInChildren<InputField>(false).Single(field=>field.name=="Focus.PlanNote");
                Assert(input.text=="synthetic personal plan"&&f.Latest.FocusStepRecords[0].PlanTimePeriod=="저녁","Structured plan draft did not survive navigation.");
                f.Click("Focus.Action");Assert(f.Latest.InstructionStep==1&&f.VisibleText.Contains("저녁 · synthetic personal plan"),"Plan review omitted the actual chosen content.");
                f.Capture("adjust-07-plan-review",true);
            }
        }
        static void PrepareCourse(Fixture f,int target)
        {
            Assert(f.Service.SelectMbctProgram("8w"),f.Service.LastError);
            for(int order=1;order<target;order++)
            {
                while(FocusMissionPolicy.Availability(f.Service.Snapshot)!=null){f.Clock.UtcNow=f.Clock.UtcNow.AddDays(1);Assert(f.Service.RefreshCycle(Array.Empty<string>()),f.Service.LastError);}
                string id="synthetic-adjustment-before-"+order;var mission=FocusMissionPolicy.MissionAt(order,"8w");
                Assert(f.Service.BeginFocusSession(id,mission.Id,order)&&f.Service.StartSession(id),f.Service.LastError);
                while(true)
                {
                    var current=Session(f,id);if(current.InstructionStep>=mission.Steps.Length)break;
                    int index=current.InstructionStep;var step=mission.Steps[index];
                    string value=step.Options.FirstOrDefault()??(step.RequiredAnswer?"synthetic preparation":"");
                    Assert(f.Service.ConfirmFocusStep(id,index,value,string.IsNullOrEmpty(value)),f.Service.LastError);
                }
                Assert(f.Service.CompleteFocusSession(id),f.Service.LastError);
            }
            while(FocusMissionPolicy.Availability(f.Service.Snapshot)!=null){f.Clock.UtcNow=f.Clock.UtcNow.AddDays(1);Assert(f.Service.RefreshCycle(Array.Empty<string>()),f.Service.LastError);}
            f.Ui.ShowHome();f.Click("기본 미션 시작하기");f.Click("시작");
        }
        static void CommunicationChoices(Report report)
        {
            using(var f=new Fixture(report))
            {
                PrepareCourse(f,25);Assert(f.Latest.MissionId=="course:P25"&&f.Latest.FocusVersion==3,"P25 v3 fixture missing.");
                f.Click("Focus.Action");f.Click("Focus.Action");
                Assert(f.Latest.InstructionStep==2&&!Button(f,"Focus.Action").interactable,"Intentional action must be chosen explicitly.");
                foreach(string value in new[]{"말하거나 보냈어요","고쳐서 전했어요","나중에 전하기","전하지 않기"})Assert(f.Has("Focus.Option."+value),"Communication option missing: "+value);
                f.Capture("adjust-08-communication-choices",true);f.Click("Focus.Option.전하지 않기");f.Click("Focus.Action");
                Assert(f.Latest.FocusStepRecords[2].Value=="전하지 않기"&&f.Latest.InstructionStep==3,"Actual choice was not preserved as the performed action.");
                f.Capture("adjust-09-communication-after-choice",true);
            }
        }
        static void ActualAlternative(Report report)
        {
            using(var f=new Fixture(report))
            {
                PrepareCourse(f,19);string sourceId=f.Latest.SessionId;int before=f.Service.Snapshot.Nutrient;
                f.Click("Focus.Option.짧은 소리 실습");f.Capture("adjust-10-sound-alternative-choice",true);f.Click("Focus.Action");
                var support=f.Latest;
                Assert(support.FocusSupport&&support.MissionId=="course:P02"&&support.FocusRole==FocusMissionCatalog.AdditionalRole&&support.FocusSourceSessionId==sourceId,"Selected alternative was mislabeled as mountain completion.");
                Assert(Session(f,sourceId).Status==ParticipationState.Ended&&FocusMissionPolicy.NextOrder(f.Service.Snapshot,"8w")==19,"Actual alternative advanced the original course.");
                Assert(f.Service.Snapshot.Nutrient==before&&f.VisibleText.Contains("주변 소리 만나기"),"New support activity was completed or mislabeled before start.");
                f.Capture("adjust-11-actual-sound-support-overview",true);
                f.Click("시작");f.CompletePractice(FocusMissionCatalog.FindForSession(f.Latest),false);f.CompleteReflection("synthetic support observation","",false);
                Assert(f.Latest.MissionId=="course:P02"&&f.Latest.Status==ParticipationState.RewardCommitted&&FocusMissionPolicy.NextOrder(f.Service.Snapshot,"8w")==19,"Support completion changed original occurrence or actual activity identity.");
                f.Capture("adjust-12-sound-support-complete",true);
            }
        }
        static FocusRecommendationExposure VisibleRecommendation(Fixture f)
            =>(FocusRecommendationExposure)typeof(WeekOneQuestDemo).GetField("focusVisibleRecommendation",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(f.Ui);
        static void ObserveVisibleRecommendation(Fixture f,string capture)
        {
            var observer=f.Ui.GetComponentsInChildren<RecommendationExposure>(false).Single();
            var snapshot=VisibleRecommendation(f);Assert(snapshot!=null&&string.IsNullOrEmpty(snapshot.ShownUtc),"Rendering alone invented an exposure.");
            var rect=(RectTransform)observer.transform;var original=rect.anchoredPosition;int observed=0;
            observer.OnVisible+=()=>observed++;
            // Preview scenes do not dispatch MonoBehaviour frames. Invoke the runtime visibility
            // lifecycle with real viewport geometry, never the unconditional ReportVisible shortcut.
            rect.anchoredPosition=original+new Vector2(0,-10000);Canvas.ForceUpdateCanvases();
            observer.SendMessage("LateUpdate",SendMessageOptions.RequireReceiver);
            Assert(observed==0&&string.IsNullOrEmpty(snapshot.ShownUtc),"Offscreen recommendation was counted as shown.");
            rect.anchoredPosition=original;f.Capture(capture,true);
            observer.SendMessage("LateUpdate",SendMessageOptions.RequireReceiver);
            Assert(observed==1&&!string.IsNullOrEmpty(snapshot.ShownUtc),"Visible recommendation did not report its observed time.");
            observer.SendMessage("LateUpdate",SendMessageOptions.RequireReceiver);
            Assert(observed==1,"One visible card reported duplicate exposures.");
        }
        static void RecommendationSelection(Report report)
        {
            using(var f=new Fixture(report))
            {
                Assert(f.Service.SavePreference("favorite","호흡 관찰"),f.Service.LastError);f.Ui.ShowHome();
                ObserveVisibleRecommendation(f,"adjust-13-recommendation-visible");
                var shown=VisibleRecommendation(f);string decision=shown.DecisionId,shownUtc=shown.ShownUtc;
                Assert(shown.MissionId=="free-mission:M17","Synthetic preferred recommendation was not breathing.");
                f.Click("추천 미션 보기");Assert(f.Ui.CurrentScreen=="focus-recommendation","Recommendation details did not open.");
                Assert(VisibleRecommendation(f).DecisionId==decision,"Details rerolled the observed decision.");
                f.Click("추가 미션 시작하기");var selected=f.Latest;
                Assert(selected.MissionId=="free-mission:M17"&&selected.FocusRecommendationExposure!=null,"Selected recommendation lost actual activity or attribution.");
                Assert(selected.FocusRecommendationId==decision&&selected.FocusRecommendationExposure.DecisionId==decision
                    &&selected.FocusRecommendationExposure.ShownUtc==shownUtc,"Selected session did not freeze the actually observed decision/time.");
                f.Capture("adjust-14-recommendation-selected",true);
                f.Click("시작");Assert(f.Latest.FocusRecommendationId==decision,"Starting practice changed exposure attribution.");
            }
        }
        static void DirectLibrarySelection(Report report)
        {
            using(var f=new Fixture(report,chooseCourse:false))
            {
                Assert(f.Ui.GetComponentsInChildren<RecommendationExposure>(false).Length==0,"Fresh course selection should not expose a recommendation.");
                f.Ui.ShowFocusMissionLibrary();f.Click("Focus.FreeMission.M02");
                Assert(f.Ui.GetComponentsInChildren<RecommendationExposure>(false).Length==0,"Direct catalog detail was mislabeled as an algorithm recommendation.");
                f.Click("추가 미션 시작하기");var selected=f.Latest;
                Assert(selected.MissionId=="free-mission:M02"&&selected.FocusRecommendationExposure==null
                    &&selected.FocusRecommendationId==null,"Direct selection fabricated an observed algorithm decision.");
                f.Capture("adjust-15-direct-library-selection",true);
            }
        }
        static void DeclinedRecommendation(Report report)
        {
            using(var f=new Fixture(report))
            {
                Assert(f.Service.SavePreference("favorite","호흡 관찰"),f.Service.LastError);f.Ui.ShowHome();
                ObserveVisibleRecommendation(f,"adjust-16-before-different-choice");string decision=VisibleRecommendation(f).DecisionId;
                f.Click("추천·자유 미션 모두 보기");f.Click("Focus.FreeMission.M02");f.Click("추가 미션 시작하기");
                var selected=f.Latest;var exposure=selected.FocusRecommendationExposure;
                Assert(selected.MissionId=="free-mission:M02"&&exposure!=null&&exposure.MissionId=="free-mission:M17"
                    &&exposure.DecisionId==decision,"Declined recommendation was replaced with the chosen mission or lost its observed decision.");
                Assert(selected.FocusRecommendationReason=="saved-preference","Fixed explanation code was not preserved.");
                f.Capture("adjust-17-different-activity-selected",true);
            }
        }
    }
}
