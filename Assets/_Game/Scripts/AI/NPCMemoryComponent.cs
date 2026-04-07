using System.Collections.Generic;
using Game.Core;
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

        [SerializeField] private List<NPCMemoryEntrySO> _memories;
        [SerializeField] private GameEventSO_WorldFact _onWorldFactChanged;

        private void OnEnable()
        {
            if (_onWorldFactChanged == null)
            {
                GameLog.Warn(TAG, $"OnWorldFactChanged not assigned on {gameObject.name} — memories won't react to world changes");
                return;
            }
            _onWorldFactChanged.AddListener(HandleWorldFactChanged);
        }

        private void OnDisable()
        {
            if (_onWorldFactChanged == null) return;
            _onWorldFactChanged.RemoveListener(HandleWorldFactChanged);
        }

        /// <summary>
        /// Returns all memory entries where IsActive() is true.
        /// Evaluated on demand — callers (dialogue, shop, quest) call this when they open.
        /// Not cached: world state may change between calls.
        /// </summary>
        public NPCMemoryEntrySO[] GetActiveMemories()
        {
            if (_memories == null || _memories.Count == 0) return System.Array.Empty<NPCMemoryEntrySO>();

            var result = new List<NPCMemoryEntrySO>(_memories.Count);
            foreach (var memory in _memories)
            {
                if (memory == null) continue;
                if (memory.IsActive()) result.Add(memory);
            }
            return result.ToArray();
        }

        public NPCMemoryEntrySO[] GetActiveDialogueMemories()
        {
            NPCMemoryEntrySO[] active = GetActiveMemories();
            var result = new List<NPCMemoryEntrySO>(active.Length);
            foreach (var memory in active)
            {
                if (memory.HasDialogue()) result.Add(memory);
            }
            return result.ToArray();
        }

        private void HandleWorldFactChanged(WorldFactData data)
        {
            // No-op: GetActiveMemories() evaluates on demand so no action needed here.
            // Future: raise a local OnMemoriesChanged event for UI / dialogue pre-evaluation.
        }
    }
}
