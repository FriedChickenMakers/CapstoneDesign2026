using UnityEngine;
using UnityEngine.UI;

namespace CapstoneDesign.Runtime
{
    // Shared reading/form layout: context stays above the viewport, actions below.
    // Only the content scrolls, and a reflow preserves its exact reading position.
    [ExecuteAlways, DisallowMultipleComponent, RequireComponent(typeof(RectTransform))]
    public sealed class GardenPageLayout : MonoBehaviour
    {
        public RectTransform Viewport, Content, Footer, Navigation, Back;
        public Text Notice;
        public float Top = FigmaMissionUi.PageHeaderContentTop;
        public float ContentHeight, FooterHeight, BackWidth = 110;
        void OnEnable() { Canvas.preWillRenderCanvases += Refresh; }
        void OnDisable() { Canvas.preWillRenderCanvases -= Refresh; }

        public void Refresh()
        {
            float height = ((RectTransform)transform).rect.height;
            if (height <= 0) return;
            if (Back != null) FigmaMissionUi.SetRect(Back, 24, 0, BackWidth, 38);
            var mobile = GetComponent<FocusMobilePageLayout>();
            float bottom = height - (Navigation == null ? 16 : MissionBottomNavigation.ReservedHeight + 8)
                - (mobile == null ? 0 : mobile.BottomInset);
            if (Footer != null && Footer.gameObject.activeSelf)
            {
                bottom -= FooterHeight;
                FigmaMissionUi.SetRect(Footer, 0, bottom, 390, FooterHeight);
                bottom -= 8;
            }
            if (Notice != null && Notice.gameObject.activeSelf)
            {
                Notice.rectTransform.sizeDelta = new Vector2(342, Notice.rectTransform.sizeDelta.y);
                float noticeHeight = Mathf.Max(28, Mathf.Ceil(Notice.preferredHeight));
                bottom -= noticeHeight + 8;
                FigmaMissionUi.SetRect(Notice.rectTransform, 24, bottom, 342, noticeHeight);
            }
            float viewportHeight = Mathf.Max(0, bottom - Top);
            if (Viewport != null) FigmaMissionUi.SetRect(Viewport, 0, Top, 390, viewportHeight);
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
    }
}
