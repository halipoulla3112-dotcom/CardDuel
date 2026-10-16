using System;
using CardDuel.Cards;
using UnityEngine;

namespace CardDuel.Prototype
{
    /// <summary>
    /// Ayudas visuales de la partida: brillo en las cartas que puedes jugar y en los
    /// monstruos que pueden atacar, rótulo grande al cambiar de turno y barras de LP
    /// que bajan con animación.
    /// </summary>
    public partial class DuelPrototype
    {
        private static readonly Color PlayableGlow = new Color(0.35f, 1.6f, 0.8f);
        private static readonly Color AttackGlow = new Color(1.6f, 1.25f, 0.3f);
        private const float BannerDuration = 1.7f;

        private readonly float[] shownLife = { StartingLifePoints, StartingLifePoints };
        private readonly float[] ghostLife = { StartingLifePoints, StartingLifePoints };
        private readonly float[] lifeHitAt = { -10f, -10f };
        private readonly int[] lastLife = { StartingLifePoints, StartingLifePoints };

        private float bannerStart = -10f;
        private int bannerOwner;
        private GUIStyle bannerStyle;
        private GUIStyle barTextStyle;

        // ---------------------------------------------------- Cartas jugables

        /// <summary>Enciende el brillo de las cartas que puedes usar ahora mismo y apaga el resto.</summary>
        private void UpdateGlows()
        {
            bool canAct = IsPlayerTurn && !inCinematic && !paused && (state == State.Idle || state == State.CardSelected);
            float pulse = 0.65f + 0.35f * Mathf.Sin(Time.time * 4f);

            foreach (var card in Hand)
                card.SetGlow(canAct && card != dragged && CanPlayFromHand(card) ? PlayableGlow * pulse : (Color?)null);

            foreach (var zone in board.MonsterZones[Player])
                if (!zone.IsFree) zone.Card.SetGlow(canAct && CanAttack(zone.Card) ? AttackGlow * pulse : (Color?)null);
        }

        /// <summary>¿Se puede jugar ya esta carta de la mano? (maná, invocación del turno, tributos y hueco libre).</summary>
        private bool CanPlayFromHand(CardView card)
        {
            var data = card.Data;
            if (!CanPay(Player, data.cost)) return false;

            if (data.cardType == CardType.Monster)
            {
                if (normalSummonUsed) return false;
                int needed = TributesFor(data.level);
                bool freeZone = Array.Exists(board.MonsterZones[Player], z => z.IsFree);
                return needed > 0 ? OwnMonsters(Player).Count >= needed : freeZone;
            }
            return Array.Exists(board.SpellTrapZones[Player], z => z.IsFree);
        }

        // ---------------------------------------------------- Rótulo de turno

        private void ShowTurnBanner(int owner)
        {
            bannerOwner = owner;
            bannerStart = Time.time;
            audioSource.PlayOneShot(SoundFx.TurnBanner, 0.8f);
        }

        /// <summary>Una franja cruza la pantalla con "TU TURNO" o "TURNO DEL RIVAL": entra, se queda y sale.</summary>
        private void DrawTurnBanner(float width)
        {
            float t = Time.time - bannerStart;
            if (t > BannerDuration || bannerStart < 0f) return;
            EnsureFeedbackStyles();

            float enter = Mathf.Clamp01(t / 0.3f), exit = Mathf.Clamp01((t - (BannerDuration - 0.3f)) / 0.3f);
            float alpha = Mathf.Min(enter, 1f - exit);
            float slide = (1f - EaseOutBack(enter)) * -width + exit * exit * width;

            bool mine = bannerOwner == Player;
            var band = new Rect(0f, 260f, width, 130f);
            GUI.color = new Color(0f, 0f, 0f, 0.55f * alpha);
            GUI.DrawTexture(band, Texture2D.whiteTexture);
            GUI.color = mine ? new Color(0.35f, 0.65f, 1f, 0.9f * alpha) : new Color(1f, 0.3f, 0.25f, 0.9f * alpha);
            GUI.DrawTexture(new Rect(0f, band.y, width, 4f), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(0f, band.yMax - 4f, width, 4f), Texture2D.whiteTexture);
            GUI.color = new Color(1f, 1f, 1f, alpha);

            var fill = mine ? new Color(1f, 0.88f, 0.45f) : new Color(1f, 0.35f, 0.3f);
            var outline = mine ? new Color(0.1f, 0.2f, 0.45f) : new Color(0.3f, 0.02f, 0f);
            OutlinedLabel(new Rect(slide, band.y, width, band.height), mine ? "TU TURNO" : "TURNO DEL RIVAL", bannerStyle, fill, outline);
            GUI.color = Color.white;
        }

        // ------------------------------------------------------- Barras de LP

        /// <summary>El número de LP baja contando y un rastro rojo se queda detrás un momento antes de encogerse.</summary>
        private void UpdateLifeBars()
        {
            for (int owner = 0; owner < 2; owner++)
            {
                if (lifePoints[owner] != lastLife[owner])
                {
                    if (lifePoints[owner] < lastLife[owner]) lifeHitAt[owner] = Time.time;
                    else ghostLife[owner] = lifePoints[owner];
                    lastLife[owner] = lifePoints[owner];
                }

                shownLife[owner] = Mathf.MoveTowards(shownLife[owner], lifePoints[owner], Time.deltaTime * 4000f);
                if (Time.time - lifeHitAt[owner] > 0.45f)
                    ghostLife[owner] = Mathf.MoveTowards(ghostLife[owner], shownLife[owner], Time.deltaTime * 2500f);
            }
        }

        private void DrawLifeBar(Rect rect, int owner)
        {
            EnsureFeedbackStyles();
            float max = StartingLifePoints;
            float ratio = Mathf.Clamp01(shownLife[owner] / max);
            float ghost = Mathf.Clamp01(Mathf.Max(ghostLife[owner], shownLife[owner]) / max);

            // Tiembla y destella en rojo justo al recibir daño.
            float hit = Mathf.Clamp01(1f - (Time.time - lifeHitAt[owner]) / 0.35f);
            if (hit > 0f) rect.x += Mathf.Sin(Time.time * 80f) * 3f * hit;

            GUI.color = new Color(0f, 0f, 0f, 0.65f);
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = new Color(1f, 0.25f, 0.2f, 0.9f);
            GUI.DrawTexture(new Rect(rect.x + 2f, rect.y + 2f, (rect.width - 4f) * ghost, rect.height - 4f), Texture2D.whiteTexture);
            var full = owner == Player ? new Color(0.35f, 0.75f, 1f) : new Color(1f, 0.55f, 0.45f);
            GUI.color = Color.Lerp(full, new Color(1f, 0.85f, 0.3f), Mathf.Clamp01((0.5f - ratio) * 2f));
            if (ratio < 0.25f) GUI.color = Color.Lerp(GUI.color, new Color(1f, 0.3f, 0.25f), 0.7f);
            GUI.DrawTexture(new Rect(rect.x + 2f, rect.y + 2f, (rect.width - 4f) * ratio, rect.height - 4f), Texture2D.whiteTexture);
            if (hit > 0f)
            {
                GUI.color = new Color(1f, 1f, 1f, 0.6f * hit);
                GUI.DrawTexture(rect, Texture2D.whiteTexture);
            }
            GUI.color = Color.white;
            GUI.Label(rect, $"{Mathf.RoundToInt(shownLife[owner])} LP", barTextStyle);
        }

        private void EnsureFeedbackStyles()
        {
            if (bannerStyle != null) return;
            bannerStyle = new GUIStyle(GUI.skin.label) { fontSize = 84, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            bannerStyle.normal.textColor = Color.white;
            barTextStyle = new GUIStyle(GUI.skin.label) { fontSize = 15, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            barTextStyle.normal.textColor = Color.white;
        }
    }
}
