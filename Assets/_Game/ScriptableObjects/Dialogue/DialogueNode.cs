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

        public virtual bool IsEndNode() => nextNode == null;
    }
}
