using System;
using System.Collections.Generic;
using Game.Player;
using Game.Quest;
using UnityEngine;

namespace Game.Progression
{
    public enum RewardFactType { Killed, Quest, Dialogue }

    [Serializable]
    public struct StatReward
    {
        public StatType statType;
        public int points;
    }

    [CreateAssetMenu(menuName = "Game/Rewards/Player Reward", fileName = "PlayerReward_")]
    public class PlayerRewardSO : ScriptableObject
    {
        [SerializeField] private RewardFactType _factType;

        [SerializeField] private Game.Core.KilledFact   _killedFact;
        [SerializeField] private Game.Core.QuestFact    _questFact;
        [SerializeField] private Game.Core.DialogueFact _dialogueFact;

        [Header("Rewards")]
        [SerializeField] private int _xpReward;
        [SerializeField] private int _lpReward;
        [SerializeField] private int _goldReward;
        [SerializeField] private List<StatReward> _statRewards = new List<StatReward>();

        public RewardFactType           FactType     => _factType;
        public Game.Core.KilledFact   KilledFact   => _killedFact;
        public Game.Core.QuestFact    QuestFact    => _questFact;
        public Game.Core.DialogueFact DialogueFact => _dialogueFact;
        public int XpReward   => _xpReward;
        public int LpReward   => _lpReward;
        public int GoldReward => _goldReward;
        public IReadOnlyList<StatReward> StatRewards => _statRewards;

        public bool MatchesKilledFact(Game.Core.KilledFact fact) =>
            _factType == RewardFactType.Killed && _killedFact == fact;

        public bool MatchesDialogueFact(Game.Core.DialogueFact fact) =>
            _factType == RewardFactType.Dialogue && _dialogueFact == fact;

        public bool MatchesQuestState(QuestSO quest, Game.Core.QuestState state) =>
            _factType == RewardFactType.Quest &&
            _questFact != null &&
            !_questFact.IsStepState &&
            _questFact.Quest == quest &&
            _questFact.QuestState == state;

        public bool MatchesQuestStep(QuestSO quest, int stepIndex) =>
            _factType == RewardFactType.Quest &&
            _questFact != null &&
            _questFact.IsStepState &&
            _questFact.Quest == quest &&
            _questFact.QuestStepIndex == stepIndex;
    }
}
