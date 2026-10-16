using UnityEngine;

namespace CardDuel.Prototype
{
    /// <summary>
    /// Pantalla de victoria (dorada, con confeti) o derrota (roja, con la imagen
    /// apagándose) y menú de pausa con ajustes de volumen.
    /// </summary>
    public partial class DuelPrototype
    {
        private static readonly Color VictoryGold = new Color(1f, 0.84f, 0.3f);
        private static readonly Color DefeatRed = new Color(0.95f, 0.16f, 0.12f);

        private bool playerWon;
        private string endReason = "";
        private float endStart;

        private bool paused;
        private bool pauseSettings;

        private GUIStyle endTitleStyle;
        private GUIStyle endSubtitleStyle;
        private GUIStyle menuButtonStyle;
        private GUIStyle pauseTitleStyle;
        private GUIStyle pauseButtonStyle;
        private static Texture2D raysTexture;

        private GUIStyle PauseButtonStyle => pauseButtonStyle ??= UiKit.PurpleButton(24);

        // ------------------------------------------------------ Fin de partida

        private void ShowEndScreen(bool won, string reason)
        {
            playerWon = won;
            endReason = reason;
            endStart = Time.unscaledTime;
            messageUntil = 0f;
            ClearHighlights();

            audioSource.PlayOneShot(won ? SoundFx.Victory : SoundFx.Defeat);
            if (won) AestheticFx.Confetti(cam);
            else AestheticFx.DefeatMood(1.6f);
        }

        /// <summary>
        /// Victoria: el título dorado aparece de golpe con rayos girando detrás.
        /// Derrota: el título rojo cae desde arriba rebotando, con rayos oscuros.
        /// </summary>
        private void DrawEndScreen(float width)
        {
            EnsureEndStyles();
            float t = Time.unscaledTime - endStart;
            var center = new Vector2(width * 0.5f, 250f);
            var color = playerWon ? VictoryGold : DefeatRed;

            FullScreen(width, new Color(0f, 0f, 0f, 0.55f * Mathf.Clamp01(t / 0.6f)));

            // Rayos girando detrás del título.
            float raysAlpha = Mathf.Clamp01(t / 0.8f) * (playerWon ? 0.55f : 0.45f);
            var matrix = GUI.matrix;
            GUIUtility.RotateAroundPivot(t * (playerWon ? 14f : -8f), center);
            GUI.color = new Color(color.r, color.g, color.b, raysAlpha);
            GUI.DrawTexture(new Rect(center.x - 520f, center.y - 520f, 1040f, 1040f), RaysTexture);
            GUI.matrix = matrix;
            GUI.color = Color.white;

            // Título: aparece con rebote (victoria) o cae desde arriba (derrota).
            float k = Mathf.Clamp01(t / 0.6f);
            float scale = playerWon ? EaseOutBack(k) * (1f + 0.03f * Mathf.Sin(t * 3f)) : 1f;
            float drop = playerWon ? 0f : -360f * (1f - EaseOutBounce(Mathf.Clamp01(t / 0.9f)));
            string title = playerWon ? "¡VICTORIA!" : "DERROTA";
            var titleRect = new Rect(0f, center.y - 90f + drop, width, 180f);

            matrix = GUI.matrix;
            GUIUtility.ScaleAroundPivot(Vector2.one * Mathf.Max(scale, 0.01f), titleRect.center);
            OutlinedLabel(titleRect, title, endTitleStyle, color, playerWon ? new Color(0.35f, 0.18f, 0f) : new Color(0.2f, 0f, 0f));
            GUI.matrix = matrix;

            // Motivo y botones, que aparecen después.
            float detailsAlpha = Mathf.Clamp01((t - 0.8f) / 0.4f);
            if (detailsAlpha <= 0f) return;
            GUI.color = new Color(1f, 1f, 1f, detailsAlpha);
            OutlinedLabel(new Rect(0f, center.y + 95f, width, 50f), endReason, endSubtitleStyle, Color.white, Color.black);
            GUI.color = Color.white;

            if (t < 1.2f) return;
            if (Button(new Rect(width * 0.5f - 330f, 470f, 300f, 70f), "Revancha", true, MenuButtonStyle)) Restart();
            if (Button(new Rect(width * 0.5f + 30f, 470f, 300f, 70f), "Menú", true, MenuButtonStyle)) PrototypeBootstrap.LoadMenu();
        }

        private GUIStyle MenuButtonStyle => menuButtonStyle ??= UiKit.PurpleButton(30);

        // ------------------------------------------------------------- Pausa

        private void TogglePause()
        {
            paused = !paused;
            pauseSettings = false;
            Time.timeScale = paused ? 0f : 1f;
        }

        private void DrawPause(float width)
        {
            EnsureEndStyles();
            FullScreen(width, new Color(0f, 0f, 0f, 0.55f));

            var panel = new Rect(width * 0.5f - 230f, 110f, 460f, 500f);
            GUI.Box(panel, GUIContent.none, UiKit.Panel);
            guiRects.Add(panel);
            OutlinedLabel(new Rect(panel.x, panel.y + 20f, panel.width, 70f), pauseSettings ? "Ajustes" : "Pausa", pauseTitleStyle, VictoryGold, Color.black);

            float x = panel.x + 50f, w = panel.width - 100f;
            if (pauseSettings)
            {
                UiKit.VolumeControls(new Rect(x, panel.y + 120f, w, 240f));
                if (Button(new Rect(x, panel.y + 410f, w, 64f), "← Volver", true, MenuButtonStyle)) pauseSettings = false;
                return;
            }

            if (Button(new Rect(x, panel.y + 120f, w, 64f), "Continuar", true, MenuButtonStyle)) TogglePause();
            if (Button(new Rect(x, panel.y + 200f, w, 64f), "Ajustes", true, MenuButtonStyle)) pauseSettings = true;
            if (Button(new Rect(x, panel.y + 280f, w, 64f), "Reiniciar", true, MenuButtonStyle)) Restart();
            if (Button(new Rect(x, panel.y + 360f, w, 64f), "Menú principal", true, MenuButtonStyle)) PrototypeBootstrap.LoadMenu();
        }

        // ------------------------------------------------------------ Ayudas

        private static void FullScreen(float width, Color color)
        {
            GUI.color = color;
            GUI.DrawTexture(new Rect(0f, 0f, width, 720f), Texture2D.whiteTexture);
            GUI.color = Color.white;
        }

        /// <summary>Texto con contorno (8 copias desplazadas en el color del borde) y una sombra.</summary>
        private static void OutlinedLabel(Rect rect, string text, GUIStyle style, Color fill, Color outline)
        {
            var previous = GUI.color;
            float alpha = previous.a;
            GUI.color = new Color(0f, 0f, 0f, 0.5f * alpha);
            GUI.Label(new Rect(rect.x + 6f, rect.y + 7f, rect.width, rect.height), text, style);
            GUI.color = new Color(outline.r, outline.g, outline.b, alpha);
            for (int dx = -1; dx <= 1; dx++)
                for (int dy = -1; dy <= 1; dy++)
                    if (dx != 0 || dy != 0)
                        GUI.Label(new Rect(rect.x + dx * 3f, rect.y + dy * 3f, rect.width, rect.height), text, style);
            GUI.color = new Color(fill.r, fill.g, fill.b, alpha);
            GUI.Label(rect, text, style);
            GUI.color = previous;
        }

        private static float EaseOutBack(float t)
        {
            const float c1 = 1.70158f, c3 = c1 + 1f;
            return 1f + c3 * Mathf.Pow(t - 1f, 3f) + c1 * Mathf.Pow(t - 1f, 2f);
        }

        private static float EaseOutBounce(float t)
        {
            const float n = 7.5625f, d = 2.75f;
            if (t < 1f / d) return n * t * t;
            if (t < 2f / d) return n * (t -= 1.5f / d) * t + 0.75f;
            if (t < 2.5f / d) return n * (t -= 2.25f / d) * t + 0.9375f;
            return n * (t -= 2.625f / d) * t + 0.984375f;
        }

        /// <summary>Rayos de luz que salen del centro y se desvanecen hacia fuera.</summary>
        private static Texture2D RaysTexture
        {
            get
            {
                if (raysTexture != null) return raysTexture;
                const int size = 256, rays = 16;
                raysTexture = new Texture2D(size, size, TextureFormat.RGBA32, true) { wrapMode = TextureWrapMode.Clamp };
                for (int y = 0; y < size; y++)
                    for (int x = 0; x < size; x++)
                    {
                        var p = new Vector2(x + 0.5f - size * 0.5f, y + 0.5f - size * 0.5f) / (size * 0.5f);
                        float angle = Mathf.Atan2(p.y, p.x);
                        float ray = Mathf.Pow(Mathf.Abs(Mathf.Cos(angle * rays * 0.5f)), 6f);
                        float fade = Mathf.Clamp01(1f - p.magnitude) * Mathf.Clamp01(p.magnitude * 4f);
                        raysTexture.SetPixel(x, y, new Color(1f, 1f, 1f, ray * fade));
                    }
                raysTexture.Apply();
                return raysTexture;
            }
        }

        private void EnsureEndStyles()
        {
            if (endTitleStyle != null) return;
            endTitleStyle = new GUIStyle(GUI.skin.label) { fontSize = 132, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            endTitleStyle.normal.textColor = Color.white;
            endSubtitleStyle = new GUIStyle(GUI.skin.label) { fontSize = 28, fontStyle = FontStyle.BoldAndItalic, alignment = TextAnchor.MiddleCenter };
            endSubtitleStyle.normal.textColor = Color.white;
            pauseTitleStyle = new GUIStyle(endTitleStyle) { fontSize = 56 };
        }
    }
}
