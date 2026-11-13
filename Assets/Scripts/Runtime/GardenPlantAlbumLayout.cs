using UnityEngine;
using UnityEngine.UI;

namespace CapstoneDesign.Runtime
{
    // The album keeps its context and pagination fixed. Only the bounded page
    // of real plant thumbnails scrolls inside the shared 390-unit safe area.
    [ExecuteAlways, DisallowMultipleComponent, RequireComponent(typeof(RectTransform))]
    public sealed class GardenPlantAlbumLayout : MonoBehaviour
    {
        public RectTransform Current, Count, Viewport, Content, Pager, Back, Navigation;
        public Text Notice;
        public float ContentHeight;
        void OnEnable() { Canvas.preWillRenderCanvases += Refresh; }
        void OnDisable() { Canvas.preWillRenderCanvases -= Refresh; }

        public void Refresh()
        {
            float height = ((RectTransform)transform).rect.height;
            if (height <= 0) return;
            Place(Back, 24, 0, 100, 38);
            float top = FigmaMissionUi.PageHeaderContentTop;
            if (Current != null && Current.gameObject.activeSelf)
            {
                Place(Current, 24, top, 342, 108); top += 120;
            }
            Place(Count, 24, top, 342, 28); top += 36;
            float bottom = height - MissionBottomNavigation.ReservedHeight - 8;
            if (Pager != null && Pager.gameObject.activeSelf)
            {
                bottom -= 56; Place(Pager, 0, bottom, 390, 56);
            }
            if (Notice != null && Notice.gameObject.activeSelf)
            {
                float noticeHeight = Mathf.Max(28, Mathf.Ceil(Notice.preferredHeight));
                bottom -= noticeHeight + 8;
                Place(Notice.rectTransform, 24, bottom, 342, noticeHeight);
            }
            float viewportHeight = Mathf.Max(0, bottom - top);
            Place(Viewport, 0, top, 390, viewportHeight);
            if (Content != null)
            {
                Content.sizeDelta = new Vector2(390, Mathf.Max(viewportHeight, ContentHeight));
                if (Viewport != null) CourseRecordsLayout.ClampScroll(Viewport);
                var scroll = Viewport == null ? null : Viewport.GetComponent<ScrollRect>();
                if (scroll != null && scroll.verticalScrollbar != null)
                    scroll.verticalScrollbar.gameObject.SetActive(ContentHeight > viewportHeight + .5f);
            }
            MissionBottomNavigation.FitRect(Navigation, height);
        }

        static void Place(RectTransform target, float x, float y, float width, float height)
        {
            if (target != null) FigmaMissionUi.SetRect(target, x, y, width, height);
        }
    }
}
