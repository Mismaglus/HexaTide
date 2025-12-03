// Scripts/UI/Game/Inventory/InventorySideBarUI.cs
using System.Collections.Generic;
using UnityEngine;
using Game.Inventory;
using Game.Battle;
using Game.Units;

namespace Game.UI.Inventory
{
    public class InventorySideBarUI : MonoBehaviour
    {
        [Header("Settings")]
        public ItemType targetType;

        [Header("References")]
        public Transform contentRoot;
        public InventorySlotUI slotPrefab;

        [Header("System Refs")]
        public SelectionManager selectionManager;

        // ⭐ 新增：引用 TargetingSystem 以便监听取消事件
        public AbilityTargetingSystem targetingSystem;

        private UnitInventory _currentInventory;
        private List<InventorySlotUI> _activeSlots = new List<InventorySlotUI>();

        // ⭐ 记录当前高亮的索引，用于互斥
        private int _currentSelectedIndex = -1;

        void Start()
        {
            if (selectionManager == null)
                selectionManager = FindFirstObjectByType<SelectionManager>(FindObjectsInactive.Exclude);

            if (targetingSystem == null)
                targetingSystem = FindFirstObjectByType<AbilityTargetingSystem>(FindObjectsInactive.Include);

            if (selectionManager != null)
            {
                selectionManager.OnSelectedUnitChanged += HandleUnitChanged;
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

        void Update()
        {
            // ⭐ 轮询检查：如果 TargetingSystem 退出了瞄准模式，我们也应该取消 UI 高亮
            // (这是一个简单的防错机制，防止玩家右键取消瞄准后 UI 还亮着)
            if (targetingSystem != null && !targetingSystem.IsTargeting && _currentSelectedIndex != -1)
            {
                ClearSelection();
            }
        }

        void HandleUnitChanged(Unit unit)
        {
            if (_currentInventory != null)
            {
                _currentInventory.OnInventoryChanged -= Refresh;
                _currentInventory = null;
            }

            if (unit != null)
            {
                _currentInventory = unit.GetComponent<UnitInventory>();
                if (_currentInventory != null)
                {
                    _currentInventory.OnInventoryChanged += Refresh;
                }
            }

            Refresh();
        }

        void Refresh()
        {
            // 记录旧的选中状态以便尝试恢复 (可选，这里先简单重置)
            _currentSelectedIndex = -1;

            foreach (Transform child in contentRoot) Destroy(child.gameObject);
            _activeSlots.Clear();

            if (_currentInventory == null) return;

            var slotsData = _currentInventory.Slots;
            for (int i = 0; i < slotsData.Count; i++)
            {
                var data = slotsData[i];
                if (data.IsEmpty) continue;

                if (data.item.type != targetType) continue;

                var go = Instantiate(slotPrefab, contentRoot);
                var ui = go.GetComponent<InventorySlotUI>();

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

            // === 处理高亮互斥 ===
            // 1. 找到对应的 UI 实例 (因为 _activeSlots 的顺序和 inventoryIndex 不一定对应，需要查找)
            InventorySlotUI clickedUI = null;
            foreach (var slotUI in _activeSlots)
            {
                // 这里有个小问题：Setup 里的 closure 存了 index，但我们没法直接从外部读取 slotUI 对应的 index
                // 简单做法：Refresh 时把 inventoryIndex 存到 UI 组件里公开访问
                // 或者：为了简单，我们这里全遍历一遍 SetHighlight(false)，然后只把点击的 SetHighlight(true)
            }

            // ⭐ 既然 InventorySlotUI 没有公开 Index，我们改用更简单的“全关再开”策略
            // 这要求我们在 Setup 时最好把 inventoryIndex 存为 InventorySlotUI 的一个属性
            // 让我们假设 InventorySlotUI 有一个 public int Index { get; private set; }

            // (为了不修改 InventorySlotUI 太多，我们这里暂时略过精确查找 UI 的逻辑，
            // 直接在 OnSlotClicked 里做逻辑处理，高亮由 SlotUI 内部响应 SetHighlightState)

            if (item is ConsumableItem consumable && consumable.abilityToCast != null)
            {
                if (targetingSystem != null)
                {
                    targetingSystem.EnterTargetingMode(consumable.abilityToCast);

                    // 找到刚才点击的那个 UI 并高亮它
                    // 我们需要在 Refresh 时建立 map 或者简单的遍历
                    // 鉴于列表很短，遍历无妨
                    UpdateHighlightVisuals(inventoryIndex);
                    _currentSelectedIndex = inventoryIndex;
                }
            }
        }

        // ⭐ 辅助：更新所有格子的高亮状态
        void UpdateHighlightVisuals(int selectedInventoryIndex)
        {
            foreach (var ui in _activeSlots)
            {
                if (ui.Index == selectedInventoryIndex)
                {
                    ui.SetHighlightState(true);
                }
                else
                {
                    ui.SetHighlightState(false);
                }
            }
        }

        void ClearSelection()
        {
            _currentSelectedIndex = -1;
            // 遍历所有 UI 取消高亮
            foreach (var ui in _activeSlots)
            {
                ui.SetHighlightState(false);
            }
        }
    }
}