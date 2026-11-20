# CLAUDE.md — Assets/_Game/Data/Facts

> Fact SO assets. Class definitions: `Assets/_Game/ScriptableObjects/Facts/` (`Game.Core`).
> To see who sets / reads a fact: `Tools/Quests/Quest Explorer` → **Facts** mode, or right-click a
> fact asset → **Show in Quest Explorer**.

---

## Fact Types

| Type | Key (`ToString()`) | Stored? | Who sets it | Typical readers |
|---|---|---|---|---|
| `KilledFact` | `Killed.{guid}` | stored | `PersistentID.RegisterDeath(credited)` → `WorldStateManager.RegisterKill` (always; credit only gates kill XP). A container's `PersistentID` fact (`KilledFact_HiddenStashChest`, `KilledFact_HobbsBox`) is only its save key and has no setter | quest steps, memory conditions, `PlayerRewardSO` |
| `DialogueFact` | `Dialogue.Played.{nodeId}` | stored | `DialogueSystem` when a `StartDialogueNode.dialogueFact` plays or a `ChoiceOption.dialogueFact` is picked | quest start/completed parts, memory conditions, rewards |
| `WorldFact` | `World.{eventKey}` | stored | Scene / data components via `WorldStateManager.SetWorldEvent`: `TollGate` (paid / deferred / Harl beaten), `ArrivalSequence` (`WorldFact_Arrival_Done` when event-001 ends or is dev-skipped), `PlayerSaveAdapter.ApplyOrigin` on the new-game boot (`OriginSO.originFact` + `SaveSystem._hadBelongingsFact`), `ContainerTakeFact` (first take from a container), `NPCPresence` talk openers (`NpcTalkOpener.played`), `WorldFactTrigger` (player inside a box volume, optional hour window / fact / NPC gates), `DoorLatch._unlatchedFact`. `DialogueSystem` never sets one. Per-asset setters and readers: **Quest Explorer → Facts** (a fact with no setter, e.g. `WorldFact_QuarryPassageOpened`, shows there) | toll gates, `ArrivalSequence` (plays when unset), `NPCPresence._availableAfter` / `_talkOpeners`, memory conditions, quest parts, `FactRemovedObject` |
| `RecipeFact` | `Recipe.Known.{recipeId}` | stored | `CookingUI` Experiment tab on first discovery, or using a `RecipeItemSO` → `WorldStateManager.SetRecipeKnown` (setter shown as the `RecipeSO` in Quest Explorer) | cooking Recipes tab, quest parts, memory conditions |
| `QuestFact` | `Quest.{questId}.{State\|StepN}` | computed | derived from the quest's parts | memory conditions, other quests, rewards |
| `SkillFact` | `Skill.{skillId}` | computed | `PlayerSkills.HasSkill` | memory conditions |
| `StatFact` | `Stat.Requirements(n)` | computed | `PlayerStats` thresholds | memory conditions |
| `HourWindowFact` | `Hour.{start}-{end}` (debug only) | computed | true while `GameClock.Active`'s hour is in [start, end) (wraps midnight, start == end = all day, no clock = false). Raises **no** change event: pull-only, use it in memory conditions / choice gates (re-evaluated on each dialogue open), **never** in quest parts, talk openers or `FactRemovedObject` | Venn's `Mem_BarnGuard_NightPatrol` (unlock) and `_Greeting` / `_NightTalk` (invalidation = hidden while true), fact `HourWindowFact_Night_00_03` |
| `ElapsedFact` | `Elapsed.{sourceKey}.{h}h` (debug only) | computed | true once `_source` (a **stored** fact) is true and `_hours` game hours (decimals) passed since it turned true — timed dialogue | `NPCPresence._talkOpeners` (Hobb: `ElapsedFact_Hobb_BelongingsRummaged_2h`), any memory / quest condition |

**Seen markers** (`Dialogue.Seen.{nodeId}` / `Dialogue.Seen.{nodeId}.{choiceIndex}`) also live in
`_worldFacts` so they are saved, but they are **not facts**: written only by
`WorldStateManager.MarkDialogueSeen` (from `DialogueSystem` when a topic / choice is picked), raise **no**
event, and must never be used as quest / memory conditions. UI-only (`DialogueUI` dimming).

**Knockout XP claims** (`Knockout.XpClaimed.{killedFactKey}`) too: written only by `WorldStateManager.RegisterKnockout`
(first player knockout of an NPC), read by `IsKnockoutClaimed`, no fact event, not a fact, not a kill.

**Meal bonus claims** (`Meal.BonusClaimed.{itemId}`) follow the same rule: saved in `_worldFacts`, written only by
`WorldStateManager.ClaimMealBonus` (first eat of a `PotionItemSO` with a permanent bonus), read by
`IsMealBonusClaimed`, no event, not a fact. The max-HP part of the bonus is saved separately
(`PlayerSaveData.bonusMaxHealth`); stat bonuses are in the saved base stats.

**Timed facts:** `WorldStateManager` records `GameClock.Now` when a stored fact turns false → true (`_factSetTimes`, saved
as `SaveData.factSetTimes`). Setting it true again keeps the first time; false clears it. No time (older save, set while no
clock ran) → counts as long elapsed. Evaluation is lazy (pull readers need nothing for sleep skips / loads). For
event-driven readers (`QuestEventsManager`, `QuestLogUI`, `NPCMemoryComponent`): an `ElapsedFact` evaluated while its delay
runs is remembered with its deadline, and `WorldStateManager.Update` raises `OnFactChanged` (key = the ElapsedFact's
`ToString()`) once when the clock passes it — so quest parts complete on time. Pending deadlines aren't saved: cleared on
restore, re-registered by the quest reseed. A computed source (Quest /
Skill / Stat / Elapsed) has no time → validator V18. Quest Explorer lists no readers for the source through an `ElapsedFact`.

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

`RegisterKill(fact, entity, playerCredited)` records the kill **and** raises `OnEntityKilled` (rewards; kill XP only when `playerCredited` — `Game.AI.KillCredit`).
`SetFact(killedFact, true)` only records it — no XP, no rewards, no despawn (Quest Explorer's play-mode Toggle:
`Editor/QuestExplorer/CLAUDE.md` → Rules / Gotchas).

---

## Naming / Folders

| Fact | Name | Folder |
|---|---|---|
| DialogueFact | `DialogueFact_{NPC}_{Name}` | `Data/Facts/`, or the NPC's own `Data/NPCs/<Npc>/Facts/` / the quest folder (e.g. `TentMate/Facts/`, `Data/Quests/HiddenStash/`). `DialogueFact_blacksmith_smithing_recipe_given` (`BlackSmith/Facts/`) predates the naming rule |
| WorldFact | `WorldFact_{eventKey}` | `Data/Facts/` (some live in a per-NPC `Facts/` subfolder, e.g. `TentMate/Facts/`, or a quest folder) |
| RecipeFact | `RecipeFact_{RecipeOutput}` (`_recipeId` = same, never renamed) | `Data/Facts/` |
| QuestFact | `QuestFact_{QuestId}_{State}` (`Started` / `Completed` / `Failed` / `Step{i}`) | `Data/Facts/` |
| ElapsedFact | `ElapsedFact_{SourceName}_{N}h` (source name without its type prefix) | next to the source |
| KilledFact | `KilledFact_{GameObjectName}` | `Data/Enemies/{Scene}/Generic/` (`Game/World/Generate Missing KilledFacts`) |

Some older facts live under `Data/Quests/{Quest}/` — location is not significant; the index finds
facts anywhere via the AssetDatabase.

One `KilledFact` per tracked entity: sharing one between entities (or baking one into a prefab) makes
every instance share a GUID (validator V5).
