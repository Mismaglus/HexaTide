using System.Collections.Generic;
using UnityEngine;
using Game.Battle;

public class BattleTurnController : MonoBehaviour, ITurnOrderProvider
{
    [SerializeField] private TurnSystem turnSystem;
    [SerializeField] private BattleRules rules;
    [SerializeField] private GameObject units;

    private readonly List<ITurnActor> _actors = new();

    void Awake()
    {
        EnsureUnitsReference();
        RefreshActorList();
        if (turnSystem != null)
            turnSystem.Initialize(this);
    }

    public void RefreshActorList()
    {
        _actors.Clear();

        if (units == null)
        {
            EnsureUnitsReference();
            if (units == null)
                return;
        }

        var unitsTransform = units.transform;
        var seen = new HashSet<ITurnActor>();
        for (int i = 0; i < unitsTransform.childCount; i++)
        {
            var child = unitsTransform.GetChild(i);
            if (child == null) continue;

            ITurnActor actor = child.GetComponent<ITurnActor>();

            if (actor == null)
            {
                Debug.LogWarning($"BattleTurnController could not locate an ITurnActor under unit '{child.name}'.", child);
                continue;
            }

            if (seen.Add(actor))
                _actors.Add(actor);
        }
    }

    void EnsureUnitsReference()
    {
        if (units != null) return;

        var child = transform.Find("Units");
        if (child != null)
        {
            units = child.gameObject;
        }
        else
        {
            Debug.LogWarning($"BattleTurnController on {name} could not find a child GameObject named 'Units'.", this);
        }
    }

    public void StartBattleRound() => turnSystem?.StartRound();

    public System.Collections.Generic.IEnumerable<ITurnActor> BuildOrder()
    {
        foreach (var actor in EnumerateSide(TurnSide.Player))
            yield return actor;
        foreach (var actor in EnumerateSide(TurnSide.Enemy))
            yield return actor;
    }

    public System.Collections.Generic.IEnumerable<ITurnActor> EnumerateSide(TurnSide side)
    {
        if (rules == null)
            yield break;

        var predicate = side == TurnSide.Player
            ? new System.Func<ITurnActor, bool>(rules.IsPlayer)
            : new System.Func<ITurnActor, bool>(rules.IsEnemy);

        foreach (var actor in _actors)
        {
            if (actor == null) continue;
            if (predicate(actor))
                yield return actor;
        }
    }
}
