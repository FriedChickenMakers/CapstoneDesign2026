using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using CapstoneDesign.Runtime;
using CapstoneDesign.Runtime.LocalState;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;

namespace CapstoneDesign.EditorTools
{
    // Render the real settings panel with disposable state. Switch previews do
    // not invoke callbacks, write preferences, request permissions or send data.
    public static class SettingsLayoutValidation
    {
        sealed class Clock : IClock
        {
            public DateTimeOffset UtcNow => DateTimeOffset.Parse("2026-11-10T12:00:00+09:00");
            public TimeZoneInfo TimeZone => TimeZoneInfo.CreateCustomTimeZone("Synthetic-KST", TimeSpan.FromHours(9), "KST", "KST");
        }
        sealed class Store : IStateStore
        {
            string data;
            public GardenState Load() => data == null ? null : StateCodec.Decode(data);
            public void Save(GardenState value) => data = StateCodec.Encode(value);
        }
        [Serializable] sealed class Report
        {
            public string status = "running", error;
            public bool synthetic = true, deviceTested;
            public List<string> checks = new List<string>(), images = new List<string>();
        }
        const BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;

        public static void Run()
        {
            string root = Environment.GetEnvironmentVariable("CAPSTONE_ARTIFACTS");
            if (string.IsNullOrWhiteSpace(root)) throw new InvalidOperationException("An explicit capture directory is required.");
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null) throw new InvalidOperationException("A renderer is required.");
            string output = Path.Combine(root, "settings-layout");
            Directory.CreateDirectory(output);
            var report = new Report();
            try
            {
                foreach (var size in new[] { new Vector2Int(360, 640), new Vector2Int(390, 844), new Vector2Int(412, 915), new Vector2Int(1200, 2000) })
                {
                    EditorSceneManager.OpenScene("Assets/Scenes/MockupMain.unity");
                    AndroidPlatformBridge.UseMockProvider();
                    var nav = UnityEngine.Object.FindFirstObjectByType<MockupNavigation>(FindObjectsInactive.Include);
                    var loop = nav.activitiesPanel.GetComponent<WeekOneQuestDemo>();
                    using var service = new GardenStateService(new Store(), new Clock(), new DemoBalanceConfig { VisitorChancePercent = 0 });
                    loop.Initialize(service, true);
                    var sensor = nav.GetComponentInChildren<SensorRawDisplay>(true);
                    sensor.PrepareView();
                    nav.ShowSettings();
                    sensor.SendMessage("RefreshText", SendMessageOptions.RequireReceiver);
                    string snapshot = StateCodec.Encode(service.Snapshot);
                    foreach (bool enabled in new[] { true, false })
                    {
                        PreviewSwitches(sensor, enabled);
                        foreach (string position in new[] { "top", "middle", "bottom" })
                        {
                            string name = "settings-" + (enabled ? "on" : "off") + "-" + position + "-" + size.x + "x" + size.y + ".png";
                            string path = Path.Combine(output, name);
                            // First pass establishes the requested aspect and content height.
                            LocalLoopPreview.Render(size, path);
                            sensor.PrimaryScroll.StopMovement();
                            sensor.PrimaryScroll.verticalNormalizedPosition = position == "top" ? 1 : position == "bottom" ? 0 : .5f;
                            LocalLoopPreview.Render(size, path);
                            AssertLayout(sensor, name);
                            report.images.Add(name);
                            report.checks.Add(name + ": switch/row geometry and label fit pass");
                        }
                    }
                    if (StateCodec.Encode(service.Snapshot) != snapshot) throw new InvalidOperationException("Settings preview changed garden state.");
                    report.checks.Add(size.x + "x" + size.y + ": persistent garden unchanged");
                }
                report.status = "pass";
                Debug.Log("SETTINGS_LAYOUT_VALIDATION_PASS images=" + report.images.Count);
            }
            catch (Exception error)
            {
                report.status = "fail";
                report.error = error.ToString();
                throw;
            }
            finally
            {
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                AndroidPlatformBridge.UseMockProvider();
                File.WriteAllText(Path.Combine(output, "report.json"), JsonUtility.ToJson(report, true));
            }
        }

        static void PreviewSwitches(SensorRawDisplay sensor, bool enabled)
        {
            var update = typeof(SensorRawDisplay).GetMethod("UpdateSwitch", Hidden);
            foreach (string field in new[] { "notificationToggle", "notificationAdaptive", "musicToggle", "reducedMotionToggle" })
            {
                var button = typeof(SensorRawDisplay).GetField(field, Hidden)?.GetValue(sensor) as Button;
                if (button == null || update == null) throw new InvalidOperationException("Missing settings switch " + field);
                update.Invoke(sensor, new object[] { button, enabled });
            }
        }

        static void AssertLayout(SensorRawDisplay sensor, string name)
        {
            int switches = 0;
            foreach (var track in sensor.PrimaryScroll.content.GetComponentsInChildren<RectTransform>(false).Where(item => item.name == "Switch track"))
            {
                var row = (RectTransform)track.parent;
                var trackBounds = Bounds(track, row);
                Require(Contains(row.rect, trackBounds) && row.rect.xMax - trackBounds.xMax >= 11.5f, name + ": switch crosses paper row inset");
                var thumb = (RectTransform)track.Find("Switch thumb");
                Require(thumb != null && Contains(track.rect, Bounds(thumb, track)), name + ": thumb leaves track");
                var state = row.Find("Switch state")?.GetComponent<Text>();
                var label = row.GetComponentsInChildren<Text>(false).FirstOrDefault(item => item != state);
                Require(state != null && label != null, name + ": switch labels missing");
                var stateBounds = Bounds(state.rectTransform, row);
                var labelBounds = Bounds(label.rectTransform, row);
                Require(Contains(row.rect, stateBounds) && Contains(row.rect, labelBounds), name + ": label leaves row");
                Require(labelBounds.xMax <= stateBounds.xMin + .5f && stateBounds.xMax <= trackBounds.xMin - 8, name + ": switch text overlaps another region");
                Require(label.preferredHeight <= label.rectTransform.rect.height + 1 && state.preferredHeight <= state.rectTransform.rect.height + 1, name + ": switch label is clipped");
                switches++;
            }
            Require(switches == 4, name + ": expected all four settings switches");
            foreach (var arrow in sensor.PrimaryScroll.content.GetComponentsInChildren<CapstoneDesign.Prototype.PrototypeUiIcon>(false).Where(item => item.symbol == CapstoneDesign.Prototype.PrototypeUiIcon.Symbol.Arrow))
            {
                var row = arrow.transform.parent as RectTransform;
                if (row == null || row.GetComponent<Button>() == null) continue;
                var bounds = Bounds(arrow.rectTransform, row);
                Require(Contains(row.rect, bounds) && row.rect.xMax - bounds.xMax >= 11.5f, name + ": disclosure arrow crosses paper row inset");
            }
        }
        static Rect Bounds(RectTransform child, RectTransform parent)
        {
            var corners = new Vector3[4]; child.GetWorldCorners(corners);
            var points = corners.Select(point => (Vector2)parent.InverseTransformPoint(point)).ToArray();
            return Rect.MinMaxRect(points.Min(point => point.x), points.Min(point => point.y), points.Max(point => point.x), points.Max(point => point.y));
        }
        static bool Contains(Rect parent, Rect child) => child.xMin >= parent.xMin - .5f && child.xMax <= parent.xMax + .5f && child.yMin >= parent.yMin - .5f && child.yMax <= parent.yMax + .5f;
        static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
    }
}
