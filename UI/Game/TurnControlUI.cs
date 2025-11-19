using UnityEngine;
using UnityEngine.UI;
using Game.Battle; // 必须引用这个命名空间，才能识别 BattleStateMachine

namespace Game.UI
{
    public class TurnControlUI : MonoBehaviour
    {
        [Header("UI Components")]
        [Tooltip("请把 EndTurn 这个根物体拖进来 (它上面有 Button 组件)")]
        public Button endTurnButton;

        // 私有变量，不再自动查找，等待 BattleUIRoot 注入
        private BattleStateMachine _battleStateMachine;

        void Awake()
        {
            // 绑定按钮点击事件
            if (endTurnButton != null)
            {
                endTurnButton.onClick.RemoveAllListeners();
                endTurnButton.onClick.AddListener(OnEndTurnClicked);
            }
        }

        // ⭐ 这就是报错说“找不到”的那个方法，必须声明为 public
        public void Initialize(BattleStateMachine sm)
        {
            _battleStateMachine = sm;

            if (_battleStateMachine != null)
            {
                // 1. 先取消订阅防止重复
                _battleStateMachine.OnTurnChanged -= OnTurnChanged;
                // 2. 重新订阅
                _battleStateMachine.OnTurnChanged += OnTurnChanged;

                // 3. 根据当前回合立刻刷新按钮状态
                RefreshButtonState(_battleStateMachine.CurrentTurn);

                Debug.Log("[TurnControlUI] 初始化成功，已连接到战斗状态机！");
            }
            else
            {
                Debug.LogError("[TurnControlUI] Initialize 收到了空的状态机引用！");
            }
        }

        void OnDestroy()
        {
            if (_battleStateMachine != null)
                _battleStateMachine.OnTurnChanged -= OnTurnChanged;
        }

        // === 逻辑部分 ===

        void OnTurnChanged(TurnSide side)
        {
            RefreshButtonState(side);
        }

        void RefreshButtonState(TurnSide side)
        {
            if (endTurnButton == null) return;

            // 只有在玩家回合，按钮才可点击
            bool isPlayerTurn = (side == TurnSide.Player);
            endTurnButton.interactable = isPlayerTurn;
        }

        void OnEndTurnClicked()
        {
            if (_battleStateMachine != null)
            {
                // 点击后立即禁用，防止连点
                endTurnButton.interactable = false;
                // 发送结束请求
                _battleStateMachine.EndTurnRequest();
            }
        }
    }
}