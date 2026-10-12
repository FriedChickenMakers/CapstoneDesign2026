using UnityEngine;

namespace CapstoneDesign.Runtime
{
    // A small repeatable density texture keeps the mobile particle shader bounded.
    // This is original procedural data, not a downloaded photograph or cloud asset.
    public static class GardenCloudNoise
    {
        public static Texture2D Create()
        {
            const int size = 64;
            var values = new float[size * size];
            uint state = 0x8f31a527;
            for (int i = 0; i < values.Length; i++)
            {
                state ^= state << 13; state ^= state >> 17; state ^= state << 5;
                values[i] = (state & 65535) / 65535f;
            }
            var pixels = new Color32[values.Length];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float sum = 0;
                    for (int dy = -1; dy <= 1; dy++)
                        for (int dx = -1; dx <= 1; dx++)
                            sum += values[((y + dy + size) % size) * size + (x + dx + size) % size] *
                                (dx == 0 ? 2 : 1) * (dy == 0 ? 2 : 1);
                    byte n = (byte)Mathf.RoundToInt(Mathf.Clamp01((sum / 16 - .5f) * 2.4f + .5f) * 255);
                    pixels[y * size + x] = new Color32(n, n, n, 255);
                }
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false, true)
            { name = "Garden cloud density", wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Bilinear };
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            return texture;
        }
    }
}
