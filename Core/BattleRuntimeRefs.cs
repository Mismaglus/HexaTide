using UnityEngine;

[CreateAssetMenu(fileName = "BattleRuntimeRefs", menuName = "Game/BattleRuntimeRefs")]
public class BattleRuntimeRefs : ScriptableObject
{
    static BattleRuntimeRefs _instance;

    public static BattleRuntimeRefs Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = Resources.Load<BattleRuntimeRefs>("BattleRuntimeRefs");
            }
            return _instance;
        }
    }

    [Header("Runtime refs")]
    public BattleController battleController;
    public BattleUIRoot uiRoot;
    public Camera battleCamera;

    public static void RegisterBattle(BattleController controller, Camera camera)
    {
        if (Instance == null) return;
        Instance.battleController = controller;
        Instance.battleCamera = camera;
    }

    public static void RegisterUIRoot(BattleUIRoot ui)
    {
        if (Instance == null) return;
        Instance.uiRoot = ui;
    }

    public static void ClearBattle()
    {
        if (Instance == null) return;
        Instance.battleController = null;
        Instance.battleCamera = null;
    }
}
