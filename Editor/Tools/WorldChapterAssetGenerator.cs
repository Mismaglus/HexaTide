using System;
using System.Collections.Generic;
using System.Linq;
using Game.Localization;
using Game.World;
using UnityEditor;
using UnityEngine;

public static class WorldChapterAssetGenerator
{
    const string OutputFolder = "Assets/_Assets/World";
    const string ChaptersResourcePath = "Localization/en/Chapters";
    const string ChaptersAssetPath = "Assets/Resources/Localization/en/Chapters.json";

    [MenuItem("Tools/World/Generate Act ChapterSettings + Region Themes")]
    public static void Generate()
    {
        var regionIds = LoadRegionIds();
        if (regionIds.Count == 0)
        {
            EditorUtility.DisplayDialog(
                "Generate Chapter Assets",
                "No regions found. Expected a JSON at Assets/Resources/Localization/en/Chapters.json (Resources path: Localization/en/Chapters) with an 'items' array containing 'id' fields like REGION_1..REGION_8.",
                "OK");
            return;
        }

        EnsureFolderExists(OutputFolder);

        int created = 0;
        int updated = 0;

        // 1) Act settings (only 4 assets)
        for (int act = 1; act <= 4; act++)
        {
            string actId = $"ACT_{act}";
            string settingsPath = $"{OutputFolder}/{actId}_ChapterSettings.asset";

            var settings = AssetDatabase.LoadAssetAtPath<ChapterSettings>(settingsPath);
            bool settingsIsNew = settings == null;
            if (settingsIsNew)
            {
                settings = ScriptableObject.CreateInstance<ChapterSettings>();
                settings.name = $"{actId}_ChapterSettings";
                ConfigureActChapterSettings(settings, act);
                AssetDatabase.CreateAsset(settings, settingsPath);
                created++;
            }
            else
            {
                bool changed = ConfigureActChapterSettings(settings, act);
                if (changed) updated++;
            }
        }

        // 2) Region theme DB skeleton
        string themeDbPath = $"{OutputFolder}/RegionThemeDB.asset";
        var themeDb = AssetDatabase.LoadAssetAtPath<RegionThemeDB>(themeDbPath);
        bool themeDbIsNew = themeDb == null;
        if (themeDbIsNew)
        {
            themeDb = ScriptableObject.CreateInstance<RegionThemeDB>();
            themeDb.name = "RegionThemeDB";
            AssetDatabase.CreateAsset(themeDb, themeDbPath);
            created++;
        }
        bool themeChanged = ConfigureRegionThemes(themeDb, regionIds);
        if (themeChanged) updated++;

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        EditorUtility.DisplayDialog(
            "Generate Chapter Assets",
            $"Done. Created: {created}, Updated: {updated}.\nOutput: {OutputFolder}",
            "OK");
    }

    static bool IsSupportedChapterKey(string baseId)
    {
        if (string.IsNullOrEmpty(baseId)) return false;
        if (!baseId.StartsWith("REGION_", StringComparison.OrdinalIgnoreCase)) return false;
        return TryGetRegionNumber(baseId, out var n) && n >= 1;
    }

    static bool IsRegion(string baseId) => baseId.StartsWith("REGION_", StringComparison.OrdinalIgnoreCase);

    static bool TryGetRegionNumber(string baseId, out int number)
    {
        number = 0;
        if (string.IsNullOrEmpty(baseId)) return false;
        if (!baseId.StartsWith("REGION_", StringComparison.OrdinalIgnoreCase)) return false;
        var suffix = baseId.Substring("REGION_".Length);
        return int.TryParse(suffix, out number);
    }

    static List<string> LoadRegionIds()
    {
        // Prefer Resources path (matches runtime) so this continues to work even if assets move.
        var asset = Resources.Load<TextAsset>(ChaptersResourcePath);
        if (asset == null)
        {
            // Fallback for editor-only environments if Resources indexing is stale.
            asset = AssetDatabase.LoadAssetAtPath<TextAsset>(ChaptersAssetPath);
        }

        if (asset == null || string.IsNullOrWhiteSpace(asset.text))
            return new List<string>();

        try
        {
            var file = JsonUtility.FromJson<LocalizationItemFile>(asset.text);
            if (file?.items == null) return new List<string>();

            var ids = new List<string>();
            foreach (var item in file.items)
            {
                if (item == null) continue;
                if (string.IsNullOrEmpty(item.id)) continue;
                if (!item.id.StartsWith("REGION_", StringComparison.OrdinalIgnoreCase)) continue;
                ids.Add(item.id);
            }
            ids.Sort(StringComparer.OrdinalIgnoreCase);
            return ids;
        }
        catch (Exception ex)
        {
            Debug.LogError($"[WorldChapterAssetGenerator] Failed parsing Chapters.json: {ex.Message}");
            return new List<string>();
        }
    }

    static void EnsureFolderExists(string folder)
    {
        if (AssetDatabase.IsValidFolder(folder)) return;

        var parts = folder.Split('/');
        string current = parts[0];
        for (int i = 1; i < parts.Length; i++)
        {
            string next = current + "/" + parts[i];
            if (!AssetDatabase.IsValidFolder(next))
                AssetDatabase.CreateFolder(current, parts[i]);
            current = next;
        }
    }

    static bool ConfigureActChapterSettings(ChapterSettings settings, int act)
    {
        bool changed = false;

        if (settings == null) return false;

        int desiredAct = Mathf.Clamp(act, 1, 4);
        if (settings.actNumber != desiredAct)
        {
            settings.actNumber = desiredAct;
            changed = true;
        }

        if (changed)
            EditorUtility.SetDirty(settings);

        return changed;
    }

    static bool ConfigureRegionThemes(RegionThemeDB db, List<string> regionIds)
    {
        if (db == null) return false;
        if (db.themes == null) db.themes = new List<RegionTheme>();

        bool changed = false;

        // Ensure one entry per region id, keep existing material assignments.
        foreach (var id in regionIds)
        {
            if (string.IsNullOrEmpty(id)) continue;
            if (!id.StartsWith("REGION_", StringComparison.OrdinalIgnoreCase)) continue;

            if (!db.themes.Any(t => t != null && string.Equals(t.regionId, id, StringComparison.OrdinalIgnoreCase)))
            {
                db.themes.Add(new RegionTheme { regionId = id });
                changed = true;
            }
        }

        // Remove null/empty entries.
        for (int i = db.themes.Count - 1; i >= 0; i--)
        {
            var t = db.themes[i];
            if (t == null || string.IsNullOrEmpty(t.regionId))
            {
                db.themes.RemoveAt(i);
                changed = true;
            }
        }

        if (changed) EditorUtility.SetDirty(db);
        return changed;
    }
}
