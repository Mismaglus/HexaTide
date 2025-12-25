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

        [Header("Act Settings")]
        public bool isAct1 = true; // Simple toggle for now to distinguish generation logic

        // Runtime State
        private int _currentSeed;
        private int _playerMoveCount = 0;
        private int _currentTideRow = int.MinValue;
        private int _minRow;
        private int _maxRow;

        private Unit _playerUnit;
        private static readonly HexCoordsComparer _hexComparer = new HexCoordsComparer();
        private Dictionary<HexCoords, ChapterNode> _nodes = new Dictionary<HexCoords, ChapterNode>(_hexComparer);

        public int CurrentTideRow => _currentTideRow;
        public int MovesBeforeNextTide => movesPerTideStep - (_playerMoveCount % movesPerTideStep);

        private sealed class HexCoordsComparer : IEqualityComparer<HexCoords>
        {
            public bool Equals(HexCoords a, HexCoords b) => a.q == b.q && a.r == b.r;
            public int GetHashCode(HexCoords h) => (h.q * 397) ^ h.r;
        }

        void Awake()
        {
            Instance = this;
            if (!grid) grid = FindFirstObjectByType<BattleHexGrid>();

            // Disable FogOfWarSystem in Chapter Scene
            var fogSystem = FindFirstObjectByType<Game.Battle.FogOfWarSystem>();
            if (fogSystem != null)
            {
                Debug.LogWarning("[ChapterMapManager] Disabling FogOfWarSystem in Chapter Scene.");
                fogSystem.enableFog = false;
                fogSystem.enabled = false;
            }

            var highlighter = grid.GetComponent<Game.Common.HexHighlighter>();
            if (highlighter) highlighter.ignoreFog = true;
        }

        void Start()
        {
            if (MapRuntimeData.HasData)
            {
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
            if (grid == null || mapRecipe == null) return;

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

            Debug.Log($"[ChapterMapManager] Generated {_nodes.Count} nodes. MinRow={_minRow}, MaxRow={_maxRow}.");
        }

        void AssignContent()
        {
            Game.Common.GameRandom.Init(_currentSeed);

            var bottomRowNodes = GetNodesInRow(_minRow);
            var topRowNodes = GetNodesInRow(_maxRow);

            // 1. Start Node (Bottom Center)
            HexCoords startCoords = GetCenterOfRow(bottomRowNodes);
            if (_nodes.ContainsKey(startCoords)) _nodes[startCoords].Initialize(ChapterNodeType.Start);

            // 2. Boss / Gates (Top Row)
            if (isAct1)
            {
                // Act 1: Left Gate, Skip Gate (Center), Right Gate
                if (topRowNodes.Count >= 3)
                {
                    // Sort by Q to find Left/Right
                    topRowNodes.Sort((a, b) => a.GetComponent<HexCell>().Coords.q.CompareTo(b.GetComponent<HexCell>().Coords.q));

                    var leftNode = topRowNodes[0];
                    var rightNode = topRowNodes[topRowNodes.Count - 1];
                    var centerNode = topRowNodes[topRowNodes.Count / 2];

                    leftNode.Initialize(ChapterNodeType.Gate_Left);
                    rightNode.Initialize(ChapterNodeType.Gate_Right);
                    centerNode.Initialize(ChapterNodeType.Gate_Skip);
                }
                else
                {
                    Debug.LogWarning("Top row too narrow for 3 gates, placing single Boss.");
                    HexCoords bossCoords = GetCenterOfRow(topRowNodes);
                    if (_nodes.ContainsKey(bossCoords)) _nodes[bossCoords].Initialize(ChapterNodeType.Boss);
                }
            }
            else
            {
                // Act 2+: Single Boss at top
                HexCoords bossCoords = GetCenterOfRow(topRowNodes);
                if (_nodes.ContainsKey(bossCoords)) _nodes[bossCoords].Initialize(ChapterNodeType.Boss);
            }

            // 3. Random Content (Elites, Merchants, Mystery)
            var validCandidates = _nodes.Values.Where(n => n.type == ChapterNodeType.NormalEnemy).ToList();
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
                if (!node.isCleared)
                {
                    node.Interact();
                }
            }

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