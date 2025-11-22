using System;
using UnityEngine;

namespace Game.Battle.Combat
{
    [Serializable]
    public struct ScalingMatrix
    {
        public float Str;
        public float Dex;
        public float Int;
        public float Faith;

        public float Evaluate(Game.Units.UnitAttributes.CoreAttributes stats)
        {
            return (stats.Str * Str) + (stats.Dex * Dex) + (stats.Int * Int) + (stats.Faith * Faith);
        }
    }

    [Serializable]
    public struct DamageConfig
    {
        [Header("Physical")]
        public int basePhysical;
        public ScalingMatrix physScaling;

        [Header("Magical")]
        public int baseMagical;
        public ScalingMatrix magScaling;

        [Header("Settings")]
        public float variance;

        public static DamageConfig Default()
        {
            return new DamageConfig { basePhysical = 10, variance = 0.05f };
        }
    }

    public struct CombatResult
    {
        public bool isHit;

        // ⭐ 修复：重命名为 isCritical 以匹配 DamageEffect 的调用
        public bool isCritical;

        public int finalDamage;

        public string ToLog()
        {
            return isHit ? $"{finalDamage}{(isCritical ? " CRIT!" : "")}" : "MISS";
        }
    }
}