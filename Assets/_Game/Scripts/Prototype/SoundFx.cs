using System;
using UnityEngine;

namespace CardDuel.Prototype
{
    /// <summary>
    /// Efectos de sonido generados por código (sin archivos de audio): el lanzamiento
    /// y la caída de la moneda y la invocación de un monstruo. Se sintetizan una vez
    /// la primera vez que se piden. Cuando haya sonidos grabados, basta con cambiar
    /// estos clips por AudioClips importados.
    /// </summary>
    public static class SoundFx
    {
        private const int SampleRate = 44100;

        private static AudioClip coinFlip;
        private static AudioClip coinLand;
        private static AudioClip summon;
        private static AudioClip death;
        private static AudioClip emote;
        private static AudioClip victory;
        private static AudioClip defeat;

        /// <summary>Fanfarria de victoria: arpegio mayor ascendente y acorde brillante final.</summary>
        public static AudioClip Victory => victory != null ? victory : victory = Build("Victory", 3.2f, (data, rng) =>
        {
            float[] notes = { 523.25f, 659.25f, 783.99f, 1046.5f };
            for (int i = 0; i < notes.Length; i++)
                AddChord(data, i * 0.16f, 0.7f, new[] { notes[i], notes[i] * 2f }, 0.5f);
            AddChord(data, 0.7f, 2.4f, new[] { 523.25f, 659.25f, 783.99f, 1046.5f, 1318.5f }, 0.7f);
            AddTing(data, 0.7f, 1.6f, 0.4f);
            AddWhoosh(data, rng, 0.6f, 0.8f, 800f, 6000f, 0.12f);
        });

        /// <summary>Derrota: notas menores que bajan y un golpe grave.</summary>
        public static AudioClip Defeat => defeat != null ? defeat : defeat = Build("Defeat", 3.4f, (data, rng) =>
        {
            float[] notes = { 440f, 349.23f, 293.66f, 220f };
            for (int i = 0; i < notes.Length; i++)
                AddFallingTone(data, i * 0.38f, 0.75f, notes[i], notes[i] * 0.94f, 0.45f);
            AddChord(data, 1.5f, 1.9f, new[] { 110f, 130.81f, 164.81f }, 0.5f);
            AddBoom(data, rng, 1.5f, 0.7f);
        });

        /// <summary>"Plin" corto de dos notas ascendentes al decir una frase.</summary>
        public static AudioClip Emote => emote != null ? emote : emote = Build("Emote", 0.5f, (data, rng) =>
        {
            AddChord(data, 0f, 0.35f, new[] { 784f, 1568f }, 0.6f);
            AddChord(data, 0.08f, 0.4f, new[] { 1046.5f, 2093f }, 0.6f);
        });

        /// <summary>Lamento con tono que cae, crujido que se desmorona y golpe sordo al caer el monstruo.</summary>
        public static AudioClip Death => death != null ? death : death = Build("Death", 1.8f, (data, rng) =>
        {
            AddFallingTone(data, 0f, 1.2f, 520f, 90f, 0.5f);
            AddWhoosh(data, rng, 0.1f, 1.1f, 3000f, 250f, 0.3f);
            AddBoom(data, rng, 0.9f, 0.6f);
        });

        /// <summary>Golpe de pulgar metálico y silbido de la moneda girando.</summary>
        public static AudioClip CoinFlip => coinFlip != null ? coinFlip : coinFlip = Build("CoinFlip", 1.4f, (data, rng) =>
        {
            AddTing(data, 0f, 1.15f, 0.9f);
            AddWhoosh(data, rng, 0.05f, 1.3f, 900f, 2600f, 0.12f);
        });

        /// <summary>La moneda cae y rebota dos veces sobre la mesa.</summary>
        public static AudioClip CoinLand => coinLand != null ? coinLand : coinLand = Build("CoinLand", 1.2f, (data, rng) =>
        {
            AddThud(data, rng, 0f, 0.35f);
            AddTing(data, 0f, 1f, 1f);
            AddTing(data, 0.14f, 1.03f, 0.55f);
            AddTing(data, 0.24f, 0.98f, 0.3f);
        });

        /// <summary>Golpe grave, barrido ascendente y acorde brillante al aparecer un monstruo.</summary>
        public static AudioClip Summon => summon != null ? summon : summon = Build("Summon", 2.2f, (data, rng) =>
        {
            AddWhoosh(data, rng, 0f, 0.7f, 200f, 5000f, 0.35f);
            AddBoom(data, rng, 0.05f, 0.9f);
            AddChord(data, 0.1f, 2f, new[] { 440f, 554.37f, 659.25f, 880f, 1318.5f }, 0.22f);
        });

        private static AudioClip Build(string name, float seconds, Action<float[], System.Random> synth)
        {
            var data = new float[(int)(seconds * SampleRate)];
            synth(data, new System.Random(name.GetHashCode()));
            Normalize(data, 0.85f);
            var clip = AudioClip.Create(name, data.Length, 1, SampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        /// <summary>Tintineo metálico: parciales inarmónicos que se apagan rápido (los agudos antes).</summary>
        private static void AddTing(float[] data, float start, float pitch, float gain)
        {
            float[] partials = { 2350f, 3960f, 5780f, 8120f };
            float[] decays = { 0.35f, 0.22f, 0.12f, 0.07f };
            float[] gains = { 1f, 0.6f, 0.4f, 0.25f };
            int from = (int)(start * SampleRate);
            for (int i = from; i < data.Length; i++)
            {
                float t = (i - from) / (float)SampleRate;
                if (t > 1f) break;
                float attack = Mathf.Clamp01(t * 2000f);
                float sample = 0f;
                for (int p = 0; p < partials.Length; p++)
                    sample += gains[p] * Mathf.Exp(-t / decays[p]) * Mathf.Sin(2f * Mathf.PI * partials[p] * pitch * t);
                data[i] += gain * attack * sample * 0.3f;
            }
        }

        /// <summary>Ruido filtrado cuyo filtro se abre o cierra: el "fiu" de algo que pasa rápido.</summary>
        private static void AddWhoosh(float[] data, System.Random rng, float start, float length, float fromHz, float toHz, float gain)
        {
            int from = (int)(start * SampleRate);
            int count = (int)(length * SampleRate);
            float low = 0f, band = 0f;
            for (int i = 0; i < count && from + i < data.Length; i++)
            {
                float k = i / (float)count;
                float cutoff = Mathf.Lerp(fromHz, toHz, k * k);
                float a = 1f - Mathf.Exp(-2f * Mathf.PI * cutoff / SampleRate);
                float noise = (float)(rng.NextDouble() * 2.0 - 1.0);
                low += a * (noise - low);
                band += a * (low - band);
                float envelope = Mathf.Sin(Mathf.PI * k);
                data[from + i] += gain * envelope * (low - band) * 3f;
            }
        }

        /// <summary>Golpe grave con caída de tono, como un impacto mágico.</summary>
        private static void AddBoom(float[] data, System.Random rng, float start, float gain)
        {
            int from = (int)(start * SampleRate);
            float phase = 0f, low = 0f;
            for (int i = from; i < data.Length; i++)
            {
                float t = (i - from) / (float)SampleRate;
                if (t > 1.5f) break;
                float frequency = Mathf.Lerp(90f, 38f, Mathf.Clamp01(t / 0.6f));
                phase += 2f * Mathf.PI * frequency / SampleRate;
                float noise = (float)(rng.NextDouble() * 2.0 - 1.0);
                low += 0.04f * (noise - low);
                float body = Mathf.Sin(phase) * Mathf.Exp(-t / 0.45f);
                float crack = low * 4f * Mathf.Exp(-t / 0.08f);
                data[i] += gain * Mathf.Clamp01(t * 400f) * (body + crack);
            }
        }

        /// <summary>Acorde que entra suave, vibra un poco y se desvanece.</summary>
        private static void AddChord(float[] data, float start, float length, float[] frequencies, float gain)
        {
            int from = (int)(start * SampleRate);
            int count = (int)(length * SampleRate);
            for (int i = 0; i < count && from + i < data.Length; i++)
            {
                float t = i / (float)SampleRate;
                float envelope = Mathf.Clamp01(t / 0.25f) * Mathf.Exp(-t / 0.7f);
                float vibrato = 1f + 0.004f * Mathf.Sin(2f * Mathf.PI * 5.5f * t);
                float sample = 0f;
                for (int n = 0; n < frequencies.Length; n++)
                    sample += Mathf.Sin(2f * Mathf.PI * frequencies[n] * vibrato * t + n);
                data[from + i] += gain * envelope * sample / frequencies.Length;
            }
        }

        /// <summary>Tono de diente de sierra suavizado que baja de frecuencia mientras se apaga.</summary>
        private static void AddFallingTone(float[] data, float start, float length, float fromHz, float toHz, float gain)
        {
            int from = (int)(start * SampleRate);
            int count = (int)(length * SampleRate);
            float phase = 0f, low = 0f;
            for (int i = 0; i < count && from + i < data.Length; i++)
            {
                float k = i / (float)count;
                float frequency = Mathf.Lerp(fromHz, toHz, 1f - (1f - k) * (1f - k));
                phase += frequency / SampleRate;
                phase -= Mathf.Floor(phase);
                float saw = 2f * phase - 1f;
                low += 0.12f * (saw - low);
                float envelope = Mathf.Clamp01(k * 30f) * (1f - k);
                data[from + i] += gain * envelope * low;
            }
        }

        private static void AddThud(float[] data, System.Random rng, float start, float gain)
        {
            int from = (int)(start * SampleRate);
            float low = 0f;
            for (int i = from; i < data.Length; i++)
            {
                float t = (i - from) / (float)SampleRate;
                if (t > 0.15f) break;
                float noise = (float)(rng.NextDouble() * 2.0 - 1.0);
                low += 0.08f * (noise - low);
                data[i] += gain * low * 3f * Mathf.Exp(-t / 0.03f);
            }
        }

        private static void Normalize(float[] data, float peak)
        {
            float max = 0f;
            foreach (float sample in data) max = Mathf.Max(max, Mathf.Abs(sample));
            if (max < 0.0001f) return;
            float scale = peak / max;
            for (int i = 0; i < data.Length; i++) data[i] *= scale;
        }
    }
}
