using UnityEngine;

namespace CapstoneDesign.Runtime
{
    /// <summary>
    /// Reads the optional Android Intent extra used by hardware CI. The first
    /// mockup has one scene, so the mode is logged and exposed for later
    /// deterministic scene entry points without adding native Java code.
    /// </summary>
    [DefaultExecutionOrder(-200)]
    public sealed class AndroidTestMode : MonoBehaviour
    {
        public const string DefaultMode = "GardenPreview";

        public string ActiveMode { get; private set; } = DefaultMode;

        private void Awake()
        {
            ActiveMode = ReadRequestedMode();
            ConfigurePlatformInput();
#if UNITY_ANDROID && !UNITY_EDITOR
            if(ActiveMode=="SensorSummaryExperiment")
            {
                using(var player=new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                using(var activity=player.GetStatic<AndroidJavaObject>("currentActivity"))
                using(var bridge=new AndroidJavaClass("com.capstonedesign2026.platform.AndroidPlatformBridge"))
                {
                    long window=long.Parse(ReadIntentString("summaryWindowMs"));
                    long duration=long.Parse(ReadIntentString("summaryDurationMs"));
                    bridge.CallStatic<string>("startSensorSummaryExperiment",activity,window,duration,ReadIntentString("summaryRunId"));
                }
            }
            else if (ActiveMode == DefaultMode && AndroidPlatformBridge.InputMode == PlatformInputMode.Live)
            {
                // Retire the old implicit 48-hour trial. Explicitly started trials remain enabled.
                AndroidPlatformBridge.StopLegacyAutoAccelerationTrial();
            }
#endif
            Debug.Log("Mockup test mode: " + ActiveMode);
        }

        private static void ConfigurePlatformInput()
        {
#if UNITY_EDITOR
            if (!string.IsNullOrEmpty(System.Environment.GetEnvironmentVariable("CAPSTONE_SMOKE_SAVE_ROOT")))
            {
                AndroidPlatformBridge.UseMockProvider(); return;
            }
#endif
            string requested = ReadIntentString("platformInputMode");
            if (string.Equals(requested, "MOCK", System.StringComparison.OrdinalIgnoreCase))
            {
                AndroidPlatformBridge.UseMockProvider();
                return;
            }

            if (string.Equals(requested, "REPLAY", System.StringComparison.OrdinalIgnoreCase))
            {
                string path = ReadIntentString("platformReplayPath");
                PlatformActionResult result = AndroidPlatformBridge.UseReplayProvider(path);
                if (result.ParsedStatus == PlatformStatus.Available) return;
                Debug.LogWarning("Replay mode could not start: " + result.message);
            }

            AndroidPlatformBridge.UseLiveProvider();
        }

        private static string ReadRequestedMode()
        {
            string requested = ReadIntentString("testScene");
            return string.IsNullOrEmpty(requested) ? DefaultMode : requested;
        }

        private static string ReadIntentString(string key)
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                using (AndroidJavaClass unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                using (AndroidJavaObject activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity"))
                using (AndroidJavaObject intent = activity.Call<AndroidJavaObject>("getIntent"))
                {
                    return intent.Call<string>("getStringExtra", key);
                }
            }
            catch (System.Exception exception)
            {
                Debug.LogWarning("Intent extra read failed for " + key + ": " + exception.Message);
            }
#endif
            return string.Empty;
        }
    }
}
