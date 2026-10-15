using CardDuel.Cards;
using DG.Tweening;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace CardDuel.Prototype
{
    /// <summary>
    /// Modelo 3D de un monstruo invocado, de pie sobre su carta. Reproduce sus
    /// animaciones (reposo en bucle; ataque, golpe y muerte una vez) sin
    /// necesitar un Animator Controller.
    /// </summary>
    public class MonsterFigure : MonoBehaviour
    {
        private const float TargetHeight = 0.95f;
        private const float AppearDuration = 0.45f;

        private MonsterModelLibrary.Entry entry;
        private Animator animator;
        private PlayableGraph graph;
        private AnimationPlayableOutput output;
        private AnimationClipPlayable current;
        private AnimationClip currentClip;
        private bool looping;
        private float baseScale;

        /// <summary>Crea la figura del monstruo encima de la carta, o null si no tiene modelo.</summary>
        public static MonsterFigure Spawn(CardView card)
        {
            var library = MonsterModelLibrary.Instance;
            var entry = library != null ? library.Find(card.Data.displayName) : null;
            if (entry == null) return null;

            var root = new GameObject($"{card.Data.displayName} (figura)");
            root.transform.SetParent(card.transform, false);
            root.transform.localPosition = new Vector3(0f, CardFactory.Thickness, 0f);

            var model = Instantiate(entry.model, root.transform, false);
            model.transform.localPosition = Vector3.zero;
            model.transform.localRotation = Quaternion.identity;
            foreach (var collider in model.GetComponentsInChildren<Collider>()) Destroy(collider);

            var figure = root.AddComponent<MonsterFigure>();
            figure.entry = entry;
            figure.animator = model.GetComponentInChildren<Animator>();
            if (figure.animator == null) figure.animator = model.AddComponent<Animator>();
            figure.animator.applyRootMotion = false;
            figure.FitHeight(model);

            // Tocar la figura cuenta como tocar su carta (el raycast busca la CardView en los padres).
            var hitBox = root.AddComponent<BoxCollider>();
            hitBox.center = new Vector3(0f, TargetHeight * 0.5f, 0f);
            hitBox.size = new Vector3(CardFactory.Width, TargetHeight, CardFactory.Width);

            figure.Appear();
            return figure;
        }

        /// <summary>Escala el modelo para que mida lo mismo sea cual sea el FBX, y lo apoya sobre la carta.</summary>
        private void FitHeight(GameObject model)
        {
            var bounds = WorldBounds(model);
            if (bounds.size.y < 0.0001f) return;

            model.transform.localScale *= TargetHeight * transform.lossyScale.y / bounds.size.y;

            // Apoya los pies sobre la carta.
            bounds = WorldBounds(model);
            model.transform.position += Vector3.up * (transform.position.y - bounds.min.y);
        }

        private static Bounds WorldBounds(GameObject model)
        {
            var renderers = model.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) return new Bounds(model.transform.position, Vector3.zero);
            var bounds = renderers[0].bounds;
            foreach (var r in renderers) bounds.Encapsulate(r.bounds);
            return bounds;
        }

        private void Appear()
        {
            baseScale = transform.localScale.x;
            transform.localScale = Vector3.zero;
            transform.DOScale(baseScale, AppearDuration).SetEase(Ease.OutBack);
            Play(entry.idle, true);
        }

        public void PlayAttack() => PlayOnce(entry.attack);

        /// <summary>Lanza la animación de ataque y devuelve cuándo llega el golpe y cuánto dura.</summary>
        public (float impact, float length) PlayAttackTimed()
        {
            if (entry.attack == null) return (0.2f, 0.4f);
            Play(entry.attack, false);
            float length = entry.attack.length;
            return (Mathf.Clamp(length * 0.45f, 0.15f, 0.6f), length);
        }

        /// <summary>Se separa de su carta (que se queda en la zona) para moverse por el tablero.</summary>
        public void Detach(Transform board) => transform.SetParent(board, true);

        /// <summary>Corre (o vuela) hasta un punto del tablero mirando hacia donde va.</summary>
        public Tween MoveTo(Vector3 worldPosition, float duration)
        {
            var direction = worldPosition - transform.position;
            direction.y = 0f;
            if (direction.sqrMagnitude > 0.0001f)
                transform.DORotateQuaternion(Quaternion.LookRotation(direction, Vector3.up), 0.2f);
            Play(entry.move != null ? entry.move : entry.idle, true);
            return transform.DOMove(worldPosition, duration).SetEase(Ease.InOutSine)
                .OnComplete(() => Play(entry.idle, true));
        }

        /// <summary>Gira para mirar a un punto.</summary>
        public Tween Face(Vector3 worldPosition, float duration)
        {
            var direction = worldPosition - transform.position;
            direction.y = 0f;
            return transform.DORotateQuaternion(Quaternion.LookRotation(direction, Vector3.up), duration);
        }

        /// <summary>Vuelve a su sitio sobre la carta, mirando al frente.</summary>
        public Tween ReturnTo(CardView card, float duration)
        {
            var home = card.transform.TransformPoint(new Vector3(0f, CardFactory.Thickness, 0f));
            return DOTween.Sequence()
                .Append(MoveTo(home, duration))
                .Append(transform.DORotateQuaternion(card.transform.rotation, 0.25f))
                .OnComplete(() =>
                {
                    transform.SetParent(card.transform, true);
                    transform.localPosition = new Vector3(0f, CardFactory.Thickness, 0f);
                    transform.localRotation = Quaternion.identity;
                    Play(entry.idle, true);
                });
        }

        public void PlayHit() => PlayOnce(entry.hit);

        /// <summary>Animación de muerte y luego se encoge y desaparece. Devuelve cuánto tarda.</summary>
        public float Die()
        {
            float duration = entry.death != null ? Mathf.Min(entry.death.length, 2.5f) : 0f;
            Play(entry.death, false);
            transform.DOKill();
            transform.DOScale(0f, 0.3f).SetDelay(duration).SetEase(Ease.InBack)
                .OnComplete(() => Destroy(gameObject));
            return duration + 0.3f;
        }

        /// <summary>Desaparece sin animación de muerte (p. ej. sacrificado como tributo).</summary>
        public void Vanish()
        {
            transform.DOKill();
            transform.DOScale(0f, 0.25f).SetEase(Ease.InBack).OnComplete(() => Destroy(gameObject));
        }

        private void PlayOnce(AnimationClip clip)
        {
            if (clip != null) Play(clip, false);
        }

        private void Play(AnimationClip clip, bool loop)
        {
            if (clip == null || animator == null) return;

            if (!graph.IsValid())
            {
                graph = PlayableGraph.Create($"{name} animaciones");
                graph.SetTimeUpdateMode(DirectorUpdateMode.GameTime);
                output = AnimationPlayableOutput.Create(graph, "Animation", animator);
            }
            else if (current.IsValid())
            {
                current.Destroy();
            }

            current = AnimationClipPlayable.Create(graph, clip);
            current.SetApplyFootIK(false);
            output.SetSourcePlayable(current);
            currentClip = clip;
            looping = loop;
            graph.Play();
        }

        private void Update()
        {
            if (!current.IsValid() || currentClip == null) return;

            double time = current.GetTime();
            if (time < currentClip.length) return;

            if (looping)
                current.SetTime(time % currentClip.length);
            else if (currentClip != entry.death)
                Play(entry.idle, true);
        }

        private void OnDestroy()
        {
            transform.DOKill();
            if (graph.IsValid()) graph.Destroy();
        }
    }
}
