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
        [Header("Existing Sections...")]
        public Image portraitImage;
        public Slider hpSlider;
        public TMP_Text hpText;
        public GenericBarController apBarController;
        public GenericBarController mpBarController;
        public StrideVisualController strideController;
        public SelectionManager selectionManager;
        public GameObject contentRoot;

        // === ⭐ 新增：状态栏引用 ===
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

            // === ⭐ 绑定状态栏 ===
            if (statusPanel != null)
            {
                statusPanel.Bind(unit);
            }

            if (!hasUnit) return;

            if (portraitImage != null) portraitImage.sprite = unit.portrait;
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
                if (hpSlider != null) { hpSlider.maxValue = maxHP; hpSlider.value = maxHP - curHP; }
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