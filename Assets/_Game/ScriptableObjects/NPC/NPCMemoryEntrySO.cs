using Game.Core;
using Game.Dialogue;
using Game.World;
using UnityEngine;

namespace Game.NPC
{
    [System.Serializable]
    public class NPCMemoryEffects
    {
        [Header("Dialogue")]
        [Tooltip("Start Dialogue lines available while this memory is active. Consumed by DialogueSystem")]
        public StartDialogueNode startdialog;
    }

    /// <summary>
    /// ScriptableObject representing a single NPC memory entry: conditions for activation,
    /// conditions for permanent closure, and the effects to apply while active.
    /// </summary>
    [CreateAssetMenu(menuName = "Game/NPC/Memory Entry", fileName = "Mem_")]
    public class NPCMemoryEntrySO : ScriptableObject
    {
        private const string TAG = "[NPCMemory]";

        [Header("Conditions")]
        [Tooltip("ALL of these facts must be true for this memory to be active.")]
        public Fact[] unlockConditions;

        [Tooltip("If ANY of these facts is true, this memory is permanently closed.")]
        public Fact[] invalidationConditions;

        [Header("Effects")]
        public NPCMemoryEffects effects = new NPCMemoryEffects();

        /// <summary>Returns true when all unlock conditions are met in WorldStateManager.</summary>
        public bool IsUnlocked() => TopicUnlockEvaluator.AllTrue(unlockConditions);

        /// <summary>Returns true when any invalidation condition is met. Invalidation supersedes unlock.</summary>
        public bool IsInvalidated() => TopicUnlockEvaluator.AnyTrue(invalidationConditions);

        /// <summary>Convenience: unlocked AND not invalidated.</summary>
        public bool IsActive() => IsUnlocked() && !IsInvalidated();

        public bool HasDialogue() => effects.startdialog != null;
    }
}
