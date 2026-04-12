using UnityEngine;

namespace Game.Core
{
    /// <summary>Key format: Quest.{questId}.{stepKey}</summary>
    [CreateAssetMenu(menuName = "Game/Facts/Quest Fact", fileName = "QuestFact_")]
    public class QuestFact : Fact
    {
        [SerializeField] private string _questId;
        [SerializeField] private string _stepKey;

        /// <summary>Runtime/test initialiser. Asset-based usage sets fields via Inspector.</summary>
        public QuestFact Init(string questId, string stepKey)
        {
            Prefix = WorldFactPrefix.Quest;
            _questId = questId;
            _stepKey = stepKey;
            return this;
        }

        // OnEnable sets Prefix when the asset is loaded from disk (fields are deserialized before OnEnable).
        private void OnEnable() => Prefix = WorldFactPrefix.Quest;

        public override string ToString() => $"{WorldFactPrefix.Quest}.{_questId}.{_stepKey}";
    }
}
