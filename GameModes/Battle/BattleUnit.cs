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
    // 推荐加上这个，保证 Status Controller 存在
    [RequireComponent(typeof(UnitStatusController))]
    public class BattleUnit : MonoBehaviour
    {
        private Unit _unit;
        public Unit UnitRef => _unit ? _unit : (_unit = GetComponent<Unit>());

        public bool isPlayer => UnitRef.Faction != null && UnitRef.Faction.side == Side.Player;
        public bool IsPlayerControlled => UnitRef.IsPlayerControlled;

        private UnitAttributes _attributes;
        public UnitAttributes Attributes => _attributes ? _attributes : (_attributes = GetComponent<UnitAttributes>());

        // === ⭐ 新增：状态控制器引用 ===
        private UnitStatusController _statusController;
        public UnitStatusController Status => _statusController ? _statusController : (_statusController = GetComponent<UnitStatusController>());

        // 资源变化事件
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
            _statusController = GetComponent<UnitStatusController>();
        }

        public void NotifyStateChange()
        {
            OnResourcesChanged?.Invoke();
        }

        // === ⭐ 生命周期钩子 (由 BattleStateMachine 调用) ===

        // 1. 回合开始：重置资源 + 触发 Status (如 星蚀/月痕 扣血)
        public void OnTurnStart()
        {
            ResetTurnResources();
            if (Status) Status.OnTurnStart();
        }

        // 2. 回合结束：触发 Status (如 夜烬 扣血/衰减)
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

        // === ⭐ 修改：受伤逻辑 (支持易伤/减伤) ===
        public void TakeDamage(int amount, BattleUnit attacker = null)
        {
            if (Attributes.Core.HP <= 0) return;

            // 1. 让状态系统修正伤害 (例如 Moon Scar 的易伤)
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

            // 通知状态机处理名单移除和胜负判定
            if (BattleStateMachine.Instance != null)
            {
                BattleStateMachine.Instance.OnUnitDied(this);
            }

            // 延迟销毁，留出播放死亡动画的时间
            Destroy(gameObject, 2.0f);
        }
    }
}