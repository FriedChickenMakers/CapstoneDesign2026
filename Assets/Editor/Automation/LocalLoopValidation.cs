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
            Check(report, "Back returns through lists, detail parameters and unpaid previews", BackPageFlow);
            Check(report, "Back preserves drafts, blocks failed saves and rewinds guided steps", BackDraftFlow);
            Check(report, "Android keyboard Back retains the final draft before end-edit callbacks", KeyboardDismissal);
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
            Check(report, "MBCT guided steps reward five and reviews preserve quota", MbctGuidedFlow);
            Check(report, "MBCT routine draft, failure retry and reuse stay connected", MbctRoutineFlow);
            Check(report, "Experience journal reuses design emotions and personal plans are editable", MbctJournalFlow);
            Check(report, "Course and free rendering take one isolated snapshot", MbctRenderReads);
            Check(report, "All guided controls and face selectors stay separated", MbctLayouts);
            Check(report, "Purchase previews explain unavailable actions without spending", PreviewAvailability);
            Check(report, "Custom practice input clears previous choice and retains its label", PracticeInputFeedback);
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
                    && home.missionButton == null && home.shopButton != null && home.activityTab != null && home.settingsTab != null,
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
                loop.ShowShop(); loop.PreviewGrowth();
                Assert(home.gardenVisuals.activeSelf && loop.GetComponentInChildren<RawImage>(true)?.texture==home.gardenCamera.targetTexture,
                    "Purchase preview does not use the integrated garden texture.");
                Assert(home.plant.blossomAnchor.localScale.x>beforeScale && service.Snapshot.Nutrient==20,
                    "Purchase preview either lacks the next growth or spends saved nutrient.");
                Click(loop, "취소");
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
                home.activityTab.onClick.Invoke();
                Assert(nav.activitiesPanel.activeSelf && !home.IsGardenVisible && home.activityTab.gameObject.activeInHierarchy, "Activity entry did not preserve the shared navigation.");
                nav.ShowIsland(); home.settingsTab.onClick.Invoke();
                Assert(nav.settingsPanel.activeSelf && !home.gardenVisuals.activeSelf, "Settings entry left the garden camera active.");
                VerifySharedHeader(nav, loop);
                VerifyCollapsedDiagnostics(nav, loop);
                VerifyTabBack(nav, loop);
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

        static void VerifyTabBack(MockupNavigation nav, WeekOneQuestDemo loop)
        {
            loop.ShowHome();nav.ShowIsland();
            Assert(!nav.TryNavigateBack(), "Home root should yield to Android background behavior.");
            nav.ShowActivities();loop.ShowCourse();nav.ShowSettings();
            Assert(nav.TryNavigateBack() && nav.activitiesPanel.activeSelf && loop.CurrentScreen=="course",
                "Settings Back did not restore the previous activity page.");
            Assert(nav.TryNavigateBack() && loop.CurrentScreen=="home", "Back skipped the activity list.");
            Assert(nav.TryNavigateBack() && nav.home.IsGardenVisible, "Activity root did not return to Home.");
            Assert(!nav.TryNavigateBack(), "Returning Home retained a stale tab history.");
            nav.ShowSettings();nav.ShowSettings();nav.ShowActivities();
            Assert(nav.TryNavigateBack() && nav.settingsPanel.activeSelf, "Activity root skipped the previously visited Settings tab.");
            Assert(nav.TryNavigateBack() && nav.home.IsGardenVisible && !nav.TryNavigateBack(),
                "Repeated tab selections created a Back loop.");
        }

        static void VerifyCollapsedDiagnostics(MockupNavigation nav, WeekOneQuestDemo loop)
        {
            nav.ShowSettings();
            var sensor = nav.GetComponentInChildren<SensorRawDisplay>(true);
            sensor.PrepareView();
            sensor.output.text = "Synthetic unchanged diagnostic text";
            var summary = (Text)typeof(SensorRawDisplay).GetField("healthStatus",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).GetValue(sensor);
            summary.text = "Synthetic stale visible status";
            long reads = loop.Service.SnapshotReads;
            sensor.SendMessage("RefreshText", SendMessageOptions.RequireReceiver);
            Assert(sensor.output.text == "Synthetic unchanged diagnostic text" && loop.Service.SnapshotReads == reads,
                "Closed diagnostics still format raw output or clone the garden state.");
            Assert(summary.text != "Synthetic stale visible status", "Visible status stopped refreshing with diagnostics closed.");
            sensor.ToggleDiagnostics();
            Assert(sensor.output.text.Contains("PLATFORM") && sensor.output.text.Contains("HEALTH") &&
                sensor.output.text.Contains("ACTION:"), "Expanding diagnostics did not populate current data immediately.");
            sensor.ToggleDiagnostics();
            string previous = sensor.output.text;
            reads = loop.Service.SnapshotReads;
            sensor.SendMessage("RefreshText", SendMessageOptions.RequireReceiver);
            Assert(sensor.output.text == previous && loop.Service.SnapshotReads == reads,
                "Closing diagnostics did not suspend its formatting and state reads.");
        }

        static void VerifySharedHeader(MockupNavigation nav, WeekOneQuestDemo loop)
        {
            nav.ShowIsland();
            Assert(!nav.home.oldCamera.enabled && !nav.home.oldLight.enabled,
                "Integrated Home enabled the hidden legacy renderer.");
            var header = nav.home.homeCanvas.transform.Find("SafePortraitFrame/MainContent/Brand").GetComponent<TMPro.TMP_Text>();
            var font = header.font;
            Canvas.ForceUpdateCanvases();
            var corners = new Vector3[4];
            header.rectTransform.GetWorldCorners(corners);
            var sensor = nav.GetComponentInChildren<SensorRawDisplay>(true);
            sensor.PrepareView();
            foreach (bool settings in new[] { false, true })
            {
                if (settings) nav.ShowSettings(); else { nav.ShowActivities(); loop.ShowHome(); }
                Assert(!nav.home.oldCamera.enabled && !nav.home.oldLight.enabled,
                    "An integrated tab enabled the hidden legacy renderer.");
                var scroll = settings ? sensor.PrimaryScroll : loop.GetComponentInChildren<ScrollRect>();
                foreach (float position in new[] { 1f, 0f })
                {
                    scroll.verticalNormalizedPosition = position;
                    Canvas.ForceUpdateCanvases();
                    var actual = new Vector3[4];
                    header.rectTransform.GetWorldCorners(actual);
                    Assert(header.isActiveAndEnabled && header.font == font && actual.SequenceEqual(corners),
                        "Brand typography or position changed across tabs/scroll.");
                    Assert(!nav.GetComponentsInChildren<Text>().Any(text => text.text == "마음 정원"),
                        "A legacy duplicate brand is still visible behind the shared header.");
                }
            }
            var cover = header.transform.parent.Find("Shared header background").GetComponent<Image>();
            Assert(cover.color.a == 1 && cover.raycastTarget,
                "Scrolled controls can show through or receive taps behind the fixed brand.");
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
                Assert(Button(f.Ui, "1일차  ").interactable && !Button(f.Ui, "2일차  ").interactable, "Future course order unexpectedly selectable.");
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

        static void KeyboardDismissal()
        {
            var root = new GameObject("Synthetic keyboard fixture", typeof(RectTransform), typeof(Canvas));
            GameObject eventOwner = null;
            bool enabledFixtureEvents = false;
            const System.Reflection.BindingFlags hidden = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
            var previousSelection = UnityEngine.EventSystems.EventSystem.current?.currentSelectedGameObject;
            try
            {
                if (UnityEngine.EventSystems.EventSystem.current == null)
                {
                    eventOwner = new GameObject("Synthetic keyboard events", typeof(UnityEngine.EventSystems.EventSystem));
                    if (UnityEngine.EventSystems.EventSystem.current == null)
                    {
                        typeof(UnityEngine.EventSystems.EventSystem).GetMethod("OnEnable", hidden)
                            .Invoke(eventOwner.GetComponent<UnityEngine.EventSystems.EventSystem>(), null);
                        enabledFixtureEvents = true;
                    }
                }
                // Edit mode does not dispatch MonoBehaviour LateUpdate. Invoke
                // UGUI's actual focus routine; do not fake its private focus/text flags.
                var focus = typeof(InputField).GetMethod("ActivateInputFieldInternal", hidden);
                Assert(focus != null, "UGUI input focus routine was not found.");
                var input = GardenUi.Input(root.transform, "Synthetic draft", 0, 0, 1, 1) as GardenInputField;
                Assert(input != null, "Garden forms do not use the draft-preserving field.");
                input.caretBlinkRate = 0;
                input.text = "synthetic original";
                focus.Invoke(input, null);
                Assert(input.isFocused, "Keyboard fixture failed to focus the actual input field.");
                input.text = "synthetic typed draft";
                int endEdits = 0;string ended = null, changed = null;
                input.onEndEdit.AddListener(value => { endEdits++; ended = value; });
                input.onValueChanged.AddListener(value => changed = value);
                Assert(!input.TryDismissCanceledKeyboard(TouchScreenKeyboard.Status.Visible, "ignored") && input.isFocused,
                    "A visible keyboard was incorrectly dismissed.");
                Assert(input.TryDismissCanceledKeyboard(TouchScreenKeyboard.Status.Canceled, "synthetic final IME draft"),
                    "Canceled Android keyboard was not handled.");
                Assert(!input.isFocused && !input.wasCanceled && input.text=="synthetic final IME draft" &&
                    changed==input.text && ended==input.text && endEdits==1,
                    "Keyboard Back reverted the draft or delivered stale/duplicate save callbacks.");
                // The subclass must not change deliberate desktop Escape cancellation.
                focus.Invoke(input, null);
                input.text = "synthetic desktop edit";
                input.ProcessEvent(new Event { type=EventType.KeyDown, keyCode=KeyCode.Escape });
                input.DeactivateInputField();
                Assert(input.wasCanceled && input.text=="synthetic final IME draft",
                    "Native keyboard dismissal changed desktop Escape cancellation.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
                if (enabledFixtureEvents)
                    typeof(UnityEngine.EventSystems.EventSystem).GetMethod("OnDisable", hidden)
                        .Invoke(eventOwner.GetComponent<UnityEngine.EventSystems.EventSystem>(), null);
                if (eventOwner != null) UnityEngine.Object.DestroyImmediate(eventOwner);
                if (UnityEngine.EventSystems.EventSystem.current != null)
                    UnityEngine.EventSystems.EventSystem.current.SetSelectedGameObject(previousSelection);
            }
        }

        static void BackPageFlow()
        {
            using (var f = new Fixture(50))
            {
                Assert(!f.Ui.TryNavigateBack(), "Activity root should yield to tab history.");
                f.Ui.ShowCourse();f.Ui.ShowCourse();f.Ui.ShowCourse();
                Assert(f.Ui.TryNavigateBack() && f.Ui.CurrentScreen=="home", "Page refreshes polluted Back history.");
                f.Ui.ShowRecords();f.Ui.ShowExperienceHistory();f.Ui.ShowPersonalPlan();f.Ui.ShowPlanField("support");
                f.Ui.GetComponentInChildren<InputField>(true).text="synthetic unfinished plan";
                Assert(f.Ui.TryNavigateBack() && f.Ui.CurrentScreen=="personal-plan", "Plan editor did not return to its list.");
                f.Ui.ShowPlanField("support");
                Assert(f.Ui.GetComponentInChildren<InputField>(true).text=="synthetic unfinished plan", "Back discarded the unsaved plan draft.");
                f.Ui.TryNavigateBack();f.Ui.TryNavigateBack();f.Ui.TryNavigateBack();
                Assert(f.Ui.CurrentScreen=="records", "Nested plan Back did not reach participation records.");
                Assert(f.Service.BeginSession("synthetic-back-record",MbctPolicy.MissionId(1),1) &&
                    f.Service.StartSession("synthetic-back-record") && f.Service.EndParticipation("synthetic-back-record"), "Could not seed record.");
                f.Ui.ShowRecord("synthetic-back-record");f.Ui.ShowPracticeAnswers("synthetic-back-record");
                Assert(f.Ui.TryNavigateBack() && f.Ui.CurrentScreen=="record", "Answers Back lost its record parameter.");
                Assert(f.Ui.TryNavigateBack() && f.Ui.CurrentScreen=="records", "Record Back did not restore list.");
                f.Ui.ShowHome();f.Ui.ShowShop();
                string before=StateCodec.Encode(f.Service.Snapshot);
                f.Ui.PreviewEnvironment("environment:E01");
                Assert(f.Ui.TryNavigateBack() && f.Ui.CurrentScreen=="shop" && StateCodec.Encode(f.Service.Snapshot)==before,
                    "Back from placement committed a purchase or skipped the shop.");
                f.Ui.PreviewGrowth();
                Assert(f.Ui.TryNavigateBack() && f.Ui.CurrentScreen=="shop" && StateCodec.Encode(f.Service.Snapshot)==before,
                    "Back from growth committed a purchase or skipped the shop.");
            }
        }

        static void BackDraftFlow()
        {
            using (var f = new Fixture())
            {
                f.Ui.ShowCourse();f.Ui.SelectMission(MbctContent.Course[0]);Click(f.Ui,"시작");
                var input=f.Ui.GetComponentInChildren<InputField>(true);input.text="synthetic practice draft";
                f.Store.FailNextSaves=1;
                Assert(f.Ui.TryNavigateBack() && f.Ui.CurrentScreen=="session" && input.text=="synthetic practice draft",
                    "Back left a form after its draft save failed.");
                Assert(f.Ui.TryNavigateBack() && f.Ui.CurrentScreen=="course" &&
                    f.Service.Snapshot.Sessions.Single().Answers.Any(a=>a.Value=="synthetic practice draft"),
                    "Back failed to retain the active practice draft.");
                f.Ui.ShowSession();f.Ui.AdvancePractice(false);
                Assert(f.Ui.TryNavigateBack() && f.Ui.CurrentScreen=="session" && f.Service.Snapshot.Sessions.Single().InstructionStep==0,
                    "Back did not return to the previous practice step.");
                for(int step=0;step<MbctContent.Course[0].steps.Length;step++)f.Ui.AdvancePractice(false);
                Assert(f.Ui.CurrentScreen=="reflection", "Fixture did not reach reflection.");
                Assert(f.Ui.TryNavigateBack() && f.Ui.CurrentScreen=="session" &&
                    f.Service.Snapshot.Sessions.Single().InstructionStep==MbctContent.Course[0].steps.Length-1,
                    "Reflection Back looped into reflection instead of the last practice step.");
                f.Ui.AdvancePractice(false);f.Ui.Finish(null,null);
                Assert(f.Ui.TryNavigateBack() && f.Ui.CurrentScreen=="home", "Completion Back resurrected a completed session.");
                f.Ui.ShowRecords();f.Ui.ShowExperienceTypes();Click(f.Ui,"즐거움");
                f.Ui.GetComponentInChildren<InputField>(true).text="synthetic experience draft";Click(f.Ui,"다음");
                Assert(f.Ui.TryNavigateBack() && f.Ui.GetComponentInChildren<InputField>(true).text=="synthetic experience draft",
                    "Experience step Back lost the previous answer.");
                for(int step=0;step<4;step++)Click(f.Ui,step==3?"기록 저장":"다음");
                Assert(f.Ui.TryNavigateBack() && f.Ui.CurrentScreen=="records", "Saved experience Back reopened its submitted form.");
            }
        }

        static void M15DemoFlow()
        {
            using (var f = new Fixture())
            {
                f.Ui.ShowFree(); Click(f.Ui, "걸음 기록과 걷기"); Click(f.Ui, "시작");
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
                Assert(f.Ui.GetComponentsInChildren<Text>(true).Any(t => t.text.Contains("저장하지 못했어요")), "Save failure was not visible.");
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
                Assert(f.Ui.GetComponentsInChildren<Text>(true).Any(t => t.text.Contains("저장하지 못했어요")), "Cycle failure was not shown to the user.");
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

        static void MbctGuidedFlow()
        {
            using(var f=new Fixture(0,5))
            {
                f.Ui.ShowCourse();Click(f.Ui,"1일차  ");Click(f.Ui,"시작");Click(f.Ui,"물");
                Click(f.Ui,"다음");Click(f.Ui,"다음");Click(f.Ui,"다음");Click(f.Ui,"실습 마치기");
                Click(f.Ui,"기록 건너뛰고 완료");
                Assert(f.Service.Snapshot.Nutrient==5 && f.Service.Snapshot.MbctNextOrder==2,"Guided completion did not reward five/advance.");
                f.Ui.ShowCourse();Assert(!Button(f.Ui,"2일차  ").interactable,"Daily quota bypass through course UI.");
                Click(f.Ui,"1일차  ");Click(f.Ui,"시작");
                Assert(f.Ui.GetComponentInChildren<InputField>(true).text=="물","Eating choice did not persist for reuse.");
                f.Ui.AdvancePractice(false);f.Ui.AdvancePractice(false);f.Ui.AdvancePractice(false);f.Ui.AdvancePractice(false);
                Click(f.Ui,"기록 건너뛰고 완료");
                Assert(f.Service.Snapshot.Nutrient==5 && f.Service.Snapshot.MbctNextOrder==2,"Review advanced/rewarded twice.");
            }
        }
        static void MbctRoutineFlow()
        {
            using(var f=new Fixture(0,5))
            {
                for(int i=1;i<=3;i++)
                {
                    f.Service.BeginSession("seed-"+i,MbctPolicy.MissionId(i),i);f.Service.StartSession("seed-"+i);
                    Assert(f.Service.CompleteSession("seed-"+i),"Seed course step failed.");
                    f.Clock.UtcNow=f.Clock.UtcNow.AddDays(1);f.Ui.RefreshCycle();
                }
                Assert(f.Service.SavePreference("routine","손 씻기"),"Routine preference seed failed.");
                f.Ui.SelectMission(MbctContent.Course[3]);Click(f.Ui,"시작");
                var field=f.Ui.GetComponentInChildren<InputField>(true);
                Assert(field.text=="손 씻기","Routine default not reused.");field.text="창가에서 물 마시기";
                f.Store.FailNextSaves=1;Click(f.Ui,"다음");
                Assert(f.Service.Snapshot.Sessions.Last().InstructionStep==0 && field.text=="창가에서 물 마시기","Failed input save lost draft.");
                f.Ui.LeaveActivityPanel();
                Click(f.Ui,"다음");Click(f.Ui,"실습 마치기");Click(f.Ui,"기록 건너뛰고 완료");
                Assert(f.Service.Snapshot.PracticePreferences.Single(a=>a.Key=="routine").Value=="창가에서 물 마시기","Custom routine not reusable.");
                Assert(f.Service.Snapshot.MbctNextOrder==5 && f.Service.Snapshot.Nutrient==20,"Routine completion incorrect.");
            }
        }
        static void MbctJournalFlow()
        {
            using(var f=new Fixture(0,5))
            {
                f.Ui.ShowExperienceTypes();Click(f.Ui,"불편함");
                f.Ui.GetComponentInChildren<InputField>(true).text="메시지 답장이 늦었어요";Click(f.Ui,"다음");
                f.Ui.GetComponentInChildren<InputField>(true).text="가슴이 답답했어요";Click(f.Ui,"다음");
                Click(f.Ui,"울적해요");Click(f.Ui,"다음");
                f.Ui.GetComponentInChildren<InputField>(true).text="나를 싫어하나 봐";
                f.Store.FailNextSaves=1;Click(f.Ui,"기록 저장");
                Assert(f.Ui.CurrentScreen=="experience" && f.Service.Snapshot.Experiences.Count==0,"Journal failed save partially committed.");
                Click(f.Ui,"기록 저장");var entry=f.Service.Snapshot.Experiences.Single();
                Assert(entry.Type=="불편함" && entry.Emotion=="울적해요" && entry.Thought=="나를 싫어하나 봐" && f.Service.Snapshot.Nutrient==0,"Journal content/reward mismatch.");
                f.Ui.ShowPlanField("support");f.Ui.GetComponentInChildren<InputField>(true).text="치료진";Click(f.Ui,"저장");
                f.Ui.ShowPlanField("support");Assert(f.Ui.GetComponentInChildren<InputField>(true).text=="치료진","Saved plan could not be reviewed.");
                f.Ui.ShowExperienceRecord(entry.Id);
                var scroll=f.Ui.GetComponentInChildren<ScrollRect>(true);
                Assert(scroll!=null && scroll.viewport.GetComponent<Image>().raycastTarget,"Long journal content has no scroll input target.");
            }
        }

        static void PreviewAvailability()
        {
            using(var f=new Fixture(0))
            {
                string before=StateCodec.Encode(f.Service.Snapshot);
                f.Ui.PreviewGrowth();
                Assert(!Button(f.Ui,"확정").interactable,"Growth can be confirmed without nutrient");
                Assert(f.Ui.GetComponentsInChildren<Text>().Any(t=>t.text.Contains("영양제가 10개 더 필요해요")),"Missing visible growth availability explanation");
                Click(f.Ui,"취소");f.Ui.PreviewEnvironment("environment:E01");
                Assert(!Button(f.Ui,"확정").interactable,"Placement can be confirmed without nutrient");
                Click(f.Ui,"취소");
                Assert(StateCodec.Encode(f.Service.Snapshot)==before,"Unavailable previews changed saved state");
            }
            using(var f=new Fixture(30))
            {
                Assert(f.Service.PurchaseEnvironment("owned","environment:E01","left"),"Owned environment seed failed");
                f.Ui.PreviewEnvironment("environment:E01");
                Assert(!Button(f.Ui,"확정").interactable,"Owned environment remains purchasable");
                Assert(f.Ui.GetComponentsInChildren<Text>().Any(t=>t.text.Contains("이미 배치한 환경")),"Missing owned environment explanation");
                Click(f.Ui,"취소");f.Ui.PreviewGrowth();
                Assert(Button(f.Ui,"확정").interactable,"Affordable growth was disabled");
                Assert(((RectTransform)Button(f.Ui,"확정").transform).anchorMin.x>((RectTransform)Button(f.Ui,"취소").transform).anchorMin.x,"Confirmation is not in the shared forward-action position");
            }
        }
        static void PracticeInputFeedback()
        {
            using(var f=new Fixture(0,5))
            {
                f.Ui.SelectMission(MbctContent.Course[0]);Click(f.Ui,"시작");Click(f.Ui,"물");
                var field=f.Ui.GetComponentInChildren<InputField>();
                Assert(Button(f.Ui,"물").GetComponent<Image>().color==GardenUi.Pale,"Selected option lacks feedback");
                field.text="따뜻한 차";
                Assert(Button(f.Ui,"물").GetComponent<Image>().color==Color.white,"Custom entry retains a stale selected option");
                var label=field.transform.Find("Field label")?.GetComponent<Text>();
                Assert(label!=null && label.gameObject.activeSelf && label.text.Contains("선택"),"Field label disappears after entering text");
                Click(f.Ui,"다음");
                Assert(f.Service.Snapshot.Sessions.Last().Answers.Any(a=>a.Value=="따뜻한 차"),"Custom entry was not saved");
            }
        }

        static void SeedFullMbct(Fixture f)
        {
            for(int i=1;i<=48;i++)
            {
                while(MbctPolicy.Availability(f.Service.Snapshot)!=null){f.Clock.UtcNow=f.Clock.UtcNow.AddDays(1);f.Ui.RefreshCycle();}
                string id="layout-seed-"+i;Assert(f.Service.BeginSession(id,MbctPolicy.MissionId(i),i)&&f.Service.StartSession(id)&&f.Service.CompleteSession(id),"Layout seed failed");
                f.Clock.UtcNow=f.Clock.UtcNow.AddDays(1);f.Ui.RefreshCycle();
            }
        }
        static void MbctRenderReads()
        {
            using(var f=new Fixture(0,5))
            {
                SeedFullMbct(f);long reads=f.Service.SnapshotReads;f.Ui.ShowFree();
                Assert(f.Service.SnapshotReads-reads==1,"Free rendering cloned state inside catalog predicate");
                reads=f.Service.SnapshotReads;f.Ui.ShowCourse();
                Assert(f.Service.SnapshotReads-reads==1,"Course rendering cloned state for each row");
                var gardenBefore=GameObject.Find("Saved garden additions");f.Ui.UpdateGarden();f.Ui.UpdateGarden();
                Assert(gardenBefore==GameObject.Find("Saved garden additions"),"Unchanged state rebuilt garden geometry");
            }
        }
        static void AssertSeparateControls(WeekOneQuestDemo ui)
        {
            var page=ui.transform.Find("Local garden flow");
            var controls=page.GetComponentsInChildren<Selectable>(true).Where(c=>c.transform.parent==page && c.gameObject.activeSelf).ToArray();
            for(int i=0;i<controls.Length;i++)for(int j=i+1;j<controls.Length;j++)
            {
                var a=(RectTransform)controls[i].transform;var b=(RectTransform)controls[j].transform;
                float width=Mathf.Min(a.anchorMax.x,b.anchorMax.x)-Mathf.Max(a.anchorMin.x,b.anchorMin.x);
                float height=Mathf.Min(a.anchorMax.y,b.anchorMax.y)-Mathf.Max(a.anchorMin.y,b.anchorMin.y);
                Assert(width<=.0001f || height<=.0001f,"Overlapping controls: "+a.name+" / "+b.name+" on "+ui.CurrentScreen);
            }
        }
        static void MbctLayouts()
        {
            using(var f=new Fixture(0,5))
            {
                SeedFullMbct(f);f.Ui.ShowHome();AssertSeparateControls(f.Ui);f.Ui.ShowCourse();AssertSeparateControls(f.Ui);f.Ui.ShowFree();AssertSeparateControls(f.Ui);
                foreach(var mission in MbctContent.Course)
                {
                    f.Ui.SelectMission(mission);AssertSeparateControls(f.Ui);Click(f.Ui,"시작");
                    for(int i=0;i<mission.steps.Length;i++){AssertSeparateControls(f.Ui);f.Ui.AdvancePractice(true);}
                    AssertSeparateControls(f.Ui);
                    var faces=f.Ui.GetComponentsInChildren<CapstoneDesign.Prototype.PrototypeMoodFace>(true);
                    Assert(faces.Length==5,"Reflection did not reuse five design faces");
                    Click(f.Ui,"기뻐요");Assert(faces[0].selected && faces.Skip(1).All(face=>!face.selected),"Face selection did not highlight exactly one");
                    Click(f.Ui,"기뻐요");Assert(faces.All(face=>!face.selected),"Repeated face selection did not clear optional emotion");
                    Click(f.Ui,"기록 건너뛰고 완료");
                }
                f.Ui.ShowExperienceTypes();AssertSeparateControls(f.Ui);Click(f.Ui,"불편함");AssertSeparateControls(f.Ui);Click(f.Ui,"다음");Click(f.Ui,"다음");AssertSeparateControls(f.Ui);
                var emotionFaces=f.Ui.GetComponentsInChildren<CapstoneDesign.Prototype.PrototypeMoodFace>(true);Assert(emotionFaces.Length==5,"Journal did not reuse faces");
                Click(f.Ui,"차분해요");var input=f.Ui.GetComponentInChildren<InputField>(true);input.text="나만의 감정";
                Assert(emotionFaces.All(face=>!face.selected),"Custom emotion retained a stale selected face");
                f.Ui.ShowPlanField("warning-sign");AssertSeparateControls(f.Ui);
                f.Ui.ShowRecord(f.Service.Snapshot.Sessions.First().SessionId);AssertSeparateControls(f.Ui);
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
            public readonly DemoBalanceConfig Balance = new DemoBalanceConfig { CompletionNutrient=10, VisitorChancePercent = 0 };
            public readonly GardenStateService Service;
            public readonly WeekOneQuestDemo Ui;
            readonly Scene scene, previous;
            readonly HashSet<int> previousRootIds;
            public Fixture(int nutrient = 0, int completionReward = 10)
            {
                Balance.CompletionNutrient=completionReward;
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
