using UnityEngine;
using Game.Common; // 引用 GameRandom

public class BattleController : MonoBehaviour
{
    [Header("Battle Settings")]
    public Camera battleCamera;

    [Tooltip("如果为 0，则随机生成；否则使用固定种子 (方便调试)。")]
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
        // 如果 Inspector 里没填种子 (0)，就根据时间生成一个
        if (battleSeed == 0)
        {
            battleSeed = (int)System.DateTime.Now.Ticks;
        }

        // 初始化全局随机数生成器
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

    public void EndTurn()
    {
        // 回合结束逻辑
    }

    public void TryUndoLastAction()
    {
        // Undo 逻辑
    }

    public void SelectNextAlly()
    {
        // 循环选人逻辑
    }
}