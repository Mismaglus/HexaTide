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
        [Tooltip("拖入 MaxStride 从 2 到 6 的背景图 (共5张)")]
        public Sprite[] bgSprites; // 0:Max2, 1:Max3, 2:Max4, 3:Max5, 4:Max6

        [Tooltip("拖入 MaxStride 从 2 到 6 的 Tint 图 (共5张)")]
        public Sprite[] tintSprites;

        [Header("Generic Remaining Sprites (General Case)")]
        [Tooltip("通用：剩余 1 格")]
        public Sprite generic1ofN;
        [Tooltip("通用：剩余 2 格")]
        public Sprite generic2ofN;
        [Tooltip("通用：剩余 3 格")]
        public Sprite generic3ofN;
        [Tooltip("通用：剩余 4 格")]
        public Sprite generic4ofN;
        [Tooltip("通用：剩余 5 格 (用于 Max=5)")]
        public Sprite generic5ofN;

        [Header("Special Sprites (Only for Max = 6)")]
        [Tooltip("特例：6格上限时的剩余 3 格")]
        public Sprite special3of6;
        [Tooltip("特例：6格上限时的剩余 4 格")]
        public Sprite special4of6;
        [Tooltip("特例：6格上限时的剩余 5 格")]
        public Sprite special5of6;
        [Tooltip("特例：6格上限时的剩余 6 格 (满)")]
        public Sprite special6of6;

        /// <summary>
        /// 由 UnitFrameUI 调用
        /// </summary>
        public void UpdateView(int current, int max)
        {
            // 1. 更新背景 (Background & Tint)
            // 数组 Index 0 对应 Max 2
            int bgIndex = Mathf.Clamp(max, 2, 6) - 2;

            if (bgIndex >= 0 && bgIndex < bgSprites.Length)
                if (background) background.sprite = bgSprites[bgIndex];

            if (bgIndex >= 0 && bgIndex < tintSprites.Length)
                if (backgroundTint) backgroundTint.sprite = tintSprites[bgIndex];

            // 2. 更新前景 (RemainStride)
            Sprite targetSprite = GetRemainSprite(current, max);

            if (remainStride)
            {
                if (targetSprite != null)
                {
                    remainStride.enabled = true;
                    remainStride.sprite = targetSprite;
                    // 如果你的图片尺寸不一，可能需要 SetNativeSize，否则保持原样
                    remainStride.SetNativeSize();
                }
                else
                {
                    // 如果 stride <= 0 或者没配置图片，隐藏前景
                    remainStride.enabled = false;
                }
            }
        }

        private Sprite GetRemainSprite(int current, int max)
        {
            if (current <= 0) return null;

            // === 特殊情况：Max Stride = 6 ===
            if (max == 6)
            {
                switch (current)
                {
                    case 1: return generic1ofN;      // 1 使用通用
                    case 2: return generic2ofN;      // 2 使用通用 (你的特殊需求)
                    case 3: return special3of6;      // 3 使用特例
                    case 4: return special4of6;      // 4 使用特例
                    case 5: return special5of6;      // 5 使用特例
                    case 6: return special6of6;      // 6 使用特例
                    default: return special6of6;     // 溢出保底
                }
            }

            // === 一般情况 (Max 2~5) ===
            switch (current)
            {
                case 1: return generic1ofN;
                case 2: return generic2ofN;
                case 3: return generic3ofN;
                case 4: return generic4ofN;
                case 5: return generic5ofN;
                default: return generic5ofN; // 溢出保底
            }
        }
    }
}