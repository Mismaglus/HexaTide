// Scripts/UI/Game/Inventory/InventorySlotUI.cs
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.EventSystems;
using Game.Inventory; // 引用我们之前写的 InventoryItem

namespace Game.UI.Inventory
{
    public class InventorySlotUI : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        [Header("Components")]
        public Image iconImage;
        public TextMeshProUGUI countText;
        public Button button;

        [Header("Visuals")]
        public GameObject selectionHighlight; // 选中框 (可选)

        private InventoryItem _item;
        private int _index; // 在背包中的索引
        private System.Action<int> _onClickCallback;

        // 用于 Tooltip
        private RectTransform _rect;

        void Awake()
        {
            _rect = GetComponent<RectTransform>();
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

            if (_item != null)
            {
                // 设置图标
                iconImage.sprite = _item.icon;
                iconImage.enabled = true;
                iconImage.preserveAspect = true; // 保持图标比例

                // 设置数量 (如果 >1 则显示，否则隐藏)
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
                // 空格子处理 (如果你的设计允许空格子)
                iconImage.enabled = false;
                countText.gameObject.SetActive(false);
            }
        }

        void OnClicked()
        {
            _onClickCallback?.Invoke(_index);
        }

        // === Tooltip 支持 (鼠标悬停) ===
        public void OnPointerEnter(PointerEventData eventData)
        {
            if (_item == null) return;

            // 这里暂时打印日志，或者你可以复用 SkillTooltipController
            // 如果你有通用的 ItemTooltipController，在这里调用 Show
            // Debug.Log($"Hover Item: {_item.LocalizedName}");

            // TODO: 调用 TooltipController.ShowItem(_item, ...)
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            // TODO: 调用 TooltipController.Hide()
        }
    }
}