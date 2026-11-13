using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using CapstoneDesign.Runtime;
using CapstoneDesign.Runtime.LocalState;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;

namespace CapstoneDesign.EditorTools
{
    // Explicit graphics batch entry only. The same runtime presenter/effect is
    // sampled against a real copied scene and a synthetic in-memory save. This
    // is deterministic frame validation, not elapsed-time Play/device evidence.
    public static class GardenReducedMotionValidation
    {
        const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        const string Preference = "ui.reduce-motion";
        [Serializable] sealed class Check { public string name, status, detail; }
        [Serializable] sealed class Report
        {
            public string utc, unityVersion;
            public string environment = "Isolated synthetic Unity graphics batch; actual runtime frame sampling; no user preferences, save, device or OS input";
            public int passed, failed, assertions;
            public bool preferenceRestored, runtimeMotionRestored;
            public List<Check> checks = new List<Check>();
        }
        sealed class Store : IStateStore
        {
            string xml;
            public int Writes;
            public GardenState Load() => xml == null ? null : StateCodec.Decode(xml);
            public void Save(GardenState state) { xml = StateCodec.Encode(state); Writes++; }
        }
        sealed class Clock : IClock
        {
            public DateTimeOffset UtcNow => DateTimeOffset.Parse("2026-11-12T10:00:00+09:00");
            public TimeZoneInfo TimeZone => TimeZoneInfo.CreateCustomTimeZone("Synthetic-KST", TimeSpan.FromHours(9), "Synthetic KST", "Synthetic KST");
        }
        static Report report;
        static string output;

        public static void RunBatch()
        {
            string artifacts = Environment.GetEnvironmentVariable("CAPSTONE_ARTIFACTS");
            string project = Path.GetFullPath(Directory.GetCurrentDirectory());
            if (!Application.isBatchMode || Application.isPlaying || Application.companyName != "CapstoneSyntheticValidation"
                || project.IndexOf(Path.DirectorySeparatorChar + "artifacts" + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) < 0
                || string.IsNullOrWhiteSpace(artifacts) || SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
                throw new InvalidOperationException("A copied synthetic graphics batch project and explicit output are required.");
            output = Path.Combine(Path.GetFullPath(artifacts), "reduced-motion-validation");
            if (Directory.Exists(output) && Directory.EnumerateFileSystemEntries(output).Any())
                throw new InvalidOperationException("Use a fresh output directory.");
            Directory.CreateDirectory(output);
            report = new Report { utc = DateTimeOffset.UtcNow.ToString("O"), unityVersion = Application.unityVersion };
            // This identity check precedes every preference access. Only the
            // copied project's own setting is temporarily changed and restored.
            bool hadPreference = PlayerPrefs.HasKey(Preference);
            int originalPreference = PlayerPrefs.GetInt(Preference, 0);
            bool originalReducedMotion = FigmaMissionUi.ReducedMotion;
            try
            {
                AndroidPlatformBridge.UseMockProvider();
                Run("Legacy saved reduced motion cannot enable the runtime mode", () =>
                {
                    PlayerPrefs.SetInt(Preference, 1);
                    PlayerPrefs.Save();
                    Assert(!FigmaMissionUi.ReducedMotion, "Normal motion remains the default even when the copied project's legacy preference is enabled.");
                });
                Run("Normal motion retains orbit, white stage and celebration", () => Validate(false, false));
                Run("Reduced motion keeps a static camera and quiet committed reveal", () => Validate(true, false));
                Run("Reduced-motion interruption restores state and releases UI once", () => Validate(true, true));
            }
            finally
            {
                FigmaMissionUi.ReducedMotion = originalReducedMotion;
                report.runtimeMotionRestored = FigmaMissionUi.ReducedMotion == originalReducedMotion;
                if (hadPreference) PlayerPrefs.SetInt(Preference, originalPreference); else PlayerPrefs.DeleteKey(Preference);
                PlayerPrefs.Save();
                report.preferenceRestored = PlayerPrefs.HasKey(Preference) == hadPreference
                    && PlayerPrefs.GetInt(Preference, 0) == originalPreference;
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                File.WriteAllText(Path.Combine(output, "report.json"), JsonUtility.ToJson(report, true));
            }
            if (report.failed > 0 || !report.preferenceRestored || !report.runtimeMotionRestored) throw new InvalidOperationException("Reduced-motion validation failed; inspect its report.");
        }

        static void Run(string name, Action action)
        {
            try { action(); report.passed++; report.checks.Add(new Check { name = name, status = "pass" }); }
            catch (Exception error) { report.failed++; report.checks.Add(new Check { name = name, status = "fail", detail = error.ToString() }); }
        }
        static void Assert(bool condition, string message)
        {
            report.assertions++;
            if (!condition) throw new InvalidOperationException(message);
        }

        static void Validate(bool reduced, bool interrupt)
        {
            FigmaMissionUi.ReducedMotion = reduced;
            EditorSceneManager.OpenScene("Assets/Scenes/MockupMain.unity", OpenSceneMode.Single);
            var nav = UnityEngine.Object.FindFirstObjectByType<MockupNavigation>(FindObjectsInactive.Include);
            var ui = nav.activitiesPanel.GetComponent<WeekOneQuestDemo>();
            var store = new Store();
            using var service = new GardenStateService(store, new Clock(), new DemoBalanceConfig { VisitorChancePercent = 0 }, new LegacySnapshot { Nutrient = 137 });
            ui.Initialize(service, true);
            Assert(service.SelectMbctProgram("8w"), service.LastError);
            Assert(service.BeginSession("synthetic-reduced-motion", "free-mission:M03", 0)
                && service.StartSession("synthetic-reduced-motion"), service.LastError);
            var before = service.Snapshot;
            Assert(service.CompleteSession("synthetic-reduced-motion"), service.LastError);
            var after = service.Snapshot;
            Assert(after.PlantAppearance.Recipe.GrowthStep == before.PlantAppearance.Recipe.GrowthStep + 1,
                "Fixture earns one real committed growth step before any presentation.");
            nav.ShowIsland();
            var home = UnityEngine.Object.FindFirstObjectByType<GardenHomePresenter>(FindObjectsInactive.Include);
            if (!(bool)typeof(MonoBehaviour).GetProperty("didAwake").GetValue(home))
                typeof(GardenHomePresenter).GetMethod("Awake", Private).Invoke(home, null);
            home.RefreshFromState(before);
            Canvas.ForceUpdateCanvases();
            var camera = home.gardenCamera;
            Assert(camera != null && camera.targetTexture != null && home.ProceduralPlant != null, "Real garden camera, RenderTexture and procedural plant exist.");
            var position = camera.transform.position; var rotation = camera.transform.rotation;
            float size = camera.orthographicSize; var target = camera.targetTexture; var active = RenderTexture.active;
            int cameraCount = UnityEngine.Object.FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length;
            var canvases = UnityEngine.Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .ToDictionary(canvas => canvas, canvas => canvas.enabled);
            string saved = StateCodec.Encode(service.Snapshot); int writes = store.Writes, callbacks = 0;
            var sequence = home.gameObject.AddComponent<GardenMissionGrowthSequence>();
            var begin = typeof(GardenMissionGrowthSequence).GetMethod("Begin", Private);
            var frame = typeof(GardenMissionGrowthSequence).GetMethod("RenderFrame", Private);
            try
            {
                begin.Invoke(sequence, new object[] { home, before, after, (Action)(() => callbacks++) });
                Assert(sequence.IsPlaying && sequence.UsesReducedMotion == reduced
                    && Mathf.Approximately(sequence.PlaybackDuration, reduced ? 1.2f : 4.6f), "The runtime mode selects the correct playback duration.");
                foreach (float progress in interrupt ? new[] { .2f, .45f } : new[] { .2f, .49f, .7f, .95f })
                {
                    frame.Invoke(sequence, new object[] { progress });
                    Assert(sequence.OrdinaryUiHidden && camera.targetTexture == target, "Both modes hide ordinary UI and retain the original render target.");
                    Assert(StateCodec.Encode(service.Snapshot) == saved && store.Writes == writes, "Sampling growth never writes records, progression or currency.");
                    var materials = home.ProceduralPlant.GetComponentsInChildren<Renderer>(true).SelectMany(renderer => renderer.sharedMaterials).ToArray();
                    if (reduced)
                    {
                        Assert(Vector3.Distance(position, camera.transform.position) < .001f && Quaternion.Angle(rotation, camera.transform.rotation) < .01f
                            && Mathf.Approximately(camera.orthographicSize, size), "Reduced motion has no orbit, pan or zoom at any sampled stage.");
                        Assert(sequence.PeakParticleCount == 0 && !sequence.GetComponentsInChildren<GardenGrowthEffect>(true).Any()
                            && !materials.Any(material => material.shader.name == "Capstone/Plant Growth White"), "Quiet presentation allocates no effect and never flashes plant materials.");
                        var image = UnityEngine.Object.FindObjectsByType<RawImage>(FindObjectsSortMode.None).Single(item => item.name == "Unobstructed garden");
                        Assert(image.color.r >= .92f && image.color.r <= 1 && Mathf.Approximately(image.color.a, 1), "Quiet reveal uses only gentle bounded dimming, with no bright flash.");
                    }
                    else
                    {
                        Assert(Quaternion.Angle(rotation, camera.transform.rotation) > 1, "Normal cinematic retains its moving camera.");
                        if (Mathf.Approximately(progress, .49f)) Assert(materials.All(material => material.shader.name == "Capstone/Plant Growth White"), "Normal pre-reveal frame keeps its white silhouette.");
                        if (Mathf.Approximately(progress, .7f)) Assert(sequence.PeakParticleCount > 56, "Normal reveal keeps its celebration burst.");
                    }
                    Assert(home.RenderedRecipe.GrowthStep == (progress < .56f ? before : after).PlantAppearance.Recipe.GrowthStep,
                        "The model changes at the reveal boundary to the already committed stage.");
                    if (!interrupt && (Mathf.Approximately(progress, .49f) || Mathf.Approximately(progress, .7f)))
                        Capture(target, (reduced ? "reduced" : "normal") + (progress < .56f ? "-before.png" : "-after.png"));
                }
                if (interrupt) typeof(GardenMissionGrowthSequence).GetMethod("OnApplicationPause", Private).Invoke(sequence, new object[] { true });
                else sequence.Interrupt();
                sequence.Interrupt();
                Assert(!sequence.IsPlaying && callbacks == 1, "Finishing or interrupting releases its continuation exactly once.");
                Assert(Vector3.Distance(position, camera.transform.position) < .001f && Quaternion.Angle(rotation, camera.transform.rotation) < .01f
                    && Mathf.Approximately(camera.orthographicSize, size) && camera.targetTexture == target && RenderTexture.active == active,
                    "Camera pose, target and active RenderTexture are restored exactly.");
                Assert(canvases.All(item => item.Key == null || item.Key.enabled == item.Value), "Every surviving ordinary canvas restores its prior enabled state.");
                Assert(home.RenderedRecipe.GrowthStep == after.PlantAppearance.Recipe.GrowthStep
                    && StateCodec.Encode(service.Snapshot) == saved && store.Writes == writes, "Interruption shows the committed plant without another save or reward.");
                Assert(UnityEngine.Object.FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length == cameraCount,
                    "Neither growth mode adds a camera.");
                Assert(!UnityEngine.Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                    .Any(item => item.name == "Mission growth effects (temporary)" || item.name == "Mission growth full-screen garden"), "Temporary presentation roots are released.");
            }
            finally { sequence.Interrupt(); }
        }

        static void Capture(RenderTexture target, string name)
        {
            var previous = RenderTexture.active;
            var texture = new Texture2D(target.width, target.height, TextureFormat.RGB24, false);
            try
            {
                RenderTexture.active = target; texture.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0); texture.Apply();
                File.WriteAllBytes(Path.Combine(output, name), texture.EncodeToPNG());
            }
            finally { RenderTexture.active = previous; UnityEngine.Object.DestroyImmediate(texture); }
        }
    }
}
