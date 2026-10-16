using UnityEngine;

namespace CardDuel.Prototype
{
    /// <summary>
    /// Texturas generadas por código para el escenario (hierba, ladrillo, madera,
    /// paja, tierra...), sin necesidad de archivos de imagen.
    /// </summary>
    public static class EnvironmentTextures
    {
        private static Texture2D grass;
        private static Texture2D redBricks;
        private static Texture2D wood;
        private static Texture2D planks;
        private static Texture2D hay;
        private static Texture2D dirt;

        /// <summary>Césped con matices de verde, briznas y alguna florecilla.</summary>
        public static Texture2D Grass => grass != null ? grass : grass = Make("Grass", 256, 256, (x, y) =>
        {
            float patches = Noise(x * 0.03f, y * 0.03f);
            float blades = Noise(x * 0.9f, y * 0.25f);
            float g = 0.6f + 0.16f * patches + 0.1f * (blades - 0.5f) + 0.05f * (Hash(x, y) - 0.5f);
            var color = new Color(0.38f * g + 0.08f, g, 0.2f * g);

            // Florecillas sueltas (blancas y amarillas).
            float flower = Hash(x / 6, y / 6);
            if (flower > 0.985f && (x % 6) is >= 2 and <= 3 && (y % 6) is >= 2 and <= 3)
                color = flower > 0.993f ? new Color(1f, 0.92f, 0.35f) : new Color(0.97f, 0.97f, 0.95f);
            return color;
        });

        /// <summary>Ladrillo rojo anaranjado con juntas claras.</summary>
        public static Texture2D RedBricks => redBricks != null ? redBricks : redBricks = Make("RedBricks", 256, 128, (x, y) =>
        {
            const int rows = 8, bricksPerRow = 4, mortar = 3;
            int rowHeight = 128 / rows;
            int row = y / rowHeight;
            int brickWidth = 256 / bricksPerRow;
            int shifted = x + (row % 2 == 0 ? 0 : brickWidth / 2);
            int brick = (shifted / brickWidth) % bricksPerRow;
            if (y % rowHeight < mortar || shifted % brickWidth < mortar) return new Color(0.82f, 0.76f, 0.68f);

            float tone = 0.62f + 0.16f * Hash(brick, row) + 0.07f * (Noise(x * 0.2f, y * 0.2f) - 0.5f) + 0.04f * (Hash(x, y) - 0.5f);
            return new Color(tone, tone * 0.38f, tone * 0.27f);
        });

        /// <summary>Tablones de madera natural con vetas.</summary>
        public static Texture2D Wood => wood != null ? wood : wood = Make("Wood", 128, 256, (x, y) =>
        {
            float tone = PlankTone(x, y, 4);
            if (tone < 0f) return new Color(0.16f, 0.1f, 0.06f);
            return new Color(tone, tone * 0.66f, tone * 0.4f);
        });

        /// <summary>Tablones pintados de blanco (se tiñen con el color del material).</summary>
        public static Texture2D Planks => planks != null ? planks : planks = Make("Planks", 128, 256, (x, y) =>
        {
            float tone = PlankTone(x, y, 5);
            if (tone < 0f) return new Color(0.45f, 0.45f, 0.45f);
            float v = 0.78f + tone * 0.4f;
            return new Color(v, v, v);
        });

        /// <summary>Paja: amarilla con hebras.</summary>
        public static Texture2D Hay => hay != null ? hay : hay = Make("Hay", 128, 128, (x, y) =>
        {
            float strands = Noise(x * 1.4f, y * 0.12f);
            float v = 0.72f + 0.2f * (strands - 0.5f) + 0.06f * (Hash(x, y) - 0.5f);
            return new Color(v * 1.08f, v * 0.86f, v * 0.38f);
        });

        /// <summary>Tierra de huerto.</summary>
        public static Texture2D Dirt => dirt != null ? dirt : dirt = Make("Dirt", 128, 128, (x, y) =>
        {
            float v = 0.36f + 0.1f * (Noise(x * 0.1f, y * 0.1f) - 0.5f) + 0.08f * (Hash(x, y) - 0.5f);
            return new Color(v * 1.1f, v * 0.78f, v * 0.52f);
        });

        /// <summary>Tono de una tabla con vetas, o -1 en la junta entre tablas.</summary>
        private static float PlankTone(int x, int y, int count)
        {
            int width = 128 / count;
            int plank = x / width;
            if (x % width < 2) return -1f;
            float rings = Mathf.Sin(y * 0.12f + Noise(x * 0.3f, y * 0.02f) * 6f + plank * 3f) * 0.5f + 0.5f;
            return 0.36f + 0.08f * Hash(plank, 3) + 0.08f * rings + 0.03f * (Hash(x, y) - 0.5f);
        }

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
