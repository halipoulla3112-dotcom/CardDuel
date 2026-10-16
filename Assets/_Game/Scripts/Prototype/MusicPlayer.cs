using UnityEngine;

namespace CardDuel.Prototype
{
    /// <summary>
    /// Música de fondo tranquila de granja, generada por código: pad suave, bajo,
    /// arpegio punteado y un poco de percusión sobre C – G – Am – F, en un bucle
    /// de 8 compases que empalma sin cortes. Suena en el menú y en el duelo.
    /// </summary>
    public class MusicPlayer : MonoBehaviour
    {
        private const int SampleRate = 22050;
        private const float Bpm = 96f;
        private const int Bars = 8;
        private const float MusicGain = 0.55f;

        private static AudioClip loop;
        private AudioSource source;

        /// <summary>Añade la música al objeto indicado y empieza a sonar con un fundido.</summary>
        public static MusicPlayer Attach(GameObject host)
        {
            var player = host.AddComponent<MusicPlayer>();
            player.source = host.AddComponent<AudioSource>();
            player.source.clip = Loop;
            player.source.loop = true;
            player.source.playOnAwake = false;
            player.source.spatialBlend = 0f;
            player.source.volume = 0f;
            player.source.Play();
            return player;
        }

        private void Update()
        {
            // Entra con un fundido y respeta el volumen de música de los ajustes (y la pausa).
            float target = GameSettings.MusicVolume * MusicGain * (Time.timeScale > 0f ? 1f : 0.4f);
            source.volume = Mathf.MoveTowards(source.volume, target, Time.unscaledDeltaTime * 0.5f);
        }

        private static AudioClip Loop => loop != null ? loop : loop = Compose();

        private static AudioClip Compose()
        {
            float beat = 60f / Bpm;
            float bar = beat * 4f;
            int length = Mathf.RoundToInt(bar * Bars * SampleRate);
            var data = new float[length];

            // Acordes (MIDI): C, G, Am, F, y luego C, G, F, G para cerrar el bucle.
            int[][] chords =
            {
                new[] { 60, 64, 67 }, new[] { 55, 59, 62 }, new[] { 57, 60, 64 }, new[] { 53, 57, 60 },
                new[] { 60, 64, 67 }, new[] { 55, 59, 62 }, new[] { 53, 57, 60 }, new[] { 55, 59, 62 },
            };
            int[] arpPattern = { 0, 1, 2, 1, 0, 2, 1, 2 };
            int[] melody = { 76, -1, 74, 72, 74, -1, 71, -1, 72, -1, 76, -1, 77, 76, 74, -1 };
            var rng = new System.Random(5);

            for (int b = 0; b < Bars; b++)
            {
                float start = b * bar;
                var chord = chords[b];

                // Pad: notas del acorde que entran y salen despacio.
                foreach (int note in chord)
                    AddTone(data, start, bar + 0.3f, Freq(note), 0.06f, 0.5f, 0.6f, Wave.Soft);

                // Bajo en el 1 y el 3.
                AddTone(data, start, beat * 1.8f, Freq(chord[0] - 12), 0.22f, 0.01f, 0.5f, Wave.Sine);
                AddTone(data, start + beat * 2f, beat * 1.8f, Freq(chord[0] - 12), 0.18f, 0.01f, 0.5f, Wave.Sine);

                // Arpegio punteado en corcheas.
                for (int s = 0; s < 8; s++)
                    AddTone(data, start + s * beat * 0.5f, 0.5f, Freq(chord[arpPattern[s]] + 12), 0.07f, 0.005f, 0.18f, Wave.Pluck);

                // Melodía sencilla en la segunda mitad del bucle.
                if (b >= 4)
                    for (int s = 0; s < 4; s++)
                    {
                        int note = melody[(b - 4) * 4 + s];
                        if (note > 0) AddTone(data, start + s * beat, beat * 0.95f, Freq(note), 0.08f, 0.02f, 0.35f, Wave.Pluck);
                    }

                // Maraca suave a contratiempo.
                for (int s = 0; s < 4; s++)
                    AddShaker(data, start + (s + 0.5f) * beat, 0.025f, rng);
            }

            Normalize(data, 0.6f);
            var clip = AudioClip.Create("FarmLoop", length, 1, SampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        private enum Wave { Sine, Soft, Pluck }

        private static float Freq(int midi) => 440f * Mathf.Pow(2f, (midi - 69) / 12f);

        /// <summary>Nota con ataque y caída; lo que pasa del final del bucle vuelve al principio (empalme sin corte).</summary>
        private static void AddTone(float[] data, float start, float length, float frequency, float gain, float attack, float decay, Wave wave)
        {
            int from = Mathf.RoundToInt(start * SampleRate);
            int count = Mathf.RoundToInt(length * SampleRate);
            for (int i = 0; i < count; i++)
            {
                float t = i / (float)SampleRate;
                float envelope = Mathf.Clamp01(t / attack) * (wave == Wave.Soft
                    ? Mathf.Clamp01((length - t) / decay)
                    : Mathf.Exp(-t / decay));
                float phase = 2f * Mathf.PI * frequency * t;
                float sample = wave switch
                {
                    Wave.Soft => Mathf.Sin(phase) * 0.7f + Mathf.Sin(phase * 2.003f) * 0.2f + Mathf.Sin(phase * 0.5f) * 0.1f,
                    Wave.Pluck => Mathf.Sin(phase) + 0.3f * Mathf.Sin(phase * 2f) * Mathf.Exp(-t / 0.05f),
                    _ => Mathf.Sin(phase),
                };
                data[(from + i) % data.Length] += gain * envelope * sample;
            }
        }

        private static void AddShaker(float[] data, float start, float gain, System.Random rng)
        {
            int from = Mathf.RoundToInt(start * SampleRate);
            float previous = 0f;
            for (int i = 0; i < SampleRate * 0.06f; i++)
            {
                float t = i / (float)SampleRate;
                float noise = (float)(rng.NextDouble() * 2.0 - 1.0);
                float high = noise - previous;
                previous = noise;
                data[(from + i) % data.Length] += gain * high * Mathf.Exp(-t / 0.015f);
            }
        }

        private static void Normalize(float[] data, float peak)
        {
            float max = 0f;
            foreach (float s in data) max = Mathf.Max(max, Mathf.Abs(s));
            if (max < 0.0001f) return;
            for (int i = 0; i < data.Length; i++) data[i] *= peak / max;
        }
    }
}
