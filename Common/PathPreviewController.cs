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
            highlighter.ShowDestCursor(target, $"{path.Count}{StepsLabelSuffix}");
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
            return ShowPreview(highlighter, activeGrid, path, target);
        }
    }
}
