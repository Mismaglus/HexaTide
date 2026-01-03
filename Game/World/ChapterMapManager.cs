using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Core.Hex;
using Game.Battle;
using Game.Grid;
using Game.Units;
using System.Linq;

namespace Game.World
{
    public class ChapterMapManager : MonoBehaviour
    {
        public static ChapterMapManager Instance { get; private set; }

        [Header("Grid References")]
        public BattleHexGrid grid;

        [Header("Chapter Settings")]
        [Tooltip("Optional settings database. If assigned, MapScene can load different chapters via FlowContext.CurrentChapterId.")]
        public ChapterSettingsDB settingsDB;

        [Tooltip("Fallback settings if DB lookup fails or FlowContext is empty.")]
        public ChapterSettings defaultSettings;

        [Header("Region Themes")]
        [Tooltip("Optional per-region theme overrides (tile/border materials). This lets regions vary visually without duplicating GridRecipes.")]
        public RegionThemeDB regionThemeDB;

        [Header("Node Icon Layer (MVP)")]
        [Tooltip("Optional icon mapping for ChapterNodeType -> 3D prefab. If a type is not mapped, no icon will be shown for that node.")]
        public ChapterNodeIconLibrary nodeIconLibrary;

        [Tooltip("Optional boss-specific icon mapping (bossId -> prefab). Boss roster (ids) is read from localization Bosses.json.")]
        public BossIconLibrary bossIconLibrary;

        [Header("Debug")]
        [Tooltip("If enabled, prints a summary of node types and icon-mapping coverage after generation.")]
        public bool logNodeSummaryOnGenerate = false;

        [Tooltip("If enabled (Editor only, Play Mode), draws node type labels in the Scene View.")]
        public bool drawNodeTypeLabelsInSceneView = false;

        [Tooltip("Max number of nodes listed when logging missing icon mappings.")]
        public int debugMaxMissingIconLogs = 20;

        // Runtime State
        private int _currentSeed;
        private int _playerMoveCount = 0;
        private int _currentTideRow = int.MinValue;
        private int _minRow;
        private int _maxRow;

        private Unit _playerUnit;
        private static readonly HexCoordsComparer _hexComparer = new HexCoordsComparer();
        private Dictionary<HexCoords, ChapterNode> _nodes = new Dictionary<HexCoords, ChapterNode>(_hexComparer);
        private ChapterSettings _activeSettings;

        private GridRecipe _runtimeRecipe;

        public ChapterSettings ActiveSettings => _activeSettings;

        public int CurrentTideRow => _currentTideRow;
        public int MovesBeforeNextTide
        {
            get
            {
                int step = _activeSettings != null ? _activeSettings.movesPerTideStep : 0;
                if (step <= 0) return 0;
                return step - (_playerMoveCount % step);
            }
        }

        private sealed class HexCoordsComparer : IEqualityComparer<HexCoords>
        {
            public bool Equals(HexCoords a, HexCoords b) => a.q == b.q && a.r == b.r;
            public int GetHashCode(HexCoords h) => (h.q * 397) ^ h.r;
        }

        void Awake()
        {
            Instance = this;
            if (!grid) grid = Object.FindFirstObjectByType<BattleHexGrid>();

            // Disable FogOfWarSystem in Chapter Scene
            var fogSystem = Object.FindFirstObjectByType<Game.Battle.FogOfWarSystem>();
            if (fogSystem != null)
            {
                Debug.LogWarning("[ChapterMapManager] Disabling FogOfWarSystem in Chapter Scene.");
                fogSystem.enableFog = false;
                fogSystem.enabled = false;
            }

            var highlighter = grid != null ? grid.GetComponent<Game.Common.HexHighlighter>() : null;
            if (highlighter) highlighter.ignoreFog = true;
        }

        void Start()
        {
            EnsureFlowDefaults();

            // Load and apply chapter settings BEFORE generating/restoring map.
            var settings = LoadActiveSettings();
            ApplySettings(settings);

            if (MapRuntimeData.HasData)
            {
                // If we are returning from battle, restore act/region routing.
                RestoreFlowFromRuntimeData();

                // Re-resolve settings if needed (for safety).
                var restoredSettings = LoadActiveSettings();
                if (restoredSettings != null)
                {
                    ApplySettings(restoredSettings);
                }

                RestoreMapState();
            }
            else
            {
                GenerateNewMap();
            }

            InitializePlayer();
            UpdateTideVisuals();
        }

        void OnDestroy()
        {
            if (_playerUnit != null && _playerUnit.TryGetComponent<UnitMover>(out var mover))
            {
                mover.OnMoveFinished -= HandlePlayerMove;
            }
        }

        // =========================================================
        // Chapter Settings
        // =========================================================

        private ChapterSettings LoadActiveSettings()
        {
            // Settings are act-specific.
            int act = MapRuntimeData.HasData && MapRuntimeData.CurrentAct > 0 ? MapRuntimeData.CurrentAct : FlowContext.CurrentAct;
            if (act <= 0) act = 1;

            var s = ResolveSettingsByAct(act);
            return s != null ? s : defaultSettings;
        }

        private ChapterSettings ResolveSettingsById(string chapterId)
        {
            if (settingsDB == null) return null;
            return settingsDB.GetSettings(chapterId);
        }

        private ChapterSettings ResolveSettingsByAct(int actNumber)
        {
            if (settingsDB == null) return null;
            return settingsDB.GetSettings(actNumber);
        }

        private void ApplySettings(ChapterSettings settings)
        {
            if (settings == null) return;

            _activeSettings = settings;

            if (grid != null && settings.gridRecipe != null)
            {
                ReplaceRuntimeRecipe(settings.gridRecipe);
                grid.SetRecipe(_runtimeRecipe);
            }
        }

        private void ReplaceRuntimeRecipe(GridRecipe source)
        {
            if (_runtimeRecipe != null)
            {
#if UNITY_EDITOR
                if (!Application.isPlaying) DestroyImmediate(_runtimeRecipe);
                else Destroy(_runtimeRecipe);
#else
                Destroy(_runtimeRecipe);
#endif
            }

            _runtimeRecipe = Instantiate(source);
            _runtimeRecipe.name = $"{source.name}_Runtime";

            ApplyRegionThemeToRecipe(_runtimeRecipe);
        }

        private void ApplyRegionThemeToRecipe(GridRecipe recipe)
        {
            if (recipe == null) return;
            if (regionThemeDB == null) return;
            if (!regionThemeDB.TryGetTheme(FlowContext.CurrentChapterId, out var theme) || theme == null) return;

            if (theme.tileMaterial != null) recipe.tileMaterial = theme.tileMaterial;
            if (theme.borderMaterial != null) recipe.borderMaterial = theme.borderMaterial;
        }

        private static void EnsureFlowDefaults()
        {
            if (FlowContext.CurrentAct <= 0) FlowContext.CurrentAct = 1;
            if (string.IsNullOrEmpty(FlowContext.CurrentChapterId)) FlowContext.CurrentChapterId = "REGION_1";
        }

        private static void RestoreFlowFromRuntimeData()
        {
            if (MapRuntimeData.CurrentAct > 0) FlowContext.CurrentAct = MapRuntimeData.CurrentAct;

            if (!string.IsNullOrEmpty(MapRuntimeData.CurrentRegionId))
            {
                FlowContext.CurrentChapterId = MapRuntimeData.CurrentRegionId;
                return;
            }

            // Back-compat: older saves only had CurrentChapterId which might contain legacy Act2_/Act3_ strings.
            if (!string.IsNullOrEmpty(MapRuntimeData.CurrentChapterId))
            {
                var legacy = MapRuntimeData.CurrentChapterId;
                if (legacy.StartsWith("REGION_"))
                {
                    FlowContext.CurrentChapterId = legacy;
                    return;
                }

                if (legacy.StartsWith("Act2_"))
                {
                    FlowContext.CurrentAct = 2;
                    FlowContext.CurrentChapterId = legacy.Contains("Right") ? "REGION_4" : "REGION_2";
                    return;
                }
                if (legacy.StartsWith("Act3_"))
                {
                    FlowContext.CurrentAct = 3;
                    FlowContext.CurrentChapterId = "REGION_7";
                    return;
                }
                if (legacy.StartsWith("Act4_"))
                {
                    FlowContext.CurrentAct = 4;
                    FlowContext.CurrentChapterId = "REGION_8";
                    return;
                }
            }
        }

        // =========================================================
        // Generation / Restoration
        // =========================================================

        void GenerateNewMap()
        {
            _currentSeed = (int)System.DateTime.Now.Ticks;
            GenerateGridGeometry(_currentSeed);
            AssignContent();
            _currentTideRow = _minRow - 1;
        }

        void RestoreMapState()
        {
            Debug.Log("[ChapterMapManager] Restoring Map from Save...");
            _currentSeed = MapRuntimeData.MapSeed;
            GenerateGridGeometry(_currentSeed);
            AssignContent();

            _currentTideRow = MapRuntimeData.CurrentTideRow;
            _playerMoveCount = MapRuntimeData.MovesTaken;

            foreach (var coord in MapRuntimeData.ClearedNodes)
            {
                if (_nodes.TryGetValue(coord, out var node))
                {
                    node.SetCleared(true);
                }
            }
        }

        void GenerateGridGeometry(int seed)
        {
            if (grid == null)
            {
                Debug.LogError("[ChapterMapManager] grid is null.");
                return;
            }

            var activeRecipe = _runtimeRecipe != null ? _runtimeRecipe : (_activeSettings != null ? _activeSettings.gridRecipe : null);
            if (activeRecipe == null)
            {
                Debug.LogError("[ChapterMapManager] ChapterSettings.gridRecipe is null. Assign a GridRecipe in ChapterSettings.");
                return;
            }

            // Ensure the grid uses the active recipe, then set its runtime seed.
            grid.SetRecipe(activeRecipe);
            activeRecipe.randomSeed = seed;
            grid.Rebuild();

            _nodes.Clear();
            var allTiles = grid.EnumerateTiles().ToList();
            if (allTiles.Count == 0) return;

            _minRow = int.MaxValue;
            _maxRow = int.MinValue;

            foreach (var tile in allTiles)
            {
                if (tile.Coords.r < _minRow) _minRow = tile.Coords.r;
                if (tile.Coords.r > _maxRow) _maxRow = tile.Coords.r;

                var cell = tile.GetComponent<HexCell>();
                if (cell != null)
                {
                    cell.SetFogStatus(FogStatus.Visible);
                    cell.RefreshFogVisuals();
                }

                var node = tile.GetComponent<ChapterNode>();
                if (node == null) node = tile.gameObject.AddComponent<ChapterNode>();
                _nodes[tile.Coords] = node;
                node.Initialize(ChapterNodeType.NormalEnemy);
            }

            // Create placeholder icons immediately (will be updated after AssignContent)
            ApplyIconsToAllNodes();

            if (logNodeSummaryOnGenerate)
            {
                StartCoroutine(DumpNodeSummaryNextFrame());
            }

            Debug.Log($"[ChapterMapManager] Generated {_nodes.Count} nodes. MinRow={_minRow}, MaxRow={_maxRow}.");
        }

        void AssignContent()
        {
            Game.Common.GameRandom.Init(_currentSeed);

            // Pre-pick boss ids for this map (one per slot) so icon + gameplay are consistent.
            var pickedBossIdsByGate = PickBossIdsForSlots();

            var bottomRowNodes = GetNodesInRow(_minRow);
            var topRowNodes = GetNodesInRow(_maxRow);

            // 1. Start Node (Bottom Center)
            HexCoords startCoords = GetCenterOfRow(bottomRowNodes);
            if (_nodes.ContainsKey(startCoords)) _nodes[startCoords].Initialize(ChapterNodeType.Start);

            // 2. Boss / Gates (Top Row)
            if (_activeSettings != null && _activeSettings.IsAct1)
            {
                // Act 1: Three boss nodes on the top row that act as exits (left/right/skip).
                if (topRowNodes.Count >= 3)
                {
                    // Sort by Q to find Left/Right
                    topRowNodes.Sort((a, b) => a.GetComponent<HexCell>().Coords.q.CompareTo(b.GetComponent<HexCell>().Coords.q));

                    var leftNode = topRowNodes[0];
                    var rightNode = topRowNodes[topRowNodes.Count - 1];
                    var centerNode = topRowNodes[topRowNodes.Count / 2];

                    pickedBossIdsByGate.TryGetValue(GateKind.LeftGate, out var leftBoss);
                    pickedBossIdsByGate.TryGetValue(GateKind.RightGate, out var rightBoss);
                    pickedBossIdsByGate.TryGetValue(GateKind.SkipGate, out var skipBoss);

                    leftNode.Initialize(ChapterNodeType.Boss, leftBoss, GateKind.LeftGate, 2, "REGION_2");
                    rightNode.Initialize(ChapterNodeType.Boss, rightBoss, GateKind.RightGate, 2, "REGION_4");
                    centerNode.Initialize(ChapterNodeType.Boss, skipBoss, GateKind.SkipGate, 3, "REGION_7");
                }
                else
                {
                    Debug.LogWarning("Top row too narrow for 3 gates, placing single Boss.");
                    HexCoords bossCoords = GetCenterOfRow(topRowNodes);
                    if (_nodes.ContainsKey(bossCoords))
                    {
                        pickedBossIdsByGate.TryGetValue(GateKind.None, out var bossId);
                        _nodes[bossCoords].Initialize(ChapterNodeType.Boss, bossId);
                    }
                }
            }
            else
            {
                // Act 2+: Single Boss at top
                HexCoords bossCoords = GetCenterOfRow(topRowNodes);
                if (_nodes.ContainsKey(bossCoords))
                {
                    pickedBossIdsByGate.TryGetValue(GateKind.None, out var bossId);
                    _nodes[bossCoords].Initialize(ChapterNodeType.Boss, bossId, GateKind.None, NextActForCurrentAct(), NextRegionForCurrentAct());
                }
            }

            // 3. Random Content (Elites, Merchants, Mystery)
            var candidates = _nodes.Values.Where(n => n.type == ChapterNodeType.NormalEnemy).ToList();
            ShuffleInPlace(candidates);

            int eliteCount = _activeSettings != null ? _activeSettings.eliteCount : 0;
            int merchantCount = _activeSettings != null ? _activeSettings.merchantCount : 0;
            int mysteryCount = _activeSettings != null ? _activeSettings.mysteryCount : 0;
            int emptyCount = _activeSettings != null ? _activeSettings.emptyCount : 0;

            int noEliteBottomRows = _activeSettings != null ? Mathf.Max(0, _activeSettings.noEliteBottomRows) : 0;
            int merchantMinSeparation = _activeSettings != null ? Mathf.Max(0, _activeSettings.merchantMinSeparation) : 0;

            // Rule 1: bottom X rows cannot contain elites.
            int eliteMinAllowedRow = _minRow + noEliteBottomRows;

            int elitesPlaced = 0;
            for (int i = candidates.Count - 1; i >= 0 && elitesPlaced < eliteCount; i--)
            {
                var node = candidates[i];
                if (node == null) { candidates.RemoveAt(i); continue; }
                var coords = GetCoords(node);
                if (coords.r < eliteMinAllowedRow) continue;
                node.Initialize(ChapterNodeType.EliteEnemy);
                candidates.RemoveAt(i);
                elitesPlaced++;
            }
            if (elitesPlaced < eliteCount)
            {
                Debug.LogWarning($"[ChapterMapManager] Could not place all elites. Requested={eliteCount}, placed={elitesPlaced}. Rule noEliteBottomRows={noEliteBottomRows} may be too strict for this grid.");
            }

            // Rule 2: merchants must be spaced apart (hex distance) strictly greater than X.
            int merchantsPlaced = 0;
            var merchantCoords = new List<HexCoords>();
            for (int i = candidates.Count - 1; i >= 0 && merchantsPlaced < merchantCount; i--)
            {
                var node = candidates[i];
                if (node == null) { candidates.RemoveAt(i); continue; }
                var coords = GetCoords(node);

                bool ok = true;
                if (merchantMinSeparation > 0)
                {
                    for (int m = 0; m < merchantCoords.Count; m++)
                    {
                        if (coords.DistanceTo(merchantCoords[m]) <= merchantMinSeparation)
                        {
                            ok = false;
                            break;
                        }
                    }
                }

                if (!ok) continue;

                node.Initialize(ChapterNodeType.Merchant);
                candidates.RemoveAt(i);
                merchantCoords.Add(coords);
                merchantsPlaced++;
            }
            if (merchantsPlaced < merchantCount)
            {
                Debug.LogWarning($"[ChapterMapManager] Could not place all merchants. Requested={merchantCount}, placed={merchantsPlaced}. Rule merchantMinSeparation={merchantMinSeparation} may be too strict for this grid.");
            }

            // Remaining types: place freely.
            int mysteriesPlaced = 0;
            for (int i = candidates.Count - 1; i >= 0 && mysteriesPlaced < mysteryCount; i--)
            {
                var node = candidates[i];
                if (node == null) { candidates.RemoveAt(i); continue; }
                node.Initialize(ChapterNodeType.Mystery);
                candidates.RemoveAt(i);
                mysteriesPlaced++;
            }

            int emptiesPlaced = 0;
            for (int i = candidates.Count - 1; i >= 0 && emptiesPlaced < emptyCount; i--)
            {
                var node = candidates[i];
                if (node == null) { candidates.RemoveAt(i); continue; }
                node.Initialize(ChapterNodeType.Empty);
                candidates.RemoveAt(i);
                emptiesPlaced++;
            }

            ApplyIconsToAllNodes();

            if (logNodeSummaryOnGenerate)
            {
                StartCoroutine(DumpNodeSummaryNextFrame());
            }
        }

        private static void ShuffleInPlace<T>(List<T> list)
        {
            if (list == null || list.Count <= 1) return;
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = Game.Common.GameRandom.Range(0, i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
        }

        private static HexCoords GetCoords(ChapterNode node)
        {
            if (node == null) return default;
            var cell = node.GetComponent<HexCell>();
            return cell != null ? cell.Coords : default;
        }

        private IEnumerator DumpNodeSummaryNextFrame()
        {
            // Wait a frame so any Destroy() calls have taken effect.
            yield return null;
            DumpNodeSummary();
        }

        [ContextMenu("Debug/Dump Node Summary")]
        public void DumpNodeSummary()
        {
            if (_nodes == null || _nodes.Count == 0)
            {
                Debug.Log("[ChapterMapManager] DumpNodeSummary: no nodes (run in Play Mode after grid generation). ");
                return;
            }

            var counts = new Dictionary<ChapterNodeType, int>();
            int withAnyIconMapping = 0;
            int missingIconMapping = 0;

            int iconRootsFound = 0;
            int iconModelsFound = 0;
            int iconModelsInactive = 0;
            int renderersFound = 0;
            int renderersEnabled = 0;
            int mappedButNoIconObject = 0;

            int missingListed = 0;
            System.Text.StringBuilder missingSb = null;

            int mappedMissingListed = 0;
            System.Text.StringBuilder mappedMissingSb = null;

            foreach (var kvp in _nodes)
            {
                var node = kvp.Value;
                if (node == null) continue;

                counts.TryGetValue(node.type, out int c);
                counts[node.type] = c + 1;

                bool hasBossPrefab = false;
                if (!string.IsNullOrEmpty(node.BossId) && bossIconLibrary != null)
                {
                    hasBossPrefab = bossIconLibrary.TryGetPrefab(node.BossId, out var bossPrefab) && bossPrefab != null;
                }

                bool hasNodePrefab = false;
                if (nodeIconLibrary != null)
                {
                    hasNodePrefab = nodeIconLibrary.TryGetPrefab(node.type, out var nodePrefab) && nodePrefab != null;
                }

                bool hasMapping = hasBossPrefab || hasNodePrefab;
                if (hasMapping) withAnyIconMapping++;
                else
                {
                    missingIconMapping++;
                    if (missingListed < debugMaxMissingIconLogs)
                    {
                        missingSb ??= new System.Text.StringBuilder();
                        var cell = node.GetComponent<HexCell>();
                        var coords = cell != null ? cell.Coords : default;
                        missingSb.AppendLine($"- {coords.q},{coords.r} type={node.type} bossId={(string.IsNullOrEmpty(node.BossId) ? "(none)" : node.BossId)}");
                        missingListed++;
                    }
                }

                // Count actual icon objects in hierarchy.
                Transform iconRoot = null;
                for (int i = 0; i < node.transform.childCount; i++)
                {
                    var child = node.transform.GetChild(i);
                    if (child != null && child.name.StartsWith("ChapterNodeIcon", System.StringComparison.Ordinal))
                    {
                        iconRoot = child;
                        break;
                    }
                }

                if (iconRoot != null)
                {
                    iconRootsFound++;
                    if (iconRoot.childCount > 0)
                    {
                        iconModelsFound += iconRoot.childCount;
                        for (int i = 0; i < iconRoot.childCount; i++)
                        {
                            var child = iconRoot.GetChild(i);
                            if (child == null) continue;
                            if (!child.gameObject.activeInHierarchy) iconModelsInactive++;

                            var rs = child.GetComponentsInChildren<Renderer>(true);
                            if (rs != null && rs.Length > 0)
                            {
                                renderersFound += rs.Length;
                                foreach (var r in rs)
                                {
                                    if (r != null && r.enabled) renderersEnabled++;
                                }
                            }
                        }
                    }
                }
                else if (hasMapping)
                {
                    // If mapping exists but no icon object exists, something is deleting icons or ApplyIcon isn't running.
                    mappedButNoIconObject++;
                    if (mappedMissingListed < debugMaxMissingIconLogs)
                    {
                        mappedMissingSb ??= new System.Text.StringBuilder();
                        var cell = node.GetComponent<HexCell>();
                        var coords = cell != null ? cell.Coords : default;
                        mappedMissingSb.AppendLine($"- {coords.q},{coords.r} type={node.type} (mapping exists, but no ChapterNodeIcon_* child)");
                        mappedMissingListed++;
                    }
                }
            }

            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"[ChapterMapManager] Node Summary (nodes={_nodes.Count})");
            sb.AppendLine($"- nodeIconLibrary={(nodeIconLibrary != null ? nodeIconLibrary.name : "(null)")}");
            sb.AppendLine($"- bossIconLibrary={(bossIconLibrary != null ? bossIconLibrary.name : "(null)")}");
            sb.AppendLine($"- iconMapping: mapped={withAnyIconMapping}, missing={missingIconMapping}");
            sb.AppendLine($"- iconObjects: iconRoots={iconRootsFound}, iconModels={iconModelsFound}, mappedButNoIconObject={mappedButNoIconObject}");
            sb.AppendLine($"- iconVisibility: iconModelsInactive={iconModelsInactive}, renderersFound={renderersFound}, renderersEnabled={renderersEnabled}");
            sb.AppendLine("- counts:");
            foreach (var kv in counts.OrderBy(k => k.Key.ToString()))
            {
                sb.AppendLine($"  - {kv.Key}: {kv.Value}");
            }

            if (missingSb != null)
            {
                sb.AppendLine("- first missing icon mappings:");
                sb.Append(missingSb.ToString());
            }

            if (mappedMissingSb != null)
            {
                sb.AppendLine("- first mapped nodes missing icon objects:");
                sb.Append(mappedMissingSb.ToString());
            }

            Debug.Log(sb.ToString());
        }

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            if (!drawNodeTypeLabelsInSceneView) return;
            if (!Application.isPlaying) return;
            if (_nodes == null || _nodes.Count == 0) return;

            Gizmos.color = new Color(1f, 1f, 1f, 0.5f);
            UnityEditor.Handles.color = Color.white;
            foreach (var kvp in _nodes)
            {
                var node = kvp.Value;
                if (node == null) continue;
                var pos = node.transform.position + Vector3.up * 0.2f;
                Gizmos.DrawSphere(pos, 0.05f);

                // Labels are drawn only when this manager is selected to reduce clutter and avoid Gizmos filtering surprises.
                if (UnityEditor.Selection.activeGameObject == gameObject)
                {
                    UnityEditor.Handles.Label(pos, node.type.ToString());
                }
            }
        }
#endif

        private Dictionary<GateKind, string> PickBossIdsForSlots()
        {
            var result = new Dictionary<GateKind, string>();

            if (bossIconLibrary == null) return result;

            var allBossIds = BossIconLibrary.LoadBossIdsFromLocalization();
            if (allBossIds == null || allBossIds.Length == 0) return result;

            var regionId = FlowContext.CurrentChapterId;
            int regionNumber = TryGetRegionNumber(regionId);
            int act = FlowContext.CurrentAct;

            // Deterministic RNG per slot.
            string PickFromPrefix(string prefix, int salt)
            {
                var pool = allBossIds.Where(id => !string.IsNullOrEmpty(id) && id.StartsWith(prefix, System.StringComparison.OrdinalIgnoreCase)).ToList();
                if (pool.Count == 0) return null;
                var rng = new System.Random(unchecked(_currentSeed * 19349663) ^ salt);
                return pool[rng.Next(0, pool.Count)];
            }

            if (_activeSettings != null && _activeSettings.IsAct1)
            {
                // Act1: left/right -> Act2, skip -> Act3.
                if (regionNumber >= 1 && regionNumber <= 6)
                {
                    var prefixTo2 = $"BOSS_R{regionNumber}_TOACT2_";
                    var prefixTo3 = $"BOSS_R{regionNumber}_TOACT3_";

                    var left = PickFromPrefix(prefixTo2, unchecked((int)0x13579BDF));
                    var right = PickFromPrefix(prefixTo2, unchecked((int)0x2468ACE0));
                    var skip = PickFromPrefix(prefixTo3, unchecked((int)0xDEADBEEF));

                    if (!string.IsNullOrEmpty(left)) result[GateKind.LeftGate] = left;
                    if (!string.IsNullOrEmpty(right)) result[GateKind.RightGate] = right;
                    if (!string.IsNullOrEmpty(skip)) result[GateKind.SkipGate] = skip;
                }
            }
            else
            {
                // Act2+: single boss.
                string bossId = null;
                if (act == 2 && regionNumber >= 1 && regionNumber <= 6)
                {
                    bossId = PickFromPrefix($"BOSS_R{regionNumber}_TOACT3_", unchecked((int)0x0F00BA11));
                }
                else if (act == 3)
                {
                    bossId = PickFromPrefix("BOSS_R7_", unchecked((int)0x00C0FFEE));
                }
                else if (act == 4)
                {
                    bossId = PickFromPrefix("BOSS_R8", unchecked((int)0xBADC0DE));
                    if (string.IsNullOrEmpty(bossId)) bossId = "BOSS_R8";
                }
                else
                {
                    bossId = bossIconLibrary.PickBossId(_currentSeed);
                }

                if (!string.IsNullOrEmpty(bossId)) result[GateKind.None] = bossId;
            }

            return result;
        }

        private static int TryGetRegionNumber(string regionId)
        {
            if (string.IsNullOrEmpty(regionId)) return 0;
            if (!regionId.StartsWith("REGION_", System.StringComparison.OrdinalIgnoreCase)) return 0;
            var suffix = regionId.Substring("REGION_".Length);
            return int.TryParse(suffix, out var n) ? n : 0;
        }

        private static int NextActForCurrentAct()
        {
            var act = FlowContext.CurrentAct;
            if (act == 1) return 2;
            if (act == 2) return 3;
            if (act == 3) return 4;
            return 0;
        }

        private static string NextRegionForCurrentAct()
        {
            var act = FlowContext.CurrentAct;
            if (act == 2) return "REGION_7";
            if (act == 3) return "REGION_8";
            return null;
        }

        private void ApplyIconsToAllNodes()
        {
            foreach (var kvp in _nodes)
            {
                if (kvp.Value == null) continue;
                kvp.Value.ApplyIcon(nodeIconLibrary, bossIconLibrary);
            }
        }

        // =========================================================
        // State Management & Player Logic
        // =========================================================

        public void SaveMapState()
        {
            List<HexCoords> cleared = new List<HexCoords>();
            foreach (var kvp in _nodes)
            {
                if (kvp.Value.isCleared) cleared.Add(kvp.Key);
            }

            HexCoords playerPos = _playerUnit != null ? _playerUnit.Coords : default;
            MapRuntimeData.Save(_currentSeed, playerPos, _currentTideRow, _playerMoveCount, cleared);
        }

        void InitializePlayer()
        {
            var units = FindObjectsByType<Unit>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            foreach (var u in units)
            {
                if (u.GetComponent<Game.Core.FactionMembership>()?.side == Game.Core.Side.Player)
                {
                    _playerUnit = u;
                    break;
                }
            }

            if (_playerUnit != null)
            {
                var mover = _playerUnit.GetComponent<UnitMover>();
                if (mover != null) mover.OnMoveFinished += HandlePlayerMove;

                if (MapRuntimeData.HasData && _nodes.ContainsKey(MapRuntimeData.PlayerPosition))
                {
                    _playerUnit.WarpTo(MapRuntimeData.PlayerPosition);
                }
                else
                {
                    var startNode = _nodes.Values.FirstOrDefault(n => n.type == ChapterNodeType.Start);
                    if (startNode != null) _playerUnit.WarpTo(startNode.GetComponent<HexCell>().Coords);
                }
            }
        }

        void HandlePlayerMove(HexCoords from, HexCoords to)
        {
            MapRuntimeData.PlayerPosition = to; // Save pos immediately to memory

            if (_nodes.TryGetValue(to, out var node))
            {
                // Unity "fake null": destroyed objects compare equal to null.
                // If we keep them in _nodes, any member access (e.g., node.isCleared) can throw MissingReferenceException.
                if (node == null)
                {
                    _nodes.Remove(to);
                    Debug.LogWarning($"[ChapterMapManager] Removed destroyed ChapterNode at {to} (move {from} -> {to}).");
                    return;
                }

                if (!node.isCleared)
                {
                    node.Interact();

                    // Interact() may synchronously LoadScene (e.g., EncounterNode.StartEncounter).
                    // If so, this ChapterMap scene's objects can be destroyed immediately.
                    // Bail out to avoid touching stale references (including tide logic).
                    if (node == null)
                    {
                        Debug.LogWarning($"[ChapterMapManager] Movement commit interrupted by scene change at {to} (move {from} -> {to}).");
                        return;
                    }
                }
            }

            _playerMoveCount++;

            int step = _activeSettings != null ? _activeSettings.movesPerTideStep : 0;
            if (step > 0 && _playerMoveCount % step == 0)
            {
                StartCoroutine(RaiseTideRoutine());
            }
        }

        IEnumerator RaiseTideRoutine()
        {
            float delay = _activeSettings != null ? _activeSettings.tideAnimationDelay : 0f;
            yield return new WaitForSeconds(delay);
            _currentTideRow++;
            UpdateTideVisuals();
            CheckPlayerTrapped();
        }

        void UpdateTideVisuals()
        {
            foreach (var kvp in _nodes)
            {
                if (kvp.Key.r <= _currentTideRow)
                {
                    kvp.Value.GetComponent<HexCell>().SetFlooded(true);
                }
            }
        }

        void CheckPlayerTrapped()
        {
            if (_playerUnit == null) return;
            var cell = _nodes.ContainsKey(_playerUnit.Coords) ? _nodes[_playerUnit.Coords].GetComponent<HexCell>() : null;
            if (cell != null && cell.IsFlooded) PunishAndRespawnPlayer();
        }

        void PunishAndRespawnPlayer()
        {
            HexCoords safeSpot = FindNearestSafeTile(_playerUnit.Coords);
            if (safeSpot.Equals(_playerUnit.Coords)) Debug.LogError("Game Over - Tide consumed all!");
            else
            {
                Debug.Log($"[Tide] Rescuing player to {safeSpot}");
                _playerUnit.WarpTo(safeSpot);
            }
        }

        List<ChapterNode> GetNodesInRow(int r) => _nodes.Values.Where(n => n.GetComponent<HexCell>().Coords.r == r).ToList();

        HexCoords GetCenterOfRow(List<ChapterNode> rowNodes)
        {
            if (rowNodes == null || rowNodes.Count == 0) return default;
            rowNodes.Sort((a, b) => a.GetComponent<HexCell>().Coords.q.CompareTo(b.GetComponent<HexCell>().Coords.q));
            return rowNodes[rowNodes.Count / 2].GetComponent<HexCell>().Coords;
        }

        HexCoords FindNearestSafeTile(HexCoords origin)
        {
            Queue<HexCoords> q = new Queue<HexCoords>();
            HashSet<HexCoords> visited = new HashSet<HexCoords>();
            q.Enqueue(origin); visited.Add(origin);
            while (q.Count > 0)
            {
                HexCoords current = q.Dequeue();
                if (_nodes.TryGetValue(current, out var node) && !node.GetComponent<HexCell>().IsFlooded) return current;
                foreach (var n in current.Neighbors())
                {
                    if (!visited.Contains(n) && _nodes.ContainsKey(n))
                    {
                        visited.Add(n);
                        q.Enqueue(n);
                    }
                }
            }
            return origin;
        }

        public ChapterNode GetNodeAt(HexCoords coords)
        {
            if (_nodes.TryGetValue(coords, out var node) && node != null) return node;
            return null;
        }

        public void DebugDumpNodes()
        {
            string s = "";
            foreach (var k in _nodes.Keys) s += k.ToString() + " ";
            Debug.Log($"[ChapterMapManager] Current Nodes: {s}");
        }
    }
}
