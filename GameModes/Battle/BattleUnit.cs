using UnityEngine;
using System.Collections.Generic;
using Game.Units;
using Game.Core;
using Game.Battle.Abilities;
using Game.Grid;

namespace Game.Battle
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Unit))]
    [RequireComponent(typeof(UnitMover))]
    [RequireComponent(typeof(UnitAttributes))]
    public class BattleUnit : MonoBehaviour
    {
        private Unit _unit;
        public Unit UnitRef => _unit ? _unit : (_unit = GetComponent<Unit>());

        public bool isPlayer => UnitRef.Faction != null && UnitRef.Faction.side == Side.Player;
        public bool IsPlayerControlled => UnitRef.IsPlayerControlled;

        private UnitAttributes _attributes;
        public UnitAttributes Attributes => _attributes ? _attributes : (_attributes = GetComponent<UnitAttributes>());

        // ⭐ 新增：资源变化事件，供 UI 和 SelectionManager 监听
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
        UnitHitReaction _hitReaction;
        Animator _animator;

        void Awake()
        {
            _unit = GetComponent<Unit>();
            _mover = GetComponent<UnitMover>();
            _attributes = GetComponent<UnitAttributes>();
            _hitReaction = GetComponent<UnitHitReaction>();
            _animator = GetComponentInChildren<Animator>();
        }

        // ⭐ 供外部（如 UnitMover）调用，手动触发刷新
        public void NotifyStateChange()
        {
            OnResourcesChanged?.Invoke();
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
            // Setter 自动触发 Notify
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
            // Setter 自动触发 Notify
        }

        public void SetMaxAP(int value, bool refill = true)
        {
            Attributes.Core.MaxAP = Mathf.Max(0, value);
            if (refill) CurAP = MaxAP;
            else NotifyStateChange();
        }

        public void TakeDamage(int amount)
        {
            if (Attributes.Core.HP <= 0) return;
            Attributes.Core.HP = Mathf.Max(0, Attributes.Core.HP - amount);
            NotifyStateChange();

            Debug.Log($"{name} took {amount} damage. HP: {Attributes.Core.HP}/{Attributes.Core.HPMax}");

            if (Attributes.Core.HP > 0)
            {
                if (_hitReaction) _hitReaction.Play();
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

            var selection = FindFirstObjectByType<SelectionManager>();
            if (selection && selection.SelectedUnit == UnitRef)
            {
                // SelectionManager 会处理空引用
            }

            if (BattleStateMachine.Instance != null)
            {
                BattleStateMachine.Instance.OnUnitDied(this);
            }
            Destroy(gameObject, 2.0f);
        }
    }
}