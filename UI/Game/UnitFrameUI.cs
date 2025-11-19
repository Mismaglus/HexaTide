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
        public GenericBarController mpBarController;

        [Header("System")]
        // 移除在这里的自动查找，等待注入
        public SelectionManager selectionManager;
        public GameObject contentRoot;

        private Unit _currentUnit;

        // 删除 void Awake() {...} 
        // 删除 void OnEnable() {...} 
        // 因为如果不通过 Initialize 注入，OnEnable 里 selectionManager 也是空的

        // ⭐ 新增初始化方法
        public void Initialize(SelectionManager manager)
        {
            selectionManager = manager;

            if (selectionManager != null)
            {
                // 1. 订阅事件
                selectionManager.OnSelectedUnitChanged -= HandleSelectionChanged;
                selectionManager.OnSelectedUnitChanged += HandleSelectionChanged;

                // 2. 立即刷新一次（以防已经选中了什么）
                HandleSelectionChanged(selectionManager.SelectedUnit);

                Debug.Log("[UnitFrameUI] 初始化成功，已连接 SelectionManager");
            }
            else
            {
                Debug.LogError("[UnitFrameUI] 收到了空的 SelectionManager！");
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

        // ... 下面的 HandleSelectionChanged 和 RefreshDynamicValues 保持不变 ...
        // (为了节省篇幅，这里不重复粘贴，请保留你刚才修改过的反转血条逻辑)

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

            if (_currentUnit.TryGetComponent<UnitAttributes>(out var attrs))
            {
                float currentHP = attrs.Core.HP;
                float maxHP = attrs.Core.HPMax;
                float damage = maxHP - currentHP; // 你的反转逻辑

                if (hpSlider != null) { hpSlider.maxValue = maxHP; hpSlider.value = damage; }
                if (hpText != null) { hpText.text = $"{currentHP}/{maxHP}"; }
            }

            if (apBarController != null && _currentUnit.TryGetComponent<BattleUnit>(out var bu))
            {
                apBarController.MaxValue = bu.MaxAP;
                apBarController.CurrentValue = bu.CurAP;
            }
        }
    }
}