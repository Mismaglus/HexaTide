// Scripts/Editor/InventoryDebugTool.cs
using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;
using Game.Inventory;
using Game.Units;
using Game.Battle;

namespace Game.EditorTools
{
    public class InventoryDebugTool : EditorWindow
    {
        private List<InventoryItem> _allItems = new List<InventoryItem>();
        private Dictionary<string, List<InventoryItem>> _categorizedItems = new Dictionary<string, List<InventoryItem>>();

        private string[] _tabs = new string[] { "Consumables", "Relics", "Materials", "All" };
        private int _selectedTab = 0;
        private Vector2 _scrollPos;
        private string _searchQuery = "";

        // Shortcut: Ctrl + I
        [MenuItem("Tools/HexaTide/Inventory Debugger %i")]
        public static void ShowWindow()
        {
            var window = GetWindow<InventoryDebugTool>("Inventory Debug");
            window.minSize = new Vector2(300, 400);
            window.RefreshDatabase();
        }

        private void OnEnable()
        {
            RefreshDatabase();
        }

        private void RefreshDatabase()
        {
            _allItems.Clear();
            _categorizedItems.Clear();
            _categorizedItems["Consumables"] = new List<InventoryItem>();
            _categorizedItems["Relics"] = new List<InventoryItem>();
            _categorizedItems["Materials"] = new List<InventoryItem>();
            _categorizedItems["All"] = new List<InventoryItem>();

            // Find all InventoryItems in the project
            // We search specifically in the defined folder, or globally if preferred.
            string[] guids = AssetDatabase.FindAssets("t:InventoryItem", new[] { "Assets/_Assets/Items", "Assets/Data/Items" });
            // Note: If you have items elsewhere, remove the folder filter or add paths. 
            // Fallback to global search if folder specific returns nothing useful during dev
            if (guids.Length == 0) guids = AssetDatabase.FindAssets("t:InventoryItem");

            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                InventoryItem item = AssetDatabase.LoadAssetAtPath<InventoryItem>(path);

                if (item == null) continue;

                _allItems.Add(item);
                _categorizedItems["All"].Add(item);

                if (item is ConsumableItem) _categorizedItems["Consumables"].Add(item);
                else if (item is RelicItem) _categorizedItems["Relics"].Add(item);
                else if (item is MaterialItem) _categorizedItems["Materials"].Add(item);
            }
        }

        private void OnGUI()
        {
            GUILayout.Space(10);
            GUILayout.Label("Inventory Item Injector", EditorStyles.boldLabel);

            // 1. Target Selection Display
            UnitInventory targetInventory = FindPlayerInventory();
            if (targetInventory == null)
            {
                EditorGUILayout.HelpBox("No Player Unit with UnitInventory found in the active scene.", MessageType.Warning);
                if (!Application.isPlaying)
                {
                    EditorGUILayout.HelpBox("Enter Play Mode to inject items.", MessageType.Info);
                }
            }
            else
            {
                GUI.color = Color.green;
                EditorGUILayout.LabelField($"Target: {targetInventory.name} (Capacity: {targetInventory.Slots.Count}/{targetInventory.Capacity})", EditorStyles.helpBox);
                GUI.color = Color.white;
            }

            GUILayout.Space(10);

            // 2. Search & Refresh
            EditorGUILayout.BeginHorizontal();
            _searchQuery = EditorGUILayout.TextField("Search", _searchQuery);
            if (GUILayout.Button("Refresh", GUILayout.Width(60)))
            {
                RefreshDatabase();
            }
            EditorGUILayout.EndHorizontal();

            // 3. Tabs
            _selectedTab = GUILayout.Toolbar(_selectedTab, _tabs);

            GUILayout.Space(5);

            // 4. Item Grid
            string currentCategory = _tabs[_selectedTab];
            if (_categorizedItems.ContainsKey(currentCategory))
            {
                DrawItemList(_categorizedItems[currentCategory], targetInventory);
            }
        }

        private void DrawItemList(List<InventoryItem> items, UnitInventory target)
        {
            _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos);

            // Filter by Search
            var filtered = items.Where(x => x.name.IndexOf(_searchQuery, System.StringComparison.OrdinalIgnoreCase) >= 0).ToList();

            // Layout items in a grid/list
            foreach (var item in filtered)
            {
                EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);

                // Icon
                Texture2D icon = item.icon != null ? item.icon.texture : Texture2D.whiteTexture;
                GUILayout.Label(icon, GUILayout.Width(32), GUILayout.Height(32));

                // Info
                EditorGUILayout.BeginVertical();
                GUILayout.Label(item.name, EditorStyles.boldLabel);
                GUILayout.Label(item.type.ToString(), EditorStyles.miniLabel);
                EditorGUILayout.EndVertical();

                GUILayout.FlexibleSpace();

                // Actions
                GUI.enabled = target != null && Application.isPlaying;

                if (GUILayout.Button("+1", GUILayout.Width(40), GUILayout.Height(30)))
                {
                    target.TryAddItem(item, 1);
                    Debug.Log($"[DebugTool] Added 1x {item.name}");
                }

                if (GUILayout.Button("+5", GUILayout.Width(40), GUILayout.Height(30)))
                {
                    target.TryAddItem(item, 5);
                    Debug.Log($"[DebugTool] Added 5x {item.name}");
                }

                GUI.enabled = true;

                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.EndScrollView();
        }

        private UnitInventory FindPlayerInventory()
        {
            if (!Application.isPlaying) return null;

            // Strategy 1: Find by Tag
            GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
            if (playerObj != null)
            {
                var inv = playerObj.GetComponent<UnitInventory>();
                if (inv != null) return inv;
            }

            // Strategy 2: Find by BattleUnit isPlayer flag
            var units = FindObjectsByType<BattleUnit>(FindObjectsSortMode.None);
            foreach (var u in units)
            {
                if (u.isPlayer)
                {
                    var inv = u.GetComponent<UnitInventory>();
                    if (inv != null) return inv;
                }
            }

            return null;
        }
    }
}