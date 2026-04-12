#if UNITY_EDITOR
using System.Collections.Generic;
using Game.Dialogue;
using Game.NPC;
using UnityEditor;
using UnityEngine;

namespace Game.Editor
{
    [CustomEditor(typeof(NPCMemoryEntrySO))]
    public class NPCMemoryEntrySO_Editor : UnityEditor.Editor
    {
        private List<(ChoiceDialogueNode node, int choiceIndex)> _usages;
        private bool _foldout = true;

        private void OnEnable()
        {
            RefreshUsages();
        }

        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);

            DrawUsagesSection();
        }

        private void DrawUsagesSection()
        {
            EditorGUILayout.BeginHorizontal();
            _foldout = EditorGUILayout.Foldout(_foldout, $"Used in ChoiceDialogueNode ({_usages.Count})", true, EditorStyles.foldoutHeader);
            if (GUILayout.Button("Refresh", GUILayout.Width(60)))
                RefreshUsages();
            EditorGUILayout.EndHorizontal();

            if (!_foldout) return;

            if (_usages.Count == 0)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.LabelField("Not referenced by any ChoiceDialogueNode.", EditorStyles.miniLabel);
                EditorGUI.indentLevel--;
                return;
            }

            EditorGUI.indentLevel++;
            using var disabledScope = new EditorGUI.DisabledScope(true);
            foreach (var (node, choiceIndex) in _usages)
            {
                EditorGUILayout.ObjectField($"choices[{choiceIndex}].requiredMemory", node, typeof(ChoiceDialogueNode), false);
            }
            EditorGUI.indentLevel--;
        }

        private void RefreshUsages()
        {
            _usages = new List<(ChoiceDialogueNode, int)>();
            var self = (NPCMemoryEntrySO)target;

            string[] guids = AssetDatabase.FindAssets("t:ChoiceDialogueNode");
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var node = AssetDatabase.LoadAssetAtPath<ChoiceDialogueNode>(path);
                if (node == null || node.choices == null) continue;

                for (int i = 0; i < node.choices.Length; i++)
                {
                    if (node.choices[i].requiredMemory == self)
                        _usages.Add((node, i));
                }
            }
        }
    }
}
#endif
