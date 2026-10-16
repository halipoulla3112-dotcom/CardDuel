using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering;

namespace CardDuel.Prototype
{
    /// <summary>
    /// Helpers para construir la escena por código (sin prefabs ni assets),
    /// útil mientras el arte definitivo no existe.
    /// </summary>
    public static class Primitives
    {
        private static Mesh cubeMesh;
        private static Mesh quadMesh;
        private static TMP_FontAsset serifFont;
        private static bool serifFontTried;
        private static bool serifFontFound;
        private static Texture2D circleTexture;
        private const float TextDilate = 0.25f;
        private static readonly string[] SerifFamilies = { "Georgia", "Cambria", "Times New Roman", "Noto Serif", "Droid Serif" };
        private static readonly Dictionary<TMP_FontAsset, Material> ThickMaterials = new Dictionary<TMP_FontAsset, Material>();
        private static readonly Dictionary<Texture, Material> TexturedCache = new Dictionary<Texture, Material>();
        private static readonly Dictionary<Color, Material> LitCache = new Dictionary<Color, Material>();

        public static Mesh CubeMesh => cubeMesh != null ? cubeMesh : cubeMesh = GrabMesh(PrimitiveType.Cube);
        public static Mesh QuadMesh => quadMesh != null ? quadMesh : quadMesh = GrabMesh(PrimitiveType.Quad);

        /// <summary>Material URP Lit compartido por color.</summary>
        public static Material Lit(Color color)
        {
            if (LitCache.TryGetValue(color, out var cached) && cached != null) return cached;
            var material = NewLit(color);
            LitCache[color] = material;
            return material;
        }

        /// <summary>Material URP Lit propio (para poder animar su color).</summary>
        public static Material NewLit(Color color)
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");
            var material = new Material(shader);
            material.SetColor("_BaseColor", color);
            material.color = color;
            material.SetFloat("_Smoothness", 0.35f);
            return material;
        }

        public static Transform Box(string name, Transform parent, Vector3 localPosition, Vector3 scale, Material material, bool collider = false)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            go.transform.localScale = scale;
            go.AddComponent<MeshFilter>().sharedMesh = CubeMesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = material;
            if (collider) go.AddComponent<BoxCollider>();
            return go.transform;
        }

        /// <summary>Quad en el plano XY del padre, mirando hacia -Z. <paramref name="lift"/> lo separa hacia -Z.</summary>
        public static Transform Quad(string name, Transform parent, Vector2 position, Vector2 size, float lift, Color color)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = new Vector3(position.x, position.y, -lift);
            go.transform.localScale = new Vector3(size.x, size.y, 1f);
            go.AddComponent<MeshFilter>().sharedMesh = QuadMesh;
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = Lit(color);
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            return go.transform;
        }

        /// <summary>
        /// Texto TextMeshPro (SDF, nítido a cualquier tamaño) en el plano XY del padre, mirando hacia -Z.
        /// Se ajusta solo para caber en <paramref name="maxSize"/>; con <paramref name="wrap"/> parte las líneas.
        /// </summary>
        public static TextMeshPro Text(string name, Transform parent, string text, Vector2 position, Vector2 maxSize, float lift, Color color, bool wrap = false)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var textMesh = go.AddComponent<TextMeshPro>();
            textMesh.rectTransform.sizeDelta = maxSize;
            textMesh.rectTransform.localPosition = new Vector3(position.x, position.y, -lift);
            textMesh.alignment = TextAlignmentOptions.Center;
            textMesh.fontSharedMaterial = ThickTextMaterial(textMesh);
            textMesh.textWrappingMode = wrap ? TextWrappingModes.Normal : TextWrappingModes.NoWrap;
            textMesh.overflowMode = TextOverflowModes.Overflow;
            textMesh.enableAutoSizing = true;
            textMesh.fontSizeMin = 0.01f;
            textMesh.fontSizeMax = 100f;
            textMesh.color = color;
            textMesh.text = text;
            go.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
            return textMesh;
        }

        /// <summary>
        /// Copia del material de la fuente con el trazo engrosado: las letras se ven más
        /// negras y legibles sin cambiar su tamaño (la negrita las ensancha y el autoajuste las encoge).
        /// </summary>
        private static Material ThickTextMaterial(TMP_Text text)
        {
            if (ThickMaterials.TryGetValue(text.font, out var cached) && cached != null) return cached;
            var material = new Material(text.font.material) { name = $"{text.font.name} Thick" };
            material.SetFloat(ShaderUtilities.ID_FaceDilate, TextDilate);
            ThickMaterials[text.font] = material;
            return material;
        }

        /// <summary>Cambia la fuente de un texto conservando el trazo engrosado.</summary>
        public static void SetFont(TMP_Text text, TMP_FontAsset font)
        {
            if (font == null) return;
            text.font = font;
            text.fontSharedMaterial = ThickTextMaterial(text);
        }

        /// <summary>
        /// Fuente serif del sistema (Georgia en Windows, Noto Serif en Android...) para los
        /// textos de las cartas. Si no hay ninguna, devuelve null y se usa la de por defecto.
        /// </summary>
        public static TMP_FontAsset SerifFont
        {
            get
            {
                // Si se creó pero Unity la liberó al recargar la escena, se vuelve a crear.
                if (serifFont != null) return serifFont;
                if (serifFontTried && !serifFontFound) return null;
                serifFontTried = true;
                foreach (var family in SerifFamilies)
                {
                    serifFont = TMP_FontAsset.CreateFontAsset(family, "Regular");
                    if (serifFont == null) continue;
                    serifFontFound = true;
                    serifFont.name = family;
                    if (TMP_Settings.defaultFontAsset != null)
                        serifFont.fallbackFontAssetTable = new List<TMP_FontAsset> { TMP_Settings.defaultFontAsset };
                    break;
                }
                return serifFont;
            }
        }

        /// <summary>Material URP Lit con textura (marco, ilustración), poco brillante.</summary>
        public static Material Textured(Texture texture)
        {
            if (TexturedCache.TryGetValue(texture, out var cached) && cached != null) return cached;
            var material = NewLit(Color.white);
            material.SetTexture("_BaseMap", texture);
            material.mainTexture = texture;
            material.SetFloat("_Smoothness", 0.1f);
            TexturedCache[texture] = material;
            return material;
        }

        /// <summary>Material URP Unlit translúcido (mezcla alfa); admite color HDR para que brille con el bloom.</summary>
        public static Material UnlitTransparent(Texture texture, Color color)
        {
            var material = UnlitColor(color);
            material.SetTexture("_BaseMap", texture);
            material.mainTexture = texture;
            material.SetFloat("_Surface", 1f);
            material.SetFloat("_Blend", 0f);
            material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            material.SetFloat("_ZWrite", 0f);
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.renderQueue = (int)RenderQueue.Transparent;
            return material;
        }

        /// <summary>Material URP Unlit opaco de un color (HDR > 1 para que brille).</summary>
        public static Material UnlitColor(Color color)
        {
            var shader = Shader.Find("Universal Render Pipeline/Unlit");
            var material = new Material(shader != null ? shader : Shader.Find("Unlit/Color"));
            material.SetColor("_BaseColor", color);
            material.color = color;
            return material;
        }

        /// <summary>Material sin iluminación y con transparencia (estrellas, orbe del atributo).</summary>
        public static Material Transparent(Texture texture, Color color)
        {
            var material = new Material(Shader.Find("Sprites/Default")) { mainTexture = texture, color = color };
            return material;
        }

        /// <summary>Quad con un material propio en el plano XY del padre, mirando hacia -Z.</summary>
        public static Transform Quad(string name, Transform parent, Vector2 position, Vector2 size, float lift, Material material)
        {
            var quad = Quad(name, parent, position, size, lift, Color.white);
            quad.GetComponent<MeshRenderer>().sharedMaterial = material;
            return quad;
        }

        /// <summary>Círculo blanco de borde suave, para teñirlo con el color de cada atributo.</summary>
        public static Texture2D CircleTexture
        {
            get
            {
                if (circleTexture != null) return circleTexture;
                const int size = 128;
                circleTexture = new Texture2D(size, size, TextureFormat.RGBA32, true) { wrapMode = TextureWrapMode.Clamp };
                float r = size * 0.5f;
                for (int y = 0; y < size; y++)
                    for (int x = 0; x < size; x++)
                    {
                        float d = new Vector2(x + 0.5f - r, y + 0.5f - r).magnitude / r;
                        float alpha = Mathf.Clamp01((1f - d) * 24f);
                        float shade = Mathf.Lerp(1f, 0.55f, d * d);
                        circleTexture.SetPixel(x, y, new Color(shade, shade, shade, alpha));
                    }
                circleTexture.Apply();
                return circleTexture;
            }
        }

        private static Mesh GrabMesh(PrimitiveType type)
        {
            var go = GameObject.CreatePrimitive(type);
            go.SetActive(false);
            var mesh = go.GetComponent<MeshFilter>().sharedMesh;
            Object.Destroy(go);
            return mesh;
        }
    }
}
