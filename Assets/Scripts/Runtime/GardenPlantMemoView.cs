using System;
using System.Collections.Generic;
using System.Text;
using CapstoneDesign.Runtime.LocalState;
using UnityEngine;
using UnityEngine.Rendering;

namespace CapstoneDesign.Runtime
{
    // A presentation of saved memories, independent of their text and lifecycle.
    // Each memo remains one six-sided paper block; batches only reduce draw calls.
    [ExecuteAlways, DisallowMultipleComponent]
    public sealed class GardenPlantMemoView : MonoBehaviour
    {
        public const string PaperRepresentation = "paper-block-v1";
        public const int MemosPerBatch = 512;
        const int TrianglesPerMemo = 12;
        readonly List<Mesh> meshes = new List<Mesh>();
        readonly List<GameObject> batches = new List<GameObject>();
        string[] memoIds = Array.Empty<string>();
        Bounds[] memoBounds = Array.Empty<Bounds>();
        Matrix4x4[] memoToLocal = Array.Empty<Matrix4x4>();
        Vector3[] memoHalfSizes = Array.Empty<Vector3>();
        Material material;
        Texture2D paper;
        string signature;
        public string RepresentationKey => PaperRepresentation;
        public int MemoCount => memoIds.Length;
        public int BatchCount => meshes.Count;
        public IReadOnlyList<string> MemoIds => Array.AsReadOnly(memoIds);
        public Bounds LocalBounds { get; private set; }
        public int BuildCount { get; private set; }
        public static int OwnedMeshCount { get; private set; }
        public static int OwnedMaterialCount { get; private set; }
        public static int OwnedTextureCount { get; private set; }

        public static string SignatureFor(IReadOnlyList<GardenPlantMemo> memos)
        {
            var value = new StringBuilder(PaperRepresentation);
            if (memos != null)
                foreach (var memo in memos)
                {
                    string id = memo?.Id ?? "";
                    value.Append('|').Append(id.Length).Append(':').Append(id);
                }
            return value.ToString();
        }

        public bool Render(IReadOnlyList<GardenPlantMemo> memos, Func<Vector2, float> groundHeight, int layer)
        {
            var nextSignature = SignatureFor(memos);
            if (signature == nextSignature) return false;
            ClearMeshes();
            signature = null;
            int count = memos?.Count ?? 0;
            memoIds = new string[count]; memoBounds = new Bounds[count];
            memoToLocal = new Matrix4x4[count]; memoHalfSizes = new Vector3[count];
            LocalBounds = default;
            if (count == 0)
            {
                ReleaseAppearance();
                signature = nextSignature;
                BuildCount++;
                return true;
            }
            EnsureAppearance();
            // The front-right space avoids the flower bed, visitor and shelter.
            // A vertical grid keeps back rows visible even at the album's 8° pitch.
            // No count is dropped: many memories become smaller within this envelope.
            const float paperWidth = .25f, paperHeight = .33f, gap = .045f;
            const float maxWidth = .74f, maxHeight = .86f;
            int columns = Mathf.Max(1, Mathf.CeilToInt(Mathf.Sqrt(count * maxWidth / maxHeight * (paperHeight + gap) / (paperWidth + gap))));
            int rows = Mathf.CeilToInt(count / (float)columns);
            float size = Mathf.Min(1, Mathf.Min(maxWidth / (columns * (paperWidth + gap)), maxHeight / (rows * (paperHeight + gap))));
            float stepX = (paperWidth + gap) * size, stepY = (paperHeight + gap) * size;
            var rotation = Quaternion.Euler(0, -22, 0);
            var anchor = new Vector2(.68f, -.62f);
            float ground = groundHeight == null ? .008f : groundHeight(anchor) + .008f;
            for (int start = 0; start < count; start += MemosPerBatch)
            {
                int end = Math.Min(count, start + MemosPerBatch);
                var vertices = new List<Vector3>((end - start) * 24);
                var normals = new List<Vector3>((end - start) * 24);
                var colors = new List<Color>((end - start) * 24);
                var uvs = new List<Vector2>((end - start) * 24);
                var indices = new List<int>((end - start) * 36);
                for (int index = start; index < end; index++)
                {
                    string id = memos[index]?.Id ?? "";
                    memoIds[index] = id;
                    int column = index % columns, row = index / columns;
                    var offset = rotation * new Vector3((column - (columns - 1) * .5f) * stepX, 0, 0);
                    var center = new Vector3(anchor.x + offset.x, ground + paperHeight * size * .5f + row * stepY, anchor.y + offset.z);
                    var half = new Vector3(paperWidth, paperHeight, .018f) * (size * .5f);
                    memoToLocal[index] = Matrix4x4.TRS(center, rotation, Vector3.one);
                    memoHalfSizes[index] = half;
                    uint hash = StableHash(id);
                    Color tint = Color.Lerp(new Color(1, .91f, .56f), new Color(1, .79f, .58f), (hash & 255) / 255f).linear;
                    var corners = new Vector3[8];
                    for (int corner = 0; corner < 8; corner++)
                        corners[corner] = center + rotation * Vector3.Scale(half,
                            new Vector3((corner & 1) == 0 ? -1 : 1, (corner & 2) == 0 ? -1 : 1, (corner & 4) == 0 ? -1 : 1));
                    var bounds = new Bounds(corners[0], Vector3.zero);
                    for (int corner = 1; corner < 8; corner++) bounds.Encapsulate(corners[corner]);
                    memoBounds[index] = bounds;
                    if (index == 0) LocalBounds = bounds; else { var all = LocalBounds; all.Encapsulate(bounds); LocalBounds = all; }
                    Face(0, 2, 3, 1, Vector3.back, true);
                    Face(4, 5, 7, 6, Vector3.forward, true);
                    Face(0, 4, 6, 2, Vector3.left, false);
                    Face(1, 3, 7, 5, Vector3.right, false);
                    Face(2, 6, 7, 3, Vector3.up, false);
                    Face(0, 1, 5, 4, Vector3.down, false);

                    void Face(int a, int b, int c, int d, Vector3 normal, bool printed)
                    {
                        int first = vertices.Count;
                        vertices.Add(corners[a]); vertices.Add(corners[b]); vertices.Add(corners[c]); vertices.Add(corners[d]);
                        for (int v = 0; v < 4; v++) { normals.Add(rotation * normal); colors.Add(tint); }
                        if (printed)
                        {
                            if (normal == Vector3.forward)
                            {
                                uvs.Add(new Vector2(1, 0)); uvs.Add(new Vector2(0, 0));
                                uvs.Add(new Vector2(0, 1)); uvs.Add(new Vector2(1, 1));
                            }
                            else
                            {
                                uvs.Add(new Vector2(0, 0)); uvs.Add(new Vector2(0, 1));
                                uvs.Add(new Vector2(1, 1)); uvs.Add(new Vector2(1, 0));
                            }
                        }
                        else for (int v = 0; v < 4; v++) uvs.Add(new Vector2(.04f, .5f));
                        indices.Add(first); indices.Add(first + 1); indices.Add(first + 2);
                        indices.Add(first); indices.Add(first + 2); indices.Add(first + 3);
                    }
                }
                var mesh = new Mesh { name = "메모 종이 묶음 " + start, hideFlags = HideFlags.DontSave };
                mesh.SetVertices(vertices); mesh.SetNormals(normals); mesh.SetColors(colors);
                mesh.SetUVs(0, uvs); mesh.SetTriangles(indices, 0); mesh.RecalculateBounds();
                var node = new GameObject("메모 " + start + "–" + (end - 1)) { layer = layer, hideFlags = HideFlags.DontSave };
                node.transform.SetParent(transform, false);
                node.AddComponent<MeshFilter>().sharedMesh = mesh;
                var renderer = node.AddComponent<MeshRenderer>(); renderer.sharedMaterial = material;
                renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = true;
                meshes.Add(mesh); batches.Add(node); OwnedMeshCount++;
            }
            signature = nextSignature;
            BuildCount++;
            return true;
        }

        public Bounds GetMemoBounds(int index)
        {
            if (index < 0 || index >= memoBounds.Length) throw new ArgumentOutOfRangeException(nameof(index));
            return memoBounds[index];
        }

        public string GetMemoIdForTriangle(int batchIndex, int triangleIndex)
        {
            if (batchIndex < 0 || batchIndex >= batches.Count || triangleIndex < 0) return null;
            int within = triangleIndex / TrianglesPerMemo;
            if (within >= MemosPerBatch) return null;
            int index = batchIndex * MemosPerBatch + within;
            return index < memoIds.Length ? memoIds[index] : null;
        }

        public bool TryPickMemo(Ray worldRay, out string memoId, out float worldDistance)
            => CapturePickingSnapshot().TryPick(worldRay, out memoId, out worldDistance);

        // Immutable CPU geometry belongs to the rendered image, not the shared
        // photo studio. Later thumbnails can replace studio meshes safely.
        public PickingSnapshot CapturePickingSnapshot(Transform occlusionRoot = null)
        {
            var matrices = new Matrix4x4[memoIds.Length];
            for (int i = 0; i < matrices.Length; i++) matrices[i] = (transform.localToWorldMatrix * memoToLocal[i]).inverse;
            return new PickingSnapshot((string[])memoIds.Clone(), matrices, (Vector3[])memoHalfSizes.Clone(),
                CaptureOccluders(occlusionRoot, this));
        }

        public static bool IsOccluded(Ray ray, float distance, Transform root, GardenPlantMemoView excluded)
        {
            if (root == null) return false;
            foreach (var filter in root.GetComponentsInChildren<MeshFilter>())
            {
                if (filter.GetComponentInParent<GardenPlantMemoView>() == excluded) continue;
                var renderer = filter.GetComponent<MeshRenderer>();
                if (!CanOcclude(filter, renderer) || !renderer.bounds.IntersectRay(ray, out float entry) || entry >= distance) continue;
                if (new Occluder(filter).Blocks(ray, distance)) return true;
            }
            return false;
        }
        static bool CanOcclude(MeshFilter filter, MeshRenderer renderer) => filter.sharedMesh != null && filter.sharedMesh.isReadable
            && renderer != null && renderer.enabled && renderer.gameObject.activeInHierarchy
            && renderer.shadowCastingMode != ShadowCastingMode.ShadowsOnly
            && renderer.sharedMaterial != null && renderer.sharedMaterial.renderQueue < 3000;
        static Occluder[] CaptureOccluders(Transform root, GardenPlantMemoView excluded)
        {
            if (root == null) return Array.Empty<Occluder>();
            var result = new List<Occluder>();
            foreach (var filter in root.GetComponentsInChildren<MeshFilter>())
                if (filter.GetComponentInParent<GardenPlantMemoView>() != excluded && CanOcclude(filter, filter.GetComponent<MeshRenderer>()))
                    result.Add(new Occluder(filter));
            return result.ToArray();
        }
        internal sealed class Occluder
        {
            readonly Vector3[] vertices;
            readonly int[] triangles;
            readonly Matrix4x4 inverse;
            readonly Bounds bounds;
            readonly bool twoSided;
            public Occluder(MeshFilter filter)
            {
                vertices=filter.sharedMesh.vertices; triangles=filter.sharedMesh.triangles;
                inverse=filter.transform.worldToLocalMatrix;
                var renderer=filter.GetComponent<MeshRenderer>(); bounds=renderer.bounds;
                var material=renderer.sharedMaterial;
                twoSided=material.shader.name=="MindfulGarden/ProceduralPlantVertexColor"
                    || material.shader.name=="Capstone/Album Meadow" || material.HasProperty("_Cull") && material.GetFloat("_Cull")==0;
            }
            public bool Blocks(Ray ray,float limit)
            {
                if(!bounds.IntersectRay(ray,out float entry) || entry>=limit)return false;
                var origin=inverse.MultiplyPoint3x4(ray.origin); var direction=inverse.MultiplyVector(ray.direction.normalized);
                for(int i=0;i+2<triangles.Length;i+=3)
                {
                    var a=vertices[triangles[i]]; var edge1=vertices[triangles[i+1]]-a; var edge2=vertices[triangles[i+2]]-a;
                    var p=Vector3.Cross(direction,edge2);float determinant=Vector3.Dot(edge1,p);
                    if(twoSided ? Mathf.Abs(determinant)<.0000001f : determinant<.0000001f)continue;
                    float inv=1/determinant;var s=origin-a;float u=Vector3.Dot(s,p)*inv;if(u<0 || u>1)continue;
                    var q=Vector3.Cross(s,edge1);float v=Vector3.Dot(direction,q)*inv;if(v<0 || u+v>1)continue;
                    float at=Vector3.Dot(edge2,q)*inv;if(at>=0 && at<limit-.0005f)return true;
                }
                return false;
            }
        }
        public sealed class PickingSnapshot
        {
            readonly string[] ids;
            readonly Matrix4x4[] inverse;
            readonly Vector3[] halfSizes;
            readonly Occluder[] occluders;
            internal PickingSnapshot(string[] ids, Matrix4x4[] inverse, Vector3[] halfSizes, Occluder[] occluders)
            {this.ids=ids;this.inverse=inverse;this.halfSizes=halfSizes;this.occluders=occluders;}
            public bool TryPick(Ray ray,out string memoId,out float distance)
            {
                memoId=null;distance=float.PositiveInfinity;
                for(int axis=0;axis<3;axis++)if(float.IsNaN(ray.origin[axis]) || float.IsInfinity(ray.origin[axis])
                    || float.IsNaN(ray.direction[axis]) || float.IsInfinity(ray.direction[axis]))return false;
                if(ray.direction.sqrMagnitude<.000001f)return false;
                var direction=ray.direction.normalized;
                for(int i=0;i<ids.Length;i++)
                {
                    var origin=inverse[i].MultiplyPoint3x4(ray.origin);var delta=inverse[i].MultiplyVector(direction);
                    float near=0,far=float.PositiveInfinity;bool hit=true;
                    for(int axis=0;axis<3;axis++)
                    {
                        if(Mathf.Abs(delta[axis])<.0000001f) {if(Mathf.Abs(origin[axis])>halfSizes[i][axis]){hit=false;break;}continue;}
                        float a=(-halfSizes[i][axis]-origin[axis])/delta[axis],b=(halfSizes[i][axis]-origin[axis])/delta[axis];
                        near=Mathf.Max(near,Mathf.Min(a,b));far=Mathf.Min(far,Mathf.Max(a,b));
                        if(far<near){hit=false;break;}
                    }
                    if(hit && near<distance){distance=near;memoId=ids[i];}
                }
                if(memoId==null)return false;
                foreach(var occluder in occluders)if(occluder.Blocks(ray,distance)){memoId=null;distance=float.PositiveInfinity;return false;}
                return true;
            }
        }

        void EnsureAppearance()
        {
            if (material != null) return;
            var shader = Resources.Load<Shader>("GardenAtmosphere/PlantMemoPaper");
            if (shader == null) throw new InvalidOperationException("The garden memo paper shader is missing.");
            paper = BuildPaperTexture(); OwnedTextureCount++;
            material = new Material(shader) { name = "메모 종이 (owned)", hideFlags = HideFlags.DontSave };
            material.SetTexture("_BaseMap", paper); OwnedMaterialCount++;
        }

        static Texture2D BuildPaperTexture()
        {
            const int side = 128;
            var pixels = new Color32[side * side];
            for (int y = 0; y < side; y++)
            for (int x = 0; x < side; x++)
            {
                uint grain = StableHash((x + y * side).ToString(System.Globalization.CultureInfo.InvariantCulture));
                float shade = .94f + (grain & 31) / 31f * .055f;
                if (y > 103) shade *= .94f; // The adhesive band reads as a separate paper surface.
                if (x < 2 || x > 125 || y < 2) shade *= .84f;
                bool line = x >= 18 && x <= (y < 44 ? 75 : 108) && ((y >= 35 && y <= 37) || (y >= 55 && y <= 57) || (y >= 75 && y <= 77));
                if (line) shade *= .57f;
                byte color = (byte)Mathf.RoundToInt(shade * 255);
                pixels[y * side + x] = new Color32(color, color, color, 255);
            }
            var texture = new Texture2D(side, side, TextureFormat.RGBA32, true, false)
                { name = "메모 post-it paper (owned)", hideFlags = HideFlags.DontSave, wrapMode = TextureWrapMode.Clamp,
                    filterMode = FilterMode.Bilinear, anisoLevel = 1 };
            texture.SetPixels32(pixels); texture.Apply(true, true); return texture;
        }

        static uint StableHash(string value)
        {
            unchecked { uint hash = 2166136261; foreach (char c in value) { hash ^= c; hash *= 16777619; } return hash; }
        }

        void ClearMeshes()
        {
            foreach (var node in batches) if (node != null)
            {
                node.SetActive(false);
                var filter = node.GetComponent<MeshFilter>(); if (filter != null) filter.sharedMesh = null;
                Release(node);
            }
            foreach (var mesh in meshes) { Release(mesh); OwnedMeshCount--; }
            meshes.Clear(); batches.Clear();
        }
        void ReleaseAppearance()
        {
            if (material != null) { Release(material); material = null; OwnedMaterialCount--; }
            if (paper != null) { Release(paper); paper = null; OwnedTextureCount--; }
        }
        public void DisposeOwnedResources()
        {
            ClearMeshes(); ReleaseAppearance(); signature = null;
            memoIds = Array.Empty<string>(); memoBounds = Array.Empty<Bounds>(); LocalBounds = default;
            memoToLocal = Array.Empty<Matrix4x4>(); memoHalfSizes = Array.Empty<Vector3>();
        }
        void OnDestroy() => DisposeOwnedResources();
        static void Release(UnityEngine.Object value)
        {
            if (value == null) return;
            if (Application.isPlaying) Destroy(value); else DestroyImmediate(value);
        }
    }

}
