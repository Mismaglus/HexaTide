// Scripts/Game/Battle/BattleContext.cs
using Game.Inventory;
using UnityEngine;

namespace Game.Battle
{
    /// <summary>
    /// Serves as a bridge to pass transient data into the Battle Scene.
    /// Configure this before loading "BattleScene".
    /// </summary>
    public static class BattleContext
    {
        // The specific loot table for the upcoming battle (e.g., "Goblin Drop", "Boss Chest")
        public static LootTableSO ActiveLootTable;

        // The context for the encounter (e.g. return policy, next chapter)
        public static Game.World.EncounterContext EncounterContext;

        /// <summary>
        /// Clears the context. Call this when returning to the Map/Menu to ensure 
        /// the next battle doesn't accidentally use old data.
        /// </summary>
        public static void Reset()
        {
            ActiveLootTable = null;
            EncounterContext = null;
            Debug.Log("[BattleContext] Reset.");
        }
    }
}