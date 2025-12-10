// Scripts/UI/Game/Inventory/LootSlotUI.cs
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Game.Inventory;

namespace Game.UI.Inventory
{
    /// <summary>
    /// Represents a single loot item in the Victory screen.
    /// Clicking it triggers the description update on the main panel.
    /// </summary>
    public class LootSlotUI : MonoBehaviour
    {
        [Header("Components")]
        public Image iconImage;
        public TextMeshProUGUI nameText;
        public TextMeshProUGUI typeText;
        public Button button; // Ensure this is assigned in Prefab

        private InventoryItem _item;
        private System.Action<InventoryItem> _onSelectedCallback;

        void Awake()
        {
            if (button == null) button = GetComponent<Button>();
            if (button != null) button.onClick.AddListener(OnClicked);
        }

        public void Setup(InventoryItem item, System.Action<InventoryItem> onSelected)
        {
            _item = item;
            _onSelectedCallback = onSelected;

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
                    nameText.text = !string.IsNullOrEmpty(_item.LocalizedName) ? _item.LocalizedName : _item.name;
                }

                // 3. Type
                if (typeText)
                {
                    typeText.text = GetTypeString(_item.type);
                }

                if (button) button.interactable = true;
            }
            else
            {
                // Empty State
                if (iconImage) iconImage.enabled = false;
                if (nameText) nameText.text = "";
                if (typeText) typeText.text = "";
                if (button) button.interactable = false;
            }
        }

        private void OnClicked()
        {
            _onSelectedCallback?.Invoke(_item);
        }

        private string GetTypeString(ItemType type)
        {
            // You can replace these with LocalizationManager calls later
            switch (type)
            {
                case ItemType.Consumable: return "Consumable";
                case ItemType.Relic: return "Relic";
                case ItemType.Material: return "Material";
                default: return "Item";
            }
        }
    }
}