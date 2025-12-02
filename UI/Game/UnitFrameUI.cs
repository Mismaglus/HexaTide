using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Game.Battle;
using Game.Units;
using Game.UI.Helper;

namespace Game.UI
{
    public class UnitFrameUI : MonoBehaviour
    {
        [Header("Profile Section")]
        public Image portraitImage;

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

            if (statusPanel != null) statusPanel.Bind(unit);

            if (!hasUnit) return;

            if (portraitImage != null) portraitImage.sprite = unit.portrait;

            // 切换单位瞬间初始化（不播放动画）
            if (_currentUnit.TryGetComponent<UnitAttributes>(out var attrs))
            {
                if (hpBar != null)
                    hpBar.Initialize(attrs.Core.HP, attrs.Core.HPMax);
            }

            RefreshDynamicValues();
        }

        void RefreshDynamicValues()
        {
            if (_currentUnit == null) return;

            if (_currentUnit.TryGetComponent<UnitAttributes>(out var attrs))
            {
                float curHP = attrs.Core.HP;
                float maxHP = attrs.Core.HPMax;

                // 只有数值变化时才会触发动画
                if (hpBar != null) hpBar.UpdateValues(curHP, maxHP);
                if (hpText != null) hpText.text = $"{curHP}/{maxHP}";

                if (mpBarController != null)
                {
                    mpBarController.MaxValue = attrs.Core.MPMax;
                    mpBarController.CurrentValue = attrs.Core.MP;
                }

                if (apBarController != null)
                {
                    apBarController.MaxValue = attrs.Core.MaxAP;
                    apBarController.CurrentValue = attrs.Core.CurrentAP;
                }

                if (strideController != null)
                {
                    strideController.UpdateView(attrs.Core.CurrentStride, attrs.Core.Stride);
                }
            }
        }
    }
}