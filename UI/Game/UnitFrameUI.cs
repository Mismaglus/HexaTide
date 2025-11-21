using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Game.Battle;
using Game.Units;
using Game.UI.Helper; // 引用 StrideVisualController

namespace Game.UI
{
    public class UnitFrameUI : MonoBehaviour
    {
        [Header("Portrait Section")]
        public Image portraitImage;
        public Slider hpSlider;
        public TMP_Text hpText;

        [Header("Stats Section")]
        public GenericBarController apBarController;
        public GenericBarController mpBarController;

        [Header("Movement Section")]
        public StrideVisualController strideController; // ⭐ 替换了原来的 Text/Icon

        [Header("System")]
        public SelectionManager selectionManager;
        public GameObject contentRoot;

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
            if (!hasUnit) return;

            if (portraitImage != null) portraitImage.sprite = unit.portrait;
            RefreshDynamicValues();
        }

        void RefreshDynamicValues()
        {
            if (_currentUnit == null) return;

            // 1. 基础属性 (HP/MP/AP)
            if (_currentUnit.TryGetComponent<UnitAttributes>(out var attrs))
            {
                float curHP = attrs.Core.HP;
                float maxHP = attrs.Core.HPMax;
                if (hpSlider != null) { hpSlider.maxValue = maxHP; hpSlider.value = maxHP - curHP; }
                if (hpText != null) { hpText.text = $"{curHP}/{maxHP}"; }

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

                // 2. ⭐ 移动力更新
                // 读取 Stride (Max 暂时我们假设它在 Attributes 里或者是个固定值/计算值)
                // 注意：UnitMover 通常存的是“当前剩余步数”，最大步数在 Attributes.Core.Stride
                if (strideController != null)
                {
                    // 获取当前步数
                    int currentStride = 0;
                    if (_currentUnit.TryGetComponent<UnitMover>(out var mover))
                        currentStride = mover.CurrentStride;

                    // 获取最大步数 (用于决定显示哪张背景图)
                    int maxStride = attrs.Core.Stride;

                    // 更新 UI
                    strideController.UpdateView(currentStride, maxStride);
                }
            }
        }
    }
}