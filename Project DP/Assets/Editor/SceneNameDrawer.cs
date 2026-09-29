using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

[CustomPropertyDrawer(typeof(SceneNameAttribute))]
public class SceneNameDrawer : PropertyDrawer
{
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        if (property.propertyType != SerializedPropertyType.String)
        {
            EditorGUI.PropertyField(position, property, label);
            return;
        }

        List<string> values = new List<string> { "" };
        List<GUIContent> options = new List<GUIContent> { new GUIContent("(없음)") };
        foreach (EditorBuildSettingsScene scene in EditorBuildSettings.scenes)
        {
            if (!scene.enabled) continue;

            string sceneName = Path.GetFileNameWithoutExtension(scene.path);
            values.Add(sceneName);
            options.Add(new GUIContent(sceneName));
        }

        // Build Settings에서 빠진 씬이어도 값을 지우지 않고, 빠졌다는 것만 드러냅니다.
        string current = property.stringValue;
        int index = values.IndexOf(current);
        if (index < 0)
        {
            values.Add(current);
            options.Add(new GUIContent($"{current} (Build Settings에 없음)"));
            index = values.Count - 1;
        }

        EditorGUI.BeginProperty(position, label, property);
        EditorGUI.BeginChangeCheck();
        int picked = EditorGUI.Popup(position, label, index, options.ToArray());
        if (EditorGUI.EndChangeCheck())
            property.stringValue = values[picked];
        EditorGUI.EndProperty();
    }
}
