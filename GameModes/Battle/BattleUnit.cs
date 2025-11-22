using UnityEngine;
using System.Collections.Generic;
using Game.Units;
using Game.Core;
using Game.Battle.Abilities;
using Game.Grid; // 需要引用 Grid 系统来移除占位

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

        // 代理属性
        public int MaxAP => Attributes.Core.MaxAP;
        public int CurAP
        {
            get => Attributes.Core.CurrentAP;
            private set => Attributes.Core.CurrentAP = Mathf.Clamp(value, 0, MaxAP);
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

        public void ResetTurnResources()
        {
            // 1. AP 回满
            CurAP = MaxAP;

            // 2. MP 恢复
            int regen = Attributes.Core.MPRecovery;
            if (regen > 0 && Attributes.Core.MP < Attributes.Core.MPMax)
            {
                Attributes.Core.MP = Mathf.Min(Attributes.Core.MP + regen, Attributes.Core.MPMax);
            }

            // 3. 重置移动 (直接操作 Attributes)
            Attributes.Core.CurrentStride = Attributes.Core.Stride;
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
        }

        // ⭐⭐⭐ 新增：受伤逻辑 ⭐⭐⭐
        public void TakeDamage(int amount)
        {
            if (Attributes.Core.HP <= 0) return; // 已经死了

            // 扣血 (防止负数)
            Attributes.Core.HP = Mathf.Max(0, Attributes.Core.HP - amount);

            Debug.Log($"{name} took {amount} damage. HP: {Attributes.Core.HP}/{Attributes.Core.HPMax}");

            if (Attributes.Core.HP > 0)
            {
                // 活着：播放受击动画
                if (_hitReaction) _hitReaction.Play();
            }
            else
            {
                // 死了：进入死亡流程
                Die();
            }
        }

        // ⭐⭐⭐ 新增：死亡逻辑 ⭐⭐⭐
        private void Die()
        {
            Debug.Log($"💀 {name} has DIED!");

            // 1. 播放死亡动画
            if (_animator)
            {
                _animator.SetTrigger("Die");
                // 如果你有死亡状态机，可能需要 setBool("IsDead", true)
            }

            // 2. 清理网格占位 (非常重要！否则尸体会变成空气墙挡路)
            // 尝试找到全局的 GridOccupancy
            var occupancy = FindFirstObjectByType<GridOccupancy>();
            if (occupancy)
            {
                occupancy.Unregister(UnitRef);
            }

            // 3. 从选中系统中移除
            var selection = FindFirstObjectByType<SelectionManager>();
            if (selection && selection.SelectedUnit == UnitRef)
            {
                // 如果死的是当前选中的单位，取消选中
                // 这里 SelectionManager 可能没有公开 Deselect，但我们可以让它选 null
                // 更好的做法是在 SelectionManager 里加个 OnUnitDied 处理，或者直接 Destroy 会自动触发空检查
            }

            // 4. 通知战斗状态机 (处理胜负)
            if (BattleStateMachine.Instance != null)
            {
                BattleStateMachine.Instance.OnUnitDied(this);
            }

            // 5. 销毁物体 (延迟 2秒 让死亡动画播完)
            Destroy(gameObject, 2.0f);
        }
    }
}