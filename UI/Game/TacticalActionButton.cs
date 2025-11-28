using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Game.Battle;

namespace Game.UI
{
    [RequireComponent(typeof(Button))]
    public class TacticalActionButton : MonoBehaviour
    {
        [Header("References")]
        public Image iconImage;
        public TMP_Text labelText;

        [Header("Visuals")]
        public Color activeColor = new Color(1f, 0.8f, 0.2f, 1f); // 金色/激活
        public Color inactiveColor = Color.white;
        public string defaultLabel = "Sprint";

        private Button _btn;
        private SelectionManager _selectionManager;

        void Awake()
        {
            _btn = GetComponent<Button>();
            _btn.onClick.AddListener(OnClicked);
            _selectionManager = FindFirstObjectByType<SelectionManager>(FindObjectsInactive.Exclude);
        }

        void Start()
        {
            if (_selectionManager != null)
            {
                _selectionManager.OnTacticalStateChanged += HandleStateChanged;
                // 初始化状态
                HandleStateChanged(_selectionManager.IsTacticalMoveActive);
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
        }

        void HandleStateChanged(bool isActive)
        {
            if (iconImage) iconImage.color = isActive ? activeColor : inactiveColor;
            // 如果以后有不同职业技能，可以在这里根据 _selectionManager.SelectedUnit 动态改图标和名字
        }
    }
}