Your task is to implement a quest from a confirmed spec file, creating all required Unity assets, then prove it is
wired correctly with the Quest Explorer report.

## Tools you use

- **`quest_report`** (Unity MCP custom tool; same as `Game.Editor.QuestExplorer.QuestReport.Run(target)` via
  `execute_code` if the tool isn't listed). Targets: `list`, `audit`, a questId / quest asset / title, a fact asset
  name, a memory asset name. Returns Markdown: parts, who sets / reads each fact, involved NPC memories, gated
  choices, validator issues (V1–V17, meanings in `Assets/_Game/Editor/QuestExplorer/CLAUDE.md`).
- **`QuestEditActions`** (`Game.Editor.QuestExplorer`, via `execute_code`) — the same Undo-aware edits the Quest
  Explorer window uses. Prefer them over raw `manage_scriptable_object` edits on `QuestSO` (its parts are structs):
  - Create: `CreateQuest(id, title, description)`, `GetOrCreateDialogueFact(nodeId)`, `GetOrCreateWorldFact(key)`,
    `GetOrCreateQuestFact(quest, QuestState.X)` / `GetOrCreateQuestFact(quest, default, stepIndex)`
  - Quest structure: `AddStep(quest)`, `SetStepTitle/SetStepDescription(quest, i, text)`,
    `AddPart(quest, QuestPartSlot.Step|Completed|Failed, stepIndex)`, `SetPartFact(quest, loc, fact)`,
    `SetPartEntry(quest, loc, text)` with `QuestPartLocation.Start / StepPart(i, j) / Completed(j) / Failed(j)`
  - KilledFact: set `Selection.activeGameObject` to the enemy, then `AssignKilledFactFromSelection(quest, loc)`
  - Memories: `AddMemoryCondition / SetMemoryCondition / RemoveMemoryCondition(memory, MemoryConditionList.Unlock|Invalidation, …)`
  - Registration: `AddToQuestLog(quest)` (QuestEventsManager is auto-synced)
- In `execute_code`, write `UnityEngine.Object` (plain `Object` is ambiguous) and fully qualify game types
  (`Game.Quest.QuestSO`, `Game.Core.DialogueFact`, `Game.NPC.NPCMemoryEntrySO`, …). Note `QuestState` is
  `Game.Core.QuestState` (not `Game.Quest`); `QuestPartLocation`, `QuestPartSlot`, `MemoryConditionList` are in
  `Game.Editor.QuestExplorer`.

## Step 0 — Load context

Read these files before doing anything:
- `docs/World/Quests/QUEST_SPEC_TEMPLATE.md`
- `Assets/_Game/Data/Quests/CLAUDE.md`
- `Assets/_Game/Data/Facts/CLAUDE.md`
- `Assets/_Game/Editor/QuestExplorer/CLAUDE.md`
- `Assets/_Game/Data/NPCs/CLAUDE.md`, `DIALOGUE.md`, `MEMORIES.md` (and `TEACHING.md` if a teaching chain is involved)

---

## Step 1 — Identify the spec

If `$ARGUMENTS` contains a Quest ID, load `docs/World/Quests/{QuestId}.md`.
Otherwise list all `.md` files in `docs/World/Quests/` (excluding `QUEST_SPEC_TEMPLATE.md` and `Quest.md`) and ask:

> "Which quest would you like to implement? Found: [list]"

Verify `status: ready` in the spec. If `status: draft`, warn:

> "This spec is still marked as draft. Some sections may be incomplete. Continue anyway?"

If `status: implemented`, warn:

> "This quest is already marked as implemented. Continue to re-run or update specific parts?"

---

## Step 2 — Audit what already exists

1. Run `quest_report list`, and `quest_report {QuestId}` if the quest already exists.
2. Run `quest_report <FactName>` for each fact listed under **Already exists** — confirm who sets it.
3. Check NPC folders in `Assets/_Game/Data/NPCs/` and rewards in `Assets/_Game/Data/Rewards/`.

Report what exists and what needs to be created. Flag spec problems before building (e.g. a `WorldFact` part — no
setter exists in code, V3). Confirm with the user before proceeding.

---

## Step 3 — Create the QuestSO

`QuestEditActions.CreateQuest(id, title, description)` → `Data/Quests/{QuestId}/Quest_{QuestId}.asset`, already
added to QuestLogUI. Then add the steps (`AddStep`, `SetStepTitle`, `SetStepDescription`) and the parts
(`AddPart`) — empty for now. Check `read_console`.

---

## Step 4 — Create and wire facts

Facts are dependencies for parts, memories and rewards.
- **DialogueFact / WorldFact / QuestFact:** `GetOrCreate…` (folder `Data/Facts/`, naming in `Data/Facts/CLAUDE.md`).
  The DialogueFact node id is the planned `Start_` / choice name.
- **KilledFact:** never create one by hand. Per enemy: select it, `AssignKilledFactFromSelection(quest, loc)` (creates
  or reuses `KilledFact_{GameObject}` and wires its `PersistentID`). For many enemies in the open scene,
  `Game/World/Generate Missing KilledFacts` first, then wire. One fact per entity (V5).
- Wire each part: `SetPartFact` + `SetPartEntry`. Check `read_console`.

---

## Step 5 — NPC scaffold (if needed)

For each NPC in the spec where `exists: false`:

Follow the scaffold procedure from `.claude/commands/NPC/create.md` **Steps 2–4**, using the `identity_notes` from the
spec as inputs in place of the interactive interview. Skip the interview — all answers come from the spec.

For NPCs that already exist, read their `CLAUDE.md` to confirm the folder structure before proceeding.

---

## Step 6 — Dialogue chains

For each memory block with a dialogue in the spec:

Follow the execution procedure from `.claude/commands/NPC/dialogue.md` **Steps 6–8**, using the dialogue script from
the spec in place of user-provided content. Do not re-ask for content — it is already defined.

Work order per chain (leaf nodes first):
1. `TextDialogueNode` assets (NPC speech lines, chained via `nextNode`)
2. `ChoiceDialogueNode` assets (after their branch targets exist), with `dialogueFact` set on any choice that tracks a
   played state, and `requiredMemory` on gated choices (the gate memory is created in Step 7 — come back to set it)
3. `StartDialogueNode` asset, with `dialogueFact` set to the `DialogueFact` for this chain

---

## Step 7 — NPC Memory entries

For each memory block in the spec, create `Mem_{NPC}_{Topic}.asset` (type `Game/NPC/Memory Entry`) in
`Assets/_Game/Data/NPCs/{NPCName}/Memories/` — saving it there auto-adds it to `NPCEntity.memories`
(`NPCMemoriesAutoSync`; menu fallback `Game/Dev/Sync All NPC Memories`). Never edit that list by hand.

Set `effects.startdialog` (none for gate memories), then the conditions with
`QuestEditActions.AddMemoryCondition`. Wire `requiredMemory` on the gated choices from Step 6.

Check `read_console`.

---

## Step 8 — Reward assets

For each reward block in the spec (onStart, onStepCompleted, onCompleted, onFailed):

Create `PlayerReward_{QuestId}_{Trigger}.asset` in `Assets/_Game/Data/Rewards/` (type `Game/Rewards/Player Reward`).

Set (serialized names):
- `_factType` = `Quest`
- `_questFact` → `GetOrCreateQuestFact(quest, QuestState.IsStarted|IsCompleted|IsFailed)` or the step variant
- `_xpReward`, `_lpReward`, `_goldReward`, `_statRewards`

`PlayerRewards._rewards` on `Prefabs/Player/Player.prefab` is auto-synced (`Game/Dev/Sync Player Rewards to Prefab`).

---

## Step 9 — Verify

1. `read_console` — no errors.
2. `quest_report {QuestId}` — read it fully:
   - every stored part fact has a setter (no `NEVER SET`), each memory's reasons and gated choices match the spec;
   - **no Error or Warning**. Fix each one, or explain it to the user (e.g. a WorldFact waiting for a setter).
3. `quest_report audit` — no new issues on other quests or NPC memories.

---

## Step 10 — Update the spec

List every asset created with its path.

Update `docs/World/Quests/{QuestId}.md`:
- Fill in the **Implementation Checklist** with asset paths
- Set `status: implemented`
