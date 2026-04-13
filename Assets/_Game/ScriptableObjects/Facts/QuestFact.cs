using Game.Quest;
using UnityEngine;

namespace Game.Core
{
    public enum QuestState { IsStarted, IsCompleted, IsFailed }

    /// <summary>Computed fact — references a QuestSO and a QuestState.
    /// NOT stored in WorldStateManager._worldFacts; evaluate via WorldStateManager.IsQuestFactTrue().</summary>
    [CreateAssetMenu(menuName = "Game/Facts/Quest Fact", fileName = "QuestFact_")]
    public class QuestFact : Fact
    {
        [SerializeField] private QuestSO _quest;
        [SerializeField] private QuestState _questState;

        public QuestSO Quest => _quest;
        public QuestState QuestState => _questState;

        /// <summary>Runtime/test initialiser. Asset-based usage sets fields via Inspector.</summary>
        public QuestFact Init(QuestSO quest, QuestState state)
        {
            _quest = quest;
            _questState = state;
            return this;
        }

        // OnEnable sets Prefix when the asset is loaded from disk (fields are deserialized before OnEnable).
        private void OnEnable() => Prefix = WorldFactPrefix.Quest;

        public override string ToString() => $"Quest.{_quest?.questId ?? "null"}.{_questState}";
    }
}
