// Scripts/UI/Game/Helper/SkillTooltipController.cs
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Game.Battle;
using Game.Battle.Abilities;
using Game.Battle.Abilities.Effects;
using Game.Battle.Combat;
using Game.Units;
using Game.Common;
using Game.Localization;
using Game.Inventory; // Added for InventoryItem

namespace Game.UI
{
    public class SkillTooltipController : MonoBehaviour
    {
        // Prevent hiding on first awake if set active immediately
        private bool _skipHideOnAwake = false;

        [Header("Root References")]
        [SerializeField] private GameObject contentRoot;
        [SerializeField] private RectTransform mainRect;

        [Header("Background Section")]
        public Image backgroundTracery;

        [Header("Icon Section")]
        public Image iconImage;
        public Image iconGlow;
        public Image iconTracery;

        // 0:Phy, 1:Mag, 2:Mix, 3:Enemy
        public GameObject[] gemObjects;

        [Header("Name Section")]
        public TextMeshProUGUI labelName;
        public TextMeshProUGUI labelCost;
        public TextMeshProUGUI labelInfo;

        [Header("HUD Basic (Summary)")]
        public GameObject basicRoot;
        public TextMeshProUGUI labelSkillEffect;

        [Header("HUD Description")]
        public TextMeshProUGUI labelDescription;

        [Header("Bottom Group")]
        public TextMeshProUGUI labelFlavor;
        public TextMeshProUGUI labelAvailability;

        [Header("Visual Settings")]
        public Vector3 hoverOffset = new Vector3(0, 80f, 0);

        [Header("Tracery Assets")]
        public Sprite traceryPhysical;
        public Sprite traceryMagic;
        public Sprite traceryMixed;
        public Sprite traceryEnemy;

        [Header("Colors (RGB Only)")]
        public Color enemyTint = new Color(1f, 0.85f, 0.85f, 1f);

        public Color enemyGlow = new Color(0.8f, 0.0f, 0.0f);
        public Color phyGlow = new Color32(0xD9, 0xD3, 0xF3, 0xFF);
        public Color magGlow = new Color32(0x73, 0xB6, 0xFF, 0xFF);
        public Color mixGlow = new Color32(0xE7, 0xF2, 0xFF, 0xFF);

        public Color phyTint = new Color32(0xE8, 0xE3, 0xF7, 0xFF);
        public Color magTint = new Color32(0xEE, 0xF3, 0xFF, 0xFF);
        public Color mixTint = new Color32(0xE7, 0xF2, 0xFF, 0xFF);

        [Header("Glow Alpha Settings")]
        [Range(0f, 1f)] public float alphaPhysical = 0.10f;
        [Range(0f, 1f)] public float alphaMagic = 0.12f;
        [Range(0f, 1f)] public float alphaMixed = 0.11f;
        [Range(0f, 1f)] public float alphaEnemy = 0.0f;

        void Awake()
        {
            if (mainRect == null) mainRect = GetComponent<RectTransform>();
            if (!_skipHideOnAwake) Hide();
        }

        // =========================================================
        // ENTRY POINT 1: For Abilities (Existing)
        // =========================================================
        public void Show(Ability ability, BattleUnit caster, bool isEnemy, RectTransform slotRect)
        {
            if (ability == null) return;

            ActivateAndPosition(slotRect);

            // Visuals
            UpdateVisuals(ability, isEnemy);
            labelName.text = ability.LocalizedName;

            // Content
            UpdateCost(ability);
            UpdateInfo(ability);
            UpdateBasicEffect(ability, caster);
            labelDescription.text = ability.GetDynamicDescription(caster);
            labelFlavor.text = $"<i>\"{ability.LocalizedFlavor}\"</i>";
            UpdateAvailability(ability, caster);

            ForceLayoutRebuild();
        }

        // =========================================================
        // ENTRY POINT 2: For Inventory Items (New)
        // =========================================================
        public void Show(InventoryItem item, BattleUnit holder, RectTransform slotRect)
        {
            if (item == null) return;

            ActivateAndPosition(slotRect);

            // 1. Determine if it wraps an ability (for cost/type display)
            Ability wrappedAbility = null;
            if (item is ConsumableItem consumable)
            {
                wrappedAbility = consumable.abilityToCast;
            }

            // 2. Visuals (Treat Items generally as "Mixed" or generic unless they are explicit enemies)
            // For items, we use the item icon, but we can reuse the "Mixed" style for the frame
            iconImage.sprite = item.icon;
            ApplyThemeColor(AbilityType.Mixed, false); // Items default to Mixed/Neutral theme

            // 3. Name
            labelName.text = item.LocalizedName;

            // 4. Cost (Only if it's a consumable with an ability)
            if (wrappedAbility != null)
            {
                UpdateCost(wrappedAbility);
                UpdateInfo(wrappedAbility);
                UpdateBasicEffect(wrappedAbility, holder);
            }
            else
            {
                // Relics or Materials
                labelCost.gameObject.SetActive(false);
                labelInfo.text = GetItemTypeIcon(item.type); // Optional: show a generic bag icon
                basicRoot.SetActive(false);
            }

            // 5. Description
            // If the item has a specific description override, it uses that.
            // ConsumableItem.GetDynamicDescription falls back to ability description if generic.
            labelDescription.text = item.GetDynamicDescription(holder);

            // 6. Flavor (Items usually don't have separate flavor fields in this system yet, reuse desc or hide)
            labelFlavor.text = "";

            // 7. Availability / Item Type Label
            labelAvailability.text = GetItemTypeLabel(item.type);

            ForceLayoutRebuild();
        }

        public void Hide()
        {
            gameObject.SetActive(false);
        }

        // --- Internal Helpers ---

        void ActivateAndPosition(RectTransform slotRect)
        {
            _skipHideOnAwake = true;
            gameObject.SetActive(true);
            _skipHideOnAwake = false;

            if (slotRect != null)
                transform.position = slotRect.position + hoverOffset;
        }

        void ForceLayoutRebuild()
        {
            if (mainRect != null)
            {
                Canvas.ForceUpdateCanvases();
                LayoutRebuilder.ForceRebuildLayoutImmediate(mainRect);
            }
        }

        // Logic split from UpdateVisuals to be reusable
        void ApplyThemeColor(AbilityType type, bool isEnemy)
        {
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
                if (type == AbilityType.Physical)
                {
                    iconImage.color = phyTint;
                    iconGlow.color = GetColorWithAlpha(phyGlow, alphaPhysical);
                    if (iconTracery) iconTracery.sprite = traceryPhysical;
                    if (backgroundTracery) backgroundTracery.sprite = traceryPhysical;
                    ToggleGems(true, false, false, false);
                }
                else if (type == AbilityType.Magical)
                {
                    iconImage.color = magTint;
                    iconGlow.color = GetColorWithAlpha(magGlow, alphaMagic);
                    if (iconTracery) iconTracery.sprite = traceryMagic;
                    if (backgroundTracery) backgroundTracery.sprite = traceryMagic;
                    ToggleGems(false, true, false, false);
                }
                else // Mixed / Item
                {
                    iconImage.color = mixTint;
                    iconGlow.color = GetColorWithAlpha(mixGlow, alphaMixed);
                    if (iconTracery) iconTracery.sprite = traceryMixed;
                    if (backgroundTracery) backgroundTracery.sprite = traceryMixed;
                    ToggleGems(false, false, true, false);
                }
            }
        }

        void UpdateVisuals(Ability ability, bool isEnemy)
        {
            iconImage.sprite = ability.icon;
            ApplyThemeColor(ability.abilityType, isEnemy);
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
            if (ability.apCost > 0)
                costStr += $"{TextIcons.FormatAP(ability.apCost)} ";
            if (ability.mpCost > 0)
                costStr += $"{TextIcons.FormatMP(ability.mpCost)}";

            labelCost.text = costStr;
            labelCost.gameObject.SetActive(!string.IsNullOrEmpty(costStr));
        }

        void UpdateInfo(Ability ability)
        {
            string rangeIcon = ability.shape switch
            {
                TargetShape.Cone => "<sprite name=\"Cone\">",
                TargetShape.Disk => "<sprite name=\"Circle\">",
                TargetShape.Ring => "<sprite name=\"Circle\">",
                TargetShape.Line => "<sprite name=\"Straight\">",
                TargetShape.Single => "<sprite name=\"SingleTarget\">",
                _ => "<sprite name=\"SingleTarget\">"
            };

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

            if (totalPhys > 0) txt += $"{TextIcons.SPR_PHYS} {totalPhys}";
            if (totalPhys > 0 && totalMag > 0) txt += " + ";
            if (totalMag > 0) txt += $"{TextIcons.SPR_MAG} {totalMag}";

            labelSkillEffect.text = txt;
        }

        void UpdateAvailability(Ability ability, BattleUnit caster)
        {
            if (ability.cooldownTurns > 0)
            {
                string cdLabel = LocalizationManager.Get("UI_COOLDOWN");
                string turnLabel = LocalizationManager.Get("UI_TURNS");
                labelAvailability.text = $"{cdLabel}: {ability.cooldownTurns} {turnLabel}";
            }
            else
            {
                labelAvailability.text = LocalizationManager.Get("UI_ALWAYS_READY");
            }
        }

        string GetItemTypeLabel(ItemType type)
        {
            switch (type)
            {
                case ItemType.Consumable: return LocalizationManager.Get("TYPE_CONSUMABLE") != "TYPE_CONSUMABLE" ? LocalizationManager.Get("TYPE_CONSUMABLE") : "Consumable";
                case ItemType.Relic: return LocalizationManager.Get("TYPE_RELIC") != "TYPE_RELIC" ? LocalizationManager.Get("TYPE_RELIC") : "Relic";
                case ItemType.Material: return "Material";
                default: return "Item";
            }
        }

        string GetItemTypeIcon(ItemType type)
        {
            // Just a placeholder icon return, usually empty string if no specific icon needed in info slot
            return "";
        }
    }
}