using UnityEngine;
using UnityEngine.UI;

namespace Game.UI.Helper
{
    /// <summary>
    /// 挂载在 UI 结构中的 "Stride" 根物体上。
    /// 负责根据 MaxStride 和 CurrentStride 切换背景图和前景图。
    /// </summary>
    public class StrideVisualController : MonoBehaviour
    {
        [Header("UI Components")]
        public Image background;
        public Image backgroundTint;
        public Image remainStride;

        [Header("Max Stride Backgrounds (Index 0 = Max 2)")]
        [Tooltip("拖入 MaxStride 从 2 到 6 的背景图 (共5张)。Tint 层也会使用这张图。")]
        public Sprite[] bgSprites; // 0:Max2, 1:Max3, 2:Max4, 3:Max5, 4:Max6

        // 已移除 Full Stride 字段，逻辑中直接复用 bgSprites

        [Header("Generic Remaining Sprites (0 to 4 of N)")]
        [Tooltip("通用：剩余 0 格 (0 of N)")]
        public Sprite generic0OfN;

        [Tooltip("通用：剩余 1 格 (1 of N)")]
        public Sprite generic1OfN;

        [Tooltip("通用：剩余 2 格 (2 of N)")]
        public Sprite generic2OfN;

        [Tooltip("通用：剩余 3 格 (3 of N)")]
        public Sprite generic3OfN;

        [Tooltip("通用：剩余 4 格 (4 of N)")]
        public Sprite generic4OfN;

        // 已移除 generic5OfN：
        // 如果 Max=5 且 Current=5 -> 走 Full 逻辑 (用背景图)
        // 如果 Max=6 且 Current=5 -> 走下面的 Special 逻辑

        [Header("Special Sprites (Only for Max = 6)")]
        [Tooltip("特例：6格上限时的剩余 3 格 (3 of 6)")]
        public Sprite special3Of6;

        [Tooltip("特例：6格上限时的剩余 4 格 (4 of 6)")]
        public Sprite special4Of6;

        [Tooltip("特例：6格上限时的剩余 5 格 (5 of 6)")]
        public Sprite special5Of6;

        /// <summary>
        /// 由 UnitFrameUI 调用
        /// </summary>
        public void UpdateView(int current, int max)
        {
            // 1. 更新背景 (Background & Tint)
            // 数组 Index 0 对应 Max 2
            int bgIndex = Mathf.Clamp(max, 2, 6) - 2;

            Sprite targetBg = null;
            if (bgSprites != null && bgIndex >= 0 && bgIndex < bgSprites.Length)
            {
                targetBg = bgSprites[bgIndex];
            }

            if (targetBg != null)
            {
                if (background) background.sprite = targetBg;
                if (backgroundTint) backgroundTint.sprite = targetBg;
            }

            // 2. 更新前景 (RemainStride)
            Sprite targetRemain = GetRemainSprite(current, max, targetBg);

            if (remainStride)
            {
                if (targetRemain != null)
                {
                    remainStride.enabled = true;
                    remainStride.sprite = targetRemain;
                    remainStride.SetNativeSize();
                }
                else
                {
                    remainStride.enabled = false;
                }
            }
        }

        private Sprite GetRemainSprite(int current, int max, Sprite currentBg)
        {
            // === Case: Full Stride (满步数) ===
            // 无论是 3/3, 5/5 还是 6/6，只要满了，就显示完整的背景图作为前景
            if (current >= max)
            {
                return currentBg;
            }

            // === Case: Zero Stride (0步) ===
            if (current <= 0)
            {
                return generic0OfN;
            }

            // === Case: Special Max = 6 ===
            if (max == 6)
            {
                switch (current)
                {
                    case 1: return generic1OfN;      // 1 使用通用
                    case 2: return generic2OfN;      // 2 使用通用
                    case 3: return special3Of6;      // 3 使用特例
                    case 4: return special4Of6;      // 4 使用特例
                    case 5: return special5Of6;      // 5 使用特例
                    default: return currentBg;       // 理论上不会走到这里 (>=6 已被上面拦截)
                }
            }

            // === Case: General (Max 2~5) ===
            switch (current)
            {
                case 1: return generic1OfN;
                case 2: return generic2OfN;
                case 3: return generic3OfN;
                case 4: return generic4OfN;
                default: return currentBg;
            }
        }
    }
}