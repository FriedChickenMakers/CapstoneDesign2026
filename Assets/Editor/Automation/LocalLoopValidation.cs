using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml;
using CapstoneDesign.Runtime;
using CapstoneDesign.Runtime.LocalState;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace CapstoneDesign.EditorTools
{
    // Batch entry point; never enters Play mode, modifies PlayerPrefs, or uses FileStateStore.
    public static class LocalLoopValidation
    {
        [Serializable] public sealed class CaseResult { public string name, status, detail; }
        [Serializable] public sealed class Report
        {
            public string environment = "Unity Editor edit-mode / synthetic fixtures / no device";
            public string utc, unityVersion;
            public int passed, failed;
            public List<CaseResult> cases = new List<CaseResult>();
        }

        [MenuItem("Capstone Mockup/Validate Local Loop")]
        public static void Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Run validation in edit mode only.");
            var report = new Report { utc = DateTimeOffset.UtcNow.ToString("O"), unityVersion = Application.unityVersion };
            Check(report, "Serialized scene retains runtime component and control bindings", SceneBindings);
            Check(report, "Integrated home restores growth, preserves cancellation and links navigation", IntegratedHome);
            Check(report, "Catalog typed IDs, order and candidate references", Catalog);
            Check(report, "UI completion without notes commits one reward and survives reopen", Completion);
            Check(report, "M15 debug steps, direct completion and garden purchase form one demo flow", M15DemoFlow);
            Check(report, "UI active session survives navigation and pause", Navigation);
            Check(report, "UI save failure retains participation and permits retry", SaveFailure);
            Check(report, "Environment preview cancellation and confirmation are transactional", Placement);
            Check(report, "Skipped dates preserve course and do not repay completion", DateAdvance);
            Check(report, "Health DTO native JSON preserves null, zero, source and long timestamps", HealthDto);
            Check(report, "Health DTO status vocabulary is not silently available", StatusVocabulary);
            Check(report, "Heart graph uses a separate Graphic child and exact session samples", HeartGraph);
            Check(report, "Mission selection aborts after a one-shot cycle save failure", CycleSelectionFailure);
            Check(report, "External navigation cancels environment and growth previews", ExternalNavigationPreview);
            Check(report, "Active navigation refreshes dates while activity panel is inactive", InactivePanelCycle);
            Check(report, "Failed completion retains optional draft for successful retry", OptionalDraftRetry);
            string directory = Environment.GetEnvironmentVariable("CAPSTONE_ARTIFACTS");
            if (string.IsNullOrWhiteSpace(directory)) directory = Path.Combine(Directory.GetCurrentDirectory(), "artifacts", "local-loop-validation");
            else directory = Path.Combine(directory, "local-loop-validation");
            Directory.CreateDirectory(directory);
            File.WriteAllText(Path.Combine(directory, "results.json"), JsonUtility.ToJson(report, true));
            using (var writer = XmlWriter.Create(Path.Combine(directory, "results.xml"), new XmlWriterSettings { Indent = true }))
            {
                writer.WriteStartElement("testsuite"); writer.WriteAttributeString("name", "LocalLoopValidation");
                writer.WriteAttributeString("tests", report.cases.Count.ToString()); writer.WriteAttributeString("failures", report.failed.ToString());
                foreach (var result in report.cases)
                {
                    writer.WriteStartElement("testcase"); writer.WriteAttributeString("name", result.name);
                    if (result.status == "FAIL") { writer.WriteStartElement("failure"); writer.WriteString(result.detail); writer.WriteEndElement(); }
                    writer.WriteEndElement();
                }
                writer.WriteEndElement();
            }
            Debug.Log("Local loop validation: " + report.passed + " passed, " + report.failed + " failed; " + directory);
            if (report.failed != 0) throw new InvalidOperationException("Local loop validation failed; inspect " + directory);
        }

        static void Check(Report report, string name, Action test)
        {
            try { test(); report.passed++; report.cases.Add(new CaseResult { name = name, status = "PASS", detail = "" }); }
            catch (Exception ex) { report.failed++; report.cases.Add(new CaseResult { name = name, status = "FAIL", detail = ex.ToString() }); Debug.LogWarning(name + ": " + ex.Message); }
        }
        static void Assert(bool condition, string reason) { if (!condition) throw new InvalidOperationException(reason); }

        static void SceneBindings()
        {
            const string path = "Assets/Scenes/MockupMain.unity";
            var scene = SceneManager.GetSceneByPath(path);
            bool opened = !scene.IsValid() || !scene.isLoaded;
            if (opened) scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
            try
            {
                var components = scene.GetRootGameObjects().SelectMany(o => o.GetComponentsInChildren<WeekOneQuestDemo>(true)).ToArray();
                Assert(components.Length == 1, "Expected exactly one serialized WeekOneQuestDemo.");
                var ui = components[0];
                Assert(AssetDatabase.GetAssetPath(MonoScript.FromMonoBehaviour(ui)) == "Assets/Scripts/Runtime/WeekOneQuestDemo.cs", "Serialized MonoScript identity changed.");
                Assert(ui.questButtons.Length == 4 && ui.questButtons.All(x => x != null), "Legacy button references lost.");
                Assert(ui.questLabels.Length == 4 && ui.questLabels.All(x => x != null), "Legacy label references lost.");
                Assert(ui.summary != null && ui.rewardPlant != null, "Summary or permanent garden plant reference missing.");
                Assert(ui.Service == null, "Opening scene unexpectedly initialized persistent game service in edit mode.");
                var nav = ui.transform.root.GetComponent<MockupNavigation>();
                Assert(nav != null && nav.activitiesPanel == ui.gameObject && nav.islandPanel != null, "Scene navigation not connected to local-loop component.");
                Assert(nav.activitiesButton != null && nav.islandButton != null && nav.settingsButton != null, "Serialized navigation buttons missing.");
            }
            finally { if (opened) EditorSceneManager.CloseScene(scene, true); }
        }

        static void IntegratedHome()
        {
            const string path = "Assets/Scenes/MockupMain.unity";
            var store = new MemoryStore();
            var clock = new FakeClock();
            var balance = new DemoBalanceConfig { VisitorChancePercent = 0 };
            Scene scene = default;
            GardenStateService service = null;
            try
            {
                scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
                var nav = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<MockupNavigation>(true)).Single();
                var home = nav.home;
                var loop = nav.activitiesPanel.GetComponent<WeekOneQuestDemo>();
                Assert(home != null && home.plant != null && home.gardenRoot != null && home.homeCanvas != null
                    && home.missionButton != null && home.shopButton != null && home.activityTab != null && home.settingsTab != null,
                    "Imported home references are incomplete.");
                Assert(scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<CapstoneDesign.Prototype.PrototypeController>(true)).Count() == 0,
                    "Temporary prototype controller is running in the app scene.");
                service = new GardenStateService(store, clock, balance, new LegacySnapshot { Nutrient = 50 });
                loop.Balance = balance; loop.Initialize(service, true);
                home.RefreshFromState();
                Assert(home.plant.seed.activeSelf && home.guide.text.Contains("영양제 50개"), "Fresh saved state did not show seed and nutrient.");
                for (int i = 1; i <= 3; i++)
                {
                    Assert(service.GrowPlant("home-growth-" + i, "plant:P06"), "Growth purchase failed.");
                    loop.UpdateGarden();
                    Assert(GardenHomePresenter.StageForGrowth(service.Snapshot.Plants.Single().Growth) == i,
                        "Saved growth did not advance exactly one visible stage.");
                }
                Assert(home.plant.blossomAnchor.gameObject.activeSelf && home.plant.chamomileFlower.activeSelf
                    && !home.plant.hydrangeaFlower.activeSelf && home.guide.text.Contains("영양제 20개"),
                    "Bloom or persisted nutrient disagrees with the garden state.");
                float beforeScale = home.plant.blossomAnchor.localScale.x;
                loop.ShowShop(); loop.PreviewGrowth(); Click(loop, "취소");
                Assert(Mathf.Approximately(home.plant.blossomAnchor.localScale.x, beforeScale)
                    && service.Snapshot.Nutrient == 20, "Cancelled purchase changed the new home.");
                store.FailSave = true;
                Assert(!service.GrowPlant("home-failed-save", "plant:P06"), "Injected save failure was not exercised.");
                loop.UpdateGarden();
                Assert(Mathf.Approximately(home.plant.blossomAnchor.localScale.x, beforeScale)
                    && service.Snapshot.Nutrient == 20, "Failed purchase changed the new home.");
                store.FailSave = false;
                Assert(service.GrowPlant("home-growth-4", "plant:P06"), "Later growth purchase failed.");
                loop.UpdateGarden();
                Assert(home.plant.blossomAnchor.localScale.x > beforeScale, "Growth after bloom had no visible change.");

                home.SendMessage("Awake", SendMessageOptions.RequireReceiver);
                nav.ShowIsland();
                home.missionButton.onClick.Invoke();
                Assert(nav.activitiesPanel.activeSelf && !home.homeCanvas.gameObject.activeSelf, "Activity entry did not open the existing activity screen.");
                nav.ShowIsland(); home.settingsTab.onClick.Invoke();
                Assert(nav.settingsPanel.activeSelf && !home.gardenVisuals.activeSelf, "Settings entry left the garden camera active.");
                nav.ShowIsland(); home.shopButton.onClick.Invoke();
                Assert(nav.activitiesPanel.activeSelf && loop.CurrentScreen == "shop", "Garden entry did not open the existing shop.");
                home.SendMessage("OnDestroy", SendMessageOptions.RequireReceiver);
                service.Dispose(); service = null;
                EditorSceneManager.CloseScene(scene, true); scene = default;

                scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
                nav = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<MockupNavigation>(true)).Single();
                home = nav.home; loop = nav.activitiesPanel.GetComponent<WeekOneQuestDemo>();
                service = new GardenStateService(store, clock, balance);
                loop.Balance = balance; loop.Initialize(service, true); home.RefreshFromState();
                Assert(home.plant.chamomileFlower.activeSelf && home.guide.text.Contains("영양제 10개")
                    && home.plant.blossomAnchor.localScale.x > beforeScale, "Reopened saved growth was not restored on the home.");
            }
            finally
            {
                service?.Dispose();
                if (scene.IsValid() && scene.isLoaded) EditorSceneManager.CloseScene(scene, true);
            }
        }

        static void Catalog()
        {
            Assert(MindfulnessContent.Course.Count == 28 && MindfulnessContent.FreeMissions.Count == 17, "Mission rows missing.");
            var ids = MindfulnessContent.Course.Select(x => x.id).Concat(MindfulnessContent.FreeMissions.Select(x => x.id))
                .Concat(MindfulnessContent.Plants.Select(x => x.id)).Concat(MindfulnessContent.Environments.Select(x => x.id)).Concat(MindfulnessContent.Animals.Select(x => x.id)).ToArray();
            Assert(ids.Length == 69 && ids.Distinct().Count() == ids.Length, "Typed ID collision or missing definition.");
            for (int i = 0; i < 28; i++)
            {
                var m = MindfulnessContent.Course[i];
                Assert(m.id == "course:P" + (i + 1).ToString("00") && m.recommendedOrder == i + 1 && m.week == i / 7 + 1, "Course order changed: " + m.id);
                Assert(!string.IsNullOrWhiteSpace(m.textOnlyInstructions) && !string.IsNullOrWhiteSpace(m.source), "Source/text alternative missing: " + m.id);
                Assert(!m.audioAvailable && m.suggestedDurationMinSeconds <= m.suggestedDurationMaxSeconds, "Unreviewed audio or invalid duration: " + m.id);
            }
            foreach (var animal in MindfulnessContent.Animals)
                foreach (string id in animal.candidateEnvironmentIds) Assert(MindfulnessContent.FindGardenContent(id) != null, "Unknown animal candidate environment: " + id);
            foreach (var item in MindfulnessContent.Plants.Concat(MindfulnessContent.Environments))
                foreach (string id in item.candidateAnimalIds) Assert(MindfulnessContent.FindAnimal(id) != null, "Unknown candidate animal: " + id);
            Assert(MindfulnessContent.FindMission("P01") == null && MindfulnessContent.FindGardenContent("course:P01") == null, "Untyped/cross-type lookup accepted.");
        }

        static void Completion()
        {
            using (var f = new Fixture())
            {
                f.Ui.ShowCourse();
                Assert(Button(f.Ui, "P01  ").interactable && !Button(f.Ui, "P02  ").interactable, "Future course order unexpectedly selectable.");
                f.Ui.SelectMission(MindfulnessContent.Course[0]);
                Click(f.Ui, "시작"); f.Clock.UtcNow = f.Clock.UtcNow.AddMinutes(2);
                f.Ui.ShowReflection(); Click(f.Ui, "기록 건너뛰고 완료");
                var snapshot = f.Service.Snapshot; string session = snapshot.Sessions.Single().SessionId;
                Assert(f.Ui.CurrentScreen == "complete" && snapshot.Nutrient == f.Balance.CompletionNutrient && snapshot.NextCourseOrder == 2, "UI did not commit completion, nutrient and course together.");
                Assert(snapshot.Sessions[0].Status == ParticipationState.RewardCommitted && snapshot.Sessions[0].Mood == null && snapshot.Sessions[0].Note == null, "Optional record skipping changed completion semantics.");
                Assert(f.Service.CompleteSession(session), "Duplicate completion should be idempotent.");
                Assert(f.Service.Snapshot.Nutrient == snapshot.Nutrient && f.Service.Snapshot.RewardReceipts.Count == 1, "Duplicate reward granted.");
                using (var reopened = new GardenStateService(f.Store, f.Clock, f.Balance))
                { Assert(reopened.Open() && reopened.Snapshot.NextCourseOrder == 2 && reopened.Snapshot.Nutrient == snapshot.Nutrient, "Reopen lost committed progress."); }
            }
        }

        static void M15DemoFlow()
        {
            using (var f = new Fixture())
            {
                f.Ui.ShowFree(); Click(f.Ui, "M15 걷기"); Click(f.Ui, "시작");
                Assert(f.Ui.ToggleDebugWalk(), "Could not enable demo walking mode.");
                for (int i = 0; i < 10; i++) Assert(f.Ui.AddVirtualDebugStep(), "Virtual step was not saved.");
                var walking = f.Service.Snapshot;
                Assert(walking.Nutrient == 1 && walking.DebugWalkPeriods.Single().RewardedUnits == 1,
                    "Ten debug steps did not grant exactly one nutrient.");
                Assert(f.Ui.GetComponentsInChildren<Text>(true).Any(t => t.text.Contains("목표 10걸음") && t.text.Contains("가상 10")),
                    "M15 screen did not show debug goal and virtual progress.");
                Click(f.Ui, "직접 완료"); f.Clock.UtcNow = f.Clock.UtcNow.AddMinutes(2);
                Click(f.Ui, "기록 건너뛰고 완료");
                Assert(f.Service.Snapshot.Nutrient == 11 && f.Service.Snapshot.RewardReceipts.Count == 1,
                    "M15 direct completion did not add its separate reward once.");
                f.Ui.ShowShop(); Click(f.Ui, "캐모마일 성장"); Click(f.Ui, "확정");
                var garden = f.Service.Snapshot;
                Assert(garden.Nutrient == 1 && garden.Plants.Any(p => p.PlantId == "plant:P06"),
                    "M15 reward could not be spent on garden growth.");
            }
        }

        static void Navigation()
        {
            using (var f = new Fixture())
            {
                f.Ui.SelectMission(MindfulnessContent.Course[0]); Click(f.Ui, "시작"); Click(f.Ui, "일시정지");
                f.Ui.ShowHome(); f.Ui.SelectMission(MindfulnessContent.FreeMissions[0]);
                Assert(f.Service.Snapshot.Sessions.Count == 1 && f.Service.Snapshot.Sessions[0].Status == ParticipationState.Paused, "Navigation cloned or lost active session.");
                Click(f.Ui, "이어서 하기"); Click(f.Ui, "오늘은 여기까지");
                Assert(f.Service.Snapshot.Sessions[0].Status == ParticipationState.Ended && f.Service.Snapshot.Nutrient == 0 && f.Service.Snapshot.NextCourseOrder == 1, "Participation stop rewarded or advanced course.");
            }
        }

        static void SaveFailure()
        {
            using (var f = new Fixture())
            {
                f.Ui.SelectMission(MindfulnessContent.Course[0]); Click(f.Ui, "시작");
                string before = StateCodec.Encode(f.Service.Snapshot); f.Store.FailSave = true;
                f.Ui.Finish(null, null);
                Assert(f.Ui.CurrentScreen == "reflection" && StateCodec.Encode(f.Service.Snapshot) == before, "Failed save partially changed game/UI state.");
                Assert(f.Ui.GetComponentsInChildren<Text>(true).Any(t => t.text.Contains("저장/처리 오류")), "Save failure was not visible.");
                f.Store.FailSave = false; f.Ui.Finish(null, null);
                Assert(f.Ui.CurrentScreen == "complete" && f.Service.Snapshot.Nutrient == f.Balance.CompletionNutrient, "Retry did not commit once.");
            }
        }

        static void Placement()
        {
            using (var f = new Fixture(30))
            {
                string before = StateCodec.Encode(f.Service.Snapshot);
                f.Ui.ShowShop(); f.Ui.PreviewEnvironment("environment:E01");
                Assert(StateCodec.Encode(f.Service.Snapshot) == before, "Preview spent nutrient.");
                Click(f.Ui, "취소"); Assert(StateCodec.Encode(f.Service.Snapshot) == before, "Cancellation modified placement.");
                f.Ui.PreviewEnvironment("environment:E01"); f.Store.FailSave = true; Click(f.Ui, "확정");
                Assert(StateCodec.Encode(f.Service.Snapshot) == before, "Failed placement save spent nutrient.");
                f.Store.FailSave = false; Click(f.Ui, "확정");
                var state = f.Service.Snapshot;
                Assert(state.Nutrient == 30 - f.Balance.EnvironmentCost && state.Environments.Count == 1 && state.PurchaseReceipts.Count == 1, "Placement not committed atomically.");
                Assert(f.Service.PurchaseEnvironment(state.PurchaseReceipts.Single(), "environment:E01", "left") && f.Service.Snapshot.Nutrient == state.Nutrient, "Purchase replay spent twice.");
                f.Ui.ShowShop(); f.Ui.PreviewGrowth(); Click(f.Ui, "취소");
                Assert(f.Service.Snapshot.Nutrient == state.Nutrient && f.Service.Snapshot.Plants.Count == 0, "Growth cancellation modified state.");
            }
        }

        static void DateAdvance()
        {
            using (var f = new Fixture())
            {
                f.Ui.SelectMission(MindfulnessContent.Course[0]); Click(f.Ui, "시작"); f.Ui.Finish(null, null);
                f.Clock.UtcNow = f.Clock.UtcNow.AddDays(12); f.Ui.RefreshCycle(); f.Ui.RefreshCycle();
                var state = f.Service.Snapshot;
                Assert(state.DailyDecisions.Count == 2 && state.NextCourseOrder == 2 && state.Nutrient == f.Balance.CompletionNutrient, "Missed dates generated catch-up decisions or rewards.");
                f.Clock.UtcNow = f.Clock.UtcNow.AddDays(-20); f.Ui.RefreshCycle();
                Assert(f.Service.Snapshot.DailyDecisions.Count == 2, "Clock rollback generated an extra decision.");
            }
        }

        static void HealthDto()
        {
            const long measured = 1789981200123L, refreshed = 1790002800456L;
            string json = "{\"status\":\"PARTIAL\",\"inputMode\":\"MOCK\",\"generatedAtEpochMs\":" + refreshed + ",\"message\":null,\"healthConnect\":{\"status\":\"PARTIAL\",\"lastSuccessfulRefreshEpochMs\":" + refreshed + ",\"steps\":{\"status\":\"AVAILABLE\",\"hasValue\":true,\"value\":0,\"queryComplete\":true},\"sleep\":{\"status\":\"NO_DATA\",\"hasValue\":false},\"heartRate\":{\"status\":\"PARTIAL\",\"hasValue\":true,\"queryComplete\":false,\"completionReason\":\"PERMISSION_REVOKED\",\"queryTimeZone\":\"Asia/Seoul\",\"measuredAtEpochMs\":" + measured + ",\"lastUpdatedEpochMs\":" + refreshed + ",\"sourcePackages\":[\"synthetic.samsung\"],\"heartRateSamples\":[{\"recordId\":\"fixture-1\",\"sourcePackage\":\"synthetic.samsung\",\"measuredAtEpochMs\":" + measured + ",\"beatsPerMinute\":72,\"segmentId\":1}],\"missingIntervals\":null},\"watchData\":{\"status\":\"AVAILABLE\",\"directConnectionStatus\":\"NOT_CHECKED\"}}}";
            var dto = JsonUtility.FromJson<AndroidPlatformSnapshot>(json);
            var copy = JsonUtility.FromJson<AndroidPlatformSnapshot>(JsonUtility.ToJson(dto));
            Assert(copy.generatedAtEpochMs == refreshed && copy.ParsedStatus == PlatformStatus.Partial, "Long timestamp/status roundtrip changed.");
            Assert(copy.healthConnect.steps.hasValue && copy.healthConnect.steps.value == 0 && !copy.healthConnect.sleep.hasValue, "Normal zero and missing value conflated.");
            var h = copy.healthConnect.heartRate;
            Assert(h.measuredAtEpochMs == measured && h.lastUpdatedEpochMs == refreshed && h.measuredAtEpochMs != h.lastUpdatedEpochMs, "Query timestamp replaced measurement timestamp.");
            Assert(!h.queryComplete && h.completionReason == "PERMISSION_REVOKED" && h.queryTimeZone == "Asia/Seoul", "Partial-query metadata lost.");
            Assert(h.heartRateSamples.Length == 1 && h.heartRateSamples[0].sourcePackage == "synthetic.samsung" && h.heartRateSamples[0].measuredAtEpochMs == measured, "Sample source/time lost.");
            Assert((h.missingIntervals == null || h.missingIntervals.Length == 0) && string.IsNullOrEmpty(copy.message), "Null JSON synthesized data.");
            Assert(copy.healthConnect.watchData.directConnectionStatus == "NOT_CHECKED", "Source availability became direct watch connection.");
        }

        static void StatusVocabulary()
        {
            string[] native = { "AVAILABLE", "UNSUPPORTED", "PERMISSION_REQUIRED", "PERMISSION_DENIED", "NO_DATA", "SERVICE_UNAVAILABLE", "DISCONNECTED", "STALE", "PARTIAL", "ERROR" };
            PlatformStatus[] expected = { PlatformStatus.Available, PlatformStatus.Unsupported, PlatformStatus.PermissionRequired, PlatformStatus.PermissionDenied, PlatformStatus.NoData, PlatformStatus.ServiceUnavailable, PlatformStatus.Disconnected, PlatformStatus.Stale, PlatformStatus.Partial, PlatformStatus.Error };
            for (int i = 0; i < native.Length; i++)
            {
                var metric = JsonUtility.FromJson<HealthMetricSnapshot>("{\"status\":\"" + native[i] + "\"}");
                Assert(metric.ParsedStatus == expected[i] && !metric.hasValue, "Status mapping mismatch: " + native[i]);
            }
            Assert(PlatformStatusParser.Parse(null) == PlatformStatus.Error && PlatformStatusParser.Parse("FUTURE_STATUS") == PlatformStatus.Error, "Unknown status reported as available.");
        }

        static void HeartGraph()
        {
            using (var f = new Fixture())
            {
                var start = f.Clock.UtcNow;
                var end = start.AddMinutes(5);
                var panel = GardenUi.Box(f.Ui.transform, "Synthetic heart fixture", 0, 0, 1, 1);
                var metric = new HealthMetricSnapshot
                {
                    status = "AVAILABLE", queryComplete = true, sampleCount = 4,
                    queryStartEpochMs = start.ToUnixTimeMilliseconds(), queryEndEpochMs = end.ToUnixTimeMilliseconds(),
                    heartRateSamples = new[] {
                        new HeartRateSampleSnapshot { measuredAtEpochMs = start.AddMinutes(1).ToUnixTimeMilliseconds(), beatsPerMinute = 70, sourcePackage = "fixture-phone", segmentId = 1 },
                        new HeartRateSampleSnapshot { measuredAtEpochMs = start.AddMinutes(2).ToUnixTimeMilliseconds(), beatsPerMinute = 74, sourcePackage = "fixture-phone", segmentId = 1 },
                        new HeartRateSampleSnapshot { measuredAtEpochMs = start.AddMinutes(3).ToUnixTimeMilliseconds(), beatsPerMinute = 72, sourcePackage = "fixture-watch", segmentId = 1 },
                        new HeartRateSampleSnapshot { measuredAtEpochMs = start.AddMinutes(4).ToUnixTimeMilliseconds(), beatsPerMinute = 75, sourcePackage = "fixture-watch", segmentId = 1 }
                    }
                };
                var snapshot = new AndroidPlatformSnapshot { inputMode = "MOCK", healthConnect = new HealthConnectSnapshot { heartRate = metric } };
                HeartHistoryView.Build(panel.transform, snapshot, start.ToString("O"), end.ToString("O"));
                var graph = panel.GetComponentInChildren<HeartSampleGraph>(true);
                Assert(graph != null && graph.GetComponent<CanvasRenderer>() != null && graph.Samples.Length == 4, "Exact session samples did not create a graph.");
                Assert(graph.GetComponent<Image>() == null && graph.transform.parent.GetComponent<Image>() != null, "Graph and background must use separate objects; Unity permits only one Graphic per object.");
                Assert(graph.WindowStart == metric.queryStartEpochMs && graph.WindowEnd == metric.queryEndEpochMs, "Graph window does not match session.");
                graph.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, 320);
                graph.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, 140);
                Assert(GraphVertexCount(graph) == 24, "Expected four point quads and two within-source line quads; missing mesh or cross-source connection detected.");
                graph.Samples = new[] { metric.heartRateSamples[0] };
                Assert(GraphVertexCount(graph) == 4, "One sample must produce one point quad, not a fabricated continuous line.");
                graph.Samples = metric.heartRateSamples;
                var unmatched = GardenUi.Box(f.Ui.transform, "Unqueried heart fixture", 0, 0, 1, 1);
                HeartHistoryView.Build(unmatched.transform, snapshot, start.AddDays(1).ToString("O"), end.AddDays(1).ToString("O"));
                Assert(GraphVertexCount(unmatched.GetComponentInChildren<HeartSampleGraph>(true)) == 0, "Old unrelated samples generated geometry for an unqueried session.");
                metric.heartRateSamples = new[] { new HeartRateSampleSnapshot { measuredAtEpochMs = start.AddDays(-1).ToUnixTimeMilliseconds(), beatsPerMinute = 72, sourcePackage = "fixture-old" } };
                var outside = GardenUi.Box(f.Ui.transform, "Out-of-window sample fixture", 0, 0, 1, 1);
                HeartHistoryView.Build(outside.transform, snapshot, start.ToString("O"), end.ToString("O"));
                Assert(GraphVertexCount(outside.GetComponentInChildren<HeartSampleGraph>(true)) == 0, "A queried interval included geometry from a sample outside its own timestamp bounds.");
            }
        }

        static int GraphVertexCount(HeartSampleGraph graph)
        {
            if (graph == null) return 0;
            var populate = typeof(HeartSampleGraph).GetMethod("OnPopulateMesh",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic,
                null, new[] { typeof(VertexHelper) }, null);
            Assert(populate != null, "Heart graph mesh callback missing.");
            using (var mesh = new VertexHelper())
            {
                populate.Invoke(graph, new object[] { mesh });
                var vertex = new UIVertex();
                for (int i = 0; i < mesh.currentVertCount; i++)
                {
                    mesh.PopulateUIVertex(ref vertex, i);
                    Assert(!float.IsNaN(vertex.position.x) && !float.IsNaN(vertex.position.y) &&
                        !float.IsInfinity(vertex.position.x) && !float.IsInfinity(vertex.position.y), "Graph generated invalid vertex coordinates.");
                }
                return mesh.currentVertCount;
            }
        }

        static void CycleSelectionFailure()
        {
            using (var f = new Fixture())
            {
                string before = StateCodec.Encode(f.Service.Snapshot);
                f.Clock.UtcNow = f.Clock.UtcNow.AddDays(1);
                // Fail only the daily write: a buggy subsequent BeginSession write would succeed.
                f.Store.FailNextSaves = 1;
                f.Ui.SelectMission(MindfulnessContent.Course[0]);
                Assert(f.Store.FailNextSaves == 0, "The injected daily write failure was not exercised.");
                Assert(StateCodec.Encode(f.Service.Snapshot) == before && f.Ui.CurrentScreen == "home", "Selection continued using an old cycle after daily persistence failed.");
                Assert(f.Ui.GetComponentsInChildren<Text>(true).Any(t => t.text.Contains("오류")), "Cycle failure was not shown to the user.");
                f.Ui.SelectMission(MindfulnessContent.Course[0]);
                var state = f.Service.Snapshot;
                Assert(state.Sessions.Count == 1 && state.DailyDecisions.Count == 2 && state.Sessions[0].InstanceId == state.LastCycleId, "Retry did not bind the new session to the successfully persisted cycle.");
            }
        }

        static MockupNavigation CreateNavigation(Fixture f)
        {
            var root = new GameObject("Synthetic always-active navigation", typeof(RectTransform));
            SceneManager.MoveGameObjectToScene(root, f.Ui.gameObject.scene);
            f.Ui.transform.SetParent(root.transform, false);
            var nav = root.AddComponent<MockupNavigation>();
            nav.activitiesPanel = f.Ui.gameObject;
            nav.islandPanel = GardenUi.Box(root.transform, "Synthetic island", 0, 0, 1, 1);
            nav.settingsPanel = GardenUi.Box(root.transform, "Synthetic settings", 0, 0, 1, 1);
            nav.ShowActivities();
            return nav;
        }

        static void ExternalNavigationPreview()
        {
            using (var f = new Fixture(30))
            {
                var nav = CreateNavigation(f);
                var plant = new GameObject("Synthetic original plant");
                plant.transform.SetParent(nav.islandPanel.transform, false);
                f.Ui.rewardPlant = plant.transform;
                f.Ui.UpdateGarden();
                Vector3 committedScale = plant.transform.localScale;
                string before = StateCodec.Encode(f.Service.Snapshot);
                f.Ui.PreviewEnvironment("environment:E01");
                Assert(GameObject.Find("PREVIEW environment:E01") != null, "Environment preview fixture did not create a ghost.");
                nav.ShowSettings();
                Assert(GameObject.Find("PREVIEW environment:E01") == null && !f.Ui.gameObject.activeSelf, "External settings navigation left an environment ghost visible.");
                Assert(StateCodec.Encode(f.Service.Snapshot) == before, "Leaving an environment preview changed permanent progress.");
                nav.ShowActivities(); f.Ui.PreviewGrowth();
                Assert(plant.transform.localScale != committedScale, "Growth preview did not change the temporary visual.");
                nav.ShowIsland();
                Assert(plant.transform.localScale == committedScale && StateCodec.Encode(f.Service.Snapshot) == before, "External island navigation did not cancel temporary growth without spending.");
            }
        }

        static void InactivePanelCycle()
        {
            using (var f = new Fixture())
            {
                var nav = CreateNavigation(f); nav.ShowIsland();
                Assert(nav.gameObject.activeInHierarchy && !f.Ui.gameObject.activeInHierarchy, "Fixture must keep navigation active while activity is inactive.");
                f.Clock.UtcNow = f.Clock.UtcNow.AddDays(3);
                nav.SendMessage("Update", SendMessageOptions.RequireReceiver);
                Assert(f.Service.Snapshot.DailyDecisions.Count == 2, "Always-active navigation did not apply the current cycle while activity was hidden.");
                f.Clock.UtcNow = f.Clock.UtcNow.AddDays(1);
                nav.SendMessage("OnApplicationFocus", false, SendMessageOptions.RequireReceiver);
                Assert(f.Service.Snapshot.DailyDecisions.Count == 2, "Losing focus should not refresh the daily cycle.");
                nav.SendMessage("OnApplicationFocus", true, SendMessageOptions.RequireReceiver);
                nav.SendMessage("OnApplicationFocus", true, SendMessageOptions.RequireReceiver);
                var state = f.Service.Snapshot;
                Assert(state.DailyDecisions.Count == 3 && state.NextCourseOrder == 1 && state.Nutrient == 0, "Focus refresh either missed the cycle, duplicated it or generated progress/reward.");
            }
        }

        static void OptionalDraftRetry()
        {
            using (var f = new Fixture())
            {
                const string draft = "합성 테스트 메모\n저장 실패 후에도 그대로";
                f.Ui.SelectMission(MindfulnessContent.Course[0]); Click(f.Ui, "시작"); f.Ui.ShowReflection();
                Click(f.Ui, "잘 모르겠어요");
                f.Ui.GetComponentInChildren<InputField>(true).text = draft;
                string before = StateCodec.Encode(f.Service.Snapshot); f.Store.FailNextSaves = 1;
                Click(f.Ui, "이 기록으로 완료");
                Assert(f.Ui.CurrentScreen == "reflection" && StateCodec.Encode(f.Service.Snapshot) == before, "Failed note completion partially committed state.");
                Assert(f.Ui.GetComponentInChildren<InputField>(true).text == draft, "Rebuilt reflection page discarded the optional draft.");
                Click(f.Ui, "이 기록으로 완료");
                var state = f.Service.Snapshot;
                Assert(state.Sessions.Single().Note == draft && state.Sessions.Single().Mood == "잘 모르겠어요", "Retry without retyping lost note or selected mood.");
                Assert(state.RewardReceipts.Count == 1 && state.Nutrient == f.Balance.CompletionNutrient, "Draft retry failed to commit one reward.");
            }
        }

        static Button Button(WeekOneQuestDemo ui, string prefix)
        {
            var matches = ui.GetComponentsInChildren<Button>(true).Where(b => b.gameObject.name.StartsWith(prefix, StringComparison.Ordinal)).ToArray();
            Assert(matches.Length == 1, "Expected one UI button: " + prefix + "; found " + matches.Length);
            return matches[0];
        }
        static void Click(WeekOneQuestDemo ui, string name) { var button = Button(ui, name); Assert(button.interactable, "Button disabled: " + name); button.onClick.Invoke(); }
        sealed class FakeClock : IClock
        {
            public DateTimeOffset UtcNow { get; set; } = new DateTimeOffset(2026, 9, 23, 3, 0, 0, TimeSpan.Zero);
            public TimeZoneInfo TimeZone { get; } = TimeZoneInfo.CreateCustomTimeZone("Fixture-KST", TimeSpan.FromHours(9), "Fixture KST", "Fixture KST");
        }
        sealed class MemoryStore : IStateStore
        {
            string data; public bool FailSave; public int FailNextSaves;
            public GardenState Load() => data == null ? null : StateCodec.Decode(data);
            public void Save(GardenState state)
            {
                if (FailNextSaves > 0) { FailNextSaves--; throw new IOException("Synthetic one-shot storage failure"); }
                if (FailSave) throw new IOException("Synthetic storage failure");
                data = StateCodec.Encode(state);
            }
        }
        sealed class Fixture : IDisposable
        {
            public readonly MemoryStore Store = new MemoryStore();
            public readonly FakeClock Clock = new FakeClock();
            public readonly DemoBalanceConfig Balance = new DemoBalanceConfig { VisitorChancePercent = 0 };
            public readonly GardenStateService Service;
            public readonly WeekOneQuestDemo Ui;
            readonly Scene scene, previous;
            readonly HashSet<int> previousRootIds;
            public Fixture(int nutrient = 0)
            {
                previous = SceneManager.GetActiveScene();
                previousRootIds = new HashSet<int>(previous.GetRootGameObjects().Select(o => o.GetInstanceID()));
                // Preview scenes can coexist with an untitled unsaved scene. Do not set one active:
                // move the fixture explicitly and retain the editor's existing active scene.
                scene = EditorSceneManager.NewPreviewScene();
                var root = new GameObject("LocalLoopValidation synthetic UI", typeof(RectTransform), typeof(Canvas)); root.SetActive(false);
                SceneManager.MoveGameObjectToScene(root, scene);
                Service = new GardenStateService(Store, Clock, Balance, new LegacySnapshot { Nutrient = nutrient });
                try
                {
                    Ui = root.AddComponent<WeekOneQuestDemo>(); Ui.Balance = Balance; Ui.Initialize(Service, true);
                    Assert(ReferenceEquals(Ui.Service, Service), "Fixture did not use injected memory service.");
                }
                catch { Dispose(); throw; }
            }
            public void Dispose()
            {
                // Runtime garden rendering creates detached root objects. Move only roots created
                // during this synchronous fixture into its scene before closing it.
                if (previous.IsValid() && previous.isLoaded)
                    foreach (var root in previous.GetRootGameObjects())
                        if (!previousRootIds.Contains(root.GetInstanceID())) SceneManager.MoveGameObjectToScene(root, scene);
                EditorSceneManager.ClosePreviewScene(scene); Service.Dispose();
                if (previous.IsValid() && previous.isLoaded) SceneManager.SetActiveScene(previous);
            }
        }
    }
}
