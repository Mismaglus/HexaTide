// Scripts/UI/Game/Inventory/InventorySideBarUI.cs
using System.Collections.Generic;
using UnityEngine;
using Game.Inventory; // 引用 Backend
using Game.Battle;    // 引用 AbilityTargetingSystem
using Game.Units;     // 引用 Unit
using System.Linq;    // 用于查找

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
            // 尝试初始化系统引用
            ResolveTargetingSystem();

            // 尝试寻找并绑定玩家背包
            FindAndBindPlayerInventory();
        }

        void Update()
        {
            // 1. 兜底逻辑：如果开局没找到玩家（可能玩家是异步生成的），每帧尝试寻找
            if (!_isPlayerBound)
            {
                FindAndBindPlayerInventory();
            }

            // 2. 兜底逻辑：如果 TargetingSystem 还没找到（可能因为跨场景加载延迟），尝试寻找
            if (targetingSystem == null)
            {
                ResolveTargetingSystem();
            }

            // 3. 高亮取消逻辑：如果系统存在且不再瞄准，清除 UI 高亮
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

        // ⭐ 核心逻辑：找到场景里的玩家角色并绑定
        void FindAndBindPlayerInventory()
        {
            // 查找场景中所有 BattleUnit
            var allUnits = FindObjectsByType<BattleUnit>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);

            // 筛选出属于玩家阵营的单位
            var playerUnit = allUnits.FirstOrDefault(u => u.isPlayer);

            if (playerUnit != null)
            {
                _currentInventory = playerUnit.GetComponent<UnitInventory>();
                if (_currentInventory != null)
                {
                    // 先取消订阅防止重复，再订阅
                    _currentInventory.OnInventoryChanged -= Refresh;
                    _currentInventory.OnInventoryChanged += Refresh;

                    Refresh();

                    _isPlayerBound = true;
                    Debug.Log($"[InventorySideBar] Successfully bound to player: {playerUnit.name}");
                }
            }
        }

        // 刷新列表显示
        void Refresh()
        {
            // 1. 清空旧 UI
            foreach (Transform child in contentRoot)
            {
                Destroy(child.gameObject);
            }
            _activeSlots.Clear();
            _currentSelectedIndex = -1;

            if (_currentInventory == null) return;

            // 2. 遍历背包数据
            var slotsData = _currentInventory.Slots;
            for (int i = 0; i < slotsData.Count; i++)
            {
                var data = slotsData[i];
                // 跳过空格子
                if (data.IsEmpty) continue;

                // 类型过滤
                if (data.item.type != targetType) continue;

                // 3. 生成格子
                var go = Instantiate(slotPrefab, contentRoot);
                var ui = go.GetComponent<InventorySlotUI>();

                if (ui != null)
                {
                    // 传递数据
                    ui.Setup(data.item, data.count, i, OnSlotClicked);
                    _activeSlots.Add(ui);
                }
            }
        }

        // 点击响应
        void OnSlotClicked(int inventoryIndex)
        {
            // 防守：如果点击时系统还没找到，最后再找一次
            if (targetingSystem == null) ResolveTargetingSystem();

            if (_currentInventory == null) return;
            if (inventoryIndex < 0 || inventoryIndex >= _currentInventory.Slots.Count) return;

            var slot = _currentInventory.Slots[inventoryIndex];
            var item = slot.item;

            Debug.Log($"[Inventory] Clicked {item.name} (Type: {item.type})");

            // 1. 消耗品逻辑
            if (item.type == ItemType.Consumable && item is ConsumableItem consumable)
            {
                if (consumable.abilityToCast != null)
                {
                    if (targetingSystem != null)
                    {
                        // ⭐ 这里的调用需要 AbilityTargetingSystem 支持第二个参数 (SourceItem)
                        // 这就是报错 CS1501 的原因，请务必更新 AbilityTargetingSystem.cs
                        targetingSystem.EnterTargetingMode(consumable.abilityToCast, consumable);

                        // 设置 UI 高亮
                        UpdateHighlightVisuals(inventoryIndex);
                        _currentSelectedIndex = inventoryIndex;
                    }
                    else
                    {
                        Debug.LogError("[Inventory] AbilityTargetingSystem not found! Ensure the Battle scene is loaded.");
                    }
                }
            }
            // 2. 遗物逻辑
            else if (item.type == ItemType.Relic)
            {
                Debug.Log("Relic selected. (Passive effect is always active)");
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