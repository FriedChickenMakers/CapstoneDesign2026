using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using CapstoneDesign.Prototype;
using CapstoneDesign.Runtime;
using CapstoneDesign.Runtime.LocalState;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace CapstoneDesign.EditorTools
{
    // Isolated preview fixtures only. Never exercise OS permissions, notification
    // scheduling, preference toggles, confirmed reset or device data sources.
    public static class RemainingSettingsUiValidation
    {
        const BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;
        public static void Run(FocusMissionValidation.Report report, Action<bool,string> check, bool capture)
        {
            AndroidPlatformBridge.UseMockProvider();
            foreach (var size in new[] { new Vector2Int(320,568), new Vector2Int(390,844), new Vector2Int(1200,2000) })
            using (var f = new FocusMissionValidation.Fixture(report, true))
            {
                f.Resize(size.x, size.y);
                var panel = f.Navigation.settingsPanel;
                var audioBefore = new AudioPreviewState();
                var settings = panel.AddComponent<SensorRawDisplay>();
                // The real scene serializes these buttons; they do not pass
                // through either runtime button factory before PrepareView.
                settings.sensorPermissionButton = ExistingSceneButton(panel.transform, "Synthetic permission control");
                settings.healthPermissionButton = ExistingSceneButton(panel.transform, "Synthetic health control");
                check(settings.sensorPermissionButton.GetComponent<GardenSfxButton>() == null
                    && settings.healthPermissionButton.GetComponent<GardenSfxButton>() == null,
                    "Existing permission controls begin without factory-provided audio feedback.");
                f.Navigation.ShowSettings();
                var legacyBrand = f.Navigation.home.homeCanvas.transform.Find("SafePortraitFrame/MainContent/Brand");
                check(legacyBrand != null && !legacyBrand.gameObject.activeInHierarchy,
                    "First settings entry hides the legacy brand even before explicit PrepareView.");
                settings.PrepareView(); Settle(settings);
                check(audioBefore.Unchanged(), "Preparing settings creates no audio players or sources and changes no audio preferences.");
                foreach (var permission in new[] { settings.sensorPermissionButton, settings.healthPermissionButton })
                    check(permission.GetComponents<GardenSfxButton>().Length == 1,
                        "Preparing settings attaches exactly one feedback handler to existing permission controls.");
                string saved = StateCodec.Encode(f.Service.Snapshot);
                bool reduced = FigmaMissionUi.ReducedMotion, muted = GardenMusicPlayer.IsMuted;
                float musicLevel = GardenMusicPlayer.Volume;
                double? visualTime = GardenVisualClock.OverrideHour;
                string at = "Settings " + size + ": ";
                var design = settings.transform.Find("Settings.Design") as RectTransform;
                var title = FigmaMissionUi.Find<Text>(settings.transform, "Settings.Title");
                var back = FigmaMissionUi.Find<Button>(settings.transform, "Settings.Back");
                var home = f.Navigation.home;
                var navigation = home.NavigationBar;
                check(design != null && Mathf.Abs(design.rect.width - 390) < .01f, at + "uses the shared responsive design width.");
                check(title != null && title.fontSize == 28 && title.alignment == TextAnchor.MiddleCenter
                    && !title.transform.IsChildOf(settings.PrimaryScroll.content), at + "page heading is centered, readable and fixed outside the list.");
                check(Mathf.Abs(-FigmaMissionUi.TopLeftPosition(title.rectTransform).y - FigmaMissionUi.PageHeaderTitleY) < .01f,
                    at + "title aligns with mission and record screens.");
                check(back == null, at + "the redundant garden back button is absent from the settings heading.");
                check(Within(design, settings.PrimaryScroll.viewport), at + "the bounded list fits the safe frame.");
                CheckUnselectedNavigation(navigation, check, at);
                home.RefreshMissionStatus(); Settle(settings);
                CheckUnselectedNavigation(navigation, check, at + "after mission status refresh: ");
                home.RefreshFromState(); Settle(settings);
                CheckUnselectedNavigation(navigation, check, at + "after garden state refresh: ");
                CheckNotificationVisibility(settings, check, at);
                check(StateCodec.Encode(f.Service.Snapshot) == saved, at + "notification previews leave garden records unchanged.");
                var titleBefore = title.rectTransform.position;
                foreach (string field in new[] { "notificationToggle", "notificationAdaptive" })
                {
                    var button = Field<Button>(settings, field);
                    var label = button.GetComponentsInChildren<Text>(true).First(text => text.name != "Switch state");
                    var state = button.transform.Find("Switch state").GetComponent<Text>();
                    var track = button.transform.Find("Switch track") as RectTransform;
                    var thumb = track.Find("Switch thumb") as RectTransform;
                    var row = (RectTransform)button.transform;
                    check(Within(row, track) && Within(track, thumb) && Within(row, label.rectTransform) && Within(row, state.rectTransform),
                        at + field + " keeps labels, switch and thumb inside the control.");
                    check(Bounds(label.rectTransform, row).xMax <= Bounds(state.rectTransform, row).xMin + .5f
                        && Bounds(state.rectTransform, row).xMax <= Bounds(track, row).xMin - 7.5f,
                        at + field + " text and switch have separate touch/read regions.");
                    check(label.preferredHeight <= label.rectTransform.rect.height + 1 && state.preferredHeight <= state.rectTransform.rect.height + 1,
                        at + field + " labels wrap without clipping.");
                }
                check(!Field<Button>(settings, "notificationToggle").interactable, at + "mock mode preserves the existing native-action guard.");
                CheckVolume(settings, f, report, check, at, "musicVolume", "RenderMusicVolume", "배경 음악");
                CheckVolume(settings, f, report, check, at, "sfxVolume", "RenderSfxVolume", "효과음 음량");
                var musicRow = Field<Slider>(settings, "musicVolume").transform.parent as RectTransform;
                var sfxRow = Field<Slider>(settings, "sfxVolume").transform.parent as RectTransform;
                check(musicRow.parent == sfxRow.parent && sfxRow.GetSiblingIndex() == musicRow.GetSiblingIndex() + 1
                    && Bounds(sfxRow, settings.PrimaryScroll.content).yMax <= Bounds(musicRow, settings.PrimaryScroll.content).yMin + .5f,
                    at + "sound effects appear immediately below background music without overlapping.");
                settings.PrimaryScroll.verticalNormalizedPosition = 1; Settle(settings);
                f.Capture("remaining-settings-top-" + size.x + "x" + size.y, capture);
                settings.PrimaryScroll.verticalNormalizedPosition = 0; Settle(settings);
                check(title.rectTransform.position == titleBefore, at + "scrolling permissions never hides or moves the page title.");
                f.Capture("remaining-settings-permissions-" + size.x + "x" + size.y, capture);

                typeof(SensorRawDisplay).GetMethod("ReportNotificationSave", Hidden).Invoke(settings,
                    new object[] { new PlatformActionResult { status = "ERROR", message = "Synthetic unavailable storage" } });
                Settle(settings);
                var error = Field<Text>(settings, "notificationActionStatus");
                check(error.gameObject.activeSelf && error.text.Contains("저장하지 못했어요")
                    && error.preferredHeight <= error.rectTransform.rect.height + 1, at + "save failure explains retry and expands the section without clipping.");
                f.ClickControl(settings.TestToolsToggleButton); Settle(settings);
                f.ClickControl(settings.MissionRefreshButton); Settle(settings);
                check(settings.MissionResetConfirmButton.gameObject.activeInHierarchy, at + "destructive reset still requires its explicit confirmation.");
                f.ClickControl(FigmaMissionUi.Find<Button>(settings.transform, "Settings.CancelFullReset")); Settle(settings);
                check(!settings.MissionResetConfirmButton.gameObject.activeInHierarchy, at + "cancel retains all records and closes confirmation.");
                f.ClickControl(settings.TestToolsToggleButton); Settle(settings);

                var timeEntry = settings.GetComponentsInChildren<Button>(false).Single(button =>
                    button.GetComponentInChildren<Text>()?.text == "정원의 시간");
                f.ClickControl(timeEntry);
                var savedPosition = settings.PrimaryScroll.content.anchoredPosition;
                Settle(settings);
                var timePage = settings.GetComponentInChildren<GardenVisualDebugPage>(false);
                var timeTitle = FigmaMissionUi.Find<Text>(timePage.transform, "GardenTime.Title");
                var timeDesign = timeTitle.transform.parent as RectTransform;
                var timeFooter = timeDesign.Find("UI debug actions") as RectTransform;
                var timeScroll = timePage.GetComponentInChildren<ScrollRect>(false);
                check(!design.gameObject.activeSelf && timeTitle.fontSize == 28 && timeTitle.alignment == TextAnchor.MiddleCenter,
                    at + "garden time replaces the settings body with the same fixed heading.");
                check(Within(timeDesign, timeFooter) && Within(timeDesign, timeScroll.viewport)
                    && Bounds(timeScroll.viewport, timeDesign).yMin >= Bounds(timeFooter, timeDesign).yMax - .5f,
                    at + "time controls scroll above their visible fixed actions.");
                check(timePage.Dial.rectTransform.rect.width >= 250 && Mathf.Abs(timePage.Dial.rectTransform.rect.width - timePage.Dial.rectTransform.rect.height) < .01f,
                    at + "the time dial remains a large undistorted circle.");
                f.Capture("remaining-garden-time-" + size.x + "x" + size.y, capture);
                f.ClickControl(FigmaMissionUi.Find<Button>(timePage.transform, "GardenTime.Back")); Settle(settings);
                check(!settings.IsVisualDebugOpen && design.gameObject.activeSelf
                    && Vector2.Distance(settings.PrimaryScroll.content.anchoredPosition, savedPosition) < .01f,
                    at + "back restores the exact settings list position.");
                check(StateCodec.Encode(f.Service.Snapshot) == saved && FigmaMissionUi.ReducedMotion == reduced
                    && GardenMusicPlayer.IsMuted == muted && Mathf.Approximately(GardenMusicPlayer.Volume, musicLevel)
                    && GardenVisualClock.OverrideHour == visualTime,
                    at + "browsing, failure display and cancel never alter records or preferences.");
                f.ClickControl(navigation.HomeButton);
                check(home.homeCanvas.gameObject.activeSelf && !panel.activeSelf && navigation.ActiveTab == "홈",
                    at + "the actual home tab returns to the garden and restores its selected state.");
                check(audioBefore.Unchanged(), at + "settings preview and leaving settings preserve all audio players, sources and preferences.");
                var homeFace = navigation.HomeButton.GetComponentInChildren<GardenThemeSurface>(false);
                var homeCaption = navigation.HomeButton.GetComponentsInChildren<Text>(false).Single(text => text.text == "홈");
                var homeIcon = navigation.HomeButton.GetComponentInChildren<RawImage>(false);
                check(homeFace.style == GardenThemeSurface.Style.Home && homeFace.SageAppearance
                    && homeCaption.color == GardenSagePalette.Ink && homeIcon.color == GardenSagePalette.Ink,
                    at + "returning home restores the light sage selection and its dark readable icon and label.");
            }
        }

        static Button ExistingSceneButton(Transform parent, string name)
        {
            var rect = FigmaMissionUi.Box(parent, name, 0, 0, 100, 48, Color.white);
            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = rect.GetComponent<Image>();
            FigmaMissionUi.Label(rect, name, 0, 0, 100, 48, 16);
            return button;
        }

        sealed class AudioPreviewState
        {
            readonly float music = GardenMusicPlayer.Volume, sfx = GardenSfxPlayer.Volume;
            readonly bool muted = GardenMusicPlayer.IsMuted;
            readonly bool hasMusic = PlayerPrefs.HasKey("capstone.music.volume"), hasMute = PlayerPrefs.HasKey("capstone.music.muted"),
                hasSfx = PlayerPrefs.HasKey("capstone.sfx.volume");
            readonly float savedMusic = PlayerPrefs.GetFloat("capstone.music.volume", 1), savedSfx = PlayerPrefs.GetFloat("capstone.sfx.volume", 1);
            readonly int savedMute = PlayerPrefs.GetInt("capstone.music.muted", 0);
            readonly int[] musicPlayers = Ids<GardenMusicPlayer>(), sfxPlayers = Ids<GardenSfxPlayer>(), sources = Ids<AudioSource>();
            static int[] Ids<T>() where T : UnityEngine.Object => UnityEngine.Object.FindObjectsByType<T>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .Select(item => item.GetInstanceID()).OrderBy(id => id).ToArray();
            public bool Unchanged() => Mathf.Approximately(GardenMusicPlayer.Volume, music) && GardenMusicPlayer.IsMuted == muted
                && Mathf.Approximately(GardenSfxPlayer.Volume, sfx)
                && PlayerPrefs.HasKey("capstone.music.volume") == hasMusic && PlayerPrefs.HasKey("capstone.music.muted") == hasMute
                && PlayerPrefs.HasKey("capstone.sfx.volume") == hasSfx
                && PlayerPrefs.GetFloat("capstone.music.volume", 1).Equals(savedMusic) && PlayerPrefs.GetInt("capstone.music.muted", 0) == savedMute
                && PlayerPrefs.GetFloat("capstone.sfx.volume", 1).Equals(savedSfx)
                && Ids<GardenMusicPlayer>().SequenceEqual(musicPlayers) && Ids<GardenSfxPlayer>().SequenceEqual(sfxPlayers)
                && Ids<AudioSource>().SequenceEqual(sources);
        }

        static void CheckVolume(SensorRawDisplay settings, FocusMissionValidation.Fixture fixture,
            FocusMissionValidation.Report report, Action<bool,string> check, string at, string field, string renderer, string caption)
        {
            at += caption + ": ";
            check(!Application.isPlaying, at + "preview validation only runs in edit mode.");
            var slider = Field<Slider>(settings, field);
            var value = Field<Text>(settings, field + "Value");
            var render = typeof(SensorRawDisplay).GetMethod(renderer, Hidden);
            check(slider != null && value != null && render != null, at + "volume slider, value and read-only renderer are available.");
            check((slider.GetComponent<GardenSfxSliderFeedback>() != null) == (field == "sfxVolume"),
                at + "only the effects slider has release feedback.");
            var row = slider.transform.parent as RectTransform;
            var rect = (RectTransform)slider.transform;
            var label = row.GetComponentsInChildren<Text>(false).Single(text => text.text == caption);
            check(slider.minValue == 0 && slider.maxValue == 1 && !slider.wholeNumbers
                && slider.direction == Slider.Direction.LeftToRight && slider.interactable,
                at + "the slider is continuously adjustable from zero to full volume.");
            check(rect.rect.height >= 44 && rect.rect.width >= 220 && Within(row, rect)
                && Within(row, label.rectTransform) && Within(row, value.rectTransform),
                at + "the slider provides a large touch area and stays inside its responsive row.");
            var section = row.parent as RectTransform;
            var layout = section.GetComponent<VerticalLayoutGroup>();
            var viewport = settings.PrimaryScroll.viewport;
            var rowInViewport = Bounds(row, viewport);
            check(layout != null && Mathf.Abs(row.rect.width - (section.rect.width - layout.padding.horizontal)) <= .5f
                && Mathf.Abs(rect.rect.width - (row.rect.width - 24)) <= .5f
                && rowInViewport.xMin >= viewport.rect.xMin - .5f && rowInViewport.xMax <= viewport.rect.xMax + .5f,
                at + "the row and slider use the available width within the viewport.");
            check(Bounds(label.rectTransform, row).xMax <= Bounds(value.rectTransform, row).xMin + .5f
                && Bounds(rect, row).yMax <= Mathf.Min(Bounds(label.rectTransform, row).yMin, Bounds(value.rectTransform, row).yMin) + .5f,
                at + "title, percentage and slider occupy separate regions.");
            check(label.preferredHeight <= label.rectTransform.rect.height + 1 && value.preferredWidth <= value.rectTransform.rect.width + 1,
                at + "title and percentage fit without clipping.");
            float original = slider.value;
            var audioBefore = new AudioPreviewState();
            int callbacks = 0;
            UnityEngine.Events.UnityAction<float> observe = _ => callbacks++;
            slider.onValueChanged.AddListener(observe);
            void RenderPreview(float level)
            {
                int before = callbacks;
                render.Invoke(settings, new object[] { level });
                check(callbacks == before, at + "direct rendering does not invoke a change callback.");
                // Unity's edit-mode Slider.Rebuild emits its own change event.
                // Settling may run that lifecycle; only the direct render call
                // promises no notification, while all previews must remain read-only.
                Settle(settings);
            }
            try
            {
                foreach (float preview in new[] { 0f, .5f, 1f })
                {
                    RenderPreview(preview);
                    check(Mathf.Approximately(slider.value, preview) && value.text == Mathf.RoundToInt(preview * 100) + "%",
                        at + "the actual renderer shows matching slider and percentage at " + preview + ".");
                    check(Within(rect, slider.handleRect) && Within(rect, slider.fillRect),
                        at + "fill and handle stay inside their touch area at " + preview + ".");
                }
                foreach (float preview in new[] { 0f, .5f, 1f })
                {
                    slider.value = preview;
                    check(Mathf.Approximately(slider.value, preview) && value.text == Mathf.RoundToInt(preview * 100) + "%",
                        at + "edit-mode changes update the percentage without audio side effects at " + preview + ".");
                    check(audioBefore.Unchanged(), at + "edit-mode changes preserve both audio channels and their saved preferences.");
                }
                slider.SetValueWithoutNotify(-1); check(slider.value == 0, at + "slider clamps below zero.");
                slider.SetValueWithoutNotify(2); check(slider.value == 1, at + "slider clamps above full volume.");
                RenderPreview(.5f);
                var type = typeof(FocusMissionValidation.Fixture);
                type.GetMethod("Reveal", Hidden, null, new[] { typeof(RectTransform) }, null).Invoke(fixture, new object[] { rect });
                type.GetMethod("PrepareRaycastGraphics", Hidden).Invoke(fixture, null);
                var camera = (Camera)type.GetField("camera", Hidden).GetValue(fixture);
                var context = (EventSystem)type.GetField("pointerContext", Hidden).GetValue(fixture);
                float previousDrag = -1;
                foreach (float portion in new[] { .05f, .5f, .95f })
                {
                    var local = new Vector3(Mathf.Lerp(rect.rect.xMin, rect.rect.xMax, portion), rect.rect.center.y, 0);
                    var pointer = new PointerEventData(context) { position = RectTransformUtility.WorldToScreenPoint(camera, rect.TransformPoint(local)) };
                    var hits = (List<RaycastResult>)type.GetMethod("RaycastFixture", Hidden).Invoke(fixture, new object[] { pointer });
                    check(hits.Count > 0 && hits[0].gameObject.GetComponentInParent<Slider>() == slider,
                        at + "the slider receives the leading UI hit at " + portion + ".");
                    report.raycastChecks++;
                    pointer.pointerPressRaycast = pointer.pointerCurrentRaycast = hits[0];
                    ExecuteEvents.Execute(slider.gameObject, pointer, ExecuteEvents.pointerDownHandler);
                    ExecuteEvents.Execute(slider.gameObject, pointer, ExecuteEvents.dragHandler);
                    ExecuteEvents.Execute(slider.gameObject, pointer, ExecuteEvents.pointerUpHandler);
                    check(slider.value > previousDrag && value.text == Mathf.RoundToInt(slider.value * 100) + "%",
                        at + "synthetic preview dragging updates the slider and caption at " + portion + ".");
                    check(audioBefore.Unchanged(), at + "synthetic preview dragging creates no audio and writes no preferences.");
                    previousDrag = slider.value;
                }
                check(audioBefore.Unchanged(), at + "read-only previews preserve music and effects players, sources and preferences, including during editor rebuilds.");
            }
            finally
            {
                slider.onValueChanged.RemoveListener(observe);
                render.Invoke(settings, new object[] { original }); Settle(settings);
            }
        }

        static void CheckNotificationVisibility(SensorRawDisplay settings, Action<bool,string> check, string at)
        {
            string saved = JsonUtility.ToJson(ActivityNotificationBridge.Settings());
            var options = Field<GameObject>(settings, "notificationOptions");
            var timing = Field<Button>(settings, "notificationTiming");
            var adaptive = Field<Button>(settings, "notificationAdaptive");
            var content = settings.PrimaryScroll.content;
            var section = (RectTransform)options.transform.parent;
            var nextSection = content.Find("Garden preferences") as RectTransform;
            check(nextSection != null, at + "the garden section follows notification settings.");
            float collapsedHeight = 0, collapsedNextTop = 0;
            for (int step = 0; step < 6; step++)
            {
                bool enabled = step % 3 == 1;
                PreviewNotifications(settings, enabled, check, at);
                check(options.activeInHierarchy == enabled && timing.gameObject.activeInHierarchy == enabled
                    && adaptive.gameObject.activeInHierarchy == enabled,
                    at + "notification timing and automatic adjustment follow enabled state at step " + step + ".");
                var sectionBounds = Bounds(section, content);
                var nextBounds = Bounds(nextSection, content);
                check(Mathf.Abs(sectionBounds.yMin - nextBounds.yMax - content.GetComponent<VerticalLayoutGroup>().spacing) < 1,
                    at + "the next section retains only its ordinary gap at step " + step + ".");
                if (step == 0) { collapsedHeight = section.rect.height; collapsedNextTop = nextBounds.yMax; }
                float addedHeight = enabled ? ((RectTransform)options.transform).rect.height + section.GetComponent<VerticalLayoutGroup>().spacing : 0;
                check(Mathf.Abs(section.rect.height - collapsedHeight - addedHeight) < 1
                    && Mathf.Abs(collapsedNextTop - nextBounds.yMax - addedHeight) < 1,
                    at + "repeated off/on/off reflows the section without reserved blank space at step " + step + ".");
            }
            check(JsonUtility.ToJson(ActivityNotificationBridge.Settings()) == saved, at + "rendering notification states never changes stored notification settings.");
            // Leave all controls visible for the existing row-geometry checks.
            PreviewNotifications(settings, true, check, at);
        }

        static void PreviewNotifications(SensorRawDisplay settings, bool enabled, Action<bool,string> check, string at)
        {
            var value = new ActivityNotificationSettings { enabled = enabled, adaptive = true, minuteOfDay = 825, permissionStatus = "AVAILABLE" };
            string original = JsonUtility.ToJson(value);
            var render = typeof(SensorRawDisplay).GetMethod("RenderNotificationSettings", Hidden);
            check(render != null, at + "the actual notification renderer is available.");
            render.Invoke(settings, new object[] { value, false });
            Settle(settings);
            check(JsonUtility.ToJson(value) == original, at + "rendering leaves the supplied notification state unchanged.");
        }

        static void CheckUnselectedNavigation(MissionBottomNavigation navigation, Action<bool,string> check, string at)
        {
            check(navigation != null && navigation.ActiveTab == null, at + "settings leaves every main navigation tab unselected.");
            foreach (var button in new[] { navigation.HomeButton, navigation.MissionButton, navigation.CourseButton })
            {
                var face = button.GetComponentInChildren<GardenThemeSurface>(false);
                check(face != null && face.style == GardenThemeSurface.Style.NavigationLeaf && !face.SageAppearance,
                    at + button.name + " keeps its ordinary dark leaf surface rather than a selected fill.");
            }
            var caption = navigation.HomeButton.GetComponentsInChildren<Text>(false).Single(text => text.text == "홈");
            var icon = navigation.HomeButton.GetComponentInChildren<RawImage>(false);
            check(caption.color == Color.white && icon.color == Color.white,
                at + "the unselected home caption and icon use white ink on their dark leaf surface.");
        }

        static T Field<T>(SensorRawDisplay target, string name) where T : class => typeof(SensorRawDisplay).GetField(name, Hidden).GetValue(target) as T;
        static void Settle(SensorRawDisplay settings)
        {
            for (int i = 0; i < 3; i++)
            {
                foreach (var fit in settings.GetComponentsInChildren<FigmaMissionCanvasFit>(false)) fit.Fit();
                Canvas.ForceUpdateCanvases();
                foreach (var layout in settings.GetComponentsInChildren<GardenSettingsLayout>(false))
                {
                    if (layout.Content != null) LayoutRebuilder.ForceRebuildLayoutImmediate(layout.Content);
                    layout.Refresh();
                }
                Canvas.ForceUpdateCanvases();
            }
        }
        static Rect Bounds(RectTransform child, RectTransform parent)
        {
            var points = new Vector3[4]; child.GetWorldCorners(points);
            var local = points.Select(point => (Vector2)parent.InverseTransformPoint(point)).ToArray();
            return Rect.MinMaxRect(local.Min(point => point.x), local.Min(point => point.y), local.Max(point => point.x), local.Max(point => point.y));
        }
        static bool Within(RectTransform parent, RectTransform child)
        {
            var bounds = Bounds(child, parent); var area = parent.rect;
            return bounds.xMin >= area.xMin - .5f && bounds.xMax <= area.xMax + .5f
                && bounds.yMin >= area.yMin - .5f && bounds.yMax <= area.yMax + .5f;
        }
    }
}
