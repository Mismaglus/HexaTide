using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.EventSystems; // 引用事件系统
using Game.Battle;
using Game.Battle.Abilities;    // 引用 Ability

namespace Game.UI
{
    [RequireComponent(typeof(Button))]
    public class TacticalActionButton : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        [Header("Data Source")]
        [Tooltip("这里拖入一个代表'疾跑'的技能资产(Ability Asset)，用于显示Tooltip和读取消耗")]
        public Ability tacticalAbility;

        [Header("References")]
        public Image iconImage;
        public TMP_Text labelText;
        public SkillTooltipController tooltipController; // 拖入场景里的 TooltipController，或者自动找

        [Header("Visuals")]
        public Color activeColor = new Color(1f, 0.8f, 0.2f, 1f);
        public Color inactiveColor = Color.white;

        [Header("UI Timing")]
        [Tooltip("即时战术动作点击后，延迟多少秒再强制 Button 取消选中/退出。")]
        public float immediateDeselectDelay = 0.1f;

        private Button _btn;
        private SelectionManager _selectionManager;

        void Awake()
        {
            _btn = GetComponent<Button>();
            _btn.onClick.AddListener(OnClicked);

            _selectionManager = FindFirstObjectByType<SelectionManager>(FindObjectsInactive.Exclude);
            if (tooltipController == null)
                tooltipController = FindFirstObjectByType<SkillTooltipController>(FindObjectsInactive.Include);
        }

        void Start()
        {
            if (_selectionManager != null)
            {
                _selectionManager.OnTacticalStateChanged += HandleStateChanged;
                HandleStateChanged(_selectionManager.IsTacticalMoveActive);
            }

            // 初始化图标 (如果有配好的 Ability)
            if (tacticalAbility != null && iconImage != null)
            {
                // 如果你想直接用 Ability 里的图标，可以解开下面这行
                // iconImage.sprite = tacticalAbility.icon;
            }
        }

        void OnDestroy()
        {
            if (_selectionManager != null)
                _selectionManager.OnTacticalStateChanged -= HandleStateChanged;
        }

        void OnClicked()
        {
            if (_selectionManager != null)
                _selectionManager.ToggleTacticalAction();

            // 若技能设为即时触发，点击后清理选中状态与 Tooltip，避免按钮卡在 Selected 高亮
            if (tacticalAbility != null && tacticalAbility.triggerImmediately)
            {
                if (tooltipController != null)
                {
                    tooltipController.Hide();
                }

                StartCoroutine(DelayedDeselect());
            }
        }

        void HandleStateChanged(bool isActive)
        {
            if (iconImage) iconImage.color = isActive ? activeColor : inactiveColor;
        }

        // === 鼠标悬停逻辑 ===

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (tooltipController == null || tacticalAbility == null) return;

            // 获取当前选中的单位作为 Caster (为了计算属性加成等，虽然疾跑可能不需要)
            BattleUnit caster = null;
            if (_selectionManager != null && _selectionManager.SelectedUnit != null)
            {
                caster = _selectionManager.SelectedUnit.GetComponent<BattleUnit>();
            }

            // 调用你现有的 Tooltip 显示方法
            // isEnemy = false (因为是自己的按钮)
            // slotRect = 自己的 RectTransform (用于定位)
            tooltipController.Show(tacticalAbility, caster, false, GetComponent<RectTransform>());
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            if (tooltipController != null)
            {
                tooltipController.Hide();
            }
        }

        IEnumerator DelayedDeselect()
        {
            if (immediateDeselectDelay > 0f)
                yield return new WaitForSeconds(immediateDeselectDelay);

            if (EventSystem.current != null && _btn != null)
            {
                EventSystem.current.SetSelectedGameObject(null);

                var deselectEvt = new BaseEventData(EventSystem.current);
                _btn.OnDeselect(deselectEvt);

                var exitEvt = new PointerEventData(EventSystem.current);
                _btn.OnPointerExit(exitEvt);
            }
        }
    }
}
