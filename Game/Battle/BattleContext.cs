using Game.Inventory;

namespace Game.Battle
{
    /// <summary>
    /// 静态类，在战斗各阶段存储跨系统共享的上下文。
    /// </summary>
    public static class BattleContext
    {
        /// <summary>
        /// 当前战斗关联的 EncounterContext，可为空。
        /// </summary>
        public static Game.World.EncounterContext? EncounterContext { get; set; }

        /// <summary>
        /// 调试用：直接指定使用哪个 LootTableSO 覆盖。设置后优先使用。
        /// </summary>
        public static LootTableSO ActiveLootTable { get; set; }

        /// <summary>
        /// 调试用：直接指定使用哪个 RewardProfileSO 覆盖。优先级高于 rewardProfileId。
        /// </summary>
        public static RewardProfileSO ActiveRewardProfile { get; set; }

        /// <summary>
        /// 清空战斗上下文。通常在加载新战斗或战斗结束时调用。
        /// </summary>
        public static void Reset()
        {
            EncounterContext = null;
            ActiveLootTable = null;
            ActiveRewardProfile = null;
        }
    }
}
