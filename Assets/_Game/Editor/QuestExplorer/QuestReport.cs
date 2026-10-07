using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Game.Core;
using Game.NPC;
using Game.Quest;

namespace Game.Editor.QuestExplorer
{
    /// <summary>
    /// Plain-text (Markdown) view of the Quest Explorer data — what the window shows, readable without UI.
    /// Built for Claude (MCP <c>quest_report</c> tool or <c>execute_code</c>), also handy for logs and diffs.
    /// Everything except <see cref="Run(string)"/> is pure over a <see cref="QuestReferenceIndex"/>.
    /// </summary>
    public static class QuestReport
    {
        public const string USAGE =
            "Targets: 'list' (all quests), 'audit' (every quest + every NPC memory), a quest (questId, asset name " +
            "or title), a fact asset name, or a memory asset name. Case-insensitive.";

        /// <summary>Collects the project data (AssetDatabase, loaded scenes, prefabs) and reports on <paramref name="target"/>.</summary>
        public static string Run(string target) => Run(target, QuestIndexCollector.BuildIndex());

        public static string Run(string target, QuestReferenceIndex index)
        {
            if (index == null) return "No index.";
            string t = (target ?? string.Empty).Trim();
            if (t.Length == 0 || Is(t, "list")) return List(index);
            if (Is(t, "audit")) return Audit(index);

            var quest = index.Quests.FirstOrDefault(q => Is(t, q.questId) || Is(t, q.name) || Is(t, q.title));
            if (quest != null) return Quest(quest, index);
            var fact = index.AllFacts.FirstOrDefault(f => Is(t, f.name));
            if (fact != null) return Fact(fact, index);
            var memory = index.AllMemories.FirstOrDefault(m => Is(t, m.name));
            if (memory != null) return Memory(memory, index);

            return $"No quest, fact or memory named '{t}'. {USAGE}";
        }

        // ── List / audit ──────────────────────────────────────────────────────

        public static string List(QuestReferenceIndex index)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"# Quests ({index.Quests.Count})").AppendLine();
            sb.AppendLine("| questId | Asset | Title | Steps | Errors | Warnings | Info |");
            sb.AppendLine("|---|---|---|---|---|---|---|");
            foreach (var quest in index.Quests.OrderBy(q => q.name, StringComparer.Ordinal))
            {
                var issues = QuestValidator.Validate(quest, index, index.Quests);
                sb.AppendLine($"| {quest.questId} | {quest.name} | {quest.title} | {quest.steps?.Count ?? 0} | " +
                              $"{Count(issues, IssueSeverity.Error)} | {Count(issues, IssueSeverity.Warning)} | {Count(issues, IssueSeverity.Info)} |");
            }
            sb.AppendLine().AppendLine($"Facts: {index.AllFacts.Count} · NPC memories: {index.AllMemories.Count}");
            return sb.ToString();
        }

        /// <summary>Every quest's issues, then V14–V17 over every memory (including ones tied to no quest).</summary>
        public static string Audit(QuestReferenceIndex index)
        {
            var sb = new StringBuilder();
            int errors = 0, warnings = 0, infos = 0;
            sb.AppendLine("# Quest audit").AppendLine();
            foreach (var quest in index.Quests.OrderBy(q => q.name, StringComparer.Ordinal))
            {
                var issues = QuestValidator.Validate(quest, index, index.Quests);
                errors += Count(issues, IssueSeverity.Error);
                warnings += Count(issues, IssueSeverity.Warning);
                infos += Count(issues, IssueSeverity.Info);
                sb.AppendLine($"## {quest.name} ({issues.Count} issue(s))");
                AppendIssues(sb, issues);
                sb.AppendLine();
            }

            var memoryIssues = QuestValidator.ValidateAllMemories(index);
            errors += Count(memoryIssues, IssueSeverity.Error);
            warnings += Count(memoryIssues, IssueSeverity.Warning);
            sb.AppendLine($"## NPC memories — all {index.AllMemories.Count} ({memoryIssues.Count} issue(s))");
            AppendIssues(sb, memoryIssues);
            sb.AppendLine().AppendLine($"**Total:** {errors} error(s), {warnings} warning(s), {infos} info. " +
                                       "Quest sections may repeat memory issues listed again in the memory section.");
            return sb.ToString();
        }

        // ── Quest ─────────────────────────────────────────────────────────────

        public static string Quest(QuestSO quest, QuestReferenceIndex index)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"# Quest: {QuestReferenceIndex.QuestDisplayName(quest)} ({quest.name})");
            sb.AppendLine($"questId: `{quest.questId}` · QuestEventsManager: {YesNo(index.IsInEventsManager(quest))} · " +
                          $"QuestLogUI: {YesNo(index.IsInQuestLog(quest))}");
            if (!string.IsNullOrEmpty(quest.description)) sb.AppendLine().AppendLine($"> {OneLine(quest.description)}");

            sb.AppendLine().AppendLine("## Issues");
            AppendIssues(sb, QuestValidator.Validate(quest, index, index.Quests));

            sb.AppendLine().AppendLine("## Parts");
            int currentStep = -2;
            foreach (var (loc, part) in QuestReferenceIndex.EnumerateParts(quest))
            {
                if (loc.Slot == QuestPartSlot.Step && loc.StepIndex != currentStep)
                {
                    currentStep = loc.StepIndex;
                    var step = quest.steps[currentStep];
                    sb.AppendLine($"### Step {currentStep + 1} \"{step.title}\" — done when ALL parts are true");
                    if (!string.IsNullOrEmpty(step.description)) sb.AppendLine($"> {OneLine(step.description)}");
                }
                AppendPart(sb, quest, loc, part, index);
            }

            var targeting = index.GetQuestFactsTargeting(quest);
            sb.AppendLine().AppendLine($"## QuestFacts targeting this quest ({targeting.Count})");
            if (targeting.Count == 0) sb.AppendLine("- none");
            foreach (var qf in targeting)
            {
                sb.AppendLine($"- `{qf.name}` → {QuestReferenceIndex.QuestFactStateLabel(qf)}");
                foreach (var r in index.GetReaders(qf).Where(r => r.Source != FactLinkSource.QuestFactTarget))
                    sb.AppendLine($"  - read by [{r.Source}] {r.Label}");
            }

            var memories = index.GetInvolvedMemories(quest);
            sb.AppendLine().AppendLine($"## NPC memories ({memories.Count}) — read or set this quest's facts");
            if (memories.Count == 0) sb.AppendLine("- none");
            foreach (var im in memories)
            {
                sb.AppendLine($"### {im.Memory.name} — {QuestReferenceIndex.NpcDisplayName(im.Npc)} · {string.Join(" · ", im.ReasonLabels)}");
                AppendMemoryBody(sb, im.Memory, index, quest);
            }
            return sb.ToString();
        }

        private static void AppendPart(StringBuilder sb, QuestSO quest, QuestPartLocation loc, QuestPart part, QuestReferenceIndex index)
        {
            string entry = string.IsNullOrWhiteSpace(part.entry) ? "(empty)" : $"\"{OneLine(part.entry)}\"";
            if (part.fact == null)
            {
                sb.AppendLine($"- **{loc}** — NO FACT — entry {entry}");
                return;
            }
            sb.AppendLine($"- **{loc}** `{part.fact.name}` ({FactKind(part.fact)}) — entry {entry}");
            AppendSetters(sb, part.fact, index, "  ");
            foreach (var r in index.GetReaders(part.fact).Where(r => !(r.Source == FactLinkSource.QuestPart && r.Owner == quest && r.Location.HasValue && r.Location.Value.Equals(loc))))
                sb.AppendLine($"  - read by [{r.Source}] {r.Label}");
        }

        // ── Fact / memory ─────────────────────────────────────────────────────

        public static string Fact(Fact fact, QuestReferenceIndex index)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"# Fact: {fact.name}");
            sb.AppendLine($"{FactKind(fact)} · key `{fact}`");
            if (fact is QuestFact qf)
                sb.AppendLine($"Targets {QuestReferenceIndex.QuestDisplayName(qf.Quest)} · {QuestReferenceIndex.QuestFactStateLabel(qf)}");
            sb.AppendLine().AppendLine("## Setters");
            AppendSetters(sb, fact, index, string.Empty);
            var readers = index.GetReaders(fact);
            sb.AppendLine().AppendLine($"## Readers ({readers.Count})");
            if (readers.Count == 0) sb.AppendLine("- none");
            foreach (var r in readers) sb.AppendLine($"- [{r.Source}] {r.Label}");
            return sb.ToString();
        }

        public static string Memory(NPCMemoryEntrySO memory, QuestReferenceIndex index)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"# Memory: {memory.name} — {QuestReferenceIndex.NpcDisplayName(index.GetMemoryOwner(memory))}");
            var quests = index.Quests.Where(q => index.GetInvolvedMemories(q).Any(im => im.Memory == memory)).Select(q => q.name).ToList();
            sb.AppendLine($"Involved in: {(quests.Count == 0 ? "no quest" : string.Join(", ", quests))}");
            sb.AppendLine().AppendLine("## Issues");
            AppendIssues(sb, QuestValidator.ValidateAllMemories(index).Where(i => i.Context == memory).ToList());
            sb.AppendLine();
            AppendMemoryBody(sb, memory, index, null);
            return sb.ToString();
        }

        private static void AppendMemoryBody(StringBuilder sb, NPCMemoryEntrySO memory, QuestReferenceIndex index, QuestSO quest)
        {
            AppendConditions(sb, "Unlock (ALL true)", memory.unlockConditions, quest);
            AppendConditions(sb, "Invalidation (ANY true closes it)", memory.invalidationConditions, quest);
            var start = memory.effects?.startdialog;
            sb.AppendLine(start == null
                ? "- Start dialogue: none"
                : $"- Start dialogue: `{start.name}`{(start.dialogueFact != null ? $" (sets `{start.dialogueFact.name}`)" : string.Empty)}");
            var gates = index.GetGates(memory);
            sb.AppendLine($"- Gates choices: {(gates.Count == 0 ? "none" : string.Empty)}");
            foreach (var gate in gates) sb.AppendLine($"  - {gate.Label}");
        }

        private static void AppendConditions(StringBuilder sb, string title, Fact[] facts, QuestSO quest)
        {
            if (facts == null || facts.Length == 0)
            {
                sb.AppendLine($"- {title}: none");
                return;
            }
            sb.AppendLine($"- {title}:");
            for (int i = 0; i < facts.Length; i++)
            {
                var fact = facts[i];
                if (fact == null)
                {
                    sb.AppendLine($"  - #{i + 1} MISSING (skipped at runtime)");
                    continue;
                }
                string where = string.Empty;
                if (quest != null)
                {
                    var loc = QuestReferenceIndex.EnumerateParts(quest).Where(p => p.part.fact == fact).Select(p => (QuestPartLocation?)p.loc).FirstOrDefault();
                    where = loc.HasValue ? $" — quest {loc}" : " — outside quest";
                }
                sb.AppendLine($"  - #{i + 1} `{fact.name}` ({FactKind(fact)}){where}");
            }
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        private static void AppendSetters(StringBuilder sb, Fact fact, QuestReferenceIndex index, string indent)
        {
            if (!QuestValidator.IsStored(fact))
            {
                sb.AppendLine($"{indent}- computed (no setter)");
                return;
            }
            var setters = index.GetSetters(fact);
            if (setters.Count == 0) sb.AppendLine($"{indent}- NEVER SET");
            foreach (var s in setters) sb.AppendLine($"{indent}- set by [{s.Source}] {s.Label}");
        }

        private static void AppendIssues(StringBuilder sb, List<ValidationIssue> issues)
        {
            if (issues.Count == 0)
            {
                sb.AppendLine("- none");
                return;
            }
            foreach (var issue in issues)
            {
                string where = issue.Location.HasValue ? $" @ {issue.Location}" : string.Empty;
                string context = issue.Context != null ? $" (`{issue.Context.name}`)" : string.Empty;
                sb.AppendLine($"- **{issue.Severity} {issue.Rule}**{where}: {issue.Message}{context}");
            }
        }

        private static string FactKind(Fact fact) =>
            $"{ExplorerStyles.FactTypeName(fact)}, {(QuestValidator.IsStored(fact) ? "stored" : "computed")}";

        private static int Count(List<ValidationIssue> issues, IssueSeverity severity) => issues.Count(i => i.Severity == severity);

        private static string YesNo(bool value) => value ? "yes" : "NO";

        private static string OneLine(string text) => text.Replace("\r", string.Empty).Replace('\n', ' ');

        private static bool Is(string a, string b) => !string.IsNullOrEmpty(b) && string.Equals(a, b, StringComparison.OrdinalIgnoreCase);
    }
}
