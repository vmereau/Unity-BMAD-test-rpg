using Game.Core;
using UnityEngine;

namespace Game.Quest
{
    [System.Serializable]
    public struct QuestStepData
    {
        public QuestSO quest;
        public int stepIndex;
    }

    /// <summary>Typed event channel fired when a QuestStep transitions to completed.</summary>
    [CreateAssetMenu(menuName = "Game/Events/Quest Step Event", fileName = "OnQuestStep")]
    public class GameEventSO_QuestStep : GameEventSO<QuestStepData> { }
}
