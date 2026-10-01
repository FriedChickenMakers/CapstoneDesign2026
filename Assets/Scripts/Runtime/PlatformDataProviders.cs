using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace CapstoneDesign.Runtime
{
    /// <summary>
    /// Keeps gameplay and debug UI independent from the Android implementation.
    /// Providers can supply live platform data, deterministic replay data, or
    /// a hardware-free mock without changing consumers.
    /// </summary>
    public interface IPlatformDataProvider
    {
        PlatformInputMode Mode { get; }
        AndroidPlatformSnapshot GetSnapshot();
        PlatformActionResult StartBackgroundSensorService();
        PlatformActionResult StopBackgroundSensorService();
        PlatformActionResult RequestSensorPermissions();
        PlatformActionResult RequestHealthConnectPermissions();
        PlatformActionResult RefreshHealthData();
        string GetRecentSensorSamplesJson(int limit);
    }

    public sealed class LivePlatformDataProvider : IPlatformDataProvider
    {
        public PlatformInputMode Mode => PlatformInputMode.Live;

        public AndroidPlatformSnapshot GetSnapshot() => AndroidPlatformBridge.GetLiveSnapshot();

        public PlatformActionResult StartBackgroundSensorService() =>
            AndroidPlatformBridge.InvokeNativeAction("startBackgroundSensorService", "Sensor service is Android-only");

        public PlatformActionResult StopBackgroundSensorService() =>
            AndroidPlatformBridge.InvokeNativeAction("stopBackgroundSensorService", "Sensor service is Android-only");

        public PlatformActionResult RequestSensorPermissions() =>
            AndroidPlatformBridge.InvokeNativeAction("requestSensorPermissions", "Runtime permissions are Android-only");

        public PlatformActionResult RequestHealthConnectPermissions() =>
            AndroidPlatformBridge.InvokeNativeAction("requestHealthConnectPermissions", "Health Connect is Android-only");

        public PlatformActionResult RefreshHealthData() =>
            AndroidPlatformBridge.InvokeNativeAction("refreshHealthData", "Health Connect is Android-only");

        public string GetRecentSensorSamplesJson(int limit) =>
            AndroidPlatformBridge.GetLiveRecentSensorSamplesJson(limit);
    }

    public sealed class MockPlatformDataProvider : IPlatformDataProvider
    {
        public PlatformInputMode Mode => PlatformInputMode.Mock;

        public AndroidPlatformSnapshot GetSnapshot() =>
            AndroidPlatformBridge.CreateMockSnapshot(PlatformInputMode.Mock);

        public PlatformActionResult StartBackgroundSensorService() => MockAction("Mock sensor service is running");
        public PlatformActionResult StopBackgroundSensorService() => MockAction("Mock sensor service stopped");
        public PlatformActionResult RequestSensorPermissions() => MockAction("Mock permissions are available");
        public PlatformActionResult RequestHealthConnectPermissions() => MockAction("Mock permissions are available");
        public PlatformActionResult RefreshHealthData() => MockAction("Mock health data refreshed");

        public string GetRecentSensorSamplesJson(int limit) =>
            "{\"status\":\"AVAILABLE\",\"mode\":\"MOCK\",\"samples\":[]}";

        private static PlatformActionResult MockAction(string message)
        {
            return new PlatformActionResult { status = "AVAILABLE", message = message };
        }
    }

    /// <summary>
    /// Replays one full AndroidPlatformSnapshot per call. The JSONL format is
    /// intentionally simple: each line is JsonUtility.ToJson(snapshot). A
    /// RecordingPlatformDataProvider produces exactly this format.
    /// </summary>
    public sealed class ReplayPlatformDataProvider : IPlatformDataProvider
    {
        private readonly List<AndroidPlatformSnapshot> snapshots = new List<AndroidPlatformSnapshot>();
        private readonly bool loop;
        private int index;

        public ReplayPlatformDataProvider(string jsonLinesPath, bool loop = true)
        {
            if (string.IsNullOrWhiteSpace(jsonLinesPath))
            {
                throw new ArgumentException("Replay JSONL path is required", nameof(jsonLinesPath));
            }
            if (!File.Exists(jsonLinesPath))
            {
                throw new FileNotFoundException("Replay JSONL file was not found", jsonLinesPath);
            }

            this.loop = loop;
            foreach (string line in File.ReadLines(jsonLinesPath))
            {
                if (string.IsNullOrWhiteSpace(line)) continue;
                AndroidPlatformSnapshot snapshot = AndroidPlatformBridge.ParseSnapshot(line);
                if (snapshot.ParsedStatus != PlatformStatus.Error)
                {
                    snapshots.Add(snapshot);
                }
            }
            if (snapshots.Count == 0)
            {
                throw new InvalidDataException("Replay JSONL contains no valid platform snapshots");
            }
        }

        public PlatformInputMode Mode => PlatformInputMode.Replay;

        public AndroidPlatformSnapshot GetSnapshot()
        {
            AndroidPlatformSnapshot snapshot = snapshots[index];
            snapshot.inputMode = "REPLAY";
            if (index < snapshots.Count - 1)
            {
                index++;
            }
            else if (loop)
            {
                index = 0;
            }
            return snapshot;
        }

        public PlatformActionResult StartBackgroundSensorService() => ReplayAction();
        public PlatformActionResult StopBackgroundSensorService() => ReplayAction();
        public PlatformActionResult RequestSensorPermissions() => ReplayAction();
        public PlatformActionResult RequestHealthConnectPermissions() => ReplayAction();
        public PlatformActionResult RefreshHealthData() => ReplayAction();

        public string GetRecentSensorSamplesJson(int limit) =>
            "{\"status\":\"NO_DATA\",\"mode\":\"REPLAY\",\"samples\":[]}";

        private static PlatformActionResult ReplayAction()
        {
            return new PlatformActionResult
            {
                status = "UNSUPPORTED",
                message = "Platform actions are disabled during replay"
            };
        }
    }

    /// <summary>
    /// Decorates any provider and appends its full snapshots as JSONL. It is
    /// separate from the native sensor JSONL buffer, which remains available
    /// for raw sample inspection after the Android service runs in background.
    /// </summary>
    public sealed class RecordingPlatformDataProvider : IPlatformDataProvider, IDisposable
    {
        private readonly IPlatformDataProvider inner;
        private readonly StreamWriter writer;
        private bool stopped;

        public RecordingPlatformDataProvider(IPlatformDataProvider inner, string jsonLinesPath)
        {
            this.inner = inner ?? throw new ArgumentNullException(nameof(inner));
            string directory = Path.GetDirectoryName(jsonLinesPath);
            if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
            writer = new StreamWriter(jsonLinesPath, append: true) { AutoFlush = true };
        }

        public PlatformInputMode Mode => inner.Mode;

        public AndroidPlatformSnapshot GetSnapshot()
        {
            AndroidPlatformSnapshot snapshot = inner.GetSnapshot();
            if (!stopped)
            {
                writer.WriteLine(JsonUtility.ToJson(snapshot));
            }
            return snapshot;
        }

        public PlatformActionResult StartBackgroundSensorService() => inner.StartBackgroundSensorService();
        public PlatformActionResult StopBackgroundSensorService() => inner.StopBackgroundSensorService();
        public PlatformActionResult RequestSensorPermissions() => inner.RequestSensorPermissions();
        public PlatformActionResult RequestHealthConnectPermissions() => inner.RequestHealthConnectPermissions();
        public PlatformActionResult RefreshHealthData() => inner.RefreshHealthData();
        public string GetRecentSensorSamplesJson(int limit) => inner.GetRecentSensorSamplesJson(limit);

        public IPlatformDataProvider Stop()
        {
            Dispose();
            return inner;
        }

        public void Dispose()
        {
            if (stopped) return;
            stopped = true;
            writer.Dispose();
        }
    }
}
