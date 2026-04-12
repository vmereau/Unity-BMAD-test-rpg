# TEACHING.md — TeachChoiceDialogueNode System

> Read this before creating or modifying any teaching dialogue chain (any chain that contains a `TeachChoiceDialogueNode`).

---

## Overview

`TeachChoiceDialogueNode` is a specialised dialogue node used by trainer NPCs to offer the
player a menu of learnable skills or stat upgrades, each with an LP and/or gold cost.

**Script location:** `Assets/_Game/ScriptableObjects/Dialogue/TeachChoiceDialogueNode.cs`
`CreateAssetMenu: Game/Dialogue/Teach Choice Node` — filename prefix `TeachChoice_`

---

## Folder Rule

Teaching dialogue chains must **not** live in `Dialogues/`. They live in a dedicated sibling:

```
<NPCName>/
  Dialogues/          ← regular dialogue chains
  Teachings/          ← ALL TeachChoiceDialogueNode chains go here
    <TopicName>/
      Start_<NPC>_<Topic>.asset
      Text_<NPC>_<Topic>.asset               (optional intro text before the choice)
      TeachChoice_<NPC>_<Topic>.asset
      Text_<NPC>_<Topic>_Confirm.asset       (success text — loops back)
      Text_<NPC>_<Topic>_AtCap.asset         (required when limitStat > 0 — loops back)
```

**Before creating any asset:** check whether `Teachings/` exists under the NPC folder. If it
does not exist, create it.

---

## Node: TeachChoiceDialogueNode

| Field | Type | Purpose |
|-------|------|---------|
| `choices` | `TeachChoiceOption[]` | Array of teaching options shown to the player |

### TeachChoiceOption fields

`TeachChoiceOption` extends `ChoiceOption` and adds teaching type, cost, and routing fields.
The custom **`TeachChoiceOptionDrawer`** shows only the fields relevant to the selected `teachingType`.

> Do **not** add `[Header]` attributes to `TeachChoiceOption` fields — the PropertyDrawer
> controls all rendering and `[Header]` decorators break the fixed-height rect layout.

#### Always visible

| Field | Type | Purpose |
|-------|------|---------|
| `text` | `string` | Button label shown to the player — **see authoring rule below** |
| `requiredMemory` | `NPCMemoryEntrySO` | If set, option is hidden unless memory `IsActive()`. Null = always shown |
| `teachingType` | `TeachingType` | `SkillBased` or `StatBased` — controls which fields are shown in the Inspector |
| `goldCost` | `int` | Gold deducted on selection (0 = free) |
| `confirmNextNode` | `DialogueNode` | Node to advance to when teaching succeeds — must loop back (see below) |

#### SkillBased only

| Field | Type | Purpose |
|-------|------|---------|
| `skill` | `SkillSO` | Calls `PlayerSkills.LearnSkill()`. LP cost comes from `SkillSO.lpCost` |

#### StatBased only

| Field | Type | Purpose |
|-------|------|---------|
| `statToUpgrade` | `StatType` | Stat to raise |
| `statPoints` | `int` | Points added to the stat AND LP cost for this choice (min 1) |
| `limitStat` | `int` | Cap on training (0 = no cap). See **Stat Cap** section below |
| `denyNextNode` | `DialogueNode` | Node to advance to when the cap is reached — must loop back (see below) |

**Note:** Defense upgrades log a warning and have no effect — avoid authoring them unless intentional.

---

## Authoring Rule — Choice Button Label

The `text` field of each `TeachChoiceOption` names what the player gains. **Do not include LP
or gold costs** — the game UI generates and displays those automatically from the cost fields.

**Skill choice:**
```
<SkillDisplayName>
```
Example: `Sword Mastery`

**Stat upgrade choice:**
```
+<statPoints> <StatName>
```
Example: `+2 Strength`

Available `StatType` values: `Strength`, `Dexterity`, `Endurance`, `Intelligence`, `Defense`

---

## Loop-Back Rule — confirmNextNode and denyNextNode

`TeachChoiceDialogueNode` **always** includes an implicit exit option, so the player can leave
without buying. Both `confirmNextNode` and `denyNextNode` text nodes must therefore loop back
to the same `TeachChoiceDialogueNode` — never terminate with `nextNode = null`. Terminating
with null would close the dialogue, removing the player's ability to buy other items in the
same session.

```
TeachChoice_NPC_Topic
  ├─ choice succeeds  → Text_NPC_Topic_Confirm
  │                       └─ nextNode → TeachChoice_NPC_Topic   ← loops back, NOT null
  └─ stat at cap      → Text_NPC_Topic_AtCap
                          └─ nextNode → TeachChoice_NPC_Topic   ← loops back, NOT null
```

---

## Stat Cap (limitStat / denyNextNode)

When `limitStat > 0` on a StatBased choice:

- The cap is checked against the player's **base stat** (permanent upgrades only — equipment
  bonuses are excluded). A piece of gear cannot prevent a player from training permanently.
- The cap check happens at **apply time** (when the player clicks the button), not at display
  time. The button remains interactable even when the player is at the cap — they click it and
  receive the `denyNextNode` text instead.
- When the cap is reached: **no gold, no LP is consumed**, and dialogue advances to
  `denyNextNode`.
- If `denyNextNode` is null when the cap is reached, a warning is logged and the dialogue
  closes. Always author a `denyNextNode` when `limitStat > 0`.
- `limitStat = 0` means no cap — training is never denied by this check regardless of stat value.

**Naming convention for the at-cap text node:**

| Situation | Asset name |
|-----------|-----------|
| One stat-capped choice in the topic | `Text_<NPC>_<Topic>_AtCap.asset` |
| Multiple capped choices with different messages | `Text_<NPC>_<Topic>_<StatName>AtCap.asset` |

---

## Chain Example

```
Start_Trainer_Teach           isRepeatable=true, text="Teach me something"
  └─ nextNode → Text_Trainer_Teach        text="What would you like to learn?"
                  └─ nextNode → TeachChoice_Trainer_Teach
                                  ├─ choices[0]: "+2 Strength"
                                  │     teachingType=StatBased
                                  │     statToUpgrade=Strength, statPoints=2, goldCost=30
                                  │     limitStat=10  (base Strength cap — equipment ignored)
                                  │     confirmNextNode → Text_Trainer_Teach_Confirm
                                  │                         └─ nextNode → TeachChoice_Trainer_Teach
                                  │     denyNextNode   → Text_Trainer_Teach_AtCap
                                  │                         └─ nextNode → TeachChoice_Trainer_Teach
                                  └─ choices[1]: "Sword Mastery"
                                        teachingType=SkillBased
                                        skill=Skill_SwordMastery, goldCost=50
                                        confirmNextNode → Text_Trainer_Teach_Confirm
                                                            └─ nextNode → TeachChoice_Trainer_Teach
```

---

## Naming Conventions

| Asset | Convention |
|-------|-----------|
| Start node | `Start_<NPC>_<Topic>.asset` |
| Text node | `Text_<NPC>_<Topic>.asset`, `Text_<NPC>_<Topic>_2.asset`, ... |
| Teach choice node | `TeachChoice_<NPC>_<Topic>.asset` |
| Confirm text node | `Text_<NPC>_<Topic>_Confirm.asset` |
| At-cap text node | `Text_<NPC>_<Topic>_AtCap.asset` (or `_<StatName>AtCap` if multiple) |
| Topic folder | `<TopicName>/` inside `Teachings/` — PascalCase |

---

## Checklist When Creating a New Teaching Chain

- [ ] NPC subfolder exists
- [ ] `Teachings/` subfolder exists under NPC folder (create if missing)
- [ ] `Teachings/<TopicName>/` subfolder created
- [ ] `StartDialogueNode` created and linked to a memory (`effects.startdialog`)
- [ ] Memory added to `NPCDataSO.memories` list (or existing memory updated)
- [ ] Each `TeachChoiceOption.text` is the name/description only — no LP or gold cost (the game UI generates those)
- [ ] `teachingType` set correctly on each choice (`SkillBased` or `StatBased`)
- [ ] Skill choices: `skill` field set, stat fields left at default
- [ ] Stat choices: `skill` field null, `statToUpgrade` + `statPoints` set
- [ ] `goldCost` set on every option (0 if free)
- [ ] Each choice's `confirmNextNode` points to a text node that loops back to the `TeachChoiceDialogueNode`
- [ ] StatBased choices with a cap: `limitStat > 0` and `denyNextNode` set to an at-cap text node
- [ ] Every at-cap text node's `nextNode` loops back to the `TeachChoiceDialogueNode` (same rule as confirm)
- [ ] No `[Header]` attributes added to `TeachChoiceOption` fields (breaks PropertyDrawer layout)
