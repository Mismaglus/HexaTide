using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;

public class GameBootstrap : MonoBehaviour
{
    [Header("Scene Names")]
    public string battleUiSceneName = "BattleUI"; // 确保和截图名字一致
    public string firstBattleSceneName = "Battle";   // 截图里叫 "Battle"，不是 "Battle_Prototype"

    void Start()
    {
        StartCoroutine(BootRoutine());
    }

    IEnumerator BootRoutine()
    {
        // 1. 加载 UI
        yield return SceneManager.LoadSceneAsync(battleUiSceneName, LoadSceneMode.Additive);

        // 2. 加载 战斗
        yield return SceneManager.LoadSceneAsync(firstBattleSceneName, LoadSceneMode.Additive);
        // 放在 BattleController.Start() 里
        Game.Localization.LocalizationManager.Get("UI_COOLDOWN"); // 随便取一个Key，强制触发加载
        // 3. 设置活动场景 (为了光照和生成物体的归属)
        var battleScene = SceneManager.GetSceneByName(firstBattleSceneName);
        if (battleScene.IsValid()) SceneManager.SetActiveScene(battleScene);

        // === 关键步骤：手动连线 ===
        // 此时两个场景的 Awake 都跑完了，Refs 里应该都有东西了
        var refs = BattleRuntimeRefs.Instance;

        if (refs.uiRoot != null && refs.battleController != null)
        {
            refs.uiRoot.Initialize(refs.battleController);
        }
        else
        {
            Debug.LogError($"Bootstrap Error: Missing refs. UI: {refs.uiRoot}, Battle: {refs.battleController}");
        }
    }
}