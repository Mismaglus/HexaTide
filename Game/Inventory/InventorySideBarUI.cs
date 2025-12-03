// Scripts/UI/Game/Inventory/InventorySideBarUI.cs
using System.Collections.Generic;
using UnityEngine;
using Game.Inventory; // 引用 Backend
using Game.Battle;    // 引用 SelectionManager
using Game.Units;     // 引用 Unit

namespace Game.UI.Inventory
{
    public class InventorySideBarUI : MonoBehaviour
    {
        [Header("Settings")]
        [Tooltip("这个侧边栏显示什么类型的物品？")]
        public ItemType targetType;
        // 建议：左边栏选 Consumable，右边栏选 Relic

        [Header("References")]
        public Transform contentRoot; // Scroll View/Viewport/Content 对象
        public InventorySlotUI slotPrefab; // 上面做的 Slot Prefab

        [Header("System Refs")]
        public SelectionManager selectionManager;

        // 缓存
        private UnitInventory _currentInventory;
        private List<InventorySlotUI> _activeSlots = new List<InventorySlotUI>();

        void Start()
        {
            // 自动查找 SelectionManager
            if (selectionManager == null)
                selectionManager = FindFirstObjectByType<SelectionManager>(FindObjectsInactive.Exclude);

            if (selectionManager != null)
            {
                selectionManager.OnSelectedUnitChanged += HandleUnitChanged;
                // 初始化当前选中
                HandleUnitChanged(selectionManager.SelectedUnit);
            }
        }

        void OnDestroy()
        {
            if (selectionManager != null)
                selectionManager.OnSelectedUnitChanged -= HandleUnitChanged;

            if (_currentInventory != null)
                _currentInventory.OnInventoryChanged -= Refresh;
        }

        void HandleUnitChanged(Unit unit)
        {
            // 1. 解绑旧背包
            if (_currentInventory != null)
            {
                _currentInventory.OnInventoryChanged -= Refresh;
                _currentInventory = null;
            }

            // 2. 绑定新背包
            if (unit != null)
            {
                _currentInventory = unit.GetComponent<UnitInventory>();
                if (_currentInventory != null)
                {
                    _currentInventory.OnInventoryChanged += Refresh;
                }
            }

            // 3. 刷新界面
            Refresh();
        }

        void Refresh()
        {
            // 清理旧格子
            foreach (Transform child in contentRoot)
            {
                Destroy(child.gameObject);
            }
            _activeSlots.Clear();

            if (_currentInventory == null) return;

            // 遍历背包数据
            var slotsData = _currentInventory.Slots;
            for (int i = 0; i < slotsData.Count; i++)
            {
                var data = slotsData[i];
                if (data.IsEmpty) continue;

                // ⭐ 核心过滤：只显示符合类型的物品
                // 注意：这里我们允许 "Material" 显示在消耗品栏，或者你需要专门的 Type
                // 简单的逻辑：如果 Target 是 Relic，只显示 Relic；如果是 Consumable，显示 Consumable
                if (data.item.type != targetType) continue;

                // 生成格子
                var go = Instantiate(slotPrefab, contentRoot);
                var ui = go.GetComponent<InventorySlotUI>();

                // 传递数据 (注意传递真实的 index，以便回调时知道是背包里的第几个)
                ui.Setup(data.item, data.count, i, OnSlotClicked);

                _activeSlots.Add(ui);
            }
        }

        void OnSlotClicked(int inventoryIndex)
        {
            if (_currentInventory == null) return;

            var slotData = _currentInventory.Slots[inventoryIndex];
            if (slotData.IsEmpty) return;

            var item = slotData.item;

            Debug.Log($"Clicked Item: {item.name} at index {inventoryIndex}");

            // === 交互逻辑分支 ===

            // 1. 如果是消耗品 -> 进入技能瞄准
            if (item is ConsumableItem consumable)
            {
                if (consumable.abilityToCast != null)
                {
                    // 找到瞄准系统
                    var targeting = FindFirstObjectByType<Game.Battle.AbilityTargetingSystem>();
                    if (targeting != null)
                    {
                        // 进入瞄准！
                        // TODO: 这里有一个待解决的问题：
                        // TargetingSystem 目前只接受 Ability。
                        // 当技能释放成功时，我们还需要从背包里扣除这个物品。
                        // 我们需要在 AbilityTargetingSystem 里加一个 "OnAbilityExecuted" 回调，
                        // 或者创建一个特殊的 "ItemAbilityAction" 来处理扣除。

                        // 临时方案：先能用再说，扣除逻辑稍后完善
                        targeting.EnterTargetingMode(consumable.abilityToCast);
                    }
                }
            }

            // 2. 如果是遗物 -> 也许显示详情或者允许卸下？
            // 目前右侧栏点击遗物暂时不做任何“使用”操作
        }
    }
}