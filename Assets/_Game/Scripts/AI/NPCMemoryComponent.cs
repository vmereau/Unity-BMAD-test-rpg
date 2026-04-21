using System.Collections.Generic;
using System.Linq;
using Game.Core;
using Game.Dialogue;
using Game.NPC;
using UnityEngine;

namespace Game.AI
{
    /// <summary>
    /// Holds an NPC's memory entries and exposes the currently active set.
    /// Attach alongside NPCPresence on NPC prefabs.
    /// </summary>
    public class NPCMemoryComponent : MonoBehaviour
    {
        private const string TAG = "[NPCMemory]";

        [SerializeField] private NPCDataSO _data;
        [SerializeField] private GameEventSO_Fact onFactChanged;

        private void OnEnable()
        {
            if (onFactChanged == null)
            {
                GameLog.Warn(TAG, $"OnWorldFactChanged not assigned on {gameObject.name} — memories won't react to world changes");
                return;
            }
            onFactChanged.AddListener(HandleWorldFactChanged);
        }

        private void OnDisable()
        {
            if (onFactChanged == null) return;
            onFactChanged.RemoveListener(HandleWorldFactChanged);
        }

        /// <summary>
        /// Returns all memory entries where IsActive() is true.
        /// Evaluated on demand — callers (dialogue, shop, quest) call this when they open.
        /// Not cached: world state may change between calls.
        /// </summary>
        public NPCMemoryEntrySO[] GetActiveMemories()
        {
            if (_data.memories == null || _data.memories.Count == 0) return System.Array.Empty<NPCMemoryEntrySO>();

            var result = new List<NPCMemoryEntrySO>(_data.memories.Count);
            foreach (var memory in _data.memories)
            {
                if (memory == null) continue;
                if (memory.IsActive()) result.Add(memory);
            }
            return result.ToArray();
        }

        public List<StartDialogueNode> GetActiveStartDialogNodes()
        {
            NPCMemoryEntrySO[] activeMemories = GetActiveMemories();

            List<StartDialogueNode> result = new List<StartDialogueNode>(activeMemories.Length);
            foreach (NPCMemoryEntrySO npcMemoryEntrySo in activeMemories)
            {
                if (!npcMemoryEntrySo.HasDialogue()) continue;

                StartDialogueNode node = npcMemoryEntrySo.effects.startdialog;
                if (node.dialogueFact != null
                    && WorldStateManager.Instance != null
                    && WorldStateManager.Instance.IsDialoguePlayed(node.dialogueFact))
                    continue;

                result.Add(node);
            }

            return result;
        }

        private void HandleWorldFactChanged(FactData data)
        {
            // No-op: GetActiveMemories() evaluates on demand so no action needed here.
            // Future: raise a local OnMemoriesChanged event for UI / dialogue pre-evaluation.
        }
    }
}
