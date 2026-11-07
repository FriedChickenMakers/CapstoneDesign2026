using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using CapstoneDesign.Plants;
using CapstoneDesign.Prototype;
using CapstoneDesign.Runtime;
using CapstoneDesign.Runtime.LocalState;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

namespace CapstoneDesign.EditorTools
{
    // Run only in a separately copied, synthetic batch project. This fixture never
    // creates a file-backed state store, reads preferences, or sends physical input.
    public static class CombinedPlantMemoryValidation
    {
        [Serializable] sealed class Check { public string name, status, detail; }
        [Serializable] sealed class Report
        {
            public string utc, unityVersion, scope;
            public int passed, failed, assertions, clicks, raycasts;
            public List<Check> checks = new List<Check>();
            public List<string> captures = new List<string>(), errors = new List<string>();
        }
        static Report report;
        static string output;
        const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

        public static void RunBatch()
        {
            var args = Environment.GetCommandLineArgs(); int at = Array.IndexOf(args, "-combinedMemoryReport");
            if (!Application.isBatchMode || Application.companyName != "CapstoneSyntheticValidation"
                || !Application.productName.StartsWith("CombinedMemory-", StringComparison.Ordinal) || at < 0 || at + 1 >= args.Length)
                throw new InvalidOperationException("Use a copied CombinedMemory-* synthetic batch project and -combinedMemoryReport path.");
            string path = Path.GetFullPath(args[at + 1]); output = Path.GetDirectoryName(path); Directory.CreateDirectory(output);
            report = new Report { utc = DateTimeOffset.UtcNow.ToString("O"), unityVersion = Application.unityVersion,
                scope = "Copied MockupMain; synthetic in-memory saves; real RT rendering and EventSystem handlers; no original Editor, user save, device or OS input" };
            Application.logMessageReceived += Observe; GardenVisualClock.SetOverrideMinutes(720);
            try
            {
                Run("Scaled text layout, atlas callbacks and input metrics", ScreenDensityTextValidation.Run);
                Run("Garden/album leaf input and atomic record lifecycle 360x640", () => MobileFlow(360, 640));
                Run("Garden/album leaf input and atomic record lifecycle 390x844", () => MobileFlow(390, 844));
                Run("21-stage v6 species bounds, 60-leaf paging, deterministic nature and low angles", StressRender);
                Run("All shared studio resources released", () => {
                    Assert(!GardenMemoPopup.IsOpen, "Popup owner survived fixture disposal.");
                    Assert(GardenPlantPhotoView.ActiveViewCount == 0 && GardenPlantPhotoView.SharedCameraCount == 0
                        && GardenPlantPhotoView.OwnedTextureCount == 0 && GardenPlantPhotoView.OwnedEnvironmentMeshCount == 0,
                        "Photo textures, camera or nature meshes survived their last view.");
                });
            }
            finally
            {
                GardenMemoPopup.TryCloseOpen(); GardenVisualClock.UseSystemTime();
                Application.logMessageReceived -= Observe;
                File.WriteAllText(path, JsonUtility.ToJson(report, true));
            }
            EditorApplication.Exit(report.failed == 0 && report.errors.Count == 0 ? 0 : 1);
        }
        static void Observe(string text, string stack, LogType type)
        { if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) report.errors.Add(type + ": " + text + "\n" + stack); }
        static void Assert(bool value, string message) { report.assertions++; if (!value) throw new InvalidOperationException(message); }
        static void Run(string name, Action action)
        {
            try { action(); report.passed++; report.checks.Add(new Check { name = name, status = "pass" }); }
            catch (Exception error) { report.failed++; report.checks.Add(new Check { name = name, status = "fail", detail = error.ToString() }); GardenMemoPopup.TryCloseOpen(); }
        }
        static T Find<T>(string name) where T : Component => Resources.FindObjectsOfTypeAll<T>()
            .FirstOrDefault(item => item != null && item.name == name && item.gameObject.activeInHierarchy);
        static string Screen(WeekOneQuestDemo ui) => (string)typeof(WeekOneQuestDemo).GetField("screen", Private).GetValue(ui);
        static void Complete(GardenStateService service, string id, string mission)
        {
            Assert(service.RefreshCycle(null) && service.BeginSession(id, mission, mission.StartsWith("course:") ? 1 : 0), service.LastError);
            Assert(service.StartSession(id) && service.CompleteSession(id, null, null), service.LastError);
        }
        sealed class Clock : IClock
        {
            public DateTimeOffset Now = DateTimeOffset.Parse("2026-11-09T10:00:00Z");
            public DateTimeOffset UtcNow => Now;
            public TimeZoneInfo TimeZone => TimeZoneInfo.Utc;
        }
        sealed class Store : IStateStore
        {
            string value; public bool Fail; public int Writes;
            public GardenState Load() => value == null ? null : StateCodec.Decode(value);
            public void Save(GardenState state)
            { if (Fail) throw new IOException("Synthetic write failure"); value = StateCodec.Encode(state); Writes++; }
        }

        static void MobileFlow(int width, int height)
        {
            using (var f = new Fixture(width, height))
            {
                var state = f.Service.Snapshot; var original = WeeklyPlants.Active(state); string plantId = original.Id;
                var memory = original.Memories.Single(); string organId = memory.OrganId, memoId = memory.RecordId;
                string recipe = PlantCodec.Encode(original.Recipe), photoId = original.PhotoId;
                Assert(original.Completed && state.PlantPhotos.Count == 1 && !state.PlantAppearance.IsArchived,
                    "Completed plant must be auto-collected while remaining in its garden.");
                Assert(f.Home.ShownPlantId == plantId && f.Home.ProceduralPlant.Geometry.Organs.Any(o => o.OrganId == organId && o.Selectable),
                    "Garden did not bind the active individual's linked leaf.");
                int writes = f.Store.Writes; string before = StateCodec.Encode(state);
                f.Capture("garden-memory");
                var homeImage = f.Preview.GetComponent<RawImage>();
                Vector2 homePoint = f.LeafPoint(f.Home.ProceduralPlant, f.Home.gardenCamera, homeImage,
                    point => f.Preview.TryPickMemo(point, f.Camera, out var id) && id == organId);
                f.Tap(homeImage, homePoint);
                Assert(GardenMemoPopup.IsOpen && Find<Text>("GardenMemoPopup.Note").text == "합성 기억", "Garden tap opened the wrong record.");
                f.Capture("garden-memory-popup"); f.Click("GardenMemoPopup.Close");
                f.Drag(homeImage, homePoint); Assert(!GardenMemoPopup.IsOpen, "Orbit drag was interpreted as a memory tap.");
                f.Home.ResetView(); f.Render();
                Assert(StateCodec.Encode(f.Service.Snapshot) == before && f.Store.Writes == writes, "Reading/orbiting changed saved state.");

                // Exercise the actual popup -> editor -> failed write -> retry buttons.
                homePoint = f.LeafPoint(f.Home.ProceduralPlant, f.Home.gardenCamera, homeImage,
                    point => f.Preview.TryPickMemo(point, f.Camera, out var id) && id == organId);
                f.Tap(homeImage, homePoint); f.Click("GardenMemoPopup.Edit");
                Assert(Screen(f.Ui) == "plant-memory-edit:" + organId, "Edit action did not open this leaf's editor.");
                Find<InputField>("PlantMemory.EditField.0").text = "고마움";
                Find<InputField>("PlantMemory.EditField.1").text = "수정한 합성 기억";
                var staleSave = Find<Button>("PlantMemory.Save").onClick;
                f.Store.Fail = true; f.Click("PlantMemory.Save");
                Assert(Find<InputField>("PlantMemory.EditField.1").text == "수정한 합성 기억"
                    && !string.IsNullOrEmpty(Find<Text>("PlantMemory.Error").text), "Failed save discarded the edit or gave no retry feedback.");
                Assert(PlantAlbumHistory.MemoryRecord(f.Service.Snapshot, plantId, organId).Note == "합성 기억", "Failed edit escaped its transaction.");
                f.Store.Fail = false; f.Click("PlantMemory.Save");
                Assert(PlantAlbumHistory.MemoryRecord(f.Service.Snapshot, plantId, organId).Note == "수정한 합성 기억", "Retry did not commit the edit.");
                writes = f.Store.Writes; staleSave.Invoke();
                Assert(f.Store.Writes == writes, "A stale editor callback wrote again after navigation.");

                f.Home.navigation.ShowCourse(); f.Ui.ShowPlantPhoto(photoId); f.Render();
                var view = Find<GardenPlantPhotoView>("PlantAlbum.DetailPreview");
                var image = view.GetComponent<RawImage>();
                Assert(view.RenderedMemoIds.Contains(organId) && view.MemoRepresentationKey == GardenMemoryPicker.RepresentationKey,
                    "Album photo did not render canonical memory geometry.");
                Assert(Find<Text>("PlantAlbum.DetailPeriod").text == PlantAlbumHistory.Period(f.Service.Snapshot.PlantPhotos.Single(), f.Service.CurrentTimeZone),
                    "The photo period differs from its stored business dates.");
                Assert(Find<Text>("PlantAlbum.RecordBasic." + memoId).text.Contains("P01")
                    && Find<Text>("PlantAlbum.RecordAdditional." + memoId).text.Contains("M17"), "Daily row lost its original P/M snapshots.");
                var camera = Studio<Camera>("camera"); var plant = Studio<ProceduralPlantView>("plant");
                Vector2 pointOnPhoto = f.LeafPoint(plant, camera, image, point => view.TryPickMemo(point, f.Camera, out var id) && id == organId);
                // A second thumbnail replaces the shared studio. The first view must
                // retain its own immutable ray basis and geometry for input.
                var node = new GameObject("Synthetic other thumbnail", typeof(RectTransform), typeof(RawImage));
                node.transform.SetParent(image.transform.parent, false);
                FigmaMissionUi.SetRect((RectTransform)node.transform, -2000, 0, 80, 80);
                var other = node.AddComponent<GardenPlantPhotoView>();
                var alternate = PlantGenerator.Create("synthetic", "other-photo", 21, PlantGrowthMode.MissionCompletions);
                alternate.MeshVersion = PlantGeometry.CurrentMeshVersion;
                other.Initialize(alternate, 1.9f, false); Assert(other.RenderNow(), "Second photo did not render.");
                Assert(view.TryPickMemo(pointOnPhoto, f.Camera, out var held) && held == organId, "Shared studio reuse changed the first photo's picker.");
                UnityEngine.Object.DestroyImmediate(node);
                f.Tap(image, pointOnPhoto); Assert(GardenMemoPopup.IsOpen && Find<Text>("GardenMemoPopup.Note").text == "수정한 합성 기억",
                    "Photo leaf tap did not open the edited record.");
                f.Click("GardenMemoPopup.Close"); f.Drag(image, pointOnPhoto);
                Assert(!GardenMemoPopup.IsOpen, "Photo orbit drag opened a record.");
                view.ResetView(); f.Render(); f.Capture("album-memory-records");
                var previewRect = f.Bounds((RectTransform)view.transform);
                var records = Find<ScrollRect>("PlantAlbum.RecordsViewport"); records.verticalNormalizedPosition = 0; f.Layout();
                Assert(f.Bounds((RectTransform)view.transform) == previewRect, "Record scrolling moved the 3D preview.");

                f.Clock.Now = f.Clock.Now.AddDays(1); Complete(f.Service, "synthetic-next", "free-mission:M17");
                state = f.Service.Snapshot; string nextId = WeeklyPlants.Active(state).Id; int coins = state.Coins;
                Assert(nextId != plantId && WeeklyPlants.Active(state).Memories.Count == 0, "Next growth did not create an independent plant.");
                f.Home.navigation.ShowIsland(); f.Home.RefreshFromState(); f.Render();
                Assert(f.Home.ShownPlantId == nextId && !f.Home.ProceduralPlant.Geometry.Organs.Any(o => o.Selectable), "Archived leaves leaked into the next garden plant.");
                f.Capture("next-individual");
                f.Home.navigation.ShowCourse(); f.Ui.ShowPlantPhoto(photoId); f.Render();
                f.Click("PlantAlbum.Record." + memoId); f.Click("GardenMemoPopup.Delete");
                f.Store.Fail = true; f.Click("PlantMemory.DeleteConfirm");
                Assert(PlantAlbumHistory.MemoryRecord(f.Service.Snapshot, plantId, organId) != null, "Failed delete lost the record.");
                f.Store.Fail = false; f.Click("PlantMemory.DeleteConfirm"); f.Render();
                state = f.Service.Snapshot;
                Assert(PlantAlbumHistory.MemoryRecord(state, plantId, organId) == null
                    && PlantAlbumHistory.Records(state, state.PlantPhotos.Single()).Length == 0, "Deleted text reappeared through album fallback.");
                var retainedLeaf = PlantAlbumHistory.Leaves(state, plantId).Single();
                Assert(retainedLeaf.OrganId == organId && retainedLeaf.Slot == 0 && !retainedLeaf.Linked, "Deletion removed or moved the retained leaf geometry.");
                Assert(PlantCodec.Encode(state.PlantCollection.Plants.Single(p => p.Id == plantId).Recipe) == recipe
                    && WeeklyPlants.Active(state).Id == nextId && state.Coins == coins, "Record deletion changed appearance, active growth target or coins.");
                Assert(!Find<GardenPlantPhotoView>("PlantAlbum.DetailPreview").RenderedMemoIds.Contains(organId), "Deleted leaf remained clickable.");
                f.Capture("album-deleted-memory");
                using (var reopened = new GardenStateService(f.Store, f.Clock, new DemoBalanceConfig()))
                { Assert(reopened.Open(), reopened.LastError); Assert(StateCodec.Encode(reopened.Snapshot) == StateCodec.Encode(state), "Reopening changed the committed plant collection."); }
            }
        }

        static T Studio<T>(string name) where T : class
        {
            var studio = typeof(GardenPlantPhotoView).GetField("studio", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
            return (T)studio.GetType().GetField(name, Private).GetValue(studio);
        }
        static void SetPitch(GardenPlantPhotoView view, float angle)
        { typeof(GardenPlantPhotoView).GetField("pitch", Private).SetValue(view, angle); Assert(view.RenderNow(), "Render changed elevation."); }
        static PlantRecipe Mature(string identity, int primary, int secondary)
        {
            var recipe = PlantGenerator.Create("synthetic-memory-bounds", identity, 21, PlantGrowthMode.MissionCompletions);
            recipe.MeshVersion = PlantGeometry.CurrentMeshVersion; recipe.Counts[primary] = 14; recipe.Counts[secondary] += 7;
            for (int i = 0; i < 21; i++) recipe = PlantGenerator.Advance(recipe);
            return recipe;
        }
        static void StressRender()
        {
            using (var f = new Fixture(390, 844))
            {
                f.Home.navigation.ShowCourse(); f.Ui.ShowPlantPhoto(f.Service.Snapshot.PlantPhotos.Single().Id); f.Render();
                var view = Find<GardenPlantPhotoView>("PlantAlbum.DetailPreview");
                var leaves = Enumerable.Range(0, 60).Select(i => new PlantMemoryLeaf { OrganId = "stress/memory/" + i, Slot = i, Linked = i % 11 != 0 }).ToArray();
                int speciesIndex = 0;
                foreach (var species in PlantCatalog.All)
                {
                    var recipe = Mature("mature-" + species.Id, species.Primary, species.Secondary);
                    Assert(recipe.Mature && recipe.GrowthStep == 21 && recipe.MeshVersion == 6, "Stress fixture must use new 21-step v6 geometry.");
                    for (int page = 0; page < 3; page++)
                    {
                        view.Initialize(recipe, 1.9f, true, null, leaves, page); Assert(view.RenderNow(), "Render mature memory page.");
                        var geometry = Studio<ProceduralPlantView>("plant").Geometry;
                        Assert(geometry.TriangleCount <= 8000 && geometry.Organs.Count == (page < 2 ? 21 : 18), "A memory page exceeded mesh budget or lost its final slots.");
                        Assert(view.RenderedMemoIds.SequenceEqual(leaves.Where(l => l.Slot / 21 == page && l.Linked).Select(l => l.OrganId)), "Page picked IDs differ from displayed linked leaves.");
                        AssertFrame(Studio<ProceduralPlantView>("plant"), Studio<Camera>("camera"));
                        if (speciesIndex == 0) f.Capture("mature-page-" + page);
                    }
                    speciesIndex++;
                }
                Assert(speciesIndex >= 6, "Species matrix was unexpectedly empty.");
                view.SetMemoryPage(99); Assert(view.MemoryPage == 2, "Upper page bound lost its last page.");
                view.SetMemoryPage(-1); Assert(view.MemoryPage == 0, "Lower page bound was not clamped.");
                var sample = Mature("nature-repeatable", 0, 1);
                view.Initialize(sample, 1.9f, true, null, leaves); view.RenderNow();
                string nature = view.EnvironmentKey; byte[] pixels = f.PhotoBytes(view);
                view.Rotate(87); view.SetZoom(.75f); view.RenderNow();
                Assert(view.EnvironmentKey == nature, "Orbit/zoom changed the saved environment identity.");
                view.ResetView(); view.RenderNow();
                Assert(pixels.SequenceEqual(f.PhotoBytes(view)), "Returning to the same recipe, time and angles changed the actual rendered photo.");
                foreach (float angle in new[] { 0f, -15f, -30f, -85f })
                {
                    SetPitch(view, angle); Assert(Mathf.Abs(view.LastRenderedEnvironmentOpacity - GardenPlantPhotoView.EnvironmentOpacity(angle)) < .00001f,
                        "Low-angle environment did not apply its uniform fade.");
                    AssertFrame(Studio<ProceduralPlantView>("plant"), Studio<Camera>("camera"));
                    f.Capture("memory-low-angle-" + Math.Abs(angle));
                }
                Assert(GardenPlantPhotoView.SharedCameraCount == 1 && GardenPlantPhotoView.OwnedEnvironmentMeshCount <= 4,
                    "Memory pages created extra cameras or unbounded environment meshes.");
            }
        }
        static void AssertFrame(ProceduralPlantView plant, Camera camera)
        {
            foreach (var vertex in plant.Geometry.Vertices)
            {
                var viewport = camera.WorldToViewportPoint(plant.transform.TransformPoint(new Vector3((float)vertex.X, (float)vertex.Y, (float)vertex.Z)));
                Assert(viewport.z > 0 && viewport.x >= 0 && viewport.x <= 1 && viewport.y >= 0 && viewport.y <= 1,
                    "Plant or memory vertex is clipped at the default photo zoom: " + viewport);
            }
        }

        sealed class Fixture : IDisposable
        {
            public readonly GardenHomePresenter Home; public readonly WeekOneQuestDemo Ui;
            public readonly GardenStateService Service; public readonly Store Store = new Store(); public readonly Clock Clock = new Clock();
            public readonly Camera Camera; public readonly PrototypeGardenPreviewLayout Preview;
            readonly EventSystem events; readonly RenderTexture target; readonly int width, height;
            public Fixture(int width, int height)
            {
                this.width = width; this.height = height;
                EditorSceneManager.OpenScene("Assets/Scenes/MockupMain.unity", OpenSceneMode.Single); AndroidPlatformBridge.UseMockProvider();
                Service = new GardenStateService(Store, Clock, new DemoBalanceConfig { VisitorChancePercent = 0 }); Assert(Service.Open(), Service.LastError);
                Complete(Service, "synthetic-basic", "course:P01"); Complete(Service, "synthetic-additional", "free-mission:M17");
                Assert(Service.BeginDailyEmotion(Service.Snapshot.LastCycleId), Service.LastError);
                Assert(Service.CompleteDailyEmotion(DailyEmotionPolicy.RecordId(Service.Snapshot.LastCycleId), "편안함", "합성 기억"), Service.LastError);
                Assert(Service.SetPlantCompleted(WeeklyPlants.Active(Service.Snapshot).Id, true), Service.LastError);
                Home = UnityEngine.Object.FindFirstObjectByType<GardenHomePresenter>(FindObjectsInactive.Include); Ui = Home.activity;
                Ui.Initialize(Service, true); Home.navigation.ShowIsland(); Home.RefreshFromState(); Home.ResetView();
                Camera = GameObject.Find("MockupCamera").GetComponent<Camera>(); Camera.GetUniversalAdditionalCameraData().renderPostProcessing = false;
                foreach (var canvas in Resources.FindObjectsOfTypeAll<Canvas>().Where(c => c.gameObject.scene.IsValid() && c.isRootCanvas))
                { canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = Camera; canvas.planeDistance = canvas == Home.homeCanvas ? .75f : 1; }
                target = new RenderTexture(width, height, 24); target.Create(); Camera.targetTexture = target; Camera.aspect = width / (float)height;
                Preview = Home.homeCanvas.GetComponentInChildren<PrototypeGardenPreviewLayout>(true);
                events = UnityEngine.Object.FindFirstObjectByType<EventSystem>(FindObjectsInactive.Include);
                if (events == null) events = new GameObject("Synthetic memory event system", typeof(EventSystem)).GetComponent<EventSystem>();
                events.gameObject.SetActive(true);
                if (EventSystem.current == null) typeof(EventSystem).GetMethod("OnEnable", Private).Invoke(events, null);
                EventSystem.current = events; Render();
            }
            public void Layout()
            {
                for (int pass = 0; pass < 3; pass++)
                {
                    foreach (var caster in Resources.FindObjectsOfTypeAll<GraphicRaycaster>().Where(c => c.isActiveAndEnabled))
                        if (!RaycasterManager.GetRaycasters().Contains(caster)) typeof(BaseRaycaster).GetMethod("OnEnable", Private).Invoke(caster, null);
                    foreach (var text in Resources.FindObjectsOfTypeAll<TMP_Text>().Where(t => t.gameObject.activeInHierarchy)) text.ForceMeshUpdate(false, true);
                    Canvas.ForceUpdateCanvases();
                    foreach (var fit in Resources.FindObjectsOfTypeAll<FigmaMissionCanvasFit>().Where(x => x.isActiveAndEnabled)) fit.Fit();
                    foreach (var page in Resources.FindObjectsOfTypeAll<FocusMobilePageLayout>().Where(x => x.isActiveAndEnabled)) page.Refresh();
                    foreach (var detail in Resources.FindObjectsOfTypeAll<GardenPlantAlbumDetailLayout>().Where(x => x.isActiveAndEnabled)) detail.Refresh();
                    foreach (var popup in Resources.FindObjectsOfTypeAll<GardenMemoPopup>().Where(x => x.isActiveAndEnabled)) popup.Layout();
                    Canvas.ForceUpdateCanvases();
                }
                Camera.Render();
            }
            public void Render()
            {
                Layout(); Preview.Apply(); Home.gardenCamera.Render();
                foreach (var view in Resources.FindObjectsOfTypeAll<GardenPlantPhotoView>().Where(v => v.isActiveAndEnabled)) Assert(view.RenderNow(), "Visible photo render failed.");
                Layout();
            }
            public Rect Bounds(RectTransform rect)
            {
                var corners = new Vector3[4]; rect.GetWorldCorners(corners);
                Vector2 a = RectTransformUtility.WorldToScreenPoint(Camera, corners[0]), b = RectTransformUtility.WorldToScreenPoint(Camera, corners[2]);
                return Rect.MinMaxRect(a.x, a.y, b.x, b.y);
            }
            Vector2 Center(RectTransform rect) => RectTransformUtility.WorldToScreenPoint(Camera, rect.TransformPoint(rect.rect.center));
            public Vector2 LeafPoint(ProceduralPlantView plant, Camera camera, RawImage image, Func<Vector2, bool> picker)
            {
                var data = plant.Geometry; var rect = (RectTransform)image.transform; var uv = image.uvRect;
                foreach (var organ in data.Organs.Where(o => o.Selectable))
                    for (int triangle = organ.FirstTriangle; triangle < organ.FirstTriangle + organ.TriangleCount; triangle++)
                    {
                        var a = data.Vertices[triangle * 3]; var b = data.Vertices[triangle * 3 + 1]; var c = data.Vertices[triangle * 3 + 2];
                        var localCenter = new Vector3((float)(a.X + b.X + c.X) / 3, (float)(a.Y + b.Y + c.Y) / 3, (float)(a.Z + b.Z + c.Z) / 3);
                        var viewport = camera.WorldToViewportPoint(plant.transform.TransformPoint(localCenter));
                        if (viewport.z <= 0 || viewport.x < 0 || viewport.x > 1 || viewport.y < 0 || viewport.y > 1) continue;
                        var local = rect.rect.min + Vector2.Scale(new Vector2((viewport.x - uv.x) / uv.width, (viewport.y - uv.y) / uv.height), rect.rect.size);
                        Vector2 point = RectTransformUtility.WorldToScreenPoint(Camera, rect.TransformPoint(local));
                        if (!picker(point)) continue;
                        var hits = Hits(point); if (hits.Count > 0 && hits[0].gameObject == image.gameObject) return point;
                    }
                throw new InvalidOperationException("No actually visible linked leaf point reaches the RawImage and picker: " + image.name);
            }
            List<RaycastResult> Hits(Vector2 point)
            { var hits = new List<RaycastResult>(); events.RaycastAll(new PointerEventData(events) { position = point }, hits); report.raycasts++; return hits; }
            PointerEventData Pointer(Vector2 point)
            {
                var hits = Hits(point); Assert(hits.Count > 0, "No actual canvas raycast at " + point);
                return new PointerEventData(events) { pointerId = -1, position = point, pressPosition = point,
                    button = PointerEventData.InputButton.Left, pointerPressRaycast = hits[0], pointerCurrentRaycast = hits[0], eligibleForClick = true, useDragThreshold = true };
            }
            public void Tap(RawImage image, Vector2 point)
            {
                var data = Pointer(point); Assert(data.pointerPressRaycast.gameObject == image.gameObject, "Memory surface is not the top UI hit.");
                ExecuteEvents.Execute(image.gameObject, data, ExecuteEvents.pointerDownHandler); ExecuteEvents.Execute(image.gameObject, data, ExecuteEvents.pointerUpHandler);
                report.clicks++; Layout();
            }
            public void Drag(RawImage image, Vector2 point)
            {
                var data = Pointer(point); ExecuteEvents.Execute(image.gameObject, data, ExecuteEvents.pointerDownHandler);
                data.dragging = true; data.eligibleForClick = false; ExecuteEvents.Execute(image.gameObject, data, ExecuteEvents.beginDragHandler);
                data.position = point + new Vector2(34, 13); data.delta = new Vector2(34, 13); ExecuteEvents.Execute(image.gameObject, data, ExecuteEvents.dragHandler);
                data.position = point; data.delta = new Vector2(-34, -13); ExecuteEvents.Execute(image.gameObject, data, ExecuteEvents.dragHandler);
                ExecuteEvents.Execute(image.gameObject, data, ExecuteEvents.pointerUpHandler); ExecuteEvents.Execute(image.gameObject, data, ExecuteEvents.endDragHandler); Layout();
            }
            public void Click(string name)
            {
                Layout(); var button = Find<Button>(name); Assert(button != null && button.IsInteractable(), "Missing interactive button: " + name);
                var scroll = button.GetComponentInParent<ScrollRect>();
                if (scroll != null)
                {
                    // Reveal through the actual scroll viewport, without moving controls.
                    for (int i = 0; i <= 20; i++)
                    {
                        scroll.verticalNormalizedPosition = 1 - i / 20f; Layout();
                        var hits = Hits(Center((RectTransform)button.transform));
                        if (hits.Count > 0 && (hits[0].gameObject == button.gameObject || hits[0].gameObject.transform.IsChildOf(button.transform))) break;
                    }
                }
                var data = Pointer(Center((RectTransform)button.transform));
                Assert(data.pointerPressRaycast.gameObject == button.gameObject || data.pointerPressRaycast.gameObject.transform.IsChildOf(button.transform), "Button is obscured: " + name);
                ExecuteEvents.Execute(button.gameObject, data, ExecuteEvents.pointerDownHandler); ExecuteEvents.Execute(button.gameObject, data, ExecuteEvents.pointerUpHandler);
                ExecuteEvents.Execute(button.gameObject, data, ExecuteEvents.pointerClickHandler); report.clicks++; Layout();
            }
            public byte[] PhotoBytes(GardenPlantPhotoView view) => Pixels((RenderTexture)view.GetComponent<RawImage>().texture);
            static byte[] Pixels(RenderTexture source)
            {
                var prior = RenderTexture.active; var image = new Texture2D(source.width, source.height, TextureFormat.RGB24, false);
                try { RenderTexture.active = source; image.ReadPixels(new Rect(0, 0, source.width, source.height), 0, 0); image.Apply(); return image.EncodeToPNG(); }
                finally { RenderTexture.active = prior; UnityEngine.Object.DestroyImmediate(image); }
            }
            public void Capture(string name)
            { Layout(); string file = name + "-" + width + "x" + height + ".png"; File.WriteAllBytes(Path.Combine(output, file), Pixels(target)); report.captures.Add(file); }
            public void Dispose()
            {
                GardenMemoPopup.TryCloseOpen(); Camera.targetTexture = null; target.Release(); UnityEngine.Object.DestroyImmediate(target);
                // Runtime-only components do not receive OnDestroy in EditMode; this
                // is fixture cleanup, not a substitute for the photo lifecycle test.
                if (Home.ProceduralPlant != null) typeof(ProceduralPlantView).GetMethod("OnDestroy", Private).Invoke(Home.ProceduralPlant, null);
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single); Service.Dispose();
            }
        }
    }
}
