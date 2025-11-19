#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(SkillBarPopulator))]
public class PhyHotbarPopulatorEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        if (GUILayout.Button("Populate Now"))
        {
            foreach (var targetPopulator in targets)
            {
                var populator = targetPopulator as SkillBarPopulator;
                if (populator == null) continue;

                populator.Populate();

                if (!Application.isPlaying)
                {
                    EditorUtility.SetDirty(populator);
                    if (populator.hotBarRoot != null)
                    {
                        EditorUtility.SetDirty(populator.hotBarRoot);
                    }
                }
            }
        }
    }
}
#endif
