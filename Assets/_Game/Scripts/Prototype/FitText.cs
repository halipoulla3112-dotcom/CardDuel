using UnityEngine;

namespace CardDuel.Prototype
{
    /// <summary>Escala un TextMesh para que quepa en un rectángulo, en cuanto su malla está generada.</summary>
    [RequireComponent(typeof(TextMesh))]
    public class FitText : MonoBehaviour
    {
        public Vector2 maxSize = Vector2.one;

        private Renderer textRenderer;

        private void Awake()
        {
            textRenderer = GetComponent<Renderer>();
            transform.localScale = Vector3.one * 0.0001f;
        }

        private void LateUpdate()
        {
            var size = textRenderer.localBounds.size;
            if (size.x < 0.0001f || size.y < 0.0001f) return;

            float scale = Mathf.Min(maxSize.x / size.x, maxSize.y / size.y);
            transform.localScale = new Vector3(scale, scale, 1f);
            enabled = false;
        }
    }
}
