using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Game.Battle.Status;

namespace Game.UI
{
    public class StatusIconUI : MonoBehaviour
    {
        [Header("UI References")]
        // 你可以在 Inspector 里拖进去，也可以留空让代码自动找
        public Image iconImage;
        public TMP_Text labelDuration;

        public void Initialize(RuntimeStatus status)
        {
            if (status == null || status.Definition == null) return;

            // 1. 自动查找组件 (匹配你的 Prefab 结构)
            if (iconImage == null)
                iconImage = transform.Find("Icon/ICON")?.GetComponent<Image>();

            if (labelDuration == null)
                labelDuration = transform.Find("Label_RemainingTurn")?.GetComponent<TMP_Text>();

            // 2. 更新图标 (核心需求：从 Asset 读取 Icon)
            if (iconImage != null)
            {
                iconImage.sprite = status.Definition.icon;

                // 可选：如果你的图标是白模，可以使用配置的颜色染色
                // iconImage.color = status.Definition.effectColor;
            }

            // 3. 更新文字 (层数 vs 持续时间)
            if (labelDuration != null)
            {
                string text = "";

                // 逻辑：优先显示层数 (如 x5)，如果是 1 层则显示持续回合 (3)
                // 你可以根据需求修改这个显示逻辑
                if (status.Stacks > 1)
                {
                    text = $"{status.Stacks}"; // 或者 "x" + status.Stacks
                }
                else
                {
                    if (status.Definition.isPermanent)
                        text = ""; // 永久状态不显示数字，或者显示 "∞"
                    else
                        text = status.DurationLeft.ToString();
                }

                labelDuration.text = text;
                // 如果没字就隐藏文本框，保持整洁
                labelDuration.gameObject.SetActive(!string.IsNullOrEmpty(text));
            }
        }
    }
}