using System.Collections.Generic;
using Game.Core;
using UnityEngine;

namespace Game.Quest
{
    [CreateAssetMenu(menuName = "Game/Quest/Quest", fileName = "Quest_")]
    public class QuestSO : ScriptableObject
    {
        [Tooltip("Unique quest identifier. Used as key in QuestFact. E.g. 'FindHerbalist'.")]
        public string questId;

        [Tooltip("Display title shown in Quest Log (Story 6-5).")]
        public string title;

        [Tooltip("Full quest description shown in Quest Log (Story 6-5).")]
        [TextArea(3, 6)]
        public string description;

        [Header("Quest State Conditions")]
        [Tooltip("When this fact is true, the quest is considered started.")]
        public Fact startFact;

        [Tooltip("When ALL facts in this list are true, the quest is considered completed. Empty = never completed.")]
        public List<Fact> completedFacts = new List<Fact>();

        [Tooltip("When ANY fact in this list is true, the quest is considered failed. Empty = never failed.")]
        public List<Fact> failedFacts = new List<Fact>();

        /// <summary>True if startFact is set to true in WorldStateManager.</summary>
        public bool IsStarted
        {
            get
            {
                if (startFact == null || WorldStateManager.Instance == null) return false;
                return WorldStateManager.Instance.GetFact(startFact);
            }
        }

        /// <summary>True if all completedFacts are true. Returns false if list is empty.</summary>
        public bool IsCompleted
        {
            get
            {
                if (completedFacts == null || completedFacts.Count == 0) return false;
                if (WorldStateManager.Instance == null) return false;
                foreach (var f in completedFacts)
                    if (f == null || !WorldStateManager.Instance.GetFact(f)) return false;
                return true;
            }
        }

        /// <summary>True if any failedFact is true. Returns false if list is empty.</summary>
        public bool IsFailed
        {
            get
            {
                if (failedFacts == null || WorldStateManager.Instance == null) return false;
                foreach (var f in failedFacts)
                    if (f != null && WorldStateManager.Instance.GetFact(f)) return true;
                return false;
            }
        }
    }
}
