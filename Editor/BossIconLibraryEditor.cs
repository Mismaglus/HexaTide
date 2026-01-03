using System;
using System.Collections.Generic;
using Game.World;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Inspector UX: pick bossId via dropdown sourced from Resources/Localization/en/Bosses.json.
/// </summary>
[CustomEditor(typeof(BossIconLibrary))]
public sealed class BossIconLibraryEditor : Editor
{
    SerializedProperty _entries;
    SerializedProperty _defaultBossPrefab;

    string[] _bossIds = Array.Empty<string>();

    void OnEnable()
    {
        _entries = serializedObject.FindProperty("entries");
        _defaultBossPrefab = serializedObject.FindProperty("defaultBossPrefab");
        ReloadBossIds();
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        EditorGUILayout.PropertyField(_defaultBossPrefab);

        using (new EditorGUILayout.HorizontalScope())
        {
            EditorGUILayout.LabelField("Boss IDs (from Bosses.json)", EditorStyles.boldLabel);
            if (GUILayout.Button("Reload", GUILayout.Width(80)))
            {
                ReloadBossIds();
            }
        }

        using (new EditorGUILayout.HorizontalScope())
        {
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Populate", GUILayout.Width(100)))
            {
                PopulateEntriesFromRoster();
            }
        }

        if (_bossIds.Length == 0)
        {
            EditorGUILayout.HelpBox(
                "No boss ids found. Ensure there is a Resources file at Assets/Resources/Localization/en/Bosses.json and it contains an 'items' array with 'id' fields.",
                MessageType.Warning);
        }

        EditorGUILayout.Space(6);
        EditorGUILayout.LabelField("Prefab Mapping", EditorStyles.boldLabel);

        DrawEntriesList();

        serializedObject.ApplyModifiedProperties();
    }

    void PopulateEntriesFromRoster()
    {
        ReloadBossIds();
        if (_bossIds.Length == 0) return;

        var lib = (BossIconLibrary)target;
        Undo.RecordObject(lib, "Populate Boss Icon Library");

        var prefabById = new Dictionary<string, GameObject>(StringComparer.OrdinalIgnoreCase);
        var extras = new List<BossIconLibrary.Entry>();

        if (lib.entries != null)
        {
            foreach (var e in lib.entries)
            {
                if (string.IsNullOrWhiteSpace(e.bossId))
                {
                    extras.Add(e);
                    continue;
                }

                if (!prefabById.ContainsKey(e.bossId))
                    prefabById[e.bossId] = e.prefab;
                else if (prefabById[e.bossId] == null && e.prefab != null)
                    prefabById[e.bossId] = e.prefab;
            }
        }

        var newEntries = new List<BossIconLibrary.Entry>(_bossIds.Length + extras.Count);
        foreach (var id in _bossIds)
        {
            prefabById.TryGetValue(id, out var prefab);
            newEntries.Add(new BossIconLibrary.Entry { bossId = id, prefab = prefab });
        }

        // Keep any entries not in roster (custom/legacy ids) appended at the end.
        if (lib.entries != null)
        {
            var rosterSet = new HashSet<string>(_bossIds, StringComparer.OrdinalIgnoreCase);
            foreach (var e in lib.entries)
            {
                if (string.IsNullOrWhiteSpace(e.bossId)) continue;
                if (!rosterSet.Contains(e.bossId))
                    newEntries.Add(e);
            }
        }

        lib.entries = newEntries;
        EditorUtility.SetDirty(lib);

        serializedObject.Update();
        _entries = serializedObject.FindProperty("entries");
        Repaint();
    }

    void DrawEntriesList()
    {
        if (_entries == null) return;

        for (int i = 0; i < _entries.arraySize; i++)
        {
            var element = _entries.GetArrayElementAtIndex(i);
            var bossIdProp = element.FindPropertyRelative("bossId");
            var prefabProp = element.FindPropertyRelative("prefab");

            using (new EditorGUILayout.VerticalScope("box"))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField($"Entry {i}", EditorStyles.boldLabel);
                    if (GUILayout.Button("-", GUILayout.Width(26)))
                    {
                        _entries.DeleteArrayElementAtIndex(i);
                        break;
                    }
                }

                DrawBossIdDropdown(bossIdProp);
                EditorGUILayout.PropertyField(prefabProp);

                var id = bossIdProp.stringValue;
                if (!string.IsNullOrEmpty(id))
                {
                    string preview = BossIconLibrary.GetLocalizedBossName(id);
                    EditorGUILayout.LabelField("Name Preview", string.IsNullOrEmpty(preview) ? "(missing)" : preview);
                }
            }
        }

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Add Entry"))
            {
                _entries.arraySize += 1;
            }
        }
    }

    void DrawBossIdDropdown(SerializedProperty bossIdProp)
    {
        if (_bossIds.Length == 0)
        {
            EditorGUILayout.PropertyField(bossIdProp, new GUIContent("Boss Id"));
            return;
        }

        string current = bossIdProp.stringValue;
        int idx = Array.IndexOf(_bossIds, current);

        // Add a "Custom" option at the end.
        string[] options = new string[_bossIds.Length + 1];
        Array.Copy(_bossIds, options, _bossIds.Length);
        options[^1] = "<Custom>";

        int shownIdx = idx >= 0 ? idx : options.Length - 1;
        int newIdx = EditorGUILayout.Popup("Boss Id", shownIdx, options);

        if (newIdx >= 0 && newIdx < _bossIds.Length)
        {
            bossIdProp.stringValue = _bossIds[newIdx];
        }
        else
        {
            bossIdProp.stringValue = EditorGUILayout.TextField("Custom Id", current);
        }
    }

    void ReloadBossIds()
    {
        _bossIds = BossIconLibrary.LoadBossIdsFromLocalization();
        Array.Sort(_bossIds, StringComparer.OrdinalIgnoreCase);
        Repaint();
    }
}
