using System;
using System.IO;
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

            int lightCount = UnityEngine.Object.FindObjectsByType<Light>(FindObjectsSortMode.None).Length;
            if (lightCount != 1)
            {
                throw new InvalidOperationException("Mockup expects exactly one Light, found " + lightCount + ".");
            }

            ValidateAndroidPlatformLayer();

            Debug.Log("Mockup validation passed: " + scene.path + " / package " + PackageName);
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
