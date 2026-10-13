using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace CapstoneDesign.EditorTools
{
    // Derive from the imported Kenney lantern, keeping its authored topology.
    // Actual illumination belongs to GardenStreetLighting, not this asset.
    public static class StreetLampAssetBuilder
    {
        const string Source = "Assets/Art/External/KenneyStreetLamp/lantern.obj";
        const string Derived = "Assets/Art/StreetLampDerived/";
        const string Materials = "Assets/Materials/GardenLighting/";
        const string Prefab = "Assets/Resources/GardenModels/StreetLamp.prefab";
        const float Height = 1.10f;

        sealed class Part
        {
            public readonly List<Vector3> Vertices = new List<Vector3>();
            public readonly List<Vector3> Normals = new List<Vector3>();
            public readonly List<Vector2> Uvs = new List<Vector2>();
            public readonly List<int> Triangles = new List<int>();
        }

        public static void Build()
        {
            Directory.CreateDirectory(Derived);
            Directory.CreateDirectory(Materials);
            Directory.CreateDirectory(Path.GetDirectoryName(Prefab));
            AssetDatabase.Refresh();
            var importer = AssetImporter.GetAtPath(Source) as ModelImporter;
            if (importer == null) throw new InvalidOperationException("Kenney streetlamp source is missing.");
            importer.isReadable = true;
            importer.importNormals = ModelImporterNormals.Import;
            importer.materialImportMode = ModelImporterMaterialImportMode.None;
            importer.importAnimation = false;
            importer.SaveAndReimport();

            var source = AssetDatabase.LoadAssetAtPath<GameObject>(Source);
            var frame = new Part();
            var glass = new Part();
            bool hasBounds = false;
            var bounds = new Bounds();
            foreach (var filter in source.GetComponentsInChildren<MeshFilter>(true))
            {
                var mesh = filter.sharedMesh;
                var vertices = mesh.vertices;
                var normals = mesh.normals;
                var uv = mesh.uv;
                var toRoot = source.transform.worldToLocalMatrix * filter.transform.localToWorldMatrix;
                var normalMatrix = toRoot.inverse.transpose;
                for (int sub = 0; sub < mesh.subMeshCount; sub++)
                {
                    var triangles = mesh.GetTriangles(sub);
                    for (int t = 0; t < triangles.Length; t += 3)
                    {
                        // Kenney's original palette has charcoal at U=.71875
                        // and the separate yellow glass strip at U=.84375.
                        bool isGlass = uv[triangles[t]].x > .80f &&
                            uv[triangles[t + 1]].x > .80f && uv[triangles[t + 2]].x > .80f;
                        var part = isGlass ? glass : frame;
                        for (int corner = 0; corner < 3; corner++)
                        {
                            int index = triangles[t + corner];
                            var vertex = toRoot.MultiplyPoint3x4(vertices[index]);
                            if (!hasBounds) { bounds = new Bounds(vertex, Vector3.zero); hasBounds = true; }
                            else bounds.Encapsulate(vertex);
                            part.Triangles.Add(part.Vertices.Count);
                            part.Vertices.Add(vertex);
                            part.Normals.Add(normalMatrix.MultiplyVector(normals[index]).normalized);
                            part.Uvs.Add(uv[index]);
                        }
                    }
                }
            }
            if (!hasBounds || bounds.size.y <= 0 || frame.Triangles.Count != 320 * 3 ||
                glass.Triangles.Count != 24 * 3)
                throw new InvalidOperationException("Unexpected lantern geometry; verify the source before rebuilding.");

            float scale = Height / bounds.size.y;
            var origin = new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
            var frameMesh = SaveMesh(frame, origin, scale, "StreetLamp_Frame");
            var glassMesh = SaveMesh(glass, origin, scale, "StreetLamp_Glass");
            var root = new GameObject("StreetLamp") { layer = 30 };
            try
            {
                AddPart(root.transform, frameMesh, Material("StreetLamp_Frame", new Color(.22f, .25f, .28f), false), true);
                AddPart(root.transform, glassMesh, Material("StreetLamp_Glass", new Color(.91f, .76f, .54f), true), false);
                var bulb = new GameObject("Bulb") { layer = 30 };
                bulb.transform.SetParent(root.transform, false);
                bulb.transform.localPosition = glassMesh.bounds.center;
                PrefabUtility.SaveAsPrefabAsset(root, Prefab);
                AssetDatabase.SaveAssets();
                Debug.Log($"STREETLAMP_ASSET_BUILD_PASS triangles=344 glassTriangles=24 height={Height:F2} bulb={bulb.transform.localPosition}");
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        static Mesh SaveMesh(Part part, Vector3 origin, float scale, string name)
        {
            for (int i = 0; i < part.Vertices.Count; i++) part.Vertices[i] = (part.Vertices[i] - origin) * scale;
            var mesh = new Mesh { name = name };
            mesh.SetVertices(part.Vertices);
            mesh.SetNormals(part.Normals);
            mesh.SetUVs(0, part.Uvs);
            mesh.SetTriangles(part.Triangles, 0);
            mesh.RecalculateBounds();
            string path = Derived + name + ".asset";
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing == null) { AssetDatabase.CreateAsset(mesh, path); return mesh; }
            EditorUtility.CopySerialized(mesh, existing);
            UnityEngine.Object.DestroyImmediate(mesh);
            EditorUtility.SetDirty(existing);
            return existing;
        }

        static Material Material(string name, Color color, bool emissive)
        {
            string path = Materials + name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                var shader = Shader.Find("Universal Render Pipeline/Lit");
                if (shader == null) throw new InvalidOperationException("URP Lit shader is missing.");
                material = new Material(shader) { name = name };
                AssetDatabase.CreateAsset(material, path);
            }
            material.SetColor("_BaseColor", color);
            material.SetFloat("_Metallic", emissive ? 0 : .25f);
            material.SetFloat("_Smoothness", emissive ? .22f : .16f);
            material.SetFloat("_Cull", (float)CullMode.Back);
            // URP revalidates emission from the material's GI flags on import.
            // A black color / None flags silently strips _EMISSION, so the
            // runtime property block would never make the glass glow. Keep the
            // emissive variant in the asset; the clock sets the actual color,
            // including black during daytime, before rendering the island.
            material.SetColor("_EmissionColor", emissive ? new Color(4.5f, 2.745f, 1.26f, 1f) : Color.black);
            if (emissive) material.EnableKeyword("_EMISSION");
            else material.DisableKeyword("_EMISSION");
            material.globalIlluminationFlags = emissive ? MaterialGlobalIlluminationFlags.BakedEmissive : MaterialGlobalIlluminationFlags.None;
            EditorUtility.SetDirty(material);
            return material;
        }

        static void AddPart(Transform parent, Mesh mesh, Material material, bool castShadows)
        {
            var part = new GameObject(mesh.name) { layer = 30 };
            part.transform.SetParent(parent, false);
            part.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = part.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = castShadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
            renderer.receiveShadows = true;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
        }
    }
}
