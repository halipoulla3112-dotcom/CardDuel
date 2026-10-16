using System.Collections;
using CardDuel.Cards;
using UnityEngine;
using UnityEngine.InputSystem;

namespace CardDuel.Prototype
{
    /// <summary>
    /// Sensación de juego: vibración de cámara y pequeña congelación (hitstop) en los
    /// golpes, flecha curva al elegir objetivo y mano que se abre al pasar el ratón.
    /// </summary>
    public partial class DuelPrototype
    {
        private const float HandExpandSpacing = 1.35f;
        private const float HandExpandLift = 0.22f;
        private static readonly Color ArrowIdle = new Color(1.6f, 1.35f, 0.8f, 0.9f);
        private static readonly Color ArrowOnTarget = new Color(2.4f, 0.35f, 0.25f, 1f);

        private float shakeUntil;
        private float shakeDuration;
        private float shakeStrength;
        private Vector3 appliedShakeOffset;
        private Quaternion appliedShakeRotation = Quaternion.identity;

        private bool handExpanded;

        // Fracción del alto de pantalla en la que la flecha "se engancha" a un objetivo.
        private const float AttackAssistRadius = 0.12f;
        private CardView attackPressed;
        private bool attackDragging;

        private LineRenderer arrowLine;
        private Transform arrowHead;
        private Material arrowMaterial;

        // ------------------------------------------------- Vibración y hitstop

        /// <summary>Un golpe se nota más cuanto más daño hace; los fuertes congelan el juego un instante.</summary>
        private void Impact(int damage)
        {
            float k = Mathf.Clamp01(damage / 2600f);
            Shake(Mathf.Lerp(0.03f, 0.15f, k), Mathf.Lerp(0.2f, 0.45f, k));
            if (damage >= 1500) StartCoroutine(Hitstop(0.07f));
        }

        private void Shake(float strength, float duration)
        {
            shakeStrength = Mathf.Max(strength, Time.unscaledTime < shakeUntil ? shakeStrength : 0f);
            shakeDuration = duration;
            shakeUntil = Time.unscaledTime + duration;
        }

        private IEnumerator Hitstop(float seconds)
        {
            if (paused) yield break;
            Time.timeScale = 0.05f;
            yield return new WaitForSecondsRealtime(seconds);
            if (!paused) Time.timeScale = 1f;
        }

        /// <summary>
        /// Quita la vibración del fotograma anterior antes de que nadie mueva la cámara
        /// (se llama al principio de Update) para que no se acumule.
        /// </summary>
        private void RemoveShake()
        {
            if (cam == null) return;
            cam.transform.position -= appliedShakeOffset;
            cam.transform.rotation *= Quaternion.Inverse(appliedShakeRotation);
            appliedShakeOffset = Vector3.zero;
            appliedShakeRotation = Quaternion.identity;
        }

        private void LateUpdate()
        {
            UpdateAttackArrow();

            float remaining = shakeUntil - Time.unscaledTime;
            if (remaining <= 0f || cam == null || paused) return;
            float k = remaining / shakeDuration * shakeStrength;
            float t = Time.unscaledTime * 35f;
            appliedShakeOffset = new Vector3(Mathf.PerlinNoise(t, 0f) - 0.5f, Mathf.PerlinNoise(0f, t) - 0.5f, 0f) * 2f * k;
            appliedShakeOffset = cam.transform.rotation * appliedShakeOffset;
            appliedShakeRotation = Quaternion.Euler(0f, 0f, (Mathf.PerlinNoise(t, t) - 0.5f) * 30f * k);
            cam.transform.position += appliedShakeOffset;
            cam.transform.rotation *= appliedShakeRotation;
        }

        // ----------------------------------------------------- Flecha de ataque

        /// <summary>Al elegir objetivo, una flecha curva va de tu monstruo al ratón; roja sobre un objetivo válido.</summary>
        private void UpdateAttackArrow()
        {
            bool show = state == State.ChoosingTarget && selected != null && Pointer.current != null && !paused;
            if (!show)
            {
                if (arrowLine != null) arrowLine.gameObject.SetActive(false);
                return;
            }
            EnsureArrow();
            arrowLine.gameObject.SetActive(true);

            var start = (selected.Figure != null ? selected.Figure.transform.position : selected.transform.position) + Vector3.up * 0.35f;
            Vector2 pointer = Pointer.current.position.ReadValue();
            var ray = cam.ScreenPointToRay(pointer);
            var plane = new Plane(Vector3.up, Vector3.up * 0.05f);
            var end = plane.Raycast(ray, out float distance) ? ray.GetPoint(distance) : start + Vector3.forward;

            // Si apunta (o se acerca) a un objetivo válido, la flecha se engancha a él y se pone roja.
            var target = ResolveAttackTarget(pointer, out bool direct);
            if (target != null) end = board.transform.TransformPoint(board.CardPosition(target)) + Vector3.up * 0.1f;
            else if (direct) end = board.transform.TransformPoint(ImpactPoint(null, Opponent));
            arrowMaterial.SetColor("_BaseColor", target != null || direct ? ArrowOnTarget : ArrowIdle);

            float lift = Mathf.Clamp(Vector3.Distance(start, end) * 0.45f, 0.5f, 2.5f);
            var control = Vector3.Lerp(start, end, 0.5f) + Vector3.up * lift;
            int count = arrowLine.positionCount;
            for (int i = 0; i < count; i++)
            {
                float t = i / (count - 1f);
                arrowLine.SetPosition(i, Bezier(start, control, end, t * 0.94f));
            }

            var tip = Bezier(start, control, end, 0.94f);
            var direction = end - tip;
            arrowHead.position = tip;
            if (direction.sqrMagnitude > 0.0001f) arrowHead.rotation = Quaternion.LookRotation(direction, Vector3.up);
        }

        /// <summary>
        /// Ayuda para apuntar: el objetivo resaltado cuyo centro en pantalla está más cerca del
        /// puntero (dentro de un radio generoso). Si el rival no tiene monstruos, apuntar a su
        /// mitad del tablero cuenta como ataque directo.
        /// </summary>
        private BoardZone ResolveAttackTarget(Vector2 pointer, out bool direct)
        {
            direct = false;
            BoardZone best = null;
            float bestDistance = UnityEngine.Screen.height * AttackAssistRadius;
            foreach (var zone in highlighted)
            {
                var world = board.transform.TransformPoint(board.CardPosition(zone)) + Vector3.up * 0.3f;
                var screen = cam.WorldToScreenPoint(world);
                if (screen.z <= 0f) continue;
                float distance = Vector2.Distance(pointer, screen);
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    best = zone;
                }
            }
            if (best != null || OwnMonsters(Opponent).Count > 0) return best;

            var ray = cam.ScreenPointToRay(pointer);
            var plane = new Plane(board.transform.up, board.transform.position);
            if (plane.Raycast(ray, out float hit))
                direct = board.transform.InverseTransformPoint(ray.GetPoint(hit)).z > 0.3f;
            return null;
        }

        // ------------------------------------------------- Arrastrar para atacar

        /// <summary>Al arrastrar un monstruo listo, el monstruo se queda y la flecha sigue al puntero.</summary>
        private void BeginAttackDrag(CardView attacker)
        {
            attackPressed = null;
            attackDragging = true;
            SelectCard(attacker);
            ClearHighlights();
            foreach (var target in OwnMonsters(Opponent)) Highlight(target.Zone, AttackTarget);
            state = State.ChoosingTarget;
            ShowMessage(OwnMonsters(Opponent).Count > 0
                ? "Suelta sobre el monstruo rival al que quieres atacar."
                : "Suelta sobre el campo rival para atacar directamente.");
        }

        /// <summary>Al soltar: ataca al objetivo enganchado (o directo) o cancela si no hay ninguno.</summary>
        private void EndAttackDrag(Vector2 screenPosition)
        {
            attackDragging = false;
            var zone = ResolveAttackTarget(screenPosition, out bool direct);
            if (zone != null && zone.Card != null) StartCoroutine(PlayerAttack(selected, zone.Card));
            else if (direct) StartCoroutine(PlayerAttack(selected, null));
            else
            {
                Deselect();
                ShowMessage("Ataque cancelado.");
            }
        }

        private bool CanStartAttackDrag(CardView card) =>
            card != null && card.Owner == Player && card.Location == CardLocation.Field &&
            IsPlayerTurn && (state == State.Idle || state == State.CardSelected) && CanAttack(card);

        private static Vector3 Bezier(Vector3 a, Vector3 b, Vector3 c, float t) =>
            (1f - t) * (1f - t) * a + 2f * (1f - t) * t * b + t * t * c;

        private void EnsureArrow()
        {
            if (arrowLine != null) return;
            arrowMaterial = Primitives.UnlitTransparent(Texture2D.whiteTexture, ArrowIdle);

            var go = new GameObject("AttackArrow");
            go.transform.SetParent(transform, false);
            arrowLine = go.AddComponent<LineRenderer>();
            arrowLine.positionCount = 24;
            arrowLine.useWorldSpace = true;
            arrowLine.sharedMaterial = arrowMaterial;
            arrowLine.widthCurve = new AnimationCurve(new Keyframe(0f, 0.05f), new Keyframe(1f, 0.16f));
            arrowLine.numCapVertices = 4;
            arrowLine.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            // Punta: triángulo plano que apunta en la dirección de la flecha.
            arrowHead = new GameObject("ArrowHead").transform;
            arrowHead.SetParent(go.transform, false);
            var mesh = new Mesh { name = "ArrowHead" };
            mesh.vertices = new[] { new Vector3(-0.22f, 0f, -0.05f), new Vector3(0.22f, 0f, -0.05f), new Vector3(0f, 0f, 0.38f) };
            mesh.triangles = new[] { 0, 2, 1, 0, 1, 2 };
            mesh.RecalculateNormals();
            arrowHead.gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
            arrowHead.gameObject.AddComponent<MeshRenderer>().sharedMaterial = arrowMaterial;
        }

        // ----------------------------------------------------- Mano que se abre

        /// <summary>Con el ratón sobre la mano, las cartas se separan y suben un poco para leerlas mejor.</summary>
        private void UpdateHandExpansion(Pointer pointer, Vector2 screenPosition)
        {
            bool expand = pointer is Mouse && dragged == null && state != State.Inspecting &&
                          (hovered != null || screenPosition.y < UnityEngine.Screen.height * 0.2f);
            if (expand == handExpanded) return;
            handExpanded = expand;
            LayoutHand();
        }
    }
}
