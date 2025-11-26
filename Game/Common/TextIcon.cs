using UnityEngine;
using Game.Localization; // 引用本地化系统

namespace Game.Common
{
    public static class TextIcons
    {
        // --- 1. 颜色定义 (Hex) ---
        public const string COL_STR = "#FF5555"; // 力量红
        public const string COL_DEX = "#55FF55"; // 敏捷绿
        public const string COL_INT = "#55AAFF"; // 智力蓝
        public const string COL_FAI = "#FFDD55"; // 信仰金

        public const string COL_PHYS = "#FFAAAA"; // 物理淡红
        public const string COL_MAG = "#AAAAFF"; // 魔法淡蓝
        public const string COL_AP = "#8372AB"; // AP紫
        public const string COL_MP = "#6A96BF"; // MP蓝

        // --- 2. 图标定义 (TMP Sprite Tags) ---
        // 这里的 name 必须和你 Sprite Asset 里的名字一致
        public const string SPR_STR = "<sprite name=\"STR\">";
        public const string SPR_DEX = "<sprite name=\"DEX\">";
        public const string SPR_INT = "<sprite name=\"INT\">";
        public const string SPR_FAI = "<sprite name=\"FAI\">";

        public const string SPR_PHYS = "<sprite name=\"Physical\">";
        public const string SPR_MAG = "<sprite name=\"Magical\">";
        public const string SPR_AP = "<sprite name=\"AP\">";
        public const string SPR_MP = "<sprite name=\"MP\">";

        // --- 3. 本地化获取属性 (图标 + 翻译名) ---
        // 例如: "[图标] 力量" (带颜色)
        public static string StrName => $"{SPR_STR} {Color(LocalizationManager.Get("STAT_STR"), COL_STR)}";
        public static string DexName => $"{SPR_DEX} {Color(LocalizationManager.Get("STAT_DEX"), COL_DEX)}";
        public static string IntName => $"{SPR_INT} {Color(LocalizationManager.Get("STAT_INT"), COL_INT)}";
        public static string FaiName => $"{SPR_FAI} {Color(LocalizationManager.Get("STAT_FAI"), COL_FAI)}";

        public static string PhysName => $"{SPR_PHYS} {Color(LocalizationManager.Get("DMG_PHYSICAL"), COL_PHYS)}";
        public static string MagName => $"{SPR_MAG}  {Color(LocalizationManager.Get("DMG_MAGICAL"), COL_MAG)}";

        // --- 4. 辅助方法 ---
        public static string Color(object text, string hex) => $"<color={hex}>{text}</color>";

        // 格式化 AP/MP 消耗 (例如: "2 [AP图标]")
        public static string FormatAP(int val) => $"{Color(val, COL_AP)}{SPR_AP}";
        public static string FormatMP(int val) => $"{Color(val, COL_MP)}{SPR_MP}";
    }
}