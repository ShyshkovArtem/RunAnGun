using System.Collections.Generic;
using RunGun.Settings;
using UnityEditor;
using UnityEngine;

namespace RunGun.Editor
{
    [InitializeOnLoad]
    internal static class LocalizationTableSetup
    {
        private const string Folder = "Assets/RunGun/Resources/Localization";
        private const string EnglishPath = Folder + "/EnglishLocalization.asset";
        private const string RussianPath = Folder + "/RussianLocalization.asset";
        private const int DialogueContentVersion = 1;

        static LocalizationTableSetup()
        {
            EditorApplication.delayCall += CreateMissingTables;
        }

        [MenuItem("Tools/Run&Gun/Localization/Create Missing Tables")]
        public static void CreateMissingTables()
        {
            EnsureFolder();

            bool created = false;
            if (AssetDatabase.LoadAssetAtPath<LocalizationTable>(EnglishPath) == null)
            {
                CreateTable(EnglishPath, GameLanguage.English, english: true);
                created = true;
            }

            if (AssetDatabase.LoadAssetAtPath<LocalizationTable>(RussianPath) == null)
            {
                CreateTable(RussianPath, GameLanguage.Russian, english: false);
                created = true;
            }

            LocalizationTable englishTable = AssetDatabase.LoadAssetAtPath<LocalizationTable>(EnglishPath);
            LocalizationTable russianTable = AssetDatabase.LoadAssetAtPath<LocalizationTable>(RussianPath);
            bool synchronized = SynchronizeDialogueText(
                englishTable, GameLocalization.EnglishDialogueEntries);
            synchronized |= SynchronizeDialogueText(
                russianTable, GameLocalization.RussianDialogueEntries);

            if (!created && !synchronized)
                return;

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            GameLocalization.ReloadTables();
            Debug.Log($"Localization tables created in {Folder}.");
        }

        private static bool SynchronizeDialogueText(
            LocalizationTable table,
            IReadOnlyDictionary<string, string> dialogueEntries)
        {
            if (table == null ||
                !table.ApplyTextOverrides(DialogueContentVersion, dialogueEntries))
                return false;

            EditorUtility.SetDirty(table);
            return true;
        }

        private static void CreateTable(string path, GameLanguage language, bool english)
        {
            var table = ScriptableObject.CreateInstance<LocalizationTable>();
            var entries = new List<LocalizationTable.Entry>();

            foreach (KeyValuePair<string, string> pair in GameLocalization.DefaultRussianEntries)
            {
                entries.Add(new LocalizationTable.Entry
                {
                    key = pair.Key,
                    text = english ? pair.Key : pair.Value
                });
            }

            table.SetEntries(language, entries);
            AssetDatabase.CreateAsset(table, path);
        }

        private static void EnsureFolder()
        {
            if (AssetDatabase.IsValidFolder(Folder))
                return;

            string current = "Assets";
            string[] parts = Folder.Split('/');
            for (int i = 1; i < parts.Length; i++)
            {
                string next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, parts[i]);
                current = next;
            }
        }
    }
}
