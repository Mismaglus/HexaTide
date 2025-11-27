using UnityEngine;
using UnityEngine.InputSystem;
using Game.Battle;
using Game.Battle.Status;
using Game.UI;

public class DebugStatusTester : MonoBehaviour
{
    [Header("Status Assets")]
    public StatusDefinition stellarErosion; // Key: 1
    public StatusDefinition lunarScar;      // Key: 2
    public StatusDefinition nightCinders;   // Key: 3

    [Header("Debug Settings")]
    [Tooltip("按数字键时，一次施加几层状态？")]
    [Min(1)] public int stacksToApply = 1; // ⭐ 这里就是你要的设置

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

        // 施加状态 (带层数)
        if (kb.digit1Key.wasPressedThisFrame) ApplyToSelection(stellarErosion);
        if (kb.digit2Key.wasPressedThisFrame) ApplyToSelection(lunarScar);
        if (kb.digit3Key.wasPressedThisFrame) ApplyToSelection(nightCinders);

        // ⭐ 按 9：强制触发【回合开始】逻辑 -> 测试 星蚀 & 月痕
        if (kb.digit9Key.wasPressedThisFrame) ForceStartTurn();

        // ⭐ 按 0：强制触发【回合结束】逻辑 -> 测试 夜烬
        if (kb.digit0Key.wasPressedThisFrame) ForceEndTurn();
    }

    void ApplyToSelection(StatusDefinition def)
    {
        if (def == null) return;

        var unit = selectionManager.SelectedUnit;
        if (unit == null)
        {
            Debug.LogWarning("请先选中一个单位！");
            return;
        }

        var battleUnit = unit.GetComponent<BattleUnit>();
        if (battleUnit != null && battleUnit.Status != null)
        {
            // 传入 stacksToApply
            battleUnit.Status.ApplyStatus(def, battleUnit, stacksToApply);
            Debug.Log($"<color=yellow>[DEBUG] 给 {unit.name} 挂上了 {stacksToApply} 层 {def.name}</color>");
        }
        else
        {
            Debug.LogError($"[DEBUG] 单位 {unit.name} 缺少 BattleUnit 或 UnitStatusController 组件！");
        }
    }

    void ForceStartTurn()
    {
        var unit = selectionManager.SelectedUnit;
        if (unit?.GetComponent<BattleUnit>() is BattleUnit bu)
        {
            Debug.Log($"<color=green>[DEBUG] 强制触发 {unit.name} 的 OnTurnStart (星蚀/月痕)...</color>");
            bu.OnTurnStart();
        }
    }

    void ForceEndTurn()
    {
        var unit = selectionManager.SelectedUnit;
        if (unit?.GetComponent<BattleUnit>() is BattleUnit bu)
        {
            Debug.Log($"<color=red>[DEBUG] 强制触发 {unit.name} 的 OnTurnEnd (夜烬)...</color>");
            bu.OnTurnEnd();
        }
    }
}