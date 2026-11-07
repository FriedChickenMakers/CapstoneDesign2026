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
            readonly GardenPlantMemoView.Occluder[] occluders;
            public IReadOnlyList<string> MemoryIds { get; }
            public Bounds WorldBounds { get; }

            internal Snapshot(ProceduralPlantView plant, GardenPlantMemoView.Occluder[] blockers)
            {
                vertices = plant.Geometry.Vertices.Select(v => new Vector3((float)v.X, (float)v.Y, (float)v.Z)).ToArray();
                organs = plant.Geometry.Organs.Where(o => o.Selectable).Select(o => new PlantOrganGeometry {
                    OrganId = o.OrganId, FirstTriangle = o.FirstTriangle, TriangleCount = o.TriangleCount,
                    Center = o.Center, Selectable = true }).ToArray();
                inverse = plant.transform.worldToLocalMatrix; occluders = blockers;
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
                for (int axis = 0; axis < 3; axis++)
                    if (float.IsNaN(ray.origin[axis]) || float.IsInfinity(ray.origin[axis])
                        || float.IsNaN(ray.direction[axis]) || float.IsInfinity(ray.direction[axis])) return false;
                if (ray.direction.sqrMagnitude < .000001f) return false;
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
        }
    }
}
