using UnityEngine;
using System.Collections.Generic; // ⚡ 需要引用 List
using Game.Units;
using Game.Core;
using Game.Battle.Abilities;      // ⚡ 引用 Ability

namespace Game.Battle
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Unit))]
    [RequireComponent(typeof(UnitMover))]
    // ⚡ 强制要求有属性组件，这样就不会出现“没血条”的情况了
    [RequireComponent(typeof(UnitAttributes))]
    public class BattleUnit : MonoBehaviour
    {
        [Header("Battle Identity")]
        [SerializeField] private FactionMembership _faction;

        [Header("Action Points (AP)")]
        [SerializeField, Min(0)] private int maxAP = 2;
        public int MaxAP => maxAP;
        public int CurAP { get; private set; }

        // === 3. 新增：技能列表 ===
        [Header("Skills")]
        public List<Ability> abilities = new List<Ability>();

        // === 快捷访问属性 (可选，方便其他脚本调用) ===
        public UnitAttributes Attributes => _attributes ? _attributes : (_attributes = GetComponent<UnitAttributes>());
        private UnitAttributes _attributes;

        [SerializeField] private bool _isPlayer = true;
        public bool isPlayer
        {
            get => _faction ? (_faction.side == Side.Player) : _isPlayer;
            set
            {
                if (_faction) _faction.side = value ? Side.Player : Side.Enemy;
                _isPlayer = value;
            }
        }
        public bool IsPlayerControlled => _faction ? _faction.IsPlayerControlled : isPlayer;

        UnitMover _mover;

        void Awake()
        {
            _mover = GetComponent<UnitMover>();
            _attributes = GetComponent<UnitAttributes>(); // 缓存引用
            ResetTurnResources();
        }

        public void ResetTurnResources()
        {
            CurAP = Mathf.Max(0, MaxAP);
            _mover?.ResetStride();
        }

        public bool TrySpendAP(int cost = 1)
        {
            if (cost <= 0) return true;
            if (CurAP < cost) return false;
            CurAP -= cost;
            return true;
        }

        // ⚡ 新增：消耗 MP 的辅助方法 (连接到 UnitAttributes)
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
            CurAP = Mathf.Clamp(CurAP + amount, 0, MaxAP);
        }

        public void SetMaxAP(int value, bool refill = true)
        {
            maxAP = Mathf.Max(0, value);
            if (refill) CurAP = MaxAP;
        }
    }
}