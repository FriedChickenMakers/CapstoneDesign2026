using TMPro;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;
using UnityEngine.UI;

namespace CapstoneDesign.Prototype
{
    // One family for controls and reading; the display face is deliberately
    // limited to the garden's identity, plant name and growth celebrations.
    public static class GardenTypography
    {
        static Font regular, medium, semibold, display;
        static TMP_FontAsset bodyTmp, mediumTmp, semiboldTmp, displayTmp;
        public static Font Body => regular != null ? regular : regular = Resources.Load<Font>("Fonts/WantedSans-Regular");
        public static Font Medium => medium != null ? medium : medium = Resources.Load<Font>("Fonts/WantedSans-Medium");
        public static Font Semibold => semibold != null ? semibold : semibold = Resources.Load<Font>("Fonts/WantedSans-SemiBold");
        public static Font Display => display != null ? display : display = Resources.Load<Font>("Fonts/LINESeedKR-Bd");

        public static TMP_FontAsset BodyTmp
        {
            get
            {
                if (bodyTmp != null) return bodyTmp;
                bodyTmp = Create(Body);
                mediumTmp = Create(Medium);
                semiboldTmp = Create(Semibold);
                if (bodyTmp != null)
                {
                    bodyTmp.fontWeightTable[5].regularTypeface = mediumTmp;
                    bodyTmp.fontWeightTable[6].regularTypeface = semiboldTmp;
                    bodyTmp.fontWeightTable[7].regularTypeface = semiboldTmp;
                }
                return bodyTmp;
            }
        }
        public static TMP_FontAsset DisplayTmp => displayTmp != null ? displayTmp : displayTmp = Create(Display);

        static TMP_FontAsset Create(Font source)
        {
            if (source == null) return null;
            // Dynamic, multi-page atlases add only glyphs actually displayed.
            // No complete Hangul atlas is preallocated for each weight.
            var asset = TMP_FontAsset.CreateFontAsset(source, 48, 5, GlyphRenderMode.SDFAA, 1024, 1024,
                AtlasPopulationMode.Dynamic, true);
            asset.name = source.name + " runtime SDF";
            asset.normalStyle = asset.boldStyle = 0;
            asset.normalSpacingOffset = asset.boldSpacing = 0;
            return asset;
        }

        public static void ApplyBody(Text label)
        {
            if (label == null) return;
            SetWeight(label, (label.fontStyle & FontStyle.Bold) != 0);
        }
        public static void SetWeight(Text label, bool emphasized)
        {
            if (label == null) return;
            var source = emphasized ? Semibold : Body;
            if (source != null) label.font = source;
            label.fontStyle = FontStyle.Normal;
        }
        public static void ApplyButton(Text label, bool emphasized)
        {
            if (label == null) return;
            var source = emphasized ? Semibold : Medium;
            if (source != null) label.font = source;
            label.fontStyle = FontStyle.Normal;
        }
        public static void ApplyDisplay(Text label)
        {
            if (label == null) return;
            if (Display != null) label.font = Display;
            label.fontStyle = FontStyle.Normal;
        }
        public static void ApplyBody(TMP_Text label)
        {
            if (label != null && BodyTmp != null) label.font = BodyTmp;
        }
        public static void ApplyDisplay(TMP_Text label)
        {
            if (label == null || DisplayTmp == null) return;
            label.font = DisplayTmp;
            label.fontStyle &= ~FontStyles.Bold;
            label.fontWeight = FontWeight.Regular;
        }
    }
}
