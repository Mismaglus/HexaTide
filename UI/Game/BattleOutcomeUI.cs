// Scripts/UI/Game/BattleOutcomeUI.cs
using UnityEngine;
using UnityEngine.SceneManagement; // 暂时用于重载场景
using UnityEngine.UI;
using Game.Battle;

namespace Game.UI
{
    public class BattleOutcomeUI : MonoBehaviour
    {
        [Header("Panels")]
        [Tooltip("胜利结算界面")]
        public GameObject victoryPanel;
        [Tooltip("失败结算界面")]
        public GameObject defeatPanel;

        [Header("Buttons (Optional auto-bind)")]
        public Button victoryContinueBtn;
        public Button defeatRetryBtn;
        public Button defeatQuitBtn;

        private BattleStateMachine _battleSM;

        void Awake()
        {
            // 默认隐藏
            if (victoryPanel) victoryPanel.SetActive(false);
            if (defeatPanel) defeatPanel.SetActive(false);

            // 绑定按钮事件
            if (victoryContinueBtn) victoryContinueBtn.onClick.AddListener(OnVictoryContinue);
            if (defeatRetryBtn) defeatRetryBtn.onClick.AddListener(OnDefeatRetry);
            if (defeatQuitBtn) defeatQuitBtn.onClick.AddListener(OnDefeatQuit);
        }

        void Start()
        {
            // 自动查找状态机
            _battleSM = BattleStateMachine.Instance;
            if (_battleSM == null)
                _battleSM = FindFirstObjectByType<BattleStateMachine>(FindObjectsInactive.Include);

            if (_battleSM != null)
            {
                _battleSM.OnVictory += HandleVictory;
                _battleSM.OnDefeat += HandleDefeat;
            }
            else
            {
                Debug.LogWarning("[BattleOutcomeUI] 找不到 BattleStateMachine，无法监听胜负。");
            }
        }

        void OnDestroy()
        {
            if (_battleSM != null)
            {
                _battleSM.OnVictory -= HandleVictory;
                _battleSM.OnDefeat -= HandleDefeat;
            }
        }

        void HandleVictory()
        {
            if (victoryPanel) victoryPanel.SetActive(true);
            // 可以在这里播放胜利音效
        }

        void HandleDefeat()
        {
            if (defeatPanel) defeatPanel.SetActive(true);
            // 可以在这里播放失败音效
        }

        // === 按钮回调 ===

        void OnVictoryContinue()
        {
            Debug.Log("点击了胜利继续... (此处应加载地图或下一关)");
            // 示例：重新加载当前场景，或者返回 Map Scene
            // SceneManager.LoadScene("MapScene"); 
        }

        void OnDefeatRetry()
        {
            Debug.Log("点击了重试... 重新加载战斗场景");
            // 重新加载当前场景
            SceneManager.LoadScene(SceneManager.GetActiveScene().name);
        }

        void OnDefeatQuit()
        {
            Debug.Log("点击了退出... 返回主菜单");
            // SceneManager.LoadScene("MainMenu");
        }
    }
}