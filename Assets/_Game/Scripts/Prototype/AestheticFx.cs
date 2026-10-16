using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace CardDuel.Prototype
{
    /// <summary>
    /// Acabado visual: post-procesado de URP (bloom, ACES, etalonaje cálido y
    /// colorido, viñeta, grano), profundidad de campo para los primeros planos y
    /// partículas flotando (polen y motas de polvo dorado).
    /// </summary>
    public static class AestheticFx
    {
        private static DepthOfField depthOfField;

        public static void Setup(Camera cam, Transform parent)
        {
            cam.GetUniversalAdditionalCameraData().renderPostProcessing = true;
            BuildVolume(parent);
            BuildPollen(parent);
            BuildDust(parent);
        }

        /// <summary>En los primeros planos el fondo se desenfoca para que destaque el monstruo.</summary>
        public static void SetCloseUpFocus(bool on)
        {
            if (depthOfField == null) return;
            depthOfField.mode.Override(on ? DepthOfFieldMode.Gaussian : DepthOfFieldMode.Off);
        }

        private static void BuildVolume(Transform parent)
        {
            var go = new GameObject("PostFX");
            go.transform.SetParent(parent, false);
            var volume = go.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.priority = 10f;
            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            volume.profile = profile;

            var tonemapping = profile.Add<Tonemapping>(true);
            tonemapping.mode.Override(TonemappingMode.ACES);

            var bloom = profile.Add<Bloom>(true);
            bloom.threshold.Override(1f);
            bloom.intensity.Override(0.7f);
            bloom.scatter.Override(0.7f);
            bloom.tint.Override(new Color(1f, 0.92f, 0.8f));

            // Colores vivos de tarde de verano.
            var color = profile.Add<ColorAdjustments>(true);
            color.postExposure.Override(0.15f);
            color.contrast.Override(12f);
            color.saturation.Override(24f);

            // Sombras ligeramente azuladas y luces doradas.
            var tones = profile.Add<ShadowsMidtonesHighlights>(true);
            tones.shadows.Override(new Vector4(0.96f, 0.99f, 1.08f, 0f));
            tones.midtones.Override(new Vector4(1f, 1f, 0.98f, 0f));
            tones.highlights.Override(new Vector4(1.07f, 1.02f, 0.93f, 0.02f));

            var vignette = profile.Add<Vignette>(true);
            vignette.intensity.Override(0.22f);
            vignette.smoothness.Override(0.5f);
            vignette.color.Override(new Color(0.12f, 0.07f, 0.03f));

            var grain = profile.Add<FilmGrain>(true);
            grain.type.Override(FilmGrainLookup.Thin1);
            grain.intensity.Override(0.12f);

            depthOfField = profile.Add<DepthOfField>(true);
            depthOfField.mode.Override(DepthOfFieldMode.Off);
            depthOfField.gaussianStart.Override(2.6f);
            depthOfField.gaussianEnd.Override(9f);
            depthOfField.gaussianMaxRadius.Override(1.2f);
            depthOfField.highQualitySampling.Override(true);
        }

        /// <summary>Polen y semillas de diente de león que suben despacio con la brisa.</summary>
        private static void BuildPollen(Transform parent)
        {
            var pollen = Particles("Pollen", parent, new Vector3(0f, -0.8f, 1f), new Vector3(24f, 0.5f, 20f), 20f);
            var main = pollen.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(6f, 10f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.1f, 0.3f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.035f, 0.08f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(1.6f, 1.5f, 1.2f, 0.8f), new Color(1.8f, 1.4f, 0.5f, 0.8f));
            main.gravityModifier = -0.01f;

            var shape = pollen.shape;
            shape.rotation = new Vector3(-90f, 0f, 0f);

            var noise = pollen.noise;
            noise.enabled = true;
            noise.strength = 0.5f;
            noise.frequency = 0.3f;
            noise.scrollSpeed = 0.2f;
            pollen.Play();
        }

        /// <summary>Motas de polvo dorado suspendidas a la luz de las antorchas.</summary>
        private static void BuildDust(Transform parent)
        {
            var dust = Particles("Dust", parent, new Vector3(0f, 2f, 0f), new Vector3(16f, 5f, 14f), 18f);
            var main = dust.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(6f, 10f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.02f, 0.08f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.02f, 0.045f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(1.2f, 1f, 0.7f, 0.5f), new Color(0.9f, 0.7f, 1.2f, 0.4f));

            var noise = dust.noise;
            noise.enabled = true;
            noise.strength = 0.15f;
            noise.frequency = 0.2f;
            dust.Play();
        }

        /// <summary>Sistema de partículas que aparece y se desvanece dentro de una caja, con mezcla aditiva.</summary>
        private static ParticleSystem Particles(string name, Transform parent, Vector3 center, Vector3 box, float rate)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = center;
            var system = go.AddComponent<ParticleSystem>();
            system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = system.main;
            main.loop = true;
            main.prewarm = true;
            main.maxParticles = 400;
            main.simulationSpace = ParticleSystemSimulationSpace.World;

            var emission = system.emission;
            emission.rateOverTime = rate;

            var shape = system.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = box;

            var fade = system.colorOverLifetime;
            fade.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.2f), new GradientAlphaKey(1f, 0.7f), new GradientAlphaKey(0f, 1f) });
            fade.color = gradient;

            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = AdditiveMaterial();
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            return system;
        }

        private static Material additive;
        private static Texture2D softDot;

        /// <summary>Punto de luz con el borde muy difuminado.</summary>
        private static Texture2D SoftDot()
        {
            if (softDot != null) return softDot;
            const int size = 64;
            softDot = new Texture2D(size, size, TextureFormat.RGBA32, true) { wrapMode = TextureWrapMode.Clamp };
            float r = size * 0.5f;
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float d = Mathf.Clamp01(new Vector2(x + 0.5f - r, y + 0.5f - r).magnitude / r);
                    float a = (1f - d) * (1f - d);
                    softDot.SetPixel(x, y, new Color(1f, 1f, 1f, a));
                }
            softDot.Apply();
            return softDot;
        }

        private static Material AdditiveMaterial()
        {
            if (additive != null) return additive;
            var shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            additive = new Material(shader != null ? shader : Shader.Find("Sprites/Default"));
            additive.SetTexture("_BaseMap", SoftDot());
            additive.mainTexture = SoftDot();
            // Transparente con mezcla aditiva: las partículas suman luz y brillan con el bloom.
            additive.SetFloat("_Surface", 1f);
            additive.SetFloat("_Blend", 2f);
            additive.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            additive.SetFloat("_DstBlend", (float)BlendMode.One);
            additive.SetFloat("_ZWrite", 0f);
            additive.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            additive.renderQueue = (int)RenderQueue.Transparent;
            return additive;
        }
    }
}
