using UnityEditor;
using UnityEditor.SceneManagement;

namespace CardDuel.EditorTools
{
    /// <summary>
    /// Al abrir el editor, si la escena activa está vacía y sin guardar
    /// ("Untitled"), abre SampleScene, que es donde arranca el prototipo.
    /// </summary>
    [InitializeOnLoad]
    public static class OpenPrototypeScene
    {
        private const string ScenePath = "Assets/Scenes/SampleScene.unity";

        static OpenPrototypeScene()
        {
            EditorApplication.delayCall += TryOpen;
        }

        private static void TryOpen()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;

            var active = EditorSceneManager.GetActiveScene();
            if (!string.IsNullOrEmpty(active.path) || active.isDirty) return;

            EditorSceneManager.OpenScene(ScenePath);
        }

        [MenuItem("CardDuel/Abrir escena del prototipo")]
        private static void OpenFromMenu()
        {
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                EditorSceneManager.OpenScene(ScenePath);
        }
    }
}
