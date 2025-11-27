using UnityEngine;
using UnityEngine.InputSystem;
using Game.Battle;
using Game.Battle.Status;
using Game.UI;

public class DebugStatusTester : MonoBehaviour
{
    [Header("Drag your Status Assets here")]
    public StatusDefinition stellarErosion; // 按 1
    public StatusDefinition lunarScar;      // 按 2
    public StatusDefinition nightCinders;   // 按 3

    [Header("Settings")]
    [Tooltip("每次按键施加几层状态？(默认1，可以在运行时修改测试爆发)")]
    [Min(1)] public int stacksToApply = 1; // ⭐ 新增：层数控制

    [Header("System Refs")]
    public SelectionManager selectionManager;

    void Start()
    {
        if (selectionManager == null)
            selectionManager = FindFirstObjectByType<SelectionManager>();
    }

    void Update()
    {
        var kb = Keyboard.current;
        if (kb == null) return;

        // 按 1/2/3 施加对应状态 (带指定层数)
        if (kb.digit1Key.wasPressedThisFrame) ApplyToSelection(stellarErosion);
        if (kb.digit2Key.wasPressedThisFrame) ApplyToSelection(lunarScar);
        if (kb.digit3Key.wasPressedThisFrame) ApplyToSelection(nightCinders);

        // 按 0 强制结算回合结束 (触发 DoT)
        if (kb.digit0Key.wasPressedThisFrame) ForceEndTurn();
    }

    void ApplyToSelection(StatusDefinition def)
    {
        if (def == null) return;

        var unit = selectionManager.SelectedUnit;
        if (unit == null)
        {
            Debug.LogWarning("先选中一个单位！");
            return;
        }

        var battleUnit = unit.GetComponent<BattleUnit>();

        if (battleUnit != null)
        {
            if (battleUnit.Status != null)
            {
                // ⭐ 修改：传入 stacksToApply 参数
                battleUnit.Status.ApplyStatus(def, battleUnit, stacksToApply);
                Debug.Log($"<color=yellow>[DEBUG] 给 {unit.name} 挂上了 {stacksToApply} 层 {def.name}</color>");
            }
            else
            {
                Debug.LogError($"[DEBUG] {unit.name} 缺少 UnitStatusController 组件！请在 Prefab 上添加。");
            }
        }
    }

    void ForceEndTurn()
    {
        var unit = selectionManager.SelectedUnit;
        if (unit != null)
        {
            var bu = unit.GetComponent<BattleUnit>();
            if (bu)
            {
                Debug.Log($"<color=cyan>[DEBUG] 强制结算 {unit.name} 的回合结束事件...</color>");
                bu.OnTurnEnd();
            }
        }
    }
}