using UnityEngine;
using UnityEngine.SceneManagement;

namespace RunGun.UI
{
    internal static class MainMenuCursorBootstrap
    {
        private const string MainMenuSceneName = "MainMenu";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void RegisterCallbacks()
        {
            SceneManager.sceneLoaded -= HandleSceneLoaded;
            SceneManager.sceneLoaded += HandleSceneLoaded;
            Application.focusChanged -= HandleFocusChanged;
            Application.focusChanged += HandleFocusChanged;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void InitializeActiveScene()
        {
            ApplyForScene(SceneManager.GetActiveScene());
        }

        private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            ApplyForScene(scene);
        }

        private static void HandleFocusChanged(bool hasFocus)
        {
            if (hasFocus)
                ApplyForScene(SceneManager.GetActiveScene());
        }

        private static void ApplyForScene(Scene scene)
        {
            if (!scene.IsValid() || scene.name != MainMenuSceneName)
                return;

            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
    }
}
