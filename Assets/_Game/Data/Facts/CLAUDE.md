# CLAUDE.md — Assets/_Game/Data/Facts

> Fact SO assets. Class definitions: `Assets/_Game/ScriptableObjects/Facts/` (`Game.Core`).
> To see who sets / reads a fact: `Tools/Quests/Quest Explorer` → **Facts** mode, or right-click a
> fact asset → **Show in Quest Explorer**.

---

## Fact Types

| Type | Key (`ToString()`) | Stored? | Who sets it | Typical readers |
|---|---|---|---|---|
| `KilledFact` | `Killed.{guid}` | stored | `PersistentID.RegisterDeath()` → `WorldStateManager.RegisterKill` | quest steps, memory conditions, `PlayerRewardSO` |
| `DialogueFact` | `Dialogue.Played.{nodeId}` | stored | `DialogueSystem` when a `StartDialogueNode.dialogueFact` plays or a `ChoiceOption.dialogueFact` is picked | quest start/completed parts, memory conditions, rewards |
| `WorldFact` | `World.{eventKey}` | stored | **nothing yet** — `SetWorldEvent` has no callers | quest parts (always reported "never set") |
| `QuestFact` | `Quest.{questId}.{State\|StepN}` | computed | derived from the quest's parts | memory conditions, other quests, rewards |
| `SkillFact` | `Skill.{skillId}` | computed | `PlayerSkills.HasSkill` | memory conditions |
| `StatFact` | `Stat.Requirements(n)` | computed | `PlayerStats` thresholds | memory conditions |

Computed facts are never written to `WorldStateManager._worldFacts` and cannot be toggled.
Never build a fact key by hand — always go through the typed `WorldStateManager` API.

---

## Reverse Links (facts hold no back-references)

- **KilledFact → entity:** the only link is `PersistentID._killedFact` on the scene object (usually a
  prefab-instance override). The fact itself only has a GUID. Find the entity with Quest Explorer, or
  right-click the `PersistentID` → **Show KilledFact in Quest Explorer**.
- **DialogueFact → NPC:** attributed by walking `NPCEntity.memories[] → effects.startdialog → nextNode /
  choices[].nextNode / confirmNextNode / denyNextNode`. A dialogue unreachable from any memory is
  "unattributed" (validator V4).
- `QuestFact._questState`: 0 IsStarted, 1 IsCompleted, 2 IsFailed, ≥3 = step index + 3. Removing or
  reordering quest steps shifts these — edit steps through Quest Explorer, which remaps them.

---

## `RegisterKill` vs `SetFact`

`RegisterKill(fact, entity)` records the kill **and** raises `OnEntityKilled` (XP, rewards).
`SetFact(killedFact, true)` only records it — no XP, no rewards, no despawn. Quest Explorer's
play-mode **Toggle** uses the raw `SetFact`.

---

## Naming / Folders

| Fact | Name | Folder |
|---|---|---|
| DialogueFact | `DialogueFact_{NPC}_{Name}` | `Data/Facts/` |
| WorldFact | `WorldFact_{eventKey}` | `Data/Facts/` |
| QuestFact | `QuestFact_{QuestId}_{State}` (`Started` / `Completed` / `Failed` / `Step{i}`) | `Data/Facts/` |
| KilledFact | `KilledFact_{GameObjectName}` | `Data/Enemies/{Scene}/Generic/` (`Game/World/Generate Missing KilledFacts`) |

Some older facts live under `Data/Quests/{Quest}/` — location is not significant; the index finds
facts anywhere via the AssetDatabase.

One `KilledFact` per tracked entity: sharing one between entities (or baking one into a prefab) makes
every instance share a GUID (validator V5).
