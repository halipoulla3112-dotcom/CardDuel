using UnityEngine;

namespace CardDuel.Prototype
{
    /// <summary>
    /// Texturas medievales generadas por código (ladrillo de piedra, losas, madera),
    /// para el escenario del castillo sin necesidad de archivos de imagen.
    /// </summary>
    public static class CastleTextures
    {
        private static Texture2D stoneBricks;
        private static Texture2D flagstones;
        private static Texture2D wood;

        /// <summary>Muro de sillares: 4 hileras de bloques desplazados, con juntas oscuras.</summary>
        public static Texture2D StoneBricks => stoneBricks != null ? stoneBricks : stoneBricks = Make("StoneBricks", 256, 128, (x, y) =>
        {
            const int rows = 4, blocksPerRow = 4, mortar = 3;
            int rowHeight = 128 / rows;
            int row = y / rowHeight;
            int blockWidth = 256 / blocksPerRow;
            int shifted = x + (row % 2 == 0 ? 0 : blockWidth / 2);
            int block = (shifted / blockWidth) % blocksPerRow;
            bool joint = y % rowHeight < mortar || shifted % blockWidth < mortar;
            if (joint) return new Color(0.16f, 0.15f, 0.14f);

            float tone = 0.36f + 0.12f * Hash(block, row);
            float grain = 0.06f * (Noise(x * 0.15f, y * 0.15f) - 0.5f) + 0.04f * (Hash(x, y) - 0.5f);
            float edge = Mathf.Min(y % rowHeight - mortar, shifted % blockWidth - mortar) < 3 ? -0.05f : 0f;
            float v = tone + grain + edge;
            return new Color(v * 1.03f, v, v * 0.94f);
        });

        /// <summary>Losas de piedra grandes e irregulares de tono, para el suelo.</summary>
        public static Texture2D Flagstones => flagstones != null ? flagstones : flagstones = Make("Flagstones", 256, 256, (x, y) =>
        {
            const int cells = 4, gap = 4;
            int size = 256 / cells;
            int cx = x / size, cy = y / size;
            if (x % size < gap || y % size < gap) return new Color(0.12f, 0.11f, 0.1f);

            float tone = 0.30f + 0.1f * Hash(cx * 7, cy * 13);
            float grain = 0.08f * (Noise(x * 0.08f, y * 0.08f) - 0.5f) + 0.04f * (Hash(x, y) - 0.5f);
            float v = tone + grain;
            return new Color(v, v * 0.98f, v * 0.93f);
        });

        /// <summary>Tablones de madera con vetas.</summary>
        public static Texture2D Wood => wood != null ? wood : wood = Make("Wood", 128, 256, (x, y) =>
        {
            const int planks = 4;
            int plankWidth = 128 / planks;
            int plank = x / plankWidth;
            if (x % plankWidth < 2) return new Color(0.12f, 0.07f, 0.04f);

            float rings = Mathf.Sin((y * 0.12f + Noise(x * 0.3f, y * 0.02f) * 6f + plank * 3f)) * 0.5f + 0.5f;
            float tone = 0.32f + 0.08f * Hash(plank, 3) + 0.08f * rings + 0.03f * (Hash(x, y) - 0.5f);
            return new Color(tone, tone * 0.62f, tone * 0.36f);
        });

        private static Texture2D Make(string name, int width, int height, System.Func<int, int, Color> pixel)
        {
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, true)
            {
                name = name,
                wrapMode = TextureWrapMode.Repeat,
                filterMode = FilterMode.Trilinear,
                anisoLevel = 4,
            };
            var pixels = new Color[width * height];
            for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
                    pixels[y * width + x] = pixel(x, y);
            texture.SetPixels(pixels);
            texture.Apply(true);
            return texture;
        }

        /// <summary>Material iluminado con la textura repetida <paramref name="tiling"/> veces.</summary>
        public static Material Material(Texture2D texture, Vector2 tiling, Color tint)
        {
            var material = Primitives.NewLit(tint);
            material.SetTexture("_BaseMap", texture);
            material.mainTexture = texture;
            material.SetTextureScale("_BaseMap", tiling);
            material.mainTextureScale = tiling;
            material.SetFloat("_Smoothness", 0.08f);
            return material;
        }

        private static float Hash(int x, int y)
        {
            unchecked
            {
                int h = x * 374761393 + y * 668265263;
                h = (h ^ (h >> 13)) * 1274126177;
                return ((h ^ (h >> 16)) & 0xffff) / 65535f;
            }
        }

        /// <summary>Ruido de valor suavizado.</summary>
        private static float Noise(float x, float y)
        {
            int x0 = Mathf.FloorToInt(x), y0 = Mathf.FloorToInt(y);
            float fx = x - x0, fy = y - y0;
            fx = fx * fx * (3f - 2f * fx);
            fy = fy * fy * (3f - 2f * fy);
            float a = Mathf.Lerp(Hash(x0, y0), Hash(x0 + 1, y0), fx);
            float b = Mathf.Lerp(Hash(x0, y0 + 1), Hash(x0 + 1, y0 + 1), fx);
            return Mathf.Lerp(a, b, fy);
        }
    }
}
