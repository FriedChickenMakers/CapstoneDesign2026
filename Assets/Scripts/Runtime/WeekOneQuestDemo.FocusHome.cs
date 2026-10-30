using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using CapstoneDesign.Runtime.LocalState;

namespace CapstoneDesign.Runtime
{
    public sealed partial class WeekOneQuestDemo
    {
        static readonly string[] FocusPrograms = { "2w", "4w", "8w" };
        static readonly string[] FocusLibraryFilters = { "전체", "감각", "몸 돌봄", "마음 기록" };
        readonly HashSet<string> focusExpandedCurricula = new HashSet<string>();
        readonly HashSet<string> focusExpandedRecordSteps = new HashSet<string>();
        string focusLibraryFilter = "전체";
        public void ShowHome() => ShowFocusHome();
        public void ShowCourse() => ShowFocusCourse();
        public void ShowFree() => ShowFocusRecommendation();
        public void ShowProgramSelection() => ShowFocusProgramSelection();
        public void ShowRecords() => ShowFocusCourse();
        public void ShowRecord(string id) => ShowFocusRecord(id);

        static string FocusProgramTitle(string program) => program == "legacy" ? "이전 예시 과정" : program == "additional" ? "추천·자유 미션" : MbctPolicy.ProgramWeeks(program) + "주 코스";
        static string FocusProgramDescription(string program) => program == "2w" ? "감각부터 나를 돌보는 계획까지" : program == "4w" ? "몸·생각·행동을 차근차근 살피기" : "다양한 상황에서 반복하며 익히기";
        static string FocusCode(ActivitySession entry) => FocusMissionCatalog.Find(entry.MissionId)?.Code ?? "";
        static string FocusSessionTitle(ActivitySession entry) => entry.MissionTitleSnapshot ?? FocusMissionCatalog.Find(entry.MissionId)?.Title ?? MindfulnessContent.FindMission(entry.MissionId)?.title ?? entry.MissionId;

        FocusMissionDefinition RecommendedFocusMission(GardenState state)
        {
            var active = FocusMissionPolicy.Active(state, FocusMissionCatalog.AdditionalRole);
            if (active != null) return FocusMissionCatalog.Find(active.MissionId);
            var water = FocusMissionCatalog.Find("M03");
            if (water != null && !FocusMissionPolicy.AdditionalCompletedToday(state, water.Id)) return water;
            return FocusMissionCatalog.Additional.FirstOrDefault(m => !FocusMissionPolicy.AdditionalCompletedToday(state, m.Id)) ?? water ?? FocusMissionCatalog.Additional.First();
        }

        public void ShowFocusHome()
        {
            if (opened && !SaveActivePracticeDraft()) return;
            ClearPracticeDraft();
            if (opened && !Service.Snapshot.MbctProgramChosen) { ShowFocusProgramSelection(); return; }
            var root = BeginFigmaPage("home", ShowFocusHome, FigmaMissionUi.Background, true);
            FigmaHeader(root, "미션", "오늘은 한 가지부터, 천천히.");
            if (!opened) { FigmaMissionUi.Label(root, "저장된 내용을 불러오지 못했어요.\n기존 기록을 보존하고 있습니다.", 24, 170, 342, 100); return; }
            var state = Service.Snapshot; string program = state.MbctProgramId;
            int total = FocusMissionPolicy.TotalPracticesFor(program), done = FocusMissionPolicy.CompletedCount(state, program);
            var active = FocusMissionPolicy.Active(state, FocusMissionCatalog.BasicRole, program)
                ?? state.Sessions.LastOrDefault(a => FocusMissionPolicy.IsActive(a) && a.MissionId.StartsWith("course:", StringComparison.Ordinal));
            var mission = active == null ? FocusMissionPolicy.MissionAt(FocusMissionPolicy.NextOrder(state, program), program) : FocusMissionCatalog.FindForSession(active);
            bool finished = done >= total; string unavailable = FocusMissionPolicy.Availability(state, program);
            bool doneToday = state.Sessions.Any(a => FocusMissionPolicy.IsFocus(a) && a.FocusRole == FocusMissionCatalog.BasicRole && !a.FocusReview && a.Status == ParticipationState.RewardCommitted && a.InstanceId == state.LastCycleId);
            var body = FigmaScroll(root, 132, 628, 616);
            var card = FigmaMissionUi.Box(body, "기본 미션", 24, 8, 342, 248, FigmaMissionUi.Green, 24);
            FigmaMissionUi.Asset(card, "FocusMissions/Main/basic-leaf", 24, 20, 24, 24);
            string courseLabel = active != null && (!FocusMissionPolicy.IsFocus(active) || FocusMissionPolicy.ProgramFor(active) != program)
                ? (FocusMissionPolicy.IsFocus(active) ? FocusProgramTitle(FocusMissionPolicy.ProgramFor(active)) : "이전 미션 이어가기")
                : FocusProgramTitle(program) + " · " + done + " / " + total;
            FigmaMissionUi.Label(card, courseLabel, 56, 20, 262, 24, 14, Color.white, true);
            FigmaMissionUi.Label(card, active != null ? FocusCode(active) + " · " + FocusSessionTitle(active) : finished ? "과정을 마쳤어요" : mission.Code + " · " + mission.Title, 24, 60, 294, 60, 22, Color.white, true);
            string context = active != null ? "멈춘 곳부터 이어가요." : finished ? "쌓인 실천을 돌아봐요." : doneToday ? "오늘의 기본 미션을 마쳤어요." : unavailable != null ? "이번 주 실습을 마쳤어요. 다음 주에 이어가요." : "지금 나에게 집중하는 시간";
            FigmaMissionUi.Label(card, context, 24, 128, 294, 40, 16, Color.white);
            string action = active != null ? "기본 미션 이어서 하기" : finished ? "과정 기록 보기" : doneToday ? "오늘 완료했어요" : unavailable != null ? "다음 주에 이어가요" : "기본 미션 시작하기";
            var start = FigmaMissionUi.Button(card, action, 24, 180, 294, 48, active == null && finished ? (Action)(() => ShowFocusProgram(program)) : StartFocusBasic, Color.white, FigmaMissionUi.Green);
            start.interactable = finished || active != null || unavailable == null;
            FigmaMissionUi.Label(body, "추천 미션", 24, 280, 180, 28, 18, null, true);
            FigmaMissionUi.Label(body, "코스와 별개 · 선택", 196, 280, 170, 28, 14, FigmaMissionUi.Muted, false, TextAnchor.MiddleRight);
            BuildRecommendationCard(body, state, 320);
            FigmaMissionUi.Button(body, "추천·자유 미션 모두 보기", 24, 488, 342, 48, ShowFocusMissionLibrary, Color.white, FigmaMissionUi.Brown);
            FigmaMissionUi.Button(body, "코스 변경", 24, 552, 342, 48, ShowFocusProgramSelection, Color.clear, FigmaMissionUi.Green, 14);
        }

        void BuildRecommendationCard(Transform root, GardenState state, float y)
        {
            var mission = RecommendedFocusMission(state);
            var active = FocusMissionPolicy.Active(state, FocusMissionCatalog.AdditionalRole);
            bool completed = FocusMissionPolicy.AdditionalCompletedToday(state, mission.Id);
            var box = FigmaMissionUi.Box(root, "코스와 독립적인 추천 미션", 24, y, 342, 152, FigmaMissionUi.Warm, 20);
            FigmaMissionUi.Asset(box, mission.IconAsset, 16, 20, 44, 44);
            FigmaMissionUi.Label(box, mission.Code + " · " + mission.Title, 72, 16, 254, 48, 18, null, true);
            FigmaMissionUi.Label(box, active != null ? "멈춘 곳부터 이어가요" : FocusMissionEffort(mission), 72, 66, 254, 22, 14, FigmaMissionUi.Brown);
            FigmaMissionUi.Button(box, active != null ? "추가 미션 이어서 하기" : completed ? "오늘 기록 보기" : "추천 미션 보기", 16, 96, 310, 48,
                active != null ? (Action)(() => SelectFocusSession(active.SessionId)) : completed ? () => ShowFocusHistory(mission.Id, 0, "additional") : () => ShowFocusRecommendationFor(mission.Id), Color.white, FigmaMissionUi.Brown);
        }

        static string FocusLibraryCategory(FocusMissionDefinition mission)
        {
            switch (mission.Code)
            {
                case "M02": case "M08": case "M12": case "M14": return "감각";
                case "M07": case "M10": case "M11": case "M13": return "마음 기록";
                default: return "몸 돌봄";
            }
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
            FigmaHeader(root, state.MbctProgramChosen ? "내게 맞는 코스" : "내 속도로 시작해요", "기간을 정하고, 하루 한 가지씩 이어가요.");
            var previous = state.Sessions.LastOrDefault(a => FocusMissionPolicy.IsActive(a) && a.MissionId.StartsWith("course:", StringComparison.Ordinal));
            var body = FigmaScroll(root, 132, 628, previous == null ? 584 : 648);
            var independent = FigmaMissionUi.Button(body, "코스 없이 추천 미션 하기", 24, 8, 342, 48, ShowFocusRecommendation, FigmaMissionUi.Warm, FigmaMissionUi.Brown);
            independent.name = "추천 미션 보기";
            FigmaMissionUi.Label(body, "꾸준히 이어가고 싶다면", 24, 80, 342, 28, 18, null, true);
            for (int i = 0; i < FocusPrograms.Length; i++)
            {
                string program = FocusPrograms[i]; int done = FocusMissionPolicy.CompletedCount(state, program);
                bool selected = state.MbctProgramChosen && state.MbctProgramId == program;
                var row = FigmaMissionUi.Button(body, "", 24, 120 + i * 112, 342, 96, () => ShowFocusProgramConfirmation(program), selected ? FigmaMissionUi.Soft : Color.white, FigmaMissionUi.Ink);
                row.name = "Focus.SelectProgram." + program;
                FigmaMissionUi.Label(row.transform, selected ? "●" : "○", 16, 20, 24, 28, 22, FigmaMissionUi.Green, true, TextAnchor.MiddleCenter);
                FigmaMissionUi.Label(row.transform, FocusProgramTitle(program) + (selected ? " · 현재 코스" : ""), 52, 12, 274, 28, 20, null, true);
                FigmaMissionUi.Label(row.transform, FocusProgramDescription(program), 52, 44, 274, 24, 14, FigmaMissionUi.Muted);
                FigmaMissionUi.Label(row.transform, FocusMissionPolicy.TotalPracticesFor(program) + "회 실습" + (done > 0 ? " · " + done + "회 완료" : ""), 52, 68, 274, 24, 14, FigmaMissionUi.Green);
            }
            float y = 464;
            if (previous != null) { FigmaMissionUi.Button(body, "진행하던 기본 미션 이어가기", 24, y, 342, 48, () => SelectFocusSession(previous.SessionId)); y += 64; }
            FigmaMissionUi.Label(body, "코스를 바꿔도 기록과 정원은 유지돼요.", 24, y, 342, 28, 14, FigmaMissionUi.Muted);
            FigmaMissionUi.Button(body, "추천·자유 미션 모두 보기", 24, y + 40, 342, 48, ShowFocusMissionLibrary, Color.clear, FigmaMissionUi.Brown);
        }

        public void ShowFocusProgramConfirmation(string program)
        {
            if (!opened || !MbctPolicy.IsProgram(program) || !SaveActivePracticeDraft()) return;
            var state = Service.Snapshot;
            var root = BeginFigmaPage("focus-program-confirm", () => ShowFocusProgramConfirmation(program), FigmaMissionUi.Background, true);
            FigmaHeader(root, FocusProgramTitle(program), "내 속도로 이어가는 마음챙김");
            var body = FigmaScroll(root, 132, 628, 616);
            var box = FigmaMissionUi.Box(body, "코스 안내", 24, 8, 342, 200, FigmaMissionUi.Soft, 24);
            FigmaMissionUi.Label(box, FocusProgramDescription(program), 24, 20, 294, 56, 22, null, true);
            FigmaMissionUi.Label(box, "하루 기본 미션 1개 · 주 최대 6개\n총 " + FocusMissionPolicy.TotalPracticesFor(program) + "회 실습 · 쉬는 날은 자유롭게", 24, 88, 294, 56, 16, FigmaMissionUi.Green);
            FigmaMissionUi.Label(box, "놓친 날에도 진도는 그대로예요.", 24, 160, 294, 24, 14, FigmaMissionUi.Muted);
            int done = FocusMissionPolicy.CompletedCount(state, program);
            FigmaMissionUi.Label(body, done > 0 ? "이 코스에서 " + done + "회 실습을 마쳤어요.\n저장된 진도부터 이어갑니다." : "코스를 바꿔도 이전 기록은 남아요.\n추천 미션은 코스와 별개로 수행해요.", 24, 232, 342, 64, 16, FigmaMissionUi.Muted);
            bool current = state.MbctProgramChosen && state.MbctProgramId == program;
            var previous = state.Sessions.LastOrDefault(a => FocusMissionPolicy.IsActive(a) && a.MissionId.StartsWith("course:", StringComparison.Ordinal));
            bool blocked = previous != null && !current;
            if (blocked)
            {
                FigmaMissionUi.Label(body, "진행 중인 미션을 먼저 마쳐주세요.\n마친 뒤 코스를 바꿀 수 있어요.", 24, 320, 342, 56, 16, FigmaMissionUi.Brown);
                FigmaMissionUi.Button(body, "진행하던 기본 미션 이어가기", 24, 392, 342, 48, () => SelectFocusSession(previous.SessionId));
            }
            float actionY = blocked ? 456 : 328;
            var select = FigmaMissionUi.Button(body, done >= FocusMissionPolicy.TotalPracticesFor(program) ? "완료한 코스 보기" : done > 0 ? "이 코스 이어서 하기" : "이 코스로 시작", 24, actionY, 342, 56, () =>
            {
                if (!Apply(Service.SelectMbctProgram(program))) return;
                coursePageInitialized = false; ShowFocusHome();
            });
            select.name = "Focus.ConfirmProgram." + program; select.interactable = !blocked;
            FigmaMissionUi.Button(body, "다른 코스 보기", 24, actionY + 72, 342, 48, ShowFocusProgramSelection, Color.white, FigmaMissionUi.Green);
        }

        public void StartFocusBasic()
        {
            if (!opened || !SaveActivePracticeDraft() || !RefreshCycle()) return;
            var state = Service.Snapshot;
            if (!state.MbctProgramChosen) { ShowFocusProgramSelection(); return; }
            var active = FocusMissionPolicy.Active(state, FocusMissionCatalog.BasicRole)
                ?? state.Sessions.LastOrDefault(a => FocusMissionPolicy.IsActive(a) && a.MissionId.StartsWith("course:", StringComparison.Ordinal));
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
            var active = FocusMissionPolicy.Active(Service.Snapshot, FocusMissionCatalog.AdditionalRole);
            if (active != null) { SelectFocusSession(active.SessionId); return; }
            BeginFocus(missionId, 0, false);
        }
        void BeginFocus(string mission, int order, bool review, string program = null)
        {
            string id = Guid.NewGuid().ToString("N");
            if (!Apply(Service.BeginFocusSession(id, mission, order, review, program))) return;
            sessionId = id; ClearPracticeDraft(); ShowFocusPractice();
        }
        public void SelectFocusSession(string id)
        {
            if (!SaveActivePracticeDraft()) return;
            sessionId = id; var entry = Service.Snapshot.Sessions.FirstOrDefault(a => a.SessionId == id);
            if (entry == null) { ShowFocusHome(); return; }
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
        public void ShowFocusRecommendationFor(string missionId)
        {
            if (!opened || !SaveActivePracticeDraft()) return;
            var state = Service.Snapshot; var mission = FocusMissionCatalog.Find(missionId) ?? RecommendedFocusMission(state);
            if (mission.Role != FocusMissionCatalog.AdditionalRole) { ShowFocusMissionLibrary(); return; }
            var root = BeginFigmaPage("focus-recommendation", () => ShowFocusRecommendationFor(mission.Id), FigmaMissionUi.Background, true);
            FigmaHeader(root, "추천 미션", "코스와 상관없이, 원하는 만큼.");
            var body = FigmaScroll(root, 132, 628, 584);
            var box = FigmaMissionUi.Box(body, "추천 미션 안내", 24, 8, 342, 280, FigmaMissionUi.Hex(mission.BackgroundHex), 24);
            FigmaMissionUi.Asset(box, mission.IconAsset, 24, 24, 64, 64);
            FigmaMissionUi.Label(box, mission.Code, 104, 28, 214, 24, 14, FigmaMissionUi.Ink, true);
            FigmaMissionUi.Label(box, mission.Title, 104, 60, 214, 64, 22, null, true);
            FigmaMissionUi.Label(box, mission.Overview, 24, 144, 294, 72, 18, FigmaMissionUi.Hex(mission.TextHex));
            FigmaMissionUi.Label(box, FocusMissionEffort(mission), 24, 236, 294, 28, 14, FigmaMissionUi.Muted);
            FigmaMissionUi.Label(body, "추천 미션의 기록은 코스와 별도로 남아요.", 24, 312, 342, 48, 16, FigmaMissionUi.Muted);
            var active = FocusMissionPolicy.Active(state, FocusMissionCatalog.AdditionalRole);
            bool completed = FocusMissionPolicy.AdditionalCompletedToday(state, mission.Id);
            var button = FigmaMissionUi.Button(body, active != null ? "추가 미션 이어서 하기" : completed ? "오늘 완료했어요" : "추가 미션 시작하기", 24, 384, 342, 56,
                active != null ? (Action)(() => SelectFocusSession(active.SessionId)) : () => StartFocusAdditionalMission(mission.Id), FigmaMissionUi.Brown);
            button.interactable = active != null || !completed;
            FigmaMissionUi.Button(body, "추천·자유 미션 모두 보기", 24, 456, 342, 48, ShowFocusMissionLibrary, Color.white, FigmaMissionUi.Brown);
            FigmaMissionUi.Button(body, "미션 창으로 돌아가기", 24, 520, 342, 48, ShowHome, Color.clear, FigmaMissionUi.Green, 14);
        }

        public void ShowFocusMissionLibrary()
        {
            focusLibraryFilter = "전체";
            RenderFocusMissionLibrary();
        }

        void RenderFocusMissionLibrary()
        {
            if (!opened || !SaveActivePracticeDraft()) return;
            var state = Service.Snapshot;
            var missions = FocusMissionCatalog.Additional.Where(m => focusLibraryFilter == "전체" || FocusLibraryCategory(m) == focusLibraryFilter).ToArray();
            var root = BeginFigmaPage("focus-library", RenderFocusMissionLibrary, FigmaMissionUi.Background, true);
            FigmaHeader(root, "추천·자유 미션", "지금 마음이 가는 활동을 골라요.");
            var body = FigmaScroll(root, 132, 628, 160 + missions.Length * 104);
            for (int i = 0; i < FocusLibraryFilters.Length; i++)
            {
                string filter = FocusLibraryFilters[i]; bool selected = focusLibraryFilter == filter;
                var button = FigmaMissionUi.Button(body, filter, 24 + i * 88, 8, 78, 48, () => { focusLibraryFilter = filter; RenderFocusMissionLibrary(); }, selected ? FigmaMissionUi.Green : Color.white, selected ? Color.white : FigmaMissionUi.Green, 14);
                button.name = "Focus.LibraryFilter." + filter;
            }
            for (int i = 0; i < missions.Length; i++)
            {
                var mission = missions[i]; bool completed = FocusMissionPolicy.AdditionalCompletedToday(state, mission.Id);
                var row = FigmaMissionUi.Button(body, "", 24, 72 + i * 104, 342, 88, completed ? (Action)(() => ShowFocusHistory(mission.Id, 0, "additional")) : () => ShowFocusRecommendationFor(mission.Id), Color.white, FigmaMissionUi.Ink);
                row.name = "Focus.FreeMission." + mission.Code;
                FigmaMissionUi.Asset(row.transform, mission.IconAsset, 12, 20, 44, 44);
                FigmaMissionUi.Label(row.transform, mission.Code + " · " + mission.Title, 68, 12, 258, 40, 16, null, true);
                FigmaMissionUi.Label(row.transform, completed ? "✓ 오늘 완료 · 기록 보기" : FocusMissionEffort(mission), 68, 58, 258, 24, 14, completed ? FigmaMissionUi.Green : FigmaMissionUi.Muted);
            }
            FigmaMissionUi.Button(body, "미션 창으로 돌아가기", 24, 88 + missions.Length * 104, 342, 48, ShowHome, Color.clear, FigmaMissionUi.Green);
        }

        public void ShowFocusCourse()
        {
            if (!opened || !SaveActivePracticeDraft()) return;
            var state = Service.Snapshot;
            var root = BeginFigmaPage("course", ShowFocusCourse, FigmaMissionUi.Background, true, "내 과정");
            FigmaHeader(root, "내 과정", "내가 남긴 실천을 코스별로 돌아봐요.");
            bool legacy = state.Sessions.Any(a => !FocusMissionPolicy.IsFocus(a) || FocusMissionPolicy.ProgramFor(a) == "legacy");
            var body = FigmaScroll(root, 132, 628, 700); float y = 8;
            var programs = FocusPrograms.OrderByDescending(p => state.MbctProgramChosen && state.MbctProgramId == p)
                .ThenByDescending(p => state.Sessions.Any(a => FocusMissionPolicy.IsFocus(a) && FocusMissionPolicy.ProgramFor(a) == p)).ToArray();
            foreach (string program in programs)
            {
                int done = FocusMissionPolicy.CompletedCount(state, program), total = FocusMissionPolicy.TotalPracticesFor(program);
                bool current = state.MbctProgramChosen && state.MbctProgramId == program;
                int records = state.Sessions.Count(a => FocusMissionPolicy.IsFocus(a) && FocusMissionPolicy.ProgramFor(a) == program);
                float height = current ? 112 : records > 0 ? 96 : 72;
                var row = FigmaMissionUi.Button(body, "", 24, y, 342, height, () => ShowFocusProgram(program), current ? FigmaMissionUi.Soft : Color.white, FigmaMissionUi.Ink);
                row.name = "Focus.CourseRecords." + program;
                FigmaMissionUi.Label(row.transform, FocusProgramTitle(program) + (current ? " · 현재 코스" : ""), 16, 12, 282, 30, current ? 22 : 18, null, true);
                FigmaMissionUi.Label(row.transform, "›", 300, 16, 24, 32, 26, FigmaMissionUi.Green, false, TextAnchor.MiddleCenter);
                string detail = current || records > 0 ? "완료한 실습 " + done + " / " + total + " · 저장 기록 " + records + "개" : "아직 남긴 기록이 없어요";
                FigmaMissionUi.Label(row.transform, detail, 16, current ? 52 : 44, 310, 24, 14, FigmaMissionUi.Muted);
                if (current) FigmaMissionUi.Label(row.transform, records > 0 ? "최근 기록부터 확인해요" : "첫 실천을 마치면 여기에 쌓여요", 16, 80, 310, 24, 14, FigmaMissionUi.Green);
                y += height + 16;
            }
            FigmaMissionUi.Button(body, "추천·자유 미션 기록", 24, y + 8, 342, 56, () => ShowFocusHistory(null, 0, "additional"), FigmaMissionUi.Warm, FigmaMissionUi.Brown); y += 80;
            if (legacy) { FigmaMissionUi.Button(body, "이전 과정·예시 기록", 24, y, 342, 48, () => ShowFocusHistory(null, 0, "legacy"), Color.white, FigmaMissionUi.Green); y += 64; }
            FigmaMissionUi.Button(body, "코스 선택·변경", 24, y, 342, 48, ShowFocusProgramSelection, Color.white, FigmaMissionUi.Green); y += 64;
            FigmaMissionUi.Button(body, "미션 창으로 돌아가기", 24, y, 342, 48, ShowHome, Color.clear, FigmaMissionUi.Green, 14); y += 64;
            body.sizeDelta = new Vector2(390, Mathf.Max(628, y));
        }

        public void ShowFocusProgram(string program)
        {
            if (!opened || !MbctPolicy.IsProgram(program) || !SaveActivePracticeDraft()) return;
            var state = Service.Snapshot; int total = FocusMissionPolicy.TotalPracticesFor(program), next = FocusMissionPolicy.NextOrder(state, program);
            var entries = state.Sessions.Where(a => FocusMissionPolicy.IsFocus(a) && FocusMissionPolicy.ProgramFor(a) == program)
                .OrderByDescending(a => a.SelectedUtc, StringComparer.Ordinal).ToArray();
            var root = BeginFigmaPage("focus-course", () => ShowFocusProgram(program), FigmaMissionUi.Background, true, "내 과정");
            FigmaHeader(root, FocusProgramTitle(program), "저장된 실천과 내가 남긴 기록");
            var body = FigmaScroll(root, 132, 628, 628);
            var summary = FigmaMissionUi.Box(body, "코스 기록 요약", 24, 8, 342, 96, FigmaMissionUi.Soft, 20);
            FigmaMissionUi.Label(summary, "완료한 실습 " + FocusMissionPolicy.CompletedCount(state, program) + " / " + total, 16, 16, 310, 30, 20, null, true);
            FigmaMissionUi.Label(summary, "같은 미션도 수행할 때마다 기록돼요.", 16, 56, 310, 24, 14, FigmaMissionUi.Muted);
            FigmaMissionUi.Label(body, "최근 기록", 24, 128, 342, 28, 18, null, true);
            float y = 168;
            if (entries.Length == 0)
            {
                FigmaMissionUi.Label(body, "아직 저장된 수행 기록이 없어요.\n첫 실천을 마치면 여기에 쌓여요.", 24, y, 342, 64, 16, FigmaMissionUi.Muted); y += 88;
            }
            foreach (var entry in entries.Take(3)) { BuildFocusHistoryRow(body, entry, y); y += 112; }
            FigmaMissionUi.Button(body, "저장된 기록 보기", 24, y, 342, 48, () => ShowFocusHistory(null, 0, program), Color.white, FigmaMissionUi.Green); y += 72;
            bool expanded = focusExpandedCurricula.Contains(program);
            var disclosure = FigmaMissionUi.Button(body, expanded ? "전체 실습 순서 접기  −" : "전체 실습 순서 보기  +", 24, y, 342, 56, () =>
            {
                if (!focusExpandedCurricula.Add(program)) focusExpandedCurricula.Remove(program);
                RebuildFocusDisclosure(() => ShowFocusProgram(program));
            }, Color.white, FigmaMissionUi.Green);
            disclosure.name = "Focus.CurriculumToggle"; y += 72;
            if (expanded)
            {
                var curriculum = FigmaMissionUi.Rect(body, "펼친 실습 순서", 0, y, 390, total * 96);
                for (int i = 0; i < total; i++)
                {
                    int order = i + 1; var mission = FocusMissionPolicy.MissionAt(order, program);
                    var active = entries.LastOrDefault(a => a.FocusOrder == order && FocusMissionPolicy.IsActive(a));
                    string status = active != null ? "진행 중 · 이어서 하기" : order < next ? "완료 · 기록 보기" : order == next ? "지금 할 실습" : "앞으로 할 실습";
                    var row = FigmaMissionUi.Button(curriculum, "", 24, i * 96, 342, 80, () => ShowFocusOccurrence(program, order), order == next ? FigmaMissionUi.Soft : Color.white, FigmaMissionUi.Ink);
                    row.name = "Focus.Occurrence." + program + "." + order;
                    FigmaMissionUi.Label(row.transform, order.ToString("00"), 12, 24, 32, 28, 14, FigmaMissionUi.Green, true, TextAnchor.MiddleCenter);
                    FigmaMissionUi.Label(row.transform, mission.Code + " · " + mission.Title, 56, 8, 270, 40, 16, null, true);
                    FigmaMissionUi.Label(row.transform, status, 56, 52, 270, 24, 14, FigmaMissionUi.Muted);
                }
                y += total * 96;
            }
            FigmaMissionUi.Button(body, "내 과정으로 돌아가기", 24, y, 342, 48, ShowFocusCourse, Color.clear, FigmaMissionUi.Green); y += 64;
            body.sizeDelta = new Vector2(390, Mathf.Max(628, y));
        }

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
            var root = BeginFigmaPage("focus-occurrence", () => ShowFocusOccurrence(program, order), FigmaMissionUi.Background, true, "내 과정");
            FigmaHeader(root, mission.Code + " · " + order + "회차", FocusProgramTitle(program));
            var body = FigmaScroll(root, 132, 628, 490 + entries.Length * 112);
            var info = FigmaMissionUi.Box(body, "미션 개요", 24, 8, 342, 200, FigmaMissionUi.Hex(mission.BackgroundHex), 24);
            FigmaMissionUi.Label(info, mission.Title, 20, 24, 302, 58, 22, null, true);
            FigmaMissionUi.Label(info, mission.Overview, 20, 104, 302, 72, 17, FigmaMissionUi.Hex(mission.TextHex));
            float y = 232;
            if (entries.Length == 0) { FigmaMissionUi.Label(body, "아직 저장된 수행 기록이 없어요.\n미션 창에서 실습한 뒤 돌아와 주세요.", 24, y, 342, 68, 16, FigmaMissionUi.Muted); y += 90; }
            foreach (var entry in entries) { BuildFocusHistoryRow(body, entry, y); y += 112; }
            var active = entries.LastOrDefault(FocusMissionPolicy.IsActive);
            if (active != null) { FigmaMissionUi.Button(body, "저장한 단계 이어서 하기", 24, y, 342, 52, () => SelectFocusSession(active.SessionId)); y += 68; }
            else if (order < FocusMissionPolicy.NextOrder(state, program)) { FigmaMissionUi.Button(body, "다시 연습하기 · 추가 보상 없음", 24, y, 342, 52, () => StartFocusReview(mission.Id, order, program), Color.white, FigmaMissionUi.Green, 14); y += 68; }
            else { FigmaMissionUi.Label(body, "기본 미션은 선택한 코스의 순서대로 열려요.", 24, y, 342, 48, 14, FigmaMissionUi.Muted); y += 64; }
            FigmaMissionUi.Button(body, "코스 기록으로 돌아가기", 24, y, 342, 52, () => ShowFocusProgram(program), Color.white, FigmaMissionUi.Green);
        }

        public void ShowFocusHistory(string missionId = null, int order = 0, string program = null)
        {
            if (!opened || !SaveActivePracticeDraft()) return;
            if (missionId == null && program == null) { ShowFocusCourse(); return; }
            var state = Service.Snapshot; var definition = FocusMissionCatalog.Find(missionId);
            var list = state.Sessions.Where(a => (missionId == null || a.MissionId == (definition?.Id ?? missionId)) && (order == 0 || a.FocusOrder == order)
                && (program == null || (program == "legacy" ? !FocusMissionPolicy.IsFocus(a) || FocusMissionPolicy.ProgramFor(a) == "legacy" : FocusMissionPolicy.IsFocus(a) && FocusMissionPolicy.ProgramFor(a) == program)))
                .OrderByDescending(a => a.SelectedUtc, StringComparer.Ordinal).ToArray();
            var root = BeginFigmaPage("focus-history", () => ShowFocusHistory(missionId, order, program), FigmaMissionUi.Background, true, "내 과정");
            FigmaHeader(root, definition == null ? (program == null ? "저장된 기록" : FocusProgramTitle(program) + " 기록") : definition.Code + " 수행 기록", definition?.Title ?? "최근에 남긴 실천부터 확인해요.");
            var body = FigmaScroll(root, 132, 628, Mathf.Max(620, 140 + list.Length * 112));
            if (list.Length == 0) FigmaMissionUi.Label(body, "아직 저장된 수행 기록이 없어요.\n미션 창에서 실습한 뒤 돌아와 주세요.", 24, 40, 342, 80, 16, FigmaMissionUi.Muted);
            for (int i = 0; i < list.Length; i++) BuildFocusHistoryRow(body, list[i], 8 + i * 112);
            FigmaMissionUi.Button(body, "내 과정으로 돌아가기", 24, Mathf.Max(180, 20 + list.Length * 112), 342, 52, ShowFocusCourse, Color.white, FigmaMissionUi.Green);
        }

        void BuildFocusHistoryRow(Transform root, ActivitySession entry, float y)
        {
            bool focus = FocusMissionPolicy.IsFocus(entry);
            var row = FigmaMissionUi.Button(root, "", 24, y, 342, 96, () => ShowFocusRecord(entry.SessionId), Color.white, FigmaMissionUi.Ink);
            row.name = "Focus.Record." + entry.SessionId;
            FigmaMissionUi.Label(row.transform, FocusCode(entry) + " · " + FocusSessionTitle(entry), 16, 10, 310, 40, 16, null, true);
            string group = focus ? FocusProgramTitle(FocusMissionPolicy.ProgramFor(entry)) + (entry.FocusRole == FocusMissionCatalog.BasicRole ? " " + entry.FocusOrder + "회" : "") : "이전 기록";
            FigmaMissionUi.Label(row.transform, group + " · " + LocalTime(entry.EndedUtc ?? entry.SelectedUtc), 16, 50, 310, 22, 14, FigmaMissionUi.Muted);
            FigmaMissionUi.Label(row.transform, StateLabel(entry.Status) + (entry.FocusReview ? " · 복습" : ""), 16, 72, 310, 22, 14, FigmaMissionUi.Green);
        }

        public void ShowFocusRecord(string id)
        {
            if (!opened || !SaveActivePracticeDraft()) return;
            var entry = Service.Snapshot.Sessions.FirstOrDefault(a => a.SessionId == id);
            if (entry == null) { ShowFocusCourse(); return; }
            bool focus = FocusMissionPolicy.IsFocus(entry); string program = focus ? FocusMissionPolicy.ProgramFor(entry) : "legacy";
            var root = BeginFigmaPage("focus-record", () => ShowFocusRecord(id), FigmaMissionUi.Background, true, "내 과정");
            FigmaHeader(root, FocusCode(entry) + " 미션 기록", FocusSessionTitle(entry));
            var body = FigmaScroll(root, 132, 628, 628); float y = 8;
            string group = (focus ? FocusProgramTitle(program) + (entry.FocusRole == FocusMissionCatalog.BasicRole ? " · " + entry.FocusOrder + "회차" : "") : "이전 미션") + " · " + StateLabel(entry.Status);
            int elapsed = entry.FocusStepRecords?.Sum(step => step.ElapsedSeconds) ?? 0;
            string when = focus ? LocalTime(entry.EndedUtc ?? entry.SelectedUtc) : "시작 · " + LocalTime(entry.StartedUtc) + "\n마침 · " + (entry.EndedUtc == null ? "진행 중" : LocalTime(entry.EndedUtc));
            if (elapsed > 0) when += "\n집중한 시간 · " + FocusRecordDuration(elapsed);
            y = BuildFocusRecordCard(body, group, when, y, FigmaMissionUi.Soft);
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
            if (focus && entry.Status == ParticipationState.RewardCommitted) y = BuildFocusRecordCard(body, "받은 영양제", "+" + entry.RewardAmount + (entry.FocusReview ? " · 복습" : ""), y);
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
                        y = BuildFocusRecordCard(body, step.Title, step.Prompt + "\n" + (string.IsNullOrEmpty(step.ConfirmedUtc) ? "작성 중" : "확인 · " + LocalTime(step.ConfirmedUtc)) + "\n" + value + time, y);
                    }
                    y = BuildFocusRecordCard(body, "선택 기록", "감정 · " + (string.IsNullOrEmpty(entry.Mood) ? "선택하지 않음" : entry.Mood) + "\n한 줄 기록 · " + (string.IsNullOrEmpty(entry.Note) ? "남긴 기록 없음" : entry.Note), y);
                }
            }
            if (FocusMissionPolicy.IsActive(entry)) { FigmaMissionUi.Button(body, "저장한 단계 이어서 하기", 24, y, 342, 48, () => SelectFocusSession(id)); y += 64; }
            if (focus && entry.Status == ParticipationState.RewardCommitted) { FigmaMissionUi.Button(body, "다시 연습하기 · 추가 보상 없음", 24, y, 342, 48, () => StartFocusReview(entry.MissionId, entry.FocusOrder, program), Color.white, FigmaMissionUi.Green, 14); y += 64; }
            FigmaMissionUi.Button(body, "기록 목록", 24, y, 342, 48, () => ShowFocusHistory(null, 0, program), Color.clear, FigmaMissionUi.Green); y += 64;
            body.sizeDelta = new Vector2(390, Mathf.Max(628, y));
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
