using System.Collections.Generic;
using UnityEngine;
using Core.Hex;

namespace Game.World
{
    /// <summary>
    /// Holds the state of the Chapter Map while the player is away in a Battle scene.
    /// </summary>
    public static class MapRuntimeData
    {
        public static bool HasData { get; private set; } = false;

        // Generation Info (to rebuild the same map)
        public static int MapSeed;

        // Keep track of which chapter this runtime data belongs to.
        // NOTE: In the new model, this is a REGION_* id (kept for compatibility).
        public static string CurrentChapterId;

        // Act/Region routing state (new).
        public static int CurrentAct;
        public static string CurrentRegionId;

        // Dynamic State
        public static HexCoords PlayerPosition;
        public static int CurrentTideRow;
        public static int MovesTaken;

        // Progress
        public static HashSet<HexCoords> ClearedNodes = new HashSet<HexCoords>();

        /// <summary>
        /// Saves the runtime state and associates it with the current FlowContext.CurrentChapterId.
        /// </summary>
        public static void Save(int seed, HexCoords playerPos, int tideRow, int moves, List<HexCoords> cleared)
        {
            Save(seed, playerPos, tideRow, moves, cleared, FlowContext.CurrentChapterId);
        }

        /// <summary>
        /// Saves the runtime state and associates it with the given chapterId.
        /// </summary>
        public static void Save(int seed, HexCoords playerPos, int tideRow, int moves, List<HexCoords> cleared, string chapterId)
        {
            MapSeed = seed;
            PlayerPosition = playerPos;
            CurrentTideRow = tideRow;
            MovesTaken = moves;
            CurrentChapterId = chapterId;
            CurrentRegionId = chapterId;
            CurrentAct = FlowContext.CurrentAct;

            ClearedNodes.Clear();
            if (cleared != null)
            {
                foreach (var c in cleared) ClearedNodes.Add(c);
            }

            HasData = true;
            Debug.Log($"[MapData] Saved state. Player at {PlayerPosition}, Tide Row {CurrentTideRow}, Cleared {ClearedNodes.Count}, Chapter {CurrentChapterId}");
        }

        public static void Clear()
        {
            HasData = false;
            ClearedNodes.Clear();
            CurrentChapterId = null;
            CurrentRegionId = null;
            CurrentAct = 0;
        }
    }
}
