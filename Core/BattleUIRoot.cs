using UnityEngine;
using Game.UI;
// using Game.Battle; // 如果这一行报错，说明 namespace 不对，可以先注释掉

public class BattleUIRoot : MonoBehaviour
{
    // ⭐ 确保这一行存在，保存后 Inspector 里应该出现这个槽位
    [Header("Child Panels")]
    public TurnControlUI turnControlUI;

    void Awake()
    {
        // 核心：把自己注册进全局引用，Bootstrap 才能找到我
        BattleRuntimeRefs.RegisterUIRoot(this);
        Debug.Log("[BattleUIRoot] Registered self to BattleRuntimeRefs.");
    }

    // 由 GameBootstrap 调用
    public void Initialize(BattleController battle)
    {
        if (battle == null)
        {
            Debug.LogError("[BattleUIRoot] Initialize received NULL BattleController!");
            return;
        }

        Debug.Log("[BattleUIRoot] Wiring up UI...");

        // 获取状态机
        // 注意：根据你的截图，StateMachine 和 Controller 在同一个 System 物体上
        // 如果它们在不同物体，请调整获取方式
        var stateMachine = battle.GetComponent<Game.Battle.BattleStateMachine>();

        if (turnControlUI != null && stateMachine != null)
        {
            turnControlUI.Initialize(stateMachine);
        }
        else
        {
            Debug.LogError($"[BattleUIRoot] Wiring failed! TurnUI={turnControlUI}, SM={stateMachine}");
        }
    }
}