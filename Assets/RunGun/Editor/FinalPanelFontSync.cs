using TMPro;
using UnityEditor;
using UnityEngine;

internal static class FinalPanelFontSync
{
    private const string PlayerPrefabPath = "Assets/RunGun/PlayerDef.prefab";
    private const string DisplayFontPath = "Assets/RunGun/Font/Pervitina-Dex-FFP SDF.asset";

    [MenuItem("Tools/RunGun/Sync PlayerDef Final Panel Fonts")]
    public static void Sync()
    {
        TMP_FontAsset displayFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(DisplayFontPath);
        if (displayFont == null)
            throw new MissingReferenceException($"TMP font asset not found at '{DisplayFontPath}'.");

        GameObject prefabRoot = PrefabUtility.LoadPrefabContents(PlayerPrefabPath);
        try
        {
            Transform finalPanel = FindChildRecursive(prefabRoot.transform, "FinalPanel");
            if (finalPanel == null)
                throw new MissingReferenceException($"FinalPanel was not found in '{PlayerPrefabPath}'.");

            TMP_Text[] texts = finalPanel.GetComponentsInChildren<TMP_Text>(true);
            int changedCount = 0;
            for (int i = 0; i < texts.Length; i++)
            {
                TMP_Text text = texts[i];
                if (IsNumericTimeReadout(text.name) || text.font == displayFont)
                    continue;

                text.font = displayFont;
                EditorUtility.SetDirty(text);
                changedCount++;
            }

            PrefabUtility.SaveAsPrefabAsset(prefabRoot, PlayerPrefabPath);
            Debug.Log($"Updated {changedCount} FinalPanel text components in PlayerDef.");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(prefabRoot);
        }
    }

    private static bool IsNumericTimeReadout(string objectName)
    {
        return objectName.EndsWith("TimerTxt", System.StringComparison.Ordinal);
    }

    private static Transform FindChildRecursive(Transform root, string childName)
    {
        if (root.name == childName)
            return root;

        for (int i = 0; i < root.childCount; i++)
        {
            Transform result = FindChildRecursive(root.GetChild(i), childName);
            if (result != null)
                return result;
        }

        return null;
    }
}
