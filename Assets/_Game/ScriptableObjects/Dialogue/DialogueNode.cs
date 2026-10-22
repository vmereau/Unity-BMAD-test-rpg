using UnityEngine;

namespace Game.Dialogue
{
    public abstract class DialogueNode : ScriptableObject
    {
        [Header("Content")]
        [Tooltip("Text for this node. StartDialogueNode: topic label. TextDialogueNode: NPC speech. ChoiceDialogueNode: NPC text above choices.")]
        public string text;

        [Header("Navigation")]
        [Tooltip("Next node in the chain. Null = end of dialogue. Not used by ChoiceDialogueNode.")]
        public DialogueNode nextNode;

        [Tooltip("Stable id (the asset GUID, synced in the editor). Keys the persisted 'seen' state of this node and its choices.")]
        [SerializeField] private string _id;

        /// <summary>Stable runtime id — empty until the asset has been validated in the editor.</summary>
        public string Id => _id;

        public virtual bool IsEndNode() => nextNode == null;

#if UNITY_EDITOR
        protected virtual void OnValidate()
        {
            // Synced to the asset GUID so a duplicated asset (Ctrl+D) gets its own id.
            string path = UnityEditor.AssetDatabase.GetAssetPath(this);
            if (string.IsNullOrEmpty(path)) return;
            string guid = UnityEditor.AssetDatabase.AssetPathToGUID(path);
            if (string.IsNullOrEmpty(guid) || guid == _id) return;
            _id = guid;
            UnityEditor.EditorUtility.SetDirty(this);
        }
#endif
    }
}
