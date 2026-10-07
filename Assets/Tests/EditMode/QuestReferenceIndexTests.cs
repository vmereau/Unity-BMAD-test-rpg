using System.Collections.Generic;
using System.Linq;
using Game.Core;
using Game.Dialogue;
using Game.Editor.QuestExplorer;
using Game.NPC;
using Game.Progression;
using Game.Quest;
using Game.World;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Tests.EditMode
{
    /// <summary>
    /// Edit Mode tests for the Quest Explorer reference index. Built from hand-filled
    /// <see cref="IndexSources"/> — no AssetDatabase or scene access.
    /// </summary>
    public class QuestReferenceIndexTests
    {
        private readonly List<Object> _cleanup = new List<Object>();

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

        private NPCEntity MakeNpc(string npcName, out NPCMemoryEntrySO memory, StartDialogueNode root)
        {
            var npc = Make<NPCEntity>("NPC_" + npcName);
            npc.entityName = npcName;
            memory = Make<NPCMemoryEntrySO>("Mem_" + npcName);
            memory.effects.startdialog = root;
            npc.memories = new List<NPCMemoryEntrySO> { memory };
            return npc;
        }

        private static QuestPart Part(Fact fact, string entry = "entry") => new QuestPart { fact = fact, entry = entry };

        // ── AC 1 ──────────────────────────────────────────────────────────────

        [Test]
        public void KilledFactBinding_ProducesOneScenePersistentIdSetter()
        {
            var fact = Make<KilledFact>("KilledFact_Spider").Init("guid-1");
            var go = new GameObject("Spider");
            _cleanup.Add(go);
            var pid = go.AddComponent<PersistentID>();

            var index = QuestReferenceIndex.Build(new IndexSources
            {
                AllFacts = new Fact[] { fact },
                KilledBindings = { new KilledFactBinding { Fact = fact, GameObject = go, Component = pid, ScenePath = "Assets/Scenes/Town.unity" } }
            });

            var setters = index.GetSetters(fact);
            Assert.AreEqual(1, setters.Count);
            Assert.AreEqual(FactLinkSource.ScenePersistentID, setters[0].Source);
            Assert.AreSame(pid, setters[0].Owner);
            StringAssert.Contains("Spider [Town]", setters[0].Label);
        }

        [Test]
        public void ClosedSceneRef_ProducesClosedSceneSetterWithPath()
        {
            var fact = Make<KilledFact>().Init("guid-2");
            var index = QuestReferenceIndex.Build(new IndexSources
            {
                ClosedSceneRefs = { (fact, "Assets/Scenes/Town.unity") }
            });

            var setter = index.GetSetters(fact).Single();
            Assert.AreEqual(FactLinkSource.ClosedScene, setter.Source);
            Assert.AreEqual("Assets/Scenes/Town.unity", setter.ScenePath);
            Assert.IsNull(setter.Owner);
        }

        // ── AC 2 / AC 3 ───────────────────────────────────────────────────────

        [Test]
        public void ChoiceOptionFact_IsAttributedToNpcAndMemory()
        {
            var fact = Make<DialogueFact>().Init("Guard_Accepted");
            var start = Make<StartDialogueNode>();
            var text = Make<TextDialogueNode>();
            var choice = Make<ChoiceDialogueNode>("Choice_Offer");
            start.nextNode = text;
            text.nextNode = choice;
            choice.choices = new[] { new ChoiceOption { text = "I'll do it", dialogueFact = fact } };
            var npc = MakeNpc("Guard", out var memory, start);

            var index = QuestReferenceIndex.Build(new IndexSources
            {
                Npcs = new[] { npc },
                DialogueNodes = new DialogueNode[] { start, text, choice }
            });

            var setter = index.GetSetters(fact).Single();
            Assert.AreEqual(FactLinkSource.ChoiceOption, setter.Source);
            Assert.AreSame(npc, setter.Npc);
            Assert.AreSame(memory, setter.Memory);
            StringAssert.Contains("Guard", setter.Label);
            StringAssert.Contains("I'll do it", setter.Label);
        }

        [Test]
        public void DialogueCycle_TerminatesAndAttributesEachNodeOnce()
        {
            var fact = Make<DialogueFact>().Init("Loop");
            var start = Make<StartDialogueNode>();
            var text = Make<TextDialogueNode>();
            start.dialogueFact = fact;
            start.nextNode = text;
            text.nextNode = start; // cycle
            var npc = MakeNpc("Looper", out var memory, start);

            var index = QuestReferenceIndex.Build(new IndexSources
            {
                Npcs = new[] { npc },
                DialogueNodes = new DialogueNode[] { start, text }
            });

            Assert.AreEqual(1, index.GetSetters(fact).Count);
            Assert.AreSame(npc, index.GetDialogueOwner(text)?.npc);
            Assert.AreSame(memory, index.GetDialogueOwner(start)?.memory);
        }

        [Test]
        public void TeachChoice_ConfirmAndDenyNodes_AreTraversed()
        {
            var confirmFact = Make<DialogueFact>().Init("Confirm");
            var denyFact = Make<DialogueFact>().Init("Deny");
            var start = Make<StartDialogueNode>();
            var teach = Make<TeachChoiceDialogueNode>();
            var confirm = Make<StartDialogueNode>("Confirm");
            var deny = Make<StartDialogueNode>("Deny");
            confirm.dialogueFact = confirmFact;
            deny.dialogueFact = denyFact;
            start.nextNode = teach;
            teach.choices = new[] { new TeachChoiceOption { confirmNextNode = confirm, denyNextNode = deny } };
            var npc = MakeNpc("Trainer", out _, start);

            var index = QuestReferenceIndex.Build(new IndexSources { Npcs = new[] { npc } });

            Assert.AreSame(npc, index.GetSetters(confirmFact).Single().Npc);
            Assert.AreSame(npc, index.GetSetters(denyFact).Single().Npc);
        }

        [Test]
        public void UnreachableDialogueNode_SetterHasNoNpc()
        {
            var fact = Make<DialogueFact>().Init("Orphan");
            var start = Make<StartDialogueNode>();
            start.dialogueFact = fact;

            var index = QuestReferenceIndex.Build(new IndexSources { DialogueNodes = new DialogueNode[] { start } });

            var setter = index.GetSetters(fact).Single();
            Assert.AreEqual(FactLinkSource.StartDialogueNode, setter.Source);
            Assert.IsNull(setter.Npc);
        }

        // ── AC 4 ──────────────────────────────────────────────────────────────

        [Test]
        public void Readers_IncludeQuestPartMemoryUnlockAndReward()
        {
            var fact = Make<KilledFact>().Init("guid-3");
            var quest = Make<QuestSO>("Quest_Test");
            quest.title = "Test";
            quest.steps.Add(new QuestStep { title = "Kill", parts = new List<QuestPart> { Part(fact) } });

            var memory = Make<NPCMemoryEntrySO>();
            memory.unlockConditions = new Fact[] { fact };

            var reward = Make<PlayerRewardSO>();
            using (var so = new SerializedObject(reward))
            {
                so.FindProperty("_factType").enumValueIndex = (int)RewardFactType.Killed;
                so.FindProperty("_killedFact").objectReferenceValue = fact;
                so.ApplyModifiedPropertiesWithoutUndo();
            }

            var index = QuestReferenceIndex.Build(new IndexSources
            {
                Quests = new[] { quest },
                Memories = new[] { memory },
                Rewards = new[] { reward }
            });

            var readers = index.GetReaders(fact);
            Assert.AreEqual(3, readers.Count);
            var questReader = readers.Single(r => r.Source == FactLinkSource.QuestPart);
            StringAssert.Contains("Step 1 › Part 1", questReader.Label);
            Assert.IsTrue(readers.Any(r => r.Source == FactLinkSource.MemoryUnlock));
            Assert.IsTrue(readers.Any(r => r.Source == FactLinkSource.PlayerReward));
        }

        [Test]
        public void MemoryInvalidation_IsReaderWithOwningNpc()
        {
            var fact = Make<DialogueFact>().Init("X");
            var npc = MakeNpc("Elder", out var memory, null);
            memory.invalidationConditions = new Fact[] { fact };

            var index = QuestReferenceIndex.Build(new IndexSources { Npcs = new[] { npc } });

            var reader = index.GetReaders(fact).Single();
            Assert.AreEqual(FactLinkSource.MemoryInvalidation, reader.Source);
            Assert.AreSame(npc, reader.Npc);
        }

        [Test]
        public void Reward_OnlyFieldMatchingFactTypeIsReader()
        {
            var killed = Make<KilledFact>().Init("guid-4");
            var dialogue = Make<DialogueFact>().Init("Talk");
            var reward = Make<PlayerRewardSO>();
            using (var so = new SerializedObject(reward))
            {
                so.FindProperty("_factType").enumValueIndex = (int)RewardFactType.Dialogue;
                so.FindProperty("_killedFact").objectReferenceValue = killed;
                so.FindProperty("_dialogueFact").objectReferenceValue = dialogue;
                so.ApplyModifiedPropertiesWithoutUndo();
            }

            var index = QuestReferenceIndex.Build(new IndexSources { Rewards = new[] { reward } });

            Assert.AreEqual(0, index.GetReaders(killed).Count);
            Assert.AreEqual(1, index.GetReaders(dialogue).Count);
        }

        [Test]
        public void QuestFact_TargetsQuestAndIsListed()
        {
            var quest = Make<QuestSO>();
            var qf = Make<QuestFact>().Init(quest, QuestState.IsCompleted);

            var index = QuestReferenceIndex.Build(new IndexSources { Quests = new[] { quest }, QuestFacts = new[] { qf } });

            CollectionAssert.AreEqual(new[] { qf }, index.GetQuestFactsTargeting(quest).ToArray());
            var reader = index.GetReaders(qf).Single();
            Assert.AreEqual(FactLinkSource.QuestFactTarget, reader.Source);
            Assert.AreSame(quest, reader.Owner);
        }

        [Test]
        public void NullPartFacts_AreSkipped_AndNullQueriesReturnEmpty()
        {
            var quest = Make<QuestSO>();
            quest.completedParts.Add(Part(null));

            var index = QuestReferenceIndex.Build(new IndexSources { Quests = new[] { quest } });

            Assert.AreEqual(0, index.GetReaders(null).Count);
            Assert.AreEqual(0, index.GetSetters(null).Count);
            Assert.AreEqual(2, index.GetParts(quest).Count()); // start + completed #1
        }

        [Test]
        public void GetParts_ReturnsDisplayOrder()
        {
            var quest = Make<QuestSO>();
            quest.steps.Add(new QuestStep { parts = new List<QuestPart> { Part(null), Part(null) } });
            quest.completedParts.Add(Part(null));
            quest.failedParts.Add(Part(null));

            var locations = PartLocations(quest);

            CollectionAssert.AreEqual(
                new[] { "Start", "Step 1 › Part 1", "Step 1 › Part 2", "Completed #1", "Failed #1" },
                locations);
        }

        private static string[] PartLocations(QuestSO quest) =>
            QuestReferenceIndex.EnumerateParts(quest).Select(p => p.loc.ToString()).ToArray();

        [Test]
        public void Registration_LookupsReflectSources()
        {
            var registered = Make<QuestSO>();
            var missing = Make<QuestSO>();

            var index = QuestReferenceIndex.Build(new IndexSources
            {
                Quests = new[] { registered, missing },
                EventsManagerQuests = new HashSet<QuestSO> { registered },
                QuestLogQuests = new HashSet<QuestSO> { registered }
            });

            Assert.IsTrue(index.IsInEventsManager(registered));
            Assert.IsTrue(index.IsInQuestLog(registered));
            Assert.IsFalse(index.IsInEventsManager(missing));
            Assert.IsFalse(index.IsInQuestLog(missing));
        }

        [Test]
        public void SceneObjectsForNpc_ExcludePrefabBindings()
        {
            var npc = MakeNpc("Smith", out _, null);
            var sceneGo = new GameObject("Smith");
            var prefabGo = new GameObject("SmithPrefab");
            _cleanup.Add(sceneGo);
            _cleanup.Add(prefabGo);

            var index = QuestReferenceIndex.Build(new IndexSources
            {
                Npcs = new[] { npc },
                KilledBindings =
                {
                    new KilledFactBinding { Entity = npc, GameObject = sceneGo },
                    new KilledFactBinding { Entity = npc, GameObject = prefabGo, IsPrefab = true }
                }
            });

            CollectionAssert.AreEqual(new[] { sceneGo }, index.GetSceneObjectsForNpc(npc).ToArray());
        }

        // ── NPC memories: gates ───────────────────────────────────────────────

        [Test]
        public void ChoiceRequiredMemory_ProducesGateAttributedToNpcAndMemory()
        {
            var start = Make<StartDialogueNode>();
            var choice = Make<ChoiceDialogueNode>("Choice_Offer");
            start.nextNode = choice;
            var gated = Make<NPCMemoryEntrySO>("Mem_Gated");
            choice.choices = new[] { new ChoiceOption { text = "I already did it", requiredMemory = gated } };
            var npc = MakeNpc("Guard", out var owner, start);
            npc.memories.Add(gated);

            var index = QuestReferenceIndex.Build(new IndexSources { Npcs = new[] { npc } });

            var gate = index.GetGates(gated).Single();
            Assert.AreSame(npc, gate.Npc);
            Assert.AreSame(owner, gate.OwnerMemory);
            Assert.AreSame(choice, gate.Node);
            Assert.AreEqual(0, gate.ChoiceIndex);
            StringAssert.Contains("Guard › Choice_Offer › Choice 'I already did it'", gate.Label);
            Assert.AreEqual(0, index.GetGates(null).Count);
        }

        [Test]
        public void TeachChoiceRequiredMemory_ProducesGate()
        {
            var teach = Make<TeachChoiceDialogueNode>();
            var gated = Make<NPCMemoryEntrySO>();
            teach.choices = new[] { new TeachChoiceOption(), new TeachChoiceOption { requiredMemory = gated } };

            var index = QuestReferenceIndex.Build(new IndexSources { DialogueNodes = new DialogueNode[] { teach } });

            var gate = index.GetGates(gated).Single();
            Assert.AreEqual(1, gate.ChoiceIndex);
            Assert.IsNull(gate.Npc);
        }

        // ── NPC memories: quest involvement ───────────────────────────────────

        private QuestSO MakeQuest(Fact startFact, Fact stepFact)
        {
            var quest = Make<QuestSO>("Quest_Test");
            quest.startPart = Part(startFact);
            quest.steps.Add(new QuestStep { title = "Kill", parts = new List<QuestPart> { Part(stepFact) } });
            return quest;
        }

        [Test]
        public void MemoryUnlockedByStepFact_IsInvolvedAsReader()
        {
            var kill = Make<KilledFact>().Init("guid-m1");
            var quest = MakeQuest(null, kill);
            var memory = Make<NPCMemoryEntrySO>("Mem_Done");
            memory.unlockConditions = new Fact[] { kill };

            var index = QuestReferenceIndex.Build(new IndexSources { Quests = new[] { quest }, Memories = new[] { memory } });

            var im = index.GetInvolvedMemories(quest).Single();
            Assert.AreSame(memory, im.Memory);
            Assert.AreEqual(MemoryInvolvement.ReadsQuestFact, im.Reasons);
            CollectionAssert.Contains(im.ReasonLabels, "reads Step 1 › Part 1");
        }

        [Test]
        public void MemoryInvalidatedByTargetingQuestFact_IsInvolved()
        {
            var quest = MakeQuest(null, null);
            var completed = Make<QuestFact>().Init(quest, QuestState.IsCompleted);
            var memory = Make<NPCMemoryEntrySO>();
            memory.invalidationConditions = new Fact[] { completed };

            var index = QuestReferenceIndex.Build(new IndexSources
            {
                Quests = new[] { quest }, Memories = new[] { memory }, QuestFacts = new[] { completed }
            });

            var im = index.GetInvolvedMemories(quest).Single();
            Assert.AreEqual(MemoryInvolvement.ReadsQuestFact, im.Reasons);
            Assert.IsTrue(im.ReasonLabels.Any(l => l.StartsWith("invalidated by → ")), string.Join(", ", im.ReasonLabels));
        }

        [Test]
        public void MemoryWhoseDialogueSetsStartFact_IsInvolvedAsSetter()
        {
            var accept = Make<DialogueFact>().Init("Accept");
            var start = Make<StartDialogueNode>();
            var choice = Make<ChoiceDialogueNode>();
            start.nextNode = choice;
            choice.choices = new[] { new ChoiceOption { text = "Yes", dialogueFact = accept } };
            var npc = MakeNpc("Guard", out var memory, start);
            var quest = MakeQuest(accept, null);

            var index = QuestReferenceIndex.Build(new IndexSources { Quests = new[] { quest }, Npcs = new[] { npc } });

            var im = index.GetInvolvedMemories(quest).Single();
            Assert.AreSame(memory, im.Memory);
            Assert.AreSame(npc, im.Npc);
            Assert.AreEqual(MemoryInvolvement.SetsQuestFact, im.Reasons);
            CollectionAssert.Contains(im.ReasonLabels, "dialogue sets Start");
        }

        [Test]
        public void MemoryReadingUnrelatedFacts_IsNotInvolved()
        {
            var quest = MakeQuest(Make<DialogueFact>().Init("Start"), null);
            var memory = Make<NPCMemoryEntrySO>();
            memory.unlockConditions = new Fact[] { Make<DialogueFact>().Init("Other") };

            var index = QuestReferenceIndex.Build(new IndexSources { Quests = new[] { quest }, Memories = new[] { memory } });

            Assert.AreEqual(0, index.GetInvolvedMemories(quest).Count);
        }

        [Test]
        public void MemoryReadingAndSettingQuestFacts_IsListedOnceWithBothFlags()
        {
            var accept = Make<DialogueFact>().Init("Accept");
            var start = Make<StartDialogueNode>();
            start.dialogueFact = accept;
            var npc = MakeNpc("Guard", out var memory, start);
            memory.invalidationConditions = new Fact[] { accept };
            var quest = MakeQuest(accept, null);

            var index = QuestReferenceIndex.Build(new IndexSources { Quests = new[] { quest }, Npcs = new[] { npc } });

            var im = index.GetInvolvedMemories(quest).Single();
            Assert.AreEqual(MemoryInvolvement.ReadsQuestFact | MemoryInvolvement.SetsQuestFact, im.Reasons);
            CollectionAssert.Contains(im.ReasonLabels, "invalidated by Start");
            CollectionAssert.Contains(im.ReasonLabels, "dialogue sets Start");
        }

        [Test]
        public void IsOwnedByNpc_OnlyForMemoriesListedOnAnNpc()
        {
            var npc = MakeNpc("Guard", out var owned, null);
            var loose = Make<NPCMemoryEntrySO>();

            var index = QuestReferenceIndex.Build(new IndexSources { Npcs = new[] { npc }, Memories = new[] { owned, loose } });

            Assert.IsTrue(index.IsOwnedByNpc(owned));
            Assert.IsFalse(index.IsOwnedByNpc(loose));
        }

        // ── NPC memories: live state ──────────────────────────────────────────

        [Test]
        public void EvaluateMemory_MirrorsRuntimeSemantics()
        {
            var on = Make<DialogueFact>().Init("On");
            var off = Make<DialogueFact>().Init("Off");
            var played = Make<DialogueFact>().Init("Played");
            var values = new Dictionary<Fact, bool> { { on, true }, { off, false } };
            bool Get(Fact f) => values.TryGetValue(f, out bool v) && v;
            bool NotPlayed(DialogueFact _) => false;

            var memory = Make<NPCMemoryEntrySO>();
            Assert.AreEqual(MemoryLiveState.Active, QuestReferenceIndex.EvaluateMemory(memory, Get, NotPlayed), "empty conditions");

            memory.unlockConditions = new Fact[] { on, off };
            Assert.AreEqual(MemoryLiveState.Locked, QuestReferenceIndex.EvaluateMemory(memory, Get, NotPlayed), "unlock false");

            memory.unlockConditions = new Fact[] { on };
            memory.invalidationConditions = new Fact[] { off, on };
            Assert.AreEqual(MemoryLiveState.Invalidated, QuestReferenceIndex.EvaluateMemory(memory, Get, NotPlayed), "invalidated");

            memory.unlockConditions = new Fact[] { null, on };
            memory.invalidationConditions = null;
            Assert.AreEqual(MemoryLiveState.Active, QuestReferenceIndex.EvaluateMemory(memory, Get, NotPlayed), "null skipped");

            var startNode = Make<StartDialogueNode>();
            startNode.dialogueFact = played;
            memory.effects.startdialog = startNode;
            Assert.AreEqual(MemoryLiveState.ActiveDialoguePlayed,
                QuestReferenceIndex.EvaluateMemory(memory, Get, d => d == played), "dialogue played");
            Assert.AreEqual(MemoryLiveState.Active,
                QuestReferenceIndex.EvaluateMemory(memory, Get, null), "null isDialoguePlayed");

            memory.unlockConditions = new Fact[] { off };
            Assert.AreEqual(MemoryLiveState.Locked,
                QuestReferenceIndex.EvaluateMemory(memory, Get, d => d == played), "locked, dialogue played");

            memory.invalidationConditions = new Fact[] { on };
            Assert.AreEqual(MemoryLiveState.Invalidated,
                QuestReferenceIndex.EvaluateMemory(memory, Get, NotPlayed), "invalidated while locked");

            memory.unlockConditions = null;
            memory.invalidationConditions = null;
            startNode.dialogueFact = null;
            Assert.AreEqual(MemoryLiveState.Active,
                QuestReferenceIndex.EvaluateMemory(memory, Get, _ => true), "start dialogue without fact");
        }

        [Test]
        public void MemoryReadingPartFactAndTargetingQuestFact_IsListedOnceWithBothLabels()
        {
            var kill = Make<KilledFact>().Init("guid-m2");
            var quest = MakeQuest(null, kill);
            var completed = Make<QuestFact>().Init(quest, QuestState.IsCompleted);
            var memory = Make<NPCMemoryEntrySO>();
            memory.unlockConditions = new Fact[] { kill };
            memory.invalidationConditions = new Fact[] { completed };

            var index = QuestReferenceIndex.Build(new IndexSources
            {
                Quests = new[] { quest }, Memories = new[] { memory }, QuestFacts = new[] { completed }
            });

            var im = index.GetInvolvedMemories(quest).Single();
            CollectionAssert.AreEqual(new[] { "reads Step 1 › Part 1", "invalidated by → IsCompleted" }, im.ReasonLabels);
        }
    }
}
