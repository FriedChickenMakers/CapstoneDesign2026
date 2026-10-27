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
    // Isolated play-mode fixtures exercise the actual settings view. The client
    // Update is disabled before a player frame; no endpoint is contacted and no
    // real enrollment, behavior state or participant data is read.
    [InitializeOnLoad]
    public static class MeasurementConsentCapture
    {
        const string Key = "Capstone.MeasurementConsentCapture";
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
        static MeasurementConsentCapture()
        {
            if (SessionState.GetBool(Key, false)) EditorApplication.update += WaitForPlay;
        }
        public static void Start()
        {
            if (string.IsNullOrEmpty(Root) || string.IsNullOrEmpty(Environment.GetEnvironmentVariable("CAPSTONE_PERSONALIZATION_TEST_ROOT"))
                || string.IsNullOrEmpty(Environment.GetEnvironmentVariable("CAPSTONE_SMOKE_SAVE_ROOT")))
                throw new InvalidOperationException("Fresh isolated capture, enrollment and garden directories are required.");
            string enrollmentRoot = Environment.GetEnvironmentVariable("CAPSTONE_PERSONALIZATION_TEST_ROOT");
            if (Directory.Exists(enrollmentRoot) && Directory.EnumerateFileSystemEntries(enrollmentRoot).Any())
                throw new InvalidOperationException("The synthetic enrollment directory must be empty.");
            Directory.CreateDirectory(enrollmentRoot);
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
            scenario = new[] { "opt-out", "legacy", "opt-in" }[scenarioIndex];
            var previous = UnityEngine.Object.FindFirstObjectByType<MockupNavigation>(FindObjectsInactive.Include);
            previousNavigationId = previous == null ? 0 : previous.GetInstanceID();
            var existing = UnityEngine.Object.FindObjectsByType<PersonalizationClient>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var value in existing) UnityEngine.Object.DestroyImmediate(value.gameObject);
            client = PersonalizationClient.Instance;
            client.enabled = false;
            // Deliberately non-routable fixture identity. No universal token is
            // embedded; the in-memory value lasts only for this capture run.
            string token = Guid.NewGuid().ToString("N") + Guid.NewGuid().ToString("N");
            const string endpoint = "https://measurement-capture.invalid";
            var field = typeof(PersonalizationClient).GetField("enrollment", Hidden);
            field.SetValue(client, JsonUtility.FromJson("{\"endpoint\":\"" + endpoint + "\",\"token\":\"" + token + "\"}", field.FieldType));
            var queue = new EventQueue(new Store());
            queue.Bind(EventQueue.Fingerprint(endpoint, token));
            if (scenario != "opt-out") queue.SetConsent(true, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
            if (scenario == "opt-in") queue.EnableMeasurements(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
            typeof(PersonalizationClient).GetField("queue", Hidden).SetValue(client, queue);
            typeof(PersonalizationClient).GetField("status", Hidden).SetValue(client, scenario == "opt-out" ? "참여 안 함" : "추천 개선에 참여 중");
            Check(client.Configured && client.Consented == (scenario != "opt-out")
                && client.MeasurementConsented == (scenario == "opt-in"), scenario + "-initial-consent-state");
            EditorSceneManager.LoadSceneInPlayMode("Assets/Scenes/MockupMain.unity", new LoadSceneParameters(LoadSceneMode.Single));
            EditorApplication.update += CaptureLoadedScene;
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
                if (scenarioIndex < 3) BeginScenario();
                else Finish(null);
            }
            catch (Exception error) { Finish(error); }
        }
        static void CaptureScenario(MockupNavigation nav)
        {
            Check(nav != null, scenario + "-real-scene-loaded");
            var sensor = nav.GetComponentInChildren<SensorRawDisplay>(true);
            sensor.PrepareView();
            nav.ShowSettings();
            var section = sensor.PrimaryScroll.content.Find("Recommendation participation") as RectTransform;
            Check(section != null, scenario + "-measurement-section-present");
            string copy = string.Join("\n", section.GetComponentsInChildren<Text>(true).Select(t => t.text));
            Check(copy.Contains("90일") && copy.Contains("참여하지 않아도") && copy.Contains("일기·감정 내용")
                && copy.Contains("알림 예정·게시 수락·클릭 시각"), scenario + "-disclosure-present");
            var upgrade = section.GetComponentsInChildren<Button>(true).FirstOrDefault(b =>
                b.GetComponentsInChildren<Text>(true).Any(t => t.text.Contains("추가 항목 확인")));
            Check((upgrade != null) == (scenario == "legacy"), scenario + "-legacy-opt-in-visibility");
            CaptureSection(scenario, sensor, section);
            if (scenario == "legacy")
            {
                upgrade.onClick.Invoke();
                Check(client.MeasurementConsented, "legacy-explicit-upgrade-grants-new-consent");
                Check(!upgrade.gameObject.activeInHierarchy, "legacy-upgrade-action-hidden-after-consent");
                CaptureSection("legacy-after-explicit-opt-in", sensor, section);
            }
            if (scenario == "opt-out")
            {
                var toggle = section.GetComponentsInChildren<Button>(true).First(b =>
                    b.GetComponentsInChildren<Text>(true).Any(t => t.text == "활동 이용 기록 공유"));
                toggle.onClick.Invoke();
                Check(client.Consented && client.MeasurementConsented, "new-explicit-opt-in-grants-disclosed-consent");
                CaptureSection("new-after-explicit-opt-in", sensor, section);
            }
            if (scenario == "opt-in")
            {
                var toggle = section.GetComponentsInChildren<Button>(true).First(b =>
                    b.GetComponentsInChildren<Text>(true).Any(t => t.text == "활동 이용 기록 공유"));
                toggle.onClick.Invoke();
                Check(!client.Consented && !client.MeasurementConsented && client.DeletionPending,
                    "withdrawal-disables-both-consents-and-requests-deletion");
                CaptureSection("after-withdrawal", sensor, section);
            }
            Check(client.PendingCount == 0 && !client.enabled, scenario + "-no-data-or-network");
        }
        static void CaptureSection(string scenario, SensorRawDisplay sensor, RectTransform section)
        {
            var scroll = sensor.PrimaryScroll;
            foreach (var size in Sizes)
            foreach (bool bottom in new[] { false, true })
            {
                string name = scenario + (bottom ? "-bottom-" : "-top-") + size.x + "x" + size.y + ".png";
                string path = Path.Combine(Root, "visual", name);
                LocalLoopPreview.Render(size, path);
                Vector3 edge = section.TransformPoint(new Vector3(0, bottom ? section.rect.yMin : section.rect.yMax, 0));
                float offset = scroll.content.rect.yMax - scroll.content.InverseTransformPoint(edge).y;
                if (bottom) offset -= scroll.viewport.rect.height;
                else offset -= 120; // Keep the section heading inside the masked viewport.
                float extent = scroll.content.rect.height - scroll.viewport.rect.height;
                scroll.StopMovement();
                scroll.verticalNormalizedPosition = extent > 0 ? 1 - Mathf.Clamp01(offset / extent) : 1;
                scroll.Rebuild(CanvasUpdate.PostLayout);
                scroll.SendMessage("LateUpdate", SendMessageOptions.RequireReceiver);
                LocalLoopPreview.Render(size, path);
                foreach (var text in section.GetComponentsInChildren<Text>())
                    if (text.text.Length > 0 && text.rectTransform.rect.height + 2 < text.preferredHeight)
                        throw new InvalidOperationException("Consent text height overflow: " + name + " / " + text.name);
                report.images.Add(name);
            }
        }
        static void Finish(Exception error)
        {
            SessionState.SetBool(Key, false);
            EditorApplication.update -= WaitForPlay;
            EditorApplication.update -= CaptureLoadedScene;
            if (report == null) report = new Report();
            report.status = error == null ? "PASS" : "FAIL";
            report.error = error == null ? null : error.GetType().Name + ": " + error.Message;
            File.WriteAllText(Path.Combine(Root, "result.json"), JsonUtility.ToJson(report, true));
            Debug.Log("MEASUREMENT_CONSENT_CAPTURE_" + report.status + " checks=" + report.checks.Count + " images=" + report.images.Count);
            EditorApplication.Exit(error == null ? 0 : 1);
        }
    }
}
