using Game.NPC;
using UnityEngine;

namespace Game.Dialogue
{
    [System.Serializable]
    public class ChoiceOption
    {
        [Tooltip("Text shown on the choice button (player's voice).")]
        public string text;
        [Tooltip("Memory that must be active for this choice to appear. Null = always shown.")]
        public NPCMemoryEntrySO requiredMemory;
        [Tooltip("Node to advance to when this choice is selected. Null = close dialogue.")]
        public DialogueNode nextNode;
    }

    [CreateAssetMenu(menuName = "Game/Dialogue/Choice Node", fileName = "Choice_")]
    public class ChoiceDialogueNode : DialogueNode
    {
        [Header("Choices")]
        [Tooltip("Player choices shown after NPC text. Each choice can be memory-gated.")]
        public ChoiceOption[] choices;

        public override bool IsEndNode() => false;
    }
}
