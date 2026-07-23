using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

public static class StartLocationLightingSetup
{
    private const string PrefabPath =
        "Assets/RunGun/Levels/ControllerPrefabs/StartLocation.prefab";

    [MenuItem("RunGun/Lighting/Optimize Start Location Decorative Lights")]
    public static void Configure()
    {
        GameObject root = PrefabUtility.LoadPrefabContents(PrefabPath);

        try
        {
            Light[] lights = root.GetComponentsInChildren<Light>(true);

            foreach (Light light in lights)
            {
                light.lightmapBakeType = LightmapBakeType.Realtime;
                light.shadows = LightShadows.None;

                // Nested environment prefabs must store these as instance overrides.
                PrefabUtility.RecordPrefabInstancePropertyModifications(light);
                EditorUtility.SetDirty(light);
            }

            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            Debug.Log(
                $"Configured {lights.Length} StartLocation decorative lights " +
                "as realtime with shadows disabled.");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }
}
