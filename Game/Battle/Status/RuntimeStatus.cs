// Scripts/Game/Battle/Status/RuntimeStatus.cs
using Game.Battle;

namespace Game.Battle.Status
{
    [System.Serializable]
    public class RuntimeStatus
    {
        public StatusDefinition Definition { get; private set; }
        public BattleUnit Source { get; private set; } // 施加者

        public int DurationLeft;
        public int Stacks;

        public RuntimeStatus(StatusDefinition def, BattleUnit source)
        {
            Definition = def;
            Source = source;
            DurationLeft = def.defaultDuration;
            Stacks = 1;
        }

        public void TickDuration()
        {
            if (!Definition.isPermanent) DurationLeft--;
        }

        public bool IsExpired => !Definition.isPermanent && DurationLeft <= 0;

        public void AddStack(int duration)
        {
            if (Definition.stackBehavior == StackBehavior.AddDuration)
                DurationLeft += duration;
            else if (Definition.stackBehavior == StackBehavior.MaxDuration)
                DurationLeft = UnityEngine.Mathf.Max(DurationLeft, duration);

            if (Stacks < Definition.maxStacks) Stacks++;
        }
    }
}