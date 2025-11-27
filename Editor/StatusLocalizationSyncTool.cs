using UnityEngine;
using UnityEditor;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using Game.Battle.Status;
using Game.Localization; // 引用 LocalizationItem 定义

public class StatusLocalizationSyncTool : EditorWindow
{
    // JSON 文件保存路径
    const string PATH_CN = "Assets/Resources/Localization/zh-Hans/Status.json";
    const string PATH_EN = "Assets/Resources/Localization/en/Status.json";

    const string ID_PREFIX = "STATUS_";

    [MenuItem("Tools/Localization/Sync Status to JSON")]
    public static void ShowWindow()
    {
        GetWindow<StatusLocalizationSyncTool>("Sync Status");
    }

    void OnGUI()
    {
        GUILayout.Label("Status Effect Localization Sync", EditorStyles.boldLabel);
        EditorGUILayout.Space();

        if (GUILayout.Button("1. Auto-Fill Missing Status IDs", GUILayout.Height(40)))
        {
            AutoFillIDs();
        }
        EditorGUILayout.HelpBox("Step 1: 检查所有 Status 资产，如果 ID 为空，根据文件名自动生成 (例如 'Poison' -> 'STATUS_POISON')。", MessageType.Info);

        EditorGUILayout.Space();

        if (GUILayout.Button("2. Sync to JSON Files", GUILayout.Height(40)))
        {
            SyncToJSON();
        }
        EditorGUILayout.HelpBox($"Step 2: 将所有 Status ID 同步到:\n{PATH_CN}\n{PATH_EN}\n(只会追加新条目，不会覆盖现有翻译)", MessageType.Info);
    }

    // --- 步骤 1: 自动填充 ScriptableObject 的 ID ---
    void AutoFillIDs()
    {
        string[] guids = AssetDatabase.FindAssets("t:StatusDefinition");
        int count = 0;

        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            StatusDefinition status = AssetDatabase.LoadAssetAtPath<StatusDefinition>(path);

            if (status != null && string.IsNullOrEmpty(status.statusID))
            {
                // 自动生成 ID: 转大写，加前缀，去空格
                string rawName = status.name.Replace(" ", "_").ToUpper();
                status.statusID = ID_PREFIX + rawName;

                EditorUtility.SetDirty(status);
                count++;
                Debug.Log($"[Status AutoFill] Set ID '{status.statusID}' for {status.name}");
            }
        }

        if (count > 0) AssetDatabase.SaveAssets();
        EditorUtility.DisplayDialog("Complete", $"Updated {count} status definitions with missing IDs.", "OK");
    }

    // --- 步骤 2: 同步到 JSON ---
    void SyncToJSON()
    {
        // 1. 加载所有 Status
        var allStatus = LoadAllStatus();
        if (allStatus.Count == 0)
        {
            EditorUtility.DisplayDialog("Info", "No StatusDefinition assets found.", "OK");
            return;
        }

        // 2. 处理中文
        SyncFile(PATH_CN, allStatus, "zh-Hans");

        // 3. 处理英文
        SyncFile(PATH_EN, allStatus, "en");

        AssetDatabase.Refresh();
        EditorUtility.DisplayDialog("Complete", "Status localization files updated!", "OK");
    }

    List<StatusDefinition> LoadAllStatus()
    {
        var list = new List<StatusDefinition>();
        string[] guids = AssetDatabase.FindAssets("t:StatusDefinition");
        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            StatusDefinition s = AssetDatabase.LoadAssetAtPath<StatusDefinition>(path);
            if (s != null && !string.IsNullOrEmpty(s.statusID))
            {
                list.Add(s);
            }
            else if (s != null)
            {
                Debug.LogWarning($"[Skip] Status '{s.name}' has no ID. Run Step 1 first.");
            }
        }
        return list;
    }

    void SyncFile(string path, List<StatusDefinition> statusList, string lang)
    {
        // 1. 确保文件存在
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

        List<LocalizationItem> itemList = data.items.ToList();
        int addedCount = 0;

        // 3. 比对并追加
        foreach (var status in statusList)
        {
            // 检查是否已存在该 ID
            if (itemList.Any(x => x.id == status.statusID)) continue;

            // 创建新条目
            LocalizationItem newItem = new LocalizationItem
            {
                id = status.statusID,
                name = status.name, // 默认用文件名
                desc = "TODO: Description",
                flavor = "" // Flavor 对 Status 可能不重要，留空
            };

            if (lang == "zh-Hans")
            {
                newItem.desc = "待填写：状态描述";
            }

            itemList.Add(newItem);
            addedCount++;
        }

        // 4. 写回
        if (addedCount > 0)
        {
            data.items = itemList.ToArray();
            string newJson = JsonUtility.ToJson(data, true); // Pretty print
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