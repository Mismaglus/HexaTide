// HotbarFrameReplacer.cs
// Attach this to the HotBar root

using System.Linq;
using UnityEngine;
using UnityEngine.UI;

#if UNITY_EDITOR
using UnityEditor;
#endif

[DisallowMultipleComponent]
public class HotbarFrameReplacer : MonoBehaviour
{
    [Header("Target Sprite")]
    [Tooltip("The sprite that will replace all frame images.")]
    public Sprite newFrameSprite;

    [Header("Match Rules")]
    [Tooltip("Only replace objects whose name contains this text. Leave empty to replace all Images/SpriteRenderers.")]
    public string nameContains = "SPR_Frame";

    [Tooltip("Search inactive children as well.")]
    public bool includeInactive = true;

    [Header("UI Image Options")]
    [Tooltip("If true, set Image.type to Sliced after replacement.")]
    public bool forceSlicedForUI = true;

    [Tooltip("Keep existing Image color instead of overwriting.")]
    public bool keepImageColor = true;

    /// <summary>Apply replacement now.</summary>
    public int ReplaceAll()
    {
        if (newFrameSprite == null)
        {
            Debug.LogWarning("[HotbarFrameReplacer] newFrameSprite is null.");
            return 0;
        }

        int replaced = 0;

        // 1) Unity UI Images
        var images = GetComponentsInChildren<Image>(includeInactive);
        foreach (var img in FilterByName(images, go => go.name))
        {
#if UNITY_EDITOR
            Undo.RecordObject(img, "Replace Hotbar Frame (Image)");
#endif
            var col = img.color;
            img.sprite = newFrameSprite;
            if (forceSlicedForUI) img.type = Image.Type.Sliced;
            if (keepImageColor) img.color = col;
#if UNITY_EDITOR
            EditorUtility.SetDirty(img);
#endif
            replaced++;
        }

        // 2) SpriteRenderers (in case some frames are world-space or sprites)
        var srs = GetComponentsInChildren<SpriteRenderer>(includeInactive);
        foreach (var sr in FilterByName(srs, go => go.name))
        {
#if UNITY_EDITOR
            Undo.RecordObject(sr, "Replace Hotbar Frame (SpriteRenderer)");
#endif
            sr.sprite = newFrameSprite;
#if UNITY_EDITOR
            EditorUtility.SetDirty(sr);
#endif
            replaced++;
        }

        Debug.Log($"[HotbarFrameReplacer] Replaced {replaced} component(s).");
        return replaced;
    }

    // Helper: name filter
    private T[] FilterByName<T>(T[] comps, System.Func<Object, string> getName)
        where T : Component
    {
        if (string.IsNullOrEmpty(nameContains)) return comps;
        return comps.Where(c =>
        {
            var n = getName(c);
            return !string.IsNullOrEmpty(n) &&
                   n.IndexOf(nameContains, System.StringComparison.OrdinalIgnoreCase) >= 0;
        }).ToArray();
    }

    // Context menu for quick use
    [ContextMenu("Replace Frames Now")]
    private void ContextReplace() => ReplaceAll();
}

#if UNITY_EDITOR
[CustomEditor(typeof(HotbarFrameReplacer))]
public class HotbarFrameReplacerEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        var tool = (HotbarFrameReplacer)target;

        EditorGUILayout.Space();
        if (GUILayout.Button("Replace Frames In Children", GUILayout.Height(28)))
        {
            Undo.RegisterFullObjectHierarchyUndo(tool.gameObject, "Replace Hotbar Frames");
            int n = tool.ReplaceAll();
            Debug.Log($"[HotbarFrameReplacer] Done. Total replaced: {n}");
        }

        EditorGUILayout.HelpBox(
            "Usage:\n" +
            "1) Attach to HotBar root.\n" +
            "2) Assign NewFrameSprite.\n" +
            "3) Click the button to replace.\n" +
            "Tip: Set Name Contains to 'SPR_Frame' to only affect frame nodes; leave empty to replace all Images/SpriteRenderers under HotBar.",
            MessageType.Info);
    }
}
#endif
