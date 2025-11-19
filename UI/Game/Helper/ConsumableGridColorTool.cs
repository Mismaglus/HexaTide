using System.Linq;
using UnityEngine;
using UnityEngine.UI;

public class ConsumableGridColorTool : MonoBehaviour
{
    [Header("Target Color")]
    [SerializeField] private Color targetColor = new Color32(0x22, 0x23, 0x25, 0xFF);

    [Header("Name Filters (empty = all Images)")]
    [SerializeField] private string[] nameContainsFilters = new[] { "SPR_Background" };

    [SerializeField] private bool includeInactive = true;
    [SerializeField] private bool includeDisabledImages = true;
    [SerializeField] private bool keepOriginalAlpha = false;

    public int ApplyColor()
    {
        var images = GetComponentsInChildren<Image>(includeInactive);

        if (nameContainsFilters != null && nameContainsFilters.Length > 0)
        {
            images = images
                .Where(img =>
                    nameContainsFilters.Any(f =>
                        !string.IsNullOrEmpty(f) &&
                        img.name.IndexOf(f, System.StringComparison.OrdinalIgnoreCase) >= 0))
                .ToArray();
        }

        int count = 0;
        foreach (var img in images)
        {
            if (!includeDisabledImages && !img.enabled) continue;

            var c = targetColor;
            if (keepOriginalAlpha) c.a = img.color.a;

            img.color = c;
            count++;
        }
        return count;
    }

    // 右键组件标题或三点菜单可见
    [ContextMenu("Apply Color Now")]
    private void ContextApply() => ApplyColor();

    // 暂时暴露修改接口（如果你想从别的脚本调用）
    public void SetTargetColor(Color c, bool runNow = false)
    {
        targetColor = c;
        if (runNow) ApplyColor();
    }
}
