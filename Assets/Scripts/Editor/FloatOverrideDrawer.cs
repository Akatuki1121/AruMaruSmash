using UnityEditor;
using UnityEngine;

/// <summary>
/// FloatOverrideをInspectorで「チェックボックス + 数値」の1行で表示する。
/// チェックなし＝CharacterStatsのデフォルト値を使う（数値欄はグレーアウト）。
/// チェックあり＝この数値で上書きする。
/// </summary>
[CustomPropertyDrawer(typeof(FloatOverride))]
public class FloatOverrideDrawer : PropertyDrawer
{
    private const float ToggleWidth = 18f;
    private const float Spacing = 4f;

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        SerializedProperty useOverride = property.FindPropertyRelative("useOverride");
        SerializedProperty value = property.FindPropertyRelative("value");

        EditorGUI.BeginProperty(position, label, property);

        Rect fieldArea = EditorGUI.PrefixLabel(position, label);

        int oldIndent = EditorGUI.indentLevel;
        EditorGUI.indentLevel = 0;

        Rect toggleRect = new Rect(fieldArea.x, fieldArea.y, ToggleWidth, fieldArea.height);
        Rect valueRect = new Rect(
            fieldArea.x + ToggleWidth + Spacing,
            fieldArea.y,
            fieldArea.width - ToggleWidth - Spacing,
            fieldArea.height);

        useOverride.boolValue = EditorGUI.Toggle(toggleRect, useOverride.boolValue);

        using (new EditorGUI.DisabledScope(!useOverride.boolValue))
        {
            EditorGUI.PropertyField(valueRect, value, GUIContent.none);
        }

        EditorGUI.indentLevel = oldIndent;

        EditorGUI.EndProperty();
    }
}
