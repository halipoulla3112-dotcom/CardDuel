using System;
using System.Collections.Generic;
using CardDuel.Cards;
using DG.Tweening;
using UnityEngine;
using UnityEngine.InputSystem;

namespace CardDuel.Prototype
{
    /// <summary>
    /// Menú principal sobre la granja: "Duelo" empieza una partida y "Barajas"
    /// muestra tu mazo; al abrirlo aparecen todas sus cartas en una cuadrícula
    /// (con ×N en las repetidas) que se puede desplazar y ver en grande.
    /// </summary>
    public class MainMenu : MonoBehaviour
    {
        private enum Screen { Home, Decks, DeckCards, Settings }

        private const string DeckName = "Mazo de prueba";
        private const float GridDistance = 3.2f;
        private const float GridScale = 0.55f;
        private const int GridColumns = 6;
        private const float GridSpacingX = 0.62f;
        private const float GridSpacingY = 0.82f;
        private const float GridTopY = 0.78f;
        private const float InspectDistance = 2.2f;
        private const float OrbitRadius = 13f;
        private const float OrbitHeight = 6.5f;
        private const float OrbitSpeed = 5f;

        private static readonly Quaternion FacingCamera = Quaternion.Euler(-90f, 0f, 0f);
        // Boca abajo mirando a la cámara: la cara hacia el fondo y el dorso derecho.
        private static readonly Quaternion BackToCamera = Quaternion.Euler(90f, 0f, 0f);
        private static readonly Vector3 DeckViewPosition = new Vector3(0f, 5.5f, -8.5f);
        private static readonly Vector3 DeckViewLookAt = new Vector3(0f, 0.3f, 0f);

        private Camera cam;
        private AudioSource audioSource;
        private Screen screen = Screen.Home;
        private float orbitAngle = 200f;

        private List<CardData> deck;
        private Transform deckStack;
        private Transform grid;
        private readonly List<CardView> gridCards = new List<CardView>();
        private GameObject backdrop;
        private GameObject inspectBackdrop;
        private CardView hovered;
        private CardView inspected;
        private float scroll;
        private float maxScroll;
        private bool pressing;
        private Vector2 pressPosition;
        private float pressScroll;
        private bool dragged;

        private GUIStyle titleStyle;
        private GUIStyle subtitleStyle;
        private GUIStyle buttonStyle;
        private GUIStyle headerStyle;
        private GUIStyle closeStyle;

        private void Start()
        {
            cam = Camera.main;
            if (cam == null) cam = new GameObject("Main Camera") { tag = "MainCamera" }.AddComponent<Camera>();
            cam.fieldOfView = 50f;
            cam.nearClipPlane = 0.1f;
            if (FindAnyObjectByType<AudioListener>() == null) cam.gameObject.AddComponent<AudioListener>();
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
            Time.timeScale = 1f;
            GameSettings.Apply();

            // Mismo escenario que el duelo, con la mesa vacía en el centro.
            Board.Build(transform);
            FarmEnvironment.Build(transform);
            BoardDecor.Build(transform);
            AestheticFx.Setup(cam, transform);

            deck = SampleDeck.Build();
        }

        // ------------------------------------------------------------- Bucle

        private void Update()
        {
            if (screen == Screen.Home || screen == Screen.Settings) Orbit();
            audioSource.volume = GameSettings.EffectsVolume;
            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame) Back();
            if (screen == Screen.DeckCards && inspected == null) HandleGridInput();
            else if (screen == Screen.Decks) HandleDeckInput();
        }

        /// <summary>La cámara gira despacio alrededor de la mesa.</summary>
        private void Orbit()
        {
            orbitAngle += OrbitSpeed * Time.deltaTime;
            float a = orbitAngle * Mathf.Deg2Rad;
            cam.transform.position = new Vector3(Mathf.Sin(a) * OrbitRadius, OrbitHeight, Mathf.Cos(a) * OrbitRadius);
            cam.transform.LookAt(new Vector3(0f, 0.3f, 0f));
        }

        private void Back()
        {
            if (inspected != null) CloseInspect();
            else if (screen == Screen.DeckCards) ShowDecks();
            else if (screen == Screen.Decks || screen == Screen.Settings) ShowHome();
        }

        // -------------------------------------------------------- Pantallas

        private void ShowHome()
        {
            ClearDeckObjects();
            screen = Screen.Home;
        }

        /// <summary>Tu mazo como un montón de cartas boca abajo delante de la cámara.</summary>
        private void ShowDecks()
        {
            ClearDeckObjects();
            screen = Screen.Decks;
            MoveCameraToDeckView();

            deckStack = new GameObject("DeckStack").transform;
            deckStack.SetParent(cam.transform, false);
            deckStack.localPosition = new Vector3(0f, 0.05f, GridDistance);
            for (int i = 0; i < 8; i++)
            {
                var card = CardFactory.Create(deck[i % deck.Count], deckStack);
                card.transform.localPosition = new Vector3(i * 0.012f, i * 0.012f, -i * 0.01f);
                card.transform.localRotation = BackToCamera;
            }
            deckStack.localScale = Vector3.one * 0.9f;
            deckStack.DOLocalMoveY(0.12f, 1.4f).SetLoops(-1, LoopType.Yoyo).SetEase(Ease.InOutSine);
        }

        /// <summary>Todas las cartas del mazo en una cuadrícula (una por carta distinta, con ×N).</summary>
        private void ShowDeckCards()
        {
            ClearDeckObjects();
            screen = Screen.DeckCards;
            scroll = 0f;
            backdrop = Backdrop("Backdrop", GridDistance + 0.8f, new Color(0.04f, 0.02f, 0.07f, 0.8f));

            grid = new GameObject("Grid").transform;
            grid.SetParent(cam.transform, false);
            grid.localPosition = new Vector3(0f, 0f, GridDistance);

            var unique = new List<(CardData data, int copies)>();
            foreach (var data in deck)
            {
                int index = unique.FindIndex(u => u.data.displayName == data.displayName);
                if (index >= 0) unique[index] = (unique[index].data, unique[index].copies + 1);
                else unique.Add((data, 1));
            }

            for (int i = 0; i < unique.Count; i++)
            {
                var card = CardFactory.Create(unique[i].data, grid);
                card.transform.localScale = Vector3.zero;
                card.SetHome(GridSlot(i), FacingCamera, 0f);
                card.transform.DOScale(GridScale, 0.3f).SetDelay(i * 0.03f).SetEase(Ease.OutBack);
                if (unique[i].copies > 1) CopiesBadge(card.transform, unique[i].copies);
                gridCards.Add(card);
            }

            int rows = Mathf.CeilToInt(unique.Count / (float)GridColumns);
            float visible = 2f * GridDistance * Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad);
            maxScroll = Mathf.Max(0f, rows * GridSpacingY - (visible - 0.6f));
        }

        private static Vector3 GridSlot(int index)
        {
            int column = index % GridColumns, row = index / GridColumns;
            return new Vector3((column - (GridColumns - 1) * 0.5f) * GridSpacingX, GridTopY - row * GridSpacingY, 0f);
        }

        /// <summary>Insignia "×N" en la esquina inferior derecha de la carta.</summary>
        private static void CopiesBadge(Transform card, int copies)
        {
            var face = card.Find("Face");
            if (face == null) return;
            var badge = Primitives.Quad("Badge", face, new Vector2(0.27f, -0.43f), new Vector2(0.2f, 0.2f), 0.004f,
                Primitives.Transparent(Primitives.CircleTexture, new Color(0.35f, 0.12f, 0.55f)));
            badge.name = "CopiesBadge";
            Primitives.Text("Copies", face, $"×{copies}", new Vector2(0.27f, -0.43f), new Vector2(0.14f, 0.1f), 0.005f, Color.white);
        }

        private void MoveCameraToDeckView()
        {
            cam.transform.DOKill();
            var rotation = Quaternion.LookRotation(DeckViewLookAt - DeckViewPosition, Vector3.up);
            cam.transform.DOMove(DeckViewPosition, 0.6f).SetEase(Ease.InOutCubic);
            cam.transform.DORotateQuaternion(rotation, 0.6f).SetEase(Ease.InOutCubic);
        }

        private void ClearDeckObjects()
        {
            if (inspected != null) CloseInspect();
            if (deckStack != null) { deckStack.DOKill(); Destroy(deckStack.gameObject); }
            if (grid != null) Destroy(grid.gameObject);
            if (backdrop != null) Destroy(backdrop);
            gridCards.Clear();
            hovered = null;
        }

        /// <summary>Plano oscuro translúcido delante de la cámara, detrás de las cartas.</summary>
        private GameObject Backdrop(string name, float distance, Color color, bool opaque = false)
        {
            var go = new GameObject(name);
            go.transform.SetParent(cam.transform, false);
            go.transform.localPosition = new Vector3(0f, 0f, distance);
            go.transform.localScale = new Vector3(40f, 40f, 1f);
            go.AddComponent<MeshFilter>().sharedMesh = Primitives.QuadMesh;
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = opaque ? Primitives.UnlitColor(color) : Primitives.UnlitTransparent(Texture2D.whiteTexture, color);
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return go;
        }

        // ------------------------------------------------------------ Entrada

        private void HandleDeckInput()
        {
            var pointer = Pointer.current;
            if (pointer == null || !pointer.press.wasPressedThisFrame || IsOverGui(pointer.position.ReadValue())) return;
            if (RaycastCard(pointer.position.ReadValue()) != null) OpenDeck();
        }

        /// <summary>Desplazar con la rueda o arrastrando; tocar una carta sin arrastrar la abre en grande.</summary>
        private void HandleGridInput()
        {
            var pointer = Pointer.current;
            if (pointer == null) return;
            Vector2 position = pointer.position.ReadValue();

            if (Mouse.current != null)
            {
                float wheel = Mouse.current.scroll.ReadValue().y;
                if (Mathf.Abs(wheel) > 0.01f) SetScroll(scroll - Mathf.Sign(wheel) * 0.25f);
            }

            if (pointer.press.wasPressedThisFrame && !IsOverGui(position))
            {
                pressing = true;
                dragged = false;
                pressPosition = position;
                pressScroll = scroll;
            }
            else if (pressing && pointer.press.isPressed)
            {
                float dy = (position.y - pressPosition.y) / UnityEngine.Screen.height;
                if (Mathf.Abs(dy) > 0.01f) dragged = true;
                if (dragged) SetScroll(pressScroll + dy * 3f);
            }
            else if (pressing)
            {
                pressing = false;
                if (!dragged)
                {
                    var card = RaycastCard(position);
                    if (card != null && gridCards.Contains(card)) OpenInspect(card);
                }
            }

            UpdateHover(position);
        }

        private void SetScroll(float value)
        {
            scroll = Mathf.Clamp(value, 0f, maxScroll);
            if (grid != null) grid.localPosition = new Vector3(0f, scroll, GridDistance);
        }

        private void UpdateHover(Vector2 position)
        {
            var target = Pointer.current is Mouse && !pressing ? RaycastCard(position) : null;
            if (target != null && !gridCards.Contains(target)) target = null;
            if (target == hovered) return;
            if (hovered != null) hovered.transform.DOScale(GridScale, 0.12f);
            hovered = target;
            if (hovered != null) hovered.transform.DOScale(GridScale * 1.1f, 0.12f);
        }

        private CardView RaycastCard(Vector2 screenPosition)
        {
            var hits = Physics.RaycastAll(cam.ScreenPointToRay(screenPosition), 50f);
            Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
            foreach (var hit in hits)
            {
                var card = hit.collider.GetComponentInParent<CardView>();
                if (card != null) return card;
            }
            return null;
        }

        // -------------------------------------------------- Carta en grande

        private void OpenDeck()
        {
            audioSource.PlayOneShot(SoundFx.Emote, 0.6f);
            ShowDeckCards();
        }

        private void OpenInspect(CardView card)
        {
            inspected = card;
            hovered = null;
            if (inspectBackdrop == null) inspectBackdrop = Backdrop("InspectBackdrop", InspectDistance + 0.5f, Color.black, opaque: true);
            inspectBackdrop.SetActive(true);
            SetBadgeVisible(card, false);

            float visibleHeight = 2f * InspectDistance * Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad);
            float scale = 0.85f * Mathf.Min(visibleHeight / CardFactory.Height, visibleHeight * cam.aspect / CardFactory.Width);

            card.transform.SetParent(cam.transform, true);
            card.MoveTo(new Vector3(0f, 0f, InspectDistance), FacingCamera, 0.3f);
            card.transform.DOScale(scale, 0.3f).SetEase(Ease.OutCubic);
        }

        private void CloseInspect()
        {
            var card = inspected;
            inspected = null;
            if (inspectBackdrop != null) inspectBackdrop.SetActive(false);
            if (card == null || grid == null) return;

            card.transform.SetParent(grid, true);
            card.MoveTo(GridSlot(gridCards.IndexOf(card)), FacingCamera, 0.3f);
            card.transform.DOScale(GridScale, 0.3f).SetEase(Ease.OutCubic);
            SetBadgeVisible(card, true);
        }

        /// <summary>La insignia ×N tapa el ATK/DEF en grande, así que se oculta al inspeccionar.</summary>
        private static void SetBadgeVisible(CardView card, bool visible)
        {
            var face = card.transform.Find("Face");
            if (face == null) return;
            foreach (Transform child in face)
                if (child.name == "CopiesBadge" || child.name == "Copies") child.gameObject.SetActive(visible);
        }

        // --------------------------------------------------------------- GUI

        private readonly List<Rect> guiRects = new List<Rect>();

        private void OnGUI()
        {
            EnsureStyles();
            float scale = UnityEngine.Screen.height / 720f;
            GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(scale, scale, 1f));
            float width = UnityEngine.Screen.width / scale;
            if (Event.current.type == EventType.Layout) guiRects.Clear();

            switch (screen)
            {
                case Screen.Home: DrawHome(width); break;
                case Screen.Decks: DrawDecks(width); break;
                case Screen.DeckCards: DrawDeckCards(width); break;
                case Screen.Settings: DrawSettings(width); break;
            }
        }

        private void DrawHome(float width)
        {
            ShadowLabel(new Rect(0f, 90f, width, 120f), "CARD DUEL", titleStyle);
            ShadowLabel(new Rect(0f, 195f, width, 40f), "Duelo de cartas en la granja", subtitleStyle);

            float x = width * 0.5f - 160f;
            if (Button(new Rect(x, 300f, 320f, 66f), "Duelo")) PrototypeBootstrap.LoadDuel();
            if (Button(new Rect(x, 380f, 320f, 66f), "Barajas")) ShowDecks();
            if (Button(new Rect(x, 460f, 320f, 66f), "Ajustes")) screen = Screen.Settings;
            if (Button(new Rect(x, 540f, 320f, 66f), "Salir")) Quit();
        }

        /// <summary>Volumen general y de efectos, guardados entre partidas.</summary>
        private void DrawSettings(float width)
        {
            ShadowLabel(new Rect(0f, 40f, width, 70f), "Ajustes", headerStyle);
            var panel = new Rect(width * 0.5f - 230f, 150f, 460f, 330f);
            guiRects.Add(panel);
            GUI.Box(panel, GUIContent.none, UiKit.Panel);
            UiKit.VolumeControls(new Rect(panel.x + 50f, panel.y + 60f, panel.width - 100f, 160f));
            if (Button(new Rect(width * 0.5f - 140f, 520f, 280f, 60f), "← Volver")) screen = Screen.Home;
        }

        private void DrawDecks(float width)
        {
            ShadowLabel(new Rect(0f, 40f, width, 70f), "Barajas", headerStyle);
            ShadowLabel(new Rect(0f, 560f, width, 40f), $"{DeckName}  ·  {deck.Count} cartas", subtitleStyle);

            if (Button(new Rect(width * 0.5f - 140f, 610f, 280f, 60f), "Abrir mazo")) OpenDeck();
            if (Button(new Rect(24f, 24f, 180f, 56f), "← Volver")) ShowHome();
        }

        private void DrawDeckCards(float width)
        {
            if (inspected != null)
            {
                if (Button(new Rect(width - 84f, 16f, 68f, 68f), "X", closeStyle)) CloseInspect();
                return;
            }

            ShadowLabel(new Rect(0f, 22f, width, 60f), $"{DeckName}  ·  {deck.Count} cartas", headerStyle);
            if (Button(new Rect(24f, 24f, 180f, 56f), "← Volver")) ShowDecks();
            if (maxScroll > 0.2f)
                GUI.Label(new Rect(0f, 690f, width, 26f), "Rueda del ratón o arrastra para ver más · Toca una carta para verla en grande", subtitleStyle);
        }

        private bool Button(Rect rect, string label, GUIStyle style = null)
        {
            guiRects.Add(rect);
            bool clicked = GUI.Button(rect, label, style ?? buttonStyle);
            if (clicked) audioSource.PlayOneShot(SoundFx.Emote, 0.4f);
            return clicked;
        }

        private static void ShadowLabel(Rect rect, string text, GUIStyle style)
        {
            var previous = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, 0.6f);
            GUI.Label(new Rect(rect.x + 4f, rect.y + 4f, rect.width, rect.height), text, style);
            GUI.color = previous;
            GUI.Label(rect, text, style);
        }

        private bool IsOverGui(Vector2 screenPosition)
        {
            float scale = UnityEngine.Screen.height / 720f;
            var guiPosition = new Vector2(screenPosition.x, UnityEngine.Screen.height - screenPosition.y) / scale;
            foreach (var rect in guiRects)
                if (rect.Contains(guiPosition)) return true;
            return false;
        }

        private static void Quit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        private void EnsureStyles()
        {
            if (titleStyle != null) return;

            titleStyle = new GUIStyle(GUI.skin.label) { fontSize = 96, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            titleStyle.normal.textColor = new Color(1f, 0.9f, 0.55f);
            headerStyle = new GUIStyle(titleStyle) { fontSize = 44 };
            headerStyle.normal.textColor = Color.white;
            subtitleStyle = new GUIStyle(GUI.skin.label) { fontSize = 24, fontStyle = FontStyle.BoldAndItalic, alignment = TextAnchor.MiddleCenter };
            subtitleStyle.normal.textColor = Color.white;
            buttonStyle = UiKit.PurpleButton(30);
            closeStyle = UiKit.PurpleButton(36);
        }
    }
}
