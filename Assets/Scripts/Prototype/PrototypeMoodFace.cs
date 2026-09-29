using UnityEngine;
using UnityEngine.UI;

namespace CapstoneDesign.Prototype
{
    // Vector uGUI artwork; no bitmap or emoji font required.
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class PrototypeMoodFace : MaskableGraphic
    {
        public Mood mood = Mood.Happy;
        public bool selected;
        private static readonly Color Ink = new Color(.22f, .31f, .25f);
        private static readonly Color[] Tints = {
            Color.white, new Color(1f,.88f,.63f), new Color(.74f,.85f,.71f),
            new Color(.99f,.82f,.64f), new Color(.89f,.81f,.81f), new Color(.84f,.75f,.79f)
        };

        public void SetSelected(bool value) { if (selected == value) return; selected = value; SetVerticesDirty(); }
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            Rect r = GetPixelAdjustedRect();
            Vector2 center = r.center;
            float unit = Mathf.Min(r.width, r.height) * .5f;
            Disc(vh, center, unit, selected ? Ink : Tints[(int)mood]);
            if (selected) Disc(vh, center, unit - 3, Tints[(int)mood]);
            for (int side = -1; side <= 1; side += 2)
            {
                Vector2 eye = center + new Vector2(side * .32f, .13f) * unit;
                if (mood == Mood.Calm || mood == Mood.Neutral)
                    Curve(vh, eye, unit * .15f, unit * .065f, -1, unit * .035f);
                else Disc(vh, eye, unit * .045f, Ink);
                if (mood == Mood.Tired || mood == Mood.Down)
                    Line(vh, eye + new Vector2(-.12f, side < 0 ? .11f : .18f) * unit,
                        eye + new Vector2(.08f, side < 0 ? .18f : .11f) * unit, unit * .025f, Ink);
            }
            Vector2 mouth = center + Vector2.down * unit * .30f;
            Curve(vh, mouth, unit * .22f, unit * (mood == Mood.Neutral ? .015f : mood == Mood.Tired ? .035f : .09f),
                mood == Mood.Tired || mood == Mood.Down ? 1 : -1, unit * .035f);
        }
        private static void Disc(VertexHelper vh, Vector2 center, float radius, Color tint)
        {
            int start = vh.currentVertCount;
            vh.AddVert(center, tint, Vector2.zero);
            const int n = 40;
            for (int i = 0; i < n; i++)
            { float a = i * Mathf.PI * 2 / n; vh.AddVert(center + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * radius, tint, Vector2.zero); }
            for (int i = 0; i < n; i++) vh.AddTriangle(start, start + 1 + i, start + 1 + (i + 1) % n);
        }
        private static void Curve(VertexHelper vh, Vector2 center, float width, float depth, int direction, float thick)
        {
            Vector2 previous = center + Vector2.left * width;
            for (int i = 1; i <= 16; i++)
            {
                float t = i / 8f - 1;
                Vector2 next = center + new Vector2(t * width, direction * depth * (1 - t * t));
                Line(vh, previous, next, thick, Ink); previous = next;
            }
        }
        private static void Line(VertexHelper vh, Vector2 a, Vector2 b, float width, Color tint)
        {
            Vector2 d = (b - a).normalized;
            Vector2 n = new Vector2(-d.y, d.x) * width;
            int start = vh.currentVertCount;
            vh.AddVert(a - n, tint, Vector2.zero); vh.AddVert(a + n, tint, Vector2.zero);
            vh.AddVert(b + n, tint, Vector2.zero); vh.AddVert(b - n, tint, Vector2.zero);
            vh.AddTriangle(start, start + 1, start + 2); vh.AddTriangle(start, start + 2, start + 3);
        }
    }
}
