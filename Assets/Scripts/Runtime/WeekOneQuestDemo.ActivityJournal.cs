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
            var frame = activityFrame;
            Func<bool> current = () => opened && screen == ActivityJournalScreen && frame != null
                && frame == activityFrame && frame.activeInHierarchy && !GardenMemoPopup.IsOpen;
            FigmaHeader(root, "활동 일지", "");
            var body = FigmaScroll(root, 132, 628, 900);
            pageViewport.name = "ActivityJournal.Scroll";
            int offset = (int)activityJournalMonth.DayOfWeek;
            int days = DateTime.DaysInMonth(activityJournalMonth.Year, activityJournalMonth.Month);
            int weeks = (offset + days + 6) / 7;
            float calendarHeight = 88 + weeks * 50;
            var calendar = FigmaMissionUi.Box(body, "ActivityJournal.Calendar", 24, 8, 342, calendarHeight, Color.white, 16);
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
                    FigmaMissionUi.Muted, false, TextAnchor.MiddleCenter);
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
                if (day.Date == today.Date)
                {
                    // Outline duplicates the whole transparent button mesh and
                    // fills the cell when today is unselected. Draw only edges.
                    var border = FigmaMissionUi.Rect(button.transform, "ActivityJournal.TodayBorder", 0, 0, cellWidth, 50);
                    Color red = new Color32(196, 76, 68, 255);
                    FigmaMissionUi.Box(border, "Top", 2, 2, cellWidth - 4, 2, red);
                    FigmaMissionUi.Box(border, "Bottom", 2, 46, cellWidth - 4, 2, red);
                    FigmaMissionUi.Box(border, "Left", 2, 4, 2, 42, red);
                    FigmaMissionUi.Box(border, "Right", cellWidth - 4, 4, 2, 42, red);
                }
                FigmaMissionUi.Label(button.transform, number.ToString(CultureInfo.InvariantCulture), 0, 2, cellWidth, 23,
                    15, FigmaMissionUi.Green, selected || day == today, TextAnchor.MiddleCenter);
                if (index.ForDate(key).Count > 0)
                {
                    var sprout = GardenUi.Icon(button.transform, PrototypeUiIcon.Symbol.Garden, .5f, .5f, 0, 0);
                    sprout.name = "ActivityJournal.Sprout." + key; sprout.rectTransform.sizeDelta = new Vector2(20, 20);
                    sprout.rectTransform.anchoredPosition = new Vector2(0, -11);
                    sprout.raycastTarget = false;
                }
            }
            float y = calendarHeight + 24;
            var selectedDay = activityJournalUndated ? index.Undated : index.ForDate(activityJournalDate);
            float dayY = y;
            float dayHeight = BuildActivityJournalDay(body, state, selectedDay, y, current);
            y += dayHeight + 16;
            if (index.Undated.Count > 0)
            {
                FigmaMissionUi.Button(body, "날짜 정보 없는 이전 기록 · " + index.Undated.Count + "개", 24, y, 342, 48, () => {
                    if (!current()) return;
                    activityJournalUndated = true; activityJournalScroll = 1; RenderActivityJournal(true);
                }, Color.clear, FigmaMissionUi.Green, 15).name = "ActivityJournal.Undated";
                y += 64;
            }
            FigmaMissionUi.Button(body, "기록으로 돌아가기", 24, y, 342, 48, () => {
                if (current()) ShowFocusCourse();
            }, Color.clear, FigmaMissionUi.Green).name = "ActivityJournal.Back";
            body.sizeDelta = new Vector2(390, y + 72);
            root.GetComponent<FocusMobilePageLayout>().Refresh();
            Canvas.ForceUpdateCanvases();
            var scroll = pageViewport.GetComponent<ScrollRect>(); scroll.StopMovement();
            if (revealDay)
                body.anchoredPosition = new Vector2(0, Mathf.Clamp(dayY + Mathf.Min(dayHeight, 178) - scroll.viewport.rect.height,
                    0, Mathf.Max(0, body.rect.height - scroll.viewport.rect.height)));
            else scroll.verticalNormalizedPosition = Mathf.Clamp01(activityJournalScroll);
        }

        float BuildActivityJournalDay(Transform body, GardenState state, ActivityJournalDay day, float y, Func<bool> current)
        {
            var sessions = state.Sessions.GroupBy(session => session.SessionId).ToDictionary(group => group.Key, group => group.First());
            var emotions = state.DailyEmotionRecords.GroupBy(record => record.Id).ToDictionary(group => group.Key, group => group.First());
            var card = FigmaMissionUi.Box(body, "ActivityJournal.DateCard", 24, y, 342, 150, Color.white, 16);
            string title = day.DateKey == null ? "날짜 정보 없는 이전 기록" : day.DateKey.Replace('-', '.');
            FigmaMissionUi.Label(card, title, 16, 14, 310, 30, 20, FigmaMissionUi.Green, true).name = "ActivityJournal.SelectedDate";
            float rowY = 54;
            if (day.Count == 0)
            {
                FigmaMissionUi.Label(card, "이 날짜에 남긴 기록이 없어요.", 16, rowY, 310, 64, 17, FigmaMissionUi.Muted)
                    .name = "ActivityJournal.Empty";
                rowY += 80;
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
                            if (!current()) return; RememberActivityJournalScroll(); ShowDailyEmotion(id);
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
                        if (!current()) return; RememberActivityJournalScroll(); ShowFocusRecord(id);
                    }) + 8;
                }
            }
            card.sizeDelta = new Vector2(342, rowY + 10);
            return card.sizeDelta.y;
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
            float titleWidth = plain ? width - 28 : width;
            var heading = FigmaMissionUi.Label(row.transform, title, inset, 10, titleWidth, 26, 16, FigmaMissionUi.Green, true);
            float titleHeight = Mathf.Max(26, heading.preferredHeight); heading.rectTransform.sizeDelta = new Vector2(titleWidth, titleHeight);
            if (plain) FigmaMissionUi.Label(row.transform, "›", 286, 10, 28, 26, 20, FigmaMissionUi.Muted, false, TextAnchor.MiddleRight);
            var detail = FigmaMissionUi.Label(row.transform, summary, inset, 14 + titleHeight, width, 22, 14, FigmaMissionUi.Muted);
            float summaryHeight = Mathf.Max(22, detail.preferredHeight); detail.rectTransform.sizeDelta = new Vector2(width, summaryHeight);
            float height = titleHeight + summaryHeight + 28;
            FigmaMissionUi.SetRect((RectTransform)row.transform, 12, y, 318, height);
            return height;
        }
    }
}
