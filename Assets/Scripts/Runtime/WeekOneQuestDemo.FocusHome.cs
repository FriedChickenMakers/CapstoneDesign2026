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
        public void ShowHome() => ShowFocusHome();
        public void ShowCourse() => ShowFocusCourse();
        public void ShowFree() => ShowFocusRecommendation();
        public void ShowProgramSelection() => ShowFocusProgramSelection();
        public void ShowRecords() => ShowFocusCourse();
        public void ShowRecord(string id) => ShowFocusRecord(id);

        static string FocusProgramTitle(string program) => program == "legacy" ? "이전 예시 과정" : program == "additional" ? "추천·자유 미션" : MbctPolicy.ProgramWeeks(program) + "주 코스";
        static string FocusProgramDescription(string program) => program == "2w" ? "감각에서 시작해 나를 돌보는 계획까지" : program == "4w" ? "몸·생각·행동을 차근차근 살피기" : "다양한 상황에서 반복하며 익히기";
        static string FocusCode(ActivitySession entry) => FocusMissionCatalog.Find(entry.MissionId)?.Code ?? "";
        // IDs overlap between historical P/M content and Focus versions. Read
        // the session's own definition/snapshot, never rename an older record.
        static string FocusSessionTitle(ActivitySession entry) => FocusMissionPolicy.IsFocus(entry)
            ? entry.MissionTitleSnapshot ?? FocusMissionCatalog.FindForSession(entry)?.Title ?? entry.MissionId
            : MindfulnessContent.FindMission(entry.MissionId)?.title ?? entry.MissionId;

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
            var body = FigmaScroll(root, 132, 628, 750);
            var card = FigmaMissionUi.Box(body, "기본 미션", 24, 8, 342, 278, FigmaMissionUi.Green, 24);
            FigmaMissionUi.Asset(card, "FocusMissions/Main/basic-leaf", 24, 24, 24, 24);
            string courseLabel = active != null && (!FocusMissionPolicy.IsFocus(active) || FocusMissionPolicy.ProgramFor(active) != program)
                ? (FocusMissionPolicy.IsFocus(active) ? FocusProgramTitle(FocusMissionPolicy.ProgramFor(active)) + " · " + active.FocusOrder + "회" : "이전 미션 이어가기")
                : FocusProgramTitle(program) + " · " + done + " / " + total;
            FigmaMissionUi.Label(card, courseLabel, 56, 24, 260, 24, 14, Color.white, true);
            FigmaMissionUi.Label(card, active != null ? FocusCode(active) + " · " + FocusSessionTitle(active) : finished ? "과정을 마쳤어요" : mission.Code + " · " + mission.Title, 24, 66, 294, 62, 22, Color.white, true);
            string context = active != null ? "저장한 단계부터 이어갈 수 있어요." : finished ? "내 과정에서 쌓인 실천을 돌아봐요." : doneToday ? "오늘의 기본 미션을 마쳤어요." : unavailable != null ? "이번 주 실습을 마쳤어요. 다음 주에 이어가요." : "한 화면에서 한 가지씩 따라가요.";
            FigmaMissionUi.Label(card, context, 24, 140, 294, 48, 16, Color.white);
            string action = active != null ? "기본 미션 이어서 하기" : finished ? "과정 기록 보기" : doneToday ? "오늘 완료했어요" : unavailable != null ? "다음 주에 이어가요" : "기본 미션 시작하기";
            var start = FigmaMissionUi.Button(card, action, 24, 202, 294, 52, active == null && finished ? (Action)(() => ShowFocusProgram(program)) : StartFocusBasic, Color.white, FigmaMissionUi.Green);
            start.interactable = finished || active != null || unavailable == null;
            FigmaMissionUi.Label(body, "추천 미션", 24, 314, 240, 28, 18, null, true);
            FigmaMissionUi.Label(body, "코스와 별개 · 선택", 220, 314, 146, 28, 13, FigmaMissionUi.Muted, false, TextAnchor.MiddleRight);
            BuildRecommendationCard(body, state, 354);
            FigmaMissionUi.Button(body, "추천·자유 미션 모두 보기", 24, 590, 342, 52, ShowFocusMissionLibrary, Color.white, FigmaMissionUi.Brown);
            FigmaMissionUi.Button(body, "코스 변경", 24, 658, 342, 52, ShowFocusProgramSelection, Color.clear, FigmaMissionUi.Green, 14);
        }

        void BuildRecommendationCard(Transform root, GardenState state, float y)
        {
            var mission = RecommendedFocusMission(state);
            var active = FocusMissionPolicy.Active(state, FocusMissionCatalog.AdditionalRole);
            bool completed = FocusMissionPolicy.AdditionalCompletedToday(state, mission.Id);
            var box = FigmaMissionUi.Box(root, "코스와 독립적인 추천 미션", 24, y, 342, 214, FigmaMissionUi.Warm, 24);
            FigmaMissionUi.Asset(box, "FocusMissions/Main/spark", 20, 20, 24, 24);
            FigmaMissionUi.Label(box, "나를 위한 추천 · " + mission.Code, 52, 20, 270, 24, 14, FigmaMissionUi.Brown, true);
            FigmaMissionUi.Label(box, mission.Title, 20, 58, 302, 52, 20, null, true);
            FigmaMissionUi.Label(box, active != null ? "진행하던 단계가 저장되어 있어요." : "코스를 정하지 않아도 실천할 수 있어요.", 20, 116, 302, 24, 13, FigmaMissionUi.Brown);
            FigmaMissionUi.Button(box, active != null ? "추가 미션 이어서 하기" : completed ? "추천 미션 다시 보기" : "추천 미션 보기", 20, 150, 302, 48,
                active != null ? (Action)(() => SelectFocusSession(active.SessionId)) : () => ShowFocusRecommendationFor(mission.Id), FigmaMissionUi.Brown);
        }

        public void ShowFocusProgramSelection()
        {
            if (!opened || !SaveActivePracticeDraft()) return;
            var state = Service.Snapshot;
            var root = BeginFigmaPage("focus-programs", ShowFocusProgramSelection, FigmaMissionUi.Background, true);
            FigmaHeader(root, state.MbctProgramChosen ? "내게 맞는 코스" : "코스를 골라주세요", "기간을 정하고, 하루 한 가지씩 이어가요.");
            var previous = state.Sessions.LastOrDefault(a => FocusMissionPolicy.IsActive(a) && a.MissionId.StartsWith("course:", StringComparison.Ordinal));
            float shift = previous == null ? 0 : 84;
            var body = FigmaScroll(root, 132, 628, 884 + shift);
            for (int i = 0; i < FocusPrograms.Length; i++)
            {
                string program = FocusPrograms[i]; int done = FocusMissionPolicy.CompletedCount(state, program);
                var row = FigmaMissionUi.Button(body, "", 24, 8 + i * 148, 342, 132, () => ShowFocusProgramConfirmation(program), state.MbctProgramChosen && state.MbctProgramId == program ? FigmaMissionUi.Soft : Color.white, FigmaMissionUi.Ink);
                row.name = "Focus.SelectProgram." + program;
                FigmaMissionUi.Label(row.transform, FocusProgramTitle(program), 20, 18, 250, 30, 22, null, true);
                FigmaMissionUi.Label(row.transform, FocusProgramDescription(program), 20, 56, 302, 27, 14, FigmaMissionUi.Muted);
                FigmaMissionUi.Label(row.transform, FocusMissionPolicy.TotalPracticesFor(program) + "회 실습" + (done > 0 ? " · " + done + "회 완료" : ""), 20, 94, 302, 24, 14, FigmaMissionUi.Green);
                FigmaMissionUi.Label(row.transform, "›", 286, 20, 36, 30, 26, FigmaMissionUi.Green, false, TextAnchor.MiddleRight);
            }
            if (previous != null) FigmaMissionUi.Button(body, "진행하던 기본 미션 이어가기", 24, 452, 342, 60, () => SelectFocusSession(previous.SessionId));
            FigmaMissionUi.Label(body, "추천 미션은 언제든 할 수 있어요", 24, 464 + shift, 342, 28, 18, null, true);
            BuildRecommendationCard(body, state, 506 + shift);
            FigmaMissionUi.Button(body, "추천·자유 미션 모두 보기", 24, 744 + shift, 342, 52, ShowFocusMissionLibrary, Color.white, FigmaMissionUi.Brown);
            FigmaMissionUi.Label(body, "코스를 바꿔도 완료 기록과 정원은 유지돼요.", 24, 818 + shift, 342, 32, 13, FigmaMissionUi.Muted);
        }

        public void ShowFocusProgramConfirmation(string program)
        {
            if (!opened || !MbctPolicy.IsProgram(program) || !SaveActivePracticeDraft()) return;
            var state = Service.Snapshot;
            var root = BeginFigmaPage("focus-program-confirm", () => ShowFocusProgramConfirmation(program), FigmaMissionUi.Background, true);
            FigmaHeader(root, FocusProgramTitle(program), "내 속도로 이어가는 마음챙김");
            var body = FigmaScroll(root, 132, 628, 700);
            var box = FigmaMissionUi.Box(body, "코스 안내", 24, 8, 342, 242, FigmaMissionUi.Soft, 24);
            FigmaMissionUi.Label(box, FocusProgramDescription(program), 24, 26, 294, 62, 22, null, true);
            FigmaMissionUi.Label(box, "하루 기본 미션 1개 · 주 최대 6개\n총 " + FocusMissionPolicy.TotalPracticesFor(program) + "회 실습 · 쉬는 날은 자유롭게", 24, 116, 294, 66, 16, FigmaMissionUi.Green);
            FigmaMissionUi.Label(box, "놓친 날에도 진도는 그대로예요.", 24, 196, 294, 24, 14, FigmaMissionUi.Muted);
            int done = FocusMissionPolicy.CompletedCount(state, program);
            FigmaMissionUi.Label(body, done > 0 ? "이 코스에서 " + done + "회 실습을 마쳤어요.\n저장된 진도부터 이어갑니다." : "코스를 바꿔도 이전 기록은 남아요.\n추천 미션은 코스와 별개로 수행해요.", 24, 280, 342, 76, 16, FigmaMissionUi.Muted);
            bool current = state.MbctProgramChosen && state.MbctProgramId == program;
            var previous = state.Sessions.LastOrDefault(a => FocusMissionPolicy.IsActive(a) && a.MissionId.StartsWith("course:", StringComparison.Ordinal));
            bool blocked = previous != null && !current;
            if (blocked)
            {
                FigmaMissionUi.Label(body, "진행 중인 미션을 먼저 마쳐주세요.\n마친 뒤 코스를 바꿀 수 있어요.", 24, 370, 342, 56, 16, FigmaMissionUi.Brown);
                FigmaMissionUi.Button(body, "진행하던 기본 미션 이어가기", 24, 442, 342, 52, () => SelectFocusSession(previous.SessionId));
            }
            float actionY = blocked ? 516 : 482;
            var select = FigmaMissionUi.Button(body, done >= FocusMissionPolicy.TotalPracticesFor(program) ? "완료한 코스 보기" : done > 0 ? "이 코스 이어서 하기" : "이 코스로 시작", 24, actionY, 342, 56, () =>
            {
                if (!Apply(Service.SelectMbctProgram(program))) return;
                coursePageInitialized = false; ShowFocusHome();
            });
            select.name = "Focus.ConfirmProgram." + program; select.interactable = !blocked;
            FigmaMissionUi.Button(body, "다른 코스 보기", 24, actionY + 72, 342, 52, ShowFocusProgramSelection, Color.white, FigmaMissionUi.Green);
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
            var box = FigmaMissionUi.Box(root, "추천 미션 안내", 24, 140, 342, 280, FigmaMissionUi.Hex(mission.BackgroundHex), 24);
            FigmaMissionUi.Label(box, mission.Code + " · 가벼운 실천", 24, 26, 294, 24, 14, FigmaMissionUi.Hex(mission.AccentHex), true);
            FigmaMissionUi.Label(box, mission.Title, 24, 76, 294, 66, 24, null, true);
            FigmaMissionUi.Label(box, mission.Overview, 24, 166, 294, 84, 18, FigmaMissionUi.Hex(mission.TextHex));
            FigmaMissionUi.Label(root, "한 화면에서 한 가지씩 안내해요.\n추천 미션의 기록은 코스 진도와 별도로 남아요.", 24, 452, 342, 72, 16, FigmaMissionUi.Muted);
            var active = FocusMissionPolicy.Active(state, FocusMissionCatalog.AdditionalRole);
            bool completed = FocusMissionPolicy.AdditionalCompletedToday(state, mission.Id);
            FigmaMissionUi.Button(root, "추천·자유 미션 모두 보기", 24, 562, 342, 52, ShowFocusMissionLibrary, Color.white, FigmaMissionUi.Brown);
            FigmaMissionUi.Button(root, "‹", 24, 690, 48, 52, ShowHome, Color.clear, FigmaMissionUi.Brown, 30);
            var button = FigmaMissionUi.Button(root, active != null ? "추가 미션 이어서 하기" : completed ? "오늘 완료했어요" : "추가 미션 시작하기", 84, 690, 282, 52,
                active != null ? (Action)(() => SelectFocusSession(active.SessionId)) : () => StartFocusAdditionalMission(mission.Id), FigmaMissionUi.Brown);
            button.interactable = active != null || !completed;
        }

        public void ShowFocusMissionLibrary()
        {
            if (!opened || !SaveActivePracticeDraft()) return;
            var state = Service.Snapshot; var missions = FocusMissionCatalog.Additional;
            var root = BeginFigmaPage("focus-library", ShowFocusMissionLibrary, FigmaMissionUi.Background, true);
            FigmaHeader(root, "추천·자유 미션", "코스 선택과 상관없이 실천할 수 있어요.");
            var body = FigmaScroll(root, 132, 628, 116 + missions.Length * 108);
            for (int i = 0; i < missions.Length; i++)
            {
                var mission = missions[i]; bool completed = FocusMissionPolicy.AdditionalCompletedToday(state, mission.Id);
                var row = FigmaMissionUi.Button(body, "", 24, 8 + i * 108, 342, 96, () => ShowFocusRecommendationFor(mission.Id), Color.white, FigmaMissionUi.Ink);
                row.name = "Focus.FreeMission." + mission.Code;
                FigmaMissionUi.Label(row.transform, mission.Code, 16, 16, 48, 22, 13, FigmaMissionUi.Hex(mission.AccentHex), true);
                FigmaMissionUi.Label(row.transform, mission.Title, 72, 14, 250, 44, 17, null, true);
                FigmaMissionUi.Label(row.transform, completed ? "오늘 완료 · 기록은 내 과정에" : "자유롭게 선택 · 단계별 안내", 72, 64, 250, 22, 13, FigmaMissionUi.Muted);
            }
            FigmaMissionUi.Button(body, "미션 창으로 돌아가기", 24, 20 + missions.Length * 108, 342, 52, ShowHome, Color.white, FigmaMissionUi.Green);
        }

        public void ShowFocusCourse()
        {
            if (!opened || !SaveActivePracticeDraft()) return;
            var state = Service.Snapshot;
            var root = BeginFigmaPage("course", ShowFocusCourse, FigmaMissionUi.Background, true, "내 과정");
            FigmaHeader(root, "내 과정", "코스별로 실천과 저장한 기록을 돌아봐요.");
            bool legacy = state.Sessions.Any(a => !FocusMissionPolicy.IsFocus(a) || FocusMissionPolicy.ProgramFor(a) == "legacy");
            var body = FigmaScroll(root, 132, 628, legacy ? 1018 : 824);
            for (int i = 0; i < FocusPrograms.Length; i++)
            {
                string program = FocusPrograms[i]; int done = FocusMissionPolicy.CompletedCount(state, program), total = FocusMissionPolicy.TotalPracticesFor(program);
                bool current = state.MbctProgramChosen && state.MbctProgramId == program;
                int records = state.Sessions.Count(a => FocusMissionPolicy.IsFocus(a) && FocusMissionPolicy.ProgramFor(a) == program);
                var row = FigmaMissionUi.Button(body, "", 24, 8 + i * 144, 342, 128, () => ShowFocusProgram(program), current ? FigmaMissionUi.Soft : Color.white, FigmaMissionUi.Ink);
                row.name = "Focus.CourseRecords." + program;
                FigmaMissionUi.Label(row.transform, FocusProgramTitle(program) + (current ? " · 진행 중" : ""), 20, 18, 302, 30, 20, null, true);
                FigmaMissionUi.Label(row.transform, "완료한 실습 " + done + " / " + total + " · 저장 기록 " + records + "개", 20, 58, 302, 26, 14, FigmaMissionUi.Muted);
                FigmaMissionUi.Box(row.transform, "전체 진도", 20, 102, 302, 6, FigmaMissionUi.Border, 3);
                if (done > 0) FigmaMissionUi.Box(row.transform, "완료 진도", 20, 102, 302 * done / total, 6, FigmaMissionUi.Green, 3);
            }
            FigmaMissionUi.Button(body, "추천·자유 미션 기록", 24, 452, 342, 64, () => ShowFocusHistory(null, 0, "additional"), FigmaMissionUi.Warm, FigmaMissionUi.Brown);
            float y = 536;
            if (legacy)
            {
                FigmaMissionUi.Button(body, "이전 과정·예시 기록", 24, y, 342, 64, () => ShowFocusHistory(null, 0, "legacy"), Color.white, FigmaMissionUi.Green); y += 84;
                FigmaMissionUi.Button(body, "이전 활동 복습·추천", 24, y, 342, 64, ShowLearnedPracticeRecommendations, Color.white, FigmaMissionUi.Green); y += 84;
            }
            FigmaMissionUi.Button(body, "정원·건강 기록", 24, y, 342, 64, ShowGardenJournal, Color.white, FigmaMissionUi.Green); y += 84;
            FigmaMissionUi.Button(body, "코스 선택·변경", 24, y, 342, 52, ShowFocusProgramSelection, Color.white, FigmaMissionUi.Green);
            FigmaMissionUi.Button(body, "미션 창으로 돌아가기", 24, y + 68, 342, 52, ShowHome, Color.clear, FigmaMissionUi.Green, 14);
        }

        public void ShowFocusProgram(string program)
        {
            if (!opened || !MbctPolicy.IsProgram(program) || !SaveActivePracticeDraft()) return;
            var state = Service.Snapshot; int total = FocusMissionPolicy.TotalPracticesFor(program), next = FocusMissionPolicy.NextOrder(state, program);
            var root = BeginFigmaPage("focus-course", () => ShowFocusProgram(program), FigmaMissionUi.Background, true, "내 과정");
            FigmaHeader(root, FocusProgramTitle(program), "실습 순서와 기록을 한곳에서 확인해요.");
            var body = FigmaScroll(root, 132, 628, 284 + total * 96);
            var summary = FigmaMissionUi.Box(body, "코스 진도", 24, 8, 342, 104, FigmaMissionUi.Soft, 24);
            FigmaMissionUi.Label(summary, "완료한 실습 " + (next - 1) + " / " + total, 20, 20, 302, 28, 20, null, true);
            FigmaMissionUi.Label(summary, "같은 미션도 수행 회차별로 기록돼요.", 20, 64, 302, 24, 14, FigmaMissionUi.Muted);
            FigmaMissionUi.Button(body, "저장된 기록 보기", 24, 128, 342, 48, () => ShowFocusHistory(null, 0, program), Color.white, FigmaMissionUi.Green);
            for (int i = 0; i < total; i++)
            {
                int order = i + 1; var mission = FocusMissionPolicy.MissionAt(order, program);
                var rows = state.Sessions.Where(a => FocusMissionPolicy.IsFocus(a) && FocusMissionPolicy.ProgramFor(a) == program && a.FocusOrder == order).ToArray();
                var active = rows.LastOrDefault(FocusMissionPolicy.IsActive);
                string status = active != null ? "진행 중 · 저장한 단계 이어가기" : order < next ? "완료 · 수행 기록 보기" : order == next ? "현재 순서 · 미션 탭에서 시작" : "이후 순서 · 실습 안내 보기";
                var row = FigmaMissionUi.Button(body, "", 24, 196 + i * 96, 342, 84, () => ShowFocusOccurrence(program, order), order == next ? FigmaMissionUi.Soft : Color.white, FigmaMissionUi.Ink);
                row.name = "Focus.Occurrence." + program + "." + order;
                FigmaMissionUi.Label(row.transform, order.ToString("00"), 12, 20, 34, 28, 14, FigmaMissionUi.Green, true, TextAnchor.MiddleCenter);
                FigmaMissionUi.Label(row.transform, mission.Code + " · " + mission.Title, 56, 12, 270, 38, 16, null, true);
                FigmaMissionUi.Label(row.transform, status, 56, 56, 270, 22, 12, FigmaMissionUi.Muted);
            }
            FigmaMissionUi.Button(body, "내 과정으로 돌아가기", 24, 212 + total * 96, 342, 52, ShowFocusCourse, Color.white, FigmaMissionUi.Green);
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
            var body = FigmaScroll(root, 132, 628, 490 + entries.Length * 100);
            var info = FigmaMissionUi.Box(body, "미션 개요", 24, 8, 342, 200, FigmaMissionUi.Hex(mission.BackgroundHex), 24);
            FigmaMissionUi.Label(info, mission.Title, 20, 24, 302, 58, 22, null, true);
            FigmaMissionUi.Label(info, mission.Overview, 20, 104, 302, 72, 17, FigmaMissionUi.Hex(mission.TextHex));
            float y = 232;
            if (entries.Length == 0) { FigmaMissionUi.Label(body, "아직 저장된 수행 기록이 없어요.\n미션 창에서 실습한 뒤 돌아와 주세요.", 24, y, 342, 68, 16, FigmaMissionUi.Muted); y += 90; }
            foreach (var entry in entries) { BuildFocusHistoryRow(body, entry, y); y += 100; }
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
            FigmaHeader(root, definition == null ? (program == null ? "저장된 기록" : FocusProgramTitle(program) + " 기록") : definition.Code + " 수행 기록", definition?.Title ?? "단계별 확인과 작성한 내용을 돌아봐요.");
            var body = FigmaScroll(root, 132, 628, Mathf.Max(620, 140 + list.Length * 100));
            if (list.Length == 0) FigmaMissionUi.Label(body, "아직 저장된 수행 기록이 없어요.\n미션 창에서 실습한 뒤 돌아와 주세요.", 24, 40, 342, 80, 16, FigmaMissionUi.Muted);
            for (int i = 0; i < list.Length; i++) BuildFocusHistoryRow(body, list[i], 8 + i * 100);
            FigmaMissionUi.Button(body, "내 과정으로 돌아가기", 24, Mathf.Max(180, 20 + list.Length * 100), 342, 52, ShowFocusCourse, Color.white, FigmaMissionUi.Green);
        }

        void BuildFocusHistoryRow(Transform root, ActivitySession entry, float y)
        {
            bool focus = FocusMissionPolicy.IsFocus(entry);
            var row = FigmaMissionUi.Button(root, "", 24, y, 342, 88, () => ShowFocusRecord(entry.SessionId), Color.white, FigmaMissionUi.Ink);
            row.name = "Focus.Record." + entry.SessionId;
            FigmaMissionUi.Label(row.transform, FocusCode(entry) + " · " + FocusSessionTitle(entry), 16, 10, 310, 36, 16, null, true);
            string group = focus ? FocusProgramTitle(FocusMissionPolicy.ProgramFor(entry)) + (entry.FocusRole == FocusMissionCatalog.BasicRole ? " " + entry.FocusOrder + "회" : "") : "이전 기록";
            FigmaMissionUi.Label(row.transform, group + " · " + LocalTime(entry.EndedUtc ?? entry.SelectedUtc), 16, 48, 310, 20, 12, FigmaMissionUi.Muted);
            FigmaMissionUi.Label(row.transform, StateLabel(entry.Status) + (entry.FocusReview ? " · 복습" : ""), 16, 68, 310, 18, 12, FigmaMissionUi.Green);
        }

        public void ShowFocusRecord(string id)
        {
            if (!opened || !SaveActivePracticeDraft()) return;
            var entry = Service.Snapshot.Sessions.FirstOrDefault(a => a.SessionId == id);
            if (entry == null) { ShowFocusCourse(); return; }
            if (!FocusMissionPolicy.IsFocus(entry)) { ShowLegacyRecord(id); return; }
            bool focus = FocusMissionPolicy.IsFocus(entry); string program = focus ? FocusMissionPolicy.ProgramFor(entry) : "legacy";
            var root = BeginFigmaPage("focus-record", () => ShowFocusRecord(id), FigmaMissionUi.Background, true, "내 과정");
            FigmaHeader(root, FocusCode(entry) + " 미션 기록", FocusSessionTitle(entry));
            var parts = new List<string> {
                (focus ? FocusProgramTitle(program) + (entry.FocusRole == FocusMissionCatalog.BasicRole ? " · " + entry.FocusOrder + "회차" : "") : "이전 미션") + " · " + StateLabel(entry.Status),
                "시작 · " + LocalTime(entry.StartedUtc) + "\n마침 · " + (entry.EndedUtc == null ? "진행 중" : LocalTime(entry.EndedUtc))
            };
            if (entry.MissionId == "free-mission:M15" && entry.WalkingGoalSteps > 0)
                parts.Add("시작할 때 정한 걷기 목표 · " + entry.WalkingGoalSteps.ToString("N0") + "걸음\n걸음 확인 구간\n"
                    + LocalTime(entry.WalkingQueryStartUtc) + " ~ " + LocalTime(entry.WalkingQueryEndUtc) + "\n" + WalkingPersonalization.Explanation(entry));
            var drawings = new Dictionary<int, string>();
            if (focus) foreach (var step in entry.FocusStepRecords.OrderBy(r => r.Step))
            {
                var definition = FocusMissionCatalog.FindForSession(entry);
                bool unselectedBranch = definition != null && step.Skipped && !FocusMissionCatalog.IsApplicable(definition, entry.FocusStepRecords, step.Step);
                bool drawing = step.Value != null && step.Value.StartsWith("DRAW1|", StringComparison.Ordinal);
                string value = unselectedBranch ? "선택하지 않은 경로" : drawing ? "저장된 그림" : step.Skipped ? "입력 건너뜀" : string.IsNullOrEmpty(step.Value) ? "행동 확인" : step.Value;
                string time = step.ElapsedSeconds > 0 ? "\n집중 타이머 기록 · " + (step.ElapsedSeconds / 60).ToString("00") + ":" + (step.ElapsedSeconds % 60).ToString("00") : "";
                parts.Add((step.Step + 1) + ". " + step.Title + "\n" + step.Prompt + "\n" + (string.IsNullOrEmpty(step.ConfirmedUtc) ? "작성 중" : "확인 · " + LocalTime(step.ConfirmedUtc)) + "\n" + value + time);
                if (drawing) drawings[parts.Count - 1] = step.Value;
            }
            else foreach (var answer in entry.Answers ?? new List<PracticeAnswer>()) parts.Add(AnswerLabel(answer.Key) + "\n" + answer.Value);
            parts.Add("감정 · " + (string.IsNullOrEmpty(entry.Mood) ? "선택하지 않음" : entry.Mood));
            parts.Add("한 줄 기록\n" + (string.IsNullOrEmpty(entry.Note) ? "남긴 기록 없음" : entry.Note));
            if (focus && entry.Status == ParticipationState.RewardCommitted) parts.Add("받은 영양제 · " + entry.RewardAmount + (entry.FocusReview ? " (복습)" : ""));
            var body = FigmaScroll(root, 132, 628, 1100); float y = 8;
            for (int part = 0; part < parts.Count; part++)
            {
                string text = parts[part];
                float height = Mathf.Max(64, 28 + Mathf.Ceil(text.Length / 23f) * 23 + text.Count(c => c == '\n') * 15);
                var card = FigmaMissionUi.Box(body, "기록 항목", 24, y, 342, height, Color.white, 16);
                var label = FigmaMissionUi.Label(card, text, 16, 14, 310, height - 28, 15);
                height = Mathf.Max(height, label.preferredHeight + 28); label.rectTransform.sizeDelta = new Vector2(310, height - 28);
                if (drawings.TryGetValue(part, out var drawingValue))
                {
                    var paper = FigmaMissionUi.Box(card, "저장된 그림 종이", 16, height, 310, 232, FigmaMissionUi.Background, 12);
                    var drawing = FigmaMissionUi.Rect(paper, "저장된 그림", 8, 8, 294, 216).gameObject.AddComponent<FocusDrawingSurface>();
                    drawing.ReadOnly = true; drawing.raycastTarget = false; drawing.color = FigmaMissionUi.Green; drawing.Load(drawingValue); height += 248;
                }
                card.sizeDelta = new Vector2(342, height); y += height + 12;
            }
            if (FocusMissionPolicy.IsActive(entry)) { FigmaMissionUi.Button(body, "저장한 단계 이어서 하기", 24, y, 342, 52, () => SelectFocusSession(id)); y += 68; }
            if (focus && entry.Status == ParticipationState.RewardCommitted) { FigmaMissionUi.Button(body, "다시 연습하기 · 추가 보상 없음", 24, y, 342, 52, () => StartFocusReview(entry.MissionId, entry.FocusOrder, program), Color.white, FigmaMissionUi.Green, 14); y += 64; }
            FigmaMissionUi.Button(body, "기록 목록", 24, y, 342, 52, () => ShowFocusHistory(null, 0, program), Color.white, FigmaMissionUi.Green); y += 76;
            body.sizeDelta = new Vector2(390, Mathf.Max(628, y));
        }
    }
}
