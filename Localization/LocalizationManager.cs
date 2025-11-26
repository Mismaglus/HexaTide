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

    // --- 格式 A: 通用键值对 (UI, 菜单, 属性) ---
    [Serializable]
    public class LocalizationEntry
    {
        public string key;
        public string value;
    }
    [Serializable]
    public class LocalizationFile
    {
        public LocalizationEntry[] entries;
    }

    // --- 格式 B: 智能对象定义 (技能, 物品) ---
    // 自动生成: ID_NAME, ID_DESC, ID_FLAVOR
    [Serializable]
    public class LocalizationItem
    {
        public string id;      // 核心 ID，如 "SKILL_SLASH"
        public string name;    // 对应 ID_NAME
        public string desc;    // 对应 ID_DESC
        public string flavor;  // 对应 ID_FLAVOR
    }
    [Serializable]
    public class LocalizationItemFile
    {
        public LocalizationItem[] items;
    }

    public static class LocalizationManager
    {
        const string ResourceFolder = "Localization";

        static readonly Dictionary<LocalizationLanguage, Dictionary<string, string>> tables = new();
        static LocalizationLanguage currentLanguage = LocalizationLanguage.English;
        static bool initialized;

        // ... (CurrentLanguage, UseSystemLanguage 保持不变) ...
        public static LocalizationLanguage CurrentLanguage
        {
            get => currentLanguage;
            set
            {
                if (currentLanguage == value) return;
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
            if (string.IsNullOrEmpty(key)) return string.Empty;
            EnsureInitialized();

            // 1. 查当前语言
            if (!tables.TryGetValue(currentLanguage, out var table))
            {
                EnsureLanguageLoaded(currentLanguage);
                table = tables[currentLanguage];
            }
            if (table != null && table.TryGetValue(key, out var val)) return val;

            // 2. 查英文兜底
            if (currentLanguage != LocalizationLanguage.English)
            {
                EnsureLanguageLoaded(LocalizationLanguage.English);
                var fallback = tables[LocalizationLanguage.English];
                if (fallback != null && fallback.TryGetValue(key, out var fbVal)) return fbVal;
            }

            // Debug.LogWarning($"[Loc] Key missing: {key}");
            return key; // 找不到直接返回 Key，方便 Debug
        }

        static void EnsureInitialized()
        {
            if (initialized) return;
            EnsureLanguageLoaded(currentLanguage);
            initialized = true;
        }

        static void EnsureLanguageLoaded(LocalizationLanguage language)
        {
            if (tables.ContainsKey(language)) return;

            string langCode = language switch
            {
                LocalizationLanguage.English => "en",
                LocalizationLanguage.ChineseSimplified => "zh-Hans",
                _ => "en"
            };

            // 加载该语言文件夹下的所有 JSON
            var assets = Resources.LoadAll<TextAsset>($"{ResourceFolder}/{langCode}");
            var table = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            foreach (var asset in assets)
            {
                try
                {
                    // 尝试解析为格式 A (Key-Value)
                    var fileKv = JsonUtility.FromJson<LocalizationFile>(asset.text);
                    if (fileKv != null && fileKv.entries != null && fileKv.entries.Length > 0)
                    {
                        foreach (var e in fileKv.entries)
                            if (!string.IsNullOrEmpty(e.key)) table[e.key] = e.value;
                    }

                    // 尝试解析为格式 B (Items)
                    var fileItems = JsonUtility.FromJson<LocalizationItemFile>(asset.text);
                    if (fileItems != null && fileItems.items != null && fileItems.items.Length > 0)
                    {
                        foreach (var item in fileItems.items)
                        {
                            if (string.IsNullOrEmpty(item.id)) continue;

                            // 自动展开为扁平 Key
                            if (!string.IsNullOrEmpty(item.name)) table[$"{item.id}_NAME"] = item.name;
                            if (!string.IsNullOrEmpty(item.desc)) table[$"{item.id}_DESC"] = item.desc;
                            if (!string.IsNullOrEmpty(item.flavor)) table[$"{item.id}_FLAVOR"] = item.flavor;
                        }
                    }
                }
                catch (Exception e)
                {
                    Debug.LogError($"[Localization] Error parsing {asset.name}: {e.Message}");
                }
            }

            tables[language] = table;
            Debug.Log($"[Localization] Loaded {language}: {table.Count} keys.");
        }
    }
}