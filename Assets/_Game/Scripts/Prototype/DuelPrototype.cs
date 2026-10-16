using System;
using System.Collections;
using System.Collections.Generic;
using CardDuel.Cards;
using DG.Tweening;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace CardDuel.Prototype
{
    /// <summary>
    /// Prototipo jugable contra la máquina: una moneda decide quién empieza,
    /// los turnos se alternan (robar, invocar, atacar) y los monstruos tienen
    /// vida igual a su DEF, que baja con cada ataque recibido. Si llega a 0 el
    /// monstruo va al cementerio; sin monstruos, el ataque va directo a los LP.
    /// Funciona con ratón y con pantalla táctil.
    /// </summary>
    public partial class DuelPrototype : MonoBehaviour
    {
        private enum State { Busy, Idle, CardSelected, ChoosingTributes, ChoosingTarget, Inspecting }
        private enum PlayMode { None, SummonAttack, SetMonster, ActivateSpell, SetSpellTrap }

        private const int Player = 0;
        private const int Opponent = 1;
        private const int StartingLifePoints = 8000;
        private const float DragThreshold = 12f;
        private const float DragHeight = 0.6f;
        private const float DragFollowSpeed = 25f;
        private const float LongPressTime = 0.45f;
        private const float SummonFocusHeight = 0.45f;
        private const float SummonCameraDistance = 1.35f;
        private const float SummonCameraLift = 0.3f;
        private const float SummonCameraTravel = 0.7f;
        private const float SummonHoldTime = 1.8f;
        private const float AttackStandOff = 0.7f;
        private const float AttackRunTime = 0.8f;
        private const float AttackCameraBack = 1.6f;
        private const float AttackCameraSide = 1.05f;
        private const float AttackCameraHeight = 1.15f;
        private const float AttackCameraFollow = 6f;
        private const float DirectAttackDepth = 2.25f;
        private const float InspectDistance = 2.2f;
        private const float InspectScreenFill = 0.85f;

        private static readonly Quaternion HandFacing = Quaternion.Euler(-90f, 0f, 0f);
        private static readonly Quaternion EnemyHandFacing = Quaternion.Euler(127f, 0f, 0f);
        private static readonly Color ZoneHighlight = new Color(0.2f, 0.9f, 1f);
        private static readonly Color TributeCandidate = new Color(1f, 0.35f, 0.25f);
        private static readonly Color TributeChosen = new Color(1f, 0.9f, 0.3f);
        private static readonly Color SelectedFieldCard = new Color(1f, 0.85f, 0.3f);
        private static readonly Color DropTarget = new Color(0.4f, 1f, 0.4f);
        private static readonly Color AttackTarget = new Color(1f, 0.2f, 0.2f);
        private static readonly Color DamageColor = new Color(1f, 0.3f, 0.25f);
        private static readonly Color CoinColor = new Color(0.95f, 0.75f, 0.25f);

        [Header("Cámara")]
        [SerializeField] private Vector3 cameraPosition = new Vector3(0f, 9f, -7.2f);
        [SerializeField] private Vector3 cameraLookAt = new Vector3(0f, 0f, -0.4f);
        [SerializeField] private float cameraFieldOfView = 50f;

        [Header("Mano")]
        [SerializeField] private int startingHandSize = 4;
        [SerializeField] private Vector3 handOffset = new Vector3(0f, -1.55f, 4.2f);
        [SerializeField] private float handCardSpacing = 0.78f;
        [SerializeField] private float handMaxWidth = 4.8f;

        [Header("Mano del rival")]
        [SerializeField] private Vector3 enemyHandCenter = new Vector3(0f, 0.5f, 3.9f);
        [SerializeField] private float enemyHandSpacing = 0.55f;

        [Header("Rival")]
        [SerializeField] private float enemyActionDelay = 0.9f;

        private Camera cam;
        private AudioSource audioSource;
        private Board board;
        private Transform handRoot;

        private readonly List<CardData>[] decks = { new List<CardData>(), new List<CardData>() };
        private readonly List<CardView>[] hands = { new List<CardView>(), new List<CardView>() };
        private readonly List<CardView>[] graveyards = { new List<CardView>(), new List<CardView>() };
        private readonly int[] lifePoints = { StartingLifePoints, StartingLifePoints };
        private readonly List<BoardZone> highlighted = new List<BoardZone>();
        private readonly List<CardView> tributes = new List<CardView>();
        private readonly List<Rect> guiRects = new List<Rect>();

        private List<CardView> Hand => hands[Player];

        private State state = State.Busy;
        private PlayMode playMode;
        private CardView selected;
        private CardView hovered;
        private int tributesNeeded;
        private bool placeFaceDown;
        private BoardZone pendingZone;

        private CardView pressedCard;
        private Vector2 pressPosition;
        private float pressTime;
        private CardView dragged;
        private CardView inspected;
        private GameObject inspectBackdrop;
        private BoardZone dropTarget;

        private int turn;
        private int currentPlayer = -1;
        private bool normalSummonUsed;
        private bool gameOver;
        private string message = "";
        private float messageUntil;

        private bool inCinematic;
        private Texture2D portrait;
        private float portraitAlpha;
        private const float PortraitSize = 230f;
        private const float PortraitFadeSpeed = 4f;
        private Vector3 savedCameraPosition;
        private Quaternion savedCameraRotation;
        private string summonTitle = "";
        private float summonTitleAlpha;

        private GUIStyle labelStyle;
        private GUIStyle centeredStyle;
        private GUIStyle bigStyle;
        private GUIStyle buttonStyle;
        private GUIStyle closeStyle;
        private GUIStyle titleStyle;
        private GUIStyle panelStyle;

        private void Start()
        {
            SetupCamera();
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
            audioSource.spatialBlend = 0f;
            GameSettings.Apply();
            MusicPlayer.Attach(gameObject);
            board = Board.Build(transform);
            FarmEnvironment.Build(transform);
            BoardDecor.Build(transform);
            AestheticFx.Setup(cam, transform);

            handRoot = new GameObject("Hand").transform;
            handRoot.SetParent(cam.transform, false);
            handRoot.localPosition = handOffset;

            for (int owner = 0; owner < 2; owner++)
            {
                decks[owner].AddRange(SampleDeck.Build());
                Shuffle(decks[owner]);
                board.SetDeckCount(owner, decks[owner].Count);
            }

            if (Application.isMobilePlatform)
            {
                Screen.autorotateToPortrait = false;
                Screen.autorotateToPortraitUpsideDown = false;
                Screen.orientation = ScreenOrientation.AutoRotation;
            }

            StartCoroutine(BeginDuel());
        }

        private void SetupCamera()
        {
            cam = Camera.main;
            if (cam == null)
            {
                var go = new GameObject("Main Camera") { tag = "MainCamera" };
                cam = go.AddComponent<Camera>();
            }

            cam.transform.position = cameraPosition;
            cam.transform.LookAt(cameraLookAt);
            cam.fieldOfView = cameraFieldOfView;
            cam.nearClipPlane = 0.1f;
            if (FindAnyObjectByType<AudioListener>() == null) cam.gameObject.AddComponent<AudioListener>();
        }

        // --------------------------------------------------------------- Inicio

        private IEnumerator BeginDuel()
        {
            bool playerFirst = UnityEngine.Random.value < 0.5f;
            firstPlayer = playerFirst ? Player : Opponent;
            ShowMessage("Lanzando la moneda...");
            yield return CoinToss(playerFirst);
            ShowMessage(playerFirst ? "¡Sale TÚ! Empiezas tú." : "Sale RIVAL. Empieza el rival.");
            yield return new WaitForSeconds(1.2f);

            for (int i = 0; i < startingHandSize; i++)
            {
                DrawCard(Player);
                DrawCard(Opponent);
                yield return new WaitForSeconds(0.25f);
            }
            yield return new WaitForSeconds(0.3f);

            StartTurn(playerFirst ? Player : Opponent);
        }

        /// <summary>Lanza una moneda al aire: la cara "TÚ" o "RIVAL" que quede arriba decide quién empieza.</summary>
        private IEnumerator CoinToss(bool playerFirst)
        {
            var coin = new GameObject("Coin").transform;
            coin.SetParent(board.transform, false);
            coin.localPosition = new Vector3(0f, 0.1f, 0f);

            var body = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            Destroy(body.GetComponent<Collider>());
            body.transform.SetParent(coin, false);
            body.transform.localScale = new Vector3(1.2f, 0.04f, 1.2f);
            body.GetComponent<MeshRenderer>().sharedMaterial = Primitives.Lit(CoinColor);

            CoinFace(coin, "TÚ", 1f);
            CoinFace(coin, "RIVAL", -1f);

            float spins = 6f + (playerFirst ? 0f : 0.5f);
            audioSource.PlayOneShot(SoundFx.CoinFlip);
            yield return DOTween.Sequence()
                .Append(coin.DOLocalMoveY(3f, 0.7f).SetEase(Ease.OutQuad))
                .Append(coin.DOLocalMoveY(0.1f, 0.7f).SetEase(Ease.InQuad))
                .Insert(0f, coin.DOLocalRotate(new Vector3(360f * spins, 0f, 0f), 1.4f, RotateMode.FastBeyond360).SetEase(Ease.OutSine))
                .InsertCallback(1.4f, () => audioSource.PlayOneShot(SoundFx.CoinLand))
                .Append(coin.DOPunchScale(Vector3.one * 0.15f, 0.3f))
                .WaitForCompletion();

            yield return new WaitForSeconds(1.2f);
            yield return coin.DOScale(0f, 0.3f).SetEase(Ease.InBack).WaitForCompletion();
            Destroy(coin.gameObject);
        }

        private static void CoinFace(Transform coin, string label, float side)
        {
            var face = new GameObject(label).transform;
            face.SetParent(coin, false);
            face.localPosition = new Vector3(0f, side * 0.045f, 0f);
            face.localRotation = Quaternion.Euler(side * 90f, 0f, 0f);
            Primitives.Text("Text", face, label, Vector2.zero, new Vector2(0.9f, 0.4f), 0f, new Color(0.3f, 0.18f, 0.05f));
        }

        // ---------------------------------------------------------------- Turnos

        private void StartTurn(int owner)
        {
            if (gameOver) return;

            turn++;
            currentPlayer = owner;
            normalSummonUsed = false;
            RefillMana(owner);
            ShowTurnBanner(owner);

            // Quien empieza el duelo no roba en su primer turno.
            if (turn > 1 && DrawCard(owner) == null) return;

            if (owner == Player)
            {
                state = State.Idle;
                ShowMessage(turn == 1
                    ? "Tu turno. En el primer turno del duelo no se puede atacar."
                    : "Tu turno: robas una carta.");
            }
            else
            {
                state = State.Busy;
                StartCoroutine(EnemyTurn());
            }
        }

        private void EndPlayerTurn()
        {
            Deselect();
            audioSource.PlayOneShot(SoundFx.EndTurn, 0.8f);
            state = State.Busy;
            StartTurn(Opponent);
        }

        private void GameOver(int winner, string reason)
        {
            if (gameOver) return;
            gameOver = true;
            ClearSelection();
            state = State.Busy;
            ShowEndScreen(winner == Player, reason);
        }

        // ---------------------------------------------------------------- Input

        private void Update()
        {
            RemoveShake();
            // El retrato del jugador se desvanece en las escenas de cámara y al ver una carta en grande.
            bool showPortrait = !inCinematic && state != State.Inspecting && currentPlayer >= 0 && !gameOver;
            portraitAlpha = Mathf.MoveTowards(portraitAlpha, showPortrait ? 1f : 0f, Time.deltaTime * PortraitFadeSpeed);
            audioSource.volume = GameSettings.EffectsVolume;
            UpdateGlows();
            UpdateLifeBars();

            // Esc pausa (salvo al ver una carta en grande, que la cierra, o con la partida terminada).
            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame && state != State.Inspecting && !gameOver)
                TogglePause();
            if (paused) return;

#if UNITY_EDITOR
            // Atajos de prueba en el editor: F8 estallidos de invocación, F9 gana y F10 pierde.
            if (Keyboard.current != null && !gameOver)
            {
                // F8: los seis estallidos de invocación en fila, para ver cómo quedan.
                if (Keyboard.current.f8Key.wasPressedThisFrame)
                {
                    var attributes = new[] { CardAttribute.Fire, CardAttribute.Water, CardAttribute.Light, CardAttribute.Dark, CardAttribute.Earth, CardAttribute.Wind };
                    for (int i = 0; i < attributes.Length; i++)
                        AestheticFx.SummonBurst(board.transform.TransformPoint(new Vector3(-3.75f + i * 1.5f, 0f, 0f)), attributes[i]);
                }
                if (Keyboard.current.f9Key.wasPressedThisFrame) GameOver(Player, "(Prueba desde el editor)");
                else if (Keyboard.current.f10Key.wasPressedThisFrame) GameOver(Opponent, "(Prueba desde el editor)");
            }
#endif

            if (state == State.Busy) return;

            if (state == State.Inspecting)
            {
                if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame) CloseInspect();
                return;
            }

            var pointer = Pointer.current;
            if (pointer == null) return;

            Vector2 screenPosition = pointer.position.ReadValue();

            if (dragged != null)
            {
                UpdateDrag(screenPosition);
                if (!pointer.press.isPressed) EndDrag(screenPosition);
                return;
            }

            // Arrastrando un monstruo para atacar: la flecha sigue al puntero hasta soltar.
            if (attackDragging)
            {
                if (!pointer.press.isPressed) EndAttackDrag(screenPosition);
                return;
            }

            // Pulsado un monstruo listo: si se arrastra, empieza el ataque; si se suelta sin mover, es un toque.
            if (attackPressed != null)
            {
                if (!pointer.press.isPressed)
                {
                    attackPressed = null;
                    HandleTap(pressPosition);
                }
                else if ((screenPosition - pressPosition).sqrMagnitude > DragThreshold * DragThreshold)
                {
                    BeginAttackDrag(attackPressed);
                }
                return;
            }

            if (pressedCard != null)
            {
                if (!pointer.press.isPressed)
                {
                    pressedCard = null;
                    HandleTap(pressPosition);
                }
                else if ((screenPosition - pressPosition).sqrMagnitude > DragThreshold * DragThreshold)
                {
                    BeginDrag(pressedCard);
                }
                else if (Time.time - pressTime >= LongPressTime)
                {
                    OpenInspect(pressedCard);
                }
                return;
            }

            bool overGui = IsOverGui(screenPosition);
            UpdateHover(pointer, screenPosition, overGui);
            UpdateHandExpansion(pointer, screenPosition);

            if (!pointer.press.wasPressedThisFrame || overGui) return;

            // Una carta de la mano se puede arrastrar: esperamos a ver si el
            // jugador la mueve (arrastre), la mantiene quieta (ver en grande)
            // o la suelta sin moverla (toque).
            Raycast(screenPosition, out var card, out _);
            if (IsOwnHandCard(card) && state != State.ChoosingTributes)
            {
                pressedCard = card;
                pressPosition = screenPosition;
                pressTime = Time.time;
            }
            else if (CanStartAttackDrag(card))
            {
                attackPressed = card;
                pressPosition = screenPosition;
            }
            else
            {
                HandleTap(screenPosition);
            }
        }

        private static bool IsOwnHandCard(CardView card) =>
            card != null && card.Location == CardLocation.Hand && card.Owner == Player;

        private void UpdateHover(Pointer pointer, Vector2 screenPosition, bool overGui)
        {
            CardView target = null;
            if (!overGui && pointer is Mouse)
            {
                Raycast(screenPosition, out var card, out _);
                if (IsOwnHandCard(card)) target = card;
            }

            if (target == hovered) return;
            if (hovered != null && hovered != selected && hovered.Location == CardLocation.Hand) hovered.Lift(0f);
            hovered = target;
            if (hovered != null && hovered != selected) hovered.Lift(0.5f);
        }

        private void Raycast(Vector2 screenPosition, out CardView card, out BoardZone zone)
        {
            card = null;
            zone = null;
            var hits = Physics.RaycastAll(cam.ScreenPointToRay(screenPosition), 100f);
            Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
            foreach (var hit in hits)
            {
                if (card == null) card = hit.collider.GetComponentInParent<CardView>();
                if (zone == null) zone = hit.collider.GetComponent<BoardZone>();
            }
        }

        private void HandleTap(Vector2 screenPosition)
        {
            Raycast(screenPosition, out var card, out var zone);
            var fieldCard = card != null && card.Location == CardLocation.Field ? card : zone != null ? zone.Card : null;

            switch (state)
            {
                case State.ChoosingTributes:
                    if (fieldCard != null && fieldCard.Zone.Type == ZoneType.Monster && fieldCard.Owner == Player)
                        ToggleTribute(fieldCard);
                    else
                        ShowMessage($"Elige {tributesNeeded} de tus monstruos para sacrificar.");
                    return;

                case State.ChoosingTarget:
                    // Con ayuda para apuntar: vale tocar cerca del objetivo.
                    var targetZone = ResolveAttackTarget(screenPosition, out bool direct);
                    if (targetZone != null && targetZone.Card != null)
                        StartCoroutine(PlayerAttack(selected, targetZone.Card));
                    else if (direct)
                        StartCoroutine(PlayerAttack(selected, null));
                    else
                        ShowMessage("Toca el monstruo rival al que quieres atacar.");
                    return;
            }

            if (card != null)
            {
                switch (card.Location)
                {
                    case CardLocation.Hand:
                        if (card.Owner != Player) return;
                        if (card == selected) Deselect();
                        else SelectCard(card);
                        return;
                    case CardLocation.Field:
                        if (card == selected) Deselect();
                        else SelectCard(card);
                        return;
                    case CardLocation.Graveyard:
                        var pile = graveyards[card.Owner];
                        ShowMessage($"Cementerio {(card.Owner == Player ? "tuyo" : "del rival")}: {pile.Count} carta(s). Última: {pile[pile.Count - 1].Data.displayName}");
                        return;
                }
            }

            if (zone != null && zone.Type == ZoneType.Deck)
            {
                Deselect();
                ShowMessage(zone.Owner == Player
                    ? $"Te quedan {decks[Player].Count} cartas en el mazo. Robas al empezar tu turno."
                    : $"Al rival le quedan {decks[Opponent].Count} cartas en el mazo.");
                return;
            }

            Deselect();
        }

        // ------------------------------------------------------------ Arrastre

        private void BeginDrag(CardView card)
        {
            pressedCard = null;
            if (selected != card) SelectCard(card);
            dragged = card;
            hovered = null;
            card.transform.DOKill();

            foreach (var zone in ValidDropZones(card)) Highlight(zone, ZoneHighlight);
        }

        private void UpdateDrag(Vector2 screenPosition)
        {
            var up = board.transform.up;
            var plane = new Plane(up, board.transform.position + up * DragHeight);
            var ray = cam.ScreenPointToRay(screenPosition);
            float t = 1f - Mathf.Exp(-DragFollowSpeed * Time.deltaTime);

            var cardTransform = dragged.transform;
            if (plane.Raycast(ray, out float distance))
                cardTransform.position = Vector3.Lerp(cardTransform.position, ray.GetPoint(distance), t);
            cardTransform.rotation = Quaternion.Slerp(cardTransform.rotation, board.transform.rotation * Board.CardRotation(true, false), t);

            Raycast(screenPosition, out _, out var zone);
            var target = zone != null && highlighted.Contains(zone) ? zone : null;
            if (target == dropTarget) return;
            if (dropTarget != null) dropTarget.SetHighlight(ZoneHighlight);
            dropTarget = target;
            if (dropTarget != null) dropTarget.SetHighlight(DropTarget);
        }

        private void EndDrag(Vector2 screenPosition)
        {
            var card = dragged;
            dragged = null;
            dropTarget = null;

            Raycast(screenPosition, out _, out var zone);
            bool valid = zone != null && highlighted.Contains(zone);
            bool hadTargets = highlighted.Count > 0;
            ClearHighlights();

            if (valid)
            {
                PlayFromHand(card, zone);
                return;
            }

            // Sin zonas válidas ya se explicó el motivo al empezar el arrastre.
            if (zone != null && hadTargets) ShowMessage("Suelta la carta en una de las zonas iluminadas.");
            LayoutHand();
        }

        /// <summary>Zonas donde se puede soltar la carta. Avisa al jugador si no hay ninguna.</summary>
        private List<BoardZone> ValidDropZones(CardView card)
        {
            var zones = new List<BoardZone>();
            var data = card.Data;

            if (!CanPay(Player, data.cost))
            {
                ShowMessage($"Te falta maná: {data.displayName} cuesta {data.cost} y tienes {mana[Player]}.");
                return zones;
            }

            if (data.cardType == CardType.Monster)
            {
                if (normalSummonUsed)
                {
                    ShowMessage("Ya hiciste tu Invocación Normal este turno.");
                    return zones;
                }

                int needed = TributesFor(data.level);
                if (needed > 0 && OwnMonsters(Player).Count < needed)
                {
                    ShowMessage($"{data.displayName} necesita {needed} tributo(s) y no tienes suficientes monstruos.");
                    return zones;
                }

                foreach (var zone in board.MonsterZones[Player])
                    if (zone.IsFree) zones.Add(zone);

                // Con el campo lleno, un monstruo con tributos libera su propia zona.
                if (zones.Count == 0 && needed > 0) zones.AddRange(board.MonsterZones[Player]);
            }
            else
            {
                foreach (var zone in board.SpellTrapZones[Player])
                    if (zone.IsFree) zones.Add(zone);
            }

            if (zones.Count == 0) ShowMessage("No hay zonas libres.");
            return zones;
        }

        // ---------------------------------------------------- Ver carta en grande

        /// <summary>Muestra una carta de la mano en grande sobre un fondo negro que tapa el campo.</summary>
        private void OpenInspect(CardView card)
        {
            pressedCard = null;
            ClearSelection();
            hovered = null;
            inspected = card;
            state = State.Inspecting;

            if (inspectBackdrop == null) inspectBackdrop = CreateInspectBackdrop();
            inspectBackdrop.SetActive(true);

            // La carta ocupa como mucho el 85 % del alto o del ancho visible.
            float visibleHeight = 2f * InspectDistance * Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad);
            float visibleWidth = visibleHeight * cam.aspect;
            float scale = InspectScreenFill * Mathf.Min(visibleHeight / CardFactory.Height, visibleWidth / CardFactory.Width);

            card.transform.SetParent(cam.transform, true);
            card.MoveTo(new Vector3(0f, 0f, InspectDistance), HandFacing, 0.3f);
            card.transform.DOScale(scale, 0.3f).SetEase(Ease.OutCubic);
        }

        private void CloseInspect()
        {
            var card = inspected;
            inspected = null;
            inspectBackdrop.SetActive(false);
            state = State.Idle;

            card.transform.SetParent(handRoot, true);
            LayoutHand();
            card.transform.DOScale(1f, 0.3f).SetEase(Ease.OutCubic);
        }

        /// <summary>Plano negro delante de la cámara, por detrás de la carta inspeccionada.</summary>
        private GameObject CreateInspectBackdrop()
        {
            var shader = Shader.Find("Universal Render Pipeline/Unlit");
            var material = new Material(shader != null ? shader : Shader.Find("Unlit/Color"));
            material.SetColor("_BaseColor", Color.black);
            material.color = Color.black;

            var go = new GameObject("InspectBackdrop");
            go.transform.SetParent(cam.transform, false);
            go.transform.localPosition = new Vector3(0f, 0f, InspectDistance + 1f);
            go.transform.localScale = new Vector3(50f, 50f, 1f);
            go.AddComponent<MeshFilter>().sharedMesh = Primitives.QuadMesh;
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return go;
        }

        // ---------------------------------------------------------------- Mano

        /// <summary>Roba la carta de arriba del mazo. Si está vacío, ese jugador pierde.</summary>
        private CardView DrawCard(int owner)
        {
            var deck = decks[owner];
            if (deck.Count == 0)
            {
                GameOver(1 - owner, owner == Player ? "No te quedan cartas para robar." : "Al rival no le quedan cartas para robar.");
                return null;
            }

            var data = deck[deck.Count - 1];
            deck.RemoveAt(deck.Count - 1);

            var card = CardFactory.Create(data, board.transform);
            audioSource.PlayOneShot(SoundFx.Draw, owner == Player ? 0.9f : 0.4f);
            card.Owner = owner;
            card.transform.localPosition = board.DeckTopPosition(owner);
            card.transform.localRotation = Board.CardRotation(owner, false, false);
            board.SetDeckCount(owner, deck.Count);

            card.Location = CardLocation.Hand;
            hands[owner].Add(card);
            if (owner == Player)
            {
                card.transform.SetParent(handRoot, true);
                LayoutHand();
                card.transform.DOPunchScale(Vector3.one * 0.15f, 0.45f, 6);
            }
            else
            {
                LayoutEnemyHand();
            }
            return card;
        }

        /// <summary>Coloca las cartas de la mano en abanico delante de la cámara.</summary>
        private void LayoutHand()
        {
            int count = Hand.Count;
            float expand = handExpanded ? HandExpandSpacing : 1f;
            float spacing = Mathf.Min(handCardSpacing * expand, handMaxWidth * expand / Mathf.Max(count, 1));
            for (int i = 0; i < count; i++)
            {
                float offset = i - (count - 1) * 0.5f;
                var position = new Vector3(offset * spacing, -offset * offset * 0.02f + (handExpanded ? HandExpandLift : 0f), -i * 0.01f);
                var rotation = Quaternion.Euler(0f, 0f, -offset * 3f) * HandFacing;
                Hand[i].SetHome(position, rotation, 0.4f);
            }
        }

        /// <summary>La mano del rival, boca abajo detrás de su campo.</summary>
        private void LayoutEnemyHand()
        {
            var enemyHand = hands[Opponent];
            int count = enemyHand.Count;
            float spacing = Mathf.Min(enemyHandSpacing, handMaxWidth / Mathf.Max(count, 1));
            for (int i = 0; i < count; i++)
            {
                float offset = i - (count - 1) * 0.5f;
                var position = enemyHandCenter + new Vector3(-offset * spacing, 0f, i * 0.01f);
                enemyHand[i].SetHome(position, EnemyHandFacing, 0.4f);
            }
        }

        // ---------------------------------------------------------- Selección

        private void SelectCard(CardView card)
        {
            ClearSelection();
            selected = card;
            state = State.CardSelected;

            if (card.Location == CardLocation.Hand) card.Lift(1f);
            else if (card.Zone != null) Highlight(card.Zone, SelectedFieldCard);
        }

        private void Deselect()
        {
            ClearSelection();
            if (!gameOver) state = State.Idle;
        }

        private void ClearSelection()
        {
            if (selected != null && selected.Location == CardLocation.Hand) selected.Lift(selected == hovered ? 0.5f : 0f);
            selected = null;
            playMode = PlayMode.None;
            placeFaceDown = false;
            pendingZone = null;
            tributes.Clear();
            ClearHighlights();
        }

        private void CancelPlay()
        {
            playMode = PlayMode.None;
            pendingZone = null;
            tributes.Clear();
            ClearHighlights();
            if (selected != null && selected.Zone != null) Highlight(selected.Zone, SelectedFieldCard);
            state = State.CardSelected;
            ShowMessage("Cancelado.");
        }

        private void Highlight(BoardZone zone, Color color)
        {
            zone.SetHighlight(color);
            highlighted.Add(zone);
        }

        private void ClearHighlights()
        {
            foreach (var zone in highlighted) zone.SetHighlight(null);
            highlighted.Clear();
        }

        // ------------------------------------------------------------- Jugadas

        private static int TributesFor(int level) => level >= 7 ? 2 : level >= 5 ? 1 : 0;

        private List<CardView> OwnMonsters(int owner)
        {
            var monsters = new List<CardView>();
            foreach (var zone in board.MonsterZones[owner])
                if (!zone.IsFree) monsters.Add(zone.Card);
            return monsters;
        }

        /// <summary>Juega una carta soltada en una zona válida. Los monstruos con tributos esperan a que se elijan.</summary>
        private void PlayFromHand(CardView card, BoardZone zone)
        {
            var data = card.Data;
            selected = card;
            playMode = data.cardType == CardType.Monster ? (placeFaceDown ? PlayMode.SetMonster : PlayMode.SummonAttack)
                : data.cardType == CardType.Spell && !placeFaceDown ? PlayMode.ActivateSpell
                : PlayMode.SetSpellTrap;

            tributesNeeded = data.cardType == CardType.Monster ? TributesFor(data.level) : 0;
            if (tributesNeeded > 0)
            {
                pendingZone = zone;
                LayoutHand();
                tributes.Clear();
                foreach (var monster in OwnMonsters(Player)) Highlight(monster.Zone, TributeCandidate);
                state = State.ChoosingTributes;
                ShowMessage($"Elige {tributesNeeded} monstruo(s) para sacrificar.");
                return;
            }

            PlaySelectedTo(zone);
        }

        private void ToggleTribute(CardView monster)
        {
            if (tributes.Remove(monster))
            {
                monster.Zone.SetHighlight(TributeCandidate);
            }
            else
            {
                tributes.Add(monster);
                monster.Zone.SetHighlight(TributeChosen);
            }

            if (tributes.Count < tributesNeeded) return;

            var sacrificed = new List<CardView>(tributes);
            tributes.Clear();
            ClearHighlights();
            foreach (var tribute in sacrificed) SendToGraveyard(tribute);

            var zone = pendingZone != null && pendingZone.IsFree
                ? pendingZone
                : Array.Find(board.MonsterZones[Player], z => z.IsFree);
            pendingZone = null;
            PlaySelectedTo(zone);
        }

        private void PlaySelectedTo(BoardZone zone)
        {
            var card = selected;
            var mode = playMode;
            ClearHighlights();
            selected = null;
            playMode = PlayMode.None;
            placeFaceDown = false;
            state = State.Idle;
            if (hovered == card) hovered = null;
            Pay(Player, card.Data.cost);

            bool faceUp = mode == PlayMode.SummonAttack || mode == PlayMode.ActivateSpell;
            bool cinematic = mode == PlayMode.SummonAttack;
            var move = PlaceOnField(card, zone, faceUp, mode == PlayMode.SetMonster, spawnFigure: !cinematic);

            string cardName = card.Data.displayName;
            switch (mode)
            {
                case PlayMode.SummonAttack:
                    normalSummonUsed = true;
                    ShowMessage($"¡Invocas a {cardName} en posición de ataque!");
                    StartCoroutine(SummonCinematic(card, move));
                    break;
                case PlayMode.SetMonster:
                    normalSummonUsed = true;
                    ShowMessage("Colocas un monstruo boca abajo en defensa.");
                    break;
                case PlayMode.ActivateSpell:
                    ShowMessage($"Activas {cardName}.");
                    move.OnComplete(() => ResolveAndDiscard(card));
                    break;
                case PlayMode.SetSpellTrap:
                    ShowMessage("Colocas una carta boca abajo.");
                    break;
            }
        }

        /// <summary>Mueve una carta de la mano de su dueño a una zona del campo.</summary>
        private Tween PlaceOnField(CardView card, BoardZone zone, bool faceUp, bool defense, bool spawnFigure = true)
        {
            int owner = card.Owner;
            hands[owner].Remove(card);

            card.transform.SetParent(board.transform, true);
            card.Location = CardLocation.Field;
            card.SetGlow(null);
            card.Zone = zone;
            zone.Card = card;
            card.FaceUp = faceUp;
            card.DefensePosition = defense;
            card.TurnPlaced = turn;
            card.PositionChangedTurn = -1;
            card.AttackedTurn = -1;
            if (card.Data.cardType == CardType.Monster) card.SetLife(card.Data.defense);

            if (owner == Player) LayoutHand();
            else LayoutEnemyHand();
            var move = card.MoveTo(board.CardPosition(zone), Board.CardRotation(owner, faceUp, defense), 0.45f);
            if (spawnFigure && card.Data.cardType == CardType.Monster && faceUp) move.OnComplete(() => SpawnFigure(card));
            return move;
        }

        /// <summary>
        /// Escena de invocación: la cámara vuela hasta delante del monstruo, este aparece
        /// con su nombre en grande debajo y, al terminar, la cámara vuelve a su sitio.
        /// </summary>
        private IEnumerator SummonCinematic(CardView card, Tween move)
        {
            // En el turno del rival sigue en Busy al terminar; en el tuyo vuelve a Idle.
            var stateAfter = state == State.Busy ? State.Busy : State.Idle;
            state = State.Busy;
            BeginCloseUp();

            var fly = FlyCameraToFront(card);
            if (move != null && move.IsActive()) yield return move.WaitForCompletion();
            yield return fly.WaitForCompletion();

            SpawnFigure(card);
            audioSource.PlayOneShot(SoundFx.Summon);
            AestheticFx.SummonBurst(board.transform.TransformPoint(board.CardPosition(card.Zone)), card.Data.attribute);
            Shake(0.05f, 0.35f);
            summonTitle = card.Data.displayName;
            yield return DOTween.To(() => summonTitleAlpha, a => summonTitleAlpha = a, 1f, 0.3f).WaitForCompletion();
            yield return new WaitForSeconds(SummonHoldTime);
            yield return DOTween.To(() => summonTitleAlpha, a => summonTitleAlpha = a, 0f, 0.3f).WaitForCompletion();

            yield return EndCloseUp();
            if (!gameOver) state = stateAfter;
        }

        /// <summary>Guarda la vista normal y oculta la mano y el HUD para un primer plano.</summary>
        private void BeginCloseUp()
        {
            inCinematic = true;
            handRoot.gameObject.SetActive(false);
            AestheticFx.SetCloseUpFocus(true);
            savedCameraPosition = cam.transform.position;
            savedCameraRotation = cam.transform.rotation;
        }

        /// <summary>La cámara vuelve a la vista normal y reaparecen la mano y el HUD.</summary>
        private IEnumerator EndCloseUp()
        {
            yield return FlyCamera(savedCameraPosition, savedCameraRotation).WaitForCompletion();
            AestheticFx.SetCloseUpFocus(false);
            handRoot.gameObject.SetActive(true);
            inCinematic = false;
        }

        /// <summary>Lleva la cámara delante del monstruo de esa carta, mirándolo de frente.</summary>
        private Sequence FlyCameraToFront(CardView card)
        {
            // Los monstruos del jugador miran hacia el rival (+Z del tablero), y al revés.
            var up = board.transform.up;
            var forward = board.transform.forward * (card.Owner == Player ? 1f : -1f);
            var focus = board.transform.TransformPoint(board.CardPosition(card.Zone)) + up * SummonFocusHeight;
            var shotPosition = focus + forward * SummonCameraDistance + up * SummonCameraLift;
            return FlyCamera(shotPosition, Quaternion.LookRotation(focus - shotPosition, up));
        }

        private Sequence FlyCamera(Vector3 position, Quaternion rotation)
        {
            var cameraTransform = cam.transform;
            cameraTransform.DOKill();
            return DOTween.Sequence()
                .SetTarget(cameraTransform)
                .Join(cameraTransform.DOMove(position, SummonCameraTravel).SetEase(Ease.InOutCubic))
                .Join(cameraTransform.DORotateQuaternion(rotation, SummonCameraTravel).SetEase(Ease.InOutCubic));
        }

        /// <summary>El modelo 3D del monstruo aparece de pie sobre su carta (si tiene uno asignado).</summary>
        private static void SpawnFigure(CardView card)
        {
            if (card == null || card.Figure != null || card.Location != CardLocation.Field || !card.FaceUp) return;
            card.Figure = MonsterFigure.Spawn(card);
        }

        private void ChangePosition(CardView card)
        {
            bool flipSummon = !card.FaceUp;
            if (flipSummon)
            {
                card.FaceUp = true;
                card.DefensePosition = false;
                ShowMessage($"Invocación por volteo: ¡{card.Data.displayName}!");
            }
            else
            {
                card.DefensePosition = !card.DefensePosition;
                ShowMessage($"{card.Data.displayName} pasa a posición de {(card.DefensePosition ? "defensa" : "ataque")}.");
            }

            card.PositionChangedTurn = turn;
            var move = card.MoveTo(board.CardPosition(card.Zone), Board.CardRotation(card.Owner, card.FaceUp, card.DefensePosition), 0.35f);
            Deselect();
            if (flipSummon) StartCoroutine(SummonCinematic(card, move));
        }

        private void ActivateSet(CardView card)
        {
            card.FaceUp = true;
            Deselect();
            ShowMessage($"Activas {card.Data.displayName}.");
            card.MoveTo(board.CardPosition(card.Zone), Board.CardRotation(card.Owner, true, false), 0.35f)
                .OnComplete(() => ResolveAndDiscard(card));
        }

        /// <summary>Las mágicas/trampas normales se resuelven y van al cementerio.</summary>
        private void ResolveAndDiscard(CardView card)
        {
            DOVirtual.DelayedCall(1f, () =>
            {
                if (card == null) return;
                ShowMessage($"{card.Data.displayName} se resuelve (efecto pendiente de programar).");
                SendToGraveyard(card);
            });
        }

        private void SendToGraveyard(CardView card)
        {
            if (card == selected) Deselect();
            if (card.Zone != null)
            {
                card.Zone.Card = null;
                card.Zone = null;
            }

            if (card.Figure != null)
            {
                card.Figure.Vanish();
                card.Figure = null;
            }

            int owner = card.Owner;
            if (hands[owner].Remove(card))
            {
                if (owner == Player) LayoutHand();
                else LayoutEnemyHand();
            }

            var pile = graveyards[owner];
            card.transform.SetParent(board.transform, true);
            card.Location = CardLocation.Graveyard;
            card.SetGlow(null);
            card.FaceUp = true;
            card.DefensePosition = false;
            if (card.Data.cardType == CardType.Monster) card.SetLife(card.Data.defense);
            pile.Add(card);
            card.MoveTo(board.GraveyardPosition(owner, pile.Count - 1), Board.CardRotation(owner, true, false), 0.45f);
        }

        // ------------------------------------------------------------- Combate

        /// <summary>Solo atacan los monstruos boca arriba en ataque, una vez por turno y nunca en el turno 1.</summary>
        private bool CanAttack(CardView card) =>
            card.Location == CardLocation.Field && card.Data.cardType == CardType.Monster &&
            card.Owner == currentPlayer && card.FaceUp && !card.DefensePosition &&
            card.AttackedTurn != turn && turn > 1;

        private void BeginAttack()
        {
            var targets = OwnMonsters(Opponent);
            if (targets.Count == 0)
            {
                StartCoroutine(PlayerAttack(selected, null));
                return;
            }

            ClearHighlights();
            foreach (var target in targets) Highlight(target.Zone, AttackTarget);
            state = State.ChoosingTarget;
            ShowMessage("Elige objetivo. Ojo: los monstruos en ataque contraatacan.");
        }

        private IEnumerator PlayerAttack(CardView attacker, CardView defender)
        {
            ClearSelection();
            state = State.Busy;
            yield return Attack(attacker, defender);
            if (!gameOver) state = State.Idle;
        }

        /// <summary>
        /// El atacante golpea: al defensor le resta su ATK a la vida (si llega a 0
        /// va al cementerio) o, si no hay defensor, resta su ATK a los LP del rival.
        /// </summary>
        private IEnumerator Attack(CardView attacker, CardView defender)
        {
            attacker.AttackedTurn = turn;
            int defenderOwner = 1 - attacker.Owner;

            if (attacker.Figure != null) yield return FigureAttack(attacker, defender);
            else yield return CardAttack(attacker, defender);

            if (lifePoints[defenderOwner] <= 0)
                GameOver(attacker.Owner, defenderOwner == Player ? "Tus LP llegan a 0." : "Los LP del rival llegan a 0.");
        }

        /// <summary>Punto del tablero que recibe el golpe: el monstruo atacado o, en un ataque directo, el campo rival.</summary>
        private Vector3 ImpactPoint(CardView defender, int defenderOwner) => defender != null
            ? board.CardPosition(defender.Zone)
            : new Vector3(0f, 0.3f, defenderOwner == Player ? -DirectAttackDepth : DirectAttackDepth);

        /// <summary>
        /// El monstruo 3D deja su carta, corre hasta delante del enemigo seguido por la cámara,
        /// golpea con su animación (el daño entra en el momento del golpe) y vuelve a su carta.
        /// </summary>
        private IEnumerator FigureAttack(CardView attacker, CardView defender)
        {
            var figure = attacker.Figure;
            int defenderOwner = 1 - attacker.Owner;
            var impact = ImpactPoint(defender, defenderOwner);

            var start = figure.transform.position;
            var target = board.transform.TransformPoint(new Vector3(impact.x, 0f, impact.z));
            var direction = target - start;
            direction.y = 0f;
            direction.Normalize();
            var standPoint = target - direction * AttackStandOff;

            // La cámara se coloca detrás del atacante, mirando hacia su objetivo.
            BeginCloseUp();
            yield return FlyCamera(AttackShotPosition(start, target), AttackShotRotation(start, target)).WaitForCompletion();

            figure.Detach(board.transform);
            var run = figure.MoveTo(standPoint, AttackRunTime);
            while (run.IsActive() && !run.IsComplete())
            {
                FollowAttack(figure.transform.position, target);
                yield return null;
            }

            var (impactTime, length) = figure.PlayAttackTimed();
            yield return FollowFor(impactTime, figure, target);
            ApplyHit(attacker, defender, impact);
            yield return FollowFor(Mathf.Max(length - impactTime, 0.3f), figure, target);

            if (defender != null && defender.Life <= 0) yield return DestroyMonster(defender);

            // Si el contraataque lo ha matado, muere ahí mismo en vez de volver.
            if (attacker.Life <= 0)
            {
                yield return DestroyMonster(attacker);
                yield return EndCloseUp();
                yield break;
            }

            // Vuelve a su carta mientras la cámara regresa a la vista normal.
            var back = figure.ReturnTo(attacker, AttackRunTime);
            yield return EndCloseUp();
            if (back.IsActive()) yield return back.WaitForCompletion();
        }

        /// <summary>Ataque sin modelo 3D: la carta se lanza hacia el objetivo y vuelve.</summary>
        private IEnumerator CardAttack(CardView attacker, CardView defender)
        {
            int defenderOwner = 1 - attacker.Owner;
            var home = board.CardPosition(attacker.Zone);
            var impact = ImpactPoint(defender, defenderOwner);

            var attackerTransform = attacker.transform;
            attackerTransform.DOKill();
            yield return attackerTransform.DOLocalMove(Vector3.Lerp(home, impact, 0.8f) + Vector3.up * 0.4f, 0.25f)
                .SetEase(Ease.InQuad).WaitForCompletion();
            ApplyHit(attacker, defender, impact);
            yield return attackerTransform.DOLocalMove(home, 0.3f).SetEase(Ease.OutCubic).WaitForCompletion();

            if (defender != null && defender.Life <= 0) yield return DeathCloseUp(defender);
            if (attacker.Life <= 0) yield return DeathCloseUp(attacker);
        }

        /// <summary>La cámara se acerca para ver morir de frente al monstruo destruido.</summary>
        private IEnumerator DeathCloseUp(CardView monster)
        {
            yield return new WaitForSeconds(0.2f);
            bool closeUp = monster.Figure != null;
            if (closeUp)
            {
                BeginCloseUp();
                yield return FlyCameraToFront(monster).WaitForCompletion();
            }
            yield return DestroyMonster(monster);
            if (closeUp) yield return EndCloseUp();
            yield return new WaitForSeconds(0.45f);
        }

        /// <summary>
        /// El golpe: resta vida al defensor (o LP en un ataque directo). Un defensor boca arriba
        /// en ataque contraataca con su ATK y, si muere, el daño sobrante pasa a los LP de su
        /// dueño; en defensa (o boca abajo) ni contraataca ni deja pasar daño.
        /// </summary>
        private void ApplyHit(CardView attacker, CardView defender, Vector3 impact)
        {
            int defenderOwner = 1 - attacker.Owner;
            int damage = attacker.Data.attack;

            if (defender == null)
            {
                lifePoints[defenderOwner] = Mathf.Max(lifePoints[defenderOwner] - damage, 0);
                Impact(damage);
                Popup(impact, $"-{damage}");
                ShowMessage(defenderOwner == Player
                    ? $"¡{attacker.Data.displayName} te ataca directamente! Pierdes {damage} LP."
                    : $"¡Ataque directo de {attacker.Data.displayName}! El rival pierde {damage} LP.");
                return;
            }

            bool fightsBack = FightsBack(defender);
            int lifeBefore = defender.Life;

            // Un monstruo boca abajo se revela al ser atacado; si no, acusa el golpe.
            if (!defender.FaceUp)
            {
                defender.FaceUp = true;
                defender.MoveTo(board.CardPosition(defender.Zone),
                    Board.CardRotation(defenderOwner, true, defender.DefensePosition), 0.2f)
                    .OnComplete(() => SpawnFigure(defender));
            }
            else
            {
                defender.transform.DOPunchPosition(Vector3.right * 0.1f, 0.3f, 12);
                if (defender.Figure != null)
                {
                    if (fightsBack) defender.Figure.PlayAttack();
                    else defender.Figure.PlayHit();
                }
            }

            defender.SetLife(defender.Life - damage);
            Impact(damage);
            Popup(impact, $"-{damage}");
            string names = $"{attacker.Data.displayName} ataca a {defender.Data.displayName}";
            string result = defender.Life <= 0 ? $"¡{names} y lo destruye!" : $"{names}: le quedan {defender.Life} de vida.";

            if (!fightsBack)
            {
                ShowMessage(result);
                return;
            }

            // Daño sobrante a los LP del dueño del defensor.
            int overflow = Mathf.Max(0, damage - lifeBefore);
            if (overflow > 0)
            {
                lifePoints[defenderOwner] = Mathf.Max(lifePoints[defenderOwner] - overflow, 0);
                Popup(ImpactPoint(null, defenderOwner), $"-{overflow} LP");
                result += $" {overflow} de daño pasa a los LP.";
            }

            // Contraataque: el atacante recibe el ATK del defensor.
            int counter = defender.Data.attack;
            attacker.SetLife(attacker.Life - counter);
            var attackerPoint = attacker.Figure != null
                ? board.transform.InverseTransformPoint(attacker.Figure.transform.position)
                : board.CardPosition(attacker.Zone);
            Popup(attackerPoint, $"-{counter}");
            if (attacker.Figure != null)
            {
                var hitFigure = attacker.Figure;
                DOVirtual.DelayedCall(0.25f, () => { if (hitFigure != null) hitFigure.PlayHit(); });
            }
            result += attacker.Life <= 0
                ? $" ¡{defender.Data.displayName} contraataca y lo destruye!"
                : $" {defender.Data.displayName} contraataca: -{counter}.";
            ShowMessage(result, 5f);
        }

        /// <summary>Solo contraataca (y deja pasar daño sobrante) un monstruo boca arriba en posición de ataque.</summary>
        private static bool FightsBack(CardView defender) =>
            defender != null && defender.FaceUp && !defender.DefensePosition;

        /// <summary>El monstruo muere (animación y sonido) y su carta va al cementerio.</summary>
        private IEnumerator DestroyMonster(CardView defender)
        {
            audioSource.PlayOneShot(SoundFx.Death);
            Shake(0.06f, 0.3f);
            if (defender.Figure != null)
            {
                float dying = defender.Figure.Die();
                defender.Figure = null;
                yield return new WaitForSeconds(dying);
            }
            SendToGraveyard(defender);
            yield return new WaitForSeconds(0.3f);
        }

        /// <summary>Plano por encima del hombro del atacante, con el objetivo en el centro.</summary>
        private static Vector3 AttackShotPosition(Vector3 attacker, Vector3 target)
        {
            var direction = target - attacker;
            direction.y = 0f;
            direction.Normalize();
            var side = Vector3.Cross(Vector3.up, direction);
            return attacker - direction * AttackCameraBack + side * AttackCameraSide + Vector3.up * AttackCameraHeight;
        }

        private static Quaternion AttackShotRotation(Vector3 attacker, Vector3 target)
        {
            var look = Vector3.Lerp(attacker, target, 0.5f) + Vector3.up * SummonFocusHeight;
            return Quaternion.LookRotation(look - AttackShotPosition(attacker, target), Vector3.up);
        }

        /// <summary>La cámara sigue al atacante suavemente mientras se mueve.</summary>
        private void FollowAttack(Vector3 attacker, Vector3 target)
        {
            float t = 1f - Mathf.Exp(-AttackCameraFollow * Time.deltaTime);
            var cameraTransform = cam.transform;
            cameraTransform.position = Vector3.Lerp(cameraTransform.position, AttackShotPosition(attacker, target), t);
            cameraTransform.rotation = Quaternion.Slerp(cameraTransform.rotation, AttackShotRotation(attacker, target), t);
        }

        private IEnumerator FollowFor(float seconds, MonsterFigure figure, Vector3 target)
        {
            for (float elapsed = 0f; elapsed < seconds; elapsed += Time.deltaTime)
            {
                if (figure != null) FollowAttack(figure.transform.position, target);
                yield return null;
            }
        }

        /// <summary>Número flotante que sube y desaparece sobre el tablero.</summary>
        private void Popup(Vector3 localPosition, string text)
        {
            var root = new GameObject("Popup").transform;
            root.SetParent(board.transform, false);
            root.localPosition = localPosition + Vector3.up * 0.6f;
            root.rotation = cam.transform.rotation;
            Primitives.Text("Text", root, text, Vector2.zero, new Vector2(1.6f, 0.4f), 0f, DamageColor);

            DOTween.Sequence()
                .SetTarget(root)
                .Append(root.DOLocalMoveY(root.localPosition.y + 1f, 1.1f).SetEase(Ease.OutCubic))
                .Insert(0.7f, root.DOScale(0f, 0.4f).SetEase(Ease.InBack))
                .OnComplete(() => Destroy(root.gameObject));
        }

        // ---------------------------------------------------------------- Rival

        private IEnumerator EnemyTurn()
        {
            ShowMessage("Turno del rival.");
            yield return new WaitForSeconds(Mathf.Max(enemyActionDelay, BannerDuration));

            var summon = ChooseEnemySummon(out var sacrifices);
            if (summon != null)
            {
                foreach (var sacrifice in sacrifices)
                {
                    ShowMessage($"El rival sacrifica a {sacrifice.Data.displayName}.");
                    SendToGraveyard(sacrifice);
                    yield return new WaitForSeconds(0.5f);
                }

                // Los monstruos mucho más resistentes que fuertes se quedan defendiendo.
                bool setDefense = summon.Data.defense >= summon.Data.attack + 500;
                var zone = Array.Find(board.MonsterZones[Opponent], z => z.IsFree);
                var move = PlaceOnField(summon, zone, !setDefense, setDefense, spawnFigure: false);
                Pay(Opponent, summon.Data.cost);
                normalSummonUsed = true;
                ShowMessage(setDefense
                    ? "El rival coloca un monstruo boca abajo en defensa."
                    : $"El rival invoca a {summon.Data.displayName} en posición de ataque.");
                if (!setDefense) yield return SummonCinematic(summon, move);
                yield return new WaitForSeconds(enemyActionDelay);
            }

            foreach (var attacker in OwnMonsters(Opponent))
            {
                if (gameOver) yield break;
                if (!CanAttack(attacker)) continue;

                var target = ChooseEnemyTarget(attacker, out bool worthIt);
                if (!worthIt) continue;
                yield return Attack(attacker, target);
                yield return new WaitForSeconds(enemyActionDelay * 0.6f);
            }

            if (gameOver) yield break;
            ShowMessage("El rival termina su turno.");
            yield return new WaitForSeconds(enemyActionDelay * 0.6f);
            StartTurn(Player);
        }

        /// <summary>El monstruo de mayor ATK que el rival pueda pagar e invocar, sacrificando a sus más débiles si compensa.</summary>
        private CardView ChooseEnemySummon(out List<CardView> sacrifices)
        {
            sacrifices = new List<CardView>();
            var own = OwnMonsters(Opponent);
            own.Sort((a, b) => a.Data.attack.CompareTo(b.Data.attack));
            bool freeZone = Array.Exists(board.MonsterZones[Opponent], z => z.IsFree);

            CardView best = null;
            foreach (var card in hands[Opponent])
            {
                var data = card.Data;
                if (data.cardType != CardType.Monster || !CanPay(Opponent, data.cost)) continue;

                int needed = TributesFor(data.level);
                if (needed > own.Count || (needed == 0 && !freeZone)) continue;

                var candidates = own.GetRange(0, needed);
                if (needed > 0 && candidates[needed - 1].Data.attack >= data.attack) continue;

                if (best == null || data.attack > best.Data.attack)
                {
                    best = card;
                    sacrifices = candidates;
                }
            }
            return best;
        }

        /// <summary>
        /// Elige objetivo puntuando cada opción: destruir monstruos (cuanto más fuertes, mejor) y
        /// el daño sobrante suman; morir por el contraataque resta mucho. Sin monstruos rivales,
        /// ataque directo (null). <paramref name="worthIt"/> es false si cualquier ataque sale a perder.
        /// </summary>
        private CardView ChooseEnemyTarget(CardView attacker, out bool worthIt)
        {
            worthIt = true;
            var targets = OwnMonsters(Player);
            if (targets.Count == 0) return null;

            int atk = attacker.Data.attack;
            CardView best = null;
            float bestScore = float.MinValue;
            foreach (var target in targets)
            {
                bool kills = target.Life <= atk;
                bool counters = FightsBack(target);
                bool dies = counters && target.Data.attack >= attacker.Life;

                float score = kills ? 1000f + target.Data.attack : atk * 0.2f;
                if (kills && counters) score += atk - target.Life;
                if (dies) score -= kills ? 600f : 2000f;
                if (counters && !dies) score -= target.Data.attack * 0.3f;

                if (score > bestScore)
                {
                    bestScore = score;
                    best = target;
                }
            }

            worthIt = bestScore > 0f;
            return best;
        }

        private void ShowMessage(string text, float seconds = 4f)
        {
            message = text;
            messageUntil = Time.time + seconds;
        }

        private void Restart() => PrototypeBootstrap.LoadDuel();

        // ------------------------------------------------------------------ HUD

        private bool IsPlayerTurn => currentPlayer == Player && !gameOver;

        private List<(string label, bool enabled, Action run)> CurrentActions()
        {
            var actions = new List<(string, bool, Action)>();
            if (selected == null || !IsPlayerTurn) return actions;

            if (state == State.ChoosingTributes || state == State.ChoosingTarget)
            {
                actions.Add(("Cancelar", true, CancelPlay));
                return actions;
            }

            var card = selected;
            var data = card.Data;

            if (card.Owner != Player)
            {
                actions.Add(("Cerrar", true, Deselect));
                return actions;
            }

            if (card.Location == CardLocation.Hand)
            {
                // El botón elige qué hará la carta al soltarla en el campo.
                switch (data.cardType)
                {
                    case CardType.Monster:
                        actions.Add((placeFaceDown ? "Al soltar: colocar en defensa" : "Al soltar: invocar en ataque",
                            !normalSummonUsed, () => placeFaceDown = !placeFaceDown));
                        break;
                    case CardType.Spell:
                        actions.Add((placeFaceDown ? "Al soltar: colocar boca abajo" : "Al soltar: activar",
                            true, () => placeFaceDown = !placeFaceDown));
                        break;
                }
            }
            else if (card.Location == CardLocation.Field)
            {
                if (data.cardType == CardType.Monster)
                {
                    string attackLabel = OwnMonsters(Opponent).Count == 0 ? "Ataque directo" : "Atacar";
                    actions.Add((attackLabel, CanAttack(card), BeginAttack));

                    string label = !card.FaceUp ? "Invocación por volteo" : card.DefensePosition ? "Cambiar a ataque" : "Cambiar a defensa";
                    actions.Add((label, CanChangePosition(card), () => ChangePosition(card)));
                }
                else if (!card.FaceUp)
                {
                    actions.Add(("Activar", CanActivateSet(card), () => ActivateSet(card)));
                }
            }

            actions.Add(("Cancelar", true, Deselect));
            return actions;
        }

        private bool CanChangePosition(CardView card) =>
            card.TurnPlaced < turn && card.PositionChangedTurn < turn && card.AttackedTurn != turn;

        private bool CanActivateSet(CardView card) => card.Data.cardType == CardType.Spell || card.TurnPlaced < turn;

        private string SelectedHint()
        {
            if (selected == null || selected.Owner != Player) return null;
            var data = selected.Data;
            bool monster = data.cardType == CardType.Monster;

            if (selected.Location == CardLocation.Hand)
            {
                if (!CanPay(Player, data.cost)) return $"Te falta maná: cuesta {data.cost} y tienes {mana[Player]}.";
                if (monster && normalSummonUsed) return "Ya hiciste tu Invocación Normal este turno.";
                int needed = monster ? TributesFor(data.level) : 0;
                string tributesText = needed > 0 ? $" Necesita {needed} tributo{(needed > 1 ? "s" : "")}." : "";
                return "Arrastra la carta a tu campo para jugarla." + tributesText;
            }

            if (selected.Location == CardLocation.Field && monster)
            {
                if (turn == 1) return "En el primer turno del duelo no se puede atacar.";
                if (selected.AttackedTurn == turn) return "Ya atacó este turno.";
                if (!selected.FaceUp || selected.DefensePosition) return "Solo atacan los monstruos en posición de ataque.";
            }

            if (selected.Location == CardLocation.Field && data.cardType == CardType.Trap && !selected.FaceUp && !CanActivateSet(selected))
                return "Las trampas no se activan el turno en que se colocan.";
            return null;
        }

        private static string CardInfo(CardView card)
        {
            var data = card.Data;
            if (card.Owner != Player && !card.FaceUp)
                return "<b><size=22>Carta boca abajo</size></b>\n<color=#bbbbbb>Una carta del rival que aún no se ha revelado.</color>";

            string stats = "";
            if (data.cardType == CardType.Monster)
            {
                stats = $"\nATK {data.attack}  /  DEF {data.defense}";
                if (card.Location == CardLocation.Field)
                    stats += $"\nVida: <color={(card.Life < data.defense ? "#ff7070" : "#80ff80")}>{card.Life}</color> / {data.defense}";
            }
            return $"<b><size=22>{data.displayName}</size></b>  <color=#8fc8ff><b>{data.cost} maná</b></color>\n" +
                   $"<color=#f0c060>{CardFactory.TypeLine(data)}</color>{stats}\n\n{data.description}";
        }

        private void OnGUI()
        {
            EnsureStyles();
            float scale = Screen.height / 720f;
            GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(scale, scale, 1f));
            float width = Screen.width / scale;
            if (Event.current.type == EventType.Layout) guiRects.Clear();

            // Fin de partida y pausa ocupan toda la pantalla.
            if (gameOver)
            {
                DrawEndScreen(width);
                return;
            }
            if (paused)
            {
                DrawPause(width);
                return;
            }

            // Viendo una carta en grande: solo el botón de cerrar.
            if (state == State.Inspecting)
            {
                if (Button(new Rect(width - 84, 16, 68, 68), "X", true, closeStyle)) CloseInspect();
                return;
            }

            DrawPortrait(width);
            DrawTurnBanner(width);

            // Escena de invocación: sin HUD, solo el nombre del monstruo abajo, con sombra.
            if (inCinematic)
            {
                if (summonTitleAlpha <= 0f) return;
                var titleRect = new Rect(0f, 720f - 190f, width, 120f);
                var previous = GUI.color;
                GUI.color = new Color(0f, 0f, 0f, 0.75f * summonTitleAlpha);
                GUI.Label(new Rect(titleRect.x + 4f, titleRect.y + 4f, titleRect.width, titleRect.height), summonTitle, titleStyle);
                GUI.color = new Color(1f, 1f, 1f, summonTitleAlpha);
                GUI.Label(titleRect, summonTitle, titleStyle);
                GUI.color = previous;
                return;
            }

            // Estado del duelo
            string whose = currentPlayer < 0 ? "Lanzando moneda..."
                : currentPlayer == Player ? "<color=#80d0ff>Tu turno</color>"
                : "<color=#ff9090>Turno del rival</color>";

            // Panel de estado: turno, barras de LP animadas de los dos jugadores y datos de mazo y mano.
            Panel(new Rect(12, 12, 300, 238));
            GUI.Label(new Rect(26, 16, 276, 32), $"<b><size=24>Turno {Mathf.Max(turn, 1)}</size></b>  {whose}", labelStyle);

            GUI.Label(new Rect(26, 50, 60, 24), "<b>Tú</b>", labelStyle);
            DrawLifeBar(new Rect(86, 52, 212, 22), Player);
            GUI.Label(new Rect(26, 76, 276, 22),
                $"<size=15>Mazo {decks[Player].Count} · Mano {Hand.Count} · Cementerio {graveyards[Player].Count}</size>", labelStyle);

            GUI.Label(new Rect(26, 104, 60, 24), "<b>Rival</b>", labelStyle);
            DrawLifeBar(new Rect(86, 106, 212, 22), Opponent);
            GUI.Label(new Rect(26, 130, 276, 22),
                $"<size=15>Mazo {decks[Opponent].Count} · Mano {hands[Opponent].Count} · Cementerio {graveyards[Opponent].Count} · " +
                $"Maná <color=#8fc8ff>{mana[Opponent]}</color>/{maxMana[Opponent]}</size>", labelStyle);

            GUI.Label(new Rect(26, 158, 276, 90),
                $"Invocación Normal: {(normalSummonUsed ? "<color=#ff8080>usada</color>" : "<color=#80ff80>disponible</color>")}\n" +
                "<size=15><color=#bbbbbb>Arrastra lo que brilla: cartas al campo y monstruos hacia el rival.</color></size>",
                labelStyle);

            bool canEndTurn = IsPlayerTurn && (state == State.Idle || state == State.CardSelected);
            if (Button(new Rect(12, 260, 300, 52), "Terminar turno", canEndTurn)) EndPlayerTurn();
            if (Button(new Rect(12, 320, 300, 52), "Reiniciar", true)) Restart();
            if (Button(new Rect(12, 380, 300, 52), "Menú", true)) PrototypeBootstrap.LoadMenu();
            if (Button(new Rect(320, 12, 56, 56), "II", true, PauseButtonStyle)) TogglePause();
            DrawMana();

            // Mensajes
            if (Time.time < messageUntil)
            {
                var messageRect = gameOver
                    ? new Rect(width * 0.5f - 330, 250, 660, 110)
                    : new Rect(width * 0.5f - 320, 12, 640, 50);
                Panel(messageRect);
                GUI.Label(messageRect, message, gameOver ? bigStyle : centeredStyle);
            }

            // Carta seleccionada y acciones
            if (selected == null) return;

            float x = width - 342;
            string hint = SelectedHint();
            Panel(new Rect(x, 12, 330, 230));
            GUI.Label(new Rect(x + 14, 20, 302, 216),
                CardInfo(selected) + (hint != null ? $"\n\n<color=#ffb060>{hint}</color>" : ""),
                labelStyle);

            float y = 252;
            foreach (var (label, enabled, run) in CurrentActions())
            {
                if (Button(new Rect(x, y, 330, 50), label, enabled)) run();
                y += 58;
            }
        }

        /// <summary>Tu retrato en un círculo morado, abajo a la derecha, con un leve vaivén.</summary>
        private void DrawPortrait(float width)
        {
            if (portraitAlpha <= 0f) return;
            if (portrait == null) portrait = Resources.Load<Texture2D>("UI/PlayerPortrait");
            if (portrait == null) return;

            float bob = Mathf.Sin(Time.time * 1.6f) * 3f;
            var rect = new Rect(width - PortraitSize - 16f, 720f - PortraitSize - 12f + bob, PortraitSize, PortraitSize);
            var previous = GUI.color;
            GUI.color = new Color(1f, 1f, 1f, portraitAlpha);
            GUI.DrawTexture(rect, portrait, ScaleMode.ScaleToFit, true);
            GUI.color = previous;

            DrawEmotes(rect);
        }

        private void Panel(Rect rect)
        {
            guiRects.Add(rect);
            GUI.Box(rect, GUIContent.none, panelStyle);
        }

        private bool Button(Rect rect, string label, bool enabled, GUIStyle style = null)
        {
            guiRects.Add(rect);
            bool previous = GUI.enabled;
            GUI.enabled = enabled;
            bool clicked = GUI.Button(rect, label, style ?? buttonStyle);
            GUI.enabled = previous;
            // Los botones invisibles (el retrato) ya tienen su propio sonido.
            if (clicked && enabled && style != GUIStyle.none) audioSource.PlayOneShot(SoundFx.Click, 0.7f);
            return clicked && enabled;
        }

        private bool IsOverGui(Vector2 screenPosition)
        {
            float scale = Screen.height / 720f;
            var guiPosition = new Vector2(screenPosition.x, Screen.height - screenPosition.y) / scale;
            foreach (var rect in guiRects)
                if (rect.Contains(guiPosition)) return true;
            return false;
        }

        private void EnsureStyles()
        {
            if (labelStyle != null) return;

            labelStyle = new GUIStyle(GUI.skin.label) { fontSize = 18, richText = true, wordWrap = true };
            labelStyle.normal.textColor = Color.white;
            centeredStyle = new GUIStyle(labelStyle) { alignment = TextAnchor.MiddleCenter };
            bigStyle = new GUIStyle(centeredStyle) { fontSize = 30, fontStyle = FontStyle.Bold };
            buttonStyle = new GUIStyle(GUI.skin.button) { fontSize = 20 };
            closeStyle = new GUIStyle(GUI.skin.button) { fontSize = 36, fontStyle = FontStyle.Bold };
            titleStyle = new GUIStyle(GUI.skin.label) { fontSize = 72, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            titleStyle.normal.textColor = Color.white;

            var background = new Texture2D(1, 1);
            background.SetPixel(0, 0, new Color(0f, 0f, 0f, 0.7f));
            background.Apply();
            panelStyle = new GUIStyle(GUI.skin.box);
            panelStyle.normal.background = background;
        }

        private static void Shuffle<T>(List<T> list)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = UnityEngine.Random.Range(0, i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
        }
    }
}
