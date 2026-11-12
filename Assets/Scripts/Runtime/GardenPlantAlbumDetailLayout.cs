using UnityEngine;
using UnityEngine.UI;

namespace CapstoneDesign.Runtime
{
    // Card, records and navigation are siblings in the 390-point design.
    // Only RecordContent scrolls; dragging the preview remains a garden gesture.
    [ExecuteAlways, DisallowMultipleComponent, RequireComponent(typeof(RectTransform))]
    public sealed class GardenPlantAlbumDetailLayout : MonoBehaviour
    {
        public RectTransform Card, Preview, PlantName, Period, RecordHeading;
        public RectTransform RecordViewport, RecordContent, Back, Navigation;
        public Text Notice;
        public float RecordContentHeight;
        public float MinimumRecordHeight = 136;

        const float HeaderHeight = FigmaMissionUi.PageHeaderContentTop;
        const float HeadingHeight = 28;
        const float NoticeHeight = 56;
        RectTransform design;

        void OnEnable()
        {
            design = (RectTransform)transform;
            Canvas.preWillRenderCanvases += Refresh;
            Refresh();
        }

        void OnDisable() => Canvas.preWillRenderCanvases -= Refresh;

        public void Refresh()
        {
            if (design == null) design = transform as RectTransform;
            if (design == null) return;
            float height = design.rect.height;
            float navigationTop = height - MissionBottomNavigation.ReservedHeight;
            bool showNotice = Notice != null && Notice.gameObject.activeSelf;
            float recordBottom = navigationTop - 8 - (showNotice ? NoticeHeight : 0);
            float nameHeight = Measure(PlantName, 28);
            float periodHeight = Measure(Period, 24);
            float identityHeight = 8 + nameHeight + 4 + periodHeight + 8;

            // The removed button row gives its room to the photo. The bottom
            // shelf keeps the same inset and spacing as home and mission pages.
            // Reserve readable records first when a notice or short window needs room.
            float free = Mathf.Max(0, recordBottom - HeaderHeight - identityHeight
                - HeadingHeight - 12);
            float preferredPreview = Mathf.Clamp(238 + (height - 692) * 102 / 152, 238, 360);
            float previewHeight = Mathf.Min(preferredPreview, Mathf.Max(0, free - MinimumRecordHeight));
            if (free < MinimumRecordHeight + 96)
                previewHeight = Mathf.Min(96, free * .4f);

            Place(Card, 24, HeaderHeight, 342, identityHeight + previewHeight);
            // These three references are Card children; their coordinates are local.
            Place(PlantName, 16, 8, 310, nameHeight);
            Place(Period, 16, 12 + nameHeight, 310, periodHeight);
            Place(Preview, 16, identityHeight, 310, previewHeight);
            float headingTop = HeaderHeight + identityHeight + previewHeight + 8;
            Place(RecordHeading, 24, headingTop, 342, HeadingHeight);
            float recordTop = headingTop + HeadingHeight + 4;
            float recordHeight = Mathf.Max(0, recordBottom - recordTop);
            Place(RecordViewport, 24, recordTop, 342, recordHeight);

            if (RecordContent != null)
            {
                float contentHeight = Mathf.Max(recordHeight, RecordContentHeight);
                RecordContent.sizeDelta = new Vector2(342, contentHeight);
                RecordContent.anchoredPosition = new Vector2(0,
                    Mathf.Clamp(RecordContent.anchoredPosition.y, 0, contentHeight - recordHeight));
                var scroll = RecordViewport == null ? null : RecordViewport.GetComponent<ScrollRect>();
                if (scroll != null && scroll.verticalScrollbar != null)
                    scroll.verticalScrollbar.gameObject.SetActive(RecordContentHeight > recordHeight + .5f);
            }
            Place(Back, 24, 0, 110, 38);
            MissionBottomNavigation.FitRect(Navigation,height);
            if (showNotice)
            {
                Place(Notice.rectTransform, 24, recordBottom, 342, NoticeHeight);
                Notice.transform.SetAsLastSibling();
            }
        }

        static float Measure(RectTransform target, float minimum)
        {
            if (target == null) return minimum;
            target.sizeDelta = new Vector2(310, target.sizeDelta.y);
            var text = target.GetComponent<Text>();
            return text == null ? minimum : Mathf.Max(minimum, Mathf.Ceil(text.preferredHeight));
        }

        static void Place(RectTransform target, float x, float y, float width, float height)
        {
            if (target != null) FigmaMissionUi.SetRect(target, x, y, width, height);
        }
    }
}
