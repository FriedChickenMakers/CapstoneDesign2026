using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using CapstoneDesign.Runtime;
using CapstoneDesign.Runtime.LocalState;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;

namespace CapstoneDesign.EditorTools
{
    // Consumes exactly the same route driver as the separate visual-audit APK.
    // All mutations are confined to injected synthetic memory state.
    public static class FullChoiceVisualCapture
    {
        public static void Capture()
        {
            string output = Environment.GetEnvironmentVariable("CAPSTONE_ARTIFACTS");
            VisualAuditTraversal.Require(!string.IsNullOrWhiteSpace(output), "Set a fresh CAPSTONE_ARTIFACTS directory.");
            VisualAuditTraversal.Require(SystemInfo.graphicsDeviceType != GraphicsDeviceType.Null, "Visual audit requires a graphics renderer.");
            Directory.CreateDirectory(output);
            string[] sections = (Environment.GetEnvironmentVariable("CAPSTONE_VISUAL_AUDIT_SECTIONS") ?? "missions,shell,supplement,forms").Split(',');
            string[] filters = (Environment.GetEnvironmentVariable("CAPSTONE_VISUAL_AUDIT_FILTER") ?? "").Split(',').Where(s => s.Length > 0).ToArray();
            string sizes = Environment.GetEnvironmentVariable("CAPSTONE_VISUAL_AUDIT_SIZES") ?? "1200x2000,1080x2400";
            var manifest = new StringBuilder("image,width,height,case,screen\n");
            var metrics = new StringBuilder("image,path,text,fontSize,preferredWidth,preferredHeight,width,height,horizontalOverflow,verticalOverflow\n");
            var events = new List<string> { "Synthetic memory state; mocked platform; Editor rendered evidence; not a physical-device result." };
            int count = 0;
            double? hour = GardenVisualClock.OverrideHour;
            try
            {
                foreach (string dimensions in sizes.Split(','))
                {
                    var parts = dimensions.Trim().Split('x');
                    VisualAuditTraversal.Require(parts.Length == 2, "Use WIDTHxHEIGHT capture sizes.");
                    var size = new Vector2Int(int.Parse(parts[0], CultureInfo.InvariantCulture), int.Parse(parts[1], CultureInfo.InvariantCulture));
                    VisualAuditTraversal.Require(size.x >= 320 && size.y >= 320 && size.x <= 4096 && size.y <= 4096, "Unsupported capture dimensions.");
                    EditorSceneManager.OpenScene("Assets/Scenes/MockupMain.unity");
                    AndroidPlatformBridge.UseMockProvider(); GardenVisualClock.SetOverrideMinutes(720);
                    var nav = UnityEngine.Object.FindFirstObjectByType<MockupNavigation>(FindObjectsInactive.Include);
                    VisualAuditTraversal.Require(nav != null, "Integrated scene navigation missing.");
                    var ui = nav.activitiesPanel.GetComponent<WeekOneQuestDemo>();
                    var clock = new VisualAuditTraversal.Clock();
                    var balance = new DemoBalanceConfig { VisitorChancePercent = 0 };
                    var initial = new GardenState(); VisualAuditShellCases.Seed(initial);
                    var store = new VisualAuditTraversal.Store(); store.Save(initial);
                    using var service = new GardenStateService(store, clock, balance, new LegacySnapshot { Nutrient = 500 });
                    ui.Balance = balance; ui.Initialize(service, true); nav.ShowActivities();
                    string folder = Path.Combine(output, "visual", size.x + "x" + size.y);
                    Directory.CreateDirectory(folder);
                    // Establish requested geometry before any route-control action.
                    string warmup = Path.Combine(folder, "initial.png"); LocalLoopPreview.Render(size, warmup); File.Delete(warmup);
                    IEnumerable<string> cases = Enumerable.Empty<string>();
                    if (sections.Contains("missions")) cases = cases.Concat(VisualAuditTraversal.Cases(ui, service, clock))
                        .Concat(VisualAuditTraversal.AdditionalStates(ui, service, clock));
                    if (sections.Contains("choices")) cases = cases.Concat(VisualAuditTraversal.ChoiceStates(ui, service, clock));
                    if (sections.Contains("shell")) cases = cases.Concat(VisualAuditShellCases.Cases(nav, ui, service, clock));
                    if (sections.Contains("supplement")) cases = cases.Concat(VisualAuditSupplementCases.Cases(ui, service, clock));
                    if (sections.Contains("forms")) cases = cases.Concat(VisualAuditFormCases.Cases(nav, ui, service, clock));
                    var seen = new HashSet<string>();
                    foreach (string label in cases)
                    {
                        VisualAuditTraversal.Settle();
                        if (string.IsNullOrEmpty(label)) continue;
                        VisualAuditTraversal.Require(seen.Add(label), "Duplicate audit capture name: " + label);
                        events.Add(size.x + "x" + size.y + " " + label + " " + ui.CurrentScreen);
                        if (filters.Length > 0 && !filters.Any(label.StartsWith)) continue;
                        string path = Path.Combine(folder, label + ".png");
                        string before = StateCodec.Encode(service.Snapshot);
                        bool resume = Environment.GetEnvironmentVariable("CAPSTONE_VISUAL_AUDIT_RESUME") == "1";
                        if (!resume || !File.Exists(path)) LocalLoopPreview.Render(size, path);
                        VisualAuditTraversal.Require(before == StateCodec.Encode(service.Snapshot), "Capture mutated synthetic state: " + label);
                        string relative = size.x + "x" + size.y + "/" + label + ".png";
                        manifest.Append(Csv(relative)).Append(',').Append(size.x).Append(',').Append(size.y).Append(',').Append(Csv(label)).Append(',').Append(Csv(ui.CurrentScreen)).AppendLine();
                        foreach (var text in UnityEngine.Object.FindObjectsByType<Text>(FindObjectsSortMode.None))
                        {
                            if (!text.isActiveAndEnabled || string.IsNullOrWhiteSpace(text.text)) continue;
                            var rect = text.rectTransform.rect;
                            metrics.Append(Csv(relative)).Append(',').Append(Csv(PathOf(text.transform))).Append(',').Append(Csv(text.text)).Append(',')
                                .Append(text.fontSize).Append(',').Append(Number(text.preferredWidth)).Append(',').Append(Number(text.preferredHeight)).Append(',')
                                .Append(Number(rect.width)).Append(',').Append(Number(rect.height)).Append(',').Append(text.horizontalOverflow).Append(',').Append(text.verticalOverflow).AppendLine();
                        }
                        count++; Debug.Log("FULL_CHOICE_CAPTURE " + count + " " + relative);
                        if (count % 50 == 0)
                        {
                            File.WriteAllText(Path.Combine(output, "capture-manifest.csv"), manifest.ToString(), Encoding.UTF8);
                            File.WriteAllText(Path.Combine(output, "text-metrics.csv"), metrics.ToString(), Encoding.UTF8);
                            File.WriteAllLines(Path.Combine(output, "traversal.txt"), events);
                        }
                    }
                }
                events.Add("FULL_CHOICE_CAPTURE_PASS images=" + count);
                Debug.Log("FULL_CHOICE_CAPTURE_PASS images=" + count);
            }
            catch (Exception error) { events.Add("FAIL " + error); throw; }
            finally
            {
                if (hour.HasValue) GardenVisualClock.SetOverrideMinutes((int)Math.Round(hour.Value * 60)); else GardenVisualClock.UseSystemTime();
                File.WriteAllText(Path.Combine(output, "capture-manifest.csv"), manifest.ToString(), Encoding.UTF8);
                File.WriteAllText(Path.Combine(output, "text-metrics.csv"), metrics.ToString(), Encoding.UTF8);
                File.WriteAllLines(Path.Combine(output, "traversal.txt"), events);
            }
        }
        static string PathOf(Transform value) => value.parent == null ? value.name : PathOf(value.parent) + "/" + value.name;
        static string Number(float value) => value.ToString("0.##", CultureInfo.InvariantCulture);
        static string Csv(string value) => "\"" + (value ?? "").Replace("\"", "\"\"").Replace("\n", " / ").Replace("\r", "") + "\"";
    }
}
