#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using Game.Battle;

[CustomEditor(typeof(UnitSpawner))]
public class UnitSpawnerEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        EditorGUILayout.Space();

        var spawner = (UnitSpawner)target;

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Spawn Unit", GUILayout.Height(28)))
            {
                spawner.SpawnNow();

                // 方便查看：选中新生成的最后一个子物体并高亮父物体
                if (spawner.transform.childCount > 0)
                {
                    var last = spawner.transform.GetChild(spawner.transform.childCount - 1).gameObject;
                    Selection.activeGameObject = last;
                }
                EditorGUIUtility.PingObject(spawner.gameObject);
            }
        }
    }
}
#endif
