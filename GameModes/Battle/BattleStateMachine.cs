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
        [Header("References")]
        [SerializeField] private BattleRules rules;
        [SerializeField] private BattleTurnController turnController;

        [Header("Flow")]
        [SerializeField] private bool autoStart = true;
        [SerializeField] private TurnSide startingSide = TurnSide.Player;

        public TurnSide CurrentTurn { get; private set; } = TurnSide.Player;
        public event System.Action<TurnSide> OnTurnChanged;

        readonly List<BattleUnit> _playerUnits = new();
        readonly List<BattleUnit> _enemyUnits = new();
        readonly List<ITurnActor> _playerActors = new();
        readonly List<ITurnActor> _enemyActors = new();

        Coroutine _enemyTurnRoutine;

        void Awake()
        {
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

        public IReadOnlyList<BattleUnit> PlayerUnits => _playerUnits;
        public IReadOnlyList<BattleUnit> EnemyUnits => _enemyUnits;

        public void StartBattle(TurnSide firstSide)
        {
            startingSide = firstSide;
            BeginTurn(firstSide, notifyActors: true);
        }

        public void EndTurnRequest()
        {
            if (CurrentTurn != TurnSide.Player) return;
            if (_enemyTurnRoutine != null) return;
            _enemyTurnRoutine = StartCoroutine(RunEnemyTurnRoutine());
        }

        IEnumerator RunEnemyTurnRoutine()
        {
            BeginTurn(TurnSide.Enemy, notifyActors: true);

            foreach (var actor in _enemyActors.ToList())
            {
                if (actor == null) continue;
                yield return actor.TakeTurn();
            }

            BeginTurn(TurnSide.Player, notifyActors: true);
            _enemyTurnRoutine = null;
        }

        void BeginTurn(TurnSide side, bool notifyActors)
        {
            RebuildRosters();
            Cleanup();
            CurrentTurn = side;

            foreach (var unit in GetUnitsFor(side))
                unit.ResetTurnResources();

            if (notifyActors)
            {
                foreach (var actor in GetActorsFor(side))
                {
                    actor?.OnTurnStart();
                }
            }

            OnTurnChanged?.Invoke(CurrentTurn);
        }

        void Cleanup()
        {
            _playerUnits.RemoveAll(u => u == null);
            _enemyUnits.RemoveAll(u => u == null);
            _playerActors.RemoveAll(a => a == null);
            _enemyActors.RemoveAll(a => a == null);
        }

        IReadOnlyList<BattleUnit> GetUnitsFor(TurnSide side) => side == TurnSide.Player ? (IReadOnlyList<BattleUnit>)_playerUnits : _enemyUnits;
        IReadOnlyList<ITurnActor> GetActorsFor(TurnSide side) => side == TurnSide.Player ? (IReadOnlyList<ITurnActor>)_playerActors : _enemyActors;

        bool IsPlayerActor(ITurnActor actor)
        {
            if (rules != null)
                return rules.IsPlayer(actor);

            if (actor is Component component && component.TryGetComponent(out BattleUnit unit))
                return unit.isPlayer;

            return false;
        }
    }
}
