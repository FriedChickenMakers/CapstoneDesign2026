using System;
using System.Collections.Generic;
using System.IO;
using CapstoneDesign.Prototype;
using CapstoneDesign.Runtime;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;

namespace CapstoneDesign.EditorTools
{
    // Actual Play-mode camera passes, independent of game saves and enrollment.
    [InitializeOnLoad]
    public static class GardenRenderSchedulingValidation
    {
        const string Key = "Capstone.GardenRenderSchedulingValidation";
        static double deadline;
        static GardenRenderSchedulingDriver driver;
        static GardenRenderSchedulingValidation()
        {
            if (SessionState.GetBool(Key, false)) EditorApplication.update += WaitForPlay;
        }
        public static void Start()
        {
            if (!Application.isBatchMode || string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("CAPSTONE_ARTIFACTS")))
                throw new InvalidOperationException("Explicit batch mode and capture directory required.");
            SessionState.SetBool(Key, true);
            SessionState.SetFloat(Key + ".Deadline", (float)EditorApplication.timeSinceStartup + 90);
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            EditorApplication.update -= WaitForPlay;
            EditorApplication.update += WaitForPlay;
            EditorApplication.EnterPlaymode();
        }
        static void WaitForPlay()
        {
            if (EditorApplication.timeSinceStartup > SessionState.GetFloat(Key + ".Deadline", 0))
            { Finish(new GardenRenderSchedulingDriver.Report { error = "Play startup timed out." }); return; }
            if (!EditorApplication.isPlaying) return;
            EditorApplication.update -= WaitForPlay;
            Application.runInBackground = true;
            var owner = new GameObject("Synthetic garden render scheduling driver");
            owner.SetActive(false);
            driver = owner.AddComponent<GardenRenderSchedulingDriver>();
            driver.Finished = Finish;
            deadline = EditorApplication.timeSinceStartup + 30;
            EditorApplication.update += Watch;
            owner.SetActive(true);
        }
        static void Watch()
        {
            if (EditorApplication.timeSinceStartup > deadline)
                Finish(new GardenRenderSchedulingDriver.Report { error = "Runtime frames timed out." });
        }
        static void Finish(GardenRenderSchedulingDriver.Report report)
        {
            EditorApplication.update -= WaitForPlay;
            EditorApplication.update -= Watch;
            SessionState.SetBool(Key, false);
            if (driver != null) driver.enabled = false;
            report.status = string.IsNullOrEmpty(report.error) ? "pass" : "fail";
            string output = Path.Combine(Environment.GetEnvironmentVariable("CAPSTONE_ARTIFACTS"), "garden-render-scheduling");
            Directory.CreateDirectory(output);
            File.WriteAllText(Path.Combine(output, "report.json"), JsonUtility.ToJson(report, true));
            Debug.Log("GARDEN_RENDER_SCHEDULING_" + report.status.ToUpperInvariant() + " checks=" + report.checks.Count + " maxPassesPerFrame=" + report.maxPassesPerFrame);
            EditorApplication.Exit(report.status == "pass" ? 0 : 1);
        }
    }

    [DefaultExecutionOrder(3000)]
    public sealed class GardenRenderSchedulingDriver : MonoBehaviour
    {
        [Serializable] public sealed class FrameResult { public int phase, frame, passes; }
        [Serializable] public sealed class Report
        {
            public string status, error;
            public bool synthetic = true, actualPlayMode = true;
            public int maxPassesPerFrame, maximumRenderDepth;
            public List<string> checks = new List<string>();
            public List<FrameResult> frames = new List<FrameResult>();
        }
        public Action<Report> Finished;
        readonly Report report = new Report();
        readonly Dictionary<int, int> passes = new Dictionary<int, int>();
        Camera camera;
        GardenSkyEnvironment environment;
        PrototypeIslandDrag orbit;
        GameObject visual, surface;
        RenderTexture target;
        int phase = -1, renderDepth;
        Quaternion lastPresented, expected;
        bool completed;

        void Start()
        {
            try
            {
                visual = new GameObject("Synthetic render garden");
                var view = new GameObject("Synthetic garden camera");
                view.transform.SetParent(visual.transform, false);
                camera = view.AddComponent<Camera>(); camera.enabled = false;
                camera.orthographic = true; camera.orthographicSize = 3;
                target = new RenderTexture(256, 256, 24, RenderTextureFormat.ARGBHalf);
                camera.targetTexture = target;
                environment = GardenSkyEnvironment.Ensure(camera, visual.transform);
                surface = new GameObject("Synthetic orbit input", typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage));
                orbit = surface.AddComponent<PrototypeIslandDrag>(); orbit.previewCamera = camera;
                RenderPipelineManager.beginCameraRendering += Begin;
                RenderPipelineManager.endCameraRendering += End;
            }
            catch (Exception error) { Fail(error); }
        }
        void Begin(ScriptableRenderContext context, Camera view)
        {
            if (view != camera) return;
            passes.TryGetValue(Time.frameCount, out int count); passes[Time.frameCount] = count + 1;
            report.maximumRenderDepth = Math.Max(report.maximumRenderDepth, ++renderDepth);
            lastPresented = camera.transform.rotation;
        }
        void End(ScriptableRenderContext context, Camera view) { if (view == camera) renderDepth--; }
        int CurrentPasses() => passes.TryGetValue(Time.frameCount, out int count) ? count : 0;
        void Update()
        {
            if (completed || camera == null) return;
            try
            {
                phase++;
                if (phase == 1 || phase == 5)
                {
                    if (phase == 5) environment.enabled = true;
                    int before = CurrentPasses();
                    for (int i = 0; i < 32; i++) orbit.RestoreAngles(new Vector2(i - 16, 30));
                    expected = camera.transform.rotation;
                    Check(CurrentPasses() == before, "Orbit burst queues without synchronous camera passes, phase=" + phase);
                }
                if (phase == 2)
                {
                    camera.Render();
                    Check(CurrentPasses() == 1, "Explicit home refresh renders immediately once");
                    orbit.RestoreAngles(new Vector2(60, 35)); expected = camera.transform.rotation;
                }
                if (phase == 4)
                {
                    environment.enabled = false;
                    Check(!GardenSkyEnvironment.RequestViewRender(camera), "Disabled environment does not claim a standalone redraw");
                    orbit.ResetView(); expected = camera.transform.rotation;
                    Check(CurrentPasses() == 1, "Standalone orbit/reset retains its immediate fallback");
                }
                if (phase == 6)
                {
                    camera.gameObject.SetActive(false);
                    GardenSkyEnvironment.RequestViewRender(camera);
                }
                if (phase == 7) camera.gameObject.SetActive(true);
            }
            catch (Exception error) { Fail(error); }
        }
        void LateUpdate()
        {
            if (completed || phase < 0) return;
            try
            {
                int count = CurrentPasses();
                report.frames.Add(new FrameResult { phase = phase, frame = Time.frameCount, passes = count });
                report.maxPassesPerFrame = Math.Max(report.maxPassesPerFrame, count);
                Check(count <= 1 && renderDepth == 0, "No duplicate or recursive garden pass, phase=" + phase);
                if (phase == 1 || phase == 3 || phase == 4 || phase == 5)
                    Check(count == 1 && Quaternion.Angle(lastPresented, expected) < .01f, "Final requested orbit pose is presented, phase=" + phase);
                if (phase == 2) Check(count == 1, "Cloud/orbit request does not duplicate an earlier explicit pass");
                if (phase == 6) Check(count == 0, "Inactive camera stays idle with a pending redraw");
                if (phase == 7)
                {
                    Check(count == 1, "Pending redraw resumes when the camera becomes visible");
                    completed = true; Finished?.Invoke(report);
                }
            }
            catch (Exception error) { Fail(error); }
        }
        void Check(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
            report.checks.Add(message);
        }
        void Fail(Exception error)
        {
            if (completed) return;
            completed = true; report.error = error.ToString(); Finished?.Invoke(report);
        }
        void OnDestroy()
        {
            RenderPipelineManager.beginCameraRendering -= Begin;
            RenderPipelineManager.endCameraRendering -= End;
            if (surface != null) Destroy(surface);
            if (visual != null) Destroy(visual);
            if (target != null) { target.Release(); Destroy(target); }
        }
    }
}
