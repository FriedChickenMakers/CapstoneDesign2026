using UnityEngine;
using UnityEngine.UI;

namespace CapstoneDesign.Prototype
{
    // A quiet botanical edge, drawn from vertices instead of a raster texture.
    // It never covers the label, participates in layout, or receives a tap.
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class GardenButtonBotanicals : MaskableGraphic
    {
        public static void Apply(Button button, bool visible)
        {
            if (button == null) return;
            var child = button.transform.Find("Botanical button edge");
            if (child == null && !visible) return;
            var graphic = child != null ? child.GetComponent<GardenButtonBotanicals>() : null;
            if (graphic == null)
            {
                var go = new GameObject("Botanical button edge", typeof(RectTransform));
                go.transform.SetParent(button.transform, false);
                graphic = go.AddComponent<GardenButtonBotanicals>();
                var rect = graphic.rectTransform;
                rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
                rect.offsetMin = rect.offsetMax = Vector2.zero;
                go.AddComponent<LayoutElement>().ignoreLayout = true;
                graphic.raycastTarget = false;
                graphic.color = new Color32(199, 220, 173, 72);
                go.transform.SetAsFirstSibling();
            }
            graphic.gameObject.SetActive(visible);
        }

        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            var r = GetPixelAdjustedRect();
            // Compact paired actions keep their entire width for words.
            if (r.width < 560 || r.height < 48) return;
            float h = Mathf.Min(r.height, 96);
            Edge(mesh, new Vector2(r.xMin + 14, r.yMin + 8), h, 1);
            Edge(mesh, new Vector2(r.xMax - 14, r.yMax - 8), h, -1);
        }
        void Edge(VertexHelper mesh, Vector2 origin, float h, float direction)
        {
            Vector2 a = origin;
            Vector2 b = origin + new Vector2(7, h * .27f) * direction;
            Vector2 c = origin + new Vector2(4, h * .54f) * direction;
            Segment(mesh, a, b, 1); Segment(mesh, b, c, 1);
            Leaf(mesh, a + (b - a) * .70f, new Vector2(14, 8) * direction, 4);
            Leaf(mesh, b + (c - b) * .45f, new Vector2(-8, 9) * direction, 3.5f);
            Leaf(mesh, c, new Vector2(5, 11) * direction, 3);
        }
        void Segment(VertexHelper mesh, Vector2 a, Vector2 b, float width)
        {
            Vector2 n = new Vector2(-(b-a).y, (b-a).x).normalized * width * .5f;
            int start = mesh.currentVertCount;
            mesh.AddVert(a-n,color,Vector2.zero); mesh.AddVert(a+n,color,Vector2.zero);
            mesh.AddVert(b+n,color,Vector2.zero); mesh.AddVert(b-n,color,Vector2.zero);
            mesh.AddTriangle(start,start+1,start+2); mesh.AddTriangle(start,start+2,start+3);
        }
        void Leaf(VertexHelper mesh, Vector2 basePoint, Vector2 stem, float width)
        {
            Vector2 normal = new Vector2(-stem.y,stem.x).normalized * width;
            Vector2 middle = basePoint + stem * .55f;
            int start = mesh.currentVertCount;
            mesh.AddVert(basePoint,color,Vector2.zero); mesh.AddVert(middle+normal,color,Vector2.zero);
            mesh.AddVert(basePoint+stem,color,Vector2.zero); mesh.AddVert(middle-normal,color,Vector2.zero);
            mesh.AddTriangle(start,start+1,start+2); mesh.AddTriangle(start,start+2,start+3);
        }
    }
}
