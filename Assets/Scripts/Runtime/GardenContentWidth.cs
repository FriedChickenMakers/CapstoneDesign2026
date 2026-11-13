using UnityEngine;

namespace CapstoneDesign.Runtime
{
    // Preserve the phone layout while keeping wide-screen reading and action
    // areas on the same centered column. Values are Canvas units, not pixels.
    [ExecuteAlways, RequireComponent(typeof(RectTransform))]
    public sealed class GardenContentWidth : MonoBehaviour
    {
        public const float MaximumWidth = 1408f; // 176 eight-unit spacing steps.
        RectTransform rect;
        float left, right;
        bool initialized;

        void OnEnable()
        {
            rect = (RectTransform)transform;
            if (!initialized)
            {
                left = rect.anchorMin.x;
                right = rect.anchorMax.x;
                initialized = true;
            }
            Canvas.preWillRenderCanvases += Apply;
            Apply();
        }

        void OnDisable() => Canvas.preWillRenderCanvases -= Apply;

        public void Apply()
        {
            if (rect == null || !(rect.parent is RectTransform parent) || parent.rect.width <= 0) return;
            float ratio = Mathf.Min(1, MaximumWidth / parent.rect.width);
            var min = new Vector2(.5f + (left - .5f) * ratio, rect.anchorMin.y);
            var max = new Vector2(.5f + (right - .5f) * ratio, rect.anchorMax.y);
            if (rect.anchorMin != min) rect.anchorMin = min;
            if (rect.anchorMax != max) rect.anchorMax = max;
        }
    }
}
