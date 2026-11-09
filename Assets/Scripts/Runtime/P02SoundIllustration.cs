using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace CapstoneDesign.Runtime
{
    // A static, presentation-only illustration. Waves describe attention to sound;
    // they are not microphone measurements, playback, or a timing instruction.
    [DisallowMultipleComponent]
    [RequireComponent(typeof(RectTransform), typeof(CanvasRenderer))]
    public sealed class P02SoundIllustration : MaskableGraphic
    {
        public enum Stage { Overview, Preparation, Notice, Observe, Return, Practice, Reflection, Help }
        public Stage CurrentStage;

        static readonly Color Blue = C(80, 115, 139), Teal = C(86, 128, 120), Ink = C(56, 80, 89);
        static readonly Color Skin = C(236, 200, 171), SkinShade = C(210, 165, 141), Paper = C(245, 244, 235);
        VertexHelper mesh;
        Rect drawingRect;
        float unit, antialias;

        public void Configure(Stage stage)
        {
            if (CurrentStage == stage) return;
            CurrentStage = stage; SetVerticesDirty();
        }

        protected override void OnEnable() { base.OnEnable(); raycastTarget = false; }
        protected override void OnPopulateMesh(VertexHelper helper)
        {
            helper.Clear(); drawingRect = GetPixelAdjustedRect();
            unit = Mathf.Min(drawingRect.width, drawingRect.height) / 232f;
            if (unit <= 0) return;
            mesh = helper;
            antialias = .85f / Mathf.Max(.01f, unit * (canvas == null ? 1 : canvas.scaleFactor));
            DrawPerson();
            DrawStageContext();
            mesh = null;
        }

        void DrawPerson()
        {
            // The same silhouette and ear position are used in every stage.
            Ellipse(130, 231, 65, 4, A(Blue, .075f));
            Fill(new Path(116, 121).Curve(117, 131, 114, 137, 106, 141).Line(119, 156)
                .Line(150, 150).Line(158, 140).Curve(147, 136, 145, 129, 147, 119)
                .Line(116, 121), Skin, SkinShade);
            // A loose, softly shaded shirt; no prescribed hand or sitting gesture.
            Fill(new Path(108, 137).Curve(94, 140, 80, 147, 75, 166).Curve(71, 181, 67, 203, 67, 222)
                .Curve(95, 233, 159, 235, 190, 222).Curve(188, 202, 186, 178, 180, 163)
                .Curve(174, 147, 160, 140, 151, 138).Curve(140, 151, 122, 151, 108, 137), C(174, 196, 197), C(122, 158, 170));
            Fill(new Path(96, 146).Curve(90, 168, 91, 201, 95, 228).Curve(83, 227, 74, 225, 67, 222)
                .Curve(70, 192, 71, 174, 78, 160).Curve(82, 153, 88, 149, 96, 146), C(148, 178, 183), C(105, 143, 157));
            Fill(new Path(162, 146).Curve(169, 165, 170, 194, 166, 229).Curve(176, 227, 184, 225, 190, 222)
                .Curve(187, 194, 186, 177, 179, 161).Curve(175, 153, 169, 149, 162, 146), C(154, 183, 187), C(111, 149, 162));
            Curve(C(110, 147, 154), 1.5f, 107, 139, 119, 155, 140, 158, 153, 140);
            Curve(A(Paper, .65f), 1.5f, 111, 143, 122, 154, 139, 155, 149, 145);
            Curve(A(Paper, .34f), 1.7f, 105, 165, 101, 182, 103, 201, 107, 212);
            Curve(A(Blue, .32f), 1.4f, 148, 199, 153, 205, 160, 208, 169, 209);
            Curve(A(Blue, .3f), 1.3f, 79, 187, 82, 197, 85, 204, 89, 208);
            // Back hair gives the exposed ear a clear warm/cool silhouette.
            Fill(new Path(99, 59).Curve(98, 42, 112, 28, 131, 30).Curve(151, 28, 169, 40, 172, 58)
                .Curve(177, 80, 168, 103, 158, 117).Line(107, 117)
                .Curve(98, 100, 92, 79, 99, 59), C(97, 112, 116), C(68, 87, 94));
            Ellipse(164, 97, 5.5f, 10, SkinShade);
            Fill(new Path(110, 66).Curve(111, 49, 124, 43, 140, 46).Curve(155, 49, 164, 63, 164, 82)
                .Line(162, 105).Curve(160, 124, 147, 136, 133, 135).Curve(119, 134, 108, 122, 107, 103)
                .Line(110, 66), C(245, 218, 190), Skin);
            // Broad swept hair leaves the near ear visible instead of hiding it.
            Fill(new Path(99, 75).Curve(93, 57, 101, 39, 117, 33).Curve(136, 23, 158, 32, 167, 47)
                .Curve(174, 58, 173, 72, 169, 82).Line(160, 75).Curve(159, 64, 153, 55, 144, 52)
                .Curve(134, 64, 121, 71, 109, 72).Line(106, 87).Line(99, 88)
                .Line(99, 75), C(105, 119, 121), C(72, 91, 98));
            Curve(A(C(153, 165, 161), .55f), 1.5f, 108, 48, 120, 34, 142, 33, 156, 44);
            Curve(A(C(143, 159, 158), .4f), 1.2f, 115, 60, 127, 56, 137, 49, 142, 43);
            // A small, soft ear with one suggestion of a fold rather than anatomy.
            Fill(new Path(107, 91).Curve(100, 86, 92, 90, 93, 99).Curve(93, 107, 97, 114, 103, 114)
                .Curve(109, 113, 110, 108, 109, 102).Curve(109, 97, 111, 94, 107, 91), C(241, 209, 181), C(232, 193, 165));
            Curve(A(C(206, 162, 141), .75f), 1.15f, 98, 96, 103, 92, 107, 99, 103, 102);
            // Relaxed expression, slightly turned towards the visible ear.
            Curve(C(116, 103, 91), 1.25f, 116, 86, 120, 83, 124, 83, 128, 85);
            Curve(C(116, 103, 91), 1.25f, 142, 84, 147, 81, 151, 82, 154, 85);
            Curve(Ink, 1.5f, 116, 94, 120, 97, 124, 97, 128, 94);
            Curve(Ink, 1.5f, 141, 93, 145, 96, 149, 96, 153, 93);
            Curve(C(187, 139, 116), 1.25f, 135, 95, 134, 101, 132, 105, 138, 106);
            Curve(C(157, 114, 102), 1.4f, 127, 117, 132, 121, 139, 121, 144, 116);
            Ellipse(119, 106, 5, 2.7f, A(C(210, 150, 134), .16f));
            Ellipse(151, 104, 5, 2.7f, A(C(210, 150, 134), .16f));
        }

        void DrawStageContext()
        {
            // Effects are rebuilt exclusively from the current stage. There is no
            // retained selection, analyser value, animation phase or previous mesh.
            switch (CurrentStage)
            {
                case Stage.Overview:
                    NearWave(76, 82, 117, 3.2f, Blue);
                    NearWave(60, 71, 129, 2.6f, A(Teal, .75f));
                    break;
                case Stage.Preparation:
                    Curve(A(Blue, .5f), 2.1f, 42, 145, 47, 141, 54, 140, 62, 141);
                    Curve(A(Blue, .5f), 2.1f, 197, 141, 205, 140, 213, 143, 218, 147);
                    Curve(A(Paper, .68f), 2, 96, 221, 115, 226, 146, 226, 166, 222);
                    break;
                case Stage.Notice:
                    NearWave(77, 86, 113, 3.5f, Blue);
                    NearWave(62, 75, 125, 3.1f, Teal);
                    break;
                case Stage.Observe:
                    // Three static examples, strongest at the top; no live meter.
                    SoundWave(64, 14, 4);
                    SoundWave(100, 9, 2.8f);
                    SoundWave(136, 5, 1.8f);
                    break;
                case Stage.Return:
                    DrawReturn();
                    break;
                case Stage.Practice:
                    NearWave(77, 91, 109, 2, A(Blue, .58f));
                    break;
                case Stage.Reflection:
                    DrawNote();
                    break;
                case Stage.Help:
                    DrawBook();
                    break;
            }
        }

        void NearWave(float x, float top, float bottom, float width, Color tint)
            => Curve(tint, width, x, top, x - 14, top + 8, x - 14, bottom - 8, x, bottom);
        void SoundWave(float centerY, float amplitude, float width)
        {
            float bend = amplitude * 4f / 3f;
            Curve(Blue, width, 185, centerY, 193, centerY - bend, 203, centerY - bend, 211, centerY);
            Curve(Blue, width, 211, centerY, 219, centerY + bend, 229, centerY + bend, 237, centerY);
        }
        void DrawReturn()
        {
            Fill(new Path(30, 47).Curve(25, 35, 35, 25, 46, 29).Curve(55, 21, 71, 26, 74, 37)
                .Curve(87, 42, 82, 58, 72, 60).Line(40, 60).Curve(32, 59, 28, 54, 30, 47), C(207, 222, 226), C(171, 197, 205));
            Curve(A(Paper, .75f), 1.8f, 36, 39, 39, 33, 43, 33, 47, 35);
            Ellipse(49, 67, 3.5f, 3, C(157, 186, 197));
            Ellipse(54, 75, 2, 2, Blue);
            Curve(Blue, 3.1f, 44, 83, 37, 108, 65, 115, 88, 101);
            Curve(Blue, 3.1f, 78, 98, 82, 99, 86, 100, 88, 101);
            Curve(Blue, 3.1f, 84, 111, 85, 107, 87, 103, 88, 101);
        }
        void DrawNote()
        {
            Fill(new Path(32, 156).Curve(32, 152, 35, 150, 39, 151).Line(63, 154)
                .Curve(67, 154, 69, 157, 68, 161).Line(65, 195).Curve(65, 199, 62, 201, 58, 200)
                .Line(34, 197).Curve(30, 197, 28, 194, 29, 190).Line(32, 156), Paper, C(222, 231, 225));
            Curve(Blue, 2.1f, 38, 163, 44, 163, 53, 164, 60, 165);
            Curve(A(Blue, .8f), 1.8f, 37, 172, 44, 172, 52, 173, 58, 174);
            Curve(A(Teal, .8f), 1.8f, 36, 182, 40, 182, 45, 183, 50, 184);
        }
        void DrawBook()
        {
            Fill(new Path(191, 156).Curve(199, 152, 207, 153, 212, 157).Curve(219, 153, 227, 155, 234, 159)
                .Line(231, 189).Curve(224, 185, 217, 185, 212, 188).Curve(205, 184, 198, 184, 189, 187)
                .Line(191, 156), Paper, C(216, 228, 225));
            Curve(Blue, 1.9f, 212, 158, 212, 166, 212, 176, 212, 185);
            Curve(A(Teal, .85f), 1.5f, 195, 162, 200, 160, 204, 161, 207, 163);
            Curve(A(Teal, .7f), 1.5f, 195, 170, 200, 168, 204, 169, 207, 171);
            Curve(A(Teal, .85f), 1.5f, 217, 163, 222, 161, 226, 163, 230, 165);
        }

        sealed class Path
        {
            public readonly List<Vector2> Points = new List<Vector2>();
            public Path(float x, float y) { Points.Add(new Vector2(x, y)); }
            public Path Line(float x, float y) { Points.Add(new Vector2(x, y)); return this; }
            public Path Curve(float ax, float ay, float bx, float by, float x, float y)
            {
                var start = Points[Points.Count - 1]; var a = new Vector2(ax, ay); var b = new Vector2(bx, by); var end = new Vector2(x, y);
                for (int i = 1; i <= 12; i++) { float t = i / 12f, s = 1 - t; Points.Add(s * s * s * start + 3 * s * s * t * a + 3 * s * t * t * b + t * t * t * end); }
                return this;
            }
        }
        void Ellipse(float x, float y, float rx, float ry, Color tint)
        {
            var points = new List<Vector2>(48);
            for (int i = 0; i < 48; i++) { float a = i * Mathf.PI / 24; points.Add(new Vector2(x + Mathf.Cos(a) * rx, y + Mathf.Sin(a) * ry)); }
            Polygon(points, tint, tint);
        }
        void Curve(Color tint, float width, float x, float y, float ax, float ay, float bx, float by, float ex, float ey)
        {
            var points = new Path(x, y).Curve(ax, ay, bx, by, ex, ey).Points;
            var outline = new List<Vector2>(points.Count * 2 + 16); float radius = width / 2;
            for (int side = 1; side >= -1; side -= 2)
            {
                for (int j = 0; j < points.Count; j++)
                {
                    int i = side > 0 ? j : points.Count - 1 - j;
                    var direction = (points[Mathf.Min(i + 1, points.Count - 1)] - points[Mathf.Max(0, i - 1)]).normalized;
                    outline.Add(points[i] + new Vector2(-direction.y, direction.x) * (radius * side));
                }
                int index = side > 0 ? points.Count - 1 : 0;
                var tangent = side > 0 ? points[index] - points[index - 1] : points[0] - points[1];
                float angle = Mathf.Atan2(tangent.y, tangent.x) + Mathf.PI / 2;
                for (int i = 1; i < 8; i++) { float a = angle - i * Mathf.PI / 8; outline.Add(points[index] + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * radius); }
            }
            Polygon(outline, tint, tint);
        }
        void Fill(Path path, Color top, Color bottom) => Polygon(path.Points, top, bottom);
        static float Cross(Vector2 a, Vector2 b) => a.x * b.y - a.y * b.x;
        void Polygon(List<Vector2> points, Color top, Color bottom)
        {
            if (points.Count > 2 && (points[0] - points[points.Count - 1]).sqrMagnitude < .0001f) points.RemoveAt(points.Count - 1);
            int count = points.Count; if (count < 3) return;
            float area = 0, minY = float.MaxValue, maxY = float.MinValue;
            for (int i = 0; i < count; i++) { area += Cross(points[i], points[(i + 1) % count]); minY = Mathf.Min(minY, points[i].y); maxY = Mathf.Max(maxY, points[i].y); }
            if (Mathf.Abs(area) < .0001f) return;
            float sign = Mathf.Sign(area); int start = mesh.currentVertCount;
            for (int i = 0; i < count; i++) Vertex(points[i], Color.Lerp(top, bottom, Mathf.InverseLerp(minY, maxY, points[i].y)));
            var remaining = new List<int>(count); for (int i = 0; i < count; i++) remaining.Add(i);
            while (remaining.Count > 2)
            {
                bool clipped = false;
                for (int i = 0; i < remaining.Count; i++)
                {
                    int a = remaining[(i + remaining.Count - 1) % remaining.Count], b = remaining[i], c = remaining[(i + 1) % remaining.Count];
                    if (Cross(points[b] - points[a], points[c] - points[b]) * sign <= .00001f) continue;
                    bool inside = false;
                    foreach (int p in remaining)
                        if (p != a && p != b && p != c && Cross(points[b] - points[a], points[p] - points[a]) * sign >= -.00001f
                            && Cross(points[c] - points[b], points[p] - points[b]) * sign >= -.00001f
                            && Cross(points[a] - points[c], points[p] - points[c]) * sign >= -.00001f) { inside = true; break; }
                    if (inside) continue;
                    mesh.AddTriangle(start + a, start + b, start + c); remaining.RemoveAt(i); clipped = true; break;
                }
                if (!clipped) break;
            }
            // An outward transparent fringe antialiases curves without any texture.
            int fringe = mesh.currentVertCount;
            for (int i = 0; i < count; i++)
            {
                var before = (points[i] - points[(i + count - 1) % count]).normalized;
                var after = (points[(i + 1) % count] - points[i]).normalized;
                var n1 = new Vector2(before.y, -before.x) * sign; var n2 = new Vector2(after.y, -after.x) * sign;
                var normal = (n1 + n2).normalized;
                float distance = Mathf.Min(antialias * 2, antialias / Mathf.Max(.5f, Vector2.Dot(normal, n2)));
                Vertex(points[i] + normal * distance, A(Color.Lerp(top, bottom, Mathf.InverseLerp(minY, maxY, points[i].y)), 0));
            }
            for (int i = 0; i < count; i++) { int next = (i + 1) % count; mesh.AddTriangle(start + i, fringe + i, fringe + next); mesh.AddTriangle(start + i, fringe + next, start + next); }
        }
        void Vertex(Vector2 point, Color tint)
            => mesh.AddVert(new Vector3(drawingRect.center.x + (point.x - 128) * unit, drawingRect.center.y + (128 - point.y) * unit), tint * color, Vector2.zero);
        static Color C(byte r, byte g, byte b) => new Color32(r, g, b, 255);
        static Color A(Color value, float alpha) { value.a = alpha; return value; }
    }
}
