using Game.Core;
using Game.NPC;
using UnityEngine;

namespace Game.Dialogue
{
    [CreateAssetMenu(menuName = "Game/Dialogue/Start Node", fileName = "Start_")]
    public class StartDialogueNode : DialogueNode
    {
        [Tooltip("Fact asset used to track and check played state in WorldStateManager")]
        public DialogueFact dialogueFact;
    }
}
