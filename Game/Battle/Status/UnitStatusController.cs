using System.Collections.Generic;
using UnityEngine;
using Game.Battle;

namespace Game.Battle.Status
{
    [RequireComponent(typeof(BattleUnit))]
    public class UnitStatusController : MonoBehaviour
    {
        public List<RuntimeStatus> activeStatuses = new List<RuntimeStatus>();
        private BattleUnit _unit;

        // ⭐ 新增：UI 监听这个事件
        public event System.Action OnStatusChanged;

        void Awake() => _unit = GetComponent<BattleUnit>();

        public void ApplyStatus(StatusDefinition def, BattleUnit source)
        {
            if (def == null || _unit.Attributes.Core.HP <= 0) return;

            var existing = activeStatuses.Find(s => s.Definition == def);
            if (existing != null)
            {
                existing.AddStack(def.defaultDuration);
                def.OnStackAdded(existing, 1);
                Debug.Log($"[Status] Refreshed {def.statusID} on {_unit.name}");
            }
            else
            {
                var newStatus = new RuntimeStatus(def, source);
                activeStatuses.Add(newStatus);
                Debug.Log($"[Status] Applied {def.statusID} on {_unit.name}");
            }

            // ⭐ 通知 UI 更新
            OnStatusChanged?.Invoke();
        }

        public void RemoveStatus(StatusDefinition def)
        {
            var target = activeStatuses.Find(s => s.Definition == def);
            if (target != null)
            {
                activeStatuses.Remove(target);
                // ⭐ 通知 UI 更新
                OnStatusChanged?.Invoke();
            }
        }

        public void OnTurnStart()
        {
            for (int i = activeStatuses.Count - 1; i >= 0; i--)
            {
                var s = activeStatuses[i];
                s.Definition.OnTurnStart(s, _unit);
                CheckExpiration(s);
            }
            // ⭐ 回合开始可能会改变持续时间，统一刷新一次
            OnStatusChanged?.Invoke();
        }

        public void OnTurnEnd()
        {
            for (int i = activeStatuses.Count - 1; i >= 0; i--)
            {
                var s = activeStatuses[i];
                s.Definition.OnTurnEnd(s, _unit);
                CheckExpiration(s);
            }
            // ⭐ 回合结束刷新
            OnStatusChanged?.Invoke();
        }

        public int ApplyIncomingDamageModifiers(int damage, BattleUnit attacker)
        {
            int final = damage;
            foreach (var s in activeStatuses)
                final = s.Definition.ModifyIncomingDamage(s, final, attacker);
            return final;
        }

        void CheckExpiration(RuntimeStatus s)
        {
            if (s.IsExpired || s.Stacks <= 0)
            {
                activeStatuses.Remove(s);
                Debug.Log($"[Status] Expired: {s.Definition.statusID}");
                // 这里不需要单独 Invoke，因为外层循环结束后会统一 Invoke
            }
        }
    }
}