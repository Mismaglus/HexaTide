using UnityEngine;
using Game.Common;

public class BattleController : MonoBehaviour
{
    [Header("Battle Settings")]
    public Camera battleCamera;

    [Tooltip("如果为 0，则随机生成；否则使用固定种子 (方便调试/回放)。")]
    public int battleSeed = 0;

    [Header("Cursor Settings")] // ⭐ 新增
    public Texture2D defaultCursor;
    public Vector2 defaultCursorHotspot = Vector2.zero;

    void Awake()
    {
        if (battleCamera == null)
        {
            battleCamera = Camera.main;
        }

        InitializeRNG();
        BattleRuntimeRefs.RegisterBattle(this, battleCamera);
    }

    void Start()
    {
        // ⭐ 游戏开始时设置默认光标
        if (defaultCursor != null)
        {
            Cursor.SetCursor(defaultCursor, defaultCursorHotspot, CursorMode.Auto);
        }
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