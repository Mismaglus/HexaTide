// Scripts/Game/Inventory/ConsumableItem.cs
using UnityEngine;
using Game.Battle.Abilities;
using Game.Battle;

namespace Game.Inventory
{
    [CreateAssetMenu(menuName = "HexBattle/Inventory/Consumable Item")]
    public class ConsumableItem : InventoryItem
    {
        [Header("Consumable Logic")]
        [Tooltip("使用该物品相当于释放这个技能")]
        public Ability abilityToCast;

        [Tooltip("使用后是否消耗物品？")]
        public bool consumeOnUse = true;

        public ConsumableItem()
        {
            type = ItemType.Consumable;
            isStackable = true;
        }

        // 消耗品通常不需要 OnAcquire/OnRemove 逻辑，除非有持有加成

        public override string GetDynamicDescription(BattleUnit holder)
        {
            // 如果配置了 Ability，优先显示 Ability 的效果
            if (abilityToCast != null)
            {
                return abilityToCast.GetDynamicDescription(holder);
            }
            return base.GetDynamicDescription(holder);
        }
    }
}