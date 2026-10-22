using UnityEngine;

namespace Game.Dialogue
{
    [CreateAssetMenu(menuName = "Game/Dialogue/Text Node", fileName = "Text_")]
    public class TextDialogueNode : DialogueNode
    {
        // Inherits: text (NPC line), nextNode (next in chain), IsEndNode()
        // DialogueUI labels its advance button "Continue" when nextNode is set, "End" when null (ends the topic)
    }
}
