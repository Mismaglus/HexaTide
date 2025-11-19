#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(ConsumableGridColorTool))]
public class ConsumableGridColorToolEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        var tool = (ConsumableGridColorTool)target;

        EditorGUILayout.Space();
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("一键替换所有匹配的 Image 颜色", GUILayout.Height(28)))
            {
                Undo.RegisterFullObjectHierarchyUndo(tool.gameObject, "Apply Item Image Color");
                int n = tool.ApplyColor();
                Debug.Log($"[ConsumableGridColorTool] Modified {n} Images under {tool.name}.");
                EditorUtility.SetDirty(tool);
            }
        }

        EditorGUILayout.HelpBox(
            "用法：挂在 Consumable Grid 上；\n" +
            "仅改背景：保持 Filters 包含 \"SPR_Background\"；\n" +
            "改所有 Image：把 Filters 清空即可；\n" +
            "右键组件标题也可执行：Apply Color Now。", MessageType.Info);
    }
}
#endif
