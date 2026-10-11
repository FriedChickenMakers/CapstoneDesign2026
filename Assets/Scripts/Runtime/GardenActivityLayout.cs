using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace CapstoneDesign.Runtime
{
    // Content scrolls between two independent surfaces. Keyboard insets move the
    // action surface with the visible screen instead of moving it with the text.
    [ExecuteAlways]
    public sealed class GardenActivityLayout : MonoBehaviour
    {
        public RectTransform Header, Body, Footer;
        RectTransform frame;
        float lastKeyboard = -1;
        GameObject lastInput;
#if UNITY_ANDROID && !UNITY_EDITOR
        AndroidJavaObject decor, visibleFrame;
        float nextKeyboardRead, coveredPixels;
#endif

        void OnEnable()
        {
            frame = (RectTransform)transform;
            Canvas.preWillRenderCanvases += RefreshLayout;
        }
        void OnDisable()
        {
            Canvas.preWillRenderCanvases -= RefreshLayout;
#if UNITY_ANDROID && !UNITY_EDITOR
            decor?.Dispose(); visibleFrame?.Dispose(); decor = visibleFrame = null;
#endif
        }

        public void RefreshLayout()
        {
            if (Header == null || Body == null || Footer == null || frame == null) return;
            float keyboard = 0;
#if UNITY_ANDROID && !UNITY_EDITOR
            float keyboardPixels = KeyboardHeight();
            if (keyboardPixels > 0 && Screen.height > 0)
            {
                var canvas = GetComponentInParent<Canvas>();
                Camera eventCamera = canvas == null || canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
                if (RectTransformUtility.ScreenPointToLocalPointInRectangle(frame,
                    new Vector2(0, keyboardPixels), eventCamera, out var keyboardTop))
                    keyboard = Mathf.Max(0, keyboardTop.y - frame.rect.yMin);
            }
#endif
            float headerHeight = LayoutUtility.GetPreferredHeight(Header);
            float footerHeight = LayoutUtility.GetPreferredHeight(Footer);
            Header.sizeDelta = new Vector2(0, headerHeight);
            Footer.sizeDelta = new Vector2(0, footerHeight);
            Footer.anchoredPosition = new Vector2(0, keyboard);
            Body.offsetMax = new Vector2(0, -headerHeight);
            Body.offsetMin = new Vector2(0, footerHeight + keyboard);
            var selected = EventSystem.current == null ? null : EventSystem.current.currentSelectedGameObject;
            if (selected != lastInput || !Mathf.Approximately(keyboard, lastKeyboard))
            {
                lastInput = selected;
                lastKeyboard = keyboard;
                var field = selected == null ? null : selected.GetComponent<InputField>();
                if (field != null && field.isFocused && field.transform.IsChildOf(Body)) Reveal(field);
            }
        }

#if UNITY_ANDROID && !UNITY_EDITOR
        float KeyboardHeight()
        {
            if (!TouchScreenKeyboard.visible) { coveredPixels = 0; nextKeyboardRead = 0; return 0; }
            if (Time.unscaledTime < nextKeyboardRead) return coveredPixels;
            nextKeyboardRead = Time.unscaledTime + .1f;
            // Unity 6.3 returns an empty TouchScreenKeyboard.area on Android.
            // Read window geometry only; do not replace Unity's inset listener.
            try
            {
                if (decor == null)
                {
                    using var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
                    using var activity = player.GetStatic<AndroidJavaObject>("currentActivity");
                    using var window = activity.Call<AndroidJavaObject>("getWindow");
                    decor = window.Call<AndroidJavaObject>("getDecorView");
                    visibleFrame = new AndroidJavaObject("android.graphics.Rect");
                }
                decor.Call("getWindowVisibleDisplayFrame", visibleFrame);
                coveredPixels = Mathf.Max(0, Screen.height - visibleFrame.Get<int>("bottom"));
            }
            catch (AndroidJavaException) { coveredPixels = 0; }
            return coveredPixels;
        }
#endif

        void Reveal(InputField field)
        {
            var scroll = Body.GetComponent<ScrollRect>();
            if (scroll == null || scroll.content.rect.height <= Body.rect.height) return;
            var bounds = RectTransformUtility.CalculateRelativeRectTransformBounds(Body, field.transform);
            float bottom = Body.rect.yMin + 16;
            float top = Body.rect.yMax - 16;
            float shift = bounds.min.y < bottom ? bottom - bounds.min.y : bounds.max.y > top ? top - bounds.max.y : 0;
            var position = scroll.content.anchoredPosition;
            position.y = Mathf.Clamp(position.y + shift, 0, Mathf.Max(0, scroll.content.rect.height - Body.rect.height));
            scroll.content.anchoredPosition = position;
        }
    }
}
