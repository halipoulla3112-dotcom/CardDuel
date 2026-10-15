using System.Collections.Generic;
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
        private static Font font;
        private static Material textMaterial;
        private static readonly Dictionary<Color, Material> LitCache = new Dictionary<Color, Material>();

        public static Mesh CubeMesh => cubeMesh != null ? cubeMesh : cubeMesh = GrabMesh(PrimitiveType.Cube);
        public static Mesh QuadMesh => quadMesh != null ? quadMesh : quadMesh = GrabMesh(PrimitiveType.Quad);

        public static Font Font => font != null ? font : font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        /// <summary>Material de texto 3D que respeta la profundidad (el de TextMesh se ve a través de todo).</summary>
        public static Material TextMaterial
        {
            get
            {
                if (textMaterial != null) return textMaterial;
                var shader = Shader.Find("CardDuel/Text3D");
                textMaterial = new Material(shader != null ? shader : Font.material.shader)
                {
                    mainTexture = Font.material.mainTexture
                };
                Font.textureRebuilt -= OnFontTextureRebuilt;
                Font.textureRebuilt += OnFontTextureRebuilt;
                return textMaterial;
            }
        }

        private static void OnFontTextureRebuilt(Font rebuilt)
        {
            if (rebuilt == font && textMaterial != null) textMaterial.mainTexture = rebuilt.material.mainTexture;
        }

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

        /// <summary>Texto en el plano XY del padre, escalado para caber en <paramref name="maxSize"/>.</summary>
        public static TextMesh Text(string name, Transform parent, string text, Vector2 position, Vector2 maxSize, float lift, Color color)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = new Vector3(position.x, position.y, -lift);
            var textMesh = go.AddComponent<TextMesh>();
            textMesh.font = Font;
            textMesh.fontSize = 64;
            textMesh.characterSize = 0.1f;
            textMesh.anchor = TextAnchor.MiddleCenter;
            textMesh.alignment = TextAlignment.Center;
            textMesh.color = color;
            textMesh.text = text;
            var renderer = go.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = TextMaterial;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            go.AddComponent<FitText>().maxSize = maxSize;
            return textMesh;
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
