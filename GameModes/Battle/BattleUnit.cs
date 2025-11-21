using UnityEngine;
using System.Collections.Generic;
using Game.Units;
using Game.Core;
using Game.Battle.Abilities;

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

        void Awake()
        {
            _unit = GetComponent<Unit>();
            _mover = GetComponent<UnitMover>();
            _attributes = GetComponent<UnitAttributes>();
            // 注意：不要在 Awake 里 Reset，让数据由 Inspector 初始化
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

            // _mover.ResetStride() 已被移除，不再需要调用
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
    }
}