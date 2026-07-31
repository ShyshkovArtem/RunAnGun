using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace RunGun.Settings
{
    [CreateAssetMenu(
        fileName = "LocalizationTable",
        menuName = "RunGun/Localization/Localization Table")]
    public sealed class LocalizationTable : ScriptableObject
    {
        [Serializable]
        public sealed class Entry
        {
            [Tooltip("Stable source key. Do not translate this field.")]
            [TextArea(1, 4)]
            public string key;

            [Tooltip("Text displayed for this language.")]
            [TextArea(1, 6)]
            public string text;
        }

        [SerializeField] private GameLanguage language;
        [SerializeField] private List<Entry> entries = new();

        private Dictionary<string, Entry> _lookup;

        public GameLanguage Language => language;
        public IReadOnlyList<Entry> Entries => entries;

        public string Get(string key)
        {
            return TryGet(key, out string value) ? value : key;
        }

        public bool TryGet(string key, out string value)
        {
            value = key;
            if (string.IsNullOrEmpty(key))
                return false;

            EnsureLookup();
            if (!_lookup.TryGetValue(NormalizeKey(key), out Entry entry))
                return false;

            value = string.IsNullOrEmpty(entry.text) ? key : entry.text;
            return true;
        }

        public void SetEntries(GameLanguage tableLanguage, IEnumerable<Entry> source)
        {
            language = tableLanguage;
            entries = new List<Entry>(source);
            _lookup = null;
        }

        private void OnValidate()
        {
            _lookup = null;
        }

        private void EnsureLookup()
        {
            if (_lookup != null)
                return;

            _lookup = new Dictionary<string, Entry>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < entries.Count; i++)
            {
                Entry entry = entries[i];
                if (entry == null || string.IsNullOrEmpty(entry.key))
                    continue;

                _lookup[NormalizeKey(entry.key)] = entry;
            }
        }

        private static string NormalizeKey(string value)
        {
            value = value.Trim();
            var result = new StringBuilder(value.Length);
            bool pendingSpace = false;

            for (int i = 0; i < value.Length; i++)
            {
                char character = value[i];
                if (char.IsWhiteSpace(character))
                {
                    pendingSpace = result.Length > 0;
                    continue;
                }

                if (character == '/')
                {
                    while (result.Length > 0 && result[result.Length - 1] == ' ')
                        result.Length--;
                    result.Append('/');
                    pendingSpace = false;
                    continue;
                }

                if (pendingSpace && result.Length > 0 &&
                    result[result.Length - 1] != '/')
                {
                    result.Append(' ');
                }

                result.Append(character);
                pendingSpace = false;
            }

            return result.ToString();
        }
    }
}
