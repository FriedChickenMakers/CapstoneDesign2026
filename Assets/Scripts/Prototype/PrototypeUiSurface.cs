using UnityEngine;
using UnityEngine.UI;

namespace CapstoneDesign.Prototype
{
    // Editable uGUI geometry: no bitmap artwork or runtime layout generation.
    [AddComponentMenu("UI/Prototype Surface")]
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class PrototypeUiSurface : MaskableGraphic
    {
        public Color topColor = Color.white;
        public Color bottomColor = Color.white;
        [Range(0, 1)] public float roundness = 1;
        [Min(0)] public float cornerRadius;
        [Min(0)] public float borderWidth;
        public Color borderColor = new Color32(217,225,212,255);

        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            Rect rect = GetPixelAdjustedRect();
            if (rect.width <= 0 || rect.height <= 0) return;
            float radius = cornerRadius > 0 ? Mathf.Min(cornerRadius, Mathf.Min(rect.width,rect.height)*.5f)
                : Mathf.Min(rect.width, rect.height) * .5f * roundness;
            float edge = Mathf.Min(borderWidth, Mathf.Min(rect.width,rect.height)*.5f);
            if (edge > 0)
            {
                Rounded(mesh, rect, radius, true, rect);
                var inner=Rect.MinMaxRect(rect.xMin+edge,rect.yMin+edge,rect.xMax-edge,rect.yMax-edge);
                Rounded(mesh, inner, Mathf.Max(0,radius-edge), false, rect);
            }
            else Rounded(mesh,rect,radius,false,rect);
        }
        private void Rounded(VertexHelper mesh, Rect rect, float radius, bool border, Rect full)
        {
            if(rect.width<=0||rect.height<=0)return;
            int start=mesh.currentVertCount;
            const int segments = 24;
            const int count = 4 * (segments + 1);
            AddVertex(mesh, rect.center, full, border);
            for (int corner = 0; corner < 4; corner++)
            {
                Vector2 center = new Vector2(
                    corner == 0 || corner == 3 ? rect.xMax - radius : rect.xMin + radius,
                    corner < 2 ? rect.yMax - radius : rect.yMin + radius);
                for (int i = 0; i <= segments; i++)
                {
                    float angle = (corner * 90f + i * 90f / segments) * Mathf.Deg2Rad;
                    AddVertex(mesh, center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius, full, border);
                }
            }
            for (int i = 0; i < count; i++) mesh.AddTriangle(start, start+i+1, start+(i+1)%count+1);
        }

        private void AddVertex(VertexHelper mesh, Vector2 position, Rect rect, bool border)
        {
            float y = Mathf.InverseLerp(rect.yMin, rect.yMax, position.y);
            Color tint = (border ? borderColor : Color.Lerp(bottomColor, topColor, y)) * color;
            mesh.AddVert(position, tint, Vector2.zero);
        }
    }
}
