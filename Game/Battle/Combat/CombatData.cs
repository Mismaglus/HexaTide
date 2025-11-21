using System;
using UnityEngine;

namespace Game.Battle.Combat
{
    /// <summary>
    /// 定义一组属性加成系数
    /// </summary>
    [Serializable]
    public struct ScalingMatrix
    {
        [Tooltip("Strength scaling factor")]
        public float Str;
        [Tooltip("Dexterity scaling factor")]
        public float Dex;
        [Tooltip("Intelligence scaling factor")]
        public float Int;
        [Tooltip("Faith scaling factor")]
        public float Faith;

        // 方便的计算方法
        public float Evaluate(Game.Units.UnitAttributes.CoreAttributes stats)
        {
            return (stats.Str * Str) + (stats.Dex * Dex) + (stats.Int * Int) + (stats.Faith * Faith);
        }
    }

    [Serializable]
    public struct DamageConfig
    {
        [Header("Physical Component")]
        public int basePhysical;
        public ScalingMatrix physScaling; // 物理部分的专属加成

        [Header("Magical Component")]
        public int baseMagical;
        public ScalingMatrix magScaling;  // 魔法部分的专属加成

        [Header("Global Settings")]
        [Tooltip("0.05 means +/- 5% variance")]
        public float variance;

        public static DamageConfig Default()
        {
            return new DamageConfig
            {
                basePhysical = 10,
                physScaling = new ScalingMatrix { Str = 1.0f }, // 默认物理吃力量
                variance = 0.05f
            };
        }
    }

    public struct CombatResult
    {
        public bool isHit;
        public bool isCrit;
        public int finalDamage;

        public int rawPhysical;
        public int rawMagical;
        public int dmgPhysical; // 减伤后的物理
        public int dmgMagical;  // 减伤后的魔法

        public string ToLog()
        {
            if (!isHit) return "MISS";
            string critStr = isCrit ? " [CRIT!]" : "";
            return $"{finalDamage} (P:{dmgPhysical} + M:{dmgMagical}){critStr}";
        }
    }
}