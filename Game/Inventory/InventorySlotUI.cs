// Scripts/UI/Game/Inventory/InventorySlotUI.cs
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.EventSystems;
using Game.Inventory;

namespace Game.UI.Inventory
{
    // ⭐ 修改 1: 移除了 [RequireComponent(typeof(Animator))]
    public class InventorySlotUI : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        [Header("Components")]
        public Image iconImage;
        public TextMeshProUGUI countText;
        public Button button;

        [Header("Animator Settings")]
        [Tooltip("请将子物体 HighlightAndSelect 上的 Animator 拖到这里")]
        public Animator targetAnimator; // ⭐ 修改 2: 变为公开变量，允许手动指定

        [Tooltip("Animator 中用于控制强制高亮的 Bool 参数名")]
        public string activeBoolParam = "IsActive";

        private InventoryItem _item;
        private int _index;
        public int Index => _index;
        private System.Action<int> _onClickCallback;
        private RectTransform _rect;

        void Awake()
        {
            _rect = GetComponent<RectTransform>();

            // ⭐ 修改 3: 如果 Inspector 里没拖，尝试自动在子物体里找一个兜底
            if (targetAnimator == null)
            {
                targetAnimator = GetComponentInChildren<Animator>();
            }

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
            // ⭐ 修改 4: 安全检查
            if (targetAnimator != null)
            {
                targetAnimator.SetBool(activeBoolParam, isActive);
            }
        }

        // === Tooltip ===
        public void OnPointerEnter(PointerEventData eventData)
        {
            if (_item == null) return;
            // TooltipController.Instance.Show(_item, ...);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            // TooltipController.Instance.Hide();
        }
    }
}