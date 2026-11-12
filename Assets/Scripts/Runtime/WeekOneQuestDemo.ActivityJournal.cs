using System;
using System.Globalization;
using System.Linq;
using CapstoneDesign.Prototype;
using CapstoneDesign.Runtime.LocalState;
using UnityEngine;
using UnityEngine.UI;

namespace CapstoneDesign.Runtime
{
    public sealed partial class WeekOneQuestDemo
    {
        const string ActivityJournalScreen = "daily-emotion-history";
        DateTime activityJournalMonth;
        string activityJournalDate, activityJournalInstallation;
        GardenStateService activityJournalOwner;
        float activityJournalScroll = 1;
        bool activityJournalUndated;

        bool FromActivityJournal => screen == ActivityJournalScreen ||
            (screen == "daily-emotion" || screen == "focus-record" || screen == "record") && pageHistory.Count > 1
                && pageHistory[pageHistory.Count - 2].Key == ActivityJournalScreen;

        void ShowActivityJournal() => OpenActivityJournal(null);

        void ShowActivityJournalForMission(string sessionId) => OpenActivityJournal(sessionId);

        void OpenActivityJournal(string sessionId)
        {
            if (!opened || IsMissionDebugActive || !SaveActivePracticeDraft()) return;
            var state = Service.Snapshot;
            var session = sessionId == null ? null : state.Sessions.FirstOrDefault(item => item.SessionId == sessionId);
            bool resume = session == null && FromActivityJournal && activityJournalOwner == Service
                && activityJournalInstallation == state.InstallationId;
            if (!resume)
            {
                // These detail keys are reused for another record. An older
                // detail below the new journal must not cut the journal out of
                // navigation history when that same detail type opens again.
                pageHistory.RemoveAll(page => page.Key == "daily-emotion" || page.Key == "focus-record"
                    || page.Key == "record" || page.Key.StartsWith("record:", StringComparison.Ordinal));
                var day = JournalToday();
                string recordDay = session == null ? null : ActivityJournal.DayFor(session, Service.CurrentTimeZone);
                if (ActivityJournal.TryDay(recordDay, out var savedDay)) day = savedDay;
                activityJournalDate = ActivityJournal.Key(day);
                activityJournalMonth = new DateTime(day.Year, day.Month, 1);
                activityJournalUndated = session != null && recordDay == null; activityJournalScroll = 1;
            }
            activityJournalOwner = Service; activityJournalInstallation = state.InstallationId;
            RenderActivityJournal(session != null);
        }

        DateTime JournalToday()
        {
            if (ActivityJournal.TryDay(Service.CurrentCycleId, out var day)) return day;
            return TimeZoneInfo.ConvertTime(Service.CurrentUtc, Service.CurrentTimeZone).DateTime.AddHours(-4).Date;
        }

        void RememberActivityJournalScroll()
        {
            var scroll = pageViewport == null ? null : pageViewport.GetComponent<ScrollRect>();
            activityJournalScroll = scroll == null ? 1 : scroll.verticalNormalizedPosition;
        }

        void ReturnToActivityJournalIfSource(Action fallback)
        {
            if (FromActivityJournal) ShowDailyEmotionHistory(); else fallback?.Invoke();
        }

        void RenderActivityJournal(bool revealDay = false)
        {
            if (!opened || IsMissionDebugActive) return;
            var state = Service.Snapshot;
            var index = ActivityJournal.Build(state, Service.CurrentTimeZone);
            var today = JournalToday();
            if (activityJournalMonth == default) activityJournalMonth = new DateTime(today.Year, today.Month, 1);
            var root = BeginFigmaPage(ActivityJournalScreen, () => RenderActivityJournal(), FigmaMissionUi.Background, true, "기록");
            root.GetComponent<FocusMobilePageLayout>().FixedContent = true;
            var frame = activityFrame;
            Func<bool> current = () => opened && screen == ActivityJournalScreen && frame != null
                && frame == activityFrame && frame.activeInHierarchy && !GardenMemoPopup.IsOpen && !GardenDetailSheet.IsOpen;
            FigmaHeader(root, "활동 일지", "");
            bool fromCourse = pageHistory.Count > 1 && pageHistory[pageHistory.Count - 2].Key == "focus-course";
            FigmaMissionUi.Button(root, "‹ 기록", 16, 0, 84, 40, () => {
                if (!current()) return;
                if (fromCourse) TryNavigateBack(); else ShowFocusCourse();
            }, Color.clear, FigmaMissionUi.Green, 14).name = "ActivityJournal.Back";
            int offset = (int)activityJournalMonth.DayOfWeek;
            int days = DateTime.DaysInMonth(activityJournalMonth.Year, activityJournalMonth.Month);
            int weeks = (offset + days + 6) / 7;
            float calendarHeight = 88 + weeks * 50;
            var calendar = FigmaMissionUi.Box(root, "ActivityJournal.Calendar", 24, 110, 342, calendarHeight, Color.clear, 16);
            GardenUi.Round(calendar.GetComponent<Image>(), GardenSagePalette.Paper);
            var dayCells = new System.Collections.Generic.List<ActivityJournalLayout.DayCell>();
            var previous = FigmaMissionUi.Button(calendar, "이전", 4, 6, 52, 48, () => {
                if (!current() || activityJournalMonth.Year == 1 && activityJournalMonth.Month == 1) return;
                activityJournalMonth = activityJournalMonth.AddMonths(-1); activityJournalScroll = 1; RenderActivityJournal();
            }, Color.clear, FigmaMissionUi.Green, 14);
            previous.name = "ActivityJournal.PreviousMonth"; previous.interactable = activityJournalMonth.Year > 1 || activityJournalMonth.Month > 1;
            FigmaMissionUi.Label(calendar, activityJournalMonth.ToString("yyyy년 M월", CultureInfo.InvariantCulture),
                62, 14, 218, 30, 20, FigmaMissionUi.Green, true, TextAnchor.MiddleCenter).name = "ActivityJournal.Month";
            var next = FigmaMissionUi.Button(calendar, "다음", 286, 6, 52, 48, () => {
                if (!current() || activityJournalMonth.Year == 9999 && activityJournalMonth.Month == 12) return;
                activityJournalMonth = activityJournalMonth.AddMonths(1); activityJournalScroll = 1; RenderActivityJournal();
            }, Color.clear, FigmaMissionUi.Green, 14);
            next.name = "ActivityJournal.NextMonth"; next.interactable = activityJournalMonth.Year < 9999 || activityJournalMonth.Month < 12;
            const float cellWidth = 342f / 7;
            string[] weekdays = { "일", "월", "화", "수", "목", "금", "토" };
            for (int column = 0; column < 7; column++)
                FigmaMissionUi.Label(calendar, weekdays[column], column * cellWidth, 58, cellWidth, 22, 13,
                    FigmaMissionUi.Muted, false, TextAnchor.MiddleCenter).name = "ActivityJournal.Weekday." + column;
            for (int number = 1; number <= days; number++)
            {
                var day = new DateTime(activityJournalMonth.Year, activityJournalMonth.Month, number);
                string key = ActivityJournal.Key(day); int cell = offset + number - 1;
                bool selected = !activityJournalUndated && key == activityJournalDate;
                var button = FigmaMissionUi.Button(calendar, "", (cell % 7) * cellWidth, 82 + (cell / 7) * 50,
                    cellWidth, 50, () => {
                        if (!current()) return;
                        activityJournalDate = key; activityJournalUndated = false; activityJournalScroll = 1;
                        RenderActivityJournal(true);
                    }, selected ? FigmaMissionUi.Soft : Color.clear, FigmaMissionUi.Green);
                button.name = "ActivityJournal.Day." + key;
                RectTransform todayBorder = null;
                if (day.Date == today.Date)
                {
                    // Outline duplicates the whole transparent button mesh and
                    // fills the cell when today is unselected. Draw only edges.
                    var border = FigmaMissionUi.Rect(button.transform, "ActivityJournal.TodayBorder", 0, 0, cellWidth, 50);
                    todayBorder = border;
                    Color red = new Color32(196, 76, 68, 255);
                    FigmaMissionUi.Box(border, "Top", 2, 2, cellWidth - 4, 2, red);
                    FigmaMissionUi.Box(border, "Bottom", 2, 46, cellWidth - 4, 2, red);
                    FigmaMissionUi.Box(border, "Left", 2, 4, 2, 42, red);
                    FigmaMissionUi.Box(border, "Right", cellWidth - 4, 4, 2, 42, red);
                }
                FigmaMissionUi.Label(button.transform, number.ToString(CultureInfo.InvariantCulture), 0, 2, cellWidth, 23,
                    15, FigmaMissionUi.Green, selected || day == today, TextAnchor.MiddleCenter);
                RectTransform sproutRect = null;
                if (index.ForDate(key).Count > 0)
                {
                    var sprout = GardenUi.Icon(button.transform, PrototypeUiIcon.Symbol.Garden, .5f, .5f, 0, 0);
                    sprout.name = "ActivityJournal.Sprout." + key; sprout.rectTransform.sizeDelta = new Vector2(20, 20);
                    sprout.rectTransform.anchoredPosition = new Vector2(0, -11);
                    sprout.raycastTarget = false;
                    sproutRect = sprout.rectTransform;
                }
                dayCells.Add(new ActivityJournalLayout.DayCell { Root = (RectTransform)button.transform,
                    Column = cell % 7, Row = cell / 7, Border = todayBorder, Sprout = sproutRect });
            }
            var selectedDay = activityJournalUndated ? index.Undated : index.ForDate(activityJournalDate);
            var card = FigmaMissionUi.Box(root, "ActivityJournal.DateCard", 24, 110 + calendarHeight + 12, 342, 240, Color.clear, 16);
            GardenUi.Round(card.GetComponent<Image>(), GardenSagePalette.Paper);
            string title = selectedDay.DateKey == null ? "날짜 없는 기록" : selectedDay.DateKey.Replace('-', '.');
            FigmaMissionUi.Label(card, title, 16, 12, 210, 28, 19, FigmaMissionUi.Green, true).name = "ActivityJournal.SelectedDate";
            if (index.Undated.Count > 0)
            {
                FigmaMissionUi.Button(card, activityJournalUndated ? "날짜별 기록" : "이전 기록", 234, 4, 100, 44, () => {
                    if (!current()) return;
                    activityJournalUndated = !activityJournalUndated; activityJournalScroll = 1; RenderActivityJournal(true);
                }, Color.clear, FigmaMissionUi.Green, 14).name = "ActivityJournal.Undated";
            }
            var viewport = FigmaMissionUi.Box(card, "ActivityJournal.Scroll", 0, 52, 342, 180, Color.clear);
            viewport.GetComponent<Image>().raycastTarget = true;
            viewport.gameObject.AddComponent<RectMask2D>();
            var body = FigmaMissionUi.Rect(viewport, "ActivityJournal.Entries", 0, 0, 342, 180);
            var scroll = viewport.gameObject.AddComponent<ScrollRect>();
            scroll.viewport = viewport; scroll.content = body; scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Clamped; scroll.scrollSensitivity = 30;
            pageViewport = viewport.gameObject;
            float recordsHeight = BuildActivityJournalDay(body, state, selectedDay, current);
            body.sizeDelta = new Vector2(342, recordsHeight);
            var track = FigmaMissionUi.Box(viewport, "ActivityJournal.Scrollbar", 336, 4, 3, 172, GardenSagePalette.Border, 2);
            track.anchorMin = new Vector2(1, 0); track.anchorMax = Vector2.one;
            track.offsetMin = new Vector2(-6, 4); track.offsetMax = new Vector2(-3, -4);
            var handle = FigmaMissionUi.Box(track, "Handle", 0, 0, 3, 172, GardenSagePalette.Muted, 2);
            handle.anchorMin = Vector2.zero; handle.anchorMax = Vector2.one; handle.offsetMin = handle.offsetMax = Vector2.zero;
            var scrollbar = track.gameObject.AddComponent<Scrollbar>(); scrollbar.direction = Scrollbar.Direction.BottomToTop;
            scrollbar.handleRect = handle; scrollbar.targetGraphic = handle.GetComponent<Image>();
            scrollbar.navigation = new Navigation { mode = Navigation.Mode.None };
            track.GetComponent<Image>().raycastTarget = handle.GetComponent<Image>().raycastTarget = true;
            scrollbar.SetValueWithoutNotify(1); scroll.verticalScrollbar = scrollbar;
            scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;
            var layout = root.gameObject.AddComponent<ActivityJournalLayout>();
            layout.Calendar = calendar; layout.DayCard = card; layout.List = viewport;
            layout.Weeks = weeks; layout.Cells = dayCells.ToArray(); layout.Notice = notice;
            layout.Empty = selectedDay.Count == 0;
            layout.Navigation = root.GetComponentInChildren<MissionBottomNavigation>().GetComponent<RectTransform>();
            layout.Refresh();
            Canvas.ForceUpdateCanvases();
            scroll.StopMovement();
            scroll.verticalNormalizedPosition = revealDay ? 1 : Mathf.Clamp01(activityJournalScroll);
        }

        float BuildActivityJournalDay(RectTransform body, GardenState state, ActivityJournalDay day, Func<bool> current)
        {
            var sessions = state.Sessions.GroupBy(session => session.SessionId).ToDictionary(group => group.Key, group => group.First());
            var emotions = state.DailyEmotionRecords.GroupBy(record => record.Id).ToDictionary(group => group.Key, group => group.First());
            var card = body;
            float rowY = 0;
            if (day.Count == 0)
            {
                FigmaMissionUi.Label(card, "이 날짜에 남긴 기록이 없어요.", 16, rowY, 310, 48, 17, FigmaMissionUi.Muted)
                    .name = "ActivityJournal.Empty";
                rowY += 48;
            }
            if (day.EmotionIds.Length > 0)
            {
                foreach (string id in day.EmotionIds)
                {
                    var record = emotions[id];
                    string status = record.IsCompleted ? "완료" : Service.IsDailyEmotionExpired(id) ? "작성 기간 지남" : "작성 중";
                    string summary = string.Join(" · ", new[] { record.Mood, record.Note }.Where(value => !string.IsNullOrWhiteSpace(value))).Replace('\n', ' ');
                    if (summary.Length > 72) summary = summary.Substring(0, 72) + "…";
                    rowY += BuildActivityJournalRow(card, "DailyEmotion.HistoryRow." + id, "감정 기록 · " + status,
                        string.IsNullOrEmpty(summary) ? "남긴 내용이 없어요." : summary, rowY, () => {
                            if (!current()) return; RememberActivityJournalScroll();
                            ShowActivityJournalEmotion(id, FigmaMissionUi.Find<Button>(card, "DailyEmotion.HistoryRow." + id));
                        }, plain: true) + 8;
                }
            }
            if (day.SessionIds.Length > 0)
            {
                foreach (string id in day.SessionIds)
                {
                    var session = sessions[id];
                    string name = FocusSessionTitle(session);
                    if (string.IsNullOrWhiteSpace(name) || name == session.MissionId) name = "이전 미션 기록";
                    string role = FocusMissionPolicy.RoleFor(session);
                    string description = (role == FocusMissionCatalog.BasicRole ? "기본 미션" : role == FocusMissionCatalog.AdditionalRole ? "추가 미션" : "이전 미션")
                        + " · " + StateLabel(session.Status) + (session.FocusReview ? " · 복습" : "");
                    rowY += BuildActivityJournalRow(card, "ActivityJournal.Mission." + id, name, description, rowY, () => {
                        if (!current()) return; RememberActivityJournalScroll();
                        ShowActivityJournalMission(id, FigmaMissionUi.Find<Button>(card, "ActivityJournal.Mission." + id));
                    }) + 8;
                }
            }
            return rowY + 10;
        }

        float BuildActivityJournalRow(Transform parent, string name, string title, string summary, float y, Action open, bool plain = false)
        {
            // Start transparent so the shared shell does not add its saturated
            // selected-card decoration. Only mission rows need a quiet surface.
            var row = FigmaMissionUi.Button(parent, "", 12, y, 318, 92, open, Color.clear, FigmaMissionUi.Ink);
            row.name = name;
            if (!plain) row.GetComponent<Image>().color = new Color32(232, 237, 229, 255);
            var colors = row.colors; colors.highlightedColor = new Color(.96f, .97f, .96f);
            colors.pressedColor = new Color(.88f, .90f, .88f); row.colors = colors;
            float inset = plain ? 4 : 12, width = 318 - inset * 2;
            float titleWidth = width - 28;
            var heading = FigmaMissionUi.Label(row.transform, title, inset, 10, titleWidth, 26, 17, FigmaMissionUi.Green, true);
            float titleHeight = Mathf.Max(26, heading.preferredHeight); heading.rectTransform.sizeDelta = new Vector2(titleWidth, titleHeight);
            FigmaMissionUi.Label(row.transform, "›", 282, 10, 24, 26, 20, FigmaMissionUi.Muted, false, TextAnchor.MiddleCenter);
            var detail = FigmaMissionUi.Label(row.transform, summary, inset, 14 + titleHeight, width, 22, 15, FigmaMissionUi.Muted);
            float summaryHeight = Mathf.Max(22, detail.preferredHeight); detail.rectTransform.sizeDelta = new Vector2(width, summaryHeight);
            float height = titleHeight + summaryHeight + 28;
            FigmaMissionUi.SetRect((RectTransform)row.transform, 12, y, 318, height);
            return height;
        }
    }
}
