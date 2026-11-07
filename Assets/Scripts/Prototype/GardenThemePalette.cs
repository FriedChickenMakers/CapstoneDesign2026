using UnityEngine;

namespace CapstoneDesign.Prototype
{
    // Original garden workshop palette. All geometry using it is authored in this repository.
    public static class GardenThemePalette
    {
        public static readonly Color Background = new Color32(247, 241, 222, 255);
        public static readonly Color Paper = new Color32(255, 251, 235, 255);
        public static readonly Color PaperShade = new Color32(243, 234, 207, 255);
        public static readonly Color Ink = new Color32(49, 66, 48, 255);
        public static readonly Color Muted = new Color32(107, 111, 84, 255);
        public static readonly Color Forest = new Color32(57, 103, 75, 255);
        public static readonly Color Sage = new Color32(118, 150, 97, 255);
        public static readonly Color Leaf = new Color32(157, 184, 112, 255);
        public static readonly Color Mint = new Color32(211, 225, 169, 255);
        public static readonly Color Wood = new Color32(218, 178, 118, 255);
        public static readonly Color WoodLight = new Color32(244, 216, 162, 255);
        public static readonly Color WoodDark = new Color32(145, 109, 68, 255);
        public static readonly Color Border = new Color32(202, 187, 145, 255);
        public static readonly Color Terracotta = new Color32(179, 103, 72, 255);
        public static readonly Color Gold = new Color32(228, 188, 93, 255);
        // Controls follow their surrounding panel: warm neutral on paper,
        // muted green inside the main green mission card.
        public static readonly Color HeaderSurface = new Color32(216, 222, 206, 255);
        public static readonly Color SecondarySurface = new Color32(229, 230, 216, 255);
        public static readonly Color GrowthSurface = new Color32(208, 223, 199, 255);
    }
}
