using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Game.Battle;
using Game.Battle.Abilities;
using Game.Battle.Abilities.Effects; // 引用 DamageEffect
using Game.Battle.Combat; // 引用 CombatData
using Game.Units;

namespace Game.UI
{
    public class SkillTooltipController : MonoBehaviour
    {
        [Header("Root References")]
        [SerializeField] private GameObject contentRoot; // 指向 "Content" 或整个物体
        [SerializeField] private RectTransform mainRect; // 指向 HUD_SkillHover 自身的 RectTransform

        [Header("Icon Section")]
        public Image iconImage;
        public Image iconGlow;
        public Image iconTracery;
        public GameObject[] gemObjects; // 0:Phy, 1:Mag, 2:Mix, 3:Enemy (顺序需对应代码逻辑)

        [Header("Name Section")]
        public TextMeshProUGUI labelName;
        public TextMeshProUGUI labelCost;
        public TextMeshProUGUI labelInfo; // Range & Target

        [Header("HUD Basic")]
        public GameObject basicRoot;
        public TextMeshProUGUI labelSkillEffect; // 总伤害

        [Header("HUD Description")]
        public TextMeshProUGUI labelDescription;

        [Header("Bottom Group")]
        public TextMeshProUGUI labelFlavor;
        public TextMeshProUGUI labelAvailability;

        [Header("Visual Settings")]
        public Vector3 hoverOffset = new Vector3(0, 80f, 0); // 在图标上方显示的偏移量

        // 资源配置 (需要与 SkillBarPopulator 保持一致)
        [Header("Tracery Assets")]
        public Sprite traceryPhysical;
        public Sprite traceryMagic;
        public Sprite traceryMixed;
        public Sprite traceryEnemy;

        [Header("Colors")]
        public Color enemyTint = new Color(1f, 0.85f, 0.85f, 1f);
        public Color enemyGlow = new Color(0.8f, 0.0f, 0.0f, 0f);

        // 友军颜色配置
        public Color phyTint = new Color32(0xE8, 0xE3, 0xF7, 0xFF);
        public Color magTint = new Color32(0xEE, 0xF3, 0xFF, 0xFF);
        public Color mixTint = new Color32(0xE7, 0xF2, 0xFF, 0xFF);

        public Color phyGlow = new Color32(0xD9, 0xD3, 0xF3, 0xFF);
        public Color magGlow = new Color32(0x73, 0xB6, 0xFF, 0xFF);
        public Color mixGlow = new Color32(0xE7, 0xF2, 0xFF, 0xFF);

        // Hex Colors for Text
        const string COL_AP = "#8372AB";
        const string COL_MP = "#6A96BF";

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

            // 1. Icon & Visuals (Gems, Tracery, Glow)
            UpdateVisuals(ability, isEnemy);

            // 2. Name
            labelName.text = ability.displayName;

            // 3. Cost
            UpdateCost(ability);

            // 4. Info (Range & Target)
            UpdateInfo(ability);

            // 5. Basic Effect (Total Damage)
            UpdateBasicEffect(ability, caster);

            // 6. Description
            // 如果 caster 为空 (比如在图鉴里查看)，GetDynamicDescription 需要能处理 null
            labelDescription.text = ability.GetDynamicDescription(caster);

            // 7. Bottom Group
            labelFlavor.text = $"<style=Italic>\"{ability.flavorText}\"</style>";
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
            // 简单的跟随逻辑，假设 Tooltip 和 SkillBar 在同一个 Canvas 空间
            transform.position = slotRect.position + hoverOffset;
        }

        void UpdateVisuals(Ability ability, bool isEnemy)
        {
            // Icon
            iconImage.sprite = ability.icon;

            string typeName = GetAbilityTypeName(ability).ToLower();
            bool isPhy = typeName.Contains("phys");
            bool isMag = typeName.Contains("magic") || typeName.Contains("magical");

            // Colors & Tracery
            if (isEnemy)
            {
                iconImage.color = enemyTint;
                iconGlow.color = enemyGlow;
                if (iconTracery) iconTracery.sprite = traceryEnemy ?? traceryMixed;
                ToggleGems(false, false, false, true);
            }
            else
            {
                if (isPhy)
                {
                    iconImage.color = phyTint;
                    iconGlow.color = phyGlow;
                    if (iconTracery) iconTracery.sprite = traceryPhysical;
                    ToggleGems(true, false, false, false);
                }
                else if (isMag)
                {
                    iconImage.color = magTint;
                    iconGlow.color = magGlow;
                    if (iconTracery) iconTracery.sprite = traceryMagic;
                    ToggleGems(false, true, false, false);
                }
                else
                {
                    iconImage.color = mixTint;
                    iconGlow.color = mixGlow;
                    if (iconTracery) iconTracery.sprite = traceryMixed;
                    ToggleGems(false, false, true, false);
                }
            }
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
            if (ability.apCost > 0)
                costStr += $"<color={COL_AP}>{ability.apCost}</color><sprite name=\"AP\"> ";
            if (ability.mpCost > 0)
                costStr += $"<color={COL_MP}>{ability.mpCost}</color><sprite name=\"MP\">";

            labelCost.text = costStr;
            labelCost.gameObject.SetActive(!string.IsNullOrEmpty(costStr));
        }

        void UpdateInfo(Ability ability)
        {
            // Range Icon
            string rangeIcon = ability.shape switch
            {
                TargetShape.Cone => "<sprite name=\"Cone\">",
                TargetShape.Disk => "<sprite name=\"Circle\">", // Assuming Disk = Circle
                TargetShape.Ring => "<sprite name=\"Circle\">", // Fallback
                TargetShape.Line => "<sprite name=\"Straight\">",
                TargetShape.Single => "<sprite name=\"SingleTarget\">",
                _ => "<sprite name=\"SingleTarget\">" // Default or MultipleTargets logic
            };

            // Target Icon
            string targetIcon = ability.targetType switch
            {
                AbilityTargetType.FriendlyUnit => "<sprite name=\"Ally\">",
                AbilityTargetType.EnemyUnit => "<sprite name=\"Enemy\">",
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
                // 遍历 Effect 计算总伤
                foreach (var effect in ability.effects)
                {
                    if (effect is DamageEffect dmg)
                    {
                        var stats = caster.Attributes.Core;
                        // 计算物理
                        float p = dmg.config.basePhysical + dmg.config.physScaling.Evaluate(stats);
                        // 计算魔法
                        float m = dmg.config.baseMagical + dmg.config.magScaling.Evaluate(stats);

                        totalPhys += Mathf.RoundToInt(p);
                        totalMag += Mathf.RoundToInt(m);
                        hasDamage = true;
                    }
                }
            }
            else
            {
                // 如果没有施法者 (预览模式)，只显示基础伤害
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
            if (totalPhys > 0) txt += $"<sprite name=\"Physical\"> {totalPhys}";
            if (totalPhys > 0 && totalMag > 0) txt += " + ";
            if (totalMag > 0) txt += $"<sprite name=\"Magical\"> {totalMag}";

            labelSkillEffect.text = txt;
        }

        void UpdateAvailability(Ability ability, BattleUnit caster)
        {
            // 简单的可用性逻辑，可根据 BattleUnit 的状态扩展
            // 这里假设 Ability 有 CooldownTurns 字段，但在实际运行时可能需要一个 RuntimeAbility 类来记录当前 CD
            // 由于 Ability 是 SO，我们这里仅显示静态规则，或者如果你有 Runtime 状态，请传入

            // 这里暂时显示静态规则
            if (ability.cooldownTurns > 0)
                labelAvailability.text = $"Cooldown: {ability.cooldownTurns} Turns";
            else
                labelAvailability.text = "Always Available";

            // 如果你能获取到动态 CD (比如从 BattleUnit 的冷却管理器中)，可以在这里覆盖
        }

        // Helper copied from Populator
        string GetAbilityTypeName(Ability ability)
        {
            if (ability == null) return "Mixed";
            // 简化版反射，或者直接访问 public field
            if (ability.abilityType == AbilityType.Physical) return "Physical";
            if (ability.abilityType == AbilityType.Magical) return "Magical";
            return "Mixed";
        }
    }
}