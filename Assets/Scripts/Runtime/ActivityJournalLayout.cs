using UnityEngine;
using UnityEngine.UI;

namespace CapstoneDesign.Runtime
{
    // Month/date context stays fixed while the selected day's records scroll.
    // Short phones compact calendar spacing, preserving 44-unit date targets.
    [ExecuteAlways]
    public sealed class ActivityJournalLayout : MonoBehaviour
    {
        public sealed class DayCell
        {
            public RectTransform Root, Border, Sprout;
            public int Column, Row;
        }
        public RectTransform Calendar, DayCard, List, Navigation;
        public Text Notice;
        public DayCell[] Cells;
        public int Weeks;
        public bool Empty;
        RectTransform[] weekdays;

        void OnEnable() { Canvas.preWillRenderCanvases += Refresh; }
        void OnDisable() { Canvas.preWillRenderCanvases -= Refresh; }

        public void Refresh()
        {
            if (Calendar == null || DayCard == null || List == null || Cells == null) return;
            float height = ((RectTransform)transform).rect.height;
            if (height <= 0) return;
            MissionBottomNavigation.FitRect(Navigation, height);
            float bottom = height - MissionBottomNavigation.ReservedHeight - 8;
            if (Notice != null && Notice.gameObject.activeSelf)
            {
                Notice.rectTransform.sizeDelta = new Vector2(342, Notice.rectTransform.sizeDelta.y);
                float message = Mathf.Max(24, Mathf.Ceil(Notice.preferredHeight));
                FigmaMissionUi.SetRect(Notice.rectTransform, 24, bottom - message, 342, message);
                bottom -= message + 8;
            }
            bool compact = bottom < 680;
            float dayHeight = compact ? 44 : 50, dayTop = compact ? 68 : 82;
            const float width = 342f / 7;
            float calendarHeight = dayTop + Weeks * dayHeight + 6;
            FigmaMissionUi.SetRect(Calendar, 24, FigmaMissionUi.PageHeaderContentTop, 342, calendarHeight);
            if (weekdays == null)
            {
                weekdays = new RectTransform[7];
                for (int i = 0; i < 7; i++) weekdays[i] = Calendar.Find("ActivityJournal.Weekday." + i) as RectTransform;
            }
            for (int i = 0; i < weekdays.Length; i++)
                if (weekdays[i] != null) FigmaMissionUi.SetRect(weekdays[i], i * width, dayTop - 24, width, 22);
            foreach (var cell in Cells)
            {
                FigmaMissionUi.SetRect(cell.Root, cell.Column * width, dayTop + cell.Row * dayHeight, width, dayHeight);
                if (cell.Sprout != null) cell.Sprout.anchoredPosition = new Vector2(0, 12 - dayHeight / 2);
                if (cell.Border != null)
                {
                    FigmaMissionUi.SetRect(cell.Border, 0, 0, width, dayHeight);
                    FigmaMissionUi.SetRect((RectTransform)cell.Border.Find("Bottom"), 2, dayHeight - 4, width - 4, 2);
                    FigmaMissionUi.SetRect((RectTransform)cell.Border.Find("Left"), 2, 4, 2, dayHeight - 8);
                    FigmaMissionUi.SetRect((RectTransform)cell.Border.Find("Right"), width - 4, 4, 2, dayHeight - 8);
                }
            }
            float cardTop = FigmaMissionUi.PageHeaderContentTop + calendarHeight + 12;
            float cardHeight = Mathf.Max(0, bottom - cardTop);
            if (Empty) cardHeight = Mathf.Min(cardHeight, 112);
            FigmaMissionUi.SetRect(DayCard, 24, cardTop, 342, cardHeight);
            FigmaMissionUi.SetRect(List, 0, 44, 342, Mathf.Max(0, cardHeight - 48));
            CourseRecordsLayout.ClampScroll(List);
            var scroll = List.GetComponent<ScrollRect>();
            if (scroll != null && scroll.verticalScrollbar != null)
                scroll.verticalScrollbar.gameObject.SetActive(scroll.content.rect.height > List.rect.height + .5f);
        }
    }
}
