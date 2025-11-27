// Scripts/Game/Battle/Status/RuntimeStatus.cs
using Game.Battle;
using UnityEngine;

namespace Game.Battle.Status
{
    [System.Serializable]
    public class RuntimeStatus
    {
        public StatusDefinition Definition { get; private set; }
        public BattleUnit Source { get; private set; }

        public int DurationLeft;
        public int Stacks;

        // ⭐ 修改构造函数：接收 initialStacks
        public RuntimeStatus(StatusDefinition def, BattleUnit source, int initialStacks = 1)
        {
            Definition = def;
            Source = source;
            DurationLeft = def.defaultDuration;

            // 确保不超过上限
            Stacks = Mathf.Clamp(initialStacks, 1, def.maxStacks);
        }

        public void TickDuration()
        {
            if (!Definition.isPermanent) DurationLeft--;
        }

        public bool IsExpired => !Definition.isPermanent && DurationLeft <= 0;

        // ⭐ 修改堆叠逻辑：接收 amount
        public void AddStack(int duration, int amount)
        {
            // 1. 刷新时间
            if (Definition.stackBehavior == StackBehavior.AddDuration)
                DurationLeft += duration;
            else if (Definition.stackBehavior == StackBehavior.MaxDuration)
                DurationLeft = Mathf.Max(DurationLeft, duration);

            // 2. 增加层数 (允许一次加多层)
            if (Stacks < Definition.maxStacks)
            {
                Stacks = Mathf.Min(Stacks + amount, Definition.maxStacks);
            }
        }
    }
}