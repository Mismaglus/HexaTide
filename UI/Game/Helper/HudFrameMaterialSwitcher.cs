using UnityEngine;
using UnityEngine.UI;

public class HudFrameMaterialSwitcher : MonoBehaviour
{
    public enum HudTheme
    {
        Default = 0,
        Star = 1,
        Moon = 2,
        Night = 3,
        Chimera = 4
    }

    [Header("Frame graphics (two SPR_Frame, etc.)")]
    [SerializeField] private Graphic[] frameGraphics;

    [Header("5 materials for themes (index must match enum order)")]
    [SerializeField] private Material[] themeMaterials = new Material[5];

    [Header("Skill bar roots per theme (only one active per theme)")]
    [SerializeField] private GameObject[] skillBarVariants = new GameObject[5];

    [Header("HealthStats sprite roots per theme (only one active per theme)")]
    [SerializeField] private GameObject[] healthStatsVariants = new GameObject[5];

    [Header("Current theme")]
    [SerializeField] private HudTheme currentTheme = HudTheme.Default;

    private void Awake()
    {
        ApplyCurrentTheme();
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        ApplyCurrentTheme();
    }
#endif

    public void ApplyCurrentTheme()
    {
        SetTheme(currentTheme);
    }

    public void SetTheme(HudTheme theme)
    {
        int index = (int)theme;

        // 1. Update frame materials
        if (themeMaterials != null &&
            index >= 0 && index < themeMaterials.Length &&
            themeMaterials[index] != null)
        {
            var mat = themeMaterials[index];
            foreach (var g in frameGraphics)
            {
                if (g == null) continue;
                g.material = mat;
            }
        }
        else
        {
            Debug.LogWarning($"HudFrameMaterialSwitcher: themeMaterials[{index}] is invalid, check array length and assignments.");
        }

        // 2. Enable/disable skill bar variants
        ToggleVariantGroup(skillBarVariants, index, "SkillBar");

        // 3. Enable/disable health stats variants
        ToggleVariantGroup(healthStatsVariants, index, "HealthStats");

        currentTheme = theme;
    }

    public void SetThemeByIndex(int index)
    {
        if (index < 0 || index > (int)HudTheme.Chimera)
        {
            Debug.LogWarning($"HudFrameMaterialSwitcher: theme index {index} out of range.");
            return;
        }

        SetTheme((HudTheme)index);
    }

    private void ToggleVariantGroup(GameObject[] group, int activeIndex, string groupName)
    {
        if (group == null || group.Length == 0) return;

        for (int i = 0; i < group.Length; i++)
        {
            var go = group[i];
            if (go == null) continue;

            bool shouldBeActive = (i == activeIndex);
            if (go.activeSelf != shouldBeActive)
            {
                go.SetActive(shouldBeActive);
            }
        }

        if (activeIndex >= group.Length)
        {
            Debug.LogWarning($"HudFrameMaterialSwitcher: {groupName} group has length {group.Length}, but theme index is {activeIndex}. Some themes will not have a variant.");
        }
    }
}
