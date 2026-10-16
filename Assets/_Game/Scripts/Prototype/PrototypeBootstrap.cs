using UnityEngine;
using UnityEngine.SceneManagement;

namespace CardDuel.Prototype
{
    /// <summary>
    /// Arranca el juego automáticamente al dar a Play en SampleScene, sin tener
    /// que montar nada en el editor: abre el menú principal o, si se ha pedido
    /// con <see cref="LoadDuel"/>, directamente un duelo. Si añades un
    /// DuelPrototype o un MainMenu a mano en la escena, se usa ese.
    /// </summary>
    public static class PrototypeBootstrap
    {
        private const string SceneName = "SampleScene";

        /// <summary>Qué se abre la próxima vez que se cargue la escena: un duelo o el menú.</summary>
        private static bool startDuel;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetState() => startDuel = false;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Init()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
            TryCreate(SceneManager.GetActiveScene());
        }

        /// <summary>Recarga la escena empezando un duelo nuevo.</summary>
        public static void LoadDuel() => Reload(true);

        /// <summary>Recarga la escena en el menú principal.</summary>
        public static void LoadMenu() => Reload(false);

        private static void Reload(bool duel)
        {
            startDuel = duel;
            Time.timeScale = 1f;
            DG.Tweening.DOTween.KillAll();
            SceneManager.LoadScene(SceneName);
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode) => TryCreate(scene);

        private static void TryCreate(Scene scene)
        {
            if (scene.name != SceneName) return;
            if (Object.FindAnyObjectByType<DuelPrototype>() != null || Object.FindAnyObjectByType<MainMenu>() != null) return;

            if (startDuel) new GameObject("DuelPrototype").AddComponent<DuelPrototype>();
            else new GameObject("MainMenu").AddComponent<MainMenu>();
        }
    }
}
