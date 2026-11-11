using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CapstoneDesign.Runtime;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace CapstoneDesign.EditorTools
{
    // Explicit offline product photography. Only the imported garden prefabs are
    // photographed; the app loads the resulting images without another camera.
    public static class GardenShopProductBake
    {
        const int Width = 768, Height = 384, Layer = 31;
        [Serializable] sealed class Item
        {
            public string source, image;
            public int renderers, width = Width, height = Height;
            public float orthographicSize, cameraYaw, framingMargin;
        }
        [Serializable] sealed class Report
        {
            public string renderer, utc;
            public List<Item> items = new List<Item>();
        }

        public static void RunBatch()
        {
            string requested = Environment.GetEnvironmentVariable("CAPSTONE_SHOP_IMAGES");
            if (!Application.isBatchMode || Application.companyName != "CapstoneSyntheticValidation"
                || EditorApplication.isPlayingOrWillChangePlaymode || SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null
                || string.IsNullOrWhiteSpace(requested) || !Path.IsPathRooted(requested))
                throw new InvalidOperationException("An explicit synthetic graphics batch project and absolute CAPSTONE_SHOP_IMAGES are required.");
            string output = Path.GetFullPath(requested);
            if (!output.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                    .Any(part => part.Equals("artifacts", StringComparison.OrdinalIgnoreCase))
                || output.StartsWith(Path.GetFullPath(Application.dataPath), StringComparison.OrdinalIgnoreCase)
                || Directory.Exists(output) && Directory.EnumerateFileSystemEntries(output).Any())
                throw new InvalidOperationException("Product images require a fresh ignored artifacts directory outside Assets.");
            Directory.CreateDirectory(output);
            var report = new Report { renderer = SystemInfo.graphicsDeviceName, utc = DateTimeOffset.UtcNow.ToString("O") };
            using (var studio = new Studio())
            {
                report.items.Add(studio.Bake("FlowerBed", "flower-bed.png", output));
                report.items.Add(studio.Bake("RestCorner", "rest-corner.png", output));
            }
            File.WriteAllText(Path.Combine(output, "report.json"), JsonUtility.ToJson(report, true));
            Debug.Log("GARDEN_SHOP_PRODUCT_BAKE_PASS images=" + report.items.Count);
        }

        sealed class Studio : IDisposable
        {
            readonly Scene scene;
            readonly Camera camera;
            readonly RenderTexture target;
            readonly Material floorMaterial;
            readonly AmbientMode ambientMode = RenderSettings.ambientMode;
            readonly Color ambientSky = RenderSettings.ambientSkyColor;
            readonly Color ambientEquator = RenderSettings.ambientEquatorColor;
            readonly Color ambientGround = RenderSettings.ambientGroundColor;
            readonly Color ambientLight = RenderSettings.ambientLight;
            readonly float ambientIntensity = RenderSettings.ambientIntensity;
            readonly SphericalHarmonicsL2 ambientProbe = RenderSettings.ambientProbe;
            readonly bool fog = RenderSettings.fog;
            readonly Light sun = RenderSettings.sun;

            public Studio()
            {
                try
                {
                scene = EditorSceneManager.NewPreviewScene();
                var cameraObject = NewObject("Product photography camera");
                camera = cameraObject.AddComponent<Camera>();
                camera.enabled = false; camera.scene = scene;
                camera.orthographic = true; camera.aspect = Width / (float)Height;
                camera.nearClipPlane = .05f; camera.farClipPlane = 100;
                camera.cullingMask = 1 << Layer;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color32(239, 238, 224, 255);
                camera.allowHDR = false; camera.allowMSAA = true;
                var data = camera.GetUniversalAdditionalCameraData();
                data.renderPostProcessing = false; data.dithering = false;
                data.renderShadows = true;
                target = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 };
                target.Create(); camera.targetTexture = target;

                var key = CreateLight("Warm studio key", Quaternion.Euler(48, -35, 0), new Color(1f, .96f, .88f), 1.1f);
                key.shadows = LightShadows.Soft; key.shadowStrength = .42f;
                CreateLight("Soft studio fill", Quaternion.Euler(25, 145, 0), new Color(.89f, .94f, 1f), .45f);
                RenderSettings.sun = key; RenderSettings.fog = false;
                RenderSettings.ambientMode = AmbientMode.Flat;
                RenderSettings.ambientLight = new Color(.62f, .66f, .63f);
                RenderSettings.ambientIntensity = 1;
                var probe = new SphericalHarmonicsL2(); probe.AddAmbientLight(new Color(.62f, .66f, .63f));
                RenderSettings.ambientProbe = probe;

                var shader = Shader.Find("Universal Render Pipeline/Lit");
                if (shader == null) throw new InvalidOperationException("Product photography requires the project's URP Lit shader.");
                floorMaterial = new Material(shader) { name = "Temporary product photography floor", hideFlags = HideFlags.HideAndDontSave };
                floorMaterial.SetColor("_BaseColor", new Color32(231, 232, 211, 255));
                floorMaterial.SetFloat("_Smoothness", 0);
                }
                catch { Dispose(); throw; }
            }

            GameObject NewObject(string name)
            {
                var go = new GameObject(name) { hideFlags = HideFlags.HideAndDontSave, layer = Layer };
                SceneManager.MoveGameObjectToScene(go, scene);
                return go;
            }
            Light CreateLight(string name, Quaternion rotation, Color color, float intensity)
            {
                var go = NewObject(name); go.transform.rotation = rotation;
                var light = go.AddComponent<Light>(); light.type = LightType.Directional;
                light.color = color; light.intensity = intensity; light.cullingMask = 1 << Layer;
                return light;
            }

            public Item Bake(string resource, string filename, string output)
            {
                var prefab = Resources.Load<GameObject>(GardenSceneModels.ResourceRoot + resource);
                if (prefab == null) throw new InvalidOperationException("Missing actual garden product prefab: " + resource);
                var model = UnityEngine.Object.Instantiate(prefab);
                SceneManager.MoveGameObjectToScene(model, scene);
                var floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
                SceneManager.MoveGameObjectToScene(floor, scene);
                try
                {
                    foreach (var node in model.GetComponentsInChildren<Transform>(true)) node.gameObject.layer = Layer;
                    var renderers = model.GetComponentsInChildren<Renderer>(true);
                    if (renderers.Length == 0 || !GardenSceneModels.TryBounds(model.transform, null, out var bounds))
                        throw new InvalidOperationException("Product has no renderable geometry: " + resource);
                    foreach (var renderer in renderers)
                    { renderer.shadowCastingMode = ShadowCastingMode.On; renderer.receiveShadows = true; }
                    floor.layer = Layer;
                    floor.transform.position = new Vector3(bounds.center.x, bounds.min.y - .008f, bounds.center.z);
                    floor.transform.localScale = Vector3.one * Mathf.Max(4, bounds.size.magnitude * 2);
                    floor.GetComponent<Renderer>().sharedMaterial = floorMaterial;
                    UnityEngine.Object.DestroyImmediate(floor.GetComponent<Collider>());

                    // The rest corner's bench faces the opposite way to the
                    // flower display. Show its seat, not the back of the chair.
                    float yaw = resource == "RestCorner" ? 152 : -28;
                    float margin = resource == "RestCorner" ? 1.07f : 1.13f;
                    var rotation = Quaternion.Euler(28, yaw, 0);
                    Vector3 right = rotation * Vector3.right, up = rotation * Vector3.up;
                    float halfWidth = 0, halfHeight = 0;
                    for (int i = 0; i < 8; i++)
                    {
                        Vector3 offset = Vector3.Scale(bounds.extents, new Vector3((i & 1) == 0 ? -1 : 1,
                            (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                        halfWidth = Mathf.Max(halfWidth, Mathf.Abs(Vector3.Dot(right, offset)));
                        halfHeight = Mathf.Max(halfHeight, Mathf.Abs(Vector3.Dot(up, offset)));
                    }
                    camera.orthographicSize = Mathf.Max(halfHeight, halfWidth / camera.aspect) * margin;
                    camera.transform.SetPositionAndRotation(bounds.center - rotation * Vector3.forward * Mathf.Max(4, bounds.size.magnitude * 4), rotation);
                    camera.Render(); camera.Render();
                    var previous = RenderTexture.active;
                    var image = new Texture2D(Width, Height, TextureFormat.RGBA32, false);
                    try
                    {
                        RenderTexture.active = target;
                        image.ReadPixels(new Rect(0, 0, Width, Height), 0, 0); image.Apply();
                        File.WriteAllBytes(Path.Combine(output, filename), image.EncodeToPNG());
                    }
                    finally { RenderTexture.active = previous; UnityEngine.Object.DestroyImmediate(image); }
                    return new Item { source = GardenSceneModels.ResourceRoot + resource, image = filename,
                        renderers = renderers.Length, orthographicSize = camera.orthographicSize,
                        cameraYaw = yaw, framingMargin = margin };
                }
                finally { UnityEngine.Object.DestroyImmediate(model); UnityEngine.Object.DestroyImmediate(floor); }
            }
            public void Dispose()
            {
                if (camera != null) camera.targetTexture = null;
                if (target != null) { target.Release(); UnityEngine.Object.DestroyImmediate(target); }
                if (floorMaterial != null) UnityEngine.Object.DestroyImmediate(floorMaterial);
                if (scene.IsValid()) EditorSceneManager.ClosePreviewScene(scene);
                RenderSettings.ambientMode = ambientMode; RenderSettings.ambientSkyColor = ambientSky;
                RenderSettings.ambientEquatorColor = ambientEquator; RenderSettings.ambientGroundColor = ambientGround;
                RenderSettings.ambientLight = ambientLight;
                RenderSettings.ambientIntensity = ambientIntensity; RenderSettings.ambientProbe = ambientProbe;
                RenderSettings.fog = fog; RenderSettings.sun = sun;
            }
        }
    }
}
