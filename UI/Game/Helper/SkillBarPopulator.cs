using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;

// 假设你的 Ability 类型在这个命名空间；若不同可移除或改为 using。
using Game.Battle.Abilities;

public class SkillBarPopulator : MonoBehaviour
{
    const int MaxSlots = 8;

    [Header("Scene Refs")]
    public Transform hotBarRoot;

    [Header("描边/阴影偏移（默认使用你的数值）")]
    public Vector2 outlineOffset = new Vector2(-1f, 1f);
    public Vector2 shadowOffset = new Vector2(3f, -3f);

    [Header("Icon 颜色（按类型）")]
    // 注意：这里的 tint 只是让图标略微偏向对应资源色，不会像宝石那样纯色

    // Physical / AP（暮霭紫灰 Dusk Violet Gray）
    // 冷淡紫白 + 灰紫描边 + 深夜灰紫阴影
    public Color physicalIconTint = new Color32(0xE8, 0xE3, 0xF7, 0xFF); // #E8E3F7  淡紫冷白
    public Color physicalOutlineCol = new Color(0.541f, 0.525f, 0.647f, 0.70f); // #8A86A5, alpha 0.7
    public Color physicalShadowCol = new Color(0.165f, 0.152f, 0.220f, 0.55f); // #2A2738, alpha 0.55

    // Magic / MP（蓝色系）
    public Color magicIconTint = new Color32(0xEE, 0xF3, 0xFF, 0xFF); // #EEF3FF  近乎白，轻微偏蓝
    public Color magicOutlineCol = new Color(0.78f, 0.89f, 1.00f, 0.70f); // 约 #C7E3FF, alpha 0.7
    public Color magicShadowCol = new Color(0.09f, 0.13f, 0.21f, 0.55f); // 约 #171F35, alpha 0.55

    // Mixed（AP+MP 混合：星辉银系）
    // 图标偏冰银色，描边略亮一点的银蓝，阴影为深蓝灰，呼应宝石 #E7F2FF
    public Color mixedIconTint = new Color32(0xE7, 0xF2, 0xFF, 0xFF); // #E7F2FF  冰银色，高亮但不刺眼
    public Color mixedOutlineCol = new Color(0.7647f, 0.8353f, 0.9529f, 0.70f); // #C3D5F3, alpha 0.7
    public Color mixedShadowCol = new Color(0.15f, 0.20f, 0.28f, 0.55f);       // #273347, alpha 0.55

    [Header("Glow（柔光高光）颜色与强度")]
    public Material glowMaterial;
    public float glowScale = 1.10f;

    // 基础透明度（Idle）
    public float glowAlphaPhysical = 0.10f;  // AP
    public float glowAlphaMagic = 0.12f;  // MP
    public float glowAlphaMixed = 0.11f;  // Mixed

    // 悬停/选中额外透明度
    public float glowHoverAdd = 0.10f;
    public float glowSelectedAdd = 0.18f;

    // Glow 颜色与宝石 / 条形资源色保持一致：
    // AP：#3BD56A   MP：#73B6FF   Mixed：#E7F2FF（星辉银）
    public Color glowColorPhysical = new Color32(0xD9, 0xD3, 0xF3, 0xFF); // #D9D3F3
    public Color glowColorMagic = new Color32(0x73, 0xB6, 0xFF, 0xFF); // #73B6FF  MP 蓝色
    public Color glowColorMixed = new Color32(0xE7, 0xF2, 0xFF, 0xFF); // #E7F2FF  星辉银

    [Header("技能数据（最多 8 个）")]
    public List<Ability> abilities = new List<Ability>(MaxSlots);

    // 记录每格的悬停/选中状态，用于计算 Glow alpha
    struct SlotState { public bool hover; public bool selected; }
    private readonly Dictionary<int, SlotState> _slotStates = new Dictionary<int, SlotState>();

    [ContextMenu("Populate Now")]
    public void Populate()
    {
        if (hotBarRoot == null)
        {
            Debug.LogError("[SkillBarPopulator] hotBarRoot 未指定。");
            return;
        }

        for (int i = 0; i < MaxSlots; i++)
        {
            var ability = (i < abilities.Count) ? abilities[i] : null;
            SetupSlot(i, ability);
        }
    }

    // 对外：鼠标悬停/选中状态（你可从 UI 事件里调用）
    public void SetHover(int index, bool on)
    {
        if (!_slotStates.TryGetValue(index, out var st)) st = new SlotState();
        st.hover = on; _slotStates[index] = st;
        UpdateGlowForSlot(index);
    }

    public void SetSelected(int index, bool on)
    {
        if (!_slotStates.TryGetValue(index, out var st)) st = new SlotState();
        st.selected = on; _slotStates[index] = st;
        UpdateGlowForSlot(index);
    }

    // -------------------------------------------------------------

    void SetupSlot(int index, Ability ability)
    {
        var iconRoot = FindIconRoot(index); // HotBar/Item_xx/Item/Icon
        if (iconRoot == null)
        {
            Debug.LogWarning($"[SkillBarPopulator] 未找到槽位 {index:00} 的 Icon 节点。期望路径：HotBar/Item_{index:00}/Item/Icon");
            return;
        }

        // 禁用旧的 GlowLayer（若存在）
        var oldGlow = iconRoot.Find("GlowLayer");
        if (oldGlow != null && oldGlow.gameObject.activeSelf) oldGlow.gameObject.SetActive(false);

        // ICON
        var iconImg = GetOrCreateChildImage(iconRoot, "ICON");
        iconImg.raycastTarget = false;
        SetRectToStretch(iconImg.rectTransform);

        // HL_Glow
        var glowImg = GetOrCreateChildImage(iconRoot, "HL_Glow");
        glowImg.raycastTarget = false;
        SetRectToStretch(glowImg.rectTransform);
        if (glowMaterial != null) glowImg.material = glowMaterial;
        // 略微放大
        glowImg.rectTransform.localScale = Vector3.one * glowScale;

        // 类型映射
        string typeName = GetAbilityTypeName(ability); // Physical/Magic/Mixed/Null->Mixed
        Color tint, outCol, shaCol, glowCol; float baseAlpha;
        GetColorsForType(typeName, out tint, out outCol, out shaCol, out glowCol, out baseAlpha);

        // 宝石显隐（在 Item 层）
        var itemInner = iconRoot.parent; // Item
        ToggleGems(itemInner, ability != null ? typeName : null);

        // 设置 ICON 与描边/阴影
        var outline = iconImg.GetComponent<Outline>();
        var shadow = iconImg.GetComponent<Shadow>();

        if (ability != null && ability.icon != null)
        {
            iconImg.enabled = true;
            iconImg.sprite = ability.icon;
            iconImg.type = Image.Type.Simple;
            iconImg.preserveAspect = true;
            iconImg.color = tint;

            if (outline == null) outline = iconImg.gameObject.AddComponent<Outline>();
            outline.effectColor = outCol;
            outline.effectDistance = outlineOffset;
            outline.useGraphicAlpha = true;
            outline.enabled = true;

            if (shadow == null) shadow = iconImg.gameObject.AddComponent<Shadow>();
            shadow.effectColor = shaCol;
            shadow.effectDistance = shadowOffset;
            shadow.useGraphicAlpha = true;
            shadow.enabled = true;

            // Glow 颜色与基础 Alpha
            glowImg.enabled = true;
            var c = glowCol; c.a = baseAlpha;
            glowImg.color = c;
        }
        else
        {
            iconImg.enabled = false;
            if (outline) outline.enabled = false;
            if (shadow) shadow.enabled = false;

            glowImg.enabled = false;
            ToggleGems(itemInner, null);
        }

        // 初始化状态表
        if (!_slotStates.ContainsKey(index)) _slotStates[index] = new SlotState();
        UpdateGlowForSlot(index);
    }

    void UpdateGlowForSlot(int index)
    {
        var iconRoot = FindIconRoot(index);
        if (iconRoot == null) return;
        var glow = iconRoot.Find("HL_Glow")?.GetComponent<Image>();
        if (glow == null || !glow.enabled) return;

        // 当前类型决定基础 alpha
        string typeName = GetAbilityTypeName((index < abilities.Count) ? abilities[index] : null);
        float baseAlpha = GetGlowBaseAlpha(typeName);

        _slotStates.TryGetValue(index, out var st);
        float a = baseAlpha;
        if (st.hover) a += glowHoverAdd;
        if (st.selected) a += glowSelectedAdd;
        a = Mathf.Clamp01(a);

        var c = glow.color; c.a = a; glow.color = c;
    }

    // -------------------------------------------------------------

    void GetColorsForType(string typeName, out Color tint, out Color outline, out Color shadow, out Color glow, out float baseGlowAlpha)
    {
        // 默认 Mixed
        tint = mixedIconTint;
        outline = mixedOutlineCol;
        shadow = mixedShadowCol;
        glow = glowColorMixed;
        baseGlowAlpha = glowAlphaMixed;

        if (string.IsNullOrEmpty(typeName)) return;

        switch (typeName.ToLower())
        {
            case "physical":
                tint = physicalIconTint;
                outline = physicalOutlineCol;
                shadow = physicalShadowCol;
                glow = glowColorPhysical;
                baseGlowAlpha = glowAlphaPhysical;
                break;

            case "magic":
            case "magical":
                tint = magicIconTint;
                outline = magicOutlineCol;
                shadow = magicShadowCol;
                glow = glowColorMagic;
                baseGlowAlpha = glowAlphaMagic;
                break;

            case "mixed":
                // 使用默认 mixed
                break;
        }
    }

    float GetGlowBaseAlpha(string typeName)
    {
        if (string.IsNullOrEmpty(typeName)) return glowAlphaMixed;
        switch (typeName.ToLower())
        {
            case "physical": return glowAlphaPhysical;
            case "magic":
            case "magical": return glowAlphaMagic;
            default: return glowAlphaMixed;
        }
    }

    void ToggleGems(Transform itemInner, string typeNameOrNull)
    {
        if (itemInner == null) return;
        var gPhy = itemInner.Find("SPR_Phy_Gem");
        var gMag = itemInner.Find("SPR_Mag_Gem");
        var gMix = itemInner.Find("SPR_Mix_Gem");

        if (typeNameOrNull == null)
        {
            if (gPhy) gPhy.gameObject.SetActive(false);
            if (gMag) gMag.gameObject.SetActive(false);
            if (gMix) gMix.gameObject.SetActive(false);
            return;
        }

        string t = typeNameOrNull.ToLower();
        bool onPhy = t == "physical";
        bool onMag = t == "magic" || t == "magical";
        bool onMix = t == "mixed";

        if (gPhy) gPhy.gameObject.SetActive(onPhy);
        if (gMag) gMag.gameObject.SetActive(onMag);
        if (gMix) gMix.gameObject.SetActive(onMix);
    }

    Transform FindIconRoot(int index)
    {
        string itemName = $"Item_{index:00}";
        var item = hotBarRoot != null ? hotBarRoot.Find(itemName) : null;
        if (item == null) return null;
        var inner = item.Find("Item");
        if (inner == null) return null;
        return inner.Find("Icon");
    }

    Image GetOrCreateChildImage(Transform parent, string childName)
    {
        var t = parent.Find(childName);
        if (t == null)
        {
            var go = new GameObject(childName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            t = go.transform;
            t.SetParent(parent, false);
            var rt = (RectTransform)t;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = Vector2.zero;
        }
        return t.GetComponent<Image>();
    }

    void SetRectToStretch(RectTransform rt)
    {
        if (rt == null) return;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.localScale = Vector3.one;
        rt.localRotation = Quaternion.identity;
    }

    // 尝试通过常见字段/属性名获取类型字符串
    string GetAbilityTypeName(Ability ability)
    {
        if (ability == null) return "Mixed";
        string[] names = {
            "abilityType","AbilityType","type","Type",
            "classification","Classification","skillType","SkillType"
        };
        var tp = ability.GetType();
        foreach (var n in names)
        {
            var prop = tp.GetProperty(n, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
            if (prop != null)
            {
                var v = prop.GetValue(ability, null);
                if (v != null) return v.ToString();
            }
            var field = tp.GetField(n, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
            if (field != null)
            {
                var v = field.GetValue(ability);
                if (v != null) return v.ToString();
            }
        }
        return "Mixed";
    }
}
