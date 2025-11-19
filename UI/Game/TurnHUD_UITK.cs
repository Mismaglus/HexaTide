using Game.UI;
using UnityEngine;
using UnityEngine.UIElements;
using Game.Battle;
using Game.Units;
// 注意：SelectionManager 的命名空间按你的工程来，常见�?Game.Selection

public class TurnHUD_UITK : MonoBehaviour
{
    public BattleStateMachine battleStateMachine;
    public UIDocument doc;

    [SerializeField] private SelectionManager selection; // �?新增：从它拿当前选中
    [SerializeField] Label turnText, apText, strideLabel;
    Button endTurnBtn;
    Button settingsBtn;

    void Awake()
    {
        if (doc == null) doc = GetComponent<UIDocument>();
        var root = doc.rootVisualElement;

        turnText = root.Q<Label>("TurnText");
        apText = root.Q<Label>("APText");
        strideLabel = root.Q<Label>("StrideLabel");
        endTurnBtn = root.Q<Button>("EndTurnBtn");
        endTurnBtn.clicked += OnEndTurn;
        settingsBtn = root.Q<Button>("SettingsBtn");
        if (settingsBtn != null)
            settingsBtn.clicked += OnOpenSettings;

        if (selection == null)
            selection = Object.FindFirstObjectByType<SelectionManager>();
    }

    void OnEnable()
    {
        if (battleStateMachine != null) battleStateMachine.OnTurnChanged += UpdateTurnUI;

        // 如果 SelectionManager 暴露了事件，订�
        if (selection != null) selection.OnSelectedUnitChanged += OnSelectedUnitChanged;
        if (settingsBtn != null) settingsBtn.SetEnabled(true);

        UpdateTurnUI(battleStateMachine != null ? battleStateMachine.CurrentTurn : TurnSide.Player);
        // 初始刷新一�?
        RefreshFor(selection != null ? selection.SelectedUnit : null);
    }

    void OnDisable()
    {
        if (battleStateMachine != null) battleStateMachine.OnTurnChanged -= UpdateTurnUI;
        if (endTurnBtn != null) endTurnBtn.clicked -= OnEndTurn;
        if (selection != null) selection.OnSelectedUnitChanged -= OnSelectedUnitChanged;
        if (settingsBtn != null) settingsBtn.clicked -= OnOpenSettings;
    }

    void Update()
    {
        // 每帧轻量刷新（确保移�?步数变化立刻反映�?
        var unit = selection != null ? selection.SelectedUnit : null;
        RefreshFor(unit);

        endTurnBtn?.SetEnabled(battleStateMachine != null && battleStateMachine.CurrentTurn == TurnSide.Player);
        settingsBtn?.SetEnabled(true);
    }

    // ====== 刷新逻辑（核心） ======
    void RefreshFor(Unit selected)
    {
        UpdateAP_ForSelected(selected);
        UpdateStride_ForSelected(selected);
    }

    void UpdateAP_ForSelected(Unit selected)
    {
        if (apText == null) return;

        if (selected != null
            && selected.IsPlayerControlled
            && selected.TryGetComponent<BattleUnit>(out var bu))
        {
            apText.style.display = DisplayStyle.Flex;
            apText.text = $"AP {bu.CurAP}/{bu.MaxAP}";
        }
        else
        {
            // 非玩�?无选中 �?不显�?AP
            apText.style.display = DisplayStyle.None;
        }
    }

    void UpdateStride_ForSelected(Unit selected)
    {
        if (strideLabel == null) return;

        if (selected != null && selected.TryGetComponent<UnitMover>(out var mover))
        {
            strideLabel.style.display = DisplayStyle.Flex;
            strideLabel.text = $"Stride {mover.strideLeft}/{mover.strideMax}";
        }
        else
        {
            strideLabel.style.display = DisplayStyle.None;
        }
    }

    void OnSelectedUnitChanged(Unit u)
    {

        RefreshFor(u);
    }

    void UpdateTurnUI(TurnSide side)
    {
        if (turnText != null)
            turnText.text = side == TurnSide.Player ? "Player Turn" : "Enemy Turn";
    }

    void OnEndTurn()
    {
        if (battleStateMachine != null && battleStateMachine.CurrentTurn == TurnSide.Player)
            battleStateMachine.EndTurnRequest();
    }

    void OnOpenSettings()
    {
        SettingsPanelService.ShowOrToggle();
    }
}
