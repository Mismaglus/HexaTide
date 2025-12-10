// Scripts/UI/Game/Inventory/LootSlotUI.cs
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.EventSystems;
using Game.Inventory;
using Game.Units;

namespace Game.UI.Inventory
{
    /// <summary>
    /// specialized UI for items in the Reward/Loot window.
    /// Does not handle "Usage" logic, only "Display" and "Tooltip".
    /// </summary>
    public class LootSlotUI : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        [Header("Components")]
        public Image iconImage;
        public TextMeshProUGUI countText;
        public Image frameImage; // Optional background frame

        private InventoryItem _item;
        private SkillTooltipController _tooltipController;
        private RectTransform _rect;

        void Awake()
        {
            _rect = GetComponent<RectTransform>();
        }

        public void Setup(InventoryItem item, int count, SkillTooltipController tooltipCtrl)
        {
            _item = item;
            _tooltipController = tooltipCtrl;

            if (_item != null)
            {
                iconImage.sprite = _item.icon;
                iconImage.enabled = true;
                iconImage.preserveAspect = true; // Ensure 3D renders look right

                if (count > 1)
                {
                    countText.text = count.ToString();
                    countText.gameObject.SetActive(true);
                }
                else
                {
                    countText.gameObject.SetActive(false);
                }
            }
            else
            {
                // Safety clear
                iconImage.enabled = false;
                countText.gameObject.SetActive(false);
            }
        }

        // === Tooltip Logic ===
        public void OnPointerEnter(PointerEventData eventData)
        {
            if (_item == null || _tooltipController == null) return;

            // For rewards, we generally pass null as the 'holder' since the item isn't owned yet,
            // or we could pass the Player Unit to preview scaling.
            // We'll try to find the player if possible in the parent logic, but passing null is safe.
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