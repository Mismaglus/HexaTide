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
        public GridRecipe mapRecipe;

        [Header("Tide Configuration")]
        public int movesPerTideStep = 3;
        public float tideAnimationDelay = 0.5f;

        [Header("Generation Settings")]
        public int eliteCount = 3;
        public int merchantCount = 2;
        public int mysteryCount = 4;

        // Runtime State
        private int _currentSeed;
        private int _playerMoveCount = 0;
        private int _currentTideRow = int.MinValue;
        private int _minRow;
        private int _maxRow;

        private Unit _playerUnit;
        // Explicit comparer to avoid any hash/equality surprises with structs
        private static readonly HexCoordsComparer _hexComparer = new HexCoordsComparer();
        private Dictionary<HexCoords, ChapterNode> _nodes = new Dictionary<HexCoords, ChapterNode>(_hexComparer);

        public int CurrentTideRow => _currentTideRow;
        public int MovesBeforeNextTide => movesPerTideStep - (_playerMoveCount % movesPerTideStep);

        // Dedicated equality comparer for HexCoords to ensure consistent dictionary lookups
        private sealed class HexCoordsComparer : IEqualityComparer<HexCoords>
        {
            public bool Equals(HexCoords a, HexCoords b) => a.q == b.q && a.r == b.r;
            public int GetHashCode(HexCoords h) => (h.q * 397) ^ h.r;
        }

        void Awake()
        {
            Instance = this;
            if (!grid) grid = FindFirstObjectByType<BattleHexGrid>();

            // Force disable FogOfWarSystem if present in this scene
            var fogSystem = FindFirstObjectByType<Game.Battle.FogOfWarSystem>();
            if (fogSystem != null)
            {
                Debug.LogWarning("[ChapterMapManager] Found FogOfWarSystem in Chapter Scene. Disabling it to prevent conflicts.");
                fogSystem.enableFog = false; // Disable logic
                fogSystem.enabled = false;   // Disable component
            }

            // Disable Fog Visualization in Highlighter
            var highlighter = grid.GetComponent<Game.Common.HexHighlighter>();
            if (highlighter)
            {
                highlighter.ignoreFog = true;
            }
        }

        void Start()
        {
            // ⭐ Phase 4 Logic: Check for saved state
            if (MapRuntimeData.HasData)
            {
                RestoreMapState();
            }
            else
            {
                GenerateNewMap();
            }

            InitializePlayer();

            // Apply visual updates after player is placed
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
        // Generation / Restoration
        // =========================================================

        void GenerateNewMap()
        {
            // Pick a new seed
            _currentSeed = (int)System.DateTime.Now.Ticks;

            GenerateGridGeometry(_currentSeed);
            AssignContent(); // Randomly place content

            // Tide starts 1 row below
            _currentTideRow = _minRow - 1;
        }

        void RestoreMapState()
        {
            Debug.Log("[ChapterMapManager] Restoring Map from Save...");

            // 1. Restore Seed & Geometry
            _currentSeed = MapRuntimeData.MapSeed;
            GenerateGridGeometry(_currentSeed);

            // 2. Re-run Content Assignment (Deterministic because we reset RNG with same seed)
            AssignContent();

            // 3. Restore Vars
            _currentTideRow = MapRuntimeData.CurrentTideRow;
            _playerMoveCount = MapRuntimeData.MovesTaken;

            // 4. Apply "Cleared" status
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
            if (grid == null || mapRecipe == null) return;

            // Ensure determinism for holes
            mapRecipe.randomSeed = seed;
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

                // ⭐ 1. Force Map Visibility Logic
                // Get the cell and force it to be visible immediately
                var cell = tile.GetComponent<HexCell>();
                if (cell != null)
                {
                    cell.SetFogStatus(FogStatus.Visible);
                    // Also ensure the visual is updated immediately
                    cell.RefreshFogVisuals();
                }

                // ⭐ 2. Setup Node Logic
                var node = tile.GetComponent<ChapterNode>();
                if (node == null) node = tile.gameObject.AddComponent<ChapterNode>();

                if (_nodes.ContainsKey(tile.Coords))
                {
                    Debug.LogWarning($"[ChapterMapManager] Duplicate node found at {tile.Coords}. Overwriting.");
                }
                _nodes[tile.Coords] = node;
                node.Initialize(ChapterNodeType.NormalEnemy);
            }

            Debug.Log($"[ChapterMapManager] Generated {_nodes.Count} nodes. MinRow={_minRow}, MaxRow={_maxRow}. Grid Child Count: {grid.transform.childCount}");

            // Debug: Print all node coordinates
            // string coords = string.Join(", ", _nodes.Keys.Select(k => k.ToString()));
            // Debug.Log($"[ChapterMapManager] Node Coords: {coords}");
        }

        void AssignContent()
        {
            // Initialize RNG with the Map Seed to ensure nodes are placed in the same spots
            Game.Common.GameRandom.Init(_currentSeed);

            var bottomRowNodes = GetNodesInRow(_minRow);
            var topRowNodes = GetNodesInRow(_maxRow);

            HexCoords startCoords = GetCenterOfRow(bottomRowNodes);

            if (_nodes.ContainsKey(startCoords)) _nodes[startCoords].Initialize(ChapterNodeType.Start);

            // Sort top row by Q to identify Left/Center/Right
            topRowNodes.Sort((a, b) => a.GetComponent<HexCell>().Coords.q.CompareTo(b.GetComponent<HexCell>().Coords.q));

            // Assign Gates to Top Row
            if (topRowNodes.Count >= 3)
            {
                // Left
                topRowNodes[0].Initialize(ChapterNodeType.LeftGate);
                // Right
                topRowNodes[topRowNodes.Count - 1].Initialize(ChapterNodeType.RightGate);
                // Center (Skip)
                topRowNodes[topRowNodes.Count / 2].Initialize(ChapterNodeType.SkipGate);
            }
            else if (topRowNodes.Count > 0)
            {
                // Fallback if map is too narrow
                topRowNodes[topRowNodes.Count / 2].Initialize(ChapterNodeType.SkipGate);
            }

            var validCandidates = _nodes.Values.Where(n => n.type == ChapterNodeType.NormalEnemy).ToList();

            // Deterministic Shuffle
            validCandidates.Sort((a, b) => Game.Common.GameRandom.Range(0, 2) == 0 ? -1 : 1);

            int assigned = 0;
            for (int i = 0; i < eliteCount && assigned < validCandidates.Count; i++)
                validCandidates[assigned++].Initialize(ChapterNodeType.EliteEnemy);

            for (int i = 0; i < merchantCount && assigned < validCandidates.Count; i++)
                validCandidates[assigned++].Initialize(ChapterNodeType.Merchant);

            for (int i = 0; i < mysteryCount && assigned < validCandidates.Count; i++)
                validCandidates[assigned++].Initialize(ChapterNodeType.Mystery);
        }

        // =========================================================
        // State Management
        // =========================================================

        public void SaveMapState()
        {
            // Gather cleared nodes
            List<HexCoords> cleared = new List<HexCoords>();
            foreach (var kvp in _nodes)
            {
                if (kvp.Value.isCleared) cleared.Add(kvp.Key);
            }

            // Current player position (or target if moving, but usually called before load)
            HexCoords playerPos = _playerUnit != null ? _playerUnit.Coords : default;

            MapRuntimeData.Save(_currentSeed, playerPos, _currentTideRow, _playerMoveCount, cleared);
        }

        // =========================================================
        // Player & Gameplay
        // =========================================================

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

                // Position Player
                if (MapRuntimeData.HasData && _nodes.ContainsKey(MapRuntimeData.PlayerPosition))
                {
                    // Restore position
                    _playerUnit.WarpTo(MapRuntimeData.PlayerPosition);
                }
                else
                {
                    if (MapRuntimeData.HasData)
                    {
                        Debug.LogWarning($"[ChapterMapManager] Saved player position {MapRuntimeData.PlayerPosition} is invalid for current map. Resetting to Start.");
                    }

                    // Start position
                    var startNode = _nodes.Values.FirstOrDefault(n => n.type == ChapterNodeType.Start);
                    if (startNode != null) _playerUnit.WarpTo(startNode.GetComponent<HexCell>().Coords);
                }
            }
        }

        void HandlePlayerMove(HexCoords from, HexCoords to)
        {
            // 1. Save current position immediately so we don't lose it if we crash/reload
            MapRuntimeData.PlayerPosition = to;

            // 2. Interaction
            if (_nodes.TryGetValue(to, out var node))
            {
                if (!node.isCleared)
                {
                    // We DO NOT auto-interact here for Battles anymore, 
                    // because we need to Save BEFORE scene load.
                    // ChapterNode.Interact() handles the flow.
                    node.Interact();
                }
            }

            // 3. Tide Logic
            _playerMoveCount++;
            if (_playerMoveCount % movesPerTideStep == 0)
            {
                StartCoroutine(RaiseTideRoutine());
            }
        }

        IEnumerator RaiseTideRoutine()
        {
            yield return new WaitForSeconds(tideAnimationDelay);
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
                foreach (var n in current.Neighbors()) if (!visited.Contains(n) && _nodes.ContainsKey(n)) { visited.Add(n); q.Enqueue(n); }
            }
            return origin;
        }

        public ChapterNode GetNodeAt(HexCoords coords)
        {
            // Primary lookup
            if (_nodes.TryGetValue(coords, out var node))
            {
                // Unity destroyed objects compare equal to null; clean up if stale
                if (node != null)
                {
                    return node;
                }

                Debug.LogWarning($"[ChapterMapManager] Node at {coords} reference was destroyed. Rebuilding entry.");
                _nodes.Remove(coords);
            }

            // Fallback: linear search to guard against any comparer/hash mismatch
            foreach (var kvp in _nodes)
            {
                if (kvp.Key.q == coords.q && kvp.Key.r == coords.r && kvp.Value != null)
                {
                    Debug.LogWarning($"[ChapterMapManager] GetNodeAt fallback matched {coords} via linear scan. Updating dictionary entry.");
                    _nodes[coords] = kvp.Value;
                    return kvp.Value;
                }
            }

            // Reconstruct from grid cell if possible
            if (grid != null && grid.TryGetCell(coords, out var cell) && cell != null)
            {
                var rebuilt = cell.GetComponent<ChapterNode>() ?? cell.gameObject.AddComponent<ChapterNode>();
                _nodes[coords] = rebuilt;
                Debug.LogWarning($"[ChapterMapManager] Recreated ChapterNode at {coords} from grid cell.");
                return rebuilt;
            }

            Debug.LogWarning($"[ChapterMapManager] Node at {coords} requested but not found. Total Nodes: {_nodes.Count}");
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