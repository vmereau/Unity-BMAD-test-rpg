# CLAUDE.md — Assets/_Game/Data/Skills

> Loaded when Claude accesses skill data. Read before creating or modifying `SkillSO` or `SkillFact` assets.

---

## SkillSO

**Type:** `Game.Progression.SkillSO`  
**Asset menu:** `Game/Progression/Skill` — filename prefix `Skill_`  
**Script:** `Assets/_Game/ScriptableObjects/Skills/SkillSO.cs`

| Field | Serialized name | Purpose |
|-------|----------------|---------|
| `skillId` | `_skillId` | Unique string key used by `PlayerSkills.HasSkill()` / `LearnSkill()` |
| `displayName` | `_displayName` | Human-readable name — shown in UI and used for choice button labels |
| `description` | `_description` | Flavour text (optional) |
| `lpCost` | `_lpCost` | Learning Points consumed on learn — read by `PlayerSkills.LearnSkill()` and displayed by the teach UI |
| `statsRequirements` | `_statsRequirements` | `List<StatRequirement>` — checked before allowing the skill to be learned |
| `skillRequirements` | `_skillRequirements` | `List<SkillSO>` — prerequisite skills; all must be learned first |

**Folder convention:** group skills by category in subfolders, e.g. `Skills/Lockpicking/`, `Skills/Combat/`.

---

## SkillFact

**Type:** `Game.Core.SkillFact`  
**Asset menu:** `Game/Facts/Skill Fact` — filename prefix `SkillFact_`  
**Script:** `Assets/_Game/ScriptableObjects/Facts/SkillFact.cs`  
**Asset location:** `Assets/_Game/Data/Player/` (alongside other player-scoped facts)

`SkillFact` is a **computed `Fact`** — it evaluates `PlayerSkills.HasSkill(skill.skillId)` at runtime via `WorldStateManager.PlayerHasSkill()`. It is never stored in `WorldStateManager._worldFacts`; it reads live from `PlayerSkills`.

Use `SkillFact` in `NPCMemoryEntrySO.unlockConditions` / `invalidationConditions` to gate NPC dialogue choices by skill learned state:

| Goal | Where to set the SkillFact |
|------|--------------------------|
| Show option only after skill X is learned | `unlockConditions` of a gate memory |
| Hide option once skill X is learned | `invalidationConditions` of a gate memory |

A **gate memory** is an `NPCMemoryEntrySO` with no `effects.startdialog`, used solely as a `TeachChoiceOption.requiredMemory` reference. It must be added to `NPCDataSO.memories` — see `Assets/_Game/Data/NPCs/TEACHING.md` for the full authoring pattern.

---

## Runtime: PlayerSkills

**Script:** `Assets/_Game/Scripts/Player/Progression/PlayerSkills.cs`

Key methods:
- `HasSkill(string skillId)` → `bool`
- `CanLearnSkill(SkillSO skill)` → checks stat requirements, skill prerequisites, and whether already learned
- `LearnSkill(SkillSO skill)` → spends LP, adds to learned set, raises `_onSkillLearned` event

`DialogueSystem.ApplyTeachChoice()` calls `LearnSkill()` when a `SkillBased` teach choice is selected. `DialogueSystem` also auto-hides already-learned `SkillBased` choices at runtime — `SkillFact` gate memories provide the same behaviour with explicit author control.
