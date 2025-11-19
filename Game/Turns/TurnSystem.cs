using UnityEngine;

public interface ITurnOrderProvider
{
    // 返回本轮的行动序列（可按阵营分段或速度排序）
    System.Collections.Generic.IEnumerable<ITurnActor> BuildOrder();
}

public class TurnSystem : MonoBehaviour
{
    public enum Phase { Idle, Running }
    public Phase Current { get; private set; } = Phase.Idle;

    public System.Action OnRoundStarted;
    public System.Action OnRoundEnded;

    private ITurnOrderProvider _orderProvider;

    public void Initialize(ITurnOrderProvider provider)
    {
        _orderProvider = provider;
    }

    public void StartRound()
    {
        if (Current == Phase.Running) return;
        StartCoroutine(RunRound());
    }

    private System.Collections.IEnumerator RunRound()
    {
        Current = Phase.Running;
        OnRoundStarted?.Invoke();

        foreach (var actor in _orderProvider.BuildOrder())
        {
            actor.OnTurnStart();
            yield return actor.TakeTurn();
        }

        OnRoundEnded?.Invoke();
        Current = Phase.Idle;
    }
}
