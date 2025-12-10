// Scripts/UI/Game/Inventory/LootSlotUI.cs
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.EventSystems;
using Game.Inventory;

namespace Game.UI.Inventory
{
    /// <summary>
    /// Specialized UI for displaying loot in the Victory screen.
    /// Shows Icon, Name, and Type.
    /// </summary>
    public class LootSlotUI : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        [Header("Components")]
        public Image iconImage;
        public TextMeshProUGUI nameText;
        public TextMeshProUGUI typeText;

        private InventoryItem _item;
        private SkillTooltipController _tooltipController;
        private RectTransform _rect;

        void Awake()
        {
            _rect = GetComponent<RectTransform>();
        }

        // Note: 'count' param is kept for compatibility with BattleOutcomeUI, 
        // but we ignore it visually as requested (assuming loot is displayed as 1 per line or singular).
        public void Setup(InventoryItem item, int count, SkillTooltipController tooltipCtrl)
        {
            _item = item;
            _tooltipController = tooltipCtrl;

            if (_item != null)
            {
                // 1. Icon
                if (iconImage)
                {
                    iconImage.sprite = _item.icon;
                    iconImage.enabled = true;
                    iconImage.preserveAspect = true;
                }

                // 2. Name
                if (nameText)
                {
                    // Fallback to .name if LocalizedName is empty, though LocalizedName is preferred
                    nameText.text = !string.IsNullOrEmpty(_item.LocalizedName) ? _item.LocalizedName : _item.name;
                }

                // 3. Type
                if (typeText)
                {
                    typeText.text = GetTypeString(_item.type);
                }
            }
            else
            {
                // Clear visual state if empty
                if (iconImage) iconImage.enabled = false;
                if (nameText) nameText.text = "";
                if (typeText) typeText.text = "";
            }
        }

        private string GetTypeString(ItemType type)
        {
            switch (type)
            {
                case ItemType.Consumable: return "Consumable";
                case ItemType.Relic: return "Relic";
                case ItemType.Material: return "Material";
                default: return "Item";
            }
        }

        // === Tooltip Logic ===
        public void OnPointerEnter(PointerEventData eventData)
        {
            if (_item == null || _tooltipController == null) return;
            
            // Show tooltip without specific holder stats context (passed as null)
            _tooltipController.Show(_item, null, _rect);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            if (_tooltipController != null)
            {
                _tooltipController.Hide();
            }
        }
    }
}