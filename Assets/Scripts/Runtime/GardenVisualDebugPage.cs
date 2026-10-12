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

        public void Build(Action close, Action garden)
        {
            var frame = (RectTransform)transform;
            frame.offsetMax = new Vector2(0, GardenUi.HasSharedHeader(transform) ? -96 : 0);
            var layout = gameObject.AddComponent<GardenActivityLayout>();
            var header = GardenUi.Box(transform, "UI debug heading", 0, 1, 1, 0);
            var headerRect = (RectTransform)header.transform; headerRect.pivot = new Vector2(.5f, 1);
            var heading = GardenUi.VerticalFlow(header.transform, 12, 24, 16);
            ActionButton(heading, "‹  설정으로", close);
            GardenUi.FlowLabel(heading, "UI 디버그", GardenUi.TitleSize).fontStyle = FontStyle.Bold;
            var body = GardenUi.ScrollPage(transform, "UI debug scroll", 0, 1, 0);
            GardenUi.VerticalFlow(body, 20, 16, 24);
            body.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            GardenUi.FlowLabel(body, "정원의 시간", GardenUi.HeadingSize).fontStyle = FontStyle.Bold;
            GardenUi.FlowLabel(body, "원형 눈금을 움직여 낮과 밤을 확인해 보세요. 활동 날짜와 알림은 바뀌지 않아요.", GardenUi.BodySize, GardenUi.Muted);
            var holder = GardenUi.Box(body, "24 hour dial", 0, 0, 1, 0);
            holder.AddComponent<LayoutElement>().preferredHeight = 410;
            var face = GardenUi.Box(holder.transform, "Circular time slider", .5f, .5f, 0, 0);
            ((RectTransform)face.transform).sizeDelta = new Vector2(400, 400);
            Dial = face.AddComponent<CircularTimeSlider>();
            foreach (var hour in new[] { 0, 6, 12, 18 })
            {
                float angle = hour / 24f * Mathf.PI * 2;
                var label = GardenUi.Label(face.transform, hour.ToString("00"), .5f + Mathf.Sin(angle) * .47f - .08f,
                    .5f + Mathf.Cos(angle) * .47f - .05f, .16f, .1f, GardenUi.CaptionSize);
                label.alignment = TextAnchor.MiddleCenter;
            }
            time = GardenUi.Label(face.transform, "00:00", .18f, .37f, .64f, .26f, GardenUi.TitleSize);
            time.fontStyle = FontStyle.Bold; time.alignment = TextAnchor.MiddleCenter;
            status = GardenUi.FlowLabel(body, "", GardenUi.BodySize, GardenUi.Muted); status.alignment = TextAnchor.MiddleCenter;
            var stepRow = Row(body, "Time increments"); increments = new Button[3];
            int index = 0;
            foreach (int step in new[] { 1, 5, 15 })
            {
                int value = step;
                increments[index++] = ActionButton(stepRow, step + "분", () => { Dial.StepMinutes = value; Refresh(); });
            }
            var nudge = Row(body, "Time adjustments");
            earlier = ActionButton(nudge, "− 15분", () => Dial.Nudge(-1));
            later = ActionButton(nudge, "+ 15분", () => Dial.Nudge(1));
            var footer = GardenUi.Box(transform, "UI debug actions", 0, 0, 1, 0, GardenUi.Background);
            var footerRect = (RectTransform)footer.transform; footerRect.pivot = new Vector2(.5f, 0);
            var actions = GardenUi.VerticalFlow(footer.transform, 12, 16, 24);
            ActionButton(actions, "정원 보기", garden, true);
            ActionButton(actions, "실제 시간으로", GardenVisualClock.UseSystemTime);
            layout.Header = headerRect; layout.Body = (RectTransform)body.parent; layout.Footer = footerRect;
            Dial.Changed += GardenVisualClock.SetOverrideMinutes;
            Refresh();
        }
        static Transform Row(Transform parent, string name)
        {
            var row = GardenUi.Box(parent, name, 0, 0, 1, 0);
            var flow = row.AddComponent<HorizontalLayoutGroup>(); flow.spacing = 16;
            flow.childControlWidth = flow.childControlHeight = true; flow.childForceExpandWidth = true;
            return row.transform;
        }
        static Button ActionButton(Transform parent, string text, Action action, bool primary = false)
        {
            var button = GardenUi.Button(parent, text, 0, 0, 1, 0, action, primary);
            button.gameObject.AddComponent<GardenFlowButton>(); return button;
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
