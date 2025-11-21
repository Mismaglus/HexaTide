using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Game.Battle;
using Game.Units;

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
        public GenericBarController mpBarController; // 🔵 确保这个已连接

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

            // 1. HP 更新
            if (_currentUnit.TryGetComponent<UnitAttributes>(out var attrs))
            {
                // HP (红条：受伤量)
                float curHP = attrs.Core.HP;
                float maxHP = attrs.Core.HPMax;
                if (hpSlider != null) { hpSlider.maxValue = maxHP; hpSlider.value = maxHP - curHP; }
                if (hpText != null) { hpText.text = $"{curHP}/{maxHP}"; }

                // ⭐ MP (蓝条：剩余量)
                // 确保这里连接了 attrs.Core.MP
                if (mpBarController != null)
                {
                    mpBarController.MaxValue = attrs.Core.MPMax;
                    mpBarController.CurrentValue = attrs.Core.MP;
                }

                // ⭐ AP 更新 (从 Attributes 读取)
                if (apBarController != null)
                {
                    apBarController.MaxValue = attrs.Core.MaxAP;
                    apBarController.CurrentValue = attrs.Core.CurrentAP;
                }
            }
        }
    }
}