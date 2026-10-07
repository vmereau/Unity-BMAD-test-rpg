using System;
using System.Collections.Generic;
using _Game.ScriptableObjects.Entities;
using Game.Core;
using Game.Dialogue;
using Game.NPC;
using Game.Progression;
using Game.Quest;
using UnityEngine;

namespace Game.Editor.QuestExplorer
{
    /// <summary>Whether a link writes a fact (Setter) or depends on it (Reader).</summary>
    public enum FactLinkKind { Setter, Reader }

    /// <summary>Where a fact link was found. Add a value here when a new setter/reader source appears.</summary>
    public enum FactLinkSource
    {
        ScenePersistentID,
        PrefabPersistentID,
        ClosedScene,
        StartDialogueNode,
        ChoiceOption,
        QuestPart,
        MemoryUnlock,
        MemoryInvalidation,
        PlayerReward,
        QuestFactTarget
    }

    /// <summary>One edge between a <see cref="Fact"/> and the asset / scene object that sets or reads it.</summary>
    public sealed class FactLink
    {
        public Fact Fact;
        public FactLinkKind Kind;
        public FactLinkSource Source;
        /// <summary>Asset or scene component. Null for <see cref="FactLinkSource.ClosedScene"/>.</summary>
        public UnityEngine.Object Owner;
        /// <summary>Scene (or prefab) asset path for PersistentID / closed-scene setters.</summary>
        public string ScenePath;
        public NPCEntity Npc;
        public NPCMemoryEntrySO Memory;
        /// <summary>Human-readable description, e.g. "Guard › Mem_Guard_SpiderOffer › Choice 'I'll do it'".</summary>
        public string Label;
        /// <summary>Quest part location for <see cref="FactLinkSource.QuestPart"/> readers.</summary>
        public QuestPartLocation? Location;
    }

    /// <summary>A choice option whose visibility requires a memory (ChoiceOption.requiredMemory).</summary>
    public sealed class MemoryGateLink
    {
        /// <summary>The requiredMemory.</summary>
        public NPCMemoryEntrySO Memory;
        /// <summary>ChoiceDialogueNode or TeachChoiceDialogueNode.</summary>
        public DialogueNode Node;
        public int ChoiceIndex;
        public string ChoiceText;
        /// <summary>Owner of <see cref="Node"/> (null if unreachable from any NPC).</summary>
        public NPCEntity Npc;
        /// <summary>Memory whose startdialog chain reaches <see cref="Node"/> (may be null).</summary>
        public NPCMemoryEntrySO OwnerMemory;
        /// <summary>e.g. "Guard › Choice_Guard_SpiderOffer › Choice 'I already did it'".</summary>
        public string Label;
    }

    [Flags] public enum MemoryInvolvement { None = 0, ReadsQuestFact = 1, SetsQuestFact = 2 }

    /// <summary>A memory involved in a quest, and why.</summary>
    public sealed class InvolvedMemory
    {
        public NPCMemoryEntrySO Memory;
        /// <summary>First NPC listing the memory; null = on no NPC.</summary>
        public NPCEntity Npc;
        public MemoryInvolvement Reasons;
        /// <summary>"reads Step 1 › Part 1", "dialogue sets Start", ...</summary>
        public List<string> ReasonLabels = new List<string>();
    }

    public enum MemoryLiveState { Locked, Active, ActiveDialoguePlayed, Invalidated }

    public enum MemoryConditionList { Unlock, Invalidation }

    public enum QuestPartSlot { Start, Step, Completed, Failed }

    /// <summary>
    /// Address of a quest part. <see cref="PartIndex"/> is -1 when the location designates a whole
    /// step. Display strings are 1-based ("Step 1 › Part 2").
    /// </summary>
    public readonly struct QuestPartLocation : IEquatable<QuestPartLocation>
    {
        public readonly QuestPartSlot Slot;
        public readonly int StepIndex;
        public readonly int PartIndex;

        public QuestPartLocation(QuestPartSlot slot, int stepIndex, int partIndex)
        {
            Slot = slot;
            StepIndex = stepIndex;
            PartIndex = partIndex;
        }

        public static QuestPartLocation Start => new QuestPartLocation(QuestPartSlot.Start, -1, 0);
        public static QuestPartLocation StepPart(int step, int part) => new QuestPartLocation(QuestPartSlot.Step, step, part);
        public static QuestPartLocation Step(int step) => new QuestPartLocation(QuestPartSlot.Step, step, -1);
        public static QuestPartLocation Completed(int part) => new QuestPartLocation(QuestPartSlot.Completed, -1, part);
        public static QuestPartLocation Failed(int part) => new QuestPartLocation(QuestPartSlot.Failed, -1, part);

        public override string ToString()
        {
            switch (Slot)
            {
                case QuestPartSlot.Start: return "Start";
                case QuestPartSlot.Step:
                    return PartIndex < 0 ? $"Step {StepIndex + 1}" : $"Step {StepIndex + 1} › Part {PartIndex + 1}";
                case QuestPartSlot.Completed: return $"Completed #{PartIndex + 1}";
                case QuestPartSlot.Failed: return $"Failed #{PartIndex + 1}";
                default: return Slot.ToString();
            }
        }

        public bool Equals(QuestPartLocation other) =>
            Slot == other.Slot && StepIndex == other.StepIndex && PartIndex == other.PartIndex;

        public override bool Equals(object obj) => obj is QuestPartLocation other && Equals(other);

        public override int GetHashCode() => ((int)Slot * 397 ^ StepIndex) * 397 ^ PartIndex;
    }

    /// <summary>A <see cref="PersistentID"/> found in a loaded scene or a prefab, with its KilledFact.</summary>
    public struct KilledFactBinding
    {
        public KilledFact Fact;
        public Entity Entity;
        public GameObject GameObject;
        /// <summary>The PersistentID component (link Owner).</summary>
        public Component Component;
        /// <summary>Scene path, or prefab asset path when <see cref="IsPrefab"/>.</summary>
        public string ScenePath;
        public bool IsPrefab;
    }

    /// <summary>
    /// Raw inputs of <see cref="QuestReferenceIndex.Build"/>. Filled by <see cref="QuestIndexCollector"/>
    /// in the editor, or by hand in tests (no AssetDatabase / scenes needed).
    /// </summary>
    public sealed class IndexSources
    {
        public QuestSO[] Quests = Array.Empty<QuestSO>();
        public NPCEntity[] Npcs = Array.Empty<NPCEntity>();
        public NPCMemoryEntrySO[] Memories = Array.Empty<NPCMemoryEntrySO>();
        public DialogueNode[] DialogueNodes = Array.Empty<DialogueNode>();
        public PlayerRewardSO[] Rewards = Array.Empty<PlayerRewardSO>();
        public QuestFact[] QuestFacts = Array.Empty<QuestFact>();
        public Fact[] AllFacts = Array.Empty<Fact>();
        public List<KilledFactBinding> KilledBindings = new List<KilledFactBinding>();
        public List<(KilledFact fact, string scenePath)> ClosedSceneRefs = new List<(KilledFact fact, string scenePath)>();
        public HashSet<QuestSO> EventsManagerQuests = new HashSet<QuestSO>();
        public HashSet<QuestSO> QuestLogQuests = new HashSet<QuestSO>();
    }
}
