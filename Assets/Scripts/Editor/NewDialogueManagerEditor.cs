using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(NewDialogueManager))]
public class NewDialogueManagerEditor : Editor
{
    SerializedProperty dialogueLines;

    void OnEnable()
    {
        dialogueLines = serializedObject.FindProperty("dialogueLines");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        SerializedProperty property = serializedObject.GetIterator();
        bool enterChildren = true;
        while (property.NextVisible(enterChildren))
        {
            enterChildren = false;
            if (property.name == "m_Script")
            {
                using (new EditorGUI.DisabledScope(true))
                    EditorGUILayout.PropertyField(property, true);
                continue;
            }

            if (property.name == "dialogueLines")
            {
                DrawDialogueLinesWithIndexes(property);
                continue;
            }

            EditorGUILayout.PropertyField(property, true);
        }

        serializedObject.ApplyModifiedProperties();
    }

    void DrawDialogueLinesWithIndexes(SerializedProperty lines)
    {
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Dialogue Lines", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(lines.FindPropertyRelative("Array.size"));

        for (int i = 0; i < lines.arraySize; i++)
        {
            SerializedProperty line = lines.GetArrayElementAtIndex(i);
            SerializedProperty speaker = line.FindPropertyRelative("speakerName");
            SerializedProperty content = line.FindPropertyRelative("content");

            string speakerLabel = string.IsNullOrEmpty(speaker.stringValue) ? "No Speaker" : speaker.stringValue;
            line.isExpanded = EditorGUILayout.Foldout(
                line.isExpanded,
                $"Index {i} - {speakerLabel}",
                true);

            if (!line.isExpanded)
                continue;

            using (new EditorGUI.IndentLevelScope())
            {
                EditorGUILayout.PropertyField(speaker);
                EditorGUILayout.PropertyField(content);
            }
        }
    }
}
