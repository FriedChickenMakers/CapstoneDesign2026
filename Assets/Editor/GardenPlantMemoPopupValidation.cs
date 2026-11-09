using System;
using System.Collections.Generic;
using System.Globalization;
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
    // Explicit copied-project entry point. No source Editor polling, OS input,
    // device, user preferences or real save is used by this validation.
    public static class GardenPlantMemoPopupValidation
    {
        [Serializable] sealed class Check { public string name, status, detail; }
        [Serializable] sealed class Report
        {
            public string utc, unityVersion, scope;
            public int passed, failed, assertions, clicks, raycasts;
            public List<Check> checks = new List<Check>();
            public List<string> captures = new List<string>(), errors = new List<string>(), diagnostics = new List<string>();
        }
        static Report report; static string output;
        public static void RunBatch()
        {
            var args = Environment.GetCommandLineArgs(); int at = Array.IndexOf(args, "-memoPopupReport");
            if (!Application.isBatchMode || Application.companyName != "CapstoneSyntheticValidation"
                || !Application.productName.StartsWith("MemoPopup-", StringComparison.Ordinal) || at < 0)
                throw new InvalidOperationException("Memo popup validation requires a copied synthetic batch project.");
            string path = args[at + 1]; output = Path.GetDirectoryName(path); Directory.CreateDirectory(output);
            report = new Report { utc = DateTimeOffset.UtcNow.ToString("O"), unityVersion = Application.unityVersion,
                scope = "Hidden copied MockupMain, synthetic memory saves, real GPU/GraphicRaycaster/EventSystem pointer handlers; no source Editor, physical input or device" };
            Application.logMessageReceived += Observe; GardenVisualClock.SetOverrideMinutes(12 * 60);
            try
            {
                Run("Current memo popup and long-note scroll 360x640", () => PopupFlow(360, 640));
                Run("Current memo popup and long-note scroll 390x844", () => PopupFlow(390, 844));
                Run("Garden memo click, drag, pinch and cancellation 360x640", () => HomeInput(360, 640));
                Run("Garden memo click, drag, pinch and cancellation 390x844", () => HomeInput(390, 844));
                Run("Photo memo picking survives shared studio reuse 360x640", () => PhotoInput(360, 640));
                Run("Photo memo picking survives shared studio reuse 390x844", () => PhotoInput(390, 844));
                Run("Photo orbit follows garden drag angles and full pitch range 360x640", () => PhotoOrbit(360, 640));
                Run("Photo orbit follows garden drag angles and full pitch range 390x844", () => PhotoOrbit(390, 844));
                Run("Archived period and independent daily record list 360x640", () => AlbumRecords(360, 640));
                Run("Archived period and independent daily record list 390x844", () => AlbumRecords(390, 844));
                Run("Legacy record projection requires exact individual evidence", LegacyRecordProjection);
                Run("Environment shader applies uniform opacity to every quadrant and vertex class", UniformEnvironmentFade);
                Run("Four deterministic natural environments and low-angle GPU visibility", NaturalEnvironments);
                Run("Archived snapshots survive missing and reused daily record IDs", SnapshotHistory);
                Run("Popup owns input and closes on back, page change and full reset", PopupLifecycle);
                Run("Transformed memo rays preserve IDs across the 512 boundary", Geometry);
                Run("Popup, memo and photo owners release all resources", () => {
                    Assert(!GardenMemoPopup.IsOpen, "Popup remains after disposing its owner.");
                    Assert(GardenPlantMemoView.OwnedMeshCount == 0 && GardenPlantMemoView.OwnedMaterialCount == 0
                        && GardenPlantMemoView.OwnedTextureCount == 0, "Memo resources survived fixture disposal.");
                    Assert(GardenPlantPhotoView.ActiveViewCount == 0 && GardenPlantPhotoView.SharedCameraCount == 0
                        && GardenPlantPhotoView.OwnedTextureCount == 0, "Photo resources survived fixture disposal.");
                    Assert(GardenPlantPhotoView.OwnedEnvironmentMeshCount == 0
                        && !Resources.FindObjectsOfTypeAll<Mesh>().Any(mesh => mesh.name.StartsWith("Plant album nature ", StringComparison.Ordinal)),
                        "Natural environment meshes survived the final album owner.");
                });
            }
            finally
            {
                GardenMemoPopup.TryCloseOpen(); GardenVisualClock.UseSystemTime(); AndroidPlatformBridge.UseMockProvider();
                Application.logMessageReceived -= Observe;
                File.WriteAllText(path, JsonUtility.ToJson(report, true));
            }
            EditorApplication.Exit(report.failed == 0 && report.errors.Count == 0 ? 0 : 1);
        }
        static void Observe(string message, string stack, LogType type)
        { if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) report.errors.Add(type + ": " + message + "\n" + stack); }
        static void Assert(bool value, string message) { report.assertions++; if (!value) throw new InvalidOperationException(message); }
        static void Run(string name, Action action)
        { try { action(); report.passed++; report.checks.Add(new Check { name = name, status = "pass" }); }
          catch (Exception error) { report.failed++; report.checks.Add(new Check { name = name, status = "fail", detail = error.ToString() }); GardenMemoPopup.TryCloseOpen(); } }
        static string Xml(GardenStateService service) => StateCodec.Encode(service.Snapshot);
        static T Find<T>(string name) where T : Component => Resources.FindObjectsOfTypeAll<T>()
            .FirstOrDefault(item => item != null && item.name == name && item.gameObject.activeInHierarchy);
        static string Screen(WeekOneQuestDemo ui) => (string)typeof(WeekOneQuestDemo).GetField("screen", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(ui);
        static void Complete(GardenStateService service, string id, string code, int order = 0)
        {
            Assert(service.BeginFocusSession(id, code, order) && service.StartSession(id), "Create synthetic mission: " + service.LastError);
            var definition = FocusMissionCatalog.FindForSession(service.Snapshot.Sessions.Single(item => item.SessionId == id));
            for (int i = 0; i < definition.Steps.Length; i++)
            { var step = definition.Steps[i]; string answer = step.RequiredAnswer ? step.Options.FirstOrDefault() ?? "synthetic answer" : null;
              Assert(service.ConfirmFocusStep(id, i, answer, answer == null), "Confirm synthetic practice: " + service.LastError); }
            Assert(service.CompletePracticeSession(id), "Complete synthetic practice: " + service.LastError);
        }
        static void Daily(GardenStateService service, string suffix, string mood, string note)
        {
            int order = FocusMissionPolicy.NextOrder(service.Snapshot);
            Complete(service, "synthetic-p-" + suffix, FocusMissionPolicy.MissionAt(order, "8w").Code, order);
            Complete(service, "synthetic-m-" + suffix, "M03");
            Assert(service.BeginDailyEmotion(service.CurrentCycleId)
                && service.CompleteDailyEmotion(DailyEmotionPolicy.RecordId(service.CurrentCycleId), mood, note), "Create synthetic memo: " + service.LastError);
        }
        sealed class Clock : IClock
        { public DateTimeOffset UtcNow => DateTimeOffset.Parse("2026-11-09T03:30:00Z"); public TimeZoneInfo TimeZone => TimeZoneInfo.Utc; }
        sealed class Store : IStateStore
        {
            string value; public int Writes;
            public GardenState Load() => value == null ? null : StateCodec.Decode(value);
            public void Save(GardenState state) { value = StateCodec.Encode(state); Writes++; }
        }
        static Store Seed()
        {
            var store = new Store();
            using (var service = new GardenStateService(store, new Clock(), new DemoBalanceConfig { VisitorChancePercent = 0 }))
            {
                Assert(service.Open() && service.SelectMbctProgram("8w"), "Open synthetic memo history.");
                for (int plant = 0; plant < 3; plant++)
                {
                    int count = plant == 1 ? 1 : 2;
                    for (int index = 0; index < count; index++)
                    {
                        string text = plant == 2 && index == 0 ? string.Concat(Enumerable.Repeat("합성 기록입니다. 천천히 숨을 쉬며 오늘의 감정을 살펴보았어요. ", 20)).Substring(0, 500)
                            : plant == 0 && index == 1 ? "" : "<b>합성 메모 " + plant + "/" + index + "</b>\n식물과 함께 간직한 기록입니다.";
                        Daily(service, plant + "-" + index, plant == 1 ? "" : "편안함", text);
                        if (index + 1 < count) Assert(service.AdvancePreviewDay(), "Advance synthetic business day.");
                    }
                    if (plant < 2)
                    {
                        string source = service.Snapshot.PlantAppearance.Recipe.InstanceId;
                        Assert(service.SetPlantCompleted(source, true) && service.SavePlantPhoto("synthetic-photo-" + plant, source)
                            && service.StartNewPlant("synthetic-seed-" + plant, source) && service.AdvancePreviewDay(), "Archive memo snapshot and begin a distinct plant.");
                    }
                }
            }
            return store;
        }
        static void PopupFlow(int width, int height)
        {
            using (var f = new Fixture(width, height))
            {
                var state = f.Service.Snapshot; var memo = state.PlantAppearance.Memos[0];
                string before = Xml(f.Service); int writes = f.Store.Writes;
                var orbit = f.Preview.orbit; Vector2 angles = orbit.Angles; float zoom = f.Preview.Zoom;
                Assert(f.Ui.OpenPlantMemo(state.PlantAppearance.Recipe.InstanceId, memo.Id, null, f.Preview.transform), "Open current memo from its actual owner.");
                f.Layout(); f.AssertPopup(memo); f.Capture("home-long-note-open");
                var scroll = Find<ScrollRect>("GardenMemoPopup.Scroll");
                Assert(scroll != null && scroll.content.rect.height > scroll.viewport.rect.height, "Long note has scrollable content.");
                float prior = scroll.verticalNormalizedPosition;
                Vector2 point = f.ScreenCenter(scroll.viewport);
                var data = f.Pointer(point, -1); data.scrollDelta = new Vector2(0, -100);
                ExecuteEvents.ExecuteHierarchy(f.TopHit(point).gameObject, data, ExecuteEvents.scrollHandler); f.Layout();
                Assert(scroll.verticalNormalizedPosition < prior, "Pointer wheel reaches actual popup scroll content.");
                scroll.verticalNormalizedPosition = 0; f.Layout();
                var note = Find<Text>("GardenMemoPopup.AdditionalMission");
                Vector3 bottom = note.rectTransform.TransformPoint(new Vector3(note.rectTransform.rect.center.x, note.rectTransform.rect.yMin, 0));
                Assert(scroll.viewport.rect.Contains((Vector2)scroll.viewport.InverseTransformPoint(bottom)), "The final mission label below the long note is reachable at the bottom of the popup.");
                f.Capture("home-long-note-bottom");
                f.Click("GardenMemoPopup.Close");
                Assert(!GardenMemoPopup.IsOpen && orbit.Angles == angles && Mathf.Approximately(f.Preview.Zoom, zoom), "Closing popup preserves garden orbit and zoom.");
                Assert(Xml(f.Service) == before && f.Store.Writes == writes, "Reading and closing memos never writes state or changes rewards.");
                Assert(!f.Ui.OpenPlantMemo("wrong-plant", memo.Id, null, f.Preview.transform)
                    && !f.Ui.OpenPlantMemo(state.PlantAppearance.Recipe.InstanceId, "missing-memo", null, f.Preview.transform), "Unknown plant and memo identities cannot open a popup.");
                Assert(!GardenMemoPopup.IsOpen && Xml(f.Service) == before, "Rejected lookups are harmless.");
            }
        }
        static void HomeInput(int width, int height)
        {
            using (var f = new Fixture(width, height))
            {
                var memo = f.Service.Snapshot.PlantAppearance.Memos[1]; var image = f.Preview.GetComponent<RawImage>();
                f.Capture("garden-before-memo-tap");
                Vector2 point = f.VisibleHomeMemoPoint(memo.Id);
                string before = Xml(f.Service); int writes = f.Store.Writes; Vector2 original = f.Preview.orbit.Angles;
                f.Tap(image, point, 0); f.AssertPopup(memo); f.Capture("garden-tap-popup"); f.Click("GardenMemoPopup.Close");
                Assert(f.Preview.orbit.Angles == original, "A single memo tap does not rotate the garden.");
                f.Gesture(image, point, point + new Vector2(45, 0), false);
                Assert(!GardenMemoPopup.IsOpen && f.Preview.orbit.Angles != original, "Garden drag still rotates and never opens a memo.");
                f.Home.ResetView(); f.Render(); point = f.VisibleHomeMemoPoint(memo.Id);
                f.Gesture(image, point, point + new Vector2(45, 0), true);
                Assert(!GardenMemoPopup.IsOpen, "Returning a drag to its start is still not a tap.");
                f.Home.ResetView(); f.Render(); point = f.VisibleHomeMemoPoint(memo.Id);
                f.Pinch(image, point); Assert(!GardenMemoPopup.IsOpen, "Two-finger gesture cannot open a memo when fingers lift.");
                f.Home.ResetView(); f.Render(); point = f.VisibleHomeMemoPoint(memo.Id);
                f.CancelTap(image, point, "OnApplicationFocus", false); Assert(!GardenMemoPopup.IsOpen, "Focus loss cancels a pending home tap.");
                f.CancelTap(image, point, "OnApplicationPause", true); Assert(!GardenMemoPopup.IsOpen, "App pause cancels a pending home tap.");
                var rect = f.ScreenBounds((RectTransform)image.transform);
                f.DirectTap(image, new Vector2(rect.xMax + 20, rect.center.y), 0);
                Assert(!GardenMemoPopup.IsOpen, "Outside RawImage coordinates cannot select a memo.");
                Assert(Xml(f.Service) == before && f.Store.Writes == writes, "All garden pointer gestures preserve saved data.");
            }
        }
        static void PhotoInput(int width, int height)
        {
            using (var f = new Fixture(width, height))
            {
                var photos = f.Service.Snapshot.PlantPhotos; var first = photos[0]; var memo = first.Memos[0];
                f.Home.navigation.ShowCourse(); f.Ui.ShowPlantAlbum(); f.Layout(); f.Ui.ShowPlantPhoto(first.Id); f.Render();
                var view = Find<GardenPlantPhotoView>("PlantAlbum.DetailPreview"); Assert(view != null && view.IsInteractive, "Actual photo detail creates an interactive preview.");
                var image = view.GetComponent<RawImage>(); Vector2 point = f.PhotoPoint(view, memo.Id);
                Assert(view.TryPickMemo(point, f.Camera, out var picked) && picked == memo.Id, "Photo screen ray resolves the intended memo.");
                var otherNode = new GameObject("Synthetic competing photo", typeof(RectTransform), typeof(RawImage));
                try
                {
                    ((RectTransform)otherNode.transform).sizeDelta = new Vector2(210, 280);
                    var other = otherNode.AddComponent<GardenPlantPhotoView>(); other.Initialize(photos[1].Recipe, photos[1].Scale, false, photos[1].Memos);
                    Assert(other.RenderNow() && GardenPlantPhotoView.SharedCameraCount == 1, "Another photo reuses the same studio.");
                    Assert(view.TryPickMemo(point, f.Camera, out picked) && picked == memo.Id, "Another photo render cannot change this view's pick geometry or identity.");
                    foreach (var orbit in new[] { new Vector2(-18, 1), new Vector2(22, 1.15f), new Vector2(-47, .8f) })
                    {
                        view.ResetView(); view.Rotate(orbit.x + 18); view.SetZoom(orbit.y);
                        foreach (var paper in first.Memos)
                        {
                            var projected = f.PhotoPoint(view, paper.Id);
                            var studio = PhotoStudio();
                            var camera = (Camera)studio.GetType().GetField("camera", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(studio);
                            Assert(!camera.orthographic && Mathf.Approximately(camera.fieldOfView, 35), "Natural photo uses the same shared perspective camera with the approved field of view.");
                            Assert(view.TryPickMemo(projected, f.Camera, out picked) && picked == paper.Id,
                                "Perspective projection and per-pixel memo ray agree for both off-center papers at yaw/zoom " + orbit);
                            other.RenderNow();
                            Assert(view.TryPickMemo(projected, f.Camera, out picked) && picked == paper.Id,
                                "A competing thumbnail cannot replace this rotated view's stored eye and ray basis.");
                        }
                    }
                    view.ResetView(); point = f.PhotoPoint(view, memo.Id); other.RenderNow();
                    Assert(!other.TryPickMemo(Vector2.zero, null, out picked), "A thumbnail does not intercept its card's navigation click.");
                    f.Tap(image, point, -1); f.AssertPopup(memo); f.Capture("archived-photo-popup"); f.Click("GardenMemoPopup.Close");
                }
                finally { UnityEngine.Object.DestroyImmediate(otherNode); }
                float yaw = view.Yaw, zoom = view.Zoom; string before = Xml(f.Service); int writes = f.Store.Writes;
                f.Gesture(image, point, point + new Vector2(45, 0), false);
                Assert(!GardenMemoPopup.IsOpen && Mathf.Abs(Mathf.DeltaAngle(view.Yaw, yaw)) > 1, "Photo drag rotates without opening a memo.");
                view.ResetView(); f.Render(); point = f.PhotoPoint(view, memo.Id);
                f.Gesture(image, point, point + new Vector2(45, 0), true); Assert(!GardenMemoPopup.IsOpen, "Photo drag returning to origin is not a click.");
                view.ResetView(); f.Render(); point = f.PhotoPoint(view, memo.Id);
                f.Pinch(image, point); Assert(!GardenMemoPopup.IsOpen && !Mathf.Approximately(view.Zoom, zoom), "Photo pinch changes zoom and suppresses memo clicks.");
                view.ResetView(); f.Render(); point = f.PhotoPoint(view, memo.Id);
                f.CancelTap(image, point, "OnApplicationFocus", false); Assert(!GardenMemoPopup.IsOpen, "Photo focus loss cancels its pending click.");
                var area = f.ScreenBounds((RectTransform)image.transform);
                Assert(!view.TryPickMemo(new Vector2(area.xMin - 2, area.center.y), f.Camera, out picked), "Photo picking rejects points outside its displayed RawImage.");
                var oldUv = image.uvRect; image.uvRect = new Rect(.1f, .05f, .8f, .9f);
                point = f.PhotoPoint(view, memo.Id);
                Assert(view.TryPickMemo(point, f.Camera, out picked) && picked == memo.Id, "Photo screen conversion respects a cropped RawImage UV rectangle."); image.uvRect = oldUv;
                f.Render(); point = f.PhotoPoint(view, memo.Id); f.Tap(image, point, 0); f.AssertPopup(memo);
                f.Click("GardenMemoPopup.Close"); Assert(Screen(f.Ui) == "plant-photo:" + first.Id, "Popup close stays on the same photo detail.");
                Assert(Xml(f.Service) == before && f.Store.Writes == writes, "Photo picking and popup navigation preserve the saved snapshot.");
            }
        }
        static Store RecordListSeed()
        {
            var store = new Store();
            using (var service = new GardenStateService(store, new Clock(), new DemoBalanceConfig { VisitorChancePercent = 0 }))
            {
                Assert(service.Open() && service.SelectMbctProgram("8w"), "Open the long record-list fixture.");
                for (int day = 0; day < 12; day++)
                {
                    // Retain real one-per-day and six-per-week rules in this synthetic history.
                    while (FocusMissionPolicy.Availability(service.Snapshot) != null)
                        Assert(service.AdvancePreviewDay(), "Wait for an eligible synthetic practice day.");
                    Daily(service, "list-" + day, day == 0 ? "" : "편안함", "합성 날짜별 기록 " + day + " · 이 식물의 기록만 표시합니다.");
                    if (day < 11) Assert(service.AdvancePreviewDay(), "Advance the record-list fixture.");
                }
                string source = service.Snapshot.PlantAppearance.Recipe.InstanceId;
                Assert(service.SetPlantCompleted(source, true) && service.SavePlantPhoto("list-photo", source)
                    && service.StartNewPlant("list-next", source), "Archive twelve distinct daily snapshots.");
                source = service.Snapshot.PlantAppearance.Recipe.InstanceId;
                Assert(service.SetPlantCompleted(source, true) && service.SavePlantPhoto("empty-photo", source)
                    && service.StartNewPlant("empty-next", source), "Archive another individual with no daily records.");
            }
            return store;
        }
        static void AlbumRecords(int width, int height)
        {
            using (var f = new Fixture(width, height, RecordListSeed()))
            {
                var state = f.Service.Snapshot; var photo = state.PlantPhotos[0]; var empty = state.PlantPhotos[1];
                var records = photo.Memos.OrderByDescending(item => item.DailyEmotionRecordId, StringComparer.Ordinal).ToArray();
                string before = Xml(f.Service); int writes = f.Store.Writes;
                Assert(photo.StartedCycleId == "2026-11-08" && photo.Memos.Count == 12, "Pre-04:00 start uses the previous business day and retains twelve records.");
                f.Home.navigation.ShowCourse(); f.Ui.ShowPlantAlbum(); f.Render();
                var galleryPeriod = Find<Text>("PlantAlbum.Period." + photo.Id);
                string expectedPeriod = photo.StartedCycleId.Replace('-', '.') + " ~ " + photo.SavedCycleId.Replace('-', '.');
                Assert(galleryPeriod != null && galleryPeriod.text == expectedPeriod, "Gallery period uses persisted start and archive business dates.");
                f.Capture("album-period-gallery");
                f.Ui.ShowPlantPhoto(photo.Id); f.Render();
                var layout = Find<GardenPlantAlbumDetailLayout>("FigmaMission.Design");
                if (layout == null) layout = Resources.FindObjectsOfTypeAll<GardenPlantAlbumDetailLayout>().Single(item => item.isActiveAndEnabled);
                var scroll = Find<ScrollRect>("PlantAlbum.RecordsViewport"); var view = Find<GardenPlantPhotoView>("PlantAlbum.DetailPreview");
                Assert(Find<Text>("PlantAlbum.DetailPeriod").text == expectedPeriod, "Selected photo repeats its own saved period.");
                Assert(scroll != null && scroll.content.childCount == 12 && scroll.content.rect.height > scroll.viewport.rect.height,
                    "Every daily memo has one independently scrollable button.");
                Assert(view.GetComponentInParent<ScrollRect>() == null && layout.GetComponent<FocusMobilePageLayout>().FixedContent,
                    "The preview is fixed outside any outer ScrollRect.");
                for (int i = 0; i < records.Length; i++)
                {
                    var memo = records[i];
                    Assert(scroll.content.GetChild(i).name == "PlantAlbum.Record." + memo.Id, "Date rows are latest-first without cross-plant records.");
                    Assert(Find<Text>("PlantAlbum.RecordDate." + memo.Id).text == memo.DailyEmotionRecordId.Substring(DailyEmotionPolicy.RecordPrefix.Length).Replace('-', '.'), "Row date is the memo's business day.");
                    Assert(Find<Text>("PlantAlbum.RecordBasic." + memo.Id).text.Contains(memo.BasicMissionTitle)
                        && Find<Text>("PlantAlbum.RecordAdditional." + memo.Id).text.Contains(memo.AdditionalMissionTitle), "Both saved mission titles remain visible in their own row.");
                }
                var previewBounds = f.ScreenBounds((RectTransform)view.transform);
                Assert(Find<Button>("PlantAlbum.ZoomOut") == null && Find<Button>("PlantAlbum.ResetView") == null && Find<Button>("PlantAlbum.ZoomIn") == null,
                    "Photo detail omits all three removed inspection buttons.");
                Assert(previewBounds.height >= 90 && f.ScreenBounds(scroll.viewport).height >= 120,
                    "Both the plant and record viewport remain usable on this screen.");
                Assert(previewBounds.yMin >= f.ScreenBounds(scroll.viewport).yMax, "Fixed photo preview does not overlap the record list.");
                view.Rotate(31); view.SetZoom(1.25f); f.Render(); float yaw = view.Yaw, zoom = view.Zoom;
                f.Capture("album-records-top"); f.Click("PlantAlbum.Record." + records[0].Id); f.AssertPopup(records[0]);
                f.Capture("album-record-popup"); f.Click("GardenMemoPopup.Close");
                Vector2 point = f.ScreenCenter(scroll.viewport); var data = f.Pointer(point, -1); data.scrollDelta = new Vector2(0, -100);
                ExecuteEvents.ExecuteHierarchy(f.TopHit(point).gameObject, data, ExecuteEvents.scrollHandler); f.Layout();
                Assert(scroll.content.anchoredPosition.y > 0, "Actual pointer scrolling moves the record list.");
                scroll.verticalNormalizedPosition = 0; f.Layout();
                var offset = scroll.content.anchoredPosition;
                Assert(f.ScreenBounds((RectTransform)view.transform) == previewBounds
                    && view.Yaw == yaw && view.Zoom == zoom, "List scrolling preserves the fixed plant geometry, orbit and pinch zoom.");
                f.Capture("album-records-bottom");
                var stale = Find<Button>("PlantAlbum.Record." + records.Last().Id).onClick;
                f.Click("PlantAlbum.Record." + records.Last().Id); f.AssertPopup(records.Last());
                Assert(Find<Text>("GardenMemoPopup.Mood").text == "선택한 감정이 없어요.", "Memo-only days remain readable.");
                f.Click("GardenMemoPopup.Close");
                Assert(scroll.content.anchoredPosition == offset && view.Yaw == yaw && view.Zoom == zoom,
                    "Closing a record returns to the same scroll offset and plant view.");
                f.Ui.ShowPlantPhoto(empty.Id); f.Render(); stale.Invoke();
                Assert(!GardenMemoPopup.IsOpen && Screen(f.Ui) == "plant-photo:" + empty.Id, "A stale row from another photo cannot open its record.");
                Assert(Find<Text>("PlantAlbum.NoRecords") != null && Find<ScrollRect>("PlantAlbum.RecordsViewport").content.GetComponentsInChildren<Button>().Length == 0,
                    "An individual without memos shows an empty list without borrowing another plant's dates.");
                f.Capture("album-empty-records");
                Assert(Xml(f.Service) == before && f.Store.Writes == writes, "Album period, scroll, record clicks and photo switching never save or reward.");
            }
        }
        static void LegacyRecordProjection()
        {
            var store = Seed(); var state = store.Load(); var photo = state.PlantPhotos[0]; var memo = photo.Memos[0];
            var daily = state.DailyEmotionRecords.Single(item => item.Id == memo.DailyEmotionRecordId);
            photo.Memos.RemoveAt(0); daily.RewardVersion = 0; daily.RewardAmount = 0; daily.MemoId = "";
            var receipt = new PracticeGrowthRecord { SessionId = daily.Id, MissionId = DailyEmotionPolicy.MissionId,
                CycleId = daily.CycleId, EligibilityKey = "daily-emotion|" + daily.CycleId,
                CommittedUtc = daily.CompletedUtc, PlantInstanceId = photo.SourcePlantInstanceId, Outcome = PracticeGrowthOutcome.Grown,
                BeforeStep = 0, AfterStep = 1, BeforeGrowth = 0, AfterGrowth = .1f };
            state.PracticeGrowthRecords.Add(receipt);
            string snapshot = CapstoneDesign.Plants.PlantCodec.Encode(state);
            var projected = PlantAlbumHistory.Records(state, photo).Single(item => item.Id == "album-record:" + daily.Id);
            Assert(projected.Note == memo.Note && projected.Mood == memo.Mood && projected.BasicMissionId == memo.BasicMissionId
                && projected.AdditionalMissionId == memo.AdditionalMissionId, "Historical growth-backed records retain exact text and completed mission sources.");
            projected.Note = "detached mutation";
            Assert(CapstoneDesign.Plants.PlantCodec.Encode(state) == snapshot, "Legacy projection is detached and has no save side effects.");
            receipt.CommittedUtc = DateTimeOffset.Parse(receipt.CommittedUtc).AddSeconds(1).ToString("O");
            Assert(!PlantAlbumHistory.Records(state, photo).Any(item => item.Id == projected.Id), "Reused daily IDs with a different completion timestamp are excluded.");
            receipt.CommittedUtc = daily.CompletedUtc; receipt.PlantInstanceId = state.PlantAppearance.Recipe.InstanceId;
            Assert(!PlantAlbumHistory.Records(state, photo).Any(item => item.Id == projected.Id), "A receipt naming another individual cannot be attributed by overlapping dates.");
            receipt.PlantInstanceId = photo.SourcePlantInstanceId; receipt.Outcome = PracticeGrowthOutcome.Stored;
            Assert(!PlantAlbumHistory.Records(state, photo).Any(item => item.Id == projected.Id), "Banked growth is not evidence of a displayed plant's historical record.");
            photo.StartedUtc = photo.StartedCycleId = photo.SavedCycleId = "";
            Assert(PlantAlbumHistory.Period(photo, TimeZoneInfo.Utc).StartsWith("시작일 기록 없음 ~ "), "Old unknown starts are explicitly unknown.");
            var unknown = new GardenPlantMemo();
            Assert(PlantAlbumHistory.MissionLine(unknown, true) == "기본 · 기록 없음", "Unknown old mission metadata is not fabricated from the current catalogue.");
        }
        static void SnapshotHistory()
        {
            using (var f = new Fixture(360, 640))
            {
                var photo = f.Service.Snapshot.PlantPhotos[0]; var memo = photo.Memos[0];
                string frozen = CapstoneDesign.Plants.PlantCodec.Encode(photo);
                Assert(f.Service.ResetMissionHistory(), "Clear only visible history with the retained legacy reset API.");
                Assert(f.Service.GetDailyEmotion(memo.DailyEmotionRecordId) == null, "Archived memo's original daily record is absent.");
                f.Home.navigation.ShowCourse(); f.Ui.ShowPlantPhoto(photo.Id); f.Render();
                var view = Find<GardenPlantPhotoView>("PlantAlbum.DetailPreview");
                Assert(f.Ui.OpenPlantMemo(photo.SourcePlantInstanceId, memo.Id, photo.Id, view.transform), "Archived memo opens without its deleted daily record.");
                f.AssertPopup(memo); f.Capture("archived-record-after-history-reset"); f.Click("GardenMemoPopup.Close");
                // The latest memo shares today's ID; recreating the daily entry must
                // never overwrite the snapshot attached before the history reset.
                var current = f.Service.Snapshot.PlantAppearance.Memos.Last();
                Assert(f.Service.SelectMbctProgram("8w"), "Rechoose course after explicit history reset.");
                Daily(f.Service, "replacement", "평온함", "합성 새 기록: 이전 메모와 다른 내용");
                Assert(f.Service.GetDailyEmotion(current.DailyEmotionRecordId).Note != current.Note, "Same daily ID now intentionally contains different history text.");
                f.Home.navigation.ShowIsland(); f.Home.RefreshFromState(); f.Render();
                string before = Xml(f.Service); int writes = f.Store.Writes;
                Assert(f.Ui.OpenPlantMemo(current.PlantInstanceId, current.Id, null, f.Preview.transform), "Frozen current memo remains independently readable.");
                f.AssertPopup(current); f.Capture("memo-snapshot-reused-daily-id"); f.Click("GardenMemoPopup.Close");
                Assert(CapstoneDesign.Plants.PlantCodec.Encode(f.Service.Snapshot.PlantPhotos[0]) == frozen
                    && Xml(f.Service) == before && f.Store.Writes == writes, "Reading snapshots never substitutes or changes newer history.");
            }
        }
        static void PhotoOrbit(int width, int height)
        {
            using (var f = new Fixture(width, height))
            {
                var photo = f.Service.Snapshot.PlantPhotos[0];
                f.Home.navigation.ShowCourse(); f.Ui.ShowPlantPhoto(photo.Id); f.Render();
                var view = Find<GardenPlantPhotoView>("PlantAlbum.DetailPreview");
                var image = view.GetComponent<RawImage>(); var rect = (RectTransform)view.transform;
                Func<float> pitch = () => (float)typeof(GardenPlantPhotoView).GetField("pitch", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(view);
                Assert(Mathf.Approximately(view.Yaw, -18) && Mathf.Approximately(pitch(), 8) && Mathf.Approximately(view.Zoom, 1), "Photo retains its existing initial yaw, pitch and zoom.");
                string before = Xml(f.Service); int writes = f.Store.Writes;
                // Fixed numeric oracles are independent of the shared helper.
                // Input is normalized to the actual displayed RawImage dimensions.
                var cases = new[] {
                    new Vector4(.25f, .25f, 72, -37),
                    new Vector4(-.25f, -.25f, -108, 53),
                    new Vector4(1.25f, 0, 72, 8),
                    new Vector4(0, .8f, -18, -85),
                    new Vector4(0, -.8f, -18, 85)
                };
                foreach (var item in cases)
                {
                    view.ResetView(); f.Render();
                    var delta = Vector2.Scale(rect.rect.size, new Vector2(item.x, item.y));
                    Vector2 from = f.ScreenCenter(rect), to = RectTransformUtility.WorldToScreenPoint(f.Camera, rect.TransformPoint(rect.rect.center + delta));
                    var hit = f.TopHit(from);
                    Assert(hit.gameObject == image.gameObject, "The orbit gesture begins on the actual photo RawImage.");
                    f.Gesture(image, from, to, false); f.Render();
                    Assert(Mathf.Abs(Mathf.DeltaAngle(view.Yaw, item.z)) < .01f && Mathf.Abs(pitch() - item.w) < .01f,
                        "Photo drag matches independent 360/180-degree expectations; expected=" + item.z + "/" + item.w + ", actual=" + view.Yaw + "/" + pitch());
                    var garden = PrototypeIslandDrag.DragAngles(new Vector2(-18, 8), delta, rect.rect.size);
                    Assert(Mathf.Abs(Mathf.DeltaAngle(view.Yaw, garden.x)) < .01f && Mathf.Abs(pitch() - garden.y) < .01f,
                        "Photo and garden interpret the same normalized drag identically.");
                    Assert(!GardenMemoPopup.IsOpen && Mathf.Approximately(view.Zoom, 1), "Orbit never opens a memo or changes zoom.");
                    if (item.w == -37) f.Capture("photo-orbit-below-37");
                    if (item.w == -85) f.Capture("photo-orbit-lower-limit-85");
                    if (item.w == 85) f.Capture("photo-orbit-upper-limit-85");
                }
                view.ResetView(); view.SetZoom(1.25f); f.Render();
                Vector2 start = f.ScreenCenter(rect), end = RectTransformUtility.WorldToScreenPoint(f.Camera,
                    rect.TransformPoint(rect.rect.center + Vector2.Scale(rect.rect.size, new Vector2(.1f, .1f))));
                f.Gesture(image, start, end, false); f.Render();
                Assert(Mathf.Approximately(view.Zoom, 1.25f), "Existing non-default zoom is preserved during orbit.");
                view.ResetView(); f.Render();
                Assert(Mathf.Approximately(view.Yaw, -18) && Mathf.Approximately(pitch(), 8) && Mathf.Approximately(view.Zoom, 1), "Reset view restores the same original photo composition after extreme orbit.");
                Assert(Xml(f.Service) == before && f.Store.Writes == writes, "Full orbit and reset remain view-only with unchanged recipes, memos and rewards.");
            }
        }
        static object PhotoStudio() => typeof(GardenPlantPhotoView).GetField("studio", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
        static GameObject StudioRoot()
        {
            var studio = PhotoStudio();
            return (GameObject)studio.GetType().GetField("root", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(studio);
        }
        static Color32[] PhotoPixels(GardenPlantPhotoView view)
        {
            var target = (RenderTexture)view.GetComponent<RawImage>().texture;
            var prior = RenderTexture.active; var texture = new Texture2D(target.width, target.height, TextureFormat.RGB24, false);
            try { RenderTexture.active = target; texture.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0); texture.Apply(); return texture.GetPixels32(); }
            finally { RenderTexture.active = prior; UnityEngine.Object.DestroyImmediate(texture); }
        }
        static string PixelHash(Color32[] pixels)
        {
            ulong value = 14695981039346656037UL;
            unchecked { foreach (var pixel in pixels) { value = (value ^ pixel.r) * 1099511628211UL; value = (value ^ pixel.g) * 1099511628211UL; value = (value ^ pixel.b) * 1099511628211UL; } }
            return value.ToString("x16", CultureInfo.InvariantCulture);
        }
        static Color32[] RenderWithout(GardenPlantPhotoView view, bool removeEnvironment, bool removePlant, bool removeMemos = false)
        {
            var renderers = StudioRoot().GetComponentsInChildren<MeshRenderer>(true).Where(renderer =>
                removePlant && renderer.GetComponentInParent<ProceduralPlantView>(true) != null
                || removeMemos && renderer.GetComponentInParent<GardenPlantMemoView>(true) != null
                || removeEnvironment && renderer.name == "Plant album natural environment").ToArray();
            Assert(!removeEnvironment || renderers.Any(renderer => renderer.name == "Plant album natural environment"), "Reference render locates the actual natural environment renderer.");
            Assert(!removePlant || renderers.Any(renderer => renderer.GetComponentInParent<ProceduralPlantView>(true) != null), "Reference render locates the plant even while the shared studio is inactive between renders.");
            var flags = renderers.Select(renderer => renderer.forceRenderingOff).ToArray();
            try { foreach (var renderer in renderers) renderer.forceRenderingOff = true; Assert(view.RenderNow(), "Render independent GPU reference."); return PhotoPixels(view); }
            finally { for (int i = 0; i < renderers.Length; i++) if (renderers[i] != null) renderers[i].forceRenderingOff = flags[i]; }
        }
        static int PixelDifference(Color32 first, Color32 second) => Math.Abs(first.r - second.r) + Math.Abs(first.g - second.g) + Math.Abs(first.b - second.b);
        static void UniformEnvironmentFade()
        {
            using (var f = new Fixture(390, 844))
            {
                var shader = Resources.Load<Shader>("GardenAtmosphere/AlbumMeadow");
                var material = new Material(shader); var mesh = new Mesh(); var node = new GameObject("Synthetic uniform alpha probe");
                var target = new RenderTexture(128, 128, 24); var reader = new Texture2D(128, 128, TextureFormat.RGB24, false);
                var camera = f.Camera; var oldTarget = camera.targetTexture; var oldPosition = camera.transform.position; var oldRotation = camera.transform.rotation;
                var oldClear = camera.clearFlags; var oldColor = camera.backgroundColor; int oldMask = camera.cullingMask;
                bool oldOrtho = camera.orthographic; float oldSize = camera.orthographicSize, oldAspect = camera.aspect; var active = RenderTexture.active;
                var canvases = Resources.FindObjectsOfTypeAll<Canvas>().Where(canvas => canvas.gameObject.scene.IsValid() && canvas.enabled).ToArray();
                try
                {
                    // Screen-space canvases render independently of ordinary mesh
                    // culling. Temporarily hide only this copied scene's UI for the probe.
                    foreach (var canvas in canvases) canvas.enabled = false;
                    Assert(material.HasProperty("_EnvironmentOpacity") && !material.HasProperty("_FadeCenter") && !material.HasProperty("_ViewForward"),
                        "The environment shader exposes only whole-environment opacity, with no front/back masking uniforms.");
                    var vertices = new List<Vector3>(); var colors = new List<Color>(); var triangles = new List<int>();
                    foreach (var center in new[] { new Vector2(-.9f, -.9f), new Vector2(.9f, -.9f), new Vector2(-.9f, .9f), new Vector2(.9f, .9f) })
                    {
                        int first = vertices.Count;
                        foreach (var offset in new[] { new Vector2(-.6f, -.6f), new Vector2(.6f, -.6f), new Vector2(.6f, .6f), new Vector2(-.6f, .6f) })
                        { vertices.Add(center + offset); colors.Add(new Color(.65f, .4f, .2f, first % 8 == 0 ? 0 : 1)); }
                        triangles.AddRange(new[] { first, first + 1, first + 2, first, first + 2, first + 3 });
                    }
                    mesh.SetVertices(vertices); mesh.SetColors(colors); mesh.SetTriangles(triangles, 0); mesh.RecalculateNormals();
                    const int probeLayer = 29;
                    Assert(!Resources.FindObjectsOfTypeAll<Renderer>().Any(renderer => renderer.gameObject.scene.IsValid() && renderer.gameObject.layer == probeLayer),
                        "Uniform alpha probe uses a verified unused layer, separate from the garden's layer 30.");
                    node.layer = probeLayer; node.AddComponent<MeshFilter>().sharedMesh = mesh; node.AddComponent<MeshRenderer>().sharedMaterial = material;
                    material.SetColor("_AmbientTint", Color.white); material.SetColor("_LightTint", Color.black); material.SetVector("_LightDirection", Vector3.up);
                    camera.targetTexture = target; camera.cullingMask = 1 << probeLayer; camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Color.black;
                    camera.orthographic = true; camera.orthographicSize = 2; camera.aspect = 1; camera.transform.SetPositionAndRotation(new Vector3(0, 0, -10), Quaternion.identity);
                    camera.ResetProjectionMatrix();
                    var samples = new List<Color32>();
                    foreach (float opacity in new[] { 0f, .5f, 1f })
                    {
                        material.SetFloat("_EnvironmentOpacity", opacity); material.SetFloat("_ZWrite", opacity == 1 ? 1 : 0); camera.Render();
                        RenderTexture.active = target; reader.ReadPixels(new Rect(0, 0, 128, 128), 0, 0); reader.Apply();
                        var pixels = reader.GetPixels32(); var sample = pixels[35 * 128 + 35];
                        string file = "environment-uniform-alpha-" + opacity.ToString("F1", CultureInfo.InvariantCulture) + ".png";
                        File.WriteAllBytes(Path.Combine(output, file), reader.EncodeToPNG()); report.captures.Add(file);
                        report.diagnostics.Add("Probe alpha=" + opacity + " samples=" + string.Join("/", new[] { pixels[35 * 128 + 35], pixels[35 * 128 + 93], pixels[93 * 128 + 35], pixels[93 * 128 + 93] }.Select(pixel => pixel.r + "," + pixel.g + "," + pixel.b)));
                        foreach (var point in new[] { new Vector2Int(93, 35), new Vector2Int(35, 93), new Vector2Int(93, 93) })
                            Assert(PixelDifference(sample, pixels[point.y * 128 + point.x]) <= 2, "Equal alpha reaches every quadrant despite vertex alpha classification: " + opacity);
                        samples.Add(sample);
                    }
                    Assert(samples[0].r <= 1 && samples[0].g <= 1 && samples[0].b <= 1, "Zero opacity contributes no visible environment pixels.");
                    Assert(samples[1].r > samples[0].r + 10 && samples[1].r < samples[2].r - 10,
                        "Half opacity produces an intermediate GPU blend, not a clipped or fully opaque field.");
                    report.diagnostics.Add("Uniform shader red-channel output alpha0/.5/1=" + string.Join("/", samples.Select(sample => sample.r.ToString())));
                }
                finally
                {
                    camera.targetTexture = oldTarget; camera.cullingMask = oldMask; camera.clearFlags = oldClear; camera.backgroundColor = oldColor;
                    camera.orthographic = oldOrtho; camera.orthographicSize = oldSize; camera.aspect = oldAspect; camera.transform.SetPositionAndRotation(oldPosition, oldRotation);
                    camera.ResetProjectionMatrix(); foreach (var canvas in canvases) if (canvas != null) canvas.enabled = true;
                    RenderTexture.active = active; target.Release(); UnityEngine.Object.DestroyImmediate(target); UnityEngine.Object.DestroyImmediate(reader);
                    UnityEngine.Object.DestroyImmediate(node); UnityEngine.Object.DestroyImmediate(mesh); UnityEngine.Object.DestroyImmediate(material);
                }
            }
        }
        static string MeshDigest(Mesh mesh)
        {
            using (var stream = new MemoryStream())
            using (var writer = new BinaryWriter(stream))
            {
                foreach (var point in mesh.vertices) { writer.Write(point.x); writer.Write(point.y); writer.Write(point.z); }
                foreach (var normal in mesh.normals) { writer.Write(normal.x); writer.Write(normal.y); writer.Write(normal.z); }
                foreach (var color in mesh.colors) { writer.Write(color.r); writer.Write(color.g); writer.Write(color.b); writer.Write(color.a); }
                foreach (var index in mesh.triangles) writer.Write(index);
                writer.Flush(); using (var hash = System.Security.Cryptography.SHA256.Create()) return Convert.ToBase64String(hash.ComputeHash(stream.ToArray()));
            }
        }
        static void SetPhotoPitch(Fixture f, GardenPlantPhotoView view, float pitch)
        {
            view.ResetView(); f.Render(); var rect = (RectTransform)view.transform;
            var from = f.ScreenCenter(rect);
            var to = RectTransformUtility.WorldToScreenPoint(f.Camera, rect.TransformPoint(rect.rect.center + Vector2.up * ((8 - pitch) / 180 * rect.rect.height)));
            f.Gesture(view.GetComponent<RawImage>(), from, to, false); f.Render();
            Assert(Mathf.Abs(view.Pitch - pitch) < .01f, "Environment test uses the actual photo gesture at pitch " + pitch);
        }
        static void NaturalEnvironments()
        {
            using (var f = new Fixture(390, 844))
            {
                var photo = f.Service.Snapshot.PlantPhotos[0]; string saved = Xml(f.Service); int writes = f.Store.Writes;
                f.Home.navigation.ShowCourse(); f.Ui.ShowPlantPhoto(photo.Id); f.Render();
                var view = Find<GardenPlantPhotoView>("PlantAlbum.DetailPreview");
                var randomBefore = JsonUtility.ToJson(UnityEngine.Random.state);
                var recipes = new List<PlantRecipe>(); var keys = new HashSet<string>();
                foreach (PlantAlbumEnvironmentKind kind in Enum.GetValues(typeof(PlantAlbumEnvironmentKind)))
                {
                    Mesh firstMesh = null, secondMesh = null;
                    try
                    {
                        firstMesh = PlantAlbumEnvironment.Build(kind, 701); secondMesh = PlantAlbumEnvironment.Build(kind, 701);
                        Assert(firstMesh.vertexCount > 1000 && firstMesh.triangles.Length / 3 < 20000 && firstMesh.subMeshCount == 1,
                            "Natural preset is a bounded single-mesh landscape: " + kind);
                        Assert(MeshDigest(firstMesh) == MeshDigest(secondMesh), "Independent environment builds reproduce vertices, normals, colors and indices exactly.");
                        Assert(firstMesh.vertices.All(point => point.sqrMagnitude < .01f || new Vector2(point.x, point.z).magnitude >= PlantAlbumEnvironment.ClearRadius
                            || point.y <= PlantAlbumEnvironment.Height(new Vector2(point.x, point.z)) + .005f), "The center keeps the plant's clear ground area free from tall decor.");
                    }
                    finally { if (firstMesh != null) UnityEngine.Object.DestroyImmediate(firstMesh); if (secondMesh != null) UnityEngine.Object.DestroyImmediate(secondMesh); }
                    var recipe = PlantCodec.Clone(photo.Recipe);
                    bool found = false;
                    for (int i = 0; i < 1000; i++)
                    {
                        recipe.InstanceId = "synthetic-album-environment-" + kind + "-" + i;
                        if (PlantAlbumEnvironment.Select(recipe.InstanceId, recipe.BaseSeed) == kind) { found = true; break; }
                    }
                    Assert(found, "Find a deterministic individual assigned to " + kind); recipes.Add(recipe);
                    string encoded = PlantCodec.Encode(recipe);
                    view.Initialize(recipe, photo.Scale, true); GardenVisualClock.SetOverrideMinutes(12 * 60); Assert(view.RenderNow(), "Render daytime nature.");
                    string key = view.EnvironmentKey; keys.Add(key);
                    Assert(key == "album-nature-v1:" + (int)kind && Mathf.Approximately(view.LastRenderedEnvironmentOpacity, 1), "Stable individual selects the intended fully visible environment.");
                    var day = PhotoPixels(view); f.Capture("nature-" + kind + "-day");
                    GardenVisualClock.SetOverrideMinutes(0); view.RenderNow();
                    Assert(view.EnvironmentKey == key && PixelHash(PhotoPixels(view)) != PixelHash(day) && view.LastRenderedDaylight < .1f, "Night changes lighting but preserves individual environment selection.");
                    f.Capture("nature-" + kind + "-night");
                    GardenVisualClock.SetOverrideMinutes(12 * 60);
                    view.Initialize(recipe, photo.Scale, true, photo.Memos);
                    foreach (float yaw in new[] { 0f, 90f, 180f, -90f })
                    {
                        view.ResetView(); view.Rotate(yaw + 18); view.SetZoom(.75f); view.RenderNow(); var withEnvironment = PhotoPixels(view);
                        var withoutEnvironment = RenderWithout(view, true, false); var noPlant = RenderWithout(view, false, true);
                        var noEnvironmentOrPlant = RenderWithout(view, true, true); int plantCount = 0, visibleCount = 0;
                        for (int i = 0; i < withEnvironment.Length; i++)
                            if (PixelDifference(withoutEnvironment[i], noEnvironmentOrPlant[i]) > 20)
                            { plantCount++; if (PixelDifference(withEnvironment[i], noPlant[i]) > 2) visibleCount++; }
                        Assert(plantCount >= 20 && visibleCount >= 20, "Opaque foreground scenery cannot hide the entire plant at yaw " + yaw + ", " + kind);
                        report.diagnostics.Add("Opaque " + kind + " yaw=" + yaw + " zoom=.75 plantVisible=" + visibleCount + "/" + plantCount);
                        view.RenderNow(); f.Capture("nature-" + kind + "-opaque-yaw-" + yaw);
                        foreach (var paper in photo.Memos)
                        {
                            var point = f.PhotoPoint(view, paper.Id);
                            if (!view.TryPickMemo(point, f.Camera, out var picked) || picked != paper.Id) continue;
                            var normal = PhotoPixels(view); var withoutPaper = RenderWithout(view, false, false, true);
                            var rect = (RectTransform)view.transform;
                            RectTransformUtility.ScreenPointToLocalPointInRectangle(rect, point, f.Camera, out var local);
                            var texture = (RenderTexture)view.GetComponent<RawImage>().texture;
                            int x = Mathf.Clamp(Mathf.RoundToInt((local.x - rect.rect.xMin) / rect.rect.width * (texture.width - 1)), 1, texture.width - 2);
                            int y = Mathf.Clamp(Mathf.RoundToInt((local.y - rect.rect.yMin) / rect.rect.height * (texture.height - 1)), 1, texture.height - 2);
                            int contribution = 0;
                            for (int dy = -1; dy <= 1; dy++) for (int dx = -1; dx <= 1; dx++) contribution += PixelDifference(normal[(y + dy) * texture.width + x + dx], withoutPaper[(y + dy) * texture.width + x + dx]);
                            report.diagnostics.Add("Paper " + kind + " yaw=" + yaw + " id=" + paper.Id + " visibleContribution=" + contribution);
                            Assert(contribution > 9, "A selectable paper must contribute visible pixels instead of being hidden behind opaque scenery: " + kind + "/" + yaw);
                        }
                    }
                    view.Initialize(recipe, photo.Scale, true); view.RenderNow();
                    float previous = 1.01f;
                    foreach (float pitch in new[] { 85f, 8f, 4f, .01f, 0f, -.01f, -5f, -15f, -29.99f, -30f, -30.01f, -85f })
                    {
                        float opacity = GardenPlantPhotoView.EnvironmentOpacity(pitch);
                        Assert(opacity >= 0 && opacity <= previous, "Environment fade is bounded and monotonic while lowering pitch."); previous = opacity;
                        if (pitch >= 0) Assert(Mathf.Approximately(opacity, 1), "Horizontal and higher camera angles keep the whole environment opaque.");
                    }
                    Assert(Mathf.Approximately(GardenPlantPhotoView.EnvironmentOpacity(-15), .5f)
                        && Mathf.Abs(GardenPlantPhotoView.EnvironmentOpacity(-5) - 25f / 27f) < .00001f,
                        "The 90-to-60-degree transition uses smooth interpolation below the horizon.");
                    Assert(Mathf.Abs(GardenPlantPhotoView.EnvironmentOpacity(.01f) - GardenPlantPhotoView.EnvironmentOpacity(-.01f)) < .000001f
                        && Mathf.Abs(GardenPlantPhotoView.EnvironmentOpacity(-29.99f) - GardenPlantPhotoView.EnvironmentOpacity(-30.01f)) < .000001f,
                        "The horizon and fully faded boundary are continuous over 0.01 degrees.");
                    foreach (float pitch in new[] { 8f, 0f, -5f, -15f, -30f, -85f })
                    {
                        SetPhotoPitch(f, view, pitch); var actual = PhotoPixels(view);
                        float opacity = view.LastRenderedEnvironmentOpacity;
                        Assert(Mathf.Approximately(opacity, GardenPlantPhotoView.EnvironmentOpacity(view.Pitch)),
                            "Rendered material follows the actual gesture angle (including coordinate rounding): requested=" + pitch + ", actual=" + view.Pitch + ", opacity=" + opacity);
                        var reference = RenderWithout(view, true, false); var empty = RenderWithout(view, true, true);
                        int plantPixels = 0, matchingPlantPixels = 0; long error = 0;
                        for (int i = 0; i < actual.Length; i++)
                        {
                            int difference = PixelDifference(actual[i], reference[i]); error += difference;
                            if (PixelDifference(reference[i], empty[i]) > 20) { plantPixels++; if (difference <= 3) matchingPlantPixels++; }
                        }
                        Assert(plantPixels >= 20, "The independent no-environment GPU reference contains actual plant pixels at pitch " + pitch + "; pixels=" + plantPixels);
                        double meanError = error / (actual.Length * 3.0 * 255);
                        if (pitch <= -30)
                            Assert(meanError < .001 && matchingPlantPixels >= plantPixels * .99,
                                "Fully faded GPU output preserves the plant just like the environment-disabled reference: " + kind + "/" + pitch + ", error=" + meanError);
                        else
                        {
                            Assert(meanError > .002, "Horizontal and partially faded views retain actual environment pixels.");
                            if (pitch < 0)
                            {
                                var environmentOnly = RenderWithout(view, false, true); int visiblePlantPixels = 0;
                                for (int i = 0; i < actual.Length; i++)
                                    if (PixelDifference(reference[i], empty[i]) > 20 && PixelDifference(actual[i], environmentOnly[i]) > 2) visiblePlantPixels++;
                                Assert(opacity > 0 && opacity < 1 && visiblePlantPixels >= 20,
                                    "Partial fade keeps both environment and a measurable plant contribution: " + kind + "/" + pitch);
                                report.diagnostics.Add("Partial " + kind + " pitch=" + pitch + " visiblePlantPixels=" + visiblePlantPixels);
                            }
                        }
                        report.diagnostics.Add("Nature " + kind + " pitch=" + pitch + " opacity=" + opacity + " plantPixels=" + plantPixels
                            + " matching=" + matchingPlantPixels + " referenceMeanError=" + meanError.ToString("F6", CultureInfo.InvariantCulture));
                        view.RenderNow();
                        if (pitch == 0 || pitch == -5 || pitch == -15 || pitch == -30 || pitch == -85) f.Capture("nature-" + kind + "-pitch-" + pitch);
                    }
                    Assert(PlantCodec.Encode(recipe) == encoded, "Environment rendering never modifies the recipe.");
                }
                Assert(keys.Count == 4 && GardenPlantPhotoView.OwnedEnvironmentMeshCount <= 4 && GardenPlantPhotoView.SharedCameraCount == 1,
                    "Four distinct presets share bounded mesh caches and one camera.");
                int builds = GardenPlantPhotoView.EnvironmentBuildCount;
                foreach (var recipe in recipes)
                {
                    view.Initialize(recipe, photo.Scale, true); view.RenderNow(); string key = view.EnvironmentKey; string first = PixelHash(PhotoPixels(view));
                    view.Initialize(recipes[(recipes.IndexOf(recipe) + 1) % recipes.Count], photo.Scale, false); view.RenderNow();
                    view.Initialize(PlantCodec.Decode<PlantRecipe>(PlantCodec.Encode(recipe)), photo.Scale, true); view.RenderNow();
                    Assert(view.EnvironmentKey == key && PixelHash(PhotoPixels(view)) == first, "Another thumbnail's studio render cannot change this individual's restored environment or pixels.");
                }
                Assert(GardenPlantPhotoView.EnvironmentBuildCount == builds, "Reusing already-built environments does not regenerate meshes.");
                var reopen = recipes[0]; view.Initialize(reopen, photo.Scale, true); view.RenderNow(); string reopenedPixels = PixelHash(PhotoPixels(view));
                f.Ui.ShowPlantAlbum(); f.Render(); f.Ui.ShowPlantPhoto(photo.Id); f.Render(); view = Find<GardenPlantPhotoView>("PlantAlbum.DetailPreview");
                view.Initialize(PlantCodec.Decode<PlantRecipe>(PlantCodec.Encode(reopen)), photo.Scale, true); view.RenderNow();
                Assert(PixelHash(PhotoPixels(view)) == reopenedPixels, "Destroying and reopening the detail reconstructs the exact same assigned environment.");
                Assert(Xml(f.Service) == saved && f.Store.Writes == writes && JsonUtility.ToJson(UnityEngine.Random.state) == randomBefore,
                    "Environment selection/rendering/reopen leaves saves, recipes, rewards and global RNG untouched.");
            }
            GardenVisualClock.SetOverrideMinutes(12 * 60);
        }
        static void Geometry()
        {
            var node = new GameObject("Synthetic memo hit geometry"); var view = node.AddComponent<GardenPlantMemoView>();
            try
            {
                node.transform.position = new Vector3(3, 2, -5); node.transform.rotation = Quaternion.Euler(7, 37, 11); node.transform.localScale = new Vector3(1.2f, .85f, 1.6f);
                var memos = Enumerable.Range(0, 513).Select(i => new GardenPlantMemo { Id = "synthetic-pick-" + i, PlantInstanceId = "synthetic-owner", Mood = "편안함", Note = "합성" }).ToArray();
                view.Render(memos, _ => 0, 30);
                foreach (int index in new[] { 0, 1, 255, 511, 512 })
                {
                    Vector3 center = node.transform.TransformPoint(view.GetMemoBounds(index).center);
                    Vector3 forward = node.transform.rotation * Quaternion.Euler(0, -22, 0) * Vector3.forward;
                    Assert(view.TryPickMemo(new Ray(center - forward * 2, forward), out var id, out float distance)
                        && id == memos[index].Id && distance > 1 && distance < 2.1f, "Transformed ray selects exact memo across batch boundaries: " + index);
                }
                Assert(!view.TryPickMemo(new Ray(node.transform.position + Vector3.up * 10, Vector3.up), out _, out _), "Ray pointing away from all paper cannot hit.");
                view.Render(null, _ => 0, 30); Assert(!view.TryPickMemo(new Ray(Vector3.zero, Vector3.forward), out _, out _), "Cleared geometry cannot retain a stale pick target.");
            }
            finally { view.DisposeOwnedResources(); UnityEngine.Object.DestroyImmediate(node); }
        }
        static void PopupLifecycle()
        {
            using (var f = new Fixture(390, 844))
            {
                var state = f.Service.Snapshot; var memo = state.PlantAppearance.Memos[0]; string before = Xml(f.Service); int writes = f.Store.Writes;
                Func<bool> open = () => f.Ui.OpenPlantMemo(memo.PlantInstanceId, memo.Id, null, f.Preview.transform);
                Assert(open(), "Open popup for lifecycle checks."); f.Layout();
                var card = Find<RectTransform>("GardenMemoPopup.Card"); var hit = f.TopHit(f.ScreenCenter(card));
                Assert(hit.gameObject.transform.IsChildOf(Find<Transform>("GardenMemoPopup")), "Popup intercepts pointer input in front of the garden.");
                var pointer = f.Pointer(f.ScreenCenter(card), -1); ExecuteEvents.ExecuteHierarchy(hit.gameObject, pointer, ExecuteEvents.pointerClickHandler);
                Assert(GardenMemoPopup.IsOpen, "Clicking popup body cannot accidentally close the backdrop.");
                Assert(f.Home.navigation.TryNavigateBack() && !GardenMemoPopup.IsOpen, "Navigation back closes only the overlay.");
                Assert(f.Home.IsGardenVisible && !f.Home.navigation.activitiesPanel.activeSelf && !f.Home.navigation.settingsPanel.activeSelf
                    && Xml(f.Service) == before, "Back retains the integrated garden and unchanged data.");
                Assert(open(), "Reopen popup before backdrop dismissal."); f.Layout(); f.ClickAt("GardenMemoPopup.Backdrop", new Vector2(8, 8));
                Assert(!GardenMemoPopup.IsOpen, "Backdrop click dismisses the overlay.");
                Assert(open(), "Reopen popup before changing the source page."); f.Home.navigation.ShowCourse(); f.Ui.ShowPlantAlbum(); f.Layout();
                Assert(!GardenMemoPopup.IsOpen, "Page navigation dismisses the old popup.");
                var photo = state.PlantPhotos[0]; f.Ui.ShowPlantPhoto(photo.Id); f.Render(); var view = Find<GardenPlantPhotoView>("PlantAlbum.DetailPreview");
                Assert(!f.Ui.OpenPlantMemo(memo.PlantInstanceId, memo.Id, null, f.Preview.transform), "Inactive home owner cannot create a detached popup.");
                Assert(f.Ui.OpenPlantMemo(photo.SourcePlantInstanceId, photo.Memos[0].Id, photo.Id, view.transform), "Open archived popup before page back.");
                string page = Screen(f.Ui); Assert(f.Ui.TryNavigateBack() && !GardenMemoPopup.IsOpen && Screen(f.Ui) == page, "Activity back dismisses popup without popping photo history.");
                Assert(Xml(f.Service) == before && f.Store.Writes == writes, "Overlay lifecycle and page reads make no writes.");
                Assert(f.Ui.OpenPlantMemo(photo.SourcePlantInstanceId, photo.Memos[0].Id, photo.Id, view.transform), "Open archived popup before full reset.");
                f.Ui.FullResetCleanupOverride = _ => new PlatformActionResult { status = "AVAILABLE" };
                Assert(f.Ui.ResetAllData("synthetic-popup-reset", f.Service.Snapshot.InstallationId) && !GardenMemoPopup.IsOpen, "Full reset removes the popup and every retained memo reference.");
            }
        }

        sealed class Fixture : IDisposable
        {
            public readonly GardenHomePresenter Home; public readonly WeekOneQuestDemo Ui; public readonly GardenStateService Service;
            public readonly Store Store; public readonly Camera Camera; public readonly PrototypeGardenPreviewLayout Preview;
            readonly int width, height; readonly RenderTexture target; readonly EventSystem events;
            public Fixture(int width, int height, Store seeded = null)
            {
                this.width = width; this.height = height;
                EditorSceneManager.OpenScene("Assets/Scenes/MockupMain.unity", OpenSceneMode.Single); AndroidPlatformBridge.UseMockProvider();
                Store = seeded ?? Seed(); Service = new GardenStateService(Store, new Clock(), new DemoBalanceConfig { VisitorChancePercent = 0 }); Assert(Service.Open(), Service.LastError);
                Home = UnityEngine.Object.FindFirstObjectByType<GardenHomePresenter>(FindObjectsInactive.Include); Ui = Home.activity;
                Ui.Initialize(Service, true); Home.navigation.ShowIsland(); Home.RefreshFromState(); Home.ResetView();
                Camera = GameObject.Find("MockupCamera").GetComponent<Camera>();
                // UI normally overlays the garden's already-rendered texture.
                // Capturing it through a camera must not apply scene post effects twice.
                Camera.GetUniversalAdditionalCameraData().renderPostProcessing = false;
                foreach (var canvas in Resources.FindObjectsOfTypeAll<Canvas>().Where(item => item.gameObject.scene.IsValid() && item.isRootCanvas))
                { canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = Camera; canvas.planeDistance = canvas == Home.homeCanvas ? .75f : 1; }
                target = new RenderTexture(width, height, 24); target.Create(); Camera.targetTexture = target; Camera.aspect = width / (float)height;
                Preview = Home.homeCanvas.GetComponentInChildren<PrototypeGardenPreviewLayout>(true);
                events = UnityEngine.Object.FindFirstObjectByType<EventSystem>(FindObjectsInactive.Include);
                if (events == null) events = new GameObject("Synthetic memo pointer", typeof(EventSystem)).GetComponent<EventSystem>();
                events.gameObject.SetActive(true);
                if (EventSystem.current == null) typeof(EventSystem).GetMethod("OnEnable", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(events, null);
                EventSystem.current = events; Render();
            }
            public void Layout()
            {
                for (int pass = 0; pass < 3; pass++)
                {
                    // Scene-loaded non-ExecuteAlways raycasters do not receive their
                    // runtime OnEnable in EditMode. Register the existing components
                    // through that same lifecycle method; keep real RaycastAll sorting.
                    foreach (var caster in Resources.FindObjectsOfTypeAll<GraphicRaycaster>().Where(item => item.isActiveAndEnabled))
                        if (!RaycasterManager.GetRaycasters().Contains(caster))
                            typeof(BaseRaycaster).GetMethod("OnEnable", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(caster, null);
                    foreach (var text in Resources.FindObjectsOfTypeAll<TMP_Text>().Where(item => item.gameObject.activeInHierarchy)) text.ForceMeshUpdate(false, true);
                    Canvas.ForceUpdateCanvases();
                    foreach (var fit in Resources.FindObjectsOfTypeAll<FigmaMissionCanvasFit>().Where(item => item.isActiveAndEnabled)) fit.Fit();
                    foreach (var page in Resources.FindObjectsOfTypeAll<FocusMobilePageLayout>().Where(item => item.isActiveAndEnabled)) page.Refresh();
                    foreach (var detail in Resources.FindObjectsOfTypeAll<GardenPlantAlbumDetailLayout>().Where(item => item.isActiveAndEnabled)) detail.Refresh();
                    foreach (var popup in Resources.FindObjectsOfTypeAll<GardenMemoPopup>().Where(item => item.isActiveAndEnabled)) popup.Layout();
                    Canvas.ForceUpdateCanvases();
                }
                Camera.Render();
            }
            public void Render()
            {
                Layout(); Preview.Apply(); Home.gardenCamera.Render();
                foreach (var photo in Resources.FindObjectsOfTypeAll<GardenPlantPhotoView>().Where(item => item.isActiveAndEnabled)) photo.RenderNow();
                Layout();
            }
            public Vector2 ScreenCenter(RectTransform rect) => RectTransformUtility.WorldToScreenPoint(Camera, rect.TransformPoint(rect.rect.center));
            public Rect ScreenBounds(RectTransform rect)
            {
                var corners = new Vector3[4]; rect.GetWorldCorners(corners); Vector2 a = RectTransformUtility.WorldToScreenPoint(Camera, corners[0]), b = RectTransformUtility.WorldToScreenPoint(Camera, corners[2]);
                return Rect.MinMaxRect(a.x, a.y, b.x, b.y);
            }
            public Vector2 MemoPoint(GardenPlantMemoView memos, Camera renderCamera, RawImage image, string id)
            {
                int index = memos.MemoIds.ToList().IndexOf(id); Assert(index >= 0, "Projected memo belongs to the displayed geometry.");
                Vector3 viewport = renderCamera.WorldToViewportPoint(memos.transform.TransformPoint(memos.GetMemoBounds(index).center));
                Assert(viewport.z > 0, "Memo projection lies in front of the render camera.");
                var rect = (RectTransform)image.transform; var uv = image.uvRect;
                Vector2 local = rect.rect.min + Vector2.Scale(new Vector2((viewport.x - uv.x) / uv.width, (viewport.y - uv.y) / uv.height), rect.rect.size);
                return RectTransformUtility.WorldToScreenPoint(Camera, rect.TransformPoint(local));
            }
            public Vector2 PhotoPoint(GardenPlantPhotoView view, string id)
            {
                Assert(view.RenderNow(), "Render the requested photo before independently projecting its geometry.");
                var studio = typeof(GardenPlantPhotoView).GetField("studio", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
                var camera = (Camera)studio.GetType().GetField("camera", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(studio);
                var memos = (GardenPlantMemoView)studio.GetType().GetField("memos", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(studio);
                return MemoPoint(memos, camera, view.GetComponent<RawImage>(), id);
            }
            public Vector2 VisibleHomeMemoPoint(string id)
            {
                var view = Home.MemoView; int index = view.MemoIds.ToList().IndexOf(id); var bounds = view.GetMemoBounds(index);
                var image = Preview.GetComponent<RawImage>(); var rect = (RectTransform)image.transform; var uv = image.uvRect;
                var observations = new List<string>();
                foreach (float y in new[] { 0f, .28f, -.28f }) foreach (float x in new[] { 0f, .28f, -.28f })
                {
                    var local = bounds.center + Vector3.Scale(bounds.size, new Vector3(x, y, 0));
                    var viewport = Home.gardenCamera.WorldToViewportPoint(view.transform.TransformPoint(local));
                    var point = RectTransformUtility.WorldToScreenPoint(Camera, rect.TransformPoint(rect.rect.min
                        + Vector2.Scale(rect.rect.size, new Vector2((viewport.x - uv.x) / uv.width, (viewport.y - uv.y) / uv.height))));
                    var ray = Home.gardenCamera.ViewportPointToRay(viewport);
                    bool raw = view.TryPickMemo(ray, out var rawId, out float distance);
                    bool blocked = raw && GardenPlantMemoView.IsOccluded(ray, distance, Home.gardenRoot, view);
                    bool actual = Preview.TryPickMemo(point, Camera, out var selected);
                    observations.Add("raw=" + rawId + ",blocked=" + blocked + ",selected=" + selected);
                    if (x == 0 && y == 0 && raw && blocked)
                    {
                        Assert(!actual, "Opaque scenery correctly prevents selecting the hidden center of a paper.");
                        string detail = "Home " + width + "x" + height + ": paper center is hidden by "
                            + string.Join(", ", Home.gardenRoot.GetComponentsInChildren<MeshFilter>()
                                .Where(filter => filter.GetComponentInParent<GardenPlantMemoView>() != view
                                    && GardenPlantMemoView.IsOccluded(ray, distance, filter.transform, view))
                                .Select(filter => filter.name).Distinct());
                        if (!report.diagnostics.Contains(detail)) report.diagnostics.Add(detail);
                    }
                    if (actual && selected == id) { Assert(raw && rawId == id && !blocked, "Visible home tap ray agrees with the independent paper geometry."); return point; }
                }
                throw new InvalidOperationException("No visible point of the requested memo is pickable; homeVisible=" + Home.IsGardenVisible
                    + ",presenterActive=" + Home.isActiveAndEnabled + ",allowed=" + Preview.MemoPickingAllowed?.Invoke()
                    + ",surface=" + Preview.isActiveAndEnabled + "; " + string.Join(" | ", observations));
            }
            public RaycastResult TopHit(Vector2 point)
            {
                var data = new PointerEventData(events) { position = point }; var hits = new List<RaycastResult>(); events.RaycastAll(data, hits); report.raycasts++;
                Assert(hits.Count > 0, "Actual canvas raycast reaches a visible target at " + point + "; registered="
                    + RaycasterManager.GetRaycasters().Count + "; surfaces=" + string.Join(" | ", Resources.FindObjectsOfTypeAll<GraphicRaycaster>()
                    .Where(item => item.isActiveAndEnabled).Select(item => item.name + ":camera=" + item.eventCamera?.name + ":graphics="
                        + GraphicRegistry.GetGraphicsForCanvas(item.GetComponent<Canvas>()).Count))); return hits[0];
            }
            public PointerEventData Pointer(Vector2 point, int id)
            {
                var hit = TopHit(point);
                return new PointerEventData(events) { pointerId = id, position = point, pressPosition = point, button = PointerEventData.InputButton.Left,
                    pointerPressRaycast = hit, pointerCurrentRaycast = hit, eligibleForClick = true, useDragThreshold = true };
            }
            public void Tap(RawImage image, Vector2 point, int id)
            {
                Layout(); var data = Pointer(point, id);
                Assert(data.pointerPressRaycast.gameObject == image.gameObject || data.pointerPressRaycast.gameObject.transform.IsChildOf(image.transform), "The intended RawImage is the foremost real UI hit.");
                ExecuteEvents.Execute(image.gameObject, data, ExecuteEvents.pointerDownHandler); ExecuteEvents.Execute(image.gameObject, data, ExecuteEvents.pointerUpHandler);
                ExecuteEvents.Execute(image.gameObject, data, ExecuteEvents.pointerClickHandler); report.clicks++; Layout();
            }
            public void DirectTap(RawImage image, Vector2 point, int id)
            {
                var data = new PointerEventData(events) { pointerId = id, position = point, pressPosition = point, button = PointerEventData.InputButton.Left,
                    pointerPressRaycast = new RaycastResult { gameObject = image.gameObject, module = image.GetComponentInParent<GraphicRaycaster>() } };
                ExecuteEvents.Execute(image.gameObject, data, ExecuteEvents.pointerDownHandler); ExecuteEvents.Execute(image.gameObject, data, ExecuteEvents.pointerUpHandler); Layout();
            }
            public void Gesture(RawImage image, Vector2 from, Vector2 to, bool returnToStart)
            {
                var data = Pointer(from, 0); ExecuteEvents.Execute(image.gameObject, data, ExecuteEvents.pointerDownHandler);
                data.dragging = true; data.eligibleForClick = false; ExecuteEvents.Execute(image.gameObject, data, ExecuteEvents.beginDragHandler);
                data.delta = to - from; data.position = to; ExecuteEvents.Execute(image.gameObject, data, ExecuteEvents.dragHandler);
                if (returnToStart) { data.delta = from - to; data.position = from; ExecuteEvents.Execute(image.gameObject, data, ExecuteEvents.dragHandler); }
                ExecuteEvents.Execute(image.gameObject, data, ExecuteEvents.pointerUpHandler); ExecuteEvents.Execute(image.gameObject, data, ExecuteEvents.endDragHandler); Layout();
            }
            public void Pinch(RawImage image, Vector2 center)
            {
                var a = Pointer(center - Vector2.right * 8, 1); var b = Pointer(center + Vector2.right * 8, 2);
                ExecuteEvents.Execute(image.gameObject, a, ExecuteEvents.pointerDownHandler); ExecuteEvents.Execute(image.gameObject, b, ExecuteEvents.pointerDownHandler);
                ExecuteEvents.Execute(image.gameObject, a, ExecuteEvents.beginDragHandler); ExecuteEvents.Execute(image.gameObject, b, ExecuteEvents.beginDragHandler);
                b.position += Vector2.right * 20; b.delta = Vector2.right * 20; b.dragging = true; ExecuteEvents.Execute(image.gameObject, b, ExecuteEvents.dragHandler);
                ExecuteEvents.Execute(image.gameObject, b, ExecuteEvents.pointerUpHandler); ExecuteEvents.Execute(image.gameObject, b, ExecuteEvents.endDragHandler);
                ExecuteEvents.Execute(image.gameObject, a, ExecuteEvents.pointerUpHandler); ExecuteEvents.Execute(image.gameObject, a, ExecuteEvents.endDragHandler); Layout();
            }
            public void CancelTap(RawImage image, Vector2 point, string message, bool value)
            {
                var data = Pointer(point, 0); ExecuteEvents.Execute(image.gameObject, data, ExecuteEvents.pointerDownHandler);
                Lifecycle(image, message, value);
                ExecuteEvents.Execute(image.gameObject, data, ExecuteEvents.pointerUpHandler); Layout();
                Lifecycle(image, message, !value);
            }
            static void Lifecycle(RawImage image, string message, bool value)
            {
                // Unity SendMessage rejects non-ExecuteAlways components in an
                // EditMode fixture. Invoke the same existing lifecycle handlers
                // directly without changing the process focus or Play mode.
                int invoked = 0;
                foreach (var behaviour in image.GetComponents<MonoBehaviour>())
                {
                    var method = behaviour.GetType().GetMethod(message, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                    if (method == null) continue;
                    method.Invoke(behaviour, new object[] { value }); invoked++;
                }
                Assert(invoked > 0, "The actual surface lifecycle handler exists: " + message);
            }
            public void Click(string name)
            {
                var button = Find<Button>(name); Assert(button != null && button.IsInteractable(), "Visible popup button is available: " + name);
                ClickAt(name, ScreenCenter((RectTransform)button.transform));
            }
            public void ClickAt(string name, Vector2 point)
            {
                var button = Find<Button>(name); Assert(button != null && button.IsInteractable(), "Visible button is available: " + name); var data = Pointer(point, -1);
                Assert(data.pointerPressRaycast.gameObject == button.gameObject || data.pointerPressRaycast.gameObject.transform.IsChildOf(button.transform), "Button is the actual top raycast target: " + name);
                ExecuteEvents.Execute(button.gameObject, data, ExecuteEvents.pointerDownHandler); ExecuteEvents.Execute(button.gameObject, data, ExecuteEvents.pointerUpHandler);
                ExecuteEvents.Execute(button.gameObject, data, ExecuteEvents.pointerClickHandler); report.clicks++; Layout();
            }
            public void AssertPopup(GardenPlantMemo memo)
            {
                Layout(); Assert(GardenMemoPopup.IsOpen, "A memo popup is open.");
                var mood = Find<Text>("GardenMemoPopup.Mood"); var note = Find<Text>("GardenMemoPopup.Note"); var date = Find<Text>("GardenMemoPopup.Date");
                Assert(mood != null && note != null && date != null, "Popup exposes date, emotion and note fields.");
                Assert(string.IsNullOrEmpty(memo.Mood) || mood.text.Contains(memo.Mood), "Popup displays this memo's frozen emotion.");
                Assert(string.IsNullOrEmpty(memo.Note) || note.text == memo.Note, "Popup displays this memo's exact frozen note, including markup as literal text.");
                Assert(!note.supportRichText && !mood.supportRichText, "Saved user text cannot become rich-text markup.");
                var basic = Find<Text>("GardenMemoPopup.BasicMission"); var additional = Find<Text>("GardenMemoPopup.AdditionalMission");
                Assert(basic != null && additional != null && basic.text == PlantAlbumHistory.MissionLine(memo, true)
                    && additional.text == PlantAlbumHistory.MissionLine(memo, false), "Popup shows the frozen basic and additional mission snapshots.");
                Assert(!basic.supportRichText && !additional.supportRichText, "Stored mission titles are literal text.");
                var cycle = memo.DailyEmotionRecordId.Substring("daily-emotion:".Length);
                var expected = DateTime.ParseExact(cycle, "yyyy-MM-dd", CultureInfo.InvariantCulture);
                Assert(date.text.Contains(expected.ToString("yyyy.MM.dd", CultureInfo.InvariantCulture)), "Popup date comes from the saved business day rather than creation clock day: " + date.text);
                var card = Find<RectTransform>("GardenMemoPopup.Card"); var bounds = ScreenBounds(card);
                Assert(bounds.xMin >= -.5f && bounds.yMin >= -.5f && bounds.xMax <= width + .5f && bounds.yMax <= height + .5f, "Popup card fits the mobile screen.");
                Assert(Find<Transform>("GardenMemoPopup").GetSiblingIndex() == Find<Transform>("GardenMemoPopup").parent.childCount - 1, "Popup is the final child of its root canvas.");
            }
            public void Capture(string name)
            {
                Layout(); var previous = RenderTexture.active; var image = new Texture2D(width, height, TextureFormat.RGB24, false);
                try { RenderTexture.active = target; image.ReadPixels(new Rect(0, 0, width, height), 0, 0); image.Apply();
                    string file = name + "-" + width + "x" + height + ".png"; File.WriteAllBytes(Path.Combine(output, file), image.EncodeToPNG()); report.captures.Add(file); }
                finally { RenderTexture.active = previous; UnityEngine.Object.DestroyImmediate(image); }
            }
            public void Dispose()
            {
                GardenMemoPopup.TryCloseOpen(); Home.MemoView?.DisposeOwnedResources(); Camera.targetTexture = null; target.Release(); UnityEngine.Object.DestroyImmediate(target);
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single); Service.Dispose();
            }
        }
    }
}
