namespace Game.World
{
    public enum ReturnPolicy
    {
        ReturnToChapter, // 普通战斗/事件：结束后加载回当前 ChapterScene 并恢复状态
        ExitChapter      // Boss/门：结束后前往下一章 (Act2 / Act3)，不回当前场景
    }

    public enum GateKind
    {
        None,
        LeftGate,
        RightGate,
        SkipGate
    }

    /// <summary>
    /// Context passed from Chapter Map to Battle Scene (or Event System)
    /// to determine what happens after the encounter ends.
    /// </summary>
    [System.Serializable]
    public struct EncounterContext
    {
        public ReturnPolicy policy;
        public GateKind gateKind;     // Specifically for Act1 -> Act2 transitions
        public string nextChapterId;  // If ExitChapter, where do we go?
        public string specificEventId; // For narrative events
    }
}