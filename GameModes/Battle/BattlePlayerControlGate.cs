using UnityEngine;
using Game.Common;

namespace Game.Battle
{
    /// <summary>
    /// Keeps player controls disabled whenever the battle is in the enemy turn (or when the gate itself is disabled).
    /// Future systems can also call <see cref="PlayerControlService"/> to add additional blockers.
    /// </summary>
    [DisallowMultipleComponent]
    public class BattlePlayerControlGate : MonoBehaviour
    {
        [SerializeField] private BattleStateMachine battle;
        [SerializeField] private PlayerControlService controlService;

        readonly object _turnToken = new object();

        void Awake()
        {
            if (battle == null)
                battle = FindFirstObjectByType<BattleStateMachine>(FindObjectsInactive.Exclude);
            if (controlService == null)
                controlService = PlayerControlService.Instance ?? PlayerControlService.Ensure();

            if (battle != null)
                battle.OnTurnChanged += HandleTurnChanged;

            EnsureState();
        }

        void OnEnable()
        {
            EnsureState();
        }

        void OnDisable()
        {
            if (controlService != null)
                controlService.SetBlocked(_turnToken, false);
        }

        void OnDestroy()
        {
            if (battle != null)
                battle.OnTurnChanged -= HandleTurnChanged;

            if (controlService != null)
                controlService.SetBlocked(_turnToken, false);
        }

        void HandleTurnChanged(TurnSide side)
        {
            EnsureState(side);
        }

        void EnsureState()
        {
            var side = battle != null ? battle.CurrentTurn : TurnSide.Player;
            EnsureState(side);
        }

        void EnsureState(TurnSide side)
        {
            if (controlService == null)
                controlService = PlayerControlService.Instance ?? PlayerControlService.Ensure();
            if (controlService == null) return;

            bool shouldBlock = side != TurnSide.Player || !isActiveAndEnabled;
            controlService.SetBlocked(_turnToken, shouldBlock);
        }
    }
}
