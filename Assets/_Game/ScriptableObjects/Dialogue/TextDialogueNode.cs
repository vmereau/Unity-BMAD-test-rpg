using UnityEngine;

namespace Game.Dialogue
{
    [CreateAssetMenu(menuName = "Game/Dialogue/Text Node", fileName = "Text_")]
    public class TextDialogueNode : DialogueNode
    {
        // Inherits: text (NPC line), nextNode (next in chain), IsEndNode()
        // nextNode == null → UI shows "Farewell." button; non-null → "Continue..."
    }
}
