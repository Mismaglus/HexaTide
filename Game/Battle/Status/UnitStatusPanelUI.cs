using System.Collections.Generic;
using UnityEngine;
using Game.Battle;
using Game.Battle.Status;
using Game.Units;
namespace Game.UI
{
    public class UnitStatusPanelUI : MonoBehaviour
    {
        [Header("Prefabs")]
        public GameObject buffPrefab;
        public GameObject debuffPrefab;

        [Header("Container")]
        public Transform iconContainer; // 确保这个物体上有 HorizontalLayoutGroup

        private UnitStatusController _currentStatusCtrl;

        // 由 UnitFrameUI 调用
        public void Bind(Unit unit)
        {
            // 1. 清理旧的监听
            if (_currentStatusCtrl != null)
            {
                _currentStatusCtrl.OnStatusChanged -= Refresh;
                _currentStatusCtrl = null;
            }

            // 2. 绑定新的
            if (unit != null)
            {
                _currentStatusCtrl = unit.GetComponent<UnitStatusController>();
                if (_currentStatusCtrl != null)
                {
                    _currentStatusCtrl.OnStatusChanged += Refresh;
                }
            }

            // 3. 立即刷新
            Refresh();
        }

        void OnDestroy()
        {
            if (_currentStatusCtrl != null)
                _currentStatusCtrl.OnStatusChanged -= Refresh;
        }

        void Refresh()
        {
            // 清空旧图标
            foreach (Transform child in iconContainer)
            {
                Destroy(child.gameObject);
            }

            if (_currentStatusCtrl == null) return;

            // 生成新图标
            foreach (var status in _currentStatusCtrl.activeStatuses)
            {
                GameObject prefab = (status.Definition.type == StatusType.Buff) ? buffPrefab : debuffPrefab;

                // 如果没分那么细，就只用 buffPrefab
                if (prefab == null) prefab = buffPrefab;
                if (prefab == null) continue;

                var go = Instantiate(prefab, iconContainer);
                var ui = go.GetComponent<StatusIconUI>();
                if (ui != null)
                {
                    ui.Initialize(status);
                }
            }
        }
    }
}