---
title: 'Player origins: origin pick at New Game'
type: 'feature'
created: '2026-11-16'
status: 'done'
baseline_commit: 'd3125ead2b9a6c63ce6149250345b4ae245eadc4'
context: ['{project-root}/docs/World/player-origins.md']
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** Every new game starts from the same blank protagonist. The "Stripped" beat (promised belongings that never arrive) has no origin to hang on.

**Approach:** New Game in the start menu opens an origin pick panel. The chosen origin crosses to Core through `GameSession`. On the new-game boot, Core applies the origin's small permanent stat bonus and optional skill, and records it as world facts. Belongings are only *shown* on the pick screen ("sent ahead with the Company baggage cart"). After the arrival, Voss flatly denies any cart exists.

## Boundaries & Constraints

**Always:**
- Roster (data, `OriginSO`):
  - Blacksmith's Apprentice: Strength +1, Endurance +1, `Skill_Smithing`. Belongings: smith's hammer, leather apron, 30 gold.
  - Joiner's Apprentice: Dexterity +2, `Skill_Begginer_Lockpicking`. Belongings: chisel set and joiner's plane in a tool roll, 20 gold.
  - Wretch: nothing, `hasBelongings = false`. Wretch is the default.
- The origin applies on the **new-game path only**, before `SaveSystem` builds `_newGameSnapshot`, so death-screen Restart keeps it. Loading a save never applies an origin.
- No event that grants anything is raised: no `_onSkillLearned`, no LP spent, no level-up or XP. A stats-changed raise is allowed.
- Facts: `WorldFact_Origin_<Id>` per origin, plus `WorldFact_Origin_HadBelongings`, set through `SetWorldEvent`.
- Voss topic "Where are my belongings?" unlocks only when `WorldFact_Arrival_Done` and `WorldFact_Origin_HadBelongings` are both set. It is one-shot (invalidated by its own DialogueFact). No quest and no log entry. Author it with the `NPC:dialogue` conventions, in Voss's voice, with no em-dashes. His line alone must make the theft clear.
- Pick panel reuses the MainMenu window style (`Common/ActionButton`, TMP, no CanvasScaler).

**Ask First:** changing the roster or amounts. Granting any item or gold.

**Never:** add belongings or gold to the inventory. Add a quest or journal entry. Change the save format.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Menu pick | Blacksmith picked → Begin | Core boots, STR/END +1, Smithing known, `Origin_Blacksmith` + `HadBelongings` facts set, inventory and gold empty | N/A |
| Play in Core | No `GameSession` origin | Wretch applied, no bonus, no `HadBelongings` | N/A |
| Unknown id | Session id not in catalog | Wretch applied | `GameLog.Warn` |
| Menu load | Continue / Load Game | No origin applied, save state restored | N/A |
| Failed menu load | Unreadable slot → startup fallback | Pending origin (none) → Wretch applied | existing toast |
| Restart | Death screen Restart, no save | Snapshot restores the origin's stats, skill and facts | N/A |
| Skill already known | Grant on a set that already holds it | No duplicate, no event | N/A |

</frozen-after-approval>

## Code Map

- `Scripts/Core/GameSession.cs` -- static menu → Core hand-off; add the origin id.
- `Scripts/Core/Save/SaveSystem.cs` -- `HandleRegionLoaded(isStartup)` builds the new-game snapshot. The hook point.
- `Scripts/Player/PlayerStats.cs` -- `RestoreBaseStats` / `GetBaseStat`; base stats are saved.
- `Scripts/Player/Progression/PlayerSkills.cs` -- `RestoreSkills` replaces the set; `LearnSkill` raises events.
- `Scripts/Player/PlayerSaveAdapter.cs` -- holds every player system ref; natural owner of "apply origin".
- `Scripts/UI/Screens/MainMenuUI.cs` + `Scenes/MainMenu.unity` -- New Game → `LoadCore(null)`.
- `Data/NPCs/Warden/` -- Voss identity, dialogues, memories (`Mem_Warden_ErrandAssign` = condition example).
- `Data/Facts/WorldFact_Arrival_Done.asset` -- existing arrival fact.
- `Prefabs/Player/Player.prefab` -- `GoldSystem._startingGold` 500 → 0 (no gold at start, AC3).

## Tasks & Acceptance

**Execution:**
- [x] `ScriptableObjects/Origins/OriginSO.cs`, `OriginCatalogSO.cs` (`Game.Player`; SO classes live under `ScriptableObjects/` per project-context) -- `OriginSO`: `originId`, `displayName`, `description` (TextArea), `List<StatBonus>{StatType, int}`, `SkillSO skill` (optional), `List<string> promisedItems`, `int promisedGold`, `bool hasBelongings`, `WorldFact originFact`. `OriginCatalogSO`: `origins` list, `defaultOrigin`, `Find(id)` that falls back to the default with a warning -- data shape.
- [x] `Data/Origins/` -- `Origin_Blacksmith`, `Origin_Joiner`, `Origin_Wretch`, `OriginCatalog` (default Wretch); `Data/Facts/WorldFact_Origin_Blacksmith|Joiner|Wretch`, `WorldFact_Origin_HadBelongings` -- roster.
- [x] `Scripts/Core/GameSession.cs` -- `ChosenOriginId` + `SetOrigin(id)` / `TakeOriginId()`, reset on play -- hand-off.
- [x] `Scripts/Player/PlayerStats.cs` -- `AddBaseStat(StatType, int)` with no level / LP side effects (one stats-changed raise) -- silent bonus.
- [x] `Scripts/Player/Progression/PlayerSkills.cs` -- `GrantSkillSilently(SkillSO)`: adds the id, no LP, no event -- silent skill.
- [x] `Scripts/Player/PlayerSaveAdapter.cs` -- `ApplyOrigin(OriginSO)`: stat bonuses, skill, origin fact, and `HadBelongings` when `hasBelongings` -- one place for the grant.
- [x] `Scripts/Core/Save/SaveSystem.cs` -- serialized `OriginCatalogSO` and `WorldFact _hadBelongingsFact`. In `HandleRegionLoaded`, on `isStartup && !IsLoading`, resolve `GameSession.TakeOriginId()` through the catalog, then `_player.ApplyOrigin` **before** `BuildSaveData`. Wire both in `Core.unity` -- timing.
- [x] `Scripts/UI/Screens/OriginPickUI.cs` + MainMenu scene -- `OriginPick` panel inside `Window`: one ActionButton per origin, a detail text (description, bonuses, skill name, "Sent ahead with the Company baggage cart: …" or "You arrive with nothing."), and a Begin button. `MainMenuUI`: New Game shows the panel (hides `MainButtons`, Back visible). Begin → `GameSession.SetOrigin(id)` → `LoadCore(null)`. Esc / Back → main buttons -- pick screen.
- [x] `Data/NPCs/Warden/Dialogues/Belongings/` + `Memories/Mem_Warden_Belongings` -- start node with topic "Where are my belongings?" and its DialogueFact `DialogueFact_Warden_Belongings`. Unlock on Arrival_Done + Origin_HadBelongings; invalidate on the DialogueFact. Run the memory sync -- Voss beat.
- [x] `Assets/Tests/EditMode/` -- tests for `OriginCatalogSO.Find` (known, unknown, null → default), `AddBaseStat`, `GrantSkillSilently` (no duplicate) and `GameSession` origin take/clear -- matrix coverage.
- [x] Docs: `Scenes/CLAUDE.md` boot flow, `Scripts/Core/CLAUDE.md` (GameSession), `Data/Facts/CLAUDE.md` (WorldFact setters), `Warden/CLAUDE.md`, `docs/World/player-origins.md` status -- discoverability.

**Acceptance Criteria:**
- Given the start menu, when New Game is clicked, then the origin panel lists three origins and shows the selected origin's bonuses, skill and belongings line.
- Given a Joiner pick, when the arrival is done and the player talks to Voss, then "Where are my belongings?" is offered once. Given a Wretch pick, it never is.
- Given any origin, after boot the inventory and gold are empty and no skill-learned toast appeared.

## Design Notes

Applying in `HandleRegionLoaded` covers every new-game entry (menu, Play in Core, failed menu load, Dev: Skip Arrival) through one gate, and it runs after the facts store and player exist.

Draft Voss line: Player: "Where are my belongings? The recruiter said they'd come up on the baggage cart." Voss: "There is no baggage cart. There has never been one. You came through that gate with what's on your back, and that is all the ledger says you own. Forget it. Think about your debt."

## Verification

**Commands:**
- `run_tests` EditMode -- all pass, new origin tests included.
- `read_console` after compile -- no errors.

**Manual checks:**
- Play from MainMenu: pick Blacksmith → Character screen shows STR/END +1, Skills tab shows Smithing. Dev: Skip Arrival → Voss offers the belongings topic.

## Suggested Review Order

**New-game gate**

- Entry point: the origin is consumed and applied on the startup region, before the snapshot.
  [`SaveSystem.cs:341`](../../Assets/_Game/Scripts/Core/Save/SaveSystem.cs#L341)

- Resolves the id through the catalog (unknown / none → Wretch).
  [`SaveSystem.cs:355`](../../Assets/_Game/Scripts/Core/Save/SaveSystem.cs#L355)

- Applies bonuses, refills stamina, grants the skill and sets the origin facts.
  [`PlayerSaveAdapter.cs:175`](../../Assets/_Game/Scripts/Player/PlayerSaveAdapter.cs#L175)

- Silent grants: one stats-changed raise, no LP / skill-learned event.
  [`PlayerStats.cs:99`](../../Assets/_Game/Scripts/Player/PlayerStats.cs#L99)
  [`PlayerSkills.cs:55`](../../Assets/_Game/Scripts/Player/Progression/PlayerSkills.cs#L55)

**Data**

- Origin shape: bonuses, optional skill, display-only belongings, origin fact.
  [`OriginSO.cs:15`](../../Assets/_Game/ScriptableObjects/Origins/OriginSO.cs#L15)

- Lookup with default / first-origin fallback.
  [`OriginCatalogSO.cs:33`](../../Assets/_Game/ScriptableObjects/Origins/OriginCatalogSO.cs#L33)

**Menu hand-off and pick screen**

- Static hand-off, consumed once by Core.
  [`GameSession.cs:25`](../../Assets/_Game/Scripts/Core/GameSession.cs#L25)

- New Game opens the pick; Begin sets the origin then loads Core.
  [`MainMenuUI.cs:91`](../../Assets/_Game/Scripts/UI/Screens/MainMenuUI.cs#L91)
  [`MainMenuUI.cs:120`](../../Assets/_Game/Scripts/UI/Screens/MainMenuUI.cs#L120)

- Panel selection and the detail text with the baggage-cart line.
  [`OriginPickUI.cs:67`](../../Assets/_Game/Scripts/UI/Screens/OriginPickUI.cs#L67)
  [`OriginPickUI.cs:99`](../../Assets/_Game/Scripts/UI/Screens/OriginPickUI.cs#L99)

**Peripherals**

- Voss topic: memory gated on Arrival_Done + Origin_HadBelongings, one-shot.
  [`Mem_Warden_Belongings.asset`](../../Assets/_Game/Data/NPCs/Warden/Memories/Mem_Warden_Belongings.asset)

- Tests: catalog, silent grants, session hand-off, detail text.
  [`PlayerOriginTests.cs:17`](../../Assets/Tests/EditMode/PlayerOriginTests.cs#L17)
