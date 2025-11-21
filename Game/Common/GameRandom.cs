using UnityEngine;
using System;

namespace Game.Common
{
    /// <summary>
    /// 确定性随机数生成器。
    /// 用于替代 UnityEngine.Random，确保在相同 Seed 下产生完全一致的结果。
    /// </summary>
    public static class GameRandom
    {
        private static System.Random _rng = new System.Random();
        public static int CurrentSeed { get; private set; }

        public static void Init(int seed)
        {
            CurrentSeed = seed;
            _rng = new System.Random(seed);
            Debug.Log($"[GameRandom] Initialized with seed: {seed}");
        }

        public static float Value => (float)_rng.NextDouble();

        public static float Range(float min, float max)
        {
            return min + (float)_rng.NextDouble() * (max - min);
        }

        public static int Range(int min, int max)
        {
            return _rng.Next(min, max);
        }
    }
}