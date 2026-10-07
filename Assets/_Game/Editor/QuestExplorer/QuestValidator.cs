using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Game.Core;
using Game.NPC;
using Game.Quest;

namespace Game.Editor.QuestExplorer
{
    public enum IssueSeverity { Error, Warning, Info }

    public sealed class ValidationIssue
    {
        public IssueSeverity Severity;
        public string Message;
        /// <summary>Object to ping when the issue is clicked (fact, QuestFact, quest). May be null.</summary>
        public UnityEngine.Object Context;
        /// <summary>Part / step row to scroll to when the issue is clicked. May be null.</summary>
        public QuestPartLocation? Location;
        /// <summary>Rule id (V1..V17) — for tests and tooling.</summary>
        public string Rule;

        public override string ToString() => $"{Severity} {Rule}: {Message}";
    }

    /// <summary>Pure validation rules over a <see cref="QuestReferenceIndex"/>. Results sorted Error → Warning → Info.</summary>
    public static class QuestValidator
    {
        private static readonly Regex CountRegex = new Regex(
            @"\b(\d+|two|three|four|five|six|seven|eight|nine|ten)\b",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private static readonly Dictionary<string, int> NumberWords = new Dictionary<string, int>
        {
            { "two", 2 }, { "three", 3 }, { "four", 4 }, { "five", 5 }, { "six", 6 },
            { "seven", 7 }, { "eight", 8 }, { "nine", 9 }, { "ten", 10 }
        };

        public static List<ValidationIssue> Validate(QuestSO quest, QuestReferenceIndex index, IReadOnlyList<QuestSO> allQuests)
        {
            var issues = new List<ValidationIssue>();
            if (quest == null || index == null) return issues;

            ValidateStartAndNullFacts(quest, issues);
            ValidateSetters(quest, index, issues);
            ValidateStructure(quest, issues);
            ValidateRegistration(quest, index, issues);
            ValidateIdentity(quest, allQuests, issues);
            ValidateQuestFacts(quest, index, issues);
            ValidateMemories(quest, index, issues);
            ValidateDescriptionCount(quest, issues);

            return issues.OrderBy(i => (int)i.Severity).ToList(); // OrderBy is stable
        }

        // V1, V2
        private static void ValidateStartAndNullFacts(QuestSO quest, List<ValidationIssue> issues)
        {
            foreach (var (loc, part) in QuestReferenceIndex.EnumerateParts(quest))
            {
                if (part.fact != null) continue;
                if (loc.Slot == QuestPartSlot.Start)
                    Add(issues, "V1", IssueSeverity.Error, "Quest can never start — Start part has no fact", quest, loc);
                else
                    Add(issues, "V2", IssueSeverity.Error, $"{loc} has no fact", quest, loc);
            }
        }

        // V3, V4, V5
        private static void ValidateSetters(QuestSO quest, QuestReferenceIndex index, List<ValidationIssue> issues)
        {
            var reported = new HashSet<Fact>();
            foreach (var (loc, part) in QuestReferenceIndex.EnumerateParts(quest))
            {
                var fact = part.fact;
                if (!IsStored(fact)) continue;
                var setters = index.GetSetters(fact);

                if (setters.Count == 0)
                {
                    string msg = $"'{fact.name}' is never set — {loc} can't complete";
                    if (fact is WorldFact) msg += " (no WorldFact setter exists in code yet)";
                    Add(issues, "V3", IssueSeverity.Error, msg, fact, loc);
                    continue;
                }

                if (!reported.Add(fact)) continue;

                if (fact is DialogueFact && setters.All(s => s.Npc == null))
                    Add(issues, "V4", IssueSeverity.Warning,
                        $"'{fact.name}' is set only by dialogue not reachable from any NPC memory", fact, loc);

                if (fact is KilledFact)
                {
                    int sceneSetters = setters.Count(s => s.Source == FactLinkSource.ScenePersistentID || s.Source == FactLinkSource.ClosedScene);
                    bool prefabSetter = setters.Any(s => s.Source == FactLinkSource.PrefabPersistentID);
                    if (sceneSetters > 1 || prefabSetter)
                        Add(issues, "V5", IssueSeverity.Warning,
                            $"'{fact.name}' is shared by several entities (duplicate GUID)", fact, loc);
                }
            }
        }

        // V6, V7, V8
        private static void ValidateStructure(QuestSO quest, List<ValidationIssue> issues)
        {
            if (quest.steps != null)
                for (int i = 0; i < quest.steps.Count; i++)
                    if (quest.steps[i].parts == null || quest.steps[i].parts.Count == 0)
                        Add(issues, "V6", IssueSeverity.Warning, $"Step {i + 1} has no parts — step can never complete",
                            quest, QuestPartLocation.Step(i));

            if (quest.completedParts == null || quest.completedParts.Count == 0)
                Add(issues, "V7", IssueSeverity.Info, "No completed parts — quest never completes", quest, null);

            foreach (var (loc, part) in QuestReferenceIndex.EnumerateParts(quest))
            {
                if (!string.IsNullOrWhiteSpace(part.entry)) continue;
                if (loc.Slot == QuestPartSlot.Step)
                    Add(issues, "V8", IssueSeverity.Info, $"{loc} has an empty entry", quest, loc);
                else
                    Add(issues, "V8", IssueSeverity.Warning, $"{loc} has an empty entry (shown in the quest log)", quest, loc);
            }
        }

        // V9
        private static void ValidateRegistration(QuestSO quest, QuestReferenceIndex index, List<ValidationIssue> issues)
        {
            if (!index.IsInEventsManager(quest))
                Add(issues, "V9", IssueSeverity.Warning, "Not registered in QuestEventsManager — use Sync (Fix)", quest, null);
            if (!index.IsInQuestLog(quest))
                Add(issues, "V9", IssueSeverity.Warning, "Not listed in QuestLogUI — the quest log won't show it (Fix)", quest, null);
        }

        // V10
        private static void ValidateIdentity(QuestSO quest, IReadOnlyList<QuestSO> allQuests, List<ValidationIssue> issues)
        {
            if (string.IsNullOrWhiteSpace(quest.questId))
            {
                Add(issues, "V10", IssueSeverity.Error, "questId is empty", quest, null);
                return;
            }

            if (allQuests != null)
            {
                var duplicate = allQuests.FirstOrDefault(q => q != null && q != quest && q.questId == quest.questId);
                if (duplicate != null)
                    Add(issues, "V10", IssueSeverity.Error, $"questId '{quest.questId}' is also used by '{duplicate.name}'", duplicate, null);
            }

            string expected = $"Quest_{quest.questId}";
            if (quest.name != expected)
                Add(issues, "V10", IssueSeverity.Warning, $"Asset name '{quest.name}' should be '{expected}'", quest, null);
        }

        // V11, V12
        private static void ValidateQuestFacts(QuestSO quest, QuestReferenceIndex index, List<ValidationIssue> issues)
        {
            int stepCount = quest.steps?.Count ?? 0;
            foreach (var qf in index.GetQuestFactsTargeting(quest))
            {
                if (qf.IsStepState && qf.QuestStepIndex >= stepCount)
                    Add(issues, "V11", IssueSeverity.Error,
                        $"QuestFact '{qf.name}' targets step {qf.QuestStepIndex + 1}, quest has {stepCount} step(s)", qf, null);
            }

            foreach (var (loc, part) in QuestReferenceIndex.EnumerateParts(quest))
            {
                if (!(part.fact is QuestFact qf) || qf.Quest != quest) continue;
                if (FactOrder(qf, stepCount) >= SlotOrder(loc, stepCount))
                    Add(issues, "V12", IssueSeverity.Warning,
                        $"{loc} depends on its own quest state '{QuestReferenceIndex.QuestFactStateLabel(qf)}' (self-dependency)", qf, loc);
            }
        }

        /// <summary>
        /// V14–V17 over every memory in the index (not only the ones involved in a quest) — catches a broken
        /// memory whose last quest link is gone. Sorted Error → Warning.
        /// </summary>
        public static List<ValidationIssue> ValidateAllMemories(QuestReferenceIndex index)
        {
            var issues = new List<ValidationIssue>();
            if (index == null) return issues;
            foreach (var memory in index.AllMemories) ValidateMemory(memory, index, issues);
            return issues.OrderBy(i => (int)i.Severity).ToList();
        }

        // V14, V15, V16, V17 — only memories involved in this quest, so they surface in its Issues list.
        private static void ValidateMemories(QuestSO quest, QuestReferenceIndex index, List<ValidationIssue> issues)
        {
            foreach (var im in index.GetInvolvedMemories(quest)) ValidateMemory(im.Memory, index, issues);
        }

        private static void ValidateMemory(NPCMemoryEntrySO memory, QuestReferenceIndex index, List<ValidationIssue> issues)
        {
            var unlock = memory.unlockConditions;
            if (unlock != null)
                for (int i = 0; i < unlock.Length; i++)
                    if (unlock[i] == null)
                        Add(issues, "V14", IssueSeverity.Error,
                            $"{memory.name}: unlock condition #{i + 1} is missing — skipped at runtime, so the memory unlocks without it",
                            memory, null);
            var invalidation = memory.invalidationConditions;
            if (invalidation != null)
                for (int i = 0; i < invalidation.Length; i++)
                    if (invalidation[i] == null)
                        Add(issues, "V14", IssueSeverity.Error,
                            $"{memory.name}: invalidation condition #{i + 1} is missing — skipped at runtime, so it never closes the memory",
                            memory, null);

            var gates = index.GetGates(memory);
            if (memory.effects?.startdialog == null && gates.Count == 0)
                Add(issues, "V15", IssueSeverity.Warning,
                    $"{memory.name} has no start dialogue and gates no choice — it has no effect", memory, null);

            if (!index.IsOwnedByNpc(memory))
                Add(issues, "V16", IssueSeverity.Warning,
                    $"{memory.name} is not listed on any NPC — move it under Data/NPCs/NPC_X/Memories/ (NPCMemoriesAutoSync)",
                    memory, null);

            foreach (var gate in gates)
            {
                if (gate.Npc == null) continue;
                if (gate.Npc.memories != null && gate.Npc.memories.Contains(memory)) continue;
                Add(issues, "V17", IssueSeverity.Error,
                    $"{gate.Label} requires {memory.name}, which {QuestReferenceIndex.NpcDisplayName(gate.Npc)} doesn't have — the choice never shows",
                    memory, null);
            }
        }

        // Progression order: Start (0) → step i (1 + i) → Completed / Failed (1 + stepCount).
        private static int SlotOrder(QuestPartLocation loc, int stepCount) => loc.Slot switch
        {
            QuestPartSlot.Start => 0,
            QuestPartSlot.Step => 1 + loc.StepIndex,
            _ => 1 + stepCount
        };

        private static int FactOrder(QuestFact qf, int stepCount)
        {
            if (qf.IsStepState) return 1 + qf.QuestStepIndex;
            return qf.QuestState == QuestState.IsStarted ? 0 : 1 + stepCount;
        }

        // V13
        private static void ValidateDescriptionCount(QuestSO quest, List<ValidationIssue> issues)
        {
            if (string.IsNullOrEmpty(quest.description)) return;
            var counts = (quest.steps ?? new List<QuestStep>()).Select(s => s.parts?.Count ?? 0).ToList();
            var seen = new HashSet<int>();
            foreach (Match m in CountRegex.Matches(quest.description))
            {
                string token = m.Groups[1].Value;
                int n;
                if (!int.TryParse(token, out n) && !NumberWords.TryGetValue(token.ToLowerInvariant(), out n)) continue;
                if (n < 2 || n > 20 || !seen.Add(n)) continue;
                if (counts.Contains(n)) continue;
                string list = counts.Count == 0 ? "(no steps)" : string.Join(", ", counts);
                Add(issues, "V13", IssueSeverity.Info, $"Description says {n} but step part counts are {list}", quest, null);
            }
        }

        public static bool IsStored(Fact fact) => fact is KilledFact || fact is DialogueFact || fact is WorldFact;

        /// <summary>
        /// False for computed facts missing their target (WorldStateManager would log a warning on every
        /// evaluation) — live polling skips them.
        /// </summary>
        public static bool IsEvaluable(Fact fact) => fact switch
        {
            null => false,
            QuestFact qf => qf.Quest != null,
            SkillFact sf => sf.Skill != null,
            _ => true
        };

        private static void Add(List<ValidationIssue> issues, string rule, IssueSeverity severity, string message,
            UnityEngine.Object context, QuestPartLocation? location)
        {
            issues.Add(new ValidationIssue { Rule = rule, Severity = severity, Message = message, Context = context, Location = location });
        }
    }
}
