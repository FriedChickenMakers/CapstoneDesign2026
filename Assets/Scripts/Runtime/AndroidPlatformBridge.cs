using System;
using UnityEngine;

namespace CapstoneDesign.Runtime
{
    /// <summary>
    /// The only Unity-facing entry point for Android platform APIs. Native
    /// exceptions are converted to status-bearing JSON results and never
    /// escape into gameplay code.
    /// </summary>
    public static class AndroidPlatformBridge
    {
        private const string NativeClassName = "com.capstonedesign2026.platform.AndroidPlatformBridge";
        private static bool initialized;
        private static IPlatformDataProvider provider = new LivePlatformDataProvider();

        public static PlatformInputMode InputMode
        {
            get => provider.Mode;
            set
            {
                switch (value)
                {
                    case PlatformInputMode.Mock:
                        UseMockProvider();
                        break;
                    case PlatformInputMode.Replay:
                        throw new InvalidOperationException("UseReplayProvider requires a JSONL path.");
                    default:
                        UseLiveProvider();
                        break;
                }
            }
        }

        public static AndroidPlatformSnapshot GetSnapshot()
        {
            using var timing = UiPerformanceProbe.Measure("Platform.GetSnapshot");
            return provider.GetSnapshot();
        }

        public static PlatformActionResult StartBackgroundSensorService()
        {
            return provider.StartBackgroundSensorService();
        }

        public static PlatformActionResult StartDailyAccelerationTrial()
        {
            return InvokeNativeActivityAction("startDailyAccelerationTrial");
        }

        public static PlatformActionResult StopLegacyAutoAccelerationTrial()
        {
            return InvokeNativeAction("stopLegacyAutoAccelerationTrial", "Acceleration trial requires Android");
        }

        public static PlatformActionResult OpenSensorHistory()
        {
            return InvokeNativeActivityAction("openSensorHistory");
        }

        public static PlatformActionResult CaptureDebugSensors()
        {
            return InvokeNativeAction("captureDebugSensors", "Debug sensors require Android");
        }

        public static PlatformActionResult StartAutomaticPermissionFlow()
        {
            return InvokeNativeActivityAction("startAutomaticPermissionFlow");
        }

        public static PlatformActionResult OpenHealthPermissionSettings()
        {
            return InvokeNativeAction("openHealthPermissionSettings", "Health permissions require Android");
        }

        public static PlatformActionResult StopBackgroundSensorService()
        {
            return provider.StopBackgroundSensorService();
        }

        public static PlatformActionResult RequestSensorPermissions()
        {
            return provider.RequestSensorPermissions();
        }

        public static PlatformActionResult RequestHealthConnectPermissions()
        {
            return provider.RequestHealthConnectPermissions();
        }

        public static PlatformActionResult RefreshHealthData()
        {
            return provider.RefreshHealthData();
        }

        public static PlatformActionResult RefreshHeartRange(long startEpochMs, long endEpochMs)
        {
            if (provider.Mode != PlatformInputMode.Live) return new PlatformActionResult { status = "UNSUPPORTED", message = "MOCK/REPLAY range is fixed by its fixture" };
#if UNITY_ANDROID && !UNITY_EDITOR
            EnsureInitialized();
            try
            {
                using (var bridge = new AndroidJavaClass(NativeClassName))
                    return JsonUtility.FromJson<PlatformActionResult>(bridge.CallStatic<string>("refreshHealthRange", startEpochMs, endEpochMs));
            }
            catch (Exception ex) { return ActionError(ex.Message, "HEART_RANGE_QUERY_FAILED"); }
#else
            return new PlatformActionResult { status = "UNSUPPORTED", message = "Health Connect requires Android" };
#endif
        }

        public static PlatformActionResult RefreshStepsRange(long startEpochMs, long endEpochMs, string requestId)
        {
            if (provider.Mode != PlatformInputMode.Live) return new PlatformActionResult { status = "UNSUPPORTED", message = "Step range requires LIVE input" };
#if UNITY_ANDROID && !UNITY_EDITOR
            EnsureInitialized();
            try
            {
                using (var bridge = new AndroidJavaClass(NativeClassName))
                    return JsonUtility.FromJson<PlatformActionResult>(bridge.CallStatic<string>("refreshStepsRange", startEpochMs, endEpochMs, requestId));
            }
            catch (Exception ex) { return ActionError(ex.Message, "STEP_RANGE_QUERY_FAILED"); }
#else
            return new PlatformActionResult { status = "UNSUPPORTED", message = "Health Connect requires Android" };
#endif
        }

        public static StepRangeSnapshot GetStepRangeSnapshot()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            EnsureInitialized();
            try
            {
                using (var bridge = new AndroidJavaClass(NativeClassName))
                    return JsonUtility.FromJson<StepRangeSnapshot>(bridge.CallStatic<string>("getStepRangeSnapshotJson"));
            }
            catch (Exception ex) { return new StepRangeSnapshot { status = "ERROR", message = ex.Message }; }
#else
            return new StepRangeSnapshot();
#endif
        }

        public static PlatformActionResult ScheduleMissionRewardReminder(string sessionId,long startEpochMs,int goalSteps,long virtualSteps)
        {
            if(provider.Mode!=PlatformInputMode.Live)return new PlatformActionResult{status="UNSUPPORTED",message="Reminder requires LIVE input"};
#if UNITY_ANDROID && !UNITY_EDITOR
            EnsureInitialized();
            try{using(var bridge=new AndroidJavaClass(NativeClassName))
                return JsonUtility.FromJson<PlatformActionResult>(bridge.CallStatic<string>("scheduleMissionRewardReminder",sessionId,startEpochMs,goalSteps,virtualSteps));}
            catch(Exception ex){return ActionError(ex.Message,"REMINDER_SCHEDULE_FAILED");}
#else
            return new PlatformActionResult{status="UNSUPPORTED",message="Reminder requires Android"};
#endif
        }

        public static PlatformActionResult CancelMissionRewardReminder(string sessionId)
        {
            if(provider.Mode!=PlatformInputMode.Live)return new PlatformActionResult{status="UNSUPPORTED",message="Reminder requires LIVE input"};
#if UNITY_ANDROID && !UNITY_EDITOR
            EnsureInitialized();
            try{using(var bridge=new AndroidJavaClass(NativeClassName))
                return JsonUtility.FromJson<PlatformActionResult>(bridge.CallStatic<string>("cancelMissionRewardReminder",sessionId));}
            catch(Exception ex){return ActionError(ex.Message,"REMINDER_CANCEL_FAILED");}
#else
            return new PlatformActionResult{status="UNSUPPORTED",message="Reminder requires Android"};
#endif
        }

        public static PlatformActionResult NotifyMissionRewardReady(string sessionId)
        {
            if(provider.Mode!=PlatformInputMode.Live)return new PlatformActionResult{status="UNSUPPORTED",message="Reminder requires LIVE input"};
#if UNITY_ANDROID && !UNITY_EDITOR
            EnsureInitialized();
            try{using(var bridge=new AndroidJavaClass(NativeClassName))
                return JsonUtility.FromJson<PlatformActionResult>(bridge.CallStatic<string>("notifyMissionRewardReady",sessionId));}
            catch(Exception ex){return ActionError(ex.Message,"REMINDER_NOTIFY_FAILED");}
#else
            return new PlatformActionResult{status="UNSUPPORTED",message="Reminder requires Android"};
#endif
        }

        public static string GetRecentSensorSamplesJson(int limit = 200)
        {
            return provider.GetRecentSensorSamplesJson(limit);
        }

        public static void UseLiveProvider()
        {
            ReplaceProvider(new LivePlatformDataProvider());
        }

        public static void UseMockProvider()
        {
            ReplaceProvider(new MockPlatformDataProvider());
        }

        public static PlatformActionResult UseReplayProvider(string jsonLinesPath, bool loop = true)
        {
            try
            {
                ReplaceProvider(new ReplayPlatformDataProvider(jsonLinesPath, loop));
                return new PlatformActionResult
                {
                    status = "AVAILABLE",
                    message = "Replay provider loaded"
                };
            }
            catch (Exception exception)
            {
                return ActionError(exception.Message, "REPLAY_LOAD_FAILED");
            }
        }

        public static PlatformActionResult StartSnapshotRecording(string jsonLinesPath = null)
        {
            if (provider is RecordingPlatformDataProvider)
            {
                return new PlatformActionResult
                {
                    status = "AVAILABLE",
                    message = "Snapshot recording is already active"
                };
            }

            string path = string.IsNullOrWhiteSpace(jsonLinesPath)
                ? System.IO.Path.Combine(Application.persistentDataPath, "platform_snapshots.jsonl")
                : jsonLinesPath;
            try
            {
                provider = new RecordingPlatformDataProvider(provider, path);
                return new PlatformActionResult
                {
                    status = "AVAILABLE",
                    message = "Recording snapshots to " + path
                };
            }
            catch (Exception exception)
            {
                return ActionError(exception.Message, "RECORD_START_FAILED");
            }
        }

        public static PlatformActionResult StopSnapshotRecording()
        {
            if (!(provider is RecordingPlatformDataProvider recording))
            {
                return new PlatformActionResult
                {
                    status = "NO_DATA",
                    message = "Snapshot recording is not active"
                };
            }

            provider = recording.Stop();
            return new PlatformActionResult
            {
                status = "AVAILABLE",
                message = "Snapshot recording stopped"
            };
        }

        internal static AndroidPlatformSnapshot GetLiveSnapshot()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            EnsureInitialized();
            try
            {
                using (AndroidJavaClass bridge = new AndroidJavaClass(NativeClassName))
                {
                    string json = bridge.CallStatic<string>("getPlatformSnapshotJson");
                    AndroidPlatformSnapshot snapshot = JsonUtility.FromJson<AndroidPlatformSnapshot>(json);
                    return snapshot ?? ErrorSnapshot("Native snapshot was empty", "EMPTY_NATIVE_SNAPSHOT");
                }
            }
            catch (Exception exception)
            {
                return ErrorSnapshot(exception.Message, "NATIVE_SNAPSHOT_EXCEPTION");
            }
#else
            return CreateMockSnapshot(PlatformInputMode.Live);
#endif
        }

        internal static string GetLiveRecentSensorSamplesJson(int limit)
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            EnsureInitialized();
            try
            {
                using (AndroidJavaClass bridge = new AndroidJavaClass(NativeClassName))
                {
                    return bridge.CallStatic<string>("getRecentSensorSamplesJson", limit);
                }
            }
            catch (Exception exception)
            {
                return "{\"status\":\"ERROR\",\"message\":\"" + EscapeJson(exception.Message) + "\",\"samples\":[]}";
            }
#else
            return "{\"status\":\"NO_DATA\",\"mode\":\"MOCK\",\"samples\":[]}";
#endif
        }

        public static AndroidPlatformSnapshot ParseSnapshot(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                return ErrorSnapshot("Snapshot JSON was empty", "EMPTY_JSON");
            }

            try
            {
                return JsonUtility.FromJson<AndroidPlatformSnapshot>(json) ??
                    ErrorSnapshot("Snapshot JSON produced no object", "INVALID_JSON");
            }
            catch (Exception exception)
            {
                return ErrorSnapshot(exception.Message, "JSON_PARSE_FAILED");
            }
        }

        internal static PlatformActionResult InvokeNativeAction(string method, string editorMessage)
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            EnsureInitialized();
            try
            {
                using (AndroidJavaClass bridge = new AndroidJavaClass(NativeClassName))
                {
                    string json = bridge.CallStatic<string>(method);
                    PlatformActionResult result = JsonUtility.FromJson<PlatformActionResult>(json);
                    return result ?? ActionError("Native action returned no result", "EMPTY_NATIVE_RESULT");
                }
            }
            catch (Exception exception)
            {
                return ActionError(exception.Message, "NATIVE_ACTION_EXCEPTION");
            }
#else
            return new PlatformActionResult
            {
                status = "UNSUPPORTED",
                message = editorMessage
            };
#endif
        }

        private static PlatformActionResult InvokeNativeActivityAction(string method)
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            if (provider.Mode != PlatformInputMode.Live)
                return new PlatformActionResult { status = "UNSUPPORTED", message = "Live Android input required" };
            EnsureInitialized();
            try
            {
                using (var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                using (var activity = player.GetStatic<AndroidJavaObject>("currentActivity"))
                using (var bridge = new AndroidJavaClass(NativeClassName))
                {
                    string json = bridge.CallStatic<string>(method, activity);
                    return JsonUtility.FromJson<PlatformActionResult>(json) ??
                        ActionError("Native action returned no result", "EMPTY_NATIVE_RESULT");
                }
            }
            catch (Exception exception) { return ActionError(exception.Message, "NATIVE_ACTIVITY_ACTION_FAILED"); }
#else
            return new PlatformActionResult { status = "UNSUPPORTED", message = "Android device required" };
#endif
        }

#if UNITY_ANDROID && !UNITY_EDITOR
        private static void EnsureInitialized()
        {
            if (initialized)
            {
                return;
            }

            try
            {
                using (AndroidJavaClass unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                using (AndroidJavaObject activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity"))
                using (AndroidJavaClass bridge = new AndroidJavaClass(NativeClassName))
                {
                    string json = bridge.CallStatic<string>(
                        "initialize",
                        activity,
                        Application.unityVersion,
                        PlatformInputMode.Live.ToString().ToUpperInvariant());
                    PlatformActionResult result = JsonUtility.FromJson<PlatformActionResult>(json);
                    if (result == null || result.ParsedStatus != PlatformStatus.Available)
                    {
                        throw new InvalidOperationException(
                            result == null ? "Native initialization returned no result" : result.message);
                    }
                }
                initialized = true;
            }
            catch (Exception exception)
            {
                Debug.LogWarning("Android platform bridge initialization failed: " + exception.Message);
            }
        }
#endif

        internal static AndroidPlatformSnapshot CreateMockSnapshot(PlatformInputMode mode)
        {
            return new AndroidPlatformSnapshot
            {
                status = "AVAILABLE",
                inputMode = mode.ToString().ToUpperInvariant(),
                generatedAtEpochMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                device = new AndroidDeviceSnapshot
                {
                    status = "AVAILABLE",
                    manufacturer = "Unity",
                    model = "Editor",
                    androidVersion = "N/A",
                    androidApi = 0,
                    appVersion = Application.version,
                    unityVersion = Application.unityVersion
                },
                sensorService = new SensorServiceSnapshot
                {
                    serviceStatus = "UNSUPPORTED",
                    message = "Android SensorManager is unavailable in the Editor",
                    sensors = CreateMockSensors(mode)
                },
                healthConnect = new HealthConnectSnapshot
                {
                    status = "UNSUPPORTED",
                    permissionStatus = "UNSUPPORTED",
                    message = "Health Connect is unavailable in the Editor"
                }
            };
        }

        private static SensorCapabilitySnapshot[] CreateMockSensors(PlatformInputMode mode)
        {
            string status = mode == PlatformInputMode.Mock ? "AVAILABLE" : "UNSUPPORTED";
            return new[]
            {
                new SensorCapabilitySnapshot
                {
                    key = "ACCELEROMETER",
                    status = status,
                    name = mode == PlatformInputMode.Mock ? "Mock Accelerometer" : string.Empty,
                    values = mode == PlatformInputMode.Mock ? new[] { 0.1f, 9.8f, -0.2f } : Array.Empty<float>(),
                    source = mode == PlatformInputMode.Mock ? "Unity Mock" : string.Empty
                },
                new SensorCapabilitySnapshot { key = "GYROSCOPE", status = status },
                new SensorCapabilitySnapshot { key = "PRESSURE", status = "UNSUPPORTED" },
                new SensorCapabilitySnapshot { key = "STEP_COUNTER", status = "UNSUPPORTED" }
            };
        }

        private static AndroidPlatformSnapshot ErrorSnapshot(string message, string code)
        {
            return new AndroidPlatformSnapshot
            {
                status = "ERROR",
                message = message,
                errorCode = code
            };
        }

        internal static PlatformActionResult ActionError(string message, string code)
        {
            return new PlatformActionResult
            {
                status = "ERROR",
                message = message,
                errorCode = code
            };
        }

        private static string EscapeJson(string value)
        {
            return (value ?? string.Empty).Replace("\\", "\\\\").Replace("\"", "\\\"");
        }

        private static void ReplaceProvider(IPlatformDataProvider replacement)
        {
            if (provider is RecordingPlatformDataProvider recording)
            {
                recording.Dispose();
            }
            provider = replacement ?? throw new ArgumentNullException(nameof(replacement));
        }
    }
}
