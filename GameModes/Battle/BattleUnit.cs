// Scripts/GameModes/Battle/BattleUnit.cs
using UnityEngine;
using System.Collections.Generic;
using Game.Units;
using Game.Core;
using Game.Battle.Abilities;
using Game.Grid;
using Game.Battle.Status;

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

        [Header("Classification")]
        [Tooltip("如果勾选，该单位视为召唤物。玩家的所有'非召唤物'角色死亡时判负，召唤物存活不计入。")]
        public bool isSummon = false; // ⭐ 新增：召唤物标记

        private UnitAttributes _attributes;
        public UnitAttributes Attributes => _attributes ? _attributes : (_attributes = GetComponent<UnitAttributes>());

        private UnitStatusController _statusController;
        public UnitStatusController Status => _statusController ? _statusController : (_statusController = GetComponent<UnitStatusController>());

        // 视觉反馈组件引用
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

            // 获取通用反馈组件
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
        public void TakeDamage(int amount, BattleUnit attacker = null, bool isCrit = false)
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

            Debug.Log($"{name} took {amount} damage{(isCrit ? " (Crit)" : "")}. HP: {Attributes.Core.HP}/{Attributes.Core.HPMax}");

            // 3. 反应或死亡
            if (Attributes.Core.HP > 0)
            {
                // 触发通用受击反馈，传递具体伤害和暴击信息
                if (_visualFeedback) _visualFeedback.PlayHit(amount, isCrit);
            }
            else
            {
                Die();
            }
        }

        // === 治疗逻辑 ===
        public void Heal(int amount)
        {
            if (Attributes.Core.HP <= 0) return; // 尸体通常无法治疗
            if (amount <= 0) return;

            int current = Attributes.Core.HP;
            int max = Attributes.Core.HPMax;

            // 计算实际治疗量 (处理过量)
            int actualHeal = amount;
            if (current + actualHeal > max)
            {
                actualHeal = max - current;
            }

            // 应用治疗
            Attributes.Core.HP += actualHeal;
            NotifyStateChange();

            if (actualHeal > 0)
            {
                Debug.Log($"<color=green>{name} healed for {actualHeal}. HP: {Attributes.Core.HP}/{max}</color>");
                // 触发治疗飘字
                if (_visualFeedback) _visualFeedback.PlayHeal(actualHeal);
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

            if (FogOfWarSystem.Instance != null)
            {
                FogOfWarSystem.Instance.OnUnitDied(this);
            }

            Destroy(gameObject, 2.0f);
        }
    }
}