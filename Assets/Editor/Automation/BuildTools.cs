using System;
using System.IO;
using System.Linq;
using Mono.Cecil;
using UnityEditor.Build.Player;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.SceneManagement;
using CapstoneDesign.Runtime;

namespace CapstoneDesign.EditorTools
{
    public static class BuildTools
    {
        private const string ScenePath = "Assets/Scenes/MockupMain.unity";
        private const string PackageName = "com.capstonedesign2026.mockup";

        [MenuItem("Capstone Mockup/Validate Project")]
        public static void ValidateProject()
        {
            if (!File.Exists(Path.Combine(Directory.GetCurrentDirectory(), ScenePath)))
            {
                throw new InvalidOperationException("Mockup scene is missing. Run Capstone Mockup/Generate All first.");
            }

            Scene scene = EditorSceneManagerProxy.OpenScene(ScenePath);
            GameObject camera = GameObject.Find("MockupCamera");
            GameObject island = GameObject.Find("FloatingIsland");
            GameObject canvas = GameObject.Find("UiCanvas");
            if (camera == null || island == null || canvas == null)
            {
                throw new InvalidOperationException("Generated scene is missing camera, island, or UI canvas.");
            }

            if (GameObject.Find("MoonLight") == null)
            {
                throw new InvalidOperationException("Generated scene is missing the single moon light.");
            }

            int lightCount = UnityEngine.Object.FindObjectsByType<Light>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length;
            if (lightCount != 1)
            {
                throw new InvalidOperationException("Integrated garden expects only the original light, found " + lightCount + ".");
            }

            var home = canvas.GetComponent<GardenHomePresenter>();
            if (home == null || home.homeCanvas == null || home.plant == null || home.activity == null
                || home.navigation == null || home.gardenVisuals == null || home.shopButton == null
                || home.gardenCamera == null || home.gardenCamera.enabled || home.missionButton != null
                || home.homeCanvas.GetComponentInChildren<CapstoneDesign.Prototype.PrototypeController>(true) != null)
                throw new InvalidOperationException("Integrated home is missing visual links or still runs the prototype controller.");

            if (home.gardenRoot == null || home.gardenRoot.GetComponent<GardenSurface>() == null)
                throw new InvalidOperationException("Integrated home must use the grounded Kenney garden prefab.");
            foreach (var renderer in home.gardenRoot.GetComponentsInChildren<Renderer>(true))
            {
                if (!renderer.receiveShadows || renderer.shadowCastingMode == UnityEngine.Rendering.ShadowCastingMode.Off)
                    throw new InvalidOperationException("Garden model cannot receive/cast real shadows: " + renderer.name);
                foreach (var material in renderer.sharedMaterials)
                    if (material == null || material.shader.name.Contains("Unlit"))
                        throw new InvalidOperationException("Garden model has an unlit or missing material: " + renderer.name);
            }
            foreach (string asset in new[] { "GardenBase", "FlowerBed", "RestCorner", "StreetLamp" })
                if (Resources.Load<GameObject>(GardenSceneModels.ResourceRoot + asset) == null)
                    throw new InvalidOperationException("Missing garden resource: " + asset);
            for (int i = 1; i <= 12; i++)
                if (Resources.Load<GameObject>(GardenSceneModels.ResourceRoot + "Visitor" + i.ToString("00")) == null)
                    throw new InvalidOperationException("Missing saved visitor model: " + i);
            if (Resources.Load<Shader>("GardenAtmosphere/Sky") == null || Resources.Load<Shader>("GardenAtmosphere/Cloud") == null)
                throw new InvalidOperationException("Garden environment shaders are missing.");
            if (Resources.Load<UnityEngine.Rendering.VolumeProfile>("GardenAtmosphere/PostProcessing") == null)
                throw new InvalidOperationException("Garden post-processing profile is missing.");

            var music = Resources.Load<GardenMusicProfile>("Audio/GardenMusicProfile");
            if (music == null)
                throw new InvalidOperationException("Background music profile is missing.");
            foreach (GardenMusicContext context in Enum.GetValues(typeof(GardenMusicContext)))
            {
                var clip = music.TrackFor(context);
                if (clip == null || clip.length <= 0 || clip.channels < 1)
                    throw new InvalidOperationException("Background music is missing or invalid for " + context + ".");
            }

            ValidateAndroidPlatformLayer();

            Debug.Log("Mockup validation passed: " + scene.path + " / package " + PackageName);
        }

        [Serializable]
        private sealed class ReleaseScriptReport
        {
            public string status = "FAIL", target = "Android", options = "None", error;
            public string[] assemblies = Array.Empty<string>(), excludedMethods = Array.Empty<string>();
        }

        [MenuItem("Capstone Mockup/Validate Android Release Scripts")]
        public static void ValidateAndroidReleaseScripts()
        {
            string artifacts = Environment.GetEnvironmentVariable("CAPSTONE_ARTIFACTS");
            if (string.IsNullOrWhiteSpace(artifacts))
                throw new InvalidOperationException("Use a new explicit CAPSTONE_ARTIFACTS directory for release-script evidence.");
            string output = Path.Combine(artifacts, "release-scripts");
            if (Directory.Exists(output))
                throw new InvalidOperationException("Release-script output already exists; choose a fresh evidence directory.");
            Directory.CreateDirectory(output);
            var evidence = new ReleaseScriptReport();
            try
            {
                // Compile the actual Android player without UNITY_EDITOR or DEVELOPMENT_BUILD.
                // This verifies conditional compilation; it does not claim an APK or device run.
                var result = PlayerBuildInterface.CompilePlayerScripts(new ScriptCompilationSettings
                {
                    target = BuildTarget.Android,
                    group = BuildTargetGroup.Android,
                    options = ScriptCompilationOptions.None
                }, output);
                evidence.assemblies = result.assemblies?.ToArray() ?? Array.Empty<string>();
                string runtime = Path.Combine(output, "Assembly-CSharp.dll");
                if (!evidence.assemblies.Any(path => Path.GetFileName(path) == "Assembly-CSharp.dll") || !File.Exists(runtime))
                    throw new InvalidOperationException("Android release compilation did not produce the runtime assembly.");
                using (var assembly = AssemblyDefinition.ReadAssembly(runtime))
                {
                    var settings = assembly.MainModule.Types.FirstOrDefault(type => type.FullName == "CapstoneDesign.Runtime.SensorRawDisplay");
                    if (settings == null) throw new InvalidOperationException("Release runtime omits SensorRawDisplay.");
                    string[] developmentOnly = { "StartService", "StopService", "RequestMissionReset", "CancelMissionReset",
                        "ResetMissionRecords", "AdvanceMissionDate", "ToggleDebugWalk", "AddVirtualStep" };
                    foreach (string method in developmentOnly)
                        if (settings.Methods.Any(item => item.Name == method))
                            throw new InvalidOperationException("Development-only settings action remains in release: " + method);
                    var opened = settings.Methods.Single(item => item.Name == "OnSettingsOpened");
                    if (opened.Body.Instructions.Any(instruction => instruction.Operand is MethodReference call && call.Name == "CaptureDebugSensors"))
                        throw new InvalidOperationException("Release settings still trigger debug sensor capture.");
                    evidence.excludedMethods = developmentOnly;
                }
                evidence.status = "PASS";
                Debug.Log("ANDROID_RELEASE_SCRIPTS_PASS assemblies=" + evidence.assemblies.Length
                    + " developmentOnlyMethodsExcluded=" + evidence.excludedMethods.Length);
            }
            catch (Exception error) { evidence.error = error.ToString(); throw; }
            finally { File.WriteAllText(Path.Combine(artifacts, "release-scripts-report.json"), JsonUtility.ToJson(evidence, true)); }
        }

        [MenuItem("Capstone Mockup/Build Android")]
        public static void BuildAndroid()
        {
            ValidateProject();
            string artifacts = Environment.GetEnvironmentVariable("CAPSTONE_ARTIFACTS");
            if (string.IsNullOrWhiteSpace(artifacts))
            {
                artifacts = "/artifacts";
            }

            string buildDirectory = Path.Combine(artifacts, "build");
            Directory.CreateDirectory(buildDirectory);
            string outputPath = Path.Combine(buildDirectory, "capstone-mockup.apk");

            BuildPlayerOptions options = new BuildPlayerOptions
            {
                scenes = new[] { ScenePath },
                locationPathName = outputPath,
                target = BuildTarget.Android,
                options = BuildOptions.Development | BuildOptions.AllowDebugging
            };

            BuildReport report = BuildPipeline.BuildPlayer(options);
            if (report.summary.result != BuildResult.Succeeded)
            {
                throw new InvalidOperationException("Android build failed: " + report.summary.result + "\n" + report.summary.totalErrors + " errors");
            }

            Debug.Log("Android APK written to " + outputPath + " (" + report.summary.totalSize + " bytes)");
        }

        [MenuItem("Capstone Mockup/Run Validation")]
        public static void RunValidation()
        {
            ValidateProject();
        }

        private static class EditorSceneManagerProxy
        {
            public static Scene OpenScene(string path)
            {
                return UnityEditor.SceneManagement.EditorSceneManager.OpenScene(path);
            }
        }

        private static void ValidateAndroidPlatformLayer()
        {
            string[] requiredFiles =
            {
                "Assets/Plugins/Android/CapstonePlatform.androidlib/build.gradle",
                "Assets/Plugins/Android/CapstonePlatform.androidlib/src/main/AndroidManifest.xml",
                "Assets/Plugins/Android/CapstonePlatform.androidlib/src/main/kotlin/com/capstonedesign2026/platform/AndroidPlatformBridge.kt",
                "Assets/Plugins/Android/CapstonePlatform.androidlib/src/main/kotlin/com/capstonedesign2026/platform/SensorForegroundService.kt",
                "Assets/Plugins/Android/CapstonePlatform.androidlib/src/main/kotlin/com/capstonedesign2026/platform/HealthRepository.kt",
                "Assets/Plugins/Android/gradleTemplate.properties"
            };
            foreach (string path in requiredFiles)
            {
                if (!File.Exists(path))
                {
                    throw new InvalidOperationException("Android platform layer file is missing: " + path);
                }
            }

            GameObject sensorPanel = GameObject.Find("SensorPanel");
            SensorRawDisplay display = sensorPanel == null ? null : sensorPanel.GetComponent<SensorRawDisplay>();
            if (display == null || display.output == null || display.startServiceButton == null ||
                display.stopServiceButton == null || display.sensorPermissionButton == null ||
                display.healthPermissionButton == null || display.refreshHealthButton == null)
            {
                throw new InvalidOperationException("Android debug panel or one of its controls is not wired.");
            }

            GameObject activitiesPanel = GameObject.Find("ActivitiesPanel");
            WeekOneQuestDemo questDemo = activitiesPanel == null
                ? null
                : activitiesPanel.GetComponent<WeekOneQuestDemo>();
            if (questDemo == null || questDemo.questButtons.Length != 4 ||
                questDemo.questLabels.Length != 4 || questDemo.summary == null)
            {
                throw new InvalidOperationException("Week 1 quest POC is not wired to the activity panel.");
            }
            string questScriptPath = AssetDatabase.GetAssetPath(MonoScript.FromMonoBehaviour(questDemo));
            if (questScriptPath != "Assets/Scripts/Runtime/WeekOneQuestDemo.cs")
            {
                throw new InvalidOperationException(
                    "WeekOneQuestDemo must remain in its matching script file to keep player scene serialization stable.");
            }
            foreach (QuestDefinition quest in WeekOneQuestLibrary.Create())
            {
                if (quest.reward.nutrient < 0 || quest.reward.gardenXp < 0 || quest.reward.growth < 0f)
                {
                    throw new InvalidOperationException("Quest rewards must remain non-punitive: " + quest.id);
                }
                if (quest.weekly && string.IsNullOrWhiteSpace(quest.reward.unlock))
                {
                    throw new InvalidOperationException("Weekly quest must provide an unlock reward: " + quest.id);
                }
            }

            AndroidPlatformSnapshot mock = new MockPlatformDataProvider().GetSnapshot();
            if (mock.inputMode != "MOCK" || mock.sensorService?.sensors == null ||
                mock.sensorService.sensors.Length == 0)
            {
                throw new InvalidOperationException("Mock platform provider did not return a usable snapshot.");
            }

            string replayPath = Path.Combine(Path.GetTempPath(), "capstone-platform-validation.jsonl");
            if (File.Exists(replayPath)) File.Delete(replayPath);
            using (RecordingPlatformDataProvider recording = new RecordingPlatformDataProvider(
                       new MockPlatformDataProvider(), replayPath))
            {
                recording.GetSnapshot();
                recording.GetSnapshot();
            }
            ReplayPlatformDataProvider replay = new ReplayPlatformDataProvider(replayPath, loop: false);
            AndroidPlatformSnapshot replaySnapshot = replay.GetSnapshot();
            File.Delete(replayPath);
            if (replaySnapshot.inputMode != "REPLAY")
            {
                throw new InvalidOperationException("Replay provider did not return replay data.");
            }
        }
    }
}
