using UnityEngine;
using System;

namespace Game.Common
{
    /// <summary>
    /// 确定性随机数生成器。
    /// 用于替代 UnityEngine.Random，确保在相同 Seed 下产生完全一致的结果（方便 SL、回放、调试）。
    /// </summary>
    public static class GameRandom
    {
        private static System.Random _rng = new System.Random();
        public static int CurrentSeed { get; private set; }

        /// <summary>
        /// 初始化随机数生成器。在战斗开始或关卡加载时调用。
        /// </summary>
        public static void Init(int seed)
        {
            CurrentSeed = seed;
            _rng = new System.Random(seed);
            Debug.Log($"[GameRandom] Initialized with seed: {seed}");
        }

        /// <summary>
        /// 返回 [0.0, 1.0) 之间的浮点数
        /// </summary>
        public static float Value => (float)_rng.NextDouble();

        /// <summary>
        /// 返回 [min, max) 之间的浮点数
        /// </summary>
        public static float Range(float min, float max)
        {
            return min + (float)_rng.NextDouble() * (max - min);
        }

        /// <summary>
        /// 返回 [min, max) 之间的整数 (注意：不包含 max，与 UnityEngine.Random 一致)
        /// </summary>
        public static int Range(int min, int max)
        {
            return _rng.Next(min, max);
        }

        /// <summary>
        /// 模拟掷骰子判定
        /// </summary>
        /// <param name="chance">0~100 的概率</param>
        /// <returns>是否成功</returns>
        public static bool Roll(float chancePercentage)
        {
            return Range(0f, 100f) < chancePercentage;
        }
    }
}