using UnityEngine;
using Game.UI;
using Game.Battle;
using System.Linq; // 用于查找

public class BattleUIRoot : MonoBehaviour
{
    [Header("Child Panels")]
    public TurnControlUI turnControlUI;
    public UnitFrameUI unitFrameUI; // ⭐ 新增：把 UnitFrameUI 也拖进来
    public SkillBarController skillBarController; // ⭐ 新增
    void Awake()
    {
        BattleRuntimeRefs.RegisterUIRoot(this);
    }

    // Assets/Scripts/Core/BattleUIRoot.cs
    public void Initialize(BattleController battle)
    {
        if (battle == null) return;
        Debug.Log("[BattleUIRoot] Starting Initialization sequence...");

        var stateMachine = battle.GetComponent<BattleStateMachine>();
        var selectionManager = FindFirstObjectByType<SelectionManager>();

        // 1. 连接 TurnControlUI (保持不变)
        if (turnControlUI != null && stateMachine != null)
            turnControlUI.Initialize(stateMachine);

        // 2. 连接 UnitFrameUI (保持不变)
        if (unitFrameUI != null && selectionManager != null)
            unitFrameUI.Initialize(selectionManager);

        if (skillBarController != null)
        {
            skillBarController.Initialize(battle);
        }
    }
}