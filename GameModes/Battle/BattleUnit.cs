using UnityEngine;
using Game.Units;
using Game.Core;

namespace Game.Battle
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Unit))]
    [RequireComponent(typeof(UnitMover))]
    public class BattleUnit : MonoBehaviour
    {
        [Header("Battle")]
        [Tooltip("Whether this unit is controlled by the player.")]
        // public bool isPlayer = true;
        [SerializeField] private FactionMembership _faction;

        [SerializeField, Min(0)] private int maxAP = 2;

        public int MaxAP => maxAP;
        public int CurAP { get; private set; }

        [SerializeField] private bool _isPlayer = true; // 旧数据迁移期保留
        public bool isPlayer
        {
            get => _faction ? (_faction.side == Side.Player) : _isPlayer;
            set
            {
                if (_faction) _faction.side = value ? Side.Player : Side.Enemy;
                _isPlayer = value; // 兼容旧存档/预制
            }
        }
        public bool IsPlayerControlled => _faction ? _faction.IsPlayerControlled : isPlayer;
        UnitMover _mover;

        void Awake()
        {
            _mover = GetComponent<UnitMover>();
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
