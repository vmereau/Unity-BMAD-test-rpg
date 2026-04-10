# CLAUDE.md — Assets/_Game/Data/NPCs

> Loaded when Claude accesses NPC data. Read this before creating or modifying any NPC assets.

---

## Folder Structure

Each NPC must have its own dedicated subfolder:

```
Assets/_Game/Data/NPCs/
  <NPCName>/
    CLAUDE.md                  ← NPC identity (instantiated NPC_IDENTITY_TEMPLATE.md) — READ FIRST
    NPC_<NPCName>.asset        ← NPCDataSO
    Dialogues/
      <TopicName>/
        Start_<NPC>_<Topic>.asset
        Text_<NPC>_<Topic>.asset
        ...
    Memories/
      Mem_<NPC>_<Topic>.asset
      ...
```

**Rule:** If a dedicated NPC folder does not exist, create it before placing any assets.

---

## NPC Identity Check

When entering any NPC subfolder, immediately check whether a `CLAUDE.md` exists inside it.

- **If `CLAUDE.md` is missing or does not contain an NPC identity section:** ask the user:
  > "No NPC identity found for `<NPCName>`. Would you like me to create the folder and identity
  > now using `/NPC:create-npc`?"
  Do not proceed with dialogue or memory work until an identity exists — it is required to write
  consistent dialogue.

- **If `CLAUDE.md` exists:** read it fully before touching any dialogue or memory asset for that NPC.

The identity template is at `Assets/_Game/Data/NPCs/NPC_IDENTITY_TEMPLATE.md`.
Use `/NPC:create-npc` to scaffold a new NPC folder with identity in one step.

---

## Creating a New NPC

Use the command `/NPC:create-npc` — it handles the full scaffold interactively:
1. Asks for the NPC name
2. Creates `<NPCName>/` folder
3. Creates `NPC_<NPCName>.asset` (type: `Game/NPC/NPC Data`)
4. Walks through `NPC_IDENTITY_TEMPLATE.md` fields and creates `<NPCName>/CLAUDE.md` with the filled identity
5. Creates `Dialogues/` and `Memories/` subfolders

Manual path (if not using the command):
1. Create `<NPCName>/` folder under `Assets/_Game/Data/NPCs/`
2. Create `NPC_<NPCName>.asset` inside it
3. Copy `NPC_IDENTITY_TEMPLATE.md`, fill all fields, save as `<NPCName>/CLAUDE.md`
4. Create `Dialogues/` and `Memories/` subfolders

---

## Before Working on Dialogues

Read **`DIALOGUE.md`** in this folder. It describes:
- `StartDialogueNode`, `TextDialogueNode`, `ChoiceDialogueNode` usage
- Text length limits and chaining rules
- Naming conventions and folder layout

---

## Before Working on Memories

Read **`MEMORIES.md`** in this folder. It describes:
- `NPCMemoryEntrySO` fields and activation logic
- How memories gate dialogue topics and choices
- The `Memories/` subfolder convention
