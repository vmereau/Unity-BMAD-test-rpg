using System.Collections.Generic;
using Game.Core;
using Game.Dialogue;
using Game.NPC;
using UnityEngine;

namespace Game.AI
{
    public class NPCDialogueGraphComponent : MonoBehaviour
    {
        private const string TAG = "[DialogueGraph]";

        public StartDialogueNode[] GetAvailableStartNodes(NPCMemoryComponent memoryComponent)
        {
            var result = memoryComponent.GetActiveStartDialogNodes();
            return result.ToArray();
        }

        public ChoiceOption[] GetAvailableChoices(ChoiceDialogueNode choiceNode, NPCMemoryComponent memoryComponent)
        {
            if (choiceNode == null || choiceNode.choices == null || choiceNode.choices.Length == 0)
                return System.Array.Empty<ChoiceOption>();

            NPCMemoryEntrySO[] activeMemories = memoryComponent != null
                ? memoryComponent.GetActiveMemories()
                : System.Array.Empty<NPCMemoryEntrySO>();

            var result = new List<ChoiceOption>(choiceNode.choices.Length);
            foreach (var choice in choiceNode.choices)
            {
                if (choice == null) continue;
                if (choice.requiredMemory == null || System.Array.IndexOf(activeMemories, choice.requiredMemory) >= 0)
                    result.Add(choice);
            }
            return result.ToArray();
        }
    }
}
