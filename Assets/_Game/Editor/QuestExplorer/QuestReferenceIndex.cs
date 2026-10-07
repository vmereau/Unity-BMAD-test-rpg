using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using _Game.ScriptableObjects.Entities;
using Game.Core;
using Game.Dialogue;
using Game.NPC;
using Game.Progression;
using Game.Quest;
using UnityEngine;

namespace Game.Editor.QuestExplorer
{
    /// <summary>
    /// UI-agnostic reverse index: every <see cref="Fact"/> → its setters and readers, dialogue node →
    /// owning NPC + memory, NPC → scene objects, quest → registration and targeting QuestFacts,
    /// memory → gated choices, quest → involved memories.
    /// Pure C# over <see cref="IndexSources"/> — no AssetDatabase, no SerializedObject — so tests and a
    /// future graph view can reuse it.
    /// </summary>
    public sealed class QuestReferenceIndex
    {
        private static readonly IReadOnlyList<FactLink> NoLinks = new FactLink[0];
        private static readonly IReadOnlyList<QuestFact> NoQuestFacts = new QuestFact[0];
        private static readonly IReadOnlyList<GameObject> NoObjects = new GameObject[0];
        private static readonly IReadOnlyList<MemoryGateLink> NoGates = new MemoryGateLink[0];
        private static readonly IReadOnlyList<InvolvedMemory> NoMemories = new InvolvedMemory[0];

        private readonly Dictionary<Fact, List<FactLink>> _setters = new Dictionary<Fact, List<FactLink>>();
        private readonly Dictionary<Fact, List<FactLink>> _readers = new Dictionary<Fact, List<FactLink>>();
        private readonly Dictionary<DialogueNode, (NPCEntity npc, NPCMemoryEntrySO memory)> _dialogueOwners =
            new Dictionary<DialogueNode, (NPCEntity npc, NPCMemoryEntrySO memory)>();
        private readonly Dictionary<NPCMemoryEntrySO, NPCEntity> _memoryOwners = new Dictionary<NPCMemoryEntrySO, NPCEntity>();
        private readonly Dictionary<Entity, List<GameObject>> _sceneObjectsByEntity = new Dictionary<Entity, List<GameObject>>();
        private readonly Dictionary<QuestSO, List<QuestFact>> _questFactsByQuest = new Dictionary<QuestSO, List<QuestFact>>();
        private readonly Dictionary<NPCMemoryEntrySO, List<MemoryGateLink>> _gatesByMemory = new Dictionary<NPCMemoryEntrySO, List<MemoryGateLink>>();
        private readonly HashSet<NPCMemoryEntrySO> _ownedMemories = new HashSet<NPCMemoryEntrySO>();
        private readonly Dictionary<QuestSO, List<InvolvedMemory>> _involvedCache = new Dictionary<QuestSO, List<InvolvedMemory>>();
        private readonly HashSet<QuestSO> _eventsManagerQuests;
        private readonly HashSet<QuestSO> _questLogQuests;
        private readonly List<QuestSO> _quests;
        private readonly List<Fact> _allFacts;
        private readonly List<NPCMemoryEntrySO> _allMemories = new List<NPCMemoryEntrySO>();

        public IReadOnlyList<QuestSO> Quests => _quests;
        public IReadOnlyList<Fact> AllFacts => _allFacts;
        /// <summary>Every memory asset plus every memory listed on an NPC, ordered by name.</summary>
        public IReadOnlyList<NPCMemoryEntrySO> AllMemories => _allMemories;

        private QuestReferenceIndex(IndexSources s)
        {
            _quests = NonNull(s.Quests);
            _allFacts = NonNull(s.AllFacts);
            _eventsManagerQuests = s.EventsManagerQuests ?? new HashSet<QuestSO>();
            _questLogQuests = s.QuestLogQuests ?? new HashSet<QuestSO>();
        }

        public static QuestReferenceIndex Build(IndexSources s)
        {
            var index = new QuestReferenceIndex(s);
            index.MapDialogueOwnership(s.Npcs);
            index.MapDialogueSetters(s.DialogueNodes);
            index.MapMemoryGates(s.DialogueNodes);
            index.MapKilledSetters(s.KilledBindings, s.ClosedSceneRefs);
            index.MapQuestReaders();
            index.MapMemoryReaders(s.Memories);
            index.MapRewardReaders(s.Rewards);
            index.MapQuestFacts(s.QuestFacts);
            return index;
        }

        // ── Queries ───────────────────────────────────────────────────────────

        public IReadOnlyList<FactLink> GetSetters(Fact fact) =>
            fact != null && _setters.TryGetValue(fact, out var list) ? list : NoLinks;

        public IReadOnlyList<FactLink> GetReaders(Fact fact) =>
            fact != null && _readers.TryGetValue(fact, out var list) ? list : NoLinks;

        public IEnumerable<(QuestPartLocation loc, QuestPart part)> GetParts(QuestSO quest) => EnumerateParts(quest);

        public bool IsInEventsManager(QuestSO quest) => quest != null && _eventsManagerQuests.Contains(quest);

        public bool IsInQuestLog(QuestSO quest) => quest != null && _questLogQuests.Contains(quest);

        public IReadOnlyList<QuestFact> GetQuestFactsTargeting(QuestSO quest) =>
            quest != null && _questFactsByQuest.TryGetValue(quest, out var list) ? list : NoQuestFacts;

        public (NPCEntity npc, NPCMemoryEntrySO memory)? GetDialogueOwner(DialogueNode node) =>
            node != null && _dialogueOwners.TryGetValue(node, out var owner) ? owner : ((NPCEntity, NPCMemoryEntrySO)?)null;

        public NPCEntity GetMemoryOwner(NPCMemoryEntrySO memory) =>
            memory != null && _memoryOwners.TryGetValue(memory, out var npc) ? npc : null;

        /// <summary>Live scene GameObjects whose PersistentID entity is this NPC (prefab bindings excluded).</summary>
        public IReadOnlyList<GameObject> GetSceneObjectsForNpc(NPCEntity npc) =>
            npc != null && _sceneObjectsByEntity.TryGetValue(npc, out var list) ? list : NoObjects;

        /// <summary>Choice options whose requiredMemory is this memory.</summary>
        public IReadOnlyList<MemoryGateLink> GetGates(NPCMemoryEntrySO memory) =>
            memory != null && _gatesByMemory.TryGetValue(memory, out var list) ? list : NoGates;

        /// <summary>True when the memory is listed in any <c>NPCEntity.memories</c>.</summary>
        public bool IsOwnedByNpc(NPCMemoryEntrySO memory) => memory != null && _ownedMemories.Contains(memory);

        /// <summary>
        /// Memories whose conditions read one of the quest's part facts (or a QuestFact targeting the quest),
        /// or whose start dialogue chain sets one of the quest's part facts. Not transitive. Ordered by NPC
        /// display name (no NPC last), then memory name.
        /// </summary>
        public IReadOnlyList<InvolvedMemory> GetInvolvedMemories(QuestSO quest)
        {
            if (quest == null) return NoMemories;
            if (_involvedCache.TryGetValue(quest, out var cached)) return cached;

            var byMemory = new Dictionary<NPCMemoryEntrySO, InvolvedMemory>();
            void Involve(NPCMemoryEntrySO memory, MemoryInvolvement reason, string label)
            {
                if (memory == null) return;
                if (!byMemory.TryGetValue(memory, out var im))
                    byMemory[memory] = im = new InvolvedMemory { Memory = memory, Npc = GetMemoryOwner(memory) };
                im.Reasons |= reason;
                if (!im.ReasonLabels.Contains(label)) im.ReasonLabels.Add(label);
            }

            void InvolveReaders(Fact fact, string target)
            {
                foreach (var link in GetReaders(fact))
                {
                    if (link.Source == FactLinkSource.MemoryUnlock)
                        Involve(link.Memory, MemoryInvolvement.ReadsQuestFact, $"reads {target}");
                    else if (link.Source == FactLinkSource.MemoryInvalidation)
                        Involve(link.Memory, MemoryInvolvement.ReadsQuestFact, $"invalidated by {target}");
                }
            }

            foreach (var (loc, part) in EnumerateParts(quest))
            {
                if (part.fact == null) continue;
                // (a) conditions reading the part fact
                InvolveReaders(part.fact, loc.ToString());
                // (b) start dialogue chains setting the part fact
                foreach (var link in GetSetters(part.fact))
                {
                    if (link.Memory == null) continue;
                    if (link.Source != FactLinkSource.StartDialogueNode && link.Source != FactLinkSource.ChoiceOption) continue;
                    Involve(link.Memory, MemoryInvolvement.SetsQuestFact, $"dialogue sets {loc}");
                }
            }
            // (a) conditions reading a QuestFact that targets the quest
            foreach (var qf in GetQuestFactsTargeting(quest))
                InvolveReaders(qf, $"→ {QuestFactStateLabel(qf)}");

            var result = byMemory.Values
                .OrderBy(im => im.Npc == null ? 1 : 0)
                .ThenBy(im => NpcDisplayName(im.Npc), StringComparer.Ordinal)
                .ThenBy(im => im.Memory.name, StringComparer.Ordinal)
                .ToList();
            _involvedCache[quest] = result;
            return result;
        }

        /// <summary>
        /// Mirrors NPCMemoryEntrySO.IsActive and NPCMemoryComponent's dialogue-played skip.
        /// Null conditions are skipped, like TopicUnlockEvaluator.
        /// </summary>
        public static MemoryLiveState EvaluateMemory(NPCMemoryEntrySO memory, Func<Fact, bool> getFact,
            Func<DialogueFact, bool> isDialoguePlayed)
        {
            if (memory == null || getFact == null) return MemoryLiveState.Locked;

            if (memory.invalidationConditions != null)
                foreach (var fact in memory.invalidationConditions)
                    if (fact != null && getFact(fact)) return MemoryLiveState.Invalidated;

            if (memory.unlockConditions != null)
                foreach (var fact in memory.unlockConditions)
                    if (fact != null && !getFact(fact)) return MemoryLiveState.Locked;

            var startFact = memory.effects?.startdialog?.dialogueFact;
            return startFact != null && isDialoguePlayed != null && isDialoguePlayed(startFact)
                ? MemoryLiveState.ActiveDialoguePlayed
                : MemoryLiveState.Active;
        }

        /// <summary>All parts in display order: start, steps/parts, completed, failed.</summary>
        public static IEnumerable<(QuestPartLocation loc, QuestPart part)> EnumerateParts(QuestSO quest)
        {
            if (quest == null) yield break;
            yield return (QuestPartLocation.Start, quest.startPart);
            if (quest.steps != null)
            {
                for (int i = 0; i < quest.steps.Count; i++)
                {
                    var parts = quest.steps[i].parts;
                    if (parts == null) continue;
                    for (int j = 0; j < parts.Count; j++)
                        yield return (QuestPartLocation.StepPart(i, j), parts[j]);
                }
            }
            if (quest.completedParts != null)
                for (int j = 0; j < quest.completedParts.Count; j++)
                    yield return (QuestPartLocation.Completed(j), quest.completedParts[j]);
            if (quest.failedParts != null)
                for (int j = 0; j < quest.failedParts.Count; j++)
                    yield return (QuestPartLocation.Failed(j), quest.failedParts[j]);
        }

        public static string NpcDisplayName(NPCEntity npc)
        {
            if (npc == null) return "(no NPC)";
            return string.IsNullOrEmpty(npc.entityName) ? npc.name : npc.entityName;
        }

        public static string SceneName(string path) =>
            string.IsNullOrEmpty(path) ? "?" : Path.GetFileNameWithoutExtension(path);

        // ── Build steps ───────────────────────────────────────────────────────

        private void MapDialogueOwnership(NPCEntity[] npcs)
        {
            if (npcs == null) return;
            var stack = new Stack<DialogueNode>();
            var visited = new HashSet<DialogueNode>();
            foreach (var npc in npcs)
            {
                if (npc == null || npc.memories == null) continue;
                foreach (var memory in npc.memories)
                {
                    if (memory == null) continue;
                    _ownedMemories.Add(memory);
                    if (!_memoryOwners.ContainsKey(memory)) _memoryOwners[memory] = npc;

                    var root = memory.effects?.startdialog;
                    if (root == null) continue;

                    // Iterative DFS — dialogue graphs can contain cycles. First owner wins.
                    visited.Clear();
                    stack.Clear();
                    stack.Push(root);
                    while (stack.Count > 0)
                    {
                        var node = stack.Pop();
                        if (node == null || !visited.Add(node)) continue;
                        if (!_dialogueOwners.ContainsKey(node)) _dialogueOwners[node] = (npc, memory);
                        foreach (var next in Successors(node)) stack.Push(next);
                    }
                }
            }
        }

        private static IEnumerable<DialogueNode> Successors(DialogueNode node)
        {
            if (node.nextNode != null) yield return node.nextNode;
            switch (node)
            {
                case ChoiceDialogueNode choice when choice.choices != null:
                    foreach (var option in choice.choices)
                        if (option?.nextNode != null) yield return option.nextNode;
                    break;
                case TeachChoiceDialogueNode teach when teach.choices != null:
                    foreach (var option in teach.choices)
                    {
                        if (option == null) continue;
                        if (option.nextNode != null) yield return option.nextNode;
                        if (option.confirmNextNode != null) yield return option.confirmNextNode;
                        if (option.denyNextNode != null) yield return option.denyNextNode;
                    }
                    break;
            }
        }

        private void MapDialogueSetters(DialogueNode[] nodes)
        {
            // Union of the asset list and every node reached from an NPC, so nothing is missed.
            var all = new HashSet<DialogueNode>(_dialogueOwners.Keys);
            if (nodes != null)
                foreach (var n in nodes) if (n != null) all.Add(n);

            foreach (var node in all)
            {
                var owner = GetDialogueOwner(node);
                string prefix = owner.HasValue
                    ? $"{NpcDisplayName(owner.Value.npc)} › {owner.Value.memory.name}"
                    : "(no NPC)";

                if (node is StartDialogueNode start && start.dialogueFact != null)
                {
                    AddLink(_setters, new FactLink
                    {
                        Fact = start.dialogueFact, Kind = FactLinkKind.Setter, Source = FactLinkSource.StartDialogueNode,
                        Owner = node, Npc = owner?.npc, Memory = owner?.memory,
                        Label = $"{prefix} › Start '{Shorten(node.text)}'"
                    });
                }

                IEnumerable<ChoiceOption> options = node switch
                {
                    ChoiceDialogueNode c => c.choices,
                    TeachChoiceDialogueNode t => t.choices,
                    _ => null
                };
                if (options == null) continue;
                foreach (var option in options)
                {
                    if (option?.dialogueFact == null) continue;
                    AddLink(_setters, new FactLink
                    {
                        Fact = option.dialogueFact, Kind = FactLinkKind.Setter, Source = FactLinkSource.ChoiceOption,
                        Owner = node, Npc = owner?.npc, Memory = owner?.memory,
                        Label = $"{prefix} › {node.name} › Choice '{Shorten(option.text)}'"
                    });
                }
            }
        }

        private void MapMemoryGates(DialogueNode[] nodes)
        {
            // Same node union as MapDialogueSetters.
            var all = new HashSet<DialogueNode>(_dialogueOwners.Keys);
            if (nodes != null)
                foreach (var n in nodes) if (n != null) all.Add(n);

            foreach (var node in all)
            {
                IReadOnlyList<ChoiceOption> options = node switch
                {
                    ChoiceDialogueNode c => c.choices,
                    TeachChoiceDialogueNode t => t.choices,
                    _ => null
                };
                if (options == null) continue;

                var owner = GetDialogueOwner(node);
                for (int i = 0; i < options.Count; i++)
                {
                    var option = options[i];
                    if (option?.requiredMemory == null) continue;
                    if (!_gatesByMemory.TryGetValue(option.requiredMemory, out var list))
                        _gatesByMemory[option.requiredMemory] = list = new List<MemoryGateLink>();
                    list.Add(new MemoryGateLink
                    {
                        Memory = option.requiredMemory, Node = node, ChoiceIndex = i, ChoiceText = option.text,
                        Npc = owner?.npc, OwnerMemory = owner?.memory,
                        Label = $"{NpcDisplayName(owner?.npc)} › {node.name} › Choice '{Shorten(option.text)}'"
                    });
                }
            }
        }

        private void MapKilledSetters(List<KilledFactBinding> bindings, List<(KilledFact fact, string scenePath)> closed)
        {
            if (bindings != null)
            {
                foreach (var b in bindings)
                {
                    if (!b.IsPrefab && b.Entity != null && b.GameObject != null)
                    {
                        if (!_sceneObjectsByEntity.TryGetValue(b.Entity, out var objects))
                            _sceneObjectsByEntity[b.Entity] = objects = new List<GameObject>();
                        if (!objects.Contains(b.GameObject)) objects.Add(b.GameObject);
                    }

                    if (b.Fact == null) continue;
                    string goName = b.GameObject != null ? b.GameObject.name : "(missing)";
                    AddLink(_setters, new FactLink
                    {
                        Fact = b.Fact, Kind = FactLinkKind.Setter,
                        Source = b.IsPrefab ? FactLinkSource.PrefabPersistentID : FactLinkSource.ScenePersistentID,
                        Owner = b.Component != null ? b.Component : b.GameObject,
                        ScenePath = b.ScenePath,
                        Label = b.IsPrefab
                            ? $"{goName} [prefab {SceneName(b.ScenePath)}]"
                            : $"{goName} [{SceneName(b.ScenePath)}]"
                    });
                }
            }

            if (closed == null) return;
            foreach (var (fact, scenePath) in closed)
            {
                if (fact == null) continue;
                AddLink(_setters, new FactLink
                {
                    Fact = fact, Kind = FactLinkKind.Setter, Source = FactLinkSource.ClosedScene,
                    ScenePath = scenePath,
                    Label = $"[{SceneName(scenePath)}] (closed)"
                });
            }
        }

        private void MapQuestReaders()
        {
            foreach (var quest in _quests)
            {
                foreach (var (loc, part) in EnumerateParts(quest))
                {
                    if (part.fact == null) continue; // reported by the validator
                    AddLink(_readers, new FactLink
                    {
                        Fact = part.fact, Kind = FactLinkKind.Reader, Source = FactLinkSource.QuestPart,
                        Owner = quest, Location = loc,
                        Label = $"{QuestDisplayName(quest)} › {loc}"
                    });
                }
            }
        }

        private void MapMemoryReaders(NPCMemoryEntrySO[] memories)
        {
            var all = new HashSet<NPCMemoryEntrySO>(_memoryOwners.Keys);
            if (memories != null)
                foreach (var m in memories) if (m != null) all.Add(m);
            _allMemories.AddRange(all.OrderBy(m => m.name, StringComparer.Ordinal));

            foreach (var memory in all)
            {
                var npc = GetMemoryOwner(memory);
                AddMemoryConditions(memory, npc, memory.unlockConditions, FactLinkSource.MemoryUnlock, "unlock");
                AddMemoryConditions(memory, npc, memory.invalidationConditions, FactLinkSource.MemoryInvalidation, "invalidation");
            }
        }

        private void AddMemoryConditions(NPCMemoryEntrySO memory, NPCEntity npc, Fact[] facts, FactLinkSource source, string what)
        {
            if (facts == null) return;
            foreach (var fact in facts)
            {
                if (fact == null) continue;
                AddLink(_readers, new FactLink
                {
                    Fact = fact, Kind = FactLinkKind.Reader, Source = source,
                    Owner = memory, Npc = npc, Memory = memory,
                    Label = $"{NpcDisplayName(npc)} › {memory.name} ({what})"
                });
            }
        }

        private void MapRewardReaders(PlayerRewardSO[] rewards)
        {
            if (rewards == null) return;
            foreach (var reward in rewards)
            {
                if (reward == null) continue;
                Fact fact = reward.FactType switch
                {
                    RewardFactType.Killed => reward.KilledFact,
                    RewardFactType.Quest => reward.QuestFact,
                    RewardFactType.Dialogue => reward.DialogueFact,
                    _ => null
                };
                if (fact == null) continue;
                AddLink(_readers, new FactLink
                {
                    Fact = fact, Kind = FactLinkKind.Reader, Source = FactLinkSource.PlayerReward,
                    Owner = reward, Label = $"Reward {reward.name}"
                });
            }
        }

        private void MapQuestFacts(QuestFact[] questFacts)
        {
            if (questFacts == null) return;
            foreach (var qf in questFacts)
            {
                if (qf == null || qf.Quest == null) continue;
                if (!_questFactsByQuest.TryGetValue(qf.Quest, out var list))
                    _questFactsByQuest[qf.Quest] = list = new List<QuestFact>();
                if (!list.Contains(qf)) list.Add(qf);

                AddLink(_readers, new FactLink
                {
                    Fact = qf, Kind = FactLinkKind.Reader, Source = FactLinkSource.QuestFactTarget,
                    Owner = qf.Quest,
                    Label = $"→ {QuestDisplayName(qf.Quest)} · {QuestFactStateLabel(qf)}"
                });
            }
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        public static string QuestDisplayName(QuestSO quest)
        {
            if (quest == null) return "(no quest)";
            return string.IsNullOrEmpty(quest.title) ? quest.name : quest.title;
        }

        /// <summary>"IsStarted" / "IsCompleted" / "IsFailed" / "Step: {title}" — same wording as QuestFactEditor.</summary>
        public static string QuestFactStateLabel(QuestFact qf)
        {
            if (qf == null) return "?";
            if (!qf.IsStepState) return qf.QuestState.ToString();
            int i = qf.QuestStepIndex;
            var steps = qf.Quest != null ? qf.Quest.steps : null;
            if (steps == null || i < 0 || i >= steps.Count) return $"Step {i} (out of range)";
            return string.IsNullOrEmpty(steps[i].title) ? $"Step {i} (no title)" : $"Step: {steps[i].title}";
        }

        private static string Shorten(string text)
        {
            if (string.IsNullOrEmpty(text)) return string.Empty;
            text = text.Replace('\n', ' ');
            return text.Length <= 40 ? text : text.Substring(0, 37) + "...";
        }

        private static void AddLink(Dictionary<Fact, List<FactLink>> map, FactLink link)
        {
            if (!map.TryGetValue(link.Fact, out var list))
                map[link.Fact] = list = new List<FactLink>();
            list.Add(link);
        }

        private static List<T> NonNull<T>(T[] items) where T : UnityEngine.Object
        {
            var list = new List<T>();
            if (items == null) return list;
            foreach (var item in items) if (item != null) list.Add(item);
            return list;
        }
    }
}
