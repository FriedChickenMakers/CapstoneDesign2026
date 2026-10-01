using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using CapstoneDesign.Runtime.LocalState;

namespace CapstoneDesign.Runtime
{
    // Keeps the original serialized component identity while replacing split PlayerPrefs writes.
    [DefaultExecutionOrder(-100)]
    public sealed class WeekOneQuestDemo : MonoBehaviour
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
        GameObject view, gardenObjects, ghost;
        Text islandStatus, notice;
        Vector3 basePlantScale;
        string draftNote;
        string sessionId, mood, pendingEnvironment, pendingPurchase, error, screen = "home";
        InputField note;
        bool opened, preview;
        float nextCycleCheck;
        int freeIndex, coursePage;
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
        void OnApplicationFocus(bool focus) { if(focus && opened) OnAppReturned(); }
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
            return success;
        }
        Transform Page(string title)
        {
            if(view!=null) { view.SetActive(false); if(Application.isPlaying) Destroy(view); else DestroyImmediate(view); }
            view=GardenUi.Box(transform,"Local garden flow",0,.105f,1,.895f,new Color(.035f,.075f,.105f,.98f));
            GardenUi.Label(view.transform,title,.07f,.87f,.86f,.1f,44);
            var s=opened ? Service.Snapshot : null;
            GardenUi.Label(view.transform,(mode=="LIVE"?"":"["+mode+"]  ")+ (s==null ? "저장 상태를 확인해 주세요" : "영양제 "+s.Nutrient+"  ·  코스 "+Math.Min(28,s.NextCourseOrder)+" / 28"),.07f,.81f,.86f,.05f,25);
            notice=GardenUi.Label(view.transform,string.IsNullOrEmpty(error)?"": "저장/처리 오류 · 진행은 변경하지 않았어요\n"+error,.07f,.02f,.86f,.11f,23);
            notice.color=new Color(1,.78f,.6f);
            return view.transform;
        }
        public void ShowHome()
        {
            screen="home"; var p=Page("오늘, 할 수 있는 만큼");
            if(!opened) { GardenUi.Label(p,"저장본을 보존했습니다. 새 계정으로 초기화하지 않습니다.\n앱을 닫고 저장 공간과 복구 안내를 확인해 주세요.",.07f,.48f,.86f,.25f); return; }
            var s=Service.Snapshot;
            var mission=MindfulnessContent.Course.FirstOrDefault(m=>m.recommendedOrder==s.NextCourseOrder);
            GardenUi.Label(p,mission==null ? "4주 코스를 마쳤어요. 편한 활동을 골라보세요." : "이번 추천 · "+MindfulnessContent.WeekTitles[mission.week-1]+"\n"+mission.sourceId+"  "+mission.title,.07f,.65f,.86f,.14f,30);
            if(!string.IsNullOrEmpty(sessionId)) GardenUi.Button(p,"진행하던 활동으로",.07f,.55f,.86f,.075f,ShowSession);
            else if(mission!=null) GardenUi.Button(p,"추천 활동 안내",.07f,.55f,.86f,.075f,()=>SelectMission(mission));
            GardenUi.Button(p,"4주 코스 둘러보기",.07f,.45f,.86f,.075f,ShowCourse);
            GardenUi.Button(p,"자유 선택 활동",.07f,.35f,.86f,.075f,ShowFree);
            GardenUi.Button(p,"영양제로 정원 가꾸기",.07f,.25f,.86f,.075f,ShowShop);
            GardenUi.Button(p,"참여 기록과 돌아보기",.07f,.15f,.86f,.075f,ShowRecords);
        }
        public void ShowCourse()
        {
            screen="course";var p=Page("4주 마음챙김 코스");
            GardenUi.Label(p,(coursePage+1)+"주 · "+MindfulnessContent.WeekTitles[coursePage],.07f,.735f,.86f,.065f,29);
            for(int i=0;i<7;i++)
            {
                var m=MindfulnessContent.Course[coursePage*7+i];
                var b=GardenUi.Button(p,m.sourceId+"  "+m.title,.07f,.66f-i*.065f,.86f,.055f,()=>SelectMission(m));
                b.interactable=m.recommendedOrder<=Service.Snapshot.NextCourseOrder;
            }
            GardenUi.Button(p,"이전 주",.07f,.18f,.26f,.06f,()=>{coursePage=Math.Max(0,coursePage-1);ShowCourse();});
            GardenUi.Button(p,"다음 주",.37f,.18f,.26f,.06f,()=>{coursePage=Math.Min(3,coursePage+1);ShowCourse();});
            GardenUi.Button(p,"돌아가기",.67f,.18f,.26f,.06f,ShowHome);
        }
        public void ShowFree()
        {
            screen="free"; var p=Page("자유 선택"); var m=MindfulnessContent.FreeMissions[freeIndex];
            GardenUi.Label(p,m.sourceId+"  "+m.title+"\n\n"+m.textOnlyInstructions,.07f,.45f,.86f,.31f,30);
            GardenUi.Button(p,"이 활동 안내",.07f,.35f,.86f,.075f,()=>SelectMission(m));
            GardenUi.Button(p,"다른 활동",.07f,.25f,.40f,.075f,()=>{freeIndex=(freeIndex+1)%MindfulnessContent.FreeMissions.Count;ShowFree();});
            GardenUi.Button(p,"M15 걷기",.53f,.25f,.40f,.075f,()=>SelectMission(MindfulnessContent.FreeMissions.First(x=>x.id=="free-mission:M15")));
            GardenUi.Button(p,"돌아가기",.07f,.15f,.86f,.075f,ShowHome);
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
            bool walking=s.MissionId=="free-mission:M15";
            if(walking && enteringSession)RequestStepSyncSoon();
            GardenUi.Label(p,walking ? "미션 시작 후 걸어 보세요. 동기화가 늦어도 직접 완료할 수 있어요."
                : (m?.textOnlyInstructions ?? "편안한 만큼 참여해 주세요.")+"\n\n"+((m?.suggestedDurationSeconds ?? 0)>0 ? "권장 "+m.suggestedDurationLabel : "시간은 편한 만큼")+" · 직접 완료할 수 있어요",.07f,.56f,.86f,.23f,30);
            string progress=walking ? "\n목표 "+(DebugWalkEnabled?10:300)+"걸음 · 실제 "+s.RealStepHighWater+" + 가상 "+(DebugWalkEnabled?s.VirtualSteps:0)+"\n"+stepQueryStatus : "";
            GardenUi.Label(p,(walking?"걷기 미션": "음원 준비 중 · 자체 작성 텍스트 안내")+"\n"+StateLabel(s.Status)+progress,.07f,.41f,.86f,.15f,23);
            if(s.Status==ParticipationState.Selected) GardenUi.Button(p,"시작",.07f,.34f,.86f,.075f,()=>{if(Apply(Service.StartSession(sessionId))){RequestStepSyncSoon();ScheduleMissionReminder();}ShowSession();});
            if(s.Status==ParticipationState.InProgress || s.Status==ParticipationState.Paused)
            {
                GardenUi.Button(p,s.Status==ParticipationState.Paused?"이어서 하기":"일시정지",.07f,.35f,.86f,.075f,()=>{if(Apply(s.Status==ParticipationState.Paused?Service.ResumeSession(sessionId):Service.PauseSession(sessionId)) && walking)ScheduleMissionReminder();ShowSession();});
                GardenUi.Button(p,"직접 완료 · 선택 기록",.07f,.25f,.86f,.075f,ShowReflection);
            }
            if(walking && (s.Status==ParticipationState.InProgress || s.Status==ParticipationState.Paused))
                GardenUi.Button(p,"걸음 다시 조회",.07f,.15f,.41f,.075f,()=>{RequestStepSyncSoon(true,true);TickStepSync();ShowSession();});
            GardenUi.Button(p,"오늘은 여기까지",walking ? .52f : .07f,.15f,.41f,.075f,()=>{if(Apply(Service.EndParticipation(sessionId))){if(walking)AndroidPlatformBridge.CancelMissionRewardReminder(sessionId);sessionId=null;}ShowHome();});
            if(!walking)GardenUi.Button(p,"목록으로",.52f,.15f,.41f,.075f,ShowHome);
        }
        public void ShowReflection()
        {
            screen="reflection";var p=Page("돌아보기는 선택이에요");
            GardenUi.Label(p,"지금 기분은 어떤가요?\n잘 모르거나 기록하지 않아도 괜찮아요.",.07f,.65f,.86f,.13f,30);
            GardenUi.Button(p,"편안해요",.07f,.55f,.26f,.075f,()=>{mood="편안해요";notice.text="선택: "+mood;});
            GardenUi.Button(p,"복잡해요",.37f,.55f,.26f,.075f,()=>{mood="복잡해요";notice.text="선택: "+mood;});
            GardenUi.Button(p,"잘 모르겠어요",.67f,.55f,.26f,.075f,()=>{mood="잘 모르겠어요";notice.text="선택: "+mood;});
            note=GardenUi.Input(p,"선택 메모 · 기기에만 저장",.07f,.36f,.86f,.14f);
            note.text=draftNote ?? ""; note.onValueChanged.AddListener(value=>draftNote=value);
            GardenUi.Button(p,"이 기록으로 완료",.07f,.25f,.86f,.075f,()=>Finish(mood,note.text));
            GardenUi.Button(p,"기록 건너뛰고 완료",.07f,.15f,.86f,.075f,()=>Finish(null,null));
        }
        public void Finish(string selectedMood,string selectedNote)
        {
            var finishing=ActiveSession();
            if(!Apply(Service.CompleteSession(sessionId,selectedMood,selectedNote))) {draftNote=selectedNote;ShowReflection();return;}
            if(finishing?.MissionId=="free-mission:M15")AndroidPlatformBridge.CancelMissionRewardReminder(sessionId);
            screen="complete"; var p=Page("활동을 마쳤어요");
            GardenUi.Label(p,"완료와 보상을 함께 저장했습니다.\n같은 회차·같은 활동의 보상은 한 번만 받아요.\n\n오늘의 작은 참여가 정원에 남아요.",.07f,.45f,.86f,.30f,30);
            sessionId=null;
            GardenUi.Button(p,"영양제로 정원 가꾸기",.07f,.32f,.86f,.08f,ShowShop);
            GardenUi.Button(p,"정원으로",.07f,.21f,.86f,.08f,GoGarden);
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
            pendingEnvironment=id;pendingPurchase=Guid.NewGuid().ToString("N");screen="placement";
            var p=Page("배치 미리보기");
            GardenUi.Label(p,(MindfulnessContent.FindGardenContent(id)?.title ?? id)+"\n지정된 빈 자리 1곳에 배치합니다.\n비용 "+Balance.EnvironmentCost+" 영양제 · 아직 차감하지 않았어요",.07f,.62f,.86f,.18f,29);
            // A transparent central window reveals the real scene preview.
            view.GetComponent<Image>().color=new Color(.035f,.075f,.105f,.30f);
            ClearGhost();ghost=CreateEnvironment(id,true);
            GardenUi.Button(p,"확정",.07f,.23f,.41f,.08f,()=>{if(Apply(Service.PurchaseEnvironment(pendingPurchase,pendingEnvironment,pendingEnvironment=="environment:E01"?"left":"right"))){ClearGhost();GoGarden();}else notice.text="배치하지 못했어요 · 잔액 또는 겹치는 자리를 확인해 주세요.\n"+error;});
            GardenUi.Button(p,"취소",.52f,.23f,.41f,.08f,ShowShop);
        }
        public void PreviewGrowth()
        {
            screen="growth";pendingPurchase=Guid.NewGuid().ToString("N");var p=Page("식물 성장 미리보기");
            GardenUi.Label(p,"기존 식물의 크기가 한 단계 자라요.\n비용 "+Balance.GrowthCost+" 영양제 · 취소하면 그대로예요",.07f,.62f,.86f,.18f,30);
            view.GetComponent<Image>().color=new Color(.035f,.075f,.105f,.30f);
            if(rewardPlant!=null) rewardPlant.localScale*=1+Balance.GrowthAmount;
            GardenUi.Button(p,"확정",.07f,.23f,.41f,.08f,()=>{if(Apply(Service.GrowPlant(pendingPurchase,"plant:P06")))GoGarden();else {UpdateGarden();notice.text=error;}});
            GardenUi.Button(p,"취소",.52f,.23f,.41f,.08f,()=>{UpdateGarden();ShowShop();});
        }
        public void ShowRecords()
        {
            screen="records";var p=Page("참여 기록");var s=Service.Snapshot;
            GardenUi.Label(p,"현재 코스 "+Math.Min(28,s.NextCourseOrder)+" / 28 · 쉬어도 진행은 그대로예요",.07f,.73f,.86f,.07f,26);
            var sessions=s.Sessions.Where(x=>x.Status==ParticipationState.RewardCommitted || x.Status==ParticipationState.Ended).Reverse().Take(5).ToArray();
            for(int i=0;i<sessions.Length;i++) {var entry=sessions[i]; GardenUi.Button(p,(MindfulnessContent.FindMission(entry.MissionId)?.title ?? entry.MissionId)+" · "+StateLabel(entry.Status),.07f,.62f-i*.09f,.86f,.075f,()=>ShowRecord(entry.SessionId));}
            if(sessions.Length==0) GardenUi.Label(p,"아직 참여 기록이 없어요.",.07f,.50f,.86f,.1f,30);
            GardenUi.Button(p,"발견한 동물",.07f,.15f,.41f,.075f,ShowDiscoveries);
            GardenUi.Button(p,"돌아가기",.52f,.15f,.41f,.075f,ShowHome);
        }
        public void ShowRecord(string id)
        {
            screen="record";var s=Service.Snapshot.Sessions.First(x=>x.SessionId==id);var p=Page("활동과 측정 기록");
            GardenUi.Label(p,(MindfulnessContent.FindMission(s.MissionId)?.title ?? s.MissionId)+"\n"+LocalTime(s.StartedUtc)+" ~ "+LocalTime(s.EndedUtc)+"\n기분: "+(string.IsNullOrEmpty(s.Mood)?"기록 건너뜀":s.Mood)+"\n"+(s.Note??""),.07f,.57f,.86f,.23f,26);
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
            ClearGhost();UpdateGarden();
            if(screen=="placement" || screen=="growth")ShowShop();
        }
        public void GoGarden(){ ClearGhost(); UpdateGarden(); transform.root.GetComponent<MockupNavigation>()?.ShowIsland(); }
        void ClearGhost(){if(ghost!=null){ghost.SetActive(false);if(Application.isPlaying)Destroy(ghost);else DestroyImmediate(ghost);ghost=null;}}
        void SetupIsland()
        {
            var nav=transform.root.GetComponent<MockupNavigation>(); if(nav==null)return;
            islandStatus=GardenUi.Label(nav.islandPanel.transform,"",.07f,.17f,.86f,.11f,27);
            GardenUi.Button(nav.islandPanel.transform,"정원 가꾸기",.07f,.30f,.40f,.055f,()=>{nav.ShowActivities();ShowShop();});
            GardenUi.Button(nav.islandPanel.transform,"방문 감상",.53f,.30f,.40f,.055f,()=>{if(islandStatus!=null)islandStatus.text=VisitorText()+"\n조용히 함께 쉬어요.";});
            UpdateGarden();
        }
        string VisitorText(){if(!opened)return "저장 상태 확인 필요";var d=Service.Snapshot.DailyDecisions.LastOrDefault();return string.IsNullOrEmpty(d?.VisitorId)?"오늘은 방문한 동물이 없어요":(MindfulnessContent.FindAnimal(d.VisitorId)?.title ?? d.VisitorId)+" · "+(MindfulnessContent.FindAnimal(d.VisitorId)?.tapReaction ?? "쉬고 있어요");}
        public void UpdateGarden()
        {
            if(!opened)return;var s=Service.Snapshot;
            transform.root.GetComponent<GardenHomePresenter>()?.RefreshFromState();
            if(rewardPlant!=null) rewardPlant.localScale=basePlantScale*(1+Mathf.Clamp(s.LegacyGrowth+s.Plants.Sum(p=>p.Growth),0,2));
            if(islandStatus!=null)islandStatus.text=(mode=="LIVE"?"":"["+mode+"] ")+"영양제 "+s.Nutrient+"\n"+VisitorText();
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
                        if(runningStepQuery.PeriodId!=null)Apply(Service.ObserveDebugWalk(runningStepQuery.PeriodId,result.count));
                        if(runningStepQuery.SessionId!=null)Apply(Service.ObserveMissionSteps(runningStepQuery.SessionId,result.count));
                        stepQueryStatus="실제 걸음 갱신됨 · "+DateTime.Now.ToString("HH:mm");
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
                nextStepSync=Time.unscaledTime+30f;
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
