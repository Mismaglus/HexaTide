using System.Collections.Generic;
using Game.Core;
using UnityEngine;

[DisallowMultipleComponent]
public class UnitHighlighter : MonoBehaviour
{
    [Header("Assign the SG_OutlineShell material")]
    public Material outlineMaterialPrefab;

    [Range(0, 0.08f)] public float widthHover = 0.025f;
    [Range(0, 0.08f)] public float widthSelected = 0.035f;

    [Header("Colors (HDR)")]
    public Color colorPlayer = new Color(0.20f, 1.80f, 0.60f, 1f);     // 亮绿
    public Color colorSelected = new Color(1.80f, 1.40f, 0.50f, 1f);  // 金黄
    public Color colorEnemy = new Color(2.00f, 0.60f, 0.40f, 1f);     // 敌方红橙

    public FactionMembership _faction;
    public bool IsPlayerControlled
    {
        get
        {
            if (_faction == null) TryGetComponent(out _faction);
            if (_faction != null) return _faction.IsPlayerControlled;
            return true; // 无组件时默认可控
        }
        set
        {
            if (_faction == null) TryGetComponent(out _faction);
            if (_faction != null) { _faction.playerControlled = value; return; }
        }
    }
    private Material[] _outlineMats;   // 实例
    private bool _hoverOn, _selOn;
    private Color colorHover;
    static readonly int ColID = Shader.PropertyToID("_OutlineColor");
    static readonly int WidID = Shader.PropertyToID("_OutlineWidth");

    void Awake()
    {
        if (!outlineMaterialPrefab) return;

        var renderers = new List<Renderer>();
        var skinnedRenderers = GetComponentsInChildren<SkinnedMeshRenderer>(true);
        if (skinnedRenderers != null)
        {
            foreach (var skinned in skinnedRenderers)
            {
                if (!skinned) continue;
                renderers.Add(skinned);
            }
        }

        var meshRenderers = GetComponentsInChildren<MeshRenderer>(true);
        if (meshRenderers != null)
        {
            foreach (var mesh in meshRenderers)
            {
                if (!mesh) continue;
                renderers.Add(mesh);
            }
        }

        if (renderers.Count == 0) return;

        var outlineList = new List<Material>(renderers.Count);
        foreach (var renderer in renderers)
        {
            if (!renderer) continue;

            var outlineMat = new Material(outlineMaterialPrefab); // 每个单位一份
            var mats = renderer.sharedMaterials;
            System.Array.Resize(ref mats, mats.Length + 1);
            mats[mats.Length - 1] = outlineMat;
            renderer.sharedMaterials = mats;
            outlineList.Add(outlineMat);
        }

        if (outlineList.Count == 0) return;
        _outlineMats = outlineList.ToArray();

        if (IsPlayerControlled)
        {
            colorHover = colorPlayer;
        }
        else
        {
            colorHover = colorEnemy;
        }

        Hide(); // 默认隐藏
    }

    public void SetHover(bool on)
    {
        if (_outlineMats == null || _outlineMats.Length == 0) return;
        _hoverOn = on;
        if (on)
        {
            foreach (var mat in _outlineMats)
            {
                if (!mat) continue;
                mat.SetColor(ColID, colorHover);
                mat.SetFloat(WidID, widthHover);
            }
        }
        else if (!_selOn) Hide();
    }

    public void SetSelected(bool on, bool enemy = false)
    {
        if (_outlineMats == null || _outlineMats.Length == 0) return;
        _selOn = on;
        if (on)
        {
            foreach (var mat in _outlineMats)
            {
                if (!mat) continue;
                mat.SetColor(ColID, enemy ? colorEnemy : colorSelected);
                mat.SetFloat(WidID, widthSelected);
            }
        }
        else if (!_hoverOn) Hide();
    }

    public void Hide()
    {
        if (_outlineMats == null || _outlineMats.Length == 0) return;
        foreach (var mat in _outlineMats)
        {
            if (!mat) continue;
            mat.SetFloat(WidID, 0f);
        }
    }

    public void SetVisible(bool visible)
    {
        var renderers = GetComponentsInChildren<Renderer>(true);
        foreach (var r in renderers)
        {
            r.enabled = visible;
        }
    }
}
