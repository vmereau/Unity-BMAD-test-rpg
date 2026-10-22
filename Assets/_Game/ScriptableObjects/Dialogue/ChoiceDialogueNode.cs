using Game.Core;
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
        [Tooltip("Fact asset used to track and check played state in WorldStateManager. Null = not tracked.")]
        public DialogueFact dialogueFact;
    }

    [CreateAssetMenu(menuName = "Game/Dialogue/Choice Node", fileName = "Choice_")]
    public class ChoiceDialogueNode : DialogueNode
    {
        [Header("Choices")]
        [Tooltip("Player choices shown after NPC text. Each choice can be memory-gated.")]
        public ChoiceOption[] choices;

        public override bool IsEndNode() => false;

        /// <summary>
        /// Persisted "seen" key of <paramref name="choice"/>: <c>{Id}.{index in choices}</c>. Empty when this node
        /// has no id or the choice isn't one of its options. Reordering choices reassigns their seen state.
        /// </summary>
        public string ChoiceSeenId(ChoiceOption choice)
        {
            if (string.IsNullOrEmpty(Id) || choice == null || choices == null) return string.Empty;
            int index = System.Array.IndexOf(choices, choice);
            return index < 0 ? string.Empty : $"{Id}.{index}";
        }
    }
}
