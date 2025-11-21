using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Battle.Abilities
{
    public enum TargetShape { Self, Single, Disk, Ring, Line }
    public enum TargetFaction { Any, Ally, Enemy, SelfOnly }
    public enum AbilityType { Physical, Magical, Mixed }

    public abstract class Ability : ScriptableObject
    {
        [Header("Identity")]
        public string abilityId;
        public string displayName;
        public Sprite icon;

        [Header("Costs")]
        public int apCost = 1;
        [Min(0)] public int mpCost = 0; // ⭐ 确保这里填了数值
        public int cooldownTurns = 0;

        [Header("Targeting")]
        public TargetShape shape = TargetShape.Single;
        public TargetFaction targetFaction = TargetFaction.Enemy;
        public int minRange = 1;
        public int maxRange = 1;
        public bool requiresLoS = false;

        [Header("Classification")]
        public AbilityType abilityType = AbilityType.Physical;

        [Header("Effects")]
        public List<AbilityEffect> effects = new();

        [Header("Animation")]
        public string animTrigger = string.Empty;
        public bool faceTarget = true;
        public float preWindupSeconds = 0.2f;
        public float postRecoverSeconds = 0.2f;
        public bool waitForAnimCompletion = true;
        public int animLayerIndex = 0;
        public string animStateName = string.Empty;
        public string animStateTag = string.Empty;
        public float animWaitTimeout = 5f;

        // ⭐ 核心检查逻辑 (带 Debug)
        public virtual bool CanUse(BattleUnit caster)
        {
            if (caster == null) return false;

            // 1. 检查 AP
            if (caster.CurAP < apCost)
            {
                // Debug.Log($"[Ability] AP 不足: {caster.name} 只有 {caster.CurAP}, 需要 {apCost}");
                return false;
            }

            // 2. 检查 MP
            if (mpCost > 0)
            {
                if (caster.Attributes == null)
                {
                    Debug.LogError($"[Ability] {caster.name} 缺少 UnitAttributes 组件！");
                    return false;
                }

                if (caster.Attributes.Core.MP < mpCost)
                {
                    // 🔴 这里就是你没反应的原因！
                    Debug.Log($"[Ability] MP 不足: {caster.name} 只有 {caster.Attributes.Core.MP}, 需要 {mpCost}");
                    return false;
                }
            }

            return true;
        }

        public virtual bool IsValidTarget(BattleUnit caster, AbilityContext ctx) => ctx != null && ctx.HasAnyTarget;

        public virtual IEnumerator Execute(BattleUnit caster, AbilityContext ctx, AbilityRunner runner)
        {
            // 如果检查失败，直接退出 (这也是为什么你没看到后续 Log)
            if (!CanUse(caster))
            {
                Debug.LogWarning("[Ability] Execute 被终止: 资源不足。");
                yield break;
            }
            if (!IsValidTarget(caster, ctx))
            {
                Debug.LogWarning("[Ability] Execute 被终止: 目标无效。");
                yield break;
            }

            // ⭐ 真正扣除资源
            caster.TrySpendAP(apCost);
            if (mpCost > 0) caster.TrySpendMP(mpCost);

            // 执行效果
            yield return runner.PerformEffects(caster, this, ctx, effects);
        }
    }
}