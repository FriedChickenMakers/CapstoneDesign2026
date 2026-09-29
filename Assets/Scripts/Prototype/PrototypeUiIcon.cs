using UnityEngine;
using UnityEngine.UI;

namespace CapstoneDesign.Prototype
{
    // Small native vector marks; no font glyph, sprite import or additional material.
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class PrototypeUiIcon : MaskableGraphic
    {
        public enum Symbol { Leaf, Journal, Check, Arrow }
        public Symbol symbol;
        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            switch (symbol)
            {
                case Symbol.Leaf:
                    Path(mesh, new Vector2(.24f,.18f), new Vector2(.30f,.50f), new Vector2(.49f,.73f),
                        new Vector2(.82f,.84f), new Vector2(.78f,.52f), new Vector2(.60f,.31f), new Vector2(.30f,.27f));
                    Path(mesh, new Vector2(.20f,.13f), new Vector2(.64f,.66f));
                    break;
                case Symbol.Journal:
                    Path(mesh, new Vector2(.24f,.17f), new Vector2(.78f,.17f), new Vector2(.78f,.83f),
                        new Vector2(.24f,.83f), new Vector2(.24f,.17f));
                    Path(mesh, new Vector2(.37f,.17f), new Vector2(.37f,.83f));
                    Path(mesh, new Vector2(.47f,.62f), new Vector2(.66f,.62f));
                    Path(mesh, new Vector2(.47f,.45f), new Vector2(.66f,.45f));
                    break;
                case Symbol.Check:
                    Path(mesh, new Vector2(.22f,.49f), new Vector2(.43f,.28f), new Vector2(.80f,.73f));
                    break;
                case Symbol.Arrow:
                    Path(mesh, new Vector2(.20f,.50f), new Vector2(.78f,.50f));
                    Path(mesh, new Vector2(.53f,.77f), new Vector2(.80f,.50f), new Vector2(.53f,.23f));
                    break;
            }
        }
        private void Path(VertexHelper mesh, params Vector2[] points)
        {
            Rect r = GetPixelAdjustedRect();
            float width = Mathf.Min(r.width,r.height) * .036f;
            for (int i=1;i<points.Length;i++)
            {
                Vector2 a=r.min+Vector2.Scale(points[i-1],r.size), b=r.min+Vector2.Scale(points[i],r.size);
                Vector2 d=(b-a).normalized, n=new Vector2(-d.y,d.x)*width;
                int start=mesh.currentVertCount;
                mesh.AddVert(a-n,color,Vector2.zero);mesh.AddVert(a+n,color,Vector2.zero);
                mesh.AddVert(b+n,color,Vector2.zero);mesh.AddVert(b-n,color,Vector2.zero);
                mesh.AddTriangle(start,start+1,start+2);mesh.AddTriangle(start,start+2,start+3);
            }
        }
    }
}
