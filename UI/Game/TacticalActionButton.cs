using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.EventSystems;
using Game.Battle;
using Game.Battle.Abilities;
using Game.Battle.Status;

namespace Game.UI
{
    [RequireComponent(typeof(Button))]
    public class TacticalActionButton : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        [Header("Data Source")]
        [Tooltip("拖入 Sprint Ability Asset (需配置 Trigger Immediately = true, AP Cost = 1, Apply Status Effect = Sprint)")]
        public Ability tacticalAbility;

        [Header("References")]
        public Image iconImage;
        public SkillTooltipController tooltipController;
        public AbilityTargetingSystem targetingSystem;

        [Header("Visuals")]
        public Color activeColor = new Color(1f, 0.8f, 0.2f, 1f);
        public Color inactiveColor = Color.white;

        private Button _btn;
        private SelectionManager _selectionManager;
        private UnitStatusController _currentStatusCtrl;

        void Awake()
        {
            _btn = GetComponent<Button>();
            _btn.onClick.AddListener(OnClicked);

            _selectionManager = FindFirstObjectByType<SelectionManager>(FindObjectsInactive.Exclude);
            if (tooltipController == null)
                tooltipController = FindFirstObjectByType<SkillTooltipController>(FindObjectsInactive.Include);
            ResolveTargetingSystem();
        }

        void Start()
        {
            if (tacticalAbility != null && iconImage != null)
            {
                iconImage.sprite = tacticalAbility.icon;
            }

            // 订阅选中事件，以便切换监听对象
            if (_selectionManager != null)
            {
                _selectionManager.OnSelectedUnitChanged += HandleUnitChanged;
                HandleUnitChanged(_selectionManager.SelectedUnit);
            }
        }

        void OnDestroy()
        {
            if (_selectionManager != null)
                _selectionManager.OnSelectedUnitChanged -= HandleUnitChanged;

            if (_currentStatusCtrl != null)
                _currentStatusCtrl.OnStatusChanged -= RefreshVisuals;
        }

        void HandleUnitChanged(Game.Units.Unit unit)
        {
            // 1. 解绑旧的
            if (_currentStatusCtrl != null)
            {
                _currentStatusCtrl.OnStatusChanged -= RefreshVisuals;
                _currentStatusCtrl = null;
            }

            // 2. 绑定新的
            if (unit != null)
            {
                _currentStatusCtrl = unit.GetComponent<UnitStatusController>();
                if (_currentStatusCtrl != null)
                {
                    _currentStatusCtrl.OnStatusChanged += RefreshVisuals;
                }
            }

            RefreshVisuals();
        }

        void RefreshVisuals()
        {
            if (iconImage == null) return;

            bool isActive = false;
            if (_currentStatusCtrl != null)
            {
                isActive = _currentStatusCtrl.HasSprintState;
            }
            iconImage.color = isActive ? activeColor : inactiveColor;
        }

        void OnClicked()
        {
            if (tacticalAbility == null) return;

            // 防守式查找：允许 TargetingSystem 在其他场景/晚于本脚本激活时再获取
            if (targetingSystem == null) ResolveTargetingSystem();

            Debug.Log("[TacticalButton] Triggering Ability via System...");
            if (targetingSystem != null)
            {
                targetingSystem.EnterTargetingMode(tacticalAbility);
            }
            else
            {
                Debug.LogWarning("[TacticalButton] 未找到 AbilityTargetingSystem，无法触发技能。请确认战斗场景已加载或场景里有该组件。");
            }

            // 强制取消 UI 选中，防止按钮一直亮着
            EventSystem.current.SetSelectedGameObject(null);
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (tooltipController == null || tacticalAbility == null) return;

            BattleUnit caster = null;
            if (_selectionManager != null && _selectionManager.SelectedUnit != null)
            {
                caster = _selectionManager.SelectedUnit.GetComponent<BattleUnit>();
            }
            tooltipController.Show(tacticalAbility, caster, false, GetComponent<RectTransform>());
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            if (tooltipController != null) tooltipController.Hide();
        }

        void ResolveTargetingSystem()
        {
            if (targetingSystem != null) return;
            targetingSystem = FindFirstObjectByType<AbilityTargetingSystem>(FindObjectsInactive.Include);
        }
    }
}
