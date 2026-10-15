using UnityEngine;
using UnityEngine.SceneManagement;

namespace CardDuel.Prototype
{
    /// <summary>
    /// Arranca el prototipo automáticamente al dar a Play en SampleScene,
    /// sin tener que montar nada en el editor. Si añades un DuelPrototype
    /// a mano en la escena, se usa ese.
    /// </summary>
    public static class PrototypeBootstrap
    {
        private const string SceneName = "SampleScene";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Init()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
            TryCreate(SceneManager.GetActiveScene());
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode) => TryCreate(scene);

        private static void TryCreate(Scene scene)
        {
            if (scene.name != SceneName) return;
            if (Object.FindAnyObjectByType<DuelPrototype>() != null) return;
            new GameObject("DuelPrototype").AddComponent<DuelPrototype>();
        }
    }
}
