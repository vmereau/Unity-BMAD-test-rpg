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
        private const string TAG = "[QuestFact]";

        [SerializeField] private QuestSO _quest;
        [SerializeField] private int _questState;

        public QuestSO Quest => _quest;

        /// <summary>The base quest state. Only meaningful when IsStepState is false.</summary>
        public QuestState QuestState => (QuestState)_questState;

        /// <summary>True when this fact references a QuestStep rather than a base quest state.</summary>
        public bool IsStepState => _questState >= 3;

        /// <summary>Zero-based index into QuestSO.steps. Only valid when IsStepState is true.</summary>
        public int QuestStepIndex => _questState - 3;

        /// <summary>Runtime/test initialiser. Asset-based usage sets fields via Inspector.</summary>
        public QuestFact Init(QuestSO quest, QuestState state)
        {
            _quest = quest;
            _questState = (int)state;
            return this;
        }

        /// <summary>Runtime/test initialiser for step-state facts.</summary>
        public QuestFact InitStep(QuestSO quest, int stepIndex)
        {
            _quest = quest;
            _questState = stepIndex + 3;
            return this;
        }

        // OnEnable sets Prefix when the asset is loaded from disk (fields are deserialized before OnEnable).
        private void OnEnable() => Prefix = WorldFactPrefix.Quest;

        public override string ToString()
        {
            if (IsStepState)
                return $"Quest.{_quest?.questId ?? "null"}.Step{QuestStepIndex}";
            return $"Quest.{_quest?.questId ?? "null"}.{(QuestState)_questState}";
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (_quest == null || _quest.steps == null) return;
            int maxIndex = 2 + _quest.steps.Count; // 2 = IsFailed (highest base-state index)
            if (_questState > maxIndex)
            {
                GameLog.Warn(TAG, $"'{name}': _questState {_questState} out of range for assigned quest — clamped to 0 (IsStarted)");
                _questState = 0;
            }
        }
#endif
    }
}
