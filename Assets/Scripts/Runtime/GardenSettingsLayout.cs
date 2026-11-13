using System;
using CapstoneDesign.Prototype;
using UnityEngine;
using UnityEngine.UI;

namespace CapstoneDesign.Runtime
{
    // Settings keep the existing shared bottom navigation. The page's fixed
    // heading and optional actions leave one bounded reading/interaction area.
    [ExecuteAlways, DisallowMultipleComponent]
    public sealed class GardenSettingsLayout : MonoBehaviour
    {
        public RectTransform Viewport, Content, Footer;
        public float FooterHeight;
        void OnEnable() { Canvas.preWillRenderCanvases += Refresh; }
        void OnDisable() { Canvas.preWillRenderCanvases -= Refresh; }

        public static Button BuildHeader(RectTransform parent, string title, string back, Action close, string prefix)
        {
            var plaque = FigmaMissionUi.Box(parent, prefix + ".Header", FigmaMissionUi.PageHeaderPlaqueX,
                FigmaMissionUi.PageHeaderPlaqueY, FigmaMissionUi.PageHeaderPlaqueWidth, FigmaMissionUi.PageHeaderPlaqueHeight,
                FigmaMissionUi.Warm, 10);
            GardenThemeSurface.Apply(plaque.gameObject, GardenThemeSurface.Style.Header);
            var heading = FigmaMissionUi.Label(parent, title, FigmaMissionUi.PageHeaderTitleX, FigmaMissionUi.PageHeaderTitleY,
                FigmaMissionUi.PageHeaderTitleWidth, FigmaMissionUi.PageHeaderTitleHeight, 28, GardenThemeSurface.WoodInk, true, TextAnchor.MiddleCenter);
            heading.name = prefix + ".Title";
            if (string.IsNullOrEmpty(back)) return null;
            var button = FigmaMissionUi.Button(parent, back, 24, 0, 118, 38, close, Color.clear, FigmaMissionUi.Green, 15);
            button.name = prefix + ".Back";
            return button;
        }

        public void Refresh()
        {
            var frame = transform as RectTransform;
            if (frame == null || frame.rect.height <= 0 || Viewport == null) return;
            float bottom = frame.rect.height - MissionBottomNavigation.ReservedHeight - 8;
            var navigation = transform.root.GetComponent<MockupNavigation>();
            RectTransform shelf = navigation?.home?.NavigationBar != null
                ? (RectTransform)navigation.home.NavigationBar.transform
                : navigation?.legacyBottomNavigation?.transform as RectTransform;
            if (shelf != null && shelf.gameObject.activeInHierarchy)
            {
                var corners = new Vector3[4]; shelf.GetWorldCorners(corners);
                var sourceCanvas = shelf.GetComponentInParent<Canvas>();
                var targetCanvas = GetComponentInParent<Canvas>();
                Camera sourceCamera = sourceCanvas == null || sourceCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : sourceCanvas.worldCamera;
                Camera targetCamera = targetCanvas == null || targetCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : targetCanvas.worldCamera;
                if (RectTransformUtility.ScreenPointToLocalPointInRectangle(frame,
                    RectTransformUtility.WorldToScreenPoint(sourceCamera, corners[1]), targetCamera, out var top))
                {
                    float shelfTop = frame.rect.yMax - top.y;
                    if (shelfTop > FigmaMissionUi.PageHeaderContentTop && shelfTop < frame.rect.height)
                        bottom = Mathf.Min(bottom, shelfTop - 8);
                }
            }
            if (Footer != null)
            {
                FigmaMissionUi.SetRect(Footer, 24, bottom - FooterHeight, 342, FooterHeight);
                bottom -= FooterHeight + 8;
            }
            FigmaMissionUi.SetRect(Viewport, 0, FigmaMissionUi.PageHeaderContentTop, 390,
                Mathf.Max(0, bottom - FigmaMissionUi.PageHeaderContentTop));
            CourseRecordsLayout.ClampScroll(Viewport);
        }
    }
}
