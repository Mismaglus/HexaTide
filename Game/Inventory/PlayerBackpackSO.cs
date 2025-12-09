// Scripts/Game/Inventory/PlayerBackpackSO.cs
using System.Collections.Generic;
using UnityEngine;

namespace Game.Inventory
{
    /// <summary>
    /// Serves as the persistent data store for the player's inventory across scenes.
    /// In a full Save/Load system, you would serialize the contents of this object to JSON.
    /// </summary>
    [CreateAssetMenu(fileName = "PlayerBackpack", menuName = "HexaTide/Inventory/Player Backpack")]
    public class PlayerBackpackSO : ScriptableObject
    {
        [Header("Persistent Data")]
        // We use the same Slot definition as UnitInventory for compatibility
        public List<UnitInventory.Slot> savedSlots = new List<UnitInventory.Slot>();

        [Header("Default Config")]
        public int maxCapacity = 20;

        /// <summary>
        /// Clears data. Call this when starting a New Game.
        /// </summary>
        public void ResetToEmpty()
        {
            savedSlots.Clear();
        }

        public void LoadFrom(List<UnitInventory.Slot> currentSlots)
        {
            savedSlots.Clear();
            foreach (var slot in currentSlots)
            {
                // Create a copy to prevent reference issues if the source is destroyed
                savedSlots.Add(new UnitInventory.Slot
                {
                    item = slot.item,
                    count = slot.count
                });
            }
        }

        public List<UnitInventory.Slot> GetCopy()
        {
            var list = new List<UnitInventory.Slot>();
            foreach (var slot in savedSlots)
            {
                list.Add(new UnitInventory.Slot
                {
                    item = slot.item,
                    count = slot.count
                });
            }
            return list;
        }
    }
}