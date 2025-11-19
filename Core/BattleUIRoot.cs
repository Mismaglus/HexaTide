using UnityEngine;

public class BattleUIRoot : MonoBehaviour
{
    // ... 你的 Panel 引用 ...
    public EndTurnPanel endTurnPanel;
    public SkillBarController skillBarController; // 假设你有这个

    void Awake()
    {
        // 这一步保留，把自己注册进去
        BattleRuntimeRefs.RegisterUIRoot(this);
    }

    // 删除 void Start() {...} 

    // 新增：由外部（Bootstrap）调用的初始化方法
    public void Initialize(BattleController battle)
    {
        if (battle == null)
        {
            Debug.LogError("BattleUIRoot: Initialize called with null BattleController!");
            return;
        }

        // 这里连接具体的子面板
        if (endTurnPanel != null) endTurnPanel.Initialize(battle);
        // if (skillBarController != null) skillBarController.Initialize(battle); 

        Debug.Log("UI Wired Up Successfully!");
    }
}