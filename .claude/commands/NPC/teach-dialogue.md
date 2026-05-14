Your task is to create a new NPC teaching dialogue chain OR edit an existing one, following all project conventions.

## Step 0 — Read the rules

Before doing anything else, read all three reference documents in parallel:
- `Assets/_Game/Data/NPCs/TEACHING.md`
- `Assets/_Game/Data/NPCs/DIALOGUE.md`
- `Assets/_Game/Data/NPCs/MEMORIES.md`

These are mandatory. Do not skip them.

---

## Step 1 — Determine mode (Create or Edit)

Look at the user's prompt `$ARGUMENTS` and determine:

- **CREATE** — user wants a brand-new teaching topic (new `StartDialogueNode` + `TeachChoiceDialogueNode` chain)
- **EDIT** — user wants to modify an existing teaching topic (change options, costs, text, rewire links)
- **AMBIGUOUS** — intent is unclear

If ambiguous, ask the user directly:
> "Do you want to create a new teaching topic or edit an existing one?"

---

## Step 2 — Identify the NPC

From `$ARGUMENTS`, extract the NPC name or folder path.

Search `Assets/_Game/Data/NPCs/` for a matching subfolder:
- Exact match (e.g. "Trainer" → `Assets/_Game/Data/NPCs/Trainer/`)
- Case-insensitive partial match (e.g. "black" → `Assets/_Game/Data/NPCs/BlackSmith/`)

If no clear match or multiple candidates exist, list the candidates and ask:
> "Which NPC did you mean? Found: [list]. Please confirm by name or path."

Do not proceed until the NPC folder is confirmed.

---

## Step 3 — Identify the teaching topic (EDIT mode only)

For **EDIT** mode: extract the topic name or keywords from `$ARGUMENTS`.

Scan `Assets/_Game/Data/NPCs/<NPCName>/Teachings/` for matching topic folders or `Start_*.asset` files.

Matching strategy:
1. Exact folder name match
2. Partial folder name match (case-insensitive)
3. `StartDialogueNode` asset name match (e.g. `Start_Trainer_Teach`)

If no clear single match:
- List all candidates with their asset paths
- Ask: "Which teaching dialogue did you mean? Found: [list]"

If no `Teachings/` folder or no teaching chains exist yet, treat as CREATE mode and inform the user.

---

## Step 4 — Gather teaching content

If the user has not provided the full teaching content in `$ARGUMENTS`, ask for what is missing.

For **CREATE**, you need at minimum:
- Topic label (the `StartDialogueNode.text` — shown as the button in the topic list, e.g. "Teach me something")
- Whether this topic is repeatable (`isRepeatable`) — default `true`, ask if not obvious
- Optional intro text spoken by the NPC before the choice menu (a `TextDialogueNode` prompt)
- The list of teaching options (see per-option fields below)
- Optional confirmation text after a choice is made

For each **teaching option**, collect:

| Field | What to ask |
|-------|-------------|
| Effect type | Skill or stat upgrade? |
| **Skill choice** | Which `SkillSO` asset? (name / path). The LP cost is read from `SkillSO.lpCost` — do not ask for it separately |
| **Stat choice** | Which `StatType`? (`Strength`, `Dexterity`, `Endurance`, `Intelligence`, `Defense`) and how many `statPoints`? and what `lpCost`? |
| `goldCost` | Gold deducted on selection (0 = free) |
| `requiredMemory` | Is this option memory-gated? (Null = always shown). Gate memories using `SkillFact` conditions are the standard way to show/hide options based on learned skills — see `Assets/_Game/Data/Skills/CLAUDE.md`. Gate memories must be in `NPCDataSO.memories` to function. |

**Stat Defense warning:** If the user requests a Defense stat upgrade, warn them:
> "Defense has no base value in the stat system — a Defense upgrade logs a warning and does nothing at runtime. Are you sure you want to include it?"

For **EDIT**, you need:
- Which specific nodes, options, or fields to change
- The new content or wiring

**Text splitting rule:** If any NPC speech exceeds 300 characters, automatically plan to split it into chained `TextDialogueNode` assets. Inform the user before creating assets. Split at sentence boundaries.

---

## Step 5 — Derive choice button labels

For every `TeachChoiceOption`, derive the `text` field. The game UI appends LP and gold costs automatically — **do not include costs in the text field**.

**Skill choice:** `<SkillSO.displayName>`  — e.g. `Sword Mastery`

**Stat upgrade choice:** `+<statPoints> <StatName>` — e.g. `+2 Strength`

Present the derived labels with their costs in the plan summary (for the user's review), but set only the name in the asset `text` field.

---

## Step 6 — Memory association (CREATE mode)

**Topic memory** — for the `StartDialogueNode`:

> "Should I create a new `NPCMemoryEntrySO` (`Mem_<NPC>_<Topic>`) for this teaching topic and add
> it to `NPC_<NPCName>.asset`'s memories list? Or link it to an existing memory entry?
>
> A new always-active memory (empty unlock/invalidation conditions) is the default if you want
> this topic always available."

Wait for confirmation. Then either:
- **New memory:** Create `Mem_<NPC>_<Topic>.asset` in `<NPCName>/Memories/`, set `effects.startdialog`, add to `NPCDataSO.memories`
- **Existing memory:** Identify the correct `NPCMemoryEntrySO` and set `effects.startdialog` on it

**Gate memories** — for `TeachChoiceOption.requiredMemory`:

If any choice option needs skill-based visibility (show only if skill learned / hide once learned):
1. Create a `SkillFact` asset in `Assets/_Game/Data/Player/` referencing the relevant `SkillSO` — see `Assets/_Game/Data/Skills/CLAUDE.md`
2. Create a gate `NPCMemoryEntrySO` (no `effects.startdialog`) with `unlockConditions` / `invalidationConditions` set to the `SkillFact`
3. Add the gate memory to `NPCDataSO.memories` — **required**: `requiredMemory` is checked against the NPC's active memories list at runtime; a memory not in this list is never considered active

---

## Step 7 — Plan and confirm

Before creating or modifying any assets, present a concise plan to the user:

```
[CREATE / EDIT] — <NPC> / Teachings/<TopicName>

Assets to create:
  - Start_<NPC>_<Topic>.asset       (isRepeatable: <true/false>, text: "<label>")
  - Text_<NPC>_<Topic>.asset        ("<intro text...>")   ← if intro text provided
  - TeachChoice_<NPC>_<Topic>.asset
      choices[0]: "+2 Strength" — StatBased, LP: 1, Gold: 30    ← text field value in quotes
      choices[1]: "Sword Mastery" — SkillBased, LP: 3, Gold: 50
      ...
  - Text_<NPC>_<Topic>_Confirm.asset ("<confirmation...>")  ← if confirmation text provided
  - Mem_<NPC>_<Topic>.asset         ← if new memory

Assets to modify:
  - NPC_<NPC>.asset                 ← add memory to list
  - <ExistingNode>.asset            ← if editing

Folders to create (if needed):
  - <NPCName>/Teachings/
  - <NPCName>/Teachings/<TopicName>/
  - <NPCName>/Memories/
```

Ask: "Does this look right? Shall I proceed?"

---

## Step 8 — Execute

Once confirmed, create or modify assets using Unity MCP tools (`manage_scriptable_object`, `manage_asset`).

Work in this order:
1. Create missing folders (`Teachings/`, `Memories/`, topic subfolder) — **never place teaching assets inside `Dialogues/`**
2. Create `SkillFact` assets in `Assets/_Game/Data/Player/` if gate memories are needed
3. Create gate `NPCMemoryEntrySO` assets (no `effects.startdialog`) with `SkillFact` conditions
4. Create confirmation `TextDialogueNode` assets (leaf nodes first)
5. Create `TeachChoiceDialogueNode` asset, wire each option's `confirmNextNode` to its confirmation text; wire confirm text `nextNode` back to `TeachChoiceDialogueNode` (loop-back rule)
6. Create intro `TextDialogueNode` asset (if present), set `nextNode` to the `TeachChoiceDialogueNode`
7. Create `StartDialogueNode` asset, set `nextNode` to the first node in chain
8. Create topic `NPCMemoryEntrySO`, set `effects.startdialog` to the `StartDialogueNode`
9. Update `NPCDataSO.memories` — add **all** memories (topic + all gate memories)

**MCP array patching note:** `Array.size` patches are unsupported. Set elements directly with `data[0]`, `data[1]`, etc. — Unity auto-expands the array. Works for `choices`, `unlockConditions`, `invalidationConditions`, and `memories`.

For each `TeachChoiceOption`, set fields exactly as follows based on effect type:

**Skill choice:**
- `skill` = reference to `SkillSO` asset
- `statToUpgrade` = leave at default (0 / `Strength` — will be ignored at runtime)
- `statPoints` = leave at default (1 — will be ignored at runtime)
- `lpCost` = leave at default (0 — will be ignored at runtime; LP cost comes from `SkillSO.lpCost`)
- `goldCost` = as specified

**Stat choice:**
- `skill` = null (leave empty)
- `statToUpgrade` = the chosen `StatType`
- `statPoints` = as specified (min 1)
- `lpCost` = as specified
- `goldCost` = as specified

After each asset creation, check Unity console for errors (`read_console`).

---

## Step 9 — Verify

After all assets are created/modified:
- Confirm no Unity compilation or import errors in the console
- List every asset created or modified with its path
- Note any fields the user may want to review in the Inspector (e.g. memory unlock conditions, `isRepeatable` flag, `SkillSO` reference)
