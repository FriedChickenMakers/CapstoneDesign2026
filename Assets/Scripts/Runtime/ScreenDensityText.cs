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

        float Density
        {
            get
            {
                if (canvas == null || font == null || !font.dynamic) return pixelsPerUnit;
                var relative = canvas.transform.InverseTransformVector(transform.TransformVector(Vector3.up));
                float density = canvas.scaleFactor * relative.magnitude;
                // Whole raster font sizes avoid repeatedly filtering a fractional glyph size.
                return Mathf.Max(1, Mathf.Ceil(fontSize * density)) / Mathf.Max(1, fontSize);
            }
        }

        void LateUpdate()
        {
            float density = Density;
            if (Mathf.Approximately(lastDensity, density)) return;
            lastDensity = density;
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            // InputField computes caret/selection positions using Text.pixelsPerUnit;
            // preserve its generator contract until the input renderer is migrated too.
            if (GetComponentInParent<InputField>() != null)
            {
                base.OnPopulateMesh(mesh);
                return;
            }
            mesh.Clear();
            if (font == null) return;
            float density = Density;
            lastDensity = density;
            var settings = GetGenerationSettings(rectTransform.rect.size);
            settings.scaleFactor = density;
            m_DisableFontTextureRebuiltCallback = true;
            try
            {
                cachedTextGenerator.PopulateWithErrors(text, settings, gameObject);
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
