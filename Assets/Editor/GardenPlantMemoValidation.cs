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
using UnityEngine.Rendering;
using UnityEngine.UI;

namespace CapstoneDesign.EditorTools
{
    // Runs only in a new copied project with a different application identity.
    // No polling, user save, device, GameView or source-Editor interaction.
    public static class GardenPlantMemoValidation
    {
        [Serializable] sealed class Result { public string name, status, detail; }
        [Serializable] sealed class Report
        {
            public string utc, unityVersion, environment;
            public int passed, failed, assertions;
            public List<Result> checks = new List<Result>();
            public List<string> captures = new List<string>(), errors = new List<string>();
        }
        static Report report;
        static string output;
        public static void RunBatch()
        {
            var args = Environment.GetCommandLineArgs(); int index = Array.IndexOf(args, "-memoReport");
            if (!Application.isBatchMode || Application.companyName != "CapstoneSyntheticValidation"
                || !Application.productName.StartsWith("PlantMemos-", StringComparison.Ordinal) || index < 0)
                throw new InvalidOperationException("Memo validation requires its separate synthetic batch project.");
            string path = args[index + 1]; output = Path.GetDirectoryName(path);
            Directory.CreateDirectory(output);
            report = new Report { utc = DateTimeOffset.UtcNow.ToString("O"), unityVersion = Application.unityVersion,
                environment = "Hidden isolated GPU batch; copied production scene and cameras; synthetic memory service; detached visual stress states; no original Editor, user save or device access" };
            Application.logMessageReceived += Observe;
            GardenVisualClock.SetOverrideMinutes(12 * 60);
            try
            {
                Run("Paper geometry, stable IDs and 512+ batching without record loss", Geometry);
                Run("Actual garden and photo renders at 360x640", () => RenderFlow(360, 640));
                Run("Actual garden and photo renders at 390x844", () => RenderFlow(390, 844));
                Run("All memo and photo owned resources released", () => {
                    Assert(GardenPlantMemoView.OwnedMeshCount == 0 && GardenPlantMemoView.OwnedMaterialCount == 0
                        && GardenPlantMemoView.OwnedTextureCount == 0, "Memo graphics resources survived all owners.");
                    Assert(GardenPlantPhotoView.ActiveViewCount == 0 && GardenPlantPhotoView.SharedCameraCount == 0
                        && GardenPlantPhotoView.OwnedTextureCount == 0, "Photo graphics resources survived all views.");
                });
            }
            finally
            {
                GardenVisualClock.UseSystemTime(); Application.logMessageReceived -= Observe;
                if (report.errors.Count > 0) { report.failed++; report.checks.Add(new Result { name = "Unity runtime/shader errors", status = "fail", detail = string.Join("\n", report.errors) }); }
                File.WriteAllText(path, JsonUtility.ToJson(report, true));
            }
            Debug.Log("PLANT_MEMOS passed=" + report.passed + " failed=" + report.failed + " assertions=" + report.assertions);
            EditorApplication.Exit(report.failed == 0 ? 0 : 1);
        }
        static void Observe(string text, string stack, LogType type)
        { if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) report.errors.Add(type + ": " + text + "\n" + stack); }
        static void Assert(bool value, string message) { report.assertions++; if (!value) throw new InvalidOperationException(message); }
        static void Run(string name, Action action)
        { try { action(); report.passed++; report.checks.Add(new Result { name = name, status = "pass" }); }
          catch (Exception ex) { report.failed++; report.checks.Add(new Result { name = name, status = "fail", detail = ex.ToString() }); } }

        static List<GardenPlantMemo> Memos(int count, string plantId)
            => Enumerable.Range(0, count).Select(i => new GardenPlantMemo { Id = "synthetic-memo-" + i.ToString("D4"),
                DailyEmotionRecordId = "synthetic-emotion-" + i, PlantInstanceId = plantId,
                CreatedUtc = new DateTimeOffset(2026, 9, 1, 12, 0, 0, TimeSpan.Zero).AddDays(i).ToString("O"),
                Mood = "편안함", Note = "Synthetic visual memory " + i }).ToList();

        static void Geometry()
        {
            var node = new GameObject("Synthetic memo geometry");
            var view = node.AddComponent<GardenPlantMemoView>();
            try
            {
                foreach (int count in new[] { 0, 1, 7, 21, 60, 513 })
                {
                    var values = Memos(count, "synthetic-geometry-plant");
                    view.Render(values, _ => 0, 30);
                    Assert(view.MemoCount == count && view.MemoIds.SequenceEqual(values.Select(item => item.Id)), "A visual memo was dropped or reordered.");
                    Assert(view.BatchCount == (count + 511) / 512, "Batching imposed a count cap or extra renderer per memo.");
                    Assert(node.GetComponentsInChildren<MeshFilter>().Sum(filter => filter.sharedMesh.vertexCount) == count * 24, "A memo is not one full six-sided block.");
                    for (int i = 0; i < count; i++)
                    {
                        Assert(view.GetMemoIdForTriangle(i / 512, i % 512 * 12) == values[i].Id, "Triangle identity lost its source memo.");
                        for (int other = 0; other < i; other++)
                            Assert(!view.GetMemoBounds(i).Intersects(view.GetMemoBounds(other)), "Paper volumes overlap.");
                    }
                    int builds = view.BuildCount;
                    Assert(!view.Render(values, _ => 0, 30) && builds == view.BuildCount, "Unchanged memos rebuilt their mesh.");
                    if (count > 0) Assert(view.LocalBounds.size.x < .8f && view.LocalBounds.size.y < .9f, "Memo collection escaped the fixed garden envelope.");
                }
                view.Render(null, _ => 0, 30);
                Assert(view.BatchCount == 0 && GardenPlantMemoView.OwnedMeshCount == 0 && GardenPlantMemoView.OwnedTextureCount == 0, "Empty collection retains prior graphics resources.");
            }
            finally { view.DisposeOwnedResources(); UnityEngine.Object.DestroyImmediate(node); }
        }

        static void RenderFlow(int width, int height)
        {
            EditorSceneManager.OpenScene("Assets/Scenes/MockupMain.unity", OpenSceneMode.Single);
            AndroidPlatformBridge.UseMockProvider();
            var home = UnityEngine.Object.FindFirstObjectByType<GardenHomePresenter>(FindObjectsInactive.Include);
            using var service = new GardenStateService(new Store(), new Clock(), new DemoBalanceConfig { VisitorChancePercent = 0 });
            home.activity.Initialize(service, true); home.navigation.ShowIsland();
            var state = service.Snapshot;
            while (!state.PlantAppearance.Recipe.Mature)
                state.PlantAppearance.Recipe = PlantGenerator.Advance(state.PlantAppearance.Recipe);
            GardenPlantVisuals.Sync(state);
            state.Environments.Add(new EnvironmentPlacement { EnvironmentId = "environment:E01", Slot = "left" });
            state.Environments.Add(new EnvironmentPlacement { EnvironmentId = "environment:E02", Slot = "right" });
            state.DailyDecisions.Last().VisitorId = "animal:A07";
            string actualState = StateCodec.Encode(service.Snapshot);
            var sky = GardenSkyEnvironment.Ensure(home.gardenCamera, home.gardenVisuals.transform);
            sky.SetTimeForPreview(12);
            var photoNode = new GameObject("Synthetic saved memo photo", typeof(RectTransform), typeof(RawImage));
            ((RectTransform)photoNode.transform).sizeDelta = new Vector2(310, 300);
            var photo = photoNode.AddComponent<GardenPlantPhotoView>();
            try
            {
                using var capture = new HomeCapture(home, sky, width, height);
                home.RefreshFromState(state); home.ResetView();
                var plantMesh = home.ProceduralPlant.GetComponent<MeshFilter>().sharedMesh;
                var recipe = PlantCodec.Encode(home.RenderedRecipe);
                foreach (int count in new[] { 1, 7, 21, 60 })
                {
                    state.PlantAppearance.Memos = Memos(count, state.PlantAppearance.Recipe.InstanceId);
                    home.RefreshFromState(state);
                    Assert(home.ProceduralPlant.GetComponent<MeshFilter>().sharedMesh == plantMesh && PlantCodec.Encode(home.RenderedRecipe) == recipe,
                        "Adding memos regenerated or changed the plant.");
                    Assert(home.MemoView.MemoCount == count && home.MemoView.BatchCount == 1, "Home memo collection differs from the snapshot.");
                    capture.Render("garden-" + count + "-" + width + "x" + height);
                    var layout = home.homeCanvas.GetComponentInChildren<PrototypeGardenPreviewLayout>(true);
                    AssertProjected(home.MemoView, home.gardenCamera, layout.VisibleAreaInViewport);
                    photo.Initialize(state.PlantAppearance.Recipe, GardenPlantVisuals.ScaleForGrowth(GardenPlantVisuals.TotalGrowth(state)), true, state.PlantAppearance.Memos);
                    Assert(photo.RenderNow(), "Photo renderer declined a valid view.");
                    Assert(photo.MemoCount == count && photo.RenderedMemoIds.SequenceEqual(home.MemoView.MemoIds), "Saved-photo renderer lost source memo identities.");
                    Assert(GardenPlantPhotoView.SharedCameraCount == 1, "Photo memos created extra cameras.");
                    Capture(photoNode.GetComponent<RawImage>().texture as RenderTexture, "photo-" + count + "-" + width + "x" + height);
                    var studio = typeof(GardenPlantPhotoView).GetField("studio", BindingFlags.NonPublic | BindingFlags.Static).GetValue(null);
                    var studioCamera = (Camera)studio.GetType().GetField("camera", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(studio);
                    var studioMemos = (GardenPlantMemoView)studio.GetType().GetField("memos", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(studio);
                    AssertProjected(studioMemos, studioCamera, new Rect(0, 0, 1, 1));
                    if (count == 21)
                    {
                        photo.Rotate(180); Assert(photo.RenderNow(), "Back view failed.");
                        Capture(photoNode.GetComponent<RawImage>().texture as RenderTexture, "photo-21-back-" + width + "x" + height);
                        photo.ResetView(); GardenVisualClock.SetOverrideMinutes(23 * 60); Assert(photo.RenderNow(), "Night view failed.");
                        Capture(photoNode.GetComponent<RawImage>().texture as RenderTexture, "photo-21-night-" + width + "x" + height);
                        GardenVisualClock.SetOverrideMinutes(12 * 60);
                    }
                }
                state.PlantAppearance.IsArchived = true; home.RefreshFromState(state);
                Assert(home.MemoView.MemoCount == 0 && home.RenderedRecipe == null, "Archived garden retained memo geometry.");
                photo.Initialize(state.PlantAppearance.Recipe, .8f, true);
                Assert(photo.RenderNow() && photo.RenderedMemoIds.Count == 0, "Shared studio retained the previous photo's memos.");
                photoNode.SetActive(false);
                Assert(GardenPlantPhotoView.SharedCameraCount == 0 && GardenPlantPhotoView.OwnedTextureCount == 0, "Disabled photo view retained its shared studio.");
                Assert(StateCodec.Encode(service.Snapshot) == actualState, "Visual rendering changed the source memory state.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(photoNode);
                home.MemoView?.DisposeOwnedResources();
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            }
        }

        static void AssertProjected(GardenPlantMemoView view, Camera camera, Rect area)
        {
            for (int i = 0; i < view.MemoCount; i++)
            {
                var bounds = view.GetMemoBounds(i);
                for (int corner = 0; corner < 8; corner++)
                {
                    Vector3 point = bounds.center + Vector3.Scale(bounds.extents, new Vector3((corner & 1) == 0 ? -1 : 1,
                        (corner & 2) == 0 ? -1 : 1, (corner & 4) == 0 ? -1 : 1));
                    var projected = camera.WorldToViewportPoint(view.transform.TransformPoint(point));
                    Assert(projected.z > 0 && projected.x >= area.xMin && projected.x <= area.xMax
                        && projected.y >= area.yMin && projected.y <= area.yMax, "A memo is clipped by the default camera/UI composition.");
                }
            }
        }
        static void Capture(RenderTexture target, string name)
        {
            Assert(target != null && target.IsCreated(), "Capture target missing.");
            var old = RenderTexture.active; var image = new Texture2D(target.width, target.height, TextureFormat.RGBA32, false);
            try
            {
                RenderTexture.active = target; image.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0); image.Apply();
                var pixels = image.GetPixels32();
                string file = Path.Combine(output, name + ".png"); File.WriteAllBytes(file, image.EncodeToPNG()); report.captures.Add(file);
                Assert(pixels.Count(p => p.r > 230 && p.b > 230 && p.g < 40) < pixels.Length / 500, "Missing shader produced magenta pixels.");
            }
            finally { RenderTexture.active = old; UnityEngine.Object.DestroyImmediate(image); }
        }
        sealed class Store : IStateStore
        { string xml; public GardenState Load() => xml == null ? null : StateCodec.Decode(xml); public void Save(GardenState state) => xml = StateCodec.Encode(state); }
        sealed class Clock : IClock
        { public DateTimeOffset UtcNow => DateTimeOffset.Parse("2026-11-09T12:00:00Z"); public TimeZoneInfo TimeZone => TimeZoneInfo.Utc; }
        sealed class HomeCapture : IDisposable
        {
            readonly GardenHomePresenter home; readonly GardenSkyEnvironment sky; readonly Camera camera; readonly RenderTexture target;
            public HomeCapture(GardenHomePresenter home, GardenSkyEnvironment sky, int width, int height)
            {
                this.home = home; this.sky = sky;
                camera = GameObject.Find("MockupCamera").GetComponent<Camera>();
                var canvas = GameObject.Find("UiCanvas").GetComponent<Canvas>();
                canvas.renderMode = home.homeCanvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = home.homeCanvas.worldCamera = camera;
                canvas.planeDistance = 1; home.homeCanvas.planeDistance = .75f;
                target = new RenderTexture(width, height, 24); target.Create(); camera.targetTexture = target; camera.aspect = width / (float)height;
            }
            public void Render(string name)
            {
                for (int pass = 0; pass < 4; pass++)
                {
                    foreach (var label in home.homeCanvas.GetComponentsInChildren<TMP_Text>()) label.ForceMeshUpdate(false, true);
                    foreach (var graphic in home.homeCanvas.GetComponentsInChildren<Graphic>()) graphic.SetAllDirty();
                    Canvas.ForceUpdateCanvases();
                    foreach (var layout in home.homeCanvas.GetComponentsInChildren<PrototypeGardenPreviewLayout>(true)) layout.Apply();
                    sky.UpdateClouds(0); home.gardenCamera.Render(); Canvas.ForceUpdateCanvases(); camera.Render();
                }
                Capture(target, name);
            }
            public void Dispose() { camera.targetTexture = null; target.Release(); UnityEngine.Object.DestroyImmediate(target); }
        }
    }
}
