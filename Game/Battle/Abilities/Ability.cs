using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Game.Units;
using Game.Localization; // 引用

namespace Game.Battle.Abilities
{
    public enum TargetShape { Self, Single, Disk, Ring, Line, Cone }
    public enum AbilityType { Physical, Magical, Mixed }
    public enum AbilityTargetType { EnemyUnit, FriendlyUnit, EmptyTile, AnyTile, Self }

    public abstract class Ability : ScriptableObject
    {
        [Header("Localization Identity")]
        [Tooltip("技能的唯一ID，如 'SKILL_SLASH'。\n系统会自动查找 SKILL_SLASH_NAME, _DESC, _FLAVOR")]
        public string abilityID;
        public Sprite icon;

        // ⭐ 获取本地化名称
        public string LocalizedName => LocalizationManager.Get($"{abilityID}_NAME");
        public string LocalizedFlavor => LocalizationManager.Get($"{abilityID}_FLAVOR");
        [Header("Costs")]
        public int apCost = 1;
        [Min(0)] public int mpCost = 0;
        public int cooldownTurns = 0;

        [Header("Targeting Input")]
        public AbilityTargetType targetType = AbilityTargetType.EnemyUnit;
        public int minRange = 1;
        public int maxRange = 1;
        public bool requiresLoS = false;

        [Header("Area of Effect (AOE)")]
        public TargetShape shape = TargetShape.Single;
        [Min(0)] public int aoeRadius = 0;

        [Header("Target Filtering")]
        public bool affectEnemies = true;
        public bool affectAllies = false;
        public bool affectSelf = false;

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

        // 虚方法：检查是否可用
        public virtual bool CanUse(BattleUnit caster)
        {
            if (caster == null) return false;
            if (caster.CurAP < apCost) return false;
            if (caster.Attributes != null && caster.Attributes.Core.MP < mpCost) return false;
            return true;
        }

        // 虚方法：检查目标是否有效
        public virtual bool IsValidTarget(BattleUnit caster, AbilityContext ctx) => ctx != null && ctx.HasAnyTarget;

        // 虚方法：执行技能
        public virtual IEnumerator Execute(BattleUnit caster, AbilityContext ctx, AbilityRunner runner)
        {
            if (!CanUse(caster)) yield break;
            if (!IsValidTarget(caster, ctx)) yield break;

            caster.TrySpendAP(apCost);
            if (mpCost > 0) caster.TrySpendMP(mpCost);

            yield return runner.PerformEffects(caster, this, ctx, effects);
        }
        // ⭐ 获取本地化 Flavor Text
        // 动态描述生成
        public string GetDynamicDescription(BattleUnit caster)
        {
            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            foreach (var effect in effects)
            {
                if (effect != null)
                    sb.AppendLine(effect.GetDescription(caster));
            }
            return sb.ToString();
        }
    }
}