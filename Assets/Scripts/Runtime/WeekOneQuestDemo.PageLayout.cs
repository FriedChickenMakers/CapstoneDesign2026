using UnityEngine;
using UnityEngine.UI;

namespace CapstoneDesign.Runtime
{
    public sealed partial class WeekOneQuestDemo
    {
        GardenPageLayout ConfigureGardenPage(RectTransform root, RectTransform body, float contentHeight,
            RectTransform footer = null, float footerHeight = 0, Button back = null,
            float top = FigmaMissionUi.PageHeaderContentTop)
        {
            var mobile = root.GetComponent<FocusMobilePageLayout>();
            if (mobile != null) mobile.FixedContent = true;
            var layout = root.gameObject.AddComponent<GardenPageLayout>();
            layout.Viewport = body.parent as RectTransform;
            layout.Content = body; layout.ContentHeight = contentHeight;
            layout.Footer = footer; layout.FooterHeight = footerHeight;
            layout.Back = back == null ? null : (RectTransform)back.transform;
            layout.Notice = notice; layout.Top = top;
            var navigation = root.GetComponentInChildren<MissionBottomNavigation>();
            layout.Navigation = navigation == null ? null : (RectTransform)navigation.transform;
            layout.Refresh();
            return layout;
        }
    }
}
