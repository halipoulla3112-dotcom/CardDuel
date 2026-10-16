using UnityEngine;
using UnityEngine.Rendering;

namespace CardDuel.Prototype
{
    /// <summary>
    /// Granja al aire libre construida por código alrededor del tablero: pradera,
    /// colinas, granero rojo, silo, molino, casa de campo, vallas, árboles,
    /// balas de paja, huerto, flores y nubes, a pleno sol y con bruma lejana.
    /// </summary>
    public static class FarmEnvironment
    {
        private const float Ground = -1.4f;

        private static readonly Color BarnRed = new Color(0.78f, 0.17f, 0.12f);
        private static readonly Color TrimWhite = new Color(0.96f, 0.94f, 0.9f);
        private static readonly Color RoofSlate = new Color(0.32f, 0.3f, 0.34f);
        private static readonly Color RoofTerracotta = new Color(0.78f, 0.36f, 0.22f);
        private static readonly Color Cream = new Color(0.98f, 0.9f, 0.74f);
        private static readonly Color Bark = new Color(0.42f, 0.27f, 0.15f);
        private static readonly Color[] Leaves =
        {
            new Color(0.3f, 0.62f, 0.2f), new Color(0.42f, 0.7f, 0.22f), new Color(0.22f, 0.5f, 0.2f), new Color(0.55f, 0.72f, 0.25f),
        };
        private static readonly Color[] FlowerColors =
        {
            new Color(1f, 0.25f, 0.3f), new Color(1f, 0.85f, 0.2f), new Color(0.97f, 0.97f, 0.97f),
            new Color(0.75f, 0.4f, 1f), new Color(1f, 0.55f, 0.75f), new Color(0.35f, 0.6f, 1f),
        };

        private static System.Random random;

        public static void Build(Transform parent)
        {
            random = new System.Random(7);
            var root = new GameObject("Farm").transform;
            root.SetParent(parent, false);

            BuildGround(root);
            BuildHills(root);
            BuildTableLegs(root);
            BuildBarn(root, new Vector3(0f, Ground, 17f));
            BuildSilo(root, new Vector3(8.2f, Ground, 18f));
            BuildWindmill(root, new Vector3(-14f, Ground, 15f));
            BuildFarmhouse(root, new Vector3(-9f, Ground, -19f));
            BuildFence(root);
            BuildTrees(root);
            BuildHay(root);
            BuildCrops(root, new Rect(-27f, -9f, 9f, 16f));
            BuildFlowers(root);
            BuildClouds(root);
            SetupSunnyDay();
        }

        // ------------------------------------------------------------ Paisaje

        private static void BuildGround(Transform root)
        {
            const float size = 260f;
            Primitives.Box("Meadow", root, new Vector3(0f, Ground - 0.1f, 0f), new Vector3(size, 0.2f, size),
                EnvironmentTextures.Material(EnvironmentTextures.Grass, Vector2.one * size / 4f, Color.white));
            Primitives.Box("Path", root, new Vector3(0f, Ground + 0.005f, 11f), new Vector3(3f, 0.02f, 9f),
                EnvironmentTextures.Material(EnvironmentTextures.Dirt, new Vector2(1.5f, 4.5f), Color.white));
        }

        /// <summary>Colinas verdes a lo lejos (esferas aplastadas medio enterradas).</summary>
        private static void BuildHills(Transform root)
        {
            var hills = new[]
            {
                (new Vector3(-70f, Ground - 6f, 85f), new Vector3(90f, 26f, 50f)),
                (new Vector3(10f, Ground - 8f, 110f), new Vector3(120f, 34f, 60f)),
                (new Vector3(80f, Ground - 6f, 80f), new Vector3(80f, 24f, 50f)),
                (new Vector3(-90f, Ground - 6f, -40f), new Vector3(70f, 22f, 90f)),
                (new Vector3(95f, Ground - 6f, -30f), new Vector3(70f, 20f, 80f)),
                (new Vector3(0f, Ground - 8f, -95f), new Vector3(140f, 30f, 50f)),
            };
            for (int i = 0; i < hills.Length; i++)
            {
                var hill = Primitive(PrimitiveType.Sphere, "Hill", root, hills[i].Item1, hills[i].Item2);
                hill.GetComponent<MeshRenderer>().sharedMaterial =
                    EnvironmentTextures.Material(EnvironmentTextures.Grass, new Vector2(30f, 12f), Color.Lerp(Color.white, Leaves[i % Leaves.Length], 0.35f));
            }
        }

        /// <summary>Patas de la mesa de madera sobre la que está el tablero.</summary>
        private static void BuildTableLegs(Transform root)
        {
            var woodColor = new Color(0.85f, 0.75f, 0.65f);
            Box(root, "TableApron", new Vector3(0f, -0.38f, 0f), new Vector3(10.2f, 0.36f, 6.4f),
                EnvironmentTextures.Wood, new Vector2(4f, 1f), woodColor * 0.85f);
            float height = -0.2f - Ground;
            foreach (var leg in new[] { new Vector2(-4.8f, -2.9f), new Vector2(4.8f, -2.9f), new Vector2(-4.8f, 2.9f), new Vector2(4.8f, 2.9f) })
                Box(root, "TableLeg", new Vector3(leg.x, Ground + height * 0.5f, leg.y), new Vector3(0.55f, height, 0.55f),
                    EnvironmentTextures.Wood, new Vector2(0.5f, 1.5f), woodColor * 0.75f);
        }

        // ------------------------------------------------------- Edificios

        /// <summary>Granero rojo con tejado a dos aguas, portón con la "X" blanca y ventana del pajar.</summary>
        private static void BuildBarn(Transform root, Vector3 basePoint)
        {
            const float width = 9f, height = 5.5f, depth = 7f, pitch = 35f;
            var barn = Group(root, "Barn", basePoint);

            Box(barn, "Walls", new Vector3(0f, height * 0.5f, 0f), new Vector3(width, height, depth), EnvironmentTextures.Planks, new Vector2(4f, 1f), BarnRed);
            Gable(barn, "GableFront", height, width * 0.5f, pitch, -depth * 0.5f, BarnRed);
            Gable(barn, "GableBack", height, width * 0.5f, pitch, depth * 0.5f, BarnRed);
            Roof(barn, height, width * 0.5f, pitch, depth + 0.6f, RoofSlate);

            // Esquinas y portón con molduras blancas.
            float front = -depth * 0.5f - 0.06f;
            foreach (float x in new[] { -width * 0.5f, width * 0.5f })
                Primitives.Box("CornerTrim", barn, new Vector3(x, height * 0.5f, front), new Vector3(0.3f, height, 0.14f), Primitives.Lit(TrimWhite));
            DoorWithCross(barn, new Vector3(0f, 1.9f, front), 3.6f, 3.8f);
            Primitives.Box("LoftFrame", barn, new Vector3(0f, height + 1.2f, front), new Vector3(1.7f, 1.5f, 0.12f), Primitives.Lit(TrimWhite));
            Primitives.Box("Loft", barn, new Vector3(0f, height + 1.2f, front - 0.02f), new Vector3(1.3f, 1.1f, 0.12f), Primitives.Lit(new Color(0.2f, 0.12f, 0.08f)));
        }

        private static void DoorWithCross(Transform parent, Vector3 center, float width, float height)
        {
            var trim = Primitives.Lit(TrimWhite);
            Primitives.Box("Door", parent, center, new Vector3(width, height, 0.1f), Primitives.Lit(BarnRed * 0.85f));
            Primitives.Box("DoorTop", parent, center + new Vector3(0f, height * 0.5f, -0.05f), new Vector3(width + 0.2f, 0.22f, 0.1f), trim);
            Primitives.Box("DoorLeft", parent, center + new Vector3(-width * 0.5f, 0f, -0.05f), new Vector3(0.22f, height, 0.1f), trim);
            Primitives.Box("DoorRight", parent, center + new Vector3(width * 0.5f, 0f, -0.05f), new Vector3(0.22f, height, 0.1f), trim);
            Primitives.Box("DoorMiddle", parent, center + new Vector3(0f, 0f, -0.05f), new Vector3(0.18f, height, 0.1f), trim);
            float diagonal = Mathf.Sqrt(width * width * 0.25f + height * height);
            float angle = Mathf.Atan2(height, width * 0.5f) * Mathf.Rad2Deg;
            foreach (float side in new[] { -1f, 1f })
                foreach (float sign in new[] { -1f, 1f })
                    Primitives.Box("DoorCross", parent, center + new Vector3(side * width * 0.25f, 0f, -0.07f), new Vector3(diagonal, 0.16f, 0.08f), trim)
                        .localRotation = Quaternion.Euler(0f, 0f, sign * angle);
        }

        /// <summary>Silo cilíndrico con cúpula.</summary>
        private static void BuildSilo(Transform root, Vector3 basePoint)
        {
            const float height = 9f, radius = 1.7f;
            var silo = Primitive(PrimitiveType.Cylinder, "Silo", root, basePoint + Vector3.up * height * 0.5f, new Vector3(radius * 2f, height * 0.5f, radius * 2f));
            silo.GetComponent<MeshRenderer>().sharedMaterial = EnvironmentTextures.Material(EnvironmentTextures.Planks, new Vector2(6f, 2f), new Color(0.72f, 0.78f, 0.86f));
            var dome = Primitive(PrimitiveType.Sphere, "SiloDome", root, basePoint + Vector3.up * height, new Vector3(radius * 2f, radius * 1.3f, radius * 2f));
            dome.GetComponent<MeshRenderer>().sharedMaterial = Primitives.Lit(new Color(0.55f, 0.6f, 0.68f));
        }

        /// <summary>Molino de viento blanco con aspas que giran.</summary>
        private static void BuildWindmill(Transform root, Vector3 basePoint)
        {
            const float height = 7.5f;
            var mill = Group(root, "Windmill", basePoint);
            mill.localRotation = Quaternion.Euler(0f, 25f, 0f);
            Box(mill, "Tower", new Vector3(0f, height * 0.5f, 0f), new Vector3(2.2f, height, 2.2f), EnvironmentTextures.Planks, new Vector2(2f, 3f), TrimWhite);
            Roof(mill, height, 1.3f, 40f, 2.6f, RoofTerracotta);
            Gable(mill, "GableFront", height, 1.1f, 40f, -1.1f, TrimWhite);
            Gable(mill, "GableBack", height, 1.1f, 40f, 1.1f, TrimWhite);

            var rotor = new GameObject("Rotor").transform;
            rotor.SetParent(mill, false);
            rotor.localPosition = new Vector3(0f, height - 0.6f, -1.35f);
            Primitives.Box("Hub", rotor, Vector3.zero, new Vector3(0.45f, 0.45f, 0.4f), Primitives.Lit(new Color(0.35f, 0.22f, 0.12f)));
            for (int i = 0; i < 4; i++)
            {
                var arm = new GameObject("Blade").transform;
                arm.SetParent(rotor, false);
                arm.localRotation = Quaternion.Euler(0f, 0f, i * 90f);
                Box(arm, "Spar", new Vector3(0f, 2.2f, -0.05f), new Vector3(0.14f, 4.2f, 0.08f), EnvironmentTextures.Wood, Vector2.one, Color.white);
                Box(arm, "Sail", new Vector3(0.35f, 2.5f, -0.02f), new Vector3(0.6f, 3.2f, 0.04f), EnvironmentTextures.Planks, new Vector2(1f, 2f), Cream);
            }
            rotor.gameObject.AddComponent<Spinner>().degreesPerSecond = 35f;
        }

        /// <summary>Casa de campo color crema con tejado rojizo y chimenea.</summary>
        private static void BuildFarmhouse(Transform root, Vector3 basePoint)
        {
            const float width = 7f, height = 3.6f, depth = 5f;
            var house = Group(root, "Farmhouse", basePoint);
            house.localRotation = Quaternion.Euler(0f, 160f, 0f);
            Box(house, "Walls", new Vector3(0f, height * 0.5f, 0f), new Vector3(width, height, depth), EnvironmentTextures.Planks, new Vector2(4f, 1f), Cream);
            Gable(house, "GableFront", height, width * 0.5f, 32f, -depth * 0.5f, Cream);
            Gable(house, "GableBack", height, width * 0.5f, 32f, depth * 0.5f, Cream);
            Roof(house, height, width * 0.5f, 32f, depth + 0.6f, RoofTerracotta);
            Box(house, "Chimney", new Vector3(width * 0.25f, height + 2f, 0.8f), new Vector3(0.7f, 2.4f, 0.7f), EnvironmentTextures.RedBricks, new Vector2(0.5f, 1f), Color.white);

            float front = -depth * 0.5f - 0.06f;
            Primitives.Box("Door", house, new Vector3(0f, 1.1f, front), new Vector3(1.1f, 2.2f, 0.1f), Primitives.Lit(new Color(0.35f, 0.5f, 0.3f)));
            foreach (float x in new[] { -2.2f, 2.2f })
            {
                Primitives.Box("WindowFrame", house, new Vector3(x, 2f, front), new Vector3(1.2f, 1.1f, 0.1f), Primitives.Lit(TrimWhite));
                Primitives.Box("Window", house, new Vector3(x, 2f, front - 0.02f), new Vector3(0.9f, 0.8f, 0.1f), Primitives.Lit(new Color(0.55f, 0.75f, 0.9f)));
            }
        }

        // ---------------------------------------------------------- Detalles

        /// <summary>Valla de madera alrededor del prado, con un hueco en el camino al granero.</summary>
        private static void BuildFence(Transform root)
        {
            const float minX = -13f, maxX = 13f, minZ = -12f, maxZ = 11f;
            FenceLine(root, new Vector3(minX, 0f, minZ), new Vector3(maxX, 0f, minZ));
            FenceLine(root, new Vector3(minX, 0f, minZ), new Vector3(minX, 0f, maxZ));
            FenceLine(root, new Vector3(maxX, 0f, minZ), new Vector3(maxX, 0f, maxZ));
            FenceLine(root, new Vector3(minX, 0f, maxZ), new Vector3(-2f, 0f, maxZ));
            FenceLine(root, new Vector3(2f, 0f, maxZ), new Vector3(maxX, 0f, maxZ));
        }

        private static void FenceLine(Transform root, Vector3 from, Vector3 to)
        {
            var wood = EnvironmentTextures.Material(EnvironmentTextures.Wood, new Vector2(0.4f, 1f), new Color(0.95f, 0.85f, 0.75f));
            float length = Vector3.Distance(from, to);
            int posts = Mathf.Max(2, Mathf.RoundToInt(length / 2f) + 1);
            var direction = (to - from).normalized;
            for (int i = 0; i < posts; i++)
            {
                var p = Vector3.Lerp(from, to, i / (posts - 1f));
                Primitives.Box("Post", root, new Vector3(p.x, Ground + 0.6f, p.z), new Vector3(0.2f, 1.2f, 0.2f), wood);
            }
            var middle = (from + to) * 0.5f;
            var rotation = Quaternion.LookRotation(direction, Vector3.up);
            foreach (float y in new[] { 0.45f, 0.95f })
                Primitives.Box("Rail", root, new Vector3(middle.x, Ground + y, middle.z), new Vector3(0.08f, 0.14f, length), wood).localRotation = rotation;
        }

        /// <summary>Árboles redondeados y pinos alrededor del prado.</summary>
        private static void BuildTrees(Transform root)
        {
            var spots = new[]
            {
                new Vector2(-17f, -6f), new Vector2(-19f, 3f), new Vector2(-17.5f, 9f), new Vector2(17f, -4f), new Vector2(19f, 5f),
                new Vector2(16f, 11f), new Vector2(-7f, 25f), new Vector2(12f, 26f), new Vector2(-24f, 18f), new Vector2(25f, 15f),
                new Vector2(-26f, -14f), new Vector2(25f, -11f), new Vector2(3f, -20f), new Vector2(14f, -18f), new Vector2(-21f, 27f),
                new Vector2(30f, 25f), new Vector2(-32f, 6f), new Vector2(33f, 0f), new Vector2(20f, -24f), new Vector2(-16f, -24f),
            };
            for (int i = 0; i < spots.Length; i++)
            {
                var basePoint = new Vector3(spots[i].x, Ground, spots[i].y);
                float scale = 0.85f + (float)random.NextDouble() * 0.5f;
                if (i % 3 == 2) Pine(root, basePoint, scale);
                else RoundTree(root, basePoint, scale);
            }
        }

        private static void RoundTree(Transform root, Vector3 basePoint, float scale)
        {
            var trunk = Primitive(PrimitiveType.Cylinder, "Trunk", root, basePoint + Vector3.up * 1.3f * scale, new Vector3(0.5f, 1.3f, 0.5f) * scale);
            trunk.GetComponent<MeshRenderer>().sharedMaterial = Primitives.Lit(Bark);
            var leaves = Primitives.Lit(Leaves[random.Next(Leaves.Length)]);
            var blobs = new[] { new Vector3(0f, 3.4f, 0f), new Vector3(0.9f, 2.9f, 0.3f), new Vector3(-0.8f, 3f, -0.4f), new Vector3(0.1f, 2.8f, -0.9f) };
            foreach (var blob in blobs)
            {
                float size = (1.9f + (float)random.NextDouble() * 0.7f) * scale;
                var sphere = Primitive(PrimitiveType.Sphere, "Leaves", root, basePoint + blob * scale, Vector3.one * size);
                sphere.GetComponent<MeshRenderer>().sharedMaterial = leaves;
            }
        }

        private static void Pine(Transform root, Vector3 basePoint, float scale)
        {
            var trunk = Primitive(PrimitiveType.Cylinder, "Trunk", root, basePoint + Vector3.up * 0.8f * scale, new Vector3(0.45f, 0.8f, 0.45f) * scale);
            trunk.GetComponent<MeshRenderer>().sharedMaterial = Primitives.Lit(Bark);
            var needles = Primitives.Lit(new Color(0.16f, 0.42f, 0.24f));
            for (int i = 0; i < 3; i++)
            {
                var layer = new GameObject("Needles").transform;
                layer.SetParent(root, false);
                layer.localPosition = basePoint + Vector3.up * (1.3f + i * 1.3f) * scale;
                layer.localScale = Vector3.one * scale * (1f - i * 0.22f);
                layer.gameObject.AddComponent<MeshFilter>().sharedMesh = ConeMesh(1.8f, 2.6f, 10);
                layer.gameObject.AddComponent<MeshRenderer>().sharedMaterial = needles;
            }
        }

        /// <summary>Balas de paja redondas y una pila de balas rectangulares.</summary>
        private static void BuildHay(Transform root)
        {
            var hay = EnvironmentTextures.Material(EnvironmentTextures.Hay, new Vector2(3f, 1f), Color.white);
            var rolls = new[] { new Vector3(-5.5f, 0f, 13f), new Vector3(-7.2f, 0f, 12.2f), new Vector3(5.8f, 0f, 12.6f), new Vector3(-10.5f, 0f, -4f), new Vector3(10.8f, 0f, 2f) };
            for (int i = 0; i < rolls.Length; i++)
            {
                var roll = Primitive(PrimitiveType.Cylinder, "HayRoll", root, new Vector3(rolls[i].x, Ground + 0.75f, rolls[i].z), new Vector3(1.5f, 0.6f, 1.5f));
                roll.transform.localRotation = Quaternion.Euler(0f, i * 37f, 90f);
                roll.GetComponent<MeshRenderer>().sharedMaterial = hay;
            }
            var stack = new[] { new Vector3(8.5f, 0.35f, 9f), new Vector3(9.7f, 0.35f, 9f), new Vector3(9.1f, 1.05f, 9f) };
            foreach (var bale in stack)
                Box(root, "HayBale", new Vector3(bale.x, Ground + bale.y, bale.z), new Vector3(1.15f, 0.7f, 0.75f), EnvironmentTextures.Hay, Vector2.one, Color.white);
        }

        /// <summary>Huerto de trigo: hileras de espigas sobre tierra.</summary>
        private static void BuildCrops(Transform root, Rect area)
        {
            Box(root, "Field", new Vector3(area.center.x, Ground + 0.01f, area.center.y), new Vector3(area.width, 0.04f, area.height),
                EnvironmentTextures.Dirt, new Vector2(area.width / 2f, area.height / 2f), Color.white);
            var wheat = Primitives.Lit(new Color(0.92f, 0.76f, 0.3f));
            var green = Primitives.Lit(new Color(0.55f, 0.72f, 0.25f));
            for (float x = area.xMin + 0.6f; x < area.xMax - 0.4f; x += 1.1f)
                for (float z = area.yMin + 0.5f; z < area.yMax - 0.4f; z += 0.55f)
                {
                    float height = 0.7f + (float)random.NextDouble() * 0.35f;
                    Primitives.Box("Wheat", root, new Vector3(x + (float)random.NextDouble() * 0.2f, Ground + height * 0.5f, z), new Vector3(0.14f, height, 0.14f),
                        random.NextDouble() < 0.8 ? wheat : green);
                }
        }

        /// <summary>Flores de colores sueltas por el prado, fuera de la mesa.</summary>
        private static void BuildFlowers(Transform root)
        {
            var materials = new Material[FlowerColors.Length];
            for (int i = 0; i < materials.Length; i++) materials[i] = Primitives.Lit(FlowerColors[i]);
            var stem = Primitives.Lit(new Color(0.25f, 0.55f, 0.2f));
            int placed = 0;
            while (placed < 160)
            {
                float x = (float)(random.NextDouble() * 25.0 - 12.5);
                float z = (float)(random.NextDouble() * 22.0 - 11.5);
                if (Mathf.Abs(x) < 5.8f && Mathf.Abs(z) < 3.9f) continue;
                if (Mathf.Abs(x) < 1.8f && z > 6.5f) continue;
                Primitives.Box("Stem", root, new Vector3(x, Ground + 0.12f, z), new Vector3(0.04f, 0.24f, 0.04f), stem);
                var flower = Primitive(PrimitiveType.Sphere, "Flower", root, new Vector3(x, Ground + 0.26f, z), Vector3.one * 0.16f);
                flower.GetComponent<MeshRenderer>().sharedMaterial = materials[random.Next(materials.Length)];
                flower.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
                placed++;
            }
        }

        /// <summary>Nubes esponjosas que se desplazan despacio por el cielo.</summary>
        private static void BuildClouds(Transform root)
        {
            var white = Primitives.Lit(new Color(1f, 0.98f, 0.96f));
            for (int i = 0; i < 9; i++)
            {
                var cloud = new GameObject("Cloud").transform;
                cloud.SetParent(root, false);
                float angle = i * 40f * Mathf.Deg2Rad;
                cloud.localPosition = new Vector3(Mathf.Cos(angle) * 85f, 38f + (float)random.NextDouble() * 12f, Mathf.Sin(angle) * 85f + 20f);
                for (int p = 0; p < 5; p++)
                {
                    var puff = Primitive(PrimitiveType.Sphere, "Puff", cloud,
                        new Vector3((p - 2) * 4.5f, (float)random.NextDouble() * 2f, (float)random.NextDouble() * 3f),
                        new Vector3(9f, 5f, 7f) * (0.7f + (float)random.NextDouble() * 0.5f));
                    var renderer = puff.GetComponent<MeshRenderer>();
                    renderer.sharedMaterial = white;
                    renderer.shadowCastingMode = ShadowCastingMode.Off;
                }
                cloud.gameObject.AddComponent<Drifter>().speed = 1.2f + (float)random.NextDouble();
            }
        }

        // ---------------------------------------------------------- Ambiente

        /// <summary>Pleno día: cielo azul, sol alto detrás de ti (ilumina el tablero de frente), mucha luz ambiente y bruma lejana.</summary>
        private static void SetupSunnyDay()
        {
            var skyShader = Shader.Find("Skybox/Procedural");
            if (skyShader != null)
            {
                var sky = new Material(skyShader);
                sky.SetColor("_SkyTint", new Color(0.4f, 0.6f, 1f));
                sky.SetColor("_GroundColor", new Color(0.55f, 0.68f, 0.42f));
                sky.SetFloat("_AtmosphereThickness", 0.75f);
                sky.SetFloat("_Exposure", 1.7f);
                sky.SetFloat("_SunSize", 0.06f);
                sky.SetFloat("_SunSizeConvergence", 4f);
                RenderSettings.skybox = sky;
            }

            RenderSettings.ambientMode = AmbientMode.Trilight;
            // Pleno día: mucha luz de cielo azul y rebote verde claro del prado.
            RenderSettings.ambientSkyColor = new Color(0.9f, 0.95f, 1.05f);
            RenderSettings.ambientEquatorColor = new Color(0.85f, 0.88f, 0.8f);
            RenderSettings.ambientGroundColor = new Color(0.55f, 0.62f, 0.45f);
            RenderSettings.ambientIntensity = 1.2f;

            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = new Color(0.8f, 0.88f, 0.98f);
            RenderSettings.fogStartDistance = 70f;
            RenderSettings.fogEndDistance = 220f;

            foreach (var light in Object.FindObjectsByType<Light>())
            {
                if (light.type != LightType.Directional) continue;
                light.color = new Color(1f, 0.96f, 0.88f);
                light.intensity = 2.1f;
                light.shadows = LightShadows.Soft;
                light.shadowStrength = 0.55f;
                light.transform.rotation = Quaternion.Euler(52f, -28f, 0f);
                RenderSettings.sun = light;
            }

            DynamicGI.UpdateEnvironment();
        }

        // ---------------------------------------------------------- Ayudas

        private static Transform Group(Transform parent, string name, Vector3 position)
        {
            var group = new GameObject(name).transform;
            group.SetParent(parent, false);
            group.localPosition = position;
            return group;
        }

        private static Transform Box(Transform parent, string name, Vector3 position, Vector3 size, Texture2D texture, Vector2 tiling, Color tint)
        {
            return Primitives.Box(name, parent, position, size, EnvironmentTextures.Material(texture, tiling, tint));
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

        /// <summary>Tejado a dos aguas: dos planchas inclinadas que se juntan en la cumbrera.</summary>
        private static void Roof(Transform parent, float wallTop, float halfWidth, float pitch, float depth, Color color)
        {
            float rise = halfWidth * Mathf.Tan(pitch * Mathf.Deg2Rad);
            float slope = halfWidth / Mathf.Cos(pitch * Mathf.Deg2Rad) + 0.45f;
            var material = EnvironmentTextures.Material(EnvironmentTextures.Planks, new Vector2(4f, 2f), color);
            foreach (float side in new[] { -1f, 1f })
            {
                var plate = Primitives.Box("Roof", parent, new Vector3(side * halfWidth * 0.5f, wallTop + rise * 0.5f + 0.1f, 0f),
                    new Vector3(slope, 0.25f, depth), material);
                plate.localRotation = Quaternion.Euler(0f, 0f, -side * pitch);
            }
        }

        /// <summary>Triángulo de fachada bajo el tejado (frontón).</summary>
        private static void Gable(Transform parent, string name, float wallTop, float halfWidth, float pitch, float z, Color color)
        {
            float rise = halfWidth * Mathf.Tan(pitch * Mathf.Deg2Rad);
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = new Vector3(0f, wallTop, z);
            var mesh = new Mesh { name = "Gable" };
            mesh.vertices = new[]
            {
                new Vector3(-halfWidth, 0f, -0.05f), new Vector3(0f, rise, -0.05f), new Vector3(halfWidth, 0f, -0.05f),
                new Vector3(-halfWidth, 0f, 0.05f), new Vector3(0f, rise, 0.05f), new Vector3(halfWidth, 0f, 0.05f),
            };
            mesh.triangles = new[] { 0, 1, 2, 5, 4, 3 };
            mesh.uv = new[] { Vector2.zero, new Vector2(0.5f, 1f), Vector2.right, Vector2.zero, new Vector2(0.5f, 1f), Vector2.right };
            mesh.RecalculateNormals();
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = EnvironmentTextures.Material(EnvironmentTextures.Planks, new Vector2(4f, 1f), color);
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

    /// <summary>Gira sobre su eje Z (aspas del molino).</summary>
    public class Spinner : MonoBehaviour
    {
        public float degreesPerSecond = 30f;

        private void Update() => transform.Rotate(0f, 0f, degreesPerSecond * Time.deltaTime, Space.Self);
    }

    /// <summary>Se desplaza en X y vuelve a empezar al salir del cielo visible (nubes).</summary>
    public class Drifter : MonoBehaviour
    {
        public float speed = 1f;

        private void Update()
        {
            var p = transform.localPosition;
            p.x += speed * Time.deltaTime;
            if (p.x > 120f) p.x = -120f;
            transform.localPosition = p;
        }
    }
}
