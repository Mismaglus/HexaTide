// Scripts/Editor/GameDebugTool.cs
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using Game.Inventory;
using Game.Units;
using Game.Battle;

namespace Game.EditorTools
{
    public class GameDebugTool : EditorWindow
    {
        // --- Main Navigation ---
        private int _mainTab = 0;
        private readonly string[] _mainTabs = { "Scenes", "Battle Control", "Inventory Injector" };

        // --- Scene Data ---
        private Vector2 _sceneScrollPos;
        private List<string> _scenePaths = new List<string>();
        // ⭐ CONFIG: The folder to search for scenes
        private const string SCENE_SEARCH_FOLDER = "Assets/_Scenes";

        // --- Inventory Data ---
        private List<InventoryItem> _allItems = new List<InventoryItem>();
        private Dictionary<string, List<InventoryItem>> _categorizedItems = new Dictionary<string, List<InventoryItem>>();
        private int _invCategoryTab = 0;
        private readonly string[] _invCategoryNames = { "Consumables", "Relics", "Materials", "All" };
        private Vector2 _invScrollPos;
        private string _searchQuery = "";

        // --- Battle Setup Data ---
        private LootTableSO _selectedLootTable;

        // Shortcuts: Ctrl+G (General Debug)
        [MenuItem("Tools/HexaTide/Game Debugger %g")]
        public static void ShowWindow()
        {
            var window = GetWindow<GameDebugTool>("Game Debugger");
            window.minSize = new Vector2(350, 500);
            window.RefreshAllDatabases();
        }

        private void OnEnable()
        {
            RefreshAllDatabases();
        }

        private void RefreshAllDatabases()
        {
            RefreshSceneList();
            RefreshInventoryDatabase();
        }

        private void OnGUI()
        {
            GUILayout.Space(10);

            // Header Style
            GUIStyle headerStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 16,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = new Color(0.3f, 0.8f, 1f) }
            };
            GUILayout.Label("HEXATIDE DEBUGGER", headerStyle);
            GUILayout.Space(10);

            // Main Tabs
            _mainTab = GUILayout.Toolbar(_mainTab, _mainTabs, GUILayout.Height(30));

            // Horizontal Line
            Rect r = EditorGUILayout.GetControlRect(false, 2);
            EditorGUI.DrawRect(r, Color.gray);
            GUILayout.Space(10);

            switch (_mainTab)
            {
                case 0: DrawSceneSection(); break;
                case 1: DrawBattleSection(); break;
                case 2: DrawInventorySection(); break;
            }
        }

        // =========================================================
        // SECTION 1: SCENE SWITCHER
        // =========================================================
        private void RefreshSceneList()
        {
            _scenePaths.Clear();

            // ⭐ Validate folder exists to prevent errors
            if (!AssetDatabase.IsValidFolder(SCENE_SEARCH_FOLDER))
            {
                // Fallback: If folder doesn't exist, search everywhere but warn
                Debug.LogWarning($"[GameDebugger] Folder '{SCENE_SEARCH_FOLDER}' not found. Showing all scenes in Assets/.");
                string[] allGuids = AssetDatabase.FindAssets("t:Scene");
                foreach (string guid in allGuids)
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    if (path.StartsWith("Assets/")) _scenePaths.Add(path);
                }
                return;
            }

            // ⭐ Search specifically in the requested folder
            string[] guids = AssetDatabase.FindAssets("t:Scene", new[] { SCENE_SEARCH_FOLDER });
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                _scenePaths.Add(path);
            }
        }

        private void DrawSceneSection()
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField($"Scenes in {SCENE_SEARCH_FOLDER}", EditorStyles.boldLabel);
            if (GUILayout.Button("Refresh List", GUILayout.Width(80))) RefreshSceneList();
            EditorGUILayout.EndHorizontal();

            if (_scenePaths.Count == 0)
            {
                EditorGUILayout.HelpBox($"No scenes found in '{SCENE_SEARCH_FOLDER}'.\nCheck spelling or move scenes there.", MessageType.Info);
            }

            GUILayout.Space(5);

            _sceneScrollPos = EditorGUILayout.BeginScrollView(_sceneScrollPos);

            foreach (string path in _scenePaths)
            {
                string sceneName = Path.GetFileNameWithoutExtension(path);

                // Highlight current scene
                string currentPath = UnityEngine.SceneManagement.SceneManager.GetActiveScene().path;
                bool isCurrent = currentPath == path;

                GUI.color = isCurrent ? Color.green : Color.white;

                EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);

                GUILayout.Label(sceneName, EditorStyles.boldLabel, GUILayout.Width(150));
                GUILayout.Label(Path.GetFileName(Path.GetDirectoryName(path)), EditorStyles.miniLabel); // Subfolder name

                if (GUILayout.Button("Open", GUILayout.Width(60)))
                {
                    OpenScene(path);
                }

                EditorGUILayout.EndHorizontal();
                GUI.color = Color.white;
            }

            EditorGUILayout.EndScrollView();
        }

        private void OpenScene(string path)
        {
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                EditorSceneManager.OpenScene(path);
            }
        }

        // =========================================================
        // SECTION 2: BATTLE CONTROL
        // =========================================================
        private void DrawBattleSection()
        {
            // --- Runtime Controls ---
            EditorGUILayout.LabelField("Runtime Controls", EditorStyles.boldLabel);

            if (Application.isPlaying)
            {
                var battleSM = BattleStateMachine.Instance;
                if (battleSM != null)
                {
                    GUI.color = new Color(0.6f, 1f, 0.6f);
                    if (GUILayout.Button("FORCE VICTORY (Win)", GUILayout.Height(40)))
                    {
                        battleSM.DebugForceVictory();
                    }

                    GUI.color = new Color(1f, 0.6f, 0.6f);
                    if (GUILayout.Button("FORCE DEFEAT (Lose)", GUILayout.Height(40)))
                    {
                        battleSM.DebugForceDefeat();
                    }
                    GUI.color = Color.white;

                    GUILayout.Space(5);
                    EditorGUILayout.HelpBox($"Current Turn: {battleSM.CurrentTurn}", MessageType.Info);
                }
                else
                {
                    EditorGUILayout.HelpBox("No BattleStateMachine found in scene.", MessageType.Warning);
                }
            }
            else
            {
                EditorGUILayout.HelpBox("Enter Play Mode to use Battle Controls.", MessageType.Info);
            }

            GUILayout.Space(20);

            // --- Setup / Context ---
            EditorGUILayout.LabelField("Battle Context Setup", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Inject data here BEFORE loading a battle scene to override defaults.", MessageType.None);

            // Loot Table Injection
            GUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("Next Battle Loot:", EditorStyles.miniBoldLabel);
            _selectedLootTable = (LootTableSO)EditorGUILayout.ObjectField("Loot Table", _selectedLootTable, typeof(LootTableSO), false);

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Inject Loot Table"))
            {
                BattleContext.ActiveLootTable = _selectedLootTable;
                Debug.Log($"[Debugger] Injected Loot Table: {(_selectedLootTable ? _selectedLootTable.name : "NULL")}");
            }
            if (GUILayout.Button("Clear Context"))
            {
                BattleContext.Reset();
                Debug.Log("[Debugger] Battle Context Reset.");
            }
            EditorGUILayout.EndHorizontal();

            // Status Display
            if (Application.isPlaying)
            {
                var current = BattleContext.ActiveLootTable;
                string status = current != null ? current.name : "None (Using Scene Default)";
                EditorGUILayout.LabelField($"Active Context: {status}", EditorStyles.miniLabel);
            }
            GUILayout.EndVertical();
        }

        // =========================================================
        // SECTION 3: INVENTORY INJECTOR
        // =========================================================
        private void DrawInventorySection()
        {
            UnitInventory target = FindPlayerInventory();

            // Target Display
            if (target != null)
            {
                GUI.color = Color.green;
                EditorGUILayout.LabelField($"Linked: {target.name} | Slots: {target.Slots.Count}/{target.Capacity}", EditorStyles.helpBox);
                GUI.color = Color.white;
            }
            else
            {
                if (Application.isPlaying)
                    EditorGUILayout.HelpBox("No Player UnitInventory found in active scene.", MessageType.Warning);
                else
                    EditorGUILayout.HelpBox("Play Mode required to modify inventory.", MessageType.Info);
            }

            GUILayout.Space(5);

            // Search Bar
            EditorGUILayout.BeginHorizontal();
            _searchQuery = EditorGUILayout.TextField("Search", _searchQuery);
            if (GUILayout.Button("Refresh Data", GUILayout.Width(100))) RefreshInventoryDatabase();
            EditorGUILayout.EndHorizontal();

            // Sub-Tabs
            _invCategoryTab = GUILayout.Toolbar(_invCategoryTab, _invCategoryNames);
            GUILayout.Space(5);

            // Item Grid
            string currentCategory = _invCategoryNames[_invCategoryTab];
            if (_categorizedItems.ContainsKey(currentCategory))
            {
                DrawItemList(_categorizedItems[currentCategory], target);
            }
        }

        private void DrawItemList(List<InventoryItem> items, UnitInventory target)
        {
            _invScrollPos = EditorGUILayout.BeginScrollView(_invScrollPos);

            var filtered = items.Where(x => x.name.IndexOf(_searchQuery, System.StringComparison.OrdinalIgnoreCase) >= 0).ToList();

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

                // Buttons
                GUI.enabled = target != null && Application.isPlaying;
                if (GUILayout.Button("+1", GUILayout.Width(40), GUILayout.Height(30)))
                {
                    target.TryAddItem(item, 1);
                    Debug.Log($"[Debug] Added 1x {item.name}");
                }
                if (GUILayout.Button("+5", GUILayout.Width(40), GUILayout.Height(30)))
                {
                    target.TryAddItem(item, 5);
                    Debug.Log($"[Debug] Added 5x {item.name}");
                }
                GUI.enabled = true;

                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.EndScrollView();
        }

        // =========================================================
        // HELPERS
        // =========================================================
        private void RefreshInventoryDatabase()
        {
            _allItems.Clear();
            _categorizedItems.Clear();
            _categorizedItems["Consumables"] = new List<InventoryItem>();
            _categorizedItems["Relics"] = new List<InventoryItem>();
            _categorizedItems["Materials"] = new List<InventoryItem>();
            _categorizedItems["All"] = new List<InventoryItem>();

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

        private UnitInventory FindPlayerInventory()
        {
            if (!Application.isPlaying) return null;

            GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
            if (playerObj != null)
            {
                var inv = playerObj.GetComponent<UnitInventory>();
                if (inv != null) return inv;
            }

            var units = FindObjectsByType<BattleUnit>(FindObjectsSortMode.None);
            foreach (var u in units)
            {
                if (u.isPlayer) return u.GetComponent<UnitInventory>();
            }

            return null;
        }
    }
}