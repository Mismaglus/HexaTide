// Scripts/GameModes/Battle/BattleStateMachine.cs
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Game.Inventory;
using Game.World;

namespace Game.Battle
{
    public enum TurnSide
    {
        Player,
        Enemy
    }

    public enum BattleState
    {
        Playing,
        Victory,
        Defeat
    }

    /// <summary>
    /// Coordinates high level battle turn flow, win conditions, and loot generation.
    /// </summary>
    public class BattleStateMachine : MonoBehaviour
    {
        public static BattleStateMachine Instance { get; private set; }

        [Header("References")]
        [SerializeField] private BattleRules rules;
        [SerializeField] private BattleTurnController turnController;

        [Header("Flow")]
        [SerializeField] private bool autoStart = true;
        [SerializeField] private TurnSide startingSide = TurnSide.Player;

        [Header("Rewards (Profiles)")]
        [Tooltip("DB for RewardProfileSO lookup via EncounterContext.rewardProfileId. Optional but recommended.")]
        [SerializeField] private RewardProfileDB rewardProfileDB;

        [Tooltip("Default reward profile used when context id is missing or not found. Optional.")]
        [SerializeField] private RewardProfileSO defaultRewardProfile;

        [Header("Rewards (Fallback)")]
        [Tooltip("Default loot table used if no profile is resolved and BattleContext.ActiveLootTable is null.")]
        [SerializeField] private LootTableSO defaultLootTable;

        // Public State for UI Polling
        public BattleState State { get; private set; } = BattleState.Playing;

        public TurnSide CurrentTurn { get; private set; } = TurnSide.Player;
        public event System.Action<TurnSide> OnTurnChanged;

        // Win/Loss Events
        public event System.Action OnVictory;
        public event System.Action OnDefeat;

        // Rewards Storage
        public BattleRewardResult Rewards { get; private set; }

        readonly List<BattleUnit> _playerUnits = new();
        readonly List<BattleUnit> _enemyUnits = new();

        readonly List<ITurnActor> _playerActors = new();
        readonly List<ITurnActor> _enemyActors = new();

        Coroutine _enemyTurnRoutine;

        void Awake()
        {
            Instance = this;
            State = BattleState.Playing;

            if (rules == null)
                rules = GetComponentInParent<BattleRules>() ?? FindFirstObjectByType<BattleRules>(FindObjectsInactive.Exclude);
            if (turnController == null)
                turnController = GetComponentInParent<BattleTurnController>() ?? FindFirstObjectByType<BattleTurnController>(FindObjectsInactive.Exclude);

            CurrentTurn = startingSide;
            RebuildRosters();
        }

        void Start()
        {
            // Debug log: what reward source will likely be used
            if (BattleContext.ActiveLootTable != null)
            {
                Debug.Log($"[BattleSM] Context LootTable Override Active: {BattleContext.ActiveLootTable.name}");
            }
            else if (BattleContext.ActiveRewardProfile != null)
            {
                Debug.Log($"[BattleSM] Context RewardProfile Override Active: {BattleContext.ActiveRewardProfile.name}");
            }
            else
            {
                var ctx = BattleContext.EncounterContext;
                if (ctx.HasValue && !string.IsNullOrEmpty(ctx.Value.rewardProfileId))
                {
                    Debug.Log($"[BattleSM] EncounterContext rewardProfileId: {ctx.Value.rewardProfileId}");
                }
                else if (defaultRewardProfile != null)
                {
                    Debug.Log($"[BattleSM] Default RewardProfile Active: {defaultRewardProfile.name}");
                }
                else if (defaultLootTable != null)
                {
                    Debug.Log($"[BattleSM] Default LootTable Active: {defaultLootTable.name}");
                }
            }

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

        // === DEBUG TOOLS (Right-click component in Inspector) ===
        [ContextMenu("DEBUG: Force Victory")]
        public void DebugForceVictory()
        {
            if (State != BattleState.Playing) return;
            Debug.Log("[BattleSM] Debug Force Victory triggered.");
            EndBattle(true);
        }

        [ContextMenu("DEBUG: Force Defeat")]
        public void DebugForceDefeat()
        {
            if (State != BattleState.Playing) return;
            Debug.Log("[BattleSM] Debug Force Defeat triggered.");
            EndBattle(false);
        }
        // ========================================================

        public void RebuildRosters()
        {
            if (State != BattleState.Playing) return;

            _playerUnits.Clear();
            _enemyUnits.Clear();

            foreach (var unit in FindObjectsByType<BattleUnit>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            {
                if (unit == null) continue;
                if (unit.isPlayer) _playerUnits.Add(unit);
                else _enemyUnits.Add(unit);
            }

            // Sort ensures deterministic order
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
                // Fallback scan
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
            if (State != BattleState.Playing) return;

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
            // Defeat: No real players left (ignoring summons)
            bool hasRealPlayerCharacter = _playerUnits.Any(u => u != null && !u.isSummon);

            if (!hasRealPlayerCharacter)
            {
                EndBattle(false);
                return;
            }

            // Victory: No enemies left
            if (_enemyUnits.Count == 0)
            {
                EndBattle(true);
                return;
            }
        }

        void EndBattle(bool playerWon)
        {
            // 1. Set State immediately
            State = playerWon ? BattleState.Victory : BattleState.Defeat;

            // 2. Stop turns
            if (_enemyTurnRoutine != null) StopCoroutine(_enemyTurnRoutine);

            if (playerWon)
            {
                Debug.Log("[BattleSM] Victory.");
                CalculateRewards();
                OnVictory?.Invoke();
            }
            else
            {
                Debug.Log("[BattleSM] Defeat.");
                OnDefeat?.Invoke();
            }
        }

        private void CalculateRewards()
        {
            // Priority:
            // 1) BattleContext.ActiveLootTable (debug override)
            // 2) BattleContext.ActiveRewardProfile (debug override)
            // 3) EncounterContext.rewardProfileId -> RewardProfileDB
            // 4) defaultRewardProfile
            // 5) defaultLootTable
            // 6) none

            // 1) Debug LootTable override
            LootTableSO tableOverride = BattleContext.ActiveLootTable;
            if (tableOverride != null)
            {
                Rewards = tableOverride.GenerateRewards();
                Debug.Log($"[BattleSM] Rewards via ActiveLootTable: {Rewards.gold}g, {Rewards.experience}xp, {Rewards.items.Count} items.");
                return;
            }

            // 2) Debug RewardProfile override
            RewardProfileSO profileOverride = BattleContext.ActiveRewardProfile;
            if (profileOverride != null)
            {
                Rewards = profileOverride.GenerateRewards();
                Debug.Log($"[BattleSM] Rewards via ActiveRewardProfile: {profileOverride.name} -> {Rewards.gold}g, {Rewards.experience}xp, {Rewards.items.Count} items.");
                return;
            }

            // 3) EncounterContext profile id
            RewardProfileSO profileToUse = null;

            var ctx = BattleContext.EncounterContext;
            if (ctx.HasValue && !string.IsNullOrEmpty(ctx.Value.rewardProfileId))
            {
                if (rewardProfileDB != null)
                {
                    profileToUse = rewardProfileDB.GetProfile(ctx.Value.rewardProfileId);
                    if (profileToUse == null)
                    {
                        Debug.LogWarning($"[BattleSM] rewardProfileId '{ctx.Value.rewardProfileId}' not found in RewardProfileDB. Falling back.");
                    }
                }
                else
                {
                    Debug.LogWarning("[BattleSM] RewardProfileDB is null but EncounterContext.rewardProfileId is set. Falling back.");
                }
            }

            // 4) Default profile
            if (profileToUse == null)
                profileToUse = defaultRewardProfile;

            if (profileToUse != null)
            {
                Rewards = profileToUse.GenerateRewards();
                Debug.Log($"[BattleSM] Rewards via RewardProfile: {profileToUse.name} -> {Rewards.gold}g, {Rewards.experience}xp, {Rewards.items.Count} items.");
                return;
            }

            // 5) Fallback LootTable
            LootTableSO tableToUse = defaultLootTable;
            if (tableToUse != null)
            {
                Rewards = tableToUse.GenerateRewards();
                Debug.Log($"[BattleSM] Rewards via DefaultLootTable: {Rewards.gold}g, {Rewards.experience}xp, {Rewards.items.Count} items.");
            }
            else
            {
                Rewards = new BattleRewardResult();
                Debug.LogWarning("[BattleSM] No reward source configured. No rewards generated.");
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
            if (State != BattleState.Playing) return;
            if (CurrentTurn != TurnSide.Player) return;
            if (_enemyTurnRoutine != null) return;

            // End Player Phase
            EndCurrentTurn(TurnSide.Player);

            // Start Enemy Phase
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

                if (State != BattleState.Playing) yield break;
            }

            EndCurrentTurn(TurnSide.Enemy);

            // Back to Player
            BeginTurn(TurnSide.Player, notifyActors: true);
            _enemyTurnRoutine = null;
        }

        void BeginTurn(TurnSide side, bool notifyActors)
        {
            if (State != BattleState.Playing) return;

            Cleanup();
            CurrentTurn = side;

            // Trigger OnTurnStart (e.g. DoT effects)
            var unitsSnapshot = GetUnitsFor(side).ToArray();
            foreach (var unit in unitsSnapshot)
            {
                if (unit) unit.OnTurnStart();
            }

            if (notifyActors)
            {
                var actorsSnapshot = GetActorsFor(side).ToArray();
                foreach (var actor in actorsSnapshot)
                {
                    actor?.OnTurnStart();
                }
            }

            OnTurnChanged?.Invoke(CurrentTurn);
            Debug.Log($"[BattleSM] Turn Start: {side}");
        }

        void EndCurrentTurn(TurnSide side)
        {
            var unitsSnapshot = GetUnitsFor(side).ToArray();
            foreach (var u in unitsSnapshot)
            {
                if (u != null) u.OnTurnEnd();
            }
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
