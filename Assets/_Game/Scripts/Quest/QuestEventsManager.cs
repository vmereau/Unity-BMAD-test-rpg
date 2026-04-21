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
        [SerializeField] private GameEventSO_Fact onFactChanged;

        [Header("Output Event Channels")]
        [SerializeField] private GameEventSO_Quest _onQuestStarted;
        [SerializeField] private GameEventSO_Quest _onQuestCompleted;
        [SerializeField] private GameEventSO_Quest _onQuestFailed;
        [SerializeField] private GameEventSO_QuestStep _onQuestStepCompleted;

        private readonly Dictionary<QuestSO, QuestStateSnapshot> _lastState
            = new Dictionary<QuestSO, QuestStateSnapshot>();

        private struct QuestStateSnapshot
        {
            public bool started;
            public bool completed;
            public bool failed;
            public bool[] stepCompleted; // indexed by quest.steps index
        }

        private void Start()
        {
            // Seed initial state without firing events (prevents spurious transitions on scene load).
            foreach (var quest in _quests)
            {
                if (quest == null) continue;
                _lastState[quest] = new QuestStateSnapshot
                {
                    started       = quest.IsStarted,
                    completed     = quest.IsCompleted,
                    failed        = quest.IsFailed,
                    stepCompleted = BuildStepSnapshot(quest, null)
                };
            }
        }

        private static bool[] BuildStepSnapshot(QuestSO quest, bool[] existing = null)
        {
            int count = quest.steps?.Count ?? 0;
            if (count == 0) return System.Array.Empty<bool>();
            var arr = (existing != null && existing.Length == count) ? existing : new bool[count];
            for (int i = 0; i < arr.Length; i++)
                arr[i] = quest.IsStepCompleted(i);
            return arr;
        }

        private void OnEnable()
        {
            if (onFactChanged == null)
            {
                GameLog.Warn(TAG, "OnWorldFactChanged not assigned — QuestEventsManager will not respond to fact changes");
                return;
            }
            onFactChanged.AddListener(HandleWorldFactChanged);
        }

        private void OnDisable()
        {
            if (onFactChanged == null) return;
            onFactChanged.RemoveListener(HandleWorldFactChanged);
        }

        private void HandleWorldFactChanged(FactData _)
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
            {
                // Quest not yet tracked — seed snapshot without firing events.
                _lastState[quest] = new QuestStateSnapshot
                {
                    started       = isStarted,
                    completed     = isCompleted,
                    failed        = isFailed,
                    stepCompleted = BuildStepSnapshot(quest)
                };
                return;
            }

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

            // Step completion — fires after base-state events in this call
            var prevSteps = prev.stepCompleted ?? System.Array.Empty<bool>();
            for (int i = 0; i < quest.steps.Count; i++)
            {
                bool nowDone = quest.IsStepCompleted(i);
                bool wasDone = i < prevSteps.Length && prevSteps[i];
                if (!wasDone && nowDone)
                {
                    GameLog.Info(TAG, $"Quest step completed: '{quest.title}' step [{i}] '{quest.steps[i].title}'");
                    _onQuestStepCompleted?.Raise(new QuestStepData { quest = quest, stepIndex = i });
                }
            }

            _lastState[quest] = new QuestStateSnapshot
            {
                started       = isStarted,
                completed     = isCompleted,
                failed        = isFailed,
                stepCompleted = BuildStepSnapshot(quest, prev.stepCompleted)
            };
        }
    }
}
