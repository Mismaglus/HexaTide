// Scripts/Game/Inventory/InventoryItem.cs
using UnityEngine;
using Game.Battle; // for BattleUnit
using Game.Localization;

namespace Game.Inventory
{
    public enum ItemType { Consumable, Relic, Material, Quest }

    /// <summary>
    /// 所有库存物品的基类。
    /// </summary>
    public abstract class InventoryItem : ScriptableObject
    {
        [Header("Identity")]
        public string itemID;
        public Sprite icon;
        [Min(0)] public int price = 10;
        public ItemType type;

        [Header("Stacking")]
        public bool isStackable = false;
        [Min(1)] public int maxStack = 99;

        // 本地化属性
        public string LocalizedName => LocalizationManager.Get($"{itemID}_NAME");
        public string LocalizedDesc => LocalizationManager.Get($"{itemID}_DESC");

        /// <summary>
        /// 当物品被添加到单位背包时调用。
        /// 遗物会在这里触发生效逻辑。
        /// </summary>
        public virtual void OnAcquire(BattleUnit holder) { }

        /// <summary>
        /// 当物品从单位背包移除时调用。
        /// 遗物会在这里移除效果。
        /// </summary>
        public virtual void OnRemove(BattleUnit holder) { }

        /// <summary>
        /// 获取动态描述（支持显示具体数值）
        /// </summary>
        public virtual string GetDynamicDescription(BattleUnit holder)
        {
            return LocalizedDesc;
        }
    }
}