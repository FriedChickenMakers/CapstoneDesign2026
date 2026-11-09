using System;
using System.IO;
using System.Security.Cryptography;
using UnityEngine;

namespace CapstoneDesign.EditorTools
{
    // Offline, deterministic material bake. No extra runtime textures, materials
    // or lighting passes are required by the Canvas renderer.
    public static class TimberLightingBake
    {
        public static void RunBatch()
        {
            string source = Environment.GetEnvironmentVariable("CAPSTONE_TIMBER_SOURCE");
            string atlasPath = Environment.GetEnvironmentVariable("CAPSTONE_TIMBER_BASE_ATLAS");
            string artifacts = Environment.GetEnvironmentVariable("CAPSTONE_ARTIFACTS");
            string output = Path.Combine(artifacts ?? "", "NavigationBackingUI-baked.png");
            if (!Application.isBatchMode || string.IsNullOrWhiteSpace(source)
                || string.IsNullOrWhiteSpace(atlasPath) || string.IsNullOrWhiteSpace(artifacts)
                || Path.GetFullPath(atlasPath) == Path.GetFullPath(output))
                throw new InvalidOperationException("Use explicit source maps, a preserved unlit atlas, and a fresh artifact directory.");
            string normalPath = Path.Combine(source, "Wood026_1K-JPG_NormalGL.jpg");
            string roughnessPath = Path.Combine(source, "Wood026_1K-JPG_Roughness.jpg");
            var atlas = Load(atlasPath);
            var normal = Load(normalPath);
            var roughness = Load(roughnessPath);
            try
            {
                if (atlas.width != 1024 || atlas.height != 1056 || normal.width != 1024
                    || normal.height != 1024 || roughness.width != 1024 || roughness.height != 1024)
                    throw new InvalidOperationException("Expected the original 1K material maps and 32px white atlas strip.");
                var light = new Vector3(-.48f, .68f, .56f).normalized;
                var half = (light + Vector3.forward).normalized;
                // Bake in linear light; keep the existing wood color grading.
                for (int y = 0; y < 1024; y++)
                for (int x = 0; x < 1024; x++)
                {
                    var sample = normal.GetPixel(x, y);
                    var n = new Vector3((sample.r * 2 - 1) * 1.35f,
                        (sample.g * 2 - 1) * 1.35f, sample.b * 2 - 1).normalized;
                    float diffuse = .43f + .95f * Mathf.Max(0, Vector3.Dot(n, light));
                    float gloss = (1 - roughness.GetPixel(x, y).r) * .18f
                        * Mathf.Pow(Mathf.Max(0, Vector3.Dot(n, half)), 18);
                    Color c = atlas.GetPixel(x, y).linear;
                    c = new Color(c.r * diffuse + gloss, c.g * diffuse + gloss * .92f,
                        c.b * diffuse + gloss * .76f, 1).gamma;
                    c.a = 1;
                    atlas.SetPixel(x, y, c);
                }
                // Deliberately leave the solid white decoration strip untouched.
                atlas.Apply(false);
                Directory.CreateDirectory(artifacts);
                File.WriteAllBytes(output, atlas.EncodeToPNG());
                File.WriteAllLines(Path.Combine(artifacts, "timber-bake.txt"), new[] {
                    "OFFLINE_TIMBER_LIGHTING_BAKE_PASS",
                    "CC0 ambientCG Wood026; preserved graded albedo + NormalGL + Roughness.",
                    "Upper-left light; linear diffuse plus roughness-weighted broad highlight; white strip preserved.",
                    "baseAtlasSha256=" + Hash(atlasPath), "normalSha256=" + Hash(normalPath),
                    "roughnessSha256=" + Hash(roughnessPath), "outputSha256=" + Hash(output)
                });
                Debug.Log("OFFLINE_TIMBER_LIGHTING_BAKE_PASS");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(atlas);
                UnityEngine.Object.DestroyImmediate(normal);
                UnityEngine.Object.DestroyImmediate(roughness);
            }
        }

        static Texture2D Load(string path)
        {
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            if (!texture.LoadImage(File.ReadAllBytes(path)))
                throw new InvalidOperationException("Material input could not be decoded.");
            return texture;
        }
        static string Hash(string path)
        {
            using var sha = SHA256.Create();
            return BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(path))).Replace("-", "").ToLowerInvariant();
        }
    }
}
