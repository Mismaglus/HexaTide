using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Game.Battle;
using Game.Units;
using Game.UI.Helper; // ⭐ 确保引用了 SmoothBarController 所在的命名空间

namespace Game.UI
{
    public class UnitFrameUI : MonoBehaviour
    {
        [Header("Profile Section")]
        public Image portraitImage;

        // ⭐ 修改 1: 替换原来的 Slider
        // public Slider hpSlider; 
        [Tooltip("请挂载带有 SmoothBarController 的子物体")]
        public SmoothBarController hpBar;

        public TMP_Text hpText;

        [Header("Resource Bars")]
        public GenericBarController apBarController;
        public GenericBarController mpBarController;
        public StrideVisualController strideController;

        [Header("System Refs")]
        public SelectionManager selectionManager;
        public GameObject contentRoot;

        [Header("Status Section")]
        public UnitStatusPanelUI statusPanel;

        private Unit _currentUnit;

        public void Initialize(SelectionManager manager)
        {
            selectionManager = manager;
            if (selectionManager != null)
            {
                selectionManager.OnSelectedUnitChanged -= HandleSelectionChanged;
                selectionManager.OnSelectedUnitChanged += HandleSelectionChanged;
                // 初始化时如果有选中单位，立即刷新
                HandleSelectionChanged(selectionManager.SelectedUnit);
            }
        }

        void OnDestroy()
        {
            if (selectionManager != null)
                selectionManager.OnSelectedUnitChanged -= HandleSelectionChanged;
        }

        void Update()
        {
            // 持续刷新以响应数据变化（如果没用事件驱动的话）
            if (_currentUnit != null && (contentRoot == null || contentRoot.activeSelf))
            {
                RefreshDynamicValues();
            }
        }

        void HandleSelectionChanged(Unit unit)
        {
            _currentUnit = unit;
            bool hasUnit = (unit != null);
            if (contentRoot != null) contentRoot.SetActive(hasUnit);

            if (statusPanel != null)
            {
                statusPanel.Bind(unit);
            }

            if (!hasUnit) return;

            if (portraitImage != null) portraitImage.sprite = unit.portrait;

            // ⭐ 修改 2: 切换单位时，瞬间重置血条状态
            // 这样当你从满血单位切到残血单位时，不会看到血条“哗”地掉下去，而是直接显示正确数值
            if (_currentUnit.TryGetComponent<UnitAttributes>(out var attrs))
            {
                if (hpBar != null)
                {
                    hpBar.Initialize(attrs.Core.HP, attrs.Core.HPMax);
                }
            }

            RefreshDynamicValues();
        }

        void RefreshDynamicValues()
        {
            if (_currentUnit == null) return;

            if (_currentUnit.TryGetComponent<UnitAttributes>(out var attrs))
            {
                // HP
                float curHP = attrs.Core.HP;
                float maxHP = attrs.Core.HPMax;

                // ⭐ 修改 3: 刷新平滑血条
                // SmoothBarController 内部会判断如果 curHP 变小了，就触发白条追赶动画
                if (hpBar != null)
                {
                    hpBar.UpdateValues(curHP, maxHP);
                }

                // 旧代码备份（如果你之前的 Slider 是反向显示的，现在建议改回正向显示）
                // if (hpSlider != null) { hpSlider.maxValue = maxHP; hpSlider.value = maxHP - curHP; }

                if (hpText != null) { hpText.text = $"{curHP}/{maxHP}"; }

                // MP
                if (mpBarController != null)
                {
                    mpBarController.MaxValue = attrs.Core.MPMax;
                    mpBarController.CurrentValue = attrs.Core.MP;
                }

                // AP
                if (apBarController != null)
                {
                    apBarController.MaxValue = attrs.Core.MaxAP;
                    apBarController.CurrentValue = attrs.Core.CurrentAP;
                }

                // Stride
                if (strideController != null)
                {
                    int currentStride = attrs.Core.CurrentStride;
                    int maxStride = attrs.Core.Stride;
                    strideController.UpdateView(currentStride, maxStride);
                }
            }
        }
    }
}