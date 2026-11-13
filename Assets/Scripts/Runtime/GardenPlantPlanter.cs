using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace CapstoneDesign.Runtime
{
    // A small grounded terracotta bowl makes the generated plant's soil point
    // legible. This is presentation geometry, never a purchased/saved object.
    public sealed class GardenPlantPlanter : MonoBehaviour
    {
        public const float SoilHeight = .23f;
        Mesh ownedMesh;
        Material ownedMaterial;

        public static GardenPlantPlanter Create(Transform garden)
        {
            var root = new GameObject("Plant terracotta bowl");
            root.transform.SetParent(garden, false);
            var planter = root.AddComponent<GardenPlantPlanter>();
            planter.Build();
            GardenSceneModels.ConfigureRenderers(root);
            return planter;
        }

        void Build()
        {
            const int sides = 12;
            var vertices = new List<Vector3>();
            var colors = new List<Color>();
            var indices = new List<int>();
            var clay = new Color32(190, 134, 103, 255);
            var rim = new Color32(210, 161, 127, 255);
            var soil = new Color32(102, 83, 65, 255);
            var profile = new[] { new Vector2(.20f, 0), new Vector2(.25f, .20f),
                new Vector2(.28f, .20f), new Vector2(.28f, .27f),
                new Vector2(.23f, .27f), new Vector2(.23f, SoilHeight) };
            for (int i = 0; i < sides; i++)
            {
                float a = i * Mathf.PI * 2 / sides, b = (i + 1) * Mathf.PI * 2 / sides;
                for (int ring = 0; ring < profile.Length - 1; ring++)
                {
                    Color color = ring >= 2 ? rim : clay;
                    Vector3 p = Ring(profile[ring], a), q = Ring(profile[ring], b);
                    Vector3 r = Ring(profile[ring + 1], a), t = Ring(profile[ring + 1], b);
                    Triangle(p, r, q, color); Triangle(q, r, t, color);
                }
                Triangle(Vector3.up * SoilHeight, Ring(profile[5], b), Ring(profile[5], a), soil);
                Triangle(Vector3.zero, Ring(profile[0], a), Ring(profile[0], b), clay);
            }
            ownedMesh = new Mesh { name = "Faceted terracotta bowl", hideFlags = HideFlags.DontSave };
            ownedMesh.SetVertices(vertices); ownedMesh.SetColors(colors); ownedMesh.SetTriangles(indices, 0);
            ownedMesh.RecalculateNormals(); ownedMesh.RecalculateBounds();
            gameObject.AddComponent<MeshFilter>().sharedMesh = ownedMesh;
            ownedMaterial = new Material(Resources.Load<Shader>("ProceduralPlants/PlantVertexColor"))
                { name = "Terracotta bowl (owned)", hideFlags = HideFlags.DontSave };
            var renderer = gameObject.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = ownedMaterial; renderer.shadowCastingMode = ShadowCastingMode.On;
            renderer.receiveShadows = true;

            void Triangle(Vector3 a, Vector3 b, Vector3 c, Color color)
            {
                int first = vertices.Count;
                vertices.Add(a); vertices.Add(b); vertices.Add(c);
                // Vertex shader consumes linear albedo, unlike sRGB UI colors.
                Color linear = color.linear;
                colors.Add(linear); colors.Add(linear); colors.Add(linear);
                indices.Add(first); indices.Add(first + 1); indices.Add(first + 2);
            }
        }

        static Vector3 Ring(Vector2 profile, float angle)
            => new Vector3(Mathf.Cos(angle) * profile.x, profile.y, Mathf.Sin(angle) * profile.x);

        void OnDestroy()
        {
            Release(ownedMesh); Release(ownedMaterial);
        }
        static void Release(Object value)
        {
            if (value == null) return;
            if (Application.isPlaying) Destroy(value); else DestroyImmediate(value);
        }
    }
}
