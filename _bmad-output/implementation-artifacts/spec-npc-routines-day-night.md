---
title: 'NPC routines — day/night position'
type: 'feature'
created: '2026-10-27'
status: 'done'
baseline_commit: '2122f61'
context:
  - '{project-root}/_bmad-output/project-context.md'
  - '{project-root}/Assets/_Game/Scripts/AI/CLAUDE.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** NPCs ignore the `GameClock`: they stand at their spawn point (or patrol) around the clock. The game needs Gothic-style daily routines that can later drive activity (forging, eating…), location and dialogue availability.

**Approach:** Add an extensible per-NPC routine: hour blocks (`start → end : activity @ spot`). They are evaluated statelessly from the time of day and applied through `EntityBrain`. The first version only changes position. StartingTown NPCs keep their current day behaviour from 06:00 to 21:00 and spend 21:00–06:00 inside one of the two door-less village houses. The Guard keeps no routine.

## Boundaries & Constraints

**Always:** Routines are stateless: the current block is derived from the hour, nothing is saved, and on region load or a time skip the NPC snaps (warps) to its block. On a natural hour change it walks there. A block with no spot means "home behaviour" (spawn spot, or authored waypoints), so daytime behaviour is unchanged. Routines never interrupt combat, watching, pursuit or confronting: the new target applies when the brain returns to non-combat. Dead NPCs ignore routines. Each NPC goes to the closest door-less house: Blacksmith, Elder, Merchant and Villager to `Village house (1)` (−16.5, 2.5); Innkeeper to `Village house` (−17, 25.3). The Innkeeper is equidistant from both, so it goes to the north house to split the crowd.

**Ask First:** If a house interior is not on the baked NavMesh (unreachable spot), HALT before re-baking or moving spots outside.

**Never:** Activities other than `Stand`, animations, dialogue/interaction gating, door opening, the `House` (with a door), `OnDayNightChanged`, off-screen simulation, save data.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Evening | Clock crosses 21:00, NPC idle at day spot | Walks to its house spot, stops, turns to the spot's facing | N/A |
| Morning | Crosses 06:00 | Walks back to spawn spot (Villager resumes patrol) | N/A |
| Wrap block | Block 21→6, hour 2 | Block matches | N/A |
| Load / start at night | Region loads at 23:00 | NPC warped into the house on its first routine tick | Warp fails (off mesh) → `GameLog.Warn`, walk instead |
| Sleep skip | Bed 20:00 → 06:00 | `OnTimeSkipped` → NPC warped to its day spot | N/A |
| Combat at 21:00 | NPC fighting a spider | Keeps fighting; heads home on disengage | N/A |
| No clock | Region played without Core | Home behaviour, one `GameLog.Info` | N/A |
| Gap / overlap | No block for the hour / two match | Gap → home behaviour; overlap → first listed wins | N/A |

</frozen-after-approval>

## Code Map

- `Assets/_Game/Scripts/Core/Time/GameClock.cs` -- world clock; needs a read path for region objects
- `Assets/_Game/Scripts/AI/EntityBrain.cs` -- Idle/Patrol state machine; `ResumeNonCombat`, `DisengageFromCombat`, `CancelWarning`, `TransitionToIdle` return to non-combat
- `Assets/_Game/ScriptableObjects/Entities/Entity.cs` -- `ExecuteIdle` default = stand still (NPCs); monsters override
- `Assets/_Game/Data/Events/OnTimeSkipped.asset` -- `GameEventSO_Float` raised by `SkipTo`
- `Assets/_Game/Scenes/StartingTown.unity` -- NPC instances (prefab `NPC_base Variant`), the two `VillageHouse` instances
- `Assets/Tests/EditMode/GameTimeTests.cs` -- test style reference

## Tasks & Acceptance

**Execution:**
- [x] `Assets/_Game/Scripts/Core/Time/GameClock.cs` -- add `public static GameClock Active` (set in `Awake` when the config is valid, cleared in `OnDestroy` if it's still this instance, reset at `SubsystemRegistration`) -- region-scene readers can't serialize a Core reference
- [x] `Assets/_Game/Scripts/AI/NPC/NpcRoutineBlock.cs` -- `[Serializable] NpcRoutineBlock { int startHour; int endHour; NpcActivity activity; Transform spot }` + `enum NpcActivity { Stand }` + static pure `NpcRoutineSchedule.FindBlock(IReadOnlyList<NpcRoutineBlock>, int hour)` → index or −1 (start inclusive, end exclusive, wraps midnight, start == end = whole day, first match wins) -- extensible data, testable rule
- [x] `Assets/_Game/Scripts/AI/NPC/NpcRoutine.cs` -- MonoBehaviour (`Game.AI`, `[DefaultExecutionOrder(100)]` so it runs after `EntityBrain.Start`): `_brain`, `_onTimeSkipped`, `_blocks`, `_pollInterval` 1 s. Polls `GameClock.Active.Hour`. On a block change calls `_brain.SetRoutineSpot(spot, snap)`. Snaps on the first evaluation and on `OnTimeSkipped`. Exposes `CurrentBlock` / `CurrentActivity` -- future activity / dialogue hook
- [x] `Assets/_Game/Scripts/AI/EntityBrain.cs` -- record the spawn position in `Start`. Add `public void SetRoutineSpot(Transform spot, bool snap)`: store the spot. If dead, store only. If snap, `Warp` (guarded). If Idle/Patrolling, apply now; otherwise apply on the next non-combat return. Apply = spot → Idle anchored at the spot (walk there, face `spot.rotation` on arrival); null → original behaviour (waypoints → patrol, else Idle walking back to spawn). Non-combat returns use the current routine target -- single integration point
- [x] `Assets/Tests/EditMode/NpcRoutineScheduleTests.cs` -- matrix: normal block, wrap block, end exclusive, gap → −1, overlap → first, whole-day block, empty list
- [x] `Assets/_Game/Scenes/StartingTown.unity` (via MCP) -- `NpcRoutineSpots` parent with 5 night spots inside the two village houses (on NavMesh, spread out). Add `NpcRoutine` to Blacksmith, Elder, Innkeeper, Merchant and Villager: blocks `[6→21 Stand, no spot]`, `[21→6 Stand, house spot]`, `_onTimeSkipped` wired. The Guard is untouched
- [x] Docs -- `Scripts/AI/CLAUDE.md` routine section + rows; `Scripts/Core/CLAUDE.md` `GameClock.Active`; `deferred-work.md`: NPC routines shipped, log follow-ups (activities + animations, dialogue gating, door opening / `House`, `OnDayNightChanged`)

**Acceptance Criteria:**
- Given StartingTown played via Core at 20:59, when 21:00 passes, then the 5 NPCs walk into their house and the Guard stays at its post.
- Given an NPC inside at night, when the player sleeps until 06:00, then on wake every routine NPC stands at its day spot (the Villager patrols).
- Given a save made at 23:00, when loaded, then the routine NPCs appear inside the houses without walking across town.
- Given EditMode tests, when run, then `NpcRoutineScheduleTests` and the existing suites pass.

## Design Notes

The brain stays the only mover: `NpcRoutine` decides *where*, the brain decides *when*, so combat, witness and theft flows need no routine checks. Future block fields (dialogue availability, interaction gate, animation) extend `NpcRoutineBlock`; readers query `NpcRoutine.CurrentBlock`. Whole hours suffice for now.

## Verification

**Commands:**
- Unity MCP `run_tests` (EditMode) -- expected: all pass
- Unity MCP `read_console` after compile -- expected: no errors

**Manual checks:**
- Play Core + StartingTown, use the `GameClock` context menu `Skip 1 hour` around 20–21 h and 05–06 h; NPCs move as in the ACs

## Suggested Review Order

**Routine → brain contract**

- Entry point: the brain's only routine API; stores, guards (setup / corpse), witness-drop on snap.
  [`EntityBrain.cs:950`](../../Assets/_Game/Scripts/AI/EntityBrain.cs#L950)

- Picks patrol (home + waypoints) or Idle walking to spot / spawn.
  [`EntityBrain.cs:982`](../../Assets/_Game/Scripts/AI/EntityBrain.cs#L982)

- Arrival: stop, face spot yaw, walk back if pushed, warn on partial path.
  [`EntityBrain.cs:391`](../../Assets/_Game/Scripts/AI/EntityBrain.cs#L391)

- Non-combat returns route to the routine target instead of `_disengageState`.
  [`EntityBrain.cs:1109`](../../Assets/_Game/Scripts/AI/EntityBrain.cs#L1109)

- Warp for load / sleep snaps; failure falls back to walking.
  [`EntityBrain.cs:969`](../../Assets/_Game/Scripts/AI/EntityBrain.cs#L969)

**Schedule evaluation**

- Polls the hour, decides snap vs walk, defers walks while a menu holds the cursor.
  [`NpcRoutine.cs:78`](../../Assets/_Game/Scripts/AI/NPC/NpcRoutine.cs#L78)

- Pure block lookup: start inclusive, end exclusive, wraps midnight.
  [`NpcRoutineBlock.cs:31`](../../Assets/_Game/Scripts/AI/NPC/NpcRoutineBlock.cs#L31)

- Region-scene read path to the Core clock.
  [`GameClock.cs:24`](../../Assets/_Game/Scripts/Core/Time/GameClock.cs#L24)

**Peripherals**

- Block lookup matrix tests.
  [`NpcRoutineScheduleTests.cs:1`](../../Assets/Tests/EditMode/NpcRoutineScheduleTests.cs#L1)

- Scene: `NpcRoutineSpots` + `NpcRoutine` on 5 NPCs (Guard untouched).
  [`StartingTown.unity`](../../Assets/_Game/Scenes/StartingTown.unity)
