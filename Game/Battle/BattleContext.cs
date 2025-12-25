using UnityEngine;
using Game.World; // 引用 EncounterContext

namespace Game.Battle
{
    /// <summary>
    /// Holds static runtime data for the current battle session.
    /// Acts as a bridge between the Chapter Map and the Battle Scene.
    /// </summary>
    public static class BattleContext
    {
        // 使用 Nullable，如果是 null 说明是直接在这个场景运行 debug，或者没有前置上下文
        public static EncounterContext? EncounterContext;

        public static void Reset()
        {
            EncounterContext = null;
            // 如果未来有其他战斗临时数据（如累计伤害等），也在这里重置
        }
    }
}