// Scripts/Game/Battle/BattleRewardResult.cs
using System.Collections.Generic;
using Game.Inventory;

namespace Game.Battle
{
    /// <summary>
    /// Holds the results of the loot generation for the UI to display.
    /// </summary>
    public class BattleRewardResult
    {
        public int gold;
        public int experience;
        public List<UnitInventory.Slot> items = new List<UnitInventory.Slot>();

        public void AddItem(InventoryItem item, int count)
        {
            var existing = items.Find(x => x.item == item);
            if (existing != null)
            {
                existing.count += count;
            }
            else
            {
                items.Add(new UnitInventory.Slot { item = item, count = count });
            }
        }
    }
}