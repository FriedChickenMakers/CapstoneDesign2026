using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace CapstoneDesign.Runtime
{
    // Presentation only: this graphic never chooses a practice, paces breathing,
    // receives input, or changes a saved answer. Coordinates are a 256-point canvas.
    [DisallowMultipleComponent]
    [RequireComponent(typeof(RectTransform), typeof(CanvasRenderer))]
    public sealed class P01BreathIllustration : MaskableGraphic
    {
        public enum Stage { Overview, Preparation, Choice, Notice, Observe, Return, Practice, Reflection, Help }
        public Stage CurrentStage;
        public string FocusArea = "";
        public int HelpPage;

        static readonly Color Sage = C(133, 156, 137), SageLight = C(176, 193, 171), SageDark = C(92, 121, 105);
        static readonly Color Ink = C(69, 87, 78), Skin = C(235, 198, 168), SkinShade = C(214, 166, 139);
        static readonly Color Hair = C(91, 79, 68), HairLight = C(114, 99, 82), Paper = C(250, 246, 230);
        static readonly Color Attention = C(52, 105, 79);
        static readonly Vector2 Nose = new Vector2(132, 80), Chest = new Vector2(128, 132), Belly = new Vector2(128, 164);
        VertexHelper mesh;
        Rect drawingRect;
        float unit, antialias;

        public void Configure(Stage stage, string choice, int helpPage = 0)
        {
            choice = choice ?? "";
            helpPage = Mathf.Max(0, helpPage);
            if (CurrentStage == stage && FocusArea == choice && HelpPage == helpPage) return;
            CurrentStage = stage; FocusArea = choice; HelpPage = helpPage; SetVerticesDirty();
        }

        protected override void OnEnable() { base.OnEnable(); raycastTarget = false; }
        protected override void OnPopulateMesh(VertexHelper helper)
        {
            helper.Clear(); drawingRect = GetPixelAdjustedRect();
            // Keep the authored 256-point coordinates, but trim their empty margin.
            // Every painted path (including its antialias fringe) stays within 12..244.
            unit = Mathf.Min(drawingRect.width, drawingRect.height) / 232f;
            if (unit <= 0) return;
            mesh = helper;
            antialias = .85f / Mathf.Max(.01f, unit * (canvas == null ? 1 : canvas.scaleFactor));
            Ellipse(128, 228, 75, 6, A(SageDark, .075f));
            DrawCushion(); DrawPerson();
            if (CurrentStage != Stage.Practice) DrawStageContext();
            DrawAttention();
            mesh = null;
        }

        void DrawCushion()
        {
            Fill(new Path(54, 208).Curve(62, 200, 191, 200, 202, 209)
                .Curve(207, 216, 197, 227, 181, 228).Curve(145, 233, 86, 230, 67, 226)
                .Curve(55, 223, 49, 216, 54, 208), C(207, 191, 165), C(180, 164, 140));
            Curve(C(218, 205, 182), 1.7f, 62, 211, 101, 218, 161, 218, 196, 212);
            Curve(C(163, 149, 128), 1, 67, 223, 98, 227, 161, 228, 186, 224);
        }

        void DrawPerson()
        {
            // Folded legs, with separate overlapping fabric planes and quiet seams.
            Fill(new Path(91, 172).Curve(77, 173, 56, 187, 57, 201).Curve(59, 213, 88, 216, 119, 214)
                .Curve(147, 216, 187, 214, 197, 205).Curve(208, 193, 180, 175, 166, 173)
                .Curve(147, 169, 110, 168, 91, 172), C(118, 143, 131), C(93, 119, 108));
            Fill(new Path(76, 186).Curve(88, 182, 105, 191, 127, 199).Curve(146, 206, 171, 208, 185, 202)
                .Curve(190, 212, 161, 219, 137, 216).Curve(111, 214, 82, 207, 72, 199)
                .Curve(67, 195, 68, 190, 76, 186), Sage, C(108, 137, 123));
            Curve(A(Paper, .32f), 1.1f, 74, 191, 96, 192, 112, 209, 144, 210);
            Curve(C(80, 109, 97), 1.2f, 146, 187, 139, 193, 130, 196, 122, 198);
            // Neck and torso. The rounded, asymmetric shoulders avoid a rigid pose.
            Fill(new Path(116, 93).Line(140, 93).Curve(139, 104, 140, 109, 148, 113)
                .Curve(137, 125, 119, 126, 108, 114).Curve(116, 108, 117, 102, 116, 93), Skin, SkinShade);
            Fill(new Path(108, 109).Curve(95, 112, 88, 120, 84, 135).Curve(80, 151, 80, 169, 83, 184)
                .Curve(106, 195, 149, 195, 172, 183).Curve(176, 163, 174, 144, 169, 130)
                .Curve(165, 118, 154, 111, 145, 109).Curve(135, 120, 119, 120, 108, 109), SageLight, Sage);
            Fill(new Path(104, 117).Curve(98, 137, 99, 166, 102, 187).Curve(94, 186, 88, 185, 83, 183)
                .Curve(81, 160, 82, 137, 89, 125).Curve(92, 121, 98, 118, 104, 117), C(148, 173, 151), C(124, 152, 132));
            Curve(C(118, 146, 124), 1.2f, 110, 110, 120, 124, 135, 124, 145, 110);
            Curve(A(Paper, .38f), 1.3f, 112, 129, 111, 142, 111, 153, 113, 161);
            Curve(A(SageDark, .45f), 1.1f, 142, 169, 147, 172, 154, 174, 161, 174);
            // Arms fall naturally towards the knees; the hands are not a prescribed gesture.
            Fill(new Path(91, 120).Curve(81, 123, 78, 139, 75, 152).Curve(73, 164, 73, 173, 82, 179)
                .Line(95, 170).Curve(88, 165, 88, 161, 90, 153).Curve(94, 141, 100, 128, 91, 120), C(173, 191, 170), Sage);
            Fill(new Path(164, 122).Curve(173, 125, 176, 140, 179, 153).Curve(182, 165, 181, 175, 172, 180)
                .Line(160, 171).Curve(168, 166, 168, 160, 165, 151).Curve(162, 139, 156, 127, 164, 122), C(162, 182, 160), C(125, 152, 132));
            Fill(new Path(82, 169).Curve(85, 166, 89, 168, 94, 171).Curve(100, 174, 105, 175, 107, 179)
                .Curve(108, 183, 102, 185, 96, 184).Curve(91, 183, 85, 181, 81, 178)
                .Curve(78, 175, 79, 171, 82, 169), Skin, SkinShade);
            Fill(new Path(172, 170).Curve(168, 167, 164, 169, 160, 172).Curve(155, 175, 150, 176, 149, 180)
                .Curve(149, 184, 154, 185, 160, 184).Curve(166, 183, 172, 180, 174, 177)
                .Curve(176, 174, 175, 172, 172, 170), Skin, SkinShade);
            Curve(A(C(155, 113, 94), .6f), .8f, 86, 175, 91, 179, 97, 181, 101, 180);
            Curve(A(C(155, 113, 94), .6f), .8f, 169, 176, 165, 179, 159, 181, 155, 180);
            // Short cuff seams and soft thumb folds keep the resting hands readable
            // at phone size without outlining every finger.
            Curve(A(SageDark, .48f), 1.15f, 79, 166, 83, 169, 87, 171, 91, 170);
            Curve(A(SageDark, .48f), 1.15f, 166, 170, 170, 171, 175, 168, 177, 165);
            Curve(A(Paper, .44f), 1.2f, 83, 172, 87, 172, 91, 175, 94, 176);
            Curve(A(Paper, .38f), 1.2f, 170, 173, 167, 173, 164, 176, 161, 177);
            // Face, ears and soft hair silhouette.
            Ellipse(104, 77, 5, 9, SkinShade); Ellipse(152, 77, 5, 9, SkinShade);
            Fill(new Path(105, 59).Curve(105, 40, 117, 32, 130, 33).Curve(147, 34, 153, 46, 152, 63)
                .Line(150, 83).Curve(148, 99, 138, 106, 127, 105).Curve(115, 104, 106, 94, 105, 80)
                .Line(105, 59), C(245, 214, 184), Skin);
            Fill(new Path(103, 72).Curve(96, 56, 100, 39, 111, 32).Curve(122, 23, 141, 26, 151, 36)
                .Curve(159, 45, 158, 61, 153, 74).Line(148, 66).Curve(148, 58, 145, 52, 140, 48)
                .Curve(131, 56, 116, 57, 109, 54).Curve(107, 60, 108, 68, 106, 73)
                .Line(103, 72), HairLight, Hair);
            Curve(A(C(154, 134, 109), .45f), 1.2f, 109, 43, 116, 33, 133, 32, 143, 39);
            Curve(C(117, 94, 77), 1.15f, 111, 69, 114, 67, 117, 67, 120, 68);
            Curve(C(117, 94, 77), 1.15f, 136, 68, 140, 66, 143, 67, 146, 69);
            Curve(Ink, 1.3f, 112, 75, 115, 77, 118, 77, 121, 75);
            Curve(Ink, 1.3f, 135, 75, 139, 77, 142, 77, 145, 75);
            Curve(C(190, 142, 115), 1, 129, 76, 128, 80, 127, 84, 132, 84);
            Curve(C(156, 111, 96), 1.1f, 123, 92, 127, 95, 132, 95, 136, 91);
            Ellipse(114, 85, 5, 2.5f, A(C(213, 149, 131), .17f));
            Ellipse(142, 85, 5, 2.5f, A(C(213, 149, 131), .17f));
        }

        void DrawStageContext()
        {
            if (CurrentStage == Stage.Help)
            {
                Fill(new Path(33, 119).Curve(41, 116, 46, 118, 49, 121).Curve(54, 118, 60, 118, 66, 121)
                    .Line(64, 143).Curve(57, 140, 53, 140, 49, 143).Curve(43, 139, 37, 138, 31, 140)
                    .Line(33, 119), C(242, 235, 210), C(214, 217, 190));
                Curve(Attention, 2, 49, 122, 49, 128, 49, 135, 49, 140);
                Curve(A(Attention, .48f), 1.1f, 36, 124, 40, 123, 43, 124, 46, 126);
                Curve(A(Attention, .38f), 1.1f, 35, 131, 39, 130, 43, 131, 46, 133);
                Curve(A(Attention, .48f), 1.1f, 53, 126, 57, 124, 60, 125, 63, 127);
            }
            if (CurrentStage == Stage.Return || CurrentStage == Stage.Help && HelpPage >= 2)
            {
                // Thought and return are a static cue, not a breath or progress timer.
                Fill(new Path(166, 53).Curve(158, 36, 172, 24, 187, 29).Curve(199, 17, 218, 23, 222, 38)
                    .Curve(240, 40, 240, 64, 224, 69).Line(181, 69).Curve(169, 70, 162, 64, 166, 53), C(198, 215, 193), C(160, 187, 162));
                Curve(A(Paper, .7f), 2, 174, 41, 177, 33, 182, 33, 187, 35);
                Ellipse(180, 78, 5, 4.5f, C(155, 180, 157)); Ellipse(171, 89, 2.8f, 2.8f, SageDark);
                // Skipping the choice must not imply that the chest was selected.
                if (!Target.HasValue) return;
                var target = Target.Value;
                float edge = FocusArea == "코" ? 22 : 28;
                AttentionArrow(new Vector2(220, 82), new Vector2(237, 116),
                    new Vector2(197, target.y + 14), new Vector2(target.x + edge, target.y));
                return;
            }
            if (CurrentStage == Stage.Reflection)
            {
                Fill(new Path(35, 149).Curve(34, 145, 37, 142, 42, 143).Line(63, 146)
                    .Curve(67, 146, 69, 149, 68, 153).Line(64, 186).Curve(63, 190, 61, 192, 57, 191)
                    .Line(34, 188).Curve(30, 188, 28, 185, 29, 181).Line(35, 149), Paper, C(233, 225, 204));
                Curve(Attention, 2, 40, 155, 44, 155, 51, 156, 59, 157);
                Curve(A(Attention, .8f), 1.8f, 39, 163, 44, 164, 51, 164, 57, 165);
                Curve(A(Attention, .8f), 1.8f, 38, 172, 41, 172, 45, 173, 49, 173);
                return;
            }
            if (CurrentStage == Stage.Preparation || CurrentStage == Stage.Help && HelpPage == 1)
            {
                Curve(A(Attention, .85f), 2.6f, 59, 150, 51, 170, 52, 187, 60, 199);
                Curve(A(Attention, .85f), 2.6f, 197, 150, 205, 171, 204, 188, 196, 199);
                Curve(Paper, 3, 91, 218, 112, 223, 144, 223, 165, 218);
                return;
            }
            if (CurrentStage == Stage.Overview || CurrentStage == Stage.Observe)
            {
                Curve(A(Attention, .85f), 2.8f, 49, 90, 64, 80, 79, 88, 87, 78);
                Curve(A(Attention, .65f), 2.2f, 47, 103, 63, 94, 78, 103, 89, 93);
                if (CurrentStage == Stage.Observe)
                    Curve(A(Attention, .7f), 2.4f, 186, 103, 198, 123, 200, 148, 193, 162);
            }
        }

        Vector2? Target => FocusArea == "코" ? Nose : FocusArea == "가슴" ? Chest : FocusArea == "배" ? Belly : (Vector2?)null;
        void DrawAttention()
        {
            // Keep the saved focus when navigating back, but introductory pages
            // describe the whole posture rather than a selected body area.
            if (CurrentStage == Stage.Overview || CurrentStage == Stage.Preparation) return;
            var target = Target;
            bool choosing = CurrentStage == Stage.Choice || CurrentStage == Stage.Help && HelpPage == 0;
            if (!target.HasValue)
            {
                if (choosing)
                {
                    Mark(Nose, false); Mark(Chest, false); Mark(Belly, false);
                    Guide(Nose, 70); Guide(Chest, 124); Guide(Belly, 176);
                }
                return;
            }
            var point = target.Value;
            bool quiet = CurrentStage == Stage.Practice;
            float radius = quiet ? 13 : FocusArea == "코" ? 18 : 24;
            Ellipse(point.x, point.y, radius + 4, radius + 4, A(Paper, quiet ? .23f : .36f));
            AttentionRing(point, radius, quiet ? 1.7f : 2.6f, A(Attention, quiet ? .60f : .95f));
            // Body dots identify line endpoints, not permanent marks on the person.
            bool endpoint = choosing || CurrentStage == Stage.Notice || CurrentStage == Stage.Return
                || CurrentStage == Stage.Help && HelpPage >= 2;
            if (endpoint) Mark(point, true);
            if (choosing || CurrentStage == Stage.Notice) Guide(point, point.y - 5);
            if (CurrentStage == Stage.Notice)
                Curve(A(Attention, .65f), 2, point.x - radius - 6, point.y - 10, point.x - radius - 12, point.y - 5, point.x - radius - 12, point.y + 5, point.x - radius - 6, point.y + 10);
        }
        void AttentionRing(Vector2 point, float radius, float width, Color tint)
        {
            float x = point.x, y = point.y, k = radius * .552285f;
            Curve(tint, width, x, y-radius, x+k, y-radius, x+radius, y-k, x+radius, y);
            Curve(tint, width, x+radius, y, x+radius, y+k, x+k, y+radius, x, y+radius);
            Curve(tint, width, x, y+radius, x-k, y+radius, x-radius, y+k, x-radius, y);
            Curve(tint, width, x-radius, y, x-radius, y-k, x-k, y-radius, x, y-radius);
        }
        void Mark(Vector2 point, bool selected)
        {
            Ellipse(point.x, point.y, selected ? 5.6f : 4.2f, selected ? 5.6f : 4.2f, Paper);
            Ellipse(point.x, point.y, selected ? 3.0f : 2.2f, selected ? 3.0f : 2.2f, Attention);
        }
        void Guide(Vector2 point, float endY)
        {
            Curve(A(Attention, .85f), 2.2f, point.x + 9, point.y, 157, point.y, 174, endY, 192, endY);
            Ellipse(195, endY, 3.2f, 3.2f, Attention);
        }
        void AttentionArrow(Vector2 start, Vector2 controlA, Vector2 controlB, Vector2 tip)
        {
            const float width = 2.8f;
            Curve(Attention, width, start.x, start.y, controlA.x, controlA.y, controlB.x, controlB.y, tip.x, tip.y);
            var tangent = (tip - controlB).normalized;
            var normal = new Vector2(-tangent.y, tangent.x);
            var left = tip - tangent * 8 + normal * 4.4f;
            var right = tip - tangent * 8 - normal * 4.4f;
            Curve(Attention, width, left.x, left.y, left.x, left.y, tip.x, tip.y, tip.x, tip.y);
            Curve(Attention, width, right.x, right.y, right.x, right.y, tip.x, tip.y, tip.x, tip.y);
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
