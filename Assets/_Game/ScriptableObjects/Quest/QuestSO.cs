using System.Collections.Generic;
using Game.Core;
using UnityEngine;

namespace Game.Quest
{
    [System.Serializable]
    public struct QuestPart
    {
        [Tooltip("The fact that represents this quest part's condition.")]
        public Fact fact;

        [Tooltip("The text shown in the quest log for this part.")]
        [TextArea(1, 3)]
        public string entry;
    }

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
        [Tooltip("When this part's fact is true, the quest is considered started.")]
        public QuestPart startPart;

        [Tooltip("When ALL parts' facts are true, the quest is considered completed. Empty = never completed.")]
        public List<QuestPart> completedParts = new List<QuestPart>();

        [Tooltip("When ANY part's fact is true, the quest is considered failed. Empty = never failed.")]
        public List<QuestPart> failedParts = new List<QuestPart>();

        /// <summary>True if startPart.fact is set to true in WorldStateManager.</summary>
        public bool IsStarted
        {
            get
            {
                if (startPart.fact == null || WorldStateManager.Instance == null) return false;
                return WorldStateManager.Instance.GetFact(startPart.fact);
            }
        }

        /// <summary>True if all completedParts' facts are true. Returns false if list is empty.</summary>
        public bool IsCompleted
        {
            get
            {
                if (completedParts == null || completedParts.Count == 0) return false;
                if (WorldStateManager.Instance == null) return false;
                foreach (var p in completedParts)
                    if (p.fact == null || !WorldStateManager.Instance.GetFact(p.fact)) return false;
                return true;
            }
        }

        /// <summary>True if any failedParts' fact is true. Returns false if list is empty.</summary>
        public bool IsFailed
        {
            get
            {
                if (failedParts == null || WorldStateManager.Instance == null) return false;
                foreach (var p in failedParts)
                    if (p.fact != null && WorldStateManager.Instance.GetFact(p.fact)) return true;
                return false;
            }
        }
    }
}
