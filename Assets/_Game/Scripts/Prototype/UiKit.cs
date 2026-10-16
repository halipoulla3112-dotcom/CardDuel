using UnityEngine;

namespace CardDuel.Prototype
{
    /// <summary>Estilos de interfaz compartidos (botones morados redondeados con borde dorado).</summary>
    public static class UiKit
    {
        public static readonly Color ButtonFill = new Color(0.32f, 0.12f, 0.5f, 0.95f);
        public static readonly Color ButtonHover = new Color(0.48f, 0.2f, 0.7f, 0.98f);
        public static readonly Color ButtonActive = new Color(0.6f, 0.3f, 0.85f, 1f);
        public static readonly Color Gold = new Color(0.95f, 0.75f, 0.35f);

        /// <summary>Botón morado redondeado con borde dorado y texto claro.</summary>
        public static GUIStyle PurpleButton(int fontSize)
        {
            var style = new GUIStyle(GUI.skin.button)
            {
                fontSize = fontSize,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                border = new RectOffset(14, 14, 14, 14),
            };
            style.normal.background = RoundedTexture(ButtonFill, Gold);
            style.hover.background = RoundedTexture(ButtonHover, new Color(1f, 0.85f, 0.45f));
            style.active.background = RoundedTexture(ButtonActive, new Color(1f, 0.9f, 0.55f));
            style.normal.textColor = new Color(1f, 0.93f, 0.75f);
            style.hover.textColor = Color.white;
            style.active.textColor = Color.white;
            return style;
        }

        private static GUIStyle sliderStyle;
        private static GUIStyle thumbStyle;
        private static GUIStyle labelStyle;
        private static GUIStyle panelStyle;

        /// <summary>Panel oscuro redondeado con borde dorado.</summary>
        /// <remarks>
        /// Al recargar la escena Unity libera las texturas creadas por código, así que los
        /// estilos guardados en campos estáticos se regeneran si han perdido su fondo.
        /// </remarks>
        public static GUIStyle Panel
        {
            get
            {
                if (panelStyle != null && panelStyle.normal.background != null) return panelStyle;
                panelStyle = new GUIStyle(GUI.skin.box) { border = new RectOffset(14, 14, 14, 14) };
                panelStyle.normal.background = RoundedTexture(new Color(0.1f, 0.05f, 0.16f, 0.94f), Gold);
                return panelStyle;
            }
        }

        /// <summary>Barras de "Volumen general" y "Efectos" dentro de un rectángulo (unos 340 × 150).</summary>
        public static void VolumeControls(Rect area)
        {
            EnsureSliderStyles();
            float master = Slider(new Rect(area.x, area.y, area.width, 70f), "Volumen general", GameSettings.MasterVolume);
            if (!Mathf.Approximately(master, GameSettings.MasterVolume)) GameSettings.MasterVolume = master;
            float effects = Slider(new Rect(area.x, area.y + 80f, area.width, 70f), "Efectos", GameSettings.EffectsVolume);
            if (!Mathf.Approximately(effects, GameSettings.EffectsVolume)) GameSettings.EffectsVolume = effects;
        }

        private static float Slider(Rect rect, string label, float value)
        {
            GUI.Label(new Rect(rect.x, rect.y, rect.width, 30f), $"{label}   <b>{Mathf.RoundToInt(value * 100f)}%</b>", labelStyle);
            float result = GUI.HorizontalSlider(new Rect(rect.x, rect.y + 36f, rect.width, 26f), value, 0f, 1f, sliderStyle, thumbStyle);
            if (!Mathf.Approximately(result, value)) GameSettings.Save();
            return result;
        }

        private static void EnsureSliderStyles()
        {
            if (sliderStyle != null && sliderStyle.normal.background != null && thumbStyle.normal.background != null) return;
            sliderStyle = new GUIStyle(GUI.skin.horizontalSlider)
            {
                fixedHeight = 16f,
                border = new RectOffset(8, 8, 8, 8),
                margin = new RectOffset(0, 0, 5, 0),
                normal = { background = RoundedTexture(new Color(0.25f, 0.15f, 0.35f), Gold) },
            };
            thumbStyle = new GUIStyle(GUI.skin.horizontalSliderThumb)
            {
                fixedWidth = 28f,
                fixedHeight = 28f,
                border = new RectOffset(13, 13, 13, 13),
                normal = { background = RoundedTexture(new Color(1f, 0.85f, 0.45f), Color.white) },
                hover = { background = RoundedTexture(new Color(1f, 0.92f, 0.6f), Color.white) },
                active = { background = RoundedTexture(Color.white, Gold) },
            };
            labelStyle = new GUIStyle(GUI.skin.label) { fontSize = 22, richText = true };
            labelStyle.normal.textColor = Color.white;
        }

        /// <summary>Textura con esquinas redondeadas y borde, para usar como fondo de 9 partes.</summary>
        public static Texture2D RoundedTexture(Color fill, Color border)
        {
            const int size = 48, radius = 13, borderWidth = 3;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    // Distancia al borde del rectángulo redondeado (negativa dentro).
                    float dx = Mathf.Max(radius - x - 0.5f, x + 0.5f - (size - radius), 0f);
                    float dy = Mathf.Max(radius - y - 0.5f, y + 0.5f - (size - radius), 0f);
                    float outside = Mathf.Sqrt(dx * dx + dy * dy) - radius;
                    float edge = Mathf.Min(Mathf.Min(x, y), Mathf.Min(size - 1 - x, size - 1 - y));
                    float inner = outside > -borderWidth || (dx == 0f && dy == 0f && edge < borderWidth) ? 1f : 0f;

                    var color = Color.Lerp(fill, border, inner);
                    color.a *= Mathf.Clamp01(0.5f - outside);
                    texture.SetPixel(x, y, color);
                }
            texture.Apply();
            return texture;
        }
    }
}
