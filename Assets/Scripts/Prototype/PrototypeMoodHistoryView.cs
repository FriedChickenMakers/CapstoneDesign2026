using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CapstoneDesign.Prototype
{
    public sealed class PrototypeMoodHistoryView : MonoBehaviour
    {
        public ScrollRect scroll;
        public PrototypeMoodHistoryRow rowTemplate;
        public GameObject emptyState;
        public TMP_Text countLabel;
        private readonly List<PrototypeMoodHistoryRow> rows = new List<PrototypeMoodHistoryRow>();
        private IReadOnlyList<DailyCompletion> source;
        private bool resetScroll;
        public int VisibleCount { get; private set; }

        private void OnEnable() => resetScroll = true;

        // The scene-authored template is pooled, not a second data store.
        // Session completions are chronological; render newest first without mutating them.
        public void Render(IReadOnlyList<DailyCompletion> records)
        {
            if (source != records || VisibleCount != records.Count) resetScroll = true;
            source = records;
            VisibleCount = records.Count;
            while (rows.Count < records.Count)
            {
                var row = Instantiate(rowTemplate, scroll.content);
                row.name = "MoodRecord_" + rows.Count;
                rows.Add(row);
            }
            for (int i = 0; i < rows.Count; i++)
            {
                bool visible = i < records.Count;
                rows[i].gameObject.SetActive(visible);
                if (visible) rows[i].Bind(records[records.Count - 1 - i]);
            }
            countLabel.text = "기록 " + records.Count + "개";
            emptyState.SetActive(records.Count == 0);
            scroll.gameObject.SetActive(records.Count > 0);
        }

        private void LateUpdate()
        {
            if (!resetScroll || !scroll.gameObject.activeInHierarchy) return;
            LayoutRebuilder.ForceRebuildLayoutImmediate(scroll.content);
            scroll.StopMovement();
            scroll.verticalNormalizedPosition = 1;
            resetScroll = false;
        }
    }
}
