using UnityEngine;
using Game.Battle;

namespace Game.Inventory
{
    /// <summary>
    /// 代表用于技能分支升级的强化石。本质上属于材料类道具，但可放入奖励配置。
    /// </summary>
    [CreateAssetMenu(menuName = "HexaTide/Items/Skill Enhancer Item", fileName = "SkillEnhancer_")]
    public class SkillEnhancerItem : InventoryItem
    {
        [Header("Skill Enhancer Specific")]
        [Tooltip("该强化石作用的技能标识/名称")]
        public string skillId;

        private void OnValidate()
        {
            // 保证在 Inspector/导入时类型一致（比构造函数可靠）
            type = ItemType.Material;
        }

        /// <summary>
        /// 当物品被添加到单位背包时调用。
        /// 注意：基类接口只给了 holder，没有 count。如果你要 count，需要在 UnitInventory 添加/调用侧扩展。
        /// </summary>
        public override void OnAcquire(BattleUnit holder)
        {
            base.OnAcquire(holder);
            Debug.Log($"[SkillEnhancerItem] Acquired: {name}, skillId={skillId}, holder={(holder ? holder.name : "null")}");
        }
    }
}
