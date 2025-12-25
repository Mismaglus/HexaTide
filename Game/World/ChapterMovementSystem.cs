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
                    if (_plannedDestination == null)
                    {
                        highlighter.ClearVisuals();
                        if (outlineManager) outlineManager.Hide();

                        if (_playerUnit != null)
                        {
                            var uh = _playerUnit.GetComponentInChildren<UnitHighlighter>();
                            if (uh) uh.SetHover(false);
                        }
                    }
                }
            }
        }

        void OnHoverTile(HexCoords tile)
        {
            if (_plannedDestination.HasValue) return;

            highlighter.ClearVisuals();

            if (outlineManager)
            {
                outlineManager.ShowOutline(new HashSet<HexCoords> { tile });
            }

            if (_playerUnit != null && !_playerUnit.Coords.Equals(tile))
            {
                var path = ChapterPathfinder.FindPath(_playerUnit.Coords, tile, grid);
                if (path != null && path.Count > 0)
                {
                    highlighter.ShowDestCursor(tile, $"{path.Count} Steps");
                }
            }

            if (ChapterMapManager.Instance != null)
            {
                if (_playerUnit != null && _playerUnit.Coords.Equals(tile))
                {
                    var uh = _playerUnit.GetComponentInChildren<UnitHighlighter>();
                    if (uh) uh.SetHover(true);
                }
                else
                {
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
            if (_plannedDestination.HasValue && _plannedDestination.Value.Equals(target))
            {
                ExecuteMove();
                return;
            }

            if (doubleClickToConfirm)
            {
                PlanMove(target);
            }
            else
            {
                PlanMove(target);
                ExecuteMove();
            }
        }

        void PlanMove(HexCoords target)
        {
            if (_playerUnit.Coords.Equals(target)) return;

            if (ChapterMapManager.Instance == null) return;

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

            if (highlighter)
            {
                highlighter.ClearVisuals();
                highlighter.ShowDestCursor(target, $"{path.Count} Steps");
            }
        }

        void ExecuteMove()
        {
            if (_currentPath == null || _currentPath.Count == 0) return;

            highlighter.ClearVisuals();
            _plannedDestination = null;

            _playerMover.FollowPath(_currentPath, OnMoveFinished);
        }

        void ClearPlan()
        {
            _plannedDestination = null;
            _currentPath = null;
            highlighter.ClearVisuals();
            if (outlineManager) outlineManager.Hide();
        }

        void OnMoveFinished()
        {
        }
    }