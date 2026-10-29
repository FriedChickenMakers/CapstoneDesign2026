using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using CapstoneDesign.Prototype;
using CapstoneDesign.Runtime;
using CapstoneDesign.Runtime.LocalState;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace CapstoneDesign.EditorTools
{
    // Synthetic Editor evidence only. Real guidance/records use public runtime
    // routes, while pressed/disabled cases explicitly preview UGUI visual states.
    // Existing TabConsistencyCapture remains the detailed clipping/scroll gate.
    public static class GardenThemeCapture
    {
        sealed class Clock : IClock
        {
            public DateTimeOffset Time = DateTimeOffset.Parse("2026-10-29T12:00:00+09:00");
            public DateTimeOffset UtcNow => Time;
            public TimeZoneInfo TimeZone => TimeZoneInfo.CreateCustomTimeZone("Synthetic-KST", TimeSpan.FromHours(9), "KST", "KST");
        }
        sealed class Store : IStateStore
        {
            public string Data;
            public GardenState Load() => Data == null ? null : StateCodec.Decode(Data);
            public void Save(GardenState value) => Data = StateCodec.Encode(value);
        }
        static readonly string[] Cases =
        {
            "home-day", "home-night", "activities", "activities-pressed", "activities-disabled",
            "programs", "course", "practice", "reflection", "reflection-selected", "complete",
            "growth-current", "growth-after", "records", "record-detail", "record-health",
            "journal-long", "preferences", "settings-top", "settings-bottom"
        };

        public static void Capture()
        {
            string root = Environment.GetEnvironmentVariable("CAPSTONE_ARTIFACTS");
            Require(!string.IsNullOrWhiteSpace(root), "Use a new explicit CAPSTONE_ARTIFACTS directory.");
            Require(SystemInfo.graphicsDeviceType != GraphicsDeviceType.Null, "Rendered evidence requires a graphics device.");
            string output = Path.Combine(root, "visual");
            Directory.CreateDirectory(output);
            var chosen = ReadCases();
            var sizes = ReadSizes();
            var report = new List<string>
            {
                "Synthetic Editor state; MOCK platform; no application save; deviceTested=false.",
                "Pressed preview sends synthetic pointer events; it does not claim physical device touch coverage.",
                "settings-toggle-off previews the real music switch renderer without changing PlayerPrefs or audio settings.",
                "Renderer: " + SystemInfo.graphicsDeviceName + "; API: " + SystemInfo.graphicsDeviceType
            };
            var manifest = new StringBuilder("image,width,height,case\n");
            var controls = new StringBuilder("image,control,type,interactable,rectWidth,rectHeight\n");
            int count = 0;
            double? originalHour = GardenVisualClock.OverrideHour;
            try
            {
                foreach (var size in sizes)
                foreach (string name in chosen)
                {
                    Debug.Log("GARDEN_THEME_CAPTURE_BEGIN " + name + " " + size);
                    EditorSceneManager.OpenScene("Assets/Scenes/MockupMain.unity");
                    AndroidPlatformBridge.UseMockProvider();
                    GardenVisualClock.SetOverrideMinutes(name == "home-night" ? 0 : 720);
                    var nav = UnityEngine.Object.FindFirstObjectByType<MockupNavigation>(FindObjectsInactive.Include);
                    Require(nav != null, "Integrated navigation is missing.");
                    var ui = nav.activitiesPanel.GetComponent<WeekOneQuestDemo>();
                    var clock = new Clock();
                    var store = new Store();
                    var balance = new DemoBalanceConfig { VisitorChancePercent = 0 };
                    using var service = new GardenStateService(store, clock, balance, new LegacySnapshot { Nutrient = 50 });
                    ui.Balance = balance;
                    ui.Initialize(service, true);
                    if (name != "programs") Require(service.SelectMbctProgram("8w"), service.LastError);
                    nav.ShowActivities();
                    Prepare(name, nav, ui, service, clock);
                    string saved = store.Data;
                    string snapshot = StateCodec.Encode(service.Snapshot);
                    string file = name + "-" + size.x + "x" + size.y + ".png";
                    string path = Path.Combine(output, file);
                    LocalLoopPreview.Render(size, path);
                    if (name.StartsWith("settings", StringComparison.Ordinal) || name == "shop-progress")
                    {
                        var sensor = nav.GetComponentInChildren<SensorRawDisplay>(true);
                        var scroll = name == "shop-progress" ? ui.GetComponentInChildren<ScrollRect>() : sensor.PrimaryScroll;
                        Require(scroll != null, "Capture scroll view is missing: " + name);
                        scroll.verticalNormalizedPosition = name.EndsWith("bottom", StringComparison.Ordinal) ? 0 : 1;
                        if (scroll.verticalScrollbar != null) scroll.verticalScrollbar.value = scroll.verticalNormalizedPosition;
                        LocalLoopPreview.Render(size, path);
                    }
                    var themes = UnityEngine.Object.FindObjectsByType<GardenThemeSurface>(FindObjectsSortMode.None);
                    Require(themes.Length > 0, "No theme surfaces rendered for " + name);
                    Require(themes.All(surface => !surface.raycastTarget), "Decorative surface intercepts input in " + name);
                    Require(store.Data == saved && StateCodec.Encode(service.Snapshot) == snapshot,
                        "Capturing presentation mutated the saved garden in " + name);
                    if(name=="record-health")
                    {
                        var graph=ui.GetComponentInChildren<HeartSampleGraph>();
                        Require(graph!=null,"Synthetic heart chart is missing.");
                        var layer=graph.transform.parent.parent;
                        var paper=layer.parent.Find("Garden theme surface");
                        Require(paper==null || layer.GetSiblingIndex()>paper.GetSiblingIndex(),
                            "Opaque page decoration covers the heart chart.");
                    }
                    foreach (var control in UnityEngine.Object.FindObjectsByType<Selectable>(FindObjectsSortMode.None))
                    {
                        if (!control.isActiveAndEnabled) continue;
                        var rect = ((RectTransform)control.transform).rect;
                        controls.Append(Csv(file)).Append(',').Append(Csv(control.name)).Append(',')
                            .Append(control.GetType().Name).Append(',').Append(control.interactable).Append(',')
                            .Append(rect.width.ToString("0.##", CultureInfo.InvariantCulture)).Append(',')
                            .Append(rect.height.ToString("0.##", CultureInfo.InvariantCulture)).AppendLine();
                    }
                    manifest.Append(Csv(file)).Append(',').Append(size.x).Append(',').Append(size.y).Append(',').Append(name).AppendLine();
                    report.Add(file + ": nonblocking theme surfaces=" + themes.Length + "; unchanged persistent state PASS");
                    count++;
                }
                report.Add("GARDEN_THEME_CAPTURE_PASS images=" + count);
                Debug.Log("GARDEN_THEME_CAPTURE_PASS images=" + count + " output=" + output);
            }
            catch (Exception error)
            {
                report.Add("FAIL " + error.Message);
                throw;
            }
            finally
            {
                if (originalHour.HasValue) GardenVisualClock.SetOverrideMinutes((int)Math.Round(originalHour.Value * 60));
                else GardenVisualClock.UseSystemTime();
                AndroidPlatformBridge.UseMockProvider();
                File.WriteAllLines(Path.Combine(root, "theme-validation.txt"), report);
                File.WriteAllText(Path.Combine(root, "capture-manifest.csv"), manifest.ToString(), Encoding.UTF8);
                File.WriteAllText(Path.Combine(root, "control-metrics.csv"), controls.ToString(), Encoding.UTF8);
            }
        }

        static void Prepare(string name, MockupNavigation nav, WeekOneQuestDemo ui, GardenStateService service, Clock clock)
        {
            if (name.StartsWith("home-", StringComparison.Ordinal)) { nav.ShowIsland(); nav.home.RefreshFromState(); return; }
            if (name.StartsWith("activities", StringComparison.Ordinal))
            {
                ui.ShowHome();
                if (name == "activities") return;
                var button = ui.GetComponentsInChildren<Button>().FirstOrDefault(item => item.interactable &&
                    item.GetComponentsInChildren<GardenThemeSurface>().Any(surface => surface.style == GardenThemeSurface.Style.Primary));
                Require(button != null, "Primary activity action is missing.");
                if (name == "activities-disabled") button.interactable = false;
                else
                {
                    var pointer=new PointerEventData(EventSystem.current){button=PointerEventData.InputButton.Left};
                    ExecuteEvents.Execute(button.gameObject,pointer,ExecuteEvents.pointerEnterHandler);
                    ExecuteEvents.Execute(button.gameObject,pointer,ExecuteEvents.pointerDownHandler);
                }
                return;
            }
            if (name == "programs") { ui.ShowProgramSelection(); return; }
            if (name == "course") { ui.ShowCourse(); return; }
            if (name == "preferences") { ui.ShowRecommendationPreferences(); return; }
            if (name == "shop-progress")
            {
                Require(service.SelectMbctProgram("2w"), service.LastError);
                foreach (var mission in MbctContent.ForProgram("2w").Take(3))
                {
                    ui.SelectMission(mission);
                    string progressId = service.Snapshot.Sessions.Last().SessionId;
                    Require(service.StartSession(progressId), service.LastError);
                    clock.Time = clock.Time.AddMinutes(4);
                    ui.Finish(null, null);
                    clock.Time = clock.Time.AddDays(1);
                    Require(service.RefreshCycle(Array.Empty<string>()), service.LastError);
                }
                Require(MbctPolicy.CompletedCount(service.Snapshot) == 3, "Synthetic course progress is missing.");
                ui.ShowShop(); return;
            }
            if (name.StartsWith("growth-", StringComparison.Ordinal))
            {
                ui.PreviewGrowth();
                if (name == "growth-current") Click(ui.transform, "현재");
                return;
            }
            if (name.StartsWith("settings", StringComparison.Ordinal))
            {
                var sensor = nav.GetComponentInChildren<SensorRawDisplay>(true);
                sensor.PrepareView(); nav.ShowSettings();
                sensor.SendMessage("RefreshText", SendMessageOptions.RequireReceiver);
                if (name == "settings-toggle-off")
                {
                    // Visual state evidence only. The real music callback writes
                    // PlayerPrefs, so render its off state without changing preferences.
                    const BindingFlags hidden = BindingFlags.Instance | BindingFlags.NonPublic;
                    var button = typeof(SensorRawDisplay).GetField("musicToggle", hidden)?.GetValue(sensor) as Button;
                    var updateSwitch = typeof(SensorRawDisplay).GetMethod("UpdateSwitch", hidden);
                    Require(button != null && updateSwitch != null, "Music switch preview binding is missing.");
                    updateSwitch.Invoke(sensor, new object[] { button, false });
                    Require(button.GetComponentsInChildren<Text>().Any(label => label.text == "꺼짐"),
                        "Music switch off state was not rendered.");
                }
                return;
            }
            if (name == "journal-long")
            {
                string text = string.Concat(Enumerable.Repeat("합성 기록: 정원의 햇살 아래에서 잠시 멈추고 천천히 숨을 골랐어요. ", 6));
                Require(service.SaveExperience("theme-journal", "즐거움", text, text, text, text), service.LastError);
                ui.ShowExperienceRecord("theme-journal"); return;
            }
            ui.SelectMission(MbctContent.Course[0]);
            string id = service.Snapshot.Sessions.Last().SessionId;
            Require(service.StartSession(id), service.LastError);
            clock.Time = clock.Time.AddMinutes(4);
            if (name == "practice") { ui.ShowSession(); return; }
            if (name.StartsWith("reflection", StringComparison.Ordinal))
            {
                ui.ShowReflection();
                if (name == "reflection-selected") Click(ui.transform, "차분해요");
                return;
            }
            ui.Finish("차분해요", "합성 UI 검토용 기록입니다.");
            if (name == "complete") return;
            if (name == "records") { ui.ShowRecords(); return; }
            if (name == "record-health")
            {
                var session = service.Snapshot.Sessions.Last();
                long start = DateTimeOffset.Parse(session.StartedUtc).ToUnixTimeMilliseconds();
                long end = DateTimeOffset.Parse(session.EndedUtc).ToUnixTimeMilliseconds();
                ui.PreviewHealth = new AndroidPlatformSnapshot
                {
                    inputMode = "MOCK", healthConnect = new HealthConnectSnapshot
                    {
                        heartRate = new HealthMetricSnapshot
                        {
                            status = "AVAILABLE", queryComplete = true, queryStartEpochMs = start, queryEndEpochMs = end,
                            sampleCount = 4, displaySampleCount = 4,
                            heartRateSamples = new[]
                            {
                                new HeartRateSampleSnapshot { measuredAtEpochMs = start + 1000, beatsPerMinute = 73, sourcePackage = "Synthetic A", segmentId = 0 },
                                new HeartRateSampleSnapshot { measuredAtEpochMs = start + 60000, beatsPerMinute = 76, sourcePackage = "Synthetic A", segmentId = 0 },
                                new HeartRateSampleSnapshot { measuredAtEpochMs = start + 120000, beatsPerMinute = 75, sourcePackage = "Synthetic A", segmentId = 0 },
                                new HeartRateSampleSnapshot { measuredAtEpochMs = start + 180000, beatsPerMinute = 72, sourcePackage = "Synthetic A", segmentId = 0 }
                            }
                        }
                    }
                };
            }
            Require(name == "record-detail" || name == "record-health", "Unknown theme case: " + name);
            ui.ShowRecord(id);
        }

        static string[] ReadCases()
        {
            string filter = Environment.GetEnvironmentVariable("CAPSTONE_GARDEN_THEME_CASES");
            if (string.IsNullOrWhiteSpace(filter)) return Cases;
            var selected = filter.Split(',').Select(value => value.Trim()).Where(value => value.Length > 0).Distinct().ToArray();
            Require(selected.Length > 0 && selected.All(name => Cases.Contains(name)
                || name == "settings-toggle-off" || name == "shop-progress"),
                "Unknown or empty CAPSTONE_GARDEN_THEME_CASES.");
            return selected;
        }
        static Vector2Int[] ReadSizes()
        {
            string filter = Environment.GetEnvironmentVariable("CAPSTONE_GARDEN_THEME_SIZES");
            if (string.IsNullOrWhiteSpace(filter)) return new[] { new Vector2Int(900, 1950), new Vector2Int(900, 1600), new Vector2Int(1200, 2000), new Vector2Int(1200, 800) };
            return filter.Split(',').Select(value =>
            {
                var parts = value.Trim().Split('x');
                Require(parts.Length == 2 && int.TryParse(parts[0], out _) && int.TryParse(parts[1], out _), "Use WIDTHxHEIGHT capture sizes.");
                var size = new Vector2Int(int.Parse(parts[0], CultureInfo.InvariantCulture), int.Parse(parts[1], CultureInfo.InvariantCulture));
                Require(size.x >= 320 && size.y >= 320 && size.x <= 4096 && size.y <= 4096, "Capture size is outside the supported range.");
                return size;
            }).ToArray();
        }
        static void Click(Transform scope, string name)
        {
            var button = scope.GetComponentsInChildren<Button>().FirstOrDefault(item => item.name == name && item.interactable);
            Require(button != null, "Enabled action missing: " + name);
            button.onClick.Invoke();
        }
        static string Csv(string value) => "\"" + (value ?? "").Replace("\"", "\"\"") + "\"";
        static void Require(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
    }
}
