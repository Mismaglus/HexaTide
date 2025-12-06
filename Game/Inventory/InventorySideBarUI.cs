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

        [Header("System Refs")]
        public AbilityTargetingSystem targetingSystem;

        // 缓存
        private UnitInventory _currentInventory;
        private List<InventorySlotUI> _activeSlots = new List<InventorySlotUI>();

        // 状态标记
        private bool _isBound = false;

        void Start()
        {
            // 自动查找 TargetingSystem (用于点击物品后的逻辑)
            if (targetingSystem == null)
                targetingSystem = FindFirstObjectByType<AbilityTargetingSystem>(FindObjectsInactive.Include);

            // 尝试寻找并绑定玩家背包
            FindAndBindPlayerInventory();
        }

        void Update()
        {
            // 兜底逻辑：如果开局没找到玩家（可能玩家是异步生成的），每帧尝试寻找，直到找到为止
            if (!_isBound)
            {
                FindAndBindPlayerInventory();
            }
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
            
            // 筛选出属于玩家阵营的单位 (isPlayer == true)
            // 如果有多个玩家单位，默认取第一个 (单人游戏通常只有一个主角)
            var playerUnit = allUnits.FirstOrDefault(u => u.isPlayer);

            if (playerUnit != null)
            {
                _currentInventory = playerUnit.GetComponent<UnitInventory>();
                if (_currentInventory != null)
                {
                    // 订阅背包变化事件
                    _currentInventory.OnInventoryChanged += Refresh;
                    
                    // 立即刷新一次 UI
                    Refresh();
                    
                    _isBound = true;
                    Debug.Log($"[InventorySideBar] Successfully bound to player: {playerUnit.name}");
                }
                else
                {
                    Debug.LogWarning($"[InventorySideBar] Found player unit {playerUnit.name} but it has no UnitInventory component!");
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

            if (_currentInventory == null) return;

            // 2. 遍历背包数据
            var slotsData = _currentInventory.Slots;
            for (int i = 0; i < slotsData.Count; i++)
            {
                var data = slotsData[i];
                // 跳过空格子
                if (data.IsEmpty) continue;

                // ⭐ 类型过滤：只显示符合当前面板配置的物品 (Consumable 或 Relic)
                if (data.item.type != targetType) continue;

                // 3. 生成格子
                var go = Instantiate(slotPrefab, contentRoot);
                var ui = go.GetComponent<InventorySlotUI>();

                if (ui != null)
                {
                    // 传递数据 (传递真实的背包索引 i，以便点击时能找到对应物品)
                    ui.Setup(data.item, data.count, i, OnSlotClicked);
                    _activeSlots.Add(ui);
                }
            }
        }

        // 点击响应
        void OnSlotClicked(int inventoryIndex)
        {
            if (_currentInventory == null) return;
            if (inventoryIndex < 0 || inventoryIndex >= _currentInventory.Slots.Count) return;

            var slot = _currentInventory.Slots[inventoryIndex];
            var item = slot.item;

            Debug.Log($"[Inventory] Clicked {item.name} (Type: {item.type})");

            // 1. 消耗品逻辑：进入瞄准
            if (item.type == ItemType.Consumable && item is ConsumableItem consumable)
            {
                if (consumable.abilityToCast != null && targetingSystem != null)
                {
                    // 进入瞄准模式 (此时还没有扣除物品，等待技能释放回调)
                    targetingSystem.EnterTargetingMode(consumable.abilityToCast);
                }
            }
            // 2. 遗物逻辑：暂时仅打印 (后续可加 Tooltip)
            else if (item.type == ItemType.Relic)
            {
                Debug.Log("Relic selected. (Passive effect is always active)");
            }
        }
    }
}