using DG.Tweening;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace CardDuel.Prototype
{
    /// <summary>
    /// Acabado visual: post-procesado de URP (bloom, tonemapping neutro, etalonaje luminoso y
    /// colorido, viñeta, grano), profundidad de campo para los primeros planos y
    /// partículas flotando (polen y motas de polvo dorado).
    /// </summary>
    public static class AestheticFx
    {
        private static DepthOfField depthOfField;
        private static ColorAdjustments colorAdjustments;
        private static Vignette vignetteFx;

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
            // Neutral oscurece menos que ACES y deja los colores más limpios.
            tonemapping.mode.Override(TonemappingMode.Neutral);

            var bloom = profile.Add<Bloom>(true);
            bloom.threshold.Override(1.1f);
            bloom.intensity.Override(0.55f);
            bloom.scatter.Override(0.7f);
            bloom.tint.Override(new Color(1f, 0.96f, 0.88f));

            // Pleno día y colores vivos.
            var color = colorAdjustments = profile.Add<ColorAdjustments>(true);
            color.postExposure.Override(0.2f);
            color.contrast.Override(10f);
            color.saturation.Override(18f);

            // Sombras levantadas (sin zonas negras) y luces algo cálidas.
            var tones = profile.Add<ShadowsMidtonesHighlights>(true);
            tones.shadows.Override(new Vector4(1f, 1.02f, 1.08f, 0.08f));
            tones.midtones.Override(new Vector4(1f, 1f, 1f, 0.04f));
            tones.highlights.Override(new Vector4(1.04f, 1.02f, 0.97f, 0f));

            var vignette = vignetteFx = profile.Add<Vignette>(true);
            vignette.intensity.Override(0.12f);
            vignette.smoothness.Override(0.5f);
            vignette.color.Override(new Color(0.15f, 0.12f, 0.08f));

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

        /// <summary>Derrota: la imagen pierde el color, se oscurece un poco y aparece una viñeta roja.</summary>
        public static void DefeatMood(float duration)
        {
            if (colorAdjustments == null || vignetteFx == null) return;
            float saturation = colorAdjustments.saturation.value;
            float exposure = colorAdjustments.postExposure.value;
            float vignette = vignetteFx.intensity.value;
            vignetteFx.color.Override(new Color(0.45f, 0.02f, 0.02f));
            DOTween.To(() => 0f, t =>
            {
                colorAdjustments.saturation.Override(Mathf.Lerp(saturation, -70f, t));
                colorAdjustments.postExposure.Override(Mathf.Lerp(exposure, -0.25f, t));
                vignetteFx.intensity.Override(Mathf.Lerp(vignette, 0.5f, t));
            }, 1f, duration).SetEase(Ease.OutCubic);
        }

        /// <summary>Victoria: lluvia de confeti de colores delante de la cámara.</summary>
        public static void Confetti(Camera cam)
        {
            var go = new GameObject("Confetti");
            go.transform.SetParent(cam.transform, false);
            go.transform.localPosition = new Vector3(0f, 2.2f, 4f);
            go.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            var system = go.AddComponent<ParticleSystem>();
            system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = system.main;
            main.duration = 3f;
            main.loop = false;
            main.startLifetime = new ParticleSystem.MinMaxCurve(2.5f, 4f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.5f, 2.5f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.11f);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.gravityModifier = 0.25f;
            main.maxParticles = 600;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            var gradient = new Gradient();
            gradient.SetKeys(
                new[]
                {
                    new GradientColorKey(new Color(1f, 0.85f, 0.2f), 0f), new GradientColorKey(new Color(1f, 0.3f, 0.4f), 0.25f),
                    new GradientColorKey(new Color(0.3f, 0.7f, 1f), 0.5f), new GradientColorKey(new Color(0.5f, 1f, 0.4f), 0.75f),
                    new GradientColorKey(new Color(0.85f, 0.5f, 1f), 1f),
                },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) });
            main.startColor = new ParticleSystem.MinMaxGradient(gradient) { mode = ParticleSystemGradientMode.RandomColor };

            var emission = system.emission;
            emission.rateOverTime = 70f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 160) });

            var shape = system.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(6f, 1f, 0.1f);

            var rotation = system.rotationOverLifetime;
            rotation.enabled = true;
            rotation.z = new ParticleSystem.MinMaxCurve(-4f, 4f);

            var noise = system.noise;
            noise.enabled = true;
            noise.strength = 0.6f;
            noise.frequency = 0.6f;

            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = ConfettiMaterial();
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            system.Play();
        }

        private static Material confetti;

        /// <summary>Partículas opacas de color (cuadraditos de papel).</summary>
        private static Material ConfettiMaterial()
        {
            if (confetti != null) return confetti;
            var shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            confetti = new Material(shader != null ? shader : Shader.Find("Sprites/Default"));
            confetti.SetTexture("_BaseMap", Texture2D.whiteTexture);
            confetti.mainTexture = Texture2D.whiteTexture;
            return confetti;
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
