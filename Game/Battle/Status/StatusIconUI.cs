using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Game.Battle.Status;

namespace Game.UI
{
    public class StatusIconUI : MonoBehaviour
    {
        [Header("References (Auto-found if empty)")]
        public Image iconImage;
        public TMP_Text labelDuration; // 对应 Label_RemainingTurn

        public void Initialize(RuntimeStatus status)
        {
            // 1. 自动查找引用 (如果你不想手动拖拽)
            if (iconImage == null)
                iconImage = transform.Find("Icon/ICON")?.GetComponent<Image>();

            if (labelDuration == null)
                labelDuration = transform.Find("Label_RemainingTurn")?.GetComponent<TMP_Text>();

            if (status == null || status.Definition == null) return;

            // 2. 设置图标
            if (iconImage != null)
            {
                iconImage.sprite = status.Definition.icon;
                // 如果你的图标是白色的，可以用 effectColor 染色
                // iconImage.color = status.Definition.effectColor; 
            }

            // 3. 设置文本 (优先显示层数，如果层数是1则显示持续时间，或者两者结合)
            if (labelDuration != null)
            {
                // 逻辑 A: 夜烬/星蚀类 (层数是核心) -> 显示层数
                // 逻辑 B: 普通 Buff (时间是核心) -> 显示时间

                // 这里我写一个通用的：
                // 如果层数 > 1，显示层数 (x5)
                // 否则显示持续时间 (3)
                // 如果是永久，显示 ∞

                string text = "";
                if (status.Stacks > 1)
                {
                    text = $"x{status.Stacks}";
                }
                else
                {
                    if (status.Definition.isPermanent) text = "∞";
                    else text = status.DurationLeft.ToString();
                }

                labelDuration.text = text;
            }
        }
    }
}