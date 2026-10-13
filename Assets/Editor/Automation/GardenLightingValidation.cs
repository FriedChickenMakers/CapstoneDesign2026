using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CapstoneDesign.Prototype;
using CapstoneDesign.Runtime;
using CapstoneDesign.Runtime.LocalState;
using TMPro;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

namespace CapstoneDesign.EditorTools
{
    // Synthetic in-memory state only. A/B captures isolate actual light and bloom
    // contributions rather than accepting a bright bulb or enabled component.
    public static class GardenLightingValidation
    {
        sealed class Clock : IClock
        {
            public DateTimeOffset UtcNow => new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.FromHours(9));
            public TimeZoneInfo TimeZone => TimeZoneInfo.CreateCustomTimeZone("Synthetic-KST", TimeSpan.FromHours(9), "KST", "KST");
        }
        sealed class Store : IStateStore
        {
            GardenState state;
            public GardenState Load() => state == null ? null : StateCodec.Clone(state);
            public void Save(GardenState value) => state = StateCodec.Clone(value);
        }
        readonly struct LightingState
        {
            readonly float intensity;
            readonly Color key, sky, equator, ground;
            public LightingState(Light light)
            {
                intensity = light.intensity; key = light.color;
                sky = RenderSettings.ambientSkyColor; equator = RenderSettings.ambientEquatorColor;
                ground = RenderSettings.ambientGroundColor;
            }
            public bool Matches(Light light) => intensity == light.intensity && key == light.color
                && sky == RenderSettings.ambientSkyColor && equator == RenderSettings.ambientEquatorColor
                && ground == RenderSettings.ambientGroundColor;
        }
        sealed class Difference
        {
            public int Changed, Visible, Peak;
            public double Red, Green, Blue;
            public Difference(Color32[] on, Color32[] off)
            {
                Require(on.Length == off.Length, "Comparison sizes differ");
                for (int i = 0; i < on.Length; i++)
                {
                    int r = on[i].r - off[i].r, g = on[i].g - off[i].g, b = on[i].b - off[i].b;
                    int delta = Math.Max(Math.Abs(r), Math.Max(Math.Abs(g), Math.Abs(b)));
                    if (delta > 0) Changed++;
                    if (delta >= 3) Visible++;
                    Peak = Math.Max(Peak, delta); Red += r; Green += g; Blue += b;
                }
                Red /= on.Length; Green /= on.Length; Blue /= on.Length;
            }
            public override string ToString() => "changed=" + Changed + ", visible=" + Visible + ", peak=" + Peak
                + ", mean signed RGB=" + Red.ToString("F5") + "/" + Green.ToString("F5") + "/" + Blue.ToString("F5");
        }

        public static void Capture()
        {
            string output = Environment.GetEnvironmentVariable("CAPSTONE_ARTIFACTS");
            Require(!string.IsNullOrWhiteSpace(output), "Explicit synthetic artifact directory required");
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
                using var service = new GardenStateService(new Store(), new Clock(),
                    new DemoBalanceConfig { VisitorChancePercent = 0 }, new LegacySnapshot { Nutrient = 100 });
                home.activity.Initialize(service, true);
                var state = service.Snapshot; state.LegacyGrowth = .3f;
                state.Environments.Add(new EnvironmentPlacement { EnvironmentId = "environment:E01", Slot = "left" });
                state.Environments.Add(new EnvironmentPlacement { EnvironmentId = "environment:E02", Slot = "right" });
                home.navigation.ShowIsland(); home.RefreshFromState(state);
                AssertFiniteMeshes(home, report);
                var camera = home.gardenCamera;
                var sky = GardenSkyEnvironment.Ensure(camera, home.gardenVisuals.transform);
                var lamps = sky.StreetLighting;
                Require(lamps != null && lamps.Lights.Length > 0, "Imported streetlamp lights missing");
                AssertLampGrounding(lamps, home.gardenRoot.GetComponent<GardenSurface>(), report);
                Require(lamps.Lights.All(l => l.type == LightType.Point && l.range > 0 && l.intensity > 0
                    && l.color.r > l.color.g && l.color.g > l.color.b), "Warm real point lights required");
                foreach (var boundary in new[] { (0d, true), (5.999d, true), (6d, false), (12d, false), (17.999d, false), (18d, true), (23.999d, true) })
                {
                    sky.SetTimeForPreview(boundary.Item1);
                    Require(lamps.IsLit == boundary.Item2 && lamps.Lights.All(l => l.enabled == boundary.Item2),
                        "Lamp clock boundary failed at " + boundary.Item1);
                    report.Add("Lamp clock " + boundary.Item1 + " enabled=" + boundary.Item2 + " PASS");
                }
                var volume = GardenPostProcessing.Ensure(camera, home.gardenVisuals.transform);
                Require(volume != null, "Post-processing volume missing");
                Require(volume.profile.TryGet<Bloom>(out var bloom), "Bloom profile missing");
                Require(volume.profile.TryGet<ColorAdjustments>(out var color) && color.contrast.value > 0 && color.contrast.value <= 10,
                    "Expected restrained positive contrast");
                Require(camera.allowHDR && camera.GetUniversalAdditionalCameraData().renderPostProcessing, "Garden HDR/post processing disabled");
                float bloomIntensity = bloom.intensity.value;
                Require(bloomIntensity > 0, "Bloom intensity must be positive");
                // Dither is normally enabled. Disable only in these deterministic
                // pixel comparisons so random dither cannot count as light/bloom.
                camera.GetUniversalAdditionalCameraData().dithering = false;
                report.Add("Post: bloom=" + bloomIntensity + ", threshold=" + bloom.threshold.value + ", contrast=" + color.contrast.value);
                camera.ResetProjectionMatrix(); camera.aspect = 1; camera.orthographicSize = 2.4f;
                var rotation = Quaternion.Euler(28, -25, 0);
                camera.transform.SetPositionAndRotation(new Vector3(0, .08f, 0) + rotation * Vector3.back * 5.8f, rotation);
                bloom.intensity.value = 0;
                foreach (var diagnostic in new[] { ("day", 12d), ("night", 0d) })
                {
                    sky.SetTimeForPreview(diagnostic.Item2);
                    var data = camera.GetUniversalAdditionalCameraData();
                    data.renderPostProcessing = false;
                    RenderScene(camera, Path.Combine(output, diagnostic.Item1 + "-hdr-no-post.png"), report);
                    RenderScene(camera, Path.Combine(output, diagnostic.Item1 + "-ldr-no-post.png"), null, RenderTextureFormat.ARGB32);
                    data.renderPostProcessing = true;
                    RenderScene(camera, Path.Combine(output, diagnostic.Item1 + "-hdr-post-no-bloom.png"), report);
                }
                sky.SetTimeForPreview(0);
                var originalLighting = new LightingState(sky.KeyLight);
                var fog = sky.Clouds.GetComponent<ParticleSystemRenderer>(); fog.enabled = false;
                var on = RenderScene(camera, Path.Combine(output, "night-point-lights-on.png"));
                SetLights(lamps, sky, false);
                var off = RenderScene(camera, Path.Combine(output, "night-point-lights-off.png"));
                var direct = new Difference(on, off);
                report.Add("Actual point illumination, emission unchanged: " + direct);
                Require(originalLighting.Matches(sky.KeyLight), "Point lighting changed key/ambient light");
                Require(direct.Visible > 300 && direct.Red > .02 && direct.Red > direct.Blue,
                    "Actual point lights did not visibly warm island surfaces");

                // Hide all opaque garden meshes to isolate fog receiving local
                // light. Bulb emission and island pixels cannot satisfy this check.
                fog.enabled = true;
                var meshes = home.gardenVisuals.GetComponentsInChildren<MeshRenderer>(true);
                var enabled = meshes.Select(r => r.enabled).ToArray();
                try
                {
                    foreach (var renderer in meshes) renderer.enabled = false;
                    camera.orthographicSize = 4;
                    off = RenderScene(camera, Path.Combine(output, "fog-local-light-off.png"));
                    SetLights(lamps, sky, true);
                    on = RenderScene(camera, Path.Combine(output, "fog-local-light-on.png"));
                    var fogDifference = new Difference(on, off);
                    report.Add("Isolated fog local illumination: " + fogDifference);
                    Require(fogDifference.Changed > 100 && fogDifference.Red > .001 && fogDifference.Red > fogDifference.Blue,
                        "Surrounding fog did not receive warm local light");
                }
                finally { for (int i = 0; i < meshes.Length; i++) meshes[i].enabled = enabled[i]; }
                Require(originalLighting.Matches(sky.KeyLight), "Fog lighting changed global light");
                home.homeCanvas.GetComponentInChildren<PrototypeIslandDrag>(true)?.ResetView();
                home.RefreshFromState(state);
                camera.GetUniversalAdditionalCameraData().dithering = false;
                var size = new Vector2Int(1200, 2000);
                using var homeCapture = new HomeCapture(home, sky, size);
                foreach (var time in new[] { ("day-12", 12d), ("night-00", 0d) })
                {
                    sky.SetTimeForPreview(time.Item2); sky.UpdateClouds(0);
                    bloom.intensity.value = 0;
                    var withoutPath = Path.Combine(output, "home-" + time.Item1 + "-bloom-off.png");
                    off = homeCapture.Render(withoutPath, report);
                    bloom.intensity.value = bloomIntensity;
                    var withPath = Path.Combine(output, "home-" + time.Item1 + ".png");
                    on = homeCapture.Render(withPath, report);
                    var difference = new Difference(on, off);
                    report.Add("Home " + time.Item1 + " bloom contribution: " + difference);
                    Require(difference.Visible > 100 && difference.Peak >= 3, "Bloom was not visible at " + time.Item1);
                    AssertUiUnchanged(home, on, off, size, report);
                    AssertGrowthCentered(home, report);
                }
                Require(errors.Count == 0, "Render errors: " + string.Join("; ", errors));
                report.Add("GARDEN_LIGHTING_VALIDATION_PASS");
                Debug.Log(report.Last());
            }
            catch (Exception error) { report.Add("FAIL " + error.Message); throw; }
            finally
            {
                Application.logMessageReceived -= listener;
                File.WriteAllLines(Path.Combine(output, "validation.txt"), report);
            }
        }

        static void SetLights(GardenStreetLighting lamps, GardenSkyEnvironment sky, bool enabled)
        { foreach (var light in lamps.Lights) light.enabled = enabled; sky.UpdateClouds(0); }
        static void AssertLampGrounding(GardenStreetLighting lamps, GardenSurface surface, List<string> report)
        {
            foreach (var light in lamps.Lights)
            {
                var lamp = light.transform.parent;
                var points = lamp.GetComponentsInChildren<MeshFilter>().SelectMany(filter =>
                    filter.sharedMesh.vertices.Select(vertex => surface.transform.InverseTransformPoint(filter.transform.TransformPoint(vertex)))).ToArray();
                float bottom = points.Min(p => p.y);
                var feet = points.Where(p => p.y < bottom + .001f).ToArray();
                float error = feet.Max(p => Mathf.Abs(p.y - surface.HeightInSpace(surface.transform, new Vector2(p.x, p.z))));
                Require(error < .01f, "Streetlamp base straddles a cliff or floats above the surface: " + lamp.name);
                report.Add(lamp.name + " entire base grounded, maximum gap=" + error + " PASS");
            }
        }
        static void Require(bool pass, string message) { if (!pass) throw new Exception(message); }
        static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        static bool Finite(Vector3 value) => Finite(value.x) && Finite(value.y) && Finite(value.z);
        static bool Finite(Vector4 value) => Finite(value.x) && Finite(value.y) && Finite(value.z) && Finite(value.w);
        static void AssertFiniteMeshes(GardenHomePresenter home, List<string> report)
        {
            int count = 0;
            foreach (var mesh in home.gardenVisuals.GetComponentsInChildren<MeshFilter>(true)
                .Select(filter => filter.sharedMesh).Where(mesh => mesh != null).Distinct())
            {
                Require(mesh.vertices.All(Finite) && mesh.normals.All(Finite) && mesh.tangents.All(Finite),
                    "Nonfinite positions/normals/tangents in " + mesh.name);
                var uv = new List<Vector4>(); mesh.GetUVs(0, uv);
                Require(uv.All(Finite), "Nonfinite UV0 in " + mesh.name);
                if (mesh.name == "Island-level-meadow" || mesh.name == "Chamomile-open-petals")
                {
                    Require(!mesh.HasVertexAttribute(VertexAttribute.Tangent) && !mesh.HasVertexAttribute(VertexAttribute.TexCoord0),
                        "Expanded solid-palette mesh retained uninitialized source UV/tangent channels: " + mesh.name);
                    report.Add(mesh.name + " vertex attributes: finite geometry, no stale UV/tangent channels PASS");
                }
                count++;
            }
            report.Add("Finite positions/normals/tangents/UV0: " + count + " distinct garden meshes PASS");
        }
        static Color32[] RenderScene(Camera camera, string path, List<string> report = null,
            RenderTextureFormat format = RenderTextureFormat.ARGBHalf)
        {
            var oldTarget = camera.targetTexture; var oldActive = RenderTexture.active;
            var target = new RenderTexture(900, 900, 24, format);
            var texture = new Texture2D(900, 900, TextureFormat.RGBA32, false);
            try
            {
                camera.targetTexture = target; camera.aspect = 1; camera.Render(); camera.Render();
                RenderTexture.active = target;
                if (report != null)
                {
                    var floating = new Texture2D(900, 900, TextureFormat.RGBAFloat, false, true);
                    try
                    {
                        floating.ReadPixels(new Rect(0, 0, 900, 900), 0, 0); floating.Apply();
                        var values = floating.GetPixels();
                        int invalid = 0, negative = 0, high = 0; float minimum = float.MaxValue, maximum = float.MinValue;
                        foreach (var value in values)
                        {
                            bool finite = !(float.IsNaN(value.r) || float.IsNaN(value.g) || float.IsNaN(value.b)
                                || float.IsInfinity(value.r) || float.IsInfinity(value.g) || float.IsInfinity(value.b));
                            if (!finite) { invalid++; continue; }
                            float low = Mathf.Min(value.r, value.g, value.b), top = Mathf.Max(value.r, value.g, value.b);
                            minimum = Mathf.Min(minimum, low); maximum = Mathf.Max(maximum, top);
                            if (low < 0) negative++; if (top > 8) high++;
                        }
                        report.Add(Path.GetFileName(path) + " float RGB invalid=" + invalid + ", negative=" + negative
                            + ", over8=" + high + ", min/max=" + minimum + "/" + maximum
                            + ", center450,290=" + values[290 * 900 + 450] + ", center450,610=" + values[610 * 900 + 450]);
                    }
                    finally { UnityEngine.Object.DestroyImmediate(floating); }
                }
                texture.ReadPixels(new Rect(0, 0, 900, 900), 0, 0); texture.Apply();
                var pixels = texture.GetPixels32();
                Require(pixels.All(p => p.a >= 250), "Transparent pixels in composited garden target");
                File.WriteAllBytes(path, texture.EncodeToPNG()); return pixels;
            }
            finally
            {
                camera.targetTexture = oldTarget; RenderTexture.active = oldActive; target.Release();
                UnityEngine.Object.DestroyImmediate(target); UnityEngine.Object.DestroyImmediate(texture);
            }
        }
        sealed class HomeCapture : IDisposable
        {
            readonly GardenHomePresenter home;
            readonly GardenSkyEnvironment sky;
            readonly Camera camera;
            readonly RenderTexture target;
            readonly Texture2D texture;
            readonly Vector2Int size;
            public HomeCapture(GardenHomePresenter home, GardenSkyEnvironment sky, Vector2Int size)
            {
                this.home = home; this.sky = sky; this.size = size;
                camera = GameObject.Find("MockupCamera").GetComponent<Camera>();
                var canvas = GameObject.Find("UiCanvas").GetComponent<Canvas>();
                canvas.renderMode = home.homeCanvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = home.homeCanvas.worldCamera = camera;
                canvas.planeDistance = 1; home.homeCanvas.planeDistance = .75f;
                target = new RenderTexture(size.x, size.y, 24);
                texture = new Texture2D(size.x, size.y, TextureFormat.RGBA32, false);
                camera.targetTexture = target; camera.aspect = (float)size.x / size.y;
                foreach (var text in UnityEngine.Object.FindObjectsByType<Text>(FindObjectsSortMode.None))
                { text.font?.RequestCharactersInTexture(text.text, text.fontSize, text.fontStyle); text.SetAllDirty(); }
                foreach (var text in home.homeCanvas.GetComponentsInChildren<TMP_Text>(true))
                { text.ForceMeshUpdate(true, true); text.SetAllDirty(); }
            }
            void Draw()
            {
                Canvas.ForceUpdateCanvases();
                foreach (var layout in home.homeCanvas.GetComponentsInChildren<PrototypeGardenPreviewLayout>(true)) layout.Apply();
                sky.UpdateClouds(0); home.gardenCamera.Render();
                Canvas.ForceUpdateCanvases(); camera.Render();
            }
            Color32[] Pixels()
            {
                var previous = RenderTexture.active; RenderTexture.active = target;
                texture.ReadPixels(new Rect(0, 0, size.x, size.y), 0, 0); texture.Apply();
                RenderTexture.active = previous; return texture.GetPixels32();
            }
            public Color32[] Render(string path, List<string> report)
            {
                // Keep target dimensions and camera projection stable across both
                // captures AND their UI bounds checks. Initial passes establish
                // canvas depths and TMP meshes before a frame can count as evidence.
                for (int i = 0; i < 4; i++) Draw();
                var previous = Pixels();
                for (int pass = 0; pass < 4; pass++)
                {
                    Draw(); var current = Pixels();
                    if (current.SequenceEqual(previous))
                    {
                        File.WriteAllBytes(path, texture.EncodeToPNG());
                        report.Add(Path.GetFileName(path) + " consecutive complete frames pixel-identical after " + (pass + 5) + " passes");
                        return current;
                    }
                    previous = current;
                }
                throw new Exception("Home capture did not settle: " + Path.GetFileName(path));
            }
            public void Dispose()
            {
                camera.targetTexture = null; target.Release();
                UnityEngine.Object.DestroyImmediate(target); UnityEngine.Object.DestroyImmediate(texture);
            }
        }
        static void AssertGrowthCentered(GardenHomePresenter home, List<string> report)
        {
            var surface = home.stage.rectTransform.parent as RectTransform;
            Require(surface != null && surface.name == "Growth surface" && Mathf.Abs(surface.rect.height - 64) < .01f,
                "Compact growth strip height changed");
            home.stage.ForceMeshUpdate(true, true);
            float glyphCenter = surface.InverseTransformPoint(home.stage.transform.TransformPoint(GlyphBounds(home.stage).center)).y;
            float error = Mathf.Abs(glyphCenter - surface.rect.center.y);
            Require(error < 1, "Growth label visible glyphs are not vertically centered: " + error);
            foreach (var dot in home.progressDots)
            {
                float center = surface.InverseTransformPoint(dot.transform.TransformPoint(dot.rectTransform.rect.center)).y;
                Require(Mathf.Abs(center - surface.rect.center.y) < .01f, "Growth marker is not vertically centered");
            }
            report.Add("Growth card 1200x2000: height=64, glyph midpoint error=" + error + ", four centered markers PASS");
        }
        static void AssertUiUnchanged(GardenHomePresenter home, Color32[] on, Color32[] off, Vector2Int size, List<string> report)
        {
            var camera = home.homeCanvas.worldCamera;
            Require(!camera.GetUniversalAdditionalCameraData().renderPostProcessing, "Overlay camera must not post-process UI");
            foreach (var label in new[] { home.title, home.guide, home.stage,
                home.homeTabLabel, home.activityTabLabel, home.settingsTabLabel })
            {
                label.ForceMeshUpdate(true, true);
                var bounds = GlyphBounds(label);
                var min = camera.WorldToViewportPoint(label.transform.TransformPoint(bounds.min));
                var max = camera.WorldToViewportPoint(label.transform.TransformPoint(bounds.max));
                // Full visible glyph bounds plus two pixels stay within the
                // opaque card, avoiding its rounded transparent corners.
                int x0 = Mathf.Clamp(Mathf.FloorToInt(min.x * size.x) - 2, 0, size.x);
                int x1 = Mathf.Clamp(Mathf.CeilToInt(max.x * size.x) + 2, 0, size.x);
                int y0 = Mathf.Clamp(Mathf.FloorToInt(min.y * size.y) - 2, 0, size.y);
                int y1 = Mathf.Clamp(Mathf.CeilToInt(max.y * size.y) + 2, 0, size.y);
                Require(x1 > x0 && y1 > y0, "UI comparison rectangle is outside the image: " + label.name);
                int changed = 0, ink = 0;
                for (int y = y0; y < y1; y++) for (int x = x0; x < x1; x++)
                {
                    int i = y * size.x + x;
                    if (on[i].r != off[i].r || on[i].g != off[i].g || on[i].b != off[i].b) changed++;
                    if (off[i].g < 180 && off[i].g > off[i].r && off[i].g > off[i].b) ink++;
                }
                Require(ink > 20, "Baseline UI glyphs were not actually rendered: " + label.name);
                Require(changed == 0, "Bloom changed overlay UI pixels: " + label.name + " count=" + changed);
                report.Add("Bloom leaves UI " + label.name + " unchanged: " + (x1 - x0) * (y1 - y0) + " pixels PASS");
            }
        }
        static Bounds GlyphBounds(TMP_Text label)
        {
            // TMP.textBounds reports the font's ascender/descender, not the
            // visible glyph quads used by its geometry/Midline alignment.
            var minimum = Vector3.one * float.MaxValue;
            var maximum = Vector3.one * float.MinValue;
            int count = 0;
            for (int i = 0; i < label.textInfo.characterCount; i++)
            {
                var character = label.textInfo.characterInfo[i];
                if (!character.isVisible) continue;
                minimum = Vector3.Min(minimum, character.bottomLeft);
                maximum = Vector3.Max(maximum, character.topRight); count++;
            }
            Require(count > 0, "Visible glyph geometry missing: " + label.name);
            return new Bounds((minimum + maximum) * .5f, maximum - minimum);
        }
    }
}
