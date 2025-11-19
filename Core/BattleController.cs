using UnityEngine;

public class BattleController : MonoBehaviour
{
    public Camera battleCamera;

    void Awake()
    {
        if (battleCamera == null)
        {
            battleCamera = Camera.main;
        }

        BattleRuntimeRefs.RegisterBattle(this, battleCamera);
    }

    void OnDestroy()
    {
        // 仅当这个 BattleController 是当前注册的那个时清理
        if (BattleRuntimeRefs.Instance != null &&
            BattleRuntimeRefs.Instance.battleController == this)
        {
            BattleRuntimeRefs.ClearBattle();
        }
    }

    public void EndTurn()
    {
        // 回合结束逻辑
    }

    public void TryUndoLastAction()
    {
        // Undo 逻辑
    }

    public void SelectNextAlly()
    {
        // 循环选人逻辑
    }
}
