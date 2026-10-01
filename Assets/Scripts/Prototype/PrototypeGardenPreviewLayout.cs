using UnityEngine;
using UnityEngine.UI;

namespace CapstoneDesign.Prototype
{
    // Presentation only: uses the empty space between the guide and footer, not a fixed 600x540 box.
    [ExecuteAlways, RequireComponent(typeof(RawImage))]
    public sealed class PrototypeGardenPreviewLayout : MonoBehaviour
    {
        public RectTransform upperBoundary, lowerBoundary;
        public PrototypeIslandDrag orbit;
        public Transform gardenRoot;
        public float horizontalPadding = 8;
        public float boundaryGap = 12;
        [Range(0, .2f)] public float edgeMargin = .02f;
        private bool boundsReady;
        private Vector3 measuredFocus;
        private Transform measuredRoot;
        private float horizontalRadius, radius;

        private void OnEnable()
        {
            boundsReady = false;
            Canvas.preWillRenderCanvases += Apply;
            Apply();
        }
        private void OnDisable() => Canvas.preWillRenderCanvases -= Apply;
        private void LateUpdate() => Apply();
        private void OnValidate() => boundsReady = false;

        public void Apply()
        {
            if (upperBoundary == null || lowerBoundary == null || orbit == null || gardenRoot == null) return;
            var rect = (RectTransform)transform;
            var parent = rect.parent as RectTransform;
            if (parent == null) return;
            float upper = parent.InverseTransformPoint(upperBoundary.TransformPoint(new Vector3(0, upperBoundary.rect.yMin, 0))).y;
            float lower = parent.InverseTransformPoint(lowerBoundary.TransformPoint(new Vector3(0, lowerBoundary.rect.yMax, 0))).y;
            if (!TryGetArea(parent.rect, upper, lower, horizontalPadding, boundaryGap, out Rect area)) return;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f);
            var position = area.center - parent.rect.center;
            if (rect.anchoredPosition != position) rect.anchoredPosition = position;
            if (rect.sizeDelta != area.size) rect.sizeDelta = area.size;

            // Only the visible preview controls the shared camera/texture.
            if (!isActiveAndEnabled || orbit.previewCamera == null || !orbit.previewCamera.orthographic) return;
            if (!boundsReady || measuredRoot != gardenRoot || measuredFocus != orbit.focus) RecalculateBounds();
            if (!boundsReady) return;
            float aspect = area.width / area.height;
            if (!TryGetSize(aspect, horizontalRadius, radius, edgeMargin, out float size)) return;
            var camera = orbit.previewCamera;
            // The RT is shared; override projection aspect to match the final UI rectangle.
            // This compensates the RawImage stretch without resizing/allocating a texture each frame.
            if (Mathf.Abs(camera.aspect - aspect) > .00001f) camera.aspect = aspect;
            if (Mathf.Abs(camera.orthographicSize - size) > .00001f) camera.orthographicSize = size;
        }

        public void RecalculateBounds()
        {
            boundsReady = false;
            horizontalRadius = radius = 0;
            if (gardenRoot == null || orbit == null) return;
            foreach (var filter in gardenRoot.GetComponentsInChildren<MeshFilter>(true))
            {
                if (filter.sharedMesh == null) continue;
                foreach (var vertex in filter.sharedMesh.vertices)
                {
                    var delta = filter.transform.TransformPoint(vertex) - orbit.focus;
                    horizontalRadius = Mathf.Max(horizontalRadius, new Vector2(delta.x, delta.z).magnitude);
                    radius = Mathf.Max(radius, delta.magnitude);
                }
            }
            measuredRoot = gardenRoot; measuredFocus = orbit.focus;
            boundsReady = radius > 0;
        }

        public static bool TryGetArea(Rect parent, float upper, float lower, float padding, float gap, out Rect area)
        {
            padding = Mathf.Max(0, padding); gap = Mathf.Max(0, gap);
            area = Rect.MinMaxRect(parent.xMin + padding, Mathf.Max(parent.yMin, lower + gap),
                parent.xMax - padding, Mathf.Min(parent.yMax, upper - gap));
            return area.width > 0 && area.height > 0;
        }

        public static bool TryGetSize(float aspect, float horizontalRadius, float radius, float margin, out float size)
        {
            size = 0;
            if (aspect <= 0 || horizontalRadius <= 0 || radius <= 0 || float.IsNaN(aspect) || float.IsInfinity(aspect)) return false;
            // Yaw changes camera right in XZ only; pitch does not change it. The full radius
            // bounds camera up at every pitch. Use both bounds without unnecessary square padding.
            size = Mathf.Max(radius, horizontalRadius / aspect) / (1 - 2 * Mathf.Clamp(margin, 0, .2f));
            return true;
        }
    }
}
