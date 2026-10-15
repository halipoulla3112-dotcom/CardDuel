using CardDuel.Cards;
using UnityEngine;

namespace CardDuel.Prototype
{
    /// <summary>
    /// Construye la carta 3D a partir de su CardData: cuerpo, cara (marco,
    /// nombre, ilustración, ATK/DEF) y reverso. La cara mira a +Y local y la
    /// parte de arriba de la carta apunta a +Z local.
    /// </summary>
    public static class CardFactory
    {
        public const float Width = 0.7f;
        public const float Height = 1.0f;
        public const float Thickness = 0.02f;

        public static readonly Color BackColor = new Color(0.22f, 0.13f, 0.07f);
        private static readonly Color BarColor = new Color(0.94f, 0.90f, 0.80f);
        private static readonly Color InkColor = new Color(0.12f, 0.08f, 0.05f);

        public static CardView Create(CardData data, Transform parent)
        {
            var root = new GameObject(data.displayName);
            root.transform.SetParent(parent, false);
            root.AddComponent<BoxCollider>().size = new Vector3(Width, Thickness * 3f, Height);

            var view = root.AddComponent<CardView>();
            view.Init(data);

            Primitives.Box("Body", root.transform, Vector3.zero, new Vector3(Width, Thickness, Height), Primitives.Lit(BackColor));
            BuildFace(data, root.transform);
            BuildBack(root.transform);
            return view;
        }

        public static string AttributeName(CardAttribute attribute)
        {
            switch (attribute)
            {
                case CardAttribute.Light: return "LUZ";
                case CardAttribute.Dark: return "OSCURIDAD";
                case CardAttribute.Fire: return "FUEGO";
                case CardAttribute.Water: return "AGUA";
                case CardAttribute.Earth: return "TIERRA";
                case CardAttribute.Wind: return "VIENTO";
                default: return "";
            }
        }

        public static string TypeLine(CardData data)
        {
            switch (data.cardType)
            {
                case CardType.Spell: return "CARTA MÁGICA";
                case CardType.Trap: return "CARTA DE TRAMPA";
                default: return $"NIVEL {data.level} · {AttributeName(data.attribute)}";
            }
        }

        private static void BuildFace(CardData data, Transform card)
        {
            var face = Surface("Face", card, 1f);
            Primitives.Quad("Frame", face, Vector2.zero, new Vector2(Width * 0.96f, Height * 0.96f), 0f, FrameColor(data.cardType));
            Primitives.Quad("NameBar", face, new Vector2(0f, 0.4f), new Vector2(0.6f, 0.11f), 0.001f, BarColor);
            Primitives.Text("Name", face, data.displayName, new Vector2(0f, 0.4f), new Vector2(0.56f, 0.075f), 0.002f, InkColor);
            Primitives.Text("Type", face, TypeLine(data), new Vector2(0f, 0.305f), new Vector2(0.58f, 0.05f), 0.001f, InkColor);
            Primitives.Quad("Art", face, new Vector2(0f, 0.03f), new Vector2(0.6f, 0.44f), 0.001f, ArtColor(data));
            Primitives.Text("Glyph", face, data.displayName.Substring(0, 1).ToUpperInvariant(), new Vector2(0f, 0.03f), new Vector2(0.3f, 0.3f), 0.002f, new Color(1f, 1f, 1f, 0.8f));
            Primitives.Quad("StatsBar", face, new Vector2(0f, -0.33f), new Vector2(0.6f, 0.13f), 0.001f, BarColor);
            Primitives.Text("Stats", face, StatsLine(data), new Vector2(0f, -0.33f), new Vector2(0.56f, 0.065f), 0.002f, InkColor);
        }

        private static void BuildBack(Transform card)
        {
            var back = Surface("Back", card, -1f);
            Primitives.Quad("Border", back, Vector2.zero, new Vector2(Width * 0.96f, Height * 0.96f), 0f, new Color(0.45f, 0.25f, 0.10f));
            Primitives.Quad("Center", back, Vector2.zero, new Vector2(Width * 0.7f, Height * 0.75f), 0.001f, new Color(0.15f, 0.08f, 0.04f));
            Primitives.Text("Logo", back, "CARD\nDUEL", Vector2.zero, new Vector2(0.4f, 0.3f), 0.002f, new Color(0.95f, 0.75f, 0.35f));
        }

        /// <summary>Plano de dibujo pegado a la cara superior (side = 1) o inferior (side = -1).</summary>
        private static Transform Surface(string name, Transform card, float side)
        {
            var surface = new GameObject(name).transform;
            surface.SetParent(card, false);
            surface.localPosition = new Vector3(0f, side * (Thickness * 0.5f + 0.001f), 0f);
            surface.localRotation = Quaternion.Euler(side * 90f, 0f, 0f);
            return surface;
        }

        private static string StatsLine(CardData data)
        {
            switch (data.cardType)
            {
                case CardType.Spell: return "Mágica";
                case CardType.Trap: return "Trampa";
                default: return $"ATK {data.attack}   DEF {data.defense}";
            }
        }

        private static Color FrameColor(CardType type)
        {
            switch (type)
            {
                case CardType.Spell: return new Color(0.11f, 0.60f, 0.52f);
                case CardType.Trap: return new Color(0.66f, 0.19f, 0.50f);
                default: return new Color(0.80f, 0.55f, 0.25f);
            }
        }

        private static Color ArtColor(CardData data)
        {
            if (data.cardType == CardType.Spell) return new Color(0.55f, 0.85f, 0.78f);
            if (data.cardType == CardType.Trap) return new Color(0.85f, 0.55f, 0.75f);

            switch (data.attribute)
            {
                case CardAttribute.Light: return new Color(0.95f, 0.85f, 0.45f);
                case CardAttribute.Dark: return new Color(0.30f, 0.18f, 0.40f);
                case CardAttribute.Fire: return new Color(0.85f, 0.25f, 0.15f);
                case CardAttribute.Water: return new Color(0.20f, 0.45f, 0.85f);
                case CardAttribute.Earth: return new Color(0.50f, 0.38f, 0.22f);
                case CardAttribute.Wind: return new Color(0.40f, 0.75f, 0.45f);
                default: return Color.gray;
            }
        }
    }
}
