using UnityEngine;
using UnityEngine.UI;

namespace CapstoneDesign.Prototype
{
    // Original rounded mood medallions, drawn as native uGUI vectors.
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class PrototypeMoodFace : MaskableGraphic
    {
        public Mood mood = Mood.Happy;
        public bool selected;
        private static readonly Color Ink = new Color(.25f, .33f, .25f);
        private static readonly Color Wood = new Color(.69f, .52f, .34f);
        private static readonly Color Leaf = new Color(.49f, .64f, .37f);
        private static readonly Color Cream = new Color(1f, .96f, .83f);
        private static readonly Color[] Tints = {
            new Color(.94f,.91f,.80f), new Color(1f,.84f,.53f), new Color(.79f,.86f,.65f),
            new Color(.96f,.85f,.69f), new Color(.85f,.81f,.86f), new Color(.75f,.83f,.85f)
        };

        public void SetSelected(bool value) { if (selected == value) return; selected = value; SetVerticesDirty(); }
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            Rect r = GetPixelAdjustedRect();
            Vector2 center = r.center;
            float unit = Mathf.Min(r.width, r.height) * .5f;
            if(unit<=0)return;
            int moodIndex=Mathf.Clamp((int)mood,0,Tints.Length-1);
            Color face=Tints[moodIndex];
            Disc(vh, center+Vector2.down*unit*.045f, unit*.94f, new Color(.48f,.39f,.25f,.22f));
            Disc(vh, center, unit*.92f, selected ? Ink : Wood);
            Disc(vh, center, unit*.85f, Cream);
            Disc(vh, center, unit*.77f, face);
            // Warm highlight and cheeks give the medallions volume without textural noise.
            Ellipse(vh,center+new Vector2(-.20f,.48f)*unit,new Vector2(.25f,.09f)*unit,new Color(1f,.98f,.87f,.55f));
            for(int side=-1;side<=1;side+=2)
                Ellipse(vh,center+new Vector2(side*.45f,-.12f)*unit,new Vector2(.115f,.065f)*unit,new Color(.86f,.54f,.40f,.24f));
            for (int side = -1; side <= 1; side += 2)
            {
                Vector2 eye = center + new Vector2(side * .29f, .12f) * unit;
                if (mood == Mood.Calm)
                    Curve(vh, eye, unit * .105f, unit * .045f, -1, unit * .027f);
                else if(mood==Mood.Tired)
                {
                    Line(vh,eye+new Vector2(-.095f,.018f)*unit,eye+new Vector2(.095f,-.018f)*unit,unit*.026f,Ink);
                    Disc(vh,eye+Vector2.down*unit*.035f,unit*.027f,Ink);
                }
                else
                    Ellipse(vh,eye,new Vector2(.039f,.057f)*unit,Ink);
                if (mood == Mood.Down)
                    Line(vh, eye + new Vector2(-.09f, side < 0 ? .10f : .16f) * unit,
                        eye + new Vector2(.09f, side < 0 ? .16f : .10f) * unit, unit * .020f, Ink);
            }
            Vector2 mouth = center + Vector2.down * unit * .24f;
            float depth=mood==Mood.Happy?.11f:mood==Mood.Calm?.055f:mood==Mood.Down?.07f:.012f;
            Curve(vh,mouth,unit*(mood==Mood.Happy?.23f:.17f),unit*depth,mood==Mood.Down?1:-1,unit*.028f);
            if(selected)
            {
                // The leaf badge reinforces selection in addition to the darker ring.
                Vector2 badge=center+new Vector2(.66f,.61f)*unit;
                Disc(vh,badge,unit*.25f,Ink);
                Disc(vh,badge,unit*.20f,Leaf);
                Line(vh,badge+new Vector2(-.10f,0)*unit,badge+new Vector2(-.025f,-.07f)*unit,unit*.025f,Cream);
                Line(vh,badge+new Vector2(-.025f,-.07f)*unit,badge+new Vector2(.11f,.08f)*unit,unit*.025f,Cream);
            }
        }
        private void Disc(VertexHelper vh, Vector2 center, float radius, Color tint)
        { Ellipse(vh,center,Vector2.one*radius,tint); }
        private void Ellipse(VertexHelper vh,Vector2 center,Vector2 radius,Color tint)
        {
            tint.a*=color.a;
            int start = vh.currentVertCount;
            vh.AddVert(center, tint, Vector2.zero);
            const int n = 32;
            for (int i = 0; i < n; i++)
            { float a = i * Mathf.PI * 2 / n; vh.AddVert(center + Vector2.Scale(new Vector2(Mathf.Cos(a), Mathf.Sin(a)),radius), tint, Vector2.zero); }
            for (int i = 0; i < n; i++) vh.AddTriangle(start, start + 1 + i, start + 1 + (i + 1) % n);
        }
        private void Curve(VertexHelper vh, Vector2 center, float width, float depth, int direction, float thick)
        {
            Vector2 previous = center + Vector2.left * width;
            for (int i = 1; i <= 12; i++)
            {
                float t = i / 6f - 1;
                Vector2 next = center + new Vector2(t * width, direction * depth * (1 - t * t));
                Line(vh, previous, next, thick, Ink); previous = next;
            }
            Disc(vh,center+Vector2.left*width,thick,Ink);
            Disc(vh,center+Vector2.right*width,thick,Ink);
        }
        private void Line(VertexHelper vh, Vector2 a, Vector2 b, float width, Color tint)
        {
            tint.a*=color.a;
            Vector2 d = (b - a).normalized;
            Vector2 n = new Vector2(-d.y, d.x) * width;
            int start = vh.currentVertCount;
            vh.AddVert(a - n, tint, Vector2.zero); vh.AddVert(a + n, tint, Vector2.zero);
            vh.AddVert(b + n, tint, Vector2.zero); vh.AddVert(b - n, tint, Vector2.zero);
            vh.AddTriangle(start, start + 1, start + 2); vh.AddTriangle(start, start + 2, start + 3);
        }
    }
}
