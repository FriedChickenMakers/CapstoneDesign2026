using System;
using System.Collections.Generic;
using System.Linq;
using CapstoneDesign.Prototype;
using CapstoneDesign.Runtime.LocalState;
using UnityEngine;
using UnityEngine.UI;

namespace CapstoneDesign.Runtime
{
    public sealed partial class WeekOneQuestDemo
    {
        GardenDetailSheet journalDetail;

        // Browsing a saved record never enters the editor, refreshes the cycle,
        // starts a plant, or changes the journal's navigation history.
        void ShowActivityJournalMission(string id, Button origin) => RenderActivityJournalMission(id, origin, false, 0);

        void RenderActivityJournalMission(string id, Button origin, bool expanded, float scrollY)
        {
            if (!CanShowActivityJournalDetail()) return;
            var entry = Service.Snapshot.Sessions.FirstOrDefault(item => item.SessionId == id);
            if (entry == null) return;
            string title = FocusSessionTitle(entry);
            if (string.IsNullOrWhiteSpace(title) || title == entry.MissionId) title = "이전 미션 기록";
            var sheet = CreateActivityJournalDetail(title, origin, out var current);
            if (sheet == null) return;
            var content = sheet.Content;
            bool focus = FocusMissionPolicy.IsFocus(entry);
            string role = FocusMissionPolicy.RoleFor(entry);
            string group = role == FocusMissionCatalog.BasicRole ? "기본 미션"
                : role == FocusMissionCatalog.AdditionalRole ? "추가 미션" : "이전 미션";
            int elapsed = entry.FocusStepRecords?.Where(step => step != null).Sum(step => step.ElapsedSeconds) ?? 0;
            string status = group + " · " + CourseAttemptStatus(entry);
            if (focus && role == FocusMissionCatalog.BasicRole)
                status += "\n" + FocusProgramTitle(FocusMissionPolicy.ProgramFor(entry)) + " · " + entry.FocusOrder + "회차";
            status += "\n" + CourseRecordTime(entry.EndedUtc ?? entry.StartedUtc ?? entry.SelectedUtc);
            if (elapsed > 0) status += "\n실습 시간 " + FocusRecordDuration(elapsed);
            if (entry.FocusSupport) status += "\n다른 활동으로 전환 · 코스 회차는 유지";
            if (entry.FocusEndReason == "skipped" || entry.FocusEndReason == "stopped") status += "\n완료하지 않고 마친 실습이에요.";
            float y = JournalDetailText(content, "", status, 8, "Journal.DetailStatus");
            if (!string.IsNullOrWhiteSpace(entry.Mood)) y = JournalDetailText(content, "그때의 감정", entry.Mood, y, "Journal.DetailMood");
            if (!string.IsNullOrWhiteSpace(entry.Note)) y = JournalDetailText(content, "내가 남긴 글", entry.Note, y, "Journal.DetailNote");
            bool hasAnswer = false;
            if (focus)
            {
                foreach (var answer in (entry.FocusStepRecords ?? new List<FocusStepRecord>())
                    .Where(step => step != null && !step.Skipped && !string.IsNullOrWhiteSpace(step.Value)).OrderBy(step => step.Step))
                {
                    y = JournalDetailText(content, answer.Title, answer.Value, y);
                    hasAnswer = true;
                }
            }
            // Focus records mirror their answers into the legacy compatibility
            // list. Render that list only for legacy records, never a second
            // copy of the same text or drawing beneath the current step values.
            if (!focus) foreach (var answer in entry.Answers ?? new List<PracticeAnswer>())
                if (answer != null && !string.IsNullOrWhiteSpace(answer.Value))
                {
                    y = JournalDetailText(content, AnswerLabel(answer.Key), answer.Value, y);
                    hasAnswer = true;
                }
            if (!hasAnswer && string.IsNullOrWhiteSpace(entry.Mood) && string.IsNullOrWhiteSpace(entry.Note))
                y = JournalDetailText(content, "", "남긴 글이 없어도 실습 기록은 보관돼요.", y, "Journal.DetailEmpty");

            if (entry.Photo != null)
            {
                string photoId = entry.Photo.Id;
                y = JournalDetailText(content, "함께한 사진", PhotoStatus(entry.Photo) + "\n" + CourseRecordTime(entry.Photo.CapturedUtc), y);
                bool visible = BuildMissionPhotoPreview(content, photoId, 16, y, 334, 224);
                if (!visible) FigmaMissionUi.Label(content, "사진 파일을 찾지 못했어요.\n활동 기록과 보상은 그대로 남아 있어요.",
                    32, y + 72, 302, 64, 16, FigmaMissionUi.Muted).name = "Journal.PhotoMissing";
                y += 240;
                var photoError = FigmaMissionUi.Label(content, "", 16, y + 52, 334, 48, 14, GardenUi.Warning);
                photoError.name = "Journal.PhotoError"; photoError.gameObject.SetActive(false);
                FigmaMissionUi.Button(content, "사진만 삭제", 16, y, 334, 48, () => {
                    if (!current()) return;
                    var latest = Service.Snapshot.Sessions.FirstOrDefault(item => item.SessionId == id);
                    if (latest?.Photo?.Id != photoId) return;
                    if (!Service.DeletePhoto(id))
                    {
                        photoError.text = "저장하지 못했어요. 사진을 유지했으니 다시 시도해 주세요.";
                        photoError.gameObject.SetActive(true); return;
                    }
                    Photos.Delete(photoId);
                    RenderActivityJournalMission(id, origin, expanded, sheet.Content.anchoredPosition.y);
                }, Color.clear, FigmaMissionUi.Green, 14).name = "Journal.PhotoDelete";
                y += 112;
            }

            if (entry.MissionId == "free-mission:M15" && entry.WalkingGoalSteps > 0)
                y = JournalDetailText(content, "시작할 때 정한 걷기 목표", entry.WalkingGoalSteps.ToString("N0") + "걸음", y, "Journal.WalkingTarget");
            FigmaMissionUi.Button(content, expanded ? "자세히 보기 접기  −" : "자세히 보기  +", 16, y, 334, 48,
                () => { if (current()) RenderActivityJournalMission(id, origin, !expanded, sheet.Content.anchoredPosition.y); },
                Color.clear, FigmaMissionUi.Green, 15).name = "Journal.DetailSteps";
            y += 60;
            if (expanded)
            {
                y = JournalDetailText(content, "수행 시간", "시작 · " + CourseRecordTime(entry.StartedUtc)
                    + "\n마침 · " + (string.IsNullOrEmpty(entry.EndedUtc) ? "진행 중" : CourseRecordTime(entry.EndedUtc)), y);
                if (focus)
                {
                    var definition = FocusMissionCatalog.FindForSession(entry);
                    foreach (var step in (entry.FocusStepRecords ?? new List<FocusStepRecord>()).Where(item => item != null).OrderBy(item => item.Step))
                    {
                        bool unselected = definition != null && step.Skipped && !FocusMissionCatalog.IsApplicable(definition, entry.FocusStepRecords, step.Step);
                        string value = unselected ? "선택하지 않은 경로" : step.Skipped ? "입력 건너뜀"
                            : string.IsNullOrEmpty(step.Value) ? "행동 확인" : step.Value.StartsWith("DRAW1|", StringComparison.Ordinal) ? "저장된 그림 · 위에서 확인" : step.Value;
                        y = JournalDetailText(content, step.Title, step.Prompt + "\n" + value
                            + (string.IsNullOrEmpty(step.ConfirmedUtc) ? "\n작성 중" : "\n확인 · " + CourseRecordTime(step.ConfirmedUtc))
                            + (step.ElapsedSeconds > 0 ? "\n실습 시간 " + FocusRecordDuration(step.ElapsedSeconds) : "")
                            + (step.RecommendedSeconds > 0 ? "\n시작 시 추천 " + FocusRecordDuration(step.RecommendedSeconds) : "")
                            + (step.EndReason == "too_long" ? "\n여기까지 진행" : ""), y);
                    }
                }
                if (entry.MissionId == "free-mission:M15" && entry.WalkingGoalSteps > 0)
                    y = JournalDetailText(content, "걷기 목표를 정한 기준", WalkingPersonalization.Explanation(entry)
                        + "\n걸음 확인 구간\n" + CourseRecordTime(entry.WalkingQueryStartUtc) + " ~ " + CourseRecordTime(entry.WalkingQueryEndUtc),
                        y, "Journal.WalkingProvenance");
                if (entry.Status == ParticipationState.RewardCommitted && entry.RewardAmount > 0)
                    y = JournalDetailText(content, "이미 받은 완료 보상", entry.RewardAmount + "코인", y);
                if (!focus)
                {
                    FigmaMissionUi.Button(content, "건강 기록 보기", 16, y, 334, 48, () => {
                        if (!current()) return;
                        RememberActivityJournalScroll(); CloseActivityJournalDetail(false); ShowLegacyRecord(id);
                    }, Color.clear, FigmaMissionUi.Green, 15).name = "Journal.LegacyHealth";
                    y += 60;
                }
            }
            if (FocusMissionPolicy.IsActive(entry))
            {
                sheet.FooterHeight = 76;
                FigmaMissionUi.Button(sheet.Footer, "실습 이어가기", 16, 12, 334, 48, () => {
                    if (!current()) return;
                    var latest = Service.Snapshot.Sessions.FirstOrDefault(item => item.SessionId == id);
                    if (!FocusMissionPolicy.IsActive(latest)) return;
                    RememberActivityJournalScroll(); CloseActivityJournalDetail(false); SelectFocusSession(id);
                }, FigmaMissionUi.Green, Color.white, 16).name = "Journal.MissionResume";
            }
            FinishActivityJournalDetail(sheet, y, scrollY);
        }

        void ShowActivityJournalEmotion(string id, Button origin)
        {
            if (!CanShowActivityJournalDetail()) return;
            var record = Service.GetDailyEmotion(id);
            if (record == null) return;
            var sheet = CreateActivityJournalDetail("감정 기록", origin, out var current);
            if (sheet == null) return;
            var content = sheet.Content;
            string day = ActivityJournal.DayFor(record, Service.CurrentTimeZone);
            string status = record.IsCompleted ? "완료" : Service.IsDailyEmotionExpired(id) ? "작성 기간 지남" : "작성 중";
            float y = JournalDetailText(content, "", (day == null ? "날짜 정보 없는 이전 기록" : day.Replace('-', '.')) + " · " + status,
                8, "Journal.DetailStatus");
            y = JournalDetailText(content, "그때의 감정", string.IsNullOrWhiteSpace(record.Mood) ? "선택한 감정이 없어요." : record.Mood,
                y, "Journal.DetailMood");
            y = JournalDetailText(content, "남긴 메모", string.IsNullOrWhiteSpace(record.Note) ? "남긴 메모가 없어요." : record.Note,
                y, "Journal.DetailNote");
            var state = Service.Snapshot;
            foreach (string session in new[] { record.BasicSessionId, record.AdditionalSessionId }.Where(value => !string.IsNullOrEmpty(value)).Distinct())
            {
                var entry = state.Sessions.FirstOrDefault(item => item.SessionId == session);
                if (entry == null) continue;
                string title = FocusSessionTitle(entry);
                if (string.IsNullOrWhiteSpace(title) || title == entry.MissionId) title = "이전 미션 기록";
                y = JournalDetailText(content, session == record.BasicSessionId ? "함께한 기본 미션" : "함께한 추가 미션", title, y);
            }
            if (record.IsCompleted) y = JournalDetailText(content, "", DailyEmotionRewardSummary(record), y, "Journal.EmotionReward");
            if (DailyEmotionCanEdit(id))
            {
                sheet.FooterHeight = 76;
                FigmaMissionUi.Button(sheet.Footer, "이어서 작성", 16, 12, 334, 48, () => {
                    if (!current() || !DailyEmotionCanEdit(id)) return;
                    RememberActivityJournalScroll(); CloseActivityJournalDetail(false); ShowDailyEmotion(id);
                }, FigmaMissionUi.Green, Color.white, 16).name = "Journal.EmotionResume";
            }
            FinishActivityJournalDetail(sheet, y, 0);
        }

        bool CanShowActivityJournalDetail() => opened && !IsMissionDebugActive && screen == ActivityJournalScreen
            && view != null && view.activeInHierarchy && activityFrame != null && activityFrame.activeInHierarchy;

        GardenDetailSheet CreateActivityJournalDetail(string title, Button origin, out Func<bool> current)
        {
            current = () => false;
            if (!CanShowActivityJournalDetail()) return null;
            CloseActivityJournalDetail(false);
            var frame = activityFrame;
            var owner = Service;
            var scroll = pageViewport == null ? null : pageViewport.GetComponent<ScrollRect>();
            var sheet = GardenDetailSheet.Create((RectTransform)view.transform, "Journal.Detail", title,
                () => CloseActivityJournalDetail(), origin, scroll);
            if (sheet == null) return null;
            journalDetail = sheet;
            sheet.Closed += () => { if (journalDetail == sheet) journalDetail = null; };
            current = () => this != null && isActiveAndEnabled && CanShowActivityJournalDetail()
                && journalDetail == sheet && sheet != null && sheet.isActiveAndEnabled && frame == activityFrame && ReferenceEquals(owner, Service);
            return sheet;
        }

        static float JournalDetailText(Transform parent, string heading, string value, float y, string name = null)
        {
            int firstChild = parent.childCount;
            float next = AddCourseDetailText(parent, heading, value, y, name);
            // Reuse the same typography and saved-drawing renderer as course
            // records, with a journal-owned node for tracing and validation.
            for (int i = firstChild; i < parent.childCount; i++)
                if (parent.GetChild(i).name == "Course.SavedDrawing") parent.GetChild(i).name = "Journal.SavedDrawing";
            return next;
        }

        static void FinishActivityJournalDetail(GardenDetailSheet sheet, float height, float scrollY)
        {
            sheet.Content.sizeDelta = new Vector2(366, height + 12);
            sheet.Content.anchoredPosition = new Vector2(0, scrollY);
            sheet.Refresh();
        }

        bool CloseActivityJournalDetail(bool restoreSelection = true)
        {
            if (journalDetail == null) return false;
            var sheet = journalDetail; journalDetail = null;
            sheet.Close(restoreSelection); return true;
        }
    }
}
