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
            return FilterByMemory(choiceNode.choices, memoryComponent);
        }

        public TeachChoiceOption[] GetAvailableTeachChoices(TeachChoiceDialogueNode teachNode, NPCMemoryComponent memoryComponent)
        {
            if (teachNode == null || teachNode.choices == null || teachNode.choices.Length == 0)
                return System.Array.Empty<TeachChoiceOption>();
            return FilterByMemory(teachNode.choices, memoryComponent);
        }

        private T[] FilterByMemory<T>(T[] choices, NPCMemoryComponent memoryComponent)
            where T : ChoiceOption
        {
            NPCMemoryEntrySO[] activeMemories = memoryComponent != null
                ? memoryComponent.GetActiveMemories()
                : System.Array.Empty<NPCMemoryEntrySO>();

            var result = new List<T>(choices.Length);
            foreach (var choice in choices)
            {
                if (choice == null) continue;
                if (choice.requiredMemory == null || System.Array.IndexOf(activeMemories, choice.requiredMemory) >= 0)
                    result.Add(choice);
            }
            return result.ToArray();
        }
    }
}
