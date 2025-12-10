// Scripts/Game/Inventory/LootTableSO.cs
using System.Collections.Generic;
using UnityEngine;
using Game.Battle; // For BattleRewardResult

namespace Game.Inventory
{
    [CreateAssetMenu(fileName = "NewLootTable", menuName = "HexaTide/Inventory/Loot Table")]
    public class LootTableSO : ScriptableObject
    {
        [System.Serializable]
        public class DropEntry
        {
            public InventoryItem item;
            [Range(0f, 100f)] public float chance = 100f;
            [Min(1)] public int minAmount = 1;
            [Min(1)] public int maxAmount = 1;
        }

        [Header("Currency Rewards")]
        public int minGold = 10;
        public int maxGold = 50;

        [Header("Progression Rewards")]
        public int minExp = 50;
        public int maxExp = 100;

        [Header("Item Drops")]
        [Tooltip("List of items that can potentially drop.")]
        public List<DropEntry> drops = new List<DropEntry>();

        /// <summary>
        /// Calculates the final rewards for a battle instance.
        /// </summary>
        public BattleRewardResult GenerateRewards()
        {
            var result = new BattleRewardResult();

            // 1. Roll Currency & Exp
            result.gold = Random.Range(minGold, maxGold + 1);
            result.experience = Random.Range(minExp, maxExp + 1);

            // 2. Roll Items
            foreach (var entry in drops)
            {
                if (entry.item == null) continue;

                float roll = Random.Range(0f, 100f);
                if (roll <= entry.chance)
                {
                    int qty = Random.Range(entry.minAmount, entry.maxAmount + 1);
                    result.AddItem(entry.item, qty);
                }
            }

            return result;
        }
    }
}