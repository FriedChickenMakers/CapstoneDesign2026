#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CapstoneDesign.Runtime.LocalState;
using UnityEngine;

namespace CapstoneDesign.Runtime
{
    // Only attached to the temporary scene built by VisualAuditBuild. The distinct
    // package sandbox and in-memory store keep device saves and preferences private.
    [DefaultExecutionOrder(-300)]
    public sealed class VisualAuditBootstrap : MonoBehaviour
    {
        public const string Package = "com.capstonedesign2026.visualaudit";
        WeekOneQuestDemo ui;
        MockupNavigation navigation;
        GardenStateService service;
        VisualAuditTraversal.Clock clock;
        string output;
        bool formsOnly, choicesOnly, nativeOnly;

        void Awake()
        {
            if (Application.identifier != Package) { enabled = false; return; }
            AndroidPlatformBridge.UseMockProvider();
#if UNITY_ANDROID && !UNITY_EDITOR
            using (var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
            using (var activity = player.GetStatic<AndroidJavaObject>("currentActivity"))
            using (var intent = activity.Call<AndroidJavaObject>("getIntent"))
            {
                string section = intent.Call<string>("getStringExtra","visualAuditSections");
                formsOnly = section == "forms";
                choicesOnly = section == "choices";
                nativeOnly = section == "native";
            }
#endif
            output = Path.Combine(Application.persistentDataPath,nativeOnly?"visual-audit-native":formsOnly?"visual-audit-forms":choicesOnly?"visual-audit-choices":"visual-audit");
            Directory.CreateDirectory(output);
            if (nativeOnly) return;
            clock = new VisualAuditTraversal.Clock();
            var initial = new GardenState();
            VisualAuditShellCases.Seed(initial);
            var store = new VisualAuditTraversal.Store();
            store.Save(initial);
            service = new GardenStateService(store,clock,
                new DemoBalanceConfig { VisitorChancePercent = 0 },new LegacySnapshot { Nutrient = 500 });
            navigation = FindFirstObjectByType<MockupNavigation>(FindObjectsInactive.Include);
            ui = navigation.activitiesPanel.GetComponent<WeekOneQuestDemo>();
            ui.Initialize(service,true);
        }

        IEnumerator Start()
        {
            if (!enabled) yield break;
            yield return null;
#if UNITY_ANDROID && !UNITY_EDITOR
            if (nativeOnly)
            {
                if (Application.identifier != Package || AndroidPlatformBridge.InputMode != PlatformInputMode.Mock)
                {
                    ReportFailure(new InvalidOperationException("Native visual audit requires the isolated package and MOCK input mode"));
                    yield break;
                }
                if (!TryOutput(() => File.WriteAllText(Path.Combine(output,"NATIVE_READY.txt"),
                    "Isolated native audit command listener ready; no UI traversal or automatic history launch"))) yield break;
                yield return ListenForNativeCommands();
                yield break;
            }
#endif
            if (service == null) yield break;
            navigation.ShowActivities();
            IEnumerable<string> routes = formsOnly ? VisualAuditFormCases.Cases(navigation,ui,service,clock)
                : choicesOnly ? VisualAuditTraversal.ChoiceStates(ui,service,clock)
                : VisualAuditTraversal.Cases(ui,service,clock)
                .Concat(VisualAuditTraversal.AdditionalStates(ui,service,clock))
                .Concat(VisualAuditShellCases.Cases(navigation,ui,service,clock))
                .Concat(VisualAuditSupplementCases.Cases(ui,service,clock))
                .Concat(VisualAuditFormCases.Cases(navigation,ui,service,clock));
            var cases = routes.GetEnumerator();
            int count = 0;
            StreamWriter manifest = null;
            using (cases)
            {
                if (!TryOutput(() => manifest = new StreamWriter(Path.Combine(output,"manifest.tsv")))) yield break;
                using (manifest)
                {
                    if (!TryOutput(() => manifest.WriteLine("image\twidth\theight\tcase"))) yield break;
                    while (true)
                    {
                        bool more;
                        try { more = cases.MoveNext(); }
                        catch (Exception error)
                        {
                            ReportFailure(error); yield break;
                        }
                        if (!more) break;
                        yield return new WaitForSecondsRealtime(.30f);
                        Canvas.ForceUpdateCanvases();
                        yield return new WaitForEndOfFrame();
                        if (cases.Current == null) continue;
                        string file = (++count).ToString("D4")+"-"+cases.Current+".png";
                        if (cases.Current.EndsWith("keyboard-open",StringComparison.Ordinal))
                        {
                            // Unity's framebuffer excludes the Android IME. Pause so the
                            // explicit-target host can record the composed system screen.
                            string request = Path.Combine(output,"awaiting-system-capture.txt");
                            if (!TryOutput(() => File.WriteAllText(request,file))) yield break;
                            while (File.Exists(request)) yield return new WaitForSecondsRealtime(.2f);
                            yield return new WaitForEndOfFrame();
                        }
                        if (!TryOutput(() => Capture(file,cases.Current,count,manifest))) yield break;
                    }
                }
            }
            navigation.ShowIsland();
            if (!TryOutput(() => File.WriteAllText(Path.Combine(output,"COMPLETE.txt"),
                "Synthetic isolated device visual audit: "+count+" captures"))) yield break;
#if UNITY_ANDROID && !UNITY_EDITOR
            // Read-only native history screen in the same isolated app sandbox.
            // No Health Connect grant or sensor collection is requested here.
            if (!TryOutput(() => OpenNativeAuditView("sensor-history"))) yield break;
            yield return ListenForNativeCommands();
#endif
        }

#if UNITY_ANDROID && !UNITY_EDITOR
        IEnumerator ListenForNativeCommands()
        {
            string commandPath = Path.Combine(output,"native-command.txt");
            while (enabled)
            {
                if (File.Exists(commandPath) && !TryOutput(() => HandleNativeCommand(commandPath))) yield break;
                yield return new WaitForSecondsRealtime(.2f);
            }
        }

        void HandleNativeCommand(string path)
        {
            string command = File.ReadAllText(path).Trim();
            string resultPath = Path.Combine(output,"native-result.txt");
            File.Delete(resultPath);
            string result = OpenNativeAuditView(command);
            File.WriteAllText(resultPath,result);
            // The host waits for removal as well as the result, so an earlier
            // acknowledgement cannot be mistaken for this command completing.
            File.Delete(path);
        }

        string OpenNativeAuditView(string command)
        {
            if (Application.identifier != Package || AndroidPlatformBridge.InputMode != PlatformInputMode.Mock
                || (command != "time-picker" && command != "sensor-history"))
                return JsonUtility.ToJson(new PlatformActionResult { status = "UNSUPPORTED", message = "Unsupported isolated visual audit command" });
            using (var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
            using (var activity = player.GetStatic<AndroidJavaObject>("currentActivity"))
            using (var bridge = new AndroidJavaClass("com.capstonedesign2026.platform.AndroidPlatformBridge"))
            {
                string initialized = bridge.CallStatic<string>("initialize",activity,Application.unityVersion,"MOCK");
                var initialization = JsonUtility.FromJson<PlatformActionResult>(initialized);
                if (initialization == null || initialization.ParsedStatus != PlatformStatus.Available) return initialized;
                // Opening the picker only presents its UI. The host cancels it;
                // neither command grants permissions or enables notifications.
                return command == "time-picker" ? bridge.CallStatic<string>("showActivityNotificationTimePicker")
                    : bridge.CallStatic<string>("openSensorHistory",activity);
            }
        }
#endif

        void Capture(string file,string label,int count,StreamWriter manifest)
        {
            Texture2D screenshot = null;
            try
            {
                int width = Screen.width, height = Screen.height;
                screenshot = new Texture2D(width,height,TextureFormat.RGB24,false);
                screenshot.ReadPixels(new Rect(0,0,width,height),0,0);
                screenshot.Apply();
                File.WriteAllBytes(Path.Combine(output,file),screenshot.EncodeToPNG());
                manifest.WriteLine(file+"\t"+width+"\t"+height+"\t"+label);
                manifest.Flush();
                File.WriteAllText(Path.Combine(output,"progress.txt"),count+" "+label);
            }
            finally
            {
                if (screenshot != null) Destroy(screenshot);
            }
        }

        // Keep try/catch outside iterator yield blocks. Storage failure can also
        // prevent the marker itself, so retain the original error in logcat.
        bool TryOutput(Action operation)
        {
            try { operation(); return true; }
            catch (Exception error) { ReportFailure(error); return false; }
        }

        void ReportFailure(Exception error)
        {
            Debug.LogException(error);
            try { File.WriteAllText(Path.Combine(output,"FAILED.txt"),error.ToString()); }
            catch (Exception markerError) { Debug.LogException(markerError); }
        }
    }
}
#endif
