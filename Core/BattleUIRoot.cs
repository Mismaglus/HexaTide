using UnityEngine;
using Game.UI;
using Game.Battle;
using System.Linq; // 用于查找

public class BattleUIRoot : MonoBehaviour
{
    [Header("Child Panels")]
    public TurnControlUI turnControlUI;
    public UnitFrameUI unitFrameUI; // ⭐ 新增：把 UnitFrameUI 也拖进来

    void Awake()
    {
        BattleRuntimeRefs.RegisterUIRoot(this);
    }

    public void Initialize(BattleController battle)
    {
        if (battle == null) return;

        Debug.Log("[BattleUIRoot] Starting Initialization sequence...");

        // 1. 连接 TurnControlUI
        var stateMachine = battle.GetComponent<BattleStateMachine>();
        if (turnControlUI != null && stateMachine != null)
        {
            turnControlUI.Initialize(stateMachine);
        }

        // 2. 连接 UnitFrameUI (关键步骤)
        // 此时 Battle 场景已加载，我们可以放心地找 SelectionManager 了
        var selectionManager = FindFirstObjectByType<SelectionManager>();

        if (unitFrameUI != null)
        {
            if (selectionManager != null)
            {
                unitFrameUI.Initialize(selectionManager);
            }
            else
            {
                Debug.LogError("[BattleUIRoot] 居然找不到 SelectionManager！请检查 Battle 场景 System 物体。");
            }
        }
    }
}