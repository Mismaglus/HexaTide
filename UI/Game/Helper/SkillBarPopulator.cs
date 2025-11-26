using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems; // 必须引用，用于鼠标悬停事件
using Game.Battle.Abilities;
using Game.UI; // 引用 SkillTooltipController

public class SkillBarPopulator : MonoBehaviour
{
    const int MaxSlots = 8;
    public System.Action<int> OnSkillClicked;

    [Header("Scene Refs")]
    public Transform hotBarRoot;

    // Tooltip 控制器引用 (Awake 会自动找，也可以手动拖)
    public SkillTooltipController tooltipController;

    // 当前持有的单位 (由 SkillBarController 赋值)
    [HideInInspector] public Game.Battle.BattleUnit currentOwner;

    [Header("Tracery Sprites (纹饰图标)")]
    public Sprite traceryPhysical;
    public Sprite traceryMagic;
    public Sprite traceryMixed;
    public Sprite traceryEnemy;

    [Header("Visual Settings")]
    public Vector2 outlineOffset = new Vector2(-1f, 1f);
    public Vector2 shadowOffset = new Vector2(3f, -3f);

    [Header("Enemy Style")]
    public Color enemyIconTint = new Color(1f, 0.85f, 0.85f, 1f);
    public Color enemyOutlineCol = new Color(0.6f, 0.2f, 0.2f, 1f);
    public Color enemyShadowCol = new Color(0.2f, 0.05f, 0.05f, 0.6f);
    public Color enemyGlowCol = new Color(0.8f, 0.0f, 0.0f, 0f);
    public float enemyGlowAlpha = 0.0f;

    [Header("Ally Colors")]
    public Color physicalIconTint = new Color32(0xE8, 0xE3, 0xF7, 0xFF);
    public Color physicalOutlineCol = new Color(0.541f, 0.525f, 0.647f, 0.70f);
    public Color physicalShadowCol = new Color(0.165f, 0.152f, 0.220f, 0.55f);

    public Color magicIconTint = new Color32(0xEE, 0xF3, 0xFF, 0xFF);
    public Color magicOutlineCol = new Color(0.78f, 0.89f, 1.00f, 0.70f);
    public Color magicShadowCol = new Color(0.09f, 0.13f, 0.21f, 0.55f);

    public Color mixedIconTint = new Color32(0xE7, 0xF2, 0xFF, 0xFF);
    public Color mixedOutlineCol = new Color(0.7647f, 0.8353f, 0.9529f, 0.70f);
    public Color mixedShadowCol = new Color(0.15f, 0.20f, 0.28f, 0.55f);

    [Header("Glow")]
    public Material glowMaterial;
    public float glowScale = 1.10f;
    public float glowAlphaPhysical = 0.10f;
    public float glowAlphaMagic = 0.12f;
    public float glowAlphaMixed = 0.11f;
    public float glowHoverAdd = 0.10f;
    public float glowSelectedAdd = 0.18f;
    public Color glowColorPhysical = new Color32(0xD9, 0xD3, 0xF3, 0xFF);
    public Color glowColorMagic = new Color32(0x73, 0xB6, 0xFF, 0xFF);
    public Color glowColorMixed = new Color32(0xE7, 0xF2, 0xFF, 0xFF);

    [Header("Data")]
    public List<Ability> abilities = new List<Ability>(MaxSlots);

    // Internal State
    struct SlotState { public bool hover; public bool selected; }
    private readonly Dictionary<int, SlotState> _slotStates = new Dictionary<int, SlotState>();
    private bool _isLocked = false;

    void Awake()
    {
        // 自动查找 Tooltip
        if (tooltipController == null)
            tooltipController = FindFirstObjectByType<SkillTooltipController>(FindObjectsInactive.Include);
    }

    // =========================================================

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
            Debug.LogError("【SkillBarPopulator】HotBarRoot 未赋值！");
            return;
        }

        if (abilities == null) abilities = new List<Ability>();

        // 循环处理
        for (int i = 0; i < MaxSlots; i++)
        {
            var ability = (i < abilities.Count) ? abilities[i] : null;
            try
            {
                SetupSlot(i, ability);
            }
            catch (System.Exception e)
            {
                Debug.LogError($"Slot {i} Setup Failed: {e.Message}");
            }
        }
    }

    // 外部或 EventTrigger 调用的悬停方法
    public void SetHover(int index, bool on)
    {
        if (!_slotStates.TryGetValue(index, out var st)) st = new SlotState();
        st.hover = on; _slotStates[index] = st;
        UpdateGlowForSlot(index);

        // ⭐ Tooltip 触发逻辑
        if (tooltipController != null)
        {
            if (on)
            {
                var ability = (index >= 0 && index < abilities.Count) ? abilities[index] : null;
                if (ability != null)
                {
                    var slotTrans = GetSlotTransform(index);
                    var rect = slotTrans != null ? slotTrans.GetComponent<RectTransform>() : null;
                    // 显示 Tooltip
                    tooltipController.Show(ability, currentOwner, _isLocked, rect);
                }
            }
            else
            {
                tooltipController.Hide();
            }
        }
    }

    public void SetSelected(int index, bool on)
    {
        if (_isLocked) return;
        if (!_slotStates.TryGetValue(index, out var st)) st = new SlotState();
        st.selected = on; _slotStates[index] = st;
        UpdateGlowForSlot(index);
    }

    // =========================================================

    // 通过子物体索引查找，防止改名导致找不到
    Transform GetSlotTransform(int index)
    {
        if (hotBarRoot == null) return null;
        if (index >= 0 && index < hotBarRoot.childCount)
        {
            return hotBarRoot.GetChild(index);
        }
        return null;
    }

    void SetupSlot(int index, Ability ability)
    {
        // 1. 查找层级
        Transform slotTransform = GetSlotTransform(index);
        if (slotTransform == null) return;

        Transform itemInner = slotTransform.Find("Item");
        if (itemInner == null) return;

        Transform iconRoot = itemInner.Find("Icon");
        if (iconRoot == null) return;

        // 2. 基础清理 & 获取组件
        var oldGlow = iconRoot.Find("GlowLayer");
        if (oldGlow != null && oldGlow.gameObject.activeSelf) oldGlow.gameObject.SetActive(false);

        var iconImg = GetOrCreateChildImage(iconRoot, "ICON");
        iconImg.raycastTarget = false; // 图标不阻挡射线，让父物体 Button 响应
        SetRectToStretch(iconImg.rectTransform);

        var glowImg = GetOrCreateChildImage(iconRoot, "HL_Glow");
        glowImg.raycastTarget = false;
        SetRectToStretch(glowImg.rectTransform);
        if (glowMaterial != null) glowImg.material = glowMaterial;
        glowImg.rectTransform.localScale = Vector3.one * glowScale;

        // 3. Hotkey 显示
        Transform hotkeyRoot = itemInner.Find("Input_Hotkey");
        if (hotkeyRoot != null)
        {
            bool showHotkey = (ability != null) && !_isLocked;
            hotkeyRoot.gameObject.SetActive(showHotkey);
        }

        // 4. 准备颜色数据
        string typeName = GetAbilityTypeName(ability);
        Color tint, outCol, shaCol, glowCol;
        float baseAlpha;

        if (_isLocked)
        {
            tint = enemyIconTint;
            outCol = enemyOutlineCol;
            shaCol = enemyShadowCol;
            glowCol = enemyGlowCol;
            baseAlpha = enemyGlowAlpha;
            ToggleGems(itemInner, "Enemy");
        }
        else
        {
            GetColorsForType(typeName, out tint, out outCol, out shaCol, out glowCol, out baseAlpha);
            ToggleGems(itemInner, ability != null ? typeName : null);
        }

        // 5. 应用视觉样式
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
            iconImg.color = tint;

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
            ToggleGems(itemInner, null);
        }

        if (!_slotStates.ContainsKey(index)) _slotStates[index] = new SlotState();
        UpdateGlowForSlot(index);

        // 6. 更新纹饰 Tracery
        UpdateTracery(itemInner, ability, typeName);

        // 7. 按钮交互 (Button Interaction)
        Button btn = slotTransform.GetComponent<Button>();
        if (btn == null) btn = slotTransform.gameObject.AddComponent<Button>();

        // 只要有技能，Button 就是 interactable 的（这样才能触发 PointerEnter）
        bool hasSkill = (ability != null);
        btn.interactable = hasSkill;

        // 强制修改禁用颜色为纯白，防止自动变暗
        var colors = btn.colors;
        colors.disabledColor = Color.white;
        colors.colorMultiplier = 1f;
        btn.colors = colors;

        ApplyAnimationTriggers(btn, _isLocked);

        // 点击事件
        btn.onClick.RemoveAllListeners();
        if (hasSkill && !_isLocked)
        {
            btn.onClick.AddListener(() => { OnSkillClicked?.Invoke(index); });
        }

        // ⭐⭐⭐ 核心修复：手动添加 EventTrigger 监听 Hover ⭐⭐⭐
        // Unity Button 不会自动触发代码里的 SetHover，必须用 EventTrigger 桥接
        if (hasSkill)
        {
            EventTrigger trigger = btn.gameObject.GetComponent<EventTrigger>();
            if (trigger == null) trigger = btn.gameObject.AddComponent<EventTrigger>();
            trigger.triggers.Clear();

            // Pointer Enter -> SetHover(true)
            EventTrigger.Entry entryEnter = new EventTrigger.Entry { eventID = EventTriggerType.PointerEnter };
            entryEnter.callback.AddListener((data) => { SetHover(index, true); });
            trigger.triggers.Add(entryEnter);

            // Pointer Exit -> SetHover(false)
            EventTrigger.Entry entryExit = new EventTrigger.Entry { eventID = EventTriggerType.PointerExit };
            entryExit.callback.AddListener((data) => { SetHover(index, false); });
            trigger.triggers.Add(entryExit);
        }
    }

    // =========================================================
    // 辅助方法
    // =========================================================

    void UpdateTracery(Transform itemInner, Ability ability, string typeName)
    {
        if (itemInner == null) return;
        Transform traceryRoot = itemInner.Find("SPR_Tracery");
        if (traceryRoot == null) return;

        Image traceryImg = traceryRoot.GetComponent<Image>();
        if (traceryImg == null) return;

        if (ability == null)
        {
            traceryImg.enabled = false;
            return;
        }

        traceryImg.enabled = true;
        Sprite targetSprite = traceryMixed;

        if (_isLocked)
        {
            if (traceryEnemy != null) targetSprite = traceryEnemy;
        }
        else
        {
            if (!string.IsNullOrEmpty(typeName))
            {
                string t = typeName.ToLower();
                if (t.Contains("phys")) targetSprite = traceryPhysical;
                else if (t.Contains("magic") || t.Contains("magical")) targetSprite = traceryMagic;
                else targetSprite = traceryMixed;
            }
        }

        if (targetSprite != null)
        {
            traceryImg.sprite = targetSprite;
        }
    }

    void ApplyAnimationTriggers(Button btn, bool isLocked)
    {
        btn.transition = Selectable.Transition.Animation;
        var triggers = btn.animationTriggers;
        triggers.pressedTrigger = isLocked ? triggers.highlightedTrigger : triggers.selectedTrigger;
        btn.animationTriggers = triggers;
    }

    void UpdateGlowForSlot(int index)
    {
        var slot = GetSlotTransform(index);
        if (slot == null) return;
        var glow = slot.Find("Item/Icon/HL_Glow")?.GetComponent<Image>();
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

    void GetColorsForType(string typeName, out Color tint, out Color outline, out Color shadow, out Color glow, out float baseGlowAlpha)
    {
        tint = mixedIconTint;
        outline = mixedOutlineCol;
        shadow = mixedShadowCol;
        glow = glowColorMixed;
        baseGlowAlpha = glowAlphaMixed;

        if (string.IsNullOrEmpty(typeName)) return;
        string t = typeName.ToLower();

        if (t.Contains("phys"))
        {
            tint = physicalIconTint;
            outline = physicalOutlineCol;
            shadow = physicalShadowCol;
            glow = glowColorPhysical;
            baseGlowAlpha = glowAlphaPhysical;
        }
        else if (t.Contains("magic") || t.Contains("magical"))
        {
            tint = magicIconTint;
            outline = magicOutlineCol;
            shadow = magicShadowCol;
            glow = glowColorMagic;
            baseGlowAlpha = glowAlphaMagic;
        }
    }

    float GetGlowBaseAlpha(string typeName)
    {
        if (string.IsNullOrEmpty(typeName)) return glowAlphaMixed;
        string t = typeName.ToLower();
        if (t.Contains("phys")) return glowAlphaPhysical;
        if (t.Contains("magic") || t.Contains("magical")) return glowAlphaMagic;
        return glowAlphaMixed;
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
            if (gEnemy) gEnemy.gameObject.SetActive(false);
            return;
        }

        string t = typeNameOrNull.ToLower();
        bool isEnemy = t == "enemy";
        bool onPhy = !isEnemy && t.Contains("phys");
        bool onMag = !isEnemy && (t.Contains("magic") || t.Contains("magical"));
        bool onMix = !isEnemy && !onPhy && !onMag;

        if (gPhy) gPhy.gameObject.SetActive(onPhy);
        if (gMag) gMag.gameObject.SetActive(onMag);
        if (gMix) gMix.gameObject.SetActive(onMix);
        if (gEnemy) gEnemy.gameObject.SetActive(isEnemy);
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