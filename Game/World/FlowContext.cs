namespace Game.World
{
    /// <summary>
    /// Holds high-level state across scenes. Currently tracks which chapter should be loaded next.
    /// MapScene reads this value to determine which ChapterSettings to apply when generating a new map.
    /// </summary>
    public static class FlowContext
    {
        /// <summary>
        /// The identifier of the chapter that should be loaded when MapScene starts.
        /// When exiting a chapter via a gate, BattleOutcomeUI should set this before loading MapScene.
        /// This value persists across scene loads until overwritten.
        /// </summary>
        public static string CurrentChapterId;
    }
}
