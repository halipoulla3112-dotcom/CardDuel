using UnityEngine;

namespace CardDuel.Prototype
{
    /// <summary>
    /// Tablero estilo Yu-Gi-Oh! construido por código. El jugador 0 ocupa la
    /// mitad -Z (cerca de la cámara) y el jugador 1 la mitad +Z.
    /// Por jugador: 5 zonas de monstruo, 5 de magia/trampa, campo, extra,
    /// cementerio y mazo.
    /// </summary>
    public class Board : MonoBehaviour
    {
        public const float ZoneSize = 1.15f;
        private const float ZoneTop = 0.018f;
        private const float DeckCardThickness = 0.008f;
        private const float GraveyardCardSpacing = 0.01f;
        private const float MonsterRowZ = -0.85f;
        private const float SpellTrapRowZ = -2.25f;
        private const float SideColumnX = 4.1f;
        private static readonly float[] Columns = { -2.6f, -1.3f, 0f, 1.3f, 2.6f };

        private static readonly Color MonsterZoneColor = new Color(0.32f, 0.22f, 0.12f);
        private static readonly Color SpellTrapZoneColor = new Color(0.10f, 0.26f, 0.24f);
        private static readonly Color SideZoneColor = new Color(0.18f, 0.18f, 0.22f);
        private static readonly Color BorderColor = new Color(0.75f, 0.62f, 0.30f);
        private static readonly Color LabelColor = new Color(0.85f, 0.75f, 0.45f);

        public BoardZone[][] MonsterZones { get; } = new BoardZone[2][];
        public BoardZone[][] SpellTrapZones { get; } = new BoardZone[2][];
        public BoardZone[] DeckZones { get; } = new BoardZone[2];
        public BoardZone[] GraveyardZones { get; } = new BoardZone[2];
        public BoardZone[] FieldZones { get; } = new BoardZone[2];
        public BoardZone[] ExtraZones { get; } = new BoardZone[2];

        private readonly Transform[] deckStacks = new Transform[2];
        private readonly int[] deckCounts = new int[2];

        public static Board Build(Transform parent)
        {
            var go = new GameObject("Board");
            go.transform.SetParent(parent, false);
            var board = go.AddComponent<Board>();
            board.BuildTable();
            board.BuildSide(0);
            board.BuildSide(1);
            return board;
        }

        /// <summary>Rotación de una carta tumbada sobre la mesa.</summary>
        public static Quaternion CardRotation(bool faceUp, bool defense)
        {
            var yaw = Quaternion.Euler(0f, defense ? 90f : 0f, 0f);
            return faceUp ? yaw : yaw * Quaternion.Euler(0f, 0f, 180f);
        }

        public Vector3 CardPosition(BoardZone zone)
        {
            var p = zone.transform.localPosition;
            return new Vector3(p.x, ZoneTop + CardFactory.Thickness * 0.5f + 0.002f, p.z);
        }

        public Vector3 DeckTopPosition(int owner)
        {
            var p = DeckZones[owner].transform.localPosition;
            return new Vector3(p.x, ZoneTop + deckCounts[owner] * DeckCardThickness + CardFactory.Thickness * 0.5f, p.z);
        }

        public Vector3 GraveyardPosition(int owner, int index)
        {
            var p = GraveyardZones[owner].transform.localPosition;
            return new Vector3(p.x, ZoneTop + CardFactory.Thickness * 0.5f + 0.002f + index * GraveyardCardSpacing, p.z);
        }

        /// <summary>Ajusta la altura del montón del mazo al número de cartas.</summary>
        public void SetDeckCount(int owner, int count)
        {
            deckCounts[owner] = Mathf.Max(count, 0);
            float height = deckCounts[owner] * DeckCardThickness;
            var stack = deckStacks[owner];
            var p = DeckZones[owner].transform.localPosition;
            stack.gameObject.SetActive(count > 0);
            stack.localScale = new Vector3(CardFactory.Width, Mathf.Max(height, 0.001f), CardFactory.Height);
            stack.localPosition = new Vector3(p.x, ZoneTop + height * 0.5f, p.z);
        }

        private void BuildTable()
        {
            Primitives.Box("Table", transform, new Vector3(0f, -0.1f, 0f), new Vector3(10.6f, 0.2f, 6.8f), Primitives.Lit(new Color(0.08f, 0.09f, 0.12f)));
            Primitives.Box("PlayerHalf", transform, new Vector3(0f, 0f, -1.65f), new Vector3(10.3f, 0.01f, 3.2f), Primitives.Lit(new Color(0.11f, 0.20f, 0.32f)));
            Primitives.Box("OpponentHalf", transform, new Vector3(0f, 0f, 1.65f), new Vector3(10.3f, 0.01f, 3.2f), Primitives.Lit(new Color(0.30f, 0.11f, 0.13f)));
            Primitives.Box("MidLine", transform, new Vector3(0f, 0.006f, 0f), new Vector3(10.3f, 0.004f, 0.05f), Primitives.Lit(BorderColor));
        }

        private void BuildSide(int owner)
        {
            float s = owner == 0 ? 1f : -1f;
            MonsterZones[owner] = new BoardZone[Columns.Length];
            SpellTrapZones[owner] = new BoardZone[Columns.Length];

            for (int i = 0; i < Columns.Length; i++)
            {
                MonsterZones[owner][i] = CreateZone(ZoneType.Monster, owner, i, new Vector2(Columns[i] * s, MonsterRowZ * s), null);
                SpellTrapZones[owner][i] = CreateZone(ZoneType.SpellTrap, owner, i, new Vector2(Columns[i] * s, SpellTrapRowZ * s), null);
            }

            FieldZones[owner] = CreateZone(ZoneType.Field, owner, 0, new Vector2(-SideColumnX * s, MonsterRowZ * s), "CAMPO");
            ExtraZones[owner] = CreateZone(ZoneType.Extra, owner, 0, new Vector2(-SideColumnX * s, SpellTrapRowZ * s), "EXTRA");
            GraveyardZones[owner] = CreateZone(ZoneType.Graveyard, owner, 0, new Vector2(SideColumnX * s, MonsterRowZ * s), "CEMENTERIO");
            DeckZones[owner] = CreateZone(ZoneType.Deck, owner, 0, new Vector2(SideColumnX * s, SpellTrapRowZ * s), "MAZO");

            deckStacks[owner] = Primitives.Box($"DeckStack_{owner}", transform, Vector3.zero, Vector3.one, Primitives.Lit(CardFactory.BackColor));
            SetDeckCount(owner, 0);
        }

        private BoardZone CreateZone(ZoneType type, int owner, int index, Vector2 position, string label)
        {
            Primitives.Box("Border", transform, new Vector3(position.x, 0.008f, position.y),
                new Vector3(ZoneSize + 0.08f, 0.008f, ZoneSize + 0.08f), Primitives.Lit(BorderColor));

            var color = type == ZoneType.Monster ? MonsterZoneColor : type == ZoneType.SpellTrap ? SpellTrapZoneColor : SideZoneColor;
            var material = Primitives.NewLit(color);
            var tile = Primitives.Box($"{type}Zone_{owner}_{index}", transform, new Vector3(position.x, 0.012f, position.y),
                new Vector3(ZoneSize, 0.012f, ZoneSize), material, collider: true);

            var zone = tile.gameObject.AddComponent<BoardZone>();
            zone.Init(type, owner, index, material, color);

            if (label != null)
            {
                float s = owner == 0 ? 1f : -1f;
                var labelRoot = new GameObject($"{label}_Label").transform;
                labelRoot.SetParent(transform, false);
                labelRoot.localPosition = new Vector3(position.x, 0.02f, position.y - 0.7f * s);
                labelRoot.localRotation = Quaternion.Euler(90f, owner == 0 ? 0f : 180f, 0f);
                Primitives.Text("Text", labelRoot, label, Vector2.zero, new Vector2(1.1f, 0.13f), 0f, LabelColor);
            }

            return zone;
        }
    }
}
