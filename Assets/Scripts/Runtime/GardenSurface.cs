using System;
using UnityEngine;

namespace CapstoneDesign.Runtime
{
    // Local mesh intersection works in edit-mode captures as well as on devices,
    // and does not require a physics collider or a physics simulation step.
    public sealed class GardenSurface : MonoBehaviour
    {
        public MeshFilter surface;
        public float HeightInSpace(Transform relativeTo, Vector2 xz)
        {
            if (surface == null || surface.sharedMesh == null) return .08f;
            var origin = relativeTo.TransformPoint(new Vector3(xz.x, 4, xz.y));
            var localOrigin = surface.transform.InverseTransformPoint(origin);
            var down = surface.transform.InverseTransformVector(relativeTo.TransformDirection(Vector3.down));
            var mesh = surface.sharedMesh;
            var vertices = mesh.vertices; var triangles = mesh.triangles;
            float distance = float.MaxValue;
            for (int i = 0; i < triangles.Length; i += 3)
            {
                var a = vertices[triangles[i]]; var b = vertices[triangles[i + 1]]; var c = vertices[triangles[i + 2]];
                var e1 = b - a; var e2 = c - a; var p = Vector3.Cross(down, e2);
                float determinant = Vector3.Dot(e1, p);
                if (Mathf.Abs(determinant) < .000001f) continue;
                float inverse = 1 / determinant;
                var t = localOrigin - a; float u = Vector3.Dot(t, p) * inverse;
                if (u < 0 || u > 1) continue;
                var q = Vector3.Cross(t, e1); float v = Vector3.Dot(down, q) * inverse;
                if (v < 0 || u + v > 1) continue;
                float hit = Vector3.Dot(e2, q) * inverse;
                if (hit >= 0 && hit < distance) distance = hit;
            }
            if (distance == float.MaxValue) throw new InvalidOperationException("Garden placement is outside the island surface.");
            return relativeTo.InverseTransformPoint(surface.transform.TransformPoint(localOrigin + down * distance)).y;
        }
    }
}
