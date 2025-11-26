using UnityEngine;
using UnityEditor;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using Game.Battle.Abilities;
using Game.Localization; // 引用你的 LocalizationItem 定义

public class LocalizationSyncTool : EditorWindow
{
    // 默认路径配置 (根据你之前的描述)
    const string PATH_CN = "Assets/Resources/Localization/zh-Hans/Skills.json";
    const string PATH_EN = "Assets/Resources/Localization/en/Skills.json";

    // 默认 ID 前缀
    const string ID_PREFIX = "SKILL_";

    [MenuItem("Tools/Localization/Sync Abilities to JSON")]
    public static void ShowWindow()
    {
        GetWindow<LocalizationSyncTool>("Sync Skills");
    }

    void OnGUI()
    {
        GUILayout.Label("Ability Localization Sync", EditorStyles.boldLabel);
        EditorGUILayout.Space();

        if (GUILayout.Button("1. Auto-Fill Missing Ability IDs", GUILayout.Height(40)))
        {
            AutoFillIDs();
        }
        EditorGUILayout.HelpBox("Step 1: 检查所有 Ability 资产，如果 ID 为空，根据文件名自动生成 (例如 'HeavySlash' -> 'SKILL_HEAVY_SLASH')。", MessageType.Info);

        EditorGUILayout.Space();

        if (GUILayout.Button("2. Sync to JSON Files", GUILayout.Height(40)))
        {
            SyncToJSON();
        }
        EditorGUILayout.HelpBox($"Step 2: 将所有 Ability ID 同步到:\n{PATH_CN}\n{PATH_EN}\n(只会追加新条目，不会覆盖现有翻译)", MessageType.Info);
    }

    // --- 步骤 1: 自动填充 ScriptableObject 的 ID ---
    void AutoFillIDs()
    {
        string[] guids = AssetDatabase.FindAssets("t:Ability");
        int count = 0;

        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            Ability ability = AssetDatabase.LoadAssetAtPath<Ability>(path);

            if (ability != null && string.IsNullOrEmpty(ability.abilityID))
            {
                // 自动生成 ID: 转大写，加前缀，加下划线
                string rawName = ability.name.Replace(" ", "_").ToUpper();
                // 简单的驼峰转下划线处理 (可选，这里简单直接转大写)
                ability.abilityID = ID_PREFIX + rawName;

                EditorUtility.SetDirty(ability);
                count++;
                Debug.Log($"[AutoFill] Set ID '{ability.abilityID}' for {ability.name}");
            }
        }

        if (count > 0) AssetDatabase.SaveAssets();
        EditorUtility.DisplayDialog("Complete", $"Updated {count} abilities with missing IDs.", "OK");
    }

    // --- 步骤 2: 同步到 JSON ---
    void SyncToJSON()
    {
        // 1. 获取所有有效的 Ability ID
        var allAbilities = LoadAllAbilities();
        if (allAbilities.Count == 0) return;

        // 2. 处理中文
        SyncFile(PATH_CN, allAbilities, "zh-Hans");

        // 3. 处理英文
        SyncFile(PATH_EN, allAbilities, "en");

        AssetDatabase.Refresh();
        EditorUtility.DisplayDialog("Complete", "Localization files updated!", "OK");
    }

    List<Ability> LoadAllAbilities()
    {
        var list = new List<Ability>();
        string[] guids = AssetDatabase.FindAssets("t:Ability");
        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            Ability a = AssetDatabase.LoadAssetAtPath<Ability>(path);
            if (a != null && !string.IsNullOrEmpty(a.abilityID))
            {
                list.Add(a);
            }
            else if (a != null)
            {
                Debug.LogWarning($"[Skip] Ability '{a.name}' has no ID. Run Step 1 first.");
            }
        }
        return list;
    }

    void SyncFile(string path, List<Ability> abilities, string lang)
    {
        // 1. 确保文件存在，不存在则创建空模板
        if (!File.Exists(path))
        {
            EnsureDirectory(path);
            File.WriteAllText(path, "{ \"items\": [] }");
        }

        // 2. 读取并解析
        string jsonContent = File.ReadAllText(path);
        LocalizationItemFile data = JsonUtility.FromJson<LocalizationItemFile>(jsonContent);

        if (data == null) data = new LocalizationItemFile();
        if (data.items == null) data.items = new LocalizationItem[0];

        // 转为 List 方便操作
        List<LocalizationItem> itemList = data.items.ToList();
        int addedCount = 0;

        // 3. 比对并追加
        foreach (var ability in abilities)
        {
            // 检查是否已存在该 ID
            if (itemList.Any(x => x.id == ability.abilityID)) continue;

            // 创建新条目
            LocalizationItem newItem = new LocalizationItem
            {
                id = ability.abilityID,
                name = ability.name, // 默认用文件名作为占位符
                desc = "TODO: Description",
                flavor = "TODO: Flavor text"
            };

            // (可选) 简单的中文默认值处理
            if (lang == "zh-Hans")
            {
                newItem.desc = "待填写：技能描述";
                newItem.flavor = "待填写：背景故事";
            }

            itemList.Add(newItem);
            addedCount++;
        }

        // 4. 只有当有新增时才写回文件
        if (addedCount > 0)
        {
            data.items = itemList.ToArray();
            // prettyPrint = true 让 JSON 格式化好看
            string newJson = JsonUtility.ToJson(data, true);
            File.WriteAllText(path, newJson);
            Debug.Log($"[Sync] Added {addedCount} new keys to {path}");
        }
        else
        {
            Debug.Log($"[Sync] {path} is up to date.");
        }
    }

    void EnsureDirectory(string path)
    {
        string dir = Path.GetDirectoryName(path);
        if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
    }
}