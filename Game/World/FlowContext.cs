namespace Game.World
{
    /// <summary>
    /// Holds high-level state across scenes. Currently tracks which chapter should be loaded next.
    /// MapScene reads this value to determine which ChapterSettings to apply when generating a new map.
    /// </summary>
    public static class FlowContext
    {
        /// <summary>
        /// Current act index (1..4). MapScene uses this to select act-specific ChapterSettings.
        /// This value persists across scene loads until overwritten.
        /// </summary>
        public static int CurrentAct = 1;

        /// <summary>
        /// Current region id (e.g., "REGION_1".."REGION_8").
        /// Historically this field was called "chapter"; keep the name for compatibility.
        /// </summary>
        public static string CurrentChapterId = "REGION_1";
    }
}
