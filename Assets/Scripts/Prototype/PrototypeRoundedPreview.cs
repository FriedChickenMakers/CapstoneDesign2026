using UnityEngine;
using UnityEngine.UI;

namespace CapstoneDesign.Prototype
{
    // Round the existing RawImage mesh; the texture, projection and drag target
    // keep their identity and no extra render texture or masking camera is used.
    [RequireComponent(typeof(RawImage))]
    public sealed class PrototypeRoundedPreview : BaseMeshEffect, ICanvasRaycastFilter
    {
        const float Radius = 28;
        public override void ModifyMesh(VertexHelper vertices)
        {
            if (!IsActive()) return;
            var image = (RawImage)graphic;
            Rect rect = image.GetPixelAdjustedRect();
            if (rect.width <= 0 || rect.height <= 0) return;
            float radius = Mathf.Min(Radius, Mathf.Min(rect.width, rect.height) * .5f);
            vertices.Clear();
            Add(vertices, image, rect, rect.center);
            const int segments = 8;
            for (int corner = 0; corner < 4; corner++)
            {
                Vector2 center = corner == 0 ? new Vector2(rect.xMin + radius, rect.yMax - radius)
                    : corner == 1 ? new Vector2(rect.xMin + radius, rect.yMin + radius)
                    : corner == 2 ? new Vector2(rect.xMax - radius, rect.yMin + radius)
                    : new Vector2(rect.xMax - radius, rect.yMax - radius);
                for (int point = 0; point <= segments; point++)
                {
                    float angle = (90 + corner * 90 + point * 90f / segments) * Mathf.Deg2Rad;
                    Add(vertices, image, rect, center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius);
                }
            }
            int count = 4 * (segments + 1);
            for (int index = 1; index <= count; index++) vertices.AddTriangle(0, index, index == count ? 1 : index + 1);
        }
        static void Add(VertexHelper vertices, RawImage image, Rect rect, Vector2 point)
        {
            var uv = image.uvRect;
            vertices.AddVert(point, image.color, new Vector2(
                uv.x + (point.x - rect.xMin) / rect.width * uv.width,
                uv.y + (point.y - rect.yMin) / rect.height * uv.height));
        }
        public bool IsRaycastLocationValid(Vector2 screenPoint, Camera eventCamera)
        {
            if (!IsActive()) return true;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle((RectTransform)transform, screenPoint, eventCamera, out var point)) return false;
            var rect = ((RectTransform)transform).rect;
            if (!rect.Contains(point)) return false;
            float radius = Mathf.Min(Radius, Mathf.Min(rect.width, rect.height) * .5f);
            var nearest = new Vector2(Mathf.Clamp(point.x, rect.xMin + radius, rect.xMax - radius),
                Mathf.Clamp(point.y, rect.yMin + radius, rect.yMax - radius));
            return (point - nearest).sqrMagnitude <= radius * radius;
        }
    }
}
