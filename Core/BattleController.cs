using UnityEngine;
using Game.Common;

public class BattleController : MonoBehaviour
{
    public Camera battleCamera;

    [Tooltip("如果为 0，则随机生成；否则使用固定种子 (方便调试/回放)。")]
    public int battleSeed = 0;

    void Awake()
    {
        if (battleCamera == null)
        {
            battleCamera = Camera.main;
        }

        InitializeRNG();
        BattleRuntimeRefs.RegisterBattle(this, battleCamera);
    }

    void InitializeRNG()
    {
        if (battleSeed == 0)
        {
            battleSeed = (int)System.DateTime.Now.Ticks;
        }
        GameRandom.Init(battleSeed);
    }

    void OnDestroy()
    {
        if (BattleRuntimeRefs.Instance != null &&
            BattleRuntimeRefs.Instance.battleController == this)
        {
            BattleRuntimeRefs.ClearBattle();
        }
    }

    public void EndTurn() { }
    public void TryUndoLastAction() { }
    public void SelectNextAlly() { }
}