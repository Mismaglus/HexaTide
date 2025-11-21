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

            // ❌ 删除这行！不要在出生时自动回蓝/回AP
            // ResetTurnResources(); 

            // ✅ 替代方案：仅做必要的各种状态重置，但不加数值
            _mover?.ResetStride();

            // 如果你希望单位出生时 AP 自动补满（防止你在 Inspector 里填了 0），
            // 可以取消下面这行的注释。
            // 但为了“所见即所得”，通常建议你在 Inspector 里直接把 CurrentAP 填好。
            // CurAP = MaxAP; 
        }

        // 这个方法只应该由 TurnController 在“轮到该单位行动”时调用
        public void ResetTurnResources()
        {
            // 1. AP 回满 (AP 是回合制资源，每回合刷新)
            CurAP = MaxAP;

            // 2. MP 恢复 (MP 是累积资源，每回合增加)
            int regen = Attributes.Core.MPRecovery;
            if (regen > 0 && Attributes.Core.MP < Attributes.Core.MPMax)
            {
                Attributes.Core.MP = Mathf.Min(Attributes.Core.MP + regen, Attributes.Core.MPMax);
            }

            // 3. 重置移动
            _mover?.ResetStride();
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