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

        // Dynamic State
        public static HexCoords PlayerPosition;
        public static int CurrentTideRow;
        public static int MovesTaken;

        // Progress
        public static HashSet<HexCoords> ClearedNodes = new HashSet<HexCoords>();

        public static void Save(int seed, HexCoords playerPos, int tideRow, int moves, List<HexCoords> cleared)
        {
            MapSeed = seed;
            PlayerPosition = playerPos;
            CurrentTideRow = tideRow;
            MovesTaken = moves;

            ClearedNodes.Clear();
            if (cleared != null)
            {
                foreach (var c in cleared) ClearedNodes.Add(c);
            }

            HasData = true;
            Debug.Log($"[MapData] Saved state. Player at {PlayerPosition}, Tide Row {CurrentTideRow}, Cleared {ClearedNodes.Count}");
        }

        public static void Clear()
        {
            HasData = false;
            ClearedNodes.Clear();
        }
    }
}