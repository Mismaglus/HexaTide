// Scripts/GameModes/Battle/BattleStateMachine.cs
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Game.Inventory;

namespace Game.Battle
{
    public enum TurnSide
    {
        Player,
        Enemy
    }

    public class BattleStateMachine : MonoBehaviour
    {
        public static BattleStateMachine Instance { get; private set; }

        [Header("References")]
        [SerializeField] private BattleRules rules;
        [SerializeField] private BattleTurnController turnController;

        [Header("Flow")]
        [SerializeField] private bool autoStart = true;
        [SerializeField] private TurnSide startingSide = TurnSide.Player;

        [Header("Rewards (Fallback)")]
        [Tooltip("Default loot table used if BattleContext.ActiveLootTable is null.")]
        [SerializeField] private LootTableSO defaultLootTable;

        public TurnSide CurrentTurn { get; private set; } = TurnSide.Player;
        public event System.Action<TurnSide> OnTurnChanged;

        public event System.Action OnVictory;
        public event System.Action OnDefeat;

        // ⭐ Changed to BattleRewardResult
        public BattleRewardResult Rewards { get; private set; }

        readonly List<BattleUnit> _playerUnits = new();
        readonly List<BattleUnit> _enemyUnits = new();
        readonly List<ITurnActor> _playerActors = new();
        readonly List<ITurnActor> _enemyActors = new();

        Coroutine _enemyTurnRoutine;
        bool _isBattleEnded = false;

        void Awake()
        {
            Instance = this;
            if (rules == null) rules = GetComponentInParent<BattleRules>() ?? FindFirstObjectByType<BattleRules>(FindObjectsInactive.Exclude);
            if (turnController == null) turnController = GetComponentInParent<BattleTurnController>() ?? FindFirstObjectByType<BattleTurnController>(FindObjectsInactive.Exclude);

            CurrentTurn = startingSide;
            RebuildRosters();
        }

        void Start()
        {
            if (autoStart) BeginTurn(startingSide, notifyActors: true);
        }

        void OnDisable()
        {
            if (_enemyTurnRoutine != null) StopCoroutine(_enemyTurnRoutine);
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

        public void OnUnitDied(BattleUnit unit)
        {
            if (_isBattleEnded) return;

            if (unit.isPlayer) _playerUnits.Remove(unit);
            else _enemyUnits.Remove(unit);

            var actor = unit.GetComponent<ITurnActor>();
            if (actor != null)
            {
                if (unit.isPlayer) _playerActors.Remove(actor);
                else _enemyActors.Remove(actor);
            }

            CheckBattleOutcome();
        }

        void CheckBattleOutcome()
        {
            bool hasRealPlayerCharacter = _playerUnits.Any(u => u != null && !u.isSummon);
            if (!hasRealPlayerCharacter)
            {
                EndBattle(false);
                return;
            }

            if (_enemyUnits.Count == 0)
            {
                EndBattle(true);
                return;
            }
        }

        void EndBattle(bool playerWon)
        {
            _isBattleEnded = true;
            if (_enemyTurnRoutine != null) StopCoroutine(_enemyTurnRoutine);

            if (playerWon)
            {
                Debug.Log("🏆 VICTORY!");
                CalculateRewards();
                OnVictory?.Invoke();
            }
            else
            {
                Debug.Log("☠️ DEFEAT!");
                OnDefeat?.Invoke();
            }
        }

        private void CalculateRewards()
        {
            LootTableSO tableToUse = BattleContext.ActiveLootTable;
            if (tableToUse == null) tableToUse = defaultLootTable;

            if (tableToUse != null)
            {
                Rewards = tableToUse.GenerateRewards();
                Debug.Log($"[BattleStateMachine] Rewards Generated: {Rewards.gold} Gold, {Rewards.experience} Exp, {Rewards.items.Count} Items.");
            }
            else
            {
                Rewards = new BattleRewardResult(); // Empty
                Debug.LogWarning("[BattleStateMachine] No LootTable found. No rewards generated.");
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
            if (_isBattleEnded) return;
            if (CurrentTurn != TurnSide.Player) return;
            if (_enemyTurnRoutine != null) return;

            EndCurrentTurn(TurnSide.Player);
            _enemyTurnRoutine = StartCoroutine(RunEnemyTurnRoutine());
        }

        IEnumerator RunEnemyTurnRoutine()
        {
            BeginTurn(TurnSide.Enemy, notifyActors: true);
            var actors = new List<ITurnActor>(_enemyActors);
            foreach (var actor in actors)
            {
                if (actor == null || (actor is MonoBehaviour mb && mb == null)) continue;
                yield return actor.TakeTurn();
                if (_isBattleEnded) yield break;
            }
            EndCurrentTurn(TurnSide.Enemy);
            BeginTurn(TurnSide.Player, notifyActors: true);
            _enemyTurnRoutine = null;
        }

        void BeginTurn(TurnSide side, bool notifyActors)
        {
            if (_isBattleEnded) return;
            Cleanup();
            CurrentTurn = side;
            var unitsSnapshot = GetUnitsFor(side).ToArray();
            foreach (var unit in unitsSnapshot) if (unit) unit.OnTurnStart();
            if (notifyActors)
            {
                var actorsSnapshot = GetActorsFor(side).ToArray();
                foreach (var actor in actorsSnapshot) actor?.OnTurnStart();
            }
            OnTurnChanged?.Invoke(CurrentTurn);
            Debug.Log($"⚡ Turn Start: {side}");
        }

        void EndCurrentTurn(TurnSide side)
        {
            var unitsSnapshot = GetUnitsFor(side).ToArray();
            foreach (var u in unitsSnapshot) if (u != null) u.OnTurnEnd();
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