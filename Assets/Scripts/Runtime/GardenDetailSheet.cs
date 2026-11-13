using System;
using System.Collections.Generic;
using System.Linq;
using CapstoneDesign.Prototype;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace CapstoneDesign.Runtime
{
    // Records and purchase previews share one modal contract. Only the body
    // scrolls; the title, close action and optional purchase footer stay visible.
    [ExecuteAlways, DisallowMultipleComponent]
    public sealed class GardenDetailSheet : MonoBehaviour
    {
        static GardenDetailSheet active;
        GardenModalContext context;
        Action closeRequested;
        bool released;
        public RectTransform Owner { get; private set; }
        public RectTransform Panel { get; private set; }
        public RectTransform Viewport { get; private set; }
        public RectTransform Content { get; private set; }
        public RectTransform Footer { get; private set; }
        public Text Heading { get; private set; }
        public Button CloseButton { get; private set; }
        public ScrollRect Scroll { get; private set; }
        public float FooterHeight;
        public float MaxHeight = 640;
        public event Action Closed;
        public static bool IsOpen => active != null && active.isActiveAndEnabled;

        // Callers supply the 390-unit safe-area frame fitted by
        // FigmaMissionCanvasFit. Their 366-unit body/footer content therefore
        // scales together with the title and close action at every pixel size.
        public static GardenDetailSheet Create(RectTransform owner, string prefix, string title, Action onClose,
            Button origin = null, ScrollRect underlyingScroll = null, CanvasGroup underlyingGroup = null)
        {
            if (owner == null || !owner.gameObject.activeInHierarchy) return null;
            if (active != null) active.Close(false);
            GardenMemoPopup.TryCloseOpen();
            var modalContext = GardenModalContext.Capture(owner, origin == null ? null : origin.gameObject,
                underlyingScroll, underlyingGroup);
            var overlay = FigmaMissionUi.Rect(owner, prefix + "Overlay", 0, 0, 390, owner.rect.height);
            overlay.anchorMin = Vector2.zero; overlay.anchorMax = Vector2.one;
            overlay.offsetMin = overlay.offsetMax = Vector2.zero;
            var modalGroup = overlay.gameObject.AddComponent<CanvasGroup>();
            modalGroup.ignoreParentGroups = true;
            var sheet = overlay.gameObject.AddComponent<GardenDetailSheet>();
            sheet.Owner = owner; sheet.context = modalContext; sheet.closeRequested = onClose;
            active = sheet;
            var dim = FigmaMissionUi.Box(overlay, prefix + "Dim", 0, 0, 390, owner.rect.height, new Color(0, 0, 0, .34f));
            dim.GetComponent<Image>().raycastTarget = true;
            dim.gameObject.AddComponent<FocusAdjustmentDimmerLayout>().Fit();
            sheet.Panel = FigmaMissionUi.Box(overlay, prefix + "Panel", 12, 150, 366, 640, Color.clear, 20);
            GardenUi.Round(sheet.Panel.GetComponent<Image>(), GardenSagePalette.Paper);
            sheet.Panel.GetComponent<Image>().raycastTarget = true;
            // A shared sheet uses the readable record palette even over the
            // shop's existing wood background; it never retints its owner.
            GardenThemeSurface.UseSageAppearance(sheet.Panel.gameObject);
            sheet.Panel.gameObject.AddComponent<FigmaGardenThemeScope>();
            sheet.Heading = FigmaMissionUi.Label(sheet.Panel, title, 20, 22, 254, 32, 22, FigmaMissionUi.Ink, true);
            sheet.Heading.name = prefix + "Title";
            sheet.CloseButton = FigmaMissionUi.Button(sheet.Panel, "닫기", 294, 12, 60, 44,
                sheet.RequestClose, Color.clear, FigmaMissionUi.Green, 15);
            sheet.CloseButton.name = prefix + "Close";
            sheet.Viewport = FigmaMissionUi.Box(sheet.Panel, prefix + "Scroll", 0, 72, 366, 540, Color.clear);
            sheet.Viewport.GetComponent<Image>().raycastTarget = true;
            sheet.Viewport.gameObject.AddComponent<RectMask2D>();
            sheet.Content = FigmaMissionUi.Rect(sheet.Viewport, prefix + "Content", 0, 0, 366, 540);
            sheet.Scroll = sheet.Viewport.gameObject.AddComponent<ScrollRect>();
            sheet.Scroll.viewport = sheet.Viewport; sheet.Scroll.content = sheet.Content;
            sheet.Scroll.horizontal = false; sheet.Scroll.movementType = ScrollRect.MovementType.Clamped;
            sheet.Scroll.scrollSensitivity = 30;
            sheet.Footer = FigmaMissionUi.Box(sheet.Panel, prefix + "Footer", 0, 540, 366, 0, GardenSagePalette.Paper);
            sheet.Footer.GetComponent<Image>().raycastTarget = true;
            sheet.Refresh(); overlay.SetAsLastSibling();
            EventSystem.current?.SetSelectedGameObject(sheet.CloseButton.gameObject);
            return sheet;
        }

        public void Refresh()
        {
            if (Panel == null) return;
            var frame = (RectTransform)transform;
            float height = frame.rect.height;
            if (height <= 0) return;
            float width = Mathf.Max(0, frame.rect.width - 24);
            float panelHeight = Mathf.Min(MaxHeight, Mathf.Max(0, height - 56));
            FigmaMissionUi.SetRect(Panel, 12, height - panelHeight, width, panelHeight);
            float titleWidth = Mathf.Max(0, width - 112);
            Heading.rectTransform.sizeDelta = new Vector2(titleWidth, Heading.rectTransform.sizeDelta.y);
            float titleHeight = Mathf.Max(32, Mathf.Ceil(Heading.preferredHeight));
            FigmaMissionUi.SetRect(Heading.rectTransform, 20, 22, titleWidth, titleHeight);
            FigmaMissionUi.SetRect((RectTransform)CloseButton.transform, width - 72, 12, 60, 44);
            float bodyTop = Mathf.Max(68, 22 + titleHeight + 16);
            float footer = Mathf.Clamp(FooterHeight, 0, Mathf.Max(0, panelHeight - bodyTop - 64));
            Footer.gameObject.SetActive(footer > 0);
            FigmaMissionUi.SetRect(Footer, 0, panelHeight - footer, width, footer);
            FigmaMissionUi.SetRect(Viewport, 0, bodyTop, width, Mathf.Max(0, panelHeight - bodyTop - footer - 16));
            Content.sizeDelta = new Vector2(width, Content.sizeDelta.y);
            CourseRecordsLayout.ClampScroll(Viewport);
        }

        public void RequestClose()
        {
            if (released || !isActiveAndEnabled) return;
            if (closeRequested != null) closeRequested(); else Close();
        }
        public static bool TryCloseOpen()
        {
            if (!IsOpen) return false;
            active.RequestClose(); return true;
        }
        public static void CloseForOwner(Transform owner, bool restoreSelection = false)
        {
            if (active == null || owner == null) return;
            if (active.Owner == owner || active.transform.IsChildOf(owner)) active.Close(restoreSelection);
        }
        public void Close(bool restoreSelection = true)
        {
            if (this == null || released) return;
            Release(restoreSelection);
            gameObject.SetActive(false);
            if (Application.isPlaying) Destroy(gameObject); else DestroyImmediate(gameObject);
        }
        void Release(bool restoreSelection)
        {
            if (released) return;
            released = true;
            if (active == this) active = null;
            context?.Restore(restoreSelection);
            var closed = Closed; Closed = null;
            closed?.Invoke();
        }
        void OnEnable() { Canvas.preWillRenderCanvases += Refresh; }
        void OnDisable()
        {
            Canvas.preWillRenderCanvases -= Refresh;
            bool externallyHidden = !released;
            Release(false);
            // Hiding a tab also closes its modal. It must not silently reappear
            // with a released input lock when that parent becomes active again.
            if (externallyHidden && gameObject.activeSelf)
            {
                gameObject.SetActive(false);
                if (Application.isPlaying) Destroy(gameObject);
            }
        }
        void OnDestroy() { Release(false); }
    }

    // A focus change can invoke a control's keyboard Reveal callback. Save all
    // affected offsets and restore them after focus, including nested lists.
    internal sealed class GardenModalContext
    {
        sealed class ScrollState
        {
            public ScrollRect Scroll;
            public bool Enabled;
            public Vector2 Position;
        }
        readonly List<ScrollState> scrolls = new List<ScrollState>();
        CanvasGroup group;
        bool interactable, blocksRaycasts, restored;
        GameObject selection;

        public static GardenModalContext Capture(Transform owner, GameObject origin = null,
            ScrollRect underlyingScroll = null, CanvasGroup underlyingGroup = null)
        {
            var result = new GardenModalContext { selection = origin != null ? origin : EventSystem.current?.currentSelectedGameObject };
            result.group = underlyingGroup != null ? underlyingGroup : owner.GetComponent<CanvasGroup>();
            if (result.group == null) result.group = owner.gameObject.AddComponent<CanvasGroup>();
            result.interactable = result.group.interactable; result.blocksRaycasts = result.group.blocksRaycasts;
            var candidates = owner.GetComponentsInChildren<ScrollRect>(false).AsEnumerable();
            if (result.selection != null) candidates = candidates.Concat(result.selection.GetComponentsInParent<ScrollRect>(false));
            if (underlyingScroll != null) candidates = candidates.Concat(new[] { underlyingScroll });
            foreach (var scroll in candidates.Distinct().Where(item => item != null && item.content != null))
            {
                result.scrolls.Add(new ScrollState { Scroll = scroll, Enabled = scroll.enabled, Position = scroll.content.anchoredPosition });
                scroll.StopMovement(); scroll.enabled = false;
            }
            result.group.interactable = false; result.group.blocksRaycasts = false;
            return result;
        }

        public void Restore(bool restoreSelection)
        {
            if (restored) return;
            restored = true;
            if (group != null) { group.interactable = interactable; group.blocksRaycasts = blocksRaycasts; }
            foreach (var item in scrolls) if (item.Scroll != null) item.Scroll.enabled = item.Enabled;
            if (restoreSelection && EventSystem.current != null)
                EventSystem.current.SetSelectedGameObject(selection != null && selection.activeInHierarchy ? selection : null);
            foreach (var item in scrolls)
            {
                if (item.Scroll == null || item.Scroll.content == null) continue;
                item.Scroll.content.anchoredPosition = item.Position;
                item.Scroll.StopMovement();
                if (item.Scroll.viewport != null) CourseRecordsLayout.ClampScroll(item.Scroll.viewport);
            }
        }
    }
}
