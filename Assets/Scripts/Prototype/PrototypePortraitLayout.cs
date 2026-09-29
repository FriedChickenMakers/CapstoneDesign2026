using UnityEngine;

namespace CapstoneDesign.Prototype
{
    [ExecuteAlways]
    public sealed class PrototypePortraitLayout : MonoBehaviour
    {
        public RectTransform designFrame;
        public Vector2 referenceSize = new Vector2(720, 1280);
        private void OnEnable()
        {
            Canvas.preWillRenderCanvases += Apply;
            Apply();
        }
        private void OnDisable() => Canvas.preWillRenderCanvases -= Apply;
        private void Update() => Apply();
        public void Apply()
        {
            if (designFrame == null) return;
            var canvas = (RectTransform)transform;
            if (!TryCalculate(new Vector2(Screen.width, Screen.height), canvas.rect.size,
                Screen.safeArea, referenceSize, out var size, out float scale, out var position)) return;
            designFrame.anchorMin = designFrame.anchorMax = new Vector2(.5f, .5f);
            designFrame.pivot = new Vector2(.5f, .5f);
            // One stable shell for every page. Never resize the header/navigation on tab changes.
            if (designFrame.sizeDelta != size) designFrame.sizeDelta = size;
            if (designFrame.localScale != Vector3.one * scale) designFrame.localScale = Vector3.one * scale;
            if (designFrame.anchoredPosition != position) designFrame.anchoredPosition = position;
        }

        // Screen/safe-area math is independent of page state and can be tested without a device.
        public static bool TryCalculate(Vector2 screen, Vector2 canvas, Rect safe, Vector2 reference,
            out Vector2 size, out float scale, out Vector2 position)
        {
            size = position = Vector2.zero; scale = 0;
            if (screen.x <= 0 || screen.y <= 0 || canvas.x <= 0 || canvas.y <= 0
                || reference.x <= 0 || reference.y <= 0) return false;
            safe = Rect.MinMaxRect(Mathf.Max(0, safe.xMin), Mathf.Max(0, safe.yMin),
                Mathf.Min(screen.x, safe.xMax), Mathf.Min(screen.y, safe.yMax));
            if (safe.width <= 0 || safe.height <= 0) return false;
            var units = new Vector2(canvas.x / screen.x, canvas.y / screen.y);
            scale = Mathf.Min(safe.width * units.x / reference.x, safe.height * units.y / reference.y);
            size = new Vector2(reference.x, Mathf.Max(reference.y, safe.height * units.y / scale));
            position = Vector2.Scale(safe.center - screen * .5f, units);
            return true;
        }
    }
}
