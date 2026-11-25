using System.Collections.Generic;

namespace Game.Quest
{
    /// <summary>What a quest has reported so far: completed, failed and steps count only while it is started.</summary>
    public struct QuestReportedState
    {
        public bool started;
        public bool completed;
        public bool failed;
        public bool[] steps; // indexed by QuestSO.steps index

        public bool Step(int index) => steps != null && index >= 0 && index < steps.Length && steps[index];
    }

    public enum QuestEventKind { Started, StepCompleted, Completed, Failed }

    public readonly struct QuestEventEntry
    {
        public readonly QuestEventKind kind;
        public readonly int stepIndex; // StepCompleted only, -1 otherwise

        public QuestEventEntry(QuestEventKind kind, int stepIndex = -1)
        {
            this.kind = kind;
            this.stepIndex = stepIndex;
        }
    }

    /// <summary>
    /// Pure rules behind <see cref="QuestEventsManager"/> (spec-quest-events-start-guard): a quest reports nothing but
    /// "started" until it starts, and on its start evaluation raises everything already true, in order
    /// started → steps (by index) → completed → failed.
    /// </summary>
    public static class QuestEventRules
    {
        /// <summary>
        /// Reported state from the quest's raw values. <paramref name="rawSteps"/> is taken over (not copied) and
        /// cleared when the quest is not started; null = no steps.
        /// </summary>
        public static QuestReportedState Report(bool started, bool completed, bool failed, bool[] rawSteps)
        {
            bool[] steps = rawSteps ?? System.Array.Empty<bool>();
            if (!started) System.Array.Clear(steps, 0, steps.Length);
            return new QuestReportedState
            {
                started   = started,
                completed = started && completed,
                failed    = started && failed,
                steps     = steps
            };
        }

        /// <summary>
        /// Same as <see cref="Report(bool,bool,bool,bool[])"/>, but a quest already reported as started stays started
        /// (its start fact turning false then true again re-raises nothing).
        /// </summary>
        public static QuestReportedState Report(in QuestReportedState prev, bool started, bool completed, bool failed, bool[] rawSteps) =>
            Report(prev.started || started, completed, failed, rawSteps);

        /// <summary>
        /// Appends to <paramref name="events"/> every false → true transition from <paramref name="prev"/> to
        /// <paramref name="now"/>, in raise order. A step missing from <paramref name="prev"/> counts as false.
        /// </summary>
        public static void Transitions(in QuestReportedState prev, in QuestReportedState now, List<QuestEventEntry> events)
        {
            if (events == null) return;
            if (!prev.started && now.started) events.Add(new QuestEventEntry(QuestEventKind.Started));
            int count = now.steps?.Length ?? 0;
            for (int i = 0; i < count; i++)
                if (now.steps[i] && !prev.Step(i)) events.Add(new QuestEventEntry(QuestEventKind.StepCompleted, i));
            if (!prev.completed && now.completed) events.Add(new QuestEventEntry(QuestEventKind.Completed));
            if (!prev.failed && now.failed) events.Add(new QuestEventEntry(QuestEventKind.Failed));
        }
    }
}
