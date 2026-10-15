using CardDuel.Cards;
using DG.Tweening;
using UnityEngine;

namespace CardDuel.Prototype
{
    public enum ZoneType { Monster, SpellTrap, Deck, Graveyard, Field, Extra }

    /// <summary>Una casilla del tablero. Puede contener una carta y resaltarse.</summary>
    public class BoardZone : MonoBehaviour
    {
        public ZoneType Type { get; private set; }
        public int Owner { get; private set; }
        public int Index { get; private set; }
        public CardView Card { get; set; }
        public bool IsFree => Card == null;

        private Material material;
        private Color baseColor;
        private Tween pulse;

        public void Init(ZoneType type, int owner, int index, Material zoneMaterial, Color color)
        {
            Type = type;
            Owner = owner;
            Index = index;
            material = zoneMaterial;
            baseColor = color;
        }

        /// <summary>Resalta la zona con un pulso de color, o lo quita si <paramref name="color"/> es null.</summary>
        public void SetHighlight(Color? color)
        {
            pulse?.Kill();
            pulse = null;
            material.SetColor("_BaseColor", baseColor);
            if (color == null) return;

            pulse = material.DOColor(color.Value, "_BaseColor", 0.45f)
                .SetLoops(-1, LoopType.Yoyo)
                .SetEase(Ease.InOutSine);
        }

        private void OnDestroy()
        {
            pulse?.Kill();
        }
    }
}
