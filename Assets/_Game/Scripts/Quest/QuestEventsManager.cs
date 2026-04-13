using System.Collections.Generic;
using Game.Core;
using UnityEngine;

namespace Game.Quest
{
    /// <summary>
    /// Monitors registered quests for state transitions (started, completed, failed)
    /// by reacting to world fact changes. Fires GameEventSO_Quest channels on each transition.
    /// Attach to a standalone QuestEventsManager GameObject in the scene.
    /// </summary>
    public class QuestEventsManager : MonoBehaviour
    {
        private const string TAG = "[QuestEvents]";

        [SerializeField] private List<QuestSO> _quests = new List<QuestSO>();
        [SerializeField] private GameEventSO_WorldFact _onWorldFactChanged;

        [Header("Output Event Channels")]
        [SerializeField] private GameEventSO_Quest _onQuestStarted;
        [SerializeField] private GameEventSO_Quest _onQuestCompleted;
        [SerializeField] private GameEventSO_Quest _onQuestFailed;

        private readonly Dictionary<QuestSO, QuestStateSnapshot> _lastState
            = new Dictionary<QuestSO, QuestStateSnapshot>();

        private struct QuestStateSnapshot
        {
            public bool started;
            public bool completed;
            public bool failed;
        }

        private void Start()
        {
            // Seed initial state without firing events (prevents spurious transitions on scene load).
            foreach (var quest in _quests)
            {
                if (quest == null) continue;
                _lastState[quest] = new QuestStateSnapshot
                {
                    started   = quest.IsStarted,
                    completed = quest.IsCompleted,
                    failed    = quest.IsFailed
                };
            }
        }

        private void OnEnable()
        {
            if (_onWorldFactChanged == null)
            {
                GameLog.Warn(TAG, "OnWorldFactChanged not assigned — QuestEventsManager will not respond to fact changes");
                return;
            }
            _onWorldFactChanged.AddListener(HandleWorldFactChanged);
        }

        private void OnDisable()
        {
            if (_onWorldFactChanged == null) return;
            _onWorldFactChanged.RemoveListener(HandleWorldFactChanged);
        }

        private void HandleWorldFactChanged(WorldFactData _)
        {
            foreach (var quest in _quests)
            {
                if (quest == null) continue;
                EvaluateQuest(quest);
            }
        }

        private void EvaluateQuest(QuestSO quest)
        {
            bool isStarted   = quest.IsStarted;
            bool isCompleted = quest.IsCompleted;
            bool isFailed    = quest.IsFailed;

            if (!_lastState.TryGetValue(quest, out var prev))
                prev = default;

            if (!prev.started && isStarted)
            {
                GameLog.Info(TAG, $"Quest started: '{quest.title}'");
                _onQuestStarted?.Raise(quest);
            }
            if (!prev.completed && isCompleted)
            {
                GameLog.Info(TAG, $"Quest completed: '{quest.title}'");
                _onQuestCompleted?.Raise(quest);
            }
            if (!prev.failed && isFailed)
            {
                GameLog.Info(TAG, $"Quest failed: '{quest.title}'");
                _onQuestFailed?.Raise(quest);
            }

            _lastState[quest] = new QuestStateSnapshot
            {
                started   = isStarted,
                completed = isCompleted,
                failed    = isFailed
            };
        }
    }
}
