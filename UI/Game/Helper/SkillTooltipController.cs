using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Game.Battle;
using Game.Battle.Abilities;
using Game.Battle.Abilities.Effects;
using Game.Battle.Combat;
using Game.Units;
using Game.Common;       // 引用 TextIcons
using Game.Localization; // 引用 LocalizationManager

namespace Game.UI
{
    public class SkillTooltipController : MonoBehaviour
    {
        [Header("Root References")]
        [SerializeField] private GameObject contentRoot; // 指向 "Content" 或整个物体
        [SerializeField] private RectTransform mainRect; // 指向 HUD_SkillHover 自身的 RectTransform

        [Header("Background Section")]
        public Image backgroundTracery; // 背景上的纹饰 (可选)

        [Header("Icon Section")]
        public Image iconImage;
        public Image iconGlow;
        public Image iconTracery;

        // 顺序必须对应: 0:Phy, 1:Mag, 2:Mix, 3:Enemy
        public GameObject[] gemObjects;

        [Header("Name Section")]
        public TextMeshProUGUI labelName;
        public TextMeshProUGUI labelCost;
        public TextMeshProUGUI labelInfo; // 显示范围和目标图标

        [Header("HUD Basic (Summary)")]
        public GameObject basicRoot;
        public TextMeshProUGUI labelSkillEffect; // 显示总伤害 (例如: 180 + 180)

        [Header("HUD Description")]
        public TextMeshProUGUI labelDescription;

        [Header("Bottom Group")]
        public TextMeshProUGUI labelFlavor;
        public TextMeshProUGUI labelAvailability;

        [Header("Visual Settings")]
        public Vector3 hoverOffset = new Vector3(0, 80f, 0); // 悬浮偏移量

        [Header("Tracery Assets")]
        public Sprite traceryPhysical;
        public Sprite traceryMagic;
        public Sprite traceryMixed;
        public Sprite traceryEnemy;

        [Header("Colors (RGB Only)")]
        public Color enemyTint = new Color(1f, 0.85f, 0.85f, 1f);

        // Glow 颜色基调
        public Color enemyGlow = new Color(0.8f, 0.0f, 0.0f);
        public Color phyGlow = new Color32(0xD9, 0xD3, 0xF3, 0xFF);
        public Color magGlow = new Color32(0x73, 0xB6, 0xFF, 0xFF);
        public Color mixGlow = new Color32(0xE7, 0xF2, 0xFF, 0xFF);

        // Icon Tint 颜色
        public Color phyTint = new Color32(0xE8, 0xE3, 0xF7, 0xFF);
        public Color magTint = new Color32(0xEE, 0xF3, 0xFF, 0xFF);
        public Color mixTint = new Color32(0xE7, 0xF2, 0xFF, 0xFF);

        [Header("Glow Alpha Settings")]
        [Range(0f, 1f)] public float alphaPhysical = 0.10f;
        [Range(0f, 1f)] public float alphaMagic = 0.12f;
        [Range(0f, 1f)] public float alphaMixed = 0.11f;
        [Range(0f, 1f)] public float alphaEnemy = 0.0f; // 默认敌方无高光

        void Awake()
        {
            if (mainRect == null) mainRect = GetComponent<RectTransform>();
            Hide(); // 默认隐藏
        }

        public void Show(Ability ability, BattleUnit caster, bool isEnemy, RectTransform slotRect)
        {
            if (ability == null) return;

            gameObject.SetActive(true);
            UpdatePosition(slotRect);

            // 1. 视觉样式 (颜色、纹饰、宝石)
            UpdateVisuals(ability, isEnemy);

            // 2. 技能名称 (支持本地化)
            // 假设 Ability.cs 中已添加 LocalizedName 属性
            labelName.text = ability.LocalizedName;

            // 3. 消耗 (AP/MP)
            UpdateCost(ability);

            // 4. 范围与目标 (Icon)
            UpdateInfo(ability);

            // 5. 基础效果概览 (总伤害计算)
            UpdateBasicEffect(ability, caster);

            // 6. 详细描述 (读取属性计算)
            // GetDynamicDescription 内部已处理数值计算和本地化拼接
            labelDescription.text = ability.GetDynamicDescription(caster);

            // 7. 风味文本 (支持本地化)
            labelFlavor.text = $"<style=Italic>\"{ability.LocalizedFlavor}\"</style>";

            // 8. 可用性状态 (CD/次数)
            UpdateAvailability(ability, caster);
        }

        public void Hide()
        {
            gameObject.SetActive(false);
        }

        // --- Internal Logic ---

        void UpdatePosition(RectTransform slotRect)
        {
            if (slotRect == null) return;
            // 简单的跟随逻辑，将 Tooltip 放置在目标 Slot 上方
            transform.position = slotRect.position + hoverOffset;
        }

        void UpdateVisuals(Ability ability, bool isEnemy)
        {
            // Icon
            iconImage.sprite = ability.icon;

            string typeName = GetAbilityTypeName(ability).ToLower();
            bool isPhy = typeName.Contains("phys");
            bool isMag = typeName.Contains("magic") || typeName.Contains("magical");

            // Colors & Tracery & Glow Alpha
            if (isEnemy)
            {
                iconImage.color = enemyTint;
                iconGlow.color = GetColorWithAlpha(enemyGlow, alphaEnemy);

                Sprite t = traceryEnemy ?? traceryMixed;
                if (iconTracery) iconTracery.sprite = t;
                if (backgroundTracery) backgroundTracery.sprite = t;

                ToggleGems(false, false, false, true);
            }
            else
            {
                if (isPhy)
                {
                    iconImage.color = phyTint;
                    iconGlow.color = GetColorWithAlpha(phyGlow, alphaPhysical);

                    if (iconTracery) iconTracery.sprite = traceryPhysical;
                    if (backgroundTracery) backgroundTracery.sprite = traceryPhysical;

                    ToggleGems(true, false, false, false);
                }
                else if (isMag)
                {
                    iconImage.color = magTint;
                    iconGlow.color = GetColorWithAlpha(magGlow, alphaMagic);

                    if (iconTracery) iconTracery.sprite = traceryMagic;
                    if (backgroundTracery) backgroundTracery.sprite = traceryMagic;

                    ToggleGems(false, true, false, false);
                }
                else
                {
                    iconImage.color = mixTint;
                    iconGlow.color = GetColorWithAlpha(mixGlow, alphaMixed);

                    if (iconTracery) iconTracery.sprite = traceryMixed;
                    if (backgroundTracery) backgroundTracery.sprite = traceryMixed;

                    ToggleGems(false, false, true, false);
                }
            }
        }

        Color GetColorWithAlpha(Color color, float alpha)
        {
            return new Color(color.r, color.g, color.b, alpha);
        }

        void ToggleGems(bool phy, bool mag, bool mix, bool enemy)
        {
            if (gemObjects == null || gemObjects.Length < 4) return;
            if (gemObjects[0]) gemObjects[0].SetActive(phy);
            if (gemObjects[1]) gemObjects[1].SetActive(mag);
            if (gemObjects[2]) gemObjects[2].SetActive(mix);
            if (gemObjects[3]) gemObjects[3].SetActive(enemy);
        }

        void UpdateCost(Ability ability)
        {
            string costStr = "";
            // 使用 TextIcons 的格式化方法
            if (ability.apCost > 0)
                costStr += $"{TextIcons.FormatAP(ability.apCost)} ";
            if (ability.mpCost > 0)
                costStr += $"{TextIcons.FormatMP(ability.mpCost)}";

            labelCost.text = costStr;
            // 如果无消耗则隐藏这行
            labelCost.gameObject.SetActive(!string.IsNullOrEmpty(costStr));
        }

        void UpdateInfo(Ability ability)
        {
            // 范围图标 (Range)
            string rangeIcon = ability.shape switch
            {
                TargetShape.Cone => "<sprite name=\"Cone\">",
                TargetShape.Disk => "<sprite name=\"Circle\">",
                TargetShape.Ring => "<sprite name=\"Circle\">",
                TargetShape.Line => "<sprite name=\"Straight\">",
                TargetShape.Single => "<sprite name=\"SingleTarget\">",
                _ => "<sprite name=\"SingleTarget\">" // Multi 逻辑暂缺，默认单体
            };

            // 目标图标 (Target)
            string targetIcon = ability.targetType switch
            {
                AbilityTargetType.FriendlyUnit => "<sprite name=\"Ally\">", // 推荐用双层爱心
                AbilityTargetType.EnemyUnit => "<sprite name=\"Enemy\">",   // 推荐用破碎心/骷髅
                AbilityTargetType.EmptyTile => "<sprite name=\"EmptyTile\">",
                AbilityTargetType.AnyTile => "<sprite name=\"AnyTile\">",
                AbilityTargetType.Self => "<sprite name=\"Self\">",
                _ => ""
            };

            labelInfo.text = $"{rangeIcon} {targetIcon}";
        }

        void UpdateBasicEffect(Ability ability, BattleUnit caster)
        {
            int totalPhys = 0;
            int totalMag = 0;
            bool hasDamage = false;

            if (caster != null)
            {
                // 有施法者：计算真实伤害（含属性加成）
                foreach (var effect in ability.effects)
                {
                    if (effect is DamageEffect dmg)
                    {
                        var stats = caster.Attributes.Core;
                        float p = dmg.config.basePhysical + dmg.config.physScaling.Evaluate(stats);
                        float m = dmg.config.baseMagical + dmg.config.magScaling.Evaluate(stats);

                        totalPhys += Mathf.RoundToInt(p);
                        totalMag += Mathf.RoundToInt(m);
                        hasDamage = true;
                    }
                }
            }
            else
            {
                // 无施法者（如在图鉴中）：只显示基础面板
                foreach (var effect in ability.effects)
                {
                    if (effect is DamageEffect dmg)
                    {
                        totalPhys += dmg.config.basePhysical;
                        totalMag += dmg.config.baseMagical;
                        hasDamage = true;
                    }
                }
            }

            if (!hasDamage)
            {
                basicRoot.SetActive(false);
                return;
            }

            basicRoot.SetActive(true);
            string txt = "";

            // 使用 TextIcons 定义的图标常量
            if (totalPhys > 0) txt += $"{TextIcons.SPR_PHYS} {totalPhys}";

            if (totalPhys > 0 && totalMag > 0) txt += " + ";

            if (totalMag > 0) txt += $"{TextIcons.SPR_MAG} {totalMag}";

            labelSkillEffect.text = txt;
        }

        void UpdateAvailability(Ability ability, BattleUnit caster)
        {
            // 动态替换逻辑：优先显示 CD 或 剩余次数
            // 这里假设 Ability 只有静态数据，实际项目中可能需要传入 RuntimeAbility
            if (ability.cooldownTurns > 0)
            {
                string cdLabel = LocalizationManager.Get("UI_COOLDOWN"); // "冷却"
                string turnLabel = LocalizationManager.Get("UI_TURNS");  // "回合"
                labelAvailability.text = $"{cdLabel}: {ability.cooldownTurns} {turnLabel}";
            }
            else
            {
                labelAvailability.text = LocalizationManager.Get("UI_ALWAYS_READY"); // "随时可用"
            }
        }

        // Helper
        string GetAbilityTypeName(Ability ability)
        {
            if (ability == null) return "Mixed";
            if (ability.abilityType == AbilityType.Physical) return "Physical";
            if (ability.abilityType == AbilityType.Magical) return "Magical";
            return "Mixed";
        }
    }
}