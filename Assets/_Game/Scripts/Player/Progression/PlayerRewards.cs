using System.Collections.Generic;
using Game.Core;
using Game.Economy;
using Game.Player;
using Game.Quest;
using UnityEngine;

namespace Game.Progression
{
    public class PlayerRewards : MonoBehaviour
    {
        private const string TAG = "[PlayerRewards]";

        // ── Required ──────────────────────────────────────────────────────────
        [SerializeField] private GameEventSO_KilledFact   _onEntityKilled;
        [SerializeField] private XPSystem                 _xpSystem;

        // ── Dialogue event channel ────────────────────────────────────────────
        [SerializeField] private GameEventSO_DialogueFact _onDialoguePlayed;

        // ── Quest event channels (same SO assets as QuestEventsManager) ───────
        [SerializeField] private GameEventSO_Quest     _onQuestStarted;
        [SerializeField] private GameEventSO_Quest     _onQuestCompleted;
        [SerializeField] private GameEventSO_Quest     _onQuestFailed;
        [SerializeField] private GameEventSO_QuestStep _onQuestStepCompleted;

        // ── Optional reward systems ───────────────────────────────────────────
        [SerializeField] private LearningPointSystem _lpSystem;
        [SerializeField] private PlayerStats         _playerStats;
        [SerializeField] private GoldSystem          _goldSystem;

        // ── Reward definitions ────────────────────────────────────────────────
        [SerializeField] private List<PlayerRewardSO> _rewards = new List<PlayerRewardSO>();

        private void Awake()
        {
            if (_xpSystem == null)
            {
                GameLog.Error(TAG, "XPSystem not assigned — PlayerRewards disabled.");
                enabled = false;
                return;
            }
            if (_onEntityKilled       == null) GameLog.Warn(TAG, "OnEntityKilled not assigned — no XP from kills.");
            if (_onDialoguePlayed     == null) GameLog.Warn(TAG, "OnDialoguePlayed not assigned — no dialogue rewards.");
            if (_onQuestStarted       == null) GameLog.Warn(TAG, "OnQuestStarted not assigned — no quest-started rewards.");
            if (_onQuestCompleted     == null) GameLog.Warn(TAG, "OnQuestCompleted not assigned — no quest-completed rewards.");
            if (_onQuestFailed        == null) GameLog.Warn(TAG, "OnQuestFailed not assigned — no quest-failed rewards.");
            if (_onQuestStepCompleted == null) GameLog.Warn(TAG, "OnQuestStepCompleted not assigned — no quest-step rewards.");
            if (_lpSystem             == null) GameLog.Warn(TAG, "LearningPointSystem not assigned — LP rewards skipped.");
            if (_playerStats          == null) GameLog.Warn(TAG, "PlayerStats not assigned — stat rewards skipped.");
            if (_goldSystem           == null) GameLog.Warn(TAG, "GoldSystem not assigned — gold rewards skipped.");
        }

        private void OnEnable()
        {
            if (_onEntityKilled       != null) _onEntityKilled.AddListener(HandleEntityKilled);
            if (_onDialoguePlayed     != null) _onDialoguePlayed.AddListener(HandleDialoguePlayed);
            if (_onQuestStarted       != null) _onQuestStarted.AddListener(HandleQuestStarted);
            if (_onQuestCompleted     != null) _onQuestCompleted.AddListener(HandleQuestCompleted);
            if (_onQuestFailed        != null) _onQuestFailed.AddListener(HandleQuestFailed);
            if (_onQuestStepCompleted != null) _onQuestStepCompleted.AddListener(HandleQuestStepCompleted);
        }

        private void OnDisable()
        {
            // Guard: Awake may disable before OnEnable runs
            if (_onEntityKilled       != null) _onEntityKilled.RemoveListener(HandleEntityKilled);
            if (_onDialoguePlayed     != null) _onDialoguePlayed.RemoveListener(HandleDialoguePlayed);
            if (_onQuestStarted       != null) _onQuestStarted.RemoveListener(HandleQuestStarted);
            if (_onQuestCompleted     != null) _onQuestCompleted.RemoveListener(HandleQuestCompleted);
            if (_onQuestFailed        != null) _onQuestFailed.RemoveListener(HandleQuestFailed);
            if (_onQuestStepCompleted != null) _onQuestStepCompleted.RemoveListener(HandleQuestStepCompleted);
        }

        // ── Kill handler ──────────────────────────────────────────────────────

        private void HandleEntityKilled(KilledFact fact)
        {
            // Base XP from the entity's EnemyTypeSO (always fires first)
            int baseXp = fact?.EnemyType?.XpOnKill ?? 0;
            if (baseXp > 0)
                _xpSystem.GiveExperience(baseXp);

            // Bonus rewards from matching PlayerRewardSO (e.g. special boss also gives LP)
            foreach (var reward in _rewards)
            {
                if (reward != null && reward.MatchesKilledFact(fact))
                    ApplyRewards(reward);
            }
        }

        // ── Dialogue handler ──────────────────────────────────────────────────

        private void HandleDialoguePlayed(DialogueFact fact)
        {
            foreach (var reward in _rewards)
            {
                if (reward != null && reward.MatchesDialogueFact(fact))
                    ApplyRewards(reward);
            }
        }

        // ── Quest handlers ────────────────────────────────────────────────────

        private void HandleQuestStarted(QuestSO quest)
        {
            foreach (var reward in _rewards)
            {
                if (reward != null && reward.MatchesQuestState(quest, QuestState.IsStarted))
                    ApplyRewards(reward);
            }
        }

        private void HandleQuestCompleted(QuestSO quest)
        {
            foreach (var reward in _rewards)
            {
                if (reward != null && reward.MatchesQuestState(quest, QuestState.IsCompleted))
                    ApplyRewards(reward);
            }
        }

        private void HandleQuestFailed(QuestSO quest)
        {
            foreach (var reward in _rewards)
            {
                if (reward != null && reward.MatchesQuestState(quest, QuestState.IsFailed))
                    ApplyRewards(reward);
            }
        }

        private void HandleQuestStepCompleted(QuestStepData data)
        {
            foreach (var reward in _rewards)
            {
                if (reward != null && reward.MatchesQuestStep(data.quest, data.stepIndex))
                    ApplyRewards(reward);
            }
        }

        // ── Reward applicator ─────────────────────────────────────────────────

        private void ApplyRewards(PlayerRewardSO reward)
        {
            if (reward.XpReward > 0)
                _xpSystem.GiveExperience(reward.XpReward);

            if (reward.LpReward > 0 && _lpSystem != null)
                _lpSystem.GiveLp(reward.LpReward);

            if (reward.GoldReward > 0 && _goldSystem != null)
                _goldSystem.Add(reward.GoldReward);

            if (_playerStats != null)
            {
                foreach (var statReward in reward.StatRewards)
                {
                    if (statReward.points > 0)
                        _playerStats.UpgradeStat(statReward.statType, statReward.points);
                }
            }
        }
    }
}
