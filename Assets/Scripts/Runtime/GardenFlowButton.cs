using UnityEngine;
using UnityEngine.UI;

namespace CapstoneDesign.Runtime
{
    // A two-line Korean choice needs its measured text height plus real padding.
    // Layout groups measure width before height, so this also works after rotation.
    public sealed class GardenFlowButton : MonoBehaviour, ILayoutElement
    {
        Text label;
        public void CalculateLayoutInputHorizontal() { }
        public void CalculateLayoutInputVertical() { }
        public float minWidth => 0;
        public float preferredWidth => 0;
        public float flexibleWidth => 1;
        public float minHeight => 88;
        public float preferredHeight
        {
            get
            {
                if (label == null) label = GetComponentInChildren<Text>();
                return Mathf.Max(88, (label == null ? 0 : label.preferredHeight) + 24);
            }
        }
        public float flexibleHeight => 0;
        public int layoutPriority => 1;
    }
}
