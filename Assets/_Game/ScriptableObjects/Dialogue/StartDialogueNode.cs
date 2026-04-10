using Game.NPC;
using UnityEngine;

namespace Game.Dialogue
{
    [CreateAssetMenu(menuName = "Game/Dialogue/Start Node", fileName = "Start_")]
    public class StartDialogueNode : DialogueNode
    {
        [Tooltip("If false, this topic is hidden after it has been played once (chain reached an end node). Default true.")]
        public bool isRepeatable = true;
    }
}
