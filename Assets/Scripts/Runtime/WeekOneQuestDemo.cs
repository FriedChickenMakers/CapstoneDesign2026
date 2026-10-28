using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using CapstoneDesign.Runtime.LocalState;
using CapstoneDesign.Prototype;

namespace CapstoneDesign.Runtime
{
    // Keeps the original serialized component identity while replacing split PlayerPrefs writes.
    [DefaultExecutionOrder(-100)]
    public sealed partial class WeekOneQuestDemo : MonoBehaviour
    {
        const string ClosedStepQueryCursorKey = "capstone.garden.m15.closed_step_query_cursor";
        public Button[] questButtons = Array.Empty<Button>();
        public Text[] questLabels = Array.Empty<Text>();
        public Text summary;
        public Transform rewardPlant;
        public GardenStateService Service { get; private set; }
        public bool HasOpenState => opened;
        public AndroidPlatformSnapshot PreviewHealth;
        public DemoBalanceConfig Balance = new DemoBalanceConfig();
        GameObject view, pageViewport, gardenObjects, ghost;
        string gardenSignature;
        public double LastCourseBuildMs {get;private set;}
        public double LastFreeBuildMs {get;private set;}
        Text islandStatus, notice, stateSummary;
        Vector3 basePlantScale;
        string draftNote;
        string sessionId, mood, pendingEnvironment, pendingPurchase, error, screen = "home";
        InputField note;
        bool opened, preview;
        bool integratedPurchasePreview, coursePageInitialized;
        float nextCycleCheck;
        int freeIndex, coursePage, recordsPage;
        string mode;
        string lastClosedStepQueryPeriodId;
        sealed class StepQuery
        {
            public string Id, PeriodId, SessionId;
            public long StartMs, EndMs;
            public bool IsClosedPeriod, Aggregate;
            public float StartedAt;
        }
        readonly Queue<StepQuery> stepQueries = new Queue<StepQuery>();
        StepQuery runningStepQuery;
        float nextStepSync;
        float nextStepPoll;
        float nextClosedStepSync;
        bool forceClosedStepSync, preferNewestClosedStepPeriod;
        string stepQueryStatus = "아직 걸음 구간을 조회하지 않았어요";

        void Awake() { Initialize(null, false); }
        public void Initialize(GardenStateService injected, bool isPreview)
        {
            if (Service != null) return;
            preview = isPreview;
            mode = preview ? "MOCK" : AndroidPlatformBridge.InputMode.ToString().ToUpperInvariant();
            lastClosedStepQueryPeriodId=PlayerPrefs.GetString(ClosedStepQueryCursorKey,string.Empty);
            basePlantScale = rewardPlant == null ? Vector3.one : rewardPlant.localScale;
            GardenUi.ResolveFont();
            transform.root.GetComponent<MockupNavigation>()?.PreparePresentation();
            var panelImage=GetComponent<Image>();if(panelImage!=null)panelImage.color=Color.clear;
            var light=GameObject.Find("MoonLight")?.GetComponent<Light>();if(light!=null)light.intensity=1.1f;
            RenderSettings.ambientLight=new Color(.27f,.32f,.40f);
            var camera=GameObject.Find("MockupCamera")?.GetComponent<Camera>();if(camera!=null)camera.transform.position=new Vector3(0,4.5f,-14.5f);
            foreach (var text in transform.root.GetComponentsInChildren<Text>(true)) GardenTypography.ApplyBody(text);
            for (int i=transform.childCount-1;i>=0;i--) transform.GetChild(i).gameObject.SetActive(false);
            try
            {
                Service = injected ?? new GardenStateService(new FileStateStore(Path.Combine(SaveRoot,
                    "garden-" + mode.ToLowerInvariant(), "state.xml")), new SystemClock(), Balance, mode == "LIVE" ? ReadLegacy() : new LegacySnapshot());
                opened=Service.Open();
                if (!opened) error=Service.LastError;
                else { RefreshCycle(); sessionId=Service.Snapshot.Sessions.LastOrDefault(s=>s.Status==ParticipationState.InProgress || s.Status==ParticipationState.Paused || s.Status==ParticipationState.Selected)?.SessionId; }
            }
            catch (Exception ex) { error=ex.Message; }
            SetupIsland(); ShowHome();
            if(opened)ReconcileMissionReminder();
        }
        static string SaveRoot
        {
            get
            {
#if UNITY_EDITOR
                var smoke=Environment.GetEnvironmentVariable("CAPSTONE_SMOKE_SAVE_ROOT");
                if(!string.IsNullOrEmpty(smoke))return smoke;
#endif
                return Application.persistentDataPath;
            }
        }
        static LegacySnapshot ReadLegacy()
        {
            var legacy=new LegacySnapshot { Nutrient=PlayerPrefs.GetInt("capstone.garden.nutrient",0), GardenXp=PlayerPrefs.GetInt("capstone.garden.xp",0), Growth=PlayerPrefs.GetFloat("capstone.garden.growth",0), LastUnlock=PlayerPrefs.GetString("capstone.garden.last_unlock","") };
            foreach(var q in WeekOneQuestLibrary.Create()) if(PlayerPrefs.GetInt(FirstIncompleteQuestRecommendation.CompletionKey(q.id),0)!=0) legacy.CompletedMissionIds.Add("free-mission:legacy-"+q.id);
            if(PlayerPrefs.GetInt("capstone.garden.unlock.Week 1 seed",0)!=0) legacy.UnlockIds.Add("Week 1 seed");
            if(!string.IsNullOrEmpty(legacy.LastUnlock) && !legacy.UnlockIds.Contains(legacy.LastUnlock)) legacy.UnlockIds.Add(legacy.LastUnlock);
            return legacy;
        }
        void Update()
        {
            ObserveOptionalRecommendation();
            TickRecordHeartRefresh();
            TickWalkingPersonalization();
            TickMissionNotifications();
            if(opened && Time.unscaledTime >= nextCycleCheck) { nextCycleCheck=Time.unscaledTime+30; RefreshCycle(); }
        }
        void OnApplicationFocus(bool focus) { if(!opened)return; if(focus)OnAppReturned();else SaveActivePracticeDraft(); }
        void OnApplicationPause(bool paused) { if(paused && opened)SaveActivePracticeDraft(); }
        void OnDestroy() { Service?.Dispose(); if(gardenObjects!=null) { if(Application.isPlaying) Destroy(gardenObjects); else DestroyImmediate(gardenObjects); } }
        public bool RefreshCycle()
        {
            using var timing = UiPerformanceProbe.Measure("Activities.RefreshCycle");
            if(!opened) return false;
            var state=Service.Snapshot;
            var candidates=EligibleCycleAnimals(state);
            if(!Service.RefreshCycle(candidates))
            {
                error=Service.LastError;
                if(notice!=null){notice.gameObject.SetActive(true);notice.text="일일 상태 저장 실패 · 기존 진행을 보존했어요\n"+error;}
                return false;
            }
            if(state.LastCycleId!=Service.Snapshot.LastCycleId)
            {
                UpdateGarden();
                if(screen=="home" && isActiveAndEnabled)ShowHome();
            }
            return true;
        }
        static System.Collections.Generic.IEnumerable<string> EligibleCycleAnimals(GardenState state) => state.Environments
            .SelectMany(e=>MindfulnessContent.FindGardenContent(e.EnvironmentId)?.candidateAnimalIds ?? Array.Empty<string>()).Where(id=>id=="animal:A07").Distinct();
        bool Apply(bool success)
        {
            error=success ? null : Service.LastError;
            if(success) { UpdateGarden(); ObserveOptionalRecommendation(force:true); }
            else if(notice!=null){notice.gameObject.SetActive(true);notice.text="저장하지 못했어요. 입력을 유지했으니 다시 시도해 주세요.";}
            return success;
        }
        Transform Page(string title,GardenState state=null)
        {
            using var timing = UiPerformanceProbe.Measure("Activities.Page");
            ClearRecordHeartRefresh();
            ClearIntegratedPurchasePreview();
            var background=GetComponent<Image>();if(background!=null)background.color=GardenUi.Background;
            ClearPageView();
            var content=GardenUi.ScrollPage(transform,"Local garden flow",.105f,1,
                screen=="course"?1840:screen=="home"?1200:1600);
            pageViewport=content.parent.gameObject;view=content.gameObject;
            view.AddComponent<Image>().color=GardenUi.Background;
            if(!GardenUi.HasSharedHeader(transform))
            {
                var brand=GardenUi.Label(content,"마음 정원",48f/720f,.962f,624f/720f,.03f,30);
                GardenTypography.SetWeight(brand,true);brand.color=GardenUi.Green;PlacePageHeader(brand,24,54);
            }
            var heading=GardenUi.Label(content,title,48f/720f,.87f,624f/720f,.09f,GardenUi.TitleSize);GardenTypography.SetWeight(heading,true);
            bool longTitle=heading.preferredWidth>624;
            PlacePageHeader(heading,118,longTitle?138:78);
            if(longTitle)((RectTransform)content).sizeDelta+=new Vector2(0,240);
            var s=opened ? state??Service.Snapshot : null;
            stateSummary=GardenUi.Label(content,(mode=="LIVE"?"":"["+mode+"]  ")+ (s==null ? "저장 상태를 확인해 주세요" : CourseProgressLabel(s)),48f/720f,.81f,624f/720f,.05f,GardenUi.BodySize);
            stateSummary.color=GardenUi.Muted;
            PlacePageHeader(stateSummary,longTitle?266:206,60);
            if(screen!="placement" && screen!="growth")
            {
                var progress=GardenUi.Box(content,"Course progress",48f/720f,0,624f/720f,0);
                GardenUi.FromTop(progress.transform,longTitle?342:282,4);
                GardenUi.Progress(progress.transform,s==null?0:MbctPolicy.CompletedCount(s)/(float)MbctPolicy.PracticeCount(MbctPolicy.ActiveProgram(s)),0,0,1,1);
            }
            notice=GardenUi.Label(content,string.IsNullOrEmpty(error)?"":"저장하지 못했어요. 잠시 후 다시 시도해 주세요.",48f/720f,.01f,624f/720f,.05f,GardenUi.CaptionSize);
            if(screen=="home")GardenUi.FromTop(notice.transform,1100,80);
            notice.color=GardenUi.Warning;
            return content;
        }
        static void PlacePageHeader(Text label,float top,float height)
        {
            var rect=label.rectTransform;
            rect.anchorMin=new Vector2(48f/720f,1);rect.anchorMax=new Vector2(672f/720f,1);
            rect.offsetMin=new Vector2(0,-top-height);rect.offsetMax=new Vector2(0,-top);
        }
        void ShowLegacyHome()
        {
            using var timing = UiPerformanceProbe.Measure("Activities.ShowHome");
            if(opened && !SaveActivePracticeDraft())return;ClearPracticeDraft();
            RequestWalkingBaselineRefresh();
            EnterPage("home",ShowHome);var s=opened?Service.Snapshot:null;var p=Page("오늘의 활동",s);
            if(!opened) { GardenUi.Label(p,"저장본을 보존했습니다. 새 계정으로 초기화하지 않습니다.\n앱을 닫고 저장 공간과 복구 안내를 확인해 주세요.",48f/720f,.48f,624f/720f,.25f); return; }
            var active=s.Sessions.LastOrDefault(a=>a.Status==ParticipationState.Selected || a.Status==ParticipationState.InProgress || a.Status==ParticipationState.Paused);
            sessionId=active?.SessionId;
            if(!s.MbctProgramChosen && active==null) { ShowProgramSelection();return; }
            string program=MbctPolicy.ActiveProgram(s);
            var mission=active!=null?MindfulnessContent.FindMission(active.MissionId):MbctContent.ForProgram(program).FirstOrDefault(m=>m.recommendedOrder==MbctPolicy.NextOrder(s));
            string context=active!=null?(active.Status==ParticipationState.Selected?"선택한 활동":active.Status==ParticipationState.Paused?"잠시 쉬는 활동":"진행 중인 활동"):
                mission==null?"차곡차곡 쌓인 나의 실천":"이번 추천 · "+mission.week+"주차";
            GardenUi.FromTop(GardenUi.Card(p,"Recommended activity",48f/720f,0,624f/720f,0,GardenUi.Pale).transform,314,184);
            var week=GardenUi.Label(p,context,.10f,0,.80f,0,GardenUi.CaptionSize);
            week.color=GardenUi.Muted;GardenUi.FromTop(week.transform,338,40);
            var recommendation=GardenUi.Label(p,mission?.title ?? (active!=null?"진행하던 활동":MbctPolicy.ProgramWeeks(program)+"주 활동을 마쳤어요"),.10f,0,.80f,0,GardenUi.HeadingSize);
            GardenUi.FromTop(recommendation.transform,384,104);
            GardenTypography.SetWeight(recommendation,true);
            if(active!=null) GardenUi.FromTop(GardenUi.Button(p,active.Status==ParticipationState.Selected?"선택한 활동 안내":"진행하던 활동으로",48f/720f,0,624f/720f,0,ShowSession,true).transform,522,88);
            else if(mission!=null && MbctPolicy.Availability(s)==null) GardenUi.FromTop(GardenUi.Button(p,"추천 활동 안내",48f/720f,0,624f/720f,0,()=>SelectMission(mission),true).transform,522,88);
            if(string.IsNullOrEmpty(sessionId) && MbctPolicy.Availability(s)!=null) GardenUi.FromTop(GardenUi.Label(p,MbctPolicy.Availability(s),48f/720f,0,624f/720f,0,26).transform,522,104);
            var destinations=ActivityList(p,"Activity destinations");
            var destinationsRect=(RectTransform)destinations;
            destinationsRect.anchorMin=new Vector2(48f/720f,1);destinationsRect.anchorMax=new Vector2(672f/720f,1);
            destinationsRect.pivot=new Vector2(.5f,1);
            GardenUi.FromTop(destinations,650,0);
            destinations.gameObject.AddComponent<ContentSizeFitter>().verticalFit=ContentSizeFitter.FitMode.PreferredSize;
            ActivityListItem(destinations,MbctPolicy.ProgramWeeks(program)+"주 코스 둘러보기",null,ShowCourse,PrototypeUiIcon.Symbol.Journal);
            ActivityListItem(destinations,"배운 활동 다시 하기",null,ShowFree,PrototypeUiIcon.Symbol.Leaf);
            ActivityListItem(destinations,"정원 가꾸기",null,ShowShop,PrototypeUiIcon.Symbol.Leaf);
            ActivityListItem(destinations,"참여 기록과 나의 계획",null,ShowRecords,PrototypeUiIcon.Symbol.Journal);
            AddOptionalRecommendation(destinations,s);
        }
        void ShowLegacyCourse()
        {
            using var timing = UiPerformanceProbe.Measure("Activities.ShowCourse");
            var timer=System.Diagnostics.Stopwatch.StartNew();long reads=Service.SnapshotReads;
            var state=Service.Snapshot;string availability=MbctPolicy.Availability(state);
            string program=MbctPolicy.ActiveProgram(state);int weeks=MbctPolicy.ProgramWeeks(program),nextOrder=MbctPolicy.NextOrder(state);
            var course=MbctContent.ForProgram(program);
            if(!coursePageInitialized)
            {
                coursePage=Mathf.Clamp((nextOrder-1)/MbctPolicy.PracticesPerWeek,0,weeks-1);
                coursePageInitialized=true;
            }
            coursePage=Mathf.Clamp(coursePage,0,weeks-1);
            EnterPage("course",ShowCourse);var p=FocusPage(weeks+"주 마음챙김 활동",(coursePage+1)+"주 · "+MbctContent.TitlesForProgram(program)[coursePage]);
            GardenUi.FlowLabel(p,"새 활동은 순서대로 열려요. 완료한 활동은 다시 할 수 있어요.",GardenUi.BodySize,GardenUi.Muted);
            var activities=ActivityList(p,"Week activities");
            for(int i=0;i<6;i++)
            {
                var m=course[coursePage*6+i];bool learned=m.recommendedOrder<nextOrder;
                bool current=m.recommendedOrder==nextOrder;
                string detail=current?(availability==null?"이번 추천 활동":"다음 참여에서 이어가기"):null;
                var b=ActivityListItem(activities,(i+1)+"일차  "+m.title,detail,()=>SelectMission(m),
                    learned?PrototypeUiIcon.Symbol.Check:current?PrototypeUiIcon.Symbol.Arrow:PrototypeUiIcon.Symbol.Lock,current);
                b.interactable=learned || (current && availability==null);
                if(!b.interactable){foreach(var label in b.GetComponentsInChildren<Text>())label.color=GardenUi.Muted;foreach(var icon in b.GetComponentsInChildren<PrototypeUiIcon>())icon.color=GardenUi.Muted;}
            }
            DockPair("이전 주",()=>{coursePage--;ShowCourse();},"다음 주",()=>{coursePage++;ShowCourse();});
            var buttons=activityActions.GetComponentsInChildren<Button>();
            buttons.First(b=>b.name=="이전 주").interactable=coursePage>0;
            buttons.First(b=>b.name=="다음 주").interactable=coursePage<weeks-1;
            DockAction("다른 기간 살펴보기",ShowProgramSelection);
            DockAction("돌아가기",ShowHome);
            LastCourseBuildMs=timer.Elapsed.TotalMilliseconds;TraceScreen("course",LastCourseBuildMs,Service.SnapshotReads-reads);
        }
        void ShowLegacyFree()
        {
            using var timing = UiPerformanceProbe.Measure("Activities.ShowFree");
            var timer=System.Diagnostics.Stopwatch.StartNew();long reads=Service.SnapshotReads;
            var state=Service.Snapshot;EnterPage("free",ShowFree);var p=FocusPage("배운 활동 다시 하기","내 속도로 복습");
            var learnedIds=new System.Collections.Generic.HashSet<string>(MbctPolicy.AllCompletedIds(state));
            var learned=MbctContent.AllCourses.Where(m=>learnedIds.Contains(m.id)).ToArray();
            if(learned.Length==0)Guidance(p,"추천 활동을 해본 뒤 다시 연습할 수 있어요.");
            else
            {
                freeIndex=Math.Min(freeIndex,learned.Length-1);var m=learned[freeIndex];
                GardenUi.FlowLabel(p,(freeIndex+1)+" / "+learned.Length+" · 배운 활동",GardenUi.CaptionSize,GardenUi.Muted);
                GardenTypography.SetWeight(GardenUi.FlowLabel(p,m.title,GardenUi.HeadingSize),true);
                Guidance(p,m.purpose);
                GardenUi.FlowLabel(p,"복습에는 추가 보상이 없어요.",GardenUi.CaptionSize,GardenUi.Muted);
                var another=GardenUi.Button(p,"다른 활동",0,0,1,0,()=>{freeIndex=(freeIndex+1)%learned.Length;ShowFree();});
                another.gameObject.AddComponent<GardenFlowButton>();
                DockAction("이 활동 안내",()=>SelectMission(m),true);
            }
            var walking=GardenUi.MenuButton(p,"걸음 기록과 걷기",null,0,0,1,0,()=>SelectMission(MindfulnessContent.FindMission("free-mission:M15")),PrototypeUiIcon.Symbol.Leaf);
            walking.gameObject.AddComponent<LayoutElement>().preferredHeight=96;
            var preference=GardenUi.MenuButton(p,"추가 활동 추천 설정",null,0,0,1,0,ShowRecommendationPreferences,PrototypeUiIcon.Symbol.Gear);
            preference.gameObject.AddComponent<LayoutElement>().preferredHeight=96;
            DockAction("돌아가기",ShowHome);
            LastFreeBuildMs=timer.Elapsed.TotalMilliseconds;TraceScreen("free",LastFreeBuildMs,Service.SnapshotReads-reads);
        }
        static string CourseProgressLabel(GardenState state) => "영양제 "+state.Nutrient+"  ·  "+MbctPolicy.ProgramWeeks(MbctPolicy.ActiveProgram(state))+"주 코스 "+MbctPolicy.CompletedCount(state)+" / "+MbctPolicy.PracticeCount(MbctPolicy.ActiveProgram(state));
        static void TraceScreen(string name,double milliseconds,long snapshots)
        {
            if(Debug.isDebugBuild)Debug.Log("MBCT_UI "+name+" buildMs="+milliseconds.ToString("F2",System.Globalization.CultureInfo.InvariantCulture)+" snapshots="+snapshots);
        }
        public void SelectMission(MissionDefinition mission,RecommendationContext recommendation=null)
        {
            if(!opened){ShowHome();return;}
            if(!RefreshCycle()){ShowHome();return;}
            // One active session survives navigation and app restart. Selecting elsewhere cannot clone it.
            var active=Service.Snapshot.Sessions.LastOrDefault(s=>s.Status==ParticipationState.Selected || s.Status==ParticipationState.InProgress || s.Status==ParticipationState.Paused);
            if(active!=null) { sessionId=active.SessionId; ShowSession(); return; }
            var id=Guid.NewGuid().ToString("N");
            if(Apply(Service.BeginSession(id,mission.id,mission.type=="course"?mission.recommendedOrder:0,recommendation))) {sessionId=id; mood=null;draftNote=null;}
            ShowSession();
        }
        ActivitySession ActiveSession() => Service.Snapshot.Sessions.FirstOrDefault(s=>s.SessionId==sessionId);
        public void ShowSession()
        {
            if(FocusMissionPolicy.IsFocus(ActiveSession())){ShowFocusPractice();return;}
            using var timing = UiPerformanceProbe.Measure("Activities.ShowSession");
            // A platform refresh can arrive while the optional input is focused.
            // Save before taking the replacement view's snapshot or disabling it.
            if(opened && !SaveActivePracticeDraft())return;
            ClearPracticeDraft();
            bool enteringSession=screen!="session";
            EnterPage("session",ShowSession); var s=ActiveSession(); if(s==null){ShowHome();return;}
            var m=MindfulnessContent.FindMission(s.MissionId); var p=FocusPage(m?.title ?? s.MissionId,
                s.Status==ParticipationState.Selected?"활동 안내":s.Status==ParticipationState.Paused?"잠시 쉬는 중":"활동 중");
            if(m?.mbctActivity>0){ShowMbctSession(s,m,p);return;}
            bool walking=s.MissionId=="free-mission:M15";
            walkingIntroStart=null;walkingIntroGoal=null;walkingIntroExplanation=null;walkingIntroSession=null;
            if(walking && s.Status==ParticipationState.Selected)RequestWalkingBaselineRefresh();
            if(walking && enteringSession)RequestStepSyncSoon();
            Guidance(p,walking ? "오늘의 걸음을 확인하며 편한 속도로 걸어 보세요. 동기화가 늦어도 직접 완료할 수 있어요."
                : (m?.textOnlyInstructions ?? "편안한 만큼 참여해 주세요."));
            if(walking)
            {
                var target=s.Status==ParticipationState.Selected?WalkingPersonalization.Evaluate(
                    ReadWalkingHealth()?.healthConnect?.personalization,Service.CurrentUtc,Service.CurrentTimeZone):null;
                int goal=DebugWalkEnabled?10:target?.GoalSteps??WalkingPersonalization.TargetFor(s);
                bool today=s.Status==ParticipationState.Selected || (s.WalkingGoalSteps>0 &&
                    TimeZoneInfo.ConvertTime(WalkingPersonalization.QueryStartFor(s),Service.CurrentTimeZone).Date==TimeZoneInfo.ConvertTime(Service.CurrentUtc,Service.CurrentTimeZone).Date);
                string scope=s.WalkingGoalSteps==0 && s.Status!=ParticipationState.Selected?"시작 후":today?"오늘 총":"시작한 날";
                var goalLabel=GardenUi.FlowLabel(p,scope+" "+goal.ToString("N0")+"걸음",GardenUi.HeadingSize);GardenTypography.SetWeight(goalLabel,true);
                var explanation=GardenUi.FlowLabel(p,target==null?WalkingPersonalization.Explanation(s):WalkingPersonalization.Explanation(target),GardenUi.BodySize,GardenUi.Muted);
                if(s.Status==ParticipationState.Selected){walkingIntroGoal=goalLabel;walkingIntroExplanation=explanation;walkingIntroSession=s.SessionId;}
                if(s.Status!=ParticipationState.Selected)
                {
                    GardenUi.FlowLabel(p,"실제 "+s.RealStepHighWater+"걸음"+(DebugWalkEnabled?" · 연습 "+s.VirtualSteps+"걸음":""));
                    GardenUi.FlowLabel(p,stepQueryStatus,GardenUi.CaptionSize,GardenUi.Muted);
                    var retry=GardenUi.Button(p,"걸음 다시 조회",0,0,1,0,()=>{RequestStepSyncSoon(true,true);TickStepSync();ShowSession();});
                    retry.gameObject.AddComponent<LayoutElement>().preferredHeight=88;
                }
            }
            else GardenUi.FlowLabel(p,(m?.suggestedDurationSeconds ?? 0)>0?"권장 "+m.suggestedDurationLabel:"시간은 편한 만큼",GardenUi.BodySize,GardenUi.Muted);
            if(s.Status==ParticipationState.Selected)
            {
                var start=DockAction("시작",()=>
                {
                    if(walking){StartSelectedWalking();return;}
                    if(Apply(Service.StartSession(sessionId)))
                    {RequestStepSyncSoon();ScheduleMissionReminder();}
                    ShowSession();
                },true);
                if(walking){walkingIntroStart=start;ApplyWalkingBaselinePreview(ReadWalkingHealth());}
                DockAction("선택 취소",()=>CancelSelectedPractice(s));
            }
            else if(s.Status==ParticipationState.Paused)
            {
                GardenUi.FlowLabel(p,"진행은 저장됐어요. 종료하면 완료 보상 없이 참여 기록만 남아요.",GardenUi.BodySize,GardenUi.Muted);
                DockAction("이어서 하기",()=>{if(Apply(Service.ResumeSession(sessionId)) && walking)ScheduleMissionReminder();ShowSession();},true);
                DockAction("오늘은 여기까지",()=>EndPractice(s));
            }
            else
            {
                DockAction("직접 완료 · 선택 기록",ShowReflection,true);
                DockAction("잠시 쉬기",()=>PausePractice(s));
            }
        }
        public void ShowReflection()
        {
            if(FocusMissionPolicy.IsFocus(ActiveSession())){ShowFocusEmotion();return;}
            EnterPage("reflection",ShowReflection);var p=FocusPage("지금 기분은 어떤가요?","마무리 · 선택 기록");
            GardenUi.FlowLabel(p,"잘 모르거나 기록하지 않아도 괜찮아요.");
            var faces=GardenUi.Box(p,"Reflection emotions",0,0,1,0);faces.AddComponent<LayoutElement>().preferredHeight=208;
            Button unknown=null;
            GardenUi.MoodPicker(faces.transform,0,0,1,1,mood,value=>{mood=value;if(unknown!=null)GardenUi.StyleButton(unknown);});
            unknown=GardenUi.Button(p,"잘 모르겠어요",0,0,1,0,()=>{mood=mood=="잘 모르겠어요"?null:"잘 모르겠어요";ShowReflection();});
            unknown.gameObject.AddComponent<LayoutElement>().preferredHeight=80;
            GardenUi.StyleButton(unknown,selected:mood=="잘 모르겠어요");
            note=GardenUi.FlowInput(p,"선택 메모 · 기기에만 저장");
            note.text=draftNote ?? ""; note.onValueChanged.AddListener(value=>draftNote=value);
            DockAction("이 기록으로 완료",()=>Finish(mood,note.text),true);
            DockAction("기록 건너뛰고 완료",()=>Finish(null,null));
        }
        public void Finish(string selectedMood,string selectedNote)
        {
            var finishing=ActiveSession();int nutrientBefore=Service.Snapshot.Nutrient;
            if(!Apply(Service.CompleteSession(sessionId,selectedMood,selectedNote,EligibleCycleAnimals(Service.Snapshot)))) {draftNote=selectedNote;ShowReflection();return;}
            if(finishing?.MissionId=="free-mission:M15")AndroidPlatformBridge.CancelMissionRewardReminder(sessionId);
            sessionId=null;
            ResetCompletedNavigation();
            ShowCompletion(Service.Snapshot.Nutrient!=nutrientBefore);
        }
        void ShowCompletion(bool rewarded)
        {
            EnterPage("complete",()=>ShowCompletion(rewarded));var p=FocusPage("활동을 마쳤어요","오늘의 작은 참여",true);
            Guidance(p,!rewarded?"복습을 기록했어요. 추가 보상은 없어요.":"활동을 기록하고 영양제 "+Balance.CompletionNutrient+"개를 받았어요.");
            GardenUi.FlowLabel(p,"오늘의 작은 참여가 정원에 남아요.");
            DockAction("정원으로",GoGarden,true);
            DockAction("영양제로 정원 가꾸기",ShowShop);
        }
        public void ShowShop()
        {
            if(!opened){ShowHome();return;}
            EnterPage("shop",ShowShop);ClearGhost();var p=Page("정원을 가꾸어요");
            GardenUi.Label(p,"미리보기와 취소에는 비용이 없어요.\n새 환경의 동물 후보는 다음 일일 회차에 반영돼요.",.07f,.64f,.86f,.14f,28);
            GardenUi.Button(p,"식물 성장 · "+Balance.GrowthCost+" 영양제",.07f,.52f,.86f,.085f,PreviewGrowth);
            GardenUi.Button(p,"꽃밭 미리보기 · "+Balance.EnvironmentCost+" 영양제",.07f,.40f,.86f,.085f,()=>PreviewEnvironment("environment:E01"));
            GardenUi.Button(p,"쉼터 미리보기 · "+Balance.EnvironmentCost+" 영양제",.07f,.28f,.86f,.085f,()=>PreviewEnvironment("environment:E02"));
            GardenUi.Button(p,"돌아가기",.07f,.16f,.86f,.075f,ShowHome);
        }
        public void PreviewEnvironment(string id)
        {
            ClearGhost();
            pendingEnvironment=id;pendingPurchase=Guid.NewGuid().ToString("N");EnterPage("placement",()=>PreviewEnvironment(id),id);
            var p=Page("배치 미리보기");
            GardenUi.Label(p,(MindfulnessContent.FindGardenContent(id)?.title ?? id)+"\n지정된 빈 자리 1곳에 배치합니다.\n비용 "+Balance.EnvironmentCost+" 영양제 · 아직 차감하지 않았어요",.07f,.65f,.86f,.14f,29);
            ThemePreview();
            var state=Service.Snapshot;string slot=id=="environment:E01"?"left":"right";
            bool occupied=state.Environments.Any(e=>e.EnvironmentId==id || e.Slot==slot);
            string unavailable=occupied?"이미 배치한 환경이에요. 정원에서 확인할 수 있어요.":
                state.Nutrient<Balance.EnvironmentCost?"영양제가 "+(Balance.EnvironmentCost-state.Nutrient)+"개 더 필요해요. 미리보기는 자유예요.":null;
            if(!occupied)state.Environments.Add(new EnvironmentPlacement{EnvironmentId=id,Slot=slot});
            if(!ShowIntegratedPurchasePreview(state))ghost=CreateEnvironment(id,true);
            GardenUi.Button(p,"취소",.07f,.23f,.41f,.08f,ShowShop);
            var confirm=GardenUi.Button(p,"확정",.52f,.23f,.41f,.08f,()=>
            {
                if(Apply(Service.PurchaseEnvironment(pendingPurchase,pendingEnvironment,slot))){ClearGhost();GoGarden();}
                else
                {
                    ShowIntegratedPurchasePreview(state);
                    notice.text="배치하지 못했어요. 영양제는 그대로예요. 다시 시도해 주세요.";
                }
            },unavailable==null);
            SetPurchaseAvailability(p,confirm,unavailable);
        }
        public void PreviewGrowth()
        {
            pendingGrowthRecordSession=null;growthLinkPage=0;RenderGrowthPreview();
        }
        void RenderGrowthPreview()
        {
            ClearGhost();
            EnterPage("growth",RenderGrowthPreview);pendingPurchase=Guid.NewGuid().ToString("N");
            var current=Service.Snapshot;
            var after=StateCodec.Clone(current);
            bool canGrow=GardenPlantVisuals.CanGrow(current);
            GardenPlantVisuals.PreviewGrowth(after,Balance.GrowthAmount);
            var p=FocusPage("식물 성장 미리보기",(mode=="LIVE"?"":"["+mode+"] ")+"영양제 "+current.Nutrient+"개");
            int beforeStage=GardenPlantVisuals.StageForGrowth(GardenPlantVisuals.TotalGrowth(current));
            int afterStage=GardenPlantVisuals.StageForGrowth(GardenPlantVisuals.TotalGrowth(after));
            string description=!canGrow?"이 식물은 성장을 마쳤어요. 정원에서 계속 함께할 수 있어요.":
                afterStage>beforeStage?new[]{"", "씨앗에서 새싹이 나와요.", "새싹에 봉오리가 맺혀요.", "봉오리에서 꽃이 피어나요."}[afterStage]:
                "꽃이 핀 식물이 조금 더 자라요.";
            GardenUi.FlowLabel(p,description);
            var compare=GardenUi.Box(p,"Growth comparison controls",0,0,1,0);
            compare.AddComponent<LayoutElement>().preferredHeight=88;
            Button beforeButton=null,afterButton=null;
            PurchasePreviewFrame? framing=null;
            void SelectPreview(bool future)
            {
                if(!ShowIntegratedPurchasePreview(future?after:current,framing) && rewardPlant!=null)
                    rewardPlant.localScale=basePlantScale*(1+Mathf.Clamp(GardenPlantVisuals.TotalGrowth(future?after:current),0,2));
                GardenUi.StyleButton(beforeButton,selected:!future);
                GardenUi.StyleButton(afterButton,selected:future);
            }
            beforeButton=GardenUi.Button(compare.transform,"현재",0,0,.485f,1,()=>SelectPreview(false));
            afterButton=GardenUi.Button(compare.transform,"성장 후",.515f,0,.485f,1,()=>SelectPreview(true));
            afterButton.interactable=canGrow;
            var home=transform.root.GetComponent<GardenHomePresenter>();
            // Measure both views once, then lock camera position/scale for the
            // comparison. Automatic reframing must not hide the growth itself.
            if(ShowIntegratedPurchasePreview(current))
            {
                var beforeFrame=new PurchasePreviewFrame(home.gardenCamera);
                ShowIntegratedPurchasePreview(after);
                var afterFrame=new PurchasePreviewFrame(home.gardenCamera);
                afterFrame.Size=Mathf.Max(afterFrame.Size,beforeFrame.Size+Vector3.Distance(beforeFrame.Position,afterFrame.Position));
                framing=afterFrame;
            }
            SelectPreview(canGrow);
            string unavailable=!canGrow?"성장을 마쳤어요":current.Nutrient<Balance.GrowthCost?
                "영양제가 "+(Balance.GrowthCost-current.Nutrient)+"개 더 필요해요. 미리보기는 자유예요.":null;
            if(canGrow) GardenUi.FlowLabel(p,unavailable??"확정할 때만 영양제를 사용해요.",GardenUi.BodySize,
                unavailable==null?GardenUi.Muted:GardenUi.Warning);
            if(canGrow)AddGrowthRecordChoice(p,current);
            var confirm=DockAction(!canGrow?"성장을 마쳤어요":"영양제 "+Balance.GrowthCost+"개로 성장",()=>
            {
                if(Apply(Service.GrowPlant(pendingPurchase,"plant:P06",pendingGrowthRecordSession)))
                {
                    GoGarden();
                    home?.ShowGrowthConfirmation(Balance.GrowthCost);
                }
                else
                {
                    SelectPreview(true);
                    notice.gameObject.SetActive(true);
                    notice.text="저장하지 못했어요. 영양제는 그대로예요. 다시 시도해 주세요.";
                }
            },true);
            confirm.name="확정";
            confirm.interactable=unavailable==null;
            if(unavailable!=null)
            {
                GardenUi.StyleButton(confirm);
                confirm.GetComponentInChildren<Text>().color=GardenUi.Muted;
            }
            DockAction("취소",()=>{UpdateGarden();ShowShop();});
        }
        void SetPurchaseAvailability(Transform parent,Button confirm,string unavailable)
        {
            confirm.interactable=unavailable==null;
            if(unavailable!=null)confirm.GetComponentInChildren<Text>().color=GardenUi.Muted;
            var explanation=GardenUi.Label(parent,unavailable??"확정할 때만 영양제가 차감돼요.",.07f,.10f,.86f,.10f,25);
            explanation.color=unavailable==null?GardenUi.Muted:GardenUi.Warning;
        }

        // Render a detached state snapshot through the same art/camera used at
        // home. Previewing never submits that snapshot to the state service.
        struct PurchasePreviewFrame
        {
            public Vector3 Position;
            public Quaternion Rotation;
            public float Size;
            public PurchasePreviewFrame(Camera camera)
            { Position=camera.transform.position;Rotation=camera.transform.rotation;Size=camera.orthographicSize; }
        }
        bool ShowIntegratedPurchasePreview(GardenState candidate,PurchasePreviewFrame? framing=null)
        {
            var home=transform.root.GetComponent<GardenHomePresenter>();
            if(home==null || home.gardenCamera==null || home.gardenCamera.targetTexture==null)return false;
            integratedPurchasePreview=true;
            home.gardenVisuals.SetActive(true);
            home.RefreshFromState(candidate);
            var orbit=home.homeCanvas.GetComponentInChildren<PrototypeIslandDrag>(true);
            orbit?.ResetView();
            float radius=0;Vector3 focus=orbit!=null?orbit.focus:home.gardenRoot.position;
            foreach(var filter in home.gardenRoot.GetComponentsInChildren<MeshFilter>())
                if(filter.sharedMesh!=null)foreach(var vertex in filter.sharedMesh.vertices)
                    radius=Mathf.Max(radius,(filter.transform.TransformPoint(vertex)-focus).magnitude);
            home.gardenCamera.ResetProjectionMatrix();
            home.gardenCamera.aspect=1.6f;
            home.gardenCamera.orthographicSize=Mathf.Max(.1f,radius*1.04f);
            if(screen=="growth" && home.ProceduralPlant!=null && home.Planter!=null)
                FrameGrowthSubject(home);
            if(framing.HasValue)
            {
                var fixedFrame=framing.Value;
                home.gardenCamera.transform.SetPositionAndRotation(fixedFrame.Position,fixedFrame.Rotation);
                home.gardenCamera.orthographicSize=fixedFrame.Size;
            }
            home.gardenVisuals.GetComponent<GardenSkyEnvironment>()?.UpdateClouds(Time.unscaledTime);
            if(SystemInfo.graphicsDeviceType!=UnityEngine.Rendering.GraphicsDeviceType.Null)
                home.gardenCamera.Render();
            var existing=view.transform.Find("Purchase garden viewport");
            var viewport=existing!=null?existing.gameObject:screen=="growth"
                ?GardenUi.Box(view.transform,"Purchase garden viewport",0,0,1,0)
                :GardenUi.Box(view.transform,"Purchase garden viewport",.07f,.345f,.86f,.275f);
            if(screen=="growth" && viewport.GetComponent<LayoutElement>()==null)
                viewport.AddComponent<LayoutElement>().preferredHeight=400;
            var existingImage=viewport.GetComponentInChildren<RawImage>();
            if(existingImage==null)
            {
                var picture=GardenUi.Box(viewport.transform,"Garden preview",0,0,1,1);
                existingImage=picture.AddComponent<RawImage>();existingImage.raycastTarget=false;
                var fit=picture.AddComponent<AspectRatioFitter>();fit.aspectMode=AspectRatioFitter.AspectMode.FitInParent;fit.aspectRatio=1.6f;
            }
            existingImage.texture=home.gardenCamera.targetTexture;
            var surface=view.GetComponent<Image>();if(surface!=null)surface.color=GardenUi.Background;
            return true;
        }
        static void FrameGrowthSubject(GardenHomePresenter home)
        {
            var camera=home.gardenCamera;
            var rotation=camera.transform.rotation;
            var inverse=Quaternion.Inverse(rotation);
            var bounds=new Bounds(inverse*home.ProceduralPlant.transform.position,Vector3.zero);
            // Measure in camera axes so both the planter and the whole plant fit
            // without making the surrounding island the subject of comparison.
            foreach(var root in new[]{home.ProceduralPlant.transform,home.Planter.transform})
                foreach(var filter in root.GetComponentsInChildren<MeshFilter>())
                    if(filter.sharedMesh!=null)foreach(var vertex in filter.sharedMesh.vertices)
                        bounds.Encapsulate(inverse*filter.transform.TransformPoint(vertex));
            camera.transform.position=rotation*bounds.center-camera.transform.forward*PrototypeIslandDrag.DefaultDistance;
            camera.orthographicSize=Mathf.Max(.25f,Mathf.Max(bounds.extents.y,bounds.extents.x/camera.aspect)*1.18f);
        }
        void ClearIntegratedPurchasePreview()
        {
            if(!integratedPurchasePreview)return;
            integratedPurchasePreview=false;
            var home=transform.root.GetComponent<GardenHomePresenter>();
            if(home==null)return;
            home.RefreshFromState();
            home.gardenVisuals.SetActive(home.IsGardenVisible);
        }
        void ThemePreview()
        {
            if(transform.root.GetComponent<GardenHomePresenter>()==null)
            {
                var background=GetComponent<Image>();if(background!=null)background.color=Color.clear;
            }
            var tint=GardenUi.Background;tint.a=.30f;view.GetComponent<Image>().color=tint;
            GardenUi.Card(view.transform,"Preview heading",.03f,.63f,.94f,.35f,GardenUi.Background).transform.SetAsFirstSibling();
            GardenUi.Card(view.transform,"Preview actions",.03f,.01f,.94f,.33f,GardenUi.Background).transform.SetAsFirstSibling();
        }
        void ShowLegacyRecords()
        {
            EnterPage("records",ShowRecords);var p=FocusPage("참여 기록","나의 활동");var s=Service.Snapshot;
            var all=s.Sessions.Where(x=>x.Status==ParticipationState.RewardCommitted || x.Status==ParticipationState.Ended).Reverse().ToArray();
            int pages=Math.Max(1,(all.Length+2)/3);recordsPage=Math.Min(recordsPage,pages-1);
            var sessions=all.Skip(recordsPage*3).Take(3).ToArray();
            var records=sessions.Length>0?ActivityList(p,"Participation history"):null;
            for(int i=0;i<sessions.Length;i++)
            {
                var entry=sessions[i];
                string detail=LocalTime(entry.EndedUtc)+" · "+StateLabel(entry.Status);
                ActivityListItem(records,MindfulnessContent.FindMission(entry.MissionId)?.title ?? entry.MissionId,detail,()=>ShowRecord(entry.SessionId),PrototypeUiIcon.Symbol.Journal);
            }
            if(sessions.Length==0) Guidance(p,"아직 참여 기록이 없어요. 활동을 마치면 이곳에서 볼 수 있어요.");
            if(pages>1)
            {
                DockPair("이전 기록",()=>{recordsPage--;ShowRecords();},"다음 기록",()=>{recordsPage++;ShowRecords();});
                var buttons=activityActions.GetComponentsInChildren<Button>();
                buttons.First(b=>b.name=="이전 기록").interactable=recordsPage>0;
                buttons.First(b=>b.name=="다음 기록").interactable=recordsPage<pages-1;
            }
            var destinations=ActivityList(p,"Record destinations");
            ActivityListItem(destinations,"경험 알아차리기",null,ShowExperienceTypes,PrototypeUiIcon.Symbol.Journal);
            ActivityListItem(destinations,"경험 기록과 나의 계획",null,ShowExperienceHistory,PrototypeUiIcon.Symbol.Journal);
            ActivityListItem(destinations,"식물 성장 기록",null,ShowGrowthHistory,PrototypeUiIcon.Symbol.Leaf);
            ActivityListItem(destinations,"이번 주와 지금까지",null,ShowRecordSummary,PrototypeUiIcon.Symbol.Journal);
            ActivityListItem(destinations,"발견한 동물",null,ShowDiscoveries,PrototypeUiIcon.Symbol.Leaf);
            DockAction("돌아가기",ShowHome);
        }
        void ShowLegacyRecord(string id)
        {
            EnterPage("record",()=>ShowRecord(id),id);var s=Service.Snapshot.Sessions.First(x=>x.SessionId==id);var p=Page("활동과 측정 기록");
            GardenUi.ScrollText(p,(MindfulnessContent.FindMission(s.MissionId)?.title ?? s.MissionId)+"\n"+LocalTime(s.StartedUtc)+" ~ "+LocalTime(s.EndedUtc)+"\n기분: "+(string.IsNullOrEmpty(s.Mood)?"기록 건너뜀":s.Mood)+"\n"+(s.Note??""),.07f,.57f,.86f,.23f);
            if(MbctPolicy.IsMbct(s.MissionId))GardenUi.Button(p,"실습에서 적은 내용",.07f,.075f,.54f,.055f,()=>ShowPracticeAnswers(id));
            BuildRecordHeart(p,s);
            GardenUi.Button(p,"기록 목록",.65f,.075f,.28f,.055f,ShowRecords);
        }
        public void ShowDiscoveries()
        {
            EnterPage("discoveries",ShowDiscoveries);var p=Page("정원에서 만난 친구들");
            var ids=Service.Snapshot.DiscoveredAnimalIds;
            GardenUi.Label(p,ids.Count==0?"아직 만난 동물이 없어요.\n아무도 방문하지 않는 날도 자연스러워요.":string.Join("\n",ids.Select(id=>MindfulnessContent.FindAnimal(id)?.title ?? id)),.07f,.29f,.86f,.48f,32);
            GardenUi.Button(p,"돌아가기",.07f,.15f,.86f,.08f,ShowRecords);
        }
        static string StateLabel(ParticipationState s) => s==ParticipationState.Selected?"선택됨":s==ParticipationState.InProgress?"참여 중":s==ParticipationState.Paused?"일시정지":s==ParticipationState.Ended?"참여 종료":"완료";
        static string LocalTime(string iso) => DateTimeOffset.TryParse(iso,out var time)?time.ToLocalTime().ToString("MM/dd HH:mm"):"시작 전";
        public bool LeaveActivityPanel()
        {
            if(!SaveActivePracticeDraft())return false;ClearGhost();UpdateGarden();
            if(screen=="placement" || screen=="growth" || screen=="growth-link")ShowShop();
            return true;
        }
        public void GoGarden(){ ClearGhost(); UpdateGarden(); transform.root.GetComponent<MockupNavigation>()?.ShowIsland(); }
        void ClearGhost(){ClearIntegratedPurchasePreview();if(ghost!=null){ghost.SetActive(false);if(Application.isPlaying)Destroy(ghost);else DestroyImmediate(ghost);ghost=null;}}
        void SetupIsland()
        {
            var nav=transform.root.GetComponent<MockupNavigation>(); if(nav==null)return;
            islandStatus=GardenUi.Label(nav.islandPanel.transform,"",.07f,.17f,.86f,.11f,27);
            GardenUi.Button(nav.islandPanel.transform,"정원 가꾸기",.07f,.30f,.40f,.055f,()=>{nav.ShowActivities();ShowShop();});
            GardenUi.Button(nav.islandPanel.transform,"방문 감상",.53f,.30f,.40f,.055f,()=>{if(islandStatus!=null)islandStatus.text=VisitorText()+"\n조용히 함께 쉬어요.";});
            UpdateGarden();
        }
        string VisitorText(GardenState state=null){if(!opened)return "저장 상태 확인 필요";var d=(state??Service.Snapshot).DailyDecisions.LastOrDefault();return string.IsNullOrEmpty(d?.VisitorId)?"오늘은 방문한 동물이 없어요":(MindfulnessContent.FindAnimal(d.VisitorId)?.title ?? d.VisitorId)+" · "+(MindfulnessContent.FindAnimal(d.VisitorId)?.tapReaction ?? "쉬고 있어요");}
        public void UpdateGarden()
        {
            if(!opened)return;RefreshMissionNavigation();var s=Service.Snapshot;
            if(stateSummary!=null)stateSummary.text=(mode=="LIVE"?"":"["+mode+"]  ")+CourseProgressLabel(s);
            var home=transform.root.GetComponent<GardenHomePresenter>();
            bool integratedHome=home!=null && home.gardenRoot!=null;
            // The integrated presenter owns the saved scenery. Legacy copies
            // at the scene root also reach its camera and escape plant focus.
            if(integratedHome)
            {
                if(gardenObjects!=null)
                {
                    gardenObjects.SetActive(false);
                    if(Application.isPlaying)Destroy(gardenObjects);else DestroyImmediate(gardenObjects);
                    gardenObjects=null;
                }
                gardenSignature=null;
            }
            home?.RefreshFromState(s);
            if(rewardPlant!=null) rewardPlant.localScale=basePlantScale*(1+Mathf.Clamp(s.LegacyGrowth+s.Plants.Sum(p=>p.Growth),0,2));
            if(islandStatus!=null)islandStatus.text=(mode=="LIVE"?"":"["+mode+"] ")+"영양제 "+s.Nutrient+"\n"+VisitorText(s);
            if(integratedHome)return;
            var signature=string.Join("|",s.Environments.Select(e=>e.EnvironmentId+":"+e.Slot))+"/"+s.DailyDecisions.LastOrDefault()?.VisitorId;
            if(gardenObjects!=null && signature==gardenSignature)return;gardenSignature=signature;
            if(gardenObjects!=null){gardenObjects.SetActive(false);if(Application.isPlaying)Destroy(gardenObjects);else DestroyImmediate(gardenObjects);}
            gardenObjects=new GameObject("Saved garden additions");
            foreach(var e in s.Environments)CreateEnvironment(e.EnvironmentId,false).transform.SetParent(gardenObjects.transform,true);
            var visitor=s.DailyDecisions.LastOrDefault()?.VisitorId;
            if(!string.IsNullOrEmpty(visitor))CreateVisitor(visitor).transform.SetParent(gardenObjects.transform,true);
        }
        static GameObject CreateEnvironment(string id,bool isGhost)
        {
            var go=GardenSceneModels.CreateEnvironment(id);
            if(isGhost)go.name="PREVIEW "+id;
            return go;
        }
        static GameObject CreateVisitor(string id) => GardenSceneModels.CreateVisitor(id);
        public string CurrentScreen => screen;

        public bool DebugWalkEnabled => opened && Service.Snapshot.DebugWalkPeriods.Any(p=>string.IsNullOrEmpty(p.EndedUtc));
        public string DebugWalkSummary
        {
            get
            {
                if(!opened)return "걸음 보상: 저장 상태를 확인해 주세요";
                var p=Service.Snapshot.DebugWalkPeriods.LastOrDefault(x=>string.IsNullOrEmpty(x.EndedUtc));
                if(p==null)return "디버그 걸음 보상 OFF · 일반 걷기는 개인별 목표를 사용해요.";
                return "디버그 걸음 보상 ON · 실제 "+p.RealStepHighWater+" + 가상 "+p.VirtualSteps+
                    " = "+(p.RealStepHighWater+p.VirtualSteps)+"걸음\n10걸음당 영양제 1개 · 지급 "+p.RewardedUnits+
                    "개 · 다음 보상 "+((p.RealStepHighWater+p.VirtualSteps)%10)+"/10\n"+stepQueryStatus;
            }
        }
        public bool ToggleDebugWalk()
        {
            if(!opened)return false;
            bool wasEnabled=DebugWalkEnabled;
            if(!Apply(wasEnabled?Service.DisableDebugWalk():Service.EnableDebugWalk()))return false;
            RequestStepSyncSoon(wasEnabled,wasEnabled);
            ReconcileMissionReminder();
            MaybeNotifyMissionReady();
            if(screen=="session")ShowSession();
            return true;
        }
        public bool AddVirtualDebugStep()
        {
            if(!opened || !DebugWalkEnabled)return false;
            if(!Apply(Service.AddVirtualStep()))return false;
            ScheduleMissionReminder();
            MaybeNotifyMissionReady();
            if(screen=="session")ShowSession();
            return true;
        }
        public void RequestStepSyncSoon(bool includeClosedPeriod=false,bool newestClosedFirst=false)
        {
            nextStepSync=0;
            stepQueries.Clear();
            if(includeClosedPeriod)forceClosedStepSync=true;
            if(newestClosedFirst)preferNewestClosedStepPeriod=true;
        }
        public void TickStepSync()
        {
            if(!opened || mode!="LIVE" || Application.platform!=RuntimePlatform.Android)return;
            if(runningStepQuery!=null && Time.unscaledTime-runningStepQuery.StartedAt>45f)
            {
                // The native query has its own shorter bound. Recover if the bridge lost
                // its result; a stale response cannot match a new request/window.
                runningStepQuery=null;stepQueries.Clear();nextStepSync=Time.unscaledTime+5f;
                stepQueryStatus="걸음 조회가 늦어지고 있어요. 잠시 후 다시 확인해요.";
                RefreshWalkingSession();
                return;
            }
            if(runningStepQuery!=null)
            {
                if(Time.unscaledTime<nextStepPoll)return;
                nextStepPoll=Time.unscaledTime+0.75f;
                var result=AndroidPlatformBridge.GetStepRangeSnapshot();
                if(result!=null && result.requestId==runningStepQuery.Id && !result.refreshing)
                {
                    if(result.queryComplete && result.hasValue && result.status=="AVAILABLE")
                    {
                        bool applied=true;
                        if(runningStepQuery.PeriodId!=null)applied=Apply(Service.ObserveDebugWalk(runningStepQuery.PeriodId,result.count,result.source));
                        if(runningStepQuery.SessionId!=null)applied=ApplyQueriedMissionSteps(runningStepQuery.SessionId,runningStepQuery.Id,runningStepQuery.StartMs,runningStepQuery.EndMs,runningStepQuery.Aggregate,result);
                        if(applied)
                        {
                            stepQueryStatus="실제 걸음 갱신됨 · "+result.sourceLabel+" · "+DateTime.Now.ToString("HH:mm");
                            MaybeNotifyMissionReady();
                        }
                        else {stepQueryStatus="걸음 갱신을 다시 확인하고 있어요.";nextStepSync=0;}
                    }
                    else stepQueryStatus=(result.status=="NO_DATA"?"이 구간의 걸음 기록 없음 · 동기화 대기 가능":result.status+" · "+result.message)+" · "+DateTime.Now.ToString("HH:mm");
                    runningStepQuery=null;
                    RefreshWalkingSession();
                }
            }
            if(runningStepQuery!=null)return;
            if(stepQueries.Count==0 && Time.unscaledTime>=nextStepSync)
            {
                QueueStepQueries();
                nextStepSync=Time.unscaledTime+60f;
            }
            if(stepQueries.Count==0)return;
            var query=stepQueries.Dequeue();
            stepQueryStatus="걸음 구간 조회 중 · "+DateTime.Now.ToString("HH:mm");
            nextStepPoll=Time.unscaledTime+0.75f;
            if(query.IsClosedPeriod)
            {
                lastClosedStepQueryPeriodId=query.PeriodId;
                PlayerPrefs.SetString(ClosedStepQueryCursorKey,lastClosedStepQueryPeriodId);
                PlayerPrefs.Save();
                nextClosedStepSync=Time.unscaledTime+300f;
                forceClosedStepSync=false;
                preferNewestClosedStepPeriod=false;
            }
            var action=AndroidPlatformBridge.RefreshStepsRange(query.StartMs,query.EndMs,query.Id,query.Aggregate);
            if(action.ParsedStatus==PlatformStatus.Available){query.StartedAt=Time.unscaledTime;runningStepQuery=query;}
            else {stepQueryStatus=action.status+" · "+action.message+" · "+DateTime.Now.ToString("HH:mm");nextStepSync=Time.unscaledTime+5f;RefreshWalkingSession();}
        }
        void RefreshWalkingSession()
        {
            // A background step query must never rebuild an unrelated practice input.
            if(screen=="session" && ActiveSession()?.MissionId=="free-mission:M15")ShowSession();
        }
        void QueueStepQueries()
        {
            var state=Service.Snapshot;
            long now=DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            var activePeriod=state.DebugWalkPeriods.LastOrDefault(x=>string.IsNullOrEmpty(x.EndedUtc));
            if(activePeriod!=null)
            {
                long start=DateTimeOffset.Parse(activePeriod.StartedUtc).ToUnixTimeMilliseconds();
                if(now>start)stepQueries.Enqueue(new StepQuery{Id=Guid.NewGuid().ToString("N"),PeriodId=activePeriod.Id,StartMs=start,EndMs=now});
            }
            var mission=state.Sessions.LastOrDefault(s=>s.MissionId=="free-mission:M15" &&
                (s.Status==ParticipationState.InProgress || s.Status==ParticipationState.Paused));
            if(mission!=null && DateTimeOffset.TryParse(DebugWalkEnabled?mission.StartedUtc:WalkingPersonalization.QueryStartFor(mission).ToString("o"),out var startAt) && now>startAt.ToUnixTimeMilliseconds())
                stepQueries.Enqueue(new StepQuery{Id=Guid.NewGuid().ToString("N"),SessionId=mission.SessionId,
                    StartMs=startAt.ToUnixTimeMilliseconds(),EndMs=DebugWalkEnabled?now:WalkingPersonalization.QueryEndFor(mission,DateTimeOffset.UtcNow).ToUnixTimeMilliseconds(),
                    Aggregate=!DebugWalkEnabled && mission.WalkingGoalSteps>0});

            // Closed windows remain eligible indefinitely so late Health Connect
            // sync can still be observed. Refresh at most one every five minutes,
            // except an app return, explicit refresh, or debug-mode stop.
            if(!forceClosedStepSync && Time.unscaledTime<nextClosedStepSync)return;
            var closedPeriods=state.DebugWalkPeriods.Where(x=>!string.IsNullOrEmpty(x.EndedUtc)).ToArray();
            if(closedPeriods.Length==0)return;
            int lastIndex=Array.FindIndex(closedPeriods,x=>x.Id==lastClosedStepQueryPeriodId);
            int nextIndex=preferNewestClosedStepPeriod || lastIndex<0
                ? closedPeriods.Length-1
                : (lastIndex+1)%closedPeriods.Length;
            var closed=closedPeriods[nextIndex];

            long closedStart=DateTimeOffset.Parse(closed.StartedUtc).ToUnixTimeMilliseconds();
            long closedEnd=DateTimeOffset.Parse(closed.EndedUtc).ToUnixTimeMilliseconds();
            if(closedEnd>closedStart)
                stepQueries.Enqueue(new StepQuery{Id=Guid.NewGuid().ToString("N"),PeriodId=closed.Id,StartMs=closedStart,EndMs=closedEnd,IsClosedPeriod=true});
            else
            {
                // Skip an empty interval without letting it pin the round-robin cursor.
                lastClosedStepQueryPeriodId=closed.Id;
                PlayerPrefs.SetString(ClosedStepQueryCursorKey,lastClosedStepQueryPeriodId);
                PlayerPrefs.Save();
                nextClosedStepSync=Time.unscaledTime+300f;
                forceClosedStepSync=false;
                preferNewestClosedStepPeriod=false;
            }
        }
        void MaybeNotifyMissionReady()
        {
            if(!opened)return;
            var mission=Service.Snapshot.Sessions.LastOrDefault(s=>s.MissionId=="free-mission:M15" &&
                (s.Status==ParticipationState.InProgress || s.Status==ParticipationState.Paused));
            if(mission==null)return;
            if(!DebugWalkEnabled && !WalkingEvidence.CanAutomaticallyComplete(mission,false))return;
            int goal=DebugWalkEnabled?10:WalkingPersonalization.TargetFor(mission);
            if(mission.RealStepHighWater+(DebugWalkEnabled?mission.VirtualSteps:0)<goal)return;
            if(!mission.GoalNotificationCommitted)
            {
                ScheduleMissionReminder();
                var action=AndroidPlatformBridge.NotifyMissionRewardReady(mission.SessionId,mission.RealStepHighWater);
                if(action.ParsedStatus==PlatformStatus.Available)Apply(Service.MarkMissionGoalNotified(mission.SessionId));
            }
            if(!DebugWalkEnabled)CompleteWalkingGoal(mission.SessionId);
        }
        void ScheduleMissionReminder()
        {
            if(!opened || mode!="LIVE")return;
            var mission=Service.Snapshot.Sessions.LastOrDefault(s=>s.MissionId=="free-mission:M15" &&
                (s.Status==ParticipationState.InProgress || s.Status==ParticipationState.Paused));
            if(mission==null || !DateTimeOffset.TryParse(DebugWalkEnabled?mission.StartedUtc:WalkingPersonalization.QueryStartFor(mission).ToString("o"),out var started))return;
            long end=!DebugWalkEnabled && DateTimeOffset.TryParse(mission.WalkingQueryEndUtc,out var until)?until.ToUnixTimeMilliseconds():long.MaxValue;
            AndroidPlatformBridge.ScheduleMissionRewardReminder(mission.SessionId,started.ToUnixTimeMilliseconds(),DebugWalkEnabled?10:WalkingPersonalization.TargetFor(mission),
                DebugWalkEnabled?mission.VirtualSteps:0,end,!DebugWalkEnabled && mission.WalkingGoalSteps>0);
        }
        void ReconcileMissionReminder()
        {
            if(!opened || mode!="LIVE")return;
            ScheduleMissionReminder();
            foreach(var closed in Service.Snapshot.Sessions.Where(s=>s.MissionId=="free-mission:M15" &&
                (s.Status==ParticipationState.Ended || s.Status==ParticipationState.RewardCommitted)))
                if(closed.Status==ParticipationState.RewardCommitted)AndroidPlatformBridge.AcknowledgeMissionAchievement(closed.SessionId);
                else AndroidPlatformBridge.CancelMissionRewardReminder(closed.SessionId);
        }
        public void OnAppReturned()
        {
            if(!opened)return;
            nextNotificationPoll=0;
            RefreshCycle();
            RequestStepSyncSoon(true);
            RequestWalkingBaselineRefresh();
            ReconcileMissionReminder();
        }
    }
    public sealed class GardenMaterialCleanup : MonoBehaviour
    {
        void OnDestroy(){var r=GetComponent<Renderer>();if(r!=null && r.sharedMaterial!=null){if(Application.isPlaying)Destroy(r.sharedMaterial);else DestroyImmediate(r.sharedMaterial);}}
    }
}
