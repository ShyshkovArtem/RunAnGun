using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class RunGunSceneSetup
{
    [MenuItem("RunGun/Setup Sample Scene Player")]
    public static void SetupSampleScenePlayer()
    {
        const string scenePath = "Assets/Scenes/SampleScene.unity";
        const string playerPrefabPath = "Assets/FirstPersonControllerPro/Player/Prefab/Player.prefab";

        var scene = EditorSceneManager.OpenScene(scenePath);
        var playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(playerPrefabPath);
        if (playerPrefab == null)
        {
            Debug.LogError($"Player prefab not found at {playerPrefabPath}");
            EditorApplication.Exit(1);
            return;
        }

        foreach (var camera in Object.FindObjectsByType<Camera>(FindObjectsSortMode.None))
        {
            if (camera.CompareTag("MainCamera") && !camera.transform.IsChildOfPrefabInstance())
                Object.DestroyImmediate(camera.gameObject);
        }

        var existingPlayer = GameObject.Find("Player");
        if (existingPlayer == null)
        {
            var player = (GameObject)PrefabUtility.InstantiatePrefab(playerPrefab, scene);
            player.name = "Player";
            player.transform.position = new Vector3(0f, 1.1f, 0f);
            player.transform.rotation = Quaternion.identity;
        }

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
    }

    private static bool IsChildOfPrefabInstance(this Transform transform)
    {
        while (transform != null)
        {
            if (PrefabUtility.IsPartOfPrefabInstance(transform.gameObject))
                return true;

            transform = transform.parent;
        }

        return false;
    }
}
