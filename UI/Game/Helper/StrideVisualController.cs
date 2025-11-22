using UnityEngine;
using UnityEngine.UI;

namespace Game.UI.Helper
{
    public class StrideVisualController : MonoBehaviour
    {
        [Header("UI Components")]
        public Image background;
        public Image backgroundTint;
        public Image remainStride;

        [Header("Max Stride Backgrounds (Index 0 = Max 2)")]
        public Sprite[] bgSprites; // 0:Max2 ... 4:Max6

        [Header("Generic Remaining Sprites")]
        public Sprite generic0OfN;
        public Sprite generic1OfN;
        public Sprite generic2OfN;
        public Sprite generic3OfN;
        public Sprite generic4OfN;

        [Header("Special Sprites (Max = 6)")]
        public Sprite special3Of6;
        public Sprite special4Of6;
        public Sprite special5Of6;

        // ⭐ 核心修复：定义底槽颜色 (深色)
        // 当我们用金条图做背景时，把它染黑，变成“空槽”
        private Color slotBaseColor = new Color(0.2f, 0.2f, 0.2f, 0.8f);
        private Color fullColor = Color.white;

        public void UpdateView(int current, int max)
        {
            // 1. 确定背景图 (Max Stride)
            int bgIndex = Mathf.Clamp(max, 2, 6) - 2;
            Sprite targetBg = null;
            if (bgSprites != null && bgIndex >= 0 && bgIndex < bgSprites.Length)
                targetBg = bgSprites[bgIndex];

            // 2. 更新背景组件
            if (targetBg != null)
            {
                if (background)
                {
                    background.sprite = targetBg;
                    // ⭐ 关键技巧：永远把背景染黑作为“底座”
                    // 这样无论前景有没有透明度，背景都不会干扰视觉，反而提供轮廓
                    background.color = slotBaseColor;
                    background.enabled = true;
                }
                if (backgroundTint)
                {
                    backgroundTint.sprite = targetBg;
                    backgroundTint.color = slotBaseColor;
                    backgroundTint.enabled = true;
                }
            }

            // 3. 确定前景图 (Remaining)
            Sprite targetRemain = GetRemainSprite(current, max, targetBg);

            if (remainStride)
            {
                if (targetRemain != null)
                {
                    remainStride.enabled = true;
                    remainStride.sprite = targetRemain;
                }
                else
                {
                    // 如果是 0 步，且没有 generic0OfN (或者逻辑返回 null)，则隐藏前景
                    remainStride.enabled = false;
                }
            }
        }

        private Sprite GetRemainSprite(int current, int max, Sprite currentBg)
        {
            // 满步数 -> 显示完整的bg (CurrentBg) 作为前景
            if (current >= max) return currentBg;

            // 0步 -> 显示 0 of N (如果没有配图，返回 null 会隐藏前景，只露底座)
            if (current <= 0) return generic0OfN;

            // Max = 6 特殊处理
            if (max == 6)
            {
                switch (current)
                {
                    case 1: return generic1OfN;
                    case 2: return generic2OfN;
                    case 3: return special3Of6;
                    case 4: return special4Of6;
                    case 5: return special5Of6;
                    default: return currentBg;
                }
            }

            // 通用处理 (Max 2~5)
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