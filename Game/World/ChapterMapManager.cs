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
        private Dictionary<HexCoords, ChapterNode> _nodes = new Dictionary<HexCoords, ChapterNode>();

        public int CurrentTideRow => _currentTideRow;
        public int MovesBeforeNextTide => movesPerTideStep - (_playerMoveCount % movesPerTideStep);

        void Awake()
        {
            Instance = this;
            if (!grid) grid = FindFirstObjectByType<BattleHexGrid>();
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
                }

                // ⭐ 2. Setup Node Logic
                var node = tile.GetComponent<ChapterNode>();
                if (node == null) node = tile.gameObject.AddComponent<ChapterNode>();

                _nodes[tile.Coords] = node;
                node.Initialize(ChapterNodeType.NormalEnemy);
            }
        }

        void AssignContent()
        {
            // Initialize RNG with the Map Seed to ensure nodes are placed in the same spots
            Game.Common.GameRandom.Init(_currentSeed);

            var bottomRowNodes = GetNodesInRow(_minRow);
            var topRowNodes = GetNodesInRow(_maxRow);

            HexCoords startCoords = GetCenterOfRow(bottomRowNodes);
            HexCoords bossCoords = GetCenterOfRow(topRowNodes);

            if (_nodes.ContainsKey(startCoords)) _nodes[startCoords].Initialize(ChapterNodeType.Start);
            if (_nodes.ContainsKey(bossCoords)) _nodes[bossCoords].Initialize(ChapterNodeType.Boss);

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
                if (MapRuntimeData.HasData)
                {
                    // Restore position
                    _playerUnit.WarpTo(MapRuntimeData.PlayerPosition);
                }
                else
                {
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
            if (_nodes.TryGetValue(coords, out var node))
            {
                return node;
            }
            return null;
        }
    }
}