using UnityEngine;
using Game.Battle;
using Game.Battle.Status;
using Game.UI; // 引用 SkillBarController 所在的命名空间以便查找

public class DebugStatusTester : MonoBehaviour
{
    [Header("Drag your Status Assets here")]
    public StatusDefinition stellarErosion; // 按 1
    public StatusDefinition lunarScar;      // 按 2
    public StatusDefinition nightCinders;   // 按 3

    [Header("System Refs")]
    public SelectionManager selectionManager;

    void Start()
    {
        if (selectionManager == null)
            selectionManager = FindFirstObjectByType<SelectionManager>();
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Alpha1)) ApplyToSelection(stellarErosion);
        if (Input.GetKeyDown(KeyCode.Alpha2)) ApplyToSelection(lunarScar);
        if (Input.GetKeyDown(KeyCode.Alpha3)) ApplyToSelection(nightCinders);

        // 按 0 结束当前单位的回合（方便触发 夜烬 的结算）
        if (Input.GetKeyDown(KeyCode.Alpha0)) ForceEndTurn();
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
        if (battleUnit && battleUnit.Status)
        {
            // 这里的 source 传自己，模拟自己给自己挂（或者你可以传 null）
            battleUnit.Status.ApplyStatus(def, battleUnit);
            Debug.Log($"<color=yellow>[DEBUG] 给 {unit.name} 挂上了 {def.name}</color>");
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
                bu.OnTurnEnd(); // 手动触发 Status 结算
            }
        }
    }
}