using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Localization
{
    public enum LocalizationLanguage
    {
        English,
        ChineseSimplified,
    }

    [Serializable]
    public class LocalizationEntry
    {
        public string key;
        public string value;
    }

    [Serializable]
    public class LocalizationFile
    {
        public LocalizationEntry[] entries = Array.Empty<LocalizationEntry>();
    }

    public static class LocalizationManager
    {
        const string ResourceFolder = "Localization";

        static readonly Dictionary<LocalizationLanguage, Dictionary<string, string>> tables = new();
        static LocalizationLanguage currentLanguage = LocalizationLanguage.English;
        static bool initialized;

        public static LocalizationLanguage CurrentLanguage
        {
            get => currentLanguage;
            set
            {
                if (currentLanguage == value)
                    return;

                currentLanguage = value;
                EnsureLanguageLoaded(value);
            }
        }

        public static void UseSystemLanguage()
        {
            CurrentLanguage = Application.systemLanguage switch
            {
                SystemLanguage.ChineseSimplified => LocalizationLanguage.ChineseSimplified,
                _ => LocalizationLanguage.English,
            };
        }

        public static string Get(string key)
        {
            if (string.IsNullOrEmpty(key))
                return string.Empty;

            EnsureInitialized();

            if (!tables.TryGetValue(currentLanguage, out var table))
            {
                EnsureLanguageLoaded(currentLanguage);
                table = tables[currentLanguage];
            }

            if (table.TryGetValue(key, out var value) && !string.IsNullOrEmpty(value))
                return value;

            if (currentLanguage != LocalizationLanguage.English)
            {
                EnsureLanguageLoaded(LocalizationLanguage.English);
                var fallbackTable = tables[LocalizationLanguage.English];
                if (fallbackTable.TryGetValue(key, out var fallbackValue) && !string.IsNullOrEmpty(fallbackValue))
                    return fallbackValue;
            }

            Debug.LogWarning($"Missing localization for key '{key}' in language '{currentLanguage}'.");
            return key;
        }

        static void EnsureInitialized()
        {
            if (initialized)
                return;

            EnsureLanguageLoaded(currentLanguage);
            initialized = true;
        }

        static void EnsureLanguageLoaded(LocalizationLanguage language)
        {
            if (tables.ContainsKey(language))
                return;

            var assetName = GetResourceName(language);
            if (string.IsNullOrEmpty(assetName))
            {
                tables[language] = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                return;
            }

            var textAsset = Resources.Load<TextAsset>($"{ResourceFolder}/{assetName}");
            if (textAsset == null)
            {
                Debug.LogWarning($"Localization file '{assetName}' not found in Resources/{ResourceFolder}.");
                tables[language] = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                return;
            }

            var file = JsonUtility.FromJson<LocalizationFile>(textAsset.text);
            var table = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (file?.entries != null)
            {
                foreach (var entry in file.entries)
                {
                    if (string.IsNullOrEmpty(entry?.key))
                        continue;

                    table[entry.key] = entry.value ?? string.Empty;
                }
            }

            tables[language] = table;
        }

        static string GetResourceName(LocalizationLanguage language) => language switch
        {
            LocalizationLanguage.English => "en",
            LocalizationLanguage.ChineseSimplified => "zh-Hans",
            _ => null,
        };
    }
}
