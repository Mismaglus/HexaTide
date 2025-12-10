// Scripts/Game/Battle/BattleContext.cs
using Game.Inventory;

namespace Game.Battle
{
    /// <summary>
    /// Holds transient data for the upcoming or active battle.
    /// Use this to pass configuration (Enemies, Loot, MapLayout) into the generic Battle Scene.
    /// </summary>
    public static class BattleContext
    {
        // The loot table to use for the next (or current) victory
        public static LootTableSO ActiveLootTable;

        /// <summary>
        /// Clears data to prevent stale state when returning to main menu.
        /// </summary>
        public static void Reset()
        {
            ActiveLootTable = null;
        }
    }
}