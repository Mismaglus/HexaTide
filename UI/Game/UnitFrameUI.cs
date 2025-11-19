using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Game.Battle; // 引用 SelectionManager, BattleUnit
using Game.Units;  // 引用 Unit, UnitAttributes

namespace Game.UI
{
    public class UnitFrameUI : MonoBehaviour
    {
        [Header("Portrait Section (Figure 2)")]
        [Tooltip("对应 SPR_Portrait")]
        public Image portraitImage;
        [Tooltip("对应 Slider_Damage")]
        public Slider hpSlider;
        [Tooltip("对应 Label_HitPoints")]
        public TMP_Text hpText;

        [Header("Stats Section (Figure 1)")]
        [Tooltip("对应 Bar_AP 上挂载的 GenericBarController")]
        public GenericBarController apBarController;
        [Tooltip("对应 Bar_MP 上挂载的 GenericBarController (如果有)")]
        public GenericBarController mpBarController;

        [Header("System")]
        public SelectionManager selectionManager;
        [Tooltip("如果不希望没选中时隐藏整个UI，可以不填")]
        public GameObject contentRoot;

        private Unit _currentUnit;

        void Awake()
        {
            if (selectionManager == null)
                selectionManager = FindFirstObjectByType<SelectionManager>(FindObjectsInactive.Exclude);
        }

        void OnEnable()
        {
            if (selectionManager != null)
            {
                selectionManager.OnSelectedUnitChanged += HandleSelectionChanged;
                HandleSelectionChanged(selectionManager.SelectedUnit);
            }
        }

        void OnDisable()
        {
            if (selectionManager != null)
                selectionManager.OnSelectedUnitChanged -= HandleSelectionChanged;
        }

        void Update()
        {
            // 轮询刷新（最稳健的方式，防止数值改变了UI没变）
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

            // 1. 刷新静态信息 (头像、名字)
            if (portraitImage != null) portraitImage.sprite = unit.portrait;
            // if (nameText != null) nameText.text = unit.unitName; // 如果以后有名字 Label 再加

            // 2. 立即刷新动态数值
            RefreshDynamicValues();
        }

        // UnitFrameUI.cs

        void RefreshDynamicValues()
        {
            if (_currentUnit == null) return;

            // === HP 部分 ===
            if (_currentUnit.TryGetComponent<UnitAttributes>(out var attrs))
            {
                float currentHP = attrs.Core.HP;
                float maxHP = attrs.Core.HPMax;

                // 1. 这里的逻辑要反转：计算“受到的伤害”
                // 满血时：damage = 0 (条为空)
                // 没血时：damage = max (条为满)
                float damage = maxHP - currentHP;

                if (hpSlider != null)
                {
                    hpSlider.maxValue = maxHP;
                    hpSlider.value = damage; // <--- 关键修改：传给 Slider 的是伤害量
                }

                // 2. 但是文字依然要显示“剩余血量”
                if (hpText != null)
                {
                    // 显示格式： 101/110
                    hpText.text = $"{currentHP}/{maxHP}";
                }
            }

            // === AP 部分 (保持原样，因为它还是常规的剩余量) ===
            if (apBarController != null && _currentUnit.TryGetComponent<BattleUnit>(out var bu))
            {
                apBarController.MaxValue = bu.MaxAP;
                apBarController.CurrentValue = bu.CurAP;
            }
        }
    }
}