using CapstoneDesign.Prototype;
using System;
using UnityEngine;
using UnityEngine.UI;

namespace CapstoneDesign.Runtime
{
    public sealed class GardenVisualDebugPage : MonoBehaviour
    {
        public CircularTimeSlider Dial { get; private set; }
        Text time, status;
        Button[] increments;
        Button earlier, later;
        float nextClockUpdate;

        public void Build(Action close, Action garden, Action presetGarden)
        {
            var design = FigmaMissionUi.Rect(transform, "GardenTime.Design", 0, 0, 390, 844);
            var fit = gameObject.AddComponent<FigmaMissionCanvasFit>(); fit.Design = design; fit.Fit();
            FigmaMissionUi.UseGardenShell(design.gameObject);
            var backdrop = design.gameObject.AddComponent<Image>(); backdrop.color = GardenSagePalette.Background; backdrop.raycastTarget = true;
            design.gameObject.AddComponent<QaLayoutTelemetry>().ScreenId="garden-time";
            GardenSettingsLayout.BuildHeader(design, "정원의 시간", "‹ 설정", close, "GardenTime");
            var viewport = FigmaMissionUi.Box(design, "UI debug scroll", 0, 110, 390, 480, Color.clear);
            viewport.GetComponent<Image>().raycastTarget = true; viewport.gameObject.AddComponent<RectMask2D>();
            var body = FigmaMissionUi.Rect(viewport, "GardenTime.Content", 0, 0, 390, 1);
            var flow = body.gameObject.AddComponent<VerticalLayoutGroup>(); flow.spacing = 12;
            flow.padding = new RectOffset(24, 24, 4, 16); flow.childControlHeight = flow.childControlWidth = true;
            flow.childForceExpandHeight = false; flow.childForceExpandWidth = true;
            body.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            var scroll = viewport.gameObject.AddComponent<ScrollRect>(); scroll.viewport = viewport; scroll.content = body;
            scroll.horizontal = false; scroll.movementType = ScrollRect.MovementType.Clamped;
            GardenUi.AddScrollIndicator(scroll);
            Copy(body, "눈금을 움직여 낮과 밤을 미리 봐요.\n활동 날짜와 알림은 바뀌지 않아요.", 15);
            var presets = Row(body, "Garden time presets");
            ActionButton(presets, "낮 12시 보기", () => ShowPreset(12 * 60, presetGarden)).name = "GardenTime.Noon";
            ActionButton(presets, "밤 0시 보기", () => ShowPreset(0, presetGarden)).name = "GardenTime.Midnight";
            var holder = GardenUi.Box(body, "24 hour dial", 0, 0, 1, 0);
            holder.AddComponent<LayoutElement>().preferredHeight = 270;
            var face = GardenUi.Box(holder.transform, "Circular time slider", .5f, .5f, 0, 0);
            ((RectTransform)face.transform).sizeDelta = new Vector2(260, 260);
            Dial = face.AddComponent<CircularTimeSlider>();
            foreach (var hour in new[] { 0, 6, 12, 18 })
            {
                float angle = hour / 24f * Mathf.PI * 2;
                var label = GardenUi.Label(face.transform, hour.ToString("00"), .5f + Mathf.Sin(angle) * .47f - .08f,
                    .5f + Mathf.Cos(angle) * .47f - .07f, .16f, .14f, 14);
                label.fontSize = 14;
                label.alignment = TextAnchor.MiddleCenter;
            }
            time = GardenUi.Label(face.transform, "00:00", .18f, .37f, .64f, .26f, 28);
            time.fontSize = 28; time.name = "GardenTime.Time";
            GardenTypography.SetWeight(time, true); time.alignment = TextAnchor.MiddleCenter;
            status = Copy(body, "", 15); status.name = "GardenTime.Status";
            var stepRow = Row(body, "Time increments"); increments = new Button[3];
            int index = 0;
            foreach (int step in new[] { 1, 5, 15 })
            {
                int value = step;
                increments[index] = ActionButton(stepRow, step + "분", () => { Dial.StepMinutes = value; Refresh(); });
                increments[index++].name = "GardenTime.Step." + step;
            }
            var nudge = Row(body, "Time adjustments");
            earlier = ActionButton(nudge, "− 15분", () => Dial.Nudge(-1));
            later = ActionButton(nudge, "+ 15분", () => Dial.Nudge(1));
            earlier.name = "GardenTime.Earlier"; later.name = "GardenTime.Later";
            var footer = FigmaMissionUi.Rect(design, "UI debug actions", 24, 620, 342, 104);
            FigmaMissionUi.Button(footer, "정원 보기", 0, 0, 342, 48, garden, FigmaMissionUi.Green, Color.white, 17).name = "GardenTime.Garden";
            FigmaMissionUi.Button(footer, "실제 시간으로", 0, 56, 342, 44, GardenVisualClock.UseSystemTime,
                Color.clear, FigmaMissionUi.Green, 16).name = "GardenTime.SystemTime";
            var layout = design.gameObject.AddComponent<GardenSettingsLayout>();
            layout.Viewport = viewport; layout.Content = body; layout.Footer = footer; layout.FooterHeight = 104; layout.Refresh();
            Dial.Changed += GardenVisualClock.SetOverrideMinutes;
            Refresh();
        }
        static void ShowPreset(int minute, Action garden)
        {
            GardenVisualClock.SetOverrideMinutes(minute);
            garden?.Invoke();
        }
        static Transform Row(Transform parent, string name)
        {
            var row = GardenUi.Box(parent, name, 0, 0, 1, 0);
            var flow = row.AddComponent<HorizontalLayoutGroup>(); flow.spacing = 12;
            flow.childControlWidth = flow.childControlHeight = true; flow.childForceExpandWidth = true;
            row.AddComponent<LayoutElement>().preferredHeight = 48;
            return row.transform;
        }
        static Button ActionButton(Transform parent, string text, Action action, bool primary = false)
        {
            var button = FigmaMissionUi.Button(parent, text, 0, 0, 160, 48, action,
                primary ? FigmaMissionUi.Green : Color.white, primary ? Color.white : FigmaMissionUi.Green, 16);
            var item = button.gameObject.AddComponent<LayoutElement>(); item.minHeight = item.preferredHeight = 48; item.flexibleWidth = 1;
            return button;
        }
        static Text Copy(Transform parent, string text, int size)
        {
            var label = FigmaMissionUi.Label(parent, text, 0, 0, 342, 24, size, FigmaMissionUi.Muted, false, TextAnchor.MiddleCenter);
            return label;
        }
        void OnEnable() { GardenVisualClock.Changed += Refresh; Refresh(); }
        void OnDisable() { GardenVisualClock.Changed -= Refresh; }
        void Update()
        {
            if (GardenVisualClock.OverrideHour.HasValue || Time.unscaledTime < nextClockUpdate) return;
            nextClockUpdate = Time.unscaledTime + 1; Refresh();
        }
        void Refresh()
        {
            if (Dial == null) return;
            int minute = GardenVisualClock.CurrentMinute;
            Dial.SetMinuteWithoutNotify(minute);
            time.text = (minute / 60).ToString("00") + ":" + (minute % 60).ToString("00");
            status.text = GardenVisualClock.OverrideHour.HasValue ? "정원 미리보기 시간" : "기기의 실제 시간";
            for (int i = 0; i < increments.Length; i++) GardenUi.StyleButton(increments[i], selected:Dial.StepMinutes == new[] { 1, 5, 15 }[i]);
            earlier.GetComponentInChildren<Text>().text = "− " + Dial.StepMinutes + "분";
            later.GetComponentInChildren<Text>().text = "+ " + Dial.StepMinutes + "분";
        }
    }
}
