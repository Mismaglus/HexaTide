using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Game.Units;
using Game.Localization;

namespace Game.Battle.Abilities
{
    public enum TargetShape { Self, Single, Disk, Ring, Line, Cone }
    public enum AbilityType { Physical, Magical, Mixed }
    public enum AbilityTargetType { EnemyUnit, FriendlyUnit, EmptyTile, AnyTile, Self }

    public abstract class Ability : ScriptableObject
    {
        [Header("Localization Identity")]
        public string abilityID;
        public Sprite icon;

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

        [Tooltip("-1 代表无限/全图")]
        [Min(-1)] public int aoeRadius = 0;

        // === ⭐ 新增：AOE 形状微调参数 ===
        [Header("Shape Settings")]
        [Tooltip("仅用于 Cone：扇形角度 (度)。标准战棋通常为 60 或 90。")]
        [Range(30f, 180f)] public float coneAngle = 60f;

        [Tooltip("仅用于 Line：直线宽度 (世界单位)。\n假设六边形半径为1，宽度 1.74 刚好覆盖一列。\n想要打到边缘，尝试设置 2.0 或更大。")]
        [Min(0.1f)] public float lineWidth = 1.0f;

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

        public virtual bool CanUse(BattleUnit caster)
        {
            if (caster == null) return false;
            if (caster.CurAP < apCost) return false;
            if (caster.Attributes != null && caster.Attributes.Core.MP < mpCost) return false;
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