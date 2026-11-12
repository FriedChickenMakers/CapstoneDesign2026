using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using CapstoneDesign.Runtime.LocalState;
using CapstoneDesign.Prototype;

namespace CapstoneDesign.Runtime
{
    public sealed partial class WeekOneQuestDemo
    {
        static readonly string[] FocusPrograms = { "2w", "4w", "8w" };
        readonly HashSet<string> focusExpandedCurricula = new HashSet<string>();
        readonly HashSet<string> focusExpandedRecordSteps = new HashSet<string>();
        FocusRecommendationExposure focusVisibleRecommendation;
        string focusRecommendationSignature;

        FocusRecommendationExposure PrepareFocusRecommendation(GardenState state)
        {
            var suggestion=FocusAdditionalRecommendationPolicy.Select(state,Service.CurrentUtc);
            string signature=suggestion.MissionId+"|"+suggestion.PolicyId+"|"+suggestion.Reason+"|"+state.LastCycleId+"|"+state.Sessions.Count;
            if(focusVisibleRecommendation==null || focusRecommendationSignature!=signature
                || DateTimeOffset.TryParse(focusVisibleRecommendation.ShownUtc,out var prior) && (Service.CurrentUtc-prior).TotalHours>=24)
            {
                focusRecommendationSignature=signature;
                focusVisibleRecommendation=new FocusRecommendationExposure {DecisionId=Guid.NewGuid().ToString("N"),
                    MissionId=suggestion.MissionId,PolicyId=suggestion.PolicyId,Reason=suggestion.Reason};
            }
            return focusVisibleRecommendation;
        }
        RecommendationExposure ObserveFocusRecommendation(Transform target,FocusRecommendationExposure snapshot,RectTransform viewport=null)
        {
            var visibility=target.gameObject.AddComponent<RecommendationExposure>();
            visibility.Viewport=viewport;
            visibility.OnVisible=()=>
            {
                if(string.IsNullOrEmpty(snapshot.ShownUtc))snapshot.ShownUtc=Service.CurrentUtc.ToString("o");
                if(!preview && Application.isPlaying)PersonalizationClient.Instance.RecordMeasuredFocusExposure(snapshot);
            };
            return visibility;
        }
        FocusRecommendationExposure SelectedFocusRecommendation()
        {
            var snapshot=focusVisibleRecommendation;
            if(snapshot==null || !DateTimeOffset.TryParse(snapshot.ShownUtc,out var shown)
                || shown>Service.CurrentUtc || (Service.CurrentUtc-shown).TotalHours>=24)return null;
            return snapshot;
        }
        public void ShowHome() => ShowFocusHome();
        public void ShowCourse() => ShowFocusCourse();
        public void ShowFree() => ShowFocusRecommendation();
        public void ShowProgramSelection() => ShowFocusProgramSelection();
        public void ShowRecords() => ShowFocusCourse();
        public void ShowRecord(string id) => ShowFocusRecord(id);

        static string FocusProgramTitle(string program) => program == "legacy" ? "이전 예시 과정" : program == "additional" ? "추가 미션" : MbctPolicy.ProgramWeeks(program) + "주 코스";
        static string FocusProgramDescription(string program) => program == "2w" ? "감각부터 나를 돌보는 계획까지" : program == "4w" ? "몸·생각·행동을 차근차근 살피기" : "다양한 상황에서 반복하며 익히기";
        // IDs overlap between historical P/M content and Focus versions. Read
        // the session's own definition/snapshot, never rename an older record.
        static string FocusSessionTitle(ActivitySession entry) => FocusMissionPolicy.IsFocus(entry)
            ? entry.MissionTitleSnapshot ?? FocusMissionCatalog.FindForSession(entry)?.Title ?? entry.MissionId
            : MindfulnessContent.FindMission(entry.MissionId)?.title ?? entry.MissionId;

        static ActivitySession ActiveAdditionalMission(GardenState state) => FocusMissionPolicy.Active(state, FocusMissionCatalog.AdditionalRole)
            ?? state.Sessions.LastOrDefault(s => FocusMissionPolicy.IsActive(s) && s.MissionId.StartsWith("free-mission:", StringComparison.Ordinal));

        FocusMissionDefinition RecommendedFocusMission(GardenState state)
        {
            var active = ActiveAdditionalMission(state);
            if (active != null) return FocusMissionCatalog.FindForSession(active) ?? FocusMissionCatalog.Find(active.MissionId) ?? FocusMissionCatalog.Find("M03");
            var today = FocusMissionPolicy.TodayPractice(state, FocusMissionCatalog.AdditionalRole);
            // Keep today's completed card instead of offering another M mission.
            return FocusMissionCatalog.Find(today?.MissionId) ?? FocusMissionCatalog.Find(FocusAdditionalRecommendationPolicy.Select(state, Service.CurrentUtc).MissionId);
        }

        public void ShowFocusHome()
        {
            if(HasPendingFullReset){ShowFullResetRecovery();return;}
            RestoreMissionDebug();
            if (!opened) { ShowGardenLoadFailure("home", "미션", ShowFocusHome); return; }
            if (!SaveActivePracticeDraft()) return;
            ClearPracticeDraft();
            if (opened && !Service.Snapshot.MbctProgramChosen) { ShowFocusProgramSelection(); return; }
            var root = BeginFigmaPage("home", ShowFocusHome, FigmaMissionUi.Background, true);
            root.GetComponent<FocusMobilePageLayout>().FixedContent = true;
            FigmaHeader(root, "오늘의 미션", "");
            if (!opened) { FigmaMissionUi.Label(root, "저장된 내용을 불러오지 못했어요.\n기존 기록을 보존하고 있습니다.", 24, 170, 342, 100); return; }
            var state = Service.Snapshot; string program = state.MbctProgramId;
            bool needsPlant = !GardenPlantVisuals.HasCurrentPlant(state);
            string archivedSource = state.PlantAppearance?.Recipe?.InstanceId;
            int total = FocusMissionPolicy.TotalPracticesFor(program), done = FocusMissionPolicy.CompletedCount(state, program);
            var active = FocusMissionPolicy.Active(state, FocusMissionCatalog.BasicRole, program)
                ?? state.Sessions.LastOrDefault(a => FocusMissionPolicy.IsActive(a) && !a.FocusSupport && a.MissionId.StartsWith("course:", StringComparison.Ordinal));
            var todayBasic = FocusMissionPolicy.TodayPractice(state, FocusMissionCatalog.BasicRole);
            var mission = active != null ? FocusMissionCatalog.FindForSession(active)
                : FocusMissionCatalog.Find(todayBasic?.MissionId) ?? FocusMissionPolicy.MissionAt(FocusMissionPolicy.NextOrder(state, program), program);
            bool finished = done >= total; string unavailable = FocusMissionPolicy.Availability(state, program);
            bool doneToday = state.Sessions.Any(a => FocusMissionPolicy.IsFocus(a) && a.FocusRole == FocusMissionCatalog.BasicRole && !a.FocusReview && a.Status == ParticipationState.RewardCommitted && a.InstanceId == state.LastCycleId);
            bool practicedToday = FocusMissionPolicy.PracticeClaimedToday(state, FocusMissionCatalog.BasicRole);
            var body = FigmaMissionUi.Rect(root, "MissionHome.Content", 0, 104, 390, 600);
            pageViewport = null;
            var card = FigmaMissionUi.Box(body, "기본 미션", 24, 8, 342, 220, FigmaMissionUi.Green, 24);
            var icon = FigmaMissionUi.Asset(card, "FocusMissions/Main/basic-leaf", 24, 20, 24, 24);
            string courseLabel = active != null && (!FocusMissionPolicy.IsFocus(active) || FocusMissionPolicy.ProgramFor(active) != program)
                ? (FocusMissionPolicy.IsFocus(active) ? FocusProgramTitle(FocusMissionPolicy.ProgramFor(active)) : "이전 미션 이어가기")
                : FocusProgramTitle(program) + " · " + done + " / " + total;
            var badge = FigmaMissionUi.Label(card, courseLabel, 56, 20, 262, 24, 16, Color.white, true);
            var title = FigmaMissionUi.Label(card, active != null ? FocusSessionTitle(active) : todayBasic != null ? FocusSessionTitle(todayBasic) : finished ? "과정을 마쳤어요" : mission.Title, 24, 56, 294, 56, 24, Color.white, true);
            string context = needsPlant ? "새 씨앗을 심으면 미션을 시작할 수 있어요." : active != null ? "멈춘 곳부터 이어가요." : finished ? "쌓인 실천을 돌아봐요." : practicedToday ? "오늘의 기본 미션 실습을 마쳤어요." : unavailable != null ? "이번 주 실습을 마쳤어요. 다음 주에 이어가요." : "식물 성장 · 완료 보상 " + GardenEconomyPolicy.BasicMissionCoins + "코인";
            var detail = FigmaMissionUi.Label(card, context, 24, 112, 294, 40, 16, Color.white);
            string action = active != null ? "기본 미션 이어서 하기" : finished ? "과정 기록 보기" : doneToday ? "오늘 완료했어요" : practicedToday ? "내일 이어가요" : unavailable != null ? "다음 주에 이어가요" : "기본 미션 시작하기";
            var start = FigmaMissionUi.Button(card, needsPlant ? "새 식물 키우기" : action, 24, 156, 294, 48,
                needsPlant ? (Action)(() => StartNewGardenPlant(archivedSource)) : active == null && finished ? (Action)(() => ShowFocusProgram(program)) : StartFocusBasic, FigmaMissionUi.Green, Color.white, tone: GardenThemeSurface.Tone.Growth);
            start.name = needsPlant ? "Mission.StartNewPlant" : "Focus.BasicAction";
            start.interactable = needsPlant || finished || active != null || unavailable == null;
            var basic = new FocusMissionHomeLayout.Card { Root=card, Icon=icon.rectTransform, Badge=badge, Title=title, Detail=detail, Action=start, TitleSize=24, DetailSize=16, CenterText=true };
            var additional = BuildRecommendationCard(body, state, 292);
            var emotion = BuildDailyEmotionCard(body, state, 452);
            BuildMissionDebugLauncher(root);
            var layout = root.gameObject.AddComponent<FocusMissionHomeLayout>();
            layout.Body = body;
            layout.Header = root.Find("정원 제목표") as RectTransform;
            layout.Heading = root.Find("오늘의 미션") as RectTransform;
            layout.Notice = notice;
            layout.DebugLauncher = root.Find("Focus.DebugLauncher") as RectTransform;
            layout.Cards = new[] { basic, additional, emotion };
            layout.Refresh();
        }

        FocusMissionHomeLayout.Card BuildRecommendationCard(RectTransform root, GardenState state, float y)
        {
            var mission = RecommendedFocusMission(state);
            var active = ActiveAdditionalMission(state);
            var today = FocusMissionPolicy.TodayPractice(state, FocusMissionCatalog.AdditionalRole);
            bool completed = FocusMissionPolicy.AdditionalAvailability(state) != null;
            var box = FigmaMissionUi.Box(root, "추가 미션", 24, y, 342, 144, FigmaMissionUi.Warm, 20);
            var icon = FigmaMissionUi.Asset(box, mission.IconAsset, 16, 12, 24, 24);
            var badge = FigmaMissionUi.Label(box, "추가 미션", 48, 12, 278, 24, 16, FigmaMissionUi.Muted, true);
            var title = FigmaMissionUi.Label(box, active != null ? FocusSessionTitle(active) : today != null ? FocusSessionTitle(today) : mission.Title, 16, 40, 310, 48, 22, null, true);
            var detail = FigmaMissionUi.Label(box, active != null ? "멈춘 곳부터 이어가요" : completed ? "다음 추가 미션은 내일 열려요" : "식물 성장 · 완료 보상 " + GardenEconomyPolicy.AdditionalMissionCoins + "코인", 16, 86, 310, 24, 16, FigmaMissionUi.Brown);
            var snapshot = active == null && !completed ? PrepareFocusRecommendation(state) : null;
            var visibility = snapshot == null ? null : ObserveFocusRecommendation(box, snapshot, root);
            var button = FigmaMissionUi.Button(box, active != null ? "이어서 하기" : completed ? "오늘 완료했어요" : "시작하기", 16, 88, 310, 48,
                () => { visibility?.ReportVisible(); StartFocusAdditional(); }, FigmaMissionUi.Green, Color.white);
            button.name = "Focus.AdditionalAction";
            button.interactable = active != null || !completed;
            return new FocusMissionHomeLayout.Card { Root=box, Icon=icon.rectTransform, Badge=badge, Title=title, Detail=detail, Action=button, TitleSize=22, DetailSize=16, CenterText=true };
        }

        static string FocusMissionEffort(FocusMissionDefinition mission)
        {
            int seconds = mission.Steps.Sum(step => step.RecommendedSeconds);
            string activity = mission.Steps.Any(step => step.Kind == "draw") ? "그림" : mission.Steps.Any(step => step.Kind == "input") ? "짧은 기록" : seconds > 0 ? "잠시 집중" : "가벼운 실천";
            string duration = mission.Code == "M15" || mission.Code == "M16" ? "내 속도로" : seconds > 0 ? "집중 권장 " + (seconds < 60 ? seconds + "초" : Mathf.CeilToInt(seconds / 60f) + "분") : "시간은 자유롭게";
            return activity + " · " + duration;
        }

        public void ShowFocusProgramSelection()
        {
            if (!opened || !SaveActivePracticeDraft()) return;
            var state = Service.Snapshot;
            var root = BeginFigmaPage("focus-programs", ShowFocusProgramSelection, FigmaMissionUi.Background, true);
            FigmaHeader(root, state.MbctProgramChosen ? "내게 맞는 코스" : "내 속도로 시작해요", "");
            var back = FigmaMissionUi.Button(root, "‹ 기록", 24, 0, 110, 38, ShowFocusCourse, Color.clear, FigmaMissionUi.Green, 14);
            back.name = "Focus.ProgramsBack";
            var previous = state.Sessions.LastOrDefault(a => FocusMissionPolicy.IsActive(a) && !a.FocusSupport && a.MissionId.StartsWith("course:", StringComparison.Ordinal));
            var body = FigmaScroll(root, 110, 500, 600);
            var additional = ActiveAdditionalMission(state);
            bool additionalCompleted = FocusMissionPolicy.AdditionalAvailability(state) != null;
            FigmaMissionUi.Label(body, "나에게 맞는 기간을 골라요.", 24, 4, 342, 30, 18, FigmaMissionUi.Ink, false, TextAnchor.MiddleCenter);
            var footer = FigmaMissionUi.Rect(root, "Focus.ProgramsActions", 0, 0, 390, previous == null ? 52 : 112);
            var independent = FigmaMissionUi.Button(footer, additional != null ? "추가 미션 이어서 하기" : additionalCompleted ? "오늘 추가 미션 완료" : "추가 미션 시작하기", 24, previous == null ? 0 : 60, 342, 48, StartFocusAdditional, FigmaMissionUi.Warm, FigmaMissionUi.Green);
            independent.name = "Focus.TodayAdditional";
            independent.interactable = additional != null || !additionalCompleted;
            for (int i = 0; i < FocusPrograms.Length; i++)
            {
                string program = FocusPrograms[i]; int done = FocusMissionPolicy.CompletedCount(state, program);
                bool selected = state.MbctProgramChosen && state.MbctProgramId == program;
                var row = FigmaMissionUi.Button(body, "", 24, 48 + i * 120, 342, 108, () => ShowFocusProgramConfirmation(program), selected ? FigmaMissionUi.Soft : Color.white, FigmaMissionUi.Ink);
                row.name = "Focus.SelectProgram." + program;
                FigmaMissionUi.Label(row.transform, selected ? "●" : "○", 16, 20, 24, 28, 22, FigmaMissionUi.Green, true, TextAnchor.MiddleCenter);
                FigmaMissionUi.Label(row.transform, FocusProgramTitle(program) + (selected ? " · 현재 코스" : ""), 52, 12, 274, 28, 20, null, true);
                FigmaMissionUi.Label(row.transform, FocusProgramDescription(program), 52, 44, 274, 24, 15, FigmaMissionUi.Muted);
                FigmaMissionUi.Label(row.transform, FocusMissionPolicy.TotalPracticesFor(program) + "회 실습" + (done > 0 ? " · " + done + "회 완료" : ""), 52, 76, 274, 24, 15, FigmaMissionUi.Green);
            }
            if (previous != null) FigmaMissionUi.Button(footer, "진행하던 기본 미션 이어가기", 24, 0, 342, 48, () => SelectFocusSession(previous.SessionId));
            FigmaMissionUi.Label(body, "코스를 바꿔도 기록과 정원은 유지돼요.", 24, 416, 342, 32, 15, FigmaMissionUi.Muted, false, TextAnchor.MiddleCenter);
            BuildMissionDebugLauncher(root);
            ConfigureGardenPage(root, body, 464, footer, previous == null ? 52 : 112, back);
        }

        public void ShowFocusProgramConfirmation(string program)
        {
            if (!opened || !MbctPolicy.IsProgram(program) || !SaveActivePracticeDraft()) return;
            var state = Service.Snapshot;
            var root = BeginFigmaPage("focus-program-confirm", () => ShowFocusProgramConfirmation(program), FigmaMissionUi.Background, true);
            FigmaHeader(root, FocusProgramTitle(program), "");
            var back = FigmaMissionUi.Button(root, "‹ 코스 선택", 24, 0, 110, 38, ShowFocusProgramSelection, Color.clear, FigmaMissionUi.Green, 14);
            var body = FigmaScroll(root, 110, 500, 450);
            float y = BuildFocusRecordCard(body, "이 코스에서", FocusProgramDescription(program), 0, FigmaMissionUi.Soft);
            y = BuildFocusRecordCard(body, "진행 방법", "하루 기본 미션 1개 · 주 최대 6개\n총 " + FocusMissionPolicy.TotalPracticesFor(program) + "회 실습\n놓친 날에도 진도는 그대로예요.", y);
            int done = FocusMissionPolicy.CompletedCount(state, program);
            bool completed = done >= FocusMissionPolicy.TotalPracticesFor(program);
            y = BuildFocusRecordCard(body, completed ? "완료한 코스" : done > 0 ? "저장된 진도" : "기록은 그대로", completed ? "모든 실습을 마쳤어요. 남긴 기록을 확인해요." : done > 0 ? done + "회 완료 · 이어서 시작할 수 있어요." : "코스를 바꿔도 기록과 정원은 유지돼요.\n추가 미션은 코스와 별개로 하루 1개예요.", y);
            bool current = state.MbctProgramChosen && state.MbctProgramId == program;
            var previous = state.Sessions.LastOrDefault(a => FocusMissionPolicy.IsActive(a) && !a.FocusSupport && a.MissionId.StartsWith("course:", StringComparison.Ordinal));
            bool blocked = !completed && previous != null && !current;
            var footer = FigmaMissionUi.Rect(root, "Focus.ProgramActions", 0, 0, 390, blocked ? 176 : 116);
            if (blocked)
            {
                y = BuildFocusRecordCard(body, "먼저 진행 중인 미션을 마쳐요", "진행하던 미션을 마치면 코스를 바꿀 수 있어요.", y, FigmaMissionUi.Warm);
                FigmaMissionUi.Button(footer, "진행하던 기본 미션 이어가기", 24, 0, 342, 52, () => SelectFocusSession(previous.SessionId));
            }
            float actionY = blocked ? 60 : 0;
            var select = FigmaMissionUi.Button(footer, completed ? "코스 기록 보기" : done > 0 ? "이 코스 이어서 하기" : "이 코스로 시작", 24, actionY, 342, 52, () =>
            {
                if (completed) { ShowFocusProgram(program); return; }
                if (!Apply(Service.SelectMbctProgram(program))) return;
                coursePageInitialized = false; ShowFocusHome();
            });
            select.name = "Focus.ConfirmProgram." + program; select.interactable = !blocked;
            FigmaMissionUi.Button(footer, "다른 코스 보기", 24, actionY + 64, 342, 44, ShowFocusProgramSelection, Color.clear, FigmaMissionUi.Green);
            ConfigureGardenPage(root, body, y, footer, blocked ? 176 : 116, back);
        }

        public void StartFocusBasic()
        {
            if (!opened || !SaveActivePracticeDraft() || !RefreshCycle()) return;
            if (!RequireMissionPlant()) return;
            var state = Service.Snapshot;
            if (!state.MbctProgramChosen) { ShowFocusProgramSelection(); return; }
            var active = FocusMissionPolicy.Active(state, FocusMissionCatalog.BasicRole)
                ?? state.Sessions.LastOrDefault(a => FocusMissionPolicy.IsActive(a) && !a.FocusSupport && a.MissionId.StartsWith("course:", StringComparison.Ordinal));
            if (active != null) { SelectFocusSession(active.SessionId); return; }
            int order = FocusMissionPolicy.NextOrder(state);
            var mission = FocusMissionPolicy.MissionAt(order, state.MbctProgramId);
            if (mission == null) { ShowFocusProgram(state.MbctProgramId); return; }
            BeginFocus(mission.Id, order, false);
        }

        public void StartFocusAdditional() { if (opened) StartFocusAdditionalMission(RecommendedFocusMission(Service.Snapshot).Id); }
        public void StartFocusAdditionalMission(string missionId)
        {
            if (!opened || !SaveActivePracticeDraft() || !RefreshCycle()) return;
            if (!RequireMissionPlant()) return;
            var active = ActiveAdditionalMission(Service.Snapshot);
            if (active != null) { SelectFocusSession(active.SessionId); return; }
            // Ordinary navigation always opens the assigned card. Arbitrary P/M
            // selection belongs to the isolated development menu.
            BeginFocus(RecommendedFocusMission(Service.Snapshot).Id, 0, false);
        }
        void BeginFocus(string mission, int order, bool review, string program = null)
        {
            if (!RequireMissionPlant()) return;
            string id = Guid.NewGuid().ToString("N");
            var attribution = !review && order == 0 ? SelectedFocusRecommendation() : null;
            if (!Apply(Service.BeginFocusSession(id, mission, order, review, program, null, attribution))) return;
            focusVisibleRecommendation = null; focusRecommendationSignature = null;
            sessionId = id; ClearPracticeDraft(); ShowFocusPractice();
        }
        public void SelectFocusSession(string id)
        {
            if (!SaveActivePracticeDraft()) return;
            sessionId = id; var entry = Service.Snapshot.Sessions.FirstOrDefault(a => a.SessionId == id);
            if (entry == null) { ShowFocusHome(); return; }
            if (FocusMissionPolicy.IsActive(entry) && !RequireMissionPlant()) return;
            if (entry.Status == ParticipationState.Paused && !Apply(Service.ResumeSession(id))) return;
            if (FocusMissionPolicy.IsFocus(entry)) ShowFocusPractice(); else ShowSession();
        }
        public void StartFocusReview(string missionId, int order, string program = null)
        {
            if (!opened || !SaveActivePracticeDraft()) return;
            var definition = FocusMissionCatalog.Find(missionId);
            if (definition != null && Service.Snapshot.Sessions.Any(a => FocusMissionPolicy.IsFocus(a) && a.FocusRole == definition.Role && FocusMissionPolicy.IsActive(a)))
            {
                if (notice != null) { notice.text = "진행 중인 " + (definition.Role == FocusMissionCatalog.BasicRole ? "기본" : "추가") + " 미션을 마치면 복습할 수 있어요."; notice.gameObject.SetActive(true); notice.transform.SetAsLastSibling(); }
                return;
            }
            BeginFocus(missionId, order, true, program);
        }

        public void ShowFocusRecommendation() => ShowFocusRecommendationFor(null);
        public void ShowFocusRecommendationFor(string missionId) => ShowFocusRecommendationDetails(missionId,missionId==null);
        void ShowFocusRecommendationDetails(string missionId,bool recommended)
        {
            if (!opened || !SaveActivePracticeDraft()) return;
            var state = Service.Snapshot; var mission = RecommendedFocusMission(state);
            var active = ActiveAdditionalMission(state);
            if (active != null && !FocusMissionPolicy.IsFocus(active)) { SelectFocusSession(active.SessionId); return; }
            var root = BeginFigmaPage("focus-recommendation", () => ShowFocusRecommendationFor(mission.Id), FigmaMissionUi.Background, true);
            FigmaHeader(root, "추가 미션", "하루 한 가지, 내 속도로 이어가요.");
            var body = FigmaScroll(root, 132, 628, 584);
            var box = FigmaMissionUi.Box(body, "추가 미션 안내", 24, 8, 342, 280, FigmaMissionUi.Hex(mission.BackgroundHex), 24);
            FigmaMissionUi.Asset(box, mission.IconAsset, 24, 24, 64, 64);
            FigmaMissionUi.Label(box, mission.Title, 104, 28, 214, 96, 22, null, true);
            FigmaMissionUi.Label(box, mission.Overview, 24, 144, 294, 72, 18, FigmaMissionUi.Hex(mission.TextHex));
            FigmaMissionUi.Label(box, FocusMissionEffort(mission), 24, 236, 294, 28, 14, FigmaMissionUi.Muted);
            FigmaMissionUi.Label(body, "하루 1개를 마칠 수 있어요.\n미완료 미션은 다음 날에도 이어져요.", 24, 312, 342, 48, 16, FigmaMissionUi.Muted);
            bool completed = FocusMissionPolicy.AdditionalAvailability(state) != null;
            var button = FigmaMissionUi.Button(body, active != null ? "추가 미션 이어서 하기" : completed ? "오늘 완료했어요" : "추가 미션 시작하기", 24, 384, 342, 56,
                active != null ? (Action)(() => SelectFocusSession(active.SessionId)) : () => StartFocusAdditionalMission(mission.Id), FigmaMissionUi.Brown);
            button.interactable = active != null || !completed;
            FigmaMissionUi.Button(body, "오늘의 미션으로", 24, 456, 342, 48, ShowHome, Color.clear, FigmaMissionUi.Green, 14);
        }

        public void ShowFocusMissionLibrary()
        {
            ShowMissionDebugPicker();
        }

        public void ShowFocusCourse()
        {
            RestoreMissionDebug();
            if (!opened) { ShowGardenLoadFailure("course", "기록", ShowFocusCourse); return; }
            if (!SaveActivePracticeDraft()) return;
            var state = Service.Snapshot;
            var root = BeginFigmaPage("course", ShowFocusCourse, FigmaMissionUi.Background, true, "기록");
            FigmaHeader(root, "기록", "지금의 코스와 내가 남긴 기록");
            bool legacy = state.Sessions.Any(a => !FocusMissionPolicy.IsFocus(a) || FocusMissionPolicy.ProgramFor(a) == "legacy");
            var body = FigmaScroll(root, 132, 628, 440); float y = 8;
            if (state.MbctProgramChosen)
            {
                string program = state.MbctProgramId;
                int done = FocusMissionPolicy.CompletedCount(state, program), total = FocusMissionPolicy.TotalPracticesFor(program);
                int records = state.Sessions.Count(a => FocusMissionPolicy.IsFocus(a) && FocusMissionPolicy.ProgramFor(a) == program);
                const float courseHeight = 140;
                var row = FigmaMissionUi.Button(body, "", 24, y, 342, courseHeight, () => ShowFocusProgram(program), FigmaMissionUi.Soft, FigmaMissionUi.Ink);
                row.name = "Focus.CourseRecords." + program;
                FigmaMissionUi.Label(row.transform, done >= total ? FocusProgramTitle(program) + " · 완료" : "현재 : " + FocusProgramTitle(program), 36, 18, 270, 32, 22, null, true, TextAnchor.MiddleCenter);
                FigmaMissionUi.Label(row.transform, "›", 300, 18, 24, 32, 26, FigmaMissionUi.Green, false, TextAnchor.MiddleCenter);
                FigmaMissionUi.Label(row.transform, "완료한 실습 " + done + " / " + total + " · 저장 기록 " + records + "개", 16, 62, 310, 24, 15, FigmaMissionUi.Muted, false, TextAnchor.MiddleCenter);
                FigmaMissionUi.Label(row.transform, records > 0 ? "최근 기록부터 확인해요" : "첫 실천을 마치면 여기에 쌓여요", 16, 96, 310, 24, 15, FigmaMissionUi.Green, false, TextAnchor.MiddleCenter);
                y += courseHeight;
            }
            else
            {
                FigmaMissionUi.Label(body, "아직 선택한 코스가 없어요.", 24, y, 342, 48, 18, FigmaMissionUi.Muted, false, TextAnchor.MiddleCenter).name = "Focus.NoCurrentCourse";
                y += 48;
            }
            FigmaMissionUi.Button(body, "코스 선택·변경", 226, y + 4, 140, 44, ShowFocusProgramSelection, Color.clear, FigmaMissionUi.Green, 14).name = "Focus.ChangeProgram";
            y += 64;
            FigmaMissionUi.Button(body, "활동 일지", 24, y, 342, 56, ShowDailyEmotionHistory, Color.white, FigmaMissionUi.Green).name = "DailyEmotion.History"; y += 72;
            if (legacy) { FigmaMissionUi.Button(body, "이전 과정·예시 기록", 24, y, 342, 48, () => ShowFocusHistory(null, 0, "legacy"), Color.white, FigmaMissionUi.Green); y += 64; }
            FigmaMissionUi.Button(body, "식물 사진첩", 24, y, 342, 56, ShowPlantAlbum, Color.white, FigmaMissionUi.Green).name = "PlantAlbum.Open"; y += 72;
            body.sizeDelta = new Vector2(390, y + 8);
        }

        public void ShowFocusProgram(string program) => RenderCourseRecords(program);

        void RebuildFocusDisclosure(Action rebuild)
        {
            var previous = pageViewport == null ? null : pageViewport.GetComponent<ScrollRect>();
            float scrollY = previous?.content == null ? 0 : previous.content.anchoredPosition.y;
            rebuild(); Canvas.ForceUpdateCanvases();
            var scroll = pageViewport == null ? null : pageViewport.GetComponent<ScrollRect>();
            if (scroll?.content == null || scroll.viewport == null) return;
            scroll.content.anchoredPosition = new Vector2(0, Mathf.Clamp(scrollY, 0, Mathf.Max(0, scroll.content.rect.height - scroll.viewport.rect.height)));
            scroll.velocity = Vector2.zero;
        }

        // Older navigation links now open a course instead of a weekly record group.
        public void ShowFocusWeek(int week) => ShowFocusProgram(Service.Snapshot.MbctProgramId);

        public void ShowFocusOccurrence(string program, int order)
        {
            if (!opened || !SaveActivePracticeDraft()) return;
            var mission = FocusMissionPolicy.MissionAt(order, program); if (mission == null) return;
            var state = Service.Snapshot;
            var entries = state.Sessions.Where(a => FocusMissionPolicy.IsFocus(a) && FocusMissionPolicy.ProgramFor(a) == program && a.FocusOrder == order).OrderByDescending(a => a.SelectedUtc, StringComparer.Ordinal).ToArray();
            var root = BeginFigmaPage("focus-occurrence", () => ShowFocusOccurrence(program, order), FigmaMissionUi.Background, true, "기록");
            FigmaHeader(root, order + "회차", "");
            var back = FigmaMissionUi.Button(root, "‹ 코스 기록", 24, 0, 110, 38, () => ShowFocusProgram(program), Color.clear, FigmaMissionUi.Green, 14);
            var body = FigmaScroll(root, 110, 500, 400 + entries.Length * 112);
            float y = BuildFocusRecordCard(body, FocusProgramTitle(program) + " · " + mission.Title, mission.Overview, 0, FigmaMissionUi.Soft);
            if (entries.Length == 0) { FigmaMissionUi.Label(body, "아직 저장된 수행 기록이 없어요.", 24, y, 342, 48, 17, FigmaMissionUi.Muted); y += 64; }
            foreach (var entry in entries) { BuildFocusHistoryRow(body, entry, y); y += 112; }
            var active = entries.LastOrDefault(FocusMissionPolicy.IsActive);
            RectTransform footer = null;
            if (active != null)
            {
                footer = FigmaMissionUi.Rect(root, "Focus.OccurrenceActions", 0, 0, 390, 52);
                FigmaMissionUi.Button(footer, "저장한 단계 이어서 하기", 24, 0, 342, 52, () => SelectFocusSession(active.SessionId));
            }
            else { FigmaMissionUi.Label(body, "기본 미션은 선택한 코스의 순서대로 열려요.", 24, y, 342, 48, 14, FigmaMissionUi.Muted); y += 64; }
            ConfigureGardenPage(root, body, y, footer, footer == null ? 0 : 52, back);
        }

        public void ShowFocusHistory(string missionId = null, int order = 0, string program = null)
        {
            if (!opened || !SaveActivePracticeDraft()) return;
            if (missionId == null && program == null) { ShowFocusCourse(); return; }
            var state = Service.Snapshot; var definition = FocusMissionCatalog.Find(missionId);
            var list = state.Sessions.Where(a => (missionId == null || a.MissionId == (definition?.Id ?? missionId)) && (order == 0 || a.FocusOrder == order)
                && (program == null || (program == "legacy" ? !FocusMissionPolicy.IsFocus(a) || FocusMissionPolicy.ProgramFor(a) == "legacy" : FocusMissionPolicy.IsFocus(a) && FocusMissionPolicy.ProgramFor(a) == program)))
                .OrderByDescending(a => a.SelectedUtc, StringComparer.Ordinal).ToArray();
            var root = BeginFigmaPage("focus-history", () => ShowFocusHistory(missionId, order, program), FigmaMissionUi.Background, true, "기록");
            FigmaHeader(root, definition == null ? (program == null ? "저장된 기록" : FocusProgramTitle(program) + " 기록") : "수행 기록", "");
            var back = FigmaMissionUi.Button(root, "‹ 기록", 24, 0, 110, 38, ShowFocusCourse, Color.clear, FigmaMissionUi.Green, 14);
            back.name = "Focus.HistoryBack";
            var body = FigmaScroll(root, 110, 500, 200 + list.Length * 112);
            float top = definition == null ? 8 : 52;
            if (definition != null) FigmaMissionUi.Label(body, definition.Title, 24, 4, 342, 40, 20, FigmaMissionUi.Ink, true, TextAnchor.MiddleCenter);
            if (list.Length == 0) FigmaMissionUi.Label(body, "아직 저장된 수행 기록이 없어요.", 24, top + 32, 342, 64, 18, FigmaMissionUi.Muted, false, TextAnchor.MiddleCenter);
            for (int i = 0; i < list.Length; i++) BuildFocusHistoryRow(body, list[i], top + i * 112);
            ConfigureGardenPage(root, body, list.Length == 0 ? top + 112 : top + list.Length * 112, back: back);
        }

        void BuildFocusHistoryRow(Transform root, ActivitySession entry, float y)
        {
            bool focus = FocusMissionPolicy.IsFocus(entry);
            var row = FigmaMissionUi.Button(root, "", 24, y, 342, 96, () => ShowFocusRecord(entry.SessionId), Color.white, FigmaMissionUi.Ink);
            row.name = "Focus.Record." + entry.SessionId;
            FigmaMissionUi.Label(row.transform, FocusSessionTitle(entry), 16, 10, 310, 40, 16, null, true);
            string group = focus ? FocusProgramTitle(FocusMissionPolicy.ProgramFor(entry)) + (entry.FocusRole == FocusMissionCatalog.BasicRole ? " " + entry.FocusOrder + "회" : "") : "이전 기록";
            FigmaMissionUi.Label(row.transform, group + " · " + LocalTime(entry.EndedUtc ?? entry.SelectedUtc), 16, 50, 310, 22, 14, FigmaMissionUi.Muted);
            FigmaMissionUi.Label(row.transform, StateLabel(entry.Status) + (entry.FocusReview ? " · 복습" : ""), 16, 72, 310, 22, 14, FigmaMissionUi.Green);
        }

        public void ShowFocusRecord(string id)
        {
            if (!opened || !SaveActivePracticeDraft()) return;
            var entry = Service.Snapshot.Sessions.FirstOrDefault(a => a.SessionId == id);
            if (entry == null) { ShowFocusCourse(); return; }
            if (!FocusMissionPolicy.IsFocus(entry)) { ShowLegacyRecord(id); return; }
            bool focus = FocusMissionPolicy.IsFocus(entry); string program = focus ? FocusMissionPolicy.ProgramFor(entry) : "legacy";
            var root = BeginFigmaPage("focus-record", () => ShowFocusRecord(id), FigmaMissionUi.Background, true, "기록");
            FigmaHeader(root, "미션 기록", "");
            var back = FigmaMissionUi.Button(root, "기록 목록", 24, 0, 110, 38,
                () => ReturnToActivityJournalIfSource(() => ShowFocusHistory(null, 0, program)), Color.clear, FigmaMissionUi.Green, 14);
            back.name = "Focus.RecordBack";
            var body = FigmaScroll(root, 110, 500, 628); float y = 8;
            y = BuildFocusRecordCard(body, "실습", FocusSessionTitle(entry), y, FigmaMissionUi.Soft);
            string group = (focus ? FocusProgramTitle(program) + (entry.FocusRole == FocusMissionCatalog.BasicRole ? " · " + entry.FocusOrder + "회차" : "") : "이전 미션") + " · " + StateLabel(entry.Status);
            int elapsed = entry.FocusStepRecords?.Sum(step => step.ElapsedSeconds) ?? 0;
            string when = focus ? LocalTime(entry.EndedUtc ?? entry.SelectedUtc) : "시작 · " + LocalTime(entry.StartedUtc) + "\n마침 · " + (entry.EndedUtc == null ? "진행 중" : LocalTime(entry.EndedUtc));
            if(entry.FocusSupport)when+="\n다른 목적의 활동으로 전환 · 원 코스 회차는 유지";
            if(entry.FocusEndReason=="skipped"||entry.FocusEndReason=="stopped")when+="\n미완료로 마침 · 실제 수행만 기록";
            if (elapsed > 0) when += "\n집중한 시간 · " + FocusRecordDuration(elapsed);
            y = BuildFocusRecordCard(body, group, when, y, FigmaMissionUi.Soft);
            y = BuildMissionPhotoRecord(body,entry,y);
            if (!string.IsNullOrEmpty(entry.Mood)) y = BuildFocusRecordCard(body, "그때의 감정", entry.Mood, y);
            if (!string.IsNullOrEmpty(entry.Note)) y = BuildFocusRecordCard(body, "내가 남긴 한 줄", entry.Note, y);
            if (focus)
            {
                foreach (var step in entry.FocusStepRecords.OrderBy(r => r.Step).Where(r => !r.Skipped && !string.IsNullOrEmpty(r.Value)))
                    y = BuildFocusRecordCard(body, step.Title, step.Value.StartsWith("DRAW1|", StringComparison.Ordinal) ? "저장된 그림" : step.Value, y, null, step.Value.StartsWith("DRAW1|", StringComparison.Ordinal) ? step.Value : null);
            }
            else foreach (var answer in entry.Answers ?? new List<PracticeAnswer>()) y = BuildFocusRecordCard(body, AnswerLabel(answer.Key), answer.Value, y);
            if (entry.MissionId == "free-mission:M15" && entry.WalkingGoalSteps > 0)
                y = BuildFocusRecordCard(body, "시작할 때 정한 걷기 목표", entry.WalkingGoalSteps.ToString("N0") + "걸음\n걸음 확인 구간\n" + LocalTime(entry.WalkingQueryStartUtc) + " ~ " + LocalTime(entry.WalkingQueryEndUtc) + "\n" + WalkingPersonalization.Explanation(entry), y);
            bool hasPersonalRecord = !string.IsNullOrEmpty(entry.Mood) || !string.IsNullOrEmpty(entry.Note) || (entry.FocusStepRecords != null && entry.FocusStepRecords.Any(r => !r.Skipped && !string.IsNullOrEmpty(r.Value))) || (entry.Answers != null && entry.Answers.Count > 0);
            if (!hasPersonalRecord) y = BuildFocusRecordCard(body, "실천을 남겼어요", "감정이나 글을 남기지 않아도 수행 내용은 저장돼요.", y);
            if (focus && entry.Status == ParticipationState.RewardCommitted && entry.RewardAmount > 0)
                y = BuildFocusRecordCard(body, "이전에 받은 코인", "+" + entry.RewardAmount, y);
            if (focus)
            {
                bool expanded = focusExpandedRecordSteps.Contains(id);
                var toggle = FigmaMissionUi.Button(body, expanded ? "단계별 수행 접기  −" : "단계별 수행 보기  +", 24, y, 342, 56, () =>
                {
                    if (!focusExpandedRecordSteps.Add(id)) focusExpandedRecordSteps.Remove(id);
                    RebuildFocusDisclosure(() => ShowFocusRecord(id));
                }, Color.white, FigmaMissionUi.Green);
                toggle.name = "Focus.RecordStepsToggle"; y += 72;
                if (expanded)
                {
                    y = BuildFocusRecordCard(body, "수행 시간", "시작 · " + LocalTime(entry.StartedUtc) + "\n마침 · " + (entry.EndedUtc == null ? "진행 중" : LocalTime(entry.EndedUtc)), y);
                    var definition = FocusMissionCatalog.FindForSession(entry);
                    foreach (var step in entry.FocusStepRecords.OrderBy(r => r.Step))
                    {
                        bool unselectedBranch = definition != null && step.Skipped && !FocusMissionCatalog.IsApplicable(definition, entry.FocusStepRecords, step.Step);
                        bool drawing = step.Value != null && step.Value.StartsWith("DRAW1|", StringComparison.Ordinal);
                        string value = unselectedBranch ? "선택하지 않은 경로" : drawing ? "그림 저장됨 · 위에서 확인" : step.Skipped ? "입력 건너뜀" : string.IsNullOrEmpty(step.Value) ? "행동 확인" : step.Value;
                        string time = step.ElapsedSeconds > 0 ? "\n집중 타이머 기록 · " + FocusRecordDuration(step.ElapsedSeconds) : "";
                        if(step.EndReason=="too_long")time+="\n너무 길어요 · 여기까지 진행";
                        if(step.RecommendedSeconds>0)time+="\n시작 시 추천 · "+FocusRecordDuration(step.RecommendedSeconds);
                        y = BuildFocusRecordCard(body, step.Title, step.Prompt + "\n" + (string.IsNullOrEmpty(step.ConfirmedUtc) ? "작성 중" : "확인 · " + LocalTime(step.ConfirmedUtc)) + "\n" + value + time, y);
                    }
                    y = BuildFocusRecordCard(body, "선택 기록", "감정 · " + (string.IsNullOrEmpty(entry.Mood) ? "선택하지 않음" : entry.Mood) + "\n한 줄 기록 · " + (string.IsNullOrEmpty(entry.Note) ? "남긴 기록 없음" : entry.Note), y);
                }
            }
            RectTransform footer = null;
            if (FocusMissionPolicy.IsActive(entry))
            {
                footer = FigmaMissionUi.Rect(root, "Focus.RecordActions", 0, 0, 390, 52);
                FigmaMissionUi.Button(footer, "저장한 단계 이어서 하기", 24, 0, 342, 52, () => SelectFocusSession(id));
            }
            ConfigureGardenPage(root, body, y, footer, footer == null ? 0 : 52, back);
        }

        static string FocusRecordDuration(int seconds) => (seconds / 60).ToString("00") + ":" + (seconds % 60).ToString("00");
        static float BuildFocusRecordCard(Transform root, string title, string value, float y, Color? fill = null, string drawingValue = null)
        {
            var card = FigmaMissionUi.Box(root, "기록 · " + title, 24, y, 342, 100, fill ?? Color.white, 16);
            var heading = FigmaMissionUi.Label(card, title, 16, 16, 310, 24, 14, FigmaMissionUi.Muted, true);
            float headingHeight = Mathf.Max(24, heading.preferredHeight); heading.rectTransform.sizeDelta = new Vector2(310, headingHeight);
            var label = FigmaMissionUi.Label(card, value ?? "", 16, 24 + headingHeight, 310, 48, 16, FigmaMissionUi.Ink);
            float textHeight = Mathf.Max(24, label.preferredHeight); label.rectTransform.sizeDelta = new Vector2(310, textHeight);
            float height = 40 + headingHeight + textHeight;
            if (drawingValue != null)
            {
                var paper = FigmaMissionUi.Box(card, "저장된 그림 종이", 16, height - 8, 310, 232, FigmaMissionUi.Background, 12);
                var drawingRect = FigmaMissionUi.Rect(paper, "저장된 그림", 8, 8, 294, 216);
                drawingRect.gameObject.AddComponent<CanvasRenderer>();
                var drawing = drawingRect.gameObject.AddComponent<FocusDrawingSurface>();
                drawing.ReadOnly = true; drawing.raycastTarget = false; drawing.color = FigmaMissionUi.Green; drawing.Load(drawingValue); height += 240;
            }
            card.sizeDelta = new Vector2(342, height); return y + height + 16;
        }
    }
}
