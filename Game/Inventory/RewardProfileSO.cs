using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using Game.Battle;

namespace Game.Inventory
{
    /// <summary>
    /// 奖励配置 ScriptableObject。
    /// 每种 Encounter 或节点类型可绑定一个 RewardProfile，实现定制化奖励。
    /// </summary>
    [CreateAssetMenu(menuName = "HexaTide/Reward/Reward Profile", fileName = "RewardProfile")]
    public class RewardProfileSO : ScriptableObject
    {
        [Header("Identification")]
        [Tooltip("唯一标识，用作查找、匹配节点时的 key")]
        public string profileId = "Default";

        [Header("Base Loot Table (Optional)")]
        [Tooltip("基础掉落表，用于生成随机奖励的基础。如果为空，则不会生成随机掉落。")]
        public LootTableSO baseLootTable;

        [Header("Guaranteed Currency")]
        [Tooltip("本奖励是否包含固定或随机的金币奖励")]
        public bool includeGold = true;
        [Tooltip("金币奖励的最小值（当 includeGold 为 true 且 baseLootTable 为 null 时使用）")]
        public int minGold = 5;
        [Tooltip("金币奖励的最大值（当 includeGold 为 true 且 baseLootTable 为 null 时使用）")]
        public int maxGold = 10;

        [Tooltip("本奖励是否包含固定或随机的经验奖励")]
        public bool includeExperience = true;
        [Tooltip("经验奖励的最小值（当 includeExperience 为 true 且 baseLootTable 为 null 时使用）")]
        public int minExperience = 5;
        [Tooltip("经验奖励的最大值（当 includeExperience 为 true 且 baseLootTable 为 null 时使用）")]
        public int maxExperience = 10;

        [Header("Guaranteed Item Rewards")]
        [Tooltip("必定掉落的遗物（可选，Boss 专用）")]
        public RelicItem guaranteedRelic;

        [Tooltip("是否必定掉落一个技能强化石")]
        public bool includeSkillEnhancer = false;

        [Tooltip("强化石掉落数量")]
        public int minSkillEnhancers = 1;
        public int maxSkillEnhancers = 1;

        [Tooltip("用于随机生成强化石的集合。如果为空，则默认随机从所有可用强化石中选择。")]
        public List<SkillEnhancerItem> skillEnhancerPool;

        [Header("Consumables")]
        [Tooltip("消耗品奖励池（如果 empty 则不会随机掉落）")]
        public List<ConsumableItem> consumablePool;

        [Tooltip("消耗品随机掉落的数量范围")]
        public int minConsumables = 0;
        public int maxConsumables = 0;

        [Header("Relic Pool")]
        [Tooltip("可随机掉落的遗物池，若为空则不会随机掉落遗物")]
        public List<RelicItem> relicPool;

        [Tooltip("随机遗物掉落数量范围")]
        public int minRandomRelics = 0;
        public int maxRandomRelics = 0;

        /// <summary>
        /// 根据当前配置生成一个 BattleRewardResult。
        /// 若 BaseLootTable 不为空，则先根据表生成基础奖励，再叠加固定部分。
        /// </summary>
        public BattleRewardResult GenerateRewards()
        {
            var result = new BattleRewardResult();

            // 1) 基础掉落表生成
            if (baseLootTable != null)
            {
                var baseResult = baseLootTable.GenerateRewards();
                result.gold += baseResult.gold;
                result.experience += baseResult.experience;

                // baseResult.items 是 List<UnitInventory.Slot>
                // 用 AddItem 复制并合并，避免引用同一个 Slot 实例（如果 Slot 是 class）
                foreach (var slot in baseResult.items)
                {
                    if (slot == null || slot.item == null || slot.count <= 0) continue;
                    result.AddItem(slot.item, slot.count);
                }
            }
            else
            {
                // 自行生成基础 Currency
                if (includeGold)
                    result.gold += Random.Range(minGold, maxGold + 1);

                if (includeExperience)
                    result.experience += Random.Range(minExperience, maxExperience + 1);
            }

            // 2) 固定遗物（如 Boss 专属）
            if (guaranteedRelic != null)
            {
                result.AddItem(guaranteedRelic, 1);
            }

            // 3) 随机遗物
            if (relicPool != null && relicPool.Count > 0 && maxRandomRelics > 0)
            {
                int relicCount = Random.Range(minRandomRelics, maxRandomRelics + 1);
                for (int i = 0; i < relicCount; i++)
                {
                    var r = relicPool[Random.Range(0, relicPool.Count)];
                    if (r != null)
                        result.AddItem(r, 1);
                }
            }

            // 4) 技能强化石
            if (includeSkillEnhancer)
            {
                int count = Random.Range(minSkillEnhancers, maxSkillEnhancers + 1);
                for (int i = 0; i < count; i++)
                {
                    SkillEnhancerItem item = null;

                    if (skillEnhancerPool != null && skillEnhancerPool.Count > 0)
                    {
                        item = skillEnhancerPool[Random.Range(0, skillEnhancerPool.Count)];
                    }
                    else
                    {
                        // 如果未定义池，尝试从资源中随机获取（仅用于临时兜底）
                        item = Resources.FindObjectsOfTypeAll<SkillEnhancerItem>().FirstOrDefault();
                    }

                    if (item != null)
                        result.AddItem(item, 1);
                }
            }

            // 5) 随机消耗品
            if (consumablePool != null && consumablePool.Count > 0 && maxConsumables > 0)
            {
                int count = Random.Range(minConsumables, maxConsumables + 1);
                for (int i = 0; i < count; i++)
                {
                    var c = consumablePool[Random.Range(0, consumablePool.Count)];
                    if (c != null)
                        result.AddItem(c, 1);
                }
            }

            return result;
        }
    }
}
