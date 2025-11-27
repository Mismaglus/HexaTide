using System.Collections.Generic;
using UnityEngine;
using Game.Battle.Status;
using Game.Units;

namespace Game.UI
{
    public class UnitStatusPanelUI : MonoBehaviour
    {
        [Header("Prefabs")]
        [Tooltip("背景是绿色的那个 Prefab")]
        public GameObject buffPrefab;

        [Tooltip("背景是红色的那个 Prefab")]
        public GameObject debuffPrefab;

        [Header("Container")]
        [Tooltip("图标生成的父节点 (通常就是挂此脚本的物体)")]
        public Transform iconContainer;

        private UnitStatusController _currentStatusCtrl;

        void Awake()
        {
            if (iconContainer == null) iconContainer = transform;
        }

        // 由 UnitFrameUI 调用
        public void Bind(Unit unit)
        {
            // 1. 清理旧的监听
            if (_currentStatusCtrl != null)
            {
                _currentStatusCtrl.OnStatusChanged -= Refresh;
                _currentStatusCtrl = null;
            }

            // 2. 绑定新单位
            if (unit != null)
            {
                _currentStatusCtrl = unit.GetComponent<UnitStatusController>();
                if (_currentStatusCtrl != null)
                {
                    _currentStatusCtrl.OnStatusChanged += Refresh;
                }
            }

            // 3. 立即刷新一次
            Refresh();
        }

        void OnDestroy()
        {
            if (_currentStatusCtrl != null)
                _currentStatusCtrl.OnStatusChanged -= Refresh;
        }

        void Refresh()
        {
            // A. 清空当前显示的图标
            foreach (Transform child in iconContainer)
            {
                Destroy(child.gameObject);
            }

            if (_currentStatusCtrl == null) return;

            // B. 遍历单位身上的所有状态并生成图标
            foreach (var status in _currentStatusCtrl.activeStatuses)
            {
                // 根据类型决定用哪个 Prefab
                GameObject prefabToUse = (status.Definition.type == StatusType.Buff) ? buffPrefab : debuffPrefab;

                // 兜底：如果没分配 Debuff Prefab，全用 Buff 样式
                if (prefabToUse == null) prefabToUse = buffPrefab;
                if (prefabToUse == null) continue;

                // 实例化
                var go = Instantiate(prefabToUse, iconContainer);

                // 初始化数据
                var ui = go.GetComponent<StatusIconUI>();
                if (ui != null)
                {
                    ui.Initialize(status);
                }
            }
        }
    }
}