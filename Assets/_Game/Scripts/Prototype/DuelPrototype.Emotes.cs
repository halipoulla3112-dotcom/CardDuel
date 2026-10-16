using UnityEngine;

namespace CardDuel.Prototype
{
    /// <summary>
    /// Frases del jugador al estilo Hearthstone: al pulsar el retrato se abre un
    /// abanico de opciones y la elegida aparece en un bocadillo junto al retrato.
    /// </summary>
    public partial class DuelPrototype
    {
        private const float EmoteCooldown = 1.5f;
        private const float BubbleDuration = 3.2f;
        private const float EmoteMenuRadius = 150f;
        private const float EmoteMenuSpan = 235f;

        private static readonly (string label, string line)[] Emotes =
        {
            ("Saludos", "¡Saludos, retador!"),
            ("Bien jugado", "Bien jugado."),
            ("Gracias", "Te lo agradezco."),
            ("Vaya", "Vaya... eso ha sido impresionante."),
            ("Uy", "Uy, eso no lo vi venir."),
            ("Amenaza", "¡Mi espada no conoce la derrota!"),
        };

        private bool emoteMenuOpen;
        private string bubbleText = "";
        private float bubbleStart = -10f;
        private float lastEmoteTime = -10f;

        private GUIStyle emoteStyle;
        private GUIStyle bubbleStyle;

        /// <summary>Retrato pulsable, abanico de frases y bocadillo. Se llama justo después de dibujar el retrato.</summary>
        private void DrawEmotes(Rect portraitRect)
        {
            EnsureEmoteStyles();
            bool interactive = portraitAlpha >= 0.95f && !gameOver;
            if (!interactive) emoteMenuOpen = false;

            // El retrato entero es un botón invisible que abre y cierra el abanico.
            if (interactive && Button(portraitRect, "", true, GUIStyle.none))
            {
                emoteMenuOpen = !emoteMenuOpen;
                audioSource.PlayOneShot(SoundFx.Emote, 0.4f);
            }

            if (emoteMenuOpen) DrawEmoteMenu(portraitRect);
            DrawBubble(portraitRect);
        }

        /// <summary>Opciones en una columna curvada a la izquierda del retrato (como un abanico).</summary>
        private void DrawEmoteMenu(Rect portraitRect)
        {
            var center = portraitRect.center;
            bool ready = Time.time - lastEmoteTime >= EmoteCooldown;
            int last = Emotes.Length - 1;
            for (int i = 0; i <= last; i++)
            {
                float k = i / (float)last;
                float x = center.x - EmoteMenuRadius - 40f * Mathf.Sin(Mathf.PI * k);
                float y = center.y - 135f + k * EmoteMenuSpan;
                var rect = new Rect(x - 78f, y - 20f, 156f, 40f);
                if (Button(rect, Emotes[i].label, ready, emoteStyle)) Say(Emotes[i].line);
            }
        }

        private void Say(string line)
        {
            bubbleText = line;
            bubbleStart = Time.time;
            lastEmoteTime = Time.time;
            emoteMenuOpen = false;
            audioSource.PlayOneShot(SoundFx.Emote);
        }

        /// <summary>Bocadillo junto al retrato que aparece, se queda un momento y se desvanece.</summary>
        private void DrawBubble(Rect portraitRect)
        {
            float elapsed = Time.time - bubbleStart;
            if (elapsed > BubbleDuration || portraitAlpha <= 0f) return;

            float alpha = Mathf.Min(Mathf.Clamp01(elapsed / 0.15f), Mathf.Clamp01((BubbleDuration - elapsed) / 0.4f)) * portraitAlpha;
            float pop = 1f + 0.12f * Mathf.Clamp01(1f - elapsed / 0.15f);

            var size = new Vector2(330f, 74f) * pop;
            var rect = new Rect(portraitRect.x - size.x + 40f, portraitRect.y - size.y + 10f, size.x, size.y);

            var previous = GUI.color;
            GUI.color = new Color(1f, 1f, 1f, alpha);

            // Pico del bocadillo apuntando al retrato.
            var tail = new Rect(rect.xMax - 64f, rect.yMax - 14f, 26f, 26f);
            var matrix = GUI.matrix;
            GUIUtility.RotateAroundPivot(45f, tail.center);
            GUI.Box(tail, GUIContent.none, bubbleStyle);
            GUI.matrix = matrix;

            GUI.Box(rect, bubbleText, bubbleStyle);
            GUI.color = previous;
        }

        private void EnsureEmoteStyles()
        {
            if (emoteStyle != null) return;

            emoteStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 19,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                border = new RectOffset(14, 14, 14, 14),
            };
            emoteStyle.normal.background = RoundedTexture(new Color(0.32f, 0.12f, 0.5f, 0.95f), new Color(0.95f, 0.75f, 0.35f));
            emoteStyle.hover.background = RoundedTexture(new Color(0.48f, 0.2f, 0.7f, 0.98f), new Color(1f, 0.85f, 0.45f));
            emoteStyle.active.background = RoundedTexture(new Color(0.6f, 0.3f, 0.85f, 1f), new Color(1f, 0.9f, 0.55f));
            emoteStyle.normal.textColor = new Color(1f, 0.93f, 0.75f);
            emoteStyle.hover.textColor = Color.white;
            emoteStyle.active.textColor = Color.white;

            bubbleStyle = new GUIStyle(GUI.skin.box)
            {
                fontSize = 20,
                fontStyle = FontStyle.BoldAndItalic,
                alignment = TextAnchor.MiddleCenter,
                wordWrap = true,
                padding = new RectOffset(16, 16, 10, 10),
                border = new RectOffset(14, 14, 14, 14),
            };
            bubbleStyle.normal.background = RoundedTexture(new Color(1f, 0.97f, 0.9f), new Color(0.45f, 0.28f, 0.12f));
            bubbleStyle.normal.textColor = new Color(0.25f, 0.14f, 0.06f);
        }

        /// <summary>Textura con esquinas redondeadas y borde, para usar como fondo de 9 partes.</summary>
        private static Texture2D RoundedTexture(Color fill, Color border)
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
