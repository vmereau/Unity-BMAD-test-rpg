---
title: 'Quest Explorer NPC Memories'
slug: 'quest-explorer-npc-memories'
created: '2026-10-06'
status: 'completed'
stepsCompleted: [1, 2, 3, 4]
tech_stack: ['Unity 6000.6.2f1', 'C#', 'UI Toolkit (EditorWindow, UIElements)', 'SerializedObject + Undo', 'NUnit EditMode tests']
files_to_modify:
  - 'Assets/_Game/Editor/QuestExplorer/QuestIndexTypes.cs'
  - 'Assets/_Game/Editor/QuestExplorer/QuestReferenceIndex.cs'
  - 'Assets/_Game/Editor/QuestExplorer/QuestValidator.cs'
  - 'Assets/_Game/Editor/QuestExplorer/QuestEditActions.cs'
  - 'Assets/_Game/Editor/QuestExplorer/QuestDetailView.cs'
  - 'Assets/_Game/Editor/QuestExplorer/QuestExplorerWindow.cs'
  - 'Assets/Tests/EditMode/QuestReferenceIndexTests.cs'
  - 'Assets/Tests/EditMode/QuestValidatorTests.cs'
  - 'Assets/Tests/EditMode/QuestMemoryEditActionsTests.cs'
  - 'Assets/_Game/Editor/QuestExplorer/CLAUDE.md'
code_patterns: ['Collector (I/O) → IndexSources (data) → QuestReferenceIndex.Build (pure) → QuestValidator (pure) → UI Toolkit views', 'Undo-aware edits: InGroup + SerializedObject + ApplyModifiedProperties + SetDirty, one named group per public op', 'Rule ids V1..Vn on ValidationIssue, sorted Error → Warning → Info', 'Play-mode polling via QuestExplorerWindow.UpdateLive → view.UpdateLive(wsm), skip !IsEvaluable facts', 'PersistentFoldout keyed by quest GUID prefix']
test_patterns: ['Assets/Tests/EditMode, namespace Tests.EditMode (Quest Explorer tests)', 'ScriptableObject.CreateInstance via Make<T>() with TearDown DestroyImmediate cleanup', 'hand-filled IndexSources, no AssetDatabase', 'Fact subclasses created via Make<T>().Init(key)']
---

# Tech-Spec: Quest Explorer NPC Memories

**Created:** 2026-10-06

## Overview

### Problem Statement

The Quest Explorer (`Tools/Quests/Quest Explorer`) only shows NPC memories indirectly, as fact readers
inside each quest part's collapsed **Links** foldout. You can't see at a glance which NPC memories drive
a quest's dialogue flow. Specifically:

1. `ChoiceOption.requiredMemory` gates are not indexed at all. The original Quest Explorer spec says
   they are "shown under the memory", but that was never built. Memories whose only job is gating a choice
   (`Mem_Guard_Spiders_Already_Killed` / `Mem_Guard_Spiders_Not_Killed` → `Choice_Guard_SpiderOffer`)
   look meaningless in the tool.
2. There is no per-quest memory view, and memories that matter only through their dialogue (e.g.
   `Mem_Guard_SpiderOffer`, whose dialogue sets the quest's start fact) are hard to find.
3. Broken memory data goes unnoticed. `QuestReferenceIndex.AddMemoryConditions` skips null/missing
   conditions without reporting them, and no validator rule covers memories. A memory pointing at a deleted
   `KilledFact` (the recent Spider bug) went undetected.
4. Memory conditions can't be edited from the explorer. You have to find the asset and use the Inspector.

### Solution

Extend the index with memory gate links (choice option → `requiredMemory`) and a quest → involved memories
query. Add an **NPC Memories** section to the quest detail view, grouped by NPC. Each memory shows its unlock /
invalidation conditions, start dialogue and gated choices, plus a live active / locked / invalidated state
in Play Mode. Add memory validator rules, and Undo-aware add / remove / replace of condition facts.

### Scope

**In Scope:**

- Index: `ChoiceOption.requiredMemory` (and `TeachChoiceOption.requiredMemory`) → memory gate links,
  attributed to the owning NPC + dialogue node.
- Index: quest → involved memories. A memory is involved if either:
  - (a) any unlock / invalidation condition is a fact used by one of the quest's parts, or a `QuestFact`
    targeting the quest; or
  - (b) its `startdialog` chain sets (via `StartDialogueNode.dialogueFact` / `ChoiceOption.dialogueFact`) a
    fact used by one of the quest's parts.
- UI: **NPC Memories** section in `QuestDetailView`, grouped by NPC. Per memory: name (ping/select),
  why it's involved, unlock and invalidation condition rows (fact + its quest-part location if any), start
  dialogue, gated choices, and validation issues.
- Play Mode: a read-only live state per memory (active / locked / invalidated), computed from
  `WorldStateManager` fact values with the same logic as runtime.
- Validator: new memory rules, covering at least these:
  - missing / null condition reference
  - memory with no start dialogue and no gated choice
  - memory not listed on any NPC
  - `requiredMemory` not owned by the NPC that owns the choice
- Editing: Undo-aware add / remove / replace of unlock and invalidation condition facts via `SerializedObject`
  (`QuestEditActions` pattern).
- EditMode tests for the new index links, the involvement query, and validator rules.
- Quest Explorer `CLAUDE.md` update.

**Out of Scope:**

- Creating new memory assets from the explorer.
- Editing `requiredMemory` on choices, or a memory's `startdialog` (no dialogue graph editing).
- A separate Memories window mode, and memory → choice chains in Facts mode / part Links.
- Transitive membership (memories gating choices inside dialogues owned by involved memories).
- Runtime changes to `NPCMemoryComponent` / dialogue logic.

## Context for Development

### Codebase Patterns

- Quest Explorer architecture (from `Assets/_Game/Editor/QuestExplorer/CLAUDE.md`): `QuestIndexCollector`
  (editor I/O) → `IndexSources` (plain data) → `QuestReferenceIndex.Build()` (pure) →
  `QuestValidator.Validate()` (pure, rules V1–V13) → `QuestExplorerWindow` / `QuestDetailView` (UI Toolkit),
  with edits in `QuestEditActions` (Undo-aware `SerializedObject`). Assembly `Game.Editor`, namespace
  `Game.Editor.QuestExplorer`.
- The index already collects all `NPCMemoryEntrySO` (`IndexSources.Memories`), maps memory → owning NPC
  (`_memoryOwners`, from `NPCEntity.memories`), and dialogue node → (NPC, memory) via DFS from
  `memory.effects.startdialog`. Memory conditions are fact readers (`FactLinkSource.MemoryUnlock` /
  `MemoryInvalidation`). Null conditions are skipped without a report.
- `ChoiceOption.requiredMemory` (`ScriptableObjects/Dialogue/ChoiceDialogueNode.cs`): "Memory that must be
  active for this choice to appear". Runtime check is in `NPCDialogueGraphComponent` (line ~44) against
  `NPCMemoryComponent.GetActiveMemories()`. `TeachChoiceOption` also has `requiredMemory`.
- `NPCMemoryEntrySO`: `Fact[] unlockConditions` (ALL true → active), `Fact[] invalidationConditions` (ANY true
  → permanently closed), `effects.startdialog`.
- The existing Inspector `NPCMemoryEntrySO_Editor` already lists `requiredMemory` usages in `ChoiceDialogueNode`s
  (useful reference).

**Investigation results (Step 2):**

- **Runtime semantics** (`NPCMemoryEntrySO`): `IsUnlocked() = TopicUnlockEvaluator.AllTrue(unlockConditions)`,
  `IsInvalidated() = AnyTrue(invalidationConditions)`, `IsActive() = unlocked && !invalidated`. In
  `TopicUnlockEvaluator`, an empty unlock array means always unlocked, an empty invalidation array means never
  invalidated, and **null elements are skipped**. A missing unlock condition therefore *removes* that requirement,
  so the memory unlocks earlier than authored, and a missing invalidation condition means the memory never closes
  for that cause. Missing references are Errors.
- `NPCMemoryComponent.GetActiveMemories()` iterates **only the owning `NPCEntity.memories`**.
  `NPCDialogueGraphComponent.FilterByMemory` shows a choice when `requiredMemory == null` or it's in that NPC's
  active memories. A `requiredMemory` not in the NPC's list hides the choice forever.
  `GetActiveStartDialogNodes` also skips an active memory whose `startdialog.dialogueFact` is already played.
- `TeachChoiceOption : ChoiceOption`, so `requiredMemory` is on both `ChoiceDialogueNode.choices` and
  `TeachChoiceDialogueNode.choices`. The index already walks both in `MapDialogueSetters` (`options` switch).
- `NPCMemoriesAutoSync` (`Scripts/Editor/`) fills `NPCEntity.memories` from `Data/NPCs/NPC_X/Memories/**`. A memory
  on no NPC is one that lives outside an NPC `Memories` folder.
- **Index today:** `_memoryOwners` (memory → first NPC, from `npc.memories`), `_dialogueOwners` (node → (npc, memory),
  DFS from `startdialog`), and `IndexSources.Memories` (all memory assets; the collector loads `t:NPCMemoryEntrySO`).
  Dialogue setter `FactLink`s already carry `Memory` / `Npc`, which gives involvement (b) directly. Memory reader
  links (`MemoryUnlock` / `MemoryInvalidation`, `Owner = memory`) give involvement (a). `FactLink` is keyed by
  `Fact`, so a memory gate needs its own link type.
- **Validator:** `Validate(quest, index, allQuests)` runs private `ValidateX` groups. `Add(issues, rule, severity,
  message, context, location)`. The highest existing rule is **V13**, so new rules start at V14. Issues are shown in
  `QuestDetailView.BuildIssues`. On click, `OnIssueClicked` pings `Context` and scrolls to `_locationRows[Location]`.
- **Edits:** `QuestEditActions.ModifyQuest(quest, undoName, Func<SerializedObject,bool>)` wraps `InGroup` (named
  Undo group, prefix `"Quest Explorer: "`) + `ApplyModifiedProperties` + `SetDirty`. It's quest-specific, so
  generalize it to any `UnityEngine.Object`. `NPCMemoryEntrySO` fields are public (`unlockConditions`,
  `invalidationConditions`). Use `SerializedObject` anyway for Undo.
- **UI:** `QuestDetailView` (`VisualElement`) builds Header → Issues → Sections (`Section(text, key)` =
  `PersistentFoldout` keyed `{questGuid}/section/{key}`). Part rows use `ExplorerStyles.Box/Row/Badge`,
  `ObjectField { objectType = typeof(Fact) }`, `LiveFactBadge(fact, host)` (true/false/invalid + Toggle for stored
  facts), and `Edit(changed, structureChanged)` → `_host.OnDataEdited`. Live: `QuestExplorerWindow.UpdateLive`
  (scheduled every `LIVE_INTERVAL_MS` in Play Mode) → `_questView.UpdateLive(wsm)`.
- **Tests:** `QuestReferenceIndexTests` (14 tests) and `QuestValidatorTests` (27 tests), namespace
  `Tests.EditMode`. They use `Make<T>(name)` helpers with `TearDown` cleanup, `KilledFact.Init(guid)` /
  `DialogueFact.Init(key)`, and `MakeNpc(name, out memory, root)`.

### Files to Reference

| File | Purpose |
| ---- | ------- |
| `Assets/_Game/Editor/QuestExplorer/*.cs` | Explorer index, validator, UI, edit actions |
| `Assets/_Game/Editor/QuestExplorer/CLAUDE.md` | Architecture + "add a new source" checklist |
| `_bmad-output/implementation-artifacts/tech-spec-quest-explorer-editor-window.md` | Original explorer spec |
| `Assets/_Game/ScriptableObjects/Entities/NPC/NPCMemoryEntrySO.cs` | Memory data model |
| `Assets/_Game/ScriptableObjects/Dialogue/ChoiceDialogueNode.cs` | `ChoiceOption.requiredMemory` |
| `Assets/_Game/Scripts/AI/NPC/NPCMemoryComponent.cs`, `NPCDialogueGraphComponent.cs` | Runtime active-memory logic |
| `Assets/_Game/Scripts/Editor/NPCMemoryEntrySO_Editor.cs` | Existing requiredMemory usage scan |
| `Assets/_Game/ScriptableObjects/Dialogue/TeachChoiceDialogueNode.cs` | `TeachChoiceOption : ChoiceOption` |
| `Assets/_Game/Scripts/World/TopicUnlockEvaluator.cs` | AllTrue / AnyTrue semantics (nulls skipped) |
| `Assets/_Game/Scripts/Editor/NPCMemoriesAutoSync.cs` | Fills `NPCEntity.memories` from NPC folders |
| `Assets/Tests/EditMode/QuestReferenceIndexTests.cs`, `QuestValidatorTests.cs` | Test patterns |

### Technical Decisions

- Membership = conditions (a) + dialogue setters (b). Not transitive.
- Editing is limited to condition facts. Dialogue data stays read-only.
- Play-mode memory state is shown in the quest view only.
- New validator rules V14–V17 run only over the quest's involved memories, so they surface in that quest's Issues
  list.
- Memory gates get a dedicated `MemoryGateLink` type (not `FactLink`, which is keyed by fact).
- The live memory state is computed by a pure helper that mirrors runtime (nulls skipped, empty unlock = unlocked),
  with a `Func<Fact,bool>` getter so it's testable. Facts with `!IsEvaluable` count as false, so polling doesn't
  spam warnings.
- Conditions are added through a trailing empty "add" `ObjectField`, never by inserting null slots, so the UI
  itself never creates a V14 error.

## Implementation Plan

### Tasks

- [x] Task 1: Add memory types to the index model
  - File: `Assets/_Game/Editor/QuestExplorer/QuestIndexTypes.cs`
  - Action: Add:
    ```csharp
    /// <summary>A choice option whose visibility requires a memory (ChoiceOption.requiredMemory).</summary>
    public sealed class MemoryGateLink
    {
        public NPCMemoryEntrySO Memory;      // the requiredMemory
        public DialogueNode Node;            // ChoiceDialogueNode or TeachChoiceDialogueNode
        public int ChoiceIndex;
        public string ChoiceText;
        public NPCEntity Npc;                // owner of Node (null if unreachable from any NPC)
        public NPCMemoryEntrySO OwnerMemory; // memory whose startdialog chain reaches Node (may be null)
        public string Label;                 // "Guard › Choice_Guard_SpiderOffer › Choice 'I already did it'"
    }

    [Flags] public enum MemoryInvolvement { None = 0, ReadsQuestFact = 1, SetsQuestFact = 2 }

    /// <summary>A memory involved in a quest, and why.</summary>
    public sealed class InvolvedMemory
    {
        public NPCMemoryEntrySO Memory;
        public NPCEntity Npc;                // GetMemoryOwner; null = on no NPC
        public MemoryInvolvement Reasons;
        public List<string> ReasonLabels = new List<string>(); // "reads Step 1 › Part 1", "dialogue sets Start", ...
    }

    public enum MemoryLiveState { Locked, Active, ActiveDialoguePlayed, Invalidated }

    public enum MemoryConditionList { Unlock, Invalidation }
    ```
  - Notes: `System`, `Game.Dialogue` and `Game.NPC` are already imported in this file.

- [x] Task 2: Index memory gates, ownership and quest involvement
  - File: `Assets/_Game/Editor/QuestExplorer/QuestReferenceIndex.cs`
  - Action:
    1. Fields: `Dictionary<NPCMemoryEntrySO, List<MemoryGateLink>> _gatesByMemory`,
       `HashSet<NPCMemoryEntrySO> _ownedMemories` (every memory present in any `npc.memories`, filled in
       `MapDialogueOwnership` next to `_memoryOwners`), and `Dictionary<QuestSO, List<InvolvedMemory>> _involvedCache`.
    2. New build step `MapMemoryGates(DialogueNode[] nodes)`, called in `Build` right after `MapDialogueSetters`.
       It iterates the same node union (`_dialogueOwners.Keys` ∪ `nodes`). For each `ChoiceDialogueNode.choices` /
       `TeachChoiceDialogueNode.choices` option at index `i` with `option?.requiredMemory != null`, add a
       `MemoryGateLink`: Npc / OwnerMemory from `GetDialogueOwner(node)`, `ChoiceText = option.text`, label
       `"{NpcDisplayName or (no NPC)} › {node.name} › Choice '{Shorten(option.text)}'"`.
    3. Queries:
       - `IReadOnlyList<MemoryGateLink> GetGates(NPCMemoryEntrySO memory)` (empty list if none or null).
       - `bool IsOwnedByNpc(NPCMemoryEntrySO memory)`.
       - `IReadOnlyList<InvolvedMemory> GetInvolvedMemories(QuestSO quest)`, computed lazily and cached in
         `_involvedCache`:
         - (a) For each `(loc, part)` in `EnumerateParts(quest)` with `part.fact != null`, each reader of
           `part.fact` with `Source == MemoryUnlock` adds `ReadsQuestFact` with label `"reads {loc}"`, and
           `MemoryInvalidation` adds label `"invalidated by {loc}"`. Then for each `qf` in
           `GetQuestFactsTargeting(quest)`, its MemoryUnlock / MemoryInvalidation readers add `ReadsQuestFact`
           with labels `"reads → {QuestFactStateLabel(qf)}"` / `"invalidated by → {QuestFactStateLabel(qf)}"`.
           Memory = `link.Memory`.
         - (b) For each part fact, each setter with `Memory != null` and `Source` `StartDialogueNode` / `ChoiceOption`
           adds `SetsQuestFact` on `link.Memory` with label `"dialogue sets {loc}"`.
         - Merge by memory (one `InvolvedMemory` per memory, de-duplicated labels, `Npc = GetMemoryOwner`). Order by
           `NpcDisplayName(Npc)` (null NPC last), then `memory.name` (ordinal).
    4. Pure static evaluator (add `using System;`):
       ```csharp
       /// <summary>
       /// Mirrors NPCMemoryEntrySO.IsActive and NPCMemoryComponent's dialogue-played skip.
       /// Null conditions are skipped, like TopicUnlockEvaluator.
       /// </summary>
       public static MemoryLiveState EvaluateMemory(NPCMemoryEntrySO memory, Func<Fact, bool> getFact,
           Func<DialogueFact, bool> isDialoguePlayed)
       ```
       Rules: any non-null invalidation fact true → `Invalidated`. Otherwise, if every non-null unlock fact is true
       (empty or all-null counts as true) → `ActiveDialoguePlayed` when `memory.effects?.startdialog?.dialogueFact
       != null && isDialoguePlayed(it)`, else `Active`. Otherwise → `Locked`. Null memory → `Locked`.
  - Notes: The existing `AddMemoryConditions` keeps skipping nulls for links. Detecting nulls is the validator's job.

- [x] Task 3: Memory validator rules V14–V17
  - File: `Assets/_Game/Editor/QuestExplorer/QuestValidator.cs`
  - Action: Add `ValidateMemories(quest, index, issues)` to `Validate`, after `ValidateQuestFacts`. For each
    `InvolvedMemory im` in `index.GetInvolvedMemories(quest)` (Context = `im.Memory`, Location = null):
    - **V14 Error**: each null element `i` in `unlockConditions` →
      `"{memory.name}: unlock condition #{i+1} is missing — skipped at runtime, so the memory unlocks without it"`.
      Each null element in `invalidationConditions` →
      `"{memory.name}: invalidation condition #{i+1} is missing — skipped at runtime, so it never closes the memory"`.
    - **V15 Warning**: `im.Memory.effects?.startdialog == null && index.GetGates(im.Memory).Count == 0` →
      `"{memory.name} has no start dialogue and gates no choice — it has no effect"`.
    - **V16 Warning**: `!index.IsOwnedByNpc(im.Memory)` →
      `"{memory.name} is not listed on any NPC — move it under Data/NPCs/NPC_X/Memories/ (NPCMemoriesAutoSync)"`.
    - **V17 Error**: each gate in `GetGates(im.Memory)` with `gate.Npc != null` and
      `(gate.Npc.memories == null || !gate.Npc.memories.Contains(im.Memory))` →
      `"{gate.Label} requires {memory.name}, which {NpcDisplayName(gate.Npc)} doesn't have — the choice never shows"`.
  - Notes: Update the `ValidationIssue.Rule` XML doc to "V1..V17". By design, rules only cover memories involved in
    the validated quest. Add `using Game.NPC;`.

- [x] Task 4: Undo-aware memory condition edits
  - File: `Assets/_Game/Editor/QuestExplorer/QuestEditActions.cs`
  - Action:
    1. Generalize `ModifyQuest` into `private static bool Modify(UnityEngine.Object target, string undoName,
       Func<SerializedObject, bool> edit)` with the same body. Keep `ModifyQuest(quest, …) => Modify(quest, …)` so
       existing call sites don't change.
    2. Helper: `private static SerializedProperty ConditionsProp(SerializedObject so, MemoryConditionList list) =>
       so.FindProperty(list == MemoryConditionList.Unlock ? "unlockConditions" : "invalidationConditions");`
    3. Public ops (one Undo group each):
       - `AddMemoryCondition(NPCMemoryEntrySO memory, MemoryConditionList list, Fact fact)`. Returns false if
         `fact == null` or it's already in the array. Otherwise `InsertArrayElementAtIndex(arraySize)` and set the
         new element's `objectReferenceValue = fact`. Undo `"Add Memory Condition"`.
       - `SetMemoryCondition(NPCMemoryEntrySO memory, MemoryConditionList list, int index, Fact fact)`.
         Bounds-checked replace (null allowed). Undo `"Set Memory Condition"`.
       - `RemoveMemoryCondition(NPCMemoryEntrySO memory, MemoryConditionList list, int index)`. Bounds-checked
         `DeleteArrayElementAtIndex(index)`. **If `arraySize` didn't shrink** (legacy behaviour nulls a non-null
         object reference on the first delete), call it again. Undo `"Remove Memory Condition"`.
  - Notes: Add `using Game.NPC;`.

- [x] Task 5: NPC Memories section in the quest detail view
  - File: `Assets/_Game/Editor/QuestExplorer/QuestDetailView.cs`
  - Action:
    1. `BuildSections`: after the Failed section, `Add(MemoriesSection())`. The section is
       `Section($"NPC Memories ({n})", "memories")`. If `n == 0`, add a muted `Label("No NPC memory reads or sets
       this quest's facts.")`.
    2. Group `_index.GetInvolvedMemories(_quest)` by `Npc` (keep the index order). One sub-foldout per NPC,
       `MakeFoldout(NpcDisplayName(npc), $"{_keyPrefix}/memnpc/{npc?.name ?? "none"}", true)`, with null owners under
       `"(no NPC)"`. Under each NPC foldout, first add `LinkRowFactory.BuildSceneObject(go)` for each
       `_index.GetSceneObjectsForNpc(npc)`.
    3. `MemoryRow(InvolvedMemory im, Foldout npcFoldout)` → `ExplorerStyles.Box()`, registered in new
       `Dictionary<NPCMemoryEntrySO, VisualElement> _memoryRows` and `Dictionary<NPCMemoryEntrySO, Foldout>
       _memoryFoldouts`:
       - Header line: `ExplorerStyles.ReadOnlyObjectField(memory, typeof(NPCMemoryEntrySO))` (flexGrow 1), one
         `ExplorerStyles.Badge(label)` per `ReasonLabels`, and a live-state badge (`ExplorerStyles.Badge("")`, hidden
         until Play Mode).
       - Bold label "Unlock — all must be true", then `ConditionRow(memory, Unlock, i, element)` for each element, then
         `AddConditionField(memory, Unlock)`. Then "Invalidation — any closes it" the same way.
       - `ConditionRow`: an `ObjectField { objectType = typeof(Fact), allowSceneObjects = false, value = element }`.
         On change → `Edit(QuestEditActions.SetMemoryCondition(memory, list, i, newValue as Fact), true)`. Then:
         - if the element is null, a red `"missing — skipped at runtime"` label (`ExplorerStyles.ErrorColor`);
         - otherwise a badge with its quest location if it's one of this quest's part facts (first match in
           `QuestReferenceIndex.EnumerateParts(_quest)`, `loc.ToString()`), else a muted `"outside quest"` label, plus
           a `LiveFactBadge(element, _host)` (Badge + Toggle) added to `_liveBadges`;
         - a `✕` button → `Edit(QuestEditActions.RemoveMemoryCondition(memory, list, i), true)`.
       - `AddConditionField`: an empty `ObjectField("+ add") { objectType = typeof(Fact), allowSceneObjects = false }`.
         On a non-null value → `Edit(QuestEditActions.AddMemoryCondition(memory, list, fact), true)`. If that returns
         false (duplicate), reset the field to null.
       - "Start dialogue": `ReadOnlyObjectField(memory.effects?.startdialog, typeof(StartDialogueNode))` when set,
         else a muted `"none"`.
       - `$"Gates choices ({gates.Count})"`: per `MemoryGateLink`, a row with
         `ReadOnlyObjectField(gate.Node, typeof(DialogueNode))` and `Label(gate.Label)`, or a muted `"none"`.
    4. Live: keep `List<(NPCMemoryEntrySO memory, Label badge)> _memoryStates`. In `UpdateLive(wsm)`, for each pair:
       `QuestReferenceIndex.EvaluateMemory(memory, f => QuestValidator.IsEvaluable(f) && wsm.GetFact(f),
       d => wsm.IsDialoguePlayed(d))` →
       - Active → "active" / `OkColor`
       - ActiveDialoguePlayed → "active · dialogue played" / `WarnColor`
       - Locked → "locked" / `BadgeBackground`
       - Invalidated → "invalidated" / `MutedText`

       Then `display = Flex`.
    5. `OnIssueClicked`: keep the ping. Also, if `issue.Context is NPCMemoryEntrySO m && _memoryRows.TryGetValue(m,
       out var memRow)`, set the memories section foldout and `_memoryFoldouts[m]` to open, then
       `_host.ScrollDetailTo(memRow)` and `ExplorerStyles.Flash(memRow)`.
  - Notes: Add `using Game.NPC;`, `using Game.Dialogue;`. Each memory edit passes `structureChanged: true`, so the index
    rebuilds and memberships / badges refresh.

- [x] Task 6: Index tests
  - File: `Assets/Tests/EditMode/QuestReferenceIndexTests.cs`
  - Action: Add tests, using hand-built `IndexSources` and the existing `Make<T>` / `MakeNpc` helpers:
    1. A `ChoiceDialogueNode` reachable from NPC A's memory M1 (Start → Choice), whose option 0 has
       `requiredMemory = M2` → `GetGates(M2)` has one link with `Npc == A`, `OwnerMemory == M1`, `ChoiceIndex == 0`.
    2. A `TeachChoiceDialogueNode` option with `requiredMemory` → one gate.
    3. A memory whose unlock condition is a quest step part fact → `GetInvolvedMemories(quest)` contains it with
       `ReadsQuestFact` and the label `"reads Step 1 › Part 1"`.
    4. A memory invalidated by a `QuestFact` targeting the quest (in `IndexSources.QuestFacts`) → involved with a
       label starting `"invalidated by → "`.
    5. A memory whose startdialog chain has a choice setting the quest's Start `DialogueFact` → involved with
       `SetsQuestFact` and `"dialogue sets Start"`.
    6. A memory reading only unrelated facts → not involved.
    7. A memory that both reads and sets quest facts → listed once, with both flags.
    8. `IsOwnedByNpc` is true for a memory in `npc.memories` and false for a memory only in `IndexSources.Memories`.
    9. `EvaluateMemory` with a dictionary-backed `getFact`:
       - empty conditions → Active;
       - an unlock fact false → Locked;
       - unlock true and an invalidation true → Invalidated;
       - `{ null, trueFact }` unlock → Active;
       - unlocked with the start dialogue fact played → ActiveDialoguePlayed.

- [x] Task 7: Validator tests
  - File: `Assets/Tests/EditMode/QuestValidatorTests.cs`
  - Action: Extend the existing Guard / `Mem_Offer` / `Mem_Reward` fixture, or add a local builder. Tests:
    - V14: an involved memory with `unlockConditions = { questPartFact, null }` → exactly one V14 Error containing
      "unlock condition #2". Same check for a null invalidation element.
    - V15: an involved memory with no startdialog and no gate → V15 Warning. With a gate → no V15.
    - V16: an involved memory that's in no `npc.memories` → V16 Warning.
    - V17: a choice in NPC A's dialogue requiring a memory owned only by NPC B → V17 Error. Requiring a memory A
      owns → no V17.
    - Scope: a memory not involved in the quest, with a null condition → no V14.
    - Ordering: the V14 / V17 Errors come before the V15 / V16 Warnings.

- [x] Task 8: Documentation
  - File: `Assets/_Game/Editor/QuestExplorer/CLAUDE.md`
  - Action:
    - Architecture block: `QuestReferenceIndex` also maps memory → gating choices (`MemoryGateLink`), memory
      ownership, and quest → involved memories (reads / sets quest facts), plus static `EvaluateMemory`.
    - Validator: rules V1–V17. V14–V17 are memory rules, scoped to memories involved in the validated quest.
    - Rules / Gotchas: (1) `TopicUnlockEvaluator` skips null conditions, so a missing unlock reference makes a memory
      unlock early (V14 is an Error). (2) `requiredMemory` must be in the owning NPC's `memories`, or the choice
      never shows (V17). (3) `DeleteArrayElementAtIndex` on an object-reference array may null the element instead
      of removing it; `RemoveMemoryCondition` checks `arraySize` and deletes again.
    - Mention the quest detail's **NPC Memories** section (condition editing, live state in Play Mode).

### Acceptance Criteria

- [ ] AC 1: Given `Quest_SpiderClear` open in the Quest Explorer, when the detail view loads, then an **NPC Memories (5)** section lists under "Guard": `Mem_Guard_SpiderOffer`, `Mem_Guard_SpiderQuestActive`, `Mem_Guard_SpiderReward`, `Mem_Guard_Spiders_Already_Killed` and `Mem_Guard_Spiders_Not_Killed`. Each has at least one reason badge.
- [ ] AC 2: Given `Mem_Guard_Spiders_Already_Killed`, when its row is shown, then "Gates choices (1+)" lists the `Choice_Guard_SpiderOffer` option that requires it, and its unlock rows show the location badges "Step 1 › Part 1" and "Step 1 › Part 2".
- [ ] AC 3: Given `Mem_Guard_SpiderOffer`, when its row is shown, then it has the reason "dialogue sets Start" (its dialogue sets `DialogueFact_Guard_SpiderQuestAccepted`), and its start dialogue field shows its `StartDialogueNode`.
- [ ] AC 4: Given a quest whose involved memory has a null (missing) unlock condition, when the quest is validated, then a **V14 Error** appears in Issues. Clicking it pings the memory, expands its row and scrolls to it. The null row shows "missing — skipped at runtime".
- [ ] AC 5: Given an involved memory with no start dialogue and no gated choice, when validated, then a **V15 Warning** is shown.
- [ ] AC 6: Given an involved memory that no `NPCEntity.memories` contains, when validated, then a **V16 Warning** is shown and the memory is grouped under "(no NPC)".
- [ ] AC 7: Given a choice in NPC A's dialogue whose `requiredMemory` is owned only by NPC B, when a quest involving that memory is validated, then a **V17 Error** names the choice and NPC A.
- [ ] AC 8: Given a memory row, when the user assigns a Fact in its unlock "+ add" field, then the fact is appended to `unlockConditions`, the index rebuilds (badges update), and one **Ctrl+Z** removes it.
- [ ] AC 9: Given a memory condition row, when the user clicks ✕, then that element is removed: the array size shrinks rather than the element being nulled, and Undo restores it.
- [ ] AC 10: Given a condition row, when the user replaces its fact in the ObjectField, then the asset updates and the change is undoable. Given a fact already in the list, when it's assigned in "+ add", then nothing is added and the field resets.
- [ ] AC 11: Given Play Mode with `WorldStateManager` running and the quest detail open, then each memory shows a live badge (locked / active / active · dialogue played / invalidated). The badge updates within one live tick after one of its stored condition facts is flipped with that fact's Toggle button.
- [ ] AC 12: Given a condition fact that can't be evaluated (e.g. a `QuestFact` with no quest), when Play Mode polls, then that fact counts as false for the memory state, its LiveFactBadge shows "invalid", and nothing is logged on each tick.
- [ ] AC 13: Given a quest whose facts no memory reads or sets, when it's shown, then the section reads "NPC Memories (0)" with the muted "No NPC memory reads or sets this quest's facts." text.
- [ ] AC 14: Given the EditMode suite, when it runs, then all new index and validator tests and all pre-existing tests pass.

## Additional Context

### Dependencies

- None external. Builds on the Quest Explorer (`tech-spec-quest-explorer-editor-window.md`, completed).
- No runtime code changes. Everything is editor-only (`Game.Editor` assembly) plus EditMode tests.

### Testing Strategy

- **Unit (EditMode)**: Tasks 6–7, covering pure index / validator / `EvaluateMemory` behaviour over hand-built
  `IndexSources`. Run via MCP `run_tests` (EditMode). The existing 14 + 27 Quest Explorer tests must still pass.
- **Manual (Editor)**: Open `Tools/Quests/Quest Explorer`, select Spider Infestation, and check AC 1–3. For V14,
  clear one unlock element on a memory, check the error and the red row, then Undo. Also add, replace and remove a
  condition, and Undo each (AC 8–10).
- **Manual (Play Mode)**: With Spider Infestation open, Toggle `DialogueFact_Guard_SpiderQuestAccepted` (Start part).
  `Mem_Guard_SpiderOffer` should go to "invalidated" and `Mem_Guard_SpiderQuestActive` to "active". Toggle both
  DarknessSpider KilledFacts (Step 1 parts), and `Mem_Guard_Spiders_Already_Killed` should go to "active". Check
  that `read_console` shows no per-tick warnings.

### Notes

- **Risk: `DeleteArrayElementAtIndex` legacy behaviour** on object-reference arrays (it nulls the element first and
  removes it on a second call). Task 4 handles this with an `arraySize` check, and AC 9 verifies it.
- **Risk: rebuild churn.** Every condition edit triggers an index rebuild (`structureChanged: true`). That's fine at
  the current data size, and rebuilds are already debounced.
- **Limitation:** V14–V17 only run for memories involved in the selected quest. A broken memory tied to no quest stays
  invisible until a future Memories mode.
- **Limitation:** Membership isn't transitive. A memory gating a choice inside an involved memory's dialogue only
  appears if it also reads or sets the quest's facts. Both Spider gate memories do.
- **Future:** a Memories window mode, editing `requiredMemory` / `startdialog`, creating memories from a quest part,
  and memory → choice chains in Facts mode / part Links.
- Test fixture: the `Quest_SpiderClear` / `NPC_Guard` Spider memories cover every case. Involvement (a) applies to
  all five memories. Offer's invalidation reads the Start fact, and its dialogue also sets it, so (b) applies
  to Offer too. Choice gating applies to Already_Killed and Not_Killed.

## Review Notes

- Adversarial review completed
- Findings: 16 total, 10 fixed, 6 skipped
- Resolution approach: auto-fix
- Fixed: F4 (edited memories stay listed for the session via `QuestExplorerWindow.EditedMemories`), F5 (memory
  row disabled after an edit until the rebuild), F6 (None removes the condition; Set rejects duplicates), F7
  (`QuestMemoryEditActionsTests`), F10 (group by NPC reference), F12 ("already listed" notice), F13/F14 (extra
  `EvaluateMemory` and reason de-duplication tests), F15 (legacy delete wording in CLAUDE.md / comment).
- Skipped as accepted limitations: F1 (V14 only covers memories still involved in the quest — needs a future
  global memory validation / Memories mode), F2/F3 (first-owner-wins dialogue attribution for nodes shared
  between NPCs), F8, F9, F11, F16 (behaviour specified by this spec).
