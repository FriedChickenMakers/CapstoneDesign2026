using System;

namespace CapstoneDesign.Runtime
{
    public enum PlatformStatus
    {
        Available,
        Unsupported,
        PermissionRequired,
        PermissionDenied,
        NoData,
        ServiceUnavailable,
        Disconnected,
        Stale,
        Partial,
        Error
    }

    public enum PlatformInputMode
    {
        Live,
        Replay,
        Mock
    }

    [Serializable]
    public sealed class PlatformActionResult
    {
        public string status = "ERROR";
        public string message = string.Empty;
        public string errorCode = string.Empty;

        public PlatformStatus ParsedStatus => PlatformStatusParser.Parse(status);
    }

    [Serializable]
    public sealed class AndroidDeviceSnapshot
    {
        public string status = "NO_DATA";
        public string manufacturer = string.Empty;
        public string model = string.Empty;
        public string androidVersion = string.Empty;
        public int androidApi;
        public string appVersion = string.Empty;
        public string unityVersion = string.Empty;
    }

    [Serializable]
    public sealed class RuntimePermissionSnapshot
    {
        public string activityRecognition = "NO_DATA";
        public string notifications = "NO_DATA";
    }

    [Serializable]
    public sealed class SensorCapabilitySnapshot
    {
        public string key = string.Empty;
        public string status = "NO_DATA";
        public string name = string.Empty;
        public string vendor = string.Empty;
        public int androidType;
        public int version;
        public float resolution;
        public float maxRange;
        public float power;
        public int minDelay;
        public int reportingMode = -1;
        public bool isWakeUpSensor;
        public long receivedIntervalMs;
        public float[] values = Array.Empty<float>();
        public long eventTimestampNanos;
        public long receivedAtEpochMs;
        public string source = string.Empty;

        public PlatformStatus ParsedStatus => PlatformStatusParser.Parse(status);
    }

    [Serializable]
    public sealed class SensorServiceSnapshot
    {
        public string serviceStatus = "NO_DATA";
        public bool running;
        public string message = string.Empty;
        public string errorCode = string.Empty;
        public int sampleCount;
        public long persistedSampleCount;
        public long droppedPendingSamples;
        public long malformedTailLines;
        public int pendingSampleCount;
        public string captureSessionId = string.Empty;
        public long lastEventEpochMs;
        public SensorCapabilitySnapshot[] sensors = Array.Empty<SensorCapabilitySnapshot>();
    }

    [Serializable]
    public sealed class HeartRateSampleSnapshot
    {
        public string recordId = string.Empty;
        public string sourcePackage = string.Empty;
        public long measuredAtEpochMs;
        public long beatsPerMinute;
        public int segmentId;
    }

    [Serializable]
    public sealed class HealthMissingIntervalSnapshot
    {
        public long startEpochMs;
        public long endEpochMs;
        public string sourcePackage = string.Empty;
    }

    [Serializable]
    public sealed class HealthMetricSnapshot
    {
        public string status = "NO_DATA";
        public bool hasValue;
        public double value;
        public string displayValue = "—";
        public string unit = string.Empty;
        public string source = string.Empty;
        public string sourcePackage = string.Empty;
        public long measuredAtEpochMs;
        public long lastUpdatedEpochMs;
        public string message = string.Empty;
        public string errorCode = string.Empty;
        public long queryStartEpochMs;
        public long queryEndEpochMs;
        public string queryTimeZone = string.Empty;
        public bool queryComplete;
        public string completionReason = string.Empty;
        public int pagesRead;
        public int recordCount;
        public string sourceFilter = string.Empty;
        public string sourceBreakdown = string.Empty;
        public string[] sourcePackages = Array.Empty<string>();
        public int sampleCount;
        public int displaySampleCount;
        public HeartRateSampleSnapshot[] heartRateSamples = Array.Empty<HeartRateSampleSnapshot>();
        public HealthMissingIntervalSnapshot[] missingIntervals = Array.Empty<HealthMissingIntervalSnapshot>();

        public PlatformStatus ParsedStatus => PlatformStatusParser.Parse(status);
    }

    [Serializable]
    public sealed class StepRangeSnapshot
    {
        public string status = "NO_DATA";
        public string requestId = string.Empty;
        public string source = string.Empty;
        public string sourceLabel = string.Empty;
        public string sourceBreakdown = string.Empty;
        public string message = string.Empty;
        public bool refreshing;
        public bool queryComplete;
        public bool hasValue;
        public long count;
        public long queryStartEpochMs;
        public long queryEndEpochMs;
        public long lastUpdatedEpochMs;
    }

    [Serializable]
    public sealed class WatchDataSnapshot
    {
        public string status = "DISCONNECTED";
        public string directConnectionStatus = "NOT_CHECKED";
        public string message = string.Empty;
        public string source = string.Empty;
    }

    [Serializable]
    public sealed class HealthConnectSnapshot
    {
        public string status = "SERVICE_UNAVAILABLE";
        public string message = string.Empty;
        public string permissionStatus = "NO_DATA";
        public bool refreshing;
        public int grantedPermissionCount;
        public int requiredPermissionCount;
        public long lastRefreshEpochMs;
        public long lastSuccessfulRefreshEpochMs;
        public string samsungHealthStatus = "SERVICE_UNAVAILABLE";
        public string samsungHealthMessage = string.Empty;
        public HealthMetricSnapshot steps = new HealthMetricSnapshot();
        public HealthMetricSnapshot samsungSteps = new HealthMetricSnapshot();
        public HealthMetricSnapshot sleep = new HealthMetricSnapshot();
        public HealthMetricSnapshot heartRate = new HealthMetricSnapshot();
        public HealthMetricSnapshot exercise = new HealthMetricSnapshot();
        public WatchDataSnapshot watchData = new WatchDataSnapshot();
    }

    [Serializable]
    public sealed class DailyAccelerationSnapshot
    {
        public string status = "UNKNOWN";
        public string reason = "NO_STATUS_RECORD";
        public long startedEpochMs;
        public long updatedEpochMs;
        public long lastSampleEpochMs;
        public long lastWriteEpochMs;
    }

    [Serializable]
    public sealed class AndroidPlatformSnapshot
    {
        public string status = "NO_DATA";
        public string message = string.Empty;
        public string errorCode = string.Empty;
        public long generatedAtEpochMs;
        public string inputMode = "LIVE";
        public AndroidDeviceSnapshot device = new AndroidDeviceSnapshot();
        public SensorServiceSnapshot sensorService = new SensorServiceSnapshot();
        public DailyAccelerationSnapshot dailyAcceleration = new DailyAccelerationSnapshot();
        public HealthConnectSnapshot healthConnect = new HealthConnectSnapshot();
        public RuntimePermissionSnapshot runtimePermissions = new RuntimePermissionSnapshot();

        public PlatformStatus ParsedStatus => PlatformStatusParser.Parse(status);
    }

    public static class PlatformStatusParser
    {
        public static PlatformStatus Parse(string status)
        {
            switch (status)
            {
                case "AVAILABLE": return PlatformStatus.Available;
                case "UNSUPPORTED": return PlatformStatus.Unsupported;
                case "PERMISSION_REQUIRED": return PlatformStatus.PermissionRequired;
                case "PERMISSION_DENIED": return PlatformStatus.PermissionDenied;
                case "NO_DATA": return PlatformStatus.NoData;
                case "SERVICE_UNAVAILABLE": return PlatformStatus.ServiceUnavailable;
                case "DISCONNECTED": return PlatformStatus.Disconnected;
                case "STALE": return PlatformStatus.Stale;
                case "PARTIAL": return PlatformStatus.Partial;
                default: return PlatformStatus.Error;
            }
        }
    }
}
