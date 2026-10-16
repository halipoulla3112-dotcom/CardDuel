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
        private static readonly Color InkColor = Color.black;

        public static CardView Create(CardData data, Transform parent)
        {
            var root = new GameObject(data.displayName);
            root.transform.SetParent(parent, false);
            root.AddComponent<BoxCollider>().size = new Vector3(Width, Thickness * 3f, Height);

            var view = root.AddComponent<CardView>();
            view.Init(data);

            Primitives.Box("Body", root.transform, Vector3.zero, new Vector3(Width, Thickness, Height), Primitives.Lit(BackColor));
            if (data.cardType == CardType.Monster && MonsterFrame != null) BuildMonsterFace(data, root.transform);
            else BuildFace(data, root.transform);
            CostGem(root.transform.Find("Face"), data.cost);
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
            var art = Primitives.Quad("Art", face, new Vector2(0f, 0.07f), new Vector2(0.6f, 0.34f), 0.001f, ArtColor(data));
            if (data.artwork != null)
            {
                var material = Primitives.NewLit(Color.white);
                material.SetTexture("_BaseMap", data.artwork.texture);
                art.GetComponent<MeshRenderer>().sharedMaterial = material;
            }
            else
            {
                Primitives.Text("Glyph", face, data.displayName.Substring(0, 1).ToUpperInvariant(), new Vector2(0f, 0.07f), new Vector2(0.24f, 0.24f), 0.002f, new Color(1f, 1f, 1f, 0.8f));
            }
            Primitives.Quad("DescriptionBox", face, new Vector2(0f, -0.18f), new Vector2(0.6f, 0.15f), 0.001f, BarColor);
            Primitives.Text("Description", face, data.description, new Vector2(0f, -0.18f), new Vector2(0.56f, 0.13f), 0.002f, InkColor, wrap: true);
            Primitives.Quad("StatsBar", face, new Vector2(0f, -0.34f), new Vector2(0.6f, 0.1f), 0.001f, BarColor);
            Primitives.Text("Stats", face, StatsLine(data), new Vector2(0f, -0.34f), new Vector2(0.56f, 0.06f), 0.002f, InkColor);
        }

        // -------------------------------------------------- Marco de monstruo

        // El marco es una imagen de 1038 x 1515 px; las zonas se dan en sus píxeles.
        private const float FramePixelsW = 1038f;
        private const float FramePixelsH = 1515f;
        private const float StarSpacingPx = 63f;
        private static readonly Color NameGoldTop = new Color(1f, 0.93f, 0.7f);
        private static readonly Color NameGoldBottom = new Color(0.85f, 0.62f, 0.3f);
        private static readonly Color ParchmentInk = new Color(0.08f, 0.07f, 0.16f);

        private static Texture2D monsterFrame;
        private static Texture2D levelStar;
        private static Material starMaterial;

        private static Texture2D MonsterFrame => monsterFrame != null ? monsterFrame : monsterFrame = Resources.Load<Texture2D>("CardFrames/MonsterFrame");
        private static Texture2D LevelStar => levelStar != null ? levelStar : levelStar = Resources.Load<Texture2D>("CardFrames/LevelStar");

        /// <summary>Centro de una zona del marco (en píxeles de la imagen) en coordenadas de la cara.</summary>
        private static Vector2 FramePoint(float x, float y) =>
            new Vector2((x / FramePixelsW - 0.5f) * Width, (0.5f - y / FramePixelsH) * Height);

        /// <summary>Tamaño de una zona del marco (en píxeles de la imagen) en unidades de la cara.</summary>
        private static Vector2 FrameSize(float w, float h) =>
            new Vector2(w / FramePixelsW * Width, h / FramePixelsH * Height);

        /// <summary>Texto colocado sobre un rectángulo del marco (x, y = esquina superior izquierda en píxeles).</summary>
        private static TMPro.TextMeshPro FrameText(string name, Transform face, string text, float x, float y, float w, float h,
            Color color, TMPro.TextAlignmentOptions alignment, TMPro.FontStyles style, float maxFontSize, bool wrap = false, bool autoSize = true)
        {
            var label = Primitives.Text(name, face, text, FramePoint(x + w * 0.5f, y + h * 0.5f), FrameSize(w, h), 0.002f, color, wrap);
            Primitives.SetFont(label, Primitives.SerifFont);
            label.alignment = alignment;
            label.fontStyle = style;
            label.fontSizeMax = maxFontSize;
            // Las líneas cortas (tipo, ATK/DEF) van a tamaño fijo, como en la plantilla.
            if (!autoSize)
            {
                label.enableAutoSizing = false;
                label.fontSize = maxFontSize;
            }
            return label;
        }

        /// <summary>Cara de monstruo con el marco ilustrado: nombre dorado, atributo, estrellas de nivel, arte, tipo, descripción y ATK/DEF.</summary>
        private static void BuildMonsterFace(CardData data, Transform card)
        {
            var face = Surface("Face", card, 1f);
            Primitives.Quad("Frame", face, Vector2.zero, new Vector2(Width, Height), 0f, Primitives.Textured(MonsterFrame));

            // Nombre en dorado con degradado, como en la plantilla.
            var title = FrameText("Name", face, data.displayName, 104f, 68f, 748f, 96f,
                Color.white, TMPro.TextAlignmentOptions.Left, TMPro.FontStyles.SmallCaps, 100f);
            title.enableVertexGradient = true;
            title.colorGradient = new TMPro.VertexGradient(NameGoldTop, NameGoldTop, NameGoldBottom, NameGoldBottom);

            // Orbe del atributo: círculo del color del atributo sobre el aro del marco.
            Primitives.Quad("Attribute", face, FramePoint(915f, 113f), FrameSize(90f, 90f), 0.001f,
                Primitives.Transparent(Primitives.CircleTexture, AttributeColor(data.attribute)));
            FrameText("AttributeName", face, AttributeName(data.attribute), 873f, 96f, 84f, 34f,
                Color.white, TMPro.TextAlignmentOptions.Center, TMPro.FontStyles.Bold, 100f);

            // Estrellas de nivel, alineadas a la derecha como en la plantilla.
            if (starMaterial == null && LevelStar != null) starMaterial = Primitives.Transparent(LevelStar, Color.white);
            float spacing = Mathf.Min(StarSpacingPx, 820f / Mathf.Max(data.level, 1));
            for (int i = 0; i < data.level && starMaterial != null; i++)
                Primitives.Quad($"Star{i}", face, FramePoint(902f - i * spacing, 212f), FrameSize(60f, 60f), 0.001f, starMaterial);

            // Ilustración: el arte de la carta o, si no tiene, su inicial en el color del atributo.
            if (data.artwork != null)
            {
                Primitives.Quad("Art", face, FramePoint(520f, 654f), FrameSize(804f, 778f), 0.001f, Primitives.Textured(data.artwork.texture));
            }
            else
            {
                var glyphColor = AttributeColor(data.attribute);
                glyphColor.a = 0.55f;
                Primitives.Text("Glyph", face, data.displayName.Substring(0, 1).ToUpperInvariant(),
                    FramePoint(520f, 654f), FrameSize(420f, 420f), 0.002f, glyphColor);
            }

            FrameText("Type", face, $"[Monstruo / {TitleCase(AttributeName(data.attribute))}]", 98f, 1116f, 830f, 46f,
                ParchmentInk, TMPro.TextAlignmentOptions.Left, TMPro.FontStyles.Bold | TMPro.FontStyles.SmallCaps, 0.38f, autoSize: false);
            FrameText("Description", face, data.description, 98f, 1170f, 840f, 160f,
                ParchmentInk, TMPro.TextAlignmentOptions.TopLeft, TMPro.FontStyles.Italic, 0.34f, wrap: true);
            FrameText("Stats", face, StatsLine(data), 545f, 1348f, 400f, 48f,
                ParchmentInk, TMPro.TextAlignmentOptions.Right, TMPro.FontStyles.Normal, 0.42f, autoSize: false);
        }

        private static string TitleCase(string text) =>
            string.IsNullOrEmpty(text) ? text : text.Substring(0, 1) + text.Substring(1).ToLowerInvariant();

        private static Color AttributeColor(CardAttribute attribute)
        {
            switch (attribute)
            {
                case CardAttribute.Light: return new Color(0.95f, 0.8f, 0.3f);
                case CardAttribute.Dark: return new Color(0.55f, 0.25f, 0.8f);
                case CardAttribute.Fire: return new Color(0.9f, 0.3f, 0.15f);
                case CardAttribute.Water: return new Color(0.2f, 0.5f, 0.95f);
                case CardAttribute.Earth: return new Color(0.6f, 0.45f, 0.25f);
                case CardAttribute.Wind: return new Color(0.3f, 0.75f, 0.4f);
                default: return Color.gray;
            }
        }

        private static readonly Color ManaBlue = new Color(0.2f, 0.45f, 1f);

        /// <summary>Gema azul con el coste en maná, en la esquina superior izquierda.</summary>
        private static void CostGem(Transform face, int cost)
        {
            if (face == null) return;
            var center = new Vector2(-Width * 0.5f + 0.06f, Height * 0.5f - 0.06f);
            Primitives.Quad("CostRing", face, center, new Vector2(0.17f, 0.17f), 0.006f,
                Primitives.Transparent(Primitives.CircleTexture, new Color(0.95f, 0.78f, 0.35f)));
            Primitives.Quad("CostGem", face, center, new Vector2(0.14f, 0.14f), 0.007f,
                Primitives.Transparent(Primitives.CircleTexture, ManaBlue));
            var label = Primitives.Text("Cost", face, cost.ToString(), center, new Vector2(0.09f, 0.09f), 0.008f, Color.white);
            label.fontStyle = TMPro.FontStyles.Bold;
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
                default: return $"ATK / {data.attack}    DEF / {data.defense}";
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
