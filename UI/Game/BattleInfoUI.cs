using UnityEngine;
using TMPro;
using Game.Battle;
using Game.Localization;
using System.Collections.Generic;
using System.Text;

namespace Game.UI
{
    public class BattleInfoUI : MonoBehaviour
    {
        [Header("References")]
        public TextMeshProUGUI chapterLabel;
        public TextMeshProUGUI turnLabel;

        [Header("Settings")]
        [Tooltip("当前章节索引 (1 = 第一章/Chapter I)。")]
        public int currentChapterIndex = 1;

        [Header("Localization Keys")]
        public string keyChapterFormat = "UI_CHAPTER_FORMAT"; // "Chapter {0}" or "第{0}章"
        public string keyTurnFormat = "UI_TURN_FORMAT";       // "Turn {0}" or "第{0}回合"

        // Runtime State
        private int _currentTurnCount = 1;
        private BattleStateMachine _sm;

        void Awake()
        {
            _sm = FindFirstObjectByType<BattleStateMachine>(FindObjectsInactive.Exclude);
        }

        void OnEnable()
        {
            if (_sm != null) _sm.OnTurnChanged += HandleTurnChanged;
            RefreshUI();
        }

        void OnDisable()
        {
            if (_sm != null) _sm.OnTurnChanged -= HandleTurnChanged;
        }

        void HandleTurnChanged(TurnSide side)
        {
            // 只有在非开局时切回玩家回合才+1
            if (side == TurnSide.Player && Time.timeSinceLevelLoad > 0.1f)
            {
                _currentTurnCount++;
            }
            RefreshUI();
        }

        public void RefreshUI()
        {
            string chapterFmt = LocalizationManager.Get(keyChapterFormat);
            string turnFmt = LocalizationManager.Get(keyTurnFormat);

            // 简单判断是否为中文环境 (也可以检查 LocalizationManager.CurrentLanguage)
            bool useChinese = chapterFmt.Contains("第") || turnFmt.Contains("第");

            string chapNumStr;
            string turnNumStr;

            if (useChinese)
            {
                // 中文：章节用中文数字 (第一章, 第十二章)
                chapNumStr = IntToChinese(currentChapterIndex);

                // 中文：回合数
                // 策略：如果回合数太大(>99)，中文太长会破坏UI，建议切回阿拉伯数字
                // 例如："第一百二十五回合" (7字) vs "第125回合" (4字)
                if (_currentTurnCount <= 99)
                    turnNumStr = IntToChinese(_currentTurnCount);
                else
                    turnNumStr = _currentTurnCount.ToString();
            }
            else
            {
                // 英文：章节用罗马数字 (Chapter I, Chapter XI)
                chapNumStr = IntToRoman(currentChapterIndex);
                // 英文：回合用阿拉伯数字 (Turn 1, Turn 12)
                turnNumStr = _currentTurnCount.ToString();
            }

            if (chapterLabel != null) chapterLabel.text = string.Format(chapterFmt, chapNumStr);
            if (turnLabel != null) turnLabel.text = string.Format(turnFmt, turnNumStr);
        }

        public void ResetCounter()
        {
            _currentTurnCount = 1;
            RefreshUI();
        }

        // =========================================================
        // ⭐ 通用算法区域
        // =========================================================

        /// <summary>
        /// 通用整数转罗马数字 (支持 1 - 3999)
        /// </summary>
        private string IntToRoman(int num)
        {
            if (num < 1) return num.ToString();
            if (num >= 4000) return num.ToString(); // 罗马数字标准不支持 >= 4000

            StringBuilder sb = new StringBuilder();

            // 映射表
            (int val, string s)[] map = {
                (1000, "M"), (900, "CM"), (500, "D"), (400, "CD"),
                (100, "C"), (90, "XC"), (50, "L"), (40, "XL"),
                (10, "X"), (9, "IX"), (5, "V"), (4, "IV"), (1, "I")
            };

            foreach (var pair in map)
            {
                while (num >= pair.val)
                {
                    sb.Append(pair.s);
                    num -= pair.val;
                }
            }
            return sb.ToString();
        }

        /// <summary>
        /// 通用整数转中文数字 (支持 0 - 9999)
        /// 自动处理 "一十" -> "十" 的口语化习惯
        /// </summary>
        private string IntToChinese(int num)
        {
            if (num == 0) return "零";
            if (num < 0) return num.ToString(); // 负数暂不处理

            string[] digits = { "零", "一", "二", "三", "四", "五", "六", "七", "八", "九" };
            string[] units = { "", "十", "百", "千", "万" };

            // 拆分数字
            List<int> parts = new List<int>();
            int temp = num;
            while (temp > 0)
            {
                parts.Add(temp % 10);
                temp /= 10;
            }

            StringBuilder sb = new StringBuilder();
            bool zeroFlag = false; // 连续零标记

            for (int i = parts.Count - 1; i >= 0; i--)
            {
                int digit = parts[i];
                int unitIdx = i; // 0=个, 1=十, 2=百...

                if (digit != 0)
                {
                    if (zeroFlag)
                    {
                        sb.Append(digits[0]); // 补零 (例如 101 -> 一百零一)
                        zeroFlag = false;
                    }

                    // 处理 "一十" -> "十" 的特殊情况 (仅在十位且总位数<=2时，如 12->十二, 但 112->一百一十二)
                    if (digit == 1 && unitIdx == 1 && parts.Count == 2)
                    {
                        // 不加"一"，直接加单位
                    }
                    else
                    {
                        sb.Append(digits[digit]);
                    }

                    sb.Append(units[unitIdx]);
                }
                else
                {
                    // 遇到0，不立即输出，而是标记，只有下一位非0时才补零
                    // 个位数的0不输出 (如 20 -> 二十，而不是 二十零)
                    if (unitIdx != 0)
                        zeroFlag = true;
                }
            }

            return sb.ToString();
        }
    }
}