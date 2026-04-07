using Game.Core;
using Game.World;
using UnityEngine;

namespace Game.NPC
{
    [System.Serializable]
    public class NPCMemoryEffects
    {
        [Header("Dialogue")]
        [Tooltip("Dialogue lines available while this memory is active. Consumed by DialogueSystem (future).")]
        public string[] dialogueLines;

        [Header("Shop")]
        [Range(-1f, 1f)]
        [Tooltip("Price modifier. 0 = no effect. -0.1 = 10% discount. Consumed by ShopSystem (future).")]
        public float shopPriceModifier;

        [Tooltip("One-shot line played first time shop is opened while memory is active. Set '' to skip.")]
        public string shopRevealDialogueLine;

        [Header("Routine")]
        [Tooltip("Routine override while this memory is active. None = no change. Consumed by NPCScheduler (future).")]
        public NPCState routineOverride = NPCState.None;

        [Tooltip("If true, routineOverride is applied. If false, NPC keeps default schedule.")]
        public bool overrideRoutine;

        [Header("Quest")]
        [Tooltip("Dialogue key that initiates or references a quest. Empty = no quest effect.")]
        public string questDialogueKey;
    }

    /// <summary>
    /// ScriptableObject representing a single NPC memory entry: conditions for activation,
    /// conditions for permanent closure, and the effects to apply while active.
    /// </summary>
    [CreateAssetMenu(menuName = "Game/NPC/Memory Entry", fileName = "Mem_")]
    public class NPCMemoryEntrySO : ScriptableObject
    {
        private const string TAG = "[NPCMemory]";

        [Header("Identity")]
        [Tooltip("Unique ID for this memory — used in logs and save data.")]
        public string memoryId;

        [Header("Conditions")]
        [Tooltip("ALL of these world fact keys must be true for this memory to be active.")]
        public string[] unlockConditions;

        [Tooltip("If ANY of these world fact keys is true, this memory is permanently closed.")]
        public string[] invalidationConditions;

        [Header("Effects")]
        public NPCMemoryEffects effects = new NPCMemoryEffects();

        /// <summary>Returns true when all unlock conditions are met in WorldStateManager.</summary>
        public bool IsUnlocked() => TopicUnlockEvaluator.AllTrue(unlockConditions);

        /// <summary>Returns true when any invalidation condition is met. Invalidation supersedes unlock.</summary>
        public bool IsInvalidated() => TopicUnlockEvaluator.AnyTrue(invalidationConditions);

        /// <summary>Convenience: unlocked AND not invalidated.</summary>
        public bool IsActive() => IsUnlocked() && !IsInvalidated();

        public bool HasDialogue() => effects.dialogueLines != null && effects.dialogueLines.Length > 0;

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (string.IsNullOrEmpty(memoryId))
                GameLog.Warn(TAG, $"NPCMemoryEntrySO '{name}' has no memoryId set");
        }
#endif
    }
}
