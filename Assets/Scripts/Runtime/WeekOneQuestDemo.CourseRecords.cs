using System;
using System.Linq;
using CapstoneDesign.Prototype;
using CapstoneDesign.Runtime.LocalState;
using UnityEngine;
using UnityEngine.UI;

namespace CapstoneDesign.Runtime
{
    public sealed partial class WeekOneQuestDemo
    {
        RectTransform courseDetailOverlay;
        GardenDetailSheet courseDetailSheet;
        CanvasGroup courseRecordsGroup;
        ScrollRect courseRecordsScroller;
        Button courseDetailOrigin;

        void RenderCourseRecords(string program, int? selectedWeek = null)
        {
            if (!opened || !MbctPolicy.IsProgram(program) || !SaveActivePracticeDraft()) return;
            var summary = CourseRecordSummary.Build(Service.Snapshot, program);
            int week = Mathf.Clamp(selectedWeek ?? summary.DefaultWeek, 1, summary.Weeks);
            var root = BeginFigmaPage("focus-course", () => RenderCourseRecords(program, week), FigmaMissionUi.Background, true, "기록");
            root.GetComponent<FocusMobilePageLayout>().FixedContent = true;
            var frame = activityFrame;
            Func<bool> current = () => frame != null && frame == activityFrame && frame.activeInHierarchy
                && screen == "focus-course" && courseDetailOverlay == null;
            courseRecordsGroup = root.gameObject.AddComponent<CanvasGroup>();
            FigmaHeader(root, "코스 기록", "");
            FigmaMissionUi.Button(root, "‹ 기록", 16, 0, 72, 40, () => { if (current()) ShowFocusCourse(); },
                Color.clear, FigmaMissionUi.Green, 14).name = "Course.Back";

            var info = FigmaMissionUi.Rect(root, "Course.Summary", 24, 110, 342, 104);
            FigmaMissionUi.Label(info, "현재 : " + FocusProgramTitle(program), 0, 0, 342, 26, 18,
                FigmaMissionUi.Green, false, TextAnchor.MiddleCenter);
            FigmaMissionUi.Label(info, summary.Completed + " / " + summary.Total + " 실습 완료", 0, 32, 342, 34, 24,
                FigmaMissionUi.Ink, true, TextAnchor.MiddleCenter).name = "Course.Completed";
            // The decorative theme draws a full-rect child, so this measured
            // indicator owns its two images and has no independent overlay.
            var track = FigmaMissionUi.Rect(info, "Course.ProgressTrack", 16, 82, 310, 8);
            var trackImage = track.gameObject.AddComponent<Image>();
            GardenUi.Round(trackImage, GardenSagePalette.Border); trackImage.raycastTarget = false;
            var progress = FigmaMissionUi.Rect(track, "Course.Progress", 0, 0, 310, 8).gameObject.AddComponent<Image>();
            GardenUi.Round(progress, GardenSagePalette.Primary); progress.raycastTarget = false;
            progress.type = Image.Type.Filled; progress.fillMethod = Image.FillMethod.Horizontal;
            progress.fillAmount = Mathf.Clamp01((float)summary.Completed / summary.Total);

            int columns = Mathf.Min(4, summary.Weeks), rows = Mathf.CeilToInt(summary.Weeks / (float)columns);
            var weeks = FigmaMissionUi.Rect(root, "Course.Weeks", 24, 226, 342, rows * 60 - 8);
            float cellWidth = (342 - (columns - 1) * 8) / columns;
            for (int number = 1; number <= summary.Weeks; number++)
            {
                int target = number;
                int count = summary.Occurrences.Count(item => item.Week == target && item.Completed);
                bool selected = target == week;
                var button = FigmaMissionUi.Button(weeks, "", (number - 1) % columns * (cellWidth + 8),
                    (number - 1) / columns * 60, cellWidth, 52, () => {
                        if (current()) RenderCourseRecords(program, target);
                    }, Color.clear, FigmaMissionUi.Ink);
                button.name = "Course.Week." + number;
                GardenUi.Round(button.image, selected ? GardenSagePalette.Selected : GardenSagePalette.Paper);
                FigmaMissionUi.Label(button.transform, number + "주", 0, 4, cellWidth, 24, 17,
                    FigmaMissionUi.Green, selected, TextAnchor.MiddleCenter);
                FigmaMissionUi.Label(button.transform, count + "/6", 0, 29, cellWidth, 19, 12,
                    FigmaMissionUi.Muted, false, TextAnchor.MiddleCenter);
            }
            int weekDone = summary.Occurrences.Count(item => item.Week == week && item.Completed);
            var heading = FigmaMissionUi.Label(root, week + "주차", 24, 350, 342, 30, 18, FigmaMissionUi.Ink, true);
            heading.name = "Course.WeekHeading";
            FigmaMissionUi.Label(heading.transform, weekDone + "/6 완료", 232, 0, 110, 30, 14,
                FigmaMissionUi.Muted, false, TextAnchor.MiddleRight);
            var body = FigmaScroll(root, 388, 360, 600);
            pageViewport.name = "Course.List";
            courseRecordsScroller = pageViewport.GetComponent<ScrollRect>();
            float y = 4;
            foreach (var item in summary.Occurrences.Where(item => item.Week == week))
                y = BuildCourseOccurrenceRow(body, item, program, current, y);
            body.sizeDelta = new Vector2(390, y + 8);
            var layout = root.gameObject.AddComponent<CourseRecordsLayout>();
            layout.Summary = info; layout.Weeks = weeks; layout.WeekRows = rows;
            layout.WeekHeading = heading.rectTransform; layout.Viewport = (RectTransform)pageViewport.transform;
            layout.Navigation = (RectTransform)root.GetComponentInChildren<MissionBottomNavigation>().transform;
            layout.Notice = notice; layout.Refresh();
        }

        float BuildCourseOccurrenceRow(Transform body, CourseRecordOccurrence item, string program, Func<bool> current, float y)
        {
            var button = FigmaMissionUi.Button(body, "", 24, y, 342, 84, null, Color.clear, FigmaMissionUi.Ink);
            button.name = "Focus.Occurrence." + program + "." + item.Order;
            GardenUi.Round(button.image, item.Status == "active" ? GardenSagePalette.Selected : GardenSagePalette.Paper);
            button.onClick.AddListener(() => {
                if (!current()) return;
                courseDetailOrigin = button;
                ShowCourseRecordDetail(program, item.Order);
            });
            FigmaMissionUi.Label(button.transform, item.Order.ToString("00"), 8, 15, 32, 28, 14,
                FigmaMissionUi.Muted, false, TextAnchor.MiddleCenter);
            var title = FigmaMissionUi.Label(button.transform, item.Title, 48, 12, 202, 28, 18, FigmaMissionUi.Ink, true);
            float titleHeight = Mathf.Max(28, Mathf.Ceil(title.preferredHeight));
            title.rectTransform.sizeDelta = new Vector2(202, titleHeight);
            FigmaMissionUi.Label(button.transform, item.StatusText, 252, 14, 80, 26, 14,
                item.Status == "upcoming" ? FigmaMissionUi.Muted : FigmaMissionUi.Green, false, TextAnchor.MiddleCenter);
            var dated = item.CompletedEntry ?? item.Active ?? item.Latest;
            string date = dated == null ? "" : CourseRecordDate(dated, false);
            string detail = date + (item.Entries.Length > 1 ? " · 기록 " + item.Entries.Length + "개" : "");
            float height = 24 + titleHeight;
            if (detail.Length > 0)
            {
                FigmaMissionUi.Label(button.transform, detail, 48, 16 + titleHeight, 274, 23, 13, FigmaMissionUi.Muted);
                height += 27;
            }
            height = Mathf.Max(72, height);
            FigmaMissionUi.SetRect((RectTransform)button.transform, 24, y, 342, height);
            return y + height + 10;
        }

        string CourseRecordDate(ActivitySession entry, bool withTime)
        {
            string iso = entry.EndedUtc ?? entry.StartedUtc ?? entry.SelectedUtc;
            return CourseRecordTime(iso, withTime);
        }

        string CourseRecordTime(string iso, bool withTime = true)
        {
            if (!DateTimeOffset.TryParse(iso, out var instant)) return "날짜 정보 없음";
            return TimeZoneInfo.ConvertTime(instant, Service.CurrentTimeZone).ToString(withTime ? "yyyy.MM.dd HH:mm" : "MM.dd");
        }

        static string CourseAttemptStatus(ActivitySession entry)
        {
            string status = entry.Status == ParticipationState.RewardCommitted ? "완료"
                : FocusMissionPolicy.IsActive(entry) ? "진행 중"
                : entry.FocusEndReason == "skipped" ? "넘김"
                : entry.FocusEndReason == "alternative" ? "전환" : "중단";
            return status + (entry.FocusReview ? " · 복습" : "");
        }

        void ShowCourseRecordDetail(string program, int order, string selectedId = null, bool expanded = false, bool preserveScroll = false)
        {
            if (!opened || screen != "focus-course" || view == null) return;
            var item = CourseRecordSummary.Build(Service.Snapshot, program).Occurrences.FirstOrDefault(row => row.Order == order);
            if (item == null) return;
            var previousScroll = courseDetailOverlay == null ? null : courseDetailOverlay.GetComponentInChildren<ScrollRect>();
            float scrollY = preserveScroll && previousScroll != null ? previousScroll.content.anchoredPosition.y : 0;
            CloseCourseRecordDetail(false);
            var entry = item.Entries.FirstOrDefault(record => record.SessionId == selectedId) ?? item.Latest;
            var root = (RectTransform)view.transform;
            var frame = activityFrame;
            var sheet = GardenDetailSheet.Create(root, "Course.Detail", entry == null ? item.Title : FocusSessionTitle(entry),
                () => CloseCourseRecordDetail(), courseDetailOrigin, courseRecordsScroller, courseRecordsGroup);
            if (sheet == null) return;
            courseDetailSheet = sheet;
            var overlay = (RectTransform)sheet.transform;
            courseDetailOverlay = overlay;
            sheet.Closed += () => {
                if (courseDetailSheet != sheet) return;
                courseDetailSheet = null; courseDetailOverlay = null;
            };
            Func<bool> current = () => overlay != null && overlay == courseDetailOverlay && overlay.gameObject.activeInHierarchy
                && frame != null && frame == activityFrame && screen == "focus-course";
            var content = sheet.Content;
            float y = 8;
            if (item.Entries.Length > 1)
            {
                y = AddCourseDetailText(content, "수행 기록", "", y);
                foreach (var attempt in item.Entries)
                {
                    string id = attempt.SessionId;
                    var pick = FigmaMissionUi.Button(content, CourseRecordDate(attempt, true) + " · " + CourseAttemptStatus(attempt),
                        16, y, 334, 48, () => { if (current()) ShowCourseRecordDetail(program, order, id); }, Color.clear, FigmaMissionUi.Green, 14);
                    pick.name = "Course.Attempt." + id;
                    GardenUi.Round(pick.image, id == entry.SessionId ? GardenSagePalette.Selected : GardenSagePalette.Background);
                    y += 56;
                }
                y += 8;
            }
            if (entry == null)
            {
                y = AddCourseDetailText(content, item.StatusText, FocusMissionPolicy.MissionAt(order, program).Overview, y);
                y = AddCourseDetailText(content, "", "아직 저장된 수행 기록이 없어요.", y);
            }
            else
            {
                int elapsed = entry.FocusStepRecords?.Sum(step => step.ElapsedSeconds) ?? 0;
                string status = CourseAttemptStatus(entry) + " · " + CourseRecordDate(entry, true)
                    + (elapsed > 0 ? "\n실습 시간 " + FocusRecordDuration(elapsed) : "");
                y = AddCourseDetailText(content, "", status, y, "Course.DetailStatus");
                if (!string.IsNullOrWhiteSpace(entry.Mood)) y = AddCourseDetailText(content, "그때의 감정", entry.Mood, y);
                if (!string.IsNullOrWhiteSpace(entry.Note)) y = AddCourseDetailText(content, "내가 남긴 글", entry.Note, y, "Course.DetailNote");
                var answers = (entry.FocusStepRecords ?? new System.Collections.Generic.List<FocusStepRecord>())
                    .Where(step => !step.Skipped && !string.IsNullOrWhiteSpace(step.Value)).OrderBy(step => step.Step);
                foreach (var answer in answers)
                    y = AddCourseDetailText(content, answer.Title, answer.Value, y);
                if (entry.Photo != null)
                {
                    y = AddCourseDetailText(content, "함께한 사진", PhotoStatus(entry.Photo) + "\n" + CourseRecordTime(entry.Photo.CapturedUtc), y);
                    bool visible = BuildMissionPhotoPreview(content, entry.Photo.Id, 16, y, 334, 224);
                    if (!visible) FigmaMissionUi.Label(content, "사진 파일을 찾지 못했어요.", 32, y + 90, 302, 32, 15, FigmaMissionUi.Muted);
                    y += 240;
                    var photoNotice = FigmaMissionUi.Label(content, "", 16, y + 52, 334, 48, 14, GardenUi.Warning);
                    photoNotice.gameObject.SetActive(false);
                    FigmaMissionUi.Button(content, "사진만 삭제", 16, y, 334, 48, () => {
                        if (!current()) return;
                        if (!Service.DeletePhoto(entry.SessionId)) { photoNotice.text = "저장하지 못했어요. 다시 시도해 주세요."; photoNotice.gameObject.SetActive(true); return; }
                        Photos.Delete(entry.Photo.Id); ShowCourseRecordDetail(program, order, entry.SessionId, expanded, true);
                    }, Color.clear, FigmaMissionUi.Green, 14).name = "Course.PhotoDelete";
                    y += 112;
                }
                FigmaMissionUi.Button(content, expanded ? "자세히 보기 접기  −" : "자세히 보기  +", 16, y, 334, 48,
                    () => { if (current()) ShowCourseRecordDetail(program, order, entry.SessionId, !expanded, true); },
                    Color.clear, FigmaMissionUi.Green, 15).name = "Course.DetailSteps";
                y += 60;
                if (expanded)
                {
                    y = AddCourseDetailText(content, "수행 시간", "시작 · " + CourseRecordTime(entry.StartedUtc)
                        + "\n마침 · " + (entry.EndedUtc == null ? "진행 중" : CourseRecordTime(entry.EndedUtc)), y);
                    var definition = FocusMissionCatalog.FindForSession(entry);
                    foreach (var step in (entry.FocusStepRecords ?? new System.Collections.Generic.List<FocusStepRecord>()).OrderBy(step => step.Step))
                    {
                        bool unselected = definition != null && step.Skipped && !FocusMissionCatalog.IsApplicable(definition, entry.FocusStepRecords, step.Step);
                        string value = unselected ? "선택하지 않은 경로" : step.Skipped ? "입력 건너뜀" : string.IsNullOrEmpty(step.Value) ? "행동 확인" : step.Value.StartsWith("DRAW1|", StringComparison.Ordinal) ? "저장된 그림" : step.Value;
                        y = AddCourseDetailText(content, step.Title, step.Prompt + "\n" + value
                            + (string.IsNullOrEmpty(step.ConfirmedUtc) ? "\n작성 중" : "\n확인 · " + CourseRecordTime(step.ConfirmedUtc))
                            + (step.ElapsedSeconds > 0 ? "\n실습 시간 " + FocusRecordDuration(step.ElapsedSeconds) : "")
                            + (step.RecommendedSeconds > 0 ? "\n시작 시 추천 " + FocusRecordDuration(step.RecommendedSeconds) : "")
                            + (step.EndReason == "too_long" ? "\n여기까지 진행" : ""), y);
                    }
                    if (entry.RewardAmount > 0) y = AddCourseDetailText(content, "완료 보상", entry.RewardAmount + "코인", y);
                }
                if (FocusMissionPolicy.IsActive(entry))
                {
                    FigmaMissionUi.Button(content, "저장한 단계 이어서 하기", 16, y, 334, 48, () => {
                        if (!current()) return;
                        CloseCourseRecordDetail(false); SelectFocusSession(entry.SessionId);
                    }, FigmaMissionUi.Green, Color.white, 16).name = "Course.Resume";
                    y += 60;
                }
                FigmaMissionUi.Button(content, "그날 활동 일지", 16, y, 334, 48,
                    () => { if (current()) ShowActivityJournalForMission(entry.SessionId); },
                    FigmaMissionUi.Green, Color.white, 16).name = "Course.Journal";
                y += 64;
            }
            content.sizeDelta = new Vector2(366, y + 12);
            content.anchoredPosition = new Vector2(0, scrollY); sheet.Refresh();
        }

        static float AddCourseDetailText(Transform parent, string heading, string value, float y, string name = null)
        {
            if (!string.IsNullOrEmpty(heading))
            {
                var label = FigmaMissionUi.Label(parent, heading, 16, y, 334, 24, 14, FigmaMissionUi.Muted, true);
                float height = Mathf.Max(24, Mathf.Ceil(label.preferredHeight)); label.rectTransform.sizeDelta = new Vector2(334, height);
                y += height + 6;
            }
            if (!string.IsNullOrEmpty(value))
            {
                bool drawing = value.StartsWith("DRAW1|", StringComparison.Ordinal);
                if (drawing)
                {
                    var paper = FigmaMissionUi.Box(parent, "Course.SavedDrawing", 16, y, 334, 220, GardenSagePalette.Background, 12);
                    var surface = FigmaMissionUi.Rect(paper, "저장된 그림", 8, 8, 318, 204).gameObject.AddComponent<FocusDrawingSurface>();
                    surface.ReadOnly = true; surface.raycastTarget = false; surface.color = FigmaMissionUi.Green; surface.Load(value);
                    y += 220;
                }
                else
                {
                    var label = FigmaMissionUi.Label(parent, value, 16, y, 334, 26, 16, FigmaMissionUi.Ink);
                    if (name != null) label.name = name;
                    float height = Mathf.Max(26, Mathf.Ceil(label.preferredHeight)); label.rectTransform.sizeDelta = new Vector2(334, height);
                    y += height;
                }
            }
            return y + 18;
        }

        bool CloseCourseRecordDetail(bool restoreSelection = true)
        {
            if (courseDetailSheet == null) return false;
            var sheet = courseDetailSheet;
            courseDetailSheet = null; courseDetailOverlay = null;
            sheet.Close(restoreSelection);
            return true;
        }
    }
}
