using UnityEngine;
using UnityEngine.InputSystem;
using Core.Hex;
using Game.Grid;
using Game.Units;
using Game.Common;
using Game.Battle; // For BattleRules, BattleHexGrid

namespace Game.World
{
    /// <summary>
    /// Handles player input specifically for the Chapter Map (Overworld).
    /// Replaces SelectionManager which is used in Combat.
    /// </summary>
    public class ChapterInputController : MonoBehaviour
    {
        [Header("References")]
        public BattleHexGrid grid;
        public BattleRules mapRules; // Reuse BattleRules for "CanStep" logic
        public HexHighlighter highlighter;

        [Header("Config")]
        public LayerMask terrainLayer = ~0;

        // Cache
        private Camera _cam;
        private Unit _playerUnit;
        private UnitMover _playerMover;
        private HexCoords? _hoveredCoords;

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
            // Find the unit tagged as Player Side
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
            if (_playerUnit == null)
            {
                FindPlayer();
                return;
            }

            // Block input if moving
            if (_playerMover != null && _playerMover.IsMoving) return;

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

                    // Visual Feedback: Show path or highlight
                    if (highlighter)
                    {
                        highlighter.ClearAll();
                        highlighter.SetHover(_hoveredCoords);

                        // Optional: Preview Path logic here
                        // var path = HexPathfinder.FindPath(_playerUnit.Coords, _hoveredCoords.Value, mapRules, _playerUnit);
                        // if (path != null) highlighter.ApplyRange(path);
                    }
                }
            }
            else
            {
                if (_hoveredCoords.HasValue)
                {
                    _hoveredCoords = null;
                    if (highlighter) highlighter.ClearAll();
                }
            }
        }

        void HandleMouseClick()
        {
            if (Mouse.current == null || !Mouse.current.leftButton.wasPressedThisFrame) return;
            if (!_hoveredCoords.HasValue) return;

            MovePlayerTo(_hoveredCoords.Value);
        }

        void MovePlayerTo(HexCoords target)
        {
            if (_playerUnit.Coords.Equals(target)) return;

            // 1. Pathfinding
            // We reuse BattleRules because it already encapsulates "Occupancy" and "Terrain" checks.
            // On the map, "Occupancy" might mean "Obstacle Node".
            var path = HexPathfinder.FindPath(_playerUnit.Coords, target, mapRules, _playerUnit);

            if (path == null || path.Count == 0)
            {
                Debug.Log("[ChapterInput] Cannot reach target (Blocked or Unwalkable).");
                // TODO: Play "Error" sound
                return;
            }

            // 2. Execute Move
            // Map movement usually ignores AP/Stride costs (infinite range until blocked), 
            // but ChapterMapManager tracks "Steps".
            // We can let UnitMover handle the animation.

            _playerMover.FollowPath(path, OnMoveComplete);
        }

        void OnMoveComplete()
        {
            if (highlighter) highlighter.ClearAll();
        }
    }
}