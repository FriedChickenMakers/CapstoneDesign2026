using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace CapstoneDesign.Runtime
{
    // Presentation only. The illustration does not read room brightness, prescribe
    // a brightness level, or confirm that the user performed the activity.
    [DisallowMultipleComponent]
    [RequireComponent(typeof(RectTransform), typeof(CanvasRenderer))]
    public sealed class M01LightIllustration : MaskableGraphic
    {
        public enum Stage { Overview, Choice, Curtain, Lamp, Brighten, Help, Complete }
        [SerializeField] Stage currentStage;
        public Stage CurrentStage => currentStage;

        static readonly Color Paper = C(249, 244, 224), Linen = C(217, 217, 184);
        static readonly Color Sage = C(133, 159, 135), SageDark = C(77, 116, 92);
        static readonly Color Wood = C(155, 130, 94), Brass = C(156, 131, 83);
        static readonly Color Sunlight = C(244, 207, 120), Ink = C(91, 91, 66);
        VertexHelper mesh;
        Rect drawingRect;
        float unit, antialias, modelScale = 1;
        Vector2 modelCenter = new Vector2(128, 128);

        public void Configure(Stage stage)
        {
            if (currentStage == stage) return;
            currentStage = stage;
            SetVerticesDirty();
        }

        protected override void OnEnable() { base.OnEnable(); raycastTarget = false; }
        protected override void OnPopulateMesh(VertexHelper helper)
        {
            // Rebuild from the current stage only, including every arrow and light
            // beam. Returning to a previous slide cannot retain a later effect.
            helper.Clear(); drawingRect = GetPixelAdjustedRect();
            unit = Mathf.Min(drawingRect.width, drawingRect.height) / 232f;
            if (unit <= 0) return;
            mesh = helper; modelScale = 1; modelCenter = new Vector2(128, 128);
            antialias = .85f / Mathf.Max(.01f, unit * (canvas == null ? 1 : canvas.scaleFactor));
            switch (currentStage)
            {
                case Stage.Curtain: DrawCurtainScene(); break;
                case Stage.Lamp: DrawLampScene(); break;
                case Stage.Brighten: DrawRoom(true); break;
                case Stage.Complete:
                    DrawRoom(true); DrawCompletion(); break;
                case Stage.Help:
                    DrawRoom(false); DrawBook(); break;
                default:
                    DrawRoom(false);
                    if (currentStage == Stage.Choice) DrawChoiceCues();
                    break;
            }
            mesh = null;
        }

        void DrawRoom(bool bright)
        {
            // An open vignette, rather than another card behind the central icon.
            Ellipse(128, 228, 109, 8, A(Wood, .075f));
            Fill(new Path(31, 202).Curve(62, 194, 171, 196, 217, 207)
                .Curve(231, 214, 215, 226, 195, 228).Curve(146, 237, 67, 231, 35, 224)
                .Curve(18, 218, 19, 207, 31, 202), C(235, 226, 201), C(219, 207, 177));
            Curve(A(Paper, .72f), 1.6f, 35, 212, 78, 218, 160, 222, 206, 214);
            if (bright)
            {
                Fill(new Path(74, 150).Line(123, 150).Line(199, 223).Line(110, 221)
                    .Line(74, 150), A(Sunlight, .34f), A(Paper, .08f));
                Fill(new Path(82, 153).Line(97, 153).Line(153, 221).Line(126, 220)
                    .Line(82, 153), A(Paper, .48f), A(Sunlight, .08f));
            }
            At(91, 118, .72f, () => DrawWindow(bright));
            At(194, 167, .64f, () => DrawLamp(bright, false));
            // A small plant ties the interior to the garden without competing
            // with either of the two ways to brighten the room.
            At(103, 203, .41f, DrawPlant);
        }

        void DrawCurtainScene()
        {
            Ellipse(131, 228, 103, 7, A(Wood, .08f));
            Fill(new Path(82, 166).Line(174, 166).Line(235, 227).Line(74, 226)
                .Line(82, 166), A(Sunlight, .4f), A(Sunlight, .06f));
            Fill(new Path(101, 168).Line(121, 168).Line(150, 227).Line(109, 227)
                .Line(101, 168), A(Paper, .62f), A(Paper, .06f));
            Fill(new Path(145, 167).Line(164, 167).Line(218, 226).Line(188, 226)
                .Line(145, 167), A(Paper, .49f), A(Paper, .04f));
            DrawWindow(true);
            // Short outward arrows belong only to the opening-curtain guide.
            Arrow(58, 112, 44, 117, 25, 111, SageDark);
            Arrow(198, 112, 212, 117, 231, 111, SageDark);
        }

        void DrawWindow(bool open)
        {
            Rounded(57, 40, 147, 170, 6, A(Wood, .09f), A(Wood, .02f));
            Rounded(54, 35, 148, 169, 5, C(200, 178, 141), Wood);
            Rounded(62, 43, 132, 151, 2, C(207, 226, 223), C(238, 238, 205));
            // The quiet outdoors is entirely inside the frame.
            Ellipse(160, 74, 17, 17, C(244, 219, 157));
            Ellipse(160, 74, 12, 12, C(250, 231, 179));
            Fill(new Path(62, 160).Curve(81, 150, 91, 153, 109, 164)
                .Curve(135, 142, 168, 147, 194, 160).Line(194, 194)
                .Line(62, 194).Line(62, 160), C(182, 203, 170), C(159, 184, 150));
            Curve(A(Paper, .74f), 2.2f, 73, 58, 73, 75, 73, 89, 73, 103);
            Curve(A(Paper, .52f), 1.4f, 79, 57, 79, 68, 79, 77, 79, 84);
            Rounded(124, 43, 7, 151, 1, C(226, 212, 179), C(184, 158, 118));
            Rounded(62, 116, 132, 6, 1, C(226, 212, 179), C(191, 168, 132));
            Rounded(48, 195, 160, 10, 3, C(220, 201, 165), C(174, 148, 110));
            Curve(A(Paper, .6f), 1.4f, 54, 198, 98, 198, 159, 198, 201, 198);
            // Separate fabric planes and folds make the curtains recognizable
            // without relying on an arrow or a label in the introductory scene.
            if (open)
            {
                Fill(new Path(51, 40).Line(77, 40).Curve(77, 66, 70, 86, 65, 105)
                    .Curve(61, 132, 68, 160, 69, 191).Curve(61, 194, 52, 195, 44, 191)
                    .Curve(49, 149, 46, 98, 51, 40), C(191, 207, 177), Sage);
                Fill(new Path(179, 40).Line(205, 40).Curve(210, 94, 206, 148, 212, 191)
                    .Curve(203, 195, 195, 194, 187, 191).Curve(188, 160, 195, 132, 191, 105)
                    .Curve(186, 86, 179, 66, 179, 40), C(186, 204, 173), C(120, 150, 123));
                Curve(A(Paper, .57f), 1.8f, 60, 46, 61, 77, 55, 96, 55, 113);
                Curve(A(SageDark, .45f), 1.4f, 66, 126, 63, 145, 63, 168, 63, 186);
                Curve(A(Paper, .5f), 1.8f, 195, 47, 194, 76, 200, 97, 201, 115);
                Curve(A(SageDark, .42f), 1.4f, 192, 129, 195, 149, 195, 171, 194, 187);
                Curve(C(161, 144, 96), 3.4f, 47, 121, 53, 125, 61, 125, 66, 122);
                Curve(C(161, 144, 96), 3.4f, 190, 122, 195, 125, 203, 125, 209, 121);
            }
            else
            {
                Fill(new Path(51, 40).Line(105, 40).Curve(106, 84, 92, 106, 87, 137)
                    .Curve(84, 157, 86, 176, 83, 194).Curve(70, 198, 55, 197, 44, 192)
                    .Curve(48, 147, 46, 91, 51, 40), C(197, 211, 181), Sage);
                Fill(new Path(151, 40).Line(205, 40).Curve(210, 92, 208, 145, 212, 192)
                    .Curve(200, 197, 186, 197, 173, 194).Curve(170, 174, 173, 157, 169, 137)
                    .Curve(164, 106, 150, 84, 151, 40), C(190, 206, 175), C(126, 154, 127));
                Curve(A(Paper, .5f), 1.8f, 67, 47, 70, 90, 62, 144, 62, 188);
                Curve(A(SageDark, .32f), 1.4f, 86, 47, 88, 91, 76, 148, 76, 188);
                Curve(A(Paper, .47f), 1.8f, 184, 47, 182, 89, 194, 146, 193, 188);
                Curve(A(SageDark, .3f), 1.4f, 168, 47, 168, 92, 181, 148, 180, 188);
            }
            Curve(C(141, 123, 91), 5, 43, 35, 91, 34, 168, 34, 213, 35);
            Ellipse(42, 35, 4.3f, 4.3f, C(148, 128, 94));
            Ellipse(214, 35, 4.3f, 4.3f, C(148, 128, 94));
        }

        void DrawLampScene()
        {
            Ellipse(128, 229, 72, 6, A(Wood, .09f));
            At(126, 128, 1.05f, () => DrawLamp(true, true));
        }

        void DrawLamp(bool lit, bool switchCue)
        {
            if (lit)
            {
                Ellipse(128, 126, 70, 69, A(Sunlight, .085f));
                Ellipse(128, 128, 54, 52, A(Sunlight, .11f));
                Fill(new Path(101, 124).Line(157, 124).Line(190, 211).Curve(154, 220, 102, 220, 66, 211)
                    .Line(101, 124), A(Sunlight, .43f), A(Sunlight, .06f));
                Ellipse(128, 213, 59, 6, A(Sunlight, .13f));
            }
            // A single floor lamp; a broad textile shade, slender stem and base.
            Rounded(124, 116, 8, 100, 3, C(180, 157, 111), C(126, 110, 76));
            Curve(A(Paper, .47f), 1.4f, 126, 134, 126, 157, 126, 187, 126, 207);
            Ellipse(128, 217, 29, 6.5f, C(137, 119, 85));
            Ellipse(128, 214, 28, 6, C(181, 158, 111));
            Curve(A(Paper, .5f), 1.4f, 109, 212, 119, 208, 136, 208, 146, 212);
            Fill(new Path(104, 68).Curve(116, 64, 140, 64, 152, 68).Line(174, 125)
                .Curve(153, 134, 103, 134, 82, 125).Line(104, 68),
                lit ? C(249, 230, 179) : C(235, 223, 187), lit ? C(227, 192, 123) : C(207, 191, 145));
            Fill(new Path(104, 68).Line(112, 68).Line(100, 128).Curve(94, 128, 87, 127, 82, 125)
                .Line(104, 68), lit ? C(239, 213, 160) : C(221, 204, 163), C(192, 167, 115));
            Fill(new Path(145, 68).Line(152, 68).Line(174, 125).Curve(167, 128, 161, 128, 154, 130)
                .Line(145, 68), lit ? C(241, 216, 164) : C(224, 208, 168), C(195, 169, 116));
            Curve(A(Paper, .58f), 1.5f, 118, 73, 115, 88, 111, 105, 108, 121);
            Curve(A(Brass, .22f), 1.1f, 140, 72, 144, 89, 147, 105, 151, 122);
            Ellipse(128, 68, 24, 4, C(222, 205, 164));
            Ellipse(128, 67, 22, 2.6f, lit ? C(251, 238, 201) : Paper);
            Curve(C(172, 147, 100), 2.3f, 83, 125, 106, 134, 151, 134, 173, 125);
            if (lit) Ellipse(128, 130, 8, 3.5f, C(255, 242, 194));
            Curve(Brass, 1.5f, 158, 131, 158, 141, 158, 150, 158, 160);
            Ellipse(158, 164, 3.8f, 6, SageDark);
            if (switchCue)
            {
                Ellipse(158, 164, 10, 13, A(Sage, .15f));
                Arrow(216, 153, 196, 146, 174, 161, SageDark);
                // A restrained three-ray halo emphasizes the newly lit shade.
                Curve(A(Brass, .75f), 2.1f, 74, 85, 72, 83, 69, 81, 66, 79);
                Curve(A(Brass, .75f), 2.1f, 181, 86, 184, 84, 188, 82, 191, 80);
                Curve(A(Brass, .65f), 2.1f, 128, 51, 128, 48, 128, 44, 128, 41);
            }
        }

        void DrawChoiceCues()
        {
            Curve(A(SageDark, .7f), 2, 35, 60, 27, 66, 26, 79, 29, 88);
            Curve(A(Brass, .8f), 2, 222, 136, 232, 141, 235, 152, 232, 162);
        }

        void DrawPlant()
        {
            Curve(SageDark, 3, 128, 171, 129, 147, 126, 121, 126, 94);
            Fill(new Path(126, 127).Curve(103, 128, 89, 110, 92, 93)
                .Curve(113, 92, 128, 105, 126, 127), C(157, 184, 139), SageDark);
            Fill(new Path(127, 143).Curve(129, 118, 145, 109, 163, 112)
                .Curve(161, 133, 145, 146, 127, 143), C(173, 192, 143), Sage);
            Fill(new Path(126, 104).Curve(112, 85, 120, 67, 132, 60)
                .Curve(146, 77, 140, 95, 126, 104), C(181, 198, 151), Sage);
            Fill(new Path(99, 159).Line(157, 159).Line(149, 204).Curve(136, 210, 120, 210, 107, 204)
                .Line(99, 159), C(216, 183, 145), C(183, 145, 107));
            Ellipse(128, 159, 29, 6, C(226, 195, 158));
            Ellipse(128, 158, 22, 3, C(137, 118, 86));
            Curve(A(Paper, .4f), 2.3f, 111, 170, 111, 182, 112, 192, 114, 198);
        }

        void DrawBook()
        {
            Fill(new Path(135, 187).Curve(145, 182, 157, 184, 164, 189)
                .Curve(173, 184, 184, 184, 193, 190).Line(191, 219)
                .Curve(179, 214, 172, 214, 164, 218).Curve(154, 212, 144, 212, 133, 216)
                .Line(135, 187), Paper, Linen);
            Curve(SageDark, 2, 164, 190, 163, 199, 163, 207, 164, 215);
            Curve(A(SageDark, .66f), 1.6f, 141, 193, 147, 191, 154, 193, 158, 195);
            Curve(A(SageDark, .6f), 1.5f, 140, 201, 147, 199, 153, 201, 158, 203);
            Curve(A(SageDark, .66f), 1.6f, 171, 195, 177, 192, 183, 194, 187, 196);
        }

        void DrawCompletion()
        {
            Ellipse(200, 69, 18, 18, C(226, 234, 203));
            Curve(SageDark, 3, 191, 69, 193, 72, 195, 74, 198, 76);
            Curve(SageDark, 3, 198, 76, 202, 70, 206, 65, 210, 61);
        }

        void At(float x, float y, float scale, Action draw)
        {
            var previousCenter = modelCenter; float previousScale = modelScale, previousAa = antialias;
            modelCenter += new Vector2(x - 128, y - 128) * modelScale;
            modelScale *= scale; antialias /= scale;
            try { draw(); } finally { modelCenter = previousCenter; modelScale = previousScale; antialias = previousAa; }
        }
        void Rounded(float x, float y, float width, float height, float radius, Color top, Color bottom)
            => Fill(new Path(x + radius, y).Line(x + width - radius, y)
                .Curve(x + width, y, x + width, y, x + width, y + radius).Line(x + width, y + height - radius)
                .Curve(x + width, y + height, x + width, y + height, x + width - radius, y + height)
                .Line(x + radius, y + height).Curve(x, y + height, x, y + height, x, y + height - radius)
                .Line(x, y + radius).Curve(x, y, x, y, x + radius, y), top, bottom);
        void Arrow(float x, float y, float controlX, float controlY, float endX, float endY, Color tint)
        {
            Curve(tint, 2.7f, x, y, controlX, controlY, controlX, controlY, endX, endY);
            var direction = (new Vector2(endX, endY) - new Vector2(controlX, controlY)).normalized;
            var normal = new Vector2(-direction.y, direction.x); var tip = new Vector2(endX, endY);
            var a = tip - direction * 8 + normal * 4; var b = tip - direction * 8 - normal * 4;
            Curve(tint, 2.7f, a.x, a.y, a.x, a.y, endX, endY, endX, endY);
            Curve(tint, 2.7f, b.x, b.y, b.x, b.y, endX, endY, endX, endY);
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
            for (int i = 0; i < 48; i++) { float angle = i * Mathf.PI / 24; points.Add(new Vector2(x + Mathf.Cos(angle) * rx, y + Mathf.Sin(angle) * ry)); }
            Polygon(points, tint, tint);
        }
        void Curve(Color tint, float width, float x, float y, float ax, float ay, float bx, float by, float endX, float endY)
        {
            var points = new Path(x, y).Curve(ax, ay, bx, by, endX, endY).Points;
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
            // A transparent outward fringe smooths the vector edges at every
            // screen scale without generating textures or persistent resources.
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
        {
            point = modelCenter + (point - new Vector2(128, 128)) * modelScale;
            mesh.AddVert(new Vector3(drawingRect.center.x + (point.x - 128) * unit, drawingRect.center.y + (128 - point.y) * unit), tint * color, Vector2.zero);
        }
        static Color C(byte r, byte g, byte b) => new Color32(r, g, b, 255);
        static Color A(Color value, float alpha) { value.a = alpha; return value; }
    }
}
