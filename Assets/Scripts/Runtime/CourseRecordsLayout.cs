using UnityEngine;
using UnityEngine.UI;

namespace CapstoneDesign.Runtime
{
    // The course summary and week picker stay in view; only the six-practice
    // list scrolls. Measurements use the shared 390-point safe-area design.
    [ExecuteAlways]
    public sealed class CourseRecordsLayout : MonoBehaviour
    {
        public RectTransform Summary, Weeks, WeekHeading, Viewport, Navigation;
        public Text Notice;
        public int WeekRows = 2;

        void OnEnable() { Canvas.preWillRenderCanvases += Refresh; }
        void OnDisable() { Canvas.preWillRenderCanvases -= Refresh; }

        public void Refresh()
        {
            float height = ((RectTransform)transform).rect.height;
            if (height <= 0) return;
            MissionBottomNavigation.FitRect(Navigation, height);
            if (Summary != null) FigmaMissionUi.SetRect(Summary, 24, 110, 342, 104);
            float weeksHeight = Mathf.Max(1, WeekRows) * 60 - 8;
            if (Weeks != null) FigmaMissionUi.SetRect(Weeks, 24, 226, 342, weeksHeight);
            float headingTop = 226 + weeksHeight + 12;
            if (WeekHeading != null) FigmaMissionUi.SetRect(WeekHeading, 24, headingTop, 342, 30);
            float listTop = headingTop + 38;
            float bottom = height - MissionBottomNavigation.ReservedHeight - 8;
            if (Notice != null && Notice.gameObject.activeSelf)
            {
                Notice.rectTransform.sizeDelta = new Vector2(342, Notice.rectTransform.sizeDelta.y);
                float noticeHeight = Mathf.Max(24, Mathf.Ceil(Notice.preferredHeight));
                FigmaMissionUi.SetRect(Notice.rectTransform, 24, bottom - noticeHeight, 342, noticeHeight);
                bottom -= noticeHeight + 8;
            }
            if (Viewport != null)
            {
                FigmaMissionUi.SetRect(Viewport, 0, listTop, 390, Mathf.Max(0, bottom - listTop));
                ClampScroll(Viewport);
            }
        }

        internal static void ClampScroll(RectTransform viewport)
        {
            var scroll = viewport.GetComponent<ScrollRect>();
            if (scroll == null || scroll.content == null) return;
            var content = scroll.content;
            float max = Mathf.Max(0, content.rect.height - viewport.rect.height);
            var position = content.anchoredPosition;
            float y = Mathf.Clamp(position.y, 0, max);
            if (!Mathf.Approximately(y, position.y))
            {
                content.anchoredPosition = new Vector2(position.x, y);
                scroll.StopMovement();
            }
        }
    }

    // This sheet uses the same safe-area frame as its underlying course page.
    // The heading and close action remain visible while long records scroll.
    [ExecuteAlways]
    public sealed class CourseRecordSheetLayout : MonoBehaviour
    {
        public RectTransform Panel, Title, Viewport, Close;
        public Text Heading;

        void OnEnable() { Canvas.preWillRenderCanvases += Refresh; }
        void OnDisable() { Canvas.preWillRenderCanvases -= Refresh; }

        public void Refresh()
        {
            if (Panel == null) return;
            var frame = (RectTransform)transform;
            float height = frame.rect.height;
            if (height <= 0) return;
            float width = Mathf.Max(0, frame.rect.width - 24);
            float panelHeight = Mathf.Min(640, Mathf.Max(0, height - 56));
            FigmaMissionUi.SetRect(Panel, 12, height - panelHeight, width, panelHeight);
            float titleWidth = Mathf.Max(0, width - 112);
            float titleHeight = 32;
            if (Heading != null)
            {
                Heading.rectTransform.sizeDelta = new Vector2(titleWidth, Heading.rectTransform.sizeDelta.y);
                titleHeight = Mathf.Max(32, Mathf.Ceil(Heading.preferredHeight));
            }
            if (Title != null) FigmaMissionUi.SetRect(Title, 20, 22, titleWidth, titleHeight);
            if (Close != null) FigmaMissionUi.SetRect(Close, width - 72, 12, 60, 44);
            float bodyTop = Mathf.Max(68, 22 + titleHeight + 16);
            if (Viewport != null)
            {
                FigmaMissionUi.SetRect(Viewport, 0, bodyTop, width, Mathf.Max(0, panelHeight - bodyTop - 16));
                CourseRecordsLayout.ClampScroll(Viewport);
            }
        }
    }
}
