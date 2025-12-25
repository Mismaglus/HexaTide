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
        public ChapterOutlineManager outlineManager;

        [Header("Settings")]
        public LayerMask terrainLayer = ~0;
        public bool doubleClickToConfirm = true;

        // State
        private Camera _cam;
        private Unit _playerUnit;
        private UnitMover _playerMover;

        private HexCoords? _hoveredCoords;
        private HexCoords? _plannedDestination;
        private List<HexCoords> _currentPath;

        void Start()
        {
            _cam = Camera.main;
            if (!grid) grid = Object.FindFirstObjectByType<BattleHexGrid>();
            if (!highlighter) highlighter = Object.FindFirstObjectByType<HexHighlighter>();
            if (!outlineManager) outlineManager = Object.FindFirstObjectByType<ChapterOutlineManager>();

            FindPlayer();
        }

        void FindPlayer()
        {
            var units = Object.FindObjectsByType<Game.Core.FactionMembership>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
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
            if (_playerUnit == null) { FindPlayer(); return; }
            if (_playerMover != null && _playerMover.IsMoving) return;

            HandleInput();
        }

        void HandleInput()
        {
            if (Mouse.current.rightButton.wasPressedThisFrame)
            {
                ClearPlan();
                return;
            }

            if (Mouse.current == null) return;
            Vector2 mousePos = Mouse.current.position.ReadValue();

            if (HexRaycaster.TryPick(_cam, mousePos, out var go, out var tag, (int)terrainLayer))
            {
                if (!_hoveredCoords.HasValue || !_hoveredCoords.Value.Equals(tag.Coords))
                {
                    _hoveredCoords = tag.Coords;
                    OnHoverTile(_hoveredCoords.Value);
                }

                if (Mouse.current.leftButton.wasPressedThisFrame)
                {
                    OnLeftClick(_hoveredCoords.Value);
                }
            }
            else
            {
                if (_hoveredCoords.HasValue)
                {
                    _hoveredCoords = null;
                    highlighter.SetHover(null);

                    // Only clear visuals if we are NOT planning a move
                    if (!_plannedDestination.HasValue)
                    {
                        highlighter.ClearVisuals();
                        if (outlineManager) outlineManager.Hide();
                    }
                }
            }
        }

        void OnHoverTile(HexCoords tile)
        {
            highlighter.SetHover(tile);

            // If we are NOT planning, we can show outline or simple hover
            if (!_plannedDestination.HasValue)
            {
                highlighter.ClearVisuals(); // Ensure no stale cursors
                if (outlineManager)
                {
                    outlineManager.ShowOutline(new HashSet<HexCoords> { tile });
                }
            }
        }

        void OnLeftClick(HexCoords target)
        {
            // If we clicked the same tile we planned for, execute
            if (_plannedDestination.HasValue && _plannedDestination.Value.Equals(target))
            {
                ExecuteMove();
                return;
            }

            // Otherwise, plan a new move
            PlanMove(target);
        }

        void PlanMove(HexCoords target)
        {
            if (_playerUnit.Coords.Equals(target)) return;

            if (ChapterMapManager.Instance == null) return;

            // Check if nodes exist (optional safety)
            var startNode = ChapterMapManager.Instance.GetNodeAt(_playerUnit.Coords);
            var endNode = ChapterMapManager.Instance.GetNodeAt(target);

            if (startNode == null || endNode == null)
            {
                Debug.LogWarning($"[ChapterMovementSystem] Aborting PlanMove. Start or End node missing.");
                return;
            }

            var path = ChapterPathfinder.FindPath(_playerUnit.Coords, target, grid);

            if (path == null || path.Count == 0)
            {
                Debug.Log($"Path blocked or invalid.");
                return;
            }

            _plannedDestination = target;
            _currentPath = path;

            // Visuals
            if (highlighter)
            {
                highlighter.ClearVisuals();
                highlighter.ShowDestCursor(target, $"{path.Count} Steps");
                highlighter.SetSelected(target);
            }
        }

        void ExecuteMove()
        {
            if (_currentPath == null || _currentPath.Count == 0) return;

            // Clear visuals before moving
            highlighter.ClearVisuals();
            highlighter.SetSelected(null);
            highlighter.SetHover(null);

            _plannedDestination = null;

            _playerMover.FollowPath(_currentPath, OnMoveFinished);
        }

        void ClearPlan()
        {
            _plannedDestination = null;
            _currentPath = null;

            highlighter.ClearVisuals();
            highlighter.SetSelected(null);

            if (outlineManager) outlineManager.Hide();
        }

        void OnMoveFinished()
        {
            // Optional: Auto-interact logic is handled by ChapterMapManager via UnitMover events usually, 
            // or we can trigger something here.
            // ChapterMapManager listens to OnMoveFinished on the UnitMover.
        }
    }