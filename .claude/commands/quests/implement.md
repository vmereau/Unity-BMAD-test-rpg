Your task is to implement a quest from a confirmed spec file, creating all required Unity assets.

## Step 0 — Load context

Read these files before doing anything:
- `docs/Quests/QUEST_SPEC_TEMPLATE.md`
- `Assets/_Game/Data/Quests/CLAUDE.md`
- `Assets/_Game/Data/NPCs/DIALOGUE.md`
- `Assets/_Game/Data/NPCs/MEMORIES.md`

---

## Step 1 — Identify the spec

If `$ARGUMENTS` contains a Quest ID, load `docs/Quests/{QuestId}.md`.
Otherwise list all `.md` files in `docs/Quests/` (excluding `QUEST_SPEC_TEMPLATE.md`) and ask:

> "Which quest would you like to implement? Found: [list]"

Verify `status: ready` in the spec. If `status: draft`, warn:

> "This spec is still marked as draft. Some sections may be incomplete. Continue anyway?"

If `status: implemented`, warn:

> "This quest is already marked as implemented. Continue to re-run or update specific parts?"

---

## Step 2 — Audit what already exists

Before creating anything, scan for existing assets matching the spec:
- Fact assets in `Assets/_Game/Data/Facts/`
- `Quest_{QuestId}.asset` in `Assets/_Game/Data/Quests/`
- NPC folders in `Assets/_Game/Data/NPCs/`
- Reward assets in `Assets/_Game/Data/Rewards/`

Report what exists and what needs to be created. Confirm with the user before proceeding.

---

## Step 3 — Create Fact assets

Create all Fact assets listed under **"To create"** in the spec. Facts are dependencies for everything else — create them first.

For each fact:
- `WorldFact` → `manage_scriptable_object` type `Game/Facts/World Fact`, set `_eventKey`
- `DialogueFact` → type `Game/Facts/Dialogue Fact`, set `_nodeId` (use the planned `Start_` asset name as the node ID)
- `KilledFact` → type `Game/Facts/Killed Fact`, use **Generate GUID** context menu after creation

Save all Fact assets to `Assets/_Game/Data/Facts/`.

Check `read_console` after each batch.

---

## Step 4 — Create QuestSO

Create `Quest_{QuestId}.asset` in `Assets/_Game/Data/Quests/` (type `Game/Quest/Quest`).

Set fields from the spec:
- `questId`, `title`, `description`
- `startPart` → wire fact asset + entry text
- `completedParts[]` → wire fact assets + entry text
- `failedParts[]` → wire fact assets + entry text (skip if empty)
- `steps[]` → for each step: title, description, parts (fact + entry)

Check `read_console`.

---

## Step 5 — NPC scaffold (if needed)

For each NPC in the spec where `exists: false`:

Follow the scaffold procedure from `.claude/commands/NPC/create.md` **Steps 2–4**, using the `identity_notes` from the spec as inputs in place of the interactive interview. Skip the interview — all answers come from the spec.

For NPCs that already exist, read their `CLAUDE.md` to confirm the folder structure before proceeding.

---

## Step 6 — Dialogue chains

For each memory block in the spec:

Follow the execution procedure from `.claude/commands/NPC/dialogue.md` **Steps 6–8**, using the dialogue script from the spec in place of user-provided content. Do not re-ask for content — it is already defined.

Work order per chain (leaf nodes first):
1. `TextDialogueNode` assets (NPC speech lines, chained via `nextNode`)
2. `ChoiceDialogueNode` assets (after their branch targets exist), with `dialogueFact` set on any choice that tracks a played state
3. `StartDialogueNode` asset, with `dialogueFact` set to the `DialogueFact` for this chain

---

## Step 7 — NPC Memory entries

For each memory block in the spec, create `Mem_{NPC}_{Topic}.asset` (type `Game/NPC/Memory Entry`) in `Assets/_Game/Data/NPCs/{NPCName}/Memories/`.

Set:
- `unlockConditions[]` → wire Fact assets from spec
- `invalidationConditions[]` → wire Fact assets from spec
- `effects.startdialog` → wire the `StartDialogueNode` created in Step 6

Add the memory entry to `NPCDataSO.memories[]`.

Check `read_console`.

---

## Step 8 — Reward assets

For each reward block in the spec (onStart, onStepCompleted, onCompleted, onFailed):

Create `PlayerReward_{QuestId}_{Trigger}.asset` in `Assets/_Game/Data/Rewards/` (type `Game/Rewards/Player Reward`).

Set:
- `_factType` = `Quest`
- `_questFact` → create a `QuestFact` SO referencing this quest + the matching state or step index. Save as `QuestFact_{QuestId}_{State}.asset` in `Assets/_Game/Data/Facts/`.
- `_xpReward`, `_lpReward`, `_goldReward`, `_statRewards[]` from spec

Add each reward asset to `PlayerRewards._rewards[]` on the PlayerRewards GameObject in the scene.

---

## Step 9 — Register quest

1. Add `Quest_{QuestId}.asset` to `QuestEventsManager._quests[]` in the scene
2. Add `Quest_{QuestId}.asset` to `QuestLogUI._allQuests[]` in the scene

---

## Step 10 — Verify and update spec

Check `read_console` for any remaining errors.

List every asset created with its path.

Update `docs/Quests/{QuestId}.md`:
- Fill in the **Implementation Checklist** with asset paths
- Set `status: implemented`
