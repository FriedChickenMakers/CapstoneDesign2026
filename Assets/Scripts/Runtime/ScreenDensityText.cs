using UnityEngine;
using UnityEngine.UI;

namespace CapstoneDesign.Runtime
{
    // Legacy Text only accounts for Canvas.scaleFactor. Our nested design frames
    // also scale, so rasterize at their final density instead of enlarging glyphs.
    [ExecuteAlways]
    public sealed class ScreenDensityText : Text
    {
        readonly UIVertex[] quad = new UIVertex[4];
        float lastDensity;
        // InputField computes caret/selection positions using Text.pixelsPerUnit.
        // Its layout and rendering must both keep that same generator contract.
        bool UsesInputFieldMetrics => GetComponentInParent<InputField>() != null;

        float Density
        {
            get
            {
                if (UsesInputFieldMetrics || canvas == null || font == null || !font.dynamic) return pixelsPerUnit;
                var relative = canvas.transform.InverseTransformVector(transform.TransformVector(Vector3.up));
                float density = canvas.scaleFactor * relative.magnitude;
                // Whole raster font sizes avoid repeatedly filtering a fractional glyph size.
                return Mathf.Max(1, Mathf.Ceil(fontSize * density)) / Mathf.Max(1, fontSize);
            }
        }

        public override float preferredWidth
        {
            get
            {
                if (UsesInputFieldMetrics) return base.preferredWidth;
                float density = Density;
                var settings = GetGenerationSettings(Vector2.zero);
                settings.scaleFactor = density;
                return cachedTextGeneratorForLayout.GetPreferredWidth(text, settings) / density;
            }
        }

        public override float preferredHeight
        {
            get
            {
                if (UsesInputFieldMetrics) return base.preferredHeight;
                float density = Density;
                var settings = GetGenerationSettings(new Vector2(GetPixelAdjustedRect().width, 0));
                settings.scaleFactor = density;
                return cachedTextGeneratorForLayout.GetPreferredHeight(text, settings) / density;
            }
        }

        void LateUpdate()
        {
            float density = Density;
            if (Mathf.Approximately(lastDensity, density)) return;
            lastDensity = density;
            SetVerticesDirty();
            SetLayoutDirty();
        }

        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            if (UsesInputFieldMetrics)
            {
                base.OnPopulateMesh(mesh);
                return;
            }
            if (font == null)
            {
                mesh.Clear();
                return;
            }
            float density = Density;
            var settings = GetGenerationSettings(rectTransform.rect.size);
            settings.scaleFactor = density;
            m_DisableFontTextureRebuiltCallback = true;
            try
            {
                cachedTextGenerator.PopulateWithErrors(text, settings, gameObject);
                // Populating can rebuild the font atlas, which synchronously
                // renders another Text into Graphic's shared VertexHelper.
                // Clear after those callbacks, as Unity's Text renderer does.
                mesh.Clear();
                var vertices = cachedTextGenerator.verts;
                for (int start = 0; start + 3 < vertices.Count; start += 4)
                {
                    for (int corner = 0; corner < 4; corner++)
                    {
                        quad[corner] = vertices[start + corner];
                        quad[corner].position /= density;
                    }
                    mesh.AddUIVertexQuad(quad);
                }
            }
            finally { m_DisableFontTextureRebuiltCallback = false; }
        }
    }
}
