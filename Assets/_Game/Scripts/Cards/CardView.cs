using CardDuel.Prototype;
using DG.Tweening;
using UnityEngine;

namespace CardDuel.Cards
{
    public enum CardLocation { Deck, Hand, Field, Graveyard }

    /// <summary>
    /// Representación 3D de una carta. Guarda su estado en el duelo y anima
    /// sus movimientos con DOTween. Las posiciones son locales a su padre
    /// (la mano bajo la cámara, o el tablero).
    /// </summary>
    public class CardView : MonoBehaviour
    {
        private static readonly Vector3 LiftOffset = new Vector3(0f, 0.4f, -0.3f);

        [SerializeField] private CardData data;
        [SerializeField] private float liftDuration = 0.15f;

        public CardData Data => data;
        public CardLocation Location { get; set; }
        public BoardZone Zone { get; set; }
        public bool FaceUp { get; set; }
        public bool DefensePosition { get; set; }
        public int TurnPlaced { get; set; }
        public int PositionChangedTurn { get; set; } = -1;
        public int Owner { get; set; }
        public int AttackedTurn { get; set; } = -1;

        /// <summary>Vida actual en el campo. Empieza igual a la DEF y baja con cada ataque recibido.</summary>
        public int Life { get; private set; }

        private static readonly Color DamagedColor = new Color(0.8f, 0.1f, 0.1f);
        private TextMesh statsText;
        private Color statsColor;

        private Vector3 homePosition;
        private Quaternion homeRotation = Quaternion.identity;
        private float lift;

        public void Init(CardData cardData) => data = cardData;

        /// <summary>Fija la vida y actualiza la línea ATK/DEF de la cara (en rojo si está dañada).</summary>
        public void SetLife(int life)
        {
            Life = Mathf.Max(life, 0);
            if (data.cardType != CardType.Monster) return;

            if (statsText == null)
            {
                var stats = transform.Find("Face/Stats");
                if (stats == null) return;
                statsText = stats.GetComponent<TextMesh>();
                statsColor = statsText.color;
            }

            statsText.text = $"ATK {data.attack}   DEF {Life}";
            statsText.color = Life < data.defense ? DamagedColor : statsColor;
            var fit = statsText.GetComponent<FitText>();
            if (fit != null) fit.enabled = true;
        }

        /// <summary>Fija la posición de reposo (p. ej. su hueco en la mano) y anima hasta ella.</summary>
        public void SetHome(Vector3 localPosition, Quaternion localRotation, float duration)
        {
            homePosition = localPosition;
            homeRotation = localRotation;
            AnimateTo(homePosition + LiftOffset * lift, homeRotation, duration);
        }

        /// <summary>Eleva la carta respecto a su posición de reposo (0 = en reposo, 1 = seleccionada).</summary>
        public void Lift(float amount)
        {
            if (Mathf.Approximately(amount, lift)) return;
            lift = amount;
            AnimateTo(homePosition + LiftOffset * lift, homeRotation, liftDuration);
        }

        public Tween MoveTo(Vector3 localPosition, Quaternion localRotation, float duration)
        {
            lift = 0f;
            homePosition = localPosition;
            homeRotation = localRotation;
            return AnimateTo(localPosition, localRotation, duration);
        }

        private Tween AnimateTo(Vector3 localPosition, Quaternion localRotation, float duration)
        {
            transform.DOKill();
            return DOTween.Sequence()
                .SetTarget(transform)
                .Join(transform.DOLocalMove(localPosition, duration).SetEase(Ease.OutCubic))
                .Join(transform.DOLocalRotateQuaternion(localRotation, duration).SetEase(Ease.OutCubic));
        }
    }
}
