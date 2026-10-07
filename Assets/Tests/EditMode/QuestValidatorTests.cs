using System.Collections.Generic;
using System.Linq;
using Game.Core;
using Game.Dialogue;
using Game.Editor.QuestExplorer;
using Game.NPC;
using Game.Quest;
using NUnit.Framework;
using UnityEngine;

namespace Tests.EditMode
{
    /// <summary>
    /// Edit Mode tests for <see cref="QuestValidator"/> (one positive test per rule V1–V17 plus a clean
    /// quest) and <see cref="QuestStepRemap"/>. Every test starts from a clean quest and breaks one thing.
    /// </summary>
    public class QuestValidatorTests
    {
        private readonly List<Object> _cleanup = new List<Object>();
        private QuestSO _quest;
        private IndexSources _sources;
        private KilledFact _killFact;
        private GameObject _spider;
        private NPCEntity _npc;
        private NPCMemoryEntrySO _offer;
        private StartDialogueNode _acceptNode;

        [SetUp]
        public void SetUp()
        {
            var startFact = Make<DialogueFact>("DialogueFact_Accept").Init("Accept");
            var doneFact = Make<DialogueFact>("DialogueFact_TurnIn").Init("TurnIn");
            _killFact = Make<KilledFact>("KilledFact_Spider").Init("guid-spider");

            var acceptNode = _acceptNode = Make<StartDialogueNode>();
            acceptNode.dialogueFact = startFact;
            var turnInNode = Make<StartDialogueNode>();
            turnInNode.dialogueFact = doneFact;

            var npc = _npc = Make<NPCEntity>("NPC_Guard");
            npc.entityName = "Guard";
            var offer = _offer = Make<NPCMemoryEntrySO>("Mem_Offer");
            offer.effects.startdialog = acceptNode;
            var reward = Make<NPCMemoryEntrySO>("Mem_Reward");
            reward.effects.startdialog = turnInNode;
            npc.memories = new List<NPCMemoryEntrySO> { offer, reward };

            _spider = new GameObject("Spider");
            _cleanup.Add(_spider);

            _quest = Make<QuestSO>("Quest_Clean");
            _quest.questId = "Clean";
            _quest.title = "Clean quest";
            _quest.description = "Help the guard with the spider.";
            _quest.startPart = Part(startFact);
            _quest.steps.Add(new QuestStep { title = "Kill", parts = new List<QuestPart> { Part(_killFact) } });
            _quest.completedParts.Add(Part(doneFact));

            _sources = new IndexSources
            {
                Quests = new[] { _quest },
                Npcs = new[] { npc },
                AllFacts = new Fact[] { startFact, doneFact, _killFact },
                KilledBindings = { new KilledFactBinding { Fact = _killFact, GameObject = _spider, ScenePath = "Assets/Town.unity" } },
                EventsManagerQuests = new HashSet<QuestSO> { _quest },
                QuestLogQuests = new HashSet<QuestSO> { _quest }
            };
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var obj in _cleanup)
                if (obj != null) Object.DestroyImmediate(obj);
            _cleanup.Clear();
        }

        private T Make<T>(string name = null) where T : ScriptableObject
        {
            var so = ScriptableObject.CreateInstance<T>();
            so.name = name ?? typeof(T).Name;
            _cleanup.Add(so);
            return so;
        }

        private static QuestPart Part(Fact fact, string entry = "entry") => new QuestPart { fact = fact, entry = entry };

        private List<ValidationIssue> Validate() =>
            QuestValidator.Validate(_quest, QuestReferenceIndex.Build(_sources), _sources.Quests);

        private static ValidationIssue Single(List<ValidationIssue> issues, string rule, IssueSeverity severity)
        {
            var matches = issues.Where(i => i.Rule == rule && i.Severity == severity).ToList();
            Assert.AreEqual(1, matches.Count, $"Expected one {severity} {rule}; got: {string.Join(" | ", issues)}");
            return matches[0];
        }

        [Test]
        public void CleanQuest_HasNoIssues()
        {
            var issues = Validate();
            Assert.IsEmpty(issues, string.Join(" | ", issues));
        }

        [Test]
        public void V1_StartWithoutFact_IsError()
        {
            _quest.startPart = Part(null);
            var issue = Single(Validate(), "V1", IssueSeverity.Error);
            Assert.AreEqual(QuestPartLocation.Start, issue.Location);
        }

        [Test]
        public void V2_StepPartWithoutFact_IsError()
        {
            _quest.steps[0].parts.Add(Part(null));
            var issue = Single(Validate(), "V2", IssueSeverity.Error);
            Assert.AreEqual(QuestPartLocation.StepPart(0, 1), issue.Location);
        }

        [Test]
        public void V3_KilledFactWithoutSetter_IsError()
        {
            _sources.KilledBindings.Clear();
            var issue = Single(Validate(), "V3", IssueSeverity.Error);
            StringAssert.Contains("'KilledFact_Spider' is never set — Step 1 › Part 1", issue.Message);
        }

        [Test]
        public void V3_WorldFact_MentionsMissingSetterMechanism()
        {
            var world = Make<WorldFact>("WorldFact_Camp").Init("camp");
            _quest.steps[0].parts.Add(Part(world));
            var issue = Single(Validate(), "V3", IssueSeverity.Error);
            StringAssert.Contains("no WorldFact setter exists", issue.Message);
        }

        [Test]
        public void V4_DialogueFactOnlySetByUnreachableNode_IsWarning()
        {
            var fact = Make<DialogueFact>().Init("Lost");
            var node = Make<StartDialogueNode>();
            node.dialogueFact = fact;
            _sources.DialogueNodes = new DialogueNode[] { node };
            _quest.completedParts.Add(Part(fact));
            Single(Validate(), "V4", IssueSeverity.Warning);
        }

        [Test]
        public void V5_KilledFactSharedByTwoSceneEntities_IsWarning()
        {
            var other = new GameObject("Spider2");
            _cleanup.Add(other);
            _sources.KilledBindings.Add(new KilledFactBinding { Fact = _killFact, GameObject = other, ScenePath = "Assets/Town.unity" });
            Single(Validate(), "V5", IssueSeverity.Warning);
        }

        [Test]
        public void V6_StepWithoutParts_IsWarning()
        {
            _quest.steps.Add(new QuestStep { title = "Empty", parts = new List<QuestPart>() });
            var issue = Single(Validate(), "V6", IssueSeverity.Warning);
            Assert.AreEqual(QuestPartLocation.Step(1), issue.Location);
        }

        [Test]
        public void V7_NoCompletedParts_IsInfo()
        {
            _quest.completedParts.Clear();
            Single(Validate(), "V7", IssueSeverity.Info);
        }

        [Test]
        public void V8_EmptyStartEntry_IsWarning_EmptyStepEntry_IsInfo()
        {
            _quest.startPart = Part(_quest.startPart.fact, "");
            _quest.steps[0].parts[0] = Part(_killFact, "");
            var issues = Validate();
            Single(issues, "V8", IssueSeverity.Warning);
            Single(issues, "V8", IssueSeverity.Info);
        }

        [Test]
        public void V9_NotRegistered_WarnsForEventsManagerAndQuestLog()
        {
            _sources.EventsManagerQuests.Clear();
            _sources.QuestLogQuests.Clear();
            var issues = Validate().Where(i => i.Rule == "V9").ToList();
            Assert.AreEqual(2, issues.Count);
            Assert.IsTrue(issues.All(i => i.Severity == IssueSeverity.Warning));
        }

        [Test]
        public void V10_EmptyQuestId_IsError()
        {
            _quest.questId = "";
            Single(Validate(), "V10", IssueSeverity.Error);
        }

        [Test]
        public void V10_DuplicateQuestId_IsError()
        {
            var other = Make<QuestSO>("Quest_Other");
            other.questId = "Clean";
            _sources.Quests = new[] { _quest, other };
            _sources.EventsManagerQuests.Add(other);
            _sources.QuestLogQuests.Add(other);
            StringAssert.Contains("Quest_Other", Single(Validate(), "V10", IssueSeverity.Error).Message);
        }

        [Test]
        public void V10_AssetNameMismatch_IsWarning()
        {
            _quest.name = "Quest_Renamed";
            Single(Validate(), "V10", IssueSeverity.Warning);
        }

        [Test]
        public void V11_QuestFactTargetingMissingStep_IsError()
        {
            var qf = Make<QuestFact>().InitStep(_quest, 4);
            _sources.QuestFacts = new[] { qf };
            Assert.AreSame(qf, Single(Validate(), "V11", IssueSeverity.Error).Context);
        }

        [Test]
        public void V12_PartDependingOnOwnLaterState_IsWarning()
        {
            var qf = Make<QuestFact>().InitStep(_quest, 0);
            _quest.steps[0].parts.Add(Part(qf)); // step 1 requires "step 1 completed"
            var issue = Single(Validate(), "V12", IssueSeverity.Warning);
            Assert.AreEqual(QuestPartLocation.StepPart(0, 1), issue.Location);
        }

        [Test]
        public void V12_PartDependingOnEarlierStep_IsFine()
        {
            var qf = Make<QuestFact>().InitStep(_quest, 0);
            _quest.steps.Add(new QuestStep { title = "Return", parts = new List<QuestPart> { Part(qf) } });
            Assert.IsFalse(Validate().Any(i => i.Rule == "V12"));
        }

        [Test]
        public void V13_DescriptionCountMismatch_IsInfo()
        {
            _quest.description = "Kill all FIVE spiders.";
            var issue = Single(Validate(), "V13", IssueSeverity.Info);
            StringAssert.Contains("Description says 5 but step part counts are 1", issue.Message);
        }

        [Test]
        public void V13_DescriptionCountMatchingAStep_IsFine()
        {
            _quest.description = "Kill 1 spider, then the other 2.";
            _quest.steps[0].parts.Add(Part(_killFact));
            Assert.IsFalse(Validate().Any(i => i.Rule == "V13"));
        }

        [Test]
        public void Issues_AreSortedErrorWarningInfo()
        {
            _quest.completedParts.Clear();          // V7 info
            _sources.QuestLogQuests.Clear();        // V9 warning
            _quest.startPart = Part(null);          // V1 error
            var severities = Validate().Select(i => (int)i.Severity).ToList();
            CollectionAssert.IsOrdered(severities);
            Assert.AreEqual(IssueSeverity.Error, (IssueSeverity)severities[0]);
        }

        // ── NPC memories (V14–V17) ────────────────────────────────────────────

        /// <summary>A memory reading the quest's step fact, listed on the Guard, with a start dialogue.</summary>
        private NPCMemoryEntrySO AddInvolvedMemory(string name, bool onNpc = true, bool withDialogue = true)
        {
            var memory = Make<NPCMemoryEntrySO>(name);
            memory.unlockConditions = new Fact[] { _killFact };
            if (withDialogue) memory.effects.startdialog = Make<StartDialogueNode>();
            if (onNpc) _npc.memories.Add(memory);
            else _sources.Memories = _sources.Memories.Append(memory).ToArray();
            return memory;
        }

        private ChoiceDialogueNode AddGuardChoiceRequiring(NPCMemoryEntrySO memory)
        {
            var choice = Make<ChoiceDialogueNode>("Choice_Guard");
            choice.choices = new[] { new ChoiceOption { text = "I already did it", requiredMemory = memory } };
            _acceptNode.nextNode = choice;
            return choice;
        }

        [Test]
        public void V14_NullUnlockCondition_IsError()
        {
            _offer.unlockConditions = new Fact[] { _killFact, null };
            var issue = Single(Validate(), "V14", IssueSeverity.Error);
            StringAssert.Contains("unlock condition #2", issue.Message);
            Assert.AreSame(_offer, issue.Context);
        }

        [Test]
        public void V14_NullInvalidationCondition_IsError()
        {
            _offer.invalidationConditions = new Fact[] { null };
            StringAssert.Contains("invalidation condition #1", Single(Validate(), "V14", IssueSeverity.Error).Message);
        }

        [Test]
        public void V14_MemoryNotInvolvedInQuest_IsNotReported()
        {
            var other = Make<NPCMemoryEntrySO>("Mem_Unrelated");
            other.unlockConditions = new Fact[] { Make<DialogueFact>().Init("Unrelated"), null };
            _npc.memories.Add(other);
            Assert.IsFalse(Validate().Any(i => i.Rule == "V14"));
        }

        [Test]
        public void V15_MemoryWithoutDialogueOrGate_IsWarning()
        {
            var memory = AddInvolvedMemory("Mem_Silent", withDialogue: false);
            Assert.AreSame(memory, Single(Validate(), "V15", IssueSeverity.Warning).Context);
        }

        [Test]
        public void V15_MemoryGatingAChoice_IsFine()
        {
            var memory = AddInvolvedMemory("Mem_Gate", withDialogue: false);
            AddGuardChoiceRequiring(memory);
            Assert.IsFalse(Validate().Any(i => i.Rule == "V15"));
        }

        [Test]
        public void V16_MemoryOnNoNpc_IsWarning()
        {
            var memory = AddInvolvedMemory("Mem_Loose", onNpc: false);
            Assert.AreSame(memory, Single(Validate(), "V16", IssueSeverity.Warning).Context);
        }

        [Test]
        public void V17_RequiredMemoryOwnedByAnotherNpc_IsError()
        {
            var memory = Make<NPCMemoryEntrySO>("Mem_Other");
            memory.unlockConditions = new Fact[] { _killFact };
            var other = Make<NPCEntity>("NPC_Other");
            other.entityName = "Other";
            other.memories = new List<NPCMemoryEntrySO> { memory };
            _sources.Npcs = new[] { _npc, other };
            AddGuardChoiceRequiring(memory);

            var issue = Single(Validate(), "V17", IssueSeverity.Error);
            StringAssert.Contains("Choice_Guard", issue.Message);
            StringAssert.Contains("Guard doesn't have", issue.Message);
        }

        [Test]
        public void V17_RequiredMemoryOwnedBySameNpc_IsFine()
        {
            var memory = AddInvolvedMemory("Mem_Own", withDialogue: false);
            AddGuardChoiceRequiring(memory);
            var issues = Validate();
            Assert.IsFalse(issues.Any(i => i.Rule == "V17"), string.Join(" | ", issues));
            Assert.IsEmpty(issues, string.Join(" | ", issues));
        }

        [Test]
        public void MemoryIssues_ErrorsBeforeWarnings()
        {
            AddInvolvedMemory("Mem_Loose", onNpc: false, withDialogue: false); // V15 + V16 warnings
            _offer.unlockConditions = new Fact[] { null };                      // V14 error
            var issues = Validate().Where(i => i.Rule == "V14" || i.Rule == "V15" || i.Rule == "V16").ToList();
            CollectionAssert.AreEqual(new[] { "V14", "V15", "V16" }, issues.Select(i => i.Rule).ToArray());
        }

        [Test]
        public void ValidateAllMemories_ReportsMemoryNotInvolvedInAnyQuest()
        {
            var orphan = Make<NPCMemoryEntrySO>("Mem_Broken");
            orphan.unlockConditions = new Fact[] { null }; // its only quest link was lost
            orphan.effects.startdialog = Make<StartDialogueNode>();
            _npc.memories.Add(orphan);

            var index = QuestReferenceIndex.Build(_sources);
            Assert.IsFalse(QuestValidator.Validate(_quest, index, _sources.Quests).Any(i => i.Rule == "V14"));
            var issue = QuestValidator.ValidateAllMemories(index).Single();
            Assert.AreEqual("V14", issue.Rule);
            Assert.AreSame(orphan, issue.Context);
        }

        // ── QuestReport ───────────────────────────────────────────────────────

        [Test]
        public void Report_ResolvesQuestFactMemoryListAndAudit()
        {
            var orphan = Make<NPCMemoryEntrySO>("Mem_Broken");
            orphan.unlockConditions = new Fact[] { null };
            _npc.memories.Add(orphan);
            var index = QuestReferenceIndex.Build(_sources);

            string quest = QuestReport.Run("clean", index); // questId, case-insensitive
            StringAssert.Contains("# Quest: Clean quest (Quest_Clean)", quest);
            StringAssert.Contains("### Step 1 \"Kill\"", quest);
            StringAssert.Contains("set by [ScenePersistentID] Spider [Town]", quest);
            StringAssert.Contains("### Mem_Offer — Guard · dialogue sets Start", quest);

            StringAssert.Contains("# Fact: KilledFact_Spider", QuestReport.Run("KilledFact_Spider", index));
            StringAssert.Contains("#1 MISSING", QuestReport.Run("mem_broken", index));
            StringAssert.Contains("| Clean | Quest_Clean |", QuestReport.Run("list", index));

            string audit = QuestReport.Run("audit", index);
            StringAssert.Contains("**Error V14**", audit);
            StringAssert.Contains("No quest, fact or memory named 'nope'", QuestReport.Run("nope", index));
        }

        // ── QuestStepRemap ────────────────────────────────────────────────────

        [Test]
        public void Remap_RemoveMiddle_ShiftsLaterSteps()
        {
            var map = QuestStepRemap.Compute(3, new StepOp(StepOpKind.Remove, 1));
            Assert.AreEqual(0, map[0]);
            Assert.IsNull(map[1]);
            Assert.AreEqual(1, map[2]);
        }

        [Test]
        public void Remap_RemoveFirst_ShiftsAll()
        {
            var map = QuestStepRemap.Compute(2, new StepOp(StepOpKind.Remove, 0));
            Assert.IsNull(map[0]);
            Assert.AreEqual(0, map[1]);
        }

        [Test]
        public void Remap_RemoveLast_KeepsOthers()
        {
            var map = QuestStepRemap.Compute(3, new StepOp(StepOpKind.Remove, 2));
            Assert.AreEqual(0, map[0]);
            Assert.AreEqual(1, map[1]);
            Assert.IsNull(map[2]);
        }

        [Test]
        public void Remap_MoveUpFirst_IsIdentity()
        {
            var map = QuestStepRemap.Compute(3, new StepOp(StepOpKind.MoveUp, 0));
            for (int i = 0; i < 3; i++) Assert.AreEqual(i, map[i]);
        }

        [Test]
        public void Remap_MoveDownLast_IsIdentity()
        {
            var map = QuestStepRemap.Compute(3, new StepOp(StepOpKind.MoveDown, 2));
            for (int i = 0; i < 3; i++) Assert.AreEqual(i, map[i]);
        }

        [Test]
        public void Remap_MoveDownMiddle_SwapsWithNext()
        {
            var map = QuestStepRemap.Compute(3, new StepOp(StepOpKind.MoveDown, 1));
            Assert.AreEqual(0, map[0]);
            Assert.AreEqual(2, map[1]);
            Assert.AreEqual(1, map[2]);
        }

        [Test]
        public void Remap_OutOfRange_IsIdentity()
        {
            var map = QuestStepRemap.Compute(2, new StepOp(StepOpKind.Remove, 5));
            Assert.AreEqual(0, map[0]);
            Assert.AreEqual(1, map[1]);
        }
    }
}
