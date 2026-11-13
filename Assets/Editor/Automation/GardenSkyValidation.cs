using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CapstoneDesign.Prototype;
using CapstoneDesign.Runtime;
using CapstoneDesign.Runtime.LocalState;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace CapstoneDesign.EditorTools
{
    // Captures the actual home with synthetic state, then isolates the sky to
    // distinguish world-fixed galactic detail from moving stars and scene meshes.
    public static class GardenSkyValidation
    {
        sealed class Clock : IClock
        {
            public DateTimeOffset UtcNow => new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.FromHours(9));
            public TimeZoneInfo TimeZone => TimeZoneInfo.CreateCustomTimeZone("Synthetic-KST", TimeSpan.FromHours(9), "KST", "KST");
        }
        sealed class Store : IStateStore
        {
            GardenState state = new GardenState
            {
                MigrationCompleted = true,
                InstallationId = "synthetic-sky-validation",
                Nutrient = 100,
                LegacyGrowth = .3f
            };
            public GardenState Load() => StateCodec.Clone(state);
            public void Save(GardenState value) => state = StateCodec.Clone(value);
        }

        public static void Capture()
        {
            Require(!EditorApplication.isPlayingOrWillChangePlaymode, "Run in edit mode only.");
            Require(SystemInfo.graphicsDeviceType != GraphicsDeviceType.Null, "Sky validation requires a graphics device.");
            string output = Environment.GetEnvironmentVariable("CAPSTONE_ARTIFACTS");
            Require(!string.IsNullOrWhiteSpace(output), "Explicit synthetic artifact directory required.");
            Directory.CreateDirectory(output);
            var report = new List<string>();
            var errors = new List<string>();
            Application.LogCallback listener = (message, stack, type) =>
            { if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) errors.Add(message); };
            Application.logMessageReceived += listener;
            try
            {
                EditorSceneManager.OpenScene("Assets/Scenes/MockupMain.unity");
                AndroidPlatformBridge.UseMockProvider();
                var home = UnityEngine.Object.FindFirstObjectByType<GardenHomePresenter>(FindObjectsInactive.Include);
                Require(home != null, "Production home presenter missing.");
                using var service = new GardenStateService(new Store(), new Clock(), new DemoBalanceConfig { VisitorChancePercent = 0 });
                home.activity.Initialize(service, true);
                var state = service.Snapshot;
                state.Environments.Add(new EnvironmentPlacement { EnvironmentId = "environment:E01", Slot = "left" });
                state.Environments.Add(new EnvironmentPlacement { EnvironmentId = "environment:E02", Slot = "right" });
                home.navigation.ShowIsland();
                home.RefreshFromState(state);
                var camera = home.gardenCamera;
                var sky = GardenSkyEnvironment.Ensure(camera, home.gardenVisuals.transform);
                var material = camera.GetComponent<Skybox>().material;
                Require(material != null && material.shader.isSupported && material.HasProperty("_GalaxyIntensity"),
                    "Supported atmosphere shader with galaxy control required.");
                var orbit = home.homeCanvas.GetComponentInChildren<PrototypeIslandDrag>(true);
                Require(orbit != null, "Home orbit controller missing.");
                orbit.ResetView();
                var rotation = camera.transform.rotation;
                var position = camera.transform.position;

                using (var capture = new GardenLightingValidation.HomeCapture(home, sky, new Vector2Int(1200, 2000)))
                {
                    sky.SetTimeForPreview(12);
                    material.SetFloat("_GalaxyIntensity", 1);
                    var day = CaptureHome(home, capture, Path.Combine(output, "home-noon-12.png"), report);
                    material.SetFloat("_GalaxyIntensity", 0);
                    var dayWithout = CaptureHome(home, capture, Path.Combine(output, "home-noon-galaxy-off.png"), report);
                    Require(day.SequenceEqual(dayWithout), "Galaxy changes the daytime home.");
                    report.Add("Daytime galaxy A/B: all home pixels identical PASS");

                    sky.SetTimeForPreview(0);
                    var nightWithout = CaptureHome(home, capture, Path.Combine(output, "home-midnight-galaxy-off.png"), report);
                    material.SetFloat("_GalaxyIntensity", 1);
                    var night = CaptureHome(home, capture, Path.Combine(output, "home-midnight-00.png"), report);
                    AssertGalaxyVisible(Delta(night, nightWithout), "Actual night home", report);
                    foreach (int yaw in new[] { 0, 90, 180, 270 })
                    {
                        var turn = Quaternion.AngleAxis(yaw, Vector3.up);
                        camera.transform.SetPositionAndRotation(orbit.focus + turn * (position - orbit.focus), turn * rotation);
                        CaptureHome(home, capture, Path.Combine(output, "home-night-orbit-" + yaw + ".png"), report);
                    }
                    camera.transform.SetPositionAndRotation(position, rotation);
                }

                using (var capture = new GardenLightingValidation.HomeCapture(home, sky, new Vector2Int(2000, 1200)))
                {
                    sky.SetTimeForPreview(12);
                    CaptureHome(home, capture, Path.Combine(output, "home-landscape-noon.png"), report);
                    sky.SetTimeForPreview(0);
                    CaptureHome(home, capture, Path.Combine(output, "home-landscape-midnight.png"), report);
                }

                ValidateWorldSky(camera, sky, material, rotation, output, report);
                Require(errors.Count == 0, "Render errors: " + string.Join("; ", errors));
                report.Add("GARDEN_SKY_VALIDATION_PASS");
                Debug.Log(report.Last());
            }
            catch (Exception error) { report.Add("FAIL " + error); throw; }
            finally
            {
                Application.logMessageReceived -= listener;
                File.WriteAllLines(Path.Combine(output, "validation.txt"), report);
            }
        }

        static void ValidateWorldSky(Camera camera, GardenSkyEnvironment sky, Material material,
            Quaternion rotation, string output, List<string> report)
        {
            var data = camera.GetUniversalAdditionalCameraData();
            int oldMask = camera.cullingMask;
            bool oldPost = data.renderPostProcessing;
            float oldAspect = camera.aspect;
            var oldProjection = camera.projectionMatrix;
            var oldPosition = camera.transform.position;
            var oldRotation = camera.transform.rotation;
            try
            {
                // Remove scene geometry and post-processing so differences can
                // only come from the atmosphere, not lamps, bloom or exposure.
                camera.cullingMask = 0;
                data.renderPostProcessing = false;
                data.dithering = false;
                camera.aspect = 1;
                camera.ResetProjectionMatrix();
                camera.transform.SetPositionAndRotation(Vector3.zero, rotation);
                sky.SetTimeForPreview(12);
                material.SetFloat("_GalaxyIntensity", 1);
                var day = RenderSky(camera, Path.Combine(output, "sky-noon.png"));
                material.SetFloat("_GalaxyIntensity", 0);
                var dayWithout = RenderSky(camera, null);
                Require(day.SequenceEqual(dayWithout), "Galaxy changes the daytime atmosphere.");
                double red = day.Average(p => (double)p.r), green = day.Average(p => (double)p.g), blue = day.Average(p => (double)p.b);
                Require(blue > red + 20 && green > red + 8, "Day sky does not read as blue.");
                report.Add("Day atmosphere average RGB=" + red + "/" + green + "/" + blue + " PASS");

                sky.SetTimeForPreview(0);
                int[] first = null;
                double largestOrbitChange = 0;
                foreach (int yaw in new[] { 0, 90, 180, 270, 360 })
                {
                    camera.transform.rotation = Quaternion.AngleAxis(yaw, Vector3.up) * rotation;
                    var galaxy = CaptureGalaxyDelta(camera, material, Path.Combine(output, "sky-night-orbit-" + yaw + ".png"));
                    if (yaw == 0)
                    {
                        first = galaxy;
                        AssertGalaxyVisible(first, "Isolated night atmosphere", report);
                        camera.transform.position = new Vector3(37, 19, -53);
                        var translated = CaptureGalaxyDelta(camera, material, null);
                        double movement = MeanDifference(first, translated);
                        Require(movement < .05, "Galaxy moves when only the camera position changes.");
                        camera.transform.position = Vector3.zero;
                        report.Add("Sky translation invariance: mean RGB delta=" + movement + " PASS");
                    }
                    else if (yaw == 360)
                    {
                        double closure = MeanDifference(first, galaxy);
                        Require(closure < .1, "Galaxy does not return to its original view after a full orbit.");
                        report.Add("Sky 360 degree closure: mean RGB delta=" + closure + " PASS");
                    }
                    else largestOrbitChange = Math.Max(largestOrbitChange, MeanDifference(first, galaxy));
                }
                Require(largestOrbitChange > .2, "Galaxy remains pinned to the camera while yaw changes.");
                report.Add("Galaxy-only camera yaw change: largest mean RGB delta=" + largestOrbitChange + " PASS");
            }
            finally
            {
                material.SetFloat("_GalaxyIntensity", 1);
                camera.cullingMask = oldMask;
                data.renderPostProcessing = oldPost;
                camera.aspect = oldAspect;
                camera.projectionMatrix = oldProjection;
                camera.transform.SetPositionAndRotation(oldPosition, oldRotation);
            }
        }

        static int[] CaptureGalaxyDelta(Camera camera, Material material, string path)
        {
            material.SetFloat("_GalaxyIntensity", 0);
            var without = RenderSky(camera, null);
            material.SetFloat("_GalaxyIntensity", 1);
            return Delta(RenderSky(camera, path), without);
        }

        static Color32[] CaptureHome(GardenHomePresenter home, GardenLightingValidation.HomeCapture capture,
            string path, List<string> report)
        {
            // RefreshFromState restores runtime dither defaults. Disable it for
            // every deterministic capture, never change the production defaults.
            home.gardenCamera.GetUniversalAdditionalCameraData().dithering = false;
            return capture.Render(path, report);
        }

        static Color32[] RenderSky(Camera camera, string path)
        {
            const int size = 512;
            var previousTarget = camera.targetTexture;
            var previousActive = RenderTexture.active;
            var target = new RenderTexture(size, size, 24);
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            try
            {
                camera.targetTexture = target;
                camera.Render(); camera.Render();
                RenderTexture.active = target;
                texture.ReadPixels(new Rect(0, 0, size, size), 0, 0); texture.Apply();
                var pixels = texture.GetPixels32();
                Require(pixels.All(p => p.a >= 250), "Sky contains transparent pixels.");
                if (path != null) File.WriteAllBytes(path, texture.EncodeToPNG());
                return pixels;
            }
            finally
            {
                camera.targetTexture = previousTarget;
                RenderTexture.active = previousActive;
                target.Release();
                UnityEngine.Object.DestroyImmediate(target);
                UnityEngine.Object.DestroyImmediate(texture);
            }
        }

        static int[] Delta(Color32[] on, Color32[] off)
        {
            var delta = new int[on.Length * 3];
            for (int i = 0; i < on.Length; i++)
            {
                delta[i * 3] = on[i].r - off[i].r;
                delta[i * 3 + 1] = on[i].g - off[i].g;
                delta[i * 3 + 2] = on[i].b - off[i].b;
            }
            return delta;
        }

        static void AssertGalaxyVisible(int[] delta, string label, List<string> report)
        {
            int noticeable = 0, peak = 0, lowest = 255;
            for (int i = 0; i < delta.Length; i += 3)
            {
                int difference = Math.Max(Math.Abs(delta[i]), Math.Max(Math.Abs(delta[i + 1]), Math.Abs(delta[i + 2])));
                if (difference >= 3) noticeable++;
                peak = Math.Max(peak, difference); lowest = Math.Min(lowest, difference);
            }
            Require(noticeable > delta.Length / 3 / 200 && peak >= 4 && peak - lowest >= 3,
                label + " has no visible, spatially varying galaxy contribution.");
            report.Add(label + ": galaxy visible pixels=" + noticeable + ", peak RGB change=" + peak + " PASS");
        }

        static double MeanDifference(int[] first, int[] second)
        {
            long total = 0;
            for (int i = 0; i < first.Length; i++) total += Math.Abs(first[i] - second[i]);
            return (double)total / first.Length;
        }

        static void Require(bool pass, string message) { if (!pass) throw new Exception(message); }
    }
}
