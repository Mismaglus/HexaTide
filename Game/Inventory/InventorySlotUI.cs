// Scripts/UI/Game/Inventory/InventorySlotUI.cs
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.EventSystems;
using Game.Inventory;

namespace Game.UI.Inventory
{
    [RequireComponent(typeof(Animator))] // 确保有 Animator
    public class InventorySlotUI : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        [Header("Components")]
        public Image iconImage;
        public TextMeshProUGUI countText;
        public Button button;

        // [Header("Visuals")]
        // public GameObject selectionHighlight; // ❌ 已移除：不再需要这个

        [Header("Animator Settings")]
        [Tooltip("Animator 中用于控制强制高亮的 Bool 参数名")]
        public string activeBoolParam = "IsActive";

        private InventoryItem _item;
        private int _index;
        public int Index => _index;
        private System.Action<int> _onClickCallback;
        private Animator _animator;
        private RectTransform _rect;

        void Awake()
        {
            _rect = GetComponent<RectTransform>();
            _animator = GetComponent<Animator>(); // ⭐ 获取 Animator

            if (button)
            {
                button.onClick.AddListener(OnClicked);
            }
        }

        public void Setup(InventoryItem item, int count, int index, System.Action<int> onClick)
        {
            _item = item;
            _index = index;
            _onClickCallback = onClick;

            // 重置高亮状态
            SetHighlightState(false);

            if (_item != null)
            {
                iconImage.sprite = _item.icon;
                iconImage.enabled = true;
                iconImage.preserveAspect = true;

                if (count > 1)
                {
                    countText.text = count.ToString();
                    countText.gameObject.SetActive(true);
                }
                else
                {
                    countText.gameObject.SetActive(false);
                }

                if (button) button.interactable = true;
            }
            else
            {
                iconImage.enabled = false;
                countText.gameObject.SetActive(false);
                if (button) button.interactable = false;
            }
        }

        void OnClicked()
        {
            _onClickCallback?.Invoke(_index);
        }

        // ⭐ 新增：外部控制高亮的方法
        public void SetHighlightState(bool isActive)
        {
            if (_animator != null)
            {
                _animator.SetBool(activeBoolParam, isActive);
            }
        }

        // === Tooltip ===
        public void OnPointerEnter(PointerEventData eventData)
        {
            if (_item == null) return;
            // 调用你的 TooltipController (如果有的话)
            // TooltipController.Instance.Show(_item, ...);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            // TooltipController.Instance.Hide();
        }
    }
}