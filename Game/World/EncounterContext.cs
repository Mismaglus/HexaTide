namespace Game.World
{
    public enum ReturnPolicy
    {
        ReturnToChapter,
        ExitChapter
    }

    public class EncounterContext
    {
        public static EncounterContext Current;

        public ReturnPolicy policy;
        public string nextChapterId = null;
        public ChapterNodeType nodeType;
    }
}
