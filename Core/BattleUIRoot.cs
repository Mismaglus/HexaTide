using UnityEngine;

public class BattleUIRoot : MonoBehaviour
{
    public EndTurnPanel endTurnPanel;
    public SkillBarController skillBarController;

    BattleController _battle;

    void Awake()
    {
        BattleRuntimeRefs.RegisterUIRoot(this);
    }

    void Start()
    {
        // Start 阶段战斗 Scene 一般已经加载完了，可以尝试拿引用
        _battle = BattleRuntimeRefs.Instance != null
            ? BattleRuntimeRefs.Instance.battleController
            : null;

        if (_battle == null)
        {
            Debug.LogWarning("BattleUIRoot: no BattleController found yet.");
        }
        else
        {
            WireUpPanels();
        }
    }

    void WireUpPanels()
    {
        if (endTurnPanel != null)
        {
            endTurnPanel.Initialize(_battle);
        }

        if (skillBarController != null)
        {
            skillBarController.Initialize(_battle);
        }
    }
}
