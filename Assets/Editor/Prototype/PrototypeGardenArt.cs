using System.Collections.Generic;
using System.IO;
using CapstoneDesign.Prototype;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace CapstoneDesign.EditorTools
{
    // Original, closed 3D geometry. All stages are inspectable from below as well as above.
    // Meshes are batched by material at authoring time; no runtime mesh allocation.
    public static class PrototypeGardenArt
    {
        // Full-island framing with margin on every side, including top/underside orbits.
        public const float DefaultOrthographicSize = 1.7f;
        private const string MeshPath = "Assets/Art/Prototype/RefinedMeshes";
        private const string MaterialPath = "Assets/Materials/Prototype";
        private static Material stem, leaf, leafLight, leafDark, ivory, cream, gold, seed, seedLight;
        private static Material[] blue;

        public static PrototypePlantView Build(out RenderTexture preview, out Camera camera)
        {
            Directory.CreateDirectory(MeshPath);
            Directory.CreateDirectory(MaterialPath);
            stem = Mat("Stem", "416447"); leaf = Mat("Leaf", "669453");
            leafLight = Mat("LeafLight", "9ABD68"); leafDark = Mat("LeafDark", "395F48");
            ivory = Mat("Ivory", "FFF6DF"); cream = Mat("PetalShade", "E8DEBC"); gold = Mat("Pollen", "E5B246");
            seed = Mat("Seed", "885734"); seedLight = Mat("SeedLight", "C78D52");
            blue = new[] { Mat("Lilac", "9A9AD5"), Mat("Blue", "849ECD"), Mat("Periwinkle", "B7B6E7"), Mat("LavenderLight", "D3C7EB") };

            var garden = new GameObject("Garden_Editable3D");
            BuildIsland(garden.transform);
            var plant = new GameObject("Plant_GrowthParts").AddComponent<PrototypePlantView>();
            plant.transform.SetParent(garden.transform, false);
            plant.seed = Root("01_Seed", plant.transform);
            plant.sprout = Root("02_Sprout", plant.transform);
            plant.bud = Root("03_Buds", plant.transform).transform;
            plant.blossomAnchor = Root("04_Bloom", plant.transform).transform;
            plant.chamomileFlower = Root("Chamomile", plant.blossomAnchor);
            plant.hydrangeaFlower = Root("Hydrangea", plant.blossomAnchor);
            BuildSeed(new Batch(plant.seed.transform));
            BuildSprout(new Batch(plant.sprout.transform));
            BuildBuds(new Batch(plant.bud));
            BuildChamomile(new Batch(plant.chamomileFlower.transform));
            BuildHydrangea(new Batch(plant.hydrangeaFlower.transform));
            foreach (Transform t in garden.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = 30;

            LightAt("Garden Soft Light", new Vector3(42, -38, 0), "FFF0D5", 1.05f);
            LightAt("Garden Fill Light", new Vector3(24, 135, 0), "D8E6FF", .42f);
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(.57f, .62f, .58f);
            const string texturePath = "Assets/Settings/Prototype/GardenPreview.renderTexture";
            preview = AssetDatabase.LoadAssetAtPath<RenderTexture>(texturePath);
            if (preview == null)
            {
                preview = new RenderTexture(1000, 900, 24) { name = "GardenPreview" };
                AssetDatabase.CreateAsset(preview, texturePath);
            }
            preview.Release(); preview.width = 1000; preview.height = 900; preview.antiAliasing = 4;
            EditorUtility.SetDirty(preview);
            camera = new GameObject("Garden Camera").AddComponent<Camera>();
            camera.orthographic = true; camera.orthographicSize = DefaultOrthographicSize;
            camera.nearClipPlane = .1f; camera.farClipPlane = 20;
            camera.backgroundColor = Color.clear; camera.clearFlags = CameraClearFlags.SolidColor;
            camera.cullingMask = 1 << 30; camera.targetTexture = preview; camera.allowHDR = false;
            Quaternion rotation = Quaternion.Euler(28, -25, 0);
            camera.transform.SetPositionAndRotation(new Vector3(0, .58f, 0) + rotation * (Vector3.back * PrototypeIslandDrag.DefaultDistance), rotation);
            return plant;
        }

        private static void BuildIsland(Transform parent)
        {
            var b = new Batch(Root("TerracedIsland", parent).transform);
            var grass = new[] { Mat("Meadow", "A6BC74"), Mat("MeadowLight", "B7CA85"), Mat("MeadowShade", "96B169") };
            var earth = new[] { Mat("Earth", "AD8158"), Mat("EarthShade", "896441"), Mat("EarthLight", "C29B6D") };
            var rock = Mat("RiverStone", "D6CBAA");
            var soil = Mat("PlantingSoil", "806549");
            const int sides = 14;
            Vector3 Ring(int i, int ring)
            {
                float a = i * Mathf.PI * 2 / sides;
                float irregular = 1 + .045f * Mathf.Sin(i * 2.7f);
                float radius = new[] { 1.38f, 1.46f, 1.41f, 1.17f, .89f }[ring] * irregular;
                float y = new[] { .035f, -.035f, -.19f, -.46f, -.57f }[ring];
                return new Vector3(Mathf.Cos(a) * radius, y, Mathf.Sin(a) * radius * .91f);
            }
            for (int i = 0; i < sides; i++)
            {
                int j = (i + 1) % sides;
                b.Triangle(new Vector3(0, .065f, 0), Ring(j, 0), Ring(i, 0), grass[i % 3]);
                for (int r = 0; r < 4; r++)
                {
                    var material = r == 0 ? grass[(i + 1) % 3] : earth[(i + r) % 3];
                    b.Triangle(Ring(i, r), Ring(j, r), Ring(i, r + 1), material);
                    b.Triangle(Ring(j, r), Ring(j, r + 1), Ring(i, r + 1), material);
                }
                b.Triangle(new Vector3(0, -.62f, 0), Ring(i, 4), Ring(j, 4), earth[1]);
            }
            b.Orb(new Vector3(0, .063f, 0), new Vector3(.66f, .065f, .56f), soil, Quaternion.identity, 12, 3);
            // Three asymmetrical groups give rotation readable landmarks, without crowding the plant.
            for (int group = 0; group < 3; group++)
            {
                float a = group * 2.15f + .35f;
                Vector3 at = new Vector3(Mathf.Cos(a) * 1.02f, .1f, Mathf.Sin(a) * .87f);
                for (int i = 0; i < 3; i++)
                {
                    Vector3 p = at + new Vector3((i - 1) * .13f, i == 1 ? .015f : 0, i * .08f);
                    b.Orb(p, new Vector3(.20f - i * .035f, .13f, .17f), rock, Quaternion.Euler(i * 13, group * 63 + i * 32, 7), 7, 3);
                }
                Vector3 tuft = at + new Vector3(-.21f, -.04f, .08f);
                for (int i = 0; i < 5; i++)
                    b.Blade(tuft, new Vector3(Mathf.Cos(i * 1.7f) * .14f, .19f + i * .017f, Mathf.Sin(i * 1.7f) * .13f), .075f, i % 2 == 0 ? leaf : leafLight);
            }
            // Tiny clover patches around the rim, authored as a handful of closed leaves.
            for (int i = 0; i < 9; i++)
            {
                float a = i * 2.399f;
                Vector3 p = new Vector3(Mathf.Cos(a) * (.7f + i % 3 * .13f), .067f, Mathf.Sin(a) * .88f);
                b.Blade(p, new Vector3(Mathf.Sin(a) * .12f, .025f, Mathf.Cos(a) * .12f), .12f, grass[i % 3]);
            }
            b.Save();
        }

        private static void BuildSeed(Batch b)
        {
            Quaternion tilt = Quaternion.Euler(12, -30, 25);
            b.Orb(new Vector3(0, .20f, 0), new Vector3(.39f, .28f, .52f), seed, tilt, 10, 5);
            b.Orb(new Vector3(-.018f, .29f, -.018f), new Vector3(.26f, .065f, .39f), seedLight, tilt, 10, 3);
            b.Tube(new[] { new Vector3(-.05f, .295f, -.18f), new Vector3(.014f, .345f, 0), new Vector3(.045f, .30f, .17f) }, .009f, .004f, cream);
            b.Save();
        }

        private static void BuildSprout(Batch b)
        {
            b.Tube(new[] { new Vector3(0, .08f, 0), new Vector3(-.035f, .28f, 0), new Vector3(.025f, .52f, .025f), new Vector3(0, .67f, 0) }, .035f, .02f, stem);
            b.Blade(new Vector3(0, .52f, 0), new Vector3(-.52f, .23f, -.13f), .35f, leaf);
            b.Blade(new Vector3(.01f, .56f, .015f), new Vector3(.50f, .25f, .08f), .38f, leafLight);
            b.Blade(new Vector3(0, .66f, 0), new Vector3(.03f, .22f, .13f), .13f, leafLight);
            b.Orb(new Vector3(-.045f, .10f, .035f), new Vector3(.20f, .09f, .24f), seed, Quaternion.Euler(10, 30, 20));
            b.Save();
        }

        private static void BuildBuds(Batch b)
        {
            Vector3[] tips = { new Vector3(.02f, 1.27f, 0), new Vector3(-.37f, .93f, .13f), new Vector3(.35f, .83f, -.10f) };
            for (int i = 0; i < tips.Length; i++)
            {
                Vector3 tip = tips[i];
                StemTo(b, tip, .032f);
                b.Orb(tip + Vector3.up * .10f, new Vector3(.24f, .35f, .24f) * (i == 0 ? 1 : .8f), ivory, Quaternion.Euler(0, i * 63, i * 8), 8, 4);
                for (int k = 0; k < 5; k++)
                {
                    float a = k * Mathf.PI * .4f;
                    b.Blade(tip - Vector3.up * .035f, new Vector3(Mathf.Cos(a) * .12f, .22f, Mathf.Sin(a) * .12f), .11f, k % 2 == 0 ? leaf : leafLight);
                }
            }
            Foliage(b, 7, .84f, false);
            b.Save();
        }

        private static void BuildChamomile(Batch b)
        {
            Vector3[] tips = { new Vector3(.06f, 1.53f, .03f), new Vector3(-.45f, 1.17f, .12f),
                new Vector3(.43f, 1.04f, -.13f), new Vector3(-.18f, 1.24f, .39f), new Vector3(.16f, .85f, -.40f) };
            for (int i = 0; i < tips.Length; i++)
            {
                StemTo(b, tips[i], .026f);
                Quaternion facing = Quaternion.FromToRotation(Vector3.up, new Vector3(tips[i].x * .4f, 1, tips[i].z * .4f).normalized);
                float radius = i == 0 ? .39f : .28f + i % 2 * .035f;
                for (int p = 0; p < 12; p++)
                {
                    float a = (p * 30 + i * 17) * Mathf.Deg2Rad;
                    Vector3 dir = new Vector3(Mathf.Cos(a), -.08f, Mathf.Sin(a));
                    b.Blade(tips[i] + facing * (dir * .075f), facing * (dir * radius), .155f * radius / .39f,
                        p % 4 == 0 ? cream : ivory, true, facing);
                }
                b.Orb(tips[i] + facing * Vector3.up * .045f, new Vector3(radius * .68f, .12f, radius * .68f), gold, facing, 10, 4);
                for (int p = 0; p < 7; p++)
                {
                    float a = p * 2.399f;
                    b.Orb(tips[i] + facing * new Vector3(Mathf.Cos(a) * radius * .19f, .102f, Mathf.Sin(a) * radius * .19f),
                        new Vector3(.032f, .027f, .032f), p % 2 == 0 ? seedLight : gold, facing, 5, 2);
                }
            }
            Foliage(b, 10, 1.05f, true);
            b.Save();
        }

        private static void BuildHydrangea(Batch b)
        {
            Vector3[] centers = { new Vector3(.01f, 1.30f, .04f), new Vector3(-.40f, .98f, .09f), new Vector3(.38f, .94f, -.16f) };
            for (int cluster = 0; cluster < centers.Length; cluster++)
            {
                Vector3 center = centers[cluster]; float radius = cluster == 0 ? .44f : .31f;
                StemTo(b, center, .041f);
                int count = cluster == 0 ? 27 : 17;
                // Fibonacci distribution wraps the complete rounded head, not just its camera-facing side.
                for (int i = 0; i < count; i++)
                {
                    float y = 1 - 1.7f * (i + .5f) / count;
                    float a = i * 2.399963f + cluster;
                    float r = Mathf.Sqrt(1 - y * y);
                    Vector3 normal = new Vector3(Mathf.Cos(a) * r, y, Mathf.Sin(a) * r);
                    Quaternion facing = Quaternion.FromToRotation(Vector3.up, normal);
                    Vector3 at = center + normal * radius;
                    float length = cluster == 0 ? .154f : .124f;
                    for (int p = 0; p < 4; p++)
                    {
                        float pa = (p * 90 + i * 23) * Mathf.Deg2Rad;
                        Vector3 dir = new Vector3(Mathf.Cos(pa), .05f, Mathf.Sin(pa));
                        b.Blade(at, facing * (dir * length), length * .96f, blue[(i + p / 2 + cluster) % blue.Length], true, facing);
                    }
                    b.Orb(at + normal * .018f, Vector3.one * .043f, ivory, facing, 5, 2);
                }
            }
            Foliage(b, 11, 1.05f, false);
            b.Save();
        }

        private static void StemTo(Batch b, Vector3 tip, float width)
        {
            b.Tube(new[] { new Vector3(0, .08f, 0), new Vector3(tip.x * .20f, tip.y * .39f, tip.z * .25f),
                new Vector3(tip.x * .8f, tip.y * .73f, tip.z * .8f), tip }, width, width * .48f, stem);
        }
        private static void Foliage(Batch b, int count, float height, bool feathered)
        {
            for (int i = 0; i < count; i++)
            {
                float a = i * 2.399f;
                Vector3 at = new Vector3(Mathf.Cos(a) * .055f, .20f + (i % 4) * height * .17f, Mathf.Sin(a) * .055f);
                Vector3 dir = new Vector3(Mathf.Cos(a) * (.52f - i % 3 * .05f), .14f + i % 2 * .07f, Mathf.Sin(a) * .52f);
                Material m = i % 3 == 0 ? leafLight : i % 3 == 1 ? leaf : leafDark;
                b.Blade(at, dir, feathered ? .14f : .38f, m);
                if (!feathered) continue;
                Vector3 side = Vector3.Cross(dir.normalized, Vector3.up);
                for (int k = 1; k <= 3; k++)
                    for (int sign = -1; sign <= 1; sign += 2)
                        b.Blade(at + dir * (k * .19f), dir * .22f + side * (sign * (.17f - k * .018f)), .08f, m);
            }
        }

        private static GameObject Root(string name, Transform parent)
        { var go = new GameObject(name); go.transform.SetParent(parent, false); return go; }
        private static void LightAt(string name, Vector3 angles, string color, float intensity)
        {
            var light = new GameObject(name).AddComponent<Light>();
            light.type = LightType.Directional; light.transform.rotation = Quaternion.Euler(angles);
            light.color = Hex(color); light.intensity = intensity; light.shadows = LightShadows.None; light.cullingMask = 1 << 30;
        }
        private static Color Hex(string hex) { ColorUtility.TryParseHtmlString("#" + hex, out Color c); return c; }
        private static Material Mat(string name, string hex)
        {
            string path = MaterialPath + "/Refined_" + name + ".mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat != null) return mat;
            mat = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = "Refined_" + name };
            mat.SetColor("_BaseColor", Hex(hex)); mat.SetFloat("_Smoothness", .08f);
            AssetDatabase.CreateAsset(mat, path);
            return mat;
        }

        // Flat faces retain their own vertices/normals. Batch by palette, not individual petal.
        private sealed class Batch
        {
            private readonly Transform root;
            private readonly Dictionary<Material, List<Vector3>> geometry = new Dictionary<Material, List<Vector3>>();
            public Batch(Transform root) { this.root = root; }
            public void Triangle(Vector3 a, Vector3 b, Vector3 c, Material material)
            {
                if (Vector3.Cross(b - a, c - a).sqrMagnitude < 1e-12f) return;
                if (!geometry.TryGetValue(material, out var vertices)) geometry[material] = vertices = new List<Vector3>();
                vertices.Add(a); vertices.Add(b); vertices.Add(c);
            }
            public void Orb(Vector3 center, Vector3 size, Material material, Quaternion rotation, int sides = 8, int rings = 3)
            {
                Vector3 P(int j, int i)
                {
                    float latitude = -Mathf.PI * .5f + j * Mathf.PI / rings, longitude = i * Mathf.PI * 2 / sides;
                    return center + rotation * Vector3.Scale(size * .5f, new Vector3(Mathf.Cos(latitude) * Mathf.Cos(longitude), Mathf.Sin(latitude), Mathf.Cos(latitude) * Mathf.Sin(longitude)));
                }
                for (int j = 0; j < rings; j++) for (int i = 0; i < sides; i++)
                {
                    Triangle(P(j, i), P(j + 1, i), P(j, i + 1), material);
                    Triangle(P(j, i + 1), P(j + 1, i), P(j + 1, i + 1), material);
                }
            }
            public void Blade(Vector3 at, Vector3 direction, float width, Material material, bool petal = false, Quaternion? face = null)
            {
                Vector3 up = face.HasValue ? face.Value * Vector3.up : Vector3.up;
                Quaternion q = Quaternion.LookRotation(direction.normalized, up);
                float length = direction.magnitude;
                Vector2[] outline = petal
                    ? new[] { new Vector2(0, 0), new Vector2(-.25f, .16f), new Vector2(-.48f, .50f), new Vector2(-.39f, .84f), new Vector2(0, 1), new Vector2(.39f, .84f), new Vector2(.48f, .50f), new Vector2(.25f, .16f) }
                    : new[] { new Vector2(0, 0), new Vector2(-.25f, .22f), new Vector2(-.50f, .45f), new Vector2(-.32f, .73f), new Vector2(0, 1), new Vector2(.32f, .73f), new Vector2(.50f, .45f), new Vector2(.25f, .22f) };
                Vector3 P(int i) => at + q * new Vector3(outline[i].x * width, Mathf.Sin(outline[i].y * Mathf.PI) * (petal ? -.03f : .02f), outline[i].y * length);
                Vector3 top = at + q * new Vector3(0, petal ? .025f : .06f, length * .49f);
                Vector3 bottom = at + q * new Vector3(0, -.035f, length * .49f);
                for (int i = 0; i < outline.Length; i++)
                { int next = (i + 1) % outline.Length; Triangle(top, P(i), P(next), material); Triangle(bottom, P(next), P(i), material); }
            }
            public void Tube(Vector3[] path, float startRadius, float endRadius, Material material)
            {
                for (int s = 0; s < path.Length - 1; s++)
                {
                    Quaternion q = Quaternion.FromToRotation(Vector3.up, (path[s + 1] - path[s]).normalized);
                    float ra = Mathf.Lerp(startRadius, endRadius, (float)s / (path.Length - 1));
                    float rb = Mathf.Lerp(startRadius, endRadius, (float)(s + 1) / (path.Length - 1));
                    Vector3 P(int i, int end) { float a = i * Mathf.PI / 3; return path[s + end] + q * new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * (end == 0 ? ra : rb); }
                    for (int i = 0; i < 6; i++)
                    {
                        Triangle(P(i, 0), P(i, 1), P(i + 1, 0), material);
                        Triangle(P(i + 1, 0), P(i, 1), P(i + 1, 1), material);
                        if (s == 0) Triangle(path[s], P(i, 0), P(i + 1, 0), material);
                        if (s == path.Length - 2) Triangle(path[s + 1], P(i + 1, 1), P(i, 1), material);
                    }
                }
            }
            public void Save()
            {
                foreach (var pair in geometry)
                {
                    string name = root.name + "_" + pair.Key.name;
                    var mesh = new Mesh { name = name };
                    mesh.SetVertices(pair.Value);
                    var indices = new int[pair.Value.Count]; for (int i = 0; i < indices.Length; i++) indices[i] = i;
                    mesh.SetTriangles(indices, 0); mesh.RecalculateNormals(); mesh.RecalculateBounds();
                    string path = MeshPath + "/" + name + ".asset";
                    var saved = AssetDatabase.LoadAssetAtPath<Mesh>(path);
                    if (saved == null) { AssetDatabase.CreateAsset(mesh, path); saved = mesh; }
                    else { EditorUtility.CopySerialized(mesh, saved); Object.DestroyImmediate(mesh); EditorUtility.SetDirty(saved); }
                    var go = Root(pair.Key.name, root);
                    go.AddComponent<MeshFilter>().sharedMesh = saved;
                    var renderer = go.AddComponent<MeshRenderer>(); renderer.sharedMaterial = pair.Key;
                    renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = false;
                }
            }
        }
    }
}
