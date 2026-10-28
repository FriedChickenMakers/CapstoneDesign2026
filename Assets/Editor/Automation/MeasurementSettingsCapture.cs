using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using CapstoneDesign.Runtime;
using CapstoneDesign.Runtime.Personalization;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace CapstoneDesign.EditorTools
{
    // Enrollment is managed outside the app. Exercise the actual settings view
    // with and without a synthetic enrollment, without contacting an endpoint
    // or reading real participant data. This is UI evidence, not transport QA.
    [InitializeOnLoad]
    public static class MeasurementSettingsCapture
    {
        const string Key = "Capstone.MeasurementSettingsCapture";
        const BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;
        static readonly Vector2Int[] Sizes = { new Vector2Int(900, 1950), new Vector2Int(900, 1600), new Vector2Int(1200, 800) };
        [Serializable] sealed class Report
        {
            public string status, error, renderer;
            public bool synthetic = true, deviceTested, networkEnabled;
            public List<string> checks = new List<string>(), images = new List<string>();
        }
        sealed class Store : IEventQueueStore
        {
            EventQueueState state = new EventQueueState();
            public EventQueueState Load() => state;
            public void Save(EventQueueState value) { state = value; }
        }
        static Report report;
        static PersonalizationClient client;
        static int scenarioIndex, previousNavigationId;
        static string scenario;
        static string Root => Environment.GetEnvironmentVariable("CAPSTONE_ARTIFACTS");
        static MeasurementSettingsCapture()
        {
            if (SessionState.GetBool(Key, false)) EditorApplication.update += WaitForPlay;
        }
        public static void Start()
        {
            if (string.IsNullOrEmpty(Root) || string.IsNullOrEmpty(Environment.GetEnvironmentVariable("CAPSTONE_PERSONALIZATION_TEST_ROOT"))
                || string.IsNullOrEmpty(Environment.GetEnvironmentVariable("CAPSTONE_SMOKE_SAVE_ROOT")))
                throw new InvalidOperationException("Fresh isolated capture, enrollment and garden directories are required.");
            string enrollmentRoot = Environment.GetEnvironmentVariable("CAPSTONE_PERSONALIZATION_TEST_ROOT");
            foreach (var directory in new[] { enrollmentRoot, Environment.GetEnvironmentVariable("CAPSTONE_SMOKE_SAVE_ROOT") })
            {
                if (Directory.Exists(directory) && Directory.EnumerateFileSystemEntries(directory).Any())
                    throw new InvalidOperationException("Synthetic enrollment and garden directories must be empty.");
                Directory.CreateDirectory(directory);
            }
            Directory.CreateDirectory(Path.Combine(Root, "visual"));
            SessionState.SetBool(Key, true);
            SessionState.SetFloat(Key + ".Deadline", (float)EditorApplication.timeSinceStartup + 180);
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            EditorApplication.update -= WaitForPlay;
            EditorApplication.update += WaitForPlay;
            EditorApplication.EnterPlaymode();
        }
        static void WaitForPlay()
        {
            if (EditorApplication.timeSinceStartup > SessionState.GetFloat(Key + ".Deadline", 0))
            { Finish(new TimeoutException()); return; }
            if (!EditorApplication.isPlaying) return;
            EditorApplication.update -= WaitForPlay;
            report = new Report { renderer = SystemInfo.graphicsDeviceName };
            try
            {
                Application.runInBackground = true;
                AndroidPlatformBridge.UseMockProvider();
                scenarioIndex = 0;
                BeginScenario();
            }
            catch (Exception error) { Finish(error); }
        }
        static void Check(bool ok, string name)
        {
            if (!ok) throw new InvalidOperationException(name);
            report.checks.Add(name);
        }
        static void BeginScenario()
        {
            scenario = new[] { "unregistered", "registered" }[scenarioIndex];
            var previous = UnityEngine.Object.FindFirstObjectByType<MockupNavigation>(FindObjectsInactive.Include);
            previousNavigationId = previous == null ? 0 : previous.GetInstanceID();
            var existing = UnityEngine.Object.FindObjectsByType<PersonalizationClient>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var value in existing) UnityEngine.Object.DestroyImmediate(value.gameObject);
            client = null;
            bool registered = scenario == "registered";
            if (registered)
            {
                client = PersonalizationClient.Instance;
                client.enabled = false;
                Check(!client.Configured, scenario + "-isolated-enrollment-starts-empty");
                // This fixture follows normal empty enrollment initialization;
                // only its private in-memory state is replaced. No UI is faked.
                string token = Guid.NewGuid().ToString("N") + Guid.NewGuid().ToString("N");
                const string endpoint = "https://measurement-capture.invalid";
                var field = typeof(PersonalizationClient).GetField("enrollment", Hidden);
                field.SetValue(client, JsonUtility.FromJson("{\"endpoint\":\"" + endpoint + "\",\"token\":\"" + token + "\"}", field.FieldType));
                var queue = new EventQueue(new Store());
                queue.Bind(EventQueue.Fingerprint(endpoint, token));
                long now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                queue.SetConsent(true, now);
                queue.EnableMeasurements(now);
                typeof(PersonalizationClient).GetField("queue", Hidden).SetValue(client, queue);
            }
            // The unregistered scenario deliberately creates no client here.
            // Verify the real scene bootstraps it before any settings/activity
            // navigation or Update; Instance must not mask a missing startup hook.
            SceneManager.sceneLoaded += OnMainSceneLoaded;
            EditorSceneManager.LoadSceneInPlayMode("Assets/Scenes/MockupMain.unity", new LoadSceneParameters(LoadSceneMode.Single));
        }
        static void OnMainSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (scene.path != "Assets/Scenes/MockupMain.unity") return;
            SceneManager.sceneLoaded -= OnMainSceneLoaded;
            try
            {
                var nav = UnityEngine.Object.FindFirstObjectByType<MockupNavigation>(FindObjectsInactive.Include);
                var startedClient = UnityEngine.Object.FindFirstObjectByType<PersonalizationClient>(FindObjectsInactive.Include);
                Check(startedClient != null, scenario + "-client-present-at-scene-bootstrap");
                if (client != null) Check(startedClient == client, scenario + "-existing-client-preserved");
                client = startedClient;
                client.enabled = false;
                Check(nav != null && nav.home != null && nav.home.IsGardenVisible
                    && !nav.activitiesPanel.activeSelf && !nav.settingsPanel.activeSelf,
                    scenario + "-bootstrap-before-settings-or-activity-navigation");
                bool registered = scenario == "registered";
                Check(client.Configured == registered && client.Consented == registered
                    && client.MeasurementConsented == registered, scenario + "-fixture-state");
                EditorApplication.update += CaptureLoadedScene;
            }
            catch (Exception error) { Finish(error); }
        }
        static void CaptureLoadedScene()
        {
            if (EditorApplication.timeSinceStartup > SessionState.GetFloat(Key + ".Deadline", 0))
            { Finish(new TimeoutException()); return; }
            var nav = UnityEngine.Object.FindFirstObjectByType<MockupNavigation>(FindObjectsInactive.Include);
            if (nav == null || nav.GetInstanceID() == previousNavigationId) return;
            EditorApplication.update -= CaptureLoadedScene;
            try
            {
                CaptureScenario(nav);
                scenarioIndex++;
                if (scenarioIndex < 2) BeginScenario();
                else Finish(null);
            }
            catch (Exception error) { Finish(error); }
        }
        static void CaptureScenario(MockupNavigation nav)
        {
            Check(nav != null, scenario + "-real-scene-loaded");
            var sensor = nav.GetComponentInChildren<SensorRawDisplay>(true);
            Check(sensor != null, scenario + "-settings-present");
            sensor.PrepareView();
            nav.ShowSettings();
            var content = sensor.PrimaryScroll.content;
            Check(!content.GetComponentsInChildren<Transform>(true).Any(t => t.name == "Recommendation participation"),
                scenario + "-no-participation-section");
            string copy = string.Join("\n", content.GetComponentsInChildren<Text>(true).Select(t => t.text));
            Check(!new[] { "QA·베타 측정 참여", "활동 이용 기록 공유", "추가 항목 확인", "알림과 전체 활동 측정에 동의",
                "추천 개선에 참여 중", "알림·활동 측정에 참여 중", "기기 대기 기록 삭제됨", "서버 삭제 대기" }.Any(copy.Contains),
                scenario + "-no-consent-controls-or-status");
            foreach (string name in new[] { "Activity reminders", "Garden preferences", "Health records", "Permissions" })
            {
                var section = content.Find(name);
                Check(section != null && section.gameObject.activeInHierarchy, scenario + "-normal-section-" + name);
            }
            Check(new[] { "활동 추천 알림", "배경음악", "정원의 시간", "심박 차트 보기", "앱 연결 및 권한" }.All(copy.Contains),
                scenario + "-normal-settings-controls-intact");
            CaptureSettings(sensor);
            Check(client == PersonalizationClient.Instance && client.PendingCount == 0 && !client.enabled,
                scenario + "-no-data-or-network");
        }
        static void CaptureSettings(SensorRawDisplay sensor)
        {
            var scroll = sensor.PrimaryScroll;
            foreach (var size in Sizes)
            {
                string name = scenario + "-settings-top-" + size.x + "x" + size.y + ".png";
                string path = Path.Combine(Root, "visual", name);
                LocalLoopPreview.Render(size, path);
                scroll.StopMovement();
                scroll.verticalNormalizedPosition = 1;
                scroll.Rebuild(CanvasUpdate.PostLayout);
                scroll.SendMessage("LateUpdate", SendMessageOptions.RequireReceiver);
                LocalLoopPreview.Render(size, path);
                foreach (var text in scroll.content.GetComponentsInChildren<Text>())
                    if (text.text.Length > 0 && text.rectTransform.rect.height + 2 < text.preferredHeight)
                        throw new InvalidOperationException("Settings text height overflow: " + name + " / " + text.name);
                report.images.Add(name);
            }
        }
        static void Finish(Exception error)
        {
            SessionState.SetBool(Key, false);
            SceneManager.sceneLoaded -= OnMainSceneLoaded;
            EditorApplication.update -= WaitForPlay;
            EditorApplication.update -= CaptureLoadedScene;
            if (report == null) report = new Report();
            report.status = error == null ? "PASS" : "FAIL";
            report.error = error == null ? null : error.GetType().Name + ": " + error.Message;
            File.WriteAllText(Path.Combine(Root, "result.json"), JsonUtility.ToJson(report, true));
            Debug.Log("MEASUREMENT_SETTINGS_CAPTURE_" + report.status + " checks=" + report.checks.Count + " images=" + report.images.Count);
            EditorApplication.Exit(error == null ? 0 : 1);
        }
    }
}
