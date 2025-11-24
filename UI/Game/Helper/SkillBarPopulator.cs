using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;
using Game.Battle.Abilities;

public class SkillBarPopulator : MonoBehaviour
{
    const int MaxSlots = 8;
    public System.Action<int> OnSkillClicked;

    [Header("Scene Refs")]
    public Transform hotBarRoot;

    [Header("描边/阴影偏移")]
    public Vector2 outlineOffset = new Vector2(-1f, 1f);
    public Vector2 shadowOffset = new Vector2(3f, -3f);

    [Header("敌方/锁定样式 (Enemy Style)")]
    // ⭐ 修改：使用淡红色而不是暗色，保证可见度
    public Color enemyIconTint = new Color(1f, 0.85f, 0.85f, 1f);
    public Color enemyOutlineCol = new Color(0.6f, 0.2f, 0.2f, 1f);
    public Color enemyShadowCol = new Color(0.2f, 0.05f, 0.05f, 0.6f);
    public Color enemyGlowCol = new Color(0.8f, 0.0f, 0.0f, 0f);
    public float enemyGlowAlpha = 0.0f; // 默认不发光，减少视觉干扰

    [Header("Icon 颜色（友军类型）")]
    // Physical / AP
    public Color physicalIconTint = new Color32(0xE8, 0xE3, 0xF7, 0xFF);
    public Color physicalOutlineCol = new Color(0.541f, 0.525f, 0.647f, 0.70f);
    public Color physicalShadowCol = new Color(0.165f, 0.152f, 0.220f, 0.55f);

    // Magic / MP
    public Color magicIconTint = new Color32(0xEE, 0xF3, 0xFF, 0xFF);
    public Color magicOutlineCol = new Color(0.78f, 0.89f, 1.00f, 0.70f);
    public Color magicShadowCol = new Color(0.09f, 0.13f, 0.21f, 0.55f);

    // Mixed
    public Color mixedIconTint = new Color32(0xE7, 0xF2, 0xFF, 0xFF);
    public Color mixedOutlineCol = new Color(0.7647f, 0.8353f, 0.9529f, 0.70f);
    public Color mixedShadowCol = new Color(0.15f, 0.20f, 0.28f, 0.55f);

    [Header("Glow（柔光高光）颜色与强度")]
    public Material glowMaterial;
    public float glowScale = 1.10f;

    // 基础透明度
    public float glowAlphaPhysical = 0.10f;
    public float glowAlphaMagic = 0.12f;
    public float glowAlphaMixed = 0.11f;

    // 交互增加透明度
    public float glowHoverAdd = 0.10f;
    public float glowSelectedAdd = 0.18f;

    // Glow 颜色
    public Color glowColorPhysical = new Color32(0xD9, 0xD3, 0xF3, 0xFF);
    public Color glowColorMagic = new Color32(0x73, 0xB6, 0xFF, 0xFF);
    public Color glowColorMixed = new Color32(0xE7, 0xF2, 0xFF, 0xFF);

    [Header("技能数据（最多 8 个）")]
    public List<Ability> abilities = new List<Ability>(MaxSlots);

    // 状态记录
    struct SlotState { public bool hover; public bool selected; }
    private readonly Dictionary<int, SlotState> _slotStates = new Dictionary<int, SlotState>();

    // 锁定状态 (是否选中了敌人)
    private bool _isLocked = false;

    public void SetLockedState(bool locked)
    {
        if (_isLocked != locked)
        {
            _isLocked = locked;
            Populate();
        }
    }

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

    public void SetHover(int index, bool on)
    {
        // 如果是敌人，不响应 Hover 变色，避免玩家误以为可点
        if (_isLocked) return;

        if (!_slotStates.TryGetValue(index, out var st)) st = new SlotState();
        st.hover = on; _slotStates[index] = st;
        UpdateGlowForSlot(index);
    }

    public void SetSelected(int index, bool on)
    {
        if (_isLocked) return;

        if (!_slotStates.TryGetValue(index, out var st)) st = new SlotState();
        st.selected = on; _slotStates[index] = st;
        UpdateGlowForSlot(index);
    }

    // -------------------------------------------------------------

    void SetupSlot(int index, Ability ability)
    {
        var iconRoot = FindIconRoot(index);
        if (iconRoot == null) return;

        // 禁用旧 Glow
        var oldGlow = iconRoot.Find("GlowLayer");
        if (oldGlow != null && oldGlow.gameObject.activeSelf) oldGlow.gameObject.SetActive(false);

        // 获取组件
        var iconImg = GetOrCreateChildImage(iconRoot, "ICON");
        iconImg.raycastTarget = false;
        SetRectToStretch(iconImg.rectTransform);

        var glowImg = GetOrCreateChildImage(iconRoot, "HL_Glow");
        glowImg.raycastTarget = false;
        SetRectToStretch(glowImg.rectTransform);
        if (glowMaterial != null) glowImg.material = glowMaterial;
        glowImg.rectTransform.localScale = Vector3.one * glowScale;

        // === ⭐ 新增逻辑：控制 Hotkey 显隐 ===
        // 只有在有技能 且 不是锁定状态(敌人) 时，才显示热键(1,2,3...)
        Transform hotkeyRoot = FindHotkeyRoot(index);
        if (hotkeyRoot != null)
        {
            bool showHotkey = (ability != null) && !_isLocked;
            hotkeyRoot.gameObject.SetActive(showHotkey);
        }
        // =================================

        // 1. 准备颜色变量
        string typeName = GetAbilityTypeName(ability);
        Color tint, outCol, shaCol, glowCol;
        float baseAlpha;

        // 2. 核心分支：敌方 vs 友军
        if (_isLocked)
        {
            // === 敌方样式 ===
            tint = enemyIconTint;
            outCol = enemyOutlineCol;
            shaCol = enemyShadowCol;
            glowCol = enemyGlowCol;
            baseAlpha = enemyGlowAlpha;

            // 强制显示 Enemy Gem
            ToggleGems(iconRoot.parent, "Enemy");
        }
        else
        {
            // === 友军样式 ===
            GetColorsForType(typeName, out tint, out outCol, out shaCol, out glowCol, out baseAlpha);
            ToggleGems(iconRoot.parent, ability != null ? typeName : null);
        }

        // 3. 应用到组件
        var outline = iconImg.GetComponent<Outline>();
        if (outline == null) outline = iconImg.gameObject.AddComponent<Outline>();

        var shadow = iconImg.GetComponent<Shadow>();
        if (shadow == null) shadow = iconImg.gameObject.AddComponent<Shadow>();

        if (ability != null && ability.icon != null)
        {
            iconImg.enabled = true;
            iconImg.sprite = ability.icon;
            iconImg.type = Image.Type.Simple;
            iconImg.preserveAspect = true;
            iconImg.color = tint; // 这里的 tint 现在是淡红色，而不是灰色

            outline.enabled = true;
            outline.effectColor = outCol;
            outline.effectDistance = outlineOffset;
            outline.useGraphicAlpha = true;

            shadow.enabled = true;
            shadow.effectColor = shaCol;
            shadow.effectDistance = shadowOffset;
            shadow.useGraphicAlpha = true;

            glowImg.enabled = true;
            glowImg.color = new Color(glowCol.r, glowCol.g, glowCol.b, baseAlpha);
        }
        else
        {
            iconImg.enabled = false;
            outline.enabled = false;
            shadow.enabled = false;
            glowImg.enabled = false;
            ToggleGems(iconRoot.parent, null);
        }

        if (!_slotStates.ContainsKey(index)) _slotStates[index] = new SlotState();
        UpdateGlowForSlot(index);

        // 4. 按钮点击逻辑
        Transform slotTransform = hotBarRoot.Find($"Item_{index:00}");
        if (slotTransform != null)
        {
            Button btn = slotTransform.GetComponent<Button>();
            if (btn == null) btn = slotTransform.gameObject.AddComponent<Button>();

            // ⭐ 关键修改：始终保持 interactable = true，防止图标变黑
            btn.interactable = true;

            // 如果想彻底禁用 Unity 的默认 Disabled 变色效果，可以修改 Transition
            if (btn.transition == Selectable.Transition.ColorTint)
            {
                var cols = btn.colors;
                // 将 disabledColor 设为白色或透明，避免 Unity 自动叠加灰色
                cols.disabledColor = Color.white;
                btn.colors = cols;
            }

            // 清除旧事件，绑定新事件
            btn.onClick.RemoveAllListeners();
            if (ability != null)
            {
                btn.onClick.AddListener(() =>
                {
                    // ⭐ 逻辑拦截：只有非锁定时才响应点击
                    if (!_isLocked) OnSkillClicked?.Invoke(index);
                });
            }
        }
    }

    void UpdateGlowForSlot(int index)
    {
        var iconRoot = FindIconRoot(index);
        if (iconRoot == null) return;
        var glow = iconRoot.Find("HL_Glow")?.GetComponent<Image>();
        if (glow == null || !glow.enabled) return;

        if (_isLocked)
        {
            var cLocked = glow.color;
            cLocked.a = enemyGlowAlpha;
            glow.color = cLocked;
            return;
        }

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
        var gEnemy = itemInner.Find("SPR_Enemy_Gem");

        if (typeNameOrNull == null)
        {
            if (gPhy) gPhy.gameObject.SetActive(false);
            if (gMag) gMag.gameObject.SetActive(false);
            if (gMix) gMix.gameObject.SetActive(false);
            if (gEnemy && gEnemy.gameObject) gEnemy.gameObject.SetActive(false);
            return;
        }

        string t = typeNameOrNull.ToLower();
        bool isEnemy = t == "enemy";
        bool onPhy = !isEnemy && t == "physical";
        bool onMag = !isEnemy && (t == "magic" || t == "magical");
        bool onMix = !isEnemy && t == "mixed";

        if (gPhy) gPhy.gameObject.SetActive(onPhy);
        if (gMag) gMag.gameObject.SetActive(onMag);
        if (gMix) gMix.gameObject.SetActive(onMix);
        if (gEnemy) gEnemy.gameObject.SetActive(isEnemy);
    }

    // 查找 Icon 节点: HotBar/Item_xx/Item/Icon
    Transform FindIconRoot(int index)
    {
        string itemName = $"Item_{index:00}";
        var item = hotBarRoot != null ? hotBarRoot.Find(itemName) : null;
        if (item == null) return null;
        var inner = item.Find("Item");
        if (inner == null) return null;
        return inner.Find("Icon");
    }

    // ⭐ 新增：查找 Hotkey 节点
    Transform FindHotkeyRoot(int index)
    {
        string itemName = $"Item_{index:00}";
        var item = hotBarRoot != null ? hotBarRoot.Find(itemName) : null;
        if (item == null) return null;
        var inner = item.Find("Item");
        if (inner == null) return null;
        return inner.Find("Input_Hotkey");
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