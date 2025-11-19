// Script/Game/Battle/Controllers/PlayerTurnActor.cs
using System.Collections;
using System.Linq;
using UnityEngine;
using Game.Battle.Actions;
using Game.Battle.Abilities;

namespace Game.Battle
{
    /// <summary>
    /// Player-side ITurnActor:
    /// 1) 回合开始重置玩家单位资源；
    /// 2) 玩家回合期间，驱动可选的 ActionQueue；
    /// 3) 当 BattleStateMachine 切到 Enemy 或外部调用 RequestEnd() 时结束。
    /// </summary>
    [DisallowMultipleComponent]
    public class PlayerTurnActor : MonoBehaviour, ITurnActor
    {
        [Header("Optional")]
        public ActionQueue queue;       // 可选：UI 可把行动入队
        public AbilityRunner runner;    // 可选：施法执行器

        [Header("Discovery (optional)")]
        public SelectionManager selectionManager;    // 可选
        public BattleUnit[] controlled; // 可选；留空则自动发现

        private BattleStateMachine _battleSM; // 可选
        private bool _endRequested;

        void Awake()
        {
            if (selectionManager == null)
                selectionManager = FindFirstObjectByType<SelectionManager>(FindObjectsInactive.Exclude);

            if (_battleSM == null)
                _battleSM = FindFirstObjectByType<BattleStateMachine>(FindObjectsInactive.Exclude);

            if (_battleSM != null)
                _battleSM.OnTurnChanged += HandleTurnChanged; // BattleStateMachine 驱动的结束
            if (queue == null)
                queue = Object.FindFirstObjectByType<ActionQueue>(FindObjectsInactive.Exclude);
            if (runner == null)
                runner = Object.FindFirstObjectByType<AbilityRunner>(FindObjectsInactive.Exclude);
        }

        void OnDestroy()
        {
            if (_battleSM != null)
                _battleSM.OnTurnChanged -= HandleTurnChanged;
        }

        void HandleTurnChanged(TurnSide side)
        {
            // 一旦不再是玩家回合，标记结束
            if (side != TurnSide.Player)
                _endRequested = true;
        }

        public void OnTurnStart()
        {
            _endRequested = false;

            // 若未手工绑定，自动发现玩家阵营单位
            if (controlled == null || controlled.Length == 0)
            {
                controlled = Object.FindObjectsByType<BattleUnit>(FindObjectsInactive.Exclude, FindObjectsSortMode.None)
                    .Where(u => u.isPlayer)
                    .ToArray();
            }

            // 重置 AP / 步幅
            foreach (var u in controlled)
                u?.ResetTurnResources();
        }

        // BattleStateMachine 模式下：只要还在玩家回合且未被标记结束，就算有未完成行动
        public bool HasPendingAction
        {
            get
            {
                if (_battleSM != null)
                    return _battleSM.CurrentTurn == TurnSide.Player && !_endRequested;
                return !_endRequested;
            }
        }

        public IEnumerator TakeTurn()
        {
            // TurnSystem：等待 UI 调用 RequestEnd()；
            // BattleStateMachine：等待 OnTurnChanged 切走。
            while (HasPendingAction)
            {
                if (queue != null && !queue.IsEmpty)
                    yield return queue.RunAll();
                yield return null;
            }
        }

        /// <summary>
        /// TurnSystem-only 时由 UI 按钮调用。
        /// 若使用 BattleStateMachine + TurnHUD_UITK，请继续用 battle.EndTurnRequest()。
        /// </summary>
        public void RequestEnd() => _endRequested = true;
    }
}
