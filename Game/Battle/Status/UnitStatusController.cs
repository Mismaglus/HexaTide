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

        public event System.Action OnStatusChanged;

        // ⭐ 核心改动：查询是否有疾跑状态
        // 现在通过类型判断 (Type Checking) 来识别
        public bool HasSprintState
        {
            get
            {
                for (int i = 0; i < activeStatuses.Count; i++)
                {
                    // 只要有一个状态是 SprintStatusDefinition (或其子类)
                    if (activeStatuses[i].Definition is SprintStatusDefinition)
                        return true;
                }
                return false;
            }
        }

        void Awake() => _unit = GetComponent<BattleUnit>();

        public void ApplyStatus(StatusDefinition def, BattleUnit source, int stacks = 1)
        {
            if (def == null || _unit.Attributes.Core.HP <= 0) return;
            if (stacks <= 0) return;

            var existing = activeStatuses.Find(s => s.Definition == def);
            if (existing != null)
            {
                existing.AddStack(def.defaultDuration, stacks);
                def.OnStackAdded(existing, stacks);
                Debug.Log($"[Status] Refreshed {def.statusID} on {_unit.name}. Stacks: {existing.Stacks}");
            }
            else
            {
                var newStatus = new RuntimeStatus(def, source, stacks);
                activeStatuses.Add(newStatus);
                Debug.Log($"[Status] Applied {def.statusID} on {_unit.name}. Stacks: {stacks}");
            }

            OnStatusChanged?.Invoke();
        }

        public void RemoveStatus(StatusDefinition def)
        {
            var target = activeStatuses.Find(s => s.Definition == def);
            if (target != null)
            {
                activeStatuses.Remove(target);
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
                Debug.Log($"[Status] Expired/Depleted: {s.Definition.statusID}");
            }
        }
    }
}