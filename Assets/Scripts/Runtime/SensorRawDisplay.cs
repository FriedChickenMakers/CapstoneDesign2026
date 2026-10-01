using System;
using System.Globalization;
using System.Text;
using UnityEngine;
using UnityEngine.UI;

namespace CapstoneDesign.Runtime
{
    /// <summary>
    /// Presents the status-bearing Android platform facade. It intentionally
    /// never calls SensorManager or Health Connect directly.
    /// </summary>
    public sealed class SensorRawDisplay : MonoBehaviour
    {
        private const float RefreshSeconds = 0.5f;
        private const float HealthRefreshSeconds = 60f;

        [SerializeField] public Text output;
        [SerializeField] public Button startServiceButton;
        [SerializeField] public Button stopServiceButton;
        [SerializeField] public Button sensorPermissionButton;
        [SerializeField] public Button healthPermissionButton;
        [SerializeField] public Button refreshHealthButton;

        private readonly StringBuilder builder = new StringBuilder(2048);
        private float nextRefresh;
        private float nextHealthRefresh;
        private string lastAction = "Ready";
        private bool automaticPermissionFlowStarted;
        private Button debugModeButton;
        private Button virtualStepButton;
        private WeekOneQuestDemo Loop => transform.root.GetComponentInChildren<WeekOneQuestDemo>(true);

        private void Awake()
        {
            ConfigureScrollablePanel();
            GardenUi.Button(transform, "Charts", .58f, .82f, .38f, .13f, OpenCharts);
            if(Debug.isDebugBuild || Application.isEditor)
            {
                debugModeButton=GardenUi.Button(transform,"걸음 디버그 OFF",.04f,.16f,.44f,.085f,ToggleDebugWalk);
                virtualStepButton=GardenUi.Button(transform,"가상 걸음 +1",.52f,.16f,.44f,.085f,AddVirtualStep);
            }
            AddListener(startServiceButton, StartService);
            AddListener(stopServiceButton, StopService);
            AddListener(sensorPermissionButton, RequestSensorPermissions);
            AddListener(healthPermissionButton, RequestHealthPermissions);
            AddListener(refreshHealthButton, RefreshHealth);
            RefreshText();
        }

        private void ConfigureScrollablePanel()
        {
            var rect=GetComponent<RectTransform>();
            rect.anchorMin=new Vector2(.04f,.16f);rect.anchorMax=new Vector2(.96f,.94f);
            rect.offsetMin=Vector2.zero;rect.offsetMax=Vector2.zero;
            var viewport=GardenUi.Box(transform,"Diagnostics viewport",.03f,.27f,.94f,.53f);
            viewport.AddComponent<RectMask2D>();
            var scroll=gameObject.AddComponent<ScrollRect>();scroll.horizontal=false;scroll.vertical=true;
            scroll.viewport=(RectTransform)viewport.transform;scroll.scrollSensitivity=30;
            output.transform.SetParent(viewport.transform,false);
            var content=output.rectTransform;content.anchorMin=new Vector2(0,1);content.anchorMax=new Vector2(1,1);
            content.pivot=new Vector2(.5f,1);content.anchoredPosition=Vector2.zero;content.sizeDelta=Vector2.zero;
            output.font=GardenUi.ResolveFont();output.fontSize=23;output.raycastTarget=true;
            output.gameObject.AddComponent<ContentSizeFitter>().verticalFit=ContentSizeFitter.FitMode.PreferredSize;
            scroll.content=content;
        }

        private void Update()
        {
            if (Time.unscaledTime >= nextHealthRefresh)
            {
                nextHealthRefresh = Time.unscaledTime + HealthRefreshSeconds;
                AndroidPlatformSnapshot current = AndroidPlatformBridge.GetSnapshot();
                if (current.healthConnect?.status == "AVAILABLE")
                {
                    PlatformActionResult result = AndroidPlatformBridge.RefreshHealthData();
                    if (result.ParsedStatus == PlatformStatus.Error)
                    {
                        lastAction = Describe(result);
                    }
                }
            }

            if (Time.unscaledTime >= nextRefresh)
            {
                nextRefresh = Time.unscaledTime + RefreshSeconds;
                RefreshText();
            }
        }

        public void OnSettingsOpened()
        {
            Loop?.RequestStepSyncSoon();
            if (Application.platform != RuntimePlatform.Android ||
                AndroidPlatformBridge.InputMode != PlatformInputMode.Live) return;
            lastAction = Describe(AndroidPlatformBridge.CaptureDebugSensors());
            AndroidPlatformBridge.RefreshHealthData();
            nextHealthRefresh = Time.unscaledTime + HealthRefreshSeconds;
            if (!automaticPermissionFlowStarted)
            {
                automaticPermissionFlowStarted = true;
                AndroidPlatformBridge.StartAutomaticPermissionFlow();
            }
            RefreshText();
        }

        private void OnApplicationFocus(bool focused)
        {
            if (focused && Application.platform == RuntimePlatform.Android &&
                gameObject.activeInHierarchy && AndroidPlatformBridge.InputMode == PlatformInputMode.Live)
                AndroidPlatformBridge.RefreshHealthData();
        }

        private void OnDestroy()
        {
            RemoveListener(startServiceButton, StartService);
            RemoveListener(stopServiceButton, StopService);
            RemoveListener(sensorPermissionButton, RequestSensorPermissions);
            RemoveListener(healthPermissionButton, RequestHealthPermissions);
            RemoveListener(refreshHealthButton, RefreshHealth);
        }

        private void StartService()
        {
            lastAction = Describe(AndroidPlatformBridge.StartBackgroundSensorService());
            RefreshText();
        }

        private void StopService()
        {
            lastAction = Describe(AndroidPlatformBridge.StopBackgroundSensorService());
            RefreshText();
        }

        private void RequestSensorPermissions()
        {
            lastAction = Describe(AndroidPlatformBridge.RequestSensorPermissions());
        }

        private void RequestHealthPermissions()
        {
            HealthConnectSnapshot health = AndroidPlatformBridge.GetSnapshot().healthConnect;
            lastAction = Describe(health?.permissionStatus == "PERMISSION_DENIED"
                ? AndroidPlatformBridge.OpenHealthPermissionSettings()
                : AndroidPlatformBridge.RequestHealthConnectPermissions());
        }

        private void RefreshHealth()
        {
            lastAction = Describe(AndroidPlatformBridge.RefreshHealthData());
            Loop?.RequestStepSyncSoon(true,true);
            nextHealthRefresh = Time.unscaledTime + HealthRefreshSeconds;
        }

        private void OpenCharts()
        {
            lastAction = Describe(AndroidPlatformBridge.OpenSensorHistory());
            RefreshText();
        }

        private void ToggleDebugWalk()
        {
            if(Loop?.ToggleDebugWalk()==true)lastAction="DEBUG 걸음 모드를 변경했어요";
            RefreshText();
        }

        private void AddVirtualStep()
        {
            if(Loop?.AddVirtualDebugStep()==true)lastAction="가상 걸음 +1 저장됨";
            RefreshText();
        }

        private void RefreshText()
        {
            if (output == null)
            {
                return;
            }

            AndroidPlatformSnapshot snapshot = AndroidPlatformBridge.GetSnapshot();
            if(debugModeButton!=null)
            {
                bool enabled=Loop?.DebugWalkEnabled==true;
                var label=debugModeButton.GetComponentInChildren<Text>();
                if(label!=null)label.text=enabled?"걸음 디버그 ON":"걸음 디버그 OFF";
                if(virtualStepButton!=null)virtualStepButton.gameObject.SetActive(enabled);
            }
            AndroidDeviceSnapshot device = snapshot.device ?? new AndroidDeviceSnapshot();
            SensorServiceSnapshot service = snapshot.sensorService ?? new SensorServiceSnapshot();
            HealthConnectSnapshot health = snapshot.healthConnect ?? new HealthConnectSnapshot();
            RuntimePermissionSnapshot permissions = snapshot.runtimePermissions ?? new RuntimePermissionSnapshot();

            builder.Length = 0;
            builder.AppendLine("멘토링 데모 · 정식 출시 기능 아님");
            if(Loop!=null)builder.AppendLine(Loop.DebugWalkSummary);
            builder.AppendLine("가속도 실험을 수동으로 켜면 Android 수집 알림이 표시돼요.\n");
            builder.AppendLine("DEVICE");
            builder.Append(device.manufacturer).Append(' ').AppendLine(device.model);
            builder.Append("Android ").Append(device.androidVersion).Append(" (API ").Append(device.androidApi).AppendLine(")");
            builder.Append("App ").Append(device.appVersion).Append(" | Unity ").AppendLine(device.unityVersion);
            builder.Append("Input ").AppendLine(snapshot.inputMode);

            builder.AppendLine("\nPLATFORM");
            AppendStatus("Health Connect", health.status);
            AppendStatus("Samsung Health app", health.samsungHealthStatus);
            AppendStatus("Raw debug capture", service.running ? "AVAILABLE/SAMPLING" : service.serviceStatus);
            AppendStatus("Samsung-origin data in Health Connect", health.watchData?.status ?? "NO_DATA");
            builder.AppendLine("Daily acceleration: open Charts; Start/Stop controls the trial");
            if (health.permissionStatus == "PERMISSION_REQUIRED" || health.permissionStatus == "PERMISSION_DENIED")
                builder.AppendLine("Health access needed: use Health Perm below");
            else if (health.watchData?.status == "NO_DATA")
                builder.AppendLine("No Samsung data yet: Samsung Health > Settings > Health Connect, allow sharing, then sync");
            builder.AppendLine("Watch direct connection: NOT_CHECKED");
            builder.Append("Last query success: ").AppendLine(FormatInstant(health.lastSuccessfulRefreshEpochMs));
            AppendStatus("Activity Permission", permissions.activityRecognition);
            AppendStatus("Notification", permissions.notifications);

            builder.AppendLine("\nRAW SENSOR (2-second capture when Settings opens)");
            AppendSensor(service, "ACCELEROMETER", "Accel");
            AppendSensor(service, "GYROSCOPE", "Gyro");
            AppendSensor(service, "GRAVITY", "Gravity");
            AppendSensor(service, "LINEAR_ACCELERATION", "Linear Accel");
            AppendSensor(service, "PRESSURE", "Pressure");
            AppendSensor(service, "STEP_COUNTER", "Step Counter");
            builder.Append("Last Event: ").AppendLine(FormatAge(service.lastEventEpochMs));
            builder.Append("Dropped: ").Append(service.droppedPendingSamples).Append(" | malformed tail: ").Append(service.malformedTailLines).AppendLine();
            builder.Append("Buffer: ").Append(service.sampleCount)
                .Append(" memory / ").Append(service.persistedSampleCount).AppendLine(" persisted");

            builder.AppendLine("\nHEALTH");
            AppendHealth("Steps (all-source aggregate)", health.steps);
            AppendHealth("Steps (Samsung only)", health.samsungSteps);
            AppendHealth("Sleep session length", health.sleep);
            AppendHealth("Heart Rate", health.heartRate);
            AppendHealth("Exercise", health.exercise);
            builder.Append("Permissions: ").Append(health.permissionStatus)
                .Append(" (").Append(health.grantedPermissionCount).Append('/')
                .Append(health.requiredPermissionCount).AppendLine(")");

            builder.Append("\nACTION: ").Append(lastAction);
            if (!string.IsNullOrEmpty(snapshot.message))
            {
                builder.Append("\nBRIDGE: ").Append(snapshot.status).Append(' ').Append(snapshot.message);
            }
            output.text = builder.ToString();
        }

        private void AppendSensor(SensorServiceSnapshot service, string key, string label)
        {
            SensorCapabilitySnapshot sensor = FindSensor(service, key);
            builder.Append(label).Append(": ");
            if (sensor == null)
            {
                builder.AppendLine("NO_DATA");
                return;
            }

            builder.Append(sensor.status);
            if (sensor.values != null && sensor.values.Length > 0)
            {
                builder.Append(" [");
                for (int index = 0; index < sensor.values.Length; index++)
                {
                    if (index > 0) builder.Append(", ");
                    builder.Append(sensor.values[index].ToString("0.###", CultureInfo.InvariantCulture));
                }
                builder.Append(']');
            }
            builder.AppendLine();
        }

        private void AppendHealth(string label, HealthMetricSnapshot metric)
        {
            metric = metric ?? new HealthMetricSnapshot();
            builder.Append(label).Append(": ").Append(metric.status);
            if (metric.hasValue)
            {
                builder.Append(" | ").Append(metric.displayValue);
                if (!string.IsNullOrEmpty(metric.source)) builder.Append(" | ").Append(metric.source);
                builder.Append(" | measured ").Append(metric.measuredAtEpochMs>0?FormatAge(metric.measuredAtEpochMs):"not supplied");
            }
            builder.Append(" | query ").Append(metric.queryComplete ? "complete" : "incomplete");
            builder.AppendLine();
            builder.Append("  Window: ").Append(FormatInstant(metric.queryStartEpochMs)).Append(" ~ ")
                .Append(FormatInstant(metric.queryEndEpochMs)).Append(" ").AppendLine(metric.queryTimeZone);
            builder.Append("  Filter: ").Append(metric.sourceFilter).Append(" | queried ")
                .AppendLine(FormatInstant(metric.lastUpdatedEpochMs));
            if(metric.measuredAtEpochMs>0)builder.Append("  Measured: ").AppendLine(FormatInstant(metric.measuredAtEpochMs));
            if(metric.sourcePackages!=null && metric.sourcePackages.Length>0)
                builder.Append("  Sources: ").AppendLine(string.Join(", ",metric.sourcePackages));
        }

        private void AppendStatus(string label, string status)
        {
            builder.Append(label).Append(": ").AppendLine(string.IsNullOrEmpty(status) ? "NO_DATA" : status);
        }

        private static SensorCapabilitySnapshot FindSensor(SensorServiceSnapshot service, string key)
        {
            if (service?.sensors == null) return null;
            for (int index = 0; index < service.sensors.Length; index++)
            {
                if (service.sensors[index]?.key == key) return service.sensors[index];
            }
            return null;
        }

        private static string FormatInstant(long epochMs)
        {
            if(epochMs<=0)return "not supplied";
            try { return DateTimeOffset.FromUnixTimeMilliseconds(epochMs).ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss zzz"); }
            catch(ArgumentOutOfRangeException) { return "invalid timestamp"; }
        }

        private static string FormatAge(long epochMs)
        {
            if (epochMs <= 0) return "never";
            long seconds = Math.Max(0L, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - epochMs) / 1000L;
            if (seconds < 60) return seconds + " sec ago";
            if (seconds < 3600) return seconds / 60 + " min ago";
            if (seconds < 86400) return seconds / 3600 + " hr ago";
            return seconds / 86400 + " day ago";
        }

        private static string Describe(PlatformActionResult result)
        {
            if (result == null) return "ERROR: empty result";
            return result.status + (string.IsNullOrEmpty(result.message) ? string.Empty : ": " + result.message);
        }

        private static void AddListener(Button button, UnityEngine.Events.UnityAction action)
        {
            if (button != null) button.onClick.AddListener(action);
        }

        private static void RemoveListener(Button button, UnityEngine.Events.UnityAction action)
        {
            if (button != null) button.onClick.RemoveListener(action);
        }
    }
}
