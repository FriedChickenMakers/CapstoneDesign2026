using UnityEngine;
using UnityEngine.Experimental.Rendering;

namespace CapstoneDesign.Runtime
{
    // Original periodic density volume. Created once, with no external image asset.
    public static class GardenCloudNoise
    {
        public static Texture3D Create()
        {
            const int size = 32, mask = size - 1;
            var values = new float[size * size * size];
            uint state = 0x8f31a527;
            for (int i = 0; i < values.Length; i++)
            {
                state ^= state << 13; state ^= state >> 17; state ^= state << 5;
                values[i] = (state & 65535) / 65535f;
            }
            var pixels = new Color32[values.Length];
            for (int z = 0; z < size; z++) for (int y = 0; y < size; y++) for (int x = 0; x < size; x++)
            {
                float sum = 0;
                for (int dz = -1; dz <= 1; dz++) for (int dy = -1; dy <= 1; dy++) for (int dx = -1; dx <= 1; dx++)
                    sum += values[(((z + dz) & mask) * size + ((y + dy) & mask)) * size + ((x + dx) & mask)] *
                        (dx == 0 ? 2 : 1) * (dy == 0 ? 2 : 1) * (dz == 0 ? 2 : 1);
                byte n = (byte)Mathf.RoundToInt(Mathf.Clamp01((sum / 64 - .5f) * 3.2f + .5f) * 255);
                pixels[(z * size + y) * size + x] = new Color32(n, n, n, 255);
            }
            // Density is scalar data, never an sRGB color texture.
            var texture = new Texture3D(size, size, size, GraphicsFormat.R8G8B8A8_UNorm, TextureCreationFlags.None)
            { name = "Garden mist density volume", wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Bilinear };
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            return texture;
        }
    }
}
