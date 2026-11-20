# CLAUDE.md — Assets/_Game/Data/NPCs

> Loaded when Claude accesses NPC data. Read this before creating or modifying any NPC assets.

---

## Folder Structure

Each NPC must have its own dedicated subfolder:

```
Assets/_Game/Data/NPCs/
  <NPCName>/
    CLAUDE.md                  ← NPC identity (instantiated NPC_IDENTITY_TEMPLATE.md) — READ FIRST (see below)
    NPC_<NPCName>.asset        ← NPCEntity (menu Game/NPC/NPC Data)
    Dialogues/
      <TopicName>/
        Start_<NPC>_<Topic>.asset
        Text_<NPC>_<Topic>.asset
        ...
    Teachings/                 ← chains using TeachChoiceDialogueNode (create if missing)
      <TopicName>/
        Start_<NPC>_<Topic>.asset
        TeachChoice_<NPC>_<Topic>.asset
        ...
    Memories/
      Mem_<NPC>_<Topic>.asset
      ...
    Facts/                     ← optional: facts owned by this NPC (e.g. TentMate/Facts/, BlackSmith/Facts/)
    <other NPC-owned data>     ← optional, at the folder root: e.g. RacketGuard/TollRacket_Harl.asset (TollRacketSO),
                                 CompanyGuard/Barks_Arrival_*.asset (BarkSetSO used by the arrival sequence)
```

**Per-NPC `<NPCName>/CLAUDE.md`** is the canonical character sheet (instantiated `NPC_IDENTITY_TEMPLATE.md`): read it
before writing any dialogue, memory or behavioural asset for that NPC and keep it up to date as the character develops.
Its *Dialogue Writing Rules* extend `DIALOGUE.md` for that NPC. Defaults it does not repeat: `NPC_<NPCName>.asset`,
`Dialogues/`, `Teachings/` and `Memories/` follow the layout above, and the scene object is an instance of
`Prefabs/Entities/Humanoids/NPC_base Variant.prefab` (no dedicated prefab). Its *Asset Reference* lists only NPC-specific
rows (scene path, routine, shop, quest role, killed fact, non-default entity / prefab).

Shared dialogue used by every NPC lives in `Shared/Dialogues/` — `TheftReturn/` (`Start_TheftReturn` → `Choice_TheftReturn`
give-back / refuse with `Signal_TheftReturn_GiveBack` / `_Refuse`, referenced by `StealthConfig`; opened after the scold
line when a caught thief still carries stolen goods — `Scripts/AI/PERCEPTION.md` → Theft return). Not reachable from a memory,
so Quest Explorer may list it as unattributed.

Shared, non-NPC-specific data: `Barks/` holds `BarkSetSO` assets (menu `Game/Dialogue/Bark Set`, name
`Barks_<Purpose>`) — one-liners said in speech bubbles (sneak warning, theft / trespass alerts and scolds, woken
sleeper, knockout, player-relationship lines…). Which code reads which set is in the SOs that reference them
(`Data/Entities/WitnessProfile_Humanoid`, `CombatConfig` — see `Scripts/AI/PERCEPTION.md` and `Scripts/AI/RELATIONS.md`). Lines never repeat twice in a row.

Every `NPC_<Name>.asset` references `WitnessProfile_Humanoid` in `_witnessProfile` (reaction to a sneaking player;
auto-assigned by `NPCEntity.Reset()` from the Create menu, **not** when created through MCP / `CreateInstance` — set
it explicitly). Swap to another `WitnessProfile_*` to make one NPC react differently; null = never reacts.

Every `NPC_<Name>.asset` also references its own `KilledFact` in `_killedFact` — the **same** asset as the scene
NPC's `PersistentID` (`PersistentID.Awake` warns on a mismatch). Objects owned by the NPC (`Ownership`) become free to
take once that fact is set; a missing fact keeps them owned forever.

`NPCEntity.memories` is **auto-synced** from `<NPCName>/Memories/**` by `NPCMemoriesAutoSync`
(menu fallback `Game/Dev/Sync All NPC Memories`) — never edit the list by hand; a memory saved
elsewhere belongs to no NPC (Quest Explorer V16). The auto-sync doesn't reliably fire for memories created
through MCP `execute_code` (`AssetDatabase.CreateAsset`) — afterwards run the menu and check `NPCEntity.memories`.

**Rule:** If a dedicated NPC folder does not exist, create it before placing any assets.

---

## NPCEntity flags

- `CanBeExecuted` (default true) — false protects the NPC from the player's execute stomp while unconscious
  (spec-execute-unconscious-npc). No NPC is protected yet; set it on story-critical NPCs if they must survive.

---

## NPC Identity Check

When entering any NPC subfolder, immediately check whether a `CLAUDE.md` exists inside it.
Exempt: combat-only NPCs with no dialogue (empty `memories`, no `Dialogues/`, e.g. `Bandit/`) and the non-NPC
folders `Barks/` and `Shared/`.

- **If `CLAUDE.md` is missing or does not contain an NPC identity section:** ask the user:
  > "No NPC identity found for `<NPCName>`. Would you like me to create the folder and identity
  > now using `/NPC:create`?"
  Do not proceed with dialogue or memory work until an identity exists — it is required to write
  consistent dialogue.

- **If `CLAUDE.md` exists:** read it fully before touching any dialogue or memory asset for that NPC.

The identity template is at `Assets/_Game/Data/NPCs/NPC_IDENTITY_TEMPLATE.md`.
Use `/NPC:create` to scaffold a new NPC folder with identity in one step.

---

## Creating a New NPC

Use the command `/NPC:create` — it handles the full scaffold interactively:
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
- Writing rules: **no em-dashes in dialogue text**, and the greeting-opener pattern most NPCs start with
- `StartDialogueNode`, `TextDialogueNode`, `ChoiceDialogueNode` usage
- Text length limits and chaining rules
- Naming conventions and folder layout
- Shop topics (`ShopDialogueNode`, scene-instance stock, topic-level visibility) — author them with
  `/NPC:shop-dialogue`

---

## Before Working on Teaching Dialogue Chains

If the dialogue chain contains (or will contain) a **`TeachChoiceDialogueNode`**, read
**`TEACHING.md`** in this folder **instead of — or in addition to — `DIALOGUE.md`**. It describes:
- `TeachChoiceDialogueNode` and `TeachChoiceOption` fields
- The `Teachings/` subfolder rule (teaching chains must **not** live in `Dialogues/`)
- Choice button label format: `<SkillName>` / `+N <StatName>` — no costs in the text (game UI renders those)
- Full authoring checklist for teaching chains

---

## Before Working on Memories

Read **`MEMORIES.md`** in this folder. It describes:
- `NPCMemoryEntrySO` fields and activation logic
- How memories gate dialogue topics and choices
- The `Memories/` subfolder convention
