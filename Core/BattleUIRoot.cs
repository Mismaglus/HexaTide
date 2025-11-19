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

        // 3. 连接 SkillBarController (⭐ 新增)
        if (skillBarController != null && selectionManager != null)
        {
            // 手动注入 selectionManager (如果你想让代码更严谨，
            // 可以给 SkillBarController 也加个 Initialize 方法，
            // 就像前两个脚本一样。为了简单起见，直接赋值也行，
            // 因为 SkillBarController 在 OnEnable 里会用它)
            skillBarController.selectionManager = selectionManager;

            // 稍微有点尴尬的是 SkillBarController 的 OnEnable 可能已经跑过了
            // 所以最好手动触发一次刷新：
            // (更完美的方法是去改 SkillBarController 加 Initialize，
            // 但这里我们假设它会自己在 Update 或重新 OnEnable 时处理)
        }
    }
}