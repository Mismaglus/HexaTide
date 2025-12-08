// Scripts/Editor/ItemLocalizationSyncTool.cs
using UnityEngine;
using UnityEditor;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using Game.Inventory;
using Game.Localization;

public class ItemLocalizationSyncTool : EditorWindow
{
    // JSON 路径 (请确保文件夹存在)
    const string PATH_CN = "Assets/Resources/Localization/zh-Hans/Items.json";
    const string PATH_EN = "Assets/Resources/Localization/en/Items.json";

    const string ID_PREFIX = "ITEM_";

    [MenuItem("Tools/Localization/Sync Items to JSON")]
    public static void ShowWindow()
    {
        GetWindow<ItemLocalizationSyncTool>("Sync Items");
    }

    void OnGUI()
    {
        GUILayout.Label("Inventory Item Localization", EditorStyles.boldLabel);
        EditorGUILayout.Space();

        if (GUILayout.Button("1. Auto-Fill Missing Item IDs", GUILayout.Height(40)))
        {
            AutoFillIDs();
        }
        EditorGUILayout.HelpBox("检查所有 InventoryItem，如果 ID 为空，根据文件名自动生成 (例如 'BasicPotion' -> 'ITEM_BASIC_POTION')。", MessageType.Info);

        EditorGUILayout.Space();

        if (GUILayout.Button("2. Sync to JSON Files", GUILayout.Height(40)))
        {
            SyncToJSON();
        }
        EditorGUILayout.HelpBox($"将 ID 同步到:\n{PATH_CN}\n{PATH_EN}", MessageType.Info);
    }

    void AutoFillIDs()
    {
        string[] guids = AssetDatabase.FindAssets("t:InventoryItem");
        int count = 0;
        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            InventoryItem item = AssetDatabase.LoadAssetAtPath<InventoryItem>(path);
            if (item != null && string.IsNullOrEmpty(item.itemID))
            {
                string rawName = item.name.Replace(" ", "_").ToUpper();
                item.itemID = ID_PREFIX + rawName;
                EditorUtility.SetDirty(item);
                count++;
            }
        }
        if (count > 0) AssetDatabase.SaveAssets();
        EditorUtility.DisplayDialog("Done", $"Updated {count} items.", "OK");
    }

    void SyncToJSON()
    {
        var allItems = LoadAllItems();
        if (allItems.Count == 0) return;

        SyncFile(PATH_CN, allItems, "zh-Hans");
        SyncFile(PATH_EN, allItems, "en");
        AssetDatabase.Refresh();
        EditorUtility.DisplayDialog("Done", "Item localization files updated!", "OK");
    }

    List<InventoryItem> LoadAllItems()
    {
        var list = new List<InventoryItem>();
        string[] guids = AssetDatabase.FindAssets("t:InventoryItem");
        foreach (string guid in guids)
        {
            var item = AssetDatabase.LoadAssetAtPath<InventoryItem>(AssetDatabase.GUIDToAssetPath(guid));
            if (item != null && !string.IsNullOrEmpty(item.itemID)) list.Add(item);
        }
        return list;
    }

    void SyncFile(string path, List<InventoryItem> items, string lang)
    {
        if (!File.Exists(path))
        {
            string dir = Path.GetDirectoryName(path);
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
            File.WriteAllText(path, "{ \"items\": [] }");
        }

        string jsonContent = File.ReadAllText(path);
        LocalizationItemFile data = JsonUtility.FromJson<LocalizationItemFile>(jsonContent);
        if (data == null) data = new LocalizationItemFile();
        if (data.items == null) data.items = new LocalizationItem[0];

        List<LocalizationItem> itemList = data.items.ToList();
        int added = 0;

        foreach (var item in items)
        {
            if (itemList.Any(x => x.id == item.itemID)) continue;

            LocalizationItem newItem = new LocalizationItem
            {
                id = item.itemID,
                name = item.name,
                desc = (lang == "zh-Hans") ? "恢复 ?? 点生命值" : "Restores ?? HP",
                flavor = ""
            };
            itemList.Add(newItem);
            added++;
        }

        if (added > 0)
        {
            data.items = itemList.ToArray();
            File.WriteAllText(path, JsonUtility.ToJson(data, true));
            Debug.Log($"Added {added} keys to {path}");
        }
    }
}