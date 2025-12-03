// Scripts/Game/Inventory/RelicItem.cs
using UnityEngine;
using Game.Battle;
using Game.Battle.Status;

namespace Game.Inventory
{
    [CreateAssetMenu(menuName = "HexBattle/Inventory/Relic Item")]
    public class RelicItem : InventoryItem
    {
        [Header("Relic Effect")]
        [Tooltip("获得的被动状态（通常是 Permanent Buff）")]
        public StatusDefinition statusEffect;

        public RelicItem()
        {
            type = ItemType.Relic;
            isStackable = false; // 遗物通常不可堆叠
        }

        public override void OnAcquire(BattleUnit holder)
        {
            if (holder == null || statusEffect == null) return;

            // 应用永久状态
            if (holder.Status)
            {
                holder.Status.ApplyStatus(statusEffect, holder, 1);
                Debug.Log($"[Relic] {holder.name} acquired {LocalizedName}, applied status {statusEffect.statusID}");
            }
        }

        public override void OnRemove(BattleUnit holder)
        {
            if (holder == null || statusEffect == null) return;

            // 移除状态
            if (holder.Status)
            {
                holder.Status.RemoveStatus(statusEffect);
                Debug.Log($"[Relic] {holder.name} lost {LocalizedName}, removed status {statusEffect.statusID}");
            }
        }

        public override string GetDynamicDescription(BattleUnit holder)
        {
            // 如果描述为空，尝试返回 Status 的描述
            string baseDesc = base.GetDynamicDescription(holder);
            if (string.IsNullOrEmpty(baseDesc) || baseDesc.StartsWith("TODO"))
            {
                if (statusEffect != null) return statusEffect.LocalizedDesc;
            }
            return baseDesc;
        }
    }
}