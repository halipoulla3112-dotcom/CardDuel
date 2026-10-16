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

        // Colores de las zonas de cristal (algo por encima de 1 para que el borde brille).
        private static readonly Color MonsterZoneColor = new Color(1.25f, 0.95f, 0.5f);
        private static readonly Color SpellTrapZoneColor = new Color(0.55f, 1.15f, 1.2f);
        private static readonly Color SideZoneColor = new Color(1f, 0.85f, 1.25f);
        private static readonly Color GoldTrim = new Color(0.95f, 0.75f, 0.35f);
        private static readonly Color MidlineGlow = new Color(2.2f, 1.7f, 0.8f);
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

        /// <summary>Igual que <see cref="CardRotation(bool, bool)"/>, girada hacia su dueño.</summary>
        public static Quaternion CardRotation(int owner, bool faceUp, bool defense)
        {
            return Quaternion.Euler(0f, owner == 0 ? 0f : 180f, 0f) * CardRotation(faceUp, defense);
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
            Primitives.Box("Table", transform, new Vector3(0f, -0.1f, 0f), new Vector3(10.6f, 0.2f, 6.8f),
                EnvironmentTextures.Material(EnvironmentTextures.Wood, new Vector2(5f, 2f), new Color(0.85f, 0.75f, 0.65f)));

            // Tu mitad es un prado de césped; la del rival, un patio de ladrillo.
            Primitives.Box("PlayerHalf", transform, new Vector3(0f, 0f, -1.65f), new Vector3(10.3f, 0.01f, 3.2f),
                EnvironmentTextures.Material(EnvironmentTextures.Grass, new Vector2(5f, 1.6f), Color.white));
            Primitives.Box("OpponentHalf", transform, new Vector3(0f, 0f, 1.65f), new Vector3(10.3f, 0.01f, 3.2f),
                EnvironmentTextures.Material(EnvironmentTextures.RedBricks, new Vector2(5f, 3.2f), Color.white));

            BuildFrame();
        }

        /// <summary>Marco de madera oscura con moldura dorada, adornos en las esquinas y línea central luminosa.</summary>
        private void BuildFrame()
        {
            const float halfX = 5.2f, halfZ = 3.27f, rail = 0.16f, height = 0.07f;
            var wood = EnvironmentTextures.Material(EnvironmentTextures.Wood, new Vector2(6f, 0.3f), new Color(0.55f, 0.4f, 0.32f));
            var gold = Primitives.NewLit(GoldTrim);
            gold.SetFloat("_Metallic", 0.85f);
            gold.SetFloat("_Smoothness", 0.7f);

            foreach (float z in new[] { -halfZ, halfZ })
            {
                Primitives.Box("FrameRail", transform, new Vector3(0f, height * 0.5f, z), new Vector3(halfX * 2f + rail, height, rail), wood);
                Primitives.Box("FrameGold", transform, new Vector3(0f, height + 0.006f, z), new Vector3(halfX * 2f + rail, 0.012f, 0.05f), gold);
            }
            foreach (float x in new[] { -halfX, halfX })
            {
                Primitives.Box("FrameRail", transform, new Vector3(x, height * 0.5f, 0f), new Vector3(rail, height, halfZ * 2f + rail), wood);
                Primitives.Box("FrameGold", transform, new Vector3(x, height + 0.006f, 0f), new Vector3(0.05f, 0.012f, halfZ * 2f + rail), gold);
            }
            foreach (var corner in new[] { new Vector2(-halfX, -halfZ), new Vector2(halfX, -halfZ), new Vector2(-halfX, halfZ), new Vector2(halfX, halfZ) })
                Primitives.Box("FrameCorner", transform, new Vector3(corner.x, height + 0.03f, corner.y), new Vector3(0.24f, 0.08f, 0.24f), gold)
                    .localRotation = Quaternion.Euler(0f, 45f, 0f);

            // Línea dorada que brilla entre los dos campos, con un rombo en el centro.
            var glow = Primitives.UnlitColor(MidlineGlow);
            Primitives.Box("MidLine", transform, new Vector3(0f, 0.01f, 0f), new Vector3(halfX * 2f, 0.006f, 0.045f), glow);
            Primitives.Box("MidEmblem", transform, new Vector3(0f, 0.016f, 0f), new Vector3(0.36f, 0.012f, 0.36f), glow)
                .localRotation = Quaternion.Euler(0f, 45f, 0f);
            Primitives.Box("MidEmblemCore", transform, new Vector3(0f, 0.02f, 0f), new Vector3(0.2f, 0.012f, 0.2f), gold)
                .localRotation = Quaternion.Euler(0f, 45f, 0f);
        }

        private static Texture2D zoneTexture;

        /// <summary>Rectángulo redondeado: relleno tenue, brillo interior junto al borde y borde nítido.</summary>
        private static Texture2D ZoneTexture
        {
            get
            {
                if (zoneTexture != null) return zoneTexture;
                const int size = 128;
                const float radius = 18f, border = 4f, glow = 16f;
                zoneTexture = new Texture2D(size, size, TextureFormat.RGBA32, true) { wrapMode = TextureWrapMode.Clamp, anisoLevel = 4 };
                for (int y = 0; y < size; y++)
                    for (int x = 0; x < size; x++)
                    {
                        // Distancia con signo al borde del rectángulo redondeado (negativa dentro).
                        float qx = Mathf.Abs(x + 0.5f - size * 0.5f) - (size * 0.5f - radius);
                        float qy = Mathf.Abs(y + 0.5f - size * 0.5f) - (size * 0.5f - radius);
                        float outside = new Vector2(Mathf.Max(qx, 0f), Mathf.Max(qy, 0f)).magnitude + Mathf.Min(Mathf.Max(qx, qy), 0f) - radius;

                        float inside = -outside;
                        float alpha = 0.22f;
                        if (inside < glow) alpha = Mathf.Lerp(0.75f, 0.22f, inside / glow);
                        if (inside < border) alpha = 1f;
                        alpha *= Mathf.Clamp01(0.5f - outside);

                        float white = inside < border ? 1f : 0.85f;
                        zoneTexture.SetPixel(x, y, new Color(white, white, white, alpha));
                    }
                zoneTexture.Apply(true);
                return zoneTexture;
            }
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

        /// <summary>
        /// Zona de cristal: panel translúcido con esquinas redondeadas y borde luminoso,
        /// que deja ver el césped o el ladrillo de debajo.
        /// </summary>
        private BoardZone CreateZone(ZoneType type, int owner, int index, Vector2 position, string label)
        {
            var color = type == ZoneType.Monster ? MonsterZoneColor : type == ZoneType.SpellTrap ? SpellTrapZoneColor : SideZoneColor;
            var material = Primitives.UnlitTransparent(ZoneTexture, color);
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
