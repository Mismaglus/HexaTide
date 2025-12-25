using UnityEngine;
using UnityEngine.InputSystem;
using Core.Hex;
using Game.Grid;
using Game.Units;
using Game.Battle; // For BattleHexGrid ref
using System.Collections.Generic;
using Game.Common;

namespace Game.World
{
    /// <summary>
    /// Handles player input and movement specifically for the Chapter Map.
    /// Decoupled from BattleRules. Uses ChapterPathfinder.
    /// </summary>
    public class ChapterMovementSystem : MonoBehaviour
    {
        [Header("References")]
        public BattleHexGrid grid;
        public HexHighlighter highlighter;
        public ChapterOutlineManager outlineManager; // Changed to ChapterOutlineManager

        [Header("Settings")]
        public LayerMask terrainLayer = ~0;
        public bool doubleClickToConfirm = true;

        [Header("Visuals")]
        // Removed ghostPrefab logic as per refactor
        // public GameObject ghostPrefab; 
        // private GameObject _activeGhost;

        // State
        private Camera _cam;
        private Unit _playerUnit;
        private UnitMover _playerMover;

        private HexCoords? _hoveredCoords;
        private HexCoords? _plannedDestination; // Where we WANT to go (awaiting confirmation)
        private List<HexCoords> _currentPath;

        void Start()
        {
            _cam = Camera.main;
            if (!grid) grid = FindFirstObjectByType<BattleHexGrid>();
            if (!highlighter) highlighter = FindFirstObjectByType<HexHighlighter>();
            if (!outlineManager) outlineManager = FindFirstObjectByType<ChapterOutlineManager>();

            FindPlayer();
        }

        void FindPlayer()
        {
            // Find unit tagged as Player Side
            var units = FindObjectsByType<Game.Core.FactionMembership>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            foreach (var f in units)
            {
                if (f.side == Game.Core.Side.Player)
                {
                    _playerUnit = f.GetComponent<Unit>();
                    _playerMover = f.GetComponent<UnitMover>();
                    break;
                }
            }
        }

        void Update()
        {
            // Safety checks
            if (_playerUnit == null) { FindPlayer(); return; }
            if (_playerMover != null && _playerMover.IsMoving) return; // Block input while moving

            HandleInput();
        }

        void HandleInput()
        {
            // 1. Cancel Logic (Right Click)
            if (Mouse.current.rightButton.wasPressedThisFrame)
            {
                ClearPlan();
                return;
            }

            // 2. Raycast Logic
            if (Mouse.current == null) return;
            Vector2 mousePos = Mouse.current.position.ReadValue();

            if (HexRaycaster.TryPick(_cam, mousePos, out var go, out var tag, (int)terrainLayer))
            {
                // On Hover Change
                if (!_hoveredCoords.HasValue || !_hoveredCoords.Value.Equals(tag.Coords))
                {
                    _hoveredCoords = tag.Coords;
                    OnHoverTile(_hoveredCoords.Value);
                }

                // On Click
                if (Mouse.current.leftButton.wasPressedThisFrame)
                {
                    OnLeftClick(_hoveredCoords.Value);
                }
            }
            else
            {
                // Mouse off grid
                if (_hoveredCoords.HasValue)
                {
                    _hoveredCoords = null;
                    if (_plannedDestination == null)
                    {
                        highlighter.ClearVisuals(); // Only clear if not planning
                        if (outlineManager) outlineManager.Hide();

                        // Clear Unit Highlight
                        if (_playerUnit != null)
                        {
                            var uh = _playerUnit.GetComponentInChildren<UnitHighlighter>();
                            if (uh) uh.SetHover(false);
                        }
                    }
                }
            }
        }

        // --- Logic ---

        void OnHoverTile(HexCoords tile)
        {
            // If we have a plan locked in, don't update highlights based on hover
            if (_plannedDestination.HasValue) return;

            highlighter.ClearVisuals();

            // Show outline if available
            if (outlineManager)
            {
                outlineManager.ShowOutline(new HashSet<HexCoords> { tile });
            }

            // Calculate path for hover preview
            if (_playerUnit != null && !_playerUnit.Coords.Equals(tile))
            {
                var path = ChapterPathfinder.FindPath(_playerUnit.Coords, tile, grid);
                if (path != null && path.Count > 0)
                {
                    highlighter.ShowDestCursor(tile, $"{path.Count} Steps");
                }
            }

            // Unit Highlight Logic
            if (ChapterMapManager.Instance != null)
            {
                // Check if there is a unit at this tile (Player)
                // In Chapter Map, usually only Player moves, but maybe we hover over enemies?
                // For now, let's just check if we hover over the player
                if (_playerUnit != null && _playerUnit.Coords.Equals(tile))
                {
                    var uh = _playerUnit.GetComponentInChildren<UnitHighlighter>();
                    if (uh) uh.SetHover(true);
                }
                else
                {
                    // Clear player highlight if not hovering
                    if (_playerUnit != null)
                    {
                        var uh = _playerUnit.GetComponentInChildren<UnitHighlighter>();
                        if (uh) uh.SetHover(false);
                    }
                }
            }
        }

        void OnLeftClick(HexCoords target)
        {
            // Case A: We are already planning a move to THIS tile -> Confirm execution
            if (_plannedDestination.HasValue && _plannedDestination.Value.Equals(target))
            {
                ExecuteMove();
                return;
            }

            // Case B: We are planning a move to a DIFFERENT tile -> Change plan
            // Case C: No plan yet -> Start planning

            if (doubleClickToConfirm)
            {
                PlanMove(target);
            }
            else
            {
                visuals

            if (_playerUnit.Coords.Equals(target)) return; // Clicked on self

                // Debugging Pathfinding
                if (ChapterMapManager.Instance == null) { Debug.LogError("ChapterMapManager missing!"); return; }
                var startNode = ChapterMapManager.Instance.GetNodeAt(_playerUnit.Coords);
                var endNode = ChapterMapManager.Instance.GetNodeAt(target);

                if (startNode == null)
                {
                    Debug.LogError($"Start Node {_playerUnit.Coords} not found in MapManager!");
                    ChapterMapManager.Instance.DebugDumpNodes();
                }
                if (endNode == null)
                {
                    Debug.LogError($"End Node {target} not found in MapManager!");
                    ChapterMapManager.Instance.DebugDumpNodes();
                    // Try fallback lookup in case comparer mismatch
                    endNode = ChapterMapManager.Instance.GetNodeAt(target);
                    if (endNode != null)
                    {
                        Debug.LogWarning($"[ChapterMovementSystem] Fallback found node at {target} after initial miss.");
                    }
                }

                if (startNode == null || endNode == null)
                {
                    Debug.LogWarning($"[ChapterMovementSystem] Aborting PlanMove because start({startNode != null}) or end({endNode != null}) is missing. Target: {target}");
                    Debug.LogWarning("[ChapterMovementSystem] Aborting PlanMove because start or end node is missing.");
                    return;
                }

                // Calculate Path using the new simple Pathfinder
                var path = ChapterPathfinder.FindPath(_playerUnit.Coords, target, grid);

                if (path == null || path.Count == 0)
                {
                    Debug.Log($"Path blocked or invalid. Start: {_playerUnit.Coords}, End: {target}");
                    // TODO: Play 'Cannot Move' sound
                    return;
                }

                // Valid Plan
                _plannedDestination = target;
                _currentPath = path;

                // Draw visuals
                if (highlighter)
                {
                    highlighter.ClearVisuals();
                    highlighter.ShowDestCursor(target, $"{path.Count} Steps");
                }

                Debug.Log($"[Map] Plan set for {target}. Click again to go.");
            }

            void ExecuteMove()
            {
                if (_currentPath == null || _currentPath.Count == 0) return;

                // Cleanup visuals before moving
                highlighter.ClearVisuals();
                _plannedDestination = null;

                // Send to Mover
                _playerMover.FollowPath(_currentPath, OnMoveFinished);
            }

            void ClearPlan()
            {
                _plannedDestination = null;
                _currentPath = null;
                highlighter.ClearVisuals
                _playerMover.FollowPath(_currentPath, OnMoveFinished);
            }

            void ClearPlan()
            {
                _plannedDestination = null;
                _currentPath = null;
                if (_activeGhost != null) Destroy(_activeGhost);
                highlighter.ClearAll();
                if (outlineManager) outlineManager.Hide();
            }

            void OnMoveFinished()
            {
                // UnitMover has finished animating
                // ChapterMapManager listens to UnitMover events to trigger encounters/tide
            }
        }
    }