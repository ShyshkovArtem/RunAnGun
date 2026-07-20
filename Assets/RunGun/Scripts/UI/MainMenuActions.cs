using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace RunGun.UI
{
    internal static class MainMenuActions
    {
        private const string MainMenuSceneName = "MainMenu";
        private const string ExitButtonName = "ExitBtn";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void RegisterSceneCallback()
        {
            SceneManager.sceneLoaded -= HandleSceneLoaded;
            SceneManager.sceneLoaded += HandleSceneLoaded;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void BindActiveScene()
        {
            BindScene(SceneManager.GetActiveScene());
        }

        private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            BindScene(scene);
        }

        private static void BindScene(Scene scene)
        {
            if (!scene.IsValid() || scene.name != MainMenuSceneName)
                return;

            Button exitButton = FindSceneButton(scene, ExitButtonName);
            if (exitButton == null)
                return;

            exitButton.onClick.RemoveListener(Quit);
            exitButton.onClick.AddListener(Quit);
        }

        private static void Quit()
        {
#if UNITY_EDITOR
            EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        private static Button FindSceneButton(Scene scene, string objectName)
        {
            GameObject[] roots = scene.GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++)
            {
                Button button = FindButtonRecursive(roots[i].transform, objectName);
                if (button != null)
                    return button;
            }

            return null;
        }

        private static Button FindButtonRecursive(Transform root, string objectName)
        {
            if (root.name == objectName)
                return root.GetComponent<Button>();

            for (int i = 0; i < root.childCount; i++)
            {
                Button found = FindButtonRecursive(root.GetChild(i), objectName);
                if (found != null)
                    return found;
            }

            return null;
        }
    }
}
