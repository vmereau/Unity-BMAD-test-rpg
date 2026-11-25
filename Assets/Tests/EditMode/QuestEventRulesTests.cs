using System.Collections.Generic;
using Game.Quest;
using NUnit.Framework;

namespace Tests.EditMode
{
    /// <summary>spec-quest-events-start-guard: quest events wait for the quest to start.</summary>
    public class QuestEventRulesTests
    {
        private readonly List<QuestEventEntry> _events = new List<QuestEventEntry>();

        private static QuestReportedState State(bool started, bool completed = false, bool failed = false, params bool[] steps) =>
            QuestEventRules.Report(started, completed, failed, (bool[])steps.Clone());

        private List<QuestEventEntry> Diff(QuestReportedState prev, QuestReportedState now)
        {
            _events.Clear();
            QuestEventRules.Transitions(prev, now, _events);
            return _events;
        }

        private static void AssertEvents(List<QuestEventEntry> events, params (QuestEventKind kind, int step)[] expected)
        {
            Assert.AreEqual(expected.Length, events.Count, "event count");
            for (int i = 0; i < expected.Length; i++)
            {
                Assert.AreEqual(expected[i].kind, events[i].kind, $"event {i} kind");
                Assert.AreEqual(expected[i].step, events[i].stepIndex, $"event {i} step");
            }
        }

        [Test]
        public void StepBeforeStart_RaisesNothing()
        {
            var prev = State(false, steps: new[] { false, false });
            var now = State(false, steps: new[] { true, false });
            AssertEvents(Diff(prev, now));
        }

        [Test]
        public void StartWithStepsDone_RaisesStartedThenStepsInOrder()
        {
            var prev = State(false, steps: new[] { true, false, true });
            var now = State(true, steps: new[] { true, false, true });
            AssertEvents(Diff(prev, now),
                (QuestEventKind.Started, -1), (QuestEventKind.StepCompleted, 0), (QuestEventKind.StepCompleted, 2));
        }

        [Test]
        public void StartAlreadyCompleted_RaisesStartedStepThenCompleted()
        {
            var prev = State(false, completed: true, steps: new[] { false, true });
            var now = State(true, completed: true, steps: new[] { false, true });
            AssertEvents(Diff(prev, now),
                (QuestEventKind.Started, -1), (QuestEventKind.StepCompleted, 1), (QuestEventKind.Completed, -1));
        }

        [Test]
        public void FailedBeforeStart_RaisesNothingThenStartedAndFailed()
        {
            var initial = State(false);
            var failedOnly = State(false, failed: true);
            AssertEvents(Diff(initial, failedOnly));
            var started = State(true, failed: true);
            AssertEvents(Diff(failedOnly, started), (QuestEventKind.Started, -1), (QuestEventKind.Failed, -1));
        }

        [Test]
        public void NormalProgress_RaisesOnlyTheNewStep()
        {
            var prev = State(true, steps: new[] { true, false });
            var now = State(true, steps: new[] { true, true });
            AssertEvents(Diff(prev, now), (QuestEventKind.StepCompleted, 1));
        }

        [Test]
        public void ReseededStartedQuest_RaisesNothingWithoutChange()
        {
            var seeded = State(true, steps: new[] { true, false });
            var now = State(true, steps: new[] { true, false });
            AssertEvents(Diff(seeded, now));
        }

        [Test]
        public void ReseededUnstartedQuestWithStepDone_RaisesStepWhenStarted()
        {
            var seeded = State(false, steps: new[] { true });
            Assert.IsFalse(seeded.Step(0), "a step is not reported before the quest starts");
            var now = State(true, steps: new[] { true });
            AssertEvents(Diff(seeded, now), (QuestEventKind.Started, -1), (QuestEventKind.StepCompleted, 0));
        }

        [Test]
        public void StepCountChanged_MissingPreviousStepsCountAsFalse()
        {
            var prev = State(true, steps: new[] { true });
            var now = State(true, steps: new[] { true, true, false });
            AssertEvents(Diff(prev, now), (QuestEventKind.StepCompleted, 1));
        }

        [Test]
        public void LastStepAndCompletionTogether_StepBeforeCompleted()
        {
            var prev = State(true, steps: new[] { false });
            var now = State(true, completed: true, steps: new[] { true });
            AssertEvents(Diff(prev, now), (QuestEventKind.StepCompleted, 0), (QuestEventKind.Completed, -1));
        }

        [Test]
        public void StartFactDropsThenReturns_LatchedStartReraisesNothing()
        {
            var reported = State(true, completed: true, steps: new[] { true });
            var dropped = QuestEventRules.Report(reported, false, true, false, new[] { true });
            Assert.IsTrue(dropped.started, "a reported start stays latched");
            AssertEvents(Diff(reported, dropped));
            var back = QuestEventRules.Report(dropped, true, true, false, new[] { true });
            AssertEvents(Diff(dropped, back));
        }

        [Test]
        public void Report_NullSteps_GivesEmptySteps()
        {
            var state = QuestEventRules.Report(true, false, false, null);
            Assert.IsNotNull(state.steps);
            Assert.AreEqual(0, state.steps.Length);
        }
    }
}
