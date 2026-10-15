using System.Linq;
using CardDuel.Cards;
using UnityEditor;
using UnityEngine;

namespace CardDuel.EditorTools
{
    /// <summary>
    /// Crea Resources/MonsterModels con un modelo de Ultimate Monsters (Quaternius)
    /// para cada monstruo del mazo de prueba, y busca sus animaciones en el FBX.
    /// Se ejecuta solo si el asset no existe; el menú lo regenera.
    /// </summary>
    [InitializeOnLoad]
    public static class MonsterModelLibraryBuilder
    {
        private const string AssetPath = "Assets/_Game/Resources/MonsterModels.asset";
        private const string ModelsRoot = "Assets/ThirdParty/Quaternius/UltimateMonsters/";

        private static readonly (string card, string model)[] DefaultModels =
        {
            ("Caballero de Ceniza", "Big/Orc_Skull"),
            ("Lobo Rúnico", "Blob/Dog"),
            ("Guardiana del Alba", "Blob/Wizard"),
            ("Golem de Musgo", "Flying/Goleling_Evolved"),
            ("Espectro Errante", "Flying/Ghost"),
            ("Sirena de Coral", "Big/Fish"),
            ("Halcón Tormenta", "Big/Birb"),
            ("Duende Chispa", "Flying/Demon"),
            ("Paladín Solar", "Big/Ninja"),
            ("Titán de las Mareas", "Flying/Glub_Evolved"),
            ("Dragón de Obsidiana", "Flying/Dragon_Evolved"),
        };

        static MonsterModelLibraryBuilder()
        {
            EditorApplication.delayCall += () =>
            {
                // También se regenera si el asset es de antes de existir la animación de movimiento.
                var library = AssetDatabase.LoadAssetAtPath<MonsterModelLibrary>(AssetPath);
                if (library == null || library.entries.TrueForAll(e => e.move == null)) Build();
            };
        }

        [MenuItem("CardDuel/Reasignar modelos 3D de los monstruos")]
        public static void Build()
        {
            var entries = new System.Collections.Generic.List<MonsterModelLibrary.Entry>();
            foreach (var (card, model) in DefaultModels)
            {
                string path = $"{ModelsRoot}{model}.fbx";
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab == null)
                {
                    Debug.LogWarning($"[CardDuel] No se encontró el modelo {path} para {card}.");
                    continue;
                }

                var clips = AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>()
                    .Where(c => !c.name.StartsWith("__preview")).ToArray();

                entries.Add(new MonsterModelLibrary.Entry
                {
                    cardName = card,
                    model = prefab,
                    idle = FindClip(clips, "Idle", "Flying", "Fly"),
                    move = FindClip(clips, "Run", "Fast_Flying", "Walk", "Flying"),
                    attack = FindClip(clips, "Punch", "Attack", "Bite", "Headbutt", "Weapon", "Swipe"),
                    hit = FindClip(clips, "HitReact", "Hit"),
                    death = FindClip(clips, "Death", "Dead", "Die"),
                });
            }

            // Si los FBX aún no se han importado, se reintentará en la próxima recarga.
            if (entries.Count == 0) return;

            var library = AssetDatabase.LoadAssetAtPath<MonsterModelLibrary>(AssetPath);
            if (library == null)
            {
                library = ScriptableObject.CreateInstance<MonsterModelLibrary>();
                AssetDatabase.CreateAsset(library, AssetPath);
            }

            library.entries = entries;
            EditorUtility.SetDirty(library);
            AssetDatabase.SaveAssets();
            Debug.Log($"[CardDuel] Modelos 3D asignados a {library.entries.Count} monstruos ({AssetPath}).");
        }

        /// <summary>La primera animación cuyo nombre (sin el prefijo del esqueleto) empieza por alguna de las palabras.</summary>
        private static AnimationClip FindClip(AnimationClip[] clips, params string[] keywords)
        {
            foreach (var keyword in keywords)
                foreach (var clip in clips)
                {
                    string name = clip.name.Contains("|") ? clip.name.Substring(clip.name.LastIndexOf('|') + 1) : clip.name;
                    if (name.StartsWith(keyword, System.StringComparison.OrdinalIgnoreCase)) return clip;
                }
            return null;
        }
    }
}
