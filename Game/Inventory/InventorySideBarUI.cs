// Scripts/UI/Game/Inventory/InventorySideBarUI.cs
using System.Collections.Generic;
using UnityEngine;
using Game.Inventory;
using Game.Battle;
using Game.Units;
using System.Linq;

namespace Game.UI.Inventory
{
    public class InventorySideBarUI : MonoBehaviour
    {
        [Header("Configuration")]
        [Tooltip("这个面板显示哪种类型的物品？(左栏选 Consumable, 右栏选 Relic)")]
        public ItemType targetType;

        [Header("Scene References")]
        public Transform contentRoot;      // ScrollView 的 Content 节点
        public InventorySlotUI slotPrefab; // Slot Prefab

        [Header("System Refs (Auto-Found)")]
        public AbilityTargetingSystem targetingSystem;

        // 缓存
        private UnitInventory _currentInventory;
        private List<InventorySlotUI> _activeSlots = new List<InventorySlotUI>();

        // 状态标记
        private bool _isPlayerBound = false;
        private int _currentSelectedIndex = -1;

        void Start()
        {
            ResolveTargetingSystem();
            FindAndBindPlayerInventory();
        }

        void Update()
        {
            // 兜底逻辑：如果开局没找到玩家（异步生成）
            if (!_isPlayerBound) FindAndBindPlayerInventory();

            // 兜底逻辑：如果 System 没找到（场景加载延迟）
            if (targetingSystem == null) ResolveTargetingSystem();

            // 高亮取消逻辑：如果系统存在且不再瞄准，清除 UI 高亮
            if (targetingSystem != null && !targetingSystem.IsTargeting && _currentSelectedIndex != -1)
            {
                ClearSelection();
            }
        }

        void ResolveTargetingSystem()
        {
            if (targetingSystem != null) return;
            targetingSystem = FindFirstObjectByType<AbilityTargetingSystem>(FindObjectsInactive.Include);
        }

        void OnDestroy()
        {
            if (_currentInventory != null)
                _currentInventory.OnInventoryChanged -= Refresh;
        }

        void FindAndBindPlayerInventory()
        {
            var allUnits = FindObjectsByType<BattleUnit>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            var playerUnit = allUnits.FirstOrDefault(u => u.isPlayer);

            if (playerUnit != null)
            {
                _currentInventory = playerUnit.GetComponent<UnitInventory>();
                if (_currentInventory != null)
                {
                    _currentInventory.OnInventoryChanged -= Refresh;
                    _currentInventory.OnInventoryChanged += Refresh;
                    Refresh();
                    _isPlayerBound = true;
                    Debug.Log($"[InventorySideBar] Successfully bound to player: {playerUnit.name}");
                }
            }
        }

        void Refresh()
        {
            foreach (Transform child in contentRoot) Destroy(child.gameObject);
            _activeSlots.Clear();
            _currentSelectedIndex = -1;

            if (_currentInventory == null) return;

            var slotsData = _currentInventory.Slots;
            for (int i = 0; i < slotsData.Count; i++)
            {
                var data = slotsData[i];
                if (data.IsEmpty) continue;
                if (data.item.type != targetType) continue;

                var go = Instantiate(slotPrefab, contentRoot);
                var ui = go.GetComponent<InventorySlotUI>();

                if (ui != null)
                {
                    ui.Setup(data.item, data.count, i, OnSlotClicked);
                    _activeSlots.Add(ui);
                }
            }
        }

        void OnSlotClicked(int inventoryIndex)
        {
            if (targetingSystem == null) ResolveTargetingSystem();

            if (_currentInventory == null) return;
            if (inventoryIndex < 0 || inventoryIndex >= _currentInventory.Slots.Count) return;

            var slot = _currentInventory.Slots[inventoryIndex];
            var item = slot.item;

            Debug.Log($"[Inventory] Clicked {item.name} (Type: {item.type})");

            if (item.type == ItemType.Consumable && item is ConsumableItem consumable)
            {
                if (consumable.abilityToCast != null)
                {
                    if (targetingSystem != null)
                    {
                        // ⭐ 核心修复：获取背包的持有者 (Player Unit)
                        var owner = _currentInventory.GetComponent<BattleUnit>();

                        // ⭐ 传入 explicitCaster = owner，不再依赖 SelectionManager.SelectedUnit
                        targetingSystem.EnterTargetingMode(consumable.abilityToCast, consumable, owner);

                        UpdateHighlightVisuals(inventoryIndex);
                        _currentSelectedIndex = inventoryIndex;
                    }
                    else
                    {
                        Debug.LogError("[Inventory] AbilityTargetingSystem not found!");
                    }
                }
            }
            else if (item.type == ItemType.Relic)
            {
                Debug.Log("Relic selected (Passive).");
            }
        }

        void UpdateHighlightVisuals(int selectedInventoryIndex)
        {
            foreach (var ui in _activeSlots)
            {
                bool shouldHighlight = (ui.Index == selectedInventoryIndex);
                ui.SetHighlightState(shouldHighlight);
            }
        }

        void ClearSelection()
        {
            _currentSelectedIndex = -1;
            foreach (var ui in _activeSlots)
            {
                ui.SetHighlightState(false);
            }
        }
    }
}