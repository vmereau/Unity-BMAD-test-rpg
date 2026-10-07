using Game.World;
using UnityEditor;
using UnityEditor.SceneManagement;

namespace Game.Editor.Save
{
    /// <summary>Shows the effective save key, a Regenerate button, and warns on prefab assets.</summary>
    [CustomEditor(typeof(SaveableObject))]
    public class SaveableObjectEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            var saveable = (SaveableObject)target;
            var go = saveable.gameObject;
            bool isPrefabAsset = PrefabUtility.IsPartOfPrefabAsset(go) || PrefabStageUtility.GetPrefabStage(go) != null;
            bool isEntity = saveable.GetComponent<PersistentID>() != null;

            EditorGUILayout.Space();
            using (new EditorGUI.DisabledScope(true))
                EditorGUILayout.TextField("Effective Save Key", saveable.SaveKey);

            if (isEntity)
                EditorGUILayout.HelpBox("Entity: the key comes from the PersistentID's KilledFact GUID. Leave Save Id empty.", MessageType.Info);
            else if (isPrefabAsset)
                EditorGUILayout.HelpBox("Prefab asset: leave Save Id empty here — a value set on the prefab is shared by every " +
                                        "instance. IDs are assigned on scene instances (Tools/Save/Validate Save IDs).", MessageType.Warning);

            using (new EditorGUI.DisabledScope(isPrefabAsset || isEntity || targets.Length > 1))
            {
                if (UnityEngine.GUILayout.Button("Regenerate ID"))
                    SaveableObjectValidator.AssignNewId(saveable);
            }
        }
    }
}
