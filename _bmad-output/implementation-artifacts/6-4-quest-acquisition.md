# Story 6.4: Quest Acquisition

Status: review

## Story

As a player,
I want to receive quests through NPC dialogue,
so that I can undertake missions and track them in the world.

## Acceptance Criteria

1. **AC1 — QuestSO data type exists:** A `QuestSO` ScriptableObject exists with `questId`, `title`, and `description` fields. Authors create quest assets via `Assets/Create > Game/Quest/Quest`.
2. **AC2 — QuestDialogueNode triggers quest start:** A `QuestDialogueNode` (new node type) exists with `quest` (QuestSO) and `questStartFact` (QuestFact) fields. When traversed by `DialogueSystem.AdvanceToNode()`, it calls `WorldStateManager.Instance.SetQuestStep(questStartFact, true)` and immediately advances to `nextNode` without displaying text.
3. **AC3 — DialogueSystem handles QuestDialogueNode:** `DialogueSystem.AdvanceToNode()` includes a `case QuestDialogueNode questNode:` branch that sets the fact and advances the chain transparently.
4. **AC4 — WorldStateManager.GetFact returns true after quest accepted:** After the player accepts the demo quest via dialogue, `WorldStateManager.Instance.GetFact(QuestFact_FindHerbalist_Started)` returns `true`.
5. **AC5 — Demo quest offered by Elder NPC in StartingTown:** The Elder NPC exists in `StartingTown.unity` and offers the "Find the Herbalist" quest through dialogue. Dialogue chain: topic "I need your help" → ChoiceNode (Accept / Not right now) → Accept path: QuestDialogueNode sets fact → confirm text → Farewell; Decline path: decline text → Farewell.
6. **AC6 — No crash on null questStartFact:** `QuestDialogueNode` with no `questStartFact` assigned logs a warning and still advances `nextNode` — no NullReferenceException.
7. **AC7 — Edit Mode tests pass:** `QuestAcquisitionTests` covers `QuestFact` key format and `QuestSO` data storage.

## Tasks / Subtasks

- [x] Task 1 — Create `QuestSO` ScriptableObject (AC: #1)
  - [x] File: `Assets/_Game/ScriptableObjects/Quest/QuestSO.cs`, namespace `Game.Quest`
  - [x] Fields: `public string questId;`, `public string title;`, `[TextArea] public string description;`
  - [x] `[CreateAssetMenu(menuName = "Game/Quest/Quest", fileName = "Quest_")]`
  - [x] See exact code in Dev Notes

- [x] Task 2 — Create `QuestDialogueNode` ScriptableObject (AC: #2)
  - [x] File: `Assets/_Game/ScriptableObjects/Dialogue/QuestDialogueNode.cs`, namespace `Game.Dialogue`
  - [x] Fields: `public QuestSO quest;`, `public QuestFact questStartFact;`
  - [x] `[CreateAssetMenu(menuName = "Game/Dialogue/Quest Node", fileName = "Quest_")]`
  - [x] Must be a **separate file** from other dialogue node types (SO subclass file separation rule)
  - [x] See exact code in Dev Notes

- [x] Task 3 — Update `DialogueSystem.AdvanceToNode()` (AC: #3, #6)
  - [x] File: `Assets/_Game/Scripts/World/DialogueSystem.cs`
  - [x] Add `using Game.Quest;` to imports
  - [x] Insert `case QuestDialogueNode questNode:` branch **before** the `StartDialogueNode _:` catch-all and **after** the `TeachChoiceDialogueNode` case
  - [x] Branch: null-guard `questStartFact`, call `WorldStateManager.Instance.SetQuestStep()`, then `AdvanceToNode(questNode.nextNode)`
  - [x] See exact code diff in Dev Notes

- [x] Task 4 — Add Edit Mode tests (AC: #7)
  - [x] File: `Assets/Tests/EditMode/QuestAcquisitionTests.cs`
  - [x] Tests: `QuestFact_ToString_ReturnsCorrectFormat`, `QuestSO_StoresFields_Correctly`, `SetQuestStep_StartedFact_StoredInWSM`
  - [x] Use the WorldStateManager reflection pattern from `WorldStateManagerFactsTests.cs` for WSM tests
  - [x] See exact code in Dev Notes

- [x] Task 5 — Create demo quest data assets (Editor work) (AC: #1, #4, #5)
  - [x] Create folders:
    - `Assets/_Game/Data/Quests/` (new folder)
    - `Assets/_Game/Data/Facts/Quests/` (new folder)
    - `Assets/_Game/Data/NPCs/Elder/` (new folder)
    - `Assets/_Game/Data/NPCs/Elder/Dialogues/` (new folder)
    - `Assets/_Game/Data/NPCs/Elder/Dialogues/FindHerbalist/` (new folder)
    - `Assets/_Game/Data/NPCs/Elder/Memories/` (new folder)
  - [x] Create `Quest_FindHerbalist.asset` in `Assets/_Game/Data/Quests/`:
    - `questId = "FindHerbalist"`, `title = "Find the Herbalist"`, `description = "The village herbalist Mira has gone missing. Search the forest east of town and bring her back safely."`
  - [x] Create `QuestFact_FindHerbalist_Started.asset` in `Assets/_Game/Data/Facts/Quests/`:
    - `_questId = "FindHerbalist"`, `_stepKey = "started"`
  - [x] Create dialogue assets in `Assets/_Game/Data/NPCs/Elder/Dialogues/FindHerbalist/`:

    | Asset | Type | Fields |
    |-------|------|--------|
    | `Start_Elder_FindHerbalist.asset` | StartDialogueNode | `text = "I need your help"`, `nextNode = Choice_Elder_HerbalistOffer`, `dialogueFact = null` |
    | `Choice_Elder_HerbalistOffer.asset` | ChoiceDialogueNode | `text = "Our herbalist Mira has gone missing. The village is running low on medicine. Will you look for her?"`, see choices below |
    | `Quest_Elder_FindHerbalist.asset` | QuestDialogueNode | `quest = Quest_FindHerbalist`, `questStartFact = QuestFact_FindHerbalist_Started`, `nextNode = Text_Elder_AcceptConfirm` |
    | `Text_Elder_AcceptConfirm.asset` | TextDialogueNode | `text = "Thank you. She was last seen heading east into the forest. Please hurry."`, `nextNode = null` |
    | `Text_Elder_Decline.asset` | TextDialogueNode | `text = "I understand. Come find me if you change your mind."`, `nextNode = null` |

  - [x] `Choice_Elder_HerbalistOffer` choices:
    - `choices[0]`: `text = "I'll look for her."`, `requiredMemory = null`, `nextNode = Quest_Elder_FindHerbalist.asset`, `dialogueFact = null`
    - `choices[1]`: `text = "Not right now."`, `requiredMemory = null`, `nextNode = Text_Elder_Decline.asset`, `dialogueFact = null`
  - [x] Create `Mem_Elder_HerbalistQuest.asset` in `Assets/_Game/Data/NPCs/Elder/Memories/`:
    - `unlockConditions = []` (empty — always active)
    - `invalidationConditions = []` (empty — never closed)
    - `effects.startdialog = Start_Elder_FindHerbalist.asset`
  - [x] Create or update `NPC_Elder.asset` in `Assets/_Game/Data/NPCs/Elder/`:
    - `npcName = "Elder Aldric"`, `memories = [Mem_Elder_HerbalistQuest.asset]`
    - Root `Assets/_Game/Data/NPCs/NPC_Elder.asset` had existing data → created new asset at `Assets/_Game/Data/NPCs/Elder/NPC_Elder.asset`

- [x] Task 6 — Add Elder NPC to StartingTown scene (Editor work) (AC: #5)
  - [x] Open `Assets/_Game/Scenes/StartingTown.unity`
  - [x] `StartingTown_NPC_Elder` already existed at position [0, 1.43, 12] (near Building_ElderHall); kept existing position
  - [x] Add components:
    - `PersistentID` — already present with `KilledFact_StartingTown_NPC_Elder.asset`
    - `NPCPresence` — `_data` updated to `NPC_Elder.asset` (Elder subfolder), `_onDialogueRequested` already assigned
    - `NPCMemoryComponent` — `_data` updated to `NPC_Elder.asset` (Elder subfolder), `_onWorldFactChanged` already assigned
    - `NPCDialogueGraphComponent` — added
    - Layer = 8 (Interactable), `CapsuleCollider` already present
  - [x] Do NOT add NPC to the player's `InteractionSystem` — it auto-detects Layer 8 colliders

## Dev Notes

### Architecture: QuestDialogueNode is a Transparent Side-Effect Node

`QuestDialogueNode` does NOT display text — it is inserted into a dialogue chain as a pure side effect:
1. `DialogueSystem.AdvanceToNode(questNode)` fires → sets `Quest.FindHerbalist.started = true` in WorldStateManager
2. Immediately calls `AdvanceToNode(questNode.nextNode)` to continue the chain
3. `DialogueUI` never gets a render call for this node

**Why:** Separating the fact-recording action from the display text keeps each node's role clean. The choice node shows "Will you help?", the QuestDialogueNode records the acceptance, and a TextDialogueNode shows "Thank you." This composable design avoids a new UI state while keeping authoring expressive.

**Consequence:** `QuestDialogueNode.text` (inherited from `DialogueNode`) is ignored at runtime. Authors may use it as a label/note for editor clarity, but it is never shown to the player.

### Exact Code — Task 1: `QuestSO.cs`

```csharp
using UnityEngine;

namespace Game.Quest
{
    [CreateAssetMenu(menuName = "Game/Quest/Quest", fileName = "Quest_")]
    public class QuestSO : ScriptableObject
    {
        [Tooltip("Unique quest identifier. Used as key in QuestFact. E.g. 'FindHerbalist'.")]
        public string questId;

        [Tooltip("Display title shown in Quest Log (Story 6-5).")]
        public string title;

        [Tooltip("Full quest description shown in Quest Log (Story 6-5).")]
        [TextArea(3, 6)]
        public string description;
    }
}
```

New folder: `Assets/_Game/ScriptableObjects/Quest/` — matches `_Game/ScriptableObjects/` subfolder convention.

### Exact Code — Task 2: `QuestDialogueNode.cs`

```csharp
using Game.Core;
using Game.Quest;
using UnityEngine;

namespace Game.Dialogue
{
    /// <summary>
    /// Transparent dialogue node that records quest acceptance in WorldStateManager.
    /// Does not display text — immediately advances to nextNode after setting the quest fact.
    /// Author note: the 'text' field (inherited) is ignored at runtime; use it as an editor label.
    /// </summary>
    [CreateAssetMenu(menuName = "Game/Dialogue/Quest Node", fileName = "Quest_")]
    public class QuestDialogueNode : DialogueNode
    {
        [Tooltip("The quest being accepted. Referenced by Quest Log (Story 6-5) for title and description.")]
        public QuestSO quest;

        [Tooltip("QuestFact to set true in WorldStateManager when this node is traversed.")]
        public QuestFact questStartFact;
    }
}
```

**Critical:** Must be in its own `.cs` file. Unity's `m_Script` reference breaks on domain reload if SO subclasses share a file (project memory rule).

### Exact Code — Task 3: DialogueSystem diff

Add `using Game.Quest;` to the imports block (after `using Game.Progression;`).

In `AdvanceToNode(DialogueNode node)`, insert the `QuestDialogueNode` case **after** the `TeachChoiceDialogueNode` case and **before** the `StartDialogueNode _:` case:

```csharp
case QuestDialogueNode questNode:
    if (questNode.questStartFact != null && WorldStateManager.Instance != null)
    {
        WorldStateManager.Instance.SetQuestStep(questNode.questStartFact, true);
        string questName = questNode.quest != null ? questNode.quest.title : questNode.name;
        GameLog.Info(TAG, $"Quest started: '{questName}' — fact set: {questNode.questStartFact}");
    }
    else
    {
        GameLog.Warn(TAG, $"QuestDialogueNode '{questNode.name}' has no questStartFact or WorldStateManager is null — no quest recorded");
    }
    AdvanceToNode(questNode.nextNode); // transparent: chain continues immediately
    break;
```

Full context around insertion point:
```csharp
// EXISTING:
case TeachChoiceDialogueNode teachNode:
    // ... existing code ...
    break;

// INSERT HERE:
case QuestDialogueNode questNode:
    // ... see above ...
    break;

// EXISTING:
case StartDialogueNode _:
    GameLog.Warn(TAG, ...);
    Close();
    break;
```

### Exact Code — Task 4: `QuestAcquisitionTests.cs`

```csharp
using System.Collections.Generic;
using System.Reflection;
using Game.Core;
using Game.Quest;
using NUnit.Framework;
using UnityEngine;

namespace Tests.EditMode
{
    /// <summary>
    /// Edit Mode tests for Story 6.4 — Quest Acquisition.
    /// Covers QuestFact key format, QuestSO data storage, and WorldStateManager integration.
    /// Uses the same WSM reflection pattern as WorldStateManagerFactsTests.cs.
    /// </summary>
    public class QuestAcquisitionTests
    {
        private WorldStateManager _wsm;
        private readonly List<Object> _cleanup = new List<Object>();

        private static readonly FieldInfo s_instanceField =
            typeof(WorldStateManager).GetField(
                "<Instance>k__BackingField",
                BindingFlags.Static | BindingFlags.NonPublic);

        [SetUp]
        public void SetUp()
        {
            var go = new GameObject("WorldStateManager_Test");
            _wsm = go.AddComponent<WorldStateManager>();
            _cleanup.Add(go);
            s_instanceField.SetValue(null, _wsm);
        }

        [TearDown]
        public void TearDown()
        {
            s_instanceField.SetValue(null, null);
            foreach (var obj in _cleanup)
            {
                if (obj != null) Object.DestroyImmediate(obj);
            }
            _cleanup.Clear();
        }

        private T MakeFact<T>(System.Func<T> factory) where T : Object
        {
            var f = factory();
            _cleanup.Add(f);
            return f;
        }

        // ── QuestFact key format ──────────────────────────────────────────────

        [Test]
        public void QuestFact_ToString_ReturnsCorrectFormat()
        {
            var fact = ScriptableObject.CreateInstance<QuestFact>();
            _cleanup.Add(fact);
            fact.Init("FindHerbalist", "started");
            Assert.That(fact.ToString(), Is.EqualTo("Quest.FindHerbalist.started"));
        }

        [Test]
        public void QuestFact_DifferentQuestAndStep_ProduceDistinctKeys()
        {
            var factA = ScriptableObject.CreateInstance<QuestFact>();
            _cleanup.Add(factA);
            factA.Init("FindHerbalist", "started");

            var factB = ScriptableObject.CreateInstance<QuestFact>();
            _cleanup.Add(factB);
            factB.Init("FindHerbalist", "completed");

            Assert.That(factA.ToString(), Is.Not.EqualTo(factB.ToString()));
        }

        // ── QuestSO data ──────────────────────────────────────────────────────

        [Test]
        public void QuestSO_StoresFields_Correctly()
        {
            var quest = ScriptableObject.CreateInstance<QuestSO>();
            _cleanup.Add(quest);
            quest.questId = "FindHerbalist";
            quest.title = "Find the Herbalist";
            quest.description = "The village herbalist is missing.";

            Assert.That(quest.questId, Is.EqualTo("FindHerbalist"));
            Assert.That(quest.title, Is.EqualTo("Find the Herbalist"));
            Assert.That(quest.description, Is.EqualTo("The village herbalist is missing."));
        }

        // ── WorldStateManager integration ──────────────────────────────────────

        [Test]
        public void SetQuestStep_StartedFact_IsRetrievable()
        {
            var fact = MakeFact(() => ScriptableObject.CreateInstance<QuestFact>().Init("FindHerbalist", "started"));
            _wsm.SetQuestStep(fact, true);
            Assert.That(_wsm.GetFact(MakeFact(() => ScriptableObject.CreateInstance<QuestFact>().Init("FindHerbalist", "started"))), Is.True);
        }

        [Test]
        public void SetQuestStep_NotStarted_ReturnsFalse()
        {
            // Quest not yet accepted — fact should be false
            Assert.That(_wsm.GetFact(MakeFact(() => ScriptableObject.CreateInstance<QuestFact>().Init("FindHerbalist", "started"))), Is.False);
        }

        [Test]
        public void SetQuestStep_NullFact_DoesNotThrow()
        {
            Assert.DoesNotThrow(() => _wsm.SetQuestStep(null, true));
        }
    }
}
```

### Dialogue Chain Diagram — Demo Quest

```
[NPCMemory: Mem_Elder_HerbalistQuest]
  unlockConditions: [] → always active
  effects.startdialog → Start_Elder_FindHerbalist

[StartDialogueNode: Start_Elder_FindHerbalist]
  text: "I need your help"
  nextNode → Choice_Elder_HerbalistOffer

[ChoiceDialogueNode: Choice_Elder_HerbalistOffer]
  text: "Our herbalist Mira has gone missing. The village needs medicine. Will you look for her?"
  choices[0]: "I'll look for her." → Quest_Elder_FindHerbalist
  choices[1]: "Not right now."    → Text_Elder_Decline

[QuestDialogueNode: Quest_Elder_FindHerbalist]  ← TRANSPARENT (no UI)
  quest: Quest_FindHerbalist
  questStartFact: QuestFact_FindHerbalist_Started  ← sets Quest.FindHerbalist.started = true
  nextNode → Text_Elder_AcceptConfirm

[TextDialogueNode: Text_Elder_AcceptConfirm]
  text: "Thank you. She was last seen heading east into the forest. Please hurry."
  nextNode: null  → Farewell

[TextDialogueNode: Text_Elder_Decline]
  text: "I understand. Come find me if you change your mind."
  nextNode: null  → Farewell
```

### Critical Patterns from Codebase

**`AdvanceToNode()` recursive call for transparent node:**
The `QuestDialogueNode` case calls `AdvanceToNode(questNode.nextNode)` at the end of its branch. This is safe because:
- Project spec guarantees DAG (no cycles) — transparent re-entry can't loop
- Recursive depth is bounded by the dialogue chain length (typically 2–5 nodes)
- `null` nextNode is already handled at the top of `AdvanceToNode()` (calls `Close()`)

**Namespace layout:**
- `QuestSO` → `Game.Quest` (new namespace, new SO folder)
- `QuestDialogueNode` → `Game.Dialogue` (same namespace as other dialogue nodes)
- `DialogueSystem` already imports `Game.Core` (for `WorldStateManager`) — add `using Game.Quest;` for `QuestSO` reference resolution

**Event assets to assign on Elder NPC:**
- `_onDialogueRequested` → `Assets/_Game/Data/Events/OnNPCDialogueRequested.asset`
- `_onWorldFactChanged` → `Assets/_Game/Data/Events/OnWorldFactChanged.asset`

**NPCDataSO memories list:**
`NPCDataSO.memories` is a `List<NPCMemoryEntrySO>`. Add `Mem_Elder_HerbalistQuest` to Elder's memories list via Inspector.

**PersistentID GUID convention:**
Format is `Region_Type_Name` → `StartingTown_NPC_Elder` (matches project rule).

**Layer 8 = Interactable:**
The `InteractionSystem` raycasts against Layer 8 only. The Elder's collider must be on Layer 8. Verify via Project Settings > Physics > Layer Collision Matrix.

**No QuestLog UI in this story:**
Story 6-5 adds the Quest Log panel and `J` key binding. Story 6-4 only establishes the data structures and acquisition mechanism. The quest fact being set in WorldStateManager IS the only observable output besides the dialogue text.

### Project Structure Notes

**New files (code):**

| File | Action |
|------|--------|
| `Assets/_Game/ScriptableObjects/Quest/QuestSO.cs` | Create |
| `Assets/_Game/ScriptableObjects/Dialogue/QuestDialogueNode.cs` | Create |
| `Assets/_Game/Scripts/World/DialogueSystem.cs` | Modify — add `case QuestDialogueNode` |
| `Assets/Tests/EditMode/QuestAcquisitionTests.cs` | Create |

**New assets (Editor-created):**

| Asset | Folder |
|-------|--------|
| `Quest_FindHerbalist.asset` | `Assets/_Game/Data/Quests/` |
| `QuestFact_FindHerbalist_Started.asset` | `Assets/_Game/Data/Facts/Quests/` |
| `NPC_Elder.asset` | `Assets/_Game/Data/NPCs/Elder/` |
| `Mem_Elder_HerbalistQuest.asset` | `Assets/_Game/Data/NPCs/Elder/Memories/` |
| `Start_Elder_FindHerbalist.asset` | `Assets/_Game/Data/NPCs/Elder/Dialogues/FindHerbalist/` |
| `Choice_Elder_HerbalistOffer.asset` | `Assets/_Game/Data/NPCs/Elder/Dialogues/FindHerbalist/` |
| `Quest_Elder_FindHerbalist.asset` | `Assets/_Game/Data/NPCs/Elder/Dialogues/FindHerbalist/` |
| `Text_Elder_AcceptConfirm.asset` | `Assets/_Game/Data/NPCs/Elder/Dialogues/FindHerbalist/` |
| `Text_Elder_Decline.asset` | `Assets/_Game/Data/NPCs/Elder/Dialogues/FindHerbalist/` |

**Scene change:** Add `StartingTown_NPC_Elder` GameObject to `Assets/_Game/Scenes/StartingTown.unity`.

**No new event channels needed** — existing `OnNPCDialogueRequested.asset` and `OnWorldFactChanged.asset` are reused.

**No new .asmdef changes** — `QuestSO` in `Assets/_Game/` compiles into the `Game` assembly automatically.

**No `_Game/ScriptableObjects/Quest/` .meta quirk** — Unity auto-generates .meta for new folders on next import.

### Testing

Manual playtest in `StartingTown.unity`:

1. Approach Elder Aldric → E to interact → dialogue opens, showing "I need your help" + "Farewell."
2. Click "I need your help" → ChoiceNode text shows + 2 choice buttons + "Farewell."
3. Click "I'll look for her." → QuestDialogueNode fires (no UI change, fact set immediately) → TextDialogueNode "Thank you..." shows + "Farewell." (end node)
4. Click "Farewell." → dialogue closes; cursor locks
5. In Unity Editor: `WorldStateManager.Instance.GetFact(QuestFact_FindHerbalist_Started)` → `true` (verify via Watches or debug breakpoint)
6. Repeat: approach Elder again → topic "I need your help" should still appear (no `dialogueFact` on `Start_Elder_FindHerbalist`) — confirming quest offer is repeatable in this story (non-repeatable gate is a story 6-5+ concern)
7. Test decline path: Click "Not right now." → decline text shown → "Farewell." → no quest fact set

### References

- `QuestFact` type: `Assets/_Game/ScriptableObjects/Facts/QuestFact.cs`
- `WorldStateManager.SetQuestStep()`: `Assets/_Game/Scripts/Core/State/WorldStateManager.cs:71`
- `DialogueSystem.AdvanceToNode()`: `Assets/_Game/Scripts/World/DialogueSystem.cs:74`
- WSM test pattern: `Assets/Tests/EditMode/WorldStateManagerFactsTests.cs`
- Dialogue graph node pattern: `Assets/_Game/ScriptableObjects/Dialogue/` (StartDialogueNode, ChoiceDialogueNode, etc.)
- NPCMemoryComponent: `Assets/_Game/Scripts/AI/NPCMemoryComponent.cs`
- NPCDialogueGraphComponent: `Assets/_Game/Scripts/AI/NPCDialogueGraphComponent.cs`
- Previous story (dialogue integration): `_bmad-output/implementation-artifacts/6-3-dialogue-graph-integration.md`
- Project conventions (57 rules): `_bmad-output/project-context.md`

## Dev Agent Record

### Agent Model Used

claude-sonnet-4-6

### Debug Log References

### Completion Notes List

- Created `QuestSO` (Game.Quest) with `questId`, `title`, `description` fields and CreateAssetMenu.
- Created `QuestDialogueNode` (Game.Dialogue) with `quest` and `questStartFact` fields; transparent node that calls `SetQuestStep` and immediately advances to `nextNode` without displaying text.
- Updated `DialogueSystem.AdvanceToNode()`: added `using Game.Quest;` and inserted `case QuestDialogueNode questNode:` branch with null-guard before the `StartDialogueNode` catch-all.
- Created `QuestAcquisitionTests.cs` with 6 tests covering QuestFact key format, QuestSO data storage, and WSM integration. All 232 EditMode tests pass (zero regressions).
- Created all data assets via Unity MCP: `Quest_FindHerbalist.asset`, `QuestFact_FindHerbalist_Started.asset`, full 5-node dialogue chain for Elder's Find Herbalist quest, `Mem_Elder_HerbalistQuest.asset`, `NPC_Elder.asset` (at Elder subfolder — root NPC_Elder.asset had existing data).
- `StartingTown_NPC_Elder` already existed at [0, 1.43, 12] near Building_ElderHall. Updated `NPCPresence._data` and `NPCMemoryComponent._data` to new Elder-subfolder NPC asset; added `NPCDialogueGraphComponent`. Scene saved.

### File List

**New code files:**
- `Assets/_Game/ScriptableObjects/Quest/QuestSO.cs`
- `Assets/_Game/ScriptableObjects/Dialogue/QuestDialogueNode.cs`
- `Assets/Tests/EditMode/QuestAcquisitionTests.cs`

**Modified code files:**
- `Assets/_Game/Scripts/World/DialogueSystem.cs`

**New asset files:**
- `Assets/_Game/Data/Quests/Quest_FindHerbalist.asset`
- `Assets/_Game/Data/Facts/Quests/QuestFact_FindHerbalist_Started.asset`
- `Assets/_Game/Data/NPCs/Elder/NPC_Elder.asset`
- `Assets/_Game/Data/NPCs/Elder/Memories/Mem_Elder_HerbalistQuest.asset`
- `Assets/_Game/Data/NPCs/Elder/Dialogues/FindHerbalist/Start_Elder_FindHerbalist.asset`
- `Assets/_Game/Data/NPCs/Elder/Dialogues/FindHerbalist/Choice_Elder_HerbalistOffer.asset`
- `Assets/_Game/Data/NPCs/Elder/Dialogues/FindHerbalist/Quest_Elder_FindHerbalist.asset`
- `Assets/_Game/Data/NPCs/Elder/Dialogues/FindHerbalist/Text_Elder_AcceptConfirm.asset`
- `Assets/_Game/Data/NPCs/Elder/Dialogues/FindHerbalist/Text_Elder_Decline.asset`

**Modified scene:**
- `Assets/_Game/Scenes/StartingTown.unity` (Elder NPC updated: new NPC_Elder.asset, NPCDialogueGraphComponent added)

## Change Log

- 2026-04-13: Story 6.4 implemented — QuestSO + QuestDialogueNode types, DialogueSystem quest branch, QuestAcquisitionTests (6 tests), Find Herbalist demo quest assets, Elder NPC wired up in StartingTown. All 232 EditMode tests pass.
