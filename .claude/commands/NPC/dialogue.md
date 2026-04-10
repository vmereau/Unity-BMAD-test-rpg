Your task is to create a new NPC dialogue chain OR edit an existing one, following all project conventions.

## Step 0 — Read the rules

Before doing anything else, read both reference documents in parallel:
- `Assets/_Game/Data/NPCs/DIALOGUE.md`
- `Assets/_Game/Data/NPCs/MEMORIES.md`

These are mandatory. Do not skip them.

---

## Step 1 — Determine mode (Create or Edit)

Look at the user's prompt `$ARGUMENTS` and determine:

- **CREATE** — user wants a brand-new dialogue topic (new `StartDialogueNode` + chain)
- **EDIT** — user wants to modify an existing dialogue topic (change text, add/remove nodes, rewire links)
- **AMBIGUOUS** — intent is unclear

If ambiguous, ask the user directly:
> "Do you want to create a new dialogue topic or edit an existing one?"

---

## Step 2 — Identify the NPC

From `$ARGUMENTS`, extract the NPC name or folder path.

Search `Assets/_Game/Data/NPCs/` for a matching subfolder:
- Exact match (e.g. "Villager" → `Assets/_Game/Data/NPCs/Villager/`)
- Case-insensitive partial match (e.g. "black" → `Assets/_Game/Data/NPCs/BlackSmith/`)

If no clear match or multiple candidates exist, list the candidates and ask:
> "Which NPC did you mean? Found: [list]. Please confirm by name or path."

Do not proceed until the NPC folder is confirmed.

---

## Step 3 — Identify the dialogue topic (EDIT mode only)

For **EDIT** mode: extract the topic name or keywords from `$ARGUMENTS`.

Scan `Assets/_Game/Data/NPCs/<NPCName>/Dialogues/` for matching topic folders or `Start_*.asset` files.

Matching strategy:
1. Exact folder name match
2. Partial folder name match (case-insensitive)
3. `StartDialogueNode` asset name match (e.g. `Start_Villager_Greetings`)

If no clear single match:
- List all candidates with their asset paths
- Ask: "Which dialogue did you mean? Found: [list]"

If no dialogues exist yet, treat as CREATE mode and inform the user.

---

## Step 4 — Gather dialogue content

If the user has not provided the full dialogue content in `$ARGUMENTS`, ask for what is missing.

For **CREATE**, you need at minimum:
- Topic label (the `StartDialogueNode.text` — shown as the button in the topic list)
- Whether this topic is repeatable (`isRepeatable`) — default true, ask if not obvious
- The NPC's speech lines (one or more, honoring the 300-character limit per `TextDialogueNode`)
- Whether the chain includes player choices (`ChoiceDialogueNode`) — if unclear, ask

For **EDIT**, you need:
- Which specific nodes or fields to change
- The new content or wiring

**ChoiceDialogueNode clarification rule:** If the user mentions choices/options/branches but has not specified what each choice says, where each choice leads, or whether any choice is memory-gated — ask for clarification before creating any assets. Do not guess branching structure.

**Text splitting rule:** If any NPC speech exceeds 300 characters, automatically plan to split it into chained `TextDialogueNode` assets. Inform the user of the split before creating assets. Split at sentence boundaries.

---

## Step 5 — Memory association (CREATE mode — StartDialogueNode)

When creating a new `StartDialogueNode`, always ask:

> "Should I create a new `NPCMemoryEntrySO` (`Mem_<NPC>_<Topic>`) for this topic and add it to
> `NPC_<NPCName>.asset`'s memories list? Or link it to an existing memory entry?
>
> A new always-active memory (empty unlock/invalidation conditions) is the default if you want
> this topic always available."

Wait for confirmation. Then either:
- **New memory:** Create `Mem_<NPC>_<Topic>.asset` in `<NPCName>/Memories/`, set `effects.startdialog`, add to `NPCDataSO.memories`
- **Existing memory:** Identify the correct `NPCMemoryEntrySO` and set `effects.startdialog` on it

---

## Step 6 — Plan and confirm

Before creating or modifying any assets, present a concise plan to the user:

```
[CREATE / EDIT] — <NPC> / <TopicName>

Assets to create:
  - Start_<NPC>_<Topic>.asset  (isRepeatable: <true/false>)
  - Text_<NPC>_<Topic>.asset   ("<first line...>")
  - Text_<NPC>_<Topic>_2.asset ("<second line...>")  ← if split needed
  - Choice_<NPC>_<Topic>.asset ← if choices present
  - Mem_<NPC>_<Topic>.asset    ← if new memory

Assets to modify:
  - NPC_<NPC>.asset            ← add memory to list
  - <ExistingNode>.asset       ← if editing

Folder to create (if needed):
  - <NPCName>/Dialogues/<TopicName>/
  - <NPCName>/Memories/
```

Ask: "Does this look right? Shall I proceed?"

---

## Step 7 — Execute

Once confirmed, create or modify assets using Unity MCP tools (`manage_scriptable_object`, `manage_asset`).

Work in this order:
1. Create missing folders (Dialogues, Memories, topic subfolder)
2. Create `TextDialogueNode` assets (leaf nodes first, then chain `nextNode` references back up)
3. Create `ChoiceDialogueNode` assets (after their branch `TextDialogueNode` targets exist)
4. Create `StartDialogueNode` asset, set `nextNode` to first node in chain
5. Create or update `NPCMemoryEntrySO`, set `effects.startdialog`
6. Update `NPCDataSO.memories` list

After each asset creation, check Unity console for errors (`read_console`).

---

## Step 8 — Verify

After all assets are created/modified:
- Confirm no Unity compilation or import errors in the console
- List every asset created or modified with its path
- Note any fields the user may want to review in the Inspector (e.g. unlock conditions on a memory, `isRepeatable` flag)
