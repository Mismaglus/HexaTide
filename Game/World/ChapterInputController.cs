using UnityEngine;
using UnityEngine.InputSystem;
using Core.Hex;
using Game.Grid;
using Game.Units;
using Game.Battle;
using System.Collections.Generic;
using Game.Common;

namespace Game.World
{
    public class ChapterInputController : MonoBehaviour
    {
        [Header("References")]
        public BattleHexGrid grid;
        public BattleRules mapRules;
        public HexHighlighter highlighter;

        [Header("Settings")]
        [Tooltip("If true, first click shows preview, second click moves. If false, moves immediately.")]
        public bool requireMoveConfirmation = true;
        public LayerMask terrainLayer = ~0;

        [Header("Visuals")]
        [Tooltip("Prefab for the 'Ghost' unit shown during confirmation.")]
        public GameObject ghostPrefab;
        private GameObject _activeGhost;

        // Runtime State
        private Camera _cam;
        private Unit _playerUnit;
        private UnitMover _playerMover;

        // Input State
        private HexCoords? _hoveredCoords;
        private HexCoords? _confirmedTarget; // The tile we are waiting to confirm
        private bool _isWaitingForConfirmation = false;

        void Start()
        {
            _cam = Camera.main;
            if (!grid) grid = FindFirstObjectByType<BattleHexGrid>();
            if (!mapRules) mapRules = FindFirstObjectByType<BattleRules>();
            if (!highlighter) highlighter = FindFirstObjectByType<HexHighlighter>();

            FindPlayer();
        }

        void FindPlayer()
        {
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
            if (_playerUnit == null) { FindPlayer(); return; }
            if (_playerMover != null && _playerMover.IsMoving) return;

            // Right Click to Cancel
            if (Mouse.current.rightButton.wasPressedThisFrame)
            {
                CancelConfirmation();
                return;
            }

            HandleMouseHover();
            HandleMouseClick();
        }

        void HandleMouseHover()
        {
            if (Mouse.current == null) return;
            Vector2 mousePos = Mouse.current.position.ReadValue();

            if (HexRaycaster.TryPick(_cam, mousePos, out var go, out var tag, (int)terrainLayer))
            {
                if (!_hoveredCoords.HasValue || !_hoveredCoords.Value.Equals(tag.Coords))
                {
                    _hoveredCoords = tag.Coords;

                    // If we are NOT waiting for confirmation, show simple highlight
                    // If we ARE waiting, we only highlight if the user hovers a DIFFERENT tile
                    if (!_isWaitingForConfirmation)
                    {
                        if (highlighter)
                        {
                            highlighter.ClearAll();
                            highlighter.SetHover(_hoveredCoords);
                        }
                    }
                }
            }
            else
            {
                _hoveredCoords = null;
                if (!_isWaitingForConfirmation && highlighter) highlighter.ClearAll();
            }
        }

        void HandleMouseClick()
        {
            if (Mouse.current == null || !Mouse.current.leftButton.wasPressedThisFrame) return;
            if (!_hoveredCoords.HasValue) return;

            HexCoords clickedTile = _hoveredCoords.Value;

            // Scenario 1: We are waiting for confirmation on this specific tile
            if (_isWaitingForConfirmation && _confirmedTarget.HasValue && clickedTile.Equals(_confirmedTarget.Value))
            {
                ExecuteMove(clickedTile);
                return;
            }

            // Scenario 2: Quick Move Enabled OR First Click
            if (!requireMoveConfirmation)
            {
                ExecuteMove(clickedTile);
            }
            else
            {
                // Enter Confirmation Mode
                ProposeMove(clickedTile);
            }
        }

        void ProposeMove(HexCoords target)
        {
            if (_playerUnit.Coords.Equals(target)) return;

            var path = HexPathfinder.FindPath(_playerUnit.Coords, target, mapRules, _playerUnit);
            if (path == null || path.Count == 0)
            {
                // Invalid path, play error sound?
                return;
            }

            // Valid path found, enter state
            _isWaitingForConfirmation = true;
            _confirmedTarget = target;

            // 1. Show Path Visuals
            if (highlighter)
            {
                highlighter.ClearAll();
                highlighter.ApplyRange(path); // Or use a specific 'Path' color
            }

            // 2. Spawn Ghost (Optional visual)
            if (ghostPrefab != null)
            {
                if (_activeGhost != null) Destroy(_activeGhost);
                Vector3 worldPos = grid.GetTileWorldPosition(target);
                _activeGhost = Instantiate(ghostPrefab, worldPos, Quaternion.identity);
                // Make sure ghost looks semi-transparent?
            }

            Debug.Log($"[Input] Waiting to confirm move to {target}. Click again to confirm.");
        }

        void ExecuteMove(HexCoords target)
        {
            // Cleanup confirmation state
            CancelConfirmation();

            // Logic from previous version
            if (_playerUnit.Coords.Equals(target)) return;

            var path = HexPathfinder.FindPath(_playerUnit.Coords, target, mapRules, _playerUnit);
            if (path != null && path.Count > 0)
            {
                _playerMover.FollowPath(path, OnMoveComplete);
            }
        }

        void CancelConfirmation()
        {
            _isWaitingForConfirmation = false;
            _confirmedTarget = null;

            if (_activeGhost != null) Destroy(_activeGhost);
            if (highlighter) highlighter.ClearAll();
        }

        void OnMoveComplete()
        {
            if (highlighter) highlighter.ClearAll();
        }
    }
}