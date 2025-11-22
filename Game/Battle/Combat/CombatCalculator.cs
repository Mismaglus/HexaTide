using UnityEngine;
using Game.Units;
using Game.Common;
using Game.Battle.Abilities.Effects; // ⭐ 引用 DamageEffect 所在的命名空间

namespace Game.Battle.Combat
{
    public static class CombatCalculator
    {
        // ⭐⭐⭐ 新增：适配器方法，供 DamageEffect 调用 ⭐⭐⭐
        public static CombatResult CalculateDamage(BattleUnit attacker, BattleUnit defender, DamageEffect effect)
        {
            // 1. 将 DamageEffect 的简单数据转换为 DamageConfig
            // 这里我们做个简单的映射：baseDamage -> basePhysical, scalingFactor -> Str Scaling
            var config = new DamageConfig();

            config.basePhysical = effect.baseDamage;
            config.physScaling.Str = effect.scalingFactor; // 假设默认缩放基于力量

            config.variance = 0.1f; // 默认 10% 浮动

            // 2. 调用核心计算逻辑
            return ResolveAttack(attacker, defender, config);
        }

        // 核心计算逻辑 (保持不变，只是更新了 isCritical 字段名)
        public static CombatResult ResolveAttack(BattleUnit attacker, BattleUnit defender, DamageConfig config)
        {
            var result = new CombatResult();
            var att = attacker.Attributes;
            var def = defender.Attributes;

            // 1. 命中 (Hit)
            float hitChance = 100f + (att.Recommended.Accuracy - def.Recommended.Evasion);
            if (GameRandom.Range(0f, 100f) >= hitChance)
            {
                result.isHit = false;
                return result;
            }
            result.isHit = true;

            // 2. 暴击 (Crit)
            // ⭐ 修复：使用 isCritical
            result.isCritical = GameRandom.Value < att.Recommended.CritChance;
            float critMult = result.isCritical ? att.Recommended.CritMult : 1.0f;

            // 3. 浮动 (Variance)
            float variance = 1.0f;
            if (config.variance > 0)
                variance = GameRandom.Range(1f - config.variance, 1f + config.variance);

            float globalMult = critMult * variance;

            // 4. 伤害计算
            float rawPhys = config.basePhysical + config.physScaling.Evaluate(att.Core);
            float defPhys = Mathf.Clamp01(def.Core.Armor - att.Recommended.PenetrationPhys);
            float finalPhys = rawPhys * (1f - defPhys);

            float rawMag = config.baseMagical + config.magScaling.Evaluate(att.Core);
            float defMag = Mathf.Clamp01(def.Core.Ward - att.Recommended.PenetrationMag);
            float finalMag = rawMag * (1f - defMag);

            result.finalDamage = Mathf.RoundToInt((finalPhys + finalMag) * globalMult);

            // 保底 1 点 (如果基础伤害>0)
            if (result.finalDamage < 1 && (rawPhys + rawMag > 0)) result.finalDamage = 1;

            return result;
        }
    }
}