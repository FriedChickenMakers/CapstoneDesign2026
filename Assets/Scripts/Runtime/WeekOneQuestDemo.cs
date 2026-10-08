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
        bool integratedPurchasePreview;
        float nextCycleCheck;
        int freeIndex, coursePage, recordsPage;
        string mode;
        string lastClosedStepQueryPeriodId;
        sealed class StepQuery
        {
            public string Id, PeriodId, SessionId;
            public long StartMs, EndMs;
            public bool IsClosedPeriod;
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
            foreach (var text in transform.root.GetComponentsInChildren<Text>(true)) text.font = GardenUi.Font;
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
            if(opened && Time.unscaledTime >= nextCycleCheck) { nextCycleCheck=Time.unscaledTime+30; RefreshCycle(); }
        }
        void OnApplicationFocus(bool focus) { if(!opened)return; if(focus)OnAppReturned();else SaveActivePracticeDraft(); }
        void OnApplicationPause(bool paused) { if(paused && opened)SaveActivePracticeDraft(); }
        void OnDestroy() { Service?.Dispose(); if(gardenObjects!=null) { if(Application.isPlaying) Destroy(gardenObjects); else DestroyImmediate(gardenObjects); } }
        public bool RefreshCycle()
        {
            if(!opened) return false;
            var state=Service.Snapshot;
            var candidates=state.Environments.SelectMany(e=>MindfulnessContent.FindGardenContent(e.EnvironmentId)?.candidateAnimalIds ?? Array.Empty<string>()).Where(id=>id=="animal:A07").Distinct();
            if(!Service.RefreshCycle(candidates))
            {
                error=Service.LastError;
                if(notice!=null)notice.text="일일 상태 저장 실패 · 기존 진행을 보존했어요\n"+error;
                return false;
            }
            if(state.LastCycleId!=Service.Snapshot.LastCycleId)UpdateGarden();
            return true;
        }
        bool Apply(bool success)
        {
            error=success ? null : Service.LastError;
            if(success) UpdateGarden();
            else if(notice!=null)notice.text="저장하지 못했어요. 입력을 유지했으니 다시 시도해 주세요.";
            return success;
        }
        Transform Page(string title,GardenState state=null)
        {
            ClearIntegratedPurchasePreview();
            var background=GetComponent<Image>();if(background!=null)background.color=GardenUi.Background;
            if(pageViewport!=null) { pageViewport.SetActive(false); if(Application.isPlaying) Destroy(pageViewport); else DestroyImmediate(pageViewport); }
            var content=GardenUi.ScrollPage(transform,"Local garden flow",.105f,1,
                screen=="course"?1840:screen=="home"?1200:1600);
            pageViewport=content.parent.gameObject;view=content.gameObject;
            view.AddComponent<Image>().color=GardenUi.Background;
            var brand=GardenUi.Label(content,"마음 정원",.07f,.962f,.86f,.03f,30);
            brand.fontStyle=FontStyle.Bold;brand.color=GardenUi.Green;PlacePageHeader(brand,24,54);
            var heading=GardenUi.Label(content,title,.07f,.87f,.86f,.09f,GardenUi.TitleSize);heading.fontStyle=FontStyle.Bold;
            bool longTitle=heading.preferredWidth>619;
            PlacePageHeader(heading,118,longTitle?138:78);
            if(longTitle)((RectTransform)content).sizeDelta+=new Vector2(0,240);
            var s=opened ? state??Service.Snapshot : null;
            stateSummary=GardenUi.Label(content,(mode=="LIVE"?"":"["+mode+"]  ")+ (s==null ? "저장 상태를 확인해 주세요" : "영양제 "+s.Nutrient+"  ·  코스 "+Math.Min(48,s.MbctCompletedIds.Count)+" / 48"),.07f,.81f,.86f,.05f,GardenUi.BodySize);
            stateSummary.color=GardenUi.Muted;
            PlacePageHeader(stateSummary,longTitle?266:206,60);
            if(screen!="placement" && screen!="growth")
            {
                var progress=GardenUi.Box(content,"Course progress",.07f,0,.86f,0);
                GardenUi.FromTop(progress.transform,longTitle?342:282,4);
                GardenUi.Progress(progress.transform,s==null?0:s.MbctCompletedIds.Count/48f,0,0,1,1);
            }
            notice=GardenUi.Label(content,string.IsNullOrEmpty(error)?"":"저장하지 못했어요. 잠시 후 다시 시도해 주세요.",.07f,.01f,.86f,.05f,GardenUi.CaptionSize);
            if(screen=="home")GardenUi.FromTop(notice.transform,1100,80);
            notice.color=GardenUi.Warning;
            return content;
        }
        static void PlacePageHeader(Text label,float top,float height)
        {
            var rect=label.rectTransform;
            rect.anchorMin=new Vector2(.07f,1);rect.anchorMax=new Vector2(.93f,1);
            rect.offsetMin=new Vector2(0,-top-height);rect.offsetMax=new Vector2(0,-top);
        }
        public void ShowHome()
        {
            if(opened && !SaveActivePracticeDraft())return;ClearPracticeDraft();
            screen="home";var s=opened?Service.Snapshot:null;var p=Page("오늘의 활동",s);
            if(!opened) { GardenUi.Label(p,"저장본을 보존했습니다. 새 계정으로 초기화하지 않습니다.\n앱을 닫고 저장 공간과 복구 안내를 확인해 주세요.",.07f,.48f,.86f,.25f); return; }
            var mission=MbctContent.Course.FirstOrDefault(m=>m.recommendedOrder==s.MbctNextOrder);
            GardenUi.FromTop(GardenUi.Card(p,"Recommended activity",.07f,0,.86f,0,GardenUi.Pale).transform,314,184);
            var week=GardenUi.Label(p,mission==null?"차곡차곡 쌓인 나의 실천":"이번 추천 · "+mission.week+"주차",.10f,0,.80f,0,GardenUi.CaptionSize);
            week.color=GardenUi.Muted;GardenUi.FromTop(week.transform,338,40);
            var recommendation=GardenUi.Label(p,mission==null?"8주 활동을 마쳤어요":mission.title,.10f,0,.80f,0,GardenUi.HeadingSize);
            GardenUi.FromTop(recommendation.transform,384,104);
            recommendation.fontStyle=FontStyle.Bold;
            if(!string.IsNullOrEmpty(sessionId)) GardenUi.FromTop(GardenUi.Button(p,"진행하던 활동으로",.07f,0,.86f,0,ShowSession,true).transform,522,88);
            else if(mission!=null && MbctPolicy.Availability(s)==null) GardenUi.FromTop(GardenUi.Button(p,"추천 활동 안내",.07f,0,.86f,0,()=>SelectMission(mission),true).transform,522,88);
            if(string.IsNullOrEmpty(sessionId) && MbctPolicy.Availability(s)!=null) GardenUi.FromTop(GardenUi.Label(p,MbctPolicy.Availability(s),.07f,0,.86f,0,26).transform,522,104);
            GardenUi.FromTop(GardenUi.MenuButton(p,"8주 코스 둘러보기",null,.07f,0,.86f,0,ShowCourse,PrototypeUiIcon.Symbol.Journal).transform,650,88);
            GardenUi.FromTop(GardenUi.MenuButton(p,"배운 활동 다시 하기",null,.07f,0,.86f,0,ShowFree,PrototypeUiIcon.Symbol.Leaf).transform,754,88);
            GardenUi.FromTop(GardenUi.MenuButton(p,"정원 가꾸기",null,.07f,0,.86f,0,ShowShop,PrototypeUiIcon.Symbol.Leaf).transform,858,88);
            GardenUi.FromTop(GardenUi.MenuButton(p,"참여 기록과 나의 계획",null,.07f,0,.86f,0,ShowRecords,PrototypeUiIcon.Symbol.Journal).transform,962,88);
        }
        public void ShowCourse()
        {
            var timer=System.Diagnostics.Stopwatch.StartNew();long reads=Service.SnapshotReads;
            var state=Service.Snapshot;string availability=MbctPolicy.Availability(state);
            screen="course";var p=Page("8주 마음챙김 활동",state);
            GardenUi.FromTop(GardenUi.Card(p,"Week focus",.07f,0,.86f,0,GardenUi.Pale).transform,314,80);
            var week=GardenUi.Label(p,(coursePage+1)+"주 · "+MbctContent.WeekTitles[coursePage],.10f,0,.80f,0,28);week.fontStyle=FontStyle.Bold;GardenUi.FromTop(week.transform,314,80);
            for(int i=0;i<6;i++)
            {
                var m=MbctContent.Course[coursePage*6+i];bool learned=m.recommendedOrder<state.MbctNextOrder;
                bool current=m.recommendedOrder==state.MbctNextOrder;
                string detail=learned?"배운 활동 · 복습 가능":current?(availability==null?"이번 추천 활동":"다음 참여에서 이어가기"):"앞 활동을 배우면 열려요";
                var b=GardenUi.MenuButton(p,(i+1)+"일차  "+m.title,detail,.07f,.65f-i*.069f,.86f,.062f,()=>SelectMission(m),
                    learned?PrototypeUiIcon.Symbol.Check:current?PrototypeUiIcon.Symbol.Arrow:PrototypeUiIcon.Symbol.Lock,current);
                GardenUi.FromTop(b.transform,418+i*196,180);
                b.interactable=learned || (current && availability==null);
                if(!b.interactable){foreach(var label in b.GetComponentsInChildren<Text>())label.color=GardenUi.Muted;foreach(var icon in b.GetComponentsInChildren<PrototypeUiIcon>())icon.color=GardenUi.Muted;}
            }
            var previous=GardenUi.Button(p,"이전 주",.07f,.18f,.26f,.06f,()=>{coursePage--;ShowCourse();});previous.interactable=coursePage>0;
            var next=GardenUi.Button(p,"다음 주",.37f,.18f,.26f,.06f,()=>{coursePage++;ShowCourse();});next.interactable=coursePage<7;
            var back=GardenUi.Button(p,"돌아가기",.67f,.18f,.26f,.06f,ShowHome);
            GardenUi.FromTop(previous.transform,1620,88);GardenUi.FromTop(next.transform,1620,88);GardenUi.FromTop(back.transform,1620,88);
            LastCourseBuildMs=timer.Elapsed.TotalMilliseconds;TraceScreen("course",LastCourseBuildMs,Service.SnapshotReads-reads);
        }
        public void ShowFree()
        {
            var timer=System.Diagnostics.Stopwatch.StartNew();long reads=Service.SnapshotReads;
            var state=Service.Snapshot;screen="free";var p=Page("배운 활동 다시 하기",state);
            var learned=MbctContent.Course.Where(m=>m.recommendedOrder<state.MbctNextOrder).ToArray();
            if(learned.Length==0)
            {
                GardenUi.Icon(p,PrototypeUiIcon.Symbol.Leaf,.41f,.59f,.18f,.14f);
                GardenUi.Label(p,"추천 활동을 해본 뒤 다시 연습할 수 있어요.",.07f,.45f,.86f,.12f,28);
            }
            else
            {
                freeIndex=Math.Min(freeIndex,learned.Length-1);var m=learned[freeIndex];
                GardenUi.Card(p,"Practice choice",.07f,.43f,.86f,.34f,GardenUi.Pale);
                GardenUi.Icon(p,PrototypeUiIcon.Symbol.Leaf,.10f,.66f,.07f,.06f);
                GardenUi.Label(p,(freeIndex+1)+" / "+learned.Length+" · 배운 활동",.20f,.68f,.67f,.04f,22).color=GardenUi.Muted;
                GardenUi.Label(p,m.title,.10f,.58f,.80f,.085f,31).fontStyle=FontStyle.Bold;
                GardenUi.Label(p,m.purpose,.10f,.475f,.80f,.09f,25);
                GardenUi.Label(p,"복습에는 추가 보상이 없어요.",.10f,.44f,.80f,.035f,21).color=GardenUi.Muted;
                GardenUi.Button(p,"이 활동 안내",.07f,.33f,.86f,.075f,()=>SelectMission(m),true);
                GardenUi.Button(p,"다른 활동",.07f,.24f,.86f,.065f,()=>{freeIndex=(freeIndex+1)%learned.Length;ShowFree();});
            }
            GardenUi.Button(p,"돌아가기",.07f,.15f,.86f,.065f,ShowHome);
            GardenUi.Button(p,"걸음 기록과 걷기",.07f,.075f,.86f,.055f,()=>SelectMission(MindfulnessContent.FindMission("free-mission:M15")));
            LastFreeBuildMs=timer.Elapsed.TotalMilliseconds;TraceScreen("free",LastFreeBuildMs,Service.SnapshotReads-reads);
        }
        static void TraceScreen(string name,double milliseconds,long snapshots)
        {
            if(Debug.isDebugBuild)Debug.Log("MBCT_UI "+name+" buildMs="+milliseconds.ToString("F2",System.Globalization.CultureInfo.InvariantCulture)+" snapshots="+snapshots);
        }
        public void SelectMission(MissionDefinition mission)
        {
            if(!opened){ShowHome();return;}
            if(!RefreshCycle()){ShowHome();return;}
            // One active session survives navigation and app restart. Selecting elsewhere cannot clone it.
            var active=Service.Snapshot.Sessions.LastOrDefault(s=>s.Status==ParticipationState.Selected || s.Status==ParticipationState.InProgress || s.Status==ParticipationState.Paused);
            if(active!=null) { sessionId=active.SessionId; ShowSession(); return; }
            var id=Guid.NewGuid().ToString("N");
            if(Apply(Service.BeginSession(id,mission.id,mission.type=="course"?mission.recommendedOrder:0))) {sessionId=id; mood=null;draftNote=null;}
            ShowSession();
        }
        ActivitySession ActiveSession() => Service.Snapshot.Sessions.FirstOrDefault(s=>s.SessionId==sessionId);
        public void ShowSession()
        {
            bool enteringSession=screen!="session";
            screen="session"; var s=ActiveSession(); if(s==null){ShowHome();return;}
            var m=MindfulnessContent.FindMission(s.MissionId); var p=Page(m?.title ?? s.MissionId);
            if(m?.mbctActivity>0){ShowMbctSession(s,m,p);return;}
            bool walking=s.MissionId=="free-mission:M15";
            if(walking && enteringSession)RequestStepSyncSoon();
            GardenUi.Label(p,walking ? "미션 시작 후 걸어 보세요. 동기화가 늦어도 직접 완료할 수 있어요."
                : (m?.textOnlyInstructions ?? "편안한 만큼 참여해 주세요.")+"\n\n"+((m?.suggestedDurationSeconds ?? 0)>0 ? "권장 "+m.suggestedDurationLabel : "시간은 편한 만큼")+" · 직접 완료할 수 있어요",.07f,.56f,.86f,.23f,30);
            string progress=walking ? "\n목표 "+(DebugWalkEnabled?10:300)+"걸음 · 실제 "+s.RealStepHighWater+" + 가상 "+(DebugWalkEnabled?s.VirtualSteps:0)+"\n"+stepQueryStatus : "";
            GardenUi.Label(p,(walking?"걷기 미션": "화면 안내를 따라 해보세요")+"\n"+StateLabel(s.Status)+progress,.07f,.41f,.86f,.15f,23);
            if(s.Status==ParticipationState.Selected) GardenUi.Button(p,"시작",.07f,.32f,.86f,.075f,()=>{if(Apply(Service.StartSession(sessionId))){RequestStepSyncSoon();ScheduleMissionReminder();}ShowSession();},true);
            if(s.Status==ParticipationState.InProgress || s.Status==ParticipationState.Paused)
            {
                GardenUi.Button(p,s.Status==ParticipationState.Paused?"이어서 하기":"일시정지",.07f,.32f,.86f,.075f,()=>{if(Apply(s.Status==ParticipationState.Paused?Service.ResumeSession(sessionId):Service.PauseSession(sessionId)) && walking)ScheduleMissionReminder();ShowSession();});
                GardenUi.Button(p,"직접 완료 · 선택 기록",.07f,.235f,.86f,.075f,ShowReflection,true);
            }
            bool canRetrySteps=walking && (s.Status==ParticipationState.InProgress || s.Status==ParticipationState.Paused);
            if(canRetrySteps)
                GardenUi.Button(p,"걸음 다시 조회",.07f,.14f,.41f,.075f,()=>{RequestStepSyncSoon(true,true);TickStepSync();ShowSession();});
            GardenUi.Button(p,"오늘은 여기까지",canRetrySteps ? .52f : .07f,.14f,walking && !canRetrySteps ? .86f : .41f,.075f,()=>{if(Apply(Service.EndParticipation(sessionId))){if(walking)AndroidPlatformBridge.CancelMissionRewardReminder(sessionId);sessionId=null;}ShowHome();});
            if(!walking)GardenUi.Button(p,"목록으로",.52f,.15f,.41f,.075f,ShowHome);
        }
        public void ShowReflection()
        {
            screen="reflection";var p=Page("돌아보기는 선택이에요");
            GardenUi.Label(p,"지금 기분은 어떤가요?\n잘 모르거나 기록하지 않아도 괜찮아요.",.07f,.68f,.86f,.10f,28);
            Button unknown=null;
            GardenUi.MoodPicker(p,.07f,.465f,.86f,.18f,mood,value=>{mood=value;notice.text=value==null?"감정 선택은 자유예요":"선택: "+value;if(unknown!=null)GardenUi.StyleButton(unknown);});
            unknown=GardenUi.Button(p,"잘 모르겠어요",.07f,.39f,.86f,.055f,()=>{mood=mood=="잘 모르겠어요"?null:"잘 모르겠어요";ShowReflection();});
            GardenUi.StyleButton(unknown,selected:mood=="잘 모르겠어요");
            note=GardenUi.Input(p,"선택 메모 · 기기에만 저장",.07f,.245f,.86f,.125f);
            note.text=draftNote ?? ""; note.onValueChanged.AddListener(value=>draftNote=value);
            GardenUi.Button(p,"이 기록으로 완료",.07f,.145f,.86f,.075f,()=>Finish(mood,note.text),true);
            GardenUi.Button(p,"기록 건너뛰고 완료",.07f,.07f,.86f,.055f,()=>Finish(null,null));
        }
        public void Finish(string selectedMood,string selectedNote)
        {
            var finishing=ActiveSession();int nutrientBefore=Service.Snapshot.Nutrient;
            if(!Apply(Service.CompleteSession(sessionId,selectedMood,selectedNote))) {draftNote=selectedNote;ShowReflection();return;}
            if(finishing?.MissionId=="free-mission:M15")AndroidPlatformBridge.CancelMissionRewardReminder(sessionId);
            screen="complete"; var p=Page("활동을 마쳤어요");
            GardenUi.Label(p,(Service.Snapshot.Nutrient==nutrientBefore?"복습을 기록했어요. 추가 보상은 없어요.":"활동을 기록했어요. 새 임무 완료 보상은 영양제 "+Balance.CompletionNutrient+"개예요.")+"\n\n오늘의 작은 참여가 정원에 남아요.",.07f,.45f,.86f,.30f,30);
            sessionId=null;
            GardenUi.Button(p,"영양제로 정원 가꾸기",.07f,.32f,.86f,.08f,ShowShop);
            GardenUi.Button(p,"정원으로",.07f,.21f,.86f,.08f,GoGarden,true);
        }
        public void ShowShop()
        {
            if(!opened){ShowHome();return;}
            screen="shop";ClearGhost();var p=Page("정원을 가꾸어요");
            GardenUi.Label(p,"미리보기와 취소에는 비용이 없어요.\n새 환경의 동물 후보는 다음 일일 회차에 반영돼요.",.07f,.64f,.86f,.14f,28);
            GardenUi.Button(p,"캐모마일 성장 · "+Balance.GrowthCost+" 영양제",.07f,.52f,.86f,.085f,PreviewGrowth);
            GardenUi.Button(p,"꽃밭 미리보기 · "+Balance.EnvironmentCost+" 영양제",.07f,.40f,.86f,.085f,()=>PreviewEnvironment("environment:E01"));
            GardenUi.Button(p,"쉼터 미리보기 · "+Balance.EnvironmentCost+" 영양제",.07f,.28f,.86f,.085f,()=>PreviewEnvironment("environment:E02"));
            GardenUi.Button(p,"돌아가기",.07f,.16f,.86f,.075f,ShowHome);
        }
        public void PreviewEnvironment(string id)
        {
            ClearGhost();
            pendingEnvironment=id;pendingPurchase=Guid.NewGuid().ToString("N");screen="placement";
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
            ClearGhost();
            screen="growth";pendingPurchase=Guid.NewGuid().ToString("N");var p=Page("식물 성장 미리보기");
            GardenUi.Label(p,"기존 식물의 크기가 한 단계 자라요.\n비용 "+Balance.GrowthCost+" 영양제 · 취소하면 그대로예요",.07f,.65f,.86f,.14f,30);
            ThemePreview();
            var state=Service.Snapshot;
            string unavailable=state.Nutrient<Balance.GrowthCost?"영양제가 "+(Balance.GrowthCost-state.Nutrient)+"개 더 필요해요. 미리보기는 자유예요.":null;
            var plant=state.Plants.FirstOrDefault(item=>item.PlantId=="plant:P06");
            if(plant==null){plant=new PlantProgress{PlantId="plant:P06"};state.Plants.Add(plant);}
            plant.Growth+=Balance.GrowthAmount;
            if(!ShowIntegratedPurchasePreview(state) && rewardPlant!=null) rewardPlant.localScale*=1+Balance.GrowthAmount;
            GardenUi.Button(p,"취소",.07f,.23f,.41f,.08f,()=>{UpdateGarden();ShowShop();});
            var confirm=GardenUi.Button(p,"확정",.52f,.23f,.41f,.08f,()=>
            {
                if(Apply(Service.GrowPlant(pendingPurchase,"plant:P06")))GoGarden();
                else
                {
                    ShowIntegratedPurchasePreview(state);
                    notice.text="저장하지 못했어요. 영양제는 그대로예요. 다시 시도해 주세요.";
                }
            },unavailable==null);
            SetPurchaseAvailability(p,confirm,unavailable);
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
        bool ShowIntegratedPurchasePreview(GardenState candidate)
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
            home.gardenCamera.aspect=1.6f;
            home.gardenCamera.orthographicSize=Mathf.Max(.1f,radius*1.04f);
            if(SystemInfo.graphicsDeviceType!=UnityEngine.Rendering.GraphicsDeviceType.Null)
                home.gardenCamera.Render();
            var existing=view.transform.Find("Purchase garden viewport");
            var viewport=existing!=null?existing.gameObject:GardenUi.Box(view.transform,"Purchase garden viewport",.07f,.345f,.86f,.275f);
            var existingImage=viewport.GetComponentInChildren<RawImage>();
            if(existingImage==null)
            {
                var picture=GardenUi.Box(viewport.transform,"Garden preview",0,0,1,1);
                existingImage=picture.AddComponent<RawImage>();existingImage.raycastTarget=false;
                var fit=picture.AddComponent<AspectRatioFitter>();fit.aspectMode=AspectRatioFitter.AspectMode.FitInParent;fit.aspectRatio=1.6f;
            }
            existingImage.texture=home.gardenCamera.targetTexture;
            view.GetComponent<Image>().color=GardenUi.Background;
            return true;
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
        public void ShowRecords()
        {
            screen="records";var p=Page("참여 기록");var s=Service.Snapshot;
            GardenUi.FromTop(GardenUi.Label(p,"현재 코스 "+s.MbctCompletedIds.Count+" / 48 · 쉬어도 진행은 그대로예요",.07f,.73f,.86f,.07f,26).transform,306,80);
            var all=s.Sessions.Where(x=>x.Status==ParticipationState.RewardCommitted || x.Status==ParticipationState.Ended).Reverse().ToArray();
            int pages=Math.Max(1,(all.Length+2)/3);recordsPage=Math.Min(recordsPage,pages-1);
            var sessions=all.Skip(recordsPage*3).Take(3).ToArray();
            for(int i=0;i<sessions.Length;i++)
            {
                var entry=sessions[i];
                var card=GardenUi.MenuButton(p,MindfulnessContent.FindMission(entry.MissionId)?.title ?? entry.MissionId,StateLabel(entry.Status),.07f,0,.86f,0,()=>ShowRecord(entry.SessionId),PrototypeUiIcon.Symbol.Journal);
                GardenUi.FromTop(card.transform,410+i*200,184);
            }
            if(sessions.Length==0) GardenUi.Label(p,"아직 참여 기록이 없어요.",.07f,.50f,.86f,.1f,30);
            GardenUi.FromTop(GardenUi.Button(p,"경험 알아차리기",.07f,.32f,.86f,.065f,ShowExperienceTypes).transform,1050,88);
            GardenUi.FromTop(GardenUi.Button(p,"경험 기록과 나의 계획",.07f,.24f,.86f,.065f,ShowExperienceHistory).transform,1154,88);
            GardenUi.FromTop(GardenUi.Button(p,"발견한 동물",.07f,.15f,.41f,.075f,ShowDiscoveries).transform,1266,112);
            GardenUi.FromTop(GardenUi.Button(p,"돌아가기",.52f,.15f,.41f,.075f,ShowHome).transform,1266,112);
            if(pages>1)
            {
                var previous=GardenUi.Button(p,"이전 기록",.07f,.075f,.41f,.055f,()=>{recordsPage--;ShowRecords();});previous.interactable=recordsPage>0;
                var next=GardenUi.Button(p,"다음 기록",.52f,.075f,.41f,.055f,()=>{recordsPage++;ShowRecords();});next.interactable=recordsPage<pages-1;
            }
        }
        public void ShowRecord(string id)
        {
            screen="record";var s=Service.Snapshot.Sessions.First(x=>x.SessionId==id);var p=Page("활동과 측정 기록");
            GardenUi.ScrollText(p,(MindfulnessContent.FindMission(s.MissionId)?.title ?? s.MissionId)+"\n"+LocalTime(s.StartedUtc)+" ~ "+LocalTime(s.EndedUtc)+"\n기분: "+(string.IsNullOrEmpty(s.Mood)?"기록 건너뜀":s.Mood)+"\n"+(s.Note??""),.07f,.57f,.86f,.23f);
            if(MbctPolicy.IsMbct(s.MissionId))GardenUi.Button(p,"실습에서 적은 내용",.07f,.075f,.54f,.055f,()=>ShowPracticeAnswers(id));
            var health=preview && PreviewHealth!=null ? PreviewHealth : AndroidPlatformBridge.GetSnapshot();
            HeartHistoryView.Build(p,health,s.StartedUtc,s.EndedUtc);
            GardenUi.Button(p,"건강 기록 다시 조회",.07f,.15f,.54f,.075f,()=>{if(DateTimeOffset.TryParse(s.StartedUtc,out var start) && DateTimeOffset.TryParse(s.EndedUtc,out var end)) { var result=AndroidPlatformBridge.RefreshHeartRange(start.ToUnixTimeMilliseconds(),end.ToUnixTimeMilliseconds()); error=result.status=="ERROR"?result.message:null; }
                ShowRecord(id);});
            GardenUi.Button(p,"다시 보기",.65f,.15f,.28f,.075f,()=>ShowRecord(id));
            GardenUi.Button(p,"기록 목록",.65f,.075f,.28f,.055f,ShowRecords);
        }
        public void ShowDiscoveries()
        {
            screen="discoveries";var p=Page("정원에서 만난 친구들");
            var ids=Service.Snapshot.DiscoveredAnimalIds;
            GardenUi.Label(p,ids.Count==0?"아직 만난 동물이 없어요.\n아무도 방문하지 않는 날도 자연스러워요.":string.Join("\n",ids.Select(id=>MindfulnessContent.FindAnimal(id)?.title ?? id)),.07f,.29f,.86f,.48f,32);
            GardenUi.Button(p,"돌아가기",.07f,.15f,.86f,.08f,ShowRecords);
        }
        static string StateLabel(ParticipationState s) => s==ParticipationState.Selected?"선택됨":s==ParticipationState.InProgress?"참여 중":s==ParticipationState.Paused?"일시정지":s==ParticipationState.Ended?"참여 종료 · 완료 보상 없음":"완료 · 보상 처리됨";
        static string LocalTime(string iso) => DateTimeOffset.TryParse(iso,out var time)?time.ToLocalTime().ToString("MM/dd HH:mm"):"시작 전";
        public void LeaveActivityPanel()
        {
            SaveActivePracticeDraft();ClearGhost();UpdateGarden();
            if(screen=="placement" || screen=="growth")ShowShop();
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
            if(!opened)return;var s=Service.Snapshot;
            if(stateSummary!=null)stateSummary.text=(mode=="LIVE"?"":"["+mode+"]  ")+"영양제 "+s.Nutrient+"  ·  코스 "+Math.Min(48,s.MbctCompletedIds.Count)+" / 48";
            transform.root.GetComponent<GardenHomePresenter>()?.RefreshFromState(s);
            if(rewardPlant!=null) rewardPlant.localScale=basePlantScale*(1+Mathf.Clamp(s.LegacyGrowth+s.Plants.Sum(p=>p.Growth),0,2));
            if(islandStatus!=null)islandStatus.text=(mode=="LIVE"?"":"["+mode+"] ")+"영양제 "+s.Nutrient+"\n"+VisitorText(s);
            var signature=string.Join("|",s.Environments.Select(e=>e.EnvironmentId+":"+e.Slot))+"/"+s.DailyDecisions.LastOrDefault()?.VisitorId;
            if(gardenObjects!=null && signature==gardenSignature)return;gardenSignature=signature;
            if(gardenObjects!=null){gardenObjects.SetActive(false);if(Application.isPlaying)Destroy(gardenObjects);else DestroyImmediate(gardenObjects);}
            gardenObjects=new GameObject("Saved garden additions");
            foreach(var e in s.Environments)CreateEnvironment(e.EnvironmentId,false).transform.SetParent(gardenObjects.transform,true);
            var visitor=s.DailyDecisions.LastOrDefault()?.VisitorId;
            if(!string.IsNullOrEmpty(visitor))CreateVisitor(visitor).transform.SetParent(gardenObjects.transform,true);
        }
        static GameObject Shape(PrimitiveType type, Transform parent,Vector3 position,Vector3 scale,Color color)
        {
            var go=GameObject.CreatePrimitive(type);go.transform.SetParent(parent,false);go.transform.localPosition=position;go.transform.localScale=scale;
            var shader=Shader.Find("Universal Render Pipeline/Lit");var mat=new Material(shader);mat.color=color;go.GetComponent<Renderer>().sharedMaterial=mat;
            var collider=go.GetComponent<Collider>();if(Application.isPlaying)Destroy(collider);else DestroyImmediate(collider);
            go.AddComponent<GardenMaterialCleanup>();return go;
        }
        static GameObject CreateEnvironment(string id,bool isGhost)
        {
            var go=new GameObject((isGhost?"PREVIEW ":"")+id);var tint=isGhost?new Color(.45f,.7f,.75f):new Color(.54f,.35f,.22f);
            if(id=="environment:E01")
            {
                go.transform.position=new Vector3(-1.25f,-.04f,.45f);
                Shape(PrimitiveType.Cube,go.transform,Vector3.zero,new Vector3(.7f,.13f,.55f),tint);
                for(int i=0;i<5;i++)Shape(PrimitiveType.Sphere,go.transform,new Vector3((i%3)*.2f-.2f,.16f,(i/3)*.2f-.1f),Vector3.one*.16f,new Color(.9f,.65f,.4f));
            }
            else
            {
                go.transform.position=new Vector3(.75f,-.04f,.1f);
                Shape(PrimitiveType.Cube,go.transform,new Vector3(0,.55f,0),new Vector3(.85f,.12f,.65f),tint);
                foreach(float x in new[]{-.32f,.32f})Shape(PrimitiveType.Cube,go.transform,new Vector3(x,.25f,0),new Vector3(.08f,.5f,.08f),tint);
            }
            return go;
        }
        static GameObject CreateVisitor(string id)
        {
            var go=new GameObject("Visitor placeholder "+id);go.transform.position=new Vector3(.45f,.02f,-.75f);
            Shape(PrimitiveType.Sphere,go.transform,Vector3.zero,new Vector3(.35f,.27f,.24f),new Color(.80f,.75f,.65f));
            Shape(PrimitiveType.Sphere,go.transform,new Vector3(0,.18f,-.10f),Vector3.one*.22f,new Color(.86f,.82f,.73f));
            foreach(float x in new[]{-.065f,.065f})Shape(PrimitiveType.Capsule,go.transform,new Vector3(x,.34f,-.10f),new Vector3(.07f,.14f,.07f),new Color(.86f,.82f,.73f));
            return go;
        }
        public string CurrentScreen => screen;

        public bool DebugWalkEnabled => opened && Service.Snapshot.DebugWalkPeriods.Any(p=>string.IsNullOrEmpty(p.EndedUtc));
        public string DebugWalkSummary
        {
            get
            {
                if(!opened)return "걸음 보상: 저장 상태를 확인해 주세요";
                var p=Service.Snapshot.DebugWalkPeriods.LastOrDefault(x=>string.IsNullOrEmpty(x.EndedUtc));
                if(p==null)return "디버그 걸음 보상 OFF · 일반 M15 목표 300걸음";
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
            if(runningStepQuery!=null)
            {
                if(Time.unscaledTime<nextStepPoll)return;
                nextStepPoll=Time.unscaledTime+0.75f;
                var result=AndroidPlatformBridge.GetStepRangeSnapshot();
                if(result!=null && result.requestId==runningStepQuery.Id && !result.refreshing)
                {
                    if(result.queryComplete && result.hasValue && result.status=="AVAILABLE")
                    {
                        if(runningStepQuery.PeriodId!=null)Apply(Service.ObserveDebugWalk(runningStepQuery.PeriodId,result.count,result.source));
                        if(runningStepQuery.SessionId!=null)Apply(Service.ObserveMissionSteps(runningStepQuery.SessionId,result.count,result.source));
                        stepQueryStatus="실제 걸음 갱신됨 · "+result.sourceLabel+" · "+DateTime.Now.ToString("HH:mm");
                        MaybeNotifyMissionReady();
                    }
                    else stepQueryStatus=(result.status=="NO_DATA"?"이 구간의 걸음 기록 없음 · 동기화 대기 가능":result.status+" · "+result.message)+" · "+DateTime.Now.ToString("HH:mm");
                    runningStepQuery=null;
                    if(screen=="session")ShowSession();
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
            var action=AndroidPlatformBridge.RefreshStepsRange(query.StartMs,query.EndMs,query.Id);
            if(action.ParsedStatus==PlatformStatus.Available)runningStepQuery=query;
            else {stepQueryStatus=action.status+" · "+action.message+" · "+DateTime.Now.ToString("HH:mm");nextStepSync=Time.unscaledTime+5f;if(screen=="session")ShowSession();}
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
            if(mission!=null && DateTimeOffset.TryParse(mission.StartedUtc,out var startAt) && now>startAt.ToUnixTimeMilliseconds())
                stepQueries.Enqueue(new StepQuery{Id=Guid.NewGuid().ToString("N"),SessionId=mission.SessionId,
                    StartMs=startAt.ToUnixTimeMilliseconds(),EndMs=now});

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
            if(mission==null || mission.GoalNotificationCommitted ||
                mission.RealStepHighWater+(DebugWalkEnabled?mission.VirtualSteps:0)<(DebugWalkEnabled?10:300))return;
            var action=AndroidPlatformBridge.NotifyMissionRewardReady(mission.SessionId);
            if(action.ParsedStatus==PlatformStatus.Available)Apply(Service.MarkMissionGoalNotified(mission.SessionId));
        }
        void ScheduleMissionReminder()
        {
            if(!opened || mode!="LIVE")return;
            var mission=Service.Snapshot.Sessions.LastOrDefault(s=>s.MissionId=="free-mission:M15" &&
                (s.Status==ParticipationState.InProgress || s.Status==ParticipationState.Paused));
            if(mission==null || !DateTimeOffset.TryParse(mission.StartedUtc,out var started))return;
            AndroidPlatformBridge.ScheduleMissionRewardReminder(mission.SessionId,started.ToUnixTimeMilliseconds(),DebugWalkEnabled?10:300,
                DebugWalkEnabled?mission.VirtualSteps:0);
        }
        void ReconcileMissionReminder()
        {
            if(!opened || mode!="LIVE")return;
            ScheduleMissionReminder();
            foreach(var closed in Service.Snapshot.Sessions.Where(s=>s.MissionId=="free-mission:M15" &&
                (s.Status==ParticipationState.Ended || s.Status==ParticipationState.RewardCommitted)))
                AndroidPlatformBridge.CancelMissionRewardReminder(closed.SessionId);
        }
        public void OnAppReturned()
        {
            if(!opened)return;
            RefreshCycle();
            RequestStepSyncSoon(true);
            ReconcileMissionReminder();
        }
    }
    public sealed class GardenMaterialCleanup : MonoBehaviour
    {
        void OnDestroy(){var r=GetComponent<Renderer>();if(r!=null && r.sharedMaterial!=null){if(Application.isPlaying)Destroy(r.sharedMaterial);else DestroyImmediate(r.sharedMaterial);}}
    }
}
