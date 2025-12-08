// Scripts/Game/Inventory/ConsumableItem.cs
using UnityEngine;
using Game.Battle.Abilities;
using Game.Battle;
using Game.Localization; // 引用本地化

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

        // ⭐ 简化版描述：
        // 1. 如果 Localization 有专门为物品写的描述 (ITEM_POTION_DESC)，直接用它。
        // 2. 如果没有，才去尝试读 Ability 的描述。
        public override string GetDynamicDescription(BattleUnit holder)
        {
            string myDesc = LocalizedDesc; // 基类会去查 "ITEM_ID_DESC"

            // 如果本地化文件里没有填(返回了Key)，或者明确标记了 TODO
            if (!string.IsNullOrEmpty(myDesc) && !myDesc.Contains(itemID) && !myDesc.Contains("TODO"))
            {
                return myDesc; // 直接返回静态文本，比如 "恢复 50 点生命值。"
            }

            // 兜底：如果没写描述，再去读技能的（可能会带有 +XX% 的补正信息，如果你不想看这个，就去把 JSON 里的描述填好）
            if (abilityToCast != null)
            {
                return abilityToCast.GetDynamicDescription(holder);
            }

            return "No Description";
        }
    }
}