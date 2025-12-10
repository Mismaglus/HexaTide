// Scripts/Editor/InventoryDebugTool.cs
using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;
using Game.Inventory;
using Game.Units;
using Game.Battle; // Added for BattleContext

namespace Game.EditorTools
{
    public class InventoryDebugTool : EditorWindow
    {
        private List<InventoryItem> _allItems = new List<InventoryItem>();
        private Dictionary<string, List<InventoryItem>> _categorizedItems = new Dictionary<string, List<InventoryItem>>();

        // Loot Table Debugging
        private LootTableSO _selectedLootTable;

        private string[] _tabs = new string[] { "Consumables", "Relics", "Materials", "All", "Battle Setup" };
        private int _selectedTab = 0;
        private Vector2 _scrollPos;
        private string _searchQuery = "";

        [MenuItem("Tools/HexaTide/Inventory Debugger %i")]
        public static void ShowWindow()
        {
            var window = GetWindow<InventoryDebugTool>("Inventory Debug");
            window.minSize = new Vector2(350, 500);
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
            _categorizedItems["Battle Setup"] = new List<InventoryItem>(); // Dummy list

            string[] guids = AssetDatabase.FindAssets("t:InventoryItem");
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
            GUILayout.Label("Inventory & Battle Debugger", EditorStyles.boldLabel);

            // 1. Target Info
            UnitInventory targetInventory = FindPlayerInventory();
            if (targetInventory != null)
            {
                GUI.color = Color.green;
                EditorGUILayout.LabelField($"Player Linked: {targetInventory.name}", EditorStyles.miniLabel);
                GUI.color = Color.white;
            }

            GUILayout.Space(5);

            // 2. Tabs
            _selectedTab = GUILayout.Toolbar(_selectedTab, _tabs);
            GUILayout.Space(5);

            if (_tabs[_selectedTab] == "Battle Setup")
            {
                DrawBattleSetup();
            }
            else
            {
                DrawInventoryInjector(targetInventory);
            }
        }

        private void DrawBattleSetup()
        {
            EditorGUILayout.LabelField("Battle Context Injection", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Set the Loot Table here. When the next battle ends (Victory), this table will be used instead of the scene default.", MessageType.Info);

            _selectedLootTable = (LootTableSO)EditorGUILayout.ObjectField("Next Victory Loot:", _selectedLootTable, typeof(LootTableSO), false);

            if (GUILayout.Button("Inject into BattleContext", GUILayout.Height(30)))
            {
                BattleContext.ActiveLootTable = _selectedLootTable;
                Debug.Log($"[Debug] Injected {_selectedLootTable?.name ?? "NULL"} into BattleContext.");
            }

            GUILayout.Space(10);
            EditorGUILayout.LabelField("Current Runtime Context:", EditorStyles.miniBoldLabel);

            if (Application.isPlaying)
            {
                var current = BattleContext.ActiveLootTable;
                if (current != null)
                {
                    GUI.color = Color.cyan;
                    EditorGUILayout.LabelField($"Active Table: {current.name}");
                    GUI.color = Color.white;
                }
                else
                {
                    EditorGUILayout.LabelField("Active Table: <None> (Will use Scene Default)");
                }
            }
            else
            {
                EditorGUILayout.LabelField("(Enter Play Mode to view live context)");
            }
        }

        private void DrawInventoryInjector(UnitInventory target)
        {
            // Search
            EditorGUILayout.BeginHorizontal();
            _searchQuery = EditorGUILayout.TextField("Search", _searchQuery);
            if (GUILayout.Button("Refresh", GUILayout.Width(60))) RefreshDatabase();
            EditorGUILayout.EndHorizontal();

            string currentCategory = _tabs[_selectedTab];
            if (!_categorizedItems.ContainsKey(currentCategory)) return;

            List<InventoryItem> items = _categorizedItems[currentCategory];
            var filtered = items.Where(x => x.name.IndexOf(_searchQuery, System.StringComparison.OrdinalIgnoreCase) >= 0).ToList();

            _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos);

            foreach (var item in filtered)
            {
                EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);

                Texture2D icon = item.icon != null ? item.icon.texture : Texture2D.whiteTexture;
                GUILayout.Label(icon, GUILayout.Width(32), GUILayout.Height(32));

                EditorGUILayout.BeginVertical();
                GUILayout.Label(item.name, EditorStyles.boldLabel);
                GUILayout.Label(item.type.ToString(), EditorStyles.miniLabel);
                EditorGUILayout.EndVertical();

                GUILayout.FlexibleSpace();

                GUI.enabled = target != null && Application.isPlaying;

                if (GUILayout.Button("+1", GUILayout.Width(40), GUILayout.Height(30)))
                {
                    target.TryAddItem(item, 1);
                    Debug.Log($"[DebugTool] Added 1x {item.name}");
                }

                GUI.enabled = true;
                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.EndScrollView();
        }

        private UnitInventory FindPlayerInventory()
        {
            if (!Application.isPlaying) return null;
            GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
            if (playerObj != null) return playerObj.GetComponent<UnitInventory>();

            var units = FindObjectsByType<BattleUnit>(FindObjectsSortMode.None);
            foreach (var u in units) if (u.isPlayer) return u.GetComponent<UnitInventory>();

            return null;
        }
    }
}