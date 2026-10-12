using ParallaxVisions.Models;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(GroupSO))]
public class ScriptableObjectPinger : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        if (GUILayout.Button("Ping File"))
        {
            EditorGUIUtility.PingObject(target);
            Selection.activeObject = target;
        }
    }
}