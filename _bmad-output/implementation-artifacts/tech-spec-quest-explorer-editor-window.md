---
title: 'Quest Explorer Editor Window'
slug: 'quest-explorer-editor-window'
created: '2026-10-06'
status: 'completed'
stepsCompleted: [1, 2, 3, 4]
tech_stack: ['Unity 6000.6.2f1', 'C# Editor scripting', 'UI Toolkit (EditorWindow.CreateGUI)', 'SceneView Handles', 'SerializedObject/Undo API', 'AssetDatabase', 'NUnit EditMode tests']
files_to_modify: ['Assets/_Game/Editor/QuestExplorer/QuestIndexTypes.cs (new)', 'Assets/_Game/Editor/QuestExplorer/QuestReferenceIndex.cs (new)', 'Assets/_Game/Editor/QuestExplorer/QuestIndexCollector.cs (new)', 'Assets/_Game/Editor/QuestExplorer/QuestValidator.cs (new)', 'Assets/_Game/Editor/QuestExplorer/QuestExplorerWindow.cs (new)', 'Assets/_Game/Editor/QuestExplorer/QuestDetailView.cs (new)', 'Assets/_Game/Editor/QuestExplorer/QuestSceneHighlighter.cs (new)', 'Assets/_Game/Editor/QuestExplorer/QuestEditActions.cs (new)', 'Assets/_Game/Editor/QuestExplorer/QuestStepRemap.cs (new)', 'Assets/_Game/Editor/QuestExplorer/QuestExplorerNamePrompt.cs (new)', 'Assets/_Game/Editor/QuestExplorer/CLAUDE.md (new)', 'Assets/_Game/Data/Facts/CLAUDE.md (new)', 'Assets/_Game/Data/Quests/CLAUDE.md', 'CLAUDE.md', 'Assets/Tests/EditMode/Tests.EditMode.asmdef', 'Assets/Tests/EditMode/QuestReferenceIndexTests.cs (new)', 'Assets/Tests/EditMode/QuestValidatorTests.cs (new)']
code_patterns: ['Game.Editor asmdef in Assets/_Game/Editor', 'UI Toolkit CreateGUI EditorWindow', 'SceneView.duringSceneGui subscribe/unsubscribe', 'SerializedObject.FindProperty for private fields', 'Undo grouping', 'CreateInstance + Init + AssetDatabase.CreateAsset', 'GameLog with TAG']
test_patterns: ['NUnit EditMode in Assets/Tests/EditMode', 'ScriptableObject.CreateInstance fixtures', '[SystemName]Tests naming']
---

# Tech-Spec: Quest Explorer Editor Window

**Created:** 2026-10-06

## Overview

### Problem Statement

Quests are wired across many disconnected assets: `QuestSO` parts reference typed `Fact` SOs
(`DialogueFact`, `KilledFact`, `QuestFact`, `WorldFact`, ...); those facts are *written* by dialogue
nodes / choice options (`DialogueFact`) and by scene `PersistentID` components (`KilledFact`), and
*read* by `NPCMemoryEntrySO` unlock/invalidation conditions, other quests (`QuestFact`),
`PlayerRewardSO`, `QuestEventsManager` and `QuestLogUI`. Fact SOs hold no back-references, so
answering "which spider is this KilledFact?" ends at a GUID string and requires a manual scene
search. Miswiring is invisible: `Quest_SpiderClear` step 1 requires
`KilledFact_Enemy_DarknessSpider`, which no scene or prefab `PersistentID` references — the step can
never complete — and the description says "all five" while only 2 facts / 3 scene spiders exist.

### Solution

An editor-only, dockable **Quest Explorer** window (UI Toolkit, `Game.Editor` namespace) backed by
a reusable **reference index** service that maps every `Fact` to its setters and readers. The window
shows quest structure with ping/select/frame links, validation warnings, play-mode live state with
fact toggles, Scene-view highlighting of linked objects, and Undo-aware inline editing helpers.

### Scope

**In Scope:**

1. **Reference index service** (rebuilt on demand + on asset/scene change):
   - Fact setters: `PersistentID._killedFact` in loaded scenes and prefabs; closed `.unity` files
     scanned by asset GUID (text search); `StartDialogueNode.dialogueFact` and
     `ChoiceOption.dialogueFact` (resolved to the owning NPC where possible).
   - Fact readers: `QuestSO` parts (start / steps / completed / failed), `NPCMemoryEntrySO`
     unlock/invalidation conditions, `QuestFact` (→ quest), `PlayerRewardSO`.
   - Quest registration: presence in `QuestEventsManager._quests` and `QuestLogUI._allQuests`.
2. **Explorer window:** quest list (left) + tree (right): Start / Steps → Parts / Completed / Failed
   → Fact → setters & readers. Ping / Select / Frame buttons; "Open scene & select" for setters in
   closed scenes. Any `Fact` asset can be opened directly (fact lookup mode).
3. **Validation:** orphan facts (no setter), null facts in parts, empty part entries, quest missing
   from `QuestEventsManager` / `QuestLogUI`, duplicate `questId`, `questId` ≠ filename suffix,
   description-number vs fact-count hint.
4. **Play-mode live state:** each fact's current value and derived quest/step state, refreshed live;
   toggle a fact for testing through the `WorldStateManager` typed API.
5. **Scene-view highlight:** for the selected quest, label + outline its linked scene objects.
6. **Inline editing (Undo-aware):** add/remove/reorder steps and parts, edit entries, "Create
   KilledFact for selected GameObject" (creates asset + assigns to its `PersistentID`), create
   `DialogueFact` / `QuestFact` in place.

**Out of Scope:**

- Node-graph view (index must be designed for reuse by a future graph spec)
- Runtime quest logic or data-model changes
- Dialogue graph editing
- Save/load
- Fixing the `Quest_SpiderClear` data itself (separate quick fix; the tool will flag it)

## Context for Development

### Codebase Patterns

**Data model:**

- `QuestSO` (`Game.Quest`, `ScriptableObjects/Quest/QuestSO.cs`): `questId`, `title`, `description`,
  `startPart` (struct `QuestPart { Fact fact; string entry; }`), `List<QuestPart> completedParts`
  (ANY true), `failedParts` (ANY true), `List<QuestStep> steps` (struct `{ title, description,
  List<QuestPart> parts }`; step active if ANY part true, completed if ALL true via
  `IsStepCompleted(i)`). Derived state only: `IsStarted` / `IsCompleted` / `IsFailed` read
  `WorldStateManager.Instance.GetFact(...)`. **`QuestPart` / `QuestStep` are structs** — edit via
  `SerializedObject` / `SerializedProperty`, never by mutating a copy.
- `Fact` (`Game.Core`, abstract SO) subclasses:
  - **Stored** (key in `WorldStateManager._worldFacts`): `KilledFact` (`_guid`, key
    `Killed.{guid}`), `DialogueFact` (`_nodeId`, key `Dialogue.Played.{nodeId}`), `WorldFact`
    (`_eventKey`, key `World.{eventKey}`).
  - **Computed** (not stored, not toggleable): `QuestFact` (`_quest`, `_questState` int: 0
    IsStarted, 1 IsCompleted, 2 IsFailed, ≥3 = step index + 3; `IsStepState`, `QuestStepIndex`),
    `SkillFact` (`_skill`), `StatFact` (`_requirements`).
- `WorldStateManager` (`Game.Core`, singleton `Instance`, in `Core.unity`): `GetFact(Fact)` handles
  stored + computed; `SetFact(Fact, bool)` raises `_onFactChanged` (+ `_onDialoguePlayed` for
  DialogueFact); `RegisterKill(KilledFact, Entity)` additionally raises `_onEntityKilled`
  (XP/rewards). Fact keys must never be built by hand.

**Fact setters (who writes a fact):**

| Fact type | Setter | Where the link lives |
|---|---|---|
| `KilledFact` | `PersistentID.RegisterDeath()` | Scene/prefab `PersistentID._killedFact` (private, serialized). Scene spiders are prefab instances overriding `_killedFact` in `m_Modifications` (`objectReference: {fileID: 11400000, guid: <fact asset guid>}`) — a text scan of `.unity` files for the fact's asset GUID finds closed-scene setters. `PersistentID.Entity` gives the `Entity` / `NPCEntity` SO. |
| `DialogueFact` | `DialogueSystem` (`Scripts/World/DialogueSystem.cs:169,190`) | `StartDialogueNode.dialogueFact`; `ChoiceOption.dialogueFact` in `ChoiceDialogueNode.choices[]`; `TeachChoiceOption : ChoiceOption` in `TeachChoiceDialogueNode.choices[]`. |
| `WorldFact` | **None in code** (`SetWorldEvent` has no callers) | `Quest_FindHerbalist` uses `WorldFact_herbalist_forest_camp_found`, referenced nowhere else → validator flags "no setter mechanism". |
| `QuestFact` / `SkillFact` / `StatFact` | Computed | Displayed as "computed"; QuestFact links to its quest + state label. |

**Dialogue → NPC attribution:** `NPCEntity : Entity` (`Game.NPC`) has `List<NPCMemoryEntrySO>
memories` (auto-synced from `Data/NPCs/NPC_X/Memories/**` by `NPCMemoriesAutoSync`).
`NPCMemoryEntrySO.effects.startdialog` (`StartDialogueNode`) is the chain root. Walk
`DialogueNode.nextNode`, `ChoiceOption.nextNode`, `TeachChoiceOption.confirmNextNode` /
`denyNextNode` with a visited set (cycles possible) to attribute every node / choice to an NPC +
memory. NPC scene object = a `PersistentID` whose `Entity` is that `NPCEntity`.

**Fact readers:** `QuestSO` parts (start / step i part j / completed / failed);
`NPCMemoryEntrySO.unlockConditions[]` / `invalidationConditions[]`; `PlayerRewardSO`
(`FactType` + `KilledFact` / `QuestFact` / `DialogueFact` public getters); `QuestFact.Quest`.
`ChoiceOption.requiredMemory` is a memory gate (shown under the memory, not as a fact reader).

**Quest registration:** `QuestEventsManager._quests` on `Assets/_Game/Prefabs/QuestEventsManager.prefab`
(auto-synced by `QuestEventsManagerAutoSync`); `QuestLogUI._allQuests` on
`Assets/_Game/Prefabs/UI/QuestLog/QuestLogUI.prefab` (manual). Read with
`PrefabUtility.LoadPrefabContents` → `SerializedObject` → unload, or `AssetDatabase.LoadAssetAtPath<GameObject>` + `GetComponent` + `SerializedObject` (read-only, preferred — no unload needed).

**Editor tooling conventions:**

- Editor windows live in `Assets/_Game/Editor/` → assembly **`Game.Editor`**
  (`ItemIconGenerator.asmdef`: Editor-only, references `Game`, rootNamespace `Game.Editor`).
  Existing windows (`HitboxTunerWindow`, `WeaponCreatorWindow`) use UI Toolkit `CreateGUI()`,
  `[MenuItem("Tools/<Area>/<Name>")]`, `SceneView.duringSceneGui` + `Handles` subscribed in
  `OnEnable` / unsubscribed in `OnDisable`, `EditorApplication.playModeStateChanged`, Undo grouping
  (`IncrementCurrentGroup` / `SetCurrentGroupName` / `CollapseUndoOperations`),
  `private const string TAG`, `GameLog.*` for logging.
- `Assets/_Game/Scripts/Editor/` compiles into the **runtime** `Game` assembly and needs
  `#if UNITY_EDITOR` — the new tool must NOT go there.
- Asset creation: `ScriptableObject.CreateInstance<T>()` + `Init(...)` +
  `AssetDatabase.CreateAsset` (see `GenerateGenericKilledFacts`: creates
  `Assets/_Game/Data/Enemies/{SceneName}/Generic/KilledFact_{GOName}.asset`, reuses an existing
  asset at that path, assigns via `SerializedObject.FindProperty("_killedFact")`). Reuse that path
  convention. Per `.claude/commands/quests/implement.md`, new `DialogueFact` / `WorldFact` /
  `QuestFact` assets go in `Assets/_Game/Data/Facts/` (some older facts live in
  `Data/Quests/{Quest}/` — the index finds facts anywhere via `AssetDatabase`, location is not
  significant). Rewards live in `Assets/_Game/Data/Rewards/`.
- Data folders scanned (all via `AssetDatabase`, no path filter except prefabs/scenes):
  `Data/Quests`, `Data/Facts`, `Data/NPCs/*/{Dialogues,Teachings,Memories}`, `Data/Rewards`,
  `Data/Enemies/StartingTown/Generic`. Scenes: only `StartingTown.unity` is a gameplay scene
  today (`Core.unity`, `TestScene.unity` also scanned — cheap).
- Stale doc paths: `Data/Quests/CLAUDE.md` and `.claude/commands/quests/*.md` reference
  `docs/Quests/...` / `docs/Systems/Quest.md`; the real folder is `docs/World/Quests/`.
- Private serialized fields (`_killedFact`, `_quest`, `_questState`, `_nodeId`, `_guid`,
  `_eventKey`, `_quests`, `_allQuests`) are read/written via `SerializedObject.FindProperty`,
  never reflection.
- Logging via `GameLog` with `TAG`; never `Debug.Log`.

### Files to Reference

| File | Purpose |
| ---- | ------- |
| `Assets/_Game/ScriptableObjects/Quest/QuestSO.cs` | Quest / step / part structure and derived state |
| `Assets/_Game/ScriptableObjects/Facts/*.cs` | Fact subclasses, key formats, `Init` helpers |
| `Assets/_Game/Scripts/Core/State/WorldStateManager.cs` | `GetFact` / `SetFact` / `RegisterKill` for play-mode state |
| `Assets/_Game/Scripts/World/PersistentID.cs` | `_killedFact` + `Entity` scene binding |
| `Assets/_Game/Scripts/World/DialogueSystem.cs` | Where DialogueFacts are written |
| `Assets/_Game/ScriptableObjects/Dialogue/*.cs` | Dialogue node traversal fields |
| `Assets/_Game/ScriptableObjects/Entities/NPC/NPCEntity.cs`, `NPCMemoryEntrySO.cs` | NPC → memories → start dialog; fact readers |
| `Assets/_Game/ScriptableObjects/Rewards/PlayerRewardSO.cs` | Reward fact readers |
| `Assets/_Game/Scripts/Quest/QuestEventsManager.cs`, `Scripts/UI/Quest/QuestLogUI.cs` | Registration lists |
| `Assets/_Game/Scripts/Editor/GenerateGenericKilledFacts.cs` | KilledFact creation + assignment pattern |
| `Assets/_Game/Scripts/Editor/QuestFactEditor.cs` | Quest state labels (IsStarted / … / `Step: {title}`) — reuse logic |
| `Assets/_Game/Editor/HitboxTunerWindow.cs` | EditorWindow + Scene GUI + Undo conventions |
| `Assets/_Game/Editor/ItemIconGenerator.asmdef` | `Game.Editor` assembly definition |
| `Assets/Tests/EditMode/Tests.EditMode.asmdef`, `WorldStateManagerFactsTests.cs` | Test assembly + fact test style |
| `Assets/_Game/Data/Quests/Spiders/Quest_SpiderClear.asset`, `Assets/_Game/Scenes/StartingTown.unity` | Real data for manual verification |

### Technical Decisions

- **Location / assembly:** new folder `Assets/_Game/Editor/QuestExplorer/`, compiled into the
  existing `Game.Editor` assembly; namespace `Game.Editor.QuestExplorer`. Menu
  `Tools/Quests/Quest Explorer`.
- **Layered design (one spec, each layer usable alone):** `QuestReferenceIndex` (pure data, no UI)
  → `QuestValidator` (pure rules over the index) → `QuestExplorerWindow` (UI Toolkit) →
  `QuestSceneHighlighter` (Scene GUI) → `QuestEditActions` (Undo-aware mutations). The index is
  UI-agnostic so a future graph view can reuse it.
- **Index scope:** `AssetDatabase.FindAssets("t:...")` for `Fact`, `QuestSO`, `NPCEntity`,
  `NPCMemoryEntrySO`, `DialogueNode`, `PlayerRewardSO`; `PersistentID` in loaded scenes
  (`Object.FindObjectsByType<PersistentID>(FindObjectsInactive.Include, FindObjectsSortMode.None)`)
  and in prefabs (`t:Prefab` → `GetComponentsInChildren<PersistentID>(true)`); closed `.unity`
  files under `Assets/_Game/Scenes/` scanned as text for each KilledFact asset GUID (setter
  recorded as scene path only → "Open scene & select" opens it additively/single after save prompt
  and then resolves the live object).
- **Index refresh:** build on window open, manual Refresh button, and mark-dirty on
  `EditorApplication.projectChanged` / `hierarchyChanged` / `EditorSceneManager.sceneOpened`
  (debounced via `EditorApplication.delayCall`). No `AssetPostprocessor` writes.
- **Play mode:** poll `WorldStateManager.Instance.GetFact` on a ~250 ms
  `rootVisualElement.schedule` while playing. Toggle only stored facts (`KilledFact`,
  `DialogueFact`, `WorldFact`) via `SetFact(fact, !value)` — documented as raw (no XP / rewards;
  no despawn). Computed facts are read-only.
- **Editing:** all mutations through `SerializedObject` + `Undo` with named groups; asset
  creation via `CreateInstance` + `Init` + `AssetDatabase.CreateAsset` +
  `Undo.RegisterCreatedObjectUndo`.
- **Tests:** add `Game.Editor` to `Tests.EditMode.asmdef` references; unit-test index traversal
  and validator rules with `ScriptableObject.CreateInstance` fixtures (no scenes).

## Implementation Plan

### Tasks

All new code: namespace `Game.Editor.QuestExplorer`, folder `Assets/_Game/Editor/QuestExplorer/`
(assembly `Game.Editor` — no `#if UNITY_EDITOR` needed). Every class that logs declares
`private const string TAG = "[QuestExplorer]";` and uses `GameLog.*`. Commit `.meta` files with
their scripts (let Unity generate them — do not hand-write).

**Layer 1 — Reference index (pure data)**

- [x] Task 1: Define index data types
  - File: `Assets/_Game/Editor/QuestExplorer/QuestIndexTypes.cs` (new)
  - Action: Declare:
    - `enum FactLinkKind { Setter, Reader }`
    - `enum FactLinkSource { ScenePersistentID, PrefabPersistentID, ClosedScene, StartDialogueNode,
      ChoiceOption, QuestPart, MemoryUnlock, MemoryInvalidation, PlayerReward, QuestFactTarget }`
    - `sealed class FactLink { Fact Fact; FactLinkKind Kind; FactLinkSource Source;
      UnityEngine.Object Owner /* asset or scene component, may be null for ClosedScene */;
      string ScenePath /* scene setters only */; NPCEntity Npc; NPCMemoryEntrySO Memory;
      string Label /* human readable, e.g. "Guard › Mem_Guard_SpiderOffer › Choice 'I'll do it'" */ }`
    - `enum QuestPartSlot { Start, Step, Completed, Failed }` and
      `readonly struct QuestPartLocation { QuestPartSlot Slot; int StepIndex; int PartIndex; }`
      with `ToString()` → `"Start"`, `"Step 1 › Part 2"` (1-based display), `"Completed #1"`, `"Failed #1"`.
    - `struct KilledFactBinding { KilledFact Fact; Entity Entity; GameObject GameObject; string ScenePath; bool IsPrefab; }`
    - `sealed class IndexSources` holding plain lists: `QuestSO[] Quests`, `NPCEntity[] Npcs`,
      `NPCMemoryEntrySO[] Memories`, `DialogueNode[] DialogueNodes`, `PlayerRewardSO[] Rewards`,
      `QuestFact[] QuestFacts`, `Fact[] AllFacts`, `List<KilledFactBinding> KilledBindings`,
      `List<(KilledFact fact, string scenePath)> ClosedSceneRefs`,
      `HashSet<QuestSO> EventsManagerQuests`, `HashSet<QuestSO> QuestLogQuests`.
  - Notes: `IndexSources` exists so tests can build the index without AssetDatabase or scenes.

- [x] Task 2: Implement `QuestReferenceIndex`
  - File: `Assets/_Game/Editor/QuestExplorer/QuestReferenceIndex.cs` (new)
  - Action: `public static QuestReferenceIndex Build(IndexSources s)` producing:
    - `IReadOnlyList<FactLink> GetSetters(Fact)`, `GetReaders(Fact)` (empty list, never null).
    - `IEnumerable<(QuestPartLocation loc, QuestPart part)> GetParts(QuestSO)` in display order
      (start, steps/parts, completed, failed).
    - `bool IsInEventsManager(QuestSO)`, `bool IsInQuestLog(QuestSO)`.
    - `IReadOnlyList<QuestFact> GetQuestFactsTargeting(QuestSO)`.
    - `(NPCEntity npc, NPCMemoryEntrySO memory)? GetDialogueOwner(DialogueNode)`.
    - `IReadOnlyList<GameObject> GetSceneObjectsForNpc(NPCEntity)` (from bindings whose `Entity == npc`, non-prefab, GameObject != null).
    - `IReadOnlyList<QuestSO> Quests`, `IReadOnlyList<Fact> AllFacts`.
  - Build algorithm:
    1. **Dialogue ownership:** for each npc → each memory in `npc.memories` (skip null) →
       `memory.effects?.startdialog` root → iterative DFS with `HashSet<DialogueNode>` visited over
       `nextNode`, `ChoiceDialogueNode.choices[].nextNode`, `TeachChoiceDialogueNode.choices[].nextNode`
       / `.confirmNextNode` / `.denyNextNode`. First owner wins; a node reached from several
       memories keeps the first and is still attributed (do not duplicate).
    2. **Dialogue setters:** for every node in `DialogueNodes`: `StartDialogueNode.dialogueFact` →
       Setter (`StartDialogueNode`, Owner = node); each `ChoiceOption` / `TeachChoiceOption` with
       `dialogueFact` → Setter (`ChoiceOption`, Owner = node, Label includes choice text). Attach
       owner npc/memory from step 1 (null if unreachable).
    3. **Killed setters:** each `KilledBindings` entry → Setter (`ScenePersistentID` or
       `PrefabPersistentID`); each `ClosedSceneRefs` entry → Setter (`ClosedScene`, Owner null).
    4. **Readers:** quest parts (`QuestPart` source, Owner = quest, Label = `"{quest.title} › {loc}"`),
       memory `unlockConditions` / `invalidationConditions` (Owner = memory, Npc = owning NPC from
       `npc.memories` reverse map), rewards (only the field matching `FactType`), and for each
       `QuestFact` with `Quest != null` → Reader on the QuestFact itself with source
       `QuestFactTarget` (records which quest a computed fact reads).
    5. Null facts in parts are skipped from link maps (validator reports them).
  - Notes: Pure C#; no `AssetDatabase`, no `SerializedObject`. Uses public fields/getters only
    (`QuestFact.Quest`, `PlayerRewardSO.KilledFact` etc. are public).

- [x] Task 3: Implement the source collector (editor I/O)
  - File: `Assets/_Game/Editor/QuestExplorer/QuestIndexCollector.cs` (new)
  - Action: `public static IndexSources Collect()`:
    - `AssetDatabase.FindAssets("t:QuestSO" / "t:NPCEntity" / "t:NPCMemoryEntrySO" /
      "t:DialogueNode" / "t:PlayerRewardSO" / "t:Fact")` → load, drop nulls. `QuestFacts` =
      `AllFacts.OfType<QuestFact>()`.
    - Loaded scenes: for each `SceneManager.GetSceneAt(i)` that `isLoaded`, collect
      `PersistentID` via `scene.GetRootGameObjects()` → `GetComponentsInChildren<PersistentID>(true)`;
      read `_killedFact` with `new SerializedObject(pid).FindProperty("_killedFact")`; Entity via
      `pid.Entity`. Skip null facts (still list them? No — `GenerateGenericKilledFacts` handles missing).
    - Prefabs: `AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/_Game/Prefabs" })` → load →
      `GetComponentsInChildren<PersistentID>(true)` → bindings with `IsPrefab = true`, ScenePath = prefab path.
    - Closed scenes: `AssetDatabase.FindAssets("t:Scene", new[] { "Assets/_Game/Scenes" })`, skip
      paths of loaded scenes; `File.ReadAllText`; regex `guid: ([0-9a-f]{32})` → for each match whose
      GUID maps (via `AssetDatabase.AssetPathToGUID` dictionary built from `AllFacts`) to a
      `KilledFact` → add `(fact, scenePath)` once per fact per scene. Wrap I/O in try/catch →
      `GameLog.Warn`.
    - Registration: `AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Game/Prefabs/QuestEventsManager.prefab")`
      → `GetComponent<QuestEventsManager>()` → `SerializedObject.FindProperty("_quests")` array →
      set; same for `"Assets/_Game/Prefabs/UI/QuestLog/QuestLogUI.prefab"` / `QuestLogUI` / `"_allQuests"`.
      Missing prefab → `GameLog.Warn` once, empty set.
    - Also: `public static QuestReferenceIndex BuildIndex() => QuestReferenceIndex.Build(Collect());`
  - Notes: Paths as `private const string`. In play mode, loaded-scene `PersistentID`s are the
    play-mode instances (needed for live highlight) — rebuild on play-mode transitions (Task 7).

**Layer 2 — Validation (pure rules)**

- [x] Task 4: Implement `QuestValidator`
  - File: `Assets/_Game/Editor/QuestExplorer/QuestValidator.cs` (new)
  - Action: `enum IssueSeverity { Error, Warning, Info }`;
    `sealed class ValidationIssue { IssueSeverity Severity; string Message; UnityEngine.Object Context; QuestPartLocation? Location; }`;
    `public static List<ValidationIssue> Validate(QuestSO quest, QuestReferenceIndex index, IReadOnlyList<QuestSO> allQuests)`.
    Rules:
    | # | Severity | Rule |
    |---|---|---|
    | V1 | Error | `startPart.fact == null` — "Quest can never start" |
    | V2 | Error | any step/completed/failed part with `fact == null` |
    | V3 | Error | part fact is `KilledFact` / `DialogueFact` / `WorldFact` with **zero setters** — "'{fact.name}' is never set — {loc} can't complete". Message for WorldFact adds "(no WorldFact setter exists in code yet)" |
    | V4 | Warning | `DialogueFact` setters exist but **none** has an owning NPC — "set only by dialogue not reachable from any NPC memory" |
    | V5 | Warning | `KilledFact` with >1 setter of source `ScenePersistentID`/`ClosedScene`, or any `PrefabPersistentID` setter — "shared by several entities (duplicate GUID)" |
    | V6 | Warning | step with `parts` null/empty — "step can never complete" |
    | V7 | Info | `completedParts` empty — "quest never completes" |
    | V8 | Warning | empty `entry` on `startPart` or `completedParts`/`failedParts` items (shown in quest log); Info for empty step-part entries |
    | V9 | Warning | not in `QuestEventsManager` (with fix hint "Sync") / not in `QuestLogUI` |
    | V10 | Error | `questId` empty; Error if another quest in `allQuests` has the same `questId`; Warning if asset name ≠ `Quest_{questId}` |
    | V11 | Error | a `QuestFact` targeting this quest with `IsStepState` and `QuestStepIndex >= steps.Count` |
    | V12 | Warning | a part uses a `QuestFact` whose `Quest == quest` and refers to the slot it sits in or a later one (self-dependency, e.g. step 0 requiring "step 0 completed") |
    | V13 | Info | description contains a count (digits 2–20 or words "two".."ten", case-insensitive, whole word) and **no** step has that many parts — "description says {n} but step part counts are {list}" |
  - Notes: Messages are user-facing; keep under ~120 chars. Results sorted Error → Warning → Info.

**Layer 3 — Window**

- [x] Task 5: Window shell, quest list and fact mode
  - File: `Assets/_Game/Editor/QuestExplorer/QuestExplorerWindow.cs` (new)
  - Action: `public class QuestExplorerWindow : EditorWindow`,
    `[MenuItem("Tools/Quests/Quest Explorer")]` → `GetWindow<QuestExplorerWindow>("Quest Explorer")`.
    `CreateGUI()` builds with code (no UXML/USS files; inline styles like `HitboxTunerWindow`):
    - Toolbar (`UnityEditor.UIElements.Toolbar`): mode `ToolbarToggle`s **Quests | Facts**,
      `ToolbarSearchField` (filters left list by name/title/questId), **Refresh** button,
      **Highlight in Scene** toggle (Task 8), issue summary label ("2 errors · 1 warning").
    - `TwoPaneSplitView` (fixed pane 220 px): left `ListView` (quests sorted by title, each row
      shows title + severity dot from validator; in play mode a state tag: Not started / Active /
      Completed / Failed); right `ScrollView` detail pane.
    - Facts mode: left list = all facts grouped by type (header rows per type) with setter/reader
      counts "S:1 R:2"; right pane = fact detail (Task 6 fact view).
    - Public static entry points: `ShowQuest(QuestSO)`, `ShowFact(Fact)` (open window, switch mode,
      select item).
    - Context menu items: `[MenuItem("CONTEXT/QuestSO/Open in Quest Explorer")]`,
      `[MenuItem("CONTEXT/Fact/Show in Quest Explorer")]`,
      `[MenuItem("CONTEXT/PersistentID/Show KilledFact in Quest Explorer")]` (reads `_killedFact`
      via SerializedObject; logs Warn if null).
    - Persist selected quest/fact via `[SerializeField]` GUID strings so selection survives domain reload.
    - Index lifecycle: `_index` built lazily in `CreateGUI`; `_indexDirty` set on
      `EditorApplication.projectChanged`, `EditorApplication.hierarchyChanged`,
      `EditorSceneManager.sceneOpened` / `sceneClosed`; when dirty, schedule one rebuild via
      `EditorApplication.delayCall` (guard against double scheduling) then refresh UI preserving
      selection. Subscribe in `OnEnable`, unsubscribe in `OnDisable`.

- [x] Task 6: Quest detail and fact detail views
  - File: `Assets/_Game/Editor/QuestExplorer/QuestDetailView.cs` (new, `VisualElement` subclass(es) used by the window)
  - Action:
    - **Header:** title, `questId`, asset `ObjectField` (read-only, ping on click), registration
      chips "EventsManager ✓/✗", "QuestLog ✓/✗" (✗ chip has a **Fix** button → Task 10),
      description (read-only label; editable via inline edit Task 9).
    - **Issues box:** validator results (`HelpBox` per issue, Error/Warning/Info type); clicking an
      issue with `Location` scrolls to and flashes that part row; with `Context` pings it.
    - **Sections** (Foldouts): Start, Step N "{title}" (one per step, shows description), Completed,
      Failed. Each **part row**: fact `ObjectField` (type `Fact`), fact type badge
      (Killed/Dialogue/World/Quest/Skill/Stat), entry text, live-state badge (Task 7), and an
      expandable **links** foldout listing setters then readers.
    - **Link row** (shared by quest and fact views): icon by source, `Label`, NPC name if any, and buttons:
      - **Ping** (`EditorGUIUtility.PingObject(Owner)`) — assets and scene objects.
      - **Select** (`Selection.activeObject = Owner` or its GameObject).
      - **Frame** (scene objects only): select then `SceneView.lastActiveSceneView?.FrameSelected()`.
      - **Open scene & select** (`ClosedScene` only): `EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()`
        → if true `EditorSceneManager.OpenScene(path, OpenSceneMode.Additive)` → rebuild index →
        find the binding for the same fact in that scene → select + frame. Disabled in play mode.
      - Readers that are other quests → **Show** button (navigates explorer to that quest).
    - **Setter summary line** on each part row when collapsed: e.g. "set by Monster_DarknessSpider (1)
      [StartingTown]" or red "never set"; for DialogueFact "set by Guard › choice 'I'll do it'".
    - **NPC context:** for DialogueFact setters with an NPC, also list that NPC's scene GameObjects
      (`GetSceneObjectsForNpc`) as Frame-able sub-rows.
    - **Fact view (Facts mode / ShowFact):** fact asset field, type, key (`fact.ToString()`),
      computed/stored badge, live value (Task 7), Setters list, Readers list (same link rows).

**Layer 4 — Play mode**

- [x] Task 7: Live state and fact toggles
  - File: `QuestExplorerWindow.cs`, `QuestDetailView.cs`
  - Action:
    - `EditorApplication.playModeStateChanged`: on `EnteredPlayMode` and `EnteredEditMode` mark
      index dirty (scene instances change) and refresh.
    - While `EditorApplication.isPlaying`, a `rootVisualElement.schedule.Execute(UpdateLive).Every(250)`
      updates: quest state tag (`IsFailed` > `IsCompleted` > `IsStarted`/Active > Not started),
      per-step state (Done = `IsStepCompleted(i)`, Active = `steps[i].IsActive()`, else Pending),
      per-part badge (true green / false grey) via `WorldStateManager.Instance.GetFact(fact)`.
      If `WorldStateManager.Instance == null` show one banner "WorldStateManager not running
      (load Core.unity)" and skip updates. Stop the scheduled item when leaving play mode.
    - Each part row for a stored fact (`KilledFact`, `DialogueFact`, `WorldFact`) gets a **Toggle**
      button in play mode → `WorldStateManager.Instance.SetFact(fact, !current)`. Tooltip:
      "Raw set — fires OnFactChanged, no XP/rewards, does not despawn entities." Computed facts
      (`QuestFact`, `SkillFact`, `StatFact`) show the badge only (no button).
    - Edit mode: badges hidden; no `WorldStateManager` access.

**Layer 5 — Scene view**

- [x] Task 8: Scene highlighter
  - File: `Assets/_Game/Editor/QuestExplorer/QuestSceneHighlighter.cs` (new)
  - Action: Static class with `Enable(QuestReferenceIndex index, QuestSO quest)` / `Disable()`;
    subscribes `SceneView.duringSceneGui` (unsubscribe in Disable; window calls Disable in
    `OnDisable` and when toggle off / no quest selected). For the quest, compute targets:
    - each part's `KilledFact` setters with a live `GameObject` (scene, not prefab);
    - each part's `DialogueFact` setters' NPC → `GetSceneObjectsForNpc`.
    Draw per target (skip null / destroyed): `Handles.DrawWireDisc(pos, Vector3.up, 1f)` +
    `Handles.Label(pos + Vector3.up * 2.2f, "{questId} · {loc} · {fact.name}")` with
    `GUIStyle` (bold, white text, dark background). Color: edit mode cyan; play mode green if
    fact true, yellow if false. Inactive GameObjects (e.g. killed spiders) drawn at transform
    position with 50 % alpha. Call `SceneView.RepaintAll()` on enable/disable and each live tick.
  - Notes: Positions from `transform.position`; one target may carry several labels → stack
    vertically (offset 0.4 per extra label).

**Layer 6 — Editing**

- [x] Task 9: Quest structure editing
  - File: `Assets/_Game/Editor/QuestExplorer/QuestEditActions.cs` (new) + wiring in `QuestDetailView.cs`
  - Action: All edits through `SerializedObject(quest)` + `ApplyModifiedProperties()` (auto-Undo),
    wrapped in `Undo.IncrementCurrentGroup()` / `Undo.SetCurrentGroupName(...)` /
    `Undo.CollapseUndoOperations(group)` when touching several objects. Operations:
    - Set part fact (ObjectField change) and entry text (`TextField` with `isDelayed = true`).
    - Edit title, description, step title/description inline (delayed TextFields).
    - Add part (to a step / completed / failed), remove part (start part: clear only).
    - Add step (appended, title "New step"), remove step, move step up/down.
    - **QuestFact index remap:** removing or moving a step changes step indices. Before applying,
      compute `QuestStepRemap.Compute(int stepCount, StepOp op)` → `Dictionary<int oldIdx, int? newIdx>`
      (pure, static, in `QuestStepRemap.cs`). For every `QuestFact` in
      `index.GetQuestFactsTargeting(quest)` with `IsStepState`: if mapped to a new index, write
      `_questState = newIdx + 3` via `SerializedObject` (Undo-recorded, same group); if mapped to
      null (removed step), leave it and show `EditorUtility.DisplayDialog` listing affected
      QuestFacts **before** removal ("These QuestFacts point to the removed step and will become
      invalid: …" — Cancel aborts). Validator V11 then reports them.
    - After any edit: `EditorUtility.SetDirty`, refresh view, re-run validator (no full index
      rebuild unless facts changed → then mark dirty).
  - File: `Assets/_Game/Editor/QuestExplorer/QuestStepRemap.cs` (new) — `enum StepOpKind { Remove, MoveUp, MoveDown }`, `struct StepOp { StepOpKind Kind; int Index; }`, `static Dictionary<int,int?> Compute(int stepCount, StepOp op)`; out-of-range → identity map.

- [x] Task 10: Fact creation and wiring helpers
  - File: `QuestEditActions.cs` + buttons in `QuestDetailView.cs`
  - Action: Part-row "＋ Fact ▾" `ToolbarMenu` / `GenericMenu` with:
    - **KilledFact from selected GameObject:** requires `Selection.activeGameObject` with a
      `PersistentID` (in a loaded scene, not a prefab asset); else `DisplayDialog` explaining.
      If its `_killedFact` is already set → reuse it. Otherwise create at
      `Assets/_Game/Data/Enemies/{SceneName}/Generic/KilledFact_{SanitizedGOName}.asset`
      (create folders with `AssetDatabase.CreateFolder` chain; if an asset already exists at that
      path, reuse it), `fact.Init(Guid.NewGuid().ToString())`, `AssetDatabase.CreateAsset`,
      `Undo.RegisterCreatedObjectUndo`; assign to `PersistentID._killedFact` via SerializedObject
      (Undo-recorded; works on prefab instances as an override); `EditorSceneManager.MarkSceneDirty`.
      Then assign the fact to the part.
    - **New DialogueFact…** / **New WorldFact…:** small modal `EditorWindow`
      (`QuestExplorerNamePrompt`, in same file or `QuestExplorerNamePrompt.cs`) asking for the
      node id / event key (non-empty, trimmed; invalid path chars replaced by `_`). Create
      `Assets/_Game/Data/Facts/DialogueFact_{id}.asset` / `WorldFact_{key}.asset` via `Init`;
      if the file exists, ask "Reuse existing?" (Yes → load and assign, No → abort). Assign to part.
    - **New QuestFact…:** prompt with quest `ObjectField` (default: current quest) + state popup
      using the same labels as `QuestFactEditor.BuildLabels` (copy the logic — it is private there;
      put a shared `internal static string[] QuestStateLabels(QuestSO)` in `QuestEditActions`).
      Create `Assets/_Game/Data/Facts/QuestFact_{questId}_{LabelSanitized}.asset` via `Init` /
      `InitStep`. Reuse-if-exists as above.
    - **Registration fixes** (header ✗ chips): EventsManager → call existing
      `Game.Editor.QuestEventsManagerAutoSync.SyncQuestsToPrefab()` (public static, `Game`
      assembly). QuestLog → `PrefabUtility.LoadPrefabContents(questLogPrefabPath)` →
      SerializedObject append to `_allQuests` → `PrefabUtility.SaveAsPrefabAsset` →
      `PrefabUtility.UnloadPrefabContents` (try/finally). Then mark index dirty.
  - Notes: After creating any asset: `AssetDatabase.SaveAssets()`; index marked dirty.

**Layer 7 — Documentation (CLAUDE.md)**

- [x] Task 11: Document the fact system
  - File: `Assets/_Game/Data/Facts/CLAUDE.md` (new)
  - Action: Concise reference covering: fact type table (type, key format, stored vs computed,
    who sets it, typical readers — copy the setter table from this spec's Codebase Patterns);
    the reverse KilledFact link (`PersistentID._killedFact` → fact, fact has only a GUID — use
    Quest Explorer "Facts" mode or `CONTEXT/PersistentID` menu to find the entity); DialogueFact
    is set when a Start node plays or a choice is picked, attributed to an NPC via
    memory → startdialog chain; WorldFact has no setter yet; naming/folder conventions
    (`DialogueFact_{NPC}_{Name}`, `QuestFact_{QuestId}_{State}`, KilledFacts under
    `Data/Enemies/{Scene}/Generic/`); `RegisterKill` vs `SetFact`. Max ~80 lines.

- [x] Task 12: Document the tool and fix stale quest docs
  - File: `Assets/_Game/Editor/QuestExplorer/CLAUDE.md` (new)
  - Action: Architecture (Collector → `IndexSources` → Index → Validator → Window/Highlighter/EditActions),
    "How to add a new Fact type / new setter source" checklist (add `FactLinkSource`, collect it in
    `QuestIndexCollector`, map it in `QuestReferenceIndex.Build`, add a validator rule if it can be
    orphaned, add a test), play-mode toggle semantics, menu paths.
  - File: `Assets/_Game/Data/Quests/CLAUDE.md`
  - Action: Fix doc paths `docs/Quests/Quest.md` and `docs/Systems/Quest.md` → `docs/World/Quests/Quest.md`;
    state that `QuestEventsManager` / `QuestLogUI` / `PlayerRewards` are **prefabs**
    (`Prefabs/QuestEventsManager.prefab` auto-synced, `Prefabs/UI/QuestLog/QuestLogUI.prefab`
    manual, `Prefabs/Player/Player.prefab` auto-synced); add "Inspect / validate a quest:
    `Tools/Quests/Quest Explorer`"; note `QuestPart.entry` may be empty on step parts.
  - File: `CLAUDE.md` (root)
  - Action: Add Folder-index rows: `Data/Facts/CLAUDE.md` (Fact types, setters/readers) and
    `Editor/QuestExplorer/CLAUDE.md` (Quest Explorer tool). Add Key File Locations row:
    "Editor-only tools → `Assets/_Game/Editor/` (`Game.Editor` asmdef). `Assets/_Game/Scripts/Editor/`
    compiles into the runtime `Game` assembly and requires `#if UNITY_EDITOR`." Add a HIGH
    checklist row: "Editor script under `Scripts/Editor/` without `#if UNITY_EDITOR` — breaks player builds."

**Layer 8 — Tests**

- [x] Task 13: Wire test assembly
  - File: `Assets/Tests/EditMode/Tests.EditMode.asmdef`
  - Action: Add `"Game.Editor"` to `references`.

- [x] Task 14: Index tests
  - File: `Assets/Tests/EditMode/QuestReferenceIndexTests.cs` (new)
  - Action: NUnit, fixtures built with `ScriptableObject.CreateInstance` + `Init` helpers; destroy
    in `[TearDown]` (`Object.DestroyImmediate`). Private serialized fields that have no `Init`
    (e.g. `NPCMemoryEntrySO` public fields are fine; `PlayerRewardSO` private fields) set via
    `SerializedObject`. Cover AC 1–5 cases (see ACs).

- [x] Task 15: Validator and remap tests
  - File: `Assets/Tests/EditMode/QuestValidatorTests.cs` (new)
  - Action: One test per rule V1–V13 (positive case) plus "clean quest → zero issues"; and
    `QuestStepRemap` tests (remove middle, remove last, move up first = identity, move down).

### Acceptance Criteria

**Index**

- [ ] AC 1: Given a `KilledFact` bound to one scene `PersistentID`, when the index is built, then `GetSetters(fact)` returns exactly one `ScenePersistentID` link whose `Owner` is that component's GameObject/component.
- [ ] AC 2: Given an NPC whose memory's `startdialog` chain is Start → Text → Choice, and the Choice has an option with `dialogueFact`, when the index is built, then that fact has one `ChoiceOption` setter attributed to that NPC and memory.
- [ ] AC 3: Given a dialogue chain with a cycle (`nextNode` pointing back to an earlier node), when the index is built, then the build terminates and each node is attributed once.
- [ ] AC 4: Given a fact used as a quest step part, a memory unlock condition and a reward, when the index is built, then `GetReaders(fact)` returns three links with sources `QuestPart` (label contains "Step 1 › Part 1"), `MemoryUnlock`, `PlayerReward`.
- [ ] AC 5: Given a `KilledFact` referenced only in a closed `.unity` file, when the collector runs, then a `ClosedScene` setter with that scene path is listed, and "Open scene & select" opens the scene and selects the spider.

**Validation**

- [ ] AC 6: Given `Quest_SpiderClear` with the current project data, when it is shown in the explorer, then an Error states `KilledFact_Enemy_DarknessSpider` is never set (Step 1 › Part 1), and an Info notes the description says 5 while step part counts are 2, 1.
- [ ] AC 7: Given `Quest_FindHerbalist`, when shown, then an Error states `WorldFact_herbalist_forest_camp_found` is never set, mentioning no WorldFact setter exists.
- [ ] AC 8: Given a quest missing from `QuestLogUI._allQuests`, when shown, then a "QuestLog ✗" chip and a Warning appear; when **Fix** is clicked, then the prefab list contains the quest and the chip turns ✓.
- [ ] AC 9: Given two quests with the same `questId`, when either is validated, then an Error reports the duplicate.

**Window / navigation**

- [ ] AC 10: Given the window is open on SpiderClear, when the user expands Step 1 › Part 2, then the setter row "Monster_DarknessSpider (1) [StartingTown]" is shown and **Frame** selects it and frames it in the Scene view.
- [ ] AC 11: Given a `KilledFact` selected in the Project window, when the user uses **Show in Quest Explorer**, then the window switches to Facts mode showing its key, setters and the quests/memories reading it.
- [ ] AC 12: Given a spider GameObject with `PersistentID`, when the user uses `CONTEXT/PersistentID/Show KilledFact in Quest Explorer`, then its fact is shown; if `_killedFact` is null a warning is logged and nothing opens.
- [ ] AC 13: Given an asset is created/deleted or the scene hierarchy changes, when the editor next idles, then the index rebuilds once and the current selection is preserved.

**Play mode**

- [ ] AC 14: Given play mode with `Core` + `StartingTown` loaded and the guard quest accepted, when the window shows SpiderClear, then the state tag reads Active, the start part badge is true, and badges update within ~0.5 s of a spider being killed.
- [ ] AC 15: Given play mode, when the user clicks **Toggle** on a `DialogueFact` part, then `WorldStateManager.GetFact` flips and dependent memories/quest state react (OnFactChanged fired); computed facts show no toggle.
- [ ] AC 16: Given play mode without `WorldStateManager`, when the window is open, then a single banner explains it and no exceptions are logged.

**Scene view**

- [ ] AC 17: Given "Highlight in Scene" on and SpiderClear selected, when looking at the Scene view, then the bound spider and the Guard NPC show a ring + label "SpiderClear · …"; in play mode killed (inactive) spiders render faded and green.
- [ ] AC 18: Given the window is closed or the toggle turned off, then no highlight is drawn (handler unsubscribed).

**Editing**

- [ ] AC 19: Given a quest with 3 steps and a QuestFact on step index 2, when the user removes step 1, then the QuestFact now targets index 1 and Ctrl+Z restores both the step and the QuestFact index in one undo.
- [ ] AC 20: Given a QuestFact targets the step being removed, when the user removes it, then a confirmation dialog lists that QuestFact; Cancel leaves everything unchanged.
- [ ] AC 21: Given a scene spider without a KilledFact selected, when the user chooses "KilledFact from selected GameObject" on a part, then a new KilledFact asset with a GUID exists under `Data/Enemies/StartingTown/Generic/`, the spider's `PersistentID` references it, the part references it, and the scene is marked dirty.
- [ ] AC 22: Given "New DialogueFact…" with an id that already exists, when confirmed, then the user is asked to reuse the existing asset and no duplicate file is created.
- [ ] AC 23: Given any edit through the explorer, when the user presses Ctrl+Z, then the quest asset returns to its previous state.

**Docs / build**

- [ ] AC 24: Given the implementation is complete, then `Data/Facts/CLAUDE.md`, `Editor/QuestExplorer/CLAUDE.md` exist, `Data/Quests/CLAUDE.md` doc paths point to `docs/World/Quests/`, and root `CLAUDE.md` indexes both new files.
- [ ] AC 25: Given the project compiles, then no Quest Explorer type is in the `Game` runtime assembly, the console has no errors, and all EditMode tests (existing + new) pass.

## Additional Context

### Dependencies

- No new packages: UI Toolkit, SceneView / Handles and AssetDatabase are built into the editor.
- `Tests.EditMode.asmdef` gains a reference to `Game.Editor`.

### Testing Strategy

**Automated (EditMode, `Tests.EditMode`):**

- `QuestReferenceIndexTests` — pure `IndexSources` fixtures: KilledFact binding setter; dialogue
  ownership through Start → Text → Choice; TeachChoice `confirmNextNode` / `denyNextNode`
  traversal; cycle termination; unreachable dialogue node → setter with null NPC; readers from
  quest parts (location labels), memory unlock / invalidation, rewards (only the field matching
  `FactType`), `QuestFact` → quest; null part facts skipped; registration lookups.
- `QuestValidatorTests` — one positive test per rule V1–V13, a clean quest producing zero issues,
  severity ordering.
- `QuestStepRemapTests` (inside `QuestValidatorTests.cs` or its own fixture) — remove first /
  middle / last, move up at index 0 (identity), move down at last (identity), move down middle.
- Run via Unity Test Runner (or MCP `run_tests` EditMode); all existing tests must stay green.

**Manual (Unity Editor, `StartingTown` + `Core` open):**

1. `Tools/Quests/Quest Explorer` → select **Spider Infestation** → verify AC 6 issues, expand Step 1
   parts, **Frame** `Monster_DarknessSpider (1)` (AC 10).
2. Close `StartingTown`, open only `Core` → Part 2 setter shows "[StartingTown] (closed)" →
   **Open scene & select** (AC 5).
3. Facts mode → `KilledFact_Monster_DarknessSpider` → setter + readers; right-click spider's
   `PersistentID` → Show KilledFact (AC 11–12).
4. Toggle Highlight in Scene → rings/labels on spider + Guard (AC 17); close window → gone (AC 18).
5. Play mode: accept the guard quest, kill the bound spider, watch badges; use Toggle on the turn-in
   DialogueFact and watch the quest complete (AC 14–15). Play with only `StartingTown` → banner (AC 16).
6. On a **duplicate test quest** (copy `Quest_SpiderClear` → `Quest_ExplorerTest`, change
   `questId`): add / remove / reorder steps, Undo (AC 19–20, 23); create a KilledFact from an
   unbound scene object (AC 21 — revert the scene afterwards, or use `TestScene`); create a
   DialogueFact twice with the same id (AC 22). Delete the test quest and generated facts afterwards.
7. Check the console is free of errors and Quest Explorer types live in `Game.Editor` (AC 25).

### Notes

**High-risk items (pre-mortem):**

- **Stale references after scene/play-mode transitions** — scene `PersistentID` links become
  "fake null" when scenes unload or play mode starts. Mitigation: rebuild on transitions; every
  link-row action null-checks `Owner` (Unity `== null`) and shows "object no longer loaded".
- **Index rebuild cost** — `FindAssets` + text scan of scenes each rebuild. Fine at current scale
  (3 scenes, ~100 SOs); debounced to one rebuild per idle. If it grows, cache closed-scene scans
  keyed by file timestamp (future).
- **Struct editing pitfalls** — `QuestPart`/`QuestStep` are structs: only `SerializedProperty` edits
  persist. Direct `quest.steps[i].title = …` silently does nothing / doesn't compile.
- **Step remap vs other references** — only `QuestFact` encodes step indices
  (`_questState >= 3`). `PlayerRewardSO.MatchesQuestStep` goes through a QuestFact, so it follows
  automatically; `QuestEventsManager` runtime state is index-based but not persisted.
- **Open scene & select** may unload the user's working scene — always go through
  `SaveCurrentModifiedScenesIfUserWantsTo` and open **additively**.
- **Raw fact toggle** on a KilledFact doesn't despawn the entity or grant XP — documented in the tooltip.

**Known limitations:**

- WorldFacts have no setter source in code; every WorldFact part is reported as never set until a
  WorldFact trigger component exists (future spec — the index has a `FactLinkSource` slot to add).
- Scenes outside `Assets/_Game/Scenes/` and prefabs outside `Assets/_Game/Prefabs/` aren't scanned.
- Dialogue reachable only through custom/niche node types not listed here is reported as
  unattributed (V4), not as an error.
- No node-graph visualisation; no dialogue editing.

**Future considerations:**

- Graph view (GraphView / Graph Toolkit) reusing `QuestReferenceIndex`.
- Run `QuestValidator` on all quests from a menu / pre-build check / CI test.
- `.claude/commands/quests/*.md` are partly outdated (say "add to QuestEventsManager in the scene"
  — it's an auto-synced prefab; reference `docs/Quests/` instead of `docs/World/Quests/`). Update
  them in a follow-up.

**Existing data issues the tool should surface:**

- Known data issue to be surfaced by validation: `Quest_SpiderClear` step 1 →
  `KilledFact_Enemy_DarknessSpider` (guid `3d29e5de14792a741b06513a2b48e039`) has no setter;
  `StartingTown.unity` spiders reference `KilledFact_Monster_DarknessSpider`,
  `KilledFact_Monster_DarknessSpider (1)` and `KilledFact_Monster_DarknessSpider Variant`.
- Second known data issue: `WorldFact_herbalist_forest_camp_found` (used by `Quest_FindHerbalist`)
  has no setter — no code calls `SetWorldEvent` and no component writes WorldFacts.
- Pre-existing, out of scope: `PlayerRewardSOEditor.cs`, `PlayerRewardsAutoSync.cs`,
  `QuestEventsManagerAutoSync.cs` in `Scripts/Editor/` (runtime `Game` assembly) lack
  `#if UNITY_EDITOR` guards — may break player builds.

## Review Notes

- Adversarial review completed (inline). Findings: 7 total, 4 fixed, 3 skipped (low / noise / undecided).
- Resolution approach: auto-fix.
- Fixed: F1 "KilledFact from selection" no longer reuses an asset already bound to another
  `PersistentID` (unique path instead); F2 live badges skip misconfigured computed facts (no
  per-tick `WorldStateManager` warnings); F3 structural edits schedule one debounced rebuild;
  F4 text edits only rebuild the detail pane when the issue list changes (keeps focus).
- Skipped: F5 V12 flags a Failed part using this quest's own IsCompleted QuestFact; F6
  `QuestFact.OnValidate` clamps orphaned step indices on reload (pre-existing); F7 prefab scan
  is uncached (fine at current scale).
- Deviations: asset creation is not registered with Undo (only the assignment is); added
  `Assets/Show in Quest Explorer`; `hierarchyChanged` ignored during play mode.
