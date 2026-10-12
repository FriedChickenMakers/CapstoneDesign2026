using System;
using System.Collections.Generic;
using System.IO;
using CapstoneDesign.Prototype;
using CapstoneDesign.Runtime;
using CapstoneDesign.Runtime.LocalState;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace CapstoneDesign.EditorTools
{
    // Synthetic pixel regression: sky discs must survive non-square render targets.
    public static class GardenSkyProjectionCapture
    {
        sealed class Clock : IClock
        {
            public DateTimeOffset UtcNow => DateTimeOffset.Parse("2026-10-12T12:00:00+09:00");
            public TimeZoneInfo TimeZone => TimeZoneInfo.Utc;
        }
        sealed class Store : IStateStore
        {
            GardenState state;
            public GardenState Load() => state == null ? null : StateCodec.Clone(state);
            public void Save(GardenState value) => state = StateCodec.Clone(value);
        }

        public static void CaptureWithOrbit()
        {
            string output = Environment.GetEnvironmentVariable("CAPSTONE_ARTIFACTS");
            try
            {
                Capture();
                Environment.SetEnvironmentVariable("CAPSTONE_ARTIFACTS", Path.Combine(output, "orbit"));
                GardenAtmosphereOrbitCapture.Capture();
            }
            finally { Environment.SetEnvironmentVariable("CAPSTONE_ARTIFACTS", output); }
        }

        public static void Capture()
        {
            string output = Environment.GetEnvironmentVariable("CAPSTONE_ARTIFACTS");
            if (string.IsNullOrEmpty(output)) throw new Exception("Explicit synthetic output required");
            Directory.CreateDirectory(output);
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var root = new GameObject("Synthetic sky root");
            var camera = new GameObject("Synthetic camera").AddComponent<Camera>();
            camera.enabled = false;
            camera.cullingMask = 0;
            camera.nearClipPlane = .1f;
            camera.farClipPlane = 100;
            camera.fieldOfView = 60;
            camera.orthographicSize = 5;
            var sky = GardenSkyEnvironment.Ensure(camera, root.transform);
            var results = new List<string>();
            foreach (var size in new[] { new Vector2Int(1200, 2000), new Vector2Int(2000, 1200), new Vector2Int(900, 900) })
            foreach (var body in new[] { ("sun", 9d), ("moon", 23d) })
            foreach (bool custom in new[] { false, true })
            {
                sky.SetTimeForPreview(body.Item2);
                camera.transform.rotation = Quaternion.LookRotation(body.Item1 == "sun" ? sky.SunDirection : sky.MoonDirection);
                camera.orthographic = true;
                camera.aspect = (float)size.x / size.y;
                camera.ResetProjectionMatrix();
                if (custom)
                    camera.projectionMatrix = Matrix4x4.Ortho(-5 * camera.aspect, 5 * camera.aspect,
                        -4, 6, camera.nearClipPlane, camera.farClipPlane);
                string name = body.Item1 + "-" + size.x + "x" + size.y + (custom ? "-off-axis" : "-standard");
                var pixels = Render(camera, size, Path.Combine(output, name + ".png"));
                var bounds = DiscBounds(pixels, size.x, body.Item1 == "moon");
                float ratio = (float)bounds.width / bounds.height;
                results.Add(name + " disc=" + bounds + " ratio=" + ratio + " cameraAspect=" + camera.aspect +
                    " projectionAspect=" + camera.projectionMatrix.m11 / camera.projectionMatrix.m00);
                if (bounds.width < 8 || bounds.height < 8 || Mathf.Abs(ratio - 1) > .08f)
                    results.Add("SHAPE_FAIL " + name);
            }
            foreach (var display in new[] { new Vector2Int(900, 350), new Vector2Int(350, 900) })
            foreach (var body in new[] { ("sun", 9d), ("moon", 23d) })
            {
                sky.SetTimeForPreview(body.Item2);
                camera.transform.rotation = Quaternion.LookRotation(body.Item1 == "sun" ? sky.SunDirection : sky.MoonDirection);
                camera.aspect = (float)display.x / display.y;
                camera.ResetProjectionMatrix();
                string name = "card-" + body.Item1 + "-" + display.x + "x" + display.y;
                var pixels = Render(camera, new Vector2Int(640, 480), Path.Combine(output, name + ".png"), display);
                var bounds = DiscBounds(pixels, display.x, body.Item1 == "moon");
                float ratio = (float)bounds.width / bounds.height;
                results.Add(name + " sharedRT=640x480 disc=" + bounds + " ratio=" + ratio + " cameraAspect=" + camera.aspect);
                if (bounds.width < 8 || bounds.height < 8 || Mathf.Abs(bounds.width - bounds.height) > 2)
                    results.Add("SHAPE_FAIL " + name);
            }
            CaptureHome(output, results);
            File.WriteAllLines(Path.Combine(output, "shape-validation.txt"), results);
            if (results.Exists(r => r.StartsWith("SHAPE_FAIL", StringComparison.Ordinal)))
                throw new Exception("GARDEN_SKY_SHAPE_FAIL: see shape-validation.txt");
            Debug.Log("GARDEN_SKY_SHAPE_PASS");
        }

        static void CaptureHome(string output, List<string> results)
        {
            EditorSceneManager.OpenScene("Assets/Scenes/MockupMain.unity");
            AndroidPlatformBridge.UseMockProvider();
            var home = UnityEngine.Object.FindFirstObjectByType<GardenHomePresenter>(FindObjectsInactive.Include);
            using var service = new GardenStateService(new Store(), new Clock(),
                new DemoBalanceConfig { VisitorChancePercent = 0 }, new LegacySnapshot { Nutrient = 100 });
            home.activity.Initialize(service, true);
            home.navigation.ShowIsland(); home.RefreshFromState(service.Snapshot);
            var camera = home.gardenCamera;
            var sky = GardenSkyEnvironment.Ensure(camera, home.gardenVisuals.transform);
            foreach (var size in new[] { new Vector2Int(1200, 2000), new Vector2Int(2000, 1200) })
            foreach (var body in new[] { ("sun", 9d), ("moon", 23d) })
            {
                string name = "home-" + body.Item1 + "-" + size.x + "x" + size.y;
                string path = Path.Combine(output, name + ".png");
                sky.SetTimeForPreview(body.Item2);
                var uiCamera = GameObject.Find("MockupCamera").GetComponent<Camera>();
                var uiCanvas = GameObject.Find("UiCanvas").GetComponent<Canvas>();
                var uiTarget = new RenderTexture(size.x, size.y, 24);
                uiCamera.targetTexture = uiTarget; uiCamera.aspect = (float)size.x / size.y;
                foreach (var canvas in new[] { uiCanvas, home.homeCanvas })
                { canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = uiCamera; canvas.planeDistance = .75f; }
                try
                {
                    // Keep the final UI target mounted while the layout chooses its
                    // capped background RT; a detached target falls back to 640x480.
                    for (int pass = 0; pass < 2; pass++)
                    {
                        // A real UI render commits Canvas pixelRect changes after
                        // target rotation; ForceUpdateCanvases alone keeps the old rect.
                        uiCamera.Render();
                        Canvas.ForceUpdateCanvases();
                        foreach (var layout in home.homeCanvas.GetComponentsInChildren<PrototypeGardenPreviewLayout>(true)) layout.Apply();
                    }
                    if (Mathf.Abs(camera.aspect - (float)size.x / size.y) > .001f)
                        throw new Exception("Home capture did not settle to the requested display aspect");
                    var direction = body.Item1 == "sun" ? sky.SunDirection : sky.MoonDirection;
                    var rotation = Quaternion.Euler(-Mathf.Asin(direction.y) * Mathf.Rad2Deg + 20,
                        Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg, 0);
                    camera.transform.SetPositionAndRotation(new Vector3(0, .58f, 0) + rotation * Vector3.back * 5.8f, rotation);
                    sky.UpdateClouds(0); camera.Render(); camera.Render();
                    var target = camera.targetTexture;
                    var pixels = Read(target, Path.Combine(output, name + "-raw.png"));
                    var bounds = DiscBounds(pixels, target.width, body.Item1 == "moon");
                    float ratio = (float)bounds.width / bounds.height;
                    results.Add(name + " RT=" + target.width + "x" + target.height + " disc=" + bounds + " ratio=" + ratio + " cameraAspect=" + camera.aspect);
                    // At 20 degrees from the optical axis, a spherical disc has
                    // a cos(20) width/height ratio. Allow two edge pixels for AA.
                    if (bounds.width < 8 || bounds.height < 8 ||
                        Mathf.Abs(bounds.width - bounds.height * Mathf.Cos(20 * Mathf.Deg2Rad)) > 2)
                        results.Add("SHAPE_FAIL " + name);
                    Canvas.ForceUpdateCanvases(); uiCamera.Render(); uiCamera.Render(); Read(uiTarget, path);
                }
                finally { uiCamera.targetTexture = null; uiTarget.Release(); UnityEngine.Object.DestroyImmediate(uiTarget); }
            }
        }

        static Color32[] Read(RenderTexture target, string path)
        {
            var active = RenderTexture.active;
            var texture = new Texture2D(target.width, target.height, TextureFormat.RGBA32, false);
            try
            {
                RenderTexture.active = target;
                texture.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0); texture.Apply();
                File.WriteAllBytes(path, texture.EncodeToPNG());
                return texture.GetPixels32();
            }
            finally { RenderTexture.active = active; UnityEngine.Object.DestroyImmediate(texture); }
        }

        static RectInt DiscBounds(Color32[] pixels, int width, bool moon)
        {
            int height = pixels.Length / width;
            var candidate = new bool[pixels.Length];
            for (int i = 0; i < pixels.Length; i++)
            {
                var p = pixels[i];
                candidate[i] = moon ? p.r > 180 && p.g > 190 && p.b > 200 : p.r > 230 && p.g > 200 && p.r > p.b + 25;
            }
            var queue = new Queue<int>();
            int largest = 0;
            var best = new RectInt();
            for (int i = 0; i < pixels.Length; i++)
            {
                if (!candidate[i]) continue;
                queue.Enqueue(i); candidate[i] = false;
                int count = 0, left = width, right = 0, bottom = height, top = 0;
                while (queue.Count > 0)
                {
                    int index = queue.Dequeue(), x = index % width, y = index / width;
                    count++; left = Math.Min(left, x); right = Math.Max(right, x);
                    bottom = Math.Min(bottom, y); top = Math.Max(top, y);
                    void Visit(int next) { if (candidate[next]) { candidate[next] = false; queue.Enqueue(next); } }
                    if (x > 0) Visit(index - 1);
                    if (x + 1 < width) Visit(index + 1);
                    if (y > 0) Visit(index - width);
                    if (y + 1 < height) Visit(index + width);
                }
                if (count <= largest) continue;
                largest = count;
                best = new RectInt(left, bottom, right - left + 1, top - bottom + 1);
            }
            return best;
        }

        static Color32[] Render(Camera camera, Vector2Int size, string path, Vector2Int? display = null)
        {
            var active = RenderTexture.active;
            var target = new RenderTexture(size.x, size.y, 24) { antiAliasing = 4 };
            var finalSize = display ?? size;
            var composite = display.HasValue ? new RenderTexture(finalSize.x, finalSize.y, 0) : null;
            var texture = new Texture2D(finalSize.x, finalSize.y, TextureFormat.RGBA32, false);
            try
            {
                camera.targetTexture = target;
                camera.Render(); camera.Render();
                if (composite != null) Graphics.Blit(target, composite);
                RenderTexture.active = composite != null ? composite : target;
                texture.ReadPixels(new Rect(0, 0, finalSize.x, finalSize.y), 0, 0); texture.Apply();
                File.WriteAllBytes(path, texture.EncodeToPNG());
                return texture.GetPixels32();
            }
            finally
            {
                RenderTexture.active = active; camera.targetTexture = null; target.Release();
                if (composite != null) { composite.Release(); UnityEngine.Object.DestroyImmediate(composite); }
                UnityEngine.Object.DestroyImmediate(texture); UnityEngine.Object.DestroyImmediate(target);
            }
        }
    }
}
