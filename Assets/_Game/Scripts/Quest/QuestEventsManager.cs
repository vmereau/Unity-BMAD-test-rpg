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

        private readonly Dictionary<QuestSO, QuestReportedState> _lastState
            = new Dictionary<QuestSO, QuestReportedState>();
        private readonly List<QuestEventEntry> _events = new List<QuestEventEntry>();
        private bool _evaluating;
        private bool _dirty; // a fact changed while events were being raised: evaluate again once they are done

        private void Start() => ReseedState();

        /// <summary>
        /// Re-reads every quest's reported state without firing events. Called on start and after a save is
        /// loaded, so the next real fact change doesn't raise stale started / completed events.
        /// </summary>
        public void ReseedState()
        {
            _lastState.Clear();
            foreach (var quest in _quests)
            {
                if (quest == null) continue;
                if (quest.startPart.fact == null)
                    GameLog.Warn(TAG, $"Quest '{quest.title}' has no start fact — it never starts, so it raises no step / completed / failed event");
                _lastState[quest] = ReadReportedState(quest, default);
            }
        }

        // Completed, failed and steps only count once the quest is started (spec-quest-events-start-guard).
        // A quest already reported as started stays started (prev).
        private static QuestReportedState ReadReportedState(QuestSO quest, in QuestReportedState prev)
        {
            int count = quest.steps?.Count ?? 0;
            var steps = count == 0 ? System.Array.Empty<bool>() : new bool[count];
            for (int i = 0; i < count; i++)
                steps[i] = quest.IsStepCompleted(i);
            return QuestEventRules.Report(prev, quest.IsStarted, quest.IsCompleted, quest.IsFailed, steps);
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
            // A listener setting a fact while events are raised re-enters here: run that evaluation after the current
            // one, so every quest's events keep their order (started → steps → completed → failed).
            if (_evaluating) { _dirty = true; return; }
            _evaluating = true;
            try
            {
                do
                {
                    _dirty = false;
                    foreach (var quest in _quests)
                    {
                        if (quest == null) continue;
                        EvaluateQuest(quest);
                    }
                } while (_dirty);
            }
            finally { _evaluating = false; }
        }

        private void EvaluateQuest(QuestSO quest)
        {
            if (!_lastState.TryGetValue(quest, out var prev))
            {
                // Quest not yet tracked — seed snapshot without firing events.
                _lastState[quest] = ReadReportedState(quest, default);
                return;
            }
            QuestReportedState now = ReadReportedState(quest, prev);
            _lastState[quest] = now;

            _events.Clear();
            QuestEventRules.Transitions(prev, now, _events);
            foreach (var e in _events)
            {
                switch (e.kind)
                {
                    case QuestEventKind.Started:
                        GameLog.Info(TAG, $"Quest started: '{quest.title}'");
                        _onQuestStarted?.Raise(quest);
                        break;
                    case QuestEventKind.StepCompleted:
                        GameLog.Info(TAG, $"Quest step completed: '{quest.title}' step [{e.stepIndex}] '{quest.steps[e.stepIndex].title}'");
                        _onQuestStepCompleted?.Raise(new QuestStepData { quest = quest, stepIndex = e.stepIndex });
                        break;
                    case QuestEventKind.Completed:
                        GameLog.Info(TAG, $"Quest completed: '{quest.title}'");
                        _onQuestCompleted?.Raise(quest);
                        break;
                    case QuestEventKind.Failed:
                        GameLog.Info(TAG, $"Quest failed: '{quest.title}'");
                        _onQuestFailed?.Raise(quest);
                        break;
                }
            }
        }
    }
}
