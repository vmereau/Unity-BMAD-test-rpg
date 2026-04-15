#if UNITY_EDITOR
using Game.Core;
using Game.Quest;
using UnityEditor;
using UnityEngine;

namespace Game.Editor
{
    [CustomEditor(typeof(QuestFact))]
    public class QuestFactEditor : UnityEditor.Editor
    {
        private SerializedProperty _questProp;
        private SerializedProperty _questStateProp;

        private void OnEnable()
        {
            _questProp      = serializedObject.FindProperty("_quest");
            _questStateProp = serializedObject.FindProperty("_questState");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            // Draw the _quest object field
            EditorGUILayout.PropertyField(_questProp, new GUIContent("Quest"));

            // Build the combined label list
            var questSO = _questProp.objectReferenceValue as QuestSO;
            bool hasQuest = questSO != null;

            string[] labels = BuildLabels(questSO);
            int current = _questStateProp.intValue;
            // Clamp to valid range and persist if steps were removed from the quest
            int max = labels.Length - 1;
            if (current > max)
            {
                GameLog.Warn("[QuestFactEditor]", $"'{target.name}': state index {current} is out of range — clamped to 0 (IsStarted)");
                current = 0;
                _questStateProp.intValue = 0; // persist immediately so the asset is not left in an invalid state
            }

            using (new EditorGUI.DisabledScope(!hasQuest))
            {
                int selected = EditorGUILayout.Popup(
                    new GUIContent("Quest State", "IsStarted / IsCompleted / IsFailed or a step title"),
                    current,
                    labels
                );

                if (selected != current)
                    _questStateProp.intValue = selected;
            }

            serializedObject.ApplyModifiedProperties();
        }

        private static string[] BuildLabels(QuestSO quest)
        {
            int stepCount = quest != null && quest.steps != null ? quest.steps.Count : 0;
            var labels = new string[3 + stepCount];
            labels[0] = "IsStarted";
            labels[1] = "IsCompleted";
            labels[2] = "IsFailed";
            for (int i = 0; i < stepCount; i++)
            {
                string title = quest.steps[i].title;
                labels[3 + i] = string.IsNullOrEmpty(title)
                    ? $"Step {i} (no title)"
                    : $"Step: {title}";
            }
            return labels;
        }
    }
}
#endif
