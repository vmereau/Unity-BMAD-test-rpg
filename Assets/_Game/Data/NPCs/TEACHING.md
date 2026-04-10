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
      Text_<NPC>_<Topic>.asset      (optional intro text before the choice)
      TeachChoice_<NPC>_<Topic>.asset
      Text_<NPC>_<Topic>_Confirm.asset  (optional confirmation / farewell text)
```

**Before creating any asset:** check whether `Teachings/` exists under the NPC folder. If it
does not exist, create it.

---

## Node: TeachChoiceDialogueNode

| Field | Type | Purpose |
|-------|------|---------|
| `choices` | `TeachChoiceOption[]` | Array of teaching options shown to the player |

### TeachChoiceOption fields

`TeachChoiceOption` extends `ChoiceOption` and adds cost + effect fields:

| Field | Type | Purpose |
|-------|------|---------|
| `text` | `string` | Button label shown to the player — **see authoring rule below** |
| `requiredMemory` | `NPCMemoryEntrySO` | If set, option is hidden unless memory `IsActive()`. Null = always shown |
| `nextNode` | `DialogueNode` | Node after selection (confirmation text or null to close) |
| `goldCost` | `int` | Gold deducted on selection (0 = free) |
| `lpCost` | `int` | LP cost for **stat** upgrades. Ignored for skill choices — LP cost comes from `SkillSO.lpCost` |
| `skill` | `SkillSO` | If set, calls `PlayerSkills.LearnSkill()`. Stat fields below are **ignored** |
| `statToUpgrade` | `StatType` | Stat to raise. Used only when `skill` is null |
| `statPoints` | `int` | Points added to the stat (min 1). Used only when `skill` is null. Defense has no base value — authoring a Defense upgrade logs a warning and does nothing |

**Mutually exclusive:** set either `skill` OR (`statToUpgrade` + `statPoints`), never both.

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
(Defense upgrades log a warning and have no effect — avoid authoring them unless intentional.)

---

## Chain Example

```
Start_Trainer_Teach           isRepeatable=true, text="Teach me something"
  └─ nextNode → Text_Trainer_Teach        text="What would you like to learn?"
                  └─ nextNode → TeachChoice_Trainer_Teach
                                  ├─ choices[0]: "+2 Strength"
                                  │     skill=null, statToUpgrade=Strength, statPoints=2
                                  │     lpCost=1, goldCost=30
                                  │     nextNode → Text_Trainer_Teach_Done (nextNode=null)
                                  ├─ choices[1]: "+1 Dexterity"
                                  │     skill=null, statToUpgrade=Dexterity, statPoints=1
                                  │     lpCost=1, goldCost=20
                                  │     nextNode → Text_Trainer_Teach_Done (nextNode=null)
                                  └─ choices[2]: "Sword Mastery"
                                        skill=Skill_SwordMastery, statToUpgrade=ignored
                                        lpCost=ignored (read from SkillSO), goldCost=50
                                        nextNode → Text_Trainer_Teach_Done (nextNode=null)
```

---

## Naming Conventions

| Asset | Convention |
|-------|-----------|
| Start node | `Start_<NPC>_<Topic>.asset` |
| Text node | `Text_<NPC>_<Topic>.asset`, `Text_<NPC>_<Topic>_2.asset`, ... |
| Teach choice node | `TeachChoice_<NPC>_<Topic>.asset` |
| Topic folder | `<TopicName>/` inside `Teachings/` — PascalCase |

---

## Checklist When Creating a New Teaching Chain

- [ ] NPC subfolder exists
- [ ] `Teachings/` subfolder exists under NPC folder (create if missing)
- [ ] `Teachings/<TopicName>/` subfolder created
- [ ] `StartDialogueNode` created and linked to a memory (`effects.startdialog`)
- [ ] Memory added to `NPCDataSO.memories` list (or existing memory updated)
- [ ] Each `TeachChoiceOption.text` is the name/description only — no LP or gold cost (the game UI generates those)
- [ ] Skill choices: `skill` field set, `statToUpgrade` / `statPoints` / `lpCost` left at default
- [ ] Stat choices: `skill` field null, `statToUpgrade` + `statPoints` + `lpCost` all set
- [ ] `goldCost` set on every option (0 if free)
- [ ] Each choice's `nextNode` points to confirmation text or null to close
- [ ] Last node in each sub-chain has `nextNode = null`
