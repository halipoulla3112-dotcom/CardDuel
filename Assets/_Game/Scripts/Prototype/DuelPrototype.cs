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
    public class DuelPrototype : MonoBehaviour
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

        private GUIStyle labelStyle;
        private GUIStyle centeredStyle;
        private GUIStyle bigStyle;
        private GUIStyle buttonStyle;
        private GUIStyle closeStyle;
        private GUIStyle panelStyle;

        private void Start()
        {
            SetupCamera();
            board = Board.Build(transform);

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
        }

        // --------------------------------------------------------------- Inicio

        private IEnumerator BeginDuel()
        {
            bool playerFirst = UnityEngine.Random.value < 0.5f;
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
            yield return DOTween.Sequence()
                .Append(coin.DOLocalMoveY(3f, 0.7f).SetEase(Ease.OutQuad))
                .Append(coin.DOLocalMoveY(0.1f, 0.7f).SetEase(Ease.InQuad))
                .Insert(0f, coin.DOLocalRotate(new Vector3(360f * spins, 0f, 0f), 1.4f, RotateMode.FastBeyond360).SetEase(Ease.OutSine))
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
            state = State.Busy;
            StartTurn(Opponent);
        }

        private void GameOver(int winner, string reason)
        {
            if (gameOver) return;
            gameOver = true;
            ClearSelection();
            state = State.Busy;
            ShowMessage($"{reason} {(winner == Player ? "¡Has ganado!" : "Has perdido.")}", float.PositiveInfinity);
        }

        // ---------------------------------------------------------------- Input

        private void Update()
        {
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
                if (card == null) card = hit.collider.GetComponent<CardView>();
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
                    if (fieldCard != null && highlighted.Contains(fieldCard.Zone))
                        StartCoroutine(PlayerAttack(selected, fieldCard));
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
            float spacing = Mathf.Min(handCardSpacing, handMaxWidth / Mathf.Max(count, 1));
            for (int i = 0; i < count; i++)
            {
                float offset = i - (count - 1) * 0.5f;
                var position = new Vector3(offset * spacing, -offset * offset * 0.02f, -i * 0.01f);
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

            bool faceUp = mode == PlayMode.SummonAttack || mode == PlayMode.ActivateSpell;
            var move = PlaceOnField(card, zone, faceUp, mode == PlayMode.SetMonster);

            string cardName = card.Data.displayName;
            switch (mode)
            {
                case PlayMode.SummonAttack:
                    normalSummonUsed = true;
                    ShowMessage($"¡Invocas a {cardName} en posición de ataque!");
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
        private Tween PlaceOnField(CardView card, BoardZone zone, bool faceUp, bool defense)
        {
            int owner = card.Owner;
            hands[owner].Remove(card);

            card.transform.SetParent(board.transform, true);
            card.Location = CardLocation.Field;
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
            return card.MoveTo(board.CardPosition(zone), Board.CardRotation(owner, faceUp, defense), 0.45f);
        }

        private void ChangePosition(CardView card)
        {
            if (!card.FaceUp)
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
            card.MoveTo(board.CardPosition(card.Zone), Board.CardRotation(card.Owner, card.FaceUp, card.DefensePosition), 0.35f);
            Deselect();
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

            int owner = card.Owner;
            if (hands[owner].Remove(card))
            {
                if (owner == Player) LayoutHand();
                else LayoutEnemyHand();
            }

            var pile = graveyards[owner];
            card.transform.SetParent(board.transform, true);
            card.Location = CardLocation.Graveyard;
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
            ShowMessage("Toca el monstruo rival al que quieres atacar.");
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
            int damage = attacker.Data.attack;

            var home = board.CardPosition(attacker.Zone);
            var impact = defender != null
                ? board.CardPosition(defender.Zone)
                : new Vector3(0f, 0.3f, defenderOwner == Player ? -3.6f : 3.6f);

            var attackerTransform = attacker.transform;
            attackerTransform.DOKill();
            yield return attackerTransform.DOLocalMove(Vector3.Lerp(home, impact, 0.8f) + Vector3.up * 0.4f, 0.25f)
                .SetEase(Ease.InQuad).WaitForCompletion();

            if (defender == null)
            {
                lifePoints[defenderOwner] = Mathf.Max(lifePoints[defenderOwner] - damage, 0);
                Popup(impact, $"-{damage}");
                ShowMessage(defenderOwner == Player
                    ? $"¡{attacker.Data.displayName} te ataca directamente! Pierdes {damage} LP."
                    : $"¡Ataque directo de {attacker.Data.displayName}! El rival pierde {damage} LP.");
            }
            else
            {
                // Un monstruo boca abajo se revela al ser atacado; si no, tiembla con el golpe.
                if (!defender.FaceUp)
                {
                    defender.FaceUp = true;
                    defender.MoveTo(board.CardPosition(defender.Zone),
                        Board.CardRotation(defenderOwner, true, defender.DefensePosition), 0.2f);
                }
                else
                {
                    defender.transform.DOPunchPosition(Vector3.right * 0.1f, 0.3f, 12);
                }

                defender.SetLife(defender.Life - damage);
                Popup(impact, $"-{damage}");

                string names = $"{attacker.Data.displayName} ataca a {defender.Data.displayName}";
                ShowMessage(defender.Life <= 0
                    ? $"¡{names} y lo destruye!"
                    : $"{names}: le quedan {defender.Life} de vida.");
            }

            yield return attackerTransform.DOLocalMove(home, 0.3f).SetEase(Ease.OutCubic).WaitForCompletion();

            if (defender != null && defender.Life <= 0)
            {
                yield return new WaitForSeconds(0.2f);
                SendToGraveyard(defender);
                yield return new WaitForSeconds(0.45f);
            }

            if (lifePoints[defenderOwner] <= 0)
                GameOver(attacker.Owner, defenderOwner == Player ? "Tus LP llegan a 0." : "Los LP del rival llegan a 0.");
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
            yield return new WaitForSeconds(enemyActionDelay);

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
                PlaceOnField(summon, zone, !setDefense, setDefense);
                normalSummonUsed = true;
                ShowMessage(setDefense
                    ? "El rival coloca un monstruo boca abajo en defensa."
                    : $"El rival invoca a {summon.Data.displayName} en posición de ataque.");
                yield return new WaitForSeconds(enemyActionDelay);
            }

            foreach (var attacker in OwnMonsters(Opponent))
            {
                if (gameOver) yield break;
                if (!CanAttack(attacker)) continue;

                yield return Attack(attacker, ChooseEnemyTarget(attacker));
                yield return new WaitForSeconds(enemyActionDelay * 0.6f);
            }

            if (gameOver) yield break;
            ShowMessage("El rival termina su turno.");
            yield return new WaitForSeconds(enemyActionDelay * 0.6f);
            StartTurn(Player);
        }

        /// <summary>El monstruo de mayor ATK que el rival pueda invocar, sacrificando a sus más débiles si compensa.</summary>
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
                if (data.cardType != CardType.Monster) continue;

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

        /// <summary>Prefiere rematar al monstruo más fuerte que pueda destruir; si no, desgasta al de menos vida.</summary>
        private CardView ChooseEnemyTarget(CardView attacker)
        {
            var targets = OwnMonsters(Player);
            if (targets.Count == 0) return null;

            CardView killable = null;
            CardView weakest = null;
            foreach (var target in targets)
            {
                if (target.Life <= attacker.Data.attack && (killable == null || target.Data.attack > killable.Data.attack))
                    killable = target;
                if (weakest == null || target.Life < weakest.Life)
                    weakest = target;
            }
            return killable != null ? killable : weakest;
        }

        private void ShowMessage(string text, float seconds = 4f)
        {
            message = text;
            messageUntil = Time.time + seconds;
        }

        private void Restart()
        {
            DOTween.KillAll();
            SceneManager.LoadScene(SceneManager.GetActiveScene().name);
        }

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
            return $"<b><size=22>{data.displayName}</size></b>\n<color=#f0c060>{CardFactory.TypeLine(data)}</color>{stats}\n\n{data.description}";
        }

        private void OnGUI()
        {
            EnsureStyles();
            float scale = Screen.height / 720f;
            GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(scale, scale, 1f));
            float width = Screen.width / scale;
            if (Event.current.type == EventType.Layout) guiRects.Clear();

            // Viendo una carta en grande: solo el botón de cerrar.
            if (state == State.Inspecting)
            {
                if (Button(new Rect(width - 84, 16, 68, 68), "X", true, closeStyle)) CloseInspect();
                return;
            }

            // Estado del duelo
            string whose = currentPlayer < 0 ? "Lanzando moneda..."
                : currentPlayer == Player ? "<color=#80d0ff>Tu turno</color>"
                : "<color=#ff9090>Turno del rival</color>";

            Panel(new Rect(12, 12, 300, 238));
            GUI.Label(new Rect(26, 20, 276, 226),
                $"<b><size=24>Turno {Mathf.Max(turn, 1)}</size></b>  {whose}\n" +
                $"<b>Tú</b>   LP <color=#80d0ff><b>{lifePoints[Player]}</b></color>\n" +
                $"<size=15>Mazo {decks[Player].Count} · Mano {Hand.Count} · Cementerio {graveyards[Player].Count}</size>\n" +
                $"<b>Rival</b>   LP <color=#ff9090><b>{lifePoints[Opponent]}</b></color>\n" +
                $"<size=15>Mazo {decks[Opponent].Count} · Mano {hands[Opponent].Count} · Cementerio {graveyards[Opponent].Count}</size>\n" +
                $"Invocación Normal: {(normalSummonUsed ? "<color=#ff8080>usada</color>" : "<color=#80ff80>disponible</color>")}\n" +
                "<size=15><color=#bbbbbb>Arrastra cartas al campo. Toca tus monstruos para atacar.</color></size>",
                labelStyle);

            bool canEndTurn = IsPlayerTurn && (state == State.Idle || state == State.CardSelected);
            if (Button(new Rect(12, 260, 300, 52), "Terminar turno", canEndTurn)) EndPlayerTurn();
            if (Button(new Rect(12, 320, 300, 52), "Reiniciar", true)) Restart();

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
