using UnityEngine;
using System.Collections.Generic;
using Game.Units;
using Game.Core;
using Game.Battle.Abilities;
using Game.Grid;
using Game.Battle.Status; // 引用 Status 命名空间

namespace Game.Battle
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Unit))]
    [RequireComponent(typeof(UnitMover))]
    [RequireComponent(typeof(UnitAttributes))]
    [RequireComponent(typeof(UnitStatusController))]
    public class BattleUnit : MonoBehaviour
    {
        private Unit _unit;
        public Unit UnitRef => _unit ? _unit : (_unit = GetComponent<Unit>());

        public bool isPlayer => UnitRef.Faction != null && UnitRef.Faction.side == Side.Player;
        public bool IsPlayerControlled => UnitRef.IsPlayerControlled;

        private UnitAttributes _attributes;
        public UnitAttributes Attributes => _attributes ? _attributes : (_attributes = GetComponent<UnitAttributes>());

        private UnitStatusController _statusController;
        public UnitStatusController Status => _statusController ? _statusController : (_statusController = GetComponent<UnitStatusController>());

        // ⭐ 新增：视觉反馈组件引用
        private UnitVisualFeedback _visualFeedback;

        public event System.Action OnResourcesChanged;

        public int MaxAP => Attributes.Core.MaxAP;
        public int CurAP
        {
            get => Attributes.Core.CurrentAP;
            private set
            {
                int clamped = Mathf.Clamp(value, 0, MaxAP);
                if (Attributes.Core.CurrentAP != clamped)
                {
                    Attributes.Core.CurrentAP = clamped;
                    NotifyStateChange();
                }
            }
        }

        [Header("Skills")]
        public List<Ability> abilities = new List<Ability>();

        UnitMover _mover;
        Animator _animator;

        void Awake()
        {
            _unit = GetComponent<Unit>();
            _mover = GetComponent<UnitMover>();
            _attributes = GetComponent<UnitAttributes>();
            _animator = GetComponentInChildren<Animator>();
            _statusController = GetComponent<UnitStatusController>();

            // ⭐ 获取通用反馈组件
            _visualFeedback = GetComponent<UnitVisualFeedback>();
        }

        public void NotifyStateChange()
        {
            OnResourcesChanged?.Invoke();
        }

        // === 生命周期钩子 (由 BattleStateMachine 调用) ===

        public void OnTurnStart()
        {
            ResetTurnResources();
            if (Status) Status.OnTurnStart();
        }

        public void OnTurnEnd()
        {
            if (Status) Status.OnTurnEnd();
        }

        public void ResetTurnResources()
        {
            CurAP = MaxAP;
            int regen = Attributes.Core.MPRecovery;
            if (regen > 0 && Attributes.Core.MP < Attributes.Core.MPMax)
            {
                Attributes.Core.MP = Mathf.Min(Attributes.Core.MP + regen, Attributes.Core.MPMax);
            }
            Attributes.Core.CurrentStride = Attributes.Core.Stride;
            NotifyStateChange();
        }

        public bool TrySpendAP(int cost = 1)
        {
            if (cost <= 0) return true;
            if (CurAP < cost) return false;
            CurAP -= cost;
            return true;
        }

        public bool TrySpendMP(int cost)
        {
            if (cost <= 0) return true;
            if (Attributes.Core.MP < cost) return false;
            Attributes.Core.MP -= cost;
            NotifyStateChange();
            return true;
        }

        public void RefundAP(int amount)
        {
            if (amount <= 0) return;
            CurAP += amount;
        }

        public void SetMaxAP(int value, bool refill = true)
        {
            Attributes.Core.MaxAP = Mathf.Max(0, value);
            if (refill) CurAP = MaxAP;
            else NotifyStateChange();
        }

        // === 受伤逻辑 ===
        public void TakeDamage(int amount, BattleUnit attacker = null)
        {
            if (Attributes.Core.HP <= 0) return;

            // 1. 让状态系统修正伤害
            if (Status)
            {
                amount = Status.ApplyIncomingDamageModifiers(amount, attacker);
            }

            // 2. 扣血
            Attributes.Core.HP = Mathf.Max(0, Attributes.Core.HP - amount);
            NotifyStateChange();

            Debug.Log($"{name} took {amount} damage. HP: {Attributes.Core.HP}/{Attributes.Core.HPMax}");

            // 3. 反应或死亡
            if (Attributes.Core.HP > 0)
            {
                // ⭐ 触发通用受击反馈 (变红/震动 或 播放 GetHit)
                if (_visualFeedback) _visualFeedback.PlayHit();
            }
            else
            {
                Die();
            }
        }

        private void Die()
        {
            Debug.Log($"💀 {name} has DIED!");
            if (_animator) _animator.SetTrigger("Die");

            var occupancy = FindFirstObjectByType<GridOccupancy>();
            if (occupancy) occupancy.Unregister(UnitRef);

            if (BattleStateMachine.Instance != null)
            {
                BattleStateMachine.Instance.OnUnitDied(this);
            }

            Destroy(gameObject, 2.0f);
        }
    }
}