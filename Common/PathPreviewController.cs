using System.Collections.Generic;
using UnityEngine;
using Core.Hex;
using Game.Battle;
using Game.Grid;
using Game.Units;
using Game.World;

namespace Game.Common
{
    public static class PathPreviewController
    {
        const string StepsLabelSuffix = "";

        public static void ClearPreview(HexHighlighter highlighter)
        {
            if (highlighter) highlighter.ClearVisuals();
        }

        public static bool ShowPreview(HexHighlighter highlighter, BattleHexGrid grid, IList<HexCoords> path, HexCoords target)
        {
            // Default label: previous behavior (Chapter scene relies on this).
            return ShowPreview(highlighter, grid, path, target, $"{path.Count}{StepsLabelSuffix}");
        }

        public static bool ShowPreview(HexHighlighter highlighter, BattleHexGrid grid, IList<HexCoords> path, HexCoords target, string labelText)
        {
            if (!highlighter || path == null || path.Count == 0)
            {
                ClearPreview(highlighter);
                return false;
            }

            if (highlighter.grid == null && grid != null) highlighter.grid = grid;
            if (highlighter.grid == null)
            {
                ClearPreview(highlighter);
                return false;
            }

            highlighter.ShowPath(path);
            highlighter.ShowDestCursor(target, labelText ?? string.Empty);
            return true;
        }

        public static bool TryShowChapterPreview(HexHighlighter highlighter, BattleHexGrid grid, Unit unit, HexCoords target, out List<HexCoords> path)
        {
            path = null;
            if (!highlighter || !unit) return false;

            if (highlighter.grid == null && grid != null) highlighter.grid = grid;
            var activeGrid = highlighter.grid != null ? highlighter.grid : grid;
            if (activeGrid == null) return false;

            if (unit.Coords.Equals(target))
            {
                ClearPreview(highlighter);
                return false;
            }

            path = ChapterPathfinder.FindPath(unit.Coords, target, activeGrid);
            return ShowPreview(highlighter, activeGrid, path, target);
        }

        public static bool TryShowBattlePreview(HexHighlighter highlighter, BattleHexGrid grid, Unit unit, BattleRules rules, bool ignorePenalties, HexCoords target, out List<HexCoords> path)
        {
            path = null;
            if (!highlighter || !unit || rules == null) return false;

            if (highlighter.grid == null && grid != null) highlighter.grid = grid;
            var activeGrid = highlighter.grid != null ? highlighter.grid : grid;
            if (activeGrid == null) return false;

            if (unit.Coords.Equals(target))
            {
                ClearPreview(highlighter);
                return false;
            }

            path = HexPathfinder.FindPath(unit.Coords, target, rules, unit, ignorePenalties);

            // Battle scene needs to show real cost (terrain/ZOC/etc), not just path length.
            int totalCost = ComputeBattlePathCost(unit.Coords, path, rules, unit, ignorePenalties);
            return ShowPreview(highlighter, activeGrid, path, target, $"{totalCost}{StepsLabelSuffix}");
        }

        static int ComputeBattlePathCost(HexCoords start, IList<HexCoords> path, BattleRules rules, Unit unit, bool ignorePenalties)
        {
            // Note: our pathfinders return a list that typically EXCLUDES the start tile.
            // So a one-step move will have path.Count == 1.
            if (path == null || path.Count == 0 || rules == null || unit == null) return 0;

            // Mirror HexPathfinder/MovementCalculator behavior so preview matches actual consumption.
            var occupancy = Object.FindFirstObjectByType<GridOccupancy>();
            var calculator = new MovementCalculator(occupancy, rules);

            int sum = 0;

            // Support both path formats:
            // - [next1, next2, ...] (excludes start)  -> cost(start->next1) + ...
            // - [start, next1, ...] (includes start)  -> cost(start->next1) + ...
            bool includesStart = path.Count > 0 && path[0].Equals(start);
            HexCoords prev = includesStart ? path[0] : start;
            int startIndex = includesStart ? 1 : 0;

            for (int i = startIndex; i < path.Count; i++)
            {
                HexCoords next = path[i];
                int step = calculator.GetMoveCost(prev, next, unit, ignorePenalties);
                if (step >= 999) return 999; // blocked/unreachable marker
                sum += Mathf.Max(1, step);
                prev = next;
            }

            return sum;
        }
    }
}
