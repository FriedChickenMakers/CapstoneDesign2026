using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace CapstoneDesign.Runtime
{
    // The same imported, lit models serve home, purchase preview and the legacy view.
    // Coordinates are in garden-local space. No progress or purchases are stored here.
    public static class GardenSceneModels
    {
        public const string ResourceRoot = "GardenModels/";
        public static GameObject CreateEnvironment(string id, Transform parent = null)
        {
            var model = Instantiate(id == "environment:E01" ? "FlowerBed" : "RestCorner", parent);
            model.name = id;
            Place(model.transform, id == "environment:E01" ? new Vector2(-.96f, .12f) : new Vector2(.95f, .20f),
                id == "environment:E01" ? -12 : -22, parent);
            return model;
        }

        public static GameObject CreateVisitor(string id, Transform parent = null)
        {
            string suffix = id != null && id.StartsWith("animal:A", StringComparison.Ordinal) ? id.Substring(8) : "01";
            var model = Instantiate("Visitor" + suffix, parent);
            model.name = "Visitor " + id;
            Place(model.transform, new Vector2(-.86f, -.58f), 155, parent);
            return model;
        }

        static GameObject Instantiate(string name, Transform parent)
        {
            var source = Resources.Load<GameObject>(ResourceRoot + name);
            if (source == null) throw new InvalidOperationException("Missing garden art: " + name);
            return UnityEngine.Object.Instantiate(source, parent, false);
        }

        public static void Place(Transform model, Vector2 xz, float yaw, Transform garden)
        {
            model.localRotation = Quaternion.Euler(0, yaw, 0);
            model.localPosition = new Vector3(xz.x, 0, xz.y);
            if (!TryBounds(model, garden, out var bounds)) return;
            // Asset origins are inconsistent. Center the visible footprint, then
            // ground its actual minimum on the imported island surface.
            model.localPosition += new Vector3(xz.x - bounds.center.x, 0, xz.y - bounds.center.z);
            TryBounds(model, garden, out bounds);
            float ground = GroundHeight(garden, xz);
            model.localPosition += Vector3.up * (ground - bounds.min.y + .002f);
        }

        public static float GroundHeight(Transform garden, Vector2 xz)
        {
            if (garden == null) return .08f;
            var surface = garden.GetComponentInParent<GardenSurface>();
            if (surface == null) surface = garden.GetComponentInChildren<GardenSurface>(true);
            return surface != null ? surface.HeightInSpace(garden, xz) : .08f;
        }

        public static bool TryBounds(Transform root, Transform relativeTo, out Bounds bounds)
        {
            bounds = default;
            bool found = false;
            foreach (var filter in root.GetComponentsInChildren<MeshFilter>(true))
            {
                if (filter.sharedMesh == null) continue;
                var b = filter.sharedMesh.bounds;
                for (int i = 0; i < 8; i++)
                {
                    var corner = b.center + Vector3.Scale(b.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                    var p = filter.transform.TransformPoint(corner);
                    if (relativeTo != null) p = relativeTo.InverseTransformPoint(p);
                    if (found) bounds.Encapsulate(p); else { bounds = new Bounds(p, Vector3.zero); found = true; }
                }
            }
            return found;
        }

        public static void ConfigureRenderers(GameObject model)
        {
            foreach (var node in model.GetComponentsInChildren<Transform>(true)) node.gameObject.layer = 30;
            foreach (var renderer in model.GetComponentsInChildren<Renderer>(true))
            {
                renderer.shadowCastingMode = ShadowCastingMode.On;
                renderer.receiveShadows = true;
            }
        }
    }

}
