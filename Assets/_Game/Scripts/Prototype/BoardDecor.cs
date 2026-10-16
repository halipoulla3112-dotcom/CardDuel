using CardDuel.Cards;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;
using UnityEngine.Rendering;

namespace CardDuel.Prototype
{
    /// <summary>
    /// Pequeños objetos de ambiente alrededor y en los bordes del tablero: setas,
    /// flores y piedras en tu lado de césped; musgo y velitas en el de ladrillo;
    /// farolillos, guirnalda de banderines, calabazas, manzanas y girasoles junto a
    /// la mesa; animales paseando y mariposas revoloteando. Nada tapa las zonas.
    /// </summary>
    public static class BoardDecor
    {
        private const float Ground = -1.4f;
        private const float FrameX = 5.2f;
        private const float FrameZ = 3.27f;

        private static readonly Color[] FlagColors =
        {
            new Color(1f, 0.35f, 0.35f), new Color(1f, 0.75f, 0.25f), new Color(1f, 0.95f, 0.4f),
            new Color(0.45f, 0.85f, 0.4f), new Color(0.35f, 0.7f, 1f), new Color(0.75f, 0.5f, 1f),
        };
        private static readonly Color[] PetalColors =
        {
            new Color(1f, 0.4f, 0.55f), new Color(1f, 0.9f, 0.3f), new Color(0.95f, 0.95f, 1f), new Color(0.7f, 0.5f, 1f),
        };
        private static readonly Color[] WingColors =
        {
            new Color(1f, 0.6f, 0.15f), new Color(0.35f, 0.65f, 1f), new Color(1f, 0.45f, 0.75f), new Color(1f, 0.9f, 0.3f), new Color(0.6f, 0.95f, 0.5f),
        };

        private static System.Random random;

        public static void Build(Transform parent)
        {
            random = new System.Random(11);
            var root = new GameObject("BoardDecor").transform;
            root.SetParent(parent, false);

            BuildGrassMargins(root);
            BuildBrickMargins(root);
            BuildLanterns(root);
            BuildBunting(root);
            BuildFarmProps(root);
            BuildCritters(root);
            BuildButterflies(root);
        }

        // ------------------------------------------------- Bordes del tablero

        /// <summary>Puntos libres en los márgenes de una mitad (entre las zonas y el marco).</summary>
        private static Vector3[] MarginSpots(float side, int count)
        {
            var spots = new Vector3[count];
            for (int i = 0; i < count; i++)
            {
                // Banda de atrás (junto al marco) o bandas laterales.
                if (i % 3 == 0)
                    spots[i] = new Vector3(Range(4.75f, 5.08f) * (random.Next(2) == 0 ? -1f : 1f), 0f, side * Range(0.4f, 3.1f));
                else
                    spots[i] = new Vector3(Range(-5.05f, 5.05f), 0f, side * Range(2.9f, 3.15f));
            }
            return spots;
        }

        /// <summary>Tu lado de césped: setas, florecillas, matas y piedrecitas.</summary>
        private static void BuildGrassMargins(Transform root)
        {
            var spots = MarginSpots(-1f, 30);
            for (int i = 0; i < spots.Length; i++)
            {
                switch (i % 4)
                {
                    case 0: Mushroom(root, spots[i], Range(1.7f, 2.4f)); break;
                    case 1: Flower(root, spots[i], PetalColors[random.Next(PetalColors.Length)], 0.2f); break;
                    case 2: Tuft(root, spots[i], new Color(0.35f, 0.72f, 0.25f)); break;
                    default: Pebble(root, spots[i]); break;
                }
            }
        }

        /// <summary>El lado de ladrillo del rival: musgo, hierbajos y velitas encendidas.</summary>
        private static void BuildBrickMargins(Transform root)
        {
            var spots = MarginSpots(1f, 24);
            for (int i = 0; i < spots.Length; i++)
            {
                switch (i % 3)
                {
                    case 0: Candle(root, spots[i]); break;
                    case 1: Moss(root, spots[i]); break;
                    default: Tuft(root, spots[i], new Color(0.45f, 0.62f, 0.25f)); break;
                }
            }
        }

        private static void Mushroom(Transform root, Vector3 p, float scale)
        {
            Cylinder(root, "MushroomStem", p + Vector3.up * 0.04f * scale, new Vector3(0.035f, 0.04f, 0.035f) * scale, Primitives.Lit(new Color(0.97f, 0.94f, 0.85f)));
            Sphere(root, "MushroomCap", p + Vector3.up * 0.085f * scale, new Vector3(0.11f, 0.06f, 0.11f) * scale, Primitives.Lit(new Color(0.9f, 0.15f, 0.12f)));
            for (int d = 0; d < 3; d++)
            {
                float angle = d * 2.1f + (float)random.NextDouble();
                var dot = p + new Vector3(Mathf.Cos(angle) * 0.03f, 0.108f, Mathf.Sin(angle) * 0.03f) * scale;
                Sphere(root, "MushroomDot", dot, Vector3.one * 0.016f * scale, Primitives.Lit(Color.white));
            }
        }

        private static void Flower(Transform root, Vector3 p, Color petals, float height)
        {
            float s = height / 0.12f;
            Primitives.Box("Stem", root, p + Vector3.up * height * 0.5f, new Vector3(0.014f, height, 0.014f) * s, Primitives.Lit(new Color(0.3f, 0.6f, 0.2f)));
            for (int i = 0; i < 5; i++)
            {
                float angle = i * Mathf.PI * 2f / 5f;
                Sphere(root, "Petal", p + new Vector3(Mathf.Cos(angle) * 0.025f * s, height, Mathf.Sin(angle) * 0.025f * s), new Vector3(0.03f, 0.012f, 0.03f) * s, Primitives.Lit(petals));
            }
            Sphere(root, "FlowerCenter", p + Vector3.up * (height + 0.005f * s), Vector3.one * 0.022f * s, Primitives.Lit(new Color(1f, 0.8f, 0.2f)));
        }

        private static void Tuft(Transform root, Vector3 p, Color color)
        {
            var material = Primitives.Lit(color);
            for (int i = 0; i < 5; i++)
            {
                var blade = Primitives.Box("Blade", root, p + new Vector3(Range(-0.05f, 0.05f), 0.07f, Range(-0.05f, 0.05f)), new Vector3(0.022f, 0.16f, 0.022f), material);
                blade.localRotation = Quaternion.Euler(Range(-25f, 25f), Range(0f, 180f), Range(-25f, 25f));
            }
        }

        private static void Pebble(Transform root, Vector3 p)
        {
            float gray = Range(0.55f, 0.75f);
            Sphere(root, "Pebble", p + Vector3.up * 0.02f, new Vector3(Range(0.08f, 0.13f), 0.055f, Range(0.07f, 0.11f)), Primitives.Lit(new Color(gray, gray, gray * 0.95f)));
        }

        private static void Moss(Transform root, Vector3 p)
        {
            var moss = Primitives.Lit(new Color(0.35f, 0.55f, 0.22f));
            for (int i = 0; i < 4; i++)
                Sphere(root, "Moss", p + new Vector3(Range(-0.07f, 0.07f), 0.015f, Range(-0.07f, 0.07f)), new Vector3(0.11f, 0.04f, 0.11f), moss);
        }

        private static void Candle(Transform root, Vector3 p)
        {
            float height = Range(0.14f, 0.22f);
            Cylinder(root, "Candle", p + Vector3.up * height * 0.5f, new Vector3(0.065f, height * 0.5f, 0.065f), Primitives.Lit(new Color(0.98f, 0.93f, 0.8f)));
            var flame = Sphere(root, "Flame", p + Vector3.up * (height + 0.03f), new Vector3(0.032f, 0.06f, 0.032f), Primitives.UnlitColor(new Color(4f, 2.4f, 0.7f)));
            flame.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
            flame.gameObject.AddComponent<Flicker>();
        }

        // --------------------------------------------- Alrededor de la mesa

        /// <summary>Farolillos en el suelo junto a las esquinas de la mesa, con luz cálida.</summary>
        private static void BuildLanterns(Transform root)
        {
            foreach (var corner in new[] { new Vector2(-6f, -3.9f), new Vector2(6f, -3.9f), new Vector2(-6.2f, 1.5f), new Vector2(6.2f, 1.5f) })
                Lantern(root, new Vector3(corner.x, Ground, corner.y), 1f);
        }

        private static void Lantern(Transform root, Vector3 basePoint, float scale)
        {
            var frame = Primitives.Lit(new Color(0.2f, 0.15f, 0.12f));
            Primitives.Box("LanternBase", root, basePoint + Vector3.up * 0.04f * scale, new Vector3(0.36f, 0.08f, 0.36f) * scale, frame);
            Primitives.Box("LanternGlass", root, basePoint + Vector3.up * 0.3f * scale, new Vector3(0.28f, 0.44f, 0.28f) * scale, Primitives.UnlitColor(new Color(2.6f, 1.7f, 0.7f)));
            Primitives.Box("LanternTop", root, basePoint + Vector3.up * 0.56f * scale, new Vector3(0.38f, 0.08f, 0.38f) * scale, frame);
            Primitives.Box("LanternHandle", root, basePoint + Vector3.up * 0.68f * scale, new Vector3(0.22f, 0.04f, 0.04f) * scale, frame);

            var light = new GameObject("LanternLight").AddComponent<Light>();
            light.transform.SetParent(root, false);
            light.transform.localPosition = basePoint + Vector3.up * 0.4f * scale;
            light.type = LightType.Point;
            light.color = new Color(1f, 0.72f, 0.4f);
            light.range = 3.5f;
            light.intensity = 1.6f;
            light.shadows = LightShadows.None;
        }

        /// <summary>Dos postes al fondo con una guirnalda de banderines de colores que se mece.</summary>
        private static void BuildBunting(Transform root)
        {
            var wood = EnvironmentTextures.Material(EnvironmentTextures.Wood, new Vector2(0.3f, 2f), new Color(0.9f, 0.8f, 0.7f));
            const float z = 3.9f, x = 5.9f, top = 2.6f;
            foreach (float px in new[] { -x, x })
            {
                Primitives.Box("BuntingPost", root, new Vector3(px, (Ground + top) * 0.5f, z), new Vector3(0.14f, top - Ground, 0.14f), wood);
                Sphere(root, "PostCap", new Vector3(px, top + 0.06f, z), Vector3.one * 0.2f, Primitives.Lit(new Color(0.95f, 0.75f, 0.35f)));
            }

            var garland = new GameObject("Garland").transform;
            garland.SetParent(root, false);
            garland.localPosition = new Vector3(0f, top - 0.05f, z);
            const int flags = 15;
            const float sag = 0.55f;
            var rope = Primitives.Lit(new Color(0.95f, 0.92f, 0.85f));
            for (int i = 0; i < flags; i++)
            {
                float t = (i + 0.5f) / flags;
                float px = Mathf.Lerp(-x, x, t);
                float py = -sag * 4f * t * (1f - t);
                // Pendiente de la curva en este tramo: dy/dx de y = -4·sag·t·(1-t) con x de -x a x.
                float slope = -sag * 4f * (1f - 2f * t) / (2f * x);
                Primitives.Box("Rope", garland, new Vector3(px, py, 0f), new Vector3(x * 2f / flags + 0.03f, 0.015f, 0.015f), rope)
                    .localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan(slope) * Mathf.Rad2Deg);
                Pennant(garland, new Vector3(px, py - 0.01f, 0f), FlagColors[i % FlagColors.Length]);
            }
            garland.gameObject.AddComponent<Sway>();
        }

        /// <summary>Banderín triangular colgando hacia abajo.</summary>
        private static void Pennant(Transform parent, Vector3 top, Color color)
        {
            var go = new GameObject("Pennant");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = top;
            var mesh = new Mesh { name = "Pennant" };
            mesh.vertices = new[]
            {
                new Vector3(-0.17f, 0f, -0.01f), new Vector3(0.17f, 0f, -0.01f), new Vector3(0f, -0.38f, -0.01f),
                new Vector3(-0.17f, 0f, 0.01f), new Vector3(0.17f, 0f, 0.01f), new Vector3(0f, -0.38f, 0.01f),
            };
            mesh.triangles = new[] { 0, 1, 2, 4, 3, 5 };
            mesh.RecalculateNormals();
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = Primitives.Lit(color);
        }

        /// <summary>Calabazas, una caja de manzanas y girasoles junto a la mesa.</summary>
        private static void BuildFarmProps(Transform root)
        {
            foreach (var spot in new[] { new Vector3(-6.4f, 0f, -2.2f), new Vector3(-6.9f, 0f, -1.5f), new Vector3(6.5f, 0f, -1.8f), new Vector3(7f, 0f, 3.4f) })
                Pumpkin(root, new Vector3(spot.x, Ground, spot.z), Range(0.8f, 1.2f));

            AppleCrate(root, new Vector3(6.6f, Ground, -0.2f));

            foreach (var spot in new[] { new Vector2(-7.2f, 3.2f), new Vector2(-7.8f, 2.2f), new Vector2(7.6f, 4.6f) })
                Sunflower(root, new Vector3(spot.x, Ground, spot.y), Range(2.2f, 2.8f));
        }

        private static void Pumpkin(Transform root, Vector3 basePoint, float scale)
        {
            var orange = Primitives.Lit(new Color(1f, 0.5f, 0.1f));
            for (int i = 0; i < 4; i++)
            {
                float angle = i * Mathf.PI * 0.5f;
                Sphere(root, "PumpkinLobe", basePoint + new Vector3(Mathf.Cos(angle) * 0.1f, 0.25f, Mathf.Sin(angle) * 0.1f) * scale,
                    new Vector3(0.36f, 0.42f, 0.36f) * scale, orange);
            }
            Cylinder(root, "PumpkinStem", basePoint + Vector3.up * 0.5f * scale, new Vector3(0.06f, 0.08f, 0.06f) * scale, Primitives.Lit(new Color(0.35f, 0.45f, 0.15f)));
        }

        private static void AppleCrate(Transform root, Vector3 basePoint)
        {
            var wood = EnvironmentTextures.Material(EnvironmentTextures.Wood, new Vector2(1f, 0.5f), new Color(1f, 0.88f, 0.75f));
            Primitives.Box("CrateBottom", root, basePoint + Vector3.up * 0.05f, new Vector3(0.8f, 0.1f, 0.55f), wood);
            foreach (float side in new[] { -1f, 1f })
            {
                Primitives.Box("CrateSide", root, basePoint + new Vector3(side * 0.38f, 0.22f, 0f), new Vector3(0.05f, 0.35f, 0.55f), wood);
                Primitives.Box("CrateEnd", root, basePoint + new Vector3(0f, 0.22f, side * 0.25f), new Vector3(0.8f, 0.3f, 0.05f), wood);
            }
            var red = Primitives.Lit(new Color(0.9f, 0.12f, 0.12f));
            var green = Primitives.Lit(new Color(0.55f, 0.85f, 0.25f));
            for (int i = 0; i < 10; i++)
                Sphere(root, "Apple", basePoint + new Vector3(Range(-0.28f, 0.28f), Range(0.32f, 0.42f), Range(-0.16f, 0.16f)), Vector3.one * 0.13f, i % 4 == 0 ? green : red);
        }

        private static void Sunflower(Transform root, Vector3 basePoint, float height)
        {
            Cylinder(root, "SunflowerStem", basePoint + Vector3.up * height * 0.5f, new Vector3(0.07f, height * 0.5f, 0.07f), Primitives.Lit(new Color(0.3f, 0.6f, 0.2f)));
            var head = new GameObject("SunflowerHead").transform;
            head.SetParent(root, false);
            head.localPosition = basePoint + Vector3.up * height;
            head.localRotation = Quaternion.Euler(-60f, Range(-20f, 20f), 0f);
            var petals = Primitives.Lit(new Color(1f, 0.82f, 0.1f));
            for (int i = 0; i < 12; i++)
            {
                var petal = new GameObject("Petal").transform;
                petal.SetParent(head, false);
                petal.localRotation = Quaternion.Euler(0f, 0f, i * 30f);
                var leaf = Sphere(petal, "PetalShape", new Vector3(0f, 0.22f, 0f), new Vector3(0.1f, 0.22f, 0.03f), petals);
                leaf.localRotation = Quaternion.identity;
            }
            Cylinder(head, "SunflowerCenter", new Vector3(0f, 0f, -0.02f), new Vector3(0.26f, 0.025f, 0.26f), Primitives.Lit(new Color(0.4f, 0.24f, 0.1f)))
                .localRotation = Quaternion.Euler(90f, 0f, 0f);
        }

        // ----------------------------------------------------- Seres vivos

        /// <summary>Animales de la granja que pasean por la hierba alrededor de la mesa.</summary>
        private static void BuildCritters(Transform root)
        {
            var library = MonsterModelLibrary.Instance;
            if (library == null) return;
            for (int i = 0; i < library.critters.Count; i++)
            {
                var critter = library.critters[i];
                if (critter.model == null) continue;
                var go = new GameObject("Critter");
                go.transform.SetParent(root, false);
                go.AddComponent<AmbientCritter>().Init(critter, i * 7 + 3);
            }
        }

        private static void BuildButterflies(Transform root)
        {
            for (int i = 0; i < 6; i++)
            {
                var go = new GameObject("Butterfly");
                go.transform.SetParent(root, false);
                go.AddComponent<Butterfly>().Init(WingColors[i % WingColors.Length], i * 1.7f);
            }
        }

        // ------------------------------------------------------------ Ayudas

        private static float Range(float min, float max) => min + (float)random.NextDouble() * (max - min);

        private static Transform Sphere(Transform parent, string name, Vector3 position, Vector3 scale, Material material) =>
            Shape(PrimitiveType.Sphere, parent, name, position, scale, material);

        private static Transform Cylinder(Transform parent, string name, Vector3 position, Vector3 scale, Material material) =>
            Shape(PrimitiveType.Cylinder, parent, name, position, scale, material);

        private static Transform Shape(PrimitiveType type, Transform parent, string name, Vector3 position, Vector3 scale, Material material)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            Object.Destroy(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            go.transform.localScale = scale;
            go.GetComponent<MeshRenderer>().sharedMaterial = material;
            return go.transform;
        }
    }

    /// <summary>Llama de vela que tiembla.</summary>
    public class Flicker : MonoBehaviour
    {
        private Vector3 baseScale;
        private float seed;

        private void Start()
        {
            baseScale = transform.localScale;
            seed = Random.value * 50f;
        }

        private void Update()
        {
            float n = Mathf.PerlinNoise(seed, Time.time * 7f);
            transform.localScale = new Vector3(baseScale.x, baseScale.y * (0.8f + 0.4f * n), baseScale.z);
        }
    }

    /// <summary>Balanceo suave (guirnalda movida por la brisa).</summary>
    public class Sway : MonoBehaviour
    {
        private void Update()
        {
            transform.localRotation = Quaternion.Euler(Mathf.Sin(Time.time * 0.9f) * 6f, 0f, Mathf.Sin(Time.time * 0.6f) * 1.2f);
        }
    }

    /// <summary>Mariposa que revolotea en curvas suaves sobre el tablero y el prado, batiendo las alas.</summary>
    public class Butterfly : MonoBehaviour
    {
        private Transform leftWing, rightWing;
        private float phase;
        private Vector3 center;

        public void Init(Color color, float phaseOffset)
        {
            phase = phaseOffset;
            center = new Vector3(Random.Range(-4f, 4f), Random.Range(0.6f, 1.6f), Random.Range(-3f, 3f));
            var material = Primitives.UnlitTransparent(WingTexture(), color);
            leftWing = Wing("LeftWing", material, -1f);
            rightWing = Wing("RightWing", material, 1f);
            var body = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            Destroy(body.GetComponent<Collider>());
            body.transform.SetParent(transform, false);
            body.transform.localScale = new Vector3(0.02f, 0.02f, 0.09f);
            body.GetComponent<MeshRenderer>().sharedMaterial = Primitives.Lit(new Color(0.15f, 0.1f, 0.08f));
        }

        private Transform Wing(string name, Material material, float side)
        {
            var hinge = new GameObject(name).transform;
            hinge.SetParent(transform, false);
            var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            Destroy(quad.GetComponent<Collider>());
            quad.transform.SetParent(hinge, false);
            quad.transform.localPosition = new Vector3(side * 0.1f, 0f, 0f);
            quad.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            quad.transform.localScale = new Vector3(0.2f, 0.17f, 1f);
            var renderer = quad.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            return hinge;
        }

        private void Update()
        {
            float t = Time.time * 0.35f + phase;
            var position = center + new Vector3(Mathf.Sin(t) * 3.5f, Mathf.Sin(t * 2.3f) * 0.35f, Mathf.Sin(t * 0.8f + 1f) * 2.5f);
            var direction = position - transform.localPosition;
            transform.localPosition = position;
            if (direction.sqrMagnitude > 0.000001f)
                transform.localRotation = Quaternion.LookRotation(new Vector3(direction.x, 0f, direction.z), Vector3.up);

            float flap = Mathf.Sin(Time.time * 18f + phase) * 55f;
            leftWing.localRotation = Quaternion.Euler(0f, 0f, flap);
            rightWing.localRotation = Quaternion.Euler(0f, 0f, -flap);
        }

        private static Texture2D wingTexture;

        /// <summary>Ala ovalada con el borde oscuro y un punto claro.</summary>
        private static Texture2D WingTexture()
        {
            if (wingTexture != null) return wingTexture;
            const int size = 64;
            wingTexture = new Texture2D(size, size, TextureFormat.RGBA32, true) { wrapMode = TextureWrapMode.Clamp };
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    var p = new Vector2((x + 0.5f) / size * 2f - 1f, (y + 0.5f) / size * 2f - 1f);
                    float d = p.magnitude;
                    float alpha = Mathf.Clamp01((1f - d) * 12f);
                    float shade = d > 0.82f ? 0.25f : (new Vector2(p.x - 0.2f, p.y - 0.2f).magnitude < 0.22f ? 1.4f : 1f);
                    wingTexture.SetPixel(x, y, new Color(shade, shade, shade, alpha));
                }
            wingTexture.Apply();
            return wingTexture;
        }
    }

    /// <summary>Animal de granja que pasea por la hierba (fuera de la mesa) y se para de vez en cuando.</summary>
    public class AmbientCritter : MonoBehaviour
    {
        private const float Ground = -1.4f;
        private const float Height = 0.55f;
        private const float Speed = 0.7f;

        private MonsterModelLibrary.Critter critter;
        private System.Random random;
        private Animator animator;
        private PlayableGraph graph;
        private AnimationPlayableOutput output;
        private AnimationClipPlayable current;
        private AnimationClip currentClip;
        private Vector3 target;
        private float waitUntil;

        public void Init(MonsterModelLibrary.Critter critterData, int seed)
        {
            critter = critterData;
            random = new System.Random(seed);
            transform.localPosition = RandomSpot();

            var model = Instantiate(critter.model, transform, false);
            foreach (var collider in model.GetComponentsInChildren<Collider>()) Destroy(collider);
            animator = model.GetComponentInChildren<Animator>();
            if (animator == null) animator = model.AddComponent<Animator>();
            animator.applyRootMotion = false;
            FitHeight(model);

            target = RandomSpot();
            waitUntil = Time.time + (float)random.NextDouble() * 3f;
            Play(critter.idle);
        }

        /// <summary>Un punto del prado dentro de la valla pero fuera de la mesa.</summary>
        private Vector3 RandomSpot()
        {
            while (true)
            {
                float x = (float)(random.NextDouble() * 20.0 - 10.0);
                float z = (float)(random.NextDouble() * 17.0 - 9.5);
                if (Mathf.Abs(x) < 7.3f && Mathf.Abs(z) < 5f) continue;
                return new Vector3(x, Ground, z);
            }
        }

        private void FitHeight(GameObject model)
        {
            var renderers = model.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) return;
            var bounds = renderers[0].bounds;
            foreach (var r in renderers) bounds.Encapsulate(r.bounds);
            if (bounds.size.y < 0.0001f) return;
            model.transform.localScale *= Height / bounds.size.y;
            bounds = renderers[0].bounds;
            foreach (var r in renderers) bounds.Encapsulate(r.bounds);
            model.transform.position += Vector3.up * (transform.position.y - bounds.min.y);
        }

        private void Update()
        {
            if (Time.time < waitUntil) return;

            var toTarget = target - transform.localPosition;
            toTarget.y = 0f;
            if (toTarget.magnitude < 0.1f)
            {
                // Llegó: se queda quieto un rato y elige otro sitio.
                Play(critter.idle);
                waitUntil = Time.time + 2f + (float)random.NextDouble() * 4f;
                target = RandomSpot();
                return;
            }

            Play(critter.walk != null ? critter.walk : critter.idle);
            var step = toTarget.normalized * Speed * Time.deltaTime;
            transform.localPosition += Vector3.ClampMagnitude(step, toTarget.magnitude);
            transform.localRotation = Quaternion.Slerp(transform.localRotation, Quaternion.LookRotation(toTarget, Vector3.up), 6f * Time.deltaTime);
        }

        private void LateUpdate()
        {
            if (!current.IsValid() || currentClip == null) return;
            double time = current.GetTime();
            if (time >= currentClip.length) current.SetTime(time % currentClip.length);
        }

        private void Play(AnimationClip clip)
        {
            if (clip == null || clip == currentClip || animator == null) return;
            if (!graph.IsValid())
            {
                graph = PlayableGraph.Create($"{name} animaciones");
                graph.SetTimeUpdateMode(DirectorUpdateMode.GameTime);
                output = AnimationPlayableOutput.Create(graph, "Animation", animator);
            }
            else if (current.IsValid())
            {
                current.Destroy();
            }
            current = AnimationClipPlayable.Create(graph, clip);
            current.SetApplyFootIK(false);
            output.SetSourcePlayable(current);
            currentClip = clip;
            graph.Play();
        }

        private void OnDestroy()
        {
            if (graph.IsValid()) graph.Destroy();
        }
    }
}
