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
    /// Refactored: Uses ShowDestCursor for preview instead of Ghosts/Range.
    /// </summary>
    public class ChapterMovementSystem : MonoBehaviour
    {
        [Header("References")]
        public BattleHexGrid grid;
        public HexHighlighter highlighter;
        public ChapterOutlineManager outlineManager;

        [Header("Settings")]
        public LayerMask terrainLayer = ~0;
        public bool doubleClickToConfirm = true;

        // State
        private Camera _cam;
        private Unit _playerUnit;
        private UnitMover _playerMover;

        private HexCoords? _hoveredCoords;
        private HexCoords? _plannedDestination; // Where we WANT to go (awaiting confirmation)
        private List<HexCoords> _currentPath;
        private HexCoords? _currentPathTarget;
        private HexCoords? _currentPathStart;

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
                        PathPreviewController.ClearPreview(highlighter); // Clean up cursor/labels
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
            Debug.Log($"[ChapterMovementSystem] Hover tile: {tile}");

            if (_plannedDestination.HasValue)
            {
                Debug.Log($"[ChapterMovementSystem] Hover preview skipped: plan locked at {_plannedDestination.Value}.");
                return;
            }

            if (_playerUnit == null)
            {
                Debug.Log("[ChapterMovementSystem] Hover aborted: player missing.");
                return;
            }

            if (!highlighter)
            {
                Debug.Log("[ChapterMovementSystem] Hover aborted: highlighter missing.");
                return;
            }

            highlighter.SetHover(tile);

            if (outlineManager)
                outlineManager.ShowOutline(new HashSet<HexCoords> { tile });

            if (_playerUnit.Coords.Equals(tile))
            {
                Debug.Log("[ChapterMovementSystem] Hover aborted: player==target.");
                PathPreviewController.ClearPreview(highlighter);
                _currentPath = null;
                _currentPathTarget = null;
                _currentPathStart = null;
                return;
            }

            if (ChapterMapManager.Instance == null)
            {
                Debug.Log("[ChapterMovementSystem] Hover note: ChapterMapManager missing.");
            }

            if (PathPreviewController.TryShowChapterPreview(highlighter, grid, _playerUnit, tile, out var path))
            {
                Debug.Log($"[ChapterMovementSystem] Hover path count: {path.Count}");
                _currentPath = path;
                _currentPathTarget = tile;
                _currentPathStart = _playerUnit.Coords;
            }
            else
            {
                Debug.Log("[ChapterMovementSystem] Hover aborted: path null/empty.");
                PathPreviewController.ClearPreview(highlighter);
                _currentPath = null;
                _currentPathTarget = null;
                _currentPathStart = null;
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
                PlanMove(target, true);
            }
            else
            {
                // If double click is disabled, just move immediately
                if (PlanMove(target, false)) ExecuteMove();
            }
        }

        bool PlanMove(HexCoords target, bool lockPlan)
        {
            if (_playerUnit == null)
            {
                Debug.Log("[ChapterMovementSystem] PlanMove aborted: player missing.");
                return false;
            }

            bool hasCachedPath = _currentPath != null
                && _currentPath.Count > 0
                && _currentPathTarget.HasValue
                && _currentPathTarget.Value.Equals(target)
                && _currentPathStart.HasValue
                && _currentPathStart.Value.Equals(_playerUnit.Coords);

            if (!hasCachedPath)
            {
                ClearPlan(); // Remove old visuals
            }
            else if (!lockPlan)
            {
                _plannedDestination = null;
            }

            if (_playerUnit.Coords.Equals(target))
            {
                Debug.Log("[ChapterMovementSystem] PlanMove aborted: player==target.");
                return false;
            }

            List<HexCoords> path = null;
            if (hasCachedPath)
            {
                path = _currentPath;
            }
            else
            {
                // Debugging Pathfinding
                if (ChapterMapManager.Instance == null)
                {
                    Debug.Log("[ChapterMovementSystem] PlanMove aborted: ChapterMapManager missing.");
                    return false;
                }

                var startNode = ChapterMapManager.Instance.GetNodeAt(_playerUnit.Coords);
                var endNode = ChapterMapManager.Instance.GetNodeAt(target);

                if (startNode == null || endNode == null)
                {
                    Debug.LogWarning($"[ChapterMovementSystem] Aborting PlanMove because start or end node is missing. Target: {target}");
                    return false;
                }

                // Calculate Path
                path = ChapterPathfinder.FindPath(_playerUnit.Coords, target, grid);
            }

            if (path == null || path.Count == 0)
            {
                Debug.Log($"[ChapterMovementSystem] PlanMove aborted: path null/blocked. Start: {_playerUnit.Coords}, End: {target}");
                // TODO: Play 'Cannot Move' sound
                return false;
            }

            // Valid Plan
            if (lockPlan) _plannedDestination = target;
            _currentPath = path;
            _currentPathTarget = target;
            _currentPathStart = _playerUnit.Coords;

            // Draw visuals: Use the new ShowDestCursor API
            if (highlighter)
            {
                PathPreviewController.ShowPreview(highlighter, grid, path, target);
            }

            if (lockPlan)
            {
                Debug.Log($"[Map] Plan set for {target} ({path.Count} steps). Click again to go.");
            }
            else
            {
                Debug.Log($"[Map] Move ready for {target} ({path.Count} steps).");
            }

            return true;
        }

        void ExecuteMove()
        {
            if (_currentPath == null || _currentPath.Count == 0)
            {
                Debug.Log("[ChapterMovementSystem] ExecuteMove aborted: path null/empty.");
                return;
            }

            var pathToMove = _currentPath;

            // Cleanup visuals before moving
            PathPreviewController.ClearPreview(highlighter);
            if (outlineManager) outlineManager.Hide();
            _plannedDestination = null;
            _currentPath = null;
            _currentPathTarget = null;
            _currentPathStart = null;

            // Send to Mover
            _playerMover.FollowPath(pathToMove, OnMoveFinished);
        }

        void ClearPlan()
        {
            _plannedDestination = null;
            _currentPath = null;
            _currentPathTarget = null;
            _currentPathStart = null;
            PathPreviewController.ClearPreview(highlighter); // Clears Cursor and Labels
            if (outlineManager) outlineManager.Hide();
        }

        void OnMoveFinished()
        {
            // UnitMover has finished animating
            // ChapterMapManager listens to UnitMover events to trigger encounters/tide
        }
    }
}
