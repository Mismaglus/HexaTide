// Scripts/UI/Game/Inventory/InventorySlotUI.cs
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.EventSystems;
using Game.Inventory;
using Game.Battle; // for BattleUnit

namespace Game.UI.Inventory
{
    public class InventorySlotUI : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        [Header("Components")]
        public Image iconImage;
        public TextMeshProUGUI countText;
        public Button button;

        [Header("Animator Settings")]
        [Tooltip("Please drag the Animator from child 'HighlightAndSelect'")]
        public Animator targetAnimator;
        [Tooltip("Animator bool param for highlighting")]
        public string activeBoolParam = "IsActive";

        private InventoryItem _item;
        private int _index;
        public int Index => _index;
        private System.Action<int> _onClickCallback;
        private RectTransform _rect;

        // Tooltip Refs
        private SkillTooltipController _tooltipController;
        private BattleUnit _owner; // Who holds this item?

        void Awake()
        {
            _rect = GetComponent<RectTransform>();

            if (targetAnimator == null)
                targetAnimator = GetComponentInChildren<Animator>();

            if (button)
                button.onClick.AddListener(OnClicked);
        }

        // ⭐ Updated Setup: Receives tooltip controller and owner
        public void Setup(InventoryItem item, int count, int index, System.Action<int> onClick, SkillTooltipController tooltipCtrl, BattleUnit owner)
        {
            _item = item;
            _index = index;
            _onClickCallback = onClick;
            _tooltipController = tooltipCtrl;
            _owner = owner;

            // Reset highlight
            SetHighlightState(false);

            if (_item != null)
            {
                if (iconImage)
                {
                    iconImage.sprite = _item.icon;
                    iconImage.enabled = true;
                    iconImage.preserveAspect = true;
                }

                if (countText)
                {
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

                if (button) button.interactable = true;
            }
            else
            {
                if (iconImage) iconImage.enabled = false;
                if (countText) countText.gameObject.SetActive(false);
                if (button) button.interactable = false;
            }
        }

        void OnClicked()
        {
            _onClickCallback?.Invoke(_index);
        }

        public void SetHighlightState(bool isActive)
        {
            if (targetAnimator != null)
            {
                targetAnimator.SetBool(activeBoolParam, isActive);
            }
        }

        // === Tooltip Logic ===
        public void OnPointerEnter(PointerEventData eventData)
        {
            if (_item == null) return;

            if (_tooltipController != null)
            {
                // Show item tooltip using the generic overload we added
                _tooltipController.Show(_item, _owner, _rect);
            }
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