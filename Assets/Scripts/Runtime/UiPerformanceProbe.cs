using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using Unity.Profiling;
using Unity.Profiling.LowLevel.Unsafe;
using UnityEngine;

namespace CapstoneDesign.Runtime
{
    // Opt-in developer capture. Never stores sensor values, user text or device identifiers.
    [DefaultExecutionOrder(-10000)]
    public sealed class UiPerformanceProbe : MonoBehaviour
    {
        const int MaxFrames = 18000, MaxOperations = 4096;
        static UiPerformanceProbe active;
        static readonly string[] Names = { "Main Thread", "PlayerLoop", "BehaviourUpdate", "UI.Layout",
            "Canvas.SendWillRenderCanvases", "GC Allocated In Frame", "Gfx.WaitForPresentOnGfxThread" };
        static readonly string[] Scopes = { "other", "Home.RefreshFromState", "Settings.RefreshText",
            "Platform.GetSnapshot", "Navigation.SetActivePanel", "Activities.Page", "Activities.ShowCourse",
            "Activities.ShowFree", "Activities.ShowHome", "Activities.ShowSession", "State.Snapshot", "Activities.RefreshCycle" };
        static readonly string[] Routes = { "unknown", "garden", "settings", "home", "course", "free",
            "session", "reflection", "complete", "records", "record", "experience-type", "experience", "shop", "growth",
            "placement", "discoveries", "experience-history", "experience-record", "answers", "personal-plan", "plan-field" };
        [Serializable] public struct Frame
        {
            public int frame, route;
            public double elapsedMs;
            public float deltaMs;
            public long mainThread, playerLoop, behaviourUpdate, uiLayout, canvasWillRender, gcBytes, presentWait;
        }
        [Serializable] public struct Operation
        {
            public int frame, route, scope;
            public double elapsedMs, durationMs;
        }
        [Serializable] public struct Metric { public string name, unit, dataType; public bool available; }
        [Serializable] sealed class Report
        {
            public string schema = "ui-performance-v1", stopReason, unity, appVersion;
            public string notes = "Frame metrics are previous completed frame samples; -1 means unavailable/no new sample. Main Thread/PlayerLoop include waits, not pure CPU work. Scope timings include nested work. Profiler and probe overhead included. No user or sensor data.";
            public string[] routes = Routes, scopes = Scopes;
            public Metric[] metrics;
            public Frame[] frames;
            public Operation[] operations;
            public int droppedOperations, width, height;
        }
        readonly ProfilerRecorder[] recorders = new ProfilerRecorder[7];
        readonly int[] counts = new int[7];
        readonly Metric[] metrics = new Metric[7];
        readonly Frame[] frames = new Frame[MaxFrames];
        readonly Operation[] operations = new Operation[MaxOperations];
        MockupNavigation navigation;
        WeekOneQuestDemo loop;
        long started;
        int frameCount, operationCount, droppedOperations, previousRoute;
        bool finished;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void StartRequestedCapture()
        {
#if UNITY_ANDROID && DEVELOPMENT_BUILD && !UNITY_EDITOR
            try
            {
                using var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
                using var activity = player.GetStatic<AndroidJavaObject>("currentActivity");
                using var intent = activity.Call<AndroidJavaObject>("getIntent");
                if (!intent.Call<bool>("getBooleanExtra", "capstoneProfile", false)) return;
                var owner = new GameObject("UI performance probe");
                DontDestroyOnLoad(owner);
                owner.AddComponent<UiPerformanceProbe>();
            }
            catch (Exception error) { UnityEngine.Debug.LogWarning("UI_PROFILE_START_FAILED " + error.GetType().Name); }
#endif
        }
        void Awake()
        {
            navigation = FindFirstObjectByType<MockupNavigation>(FindObjectsInactive.Include);
            loop = navigation?.activitiesPanel?.GetComponent<WeekOneQuestDemo>();
            var handles = new List<ProfilerRecorderHandle>();
            ProfilerRecorderHandle.GetAvailable(handles);
            for (int i = 0; i < Names.Length; i++)
            {
                metrics[i] = new Metric { name = Names[i], unit = "unavailable", dataType = "unavailable" };
                foreach (var handle in handles)
                {
                    var description = ProfilerRecorderHandle.GetDescription(handle);
                    if (description.Name != Names[i]) continue;
                    try
                    {
                        recorders[i] = new ProfilerRecorder(handle, MaxFrames + 1,
                            ProfilerRecorderOptions.StartImmediately | ProfilerRecorderOptions.SumAllSamplesInFrame);
                        metrics[i] = new Metric { name = Names[i], available = recorders[i].Valid,
                            unit = description.UnitType.ToString(), dataType = description.DataType.ToString() };
                    }
                    catch (Exception) { /* Unsupported counters stay explicitly unavailable. */ }
                    break;
                }
            }
            started = Stopwatch.GetTimestamp();
            previousRoute = CurrentRoute();
            active = this;
        }
        void Update()
        {
            if (finished) return;
            double elapsed = Milliseconds(Stopwatch.GetTimestamp() - started);
            if (elapsed >= 120000 || frameCount == MaxFrames) { Finish("limit"); return; }
            frames[frameCount++] = new Frame { frame = Time.frameCount - 1, route = previousRoute,
                elapsedMs = elapsed, deltaMs = Time.unscaledDeltaTime * 1000,
                mainThread = Read(0), playerLoop = Read(1), behaviourUpdate = Read(2), uiLayout = Read(3),
                canvasWillRender = Read(4), gcBytes = Read(5), presentWait = Read(6) };
        }
        void LateUpdate() { if (!finished) previousRoute = CurrentRoute(); }
        long Read(int i)
        {
            if (!recorders[i].Valid || recorders[i].Count <= counts[i]) return -1;
            counts[i] = recorders[i].Count;
            return recorders[i].GetSample(counts[i] - 1).Value;
        }
        int CurrentRoute()
        {
            if (navigation == null) return 0;
            if (navigation.settingsPanel != null && navigation.settingsPanel.activeSelf) return 2;
            if (navigation.activitiesPanel != null && navigation.activitiesPanel.activeSelf)
                return Math.Max(0, Array.IndexOf(Routes, loop?.CurrentScreen));
            return 1;
        }
        public static Scope Measure(string name)
        {
            var owner = active;
            return owner == null || owner.finished ? default : new Scope(owner, name);
        }
        public readonly struct Scope : IDisposable
        {
            readonly UiPerformanceProbe owner;
            readonly long start;
            readonly int scope, route, frame;
            internal Scope(UiPerformanceProbe owner, string name)
            {
                this.owner = owner; start = Stopwatch.GetTimestamp();
                scope = Math.Max(0, Array.IndexOf(Scopes, name)); route = owner.CurrentRoute(); frame = Time.frameCount;
            }
            public void Dispose()
            {
                if (owner == null || owner.finished) return;
                owner.Append(new Operation { frame = frame, route = route, scope = scope,
                    elapsedMs = Milliseconds(start - owner.started), durationMs = Milliseconds(Stopwatch.GetTimestamp() - start) });
            }
        }
        public static void Mark(string phase)
        {
            var owner = active;
            if (owner == null || owner.finished) return;
            owner.Append(new Operation { frame = Time.frameCount, route = owner.CurrentRoute(),
                scope = Math.Max(0, Array.IndexOf(Scopes, phase)), elapsedMs = Milliseconds(Stopwatch.GetTimestamp() - owner.started) });
        }
        void Append(Operation item)
        {
            if (operationCount < MaxOperations) operations[operationCount++] = item;
            else droppedOperations++;
        }
        static double Milliseconds(long ticks) => ticks * (1000d / Stopwatch.Frequency);
        void OnApplicationPause(bool paused) { if (paused) Finish("pause"); }
        void OnApplicationQuit() => Finish("quit");
        void OnDestroy() => Finish("destroy");
        void Finish(string reason)
        {
            if (finished) return;
            finished = true;
            if (active == this) active = null;
            foreach (var recorder in recorders) if (recorder.Valid) recorder.Dispose();
            var report = new Report { stopReason = reason, unity = Application.unityVersion, appVersion = Application.version,
                metrics = metrics, frames = new Frame[frameCount], operations = new Operation[operationCount],
                droppedOperations = droppedOperations, width = Screen.width, height = Screen.height };
            Array.Copy(frames, report.frames, frameCount);
            Array.Copy(operations, report.operations, operationCount);
            try
            {
                string path = Path.Combine(Application.persistentDataPath, "ui-profile.json");
                File.WriteAllText(path, JsonUtility.ToJson(report));
                UnityEngine.Debug.Log("UI_PROFILE_SAVED frames=" + frameCount + " operations=" + operationCount);
            }
            catch (Exception error) { UnityEngine.Debug.LogWarning("UI_PROFILE_SAVE_FAILED " + error.GetType().Name); }
            enabled = false;
        }
    }
}
