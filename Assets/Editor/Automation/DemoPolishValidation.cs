using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CapstoneDesign.Plants;
using CapstoneDesign.Prototype;
using CapstoneDesign.Runtime;
using CapstoneDesign.Runtime.LocalState;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

namespace CapstoneDesign.EditorTools
{
    // Explicit, synthetic Editor evidence. These callbacks and raycasts do not
    // establish device touch, accessibility-service, or Android lifecycle coverage.
    public static class DemoPolishValidation
    {
        sealed class Clock : IClock
        {
            public DateTimeOffset UtcNow { get; set; } = new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.FromHours(9));
            public TimeZoneInfo TimeZone => TimeZoneInfo.CreateCustomTimeZone("Synthetic-KST", TimeSpan.FromHours(9), "KST", "KST");
        }
        sealed class Store : IStateStore
        {
            GardenState state;
            public bool FailNextSave;
            public string Saved => state == null ? null : StateCodec.Encode(state);
            public GardenState Load() => state == null ? null : StateCodec.Clone(state);
            public void Save(GardenState candidate)
            {
                if (FailNextSave) { FailNextSave = false; throw new IOException("Synthetic storage failure"); }
                state = StateCodec.Clone(candidate);
            }
        }
        sealed class EditModeEvents : IDisposable
        {
            const System.Reflection.BindingFlags Hidden = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
            readonly EventSystem events;
            readonly bool created, manuallyEnabled, originalActive, originalEnabled;
            readonly List<GraphicRaycaster> manuallyRegistered = new List<GraphicRaycaster>();
            public EditModeEvents()
            {
                events = EventSystem.current ?? UnityEngine.Object.FindFirstObjectByType<EventSystem>(FindObjectsInactive.Include);
                if (events == null)
                {
                    events = new GameObject("Synthetic demo events", typeof(EventSystem)).GetComponent<EventSystem>();
                    created = true;
                }
                originalActive = events.gameObject.activeSelf;
                originalEnabled = events.enabled;
                events.gameObject.SetActive(true);
                events.enabled = true;
                // EventSystem is not ExecuteAlways. Edit mode may load the real
                // scene object without dispatching its registration lifecycle.
                if (EventSystem.current == null)
                {
                    Invoke(typeof(EventSystem), events, "OnEnable");
                    manuallyEnabled = true;
                }
                Require(EventSystem.current != null && EventSystem.current.isActiveAndEnabled,
                    "Editor event system failed to register");
            }
            public void RegisterActiveRaycasters()
            {
                foreach (var raycaster in UnityEngine.Object.FindObjectsByType<GraphicRaycaster>(FindObjectsSortMode.None))
                {
                    if (!raycaster.isActiveAndEnabled || RaycasterManager.GetRaycasters().Contains(raycaster)) continue;
                    Invoke(typeof(BaseRaycaster), raycaster, "OnEnable");
                    manuallyRegistered.Add(raycaster);
                    Require(RaycasterManager.GetRaycasters().Contains(raycaster), "Graphic raycaster failed to register");
                }
            }
            public void Dispose()
            {
                foreach (var raycaster in manuallyRegistered)
                    if (raycaster != null) Invoke(typeof(BaseRaycaster), raycaster, "OnDisable");
                if (events == null) return;
                if (manuallyEnabled) Invoke(typeof(EventSystem), events, "OnDisable");
                if (created) UnityEngine.Object.DestroyImmediate(events.gameObject);
                else { events.enabled = originalEnabled; events.gameObject.SetActive(originalActive); }
            }
            static void Invoke(Type type, object target, string method)
            {
                var lifecycle = type.GetMethod(method, Hidden);
                Require(lifecycle != null, "UGUI lifecycle method missing: " + type.Name + "." + method);
                lifecycle.Invoke(target, null);
            }
        }
        sealed class Fixture : IDisposable
        {
            public readonly Store Store = new Store();
            public readonly Clock Clock = new Clock();
            public readonly GardenStateService Service;
            public readonly GardenHomePresenter Home;
            public readonly SensorRawDisplay Settings;
            public readonly GardenSkyEnvironment Sky;
            public readonly EditModeEvents Events;
            public WeekOneQuestDemo Ui => Home.activity;
            public Fixture(float growth = .1f, int nutrient = 100)
            {
                EditorSceneManager.OpenScene("Assets/Scenes/MockupMain.unity");
                AndroidPlatformBridge.UseMockProvider();
                Home = UnityEngine.Object.FindFirstObjectByType<GardenHomePresenter>(FindObjectsInactive.Include);
                Require(Home != null, "Integrated home is missing");
                var balance = new DemoBalanceConfig { VisitorChancePercent = 0 };
                Service = new GardenStateService(Store, Clock, balance,
                    new LegacySnapshot { Nutrient = nutrient, Growth = growth });
                Ui.Balance = balance;
                Ui.Initialize(Service, true);
                Require(Ui.HasOpenState, "Synthetic state did not open");
                Require(Service.SelectMbctProgram("8w"), "Synthetic 8-week course did not open");
                Settings = Home.GetComponentInChildren<SensorRawDisplay>(true);
                Require(Settings != null, "Settings are missing");
                Settings.PrepareView();
                Home.navigation.ShowIsland();
                Home.RefreshFromState();
                Sky = GardenSkyEnvironment.Ensure(Home.gardenCamera, Home.gardenVisuals.transform);
                Events = new EditModeEvents();
            }
            public void Dispose()
            {
                try { Events.Dispose(); }
                finally { Service.Dispose(); }
            }
        }
        readonly struct CameraFrame
        {
            readonly Vector3 position;
            readonly Quaternion rotation;
            readonly float size, aspect;
            public CameraFrame(Camera camera)
            { position = camera.transform.position; rotation = camera.transform.rotation; size = camera.orthographicSize; aspect = camera.aspect; }
            public bool Matches(Camera camera) => Vector3.Distance(position, camera.transform.position) < .00001f
                && Quaternion.Angle(rotation, camera.transform.rotation) < .001f
                && Mathf.Abs(size - camera.orthographicSize) < .00001f && Mathf.Abs(aspect - camera.aspect) < .00001f;
        }

        public static void Capture()
        {
            string output = Environment.GetEnvironmentVariable("CAPSTONE_ARTIFACTS");
            Require(!string.IsNullOrWhiteSpace(output), "Explicit synthetic artifact directory required");
            Require(SystemInfo.graphicsDeviceType != GraphicsDeviceType.Null, "Rendered UI evidence requires a graphics device");
            Directory.CreateDirectory(output);
            var report = new List<string> { "Synthetic in-memory state; actual UI callbacks; Editor captures; deviceTested=false." };
            double? originalTime = GardenVisualClock.OverrideHour;
            try
            {
                foreach (var size in new[] { new Vector2Int(900, 1600), new Vector2Int(900, 1950),
                    new Vector2Int(1200, 2000), new Vector2Int(1200, 800) })
                {
                    using var f = new Fixture();
                    HomeControls(f, size, output, report);
                    TimePresets(f, size, output, report);
                    GrowthComparison(f, size, output, report);
                }
                GrowthUnavailable(output, report);
                report.Add("DEMO_POLISH_VALIDATION_PASS");
                File.WriteAllLines(Path.Combine(output, "demo-polish-validation.txt"), report);
                Debug.Log("DEMO_POLISH_VALIDATION_PASS " + output);
            }
            catch (Exception ex)
            {
                report.Add("FAIL " + ex.Message);
                File.WriteAllLines(Path.Combine(output, "demo-polish-validation.txt"), report);
                throw;
            }
            finally
            {
                if (originalTime.HasValue) GardenVisualClock.SetOverrideMinutes((int)Math.Round(originalTime.Value * 60));
                else GardenVisualClock.UseSystemTime();
            }
        }

        // Separate entry point so activity evidence does not require repeating
        // the more expensive garden/lighting captures.
        public static void CaptureActivities()
        {
            string output = Environment.GetEnvironmentVariable("CAPSTONE_ARTIFACTS");
            Require(!string.IsNullOrWhiteSpace(output), "Explicit synthetic artifact directory required");
            Require(SystemInfo.graphicsDeviceType != GraphicsDeviceType.Null, "Rendered UI evidence requires a graphics device");
            Directory.CreateDirectory(output);
            var report = new List<string> { "Synthetic participation records and fixed clock; deviceTested=false." };
            try
            {
                foreach (var size in new[] { new Vector2Int(900, 1600), new Vector2Int(1200, 2000) })
                {
                    using var f = new Fixture(nutrient: 0);
                    f.Home.navigation.ShowActivities();
                    var mission = MbctContent.Course[0];
                    f.Ui.SelectMission(mission); Click(f.Ui.transform, "시작");
                    f.Clock.UtcNow = f.Clock.UtcNow.AddMinutes(3);
                    FinishSyntheticPractice(f);
                    var original = f.Service.Snapshot.Sessions.Single();
                    NextSyntheticDay(f);
                    f.Ui.SelectMission(mission); f.Ui.ShowHome();
                    Require(HasLabel(f.Ui, mission.title) && HasLabel(f.Ui, "선택한 활동"),
                        "Selected review card lost its mission or state");
                    UiImage(size, output, "activity-selected-review");
                    Click(f.Ui.transform, "선택한 활동 안내"); Click(f.Ui.transform, "시작");
                    var review = f.Service.Snapshot.Sessions.Last();
                    Require(f.Service.PauseSession(review.SessionId), "Synthetic review pause failed");
                    f.Ui.ShowHome();
                    Require(HasLabel(f.Ui, mission.title) && HasLabel(f.Ui, "잠시 쉬는 활동"),
                        "Paused review card lost its mission or state");
                    UiImage(size, output, "activity-paused-review");
                    Click(f.Ui.transform, "진행하던 활동으로"); Click(f.Ui.transform, "이어서 하기");
                    f.Clock.UtcNow = f.Clock.UtcNow.AddMinutes(3);
                    FinishSyntheticPractice(f);
                    var completed = f.Service.Snapshot;
                    Require(completed.Coins == 0 && completed.PracticeGrowthRecords.Count == 2 && completed.Sessions.Count == 2 && completed.MbctNextOrder == 2,
                        "Review completion unexpectedly granted another reward or advanced the course");
                    string saved = f.Store.Saved;
                    f.Ui.ShowRecords();
                    string firstDate = DateTimeOffset.Parse(original.EndedUtc).ToLocalTime().ToString("MM/dd HH:mm");
                    string reviewDate = DateTimeOffset.Parse(completed.Sessions.Last().EndedUtc).ToLocalTime().ToString("MM/dd HH:mm");
                    Require(firstDate != reviewDate && HasLabel(f.Ui, firstDate + " · 완료")
                        && HasLabel(f.Ui, reviewDate + " · 완료")
                        && !f.Ui.GetComponentsInChildren<Text>().Any(t => t.text.Contains("보상 처리됨")),
                        "Completion/review history must display distinct dates and accurate completion text");
                    UiImage(size, output, "activity-records-completed-review");
                    Require(f.Store.Saved == saved, "Capturing participation history changed its saved records");

                    for (int order = 2; order <= 6; order++)
                    {
                        NextSyntheticDay(f);
                        for (int wait = 0; wait < 7 && MbctPolicy.Availability(f.Service.Snapshot) != null; wait++)
                            NextSyntheticDay(f);
                        Require(MbctPolicy.Availability(f.Service.Snapshot) == null, "Synthetic MBCT week did not become available");
                        string id = "synthetic-demo-course-" + order;
                        Require(f.Service.BeginSession(id, MbctPolicy.MissionId(order), order)
                            && f.Service.StartSession(id) && CompleteSyntheticPractice(f.Service,id), "Synthetic course progress failed");
                    }
                    NextSyntheticDay(f);
                    // No earlier ShowCourse call: this exercises first entry.
                    f.Ui.ShowCourse();
                    Require(HasLabel(f.Ui, "2주 · " + MbctContent.WeekTitles[1]), "First course entry missed the recommended week");
                    UiImage(size, output, "activity-course-recommended-week");
                    report.Add(size + " selected/paused review, two dated completion records, first recommended week PASS");
                }
                report.Add("DEMO_POLISH_ACTIVITIES_PASS 8 synthetic images");
                Debug.Log("DEMO_POLISH_ACTIVITIES_PASS " + output);
            }
            catch (Exception ex) { report.Add("FAIL " + ex.Message); throw; }
            finally { File.WriteAllLines(Path.Combine(output, "demo-polish-activities.txt"), report); }
        }
        static void NextSyntheticDay(Fixture f)
        {
            f.Clock.UtcNow = f.Clock.UtcNow.AddDays(1);
            Require(f.Ui.RefreshCycle(), "Synthetic daily cycle failed");
        }
        static bool HasLabel(WeekOneQuestDemo ui, string value) => ui.GetComponentsInChildren<Text>().Any(t => t.text == value);

        static void HomeControls(Fixture f, Vector2Int size, string output, List<string> report)
        {
            GardenVisualClock.SetOverrideMinutes(12 * 60);
            string saved = f.Store.Saved, snapshot = StateCodec.Encode(f.Service.Snapshot);
            string recipe = PlantCodec.Encode(f.Home.RenderedRecipe);
            var row = f.Home.homeCanvas.GetComponentsInChildren<Transform>(true).Single(t => t.name == "Garden view controls");
            Require(!f.Home.ViewControlsExpanded && !row.gameObject.activeSelf, "Home must start with folded controls");
            var toggle = Button(f.Home.homeCanvas.transform, "보기 +");
            Require(toggle.GetComponentInChildren<Text>().fontSize >= 30, "View toggle font was reduced");
            using (var capture = new GardenLightingValidation.HomeCapture(f.Home, f.Sky, size))
            {
                HomeImage(f, capture, output, "home-folded", size, report);
                f.Events.RegisterActiveRaycasters();
                AssertRaycastTarget(toggle, size);
                toggle.onClick.Invoke();
                Require(f.Home.ViewControlsExpanded && row.gameObject.activeInHierarchy, "View toggle did not expand controls");
                Require(row.GetComponentsInChildren<Button>().All(b => b.GetComponentInChildren<Text>().fontSize >= 30),
                    "Expanded view controls must retain readable text");
                HomeImage(f, capture, output, "home-expanded", size, report);
                foreach (string name in new[] { "가까이 보기", "축소" })
                {
                    var button = Button(row, name);
                    AssertRaycastTarget(button, size);
                    button.onClick.Invoke();
                }
                Require(!f.Home.IsOverview && f.Home.Zoom < .85f, "View buttons did not change the camera");
                var orbit = f.Home.homeCanvas.GetComponentInChildren<PrototypeIslandDrag>(true);
                Drag(orbit);
                Require(Vector2.Distance(orbit.Angles, orbit.defaultAngles) > 1, "Synthetic drag did not change orbit");
                f.Home.ResetView();
                AssertReset(f.Home);
                HomeImage(f, capture, output, "home-reset", size, report);
            }
            Require(f.Store.Saved == saved && StateCodec.Encode(f.Service.Snapshot) == snapshot
                && PlantCodec.Encode(f.Home.RenderedRecipe) == recipe, "Camera controls changed the saved garden or recipe");
            report.Add(size + " home fold/expand, font size, button raycast, drag/reset, unchanged save/recipe PASS");
        }

        static void TimePresets(Fixture f, Vector2Int size, string output, List<string> report)
        {
            string saved = f.Store.Saved, snapshot = StateCodec.Encode(f.Service.Snapshot);
            foreach (var preset in new[] { ("낮 12시 보기", 720, "day-12"), ("밤 0시 보기", 0, "night-00") })
            {
                // Deliberately depart from the presentation view before each preset.
                f.Home.SetOverview(false); f.Home.SetZoom(.25f); f.Home.SetViewControlsExpanded(true);
                f.Home.navigation.ShowSettings();
                Require(!f.Settings.IsVisualDebugOpen && f.Settings.PrimaryScroll.gameObject.activeSelf,
                    "Opening settings must show the ordinary settings list");
                UiImage(size, output, "settings-list-" + preset.Item3);
                Click(f.Settings.transform, "정원의 시간");
                Require(f.Settings.IsVisualDebugOpen && !f.Settings.PrimaryScroll.gameObject.activeSelf, "Time settings did not open");
                UiImage(size, output, "garden-time-" + preset.Item3);
                Click(f.Settings.transform, preset.Item1);
                Require(f.Home.IsGardenVisible && !f.Settings.IsVisualDebugOpen && f.Settings.PrimaryScroll.gameObject.activeSelf,
                    "Time preset did not return to the actual garden and close the time subpage");
                Require(GardenVisualClock.CurrentMinute == preset.Item2 && GardenVisualClock.OverrideHour.HasValue,
                    "Time preset applied the wrong hour");
                AssertReset(f.Home);
                using var capture = new GardenLightingValidation.HomeCapture(f.Home, f.Sky, size);
                HomeImage(f, capture, output, "home-" + preset.Item3, size, report);
            }
            f.Home.navigation.ShowSettings();
            Require(!f.Settings.IsVisualDebugOpen && f.Settings.PrimaryScroll.gameObject.activeSelf,
                "Returning from a preset must not reopen the time subpage");
            Click(f.Settings.transform, "정원의 시간");
            Click(f.Settings.transform, "실제 시간으로");
            Require(!GardenVisualClock.OverrideHour.HasValue, "System-time button did not clear the preview override");
            Require(f.Store.Saved == saved && StateCodec.Encode(f.Service.Snapshot) == snapshot,
                "Time preview changed activity dates, balance, or saved garden");
            report.Add(size + " day/night preset callbacks, garden/reset/list return, system clock, unchanged state PASS");
        }

        static void GrowthComparison(Fixture f, Vector2Int size, string output, List<string> report)
        {
            GardenVisualClock.SetOverrideMinutes(12 * 60);
            f.Home.navigation.ShowActivities();
            string saved = f.Store.Saved, snapshot = StateCodec.Encode(f.Service.Snapshot);
            string recipe = PlantCodec.Encode(f.Service.Snapshot.PlantAppearance.Recipe);
            string seed = f.Service.Snapshot.PlantAppearance.Recipe.BaseSeed;
            int coins = f.Service.CoinBalance;
            f.Ui.PreviewGrowth();
            Require(f.Ui.CurrentScreen == "growth" && PlantCodec.Encode(f.Home.RenderedRecipe) == recipe,
                "Growth guidance must show this plant at its saved stage");
            Require(HasLabel(f.Ui,"미션을 완료하면 식물이 자라요.")
                && !f.Ui.GetComponentsInChildren<Button>(false).Any(button=>button.name=="확정" || button.name=="성장 후"),
                "Growth guidance still exposes a paid or manual growth action");
            UiImage(size, output, "growth-mission-guidance");
            Require(f.Store.Saved == saved && StateCodec.Encode(f.Service.Snapshot) == snapshot,
                "Viewing mission growth guidance mutated coins or recipe");
            Click(f.Ui.transform, "정원으로");
            Require(f.Home.IsGardenVisible && f.Store.Saved == saved && PlantCodec.Encode(f.Home.RenderedRecipe) == recipe,
                "Returning from guidance changed the garden");

            const string id="synthetic-polish-practice-growth";
            Require(f.Service.BeginSession(id,"free-mission:M01") && f.Service.StartSession(id),f.Service.LastError);
            saved=f.Store.Saved;snapshot=StateCodec.Encode(f.Service.Snapshot);
            f.Store.FailNextSave = true;
            Require(!f.Service.CommitPracticeGrowth(id) && f.Store.Saved==saved && StateCodec.Encode(f.Service.Snapshot)==snapshot,
                "Failed practice growth changed coins, recipe or its receipt");
            Require(f.Service.CommitPracticeGrowth(id),f.Service.LastError);
            var grown=f.Service.Snapshot;
            Require(grown.Coins==coins && grown.PracticeGrowthRecords.Count==1 && grown.PurchaseReceipts.Count==0
                && grown.PlantAppearance.Recipe.BaseSeed==seed && PlantCodec.Encode(grown.PlantAppearance.Recipe)!=recipe,
                "Practice growth must advance this plant once without spending or granting coins");
            string committed=StateCodec.Encode(grown);saved=f.Store.Saved;
            Require(f.Service.CommitPracticeGrowth(id) && StateCodec.Encode(f.Service.Snapshot)==committed && f.Store.Saved==saved,
                "Duplicate practice growth changed saved state");
            f.Ui.UpdateGarden();
            Require(f.Home.guide.text.Contains("코인 "+coins+"개"),"Garden did not display unchanged coins");
            using var capture = new GardenLightingValidation.HomeCapture(f.Home, f.Sky, size);
            HomeImage(f, capture, output, "growth-practice-saved", size, report);
            report.Add(size + " mission growth guidance, unchanged balance, failed-save rollback and idempotent practice growth PASS");
        }
        static void GrowthUnavailable(string output,List<string> report)
        {
            foreach (var size in new[] { new Vector2Int(900, 1600), new Vector2Int(1200, 800) })
            {
                using (var f = new Fixture(GardenPlantVisuals.MaxVisualGrowth))
                {
                    f.Home.navigation.ShowActivities();
                    string saved = f.Store.Saved;
                    f.Ui.PreviewGrowth();
                    Require(f.Service.Snapshot.PlantAppearance.IsCompleted
                        && Button(f.Ui.transform, "사진첩으로 이동").interactable
                        && !f.Ui.GetComponentsInChildren<Button>(false).Any(button => button.name == "확정"),
                        "Completed garden must replace unavailable growth with its album action");
                    UiImage(size, output, "growth-limit");
                    Click(f.Ui.transform, "사진첩으로 이동");
                    Require(f.Ui.CurrentScreen == "plant-album" && Button(f.Ui.transform, "PlantAlbum.SaveCurrent").interactable,
                        "Completed garden cannot enter its album save flow");
                    Require(f.Store.Saved == saved, "Viewing a mature garden changed its save");
                }
                using (var f = new Fixture(.1f, 0))
                {
                    f.Home.navigation.ShowActivities();
                    string saved = f.Store.Saved;
                    f.Ui.PreviewGrowth();
                    Require(Button(f.Ui.transform,"미션으로 이동").interactable
                        && !f.Ui.GetComponentsInChildren<Button>(false).Any(button=>button.name=="확정"),
                        "A zero coin balance must still offer mission growth without payment");
                    Require(f.Store.Saved == saved, "Mission guidance with zero coins mutated state");
                    UiImage(size, output, "growth-zero-coins");
                }
                report.Add(size + " completed album routing and zero-coin mission guidance without charge PASS");
            }
        }

        static bool CompleteSyntheticPractice(GardenStateService service,string id)
        {
            var session=service.Snapshot.Sessions.First(item=>item.SessionId==id);
            if(MbctPolicy.IsMbct(session.MissionId))
                if(!service.SavePracticeStep(id,MbctContent.Find(session.MissionId).steps.Length))return false;
            return service.CompleteSession(id);
        }
        static void FinishSyntheticPractice(Fixture fixture)
        {
            var session=fixture.Service.Snapshot.Sessions.Last();
            var mission=MbctContent.Find(session.MissionId);
            for(int step=session.InstructionStep;step<mission.steps.Length;step++)
            {
                fixture.Ui.AdvancePractice(false);
                Require(fixture.Service.Snapshot.Sessions.Last().InstructionStep>step,fixture.Service.LastError??"Synthetic practice failed to advance");
            }
            fixture.Ui.Finish(null,null);
        }

        static void HomeImage(Fixture f, GardenLightingValidation.HomeCapture capture, string output,
            string name, Vector2Int size, List<string> report)
        {
            // Edit mode does not dispatch OnEnable's clock-change subscription.
            f.Sky.SetTimeForPreview(null);
            // RefreshFromState enables dither. Disable only for stable synthetic pixels.
            f.Home.gardenCamera.GetUniversalAdditionalCameraData().dithering = false;
            capture.Render(Path.Combine(output, name + "-" + size.x + "x" + size.y + ".png"), report);
        }
        static void UiImage(Vector2Int size, string output, string name)
        {
            // Match HomeImage: edit mode may not subscribe to clock changes.
            foreach (var sky in UnityEngine.Object.FindObjectsByType<GardenSkyEnvironment>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                sky.SetTimeForPreview(null);
            string path = Path.Combine(output, name + "-" + size.x + "x" + size.y + ".png");
            // The first pass resolves dynamic flow widths; the next captures the layout.
            LocalLoopPreview.Render(size, path);
            // Resizing the Editor render target changes ScrollRect bounds after
            // construction. These are top-of-page reference images; runtime
            // first-entry position is checked separately in Play smoke.
            foreach(var scroll in UnityEngine.Object.FindObjectsByType<ScrollRect>(FindObjectsSortMode.None))
                if(scroll.isActiveAndEnabled) { scroll.StopMovement(); scroll.verticalNormalizedPosition=1; }
            LocalLoopPreview.Render(size, path);
        }
        static Button Button(Transform root, string name) => root.GetComponentsInChildren<Button>()
            .Single(b => b.gameObject.activeInHierarchy && (b.name == name
                || b.GetComponentsInChildren<Text>().Any(t => t.text == name)));
        static void Click(Transform root, string name)
        {
            var button = Button(root, name);
            Require(button.IsInteractable(), "UI action disabled: " + name);
            button.onClick.Invoke();
        }
        static void AssertReset(GardenHomePresenter home)
        {
            var orbit = home.homeCanvas.GetComponentInChildren<PrototypeIslandDrag>(true);
            Require(home.IsOverview && Mathf.Abs(home.Zoom - .85f) < .00001f && !home.ViewControlsExpanded,
                "Reset must restore overview, .85 zoom, and folded controls");
            Require(orbit != null && Vector2.Distance(orbit.Angles, orbit.defaultAngles) < .00001f
                && !orbit.IsDragging, "Reset must restore the default orbit");
        }
        static void Drag(PrototypeIslandDrag orbit)
        {
            Require(orbit != null && EventSystem.current != null, "Synthetic drag requires the real home drag target");
            var raycaster = orbit.GetComponentInParent<GraphicRaycaster>();
            var rect = (RectTransform)orbit.transform;
            Vector2 center = RectTransformUtility.WorldToScreenPoint(raycaster.eventCamera, rect.TransformPoint(rect.rect.center));
            var pointer = new PointerEventData(EventSystem.current) { pointerId = 1, button = PointerEventData.InputButton.Left,
                pressPosition = center, position = center,
                pointerPressRaycast = new RaycastResult { module = raycaster, gameObject = orbit.gameObject } };
            orbit.OnBeginDrag(pointer);
            pointer.position += new Vector2(120, 40);
            orbit.OnDrag(pointer); orbit.OnEndDrag(pointer);
        }
        static void AssertRaycastTarget(Button target, Vector2Int size)
        {
            Canvas.ForceUpdateCanvases();
            var rect = (RectTransform)target.transform;
            var canvas = target.GetComponentInParent<Canvas>().rootCanvas;
            var camera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
            Vector2 point = RectTransformUtility.WorldToScreenPoint(camera, rect.TransformPoint(rect.rect.center));
            Require(new Rect(0, 0, size.x, size.y).Contains(point), "Home control is outside the capture: " + target.name);
            var hits = new List<RaycastResult>();
            Require(EventSystem.current != null, "UI raycast requires EventSystem");
            Require(target.GetComponentsInParent<GraphicRaycaster>().Any(r => r.isActiveAndEnabled
                && RaycasterManager.GetRaycasters().Contains(r)), "Target has no registered graphic raycaster");
            EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current) { position = point }, hits);
            Require(hits.Count > 0 && hits[0].gameObject.GetComponentInParent<Button>() == target,
                "Home control tap blocked: " + target.name + "; hits=" + string.Join(",", hits.Take(4).Select(h => h.gameObject.name)));
        }
        static void Require(bool condition, string message)
        { if (!condition) throw new Exception(message); }
    }
}
