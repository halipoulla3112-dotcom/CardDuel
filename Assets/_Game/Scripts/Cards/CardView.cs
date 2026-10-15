using DG.Tweening;
using UnityEngine;

namespace CardDuel.Cards
{
    /// <summary>
    /// Representación 3D de una carta en la mesa. Las animaciones usan DOTween.
    /// </summary>
    public class CardView : MonoBehaviour
    {
        [SerializeField] private CardData data;
        [SerializeField] private float hoverHeight = 0.3f;
        [SerializeField] private float animDuration = 0.2f;

        public CardData Data => data;

        private Vector3 restPosition;

        private void Awake()
        {
            restPosition = transform.localPosition;
        }

        public void Hover(bool on)
        {
            transform.DOKill();
            transform.DOLocalMove(restPosition + (on ? Vector3.up * hoverHeight : Vector3.zero), animDuration)
                .SetEase(Ease.OutQuad);
        }

        public Tween MoveTo(Vector3 worldPosition, Quaternion rotation)
        {
            transform.DOKill();
            restPosition = transform.parent ? transform.parent.InverseTransformPoint(worldPosition) : worldPosition;
            return DOTween.Sequence()
                .Join(transform.DOMove(worldPosition, animDuration * 2f).SetEase(Ease.OutCubic))
                .Join(transform.DORotateQuaternion(rotation, animDuration * 2f));
        }

        /// <summary>Gira la carta 180º sobre su eje para mostrarla boca arriba / abajo.</summary>
        public Tween Flip()
        {
            return transform.DOLocalRotate(new Vector3(0f, 0f, 180f), animDuration * 2f, RotateMode.LocalAxisAdd);
        }
    }
}
