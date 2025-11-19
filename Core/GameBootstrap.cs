using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;

public class GameBootstrap : MonoBehaviour
{
    [Header("Scene Names")]
    public string battleUiSceneName = "BattleUI";
    public string firstBattleSceneName = "Battle_Prototype";

    void Start()
    {
        StartCoroutine(BootRoutine());
    }

    IEnumerator BootRoutine()
    {
        // 确保当前是 Single 模式加载的 Main_Persistent
        // 然后加 UI Scene
        var uiOp = SceneManager.LoadSceneAsync(battleUiSceneName, LoadSceneMode.Additive);
        yield return uiOp;

        // 再加第一个战斗场景
        var battleOp = SceneManager.LoadSceneAsync(firstBattleSceneName, LoadSceneMode.Additive);
        yield return battleOp;

        // 这里可以做一些初始化，比如设置活动场景
        var battleScene = SceneManager.GetSceneByName(firstBattleSceneName);
        if (battleScene.IsValid())
        {
            SceneManager.SetActiveScene(battleScene);
        }
    }
}
