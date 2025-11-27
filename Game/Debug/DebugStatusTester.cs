using UnityEngine;
using UnityEngine.InputSystem;
using Game.Battle;
using Game.Battle.Status;
using Game.UI;
using System.Collections;

public class DebugStatusTester : MonoBehaviour
{
    [Header("Status Assets")]
    public StatusDefinition stellarErosion; // 1
    public StatusDefinition lunarScar;      // 2
    public StatusDefinition nightCinders;   // 3

    [Header("Settings")]
    [Tooltip("一次施加几层？")]
    [Min(1)] public int stacksToApply = 5;

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

        if (kb.digit1Key.wasPressedThisFrame) ApplyToSelection(stellarErosion);
        if (kb.digit2Key.wasPressedThisFrame) ApplyToSelection(lunarScar);
        if (kb.digit3Key.wasPressedThisFrame) ApplyToSelection(nightCinders);

        // 按 9：触发 OnTurnStart (现在 星蚀/月痕 的扣血和减层都在这里)
        if (kb.digit9Key.wasPressedThisFrame) ForceStartTurn();

        // 按 0：触发 OnTurnEnd (夜烬 的扣血和减层在这里)
        if (kb.digit0Key.wasPressedThisFrame) ForceEndTurn();

        // 按 8：模拟完整回合 (Start -> Wait -> End)
        if (kb.digit8Key.wasPressedThisFrame) StartCoroutine(SimulateFullTurn());
    }

    void ApplyToSelection(StatusDefinition def)
    {
        if (def == null) return;
        var unit = selectionManager.SelectedUnit;
        if (unit == null) { Debug.LogWarning("请先选中单位"); return; }

        var battleUnit = unit.GetComponent<BattleUnit>();
        if (battleUnit && battleUnit.Status)
        {
            battleUnit.Status.ApplyStatus(def, battleUnit, stacksToApply);
            Debug.Log($"<color=yellow>[DEBUG] 给 {unit.name} 挂上了 {stacksToApply} 层 {def.name}</color>");
        }
    }

    void ForceStartTurn()
    {
        var unit = selectionManager.SelectedUnit;
        if (unit?.GetComponent<BattleUnit>() is BattleUnit bu)
        {
            Debug.Log($"<color=green>[DEBUG] 触发 OnTurnStart (星蚀/月痕)...</color>");
            bu.OnTurnStart();
        }
    }

    void ForceEndTurn()
    {
        var unit = selectionManager.SelectedUnit;
        if (unit?.GetComponent<BattleUnit>() is BattleUnit bu)
        {
            Debug.Log($"<color=red>[DEBUG] 触发 OnTurnEnd (夜烬)...</color>");
            bu.OnTurnEnd();
        }
    }

    IEnumerator SimulateFullTurn()
    {
        var unit = selectionManager.SelectedUnit;
        if (unit?.GetComponent<BattleUnit>() is BattleUnit bu)
        {
            Debug.Log($"<color=cyan>[DEBUG] 模拟完整回合...</color>");
            bu.OnTurnStart();
            yield return new WaitForSeconds(0.5f);
            bu.OnTurnEnd();
        }
    }
}