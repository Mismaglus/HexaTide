using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Game.Units;
using Game.Battle; // 引用 KnockbackSystem 所在的命名空间

namespace Game.Battle.Abilities.Effects
{
    [CreateAssetMenu(menuName = "HexBattle/Effects/Knockback")]
    public class KnockbackEffect : AbilityEffect
    {
        [Header("Knockback Settings")]
        [Tooltip("击退距离 (格数)")]
        [Min(1)] public int pushDistance = 1;

        public override IEnumerator Apply(BattleUnit caster, Ability ability, AbilityContext ctx)
        {
            // 1. 查找场景中的击退系统
            // 因为 KnockbackSystem 是 MonoBehaviour，我们需要在运行时找到它
            var kbSystem = FindFirstObjectByType<KnockbackSystem>();

            if (kbSystem == null)
            {
                Debug.LogWarning($"[KnockbackEffect] 场景中找不到 KnockbackSystem 组件，击退失效。请确保它挂载在 BattleController 或类似物体上。");
                yield break;
            }

            // 2. 校验目标
            if (ctx.TargetUnits == null || ctx.TargetUnits.Count == 0)
                yield break;

            // 3. 对上下文中的每个目标单位执行击退
            foreach (var target in ctx.TargetUnits)
            {
                if (target == null) continue;

                // 通常技能不会击退施法者自己
                if (target == caster) continue;

                // 调用核心系统
                kbSystem.ApplyKnockback(caster, target, pushDistance);

                Debug.Log($"[Effect] {caster.name} knocks back {target.name} by {pushDistance} hexes.");
            }

            // 击退通常是即时触发的，不需要在 Effect 层做协程等待
            // (具体的动画等待由 KnockbackSystem 内部协程处理，或者由 AbilityRunner 处理)
            yield break;
        }

        public override string GetDescription(BattleUnit caster)
        {
            // 在技能 Tooltip 中显示的文本
            return $"Push target back <color=#FFFF00>{pushDistance}</color> hexes.";
        }
    }
}