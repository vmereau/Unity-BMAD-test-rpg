using UnityEditor;
using Game.Progression;

namespace Game.Editor
{
    [CustomEditor(typeof(PlayerRewardSO))]
    public class PlayerRewardSOEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            var factTypeProp = serializedObject.FindProperty("_factType");
            EditorGUILayout.PropertyField(factTypeProp);

            switch ((RewardFactType)factTypeProp.intValue)
            {
                case RewardFactType.Killed:
                    EditorGUILayout.PropertyField(serializedObject.FindProperty("_killedFact"));
                    break;
                case RewardFactType.Quest:
                    EditorGUILayout.PropertyField(serializedObject.FindProperty("_questFact"));
                    break;
                case RewardFactType.Dialogue:
                    EditorGUILayout.PropertyField(serializedObject.FindProperty("_dialogueFact"));
                    break;
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Rewards", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("_xpReward"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("_lpReward"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("_goldReward"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("_statRewards"), true);
            var itemRewardsProp = serializedObject.FindProperty("_itemRewards");
            EditorGUILayout.PropertyField(itemRewardsProp, true);
            // Dialogue facts are raised on every replay of their node — an item there can be farmed.
            if (itemRewardsProp.arraySize > 0 && (RewardFactType)factTypeProp.intValue == RewardFactType.Dialogue)
                EditorGUILayout.HelpBox("Dialogue rewards fire every time the node plays — item rewards here can be granted repeatedly. Prefer a Quest reward.", MessageType.Warning);

            serializedObject.ApplyModifiedProperties();
        }
    }
}
