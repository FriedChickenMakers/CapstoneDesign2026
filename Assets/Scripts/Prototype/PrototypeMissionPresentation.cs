using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CapstoneDesign.Prototype
{
    // Presentation only: never infers mission state from a page or a color.
    public sealed class PrototypeMissionPresentation : MonoBehaviour
    {
        public PrototypeUiSurface background;
        public TMP_Text header;
        public TMP_Text[] backgroundLabels;
        public Color idleBackground = new Color32(250, 250, 243, 255);
        public Color missionBackground = new Color32(39, 74, 59, 255);
        public Color missionForeground = new Color32(245, 246, 239, 255);
        public Button[] primaryButtons;
        public Color idleButtonFill = new Color32(49,91,68,255);
        public Color activeButtonFill = new Color32(227,238,220,255);
        public Color activeButtonInk = new Color32(49,91,68,255);
        public Button completedMissionButton;
        public Color completedButtonFill = new Color32(218,223,218,255);
        public Color completedButtonInk = new Color32(79,94,83,255);
        private Color[] idleLabelColors;
        private string idleHeader;

        public void Render(bool inProgress, bool dailyRecordCompleted = false)
        {
            if (idleLabelColors == null)
            {
                idleHeader = header.text;
                idleLabelColors = new Color[backgroundLabels.Length];
                for (int i = 0; i < backgroundLabels.Length; i++)
                    idleLabelColors[i] = backgroundLabels[i].color;
            }
            Color target = inProgress ? missionBackground : idleBackground;
            if (background.topColor != target || background.bottomColor != target)
            {
                background.topColor = background.bottomColor = target;
                background.SetVerticesDirty();
            }
            header.text = inProgress ? "미션 진행 중" : idleHeader;
            for (int i = 0; i < backgroundLabels.Length; i++)
                backgroundLabels[i].color = inProgress ? missionForeground : idleLabelColors[i];
            if(primaryButtons==null)return;
            foreach(var button in primaryButtons)
            {
                var surface=(PrototypeUiSurface)button.targetGraphic;
                bool completed=dailyRecordCompleted && !inProgress && button==completedMissionButton;
                Color fill=completed?completedButtonFill:inProgress?activeButtonFill:idleButtonFill;
                if(surface.topColor!=fill||surface.bottomColor!=fill)
                {surface.topColor=surface.bottomColor=fill;surface.SetVerticesDirty();}
                button.GetComponentInChildren<TMP_Text>(true).color=completed?completedButtonInk:inProgress?activeButtonInk:Color.white;
            }
        }
    }
}
