// Scripts/Game/Battle/Status/UnitStatusController.cs
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

        void Awake() => _unit = GetComponent<BattleUnit>();

        // --- Public API ---
        public void ApplyStatus(StatusDefinition def, BattleUnit source)
        {
            if (def == null || _unit.Attributes.Core.HP <= 0) return;

            var existing = activeStatuses.Find(s => s.Definition == def);
            if (existing != null)
            {
                existing.AddStack(def.defaultDuration);
                def.OnStackAdded(existing, 1);
                Debug.Log($"[Status] Refreshed {def.statusID} on {_unit.name} (Stacks: {existing.Stacks})");
            }
            else
            {
                var newStatus = new RuntimeStatus(def, source);
                activeStatuses.Add(newStatus);
                Debug.Log($"[Status] Applied {def.statusID} on {_unit.name}");
            }
            // TODO: Notify UI Update
        }

        // --- Event Handlers (由 BattleUnit 调用) ---

        public void OnTurnStart()
        {
            for (int i = activeStatuses.Count - 1; i >= 0; i--)
            {
                var s = activeStatuses[i];
                s.Definition.OnTurnStart(s, _unit);
                CheckExpiration(s);
            }
        }

        public void OnTurnEnd()
        {
            for (int i = activeStatuses.Count - 1; i >= 0; i--)
            {
                var s = activeStatuses[i];
                s.Definition.OnTurnEnd(s, _unit);
                CheckExpiration(s);
            }
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
            if (s.IsExpired || s.Stacks <= 0) // 层数归零也视为过期
            {
                activeStatuses.Remove(s);
                Debug.Log($"[Status] Expired: {s.Definition.statusID}");
            }
        }
    }
}