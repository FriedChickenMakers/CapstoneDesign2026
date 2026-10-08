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
        private bool prepared;
        private Text healthStatus;
        private Text permissionStatus;
        private Text collectorStatus;
        private Text collectorDetails;
        private Text healthActionStatus;
        private Text permissionActionStatus;
        private Text collectorActionStatus;
        private GameObject diagnostics;
        private Button diagnosticsButton;
        public ScrollRect PrimaryScroll { get; private set; }
        private WeekOneQuestDemo Loop => transform.root.GetComponentInChildren<WeekOneQuestDemo>(true);

        private void Awake() => PrepareView();

        public void PrepareView()
        {
            if (prepared) return;
            prepared = true;
            Transform content = ConfigureScrollablePanel();

            // Keep the brand and title on the same 720-unit rows as Home.
            Transform heading = Stack(content, "Settings heading", 0, 0);
            var brand = Copy(heading, "마음 정원", 30, GardenUi.Green);
            brand.fontStyle = FontStyle.Bold;
            brand.alignment = TextAnchor.MiddleLeft;
            FixedHeight(brand.gameObject, 54);
            Spacer(heading, 40);
            var title = Copy(heading, "설정", GardenUi.TitleSize);
            title.fontStyle = FontStyle.Bold;
            title.alignment = TextAnchor.MiddleLeft;
            FixedHeight(title.gameObject, 78);

            Transform healthCard = Section(content, "Health records", "건강 기록", GardenUi.Pale);
            healthStatus = Copy(healthCard, "건강 기록 확인 중", GardenUi.BodySize, GardenUi.Muted);
            Control(healthCard, "심박 차트 보기", OpenCharts, true);
            Place(refreshHealthButton, healthCard, "건강 기록 새로고침");
            healthActionStatus = ActionLine(healthCard);

            Transform permissions = Section(content, "Permissions", "앱 연결 및 권한");
            permissionStatus = Copy(permissions, "권한 확인 중", GardenUi.BodySize, GardenUi.Muted);
            Place(sensorPermissionButton, permissions, "활동 권한 확인");
            Place(healthPermissionButton, permissions, "건강 데이터 권한 확인");
            permissionActionStatus = ActionLine(permissions);

            Transform collector = Section(content, "Acceleration collection", "가속도 수집");
            collectorStatus = Copy(collector, "수집 상태 확인 중", GardenUi.HeadingSize, GardenUi.Green);
            collectorStatus.fontStyle = FontStyle.Bold;
            collectorDetails = Copy(collector, "", GardenUi.BodySize, GardenUi.Muted);
            Copy(collector, "매분 5초 · 최대 48시간\n화면 꺼짐 중 수집 · 배터리 사용", GardenUi.BodySize, GardenUi.Muted);
            Place(startServiceButton, collector, "가속도 수집 켜기", true);
            Place(stopServiceButton, collector, "가속도 수집 끄기");
            collectorActionStatus = ActionLine(collector);
            diagnosticsButton = Control(content, "상세 진단 보기  +", ToggleDiagnostics);
            diagnostics = Section(content, "Detailed diagnostics", "상세 진단").gameObject;
            if (Debug.isDebugBuild || Application.isEditor)
            {
                debugModeButton = Control(diagnostics.transform, "걸음 디버그 OFF", ToggleDebugWalk);
                virtualStepButton = Control(diagnostics.transform, "가상 걸음 +1", AddVirtualStep);
            }
            if (output == null) output = Copy(diagnostics.transform, "", GardenUi.CaptionSize);
            else output.transform.SetParent(diagnostics.transform, false);
            ConfigureCopy(output, GardenUi.CaptionSize, GardenUi.Ink);
            output.gameObject.SetActive(true);
            diagnostics.SetActive(false);

            AddListener(startServiceButton, StartService);
            AddListener(stopServiceButton, StopService);
            AddListener(sensorPermissionButton, RequestSensorPermissions);
            AddListener(healthPermissionButton, RequestHealthPermissions);
            AddListener(refreshHealthButton, RefreshHealth);
            RefreshText();
        }

        private Transform ConfigureScrollablePanel()
        {
            var rect = GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            var background = GetComponent<Image>();
            if (background != null) { background.color = Color.clear; background.raycastTarget = false; }
            var oldWidth = GetComponent<GardenContentWidth>();
            if (oldWidth != null) oldWidth.enabled = false;

            var viewport = GardenUi.Box(transform, "Settings viewport", 0, .12f, 1, .88f, Color.clear);
            GardenUi.ConstrainWidth(viewport);
            viewport.AddComponent<RectMask2D>();
            PrimaryScroll = viewport.AddComponent<ScrollRect>();
            PrimaryScroll.viewport = (RectTransform)viewport.transform;
            PrimaryScroll.horizontal = false;
            PrimaryScroll.vertical = true;
            PrimaryScroll.movementType = ScrollRect.MovementType.Clamped;
            PrimaryScroll.scrollSensitivity = 48;
            Transform content = Stack(viewport.transform, "Settings content", 24, 48);
            content.GetComponent<VerticalLayoutGroup>().padding.top = 24;
            var contentRect = (RectTransform)content;
            contentRect.anchorMin = new Vector2(0, 1);
            contentRect.anchorMax = Vector2.one;
            contentRect.pivot = new Vector2(.5f, 1);
            contentRect.anchoredPosition = Vector2.zero;
            contentRect.sizeDelta = Vector2.zero;
            content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            PrimaryScroll.content = contentRect;
            GardenUi.AddScrollIndicator(PrimaryScroll);
            return content;
        }

        private static Transform Stack(Transform parent, string name, int spacing, int padding)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var layout = go.AddComponent<VerticalLayoutGroup>();
            layout.spacing = spacing;
            layout.padding = new RectOffset(padding, padding, padding, padding);
            layout.childAlignment = TextAnchor.UpperLeft;
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            return go.transform;
        }

        private static void FixedHeight(GameObject item, float height)
        {
            var layout = item.GetComponent<LayoutElement>() ?? item.AddComponent<LayoutElement>();
            layout.minHeight = layout.preferredHeight = height;
            layout.flexibleHeight = 0;
        }

        private static void Spacer(Transform parent, float height)
        {
            var spacer = new GameObject("Header spacing", typeof(RectTransform));
            spacer.transform.SetParent(parent, false);
            FixedHeight(spacer, height);
        }

        private static Transform Section(Transform parent, string name, string heading, Color? tint = null)
        {
            Transform section = Stack(parent, name, 16, 24);
            var image = section.gameObject.AddComponent<Image>();
            GardenUi.Round(image, tint ?? Color.white, true);
            image.raycastTarget = false;
            var label = Copy(section, heading, GardenUi.HeadingSize);
            label.fontStyle = FontStyle.Bold;
            return section;
        }

        private static Text Copy(Transform parent, string text, int size, Color? color = null)
        {
            var label = GardenUi.Label(parent, text, 0, 0, 1, 1, size);
            ConfigureCopy(label, size, color ?? GardenUi.Ink);
            return label;
        }

        private static void ConfigureCopy(Text text, int size, Color color)
        {
            text.font = GardenUi.ResolveFont();
            text.fontSize = size;
            text.fontStyle = FontStyle.Normal;
            text.color = color;
            text.alignment = TextAnchor.UpperLeft;
            text.lineSpacing = 1.15f;
            text.resizeTextForBestFit = false;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.raycastTarget = false;
            var fitter = text.GetComponent<ContentSizeFitter>();
            if (fitter != null) fitter.enabled = false;
        }

        private static Button Control(Transform parent, string text, Action action, bool primary = false)
        {
            var button = GardenUi.Button(parent, text, 0, 0, 1, 1, action, primary);
            Place(button, parent, text, primary);
            return button;
        }

        private static void Place(Button button, Transform parent, string text, bool primary = false)
        {
            if (button == null) return;
            button.transform.SetParent(parent, false);
            var layout = button.GetComponent<LayoutElement>() ?? button.gameObject.AddComponent<LayoutElement>();
            layout.minHeight = layout.preferredHeight = 80;
            layout.flexibleHeight = 0;
            var label = button.GetComponentInChildren<Text>(true);
            label.text = text;
            label.fontSize = GardenUi.ButtonSize;
            label.resizeTextForBestFit = false;
            label.alignment = TextAnchor.MiddleCenter;
            label.rectTransform.anchorMin = new Vector2(.04f, 0);
            label.rectTransform.anchorMax = new Vector2(.96f, 1);
            label.rectTransform.offsetMin = label.rectTransform.offsetMax = Vector2.zero;
            GardenUi.StyleButton(button, primary);
        }

        public void ToggleDiagnostics()
        {
            bool expanded = !diagnostics.activeSelf;
            diagnostics.SetActive(expanded);
            diagnosticsButton.GetComponentInChildren<Text>(true).text = expanded ? "상세 진단 접기  −" : "상세 진단 보기  +";
            LayoutRebuilder.MarkLayoutForRebuild(PrimaryScroll.content);
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
            ShowAction(AndroidPlatformBridge.StartBackgroundSensorService(), "가속도 수집 시작을 요청했어요.", collectorActionStatus);
            RefreshText();
        }

        private void StopService()
        {
            ShowAction(AndroidPlatformBridge.StopBackgroundSensorService(), "가속도 수집 중지를 요청했어요.", collectorActionStatus);
            RefreshText();
        }

        private void RequestSensorPermissions()
        {
            ShowAction(AndroidPlatformBridge.RequestSensorPermissions(), "활동 권한을 확인해 주세요.", permissionActionStatus);
            RefreshText();
        }

        private void RequestHealthPermissions()
        {
            HealthConnectSnapshot health = AndroidPlatformBridge.GetSnapshot().healthConnect;
            ShowAction(health?.permissionStatus == "PERMISSION_DENIED"
                ? AndroidPlatformBridge.OpenHealthPermissionSettings()
                : AndroidPlatformBridge.RequestHealthConnectPermissions(), "건강 데이터 권한을 확인해 주세요.", permissionActionStatus);
            RefreshText();
        }

        private void RefreshHealth()
        {
            ShowAction(AndroidPlatformBridge.RefreshHealthData(), "건강 기록 새로고침을 요청했어요.", healthActionStatus);
            Loop?.RequestStepSyncSoon(true,true);
            nextHealthRefresh = Time.unscaledTime + HealthRefreshSeconds;
            RefreshText();
        }

        private void OpenCharts()
        {
            ShowAction(AndroidPlatformBridge.OpenSensorHistory(), "심박 차트를 열었어요.", healthActionStatus);
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
                var label=debugModeButton.GetComponentInChildren<Text>(true);
                if(label!=null)label.text=enabled?"걸음 디버그 ON":"걸음 디버그 OFF";
                if(virtualStepButton!=null)
                {
                    virtualStepButton.interactable=enabled;
                    virtualStepButton.GetComponentInChildren<Text>(true).color=enabled?GardenUi.Green:GardenUi.Muted;
                }
            }
            AndroidDeviceSnapshot device = snapshot.device ?? new AndroidDeviceSnapshot();
            SensorServiceSnapshot service = snapshot.sensorService ?? new SensorServiceSnapshot();
            HealthConnectSnapshot health = snapshot.healthConnect ?? new HealthConnectSnapshot();
            RuntimePermissionSnapshot permissions = snapshot.runtimePermissions ?? new RuntimePermissionSnapshot();
            DailyAccelerationSnapshot daily = snapshot.dailyAcceleration ?? new DailyAccelerationSnapshot();
            RefreshSummary(snapshot, health, permissions, daily);

            builder.Length = 0;
            builder.AppendLine("멘토링 데모 · 정식 출시 기능 아님");
            if(Loop!=null)builder.AppendLine(Loop.DebugWalkSummary);
            builder.AppendLine("가속도 실험을 수동으로 켜면 Android 수집 알림이 표시돼요.\n");
            builder.Append("가속도 수집: ").AppendLine(daily.status == "RUNNING" ? "수집 중" :
                daily.status == "STOPPED" ? "중지됨" : daily.status == "INTERRUPTED" ? "상태 갱신 끊김 · 수집 확인 필요" : "수집 상태 기록 없음");
            if (!string.IsNullOrEmpty(daily.reason)) builder.Append("상태 사유: ").AppendLine(DailyReason(daily.reason));
            builder.Append("마지막 저장: ").AppendLine(FormatInstant(daily.lastWriteEpochMs));
            builder.AppendLine("매분 5초 · 최대 48시간 · 화면 꺼짐 중에도 배터리를 사용해요.\n");
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
            builder.AppendLine("Heart chart: hourly means from Health Connect. Trial On/Off controls acceleration separately.");
            if (health.permissionStatus == "PERMISSION_REQUIRED" || health.permissionStatus == "PERMISSION_DENIED")
                builder.AppendLine("건강 데이터 권한이 필요해요. 위의 앱 연결 및 권한에서 확인해 주세요.");
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
            AppendHealth("Steps (preferred source)", health.steps);
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

        private void RefreshSummary(AndroidPlatformSnapshot snapshot, HealthConnectSnapshot health,
            RuntimePermissionSnapshot permissions, DailyAccelerationSnapshot daily)
        {
            bool simulated = snapshot.inputMode == "MOCK" || snapshot.inputMode == "REPLAY";
            string mode = snapshot.inputMode == "MOCK" ? "예시" : "재생";
            string healthSummary;
            bool healthNeedsAttention = false;
            if (health.refreshing) healthSummary = "건강 기록 새로고침 중";
            else if (health.permissionStatus == "PERMISSION_DENIED" || health.permissionStatus == "PERMISSION_REQUIRED")
            {
                healthSummary = "건강 데이터 권한 필요";
                healthNeedsAttention = true;
            }
            else if (health.status == "STALE")
            {
                healthSummary = "기록 갱신 필요";
                healthNeedsAttention = true;
            }
            else if (health.status == "ERROR")
            {
                healthSummary = "건강 기록 조회 실패";
                healthNeedsAttention = true;
            }
            else if (health.status == "DISCONNECTED")
            {
                healthSummary = "건강 데이터 연결 끊김";
                healthNeedsAttention = true;
            }
            else if (health.status == "PARTIAL") healthSummary = "일부 건강 기록만 확인됨";
            else if (health.status == "NO_DATA") healthSummary = "동기화된 기록 없음";
            else if (simulated) healthSummary = "건강 기록";
            else if (health.status != "AVAILABLE")
            {
                healthSummary = "건강 데이터 연결 필요";
                healthNeedsAttention = true;
            }
            else healthSummary = health.lastSuccessfulRefreshEpochMs > 0 ? "기록 연결됨" : "동기화된 기록 없음";
            if (simulated) healthSummary = mode + " · " + healthSummary;
            else if (!health.refreshing && health.lastSuccessfulRefreshEpochMs > 0)
                healthSummary += " · " + CompactInstant(health.lastSuccessfulRefreshEpochMs) + " 확인";
            healthStatus.text = healthSummary;
            healthStatus.color = healthNeedsAttention ? GardenUi.Warning : GardenUi.Muted;
            permissionStatus.text = simulated
                ? mode + " 모드 · 실제 권한 사용 안 함"
                : "활동 " + PermissionLabel(permissions.activityRecognition) +
                    " · 건강 데이터 " + PermissionLabel(health.permissionStatus);
            collectorStatus.text = daily.status == "RUNNING" ? "수집 중" :
                daily.status == "STOPPED" ? "수집 중지됨" : daily.status == "INTERRUPTED" ? "수집 상태 확인 필요" : "수집 기록 없음";
            if (daily.lastWriteEpochMs > 0)
                collectorStatus.text += " · " + CompactInstant(daily.lastWriteEpochMs) + " 저장";
            collectorStatus.color = daily.status == "INTERRUPTED" ? GardenUi.Warning : GardenUi.Green;
            bool explainStop = !string.IsNullOrEmpty(daily.reason) && daily.reason != "USER_START" &&
                daily.reason != "USER_STOP" && daily.reason != "PROCESS_RESTART" && daily.reason != "NO_STATUS_RECORD";
            collectorDetails.text = explainStop ? DailyReason(daily.reason) : string.Empty;
            collectorDetails.gameObject.SetActive(explainStop);
        }

        private static Text ActionLine(Transform parent)
        {
            var label = Copy(parent, "", GardenUi.BodySize, GardenUi.Green);
            label.gameObject.SetActive(false);
            return label;
        }

        private void ShowAction(PlatformActionResult result, string message, Text label)
        {
            lastAction = Describe(result);
            if (result != null && result.ParsedStatus == PlatformStatus.Available)
            {
                label.text = string.Empty;
                label.gameObject.SetActive(false);
                return;
            }
            string actionMessage;
            if (result == null || result.ParsedStatus == PlatformStatus.Error)
                actionMessage = "요청을 완료하지 못했어요.\n상세 진단에서 오류를 확인해 주세요.";
            else if (result.ParsedStatus == PlatformStatus.Unsupported || result.ParsedStatus == PlatformStatus.ServiceUnavailable)
                actionMessage = "현재 기기에서는 이 기능을 사용할 수 없어요.";
            else actionMessage = message;
            label.text = actionMessage;
            label.gameObject.SetActive(true);
        }

        private static string PermissionLabel(string status)
        {
            switch (status)
            {
                case "GRANTED":
                case "AVAILABLE": return "허용";
                case "PERMISSION_DENIED":
                case "DENIED": return "허용 필요";
                case "PERMISSION_REQUIRED": return "확인 필요";
                case "UNSUPPORTED": return "지원 안 함";
                default: return "확인 전";
            }
        }

        private static string CompactInstant(long epochMs)
        {
            try
            {
                var instant = DateTimeOffset.FromUnixTimeMilliseconds(epochMs).ToLocalTime();
                return instant.ToString(instant.Date == DateTimeOffset.Now.Date ? "HH:mm" : "M/d HH:mm");
            }
            catch (ArgumentOutOfRangeException) { return "시간 확인 필요"; }
        }

        private static string DailyReason(string reason)
        {
            switch (reason)
            {
                case "USER_START": return "사용자가 수집 시작";
                case "USER_STOP": return "사용자가 수집 중지";
                case "DURATION_LIMIT": return "48시간 수집 완료 · 다시 켜면 새 수집 시작";
                case "LOW_BATTERY": return "미연결 상태에서 배터리 20% 미만 · 충전 후 다시 켜세요";
                case "WRITE_FAILED": return "기록 저장 실패 · 저장 공간 확인 필요";
                case "SENSOR_UNAVAILABLE": return "가속도 센서 없음";
                case "SENSOR_REGISTRATION_FAILED": return "센서 시작 실패 · 권한 확인 후 다시 켜세요";
                case "PROCESS_RESTART": return "서비스 재시작 후 수집 재개";
                case "SERVICE_DESTROYED": return "서비스 종료 · 다시 켜면 수집 재개";
                case "HEARTBEAT_EXPIRED": return "2분 이상 상태 갱신 없음 · 다시 켜서 확인하세요";
                case "NO_STATUS_RECORD": return "이전 버전에는 중단 사유가 기록되지 않았어요";
                default: return reason;
            }
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
            if(!string.IsNullOrEmpty(metric.sourceBreakdown))
                builder.Append("  Candidate totals (not added): ").AppendLine(metric.sourceBreakdown);
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
