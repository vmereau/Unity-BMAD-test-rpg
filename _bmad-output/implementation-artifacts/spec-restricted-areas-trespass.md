---
title: 'Restricted areas: trespass detection by owners, warning chase / instant aggression'
type: 'feature'
created: '2026-11-09'
status: 'done'
baseline_commit: '4c6edde'
context: ['{project-root}/_bmad-output/implementation-artifacts/tech-spec-stealing-ownership-chase-scold.md', '{project-root}/Assets/_Game/Scripts/AI/CLAUDE.md']
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** Only owned interactables are illegal; the world has no places the player may not be. A guarded warehouse or a dock that is off-limits at night cannot be sneaked into, so the stealth / thievery loop has no "get in unseen" goal.

**Approach:** A `RestrictedArea` scene component (box volumes + owners: `NPCEntity` list and/or `FactionSO` list, optional hours window, optional instant aggression). An owner perceives a player inside its area through the normal awareness meter (walking or sneaking, sneak-shortened); when fully aware it shouts a warning ("Get out of there!") and runs the existing theft chase → forced scold; a scold grants a grace period. Highly restricted areas make owners attack instead. Tested on the dock warehouse (always, new warehouse guard) and the docks (21–06, new dock guard: stands at the dock guard post by day, patrols the docks by night).

## Boundaries & Constraints

**Always:** Only owners react (entity listed, or own faction listed); a dead owner never reacts. Reuse the Pursuing / Confronting flow, `TheftPursuitRegistry` claims and `EntityPerception` meter — no parallel chase code. Hours window = routine rule (start incl., end excl., wraps midnight, start == end = all day); no `GameClock.Active` → always restricted. Restricted-area objects, waypoints and routine spots live outside `Area1_Blockout` (tool roots are rebuilt). Pure rules in static helpers with EditMode tests. GameLog + TAG, config in SOs.

**Ask First:** Changing the faction of an existing NPC; changing faction hostility beyond adding the new Company faction symmetrically to the lists that already hold `Faction_Neutral`.

**Never:** HUD / trespass indicator; items or containers inheriting area ownership; bounty / reputation / persistent hostility; saving grace or trespass state; lock-on support against an aggro owner (known gap).

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Owner spots trespasser | Player inside warehouse, guard's cone + LOS | Meter fills (sneak slower / shorter) → Suspicious → full: alert bark, Pursuing → catch → trespass scold dialogue, area grace starts | N/A |
| Grace | Scolded, still inside | No owner of that area reacts for `trespassGraceDuration`; afterwards the meter can fill again | N/A |
| Two owners | Both see the trespasser | Both chase the area's open incident; first catcher scolds, other gives up | N/A |
| Hours window | Docks at 12:00 vs 23:00 | 12:00: legal, owners ignore; 23:00: trespass | No clock → restricted |
| Instant aggression | Area flag on, owner fully aware | Alert bark, Engaging (no warning telegraph); its hits land on the player despite neutral factions | Aggro cleared on disengage / search end / death |
| Non-owner / dead owner | Neutral NPC without ownership sees player inside | Existing behaviour only (sneak witness) | N/A |
| Player leaves mid-chase | Chase running | Chase continues by theft rules (escape / catch) | N/A |

</frozen-after-approval>

## Code Map

- `Scripts/AI/EntityPerception.cs` -- `ScanForTarget` modes (hostile > witness); `CanSeeTheft`, gizmo label
- `Scripts/AI/EntityBrain.cs` -- `RespondToDetectedTarget`, Pursuing / Confronting (`_pursuitKind`, `_pursuitTheftId`), `DisengageFromCombat`, `EndSearch`, `TransitionToDead`
- `Scripts/AI/EntityMeleeAttacker.cs:277` -- `IsHostileTarget` faction filter
- `Scripts/AI/TargetRegistry.cs` -- `FindClosestNonHostileStealthTarget`
- `Scripts/AI/NPC/NpcRoutineBlock.cs` -- `NpcRoutineSchedule.FindBlock` hour rule; `NpcRoutine` (day spot / night patrol)
- `ScriptableObjects/Events/GameEventSO_TheftCommitted.cs` -- `TheftKind`
- `ScriptableObjects/Entities/NPC/WitnessProfileSO.cs`, `ScriptableObjects/Config/StealthConfigSO.cs` -- barks, tunables
- `Scripts/World/Ownership.cs` -- sibling pattern (owner + KilledFact liveness)

## Tasks & Acceptance

**Execution:**
- [x] `Scripts/World/RestrictedArea.cs` (NEW, `Game.World`) -- owners `List<NPCEntity>` + `List<FactionSO>`, `_useHours`/`_startHour`/`_endHour`, `_instantAggression`; child/sibling `BoxCollider`s (trigger, Ignore Raycast) as shape; static active list; `IsOwnedBy(NPCEntity, FactionSO)`, `Contains(Vector3)`, `IsRestrictedNow`, `StartGrace(seconds)`, `GetOrOpenIncident()` (negative ids, reused until claimed); static `TryFindTrespassedArea(Vector3, NPCEntity, FactionSO, out area)`; red wire gizmos -- area data + query
- [x] `Scripts/World/RestrictedAreaRules.cs` (NEW) -- pure `IsInsideBox(localPoint, center, size)`, `IsRestrictedHour(hasClock, useHours, hour, start, end)` (shared rule with `NpcRoutineSchedule`) -- testable
- [x] `GameEventSO_TheftCommitted.cs` -- append `Trespass` to `TheftKind` -- reuse pursuit kind
- [x] `WitnessProfileSO.cs` + `StealthConfigSO.cs` + assets -- `TrespassAlertBarks`, `TrespassScoldBarks`; `trespassGraceDuration = 15`; NEW `Barks_TrespassAlert` ("Get out of there!", "What are you doing here?!", "Hey! You can't be in here!"), `Barks_TrespassScold` ("You don't belong here. Out!", "This area is off-limits.", "I see you here again, you'll regret it.") assigned on `WitnessProfile_Humanoid` -- data
- [x] `EntityPerception.cs` -- third mode `IsTrespassing` (priority hostile > trespass > witness): non-hostile stealth target inside an area this entity owns, perceived with the hostile formula (`DetectionRange`, sneak multipliers, fills while walking); expose `TrespassArea`; label "Trespass ·" -- detection
- [x] `EntityBrain.cs` -- trespass target on full awareness: instant-aggression area → set melee aggro target + alert bark + `TransitionToEngaging`; else alert bark (trespass set) + Pursuing with `TheftKind.Trespass` and the area incident; Confronting uses `TrespassScoldBarks` and calls `area.StartGrace`; clear aggro on `DisengageFromCombat`, `EndSearch`, death -- behaviour
- [x] `EntityMeleeAttacker.cs` -- `SetAggroTarget(FactionMember)`; `IsHostileTarget` also true for it -- aggression hits land
- [x] `Data/Factions/Faction_Company.asset` (NEW) -- same hostile list as `Faction_Neutral`; add it wherever `Faction_Neutral` is listed hostile -- faction ownership
- [x] New NPCs `WarehouseGuard`, `DockGuard` via the `NPC:create` skill (faction Company, DetectionRange 8, witness profile Humanoid, KilledFact) placed in `StartingTown` -- test actors
- [x] `StartingTown.unity` -- NEW root `RestrictedAreas`: `RA_Warehouse` (warehouse interior box, owner `NPC_WarehouseGuard`, always) and `RA_Docks` (layout `docks` rect x 1.1 z 66.1, 18.4 × 24.4, owner `Faction_Company`, 21–06); WarehouseGuard patrol (camp door → inside → river landing → back, waits that leave sneak windows); DockGuard `NpcRoutine` 06–21 spot in front of the dock guard post (−30.5, 48.5), 21–06 home = deck patrol; waypoints / spots under a root outside `Area1_Blockout` -- test setup
- [x] `Assets/Tests/EditMode/RestrictedAreaRulesTests.cs` (NEW) -- box containment (rotated via local point), hours window incl. wrap / all-day / no clock, owner matching -- matrix edges
- [x] `Scripts/AI/CLAUDE.md`, `Scripts/World/CLAUDE.md`, `Scripts/Stealth/CLAUDE.md`, `Data/NPCs` folder files -- document trespass mode, `RestrictedArea`, new guards

**Acceptance Criteria:**
- Given the warehouse guard patrolling, when the player sneaks in during a gap with no LOS, then no reaction occurs.
- Given an area with `_instantAggression` toggled on (manual test on `RA_Warehouse`), when the guard becomes fully aware, then it attacks and its punches damage the player.
- Given the DockGuard at 12:00 / 23:00, then it stands at the guard post / patrols the deck, and the docks are restricted only at night.
- Given EditMode tests, when run, then all pass (incl. existing stealth / theft tests).

## Design Notes

Trespass detection is perception-driven (continuous presence) while theft is event-driven; both converge on Pursuing so barks, give-up rules, registry claims and the forced dialogue stay single-sourced. Containment is computed on demand from box shapes (`InverseTransformPoint` vs box center/size) at the perception scan rate — no trigger events, so teleports / loads / layer-matrix settings can't desync it. Incident ids are negative to never collide with `InteractionSystem`'s positive theft ids.

As built (review notes): incident reuse lives in `EntityBrain.RespondToTrespasser` (World must not depend on `TheftPursuitRegistry`), so `RestrictedArea` exposes `OpenIncidentId` + `OpenIncident()` instead of `GetOrOpenIncident()`. Shape = enabled BoxColliders on the area GO and its children. `RA_Docks` is clipped north of the warehouse (z 56.5–78.3, centre z 67.4, 18.4 × 21.8). `DockGuard_GuardPost` stands at the guard post's ramp-foot walkway (−34.4, 53.5), facing the compound gate. `TrespassSightRange` falls back to the witness profile's `TheftSightRange` for a passive owner (DetectionRange 0).

## Verification

**Commands:**
- Unity MCP `run_tests` (EditMode) -- expected: all green
- `read_console` after compile -- expected: no errors

**Manual checks (if no CLI):**
- Play StartingTown: F3 overlay shows "Trespass" mode on the guards; sneak / walk into the warehouse, get warned, chased, scolded; grace holds 15 s; set clock to 23:00 for the docks.

## Suggested Review Order

**Detection (who notices a trespasser)**

- Entry point: third perception mode, hostile > trespass > witness, owner + area lookup per scan.
  [`EntityPerception.cs:238`](../../Assets/_Game/Scripts/AI/EntityPerception.cs#L238)

- Trespasser perceived with the hostile formula (walks or sneaks) at the trespass range.
  [`EntityPerception.cs:193`](../../Assets/_Game/Scripts/AI/EntityPerception.cs#L193)

- Area query: owned, restricted now (hours + grace), point inside an enabled box.
  [`RestrictedArea.cs:126`](../../Assets/_Game/Scripts/World/RestrictedArea.cs#L126)

- Hours window + grace in one gate; no clock means always restricted.
  [`RestrictedArea.cs:59`](../../Assets/_Game/Scripts/World/RestrictedArea.cs#L59)

**Reaction (warn → chase → scold, or attack)**

- Full awareness routes trespassers before witnesses.
  [`EntityBrain.cs:477`](../../Assets/_Game/Scripts/AI/EntityBrain.cs#L477)

- Aggression vs shared-incident chase; reuses the theft Pursuing flow.
  [`EntityBrain.cs:639`](../../Assets/_Game/Scripts/AI/EntityBrain.cs#L639)

- Re-validates the area so grace/hours changes between scans are honoured.
  [`EntityBrain.cs:634`](../../Assets/_Game/Scripts/AI/EntityBrain.cs#L634)

- Watching owners don't rescan, so they check the area themselves.
  [`EntityBrain.cs:690`](../../Assets/_Game/Scripts/AI/EntityBrain.cs#L690)

- Searching re-sight applies area rules unless already fighting that trespasser.
  [`EntityBrain.cs:597`](../../Assets/_Game/Scripts/AI/EntityBrain.cs#L597)

- Trespass picks its own alert barks; theft counter untouched.
  [`EntityBrain.cs:815`](../../Assets/_Game/Scripts/AI/EntityBrain.cs#L815)

**Grace and aggression lifetime**

- Grace at the catch, before the channel check (no endless loop).
  [`EntityBrain.cs:916`](../../Assets/_Game/Scripts/AI/EntityBrain.cs#L916)

- Grace held while the scold is open, restarted when it closes.
  [`EntityBrain.cs:940`](../../Assets/_Game/Scripts/AI/EntityBrain.cs#L940)

- Leaving combat always clears the melee aggro target.
  [`EntityBrain.cs:56`](../../Assets/_Game/Scripts/AI/EntityBrain.cs#L56)

- Retargeting onto another attacker drops the aggro target.
  [`EntityBrain.cs:1013`](../../Assets/_Game/Scripts/AI/EntityBrain.cs#L1013)

- Aggro target bypasses the faction filter so punches land on the neutral player.
  [`EntityMeleeAttacker.cs:289`](../../Assets/_Game/Scripts/AI/EntityMeleeAttacker.cs#L289)

**Pure rules, data and tests**

- Box containment, hours window, owner matching (pure, tested).
  [`RestrictedAreaRules.cs:16`](../../Assets/_Game/Scripts/World/RestrictedAreaRules.cs#L16)

- Hour window rule now shared with NPC routines.
  [`GameTime.cs:50`](../../Assets/_Game/Scripts/Core/Time/GameTime.cs#L50)

- Trespass barks fall back to the theft sets.
  [`WitnessProfileSO.cs:52`](../../Assets/_Game/ScriptableObjects/Entities/NPC/WitnessProfileSO.cs#L52)

- `Trespass` appended to the pursuit kind (serialized values unchanged).
  [`GameEventSO_TheftCommitted.cs:7`](../../Assets/_Game/ScriptableObjects/Events/GameEventSO_TheftCommitted.cs#L7)

- Grace duration tunable.
  [`StealthConfigSO.cs:87`](../../Assets/_Game/ScriptableObjects/Config/StealthConfigSO.cs#L87)

- Rotated box test now catches a local-axis sign error.
  [`RestrictedAreaRulesTests.cs:29`](../../Assets/Tests/EditMode/RestrictedAreaRulesTests.cs#L29)

