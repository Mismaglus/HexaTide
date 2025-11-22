using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Game.Battle
{
    public enum TurnSide
    {
        Player,
        Enemy
    }

    /// <summary>
    /// Coordinates high level battle turn flow and exposes turn state to UI.
    /// </summary>
    public class BattleStateMachine : MonoBehaviour
    {
        // ⭐ 单例模式，方便 BattleUnit 访问
        public static BattleStateMachine Instance { get; private set; }

        [Header("References")]
        [SerializeField] private BattleRules rules;
        [SerializeField] private BattleTurnController turnController;

        [Header("Flow")]
        [SerializeField] private bool autoStart = true;
        [SerializeField] private TurnSide startingSide = TurnSide.Player;

        public TurnSide CurrentTurn { get; private set; } = TurnSide.Player;
        public event System.Action<TurnSide> OnTurnChanged;

        // 胜负事件
        public event System.Action OnVictory;
        public event System.Action OnDefeat;

        readonly List<BattleUnit> _playerUnits = new();
        readonly List<BattleUnit> _enemyUnits = new();

        // Actor 列表用于回合循环
        readonly List<ITurnActor> _playerActors = new();
        readonly List<ITurnActor> _enemyActors = new();

        Coroutine _enemyTurnRoutine;
        bool _isBattleEnded = false;

        void Awake()
        {
            Instance = this; // 赋值单例

            if (rules == null)
                rules = GetComponentInParent<BattleRules>() ?? FindFirstObjectByType<BattleRules>(FindObjectsInactive.Exclude);
            if (turnController == null)
                turnController = GetComponentInParent<BattleTurnController>() ?? FindFirstObjectByType<BattleTurnController>(FindObjectsInactive.Exclude);

            CurrentTurn = startingSide;
            RebuildRosters();
        }

        void Start()
        {
            if (autoStart)
                BeginTurn(startingSide, notifyActors: true);
        }

        void OnDisable()
        {
            if (_enemyTurnRoutine != null)
            {
                StopCoroutine(_enemyTurnRoutine);
                _enemyTurnRoutine = null;
            }
        }

        public void RebuildRosters()
        {
            if (_isBattleEnded) return;

            _playerUnits.Clear();
            _enemyUnits.Clear();

            foreach (var unit in FindObjectsByType<BattleUnit>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            {
                if (unit == null) continue;
                if (unit.isPlayer) _playerUnits.Add(unit);
                else _enemyUnits.Add(unit);
            }

            _playerUnits.Sort((a, b) => a.GetInstanceID().CompareTo(b.GetInstanceID()));
            _enemyUnits.Sort((a, b) => a.GetInstanceID().CompareTo(b.GetInstanceID()));

            RefreshActors();
        }

        void RefreshActors()
        {
            _playerActors.Clear();
            _enemyActors.Clear();

            if (turnController != null && rules != null)
            {
                turnController.RefreshActorList();
                _playerActors.AddRange(turnController.EnumerateSide(TurnSide.Player));
                _enemyActors.AddRange(turnController.EnumerateSide(TurnSide.Enemy));
            }
            else
            {
                foreach (var mono in FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
                {
                    if (mono is not ITurnActor actor) continue;
                    if (IsPlayerActor(actor)) _playerActors.Add(actor);
                    else _enemyActors.Add(actor);
                }
            }
        }

        // ⭐⭐⭐ 新增：处理单位死亡 ⭐⭐⭐
        public void OnUnitDied(BattleUnit unit)
        {
            if (_isBattleEnded) return;

            // 1. 从单位列表中移除
            if (unit.isPlayer) _playerUnits.Remove(unit);
            else _enemyUnits.Remove(unit);

            // 2. 从 Actor 列表中移除 (防止轮到死人行动)
            var actor = unit.GetComponent<ITurnActor>();
            if (actor != null)
            {
                if (unit.isPlayer) _playerActors.Remove(actor);
                else _enemyActors.Remove(actor);
            }

            // 3. 检查胜负
            CheckBattleOutcome();
        }

        // ⭐⭐⭐ 新增：胜负判定 ⭐⭐⭐
        void CheckBattleOutcome()
        {
            if (_playerUnits.Count == 0)
            {
                EndBattle(false);
            }
            else if (_enemyUnits.Count == 0)
            {
                EndBattle(true);
            }
        }

        void EndBattle(bool playerWon)
        {
            _isBattleEnded = true;
            if (_enemyTurnRoutine != null) StopCoroutine(_enemyTurnRoutine);

            if (playerWon)
            {
                Debug.Log("🏆 VICTORY! All enemies defeated.");
                OnVictory?.Invoke();
                // TODO: Show Victory UI
            }
            else
            {
                Debug.Log("☠️ DEFEAT! All allies fallen.");
                OnDefeat?.Invoke();
                // TODO: Show Game Over UI
            }
        }

        // ... (Roster Accessors) ...
        public IReadOnlyList<BattleUnit> PlayerUnits => _playerUnits;
        public IReadOnlyList<BattleUnit> EnemyUnits => _enemyUnits;

        public void StartBattle(TurnSide firstSide)
        {
            startingSide = firstSide;
            BeginTurn(firstSide, notifyActors: true);
        }

        public void EndTurnRequest()
        {
            if (_isBattleEnded) return;
            if (CurrentTurn != TurnSide.Player) return;
            if (_enemyTurnRoutine != null) return;
            _enemyTurnRoutine = StartCoroutine(RunEnemyTurnRoutine());
        }

        IEnumerator RunEnemyTurnRoutine()
        {
            BeginTurn(TurnSide.Enemy, notifyActors: true);

            // 拷贝一份列表防止在遍历时修改 (虽然 OnUnitDied 处理了，但安全起见)
            var actors = new List<ITurnActor>(_enemyActors);
            foreach (var actor in actors)
            {
                if (actor == null || (actor is MonoBehaviour mb && mb == null)) continue; // Skip dead
                yield return actor.TakeTurn();

                if (_isBattleEnded) yield break; // 战斗结束立即停止
            }

            BeginTurn(TurnSide.Player, notifyActors: true);
            _enemyTurnRoutine = null;
        }

        void BeginTurn(TurnSide side, bool notifyActors)
        {
            if (_isBattleEnded) return;

            // 每次回合开始前稍微清理一下空引用，以防万一
            Cleanup();
            CurrentTurn = side;

            foreach (var unit in GetUnitsFor(side))
                if (unit) unit.ResetTurnResources();

            if (notifyActors)
            {
                foreach (var actor in GetActorsFor(side))
                {
                    actor?.OnTurnStart();
                }
            }

            OnTurnChanged?.Invoke(CurrentTurn);
            Debug.Log($"⚡ Turn Start: {side}");
        }

        void Cleanup()
        {
            _playerUnits.RemoveAll(u => u == null);
            _enemyUnits.RemoveAll(u => u == null);
            _playerActors.RemoveAll(a => a == null || (a is MonoBehaviour mb && mb == null));
            _enemyActors.RemoveAll(a => a == null || (a is MonoBehaviour mb && mb == null));
        }

        IReadOnlyList<BattleUnit> GetUnitsFor(TurnSide side) => side == TurnSide.Player ? (IReadOnlyList<BattleUnit>)_playerUnits : _enemyUnits;
        IReadOnlyList<ITurnActor> GetActorsFor(TurnSide side) => side == TurnSide.Player ? (IReadOnlyList<ITurnActor>)_playerActors : _enemyActors;

        bool IsPlayerActor(ITurnActor actor)
        {
            if (rules != null) return rules.IsPlayer(actor);
            if (actor is Component component && component.TryGetComponent(out BattleUnit unit)) return unit.isPlayer;
            return false;
        }
    }
}