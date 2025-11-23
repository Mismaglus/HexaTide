using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Battle.Abilities
{
    public enum TargetShape { Self, Single, Disk, Ring, Line }
    // public enum TargetFaction { Any, Ally, Enemy, SelfOnly } // 旧的可以保留或弃用，下面的 TargetType 更全面
    public enum AbilityType { Physical, Magical, Mixed }

    // ⭐ 新增：明确的目标类型定义
    public enum AbilityTargetType
    {
        EnemyUnit,      // 必须有单位，且是敌人
        FriendlyUnit,   // 必须有单位，且是友军
        EmptyTile,      // 必须是空地 (无单位)
        AnyTile         // 只要在范围内，不管有没有人都能放 (例如 AOE)
    }

    public abstract class Ability : ScriptableObject
    {
        [Header("Identity")]
        public string abilityId;
        public string displayName;
        public Sprite icon;

        [Header("Costs")]
        public int apCost = 1;
        [Min(0)] public int mpCost = 0;
        public int cooldownTurns = 0;

        [Header("Targeting")]
        public AbilityTargetType targetType = AbilityTargetType.EnemyUnit; // ⭐ 新增字段
        public TargetShape shape = TargetShape.Single;
        // public TargetFaction targetFaction = TargetFaction.Enemy; // 建议用 targetType 替代此逻辑
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

        public virtual bool CanUse(BattleUnit caster)
        {
            if (caster == null) return false;
            if (caster.CurAP < apCost) return false;
            if (mpCost > 0)
            {
                if (caster.Attributes == null) return false;
                if (caster.Attributes.Core.MP < mpCost) return false;
            }
            return true;
        }

        public virtual bool IsValidTarget(BattleUnit caster, AbilityContext ctx) => ctx != null && ctx.HasAnyTarget;

        public virtual IEnumerator Execute(BattleUnit caster, AbilityContext ctx, AbilityRunner runner)
        {
            if (!CanUse(caster)) yield break;
            if (!IsValidTarget(caster, ctx)) yield break;

            caster.TrySpendAP(apCost);
            if (mpCost > 0) caster.TrySpendMP(mpCost);

            yield return runner.PerformEffects(caster, this, ctx, effects);
        }
    }
}