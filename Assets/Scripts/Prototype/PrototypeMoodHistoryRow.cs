using System;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

namespace CapstoneDesign.Prototype
{
    // Read-only presentation of a completed day; never edits mood or awards growth.
    public sealed class PrototypeMoodHistoryRow : MonoBehaviour, ILayoutElement
    {
        public TMP_Text dateLabel;
        public TMP_Text timeLabel;
        public TMP_Text moodLabel;
        [FormerlySerializedAs("activityLabel")] public TMP_Text noteLabel;
        public PrototypeMoodFace face;
        public GameObject skippedIcon;
        public float noteTop = 174;
        public float noteHorizontalPadding = 44;
        public float noteBottomPadding = 24;
        public float emptyHeight = 184;
        public DateTime Day { get; private set; }
        public Mood RecordedMood { get; private set; }
        private static readonly CultureInfo Korean = CultureInfo.GetCultureInfo("ko-KR");

        public void Bind(DailyCompletion record)
        {
            Day = record.Day;
            RecordedMood = record.Mood;
            dateLabel.text = record.Day.ToString("yyyy.MM.dd (ddd)", Korean);
            timeLabel.text = record.CompletedAt.ToString("HH:mm", CultureInfo.InvariantCulture);
            bool skipped = record.Mood == Mood.None;
            moodLabel.text = skipped ? "감정 미선택" : PrototypeDefinition.MoodLabels[(int)record.Mood];
            noteLabel.text = record.Note;
            noteLabel.gameObject.SetActive(!string.IsNullOrEmpty(record.Note));
            face.gameObject.SetActive(!skipped);
            skippedIcon.SetActive(skipped);
            if (!skipped)
            {
                face.mood = record.Mood;
                face.SetSelected(false);
                face.SetVerticesDirty();
            }
            LayoutRebuilder.MarkLayoutForRebuild((RectTransform)transform);
        }

        // Height is driven by actual wrapped text, not a fixed two-line truncation.
        public void CalculateLayoutInputHorizontal() { }
        public void CalculateLayoutInputVertical()
        {
            float width = Mathf.Max(1, ((RectTransform)transform).rect.width - noteHorizontalPadding);
            float height = noteLabel.gameObject.activeSelf
                ? Mathf.Ceil(noteLabel.GetPreferredValues(noteLabel.text, width, float.PositiveInfinity).y) : 0;
            noteLabel.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, height);
            preferredHeight = noteLabel.gameObject.activeSelf ? noteTop + height + noteBottomPadding : emptyHeight;
        }
        public float minWidth => -1;
        public float preferredWidth => -1;
        public float flexibleWidth => -1;
        public float minHeight => emptyHeight;
        public float preferredHeight { get; private set; } = 184;
        public float flexibleHeight => -1;
        public int layoutPriority => 2;
    }
}
