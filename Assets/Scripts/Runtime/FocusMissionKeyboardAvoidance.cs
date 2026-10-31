using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace CapstoneDesign.Runtime
{
    public struct FocusKeyboardLayout
    {
        public Rect Input;
        public Rect Done;
    }

    // Temporarily presents the focused form above the native keyboard. The original
    // Figma layout and text size are restored when editing ends; the page is not shrunk.
    public sealed class FocusMissionKeyboardAvoidance : MonoBehaviour
    {
        public InputField Input;
        public string Title;
        public Color Background, Accent;
        public Action Save;
        RectTransform design, originalParent, inputRect, overlay;
        Vector2 originalAnchorsMin, originalAnchorsMax, originalPivot, originalPosition, originalSize;
        Vector2 originalTextSize, originalPlaceholderSize;
        Vector3 originalInputScale;
        float normalScale = 1, normalParentWidth, normalTopGap;
        Button done;
        bool editing;
#if UNITY_ANDROID && !UNITY_EDITOR
        AndroidJavaObject decor, visibleFrame;
        float nextKeyboardRead, coveredPixels;
#endif

        void OnEnable()
        {
            design = (RectTransform)transform;
            Canvas.preWillRenderCanvases += RefreshLayout;
        }

        void OnDisable()
        {
            Canvas.preWillRenderCanvases -= RefreshLayout;
#if UNITY_ANDROID && !UNITY_EDITOR
            decor?.Dispose(); visibleFrame?.Dispose(); decor = visibleFrame = null;
#endif
        }

        public static FocusKeyboardLayout CalculateLayout(float availableHeight, float originalInputTop, float originalInputHeight)
        {
            float available = Mathf.Max(1, availableHeight);
            float gap = Mathf.Min(12, available * .04f);
            float actionHeight = Mathf.Min(52, available * .25f);
            float actionTop = Mathf.Max(0, available - gap - actionHeight);
            float titleSpace = Mathf.Min(48, available * .24f);
            float inputHeight = Mathf.Min(Mathf.Max(1, originalInputHeight), Mathf.Max(1, actionTop - gap - titleSpace));
            float inputTop = Mathf.Clamp(originalInputTop, titleSpace, Mathf.Max(titleSpace, actionTop - gap - inputHeight));
            return new FocusKeyboardLayout
            {
                Input = new Rect(24, inputTop, 342, inputHeight),
                Done = new Rect(24, actionTop, 342, actionHeight)
            };
        }

        void RefreshLayout()
        {
            if (Input == null || design == null) return;
            bool keyboard = false;
            float covered = 0;
#if UNITY_ANDROID && !UNITY_EDITOR
            keyboard = TouchScreenKeyboard.visible;
            if (keyboard) covered = KeyboardHeight();
#elif UNITY_IOS && !UNITY_EDITOR
            keyboard = TouchScreenKeyboard.visible;
            if (keyboard) covered = TouchScreenKeyboard.area.height;
#endif
            var parent = design.parent as RectTransform;
            if (!keyboard || !Input.isFocused)
            {
                RestoreForm();
                if (!keyboard && parent != null)
                {
                    normalScale = design.localScale.x; normalParentWidth = parent.rect.width;
                    normalTopGap = (parent.rect.height - design.rect.height * normalScale) * .5f;
                }
                return;
            }
            // If Android resizes the Unity window, preserve the pre-keyboard text scale
            // and top position instead of accepting the canvas fitter's tiny full page.
            if (parent != null && normalParentWidth > 0)
            {
                float widthRatio = parent.rect.width / normalParentWidth;
                float scale = normalScale * widthRatio;
                design.localScale = Vector3.one * scale;
                design.anchoredPosition = new Vector2(0, parent.rect.height * .5f - normalTopGap * widthRatio - design.rect.height * scale * .5f);
            }
            var canvas = GetComponentInParent<Canvas>();
            Camera eventCamera = canvas == null || canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(design, new Vector2(Screen.width * .5f, covered), eventCamera, out var visibleBottom)) return;
            float available = Mathf.Clamp(design.rect.yMax - visibleBottom.y, 1, design.rect.height);
            PresentForm(available);
        }

        void PresentForm(float availableHeight)
        {
            if (!editing)
            {
                inputRect = (RectTransform)Input.transform; originalParent = (RectTransform)inputRect.parent;
                originalAnchorsMin = inputRect.anchorMin; originalAnchorsMax = inputRect.anchorMax;
                originalPivot = inputRect.pivot; originalPosition = inputRect.anchoredPosition; originalSize = inputRect.sizeDelta; originalInputScale = inputRect.localScale;
                originalTextSize = Input.textComponent.rectTransform.sizeDelta;
                if (Input.placeholder != null) originalPlaceholderSize = Input.placeholder.rectTransform.sizeDelta;
                overlay = FigmaMissionUi.Box(design, "키보드 입력 화면", 0, 0, 390, design.rect.height, Background);
                overlay.GetComponent<Image>().raycastTarget = true;
                FigmaMissionUi.Label(overlay, Title ?? "내용 입력", 24, 16, 342, 32, 22, Accent, true);
                done = FigmaMissionUi.Button(overlay, "입력 마치기", 24, 760, 342, 52, FinishEditing, Accent, Color.white);
                done.name = "Focus.KeyboardDone";
                inputRect.SetParent(overlay, false);
                inputRect.anchorMin = inputRect.anchorMax = new Vector2(0, 1); inputRect.pivot = new Vector2(0, 1); inputRect.localScale = Vector3.one;
                editing = true;
            }
            var layout = CalculateLayout(availableHeight, -originalPosition.y, originalSize.y);
            inputRect.anchoredPosition = new Vector2(layout.Input.x, -layout.Input.y);
            inputRect.sizeDelta = layout.Input.size;
            Input.textComponent.rectTransform.sizeDelta = layout.Input.size - new Vector2(32, 32);
            if (Input.placeholder != null) Input.placeholder.rectTransform.sizeDelta = layout.Input.size - new Vector2(32, 32);
            var actionRect = (RectTransform)done.transform;
            actionRect.anchoredPosition = new Vector2(layout.Done.x, -layout.Done.y); actionRect.sizeDelta = layout.Done.size;
            var actionText = done.GetComponentInChildren<Text>();
            if (actionText != null) actionText.rectTransform.sizeDelta = new Vector2(layout.Done.width - 16, layout.Done.height);
            overlay.SetAsLastSibling();
        }

        void FinishEditing()
        {
            if (Input == null) return;
            var keyboard = Input.touchScreenKeyboard;
            if (keyboard != null)
            {
                if (!Input.readOnly) Input.text = keyboard.text;
                keyboard.active = false;
            }
            Input.DeactivateInputField();
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
            Save?.Invoke(); RestoreForm();
        }

        void RestoreForm()
        {
            if (!editing) return;
            editing = false;
            if (inputRect != null && originalParent != null)
            {
                inputRect.SetParent(originalParent, false);
                inputRect.anchorMin = originalAnchorsMin; inputRect.anchorMax = originalAnchorsMax; inputRect.pivot = originalPivot;
                inputRect.anchoredPosition = originalPosition; inputRect.sizeDelta = originalSize; inputRect.localScale = originalInputScale;
                Input.textComponent.rectTransform.sizeDelta = originalTextSize;
                if (Input.placeholder != null) Input.placeholder.rectTransform.sizeDelta = originalPlaceholderSize;
            }
            if (overlay != null)
            {
                overlay.gameObject.SetActive(false);
                if (Application.isPlaying) Destroy(overlay.gameObject); else DestroyImmediate(overlay.gameObject);
            }
            overlay = null; done = null;
        }

#if UNITY_ANDROID && !UNITY_EDITOR
        float KeyboardHeight()
        {
            if (Time.unscaledTime < nextKeyboardRead) return coveredPixels;
            nextKeyboardRead = Time.unscaledTime + .1f;
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
                // Unity 6.3's Android keyboard.area is empty. Match the existing
                // activity layout's window-geometry strategy without replacing listeners.
                decor.Call("getWindowVisibleDisplayFrame", visibleFrame);
                coveredPixels = Mathf.Max(0, Screen.height - visibleFrame.Get<int>("bottom"));
            }
            catch (AndroidJavaException) { coveredPixels = 0; }
            return coveredPixels;
        }
#endif
    }
}
