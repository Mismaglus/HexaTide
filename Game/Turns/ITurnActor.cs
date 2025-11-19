public interface ITurnActor
{
    // 回合开始：刷新资源（如 ResetStride）
    void OnTurnStart();
    // 执行该 Actor 的回合（玩家等待输入，敌人跑AI）
    System.Collections.IEnumerator TakeTurn();
    // 是否还能行动（例如 Stride>0）
    bool HasPendingAction { get; }
}
