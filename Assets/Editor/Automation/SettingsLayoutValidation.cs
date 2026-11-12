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
    // Render the real settings panel with disposable state. Control previews do
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
                        PreviewControls(sensor, enabled);
                        foreach (string position in new[] { "top", "middle", "bottom" })
                        {
                            string name = "settings-" + (enabled ? "on" : "off") + "-" + position + "-" + size.x + "x" + size.y + ".png";
                            string path = Path.Combine(output, name);
                            // First pass establishes the requested aspect and content height.
                            LocalLoopPreview.Render(size, path);
                            sensor.PrimaryScroll.StopMovement();
                            sensor.PrimaryScroll.verticalNormalizedPosition = position == "top" ? 1 : position == "bottom" ? 0 : .5f;
                            LocalLoopPreview.Render(size, path);
                            AssertLayout(sensor, name, enabled);
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

        static void PreviewControls(SensorRawDisplay sensor, bool enabled)
        {
            var value = new ActivityNotificationSettings { enabled = enabled, adaptive = enabled, minuteOfDay = 825, permissionStatus = "AVAILABLE" };
            string original = JsonUtility.ToJson(value);
            var render = typeof(SensorRawDisplay).GetMethod("RenderNotificationSettings", Hidden);
            Require(render != null, "Missing notification settings renderer.");
            render.Invoke(sensor, new object[] { value, false });
            Require(JsonUtility.ToJson(value) == original, "Rendering notifications changed the supplied settings.");
            var music = typeof(SensorRawDisplay).GetField("musicVolume", Hidden)?.GetValue(sensor) as Slider;
            var renderMusic = typeof(SensorRawDisplay).GetMethod("RenderMusicVolume", Hidden);
            Require(music != null && renderMusic != null, "Missing music volume preview.");
            renderMusic.Invoke(sensor, new object[] { enabled ? 1f : 0f });
            var sfx = typeof(SensorRawDisplay).GetField("sfxVolume", Hidden)?.GetValue(sensor) as Slider;
            var renderSfx = typeof(SensorRawDisplay).GetMethod("RenderSfxVolume", Hidden);
            Require(sfx != null && renderSfx != null, "Missing sound-effects volume preview.");
            renderSfx.Invoke(sensor, new object[] { enabled ? 1f : 0f });
        }

        static void AssertLayout(SensorRawDisplay sensor, string name, bool enabled)
        {
            var options = (GameObject)typeof(SensorRawDisplay).GetField("notificationOptions", Hidden).GetValue(sensor);
            Require(options.activeInHierarchy == enabled, name + ": timing group visibility follows activity notifications");
            foreach (string field in new[] { "notificationTiming", "notificationAdaptive" })
            {
                var control = (Button)typeof(SensorRawDisplay).GetField(field, Hidden).GetValue(sensor);
                Require(control.gameObject.activeInHierarchy == enabled, name + ": " + field + " visibility follows activity notifications");
            }
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
            Require(switches == (enabled ? 2 : 1), name + ": expected only visible notification switches");
            var music = (Slider)typeof(SensorRawDisplay).GetField("musicVolume", Hidden).GetValue(sensor);
            var musicValue = (Text)typeof(SensorRawDisplay).GetField("musicVolumeValue", Hidden).GetValue(sensor);
            var musicRow = (RectTransform)music.transform.parent;
            Require(music.minValue == 0 && music.maxValue == 1 && !music.wholeNumbers
                && musicValue.text == (enabled ? "100%" : "0%"), name + ": volume preview shows the correct percentage and range");
            Require(((RectTransform)music.transform).rect.height >= 44 && Contains(musicRow.rect, Bounds((RectTransform)music.transform, musicRow))
                && Contains(musicRow.rect, Bounds(musicValue.rectTransform, musicRow)), name + ": volume slider and percentage fit their row");
            var sfx = (Slider)typeof(SensorRawDisplay).GetField("sfxVolume", Hidden).GetValue(sensor);
            var sfxValue = (Text)typeof(SensorRawDisplay).GetField("sfxVolumeValue", Hidden).GetValue(sensor);
            var sfxRow = (RectTransform)sfx.transform.parent;
            Require(sfx.minValue == 0 && sfx.maxValue == 1 && !sfx.wholeNumbers
                && sfxValue.text == (enabled ? "100%" : "0%"), name + ": sound-effects preview shows the correct percentage and range");
            Require(sfxRow.parent == musicRow.parent && sfxRow.GetSiblingIndex() == musicRow.GetSiblingIndex() + 1
                && Bounds(sfxRow, sensor.PrimaryScroll.content).yMax <= Bounds(musicRow, sensor.PrimaryScroll.content).yMin + .5f,
                name + ": sound effects sit immediately below background music");
            Require(((RectTransform)sfx.transform).rect.height >= 44 && Mathf.Abs(sfxRow.rect.width - musicRow.rect.width) < .5f
                && Contains(sfxRow.rect, Bounds((RectTransform)sfx.transform, sfxRow))
                && Contains(sfxRow.rect, Bounds(sfxValue.rectTransform, sfxRow)), name + ": sound-effects slider and percentage fit the full-width row");
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
