using UnityEngine;
using UnityEngine.InputSystem; // ⭐ 必须引用这个命名空间
using Game.Battle;
using Game.Battle.Status;
using Game.UI;

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
        // ⭐ 获取当前键盘设备，防止为空报错
        var kb = Keyboard.current;
        if (kb == null) return;

        // ⭐ 使用新输入系统的 API: wasPressedThisFrame
        // 注意：Key.1 是无效的变量名，新系统用 digit1Key, digit2Key 等
        if (kb.digit1Key.wasPressedThisFrame) ApplyToSelection(stellarErosion);
        if (kb.digit2Key.wasPressedThisFrame) ApplyToSelection(lunarScar);
        if (kb.digit3Key.wasPressedThisFrame) ApplyToSelection(nightCinders);

        // 按 0 结束当前单位的回合
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

        // 注意：这里获取的是 BattleUnit，确保你的 Unit Prefab 上挂载了 BattleUnit 和 UnitStatusController
        var battleUnit = unit.GetComponent<BattleUnit>();

        if (battleUnit != null)
        {
            if (battleUnit.Status != null)
            {
                battleUnit.Status.ApplyStatus(def, battleUnit);
                Debug.Log($"<color=yellow>[DEBUG] 给 {unit.name} 挂上了 {def.name}</color>");
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
                bu.OnTurnEnd(); // 手动触发 Status 结算
            }
        }
    }
}