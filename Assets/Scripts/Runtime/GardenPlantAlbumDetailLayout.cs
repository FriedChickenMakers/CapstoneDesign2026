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

        const float HeaderHeight = 132;
        const float IdentityHeight = 48;
        const float HeadingHeight = 24;
        const float BackHeight = 48;
        const float NavigationHeight = 84;
        const float NoticeHeight = 56;
        const float MinimumRecordHeight = 144;
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
            float navigationTop = height - NavigationHeight;
            float backTop = navigationTop - BackHeight;
            bool showNotice = Notice != null && Notice.gameObject.activeSelf;
            float recordBottom = backTop - (showNotice ? NoticeHeight : 0);

            // The removed button row gives its 48 points to the photo. At
            // 360x640, preview is 204 and records 145; at 390x844, 288 and 212.
            // Reserve readable records first when a notice or short window needs room.
            float free = Mathf.Max(0, recordBottom - HeaderHeight - IdentityHeight
                - HeadingHeight - 8);
            float preferredPreview = Mathf.Clamp(204 + (height - 693) * 84 / 151, 204, 288);
            float previewHeight = Mathf.Min(preferredPreview, Mathf.Max(0, free - MinimumRecordHeight));
            if (free < MinimumRecordHeight + 96)
                previewHeight = Mathf.Min(96, free * .4f);

            Place(Card, 24, HeaderHeight, 342, IdentityHeight + previewHeight);
            // These three references are Card children; their coordinates are local.
            Place(PlantName, 16, 2, 310, 24);
            Place(Period, 16, 26, 310, 20);
            Place(Preview, 16, IdentityHeight, 310, previewHeight);
            float headingTop = HeaderHeight + IdentityHeight + previewHeight + 4;
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
            }
            Place(Back, 24, backTop, 342, BackHeight);
            Place(Navigation, 0, navigationTop, 390, NavigationHeight);
            if (showNotice)
            {
                Place(Notice.rectTransform, 24, recordBottom, 342, NoticeHeight);
                Notice.transform.SetAsLastSibling();
            }
        }

        static void Place(RectTransform target, float x, float y, float width, float height)
        {
            if (target != null) FigmaMissionUi.SetRect(target, x, y, width, height);
        }
    }
}
