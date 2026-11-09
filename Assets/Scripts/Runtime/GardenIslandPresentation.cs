using System;
using UnityEngine;

namespace CapstoneDesign.Runtime
{
    // A scene-owned presentation of the known derived island. The authored
    // surface/asset, furniture transforms, placement rules and saves stay intact.
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public sealed class GardenIslandPresentation : MonoBehaviour
    {
        public const float LowerDepthScale = .55f;
        public const string SourceMeshName = "Island-level-meadow";
        public const string PresentedMeshName = "Island-level-meadow (shallow owned)";
        MeshFilter target;
        Mesh source;
        Mesh presented;
        public Mesh SourceMesh => source;
        public Mesh PresentedMesh => presented;
        public bool IsApplied => target != null && presented != null && target.sharedMesh == presented;

        public static GardenIslandPresentation Ensure(GardenSurface surface)
        {
            // Never attach a transient component to an imported prefab asset.
            if (surface == null || surface.surface == null || !surface.gameObject.scene.IsValid()) return null;
            var existing = surface.GetComponent<GardenIslandPresentation>();
            if (existing == null)
            {
                var mesh = surface.surface.sharedMesh;
                if (mesh == null || mesh.name != SourceMeshName || !mesh.isReadable) return null;
                existing = surface.gameObject.AddComponent<GardenIslandPresentation>();
                existing.hideFlags = HideFlags.DontSave;
            }
            existing.Bind(surface.surface);
            return existing;
        }

        void Bind(MeshFilter filter)
        {
            if (target == filter && source != null)
            {
                if (isActiveAndEnabled) Apply();
                return;
            }
            Release();
            target = filter;
            source = filter != null ? filter.sharedMesh : null;
            if (isActiveAndEnabled) Apply();
        }

        void OnEnable() => Apply();
        void OnDisable() => Release();
        void OnDestroy() => Release();

        void Apply()
        {
            if (presented != null || target == null || source == null || target.sharedMesh != source
                || source.name != SourceMeshName || !source.isReadable) return;
            var vertices = source.vertices;
            // This asset has a level meadow at .0825, its supporting rim at 0,
            // and a generated lower cap at -.1827525 in mesh-local coordinates.
            // Require that shape; a later different asset is left untouched.
            bool lower = false, rim = false, meadow = false;
            foreach (var point in vertices)
            {
                if (Mathf.Abs(point.y + .1827525f) < .000001f) lower = true;
                else if (point.y == 0) rim = true;
                else if (Mathf.Abs(point.y - .0825f) < .000001f) meadow = true;
                else return;
            }
            if (!lower || !rim || !meadow) return;
            for (int i = 0; i < vertices.Length; i++)
                if (vertices[i].y < 0) vertices[i].y *= LowerDepthScale;

            Mesh clone = null;
            try
            {
                clone = Instantiate(source);
                clone.name = PresentedMeshName;
                clone.hideFlags = HideFlags.DontSave;
                clone.vertices = vertices;
                clone.RecalculateNormals();
                clone.RecalculateBounds();
                // Publish only a complete clone. Triangles, x/z, meadow and rim
                // remain exact, including GardenSurface's upper ray intersections.
                presented = clone;
                target.sharedMesh = presented;
            }
            catch
            {
                if (clone != null) DisposeMesh(clone);
                throw;
            }
        }

        void Release()
        {
            var owned = presented;
            presented = null;
            if (owned == null) return;
            // Do not overwrite a different view's subsequent replacement.
            if (target != null && target.sharedMesh == owned) target.sharedMesh = source;
            DisposeMesh(owned);
        }

        static void DisposeMesh(Mesh mesh)
        {
            if (Application.isPlaying) Destroy(mesh);
            else DestroyImmediate(mesh);
        }
    }
}
