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
        [Tooltip("Gold the player pays the NPC when picking this choice (coins go into the NPC's inventory). 0 = free. " +
                 "The button shows the cost and is disabled while the player can't afford it.")]
        [Min(0)] public int goldCost;
        [Tooltip("Item the player hands over when picking this choice (one unit, removed from the player's inventory). " +
                 "Null = none. The button names the item and is disabled while the player doesn't have it.")]
        public Game.Inventory.ItemSO requiredItem;
        [Tooltip("Kind of item the player hands over when picking this choice: opens the Give window, where the player " +
                 "picks one matching item (it moves to the NPC's inventory). Ignored when requiredItem is set. None = no give.")]
        public Game.Inventory.GiveItemCategory requiredCategory;
        [Tooltip("Hide this choice (instead of showing it disabled) while the player holds no requiredItem / no item of " +
                 "requiredCategory.")]
        public bool hideWhenMissing;
        [Tooltip("Raised on OnDialogueSignal when this choice is picked (after the gold is paid), for gameplay systems " +
                 "reacting to the answer. Null = none.")]
        public DialogueSignalSO signal;
    }

    [CreateAssetMenu(menuName = "Game/Dialogue/Choice Node", fileName = "Choice_")]
    public class ChoiceDialogueNode : DialogueNode
    {
        [Header("Choices")]
        [Tooltip("Player choices shown after NPC text. Each choice can be memory-gated.")]
        public ChoiceOption[] choices;

        public override bool IsEndNode() => false;

#if UNITY_EDITOR
        protected override void OnValidate()
        {
            base.OnValidate();
            if (choices == null) return;
            for (int i = 0; i < choices.Length; i++)
            {
                var c = choices[i];
                if (c != null && c.requiredItem != null && c.requiredCategory != Game.Inventory.GiveItemCategory.None)
                    GameLog.Warn("[ChoiceDialogueNode]", $"{name} choice {i}: requiredItem and requiredCategory are both " +
                                                         "set — the item wins, the category is ignored");
            }
        }
#endif

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
