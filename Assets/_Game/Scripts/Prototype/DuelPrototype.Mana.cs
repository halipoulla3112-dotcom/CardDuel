using UnityEngine;

namespace CardDuel.Prototype
{
    /// <summary>
    /// Maná: quien empieza la partida tiene 2 en su primer turno y el otro 1; cada
    /// turno propio el máximo sube 1 (hasta 10) y se rellena. Jugar una carta de la
    /// mano cuesta su coste. Tu maná se ve como pelotitas abajo a la izquierda.
    /// </summary>
    public partial class DuelPrototype
    {
        private const int ManaCap = 10;
        private const float OrbSize = 26f;
        private const float OrbSpacing = 28.5f;

        private readonly int[] mana = new int[2];
        private readonly int[] maxMana = new int[2];
        private readonly bool[] hadTurn = new bool[2];
        private int firstPlayer;

        private float manaGainedAt = -10f;
        private float manaSpentAt = -10f;
        private int manaBeforeSpend;

        private static Texture2D orbFull, orbSpent, orbLocked, orbGlow;

        /// <summary>Al empezar el turno: sube el máximo (2/1 el primer turno) y se rellena.</summary>
        private void RefillMana(int owner)
        {
            maxMana[owner] = hadTurn[owner]
                ? Mathf.Min(ManaCap, maxMana[owner] + 1)
                : owner == firstPlayer ? 2 : 1;
            hadTurn[owner] = true;
            mana[owner] = maxMana[owner];
            if (owner == Player) manaGainedAt = Time.time;
        }

        private bool CanPay(int owner, int cost) => mana[owner] >= cost;

        private void Pay(int owner, int cost)
        {
            if (owner == Player)
            {
                manaBeforeSpend = mana[owner];
                manaSpentAt = Time.time;
            }
            mana[owner] = Mathf.Max(0, mana[owner] - Mathf.Max(0, cost));
        }

        /// <summary>
        /// Tus 10 pelotitas de maná: brillantes las disponibles, oscuras las gastadas y
        /// vacías las que aún no tienes. La nueva de cada turno aparece con un salto y las
        /// que gastas sueltan un destello.
        /// </summary>
        private void DrawMana()
        {
            if (!hadTurn[Player]) return;
            EnsureOrbTextures();

            var area = new Rect(12f, 640f, 300f, 70f);
            Panel(area);
            GUI.Label(new Rect(area.x + 12f, area.y + 2f, area.width - 24f, 28f),
                $"<b>MANÁ</b>   <color=#8fc8ff><b>{mana[Player]}</b></color> / {maxMana[Player]}", labelStyle);

            float gained = Mathf.Clamp01((Time.time - manaGainedAt) / 0.5f);
            float spent = Mathf.Clamp01((Time.time - manaSpentAt) / 0.45f);
            float pulse = 0.85f + 0.15f * Mathf.Sin(Time.time * 3f);

            for (int i = 0; i < ManaCap; i++)
            {
                var center = new Vector2(area.x + 22f + i * OrbSpacing, area.y + 50f);
                float size = OrbSize;

                Texture2D texture;
                if (i < mana[Player]) texture = orbFull;
                else if (i < maxMana[Player]) texture = orbSpent;
                else texture = orbLocked;

                // La pelotita nueva del turno salta al aparecer.
                if (i == maxMana[Player] - 1 && gained < 1f) size *= 1f + 0.7f * (1f - gained) * Mathf.Sin(gained * Mathf.PI);

                // Brillo suave alrededor de las disponibles.
                if (i < mana[Player])
                {
                    GUI.color = new Color(0.4f, 0.75f, 1f, 0.45f * pulse);
                    DrawOrb(center, size * 1.7f, orbGlow);
                    GUI.color = Color.white;
                }

                DrawOrb(center, size, texture);

                // Destello que se expande en las que se acaban de gastar.
                if (spent < 1f && i >= mana[Player] && i < manaBeforeSpend)
                {
                    GUI.color = new Color(0.6f, 0.85f, 1f, 1f - spent);
                    DrawOrb(center, size * (1f + 1.2f * spent), orbGlow);
                    GUI.color = Color.white;
                }
            }
        }

        private static void DrawOrb(Vector2 center, float size, Texture2D texture)
        {
            GUI.DrawTexture(new Rect(center.x - size * 0.5f, center.y - size * 0.5f, size, size), texture);
        }

        private static void EnsureOrbTextures()
        {
            if (orbFull != null && orbSpent != null && orbLocked != null && orbGlow != null) return;
            orbFull = OrbTexture(new Color(0.75f, 0.95f, 1f), new Color(0.1f, 0.38f, 1f), true);
            orbSpent = OrbTexture(new Color(0.16f, 0.2f, 0.35f), new Color(0.06f, 0.08f, 0.16f), false);
            orbGlow = GlowTexture();
            orbLocked = RingTexture();
        }

        /// <summary>Esfera: degradado del centro al borde, borde suave y un brillo arriba a la izquierda.</summary>
        private static Texture2D OrbTexture(Color core, Color edge, bool shiny)
        {
            const int size = 64;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, true) { wrapMode = TextureWrapMode.Clamp };
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    var p = new Vector2(x + 0.5f, y + 0.5f) / (size * 0.5f) - Vector2.one;
                    float d = p.magnitude;
                    var color = Color.Lerp(core, edge, Mathf.SmoothStep(0f, 1f, d));
                    float highlight = shiny ? Mathf.Clamp01(1f - (p - new Vector2(-0.35f, 0.35f)).magnitude / 0.35f) : 0f;
                    color = Color.Lerp(color, Color.white, highlight * 0.8f);
                    color.a = Mathf.Clamp01((1f - d) * 20f);
                    texture.SetPixel(x, y, color);
                }
            texture.Apply();
            return texture;
        }

        private static Texture2D GlowTexture()
        {
            const int size = 64;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, true) { wrapMode = TextureWrapMode.Clamp };
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float d = (new Vector2(x + 0.5f, y + 0.5f) / (size * 0.5f) - Vector2.one).magnitude;
                    float a = Mathf.Clamp01(1f - d);
                    texture.SetPixel(x, y, new Color(1f, 1f, 1f, a * a));
                }
            texture.Apply();
            return texture;
        }

        /// <summary>Hueco vacío: solo un aro tenue.</summary>
        private static Texture2D RingTexture()
        {
            const int size = 64;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, true) { wrapMode = TextureWrapMode.Clamp };
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float d = (new Vector2(x + 0.5f, y + 0.5f) / (size * 0.5f) - Vector2.one).magnitude;
                    float ring = Mathf.Clamp01(1f - Mathf.Abs(d - 0.85f) * 12f);
                    texture.SetPixel(x, y, new Color(0.55f, 0.65f, 0.85f, ring * 0.7f + (d < 0.85f ? 0.12f : 0f)));
                }
            texture.Apply();
            return texture;
        }
    }
}
