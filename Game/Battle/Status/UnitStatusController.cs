using System.Collections.Generic;
using UnityEngine;
using Game.Battle;

namespace Game.Battle.Status
{
    [RequireComponent(typeof(BattleUnit))]
    public class UnitStatusController : MonoBehaviour
    {
        // 存储当前所有状态
        public List<RuntimeStatus> activeStatuses = new List<RuntimeStatus>();

        private BattleUnit _unit;

        // ⭐ UI 监听此事件：当状态列表发生任何变化（添加/移除/层数改变/回合结算）时触发
        public event System.Action OnStatusChanged;

        void Awake()
        {
            _unit = GetComponent<BattleUnit>();
        }

        // === Public API ===

        /// <summary>
        /// 施加状态。如果已存在，则尝试堆叠。
        /// </summary>
        /// <param name="def">状态定义</param>
        /// <param name="source">施加者</param>
        /// <param name="stacks">施加层数 (默认为1)</param>
        public void ApplyStatus(StatusDefinition def, BattleUnit source, int stacks = 1)
        {
            if (def == null || _unit.Attributes.Core.HP <= 0) return;
            if (stacks <= 0) return;

            var existing = activeStatuses.Find(s => s.Definition == def);
            if (existing != null)
            {
                // === A. 已有状态：执行堆叠逻辑 ===
                // 刷新持续时间 + 增加指定层数
                existing.AddStack(def.defaultDuration, stacks);

                // 调用定义里的钩子 (例如某些 Buff 叠层时触发特殊效果)
                def.OnStackAdded(existing, stacks);

                Debug.Log($"[Status] Refreshed {def.statusID} on {_unit.name}. Stacks: {existing.Stacks} (+{stacks})");
            }
            else
            {
                // === B. 新状态：创建并添加 ===
                // 使用指定初始层数初始化
                var newStatus = new RuntimeStatus(def, source, stacks);
                activeStatuses.Add(newStatus);

                Debug.Log($"[Status] Applied {def.statusID} on {_unit.name}. Stacks: {stacks}");
            }

            // ⭐ 通知 UI 刷新
            OnStatusChanged?.Invoke();
        }

        public void RemoveStatus(StatusDefinition def)
        {
            var target = activeStatuses.Find(s => s.Definition == def);
            if (target != null)
            {
                activeStatuses.Remove(target);
                // ⭐ 通知 UI 刷新
                OnStatusChanged?.Invoke();
            }
        }

        // === 生命周期钩子 (由 BattleUnit 调用) ===

        // 1. 回合开始：星蚀(Stellar Erosion) / 月痕(Lunar Scar) 扣血逻辑
        public void OnTurnStart()
        {
            // 倒序遍历，防止在循环中移除元素导致报错
            for (int i = activeStatuses.Count - 1; i >= 0; i--)
            {
                var s = activeStatuses[i];
                // 执行逻辑
                s.Definition.OnTurnStart(s, _unit);
                // 检查是否过期
                CheckExpiration(s);
            }
            // ⭐ 统一刷新 UI (因为 Duration/Stack 可能变了)
            OnStatusChanged?.Invoke();
        }

        // 2. 回合结束：夜烬(Night Cinders) 扣血/衰减逻辑，普通 Buff 扣时间
        public void OnTurnEnd()
        {
            for (int i = activeStatuses.Count - 1; i >= 0; i--)
            {
                var s = activeStatuses[i];
                // 执行逻辑
                s.Definition.OnTurnEnd(s, _unit);
                // 检查过期
                CheckExpiration(s);
            }
            // ⭐ 统一刷新 UI
            OnStatusChanged?.Invoke();
        }

        // 3. 受伤修正：月痕(Lunar Scar) 易伤逻辑
        public int ApplyIncomingDamageModifiers(int damage, BattleUnit attacker)
        {
            int final = damage;
            foreach (var s in activeStatuses)
            {
                final = s.Definition.ModifyIncomingDamage(s, final, attacker);
            }
            return final;
        }

        // === 内部辅助 ===

        void CheckExpiration(RuntimeStatus s)
        {
            // 如果过期 或者 层数归零 (夜烬衰减到0)
            if (s.IsExpired || s.Stacks <= 0)
            {
                activeStatuses.Remove(s);
                Debug.Log($"[Status] Expired: {s.Definition.statusID}");
                // 这里不需要单独 Invoke，因为外层循环结束后会统一 Invoke
            }
        }
    }
}