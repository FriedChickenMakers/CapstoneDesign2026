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
            public string environment = "Unity Editor edit-mode / legacy MBCT and current shell regression / synthetic fixtures / no device";
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
            Check(report, "Course duration choice confirms, retries failed saves and selects every program", ProgramSelection);
            Check(report, "Every course connects completed activity, growth and original reflection without duplicate spending", GrowthRecordFlow);
            Check(report, "Empty and unlinked growth records never invent an activity association", GrowthRecordOptional);
            Check(report, "Weekly and cumulative summaries count actual participation without changing records", RecordSummaryFlow);
            Check(report, "UI completion without notes commits one reward and survives reopen", Completion);
            Check(report, "M15 debug steps, direct completion and garden purchase form one demo flow", M15DemoFlow);
            Check(report, "Walking auto-completion preserves reflection after leaving the panel and retries failed saves", WalkingReflectionAutoCompletion);
            Check(report, "Walking baseline refresh gates start, updates only its introduction and freezes started goals", WalkingBaselineUiRefresh);
            Check(report, "UI active session survives navigation and pause", Navigation);
            Check(report, "Activity home follows the selected and paused review, and records distinguish completion from rewards", ActivityPresentation);
            Check(report, "Course first entry follows progress and preserves manual browsing", CourseInitialPage);
            Check(report, "Back returns through lists, detail parameters and unpaid previews", BackPageFlow);
            Check(report, "Back preserves drafts, blocks failed saves and rewinds guided steps", BackDraftFlow);
            Check(report, "Android keyboard Back retains the final draft before end-edit callbacks", KeyboardDismissal);
            Check(report, "UI save failure retains participation and permits retry", SaveFailure);
            Check(report, "Environment preview cancellation and confirmation are transactional", Placement);
            Check(report, "Skipped dates preserve course and do not repay completion", DateAdvance);
            Check(report, "Health DTO native JSON preserves null, zero, source and long timestamps", HealthDto);
            Check(report, "Health DTO status vocabulary is not silently available", StatusVocabulary);
            Check(report, "Heart graph uses a separate Graphic child and exact session samples", HeartGraph);
            Check(report, "Heart refresh replaces only the chart once and ignores results after navigation", HeartRefreshUi);
            Check(report, "Mission selection aborts after a one-shot cycle save failure", CycleSelectionFailure);
            Check(report, "External navigation cancels environment and growth previews", ExternalNavigationPreview);
            Check(report, "Active navigation refreshes dates while activity panel is inactive", InactivePanelCycle);
            Check(report, "Failed completion retains optional draft for successful retry", OptionalDraftRetry);
            Check(report, "MBCT guided steps reward five and reviews preserve quota", MbctGuidedFlow);
            Check(report, "MBCT routine draft, failure retry and reuse stay connected", MbctRoutineFlow);
            Check(report, "Experience journal reuses design emotions and personal plans are editable", MbctJournalFlow);
            Check(report, "Free activity recommendation preferences persist without changing course, rewards or records", RecommendationPreferencesFlow);
            Check(report, "Course and free rendering take one isolated snapshot", MbctRenderReads);
            Check(report, "All guided controls and face selectors stay separated", MbctLayouts);
            Check(report, "Purchase previews explain unavailable actions without spending", PreviewAvailability);
            Check(report, "Custom practice input clears previous choice and retains its label", PracticeInputFeedback);
            Check(report, "Activity actions stay outside scrolling content and preserve paused drafts", FixedActivityActions);
            Check(report, "Skipping optional input removes this draft and preserves prior preferences", SkipPracticeInput);
            Check(report, "Session refresh preserves unsaved input and aborts after storage failure", RefreshPracticeDraft);
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
                Assert(service.SelectMbctProgram("8w"), service.LastError);
                ShowLegacy(loop, "Home");
                home.RefreshFromState();
                Assert(home.ProceduralPlant != null && home.RenderedRecipe.GrowthStep == 0
                    && home.guide.text.Contains("영양제 50개"), "Fresh saved state did not show a generated seed and nutrient.");
                GardenProceduralValidation.AssertGeneratedView(home);
                string seed = home.RenderedRecipe.BaseSeed;
                string beforeMesh = GardenProceduralValidation.MeshSignature(home.ProceduralPlant);
                var originalMesh = home.ProceduralPlant.GetComponent<MeshFilter>().sharedMesh;
                home.RefreshFromState();
                Assert(home.ProceduralPlant.GetComponent<MeshFilter>().sharedMesh == originalMesh,
                    "Refreshing unchanged state needlessly rebuilt the generated mesh.");
                loop.ShowShop(); loop.PreviewGrowth();
                Assert(home.RenderedRecipe.GrowthStep == 3
                    && GardenProceduralValidation.MeshSignature(home.ProceduralPlant) != beforeMesh
                    && service.Snapshot.Nutrient == 50, "Seed preview did not generate the next stage without spending.");
                Click(loop, "취소");
                Assert(home.RenderedRecipe.GrowthStep == 0
                    && GardenProceduralValidation.MeshSignature(home.ProceduralPlant) == beforeMesh
                    && service.Snapshot.Nutrient == 50, "Cancelling the seed preview did not restore the committed generated mesh.");
                for (int i = 1; i <= 3; i++)
                {
                    Assert(service.GrowPlant("home-growth-" + i, "plant:P06"), "Growth purchase failed.");
                    loop.UpdateGarden();
                    Assert(GardenHomePresenter.StageForGrowth(service.Snapshot.Plants.Single().Growth) == i,
                        "Saved growth did not advance exactly one visible stage.");
                    Assert(home.RenderedRecipe.GrowthStep == new[] { 0, 3, 4, 12 }[i]
                        && home.RenderedRecipe.BaseSeed == seed, "Generated stage or stable seed disagrees with committed growth.");
                    string afterMesh = GardenProceduralValidation.MeshSignature(home.ProceduralPlant);
                    Assert(afterMesh != beforeMesh, "Growth purchase did not change the visible generated geometry.");
                    beforeMesh = afterMesh;
                    GardenProceduralValidation.AssertGeneratedView(home);
                }
                Assert(home.RenderedRecipe.Mature && home.RenderedRecipe.SpeciesId == "species:chamomile"
                    && home.guide.text.Contains("영양제 20개"),
                    "Bloom or persisted nutrient disagrees with the garden state.");
                float beforeScale = home.ProceduralPlant.transform.localScale.x;
                loop.ShowShop(); loop.PreviewGrowth();
                Assert(home.gardenVisuals.activeSelf && loop.GetComponentInChildren<RawImage>(true)?.texture==home.gardenCamera.targetTexture,
                    "Purchase preview does not use the integrated garden texture.");
                Assert(home.ProceduralPlant.transform.localScale.x>beforeScale && service.Snapshot.Nutrient==20,
                    "Purchase preview either lacks the next growth or spends saved nutrient.");
                Click(loop, "취소");
                Assert(Mathf.Approximately(home.ProceduralPlant.transform.localScale.x, beforeScale)
                    && GardenProceduralValidation.MeshSignature(home.ProceduralPlant) == beforeMesh
                    && service.Snapshot.Nutrient == 20, "Cancelled purchase changed the generated home plant.");
                store.FailSave = true;
                Assert(!service.GrowPlant("home-failed-save", "plant:P06"), "Injected save failure was not exercised.");
                loop.UpdateGarden();
                Assert(Mathf.Approximately(home.ProceduralPlant.transform.localScale.x, beforeScale)
                    && GardenProceduralValidation.MeshSignature(home.ProceduralPlant) == beforeMesh
                    && service.Snapshot.Nutrient == 20, "Failed purchase changed the generated home plant.");
                store.FailSave = false;
                Assert(service.GrowPlant("home-growth-4", "plant:P06"), "Later growth purchase failed.");
                loop.UpdateGarden();
                Assert(home.ProceduralPlant.transform.localScale.x > beforeScale, "Growth after bloom had no visible change.");

                home.SendMessage("Awake", SendMessageOptions.RequireReceiver);
                nav.ShowIsland();
                home.activityTab.onClick.Invoke();
                var missionNavigation = loop.GetComponentsInChildren<MissionBottomNavigation>().Single();
                Assert(nav.activitiesPanel.activeSelf && !home.IsGardenVisible && missionNavigation.MissionButton.gameObject.activeInHierarchy
                    && missionNavigation.ActiveTab == "미션", "Activity entry did not preserve the shared navigation.");
                nav.ShowIsland(); home.TemporarySettingsButton.onClick.Invoke();
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
                Assert(home.RenderedRecipe.Mature && home.RenderedRecipe.BaseSeed == seed
                    && GardenProceduralValidation.MeshSignature(home.ProceduralPlant) == beforeMesh
                    && home.guide.text.Contains("영양제 10개")
                    && home.ProceduralPlant.transform.localScale.x > beforeScale,
                    "Reopened saved growth did not restore the same generated home plant.");
                GardenProceduralValidation.AssertGeneratedView(home);
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
            // Diagnostics now live inside the explicitly opened development tools.
            sensor.TestToolsToggleButton.onClick.Invoke();
            sensor.ToggleDiagnostics();
            Assert(sensor.output.text.Contains("PLATFORM") && sensor.output.text.Contains("HEALTH") &&
                sensor.output.text.Contains("ACTION:"), "Expanding diagnostics did not populate current data immediately.");
            sensor.ToggleDiagnostics();
            string previous = sensor.output.text;
            reads = loop.Service.SnapshotReads;
            sensor.SendMessage("RefreshText", SendMessageOptions.RequireReceiver);
            Assert(sensor.output.text == previous && loop.Service.SnapshotReads == reads,
                "Closing diagnostics did not suspend its formatting and state reads.");
            VerifyStableCollectorSummary(sensor);
        }

        static void VerifyStableCollectorSummary(SensorRawDisplay sensor)
        {
            const System.Reflection.BindingFlags hidden = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
            var label = (Text)typeof(SensorRawDisplay).GetField("collectorStatus",hidden).GetValue(sensor);
            bool openedDiagnostics = !label.IsActive();
            if(openedDiagnostics)sensor.ToggleDiagnostics();
            var refresh = typeof(SensorRawDisplay).GetMethod("RefreshSummary",hidden);
            var snapshot = new AndroidPlatformSnapshot { inputMode="LIVE" };
            var daily = new DailyAccelerationSnapshot { status="RUNNING", lastWriteEpochMs=1600000000000L };
            var parameters = new object[] { snapshot,snapshot.healthConnect,snapshot.runtimePermissions,daily };
            int vertices=0, layout=0;
            UnityEngine.Events.UnityAction vertexDirty=()=>vertices++, layoutDirty=()=>layout++;
            label.RegisterDirtyVerticesCallback(vertexDirty);label.RegisterDirtyLayoutCallback(layoutDirty);
            try
            {
                Assert(label.IsActive(),"Collector label fixture must be active to observe dirty callbacks.");
                refresh.Invoke(sensor,parameters);
                Assert(vertices>0 && layout>0,"Changed collector summary did not exercise dirty callbacks.");
                string first=label.text;vertices=0;layout=0;
                refresh.Invoke(sensor,parameters);
                Assert(vertices==0 && layout==0 && label.text==first,"Identical collector summary dirtied its graphic or layout.");
                daily.lastWriteEpochMs+=120000;
                refresh.Invoke(sensor,parameters);
                Assert(label.text!=first && vertices>0 && layout>0,"New collector write time did not update visible status.");
            }
            finally
            {
                label.UnregisterDirtyVerticesCallback(vertexDirty);label.UnregisterDirtyLayoutCallback(layoutDirty);
                if(openedDiagnostics)sensor.ToggleDiagnostics();
            }
        }

        static void VerifySharedHeader(MockupNavigation nav, WeekOneQuestDemo loop)
        {
            nav.ShowIsland();
            Assert(!nav.home.oldCamera.enabled && !nav.home.oldLight.enabled,
                "Integrated Home enabled the hidden legacy renderer.");
            var header = nav.home.homeCanvas.transform.Find("SafePortraitFrame/MainContent/Brand").GetComponent<TMPro.TMP_Text>();
            var font = header.font;
            var sensor = nav.GetComponentInChildren<SensorRawDisplay>(true);
            sensor.PrepareView();
            // Focus pages own their header/navigation; settings retains the fixed
            // home-canvas brand. Both shells must stay fixed above their scroll body.
            foreach (bool settings in new[] { false, true })
            {
                if (settings) nav.ShowSettings(); else nav.ShowActivities();
                Assert(!nav.home.oldCamera.enabled && !nav.home.oldLight.enabled,
                    "An integrated tab enabled the hidden legacy renderer.");
                var scroll = settings ? sensor.PrimaryScroll : loop.GetComponentInChildren<ScrollRect>();
                Assert(scroll != null, "Current tab did not provide its scroll body.");
                var navigation = settings ? nav.home.NavigationBar : loop.GetComponentsInChildren<MissionBottomNavigation>().Single();
                Assert(navigation != null && navigation.gameObject.activeInHierarchy,
                    "Current tab lost its shared three-destination navigation.");
                var rect = (RectTransform)navigation.transform;
                Canvas.ForceUpdateCanvases();
                var corners = new Vector3[4]; rect.GetWorldCorners(corners);
                var headerCorners = new Vector3[4]; header.rectTransform.GetWorldCorners(headerCorners);
                foreach (float position in new[] { 1f, 0f })
                {
                    scroll.verticalNormalizedPosition = position;
                    Canvas.ForceUpdateCanvases();
                    var actual = new Vector3[4]; rect.GetWorldCorners(actual);
                    Assert(actual.SequenceEqual(corners), "Bottom navigation moved with scrolling content.");
                    Assert(new[] { navigation.HomeButton, navigation.MissionButton, navigation.CourseButton }
                        .All(button => button != null && button.IsActive() && button.interactable),
                        "A shared destination became unavailable while scrolling.");
                    if (settings)
                    {
                        var actualHeader = new Vector3[4]; header.rectTransform.GetWorldCorners(actualHeader);
                        Assert(header.isActiveAndEnabled && header.font == font && actualHeader.SequenceEqual(headerCorners),
                            "Settings brand typography or position changed while scrolling.");
                    }
                    else Assert(!nav.home.homeCanvas.gameObject.activeInHierarchy,
                        "Inactive home chrome overlays the Focus page's own header/navigation.");
                    Assert(!nav.GetComponentsInChildren<Text>().Any(text => text.text == "마음 정원"),
                        "A legacy duplicate brand is still visible behind the current header.");
                }
            }
            var cover = header.transform.parent.Find("Shared header background").GetComponent<Image>();
            Assert(cover.gameObject.activeInHierarchy && cover.color.a == 1 && cover.raycastTarget,
                "Settings controls can show through or receive taps behind the fixed brand.");
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

        static void ProgramSelection()
        {
            foreach (string program in new[] { "2w", "4w", "8w" })
            using (var f = new Fixture(chooseDefaultProgram: false))
            {
                ShowLegacy(f.Ui, "Home");
                Assert(f.Ui.CurrentScreen == "programs" && !f.Service.Snapshot.MbctProgramChosen,
                    "A fresh account must choose its course duration.");
                int weeks = MbctPolicy.ProgramWeeks(program);
                Click(f.Ui, weeks + "주 · ");
                Assert(f.Ui.CurrentScreen == "program-confirm" && !f.Service.Snapshot.MbctProgramChosen,
                    "Reading a course description must not commit the selection.");
                f.Store.FailSave = true;
                Click(f.Ui, "이 기간으로 시작");
                Assert(!f.Service.Snapshot.MbctProgramChosen && f.Ui.CurrentScreen == "program-confirm",
                    "A failed save lost the confirmation or selected a course.");
                f.Store.FailSave = false;
                Click(f.Ui, "이 기간으로 시작");
                Assert(f.Service.Snapshot.MbctProgramChosen && MbctPolicy.ActiveProgram(f.Service.Snapshot) == program
                    && f.Ui.CurrentScreen == "home", "Retry did not open the selected course.");
                ShowLegacy(f.Ui, "Course");
                Assert(Button(f.Ui, "1일차  ").interactable && !Button(f.Ui, "2일차  ").interactable,
                    "Selecting a duration bypassed ordered course progress.");
            }
        }

        static void GrowthRecordFlow()
        {
            foreach(string program in new[]{"2w","4w","8w"})
            using(var f=new Fixture(0,5))
            {
                Assert(f.Service.SelectMbctProgram(program),f.Service.LastError);
                string remembered=null;
                var course=MbctContent.ForProgram(program);
                for(int index=0;index<2;index++)
                {
                    if(index>0){f.Clock.UtcNow=f.Clock.UtcNow.AddDays(1);Assert(f.Ui.RefreshCycle(),"Could not advance the synthetic course day.");}
                    ShowLegacy(f.Ui, "Course");Click(f.Ui,(index+1)+"일차  ");Click(f.Ui,"시작");
                    string session=f.Service.Snapshot.Sessions.Last().SessionId;
                    if(index==0)remembered=session;
                    for(int step=0;step<course[index].steps.Length;step++)f.Ui.AdvancePractice(false);
                    Assert(f.Ui.CurrentScreen=="reflection",program+" guided activity did not reach its reflection.");
                    f.Ui.Finish("차분해요","합성 성장 연결 기록 "+index);
                    Assert(f.Ui.CurrentScreen=="complete" && f.Service.Snapshot.Nutrient==(index+1)*5,
                        program+" completion did not commit the ordinary five nutrient reward.");
                }
                ShowLegacy(f.Ui, "Home");f.Ui.ShowShop();f.Ui.PreviewGrowth();
                string before=StateCodec.Encode(f.Service.Snapshot);
                Click(f.Ui,"함께 기억할 활동");Click(f.Ui,course[0].title);
                Assert(f.Ui.CurrentScreen=="growth" && StateCodec.Encode(f.Service.Snapshot)==before,
                    "Selecting a remembered activity prematurely saved growth or changed activity history.");
                Click(f.Ui,"취소");f.Ui.PreviewGrowth();
                Assert(Button(f.Ui,"함께 기억할 활동").GetComponentsInChildren<Text>(true).Any(text=>text.text=="선택하지 않아도 괜찮아요"),
                    "Cancelling a purchase leaked its remembered activity into the next purchase.");
                Click(f.Ui,"함께 기억할 활동");Click(f.Ui,course[0].title);
                Click(f.Ui,"함께 기억할 활동");
                Assert(f.Ui.TryNavigateBack() && f.Ui.CurrentScreen=="growth"
                    && Button(f.Ui,"함께 기억할 활동").GetComponentsInChildren<Text>(true).Any(text=>text.text==course[0].title),
                    "Back from the activity picker lost the current preview's explicit selection.");
                Click(f.Ui,"함께 기억할 활동");Click(f.Ui,"연결하지 않고 계속");
                Assert(Button(f.Ui,"함께 기억할 활동").GetComponentsInChildren<Text>(true).Any(text=>text.text=="선택하지 않아도 괜찮아요"),
                    "Choosing no link retained the previous association.");
                Click(f.Ui,"함께 기억할 활동");Click(f.Ui,course[0].title);
                f.Store.FailNextSaves=1;Click(f.Ui,"확정");
                Assert(f.Ui.CurrentScreen=="growth" && StateCodec.Encode(f.Service.Snapshot)==before,
                    "A failed linked growth save changed nutrient, recipe, or history.");
                Click(f.Ui,"확정");
                var state=f.Service.Snapshot;
                Assert(state.Nutrient==0 && state.GrowthHistory.Count==1 && state.GrowthHistory[0].LinkedSessionId==remembered
                    && state.PurchaseReceipts.Count==1 && MbctPolicy.NextOrder(state)==3,
                    "Retry did not atomically commit one growth purchase linked to the chosen original activity.");
                string committed=StateCodec.Encode(state);
                ShowLegacy(f.Ui, "Records");Click(f.Ui,"식물 성장 기록");Click(f.Ui,"씨앗 → 새싹");
                Assert(f.Ui.CurrentScreen=="growth-record","Growth list did not open its detail.");
                Click(f.Ui,"그때의 활동과 감정 보기");
                string recordText=string.Join("\n",f.Ui.GetComponentsInChildren<Text>(true).Select(text=>text.text));
                Assert(f.Ui.CurrentScreen=="record" && recordText.Contains(course[0].title)
                    && recordText.Contains("차분해요") && recordText.Contains("합성 성장 연결 기록 0"),
                    "Growth detail did not navigate to the exact selected activity and its stored reflection.");
                Assert(f.Ui.TryNavigateBack() && f.Ui.CurrentScreen=="growth-record"
                    && f.Ui.TryNavigateBack() && f.Ui.CurrentScreen=="growth-history",
                    "Back from a linked record lost the growth detail or history.");
                Assert(StateCodec.Encode(f.Service.Snapshot)==committed,"Reading a linked record changed saved data.");
            }
        }

        static void GrowthRecordOptional()
        {
            using(var f=new Fixture(30))
            {
                ShowLegacy(f.Ui, "Records");Click(f.Ui,"식물 성장 기록");
                Assert(f.Ui.GetComponentsInChildren<Text>(true).Any(text=>text.text.Contains("다음에 식물을 키우면")),
                    "Empty growth history did not explain how it is created.");
                f.Ui.ShowShop();f.Ui.PreviewGrowth();Click(f.Ui,"함께 기억할 활동");
                Assert(f.Ui.GetComponentsInChildren<Text>(true).Any(text=>text.text.Contains("아직 연결할 활동 기록이 없어요")),
                    "An empty activity picker did not offer a clear optional path.");
                string before=StateCodec.Encode(f.Service.Snapshot);
                Assert(f.Ui.LeaveActivityPanel() && f.Ui.CurrentScreen=="shop" && StateCodec.Encode(f.Service.Snapshot)==before,
                    "Leaving the growth picker retained an unpaid purchase or changed persistent data.");
                f.Ui.PreviewGrowth();Click(f.Ui,"함께 기억할 활동");
                Click(f.Ui,"연결하지 않고 계속");Click(f.Ui,"확정");
                Assert(f.Service.Snapshot.GrowthHistory.Single().LinkedSessionId=="", "Optional growth invented an activity association.");
                f.Ui.ShowGrowthHistory();Click(f.Ui,"씨앗 → 새싹");
                Assert(f.Ui.CurrentScreen=="growth-record"
                    && f.Ui.GetComponentsInChildren<Text>(true).Any(text=>text.text.Contains("연결한 활동 기록이 없어요"))
                    && !f.Ui.GetComponentsInChildren<Button>(true).Any(button=>button.name=="그때의 활동과 감정 보기"),
                    "An unlinked growth showed a fabricated source record or broken navigation.");
            }
        }

        static void RecordSummaryFlow()
        {
            using(var f=new Fixture(0,5))
            {
                ShowLegacy(f.Ui, "Records");Click(f.Ui,"이번 주와 지금까지");
                Assert(f.Ui.GetComponentsInChildren<Text>(true).Count(text=>text.text=="활동 완료 0회 · 참여 종료 0회")==2,
                    "Empty summaries did not show explicit zero participation.");
                Assert(f.Service.BeginSession("summary-older",MbctPolicy.MissionId(1),1)
                    && f.Service.StartSession("summary-older") && f.Service.CompleteSession("summary-older")
                    && f.Service.SaveExperience("summary-old-experience","즐거움","합성 장면",null,null,null),f.Service.LastError);
                f.Clock.UtcNow=f.Clock.UtcNow.AddDays(7);Assert(f.Ui.RefreshCycle(),"Could not advance the synthetic summary week.");
                Assert(f.Service.BeginSession("summary-current",MbctPolicy.MissionId(2),2)
                    && f.Service.StartSession("summary-current") && f.Service.CompleteSession("summary-current")
                    && f.Service.BeginSession("summary-ended",MbctPolicy.MissionId(1),1)
                    && f.Service.StartSession("summary-ended") && f.Service.EndParticipation("summary-ended")
                    && f.Service.SaveExperience("summary-current-a","불편함",null,null,null,null)
                    && f.Service.SaveExperience("summary-current-b","불편함",null,null,null,null)
                    && f.Service.SaveExperience("summary-current-c","중립",null,null,null,null)
                    && f.Service.BeginSession("summary-unfinished",MbctPolicy.MissionId(1),1),f.Service.LastError);
                ShowLegacy(f.Ui, "Records");string before=StateCodec.Encode(f.Service.Snapshot);Click(f.Ui,"이번 주와 지금까지");
                var labels=f.Ui.GetComponentsInChildren<Text>(true).Select(text=>text.text).ToArray();
                Assert(labels.Contains("활동 완료 1회 · 참여 종료 1회") && labels.Contains("활동 완료 2회 · 참여 종료 1회")
                    && labels.Contains("경험 기록 3개") && labels.Contains("경험 기록 4개")
                    && labels.Count(text=>text=="불편함 · 2개")==2 && labels.Count(text=>text=="즐거움 · 1개")==1,
                    "Weekly and cumulative summaries mixed periods, omitted ended participation, or counted unfinished work.");
                Assert(f.Ui.TryNavigateBack() && f.Ui.CurrentScreen=="records" && StateCodec.Encode(f.Service.Snapshot)==before,
                    "Summary navigation changed participation or failed to return to its record list.");
            }
        }

        static void Completion()
        {
            using (var f = new Fixture())
            {
                ShowLegacy(f.Ui, "Course");
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
                ShowLegacy(f.Ui, "Course");ShowLegacy(f.Ui, "Course");ShowLegacy(f.Ui, "Course");
                Assert(f.Ui.TryNavigateBack() && f.Ui.CurrentScreen=="home", "Page refreshes polluted Back history.");
                ShowLegacy(f.Ui, "Records");f.Ui.ShowExperienceHistory();f.Ui.ShowPersonalPlan();f.Ui.ShowPlanField("support");
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
                ShowLegacy(f.Ui, "Home");f.Ui.ShowShop();
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
                ShowLegacy(f.Ui, "Course");f.Ui.SelectMission(MbctContent.Course[0]);Click(f.Ui,"시작");
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
                ShowLegacy(f.Ui, "Records");f.Ui.ShowExperienceTypes();Click(f.Ui,"즐거움");
                f.Ui.GetComponentInChildren<InputField>(true).text="synthetic experience draft";Click(f.Ui,"다음");
                Assert(f.Ui.TryNavigateBack() && f.Ui.GetComponentInChildren<InputField>(true).text=="synthetic experience draft",
                    "Experience step Back lost the previous answer.");
                for(int step=0;step<4;step++)Click(f.Ui,step==3?"기록 저장":"다음");
                Assert(f.Ui.TryNavigateBack() && f.Ui.CurrentScreen=="records", "Saved experience Back reopened its submitted form.");
            }
        }

        static void WalkingBaselineUiRefresh()
        {
            const System.Reflection.BindingFlags hidden=System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
            var providerField=typeof(AndroidPlatformBridge).GetField("provider",System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
            var previous=providerField.GetValue(null);
            try
            {
                using(var f=new Fixture(0,5))
                {
                    var provider=new WalkingRefreshProvider {Snapshot=WalkingRefreshSnapshot(f.Clock.UtcNow.AddHours(-7),10000)};
                    providerField.SetValue(null,provider);
                    // Match the serialized scene's non-null default DTO. LIVE must ignore it.
                    f.Ui.PreviewHealth=new AndroidPlatformSnapshot{inputMode="LIVE",status="NO_DATA"};
                    typeof(WeekOneQuestDemo).GetField("preview",hidden).SetValue(f.Ui,false);
                    typeof(WeekOneQuestDemo).GetField("mode",hidden).SetValue(f.Ui,"LIVE");
                    f.Ui.OnAppReturned();f.Ui.OnAppReturned();
                    f.Ui.SelectMission(MindfulnessContent.FindMission("free-mission:M15"));
                    var start=(Button)typeof(WeekOneQuestDemo).GetField("walkingIntroStart",hidden).GetValue(f.Ui);
                    var goal=(Text)typeof(WeekOneQuestDemo).GetField("walkingIntroGoal",hidden).GetValue(f.Ui);
                    Assert(provider.Requests==1 && !start.interactable && goal.text.Contains("5,000") &&
                        start.name=="시작" && start.GetComponentInChildren<Text>(true).text=="확인 중…",
                        "Stale return did not gate/label Start or duplicated native requests.");
                    typeof(WeekOneQuestDemo).GetMethod("StartSelectedWalking",hidden).Invoke(f.Ui,null);
                    Assert(f.Service.Snapshot.Sessions.Single().Status==ParticipationState.Selected,"Immediate Start froze an old default while refreshing.");
                    provider.Snapshot=WalkingRefreshSnapshot(f.Clock.UtcNow,10000);
                    typeof(WeekOneQuestDemo).GetField("nextWalkingBaselinePoll",hidden).SetValue(f.Ui,0f);f.Ui.TickWalkingPersonalization();
                    Assert(start.interactable && start.GetComponentInChildren<Text>(true).text=="시작" && goal.text.Contains("8,000") &&
                        ReferenceEquals(goal,typeof(WeekOneQuestDemo).GetField("walkingIntroGoal",hidden).GetValue(f.Ui)),
                        "Serialized preview DTO masked fresh LIVE data or prevented in-place goal/button update.");
                    Click(f.Ui,"시작");var saved=f.Service.Snapshot.Sessions.Single();
                    Assert(saved.WalkingGoalSteps==8000 && provider.Requests==1,"Fresh Start did not persist target without a duplicate read.");
                    string windowStart=saved.WalkingQueryStartUtc,windowEnd=saved.WalkingQueryEndUtc;
                    f.Ui.ShowReflection();var input=f.Ui.GetComponentInChildren<InputField>(true);input.text="합성 목표 갱신 회고";
                    f.Clock.UtcNow=f.Clock.UtcNow.AddHours(7);f.Ui.OnAppReturned();
                    provider.Snapshot=WalkingRefreshSnapshot(f.Clock.UtcNow,6000);
                    typeof(WeekOneQuestDemo).GetField("nextWalkingBaselinePoll",hidden).SetValue(f.Ui,0f);f.Ui.TickWalkingPersonalization();
                    saved=f.Service.Snapshot.Sessions.Single();
                    Assert(f.Ui.CurrentScreen=="reflection" && ReferenceEquals(input,f.Ui.GetComponentInChildren<InputField>(true)) &&
                        input.text=="합성 목표 갱신 회고" && saved.WalkingGoalSteps==8000 && saved.WalkingQueryStartUtc==windowStart && saved.WalkingQueryEndUtc==windowEnd,
                        "Late health refresh rebuilt reflection or rewrote frozen target/date.");
                }
                using(var f=new Fixture(0,5))
                {
                    var provider=new WalkingRefreshProvider{Snapshot=WalkingRefreshSnapshot(f.Clock.UtcNow.AddHours(-7),10000)};
                    providerField.SetValue(null,provider);
                    f.Ui.PreviewHealth=new AndroidPlatformSnapshot{inputMode="LIVE",status="NO_DATA"};
                    typeof(WeekOneQuestDemo).GetField("preview",hidden).SetValue(f.Ui,false);
                    typeof(WeekOneQuestDemo).GetField("mode",hidden).SetValue(f.Ui,"LIVE");
                    f.Ui.SelectMission(MindfulnessContent.FindMission("free-mission:M15"));
                    var coordinator=(WalkingBaselineRefresh)typeof(WeekOneQuestDemo).GetField("walkingBaselineRefresh",hidden).GetValue(f.Ui);
                    typeof(WalkingBaselineRefresh).GetField("deadline",hidden).SetValue(coordinator,-1f);
                    typeof(WeekOneQuestDemo).GetField("nextWalkingBaselinePoll",hidden).SetValue(f.Ui,0f);f.Ui.TickWalkingPersonalization();
                    var start=(Button)typeof(WeekOneQuestDemo).GetField("walkingIntroStart",hidden).GetValue(f.Ui);
                    var explanation=(Text)typeof(WeekOneQuestDemo).GetField("walkingIntroExplanation",hidden).GetValue(f.Ui);
                    Assert(start.interactable && start.GetComponentInChildren<Text>(true).text=="시작" && explanation.text.Contains("기본 목표"),
                        "Timeout stranded Start or failed to restore its label and explain fallback.");
                    Click(f.Ui,"시작");
                    Assert(f.Service.Snapshot.Sessions.Single().WalkingGoalSteps==5000 && provider.Requests==1,
                        "Timeout fallback could not start or started a duplicate native read.");
                    provider.Snapshot=WalkingRefreshSnapshot(f.Clock.UtcNow,10000);
                    typeof(WeekOneQuestDemo).GetField("nextWalkingBaselinePoll",hidden).SetValue(f.Ui,0f);f.Ui.TickWalkingPersonalization();
                    Assert(f.Service.Snapshot.Sessions.Single().WalkingGoalSteps==5000,"Late timeout result rewrote an already started fallback goal.");
                }
                using(var f=new Fixture(0,5))
                {
                    f.Clock.UtcNow=DateTimeOffset.Parse("2026-09-23T23:59:00+09:00");
                    var provider=new WalkingRefreshProvider{Snapshot=WalkingRefreshSnapshot(f.Clock.UtcNow.AddHours(-7),10000)};
                    providerField.SetValue(null,provider);
                    f.Ui.PreviewHealth=new AndroidPlatformSnapshot{inputMode="LIVE",status="NO_DATA"};
                    typeof(WeekOneQuestDemo).GetField("preview",hidden).SetValue(f.Ui,false);
                    typeof(WeekOneQuestDemo).GetField("mode",hidden).SetValue(f.Ui,"LIVE");
                    f.Ui.SelectMission(MindfulnessContent.FindMission("free-mission:M15"));
                    provider.Snapshot=WalkingRefreshSnapshot(f.Clock.UtcNow,10000);
                    typeof(WeekOneQuestDemo).GetField("nextWalkingBaselinePoll",hidden).SetValue(f.Ui,0f);f.Ui.TickWalkingPersonalization();
                    Assert(provider.Requests==1,"Pre-midnight introduction duplicated its initial successful read.");
                    f.Clock.UtcNow=f.Clock.UtcNow.AddMinutes(2);Click(f.Ui,"시작");
                    Assert(provider.Requests==2 && f.Service.Snapshot.Sessions.Single().Status==ParticipationState.Selected,
                        "Start after midnight reused the prior success cooldown and froze yesterday's baseline.");
                    provider.Snapshot=WalkingRefreshSnapshot(f.Clock.UtcNow,10000);
                    typeof(WeekOneQuestDemo).GetField("nextWalkingBaselinePoll",hidden).SetValue(f.Ui,0f);f.Ui.TickWalkingPersonalization();
                    Click(f.Ui,"시작");
                    var saved=f.Service.Snapshot.Sessions.Single();
                    Assert(saved.WalkingGoalSteps==8000 && DateTimeOffset.Parse(saved.WalkingQueryStartUtc)==DateTimeOffset.Parse("2026-09-24T00:00:00+09:00"),
                        "Refreshed midnight start failed to freeze the new local date and target.");
                }
            }
            finally {providerField.SetValue(null,previous);}
        }

        static AndroidPlatformSnapshot WalkingRefreshSnapshot(DateTimeOffset at,int count)
        {
            var zone=TimeZoneInfo.CreateCustomTimeZone("Synthetic/Refresh",TimeSpan.FromHours(9),"Synthetic","Synthetic");
            var local=TimeZoneInfo.ConvertTime(at,zone);var midnight=new DateTimeOffset(local.Date,local.Offset);
            return new AndroidPlatformSnapshot{inputMode="LIVE",healthConnect=new HealthConnectSnapshot{
                status="AVAILABLE",lastRefreshEpochMs=at.ToUnixTimeMilliseconds(),personalization=new WalkingBaselineSnapshot{
                    status="AVAILABLE",queryComplete=true,lastUpdatedEpochMs=at.ToUnixTimeMilliseconds(),queryEndEpochMs=midnight.ToUnixTimeMilliseconds(),
                    days=Enumerable.Range(0,7).Select(i=>new DailyStepBaseline{hasValue=true,count=count,endEpochMs=midnight.AddDays(-i).ToUnixTimeMilliseconds()}).ToArray(),
                    sleep=new HealthMetricSnapshot{status="AVAILABLE",queryComplete=true,hasValue=true,value=300,measuredAtEpochMs=at.AddHours(-1).ToUnixTimeMilliseconds()}}}};
        }
        sealed class WalkingRefreshProvider:IPlatformDataProvider
        {
            public AndroidPlatformSnapshot Snapshot;public int Requests;
            public PlatformInputMode Mode=>PlatformInputMode.Live;
            public AndroidPlatformSnapshot GetSnapshot()=>Snapshot;
            public PlatformActionResult RefreshHealthData(){Requests++;Snapshot.healthConnect.refreshing=true;return new PlatformActionResult{status="AVAILABLE"};}
            public PlatformActionResult StartBackgroundSensorService()=>new PlatformActionResult{status="UNSUPPORTED"};
            public PlatformActionResult StopBackgroundSensorService()=>new PlatformActionResult{status="UNSUPPORTED"};
            public PlatformActionResult RequestSensorPermissions()=>new PlatformActionResult{status="UNSUPPORTED"};
            public PlatformActionResult RequestHealthConnectPermissions()=>new PlatformActionResult{status="UNSUPPORTED"};
            public string GetRecentSensorSamplesJson(int limit)=>"[]";
        }

        static void WalkingReflectionAutoCompletion()
        {
            using(var f=new Fixture(0,5))
            {
                f.Ui.SelectMission(MindfulnessContent.FindMission("free-mission:M15"));Click(f.Ui,"시작");
                string id=f.Service.Snapshot.Sessions.Single().SessionId;
                f.Ui.ShowReflection();Click(f.Ui,"기뻐요");
                f.Ui.GetComponentInChildren<InputField>(true).text="합성 걷기 회고 초안";
                Assert(f.Ui.LeaveActivityPanel(),"Could not leave reflection panel.");
                ShowLegacy(f.Ui, "Home"); // The reflection field is now gone; the session draft must survive.
                Assert(f.Service.ObserveMissionSteps(id,5000,WalkingPersonalization.AggregateSource),f.Service.LastError);
                const System.Reflection.BindingFlags hidden=System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
                var complete=typeof(WeekOneQuestDemo).GetMethod("CompleteWalkingGoal",hidden);
                var draft=typeof(WeekOneQuestDemo).GetField("draftNote",hidden);
                var activeId=typeof(WeekOneQuestDemo).GetField("sessionId",hidden);
                f.Store.FailNextSaves=1;complete.Invoke(f.Ui,new object[]{id});
                Assert(f.Service.Snapshot.Sessions.Single().Status==ParticipationState.InProgress &&
                    (string)draft.GetValue(f.Ui)=="합성 걷기 회고 초안" && f.Service.Snapshot.Nutrient==0,
                    "Failed automatic completion lost the reflection draft or committed a reward.");
                complete.Invoke(f.Ui,new object[]{id});
                var saved=f.Service.Snapshot.Sessions.Single();
                Assert(saved.Status==ParticipationState.RewardCommitted && saved.Mood=="기뻐요" &&
                    saved.Note=="합성 걷기 회고 초안" && f.Service.Snapshot.Nutrient==5,
                    "Leaving the activity panel caused automatic completion to drop its reflection.");
                Assert(activeId.GetValue(f.Ui)==null && draft.GetValue(f.Ui)==null,
                    "Successful automatic completion did not clear its stale UI session.");
                complete.Invoke(f.Ui,new object[]{id});
                Assert(f.Service.Snapshot.Nutrient==5 && f.Service.Snapshot.Sessions.Single().Note=="합성 걷기 회고 초안",
                    "Repeated completion erased reflection or duplicated its reward.");
                using(var reopened=new GardenStateService(f.Store,f.Clock,f.Balance))
                    Assert(reopened.Open() && reopened.Snapshot.Sessions.Single().Note=="합성 걷기 회고 초안",
                        "Auto-completed reflection did not survive a garden reload.");
            }
        }

        static void M15DemoFlow()
        {
            using (var f = new Fixture())
            {
                ShowLegacy(f.Ui, "Free"); Click(f.Ui, "걸음 기록과 걷기"); Click(f.Ui, "시작");
                Assert(f.Ui.ToggleDebugWalk(), "Could not enable demo walking mode.");
                for (int i = 0; i < 10; i++) Assert(f.Ui.AddVirtualDebugStep(), "Virtual step was not saved.");
                var walking = f.Service.Snapshot;
                Assert(walking.Nutrient == 1 && walking.DebugWalkPeriods.Single().RewardedUnits == 1,
                    "Ten debug steps did not grant exactly one nutrient.");
                Assert(f.Ui.GetComponentsInChildren<Text>(true).Any(t => t.text.Contains("10걸음")) &&
                    f.Ui.GetComponentsInChildren<Text>(true).Any(t => t.text.Contains("연습 10걸음")),
                    "M15 screen did not show debug goal and virtual progress.");
                Click(f.Ui, "직접 완료"); f.Clock.UtcNow = f.Clock.UtcNow.AddMinutes(2);
                Click(f.Ui, "기록 건너뛰고 완료");
                Assert(f.Service.Snapshot.Nutrient == 11 && f.Service.Snapshot.RewardReceipts.Count == 1,
                    "M15 direct completion did not add its separate reward once.");
                f.Ui.ShowShop(); Click(f.Ui, "식물 성장"); Click(f.Ui, "확정");
                var garden = f.Service.Snapshot;
                Assert(garden.Nutrient == 1 && garden.Plants.Any(p => p.PlantId == "plant:P06"),
                    "M15 reward could not be spent on garden growth.");
            }
        }

        static void Navigation()
        {
            using (var f = new Fixture())
            {
                f.Ui.SelectMission(MindfulnessContent.Course[0]); Click(f.Ui, "시작"); Click(f.Ui, "잠시 쉬기");
                ShowLegacy(f.Ui, "Home"); f.Ui.SelectMission(MindfulnessContent.FreeMissions[0]);
                Assert(f.Service.Snapshot.Sessions.Count == 1 && f.Service.Snapshot.Sessions[0].Status == ParticipationState.Paused, "Navigation cloned or lost active session.");
                Click(f.Ui, "이어서 하기"); Click(f.Ui, "잠시 쉬기"); Click(f.Ui, "오늘은 여기까지");
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

        static void HeartRefreshUi()
        {
            using (var f = new Fixture())
            {
                foreach(string id in new[]{"synthetic-heart-first","synthetic-heart-second"})
                {
                    Assert(f.Service.BeginSession(id,"free-mission:M15",0) && f.Service.StartSession(id),"Could not seed heart activity.");
                    f.Clock.UtcNow=f.Clock.UtcNow.AddMinutes(3);
                    Assert(f.Service.EndParticipation(id),"Could not finish heart activity.");
                }
                var first=f.Service.Snapshot.Sessions.First();
                long start=DateTimeOffset.Parse(first.StartedUtc).ToUnixTimeMilliseconds();
                long end=DateTimeOffset.Parse(first.EndedUtc).ToUnixTimeMilliseconds();
                var before=new AndroidPlatformSnapshot { inputMode="LIVE", healthConnect=new HealthConnectSnapshot {
                    status="AVAILABLE", heartRate=new HealthMetricSnapshot { status="NO_DATA" }
                }};
                f.Ui.PreviewHealth=before;f.Ui.ShowRecord(first.SessionId);
                const System.Reflection.BindingFlags hidden=System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
                var query=(HeartRangeRefresh)typeof(WeekOneQuestDemo).GetField("recordHeartRefresh",hidden).GetValue(f.Ui);
                var apply=typeof(WeekOneQuestDemo).GetMethod("ApplyRecordHeartRefresh",hidden);
                var chartField=typeof(WeekOneQuestDemo).GetField("recordHeartView",hidden);
                var pageField=typeof(WeekOneQuestDemo).GetField("pageViewport",hidden);
                var button=Button(f.Ui,"건강 기록 다시 조회");
                var page=(GameObject)pageField.GetValue(f.Ui);var scroll=page.GetComponent<ScrollRect>();
                scroll.verticalNormalizedPosition=.4f;float position=scroll.verticalNormalizedPosition;
                var oldChart=(GameObject)chartField.GetValue(f.Ui);
                Func<long,long,PlatformActionResult> accepted=(a,b)=>new PlatformActionResult { status="AVAILABLE" };
                query.Start("LIVE",start,end,before,0,accepted);apply.Invoke(f.Ui,null);
                Assert(!button.interactable && f.Ui.GetComponentsInChildren<Text>(true).Any(t=>t.text.Contains("조회하고 있어요")),
                    "Pending request did not show progress or block repeated taps.");
                var result=new AndroidPlatformSnapshot { inputMode="LIVE", healthConnect=new HealthConnectSnapshot {
                    status="AVAILABLE", lastRefreshEpochMs=20,
                    heartRate=new HealthMetricSnapshot { status="AVAILABLE",lastUpdatedEpochMs=20,
                        queryStartEpochMs=start,queryEndEpochMs=end,queryComplete=true,sampleCount=2,
                        heartRateSamples=new[]{
                            new HeartRateSampleSnapshot { measuredAtEpochMs=start+1000,beatsPerMinute=70,sourcePackage="synthetic-watch",segmentId=1 },
                            new HeartRateSampleSnapshot { measuredAtEpochMs=start+2000,beatsPerMinute=72,sourcePackage="synthetic-watch",segmentId=1 }
                        }
                    }
                }};
                query.Tick("LIVE",result,1,accepted);apply.Invoke(f.Ui,null);
                var chart=(GameObject)chartField.GetValue(f.Ui);
                Assert(chart!=oldChart && GraphVertexCount(chart.GetComponentInChildren<HeartSampleGraph>(true))>0,
                    "Completed exact query did not automatically replace the empty graph.");
                Assert(page==pageField.GetValue(f.Ui) && button==Button(f.Ui,"건강 기록 다시 조회") &&
                    Mathf.Approximately(position,scroll.verticalNormalizedPosition),"Heart completion rebuilt the page, controls or scroll position.");
                apply.Invoke(f.Ui,null);query.Tick("LIVE",result,2,accepted);apply.Invoke(f.Ui,null);
                Assert(chart==chartField.GetValue(f.Ui),"Unchanged result rebuilt the chart again.");
                query.Start("LIVE",start,end,result,3,accepted);apply.Invoke(f.Ui,null);
                f.Ui.ShowRecord("synthetic-heart-second");
                var secondPage=pageField.GetValue(f.Ui);var secondChart=chartField.GetValue(f.Ui);
                query.Tick("LIVE",result,4,accepted);apply.Invoke(f.Ui,null);
                Assert(!query.IsPending && query.Result==null && secondPage==pageField.GetValue(f.Ui) && secondChart==chartField.GetValue(f.Ui),
                    "Late first-activity result overwrote another activity.");
                query.Start("LIVE",start,end,before,5,accepted);apply.Invoke(f.Ui,null);
                typeof(WeekOneQuestDemo).GetMethod("OnDisable",hidden).Invoke(f.Ui,null);
                query.Tick("LIVE",result,6,accepted);apply.Invoke(f.Ui,null);
                Assert(!query.IsPending && query.Result==null && secondChart==chartField.GetValue(f.Ui),
                    "Leaving the activity tab accepted a late result.");
                query.Start("LIVE",start,end,before,7,accepted);ShowLegacy(f.Ui, "Records");
                query.Tick("LIVE",result,8,accepted);apply.Invoke(f.Ui,null);
                Assert(f.Ui.CurrentScreen=="records" && query.Result==null,"Leaving the detail page reopened it after query completion.");
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
                ShowLegacy(f.Ui, "Course");Click(f.Ui,"1일차  ");Click(f.Ui,"시작");Click(f.Ui,"물");
                Click(f.Ui,"다음");Click(f.Ui,"다음");Click(f.Ui,"다음");Click(f.Ui,"실습 마치기");
                Click(f.Ui,"기록 건너뛰고 완료");
                Assert(f.Service.Snapshot.Nutrient==5 && f.Service.Snapshot.MbctNextOrder==2,"Guided completion did not reward five/advance.");
                ShowLegacy(f.Ui, "Course");Assert(!Button(f.Ui,"2일차  ").interactable,"Daily quota bypass through course UI.");
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

        static void RecommendationPreferencesFlow()
        {
            foreach (string program in new[] { "2w", "4w", "8w" })
            using (var f = new Fixture(0, 5))
            {
                Assert(f.Service.SelectMbctProgram(program), f.Service.LastError);
                string mission = MbctPolicy.MissionId(program, 1);
                Assert(f.Service.BeginSession("preference-learned", mission, 1) &&
                    f.Service.StartSession("preference-learned") && f.Service.CompleteSession("preference-learned"),
                    "Could not seed a learned activity for recommendation preferences.");
                var before = f.Service.Snapshot;
                ShowLegacy(f.Ui, "Free");
                Click(f.Ui, "추가 활동 추천 설정");
                Assert(f.Ui.CurrentScreen == "recommendation-preferences", "Free activities did not open recommendation preferences.");
                var preferences = new[] { "몸 살펴보기", "호흡 관찰", "일상 행동", "잠시 멈추기" };
                var durations = new[] { "1", "3", "5" };
                Assert(Button(f.Ui, "3분 정도").GetComponentsInChildren<Text>(true).Any(label => label.text == "선택함"),
                    "The default three-minute option has no selected feedback.");
                foreach (string preferred in preferences)
                {
                    Click(f.Ui, preferred);
                    Assert(f.Service.Snapshot.PracticePreferences.Single(answer => answer.Key == "favorite").Value == preferred &&
                        preferences.Count(option => Button(f.Ui, option).GetComponentsInChildren<Text>(true)
                            .Any(label => label.text == "선택함")) == 1 &&
                        Button(f.Ui, preferred).GetComponentsInChildren<Text>(true).Any(label => label.text == "선택함"),
                        "Practice preference did not save or retained stale selection feedback.");
                }
                foreach (string minutes in durations)
                {
                    Click(f.Ui, minutes + "분 정도");
                    Assert(f.Service.Snapshot.PracticePreferences.Single(answer => answer.Key == "recommendation-minutes").Value == minutes &&
                        durations.Count(option => Button(f.Ui, option + "분 정도").GetComponentsInChildren<Text>(true)
                            .Any(label => label.text == "선택함")) == 1 &&
                        Button(f.Ui, minutes + "분 정도").GetComponentsInChildren<Text>(true).Any(label => label.text == "선택함"),
                        "Available duration did not save or retained stale selection feedback.");
                }
                Click(f.Ui, "배운 활동으로");
                Assert(f.Ui.CurrentScreen == "learned-recommendations", "Preferences did not return to the learned recommendation hub.");
                Click(f.Ui, "추가 활동 추천 설정");
                Assert(Button(f.Ui, "잠시 멈추기").GetComponentsInChildren<Text>(true).Any(label => label.text == "선택함") &&
                    Button(f.Ui, "5분 정도").GetComponentsInChildren<Text>(true).Any(label => label.text == "선택함"),
                    "Reopening recommendation preferences lost the selected values.");
                Assert(f.Ui.TryNavigateBack() && f.Ui.CurrentScreen == "learned-recommendations", "Back did not return from preferences to the learned recommendation hub.");
                ShowLegacy(f.Ui, "Course");
                var after = f.Service.Snapshot;
                Assert(MbctPolicy.ActiveProgram(after) == program && MbctPolicy.NextOrder(after) == 2,
                    "Editing optional recommendations changed the sequential course.");
                after.PracticePreferences = before.PracticePreferences;
                Assert(StateCodec.Encode(after) == StateCodec.Encode(before),
                    "Editing recommendation preferences changed course, nutrient, garden or participation records.");
                using (var reopened = new GardenStateService(f.Store, f.Clock, f.Balance))
                {
                    Assert(reopened.Open(), reopened.LastError);
                    var saved = reopened.Snapshot;
                    Assert(saved.PracticePreferences.Single(answer => answer.Key == "favorite").Value == "잠시 멈추기" &&
                        saved.PracticePreferences.Single(answer => answer.Key == "recommendation-minutes").Value == "5",
                        "Recommendation preferences were not durable across reopening the saved state.");
                }
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
                Assert(Button(f.Ui,"확정").transform.parent==Button(f.Ui,"취소").transform.parent
                    && Button(f.Ui,"확정").transform.GetSiblingIndex()<Button(f.Ui,"취소").transform.GetSiblingIndex(),
                    "Growth confirmation must be the first full-width action in the shared dock");
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

        static void ActivityPresentation()
        {
            using(var f=new Fixture(0,5))
            {
                var mission=MbctContent.Course[0];
                f.Ui.SelectMission(mission);Click(f.Ui,"시작");f.Ui.Finish(null,null);
                var first=f.Service.Snapshot.Sessions.Last();
                f.Clock.UtcNow=f.Clock.UtcNow.AddMinutes(5);
                f.Ui.SelectMission(mission);ShowLegacy(f.Ui, "Home");
                Assert(f.Ui.GetComponentsInChildren<Text>().Any(t=>t.text==mission.title) &&
                    f.Ui.GetComponentsInChildren<Text>().Any(t=>t.text=="선택한 활동"),"Selected review card shows the next recommended mission.");
                Click(f.Ui,"선택한 활동 안내");Click(f.Ui,"시작");ShowLegacy(f.Ui, "Home");
                Assert(f.Ui.GetComponentsInChildren<Text>().Any(t=>t.text=="진행 중인 활동"),"Active card lost its participation state.");
                var review=f.Service.Snapshot.Sessions.Last();
                Assert(f.Service.PauseSession(review.SessionId),f.Service.LastError);ShowLegacy(f.Ui, "Home");
                Assert(f.Ui.GetComponentsInChildren<Text>().Any(t=>t.text==mission.title) &&
                    f.Ui.GetComponentsInChildren<Text>().Any(t=>t.text=="잠시 쉬는 활동"),"Paused review card lost its mission or state.");
                Click(f.Ui,"진행하던 활동으로");Click(f.Ui,"이어서 하기");f.Ui.Finish(null,null);
                Assert(f.Service.Snapshot.Nutrient==5 && f.Service.Snapshot.Sessions.Count==2,"Presenting or completing a review changed the reward or cloned a session.");
                string before=StateCodec.Encode(f.Service.Snapshot);ShowLegacy(f.Ui, "Records");
                var labels=f.Ui.GetComponentsInChildren<Text>().Select(t=>t.text).ToArray();
                string firstDate=DateTimeOffset.Parse(first.EndedUtc).ToLocalTime().ToString("MM/dd HH:mm");
                string secondDate=f.Clock.UtcNow.ToLocalTime().ToString("MM/dd HH:mm");
                Assert(labels.Contains(firstDate+" · 완료") && labels.Contains(secondDate+" · 완료") &&
                    !labels.Any(t=>t.Contains("보상 처리됨")),"Repeated practice records lack dates or incorrectly claim rewards.");
                Assert(StateCodec.Encode(f.Service.Snapshot)==before,"Rendering the history changed saved records.");
            }
        }
        static void CourseInitialPage()
        {
            using(var f=new Fixture(0,5))
            {
                SeedFullMbct(f,6);ShowLegacy(f.Ui, "Course");
                Assert(f.Ui.GetComponentsInChildren<Text>().Any(t=>t.text=="2주 · "+MbctContent.WeekTitles[1]),"First course visit did not open the recommended week.");
                Click(f.Ui,"이전 주");ShowLegacy(f.Ui, "Home");ShowLegacy(f.Ui, "Course");
                Assert(f.Ui.GetComponentsInChildren<Text>().Any(t=>t.text=="1주 · "+MbctContent.WeekTitles[0]),"Returning to the course discarded manual browsing.");
                Click(f.Ui,"다음 주");
                Assert(f.Ui.GetComponentsInChildren<Text>().Any(t=>t.text=="2주 · "+MbctContent.WeekTitles[1]),"Course paging no longer advances after automatic first selection.");
            }
        }
        static void SeedFullMbct(Fixture f,int count=48)
        {
            for(int i=1;i<=count;i++)
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
                SeedFullMbct(f);long reads=f.Service.SnapshotReads;ShowLegacy(f.Ui, "Free");
                Assert(f.Service.SnapshotReads-reads==1,"Free rendering cloned state inside catalog predicate");
                reads=f.Service.SnapshotReads;ShowLegacy(f.Ui, "Course");
                Assert(f.Service.SnapshotReads-reads==1,"Course rendering cloned state for each row");
                Assert(f.Ui.GetComponentsInChildren<Text>().Any(t=>t.text=="8주 · "+MbctContent.WeekTitles[7]),"Completed course did not clamp its initial page to the final week.");
                var gardenBefore=GameObject.Find("Saved garden additions");f.Ui.UpdateGarden();f.Ui.UpdateGarden();
                Assert(gardenBefore==GameObject.Find("Saved garden additions"),"Unchanged state rebuilt garden geometry");
            }
        }
        static void AssertSeparateControls(WeekOneQuestDemo ui)
        {
            var page=ui.GetComponentsInChildren<ScrollRect>(true).First(s=>s.name=="Local garden flow").transform;
            var controls=page.GetComponentsInChildren<Selectable>(true).Where(c=>c.transform.parent==page && c.gameObject.activeSelf).ToArray();
            for(int i=0;i<controls.Length;i++)for(int j=i+1;j<controls.Length;j++)
            {
                var a=(RectTransform)controls[i].transform;var b=(RectTransform)controls[j].transform;
                float width=Mathf.Min(a.anchorMax.x,b.anchorMax.x)-Mathf.Max(a.anchorMin.x,b.anchorMin.x);
                float height=Mathf.Min(a.anchorMax.y,b.anchorMax.y)-Mathf.Max(a.anchorMin.y,b.anchorMin.y);
                Assert(width<=.0001f || height<=.0001f,"Overlapping controls: "+a.name+" / "+b.name+" on "+ui.CurrentScreen);
            }
        }
        static void FixedActivityActions()
        {
            using(var f=new Fixture(0,5))
            {
                f.Ui.SelectMission(MbctContent.Course[0]);
                var scroll=f.Ui.GetComponentsInChildren<ScrollRect>(true).Single(s=>s.name=="Local garden flow");
                var start=Button(f.Ui,"시작");
                Assert(!start.transform.IsChildOf(scroll.transform),"Start scrolls with the introduction.");
                Assert(!f.Ui.GetComponentsInChildren<Button>(true).Any(b=>b.name=="오늘은 여기까지"),"Unstarted activity exposes an end action.");
                var selectedId=f.Service.Snapshot.Sessions.Last().SessionId;
                f.Store.FailNextSaves=1;Click(f.Ui,"선택 취소");
                Assert(f.Ui.CurrentScreen=="session" && f.Service.Snapshot.Sessions.Any(s=>s.SessionId==selectedId),
                    "Failed cancellation discarded the selection or navigated away.");
                Click(f.Ui,"선택 취소");
                Assert(f.Ui.CurrentScreen=="home" && f.Service.Snapshot.Sessions.Count==0 && f.Service.Snapshot.Nutrient==0,
                    "Cancelling an unstarted activity left a participation record or reward.");
                f.Ui.SelectMission(MindfulnessContent.FindMission("free-mission:M15"));
                Assert(f.Service.Snapshot.Sessions.Single().MissionId=="free-mission:M15","Cancelled selection still blocked another activity.");
                Click(f.Ui,"선택 취소");
                Assert(f.Service.Snapshot.Sessions.Count==0,"Walking introduction did not cancel its selection.");
                f.Ui.SelectMission(MbctContent.Course[0]);
                Click(f.Ui,"시작");
                scroll=f.Ui.GetComponentsInChildren<ScrollRect>(true).Single(s=>s.name=="Local garden flow");
                var input=f.Ui.GetComponentInChildren<InputField>(true);
                input.text="합성 입력 초안";
                Assert(input.transform.IsChildOf(scroll.content),"Practice input is outside the body viewport.");
                Assert(!Button(f.Ui,"다음").transform.IsChildOf(scroll.transform) &&
                    !Button(f.Ui,"잠시 쉬기").transform.IsChildOf(scroll.transform),"Practice actions are inside the scroll mask.");
                Assert(!f.Ui.GetComponentsInChildren<Button>(true).Any(b=>b.name=="목록으로" || b.name=="여기까지"),"Live practice still exposes competing list/end actions.");
                f.Store.FailNextSaves=1;Click(f.Ui,"잠시 쉬기");
                Assert(f.Service.Snapshot.Sessions.Last().Status==ParticipationState.InProgress && input.text=="합성 입력 초안",
                    "A failed pause discarded input or changed state.");
                Click(f.Ui,"잠시 쉬기");
                Assert(f.Service.Snapshot.Sessions.Last().Status==ParticipationState.Paused,"Pause did not preserve the active session.");
                Click(f.Ui,"이어서 하기");
                Assert(f.Ui.GetComponentInChildren<InputField>(true).text=="합성 입력 초안","Resume lost the saved draft.");
                Click(f.Ui,"잠시 쉬기");
                f.Store.FailNextSaves=1;Click(f.Ui,"오늘은 여기까지");
                Assert(f.Ui.CurrentScreen=="session" && f.Service.Snapshot.Sessions.Last().Status==ParticipationState.Paused,
                    "Failed termination navigated away or changed participation.");
                Click(f.Ui,"오늘은 여기까지");
                Assert(f.Ui.CurrentScreen=="home" && f.Service.Snapshot.Sessions.Last().Status==ParticipationState.Ended && f.Service.Snapshot.Nutrient==0,
                    "Termination did not preserve its no-completion semantics.");
            }
        }
        static void SkipPracticeInput()
        {
            using(var f=new Fixture(0,5))
            {
                var mission=MbctContent.Course[0];string key=mission.steps[0].answerKey;
                Assert(key!=null && mission.steps[0].options.Length>0,"Optional-input fixture is missing its choice step.");
                Assert(f.Service.SavePreference(key,"이전 합성 선호"),f.Service.LastError);
                f.Ui.SelectMission(mission);Click(f.Ui,"시작");
                Click(f.Ui,mission.steps[0].options[0]);
                var input=f.Ui.GetComponentInChildren<InputField>(true);string draft=input.text;
                Assert(f.Service.Snapshot.Sessions.Last().Answers.Any(a=>a.Key==key),"Choice was not saved before skipping.");
                f.Store.FailNextSaves=1;Click(f.Ui,"입력 건너뛰기");
                Assert(f.Service.Snapshot.Sessions.Last().InstructionStep==0 && input.text==draft,
                    "Failed skip advanced the step or lost its draft.");
                Click(f.Ui,"입력 건너뛰기");
                Assert(f.Service.Snapshot.Sessions.Last().InstructionStep==1 && !f.Service.Snapshot.Sessions.Last().Answers.Any(a=>a.Key==key),
                    "Skipping retained the auto-saved answer.");
                f.Ui.Finish(null,null);
                Assert(f.Service.Snapshot.PracticePreferences.Single(a=>a.Key==key).Value=="이전 합성 선호",
                    "Skipped input overwrote a previous completed preference.");
            }
        }
        static void RefreshPracticeDraft()
        {
            using(var f=new Fixture(0,5))
            {
                var mission=MbctContent.Course[0];string key=mission.steps[0].answerKey;
                f.Ui.SelectMission(mission);Click(f.Ui,"시작");
                var field=f.Ui.GetComponentInChildren<InputField>(true);field.text="수정 중인 합성 초안";
                f.Store.FailNextSaves=1;f.Ui.ShowSession();
                Assert(f.Ui.GetComponentInChildren<InputField>(true)==field && field.text=="수정 중인 합성 초안",
                    "Failed background refresh replaced the unsaved input.");
                f.Ui.ShowSession();
                Assert(f.Ui.GetComponentInChildren<InputField>(true).text=="수정 중인 합성 초안" &&
                    f.Service.Snapshot.Sessions.Last().Answers.Single(a=>a.Key==key).Value=="수정 중인 합성 초안",
                    "Background refresh restored a stale draft.");
            }
        }
        static void MbctLayouts()
        {
            using(var f=new Fixture(0,5))
            {
                SeedFullMbct(f);ShowLegacy(f.Ui, "Home");AssertSeparateControls(f.Ui);ShowLegacy(f.Ui, "Course");AssertSeparateControls(f.Ui);ShowLegacy(f.Ui, "Free");AssertSeparateControls(f.Ui);
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

        // These regressions intentionally exercise saved MBCT/journal compatibility.
        // Public ShowHome/Course/Free/Records now open Focus; its complete flows are
        // independently covered by FocusMissionValidation and AllCourseValidation.
        internal static void ShowLegacy(WeekOneQuestDemo ui, string page)
        {
            var method = typeof(WeekOneQuestDemo).GetMethod("ShowLegacy" + page,
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            Assert(method != null, "Missing legacy compatibility entry: " + page);
            try { method.Invoke(ui, null); }
            catch (System.Reflection.TargetInvocationException error) when (error.InnerException != null)
            { System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(error.InnerException).Throw(); throw; }
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
            public Fixture(int nutrient = 0, int completionReward = 10, bool chooseDefaultProgram = true)
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
                    if (chooseDefaultProgram)
                    {
                        Assert(Service.SelectMbctProgram("8w"), Service.LastError);
                        ShowLegacy(Ui, "Home");
                    }
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
