using System;
using System.Collections.Generic;
using UnityEngine;

namespace CardDuel.Cards
{
    /// <summary>
    /// Qué modelo 3D (y qué animaciones) aparece al invocar cada monstruo.
    /// Vive en Resources/MonsterModels y se edita desde el Inspector: añade una
    /// entrada con el nombre de la carta y arrastra el modelo y sus animaciones.
    /// </summary>
    [CreateAssetMenu(fileName = "MonsterModels", menuName = "CardDuel/Monster Model Library")]
    public class MonsterModelLibrary : ScriptableObject
    {
        public const string ResourcePath = "MonsterModels";

        [Serializable]
        public class Entry
        {
            [Tooltip("Nombre de la carta tal como aparece en el juego.")]
            public string cardName;
            public GameObject model;
            public AnimationClip idle;
            [Tooltip("Correr o volar hacia el enemigo al atacar.")]
            public AnimationClip move;
            public AnimationClip attack;
            public AnimationClip hit;
            public AnimationClip death;
        }

        public List<Entry> entries = new List<Entry>();

        /// <summary>Animal de ambiente que pasea por la granja (no es una carta).</summary>
        [Serializable]
        public class Critter
        {
            public GameObject model;
            public AnimationClip idle;
            public AnimationClip walk;
        }

        [Tooltip("Animales que pasean alrededor de la mesa para dar ambiente.")]
        public List<Critter> critters = new List<Critter>();

        private static MonsterModelLibrary instance;

        public static MonsterModelLibrary Instance =>
            instance != null ? instance : instance = Resources.Load<MonsterModelLibrary>(ResourcePath);

        public Entry Find(string cardName)
        {
            foreach (var entry in entries)
                if (entry.model != null && string.Equals(entry.cardName, cardName, StringComparison.OrdinalIgnoreCase))
                    return entry;
            return null;
        }
    }
}
