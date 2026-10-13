using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using CapstoneDesign.Plants;
using CapstoneDesign.Runtime;
using CapstoneDesign.Runtime.LocalState;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace CapstoneDesign.EditorTools
{
    // Exercises the production home presenter, camera, persisted recipe and URP
    // material together. All fixture histories and captures are synthetic.
    public static class GardenProceduralValidation
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
                InstallationId = "synthetic-procedural-garden-validation",
                Nutrient = 100
            };
            public GardenState Load() => StateCodec.Clone(state);
            public void Save(GardenState value) => state = StateCodec.Clone(value);
        }

        public static void Capture()
        {
            Require(!EditorApplication.isPlayingOrWillChangePlaymode, "Run in edit mode only.");
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
                using var service = new GardenStateService(new Store(), new Clock(), new DemoBalanceConfig { VisitorChancePercent = 0 });
                home.activity.Initialize(service, true);
                home.navigation.ShowIsland();
                var state = service.Snapshot;
                state.Environments.Add(new EnvironmentPlacement { EnvironmentId = "environment:E01", Slot = "left" });
                state.Environments.Add(new EnvironmentPlacement { EnvironmentId = "environment:E02", Slot = "right" });
                home.RefreshFromState(state);
                Require(!home.IsOverview, "The home must initially prioritize its plant.");
                var sky = GardenSkyEnvironment.Ensure(home.gardenCamera, home.gardenVisuals.transform);
                home.gardenCamera.GetUniversalAdditionalCameraData().dithering = false;
                using var capture = new GardenLightingValidation.HomeCapture(home, sky, new Vector2Int(1200, 2000));
                string seed = home.RenderedRecipe.BaseSeed;
                string previousMesh = null;
                for (int stage = 0; stage < 4; stage++)
                {
                    state.LegacyGrowth = stage * .1f;
                    home.RefreshFromState(state);
                    AssertGeneratedView(home);
                    Require(home.RenderedRecipe.GrowthStep == new[] { 0, 3, 4, 12 }[stage], "Legacy growth stage did not reach the generated recipe.");
                    Require(home.RenderedRecipe.BaseSeed == seed, "Growing the same garden rerolled its appearance seed.");
                    string signature = MeshSignature(home.ProceduralPlant);
                    Require(signature != previousMesh, "Two growth stages display identical meshes.");
                    previousMesh = signature;
                    sky.SetTimeForPreview(12);
                    var pixels = CaptureFrame(home, capture, Path.Combine(output, "home-stage-" + stage + "-day.png"), report);
                    AssertCameraFit(home);
                    AssertNoMagenta(pixels);
                    report.Add("Legacy growth=" + state.LegacyGrowth + ", recipe step=" + home.RenderedRecipe.GrowthStep
                        + ", triangles=" + home.ProceduralPlant.TriangleCount + ", mesh=" + signature + " PASS");
                }

                foreach (var species in PlantCatalog.All)
                {
                    var candidate = SpeciesState(species);
                    home.RefreshFromState(candidate);
                    home.SetOverview(false);
                    Require(home.RenderedRecipe.SpeciesId == species.Id, "Credited activity profile selected the wrong species: " + species.Id);
                    Require(home.RenderedRecipe.Mature, "Mature legacy garden lost its bloom.");
                    AssertGeneratedView(home);
                    foreach (var time in new[] { ("day-12", 12d), ("night-00", 0d) })
                    {
                        sky.SetTimeForPreview(time.Item2);
                        string name = "home-" + species.Id.Substring("species:".Length) + "-" + time.Item1;
                        var pixels = CaptureFrame(home, capture, Path.Combine(output, name + ".png"), report);
                        AssertNoMagenta(pixels);
                        AssertCameraFit(home);
                    }
                    home.SetOverview(true);
                    sky.SetTimeForPreview(12);
                    CaptureFrame(home, capture, Path.Combine(output, "overview-" + species.Id.Substring("species:".Length) + ".png"), report);
                    AssertCameraFit(home);
                    home.SetOverview(false);
                    report.Add(species.Id + " mature generated geometry fits actual home camera in day/night, triangles="
                        + home.ProceduralPlant.TriangleCount + " PASS");
                }

                home.RefreshFromState(SpeciesState(PlantCatalog.All[0]));
                AssertPresentationControls(home, sky, capture, output, report);
                // Show(true) reloads the service's seed fixture. Restore the
                // mature gallery fixture before comparing real lamp lighting.
                home.RefreshFromState(SpeciesState(PlantCatalog.All[0]));
                AssertVisibleAndLit(home, sky, capture, output, report);
                Require(errors.Count == 0, "Render errors: " + string.Join("; ", errors));
                report.Add("GARDEN_PROCEDURAL_VALIDATION_PASS");
                Debug.Log(report.Last());
            }
            catch (Exception error) { report.Add("FAIL " + error); throw; }
            finally
            {
                Application.logMessageReceived -= listener;
                File.WriteAllLines(Path.Combine(output, "validation.txt"), report);
            }
        }

        static GardenState SpeciesState(SpeciesDefinition species)
        {
            // Each synthetic completion has a distinct daily receipt, just as
            // production profile accounting requires. Never forge a recipe or
            // bypass Sync to make the gallery show an unreachable species.
            var state = new GardenState
            {
                MigrationCompleted = true,
                InstallationId = "synthetic-home-gallery-" + species.Id,
                Nutrient = 100,
                LegacyGrowth = .3f
            };
            var counts = species.ExampleCounts();
            var firstDay = new DateTime(2025, 1, 1);
            int ordinal = 0;
            for (int category = 0; category < counts.Length; category++)
            {
                string mission = ProfileMissions[category];
                for (int i = 0; i < counts[category]; i++)
                {
                    var day = firstDay.AddDays(ordinal++);
                    string cycle = day.ToString("yyyy-MM-dd");
                    string timestamp = day.ToString("yyyy-MM-dd") + "T12:00:00+09:00";
                    state.DailyDecisions.Add(new DailyDecision
                    { CycleId = cycle, TimeZoneId = "Synthetic-KST", AppliedUtc = timestamp, VisitorId = "" });
                    state.Sessions.Add(new ActivitySession
                    {
                        SessionId = "synthetic-gallery-" + ordinal,
                        MissionId = mission, InstanceId = cycle, SelectedUtc = timestamp,
                        StartedUtc = timestamp, EndedUtc = timestamp, Status = ParticipationState.RewardCommitted
                    });
                    state.RewardReceipts.Add(cycle + "|" + mission);
                    state.LastCycleId = cycle;
                }
            }
            state.Environments.Add(new EnvironmentPlacement { EnvironmentId = "environment:E01", Slot = "left" });
            state.Environments.Add(new EnvironmentPlacement { EnvironmentId = "environment:E02", Slot = "right" });
            GardenPlantVisuals.Sync(state);
            StateCodec.Validate(state);
            return state;
        }

        // Must remain representative of the six production profile categories.
        static readonly string[] ProfileMissions =
        { "free-mission:M17", "free-mission:M04", "free-mission:M15", "free-mission:M02", "free-mission:M07", "free-mission:M03" };

        internal static void AssertGeneratedView(GardenHomePresenter home)
        {
            var view = home.ProceduralPlant;
            Require(view != null && home.RenderedRecipe != null && view.gameObject.activeSelf,
                "Home has no active generated plant.");
            var filter = view.GetComponent<MeshFilter>();
            var renderer = view.GetComponent<MeshRenderer>();
            Require(filter.sharedMesh != null && renderer.enabled && renderer.sharedMaterial != null,
                "Generated plant has no drawable mesh/material.");
            Require(renderer.sharedMaterial.shader == Resources.Load<Shader>("ProceduralPlants/PlantVertexColor"),
                "Home did not use the procedural URP material.");
            // LocalLoopValidation also runs under -nographics. Actual shader
            // support is a separate requirement of this graphics capture run.
            if (SystemInfo.graphicsDeviceType != GraphicsDeviceType.Null)
                Require(renderer.sharedMaterial.shader.isSupported, "Procedural URP material is unsupported.");
            Require(renderer.shadowCastingMode != ShadowCastingMode.Off && renderer.receiveShadows
                && renderer.sharedMaterial.FindPass("ShadowCaster") >= 0, "Generated plant no longer participates in scene shadows.");
            Require((home.gardenCamera.cullingMask & (1 << view.gameObject.layer)) != 0,
                "Generated plant is excluded from the actual garden camera.");
            Require(home.plant != null && !home.plant.seed.activeSelf && !home.plant.sprout.activeSelf
                && !home.plant.bud.gameObject.activeSelf && !home.plant.blossomAnchor.gameObject.activeSelf,
                "Legacy fixed plant is still enabled underneath the generated plant.");
            var expected = PlantGeometry.Build(home.RenderedRecipe);
            var mesh = filter.sharedMesh;
            Require(view.TriangleCount == expected.TriangleCount && mesh.vertexCount == expected.Vertices.Count,
                "Home renderer does not contain its committed recipe's generated geometry.");
            var vertices = mesh.vertices; var normals = mesh.normals; var colors = mesh.colors;
            Require(normals.Length == vertices.Length && colors.Length == vertices.Length, "Generated material inputs missing.");
            for (int i = 0; i < vertices.Length; i++)
            {
                var point = expected.Vertices[i]; var normal = expected.Normals[i]; var color = expected.Colors[i];
                Require(vertices[i] == new Vector3((float)point.X, (float)point.Y, (float)point.Z)
                    && normals[i] == new Vector3((float)normal.X, (float)normal.Y, (float)normal.Z)
                    && colors[i] == new Color((float)color.R, (float)color.G, (float)color.B, 1),
                    "Rendered mesh is stale or differs from its saved recipe at vertex " + i);
            }
            var local = home.gardenRoot.InverseTransformPoint(view.transform.position);
            var surface = home.gardenRoot.GetComponent<GardenSurface>();
            Require(surface != null && Mathf.Abs(local.y - home.PlantingHeight) < .003f,
                "Generated plant root is not grounded in its planter soil.");
        }

        static void AssertCameraFit(GardenHomePresenter home)
        {
            var filter = home.ProceduralPlant.GetComponent<MeshFilter>();
            var layout = home.homeCanvas.GetComponentInChildren<CapstoneDesign.Prototype.PrototypeGardenPreviewLayout>(true);
            Require(layout != null && layout.VisibleAreaInViewport.width > 0 && layout.VisibleAreaInViewport.height > 0,
                "Actual home has no measured area between its UI cards.");
            var visible = layout.VisibleAreaInViewport;
            foreach (var vertex in filter.sharedMesh.vertices)
            {
                var viewport = home.gardenCamera.WorldToViewportPoint(filter.transform.TransformPoint(vertex));
                Require(viewport.z > home.gardenCamera.nearClipPlane && viewport.z < home.gardenCamera.farClipPlane
                    && viewport.x >= visible.xMin - .001f && viewport.x <= visible.xMax + .001f
                    && viewport.y >= visible.yMin - .001f && viewport.y <= visible.yMax + .001f,
                    "Generated plant clips outside the actual garden area or underneath a UI card.");
            }
        }

        static void AssertVisibleAndLit(GardenHomePresenter home, GardenSkyEnvironment sky,
            GardenLightingValidation.HomeCapture capture, string output, List<string> report)
        {
            sky.SetTimeForPreview(0);
            var lit = CaptureFrame(home, capture, Path.Combine(output, "home-night-generated-lit.png"), report);
            var renderer = home.ProceduralPlant.GetComponent<MeshRenderer>();
            renderer.enabled = false;
            var hidden = CaptureFrame(home, capture, Path.Combine(output, "home-night-generated-hidden.png"), report);
            renderer.enabled = true;
            var lamps = sky.StreetLighting;
            Require(lamps != null && lamps.Lights.Length > 0 && lamps.Lights.All(light => light.enabled), "Night streetlights are missing.");
            foreach (var light in lamps.Lights) light.enabled = false;
            var unlit = CaptureFrame(home, capture, Path.Combine(output, "home-night-point-lights-off.png"), report);
            foreach (var light in lamps.Lights) light.enabled = true;
            int visible = 0, litPixels = 0; long red = 0, blue = 0;
            for (int i = 0; i < lit.Length; i++)
            {
                if (PixelDifference(lit[i], hidden[i]) < 8) continue;
                visible++;
                if (PixelDifference(lit[i], unlit[i]) >= 3) litPixels++;
                red += lit[i].r - unlit[i].r; blue += lit[i].b - unlit[i].b;
            }
            Require(visible > 100, "Generated plant did not contribute visible pixels to the home screenshot.");
            Require(litPixels > 100 && red > blue, "Real incandescent garden lights did not warm the generated plant.");
            report.Add("Generated plant contributes " + visible + " home pixels; " + litPixels
                + " receive actual streetlight changes; signed red/blue=" + red + "/" + blue + " PASS");
        }

        static void AssertPresentationControls(GardenHomePresenter home, GardenSkyEnvironment sky,
            GardenLightingValidation.HomeCapture capture, string output, List<string> report)
        {
            string recipe = PlantCodec.Encode(home.RenderedRecipe);
            string geometry = MeshSignature(home.ProceduralPlant);
            var localPosition = home.ProceduralPlant.transform.localPosition;
            float originalZoom = home.Zoom;
            home.SetOverview(false);
            sky.SetTimeForPreview(12);
            CaptureFrame(home, capture, Path.Combine(output, "plant-focus-noon.png"), report);
            float focusSize = home.gardenCamera.orthographicSize;
            home.SetOverview(true);
            CaptureFrame(home, capture, Path.Combine(output, "garden-overview-noon.png"), report);
            Require(home.IsOverview && home.gardenCamera.orthographicSize > focusSize * 1.2f,
                "Garden overview does not show a meaningfully wider view than plant focus.");
            var context = home.gardenRoot.GetComponentsInChildren<MeshRenderer>(true)
                .Where(r => !r.transform.IsChildOf(home.ProceduralPlant.transform)
                    && !r.transform.IsChildOf(home.Planter.transform)
                    && (home.plant == null || !r.transform.IsChildOf(home.plant.transform)))
                .ToDictionary(r => r, r => r.enabled);
            Require(context.Count > 0 && context.Values.Any(enabled => enabled), "Overview lost its garden scenery.");
            sky.SetTimeForPreview(0);
            CaptureFrame(home, capture, Path.Combine(output, "garden-overview-midnight.png"), report);
            home.SetOverview(false);
            Require(home.IsPlantIsolated && context.Keys.All(r => !r.enabled),
                "Plant portrait still contains competing garden meshes.");
            CaptureFrame(home, capture, Path.Combine(output, "plant-focus-midnight.png"), report);
            var orbit = home.homeCanvas.GetComponentInChildren<CapstoneDesign.Prototype.PrototypeIslandDrag>(true);
            var camera = home.gardenCamera;
            var position = camera.transform.position;
            var rotation = camera.transform.rotation;
            foreach (int yaw in new[] { 90, 180, 270 })
            {
                var turn = Quaternion.AngleAxis(yaw, Vector3.up);
                camera.transform.SetPositionAndRotation(orbit.focus + turn * (position - orbit.focus), turn * rotation);
                CaptureFrame(home, capture, Path.Combine(output, "plant-night-orbit-" + yaw + ".png"), report);
                AssertCameraFit(home);
            }
            camera.transform.SetPositionAndRotation(position, rotation);
            foreach (float zoom in new[] { -1f, 0f, 1f, 2f })
            {
                home.SetZoom(zoom);
                Require(home.Zoom >= 0 && home.Zoom <= 1, "Plant zoom is not bounded.");
                CaptureFrame(home, capture, Path.Combine(output, "plant-zoom-" + zoom + ".png"), report);
                AssertCameraFit(home);
            }
            home.SetZoom(originalZoom);
            Require(PlantCodec.Encode(home.RenderedRecipe) == recipe && MeshSignature(home.ProceduralPlant) == geometry
                && home.ProceduralPlant.transform.localPosition == localPosition,
                "Camera inspection changed the saved plant recipe, geometry or planting position.");
            home.Show(false);
            Require(!home.IsPlantIsolated && context.All(r => r.Key.enabled == r.Value),
                "Leaving home did not restore the garden renderers for shared previews.");
            home.Show(true);
            Require(!home.IsOverview && home.IsPlantIsolated && Mathf.Abs(home.Zoom - originalZoom) < .001f,
                "Returning home lost the selected plant view or zoom.");
            home.SetOverview(true);
            Require(context.All(r => r.Key == null || r.Key.enabled == r.Value),
                "Overview did not restore original renderer visibility.");
            home.SetOverview(false);
            report.Add("Plant focus, wider overview, bounded zoom, four orbit views and unchanged recipe/mesh PASS");
        }

        static Color32[] CaptureFrame(GardenHomePresenter home, GardenLightingValidation.HomeCapture capture,
            string path, List<string> report)
        {
            // RefreshFromState reapplies production post-processing defaults,
            // including dithering. Disable it immediately before each synthetic
            // comparison so random dither cannot prevent settling or count as
            // plant/light pixels. Runtime defaults remain unchanged.
            home.gardenCamera.GetUniversalAdditionalCameraData().dithering = false;
            return capture.Render(path, report);
        }

        static int PixelDifference(Color32 a, Color32 b) => Math.Max(Math.Abs(a.r - b.r), Math.Max(Math.Abs(a.g - b.g), Math.Abs(a.b - b.b)));
        static void AssertNoMagenta(Color32[] pixels) => Require(pixels.Count(p => p.r > 240 && p.b > 240 && p.g < 20) < 10,
            "Unsupported shader magenta in the actual home render.");
        static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }

        internal static string MeshSignature(ProceduralPlantView view)
        {
            var mesh = view.GetComponent<MeshFilter>().sharedMesh;
            using var bytes = new MemoryStream();
            using (var writer = new BinaryWriter(bytes, System.Text.Encoding.UTF8, true))
            {
                foreach (var vertex in mesh.vertices) { writer.Write(vertex.x); writer.Write(vertex.y); writer.Write(vertex.z); }
                foreach (var normal in mesh.normals) { writer.Write(normal.x); writer.Write(normal.y); writer.Write(normal.z); }
                foreach (var color in mesh.colors) { writer.Write(color.r); writer.Write(color.g); writer.Write(color.b); writer.Write(color.a); }
                foreach (int index in mesh.triangles) writer.Write(index);
            }
            using var hash = SHA256.Create();
            return BitConverter.ToString(hash.ComputeHash(bytes.ToArray())).Replace("-", "");
        }
    }
}
