using UnityEngine;

namespace CardDuel.Prototype
{
    /// <summary>Ajustes del jugador (volumen general y de efectos), guardados entre partidas.</summary>
    public static class GameSettings
    {
        private const string MasterKey = "CardDuel.MasterVolume";
        private const string EffectsKey = "CardDuel.EffectsVolume";

        private static bool loaded;
        private static float master = 1f;
        private static float effects = 1f;

        /// <summary>Volumen de todo el juego (0–1).</summary>
        public static float MasterVolume
        {
            get { Load(); return master; }
            set { Load(); master = Mathf.Clamp01(value); Apply(); PlayerPrefs.SetFloat(MasterKey, master); }
        }

        /// <summary>Volumen de los efectos de sonido (0–1).</summary>
        public static float EffectsVolume
        {
            get { Load(); return effects; }
            set { Load(); effects = Mathf.Clamp01(value); PlayerPrefs.SetFloat(EffectsKey, effects); }
        }

        /// <summary>Aplica el volumen general (se llama al arrancar y al cambiarlo).</summary>
        public static void Apply()
        {
            Load();
            AudioListener.volume = master;
        }

        public static void Save() => PlayerPrefs.Save();

        private static void Load()
        {
            if (loaded) return;
            loaded = true;
            master = PlayerPrefs.GetFloat(MasterKey, 1f);
            effects = PlayerPrefs.GetFloat(EffectsKey, 1f);
        }
    }
}
