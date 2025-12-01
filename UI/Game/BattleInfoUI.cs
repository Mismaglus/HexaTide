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
        public TextMeshProUGUI turnLabel; // 显示 "Round 2 - Player Turn"

        // 已移除独立的 phaseLabel，因为现在合并显示了

        [Header("Data Settings")]
        [Tooltip("当前章节索引 (1 = Chapter I)")]
        public int currentChapterIndex = 1;

        [Tooltip("章节名称的本地化Key (例如 'CHAPTER_NAME_1' -> 'Forest')")]
        public string currentChapterNameKey = "CHAPTER_NAME_1";

        [Header("Localization Formats")]
        [Tooltip("Value 应该是 'Chapter {0} - {1}' 或 '第{0}章 - {1}'")]
        public string keyChapterFormat = "UI_CHAPTER_FULL_FORMAT";

        [Tooltip("Value 应该是 'Round {0} - {1}' 或 '第{0}轮 - {1}'")]
        public string keyTurnFormat = "UI_ROUND_FULL_FORMAT";

        [Header("Phase Names")]
        public string keyPhasePlayer = "UI_PHASE_PLAYER";     // "Player Turn"
        public string keyPhaseEnemy = "UI_PHASE_ENEMY";       // "Enemy Turn"

        [Header("Colors (Rich Text)")]
        // 使用富文本颜色代码，因为我们在同一个Label里显示
        public string colorPlayerHex = "#33CCFF"; // 玩家回合蓝
        public string colorEnemyHex = "#FF5555";  // 敌方回合红

        // Runtime State
        private int _currentTurnCount = 1;
        private BattleStateMachine _sm;
        private bool _subscribed;
        private TurnSide _lastSide = TurnSide.Player;

        void Awake()
        {
            ResolveStateMachine();
        }

        void OnEnable()
        {
            ResolveStateMachine();
            if (_sm != null) Subscribe();

            var side = _sm != null ? _sm.CurrentTurn : TurnSide.Player;
            RefreshUI(side);
        }

        void OnDisable()
        {
            if (_sm != null && _subscribed)
            {
                _sm.OnTurnChanged -= HandleTurnChanged;
                _subscribed = false;
            }
        }

        void Update()
        {
            // 防守式兜底：如果事件丢失或迟到，轮询当前 TurnSide
            if (_sm == null) ResolveStateMachine();
            if (_sm != null)
            {
                if (_sm.CurrentTurn != _lastSide)
                {
                    HandleTurnChanged(_sm.CurrentTurn);
                }
            }
        }

        void HandleTurnChanged(TurnSide side)
        {
            // 计数逻辑：只有在非开局时切回玩家回合才+1
            if (side == TurnSide.Player && Time.timeSinceLevelLoad > 0.1f)
            {
                _currentTurnCount++;
            }

            RefreshUI(side);
            _lastSide = side;
        }

        public void RefreshUI(TurnSide currentSide)
        {
            // ================= 1. 章节显示 =================
            // 格式: "Chapter {0} - {1}"
            // arg0: 数字 (I / 一), arg1: 名字 (Forest / 森林)

            string chapterFmt = LocalizationManager.Get(keyChapterFormat);
            bool useChinese = chapterFmt.Contains("第"); // 简单判定语言

            // 获取章节数字符串
            string chapNumStr = useChinese ? IntToChinese(currentChapterIndex) : IntToRoman(currentChapterIndex);

            // 获取章节名
            string chapNameStr = LocalizationManager.Get(currentChapterNameKey);

            if (chapterLabel != null)
            {
                chapterLabel.text = string.Format(chapterFmt, chapNumStr, chapNameStr);
            }

            // ================= 2. 回合(Round)显示 =================
            // 格式: "Round {0} - {1}"
            // arg0: 数字 (2 / 二), arg1: 相位 (Player Turn / 玩家回合)

            string roundFmt = LocalizationManager.Get(keyTurnFormat);

            // 获取回合数字符串
            string turnNumStr;
            if (useChinese && _currentTurnCount <= 99)
                turnNumStr = IntToChinese(_currentTurnCount);
            else
                turnNumStr = _currentTurnCount.ToString();

            // 获取相位名 & 颜色处理
            string phaseName;
            string colorHex;

            if (currentSide == TurnSide.Player)
            {
                phaseName = LocalizationManager.Get(keyPhasePlayer);
                colorHex = colorPlayerHex;
            }
            else
            {
                phaseName = LocalizationManager.Get(keyPhaseEnemy);
                colorHex = colorEnemyHex;
            }

            // 拼接：给相位名加上颜色标签 <color=...>{1}</color>
            string coloredPhase = $"<color={colorHex}>{phaseName}</color>";

            if (turnLabel != null)
            {
                // 注意：这里不仅填入数字，还填入带颜色的相位名
                turnLabel.text = string.Format(roundFmt, turnNumStr, coloredPhase);
            }
        }

        public void ResetCounter()
        {
            _currentTurnCount = 1;
            RefreshUI(TurnSide.Player);
        }

        void ResolveStateMachine()
        {
            if (_sm == null)
            {
                _sm = FindFirstObjectByType<BattleStateMachine>(FindObjectsInactive.Include);
            }

            // 防守式：如果之前没订阅且现在找到了，就订阅
            if (_sm != null && !_subscribed)
            {
                Subscribe();
                _lastSide = _sm.CurrentTurn;
                RefreshUI(_lastSide);
            }
        }

        void Subscribe()
        {
            if (_sm == null) return;
            _sm.OnTurnChanged -= HandleTurnChanged;
            _sm.OnTurnChanged += HandleTurnChanged;
            _subscribed = true;
        }

        // ================= 数字转换工具 =================

        private string IntToRoman(int num)
        {
            if (num < 1) return num.ToString();
            if (num >= 4000) return num.ToString();
            StringBuilder sb = new StringBuilder();
            (int val, string s)[] map = { (1000, "M"), (900, "CM"), (500, "D"), (400, "CD"), (100, "C"), (90, "XC"), (50, "L"), (40, "XL"), (10, "X"), (9, "IX"), (5, "V"), (4, "IV"), (1, "I") };
            foreach (var pair in map) { while (num >= pair.val) { sb.Append(pair.s); num -= pair.val; } }
            return sb.ToString();
        }

        private string IntToChinese(int num)
        {
            if (num == 0) return "零";
            if (num < 0) return num.ToString();
            string[] digits = { "零", "一", "二", "三", "四", "五", "六", "七", "八", "九" };
            string[] units = { "", "十", "百", "千", "万" };
            List<int> parts = new List<int>();
            int temp = num;
            while (temp > 0) { parts.Add(temp % 10); temp /= 10; }
            StringBuilder sb = new StringBuilder();
            bool zeroFlag = false;
            for (int i = parts.Count - 1; i >= 0; i--)
            {
                int digit = parts[i];
                int unitIdx = i;
                if (digit != 0)
                {
                    if (zeroFlag) { sb.Append(digits[0]); zeroFlag = false; }
                    if (digit == 1 && unitIdx == 1 && parts.Count == 2) { }
                    else { sb.Append(digits[digit]); }
                    sb.Append(units[unitIdx]);
                }
                else { if (unitIdx != 0) zeroFlag = true; }
            }
            return sb.ToString();
        }
    }
}
