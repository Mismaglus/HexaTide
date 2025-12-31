namespace Game.World
{
    /// <summary>
    /// 战斗/事件时携带的上下文，用于决定战斗结束后的流程以及奖励。
    /// struct 保持轻量级，避免引用大量资源。
    /// </summary>
    public enum ReturnPolicy
    {
        ReturnToChapter,
        ExitChapter
    }

    public enum GateKind
    {
        None,
        LeftGate,
        RightGate,
        SkipGate
    }

    public struct EncounterContext
    {
        public ReturnPolicy policy;
        public GateKind gateKind;
        public string nextChapterId;
        public string specificEventId;

        /// <summary>
        /// 奖励配置 ID。ChapterNode 在生成时根据节点类型赋值，BattleStateMachine 会从 RewardProfileDB 中读取。
        /// </summary>
        public string rewardProfileId;
    }
}
