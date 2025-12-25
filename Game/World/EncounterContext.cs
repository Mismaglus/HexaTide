using UnityEngine;
using Core.Hex;

namespace Game.World
{
    public enum ReturnPolicy
    {
        ReturnToChapter,
        ExitChapter
    }

    public enum EncounterKind
    {
        Normal,
        Elite,
        BossGate,
        Event
    }

    public enum GateKind
    {
        Left,
        Right,
        Skip
    }

    public class EncounterContext
    {
        public static EncounterContext Current;

        public ReturnPolicy returnPolicy;
        public EncounterKind encounterKind;
        public GateKind? gateKind;
        public string chapterId;
        public HexCoords nodeCoords;
        public string destination; // Next Chapter ID or "Act3"

        public static void Reset()
        {
            Current = null;
        }
    }
}
