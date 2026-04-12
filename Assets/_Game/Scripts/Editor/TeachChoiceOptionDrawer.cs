#if UNITY_EDITOR
using Game.Dialogue;
using UnityEditor;
using UnityEngine;

namespace Game.Editor
{
    [CustomPropertyDrawer(typeof(TeachChoiceOption))]
    public class TeachChoiceOptionDrawer : PropertyDrawer
    {
        private const float LINE = 18f;   // EditorGUIUtility.singleLineHeight
        private const float SPACING = 2f; // EditorGUIUtility.standardVerticalSpacing
        private const float STEP = LINE + SPACING;

        // Fields always visible: text, requiredMemory, teachingType, goldCost, confirmNextNode
        // The leading LINE + already accounts for the foldout header row — do not count it here.
        private const int ALWAYS_LINES = 5;

        // StatBased extras: statToUpgrade, statPoints, limitStat, denyNextNode  →  4
        // SkillBased extras: skill  →  1

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            if (!property.isExpanded)
                return LINE;

            int extra = GetExtraLineCount(property);
            return LINE + (ALWAYS_LINES + extra) * STEP;
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);

            // Foldout header
            var headerRect = new Rect(position.x, position.y, position.width, LINE);
            property.isExpanded = EditorGUI.Foldout(headerRect, property.isExpanded, label, true);

            if (property.isExpanded)
            {
                EditorGUI.indentLevel++;
                float y = position.y + STEP;

                y = DrawField(position, y, property, "text");
                y = DrawField(position, y, property, "requiredMemory");
                y = DrawField(position, y, property, "teachingType");
                y = DrawField(position, y, property, "goldCost");
                y = DrawField(position, y, property, "confirmNextNode");

                var typeProp = property.FindPropertyRelative("teachingType");
                bool isSkillBased = typeProp.enumValueIndex == (int)TeachingType.SkillBased;

                if (isSkillBased)
                {
                    y = DrawField(position, y, property, "skill");
                }
                else // StatBased
                {
                    y = DrawField(position, y, property, "statToUpgrade");
                    y = DrawField(position, y, property, "statPoints");
                    y = DrawField(position, y, property, "limitStat");
                    y = DrawField(position, y, property, "denyNextNode");
                }

                EditorGUI.indentLevel--;
            }

            EditorGUI.EndProperty();
        }

        // ── Helpers ──────────────────────────────────────────────────────────────

        private static float DrawField(Rect root, float y, SerializedProperty parent, string fieldName)
        {
            var prop = parent.FindPropertyRelative(fieldName);
            if (prop == null) return y; // field not found — skip silently
            var rect = new Rect(root.x, y, root.width, LINE);
            EditorGUI.PropertyField(rect, prop);
            return y + STEP;
        }

        private static int GetExtraLineCount(SerializedProperty property)
        {
            var typeProp = property.FindPropertyRelative("teachingType");
            if (typeProp == null) return 0;
            return typeProp.enumValueIndex == (int)TeachingType.SkillBased ? 1 : 4;
        }
    }
}
#endif
