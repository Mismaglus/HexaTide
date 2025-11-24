using UnityEngine;
using Game.Common;

namespace Game.Battle
{
    [DisallowMultipleComponent]
    public class BattlePlayerControlGate : MonoBehaviour
    {
        [SerializeField] private BattleStateMachine battle;
        [SerializeField] private PlayerControlService controlService;

        // 使用特定的 Token 对象来管理锁定，防止误解锁别人的 Token
        readonly object _turnToken = new object();
        bool _isLockedByMe = false;

        void Awake()
        {
            if (battle == null)
                battle = FindFirstObjectByType<BattleStateMachine>(FindObjectsInactive.Exclude);
            if (controlService == null)
                controlService = PlayerControlService.Instance ?? PlayerControlService.Ensure();
        }

        void OnEnable()
        {
            if (battle != null)
            {
                battle.OnTurnChanged -= HandleTurnChanged;
                battle.OnTurnChanged += HandleTurnChanged;
            }
            // 初始检查
            UpdateLockState();
        }

        void OnDisable()
        {
            if (battle != null) battle.OnTurnChanged -= HandleTurnChanged;
            Unlock(); // 禁用时强制解锁，防止死锁
        }

        void HandleTurnChanged(TurnSide side)
        {
            UpdateLockState();
        }

        void UpdateLockState()
        {
            if (battle == null || controlService == null) return;

            // 逻辑：不是玩家回合 -> 锁定
            bool shouldBlock = (battle.CurrentTurn != TurnSide.Player);

            if (shouldBlock) Lock();
            else Unlock();
        }

        void Lock()
        {
            if (!_isLockedByMe)
            {
                controlService.SetBlocked(_turnToken, true);
                _isLockedByMe = true;
                // Debug.Log("[ControlGate] Input LOCKED (Enemy Turn)");
            }
        }

        void Unlock()
        {
            if (_isLockedByMe)
            {
                controlService.SetBlocked(_turnToken, false);
                _isLockedByMe = false;
                // Debug.Log("[ControlGate] Input UNLOCKED (Player Turn)");
            }
        }
    }
}