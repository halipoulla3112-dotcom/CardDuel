using UnityEngine;
using UnityEngine.Rendering;

namespace CardDuel.Prototype
{
    /// <summary>
    /// Patio de castillo medieval construido por código alrededor del tablero:
    /// suelo de losas, murallas almenadas con puerta y estandartes, torres con
    /// tejado cónico, columnas, una mesa de madera bajo el tablero y antorchas,
    /// con cielo de atardecer, niebla y luz cálida.
    /// </summary>
    public static class CastleEnvironment
    {
        private const float FloorY = -1.4f;
        private const float WallHeight = 8f;
        private const float HalfWidth = 12f;
        private const float HalfDepth = 11f;
        private const float WallThickness = 1f;

        private static readonly Color StoneTint = new Color(0.95f, 0.92f, 0.88f);
        private static readonly Color RoofColor = new Color(0.2f, 0.23f, 0.32f);
        private static readonly Color BannerRed = new Color(0.55f, 0.07f, 0.08f);
        private static readonly Color Gold = new Color(0.85f, 0.65f, 0.25f);
        private static readonly Color Iron = new Color(0.12f, 0.12f, 0.13f);
        private static readonly Color TorchLight = new Color(1f, 0.6f, 0.28f);

        public static void Build(Transform parent, Transform board)
        {
            var root = new GameObject("Castle").transform;
            root.SetParent(parent, false);

            BuildFloor(root);
            BuildWalls(root);
            BuildTowers(root);
            BuildGate(root);
            BuildBanners(root);
            BuildPillars(root);
            BuildTable(root, board);
            BuildTorches(root);
            SetupAtmosphere();
        }

        // ------------------------------------------------------------ Piezas

        private static void BuildFloor(Transform root)
        {
            var size = new Vector3(HalfWidth * 2f + 6f, 0.2f, HalfDepth * 2f + 6f);
            TexturedBox("Floor", root, new Vector3(0f, FloorY - 0.1f, 0f), size,
                CastleTextures.Flagstones, new Vector2(size.x / 3f, size.z / 3f), StoneTint);
        }

        private static void BuildWalls(Transform root)
        {
            float y = FloorY + WallHeight * 0.5f;
            float z = HalfDepth + WallThickness * 0.5f;
            float x = HalfWidth + WallThickness * 0.5f;
            float length = HalfWidth * 2f + WallThickness * 2f;

            Wall(root, "WallFar", new Vector3(0f, y, z), new Vector3(length, WallHeight, WallThickness), length);
            Wall(root, "WallNear", new Vector3(0f, y, -z), new Vector3(length, WallHeight, WallThickness), length);
            Wall(root, "WallLeft", new Vector3(-x, y, 0f), new Vector3(WallThickness, WallHeight, HalfDepth * 2f), HalfDepth * 2f);
            Wall(root, "WallRight", new Vector3(x, y, 0f), new Vector3(WallThickness, WallHeight, HalfDepth * 2f), HalfDepth * 2f);
        }

        /// <summary>Muro de sillares con almenas a lo largo de su parte alta.</summary>
        private static void Wall(Transform root, string name, Vector3 center, Vector3 size, float length)
        {
            TexturedBox(name, root, center, size, CastleTextures.StoneBricks, new Vector2(length / 2f, WallHeight), StoneTint);

            bool alongX = size.x > size.z;
            float top = center.y + size.y * 0.5f + 0.4f;
            int merlons = Mathf.FloorToInt(length / 1.6f);
            for (int i = 0; i < merlons; i++)
            {
                float offset = -length * 0.5f + 0.8f + i * 1.6f;
                var position = alongX ? new Vector3(center.x + offset, top, center.z) : new Vector3(center.x, top, center.z + offset);
                var merlonSize = alongX ? new Vector3(0.8f, 0.8f, size.z + 0.1f) : new Vector3(size.x + 0.1f, 0.8f, 0.8f);
                TexturedBox("Merlon", root, position, merlonSize, CastleTextures.StoneBricks, Vector2.one * 0.5f, StoneTint);
            }
        }

        /// <summary>Torres redondas en las esquinas con tejado cónico de pizarra.</summary>
        private static void BuildTowers(Transform root)
        {
            const float radius = 2.2f, height = 11f;
            float x = HalfWidth + WallThickness * 0.5f, z = HalfDepth + WallThickness * 0.5f;
            foreach (var corner in new[] { new Vector2(-x, -z), new Vector2(x, -z), new Vector2(-x, z), new Vector2(x, z) })
            {
                var tower = Primitive(PrimitiveType.Cylinder, "Tower", root,
                    new Vector3(corner.x, FloorY + height * 0.5f, corner.y), new Vector3(radius * 2f, height * 0.5f, radius * 2f));
                tower.GetComponent<MeshRenderer>().sharedMaterial =
                    CastleTextures.Material(CastleTextures.StoneBricks, new Vector2(6f, height), StoneTint);

                var roof = new GameObject("Roof");
                roof.transform.SetParent(root, false);
                roof.transform.localPosition = new Vector3(corner.x, FloorY + height, corner.y);
                roof.AddComponent<MeshFilter>().sharedMesh = ConeMesh(radius + 0.5f, 4.5f, 16);
                roof.AddComponent<MeshRenderer>().sharedMaterial = Primitives.Lit(RoofColor);
            }
        }

        /// <summary>Gran puerta de madera con herrajes en la muralla del fondo.</summary>
        private static void BuildGate(Transform root)
        {
            float z = HalfDepth - 0.05f;
            const float width = 4.4f, height = 5f;
            TexturedBox("GateFrame", root, new Vector3(0f, FloorY + height * 0.5f + 0.3f, z), new Vector3(width + 1.2f, height + 0.6f, 0.3f),
                CastleTextures.StoneBricks, new Vector2(2.5f, 5f), new Color(0.7f, 0.68f, 0.65f));
            TexturedBox("Gate", root, new Vector3(0f, FloorY + height * 0.5f, z - 0.2f), new Vector3(width, height, 0.2f),
                CastleTextures.Wood, new Vector2(2f, 1.5f), new Color(0.75f, 0.7f, 0.65f));
            for (int i = 0; i < 3; i++)
                Primitives.Box("GateBand", root, new Vector3(0f, FloorY + 0.9f + i * 1.6f, z - 0.32f), new Vector3(width, 0.18f, 0.06f), Primitives.Lit(Iron));
            Primitives.Box("GateSeam", root, new Vector3(0f, FloorY + height * 0.5f, z - 0.32f), new Vector3(0.08f, height, 0.06f), Primitives.Lit(Iron));
        }

        /// <summary>Estandartes rojos con borde y emblema dorados.</summary>
        private static void BuildBanners(Transform root)
        {
            float farZ = HalfDepth - 0.1f;
            float sideX = HalfWidth - 0.1f;
            Banner(root, new Vector3(-6.5f, FloorY + 6.3f, farZ), 180f);
            Banner(root, new Vector3(6.5f, FloorY + 6.3f, farZ), 180f);
            Banner(root, new Vector3(-sideX, FloorY + 6.3f, 3f), 90f);
            Banner(root, new Vector3(sideX, FloorY + 6.3f, 3f), -90f);
            Banner(root, new Vector3(-sideX, FloorY + 6.3f, -5f), 90f);
            Banner(root, new Vector3(sideX, FloorY + 6.3f, -5f), -90f);
        }

        private static void Banner(Transform root, Vector3 top, float yaw)
        {
            var banner = new GameObject("Banner").transform;
            banner.SetParent(root, false);
            banner.localPosition = top;
            banner.localRotation = Quaternion.Euler(0f, yaw, 0f);

            Primitives.Box("Pole", banner, new Vector3(0f, 0.1f, 0.1f), new Vector3(2.1f, 0.14f, 0.14f), Primitives.Lit(new Color(0.3f, 0.18f, 0.08f)));
            Primitives.Box("Cloth", banner, new Vector3(0f, -1.6f, 0.05f), new Vector3(1.7f, 3.2f, 0.05f), Primitives.Lit(BannerRed));
            Primitives.Box("TrimLeft", banner, new Vector3(-0.8f, -1.6f, 0.02f), new Vector3(0.1f, 3.2f, 0.06f), Primitives.Lit(Gold));
            Primitives.Box("TrimRight", banner, new Vector3(0.8f, -1.6f, 0.02f), new Vector3(0.1f, 3.2f, 0.06f), Primitives.Lit(Gold));
            Primitives.Box("TrimBottom", banner, new Vector3(0f, -3.15f, 0.02f), new Vector3(1.7f, 0.1f, 0.06f), Primitives.Lit(Gold));
            var emblem = Primitives.Box("Emblem", banner, new Vector3(0f, -1.3f, 0.01f), new Vector3(0.7f, 0.7f, 0.06f), Primitives.Lit(Gold));
            emblem.localRotation = Quaternion.Euler(0f, 0f, 45f);
            Primitives.Box("EmblemCore", banner, new Vector3(0f, -1.3f, -0.01f), new Vector3(0.32f, 0.32f, 0.06f), Primitives.Lit(BannerRed))
                .localRotation = Quaternion.Euler(0f, 0f, 45f);
        }

        /// <summary>Columnas de piedra a los lados del tablero.</summary>
        private static void BuildPillars(Transform root)
        {
            const float height = 7f;
            foreach (var spot in new[] { new Vector2(-8.5f, -6f), new Vector2(8.5f, -6f), new Vector2(-8.5f, 5.5f), new Vector2(8.5f, 5.5f) })
            {
                var shaft = Primitive(PrimitiveType.Cylinder, "Pillar", root,
                    new Vector3(spot.x, FloorY + height * 0.5f, spot.y), new Vector3(1.1f, height * 0.5f, 1.1f));
                shaft.GetComponent<MeshRenderer>().sharedMaterial =
                    CastleTextures.Material(CastleTextures.StoneBricks, new Vector2(2f, height), StoneTint);
                TexturedBox("PillarBase", root, new Vector3(spot.x, FloorY + 0.25f, spot.y), new Vector3(1.7f, 0.5f, 1.7f),
                    CastleTextures.StoneBricks, Vector2.one, StoneTint);
                TexturedBox("PillarCap", root, new Vector3(spot.x, FloorY + height - 0.2f, spot.y), new Vector3(1.6f, 0.4f, 1.6f),
                    CastleTextures.StoneBricks, Vector2.one, StoneTint);
            }
        }

        /// <summary>Mesa de madera bajo el tablero: el tablero se apoya en ella.</summary>
        private static void BuildTable(Transform root, Transform board)
        {
            var woodColor = new Color(0.8f, 0.72f, 0.65f);
            var top = board != null ? board.Find("Table") : null;
            if (top != null)
                top.GetComponent<MeshRenderer>().sharedMaterial = CastleTextures.Material(CastleTextures.Wood, new Vector2(4f, 2f), woodColor);

            TexturedBox("TableApron", root, new Vector3(0f, -0.38f, 0f), new Vector3(10.2f, 0.36f, 6.4f),
                CastleTextures.Wood, new Vector2(4f, 1f), woodColor * 0.8f);
            foreach (var leg in new[] { new Vector2(-4.8f, -2.9f), new Vector2(4.8f, -2.9f), new Vector2(-4.8f, 2.9f), new Vector2(4.8f, 2.9f) })
            {
                float height = -0.2f - FloorY;
                TexturedBox("TableLeg", root, new Vector3(leg.x, FloorY + height * 0.5f, leg.y), new Vector3(0.55f, height, 0.55f),
                    CastleTextures.Wood, new Vector2(0.5f, 1.5f), woodColor * 0.7f);
            }
        }

        /// <summary>Antorchas en los muros con llama y luz cálida que parpadea.</summary>
        private static void BuildTorches(Transform root)
        {
            float sideX = HalfWidth - 0.15f;
            float farZ = HalfDepth - 0.15f;
            float y = FloorY + 3.6f;
            Torch(root, new Vector3(-3.4f, y, farZ), Vector3.back);
            Torch(root, new Vector3(3.4f, y, farZ), Vector3.back);
            foreach (float z in new[] { -6f, 0f, 6f })
            {
                Torch(root, new Vector3(-sideX, y, z), Vector3.right);
                Torch(root, new Vector3(sideX, y, z), Vector3.left);
            }
        }

        private static void Torch(Transform root, Vector3 wallPoint, Vector3 outward)
        {
            var torch = new GameObject("Torch").transform;
            torch.SetParent(root, false);
            torch.localPosition = wallPoint;
            torch.localRotation = Quaternion.LookRotation(outward, Vector3.up);

            Primitives.Box("Bracket", torch, new Vector3(0f, -0.1f, 0.15f), new Vector3(0.12f, 0.12f, 0.35f), Primitives.Lit(Iron));
            var stick = Primitives.Box("Stick", torch, new Vector3(0f, 0.15f, 0.3f), new Vector3(0.1f, 0.6f, 0.1f), Primitives.Lit(new Color(0.3f, 0.18f, 0.08f)));
            stick.localRotation = Quaternion.Euler(20f, 0f, 0f);

            var flame = Primitive(PrimitiveType.Sphere, "Flame", torch, new Vector3(0f, 0.55f, 0.42f), new Vector3(0.22f, 0.38f, 0.22f));
            flame.GetComponent<MeshRenderer>().sharedMaterial = Unlit(new Color(1f, 0.7f, 0.25f));
            flame.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;

            var light = new GameObject("TorchLight").AddComponent<Light>();
            light.transform.SetParent(torch, false);
            light.transform.localPosition = new Vector3(0f, 0.6f, 0.8f);
            light.type = LightType.Point;
            light.color = TorchLight;
            light.range = 9f;
            light.intensity = 2.2f;
            light.shadows = LightShadows.None;

            torch.gameObject.AddComponent<TorchFlicker>().Init(light, flame.transform);
        }

        // ------------------------------------------------------ Ambiente

        /// <summary>Cielo de atardecer, luz cálida, ambiente tenue y algo de niebla a lo lejos.</summary>
        private static void SetupAtmosphere()
        {
            var skyShader = Shader.Find("Skybox/Procedural");
            if (skyShader != null)
            {
                var sky = new Material(skyShader);
                sky.SetColor("_SkyTint", new Color(0.85f, 0.45f, 0.35f));
                sky.SetColor("_GroundColor", new Color(0.22f, 0.17f, 0.15f));
                sky.SetFloat("_AtmosphereThickness", 1.7f);
                sky.SetFloat("_Exposure", 0.85f);
                sky.SetFloat("_SunSize", 0.05f);
                RenderSettings.skybox = sky;
            }

            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.5f, 0.45f, 0.55f);
            RenderSettings.ambientEquatorColor = new Color(0.4f, 0.33f, 0.3f);
            RenderSettings.ambientGroundColor = new Color(0.16f, 0.13f, 0.11f);

            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = new Color(0.42f, 0.32f, 0.32f);
            RenderSettings.fogStartDistance = 24f;
            RenderSettings.fogEndDistance = 70f;

            foreach (var light in Object.FindObjectsByType<Light>(FindObjectsSortMode.None))
            {
                if (light.type != LightType.Directional) continue;
                light.color = new Color(1f, 0.84f, 0.66f);
                light.intensity = 1.15f;
                light.shadows = LightShadows.Soft;
                light.transform.rotation = Quaternion.Euler(48f, -35f, 0f);
                RenderSettings.sun = light;
            }

            DynamicGI.UpdateEnvironment();
        }

        // ------------------------------------------------------- Ayudas

        private static Transform TexturedBox(string name, Transform parent, Vector3 position, Vector3 size, Texture2D texture, Vector2 tiling, Color tint)
        {
            return Primitives.Box(name, parent, position, size, CastleTextures.Material(texture, tiling, tint));
        }

        private static GameObject Primitive(PrimitiveType type, string name, Transform parent, Vector3 position, Vector3 scale)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            Object.Destroy(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            go.transform.localScale = scale;
            return go;
        }

        private static Material Unlit(Color color)
        {
            var shader = Shader.Find("Universal Render Pipeline/Unlit");
            var material = new Material(shader != null ? shader : Shader.Find("Unlit/Color"));
            material.SetColor("_BaseColor", color);
            material.color = color;
            return material;
        }

        /// <summary>Cono con base en el origen y punta hacia arriba.</summary>
        private static Mesh ConeMesh(float radius, float height, int segments)
        {
            var vertices = new Vector3[segments * 3];
            var triangles = new int[segments * 3];
            for (int i = 0; i < segments; i++)
            {
                float a0 = i * Mathf.PI * 2f / segments;
                float a1 = (i + 1) * Mathf.PI * 2f / segments;
                vertices[i * 3] = new Vector3(Mathf.Cos(a0) * radius, 0f, Mathf.Sin(a0) * radius);
                vertices[i * 3 + 1] = new Vector3(0f, height, 0f);
                vertices[i * 3 + 2] = new Vector3(Mathf.Cos(a1) * radius, 0f, Mathf.Sin(a1) * radius);
                triangles[i * 3] = i * 3;
                triangles[i * 3 + 1] = i * 3 + 1;
                triangles[i * 3 + 2] = i * 3 + 2;
            }
            var mesh = new Mesh { name = "Cone", vertices = vertices, triangles = triangles };
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }
    }

    /// <summary>Hace parpadear la luz y la llama de una antorcha.</summary>
    public class TorchFlicker : MonoBehaviour
    {
        private Light torchLight;
        private Transform flame;
        private float baseIntensity;
        private Vector3 baseScale;
        private float seed;

        public void Init(Light light, Transform flameTransform)
        {
            torchLight = light;
            flame = flameTransform;
            baseIntensity = light.intensity;
            baseScale = flameTransform.localScale;
            seed = Random.value * 100f;
        }

        private void Update()
        {
            if (torchLight == null) return;
            float n = Mathf.PerlinNoise(seed, Time.time * 6f);
            torchLight.intensity = baseIntensity * (0.75f + 0.5f * n);
            flame.localScale = new Vector3(baseScale.x, baseScale.y * (0.85f + 0.3f * n), baseScale.z);
        }
    }
}
