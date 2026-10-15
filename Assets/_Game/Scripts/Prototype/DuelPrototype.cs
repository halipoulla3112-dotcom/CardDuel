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
    /// Prototipo jugable en solitario: mazo barajado, mano inicial de 4 cartas
    /// y colocación de cartas en el tablero con las reglas básicas de
    /// Yu-Gi-Oh! (una Invocación Normal por turno, tributos, ataque/defensa,
    /// cartas boca abajo). Funciona con ratón y con pantalla táctil.
    /// </summary>
    public class DuelPrototype : MonoBehaviour
    {
        private enum State { Busy, Idle, CardSelected, ChoosingZone, ChoosingTributes }
        private enum PlayMode { None, SummonAttack, SetMonster, ActivateSpell, SetSpellTrap }

        private const int Player = 0;
        private const int OpponentDeckSize = 30;

        private static readonly Quaternion HandFacing = Quaternion.Euler(-90f, 0f, 0f);
        private static readonly Color ZoneHighlight = new Color(0.2f, 0.9f, 1f);
        private static readonly Color TributeCandidate = new Color(1f, 0.35f, 0.25f);
        private static readonly Color TributeChosen = new Color(1f, 0.9f, 0.3f);
        private static readonly Color SelectedFieldCard = new Color(1f, 0.85f, 0.3f);

        [Header("Cámara")]
        [SerializeField] private Vector3 cameraPosition = new Vector3(0f, 9f, -7.2f);
        [SerializeField] private Vector3 cameraLookAt = new Vector3(0f, 0f, -0.4f);
        [SerializeField] private float cameraFieldOfView = 50f;

        [Header("Mano")]
        [SerializeField] private int startingHandSize = 4;
        [SerializeField] private Vector3 handOffset = new Vector3(0f, -1.55f, 4.2f);
        [SerializeField] private float handCardSpacing = 0.78f;
        [SerializeField] private float handMaxWidth = 4.8f;

        private Camera cam;
        private Board board;
        private Transform handRoot;

        private readonly List<CardData> deck = new List<CardData>();
        private readonly List<CardView> hand = new List<CardView>();
        private readonly List<CardView> graveyard = new List<CardView>();
        private readonly List<BoardZone> highlighted = new List<BoardZone>();
        private readonly List<CardView> tributes = new List<CardView>();
        private readonly List<Rect> guiRects = new List<Rect>();

        private State state = State.Busy;
        private PlayMode playMode;
        private CardView selected;
        private CardView hovered;
        private int tributesNeeded;
        private int turn = 1;
        private bool normalSummonUsed;
        private string message = "";
        private float messageUntil;

        private GUIStyle labelStyle;
        private GUIStyle centeredStyle;
        private GUIStyle buttonStyle;
        private GUIStyle panelStyle;

        private void Start()
        {
            SetupCamera();
            board = Board.Build(transform);

            handRoot = new GameObject("Hand").transform;
            handRoot.SetParent(cam.transform, false);
            handRoot.localPosition = handOffset;

            deck.AddRange(SampleDeck.Build());
            Shuffle(deck);
            board.SetDeckCount(Player, deck.Count);
            board.SetDeckCount(1, OpponentDeckSize);

            if (Application.isMobilePlatform)
            {
                Screen.autorotateToPortrait = false;
                Screen.autorotateToPortraitUpsideDown = false;
                Screen.orientation = ScreenOrientation.AutoRotation;
            }

            StartCoroutine(DrawOpeningHand());
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

        private IEnumerator DrawOpeningHand()
        {
            yield return new WaitForSeconds(0.4f);
            for (int i = 0; i < startingHandSize; i++)
            {
                DrawCard();
                yield return new WaitForSeconds(0.25f);
            }
            state = State.Idle;
            ShowMessage("¡Duelo! Toca una carta de tu mano para jugarla.");
        }

        // ---------------------------------------------------------------- Input

        private void Update()
        {
            if (state == State.Busy) return;

            var pointer = Pointer.current;
            if (pointer == null) return;

            Vector2 screenPosition = pointer.position.ReadValue();
            bool overGui = IsOverGui(screenPosition);

            UpdateHover(pointer, screenPosition, overGui);

            if (pointer.press.wasPressedThisFrame && !overGui) HandleTap(screenPosition);
        }

        private void UpdateHover(Pointer pointer, Vector2 screenPosition, bool overGui)
        {
            CardView target = null;
            if (!overGui && pointer is Mouse)
            {
                Raycast(screenPosition, out var card, out _);
                if (card != null && card.Location == CardLocation.Hand) target = card;
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

            switch (state)
            {
                case State.ChoosingZone:
                    if (zone != null && highlighted.Contains(zone)) PlaySelectedTo(zone);
                    else if (card != null && card.Location == CardLocation.Hand) SelectCard(card);
                    else ShowMessage("Toca una de las zonas iluminadas.");
                    return;

                case State.ChoosingTributes:
                    var monster = card != null && card.Location == CardLocation.Field ? card : zone != null ? zone.Card : null;
                    if (monster != null && monster.Zone != null && monster.Zone.Type == ZoneType.Monster && monster.Zone.Owner == Player)
                        ToggleTribute(monster);
                    else
                        ShowMessage($"Elige {tributesNeeded} de tus monstruos para sacrificar.");
                    return;
            }

            if (card != null)
            {
                switch (card.Location)
                {
                    case CardLocation.Hand:
                    case CardLocation.Field:
                        if (card == selected) Deselect();
                        else SelectCard(card);
                        return;
                    case CardLocation.Graveyard:
                        ShowMessage($"Cementerio: {graveyard.Count} carta(s). Última: {graveyard[graveyard.Count - 1].Data.displayName}");
                        return;
                }
            }

            if (zone != null && zone == board.DeckZones[Player])
            {
                Deselect();
                if (DrawCard() != null) ShowMessage("Robas 1 carta.");
                return;
            }

            Deselect();
        }

        // ---------------------------------------------------------------- Mano

        private CardView DrawCard()
        {
            if (deck.Count == 0)
            {
                ShowMessage("¡No quedan cartas en el mazo!");
                return null;
            }

            var data = deck[deck.Count - 1];
            deck.RemoveAt(deck.Count - 1);

            var card = CardFactory.Create(data, board.transform);
            card.transform.localPosition = board.DeckTopPosition(Player);
            card.transform.localRotation = Board.CardRotation(false, false);
            board.SetDeckCount(Player, deck.Count);

            card.transform.SetParent(handRoot, true);
            card.Location = CardLocation.Hand;
            hand.Add(card);
            LayoutHand();
            return card;
        }

        /// <summary>Coloca las cartas de la mano en abanico delante de la cámara.</summary>
        private void LayoutHand()
        {
            int count = hand.Count;
            float spacing = Mathf.Min(handCardSpacing, handMaxWidth / Mathf.Max(count, 1));
            for (int i = 0; i < count; i++)
            {
                float offset = i - (count - 1) * 0.5f;
                var position = new Vector3(offset * spacing, -offset * offset * 0.02f, -i * 0.01f);
                var rotation = Quaternion.Euler(0f, 0f, -offset * 3f) * HandFacing;
                hand[i].SetHome(position, rotation, 0.4f);
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
            state = State.Idle;
        }

        private void ClearSelection()
        {
            if (selected != null && selected.Location == CardLocation.Hand) selected.Lift(selected == hovered ? 0.5f : 0f);
            selected = null;
            playMode = PlayMode.None;
            tributes.Clear();
            ClearHighlights();
        }

        private void CancelPlay()
        {
            playMode = PlayMode.None;
            tributes.Clear();
            ClearHighlights();
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

        private List<CardView> OwnMonsters()
        {
            var monsters = new List<CardView>();
            foreach (var zone in board.MonsterZones[Player])
                if (!zone.IsFree) monsters.Add(zone.Card);
            return monsters;
        }

        private void BeginPlay(PlayMode mode)
        {
            playMode = mode;
            ClearHighlights();
            var data = selected.Data;

            if (data.cardType == CardType.Monster)
            {
                tributesNeeded = TributesFor(data.level);
                if (tributesNeeded > 0)
                {
                    var monsters = OwnMonsters();
                    if (monsters.Count < tributesNeeded)
                    {
                        ShowMessage($"{data.displayName} necesita {tributesNeeded} tributo(s) y no tienes suficientes monstruos.");
                        playMode = PlayMode.None;
                        return;
                    }

                    tributes.Clear();
                    foreach (var monster in monsters) Highlight(monster.Zone, TributeCandidate);
                    state = State.ChoosingTributes;
                    ShowMessage($"Elige {tributesNeeded} monstruo(s) para sacrificar.");
                    return;
                }
            }

            ChooseZone();
        }

        private void ChooseZone()
        {
            var row = selected.Data.cardType == CardType.Monster ? board.MonsterZones[Player] : board.SpellTrapZones[Player];
            foreach (var zone in row)
                if (zone.IsFree) Highlight(zone, ZoneHighlight);

            if (highlighted.Count == 0)
            {
                ShowMessage("No hay zonas libres.");
                playMode = PlayMode.None;
                state = State.CardSelected;
                return;
            }

            state = State.ChoosingZone;
            ShowMessage("Toca una zona iluminada para colocar la carta.");
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
            ShowMessage($"Sacrificas {sacrificed.Count} monstruo(s).");
            ChooseZone();
        }

        private void PlaySelectedTo(BoardZone zone)
        {
            var card = selected;
            var mode = playMode;
            ClearHighlights();
            selected = null;
            playMode = PlayMode.None;
            state = State.Idle;

            hand.Remove(card);
            if (hovered == card) hovered = null;

            bool faceUp = mode == PlayMode.SummonAttack || mode == PlayMode.ActivateSpell;
            bool defense = mode == PlayMode.SetMonster;

            card.transform.SetParent(board.transform, true);
            card.Location = CardLocation.Field;
            card.Zone = zone;
            zone.Card = card;
            card.FaceUp = faceUp;
            card.DefensePosition = defense;
            card.TurnPlaced = turn;
            card.PositionChangedTurn = -1;

            var move = card.MoveTo(board.CardPosition(zone), Board.CardRotation(faceUp, defense), 0.45f);
            LayoutHand();

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
            card.MoveTo(board.CardPosition(card.Zone), Board.CardRotation(card.FaceUp, card.DefensePosition), 0.35f);
            Deselect();
        }

        private void ActivateSet(CardView card)
        {
            card.FaceUp = true;
            Deselect();
            ShowMessage($"Activas {card.Data.displayName}.");
            card.MoveTo(board.CardPosition(card.Zone), Board.CardRotation(true, false), 0.35f)
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
            if (hand.Remove(card)) LayoutHand();

            card.transform.SetParent(board.transform, true);
            card.Location = CardLocation.Graveyard;
            card.FaceUp = true;
            card.DefensePosition = false;
            graveyard.Add(card);
            card.MoveTo(board.GraveyardPosition(Player, graveyard.Count - 1), Board.CardRotation(true, false), 0.45f);
        }

        private void NextTurn()
        {
            Deselect();
            turn++;
            normalSummonUsed = false;
            ShowMessage($"Turno {turn}: robas una carta.");
            DrawCard();
        }

        private void Restart()
        {
            DOTween.KillAll();
            SceneManager.LoadScene(SceneManager.GetActiveScene().name);
        }

        private void ShowMessage(string text, float seconds = 4f)
        {
            message = text;
            messageUntil = Time.time + seconds;
        }

        // ------------------------------------------------------------------ HUD

        private List<(string label, bool enabled, Action run)> CurrentActions()
        {
            var actions = new List<(string, bool, Action)>();
            if (selected == null) return actions;

            if (state == State.ChoosingZone || state == State.ChoosingTributes)
            {
                actions.Add(("Cancelar", true, CancelPlay));
                return actions;
            }

            var card = selected;
            var data = card.Data;

            if (card.Location == CardLocation.Hand)
            {
                switch (data.cardType)
                {
                    case CardType.Monster:
                        int needed = TributesFor(data.level);
                        string suffix = needed > 0 ? $" ({needed} tributo{(needed > 1 ? "s" : "")})" : "";
                        actions.Add(("Invocar en ataque" + suffix, !normalSummonUsed, () => BeginPlay(PlayMode.SummonAttack)));
                        actions.Add(("Colocar en defensa" + suffix, !normalSummonUsed, () => BeginPlay(PlayMode.SetMonster)));
                        break;
                    case CardType.Spell:
                        actions.Add(("Activar", true, () => BeginPlay(PlayMode.ActivateSpell)));
                        actions.Add(("Colocar boca abajo", true, () => BeginPlay(PlayMode.SetSpellTrap)));
                        break;
                    case CardType.Trap:
                        actions.Add(("Colocar boca abajo", true, () => BeginPlay(PlayMode.SetSpellTrap)));
                        break;
                }
            }
            else if (card.Location == CardLocation.Field)
            {
                if (data.cardType == CardType.Monster)
                {
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

        private bool CanChangePosition(CardView card) => card.TurnPlaced < turn && card.PositionChangedTurn < turn;

        private bool CanActivateSet(CardView card) => card.Data.cardType == CardType.Spell || card.TurnPlaced < turn;

        private string SelectedHint()
        {
            if (selected == null) return null;
            var data = selected.Data;
            if (selected.Location == CardLocation.Hand && data.cardType == CardType.Monster && normalSummonUsed)
                return "Ya hiciste tu Invocación Normal este turno.";
            if (selected.Location == CardLocation.Field && data.cardType == CardType.Monster && !CanChangePosition(selected))
                return "No puede cambiar de posición este turno.";
            if (selected.Location == CardLocation.Field && data.cardType == CardType.Trap && !selected.FaceUp && !CanActivateSet(selected))
                return "Las trampas no se activan el turno en que se colocan.";
            return null;
        }

        private static string CardInfo(CardData data)
        {
            string stats = data.cardType == CardType.Monster ? $"\nATK {data.attack}  /  DEF {data.defense}" : "";
            return $"<b><size=22>{data.displayName}</size></b>\n<color=#f0c060>{CardFactory.TypeLine(data)}</color>{stats}\n\n{data.description}";
        }

        private void OnGUI()
        {
            EnsureStyles();
            float scale = Screen.height / 720f;
            GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(scale, scale, 1f));
            float width = Screen.width / scale;
            if (Event.current.type == EventType.Layout) guiRects.Clear();

            // Estado del duelo
            Panel(new Rect(12, 12, 300, 170));
            GUI.Label(new Rect(26, 20, 276, 160),
                $"<b><size=24>Turno {turn}</size></b>\n" +
                $"Mazo: {deck.Count}    Mano: {hand.Count}\n" +
                $"Cementerio: {graveyard.Count}\n" +
                $"Invocación Normal: {(normalSummonUsed ? "<color=#ff8080>usada</color>" : "<color=#80ff80>disponible</color>")}\n" +
                "<size=15><color=#bbbbbb>Toca tu mazo para robar.\nToca una carta para ver sus acciones.</color></size>",
                labelStyle);

            if (Button(new Rect(12, 192, 300, 52), "Siguiente turno", state != State.Busy)) NextTurn();
            if (Button(new Rect(12, 252, 300, 52), "Reiniciar", true)) Restart();

            // Mensajes
            if (Time.time < messageUntil)
            {
                var messageRect = new Rect(width * 0.5f - 300, 12, 600, 50);
                Panel(messageRect);
                GUI.Label(messageRect, message, centeredStyle);
            }

            // Carta seleccionada y acciones
            if (selected == null) return;

            float x = width - 342;
            string hint = SelectedHint();
            Panel(new Rect(x, 12, 330, 210));
            GUI.Label(new Rect(x + 14, 20, 302, 196),
                CardInfo(selected.Data) + (hint != null ? $"\n\n<color=#ffb060>{hint}</color>" : ""),
                labelStyle);

            float y = 232;
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

        private bool Button(Rect rect, string label, bool enabled)
        {
            guiRects.Add(rect);
            bool previous = GUI.enabled;
            GUI.enabled = enabled;
            bool clicked = GUI.Button(rect, label, buttonStyle);
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
            buttonStyle = new GUIStyle(GUI.skin.button) { fontSize = 20 };

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
