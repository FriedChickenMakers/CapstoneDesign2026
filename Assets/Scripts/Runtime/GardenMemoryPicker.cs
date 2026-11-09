using System;
using System.Collections.Generic;
using System.Linq;
using CapstoneDesign.Plants;
using UnityEngine;
using UnityEngine.Rendering;

namespace CapstoneDesign.Runtime
{
    // Picking data belongs to the displayed plant, never to the shared photo studio.
    // The existing RawImage gesture handlers remain the sole pointer owners.
    public static class GardenMemoryPicker
    {
        public const string RepresentationKey = "memory-leaf-v1";
        public const float DesignTouchRadius = 16;
        public const int MaximumAssistedProbesPerLeaf = 4;
        public delegate bool ScreenRay(Vector2 point, out Ray ray);
        public delegate bool ScreenProjection(Vector3 point, out Vector2 screenPoint);
        public static float TouchRadiusPixels(Canvas canvas)
        {
            float width = canvas != null ? canvas.rootCanvas.pixelRect.width : Screen.width;
            return Mathf.Clamp(DesignTouchRadius * width / 390f, 12, 24);
        }
        public static string Signature(IEnumerable<PlantMemoryLeaf> leaves, int page) => page + ":" +
            string.Join("|", (leaves ?? Enumerable.Empty<PlantMemoryLeaf>()).OrderBy(m => m.Slot)
                .Select(m => m.OrganId + ":" + m.Slot + ":" + m.Linked));

        public static Snapshot Capture(ProceduralPlantView plant, Transform occlusionRoot = null)
        {
            if (plant == null || plant.Geometry == null) return null;
            var occluders = new List<GardenPlantMemoView.Occluder>();
            if (occlusionRoot != null)
                foreach (var filter in occlusionRoot.GetComponentsInChildren<MeshFilter>())
                {
                    if (filter.transform == plant.transform || filter.transform.IsChildOf(plant.transform)) continue;
                    var renderer = filter.GetComponent<MeshRenderer>();
                    if (filter.sharedMesh == null || !filter.sharedMesh.isReadable || renderer == null || !renderer.enabled
                        || !renderer.gameObject.activeInHierarchy || renderer.shadowCastingMode == ShadowCastingMode.ShadowsOnly
                        || renderer.sharedMaterial == null || renderer.sharedMaterial.renderQueue >= 3000) continue;
                    occluders.Add(new GardenPlantMemoView.Occluder(filter));
                }
            return new Snapshot(plant, occluders.ToArray());
        }

        public sealed class Snapshot
        {
            readonly Vector3[] vertices;
            readonly PlantOrganGeometry[] organs;
            readonly Matrix4x4 inverse;
            readonly Matrix4x4 localToWorld;
            readonly GardenPlantMemoView.Occluder[] occluders;
            public IReadOnlyList<string> MemoryIds { get; }
            public Bounds WorldBounds { get; }
            public Matrix4x4 LocalToWorld => localToWorld;

            internal Snapshot(ProceduralPlantView plant, GardenPlantMemoView.Occluder[] blockers)
            {
                vertices = plant.Geometry.Vertices.Select(v => new Vector3((float)v.X, (float)v.Y, (float)v.Z)).ToArray();
                organs = plant.Geometry.Organs.Where(o => o.Selectable).Select(o => new PlantOrganGeometry {
                    OrganId = o.OrganId, FirstTriangle = o.FirstTriangle, TriangleCount = o.TriangleCount,
                    Center = o.Center, Selectable = true }).ToArray();
                inverse = plant.transform.worldToLocalMatrix;
                localToWorld = plant.transform.localToWorldMatrix; occluders = blockers;
                MemoryIds = Array.AsReadOnly(organs.Select(o => o.OrganId).ToArray());
                var bounds = new Bounds(plant.transform.position, Vector3.zero);
                bool first = true;
                foreach (var organ in organs)
                    for (int i = organ.FirstTriangle * 3; i < (organ.FirstTriangle + organ.TriangleCount) * 3; i++)
                    {
                        var point = plant.transform.TransformPoint(vertices[i]);
                        if (first) { bounds = new Bounds(point, Vector3.zero); first = false; }
                        else bounds.Encapsulate(point);
                    }
                WorldBounds = bounds;
            }

            public bool TryPick(Ray ray, out string organId, out float distance)
            {
                organId = null; distance = float.PositiveInfinity;
                if (!ValidRay(ray)) return false;
                var origin = inverse.MultiplyPoint3x4(ray.origin);
                // Do not normalize this transformed direction: hit distance stays in world units.
                var direction = inverse.MultiplyVector(ray.direction.normalized);
                int nearest = -1;
                for (int i = 0; i + 2 < vertices.Length; i += 3)
                {
                    var a = vertices[i]; var ab = vertices[i + 1] - a; var ac = vertices[i + 2] - a;
                    var p = Vector3.Cross(direction, ac); float det = Vector3.Dot(ab, p);
                    if (Mathf.Abs(det) < .0000001f) continue;
                    var delta = origin - a; float u = Vector3.Dot(delta, p) / det; if (u < 0 || u > 1) continue;
                    var q = Vector3.Cross(delta, ab); float v = Vector3.Dot(direction, q) / det; if (v < 0 || u + v > 1) continue;
                    float at = Vector3.Dot(ac, q) / det;
                    if (at >= 0 && at < distance) { distance = at; nearest = i / 3; }
                }
                // The nearest whole-plant triangle must belong to a linked leaf.
                // A leaf behind the stem, another leaf, or a flower cannot be opened through it.
                var hit = organs.FirstOrDefault(o => nearest >= o.FirstTriangle && nearest < o.FirstTriangle + o.TriangleCount);
                if (hit == null) return false;
                foreach (var blocker in occluders) if (blocker.Blocks(ray, distance)) return false;
                organId = hit.OrganId; return true;
            }

            struct Candidate
            {
                public string Id;
                public Vector2 Point;
                public float Pixels;
            }

            // Only visible triangles of this immutable snapshot can be assisted.
            // Delegates must reject points outside the actual RawImage/mask and
            // project using the same captured view that produced its pixels.
            public bool TryPickNearby(Vector2 screenPoint, ScreenRay rayAt, ScreenProjection project,
                float radiusPixels, out string organId, out float distance)
            {
                organId = null; distance = float.PositiveInfinity;
                if (rayAt == null || project == null || !Finite(screenPoint) || !Finite(radiusPixels)
                    || radiusPixels < 0 || !rayAt(screenPoint, out var direct) || !ValidRay(direct)) return false;
                if (TryPick(direct, out organId, out distance)) return true;
                // Touching a stem, flower, or unlinked leaf is deliberate input,
                // not empty space around a small linked target.
                if (!float.IsPositiveInfinity(distance) || radiusPixels == 0) return false;
                radiusPixels = Mathf.Min(radiusPixels, 24);
                var candidates = new List<Candidate>();
                foreach (var organ in organs)
                    for (int triangle = organ.FirstTriangle; triangle < organ.FirstTriangle + organ.TriangleCount; triangle++)
                    {
                        int at = triangle * 3;
                        if (!project(localToWorld.MultiplyPoint3x4(vertices[at]), out var a)
                            || !project(localToWorld.MultiplyPoint3x4(vertices[at + 1]), out var b)
                            || !project(localToWorld.MultiplyPoint3x4(vertices[at + 2]), out var c)
                            || !Finite(a) || !Finite(b) || !Finite(c)) continue;
                        var closest = ClosestTriangle(screenPoint, a, b, c);
                        // Move a fraction of a pixel inside the triangle so the
                        // visibility ray does not depend on shared-edge rounding.
                        var inside = (a + b + c) / 3 - closest;
                        var probe = closest + Vector2.ClampMagnitude(inside, radiusPixels / 64f);
                        float pixels = Vector2.Distance(screenPoint, probe);
                        if (pixels <= radiusPixels) candidates.Add(new Candidate { Id = organ.OrganId, Point = probe, Pixels = pixels });
                    }
                candidates.Sort((a, b) => a.Pixels.CompareTo(b.Pixels));
                float bestPixels = float.PositiveInfinity, margin = radiusPixels * .125f;
                string bestId = null; float bestDistance = float.PositiveInfinity;
                var attempted = new Dictionary<string,List<Vector2>>();
                foreach (var candidate in candidates)
                {
                    if (candidate.Pixels > bestPixels + margin) break;
                    if (candidate.Id == bestId) continue;
                    if (!attempted.TryGetValue(candidate.Id,out var points))
                        attempted[candidate.Id]=points=new List<Vector2>(MaximumAssistedProbesPerLeaf);
                    if (points.Count>=MaximumAssistedProbesPerLeaf
                        || points.Any(point=>(point-candidate.Point).sqrMagnitude<.25f)) continue;
                    points.Add(candidate.Point);
                    // Bound expensive full-mesh visibility rays to four per
                    // leaf (at most 84 for a full page), not one per triangle.
                    if (!rayAt(candidate.Point, out var ray) || !TryPick(ray, out var hit, out var at) || hit != candidate.Id) continue;
                    var hitPoint = ray.origin + ray.direction.normalized * at;
                    float directDepth = Vector3.Dot(hitPoint - direct.origin, direct.direction.normalized);
                    if (directDepth <= 0 || occluders.Any(blocker => blocker.Blocks(direct, directDepth))) continue;
                    if (bestId != null && hit != bestId)
                    {
                        // Equally plausible neighbouring leaves require another
                        // deliberate tap; list order never decides a private record.
                        organId = null; distance = float.PositiveInfinity; return false;
                    }
                    bestId = hit; bestPixels = candidate.Pixels; bestDistance = at;
                }
                organId = bestId; distance = bestDistance; return bestId != null;
            }

            static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
            static bool Finite(Vector2 value) => Finite(value.x) && Finite(value.y);
            static bool ValidRay(Ray ray)
            {
                for (int axis = 0; axis < 3; axis++)
                    if (!Finite(ray.origin[axis]) || !Finite(ray.direction[axis])) return false;
                float length = ray.direction.sqrMagnitude;
                return Finite(length) && length >= .000001f;
            }
            static Vector2 ClosestSegment(Vector2 point, Vector2 a, Vector2 b)
            {
                var ab = b - a;
                return a + ab * (ab.sqrMagnitude < .000001f ? 0 : Mathf.Clamp01(Vector2.Dot(point - a, ab) / ab.sqrMagnitude));
            }
            static Vector2 ClosestTriangle(Vector2 point, Vector2 a, Vector2 b, Vector2 c)
            {
                float Cross(Vector2 x, Vector2 y) => x.x * y.y - x.y * y.x;
                float area = Cross(b - a, c - a);
                if (Mathf.Abs(area) > .000001f)
                {
                    float u = Cross(b - point, c - point) / area;
                    float v = Cross(c - point, a - point) / area;
                    if (u >= 0 && v >= 0 && u + v <= 1) return point;
                }
                var ab = ClosestSegment(point, a, b); var bc = ClosestSegment(point, b, c); var ca = ClosestSegment(point, c, a);
                var nearest = (ab - point).sqrMagnitude < (bc - point).sqrMagnitude ? ab : bc;
                return (nearest - point).sqrMagnitude < (ca - point).sqrMagnitude ? nearest : ca;
            }
        }
    }
}
