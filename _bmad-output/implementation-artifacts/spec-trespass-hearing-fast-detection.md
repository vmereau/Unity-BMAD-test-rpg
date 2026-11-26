---
title: 'Trespass hearing: fast detection and investigation of the last heard spot'
type: 'feature'
created: '2026-11-26'
status: 'done'
baseline_commit: 'b223063d4c3e97f43e615db128e249a9f9911b7d'
context:
  - '{project-root}/Assets/_Game/Scripts/AI/PERCEPTION.md'
  - '{project-root}/Assets/_Game/Scripts/Stealth/CLAUDE.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** An awake restricted-area owner hears a walking or running trespasser, but hearing uses the global
`hearingFillTime` (2 s, the sleeping value), so in practice vision decides and players walk past owners unnoticed.
When a half-noticed trespasser goes out of earshot or starts sneaking, the owner only turns in place and forgets.

**Approach:** Awake owners in trespass mode fill awareness from hearing in **1 s** (new `StealthConfigSO` value). The
hearing radii stay the same as for sleeping NPCs. A Suspicious owner that heard the trespasser and then loses them (no sight,
no sound) walks to the last spot it sensed them inside the area, looks around, and gives up. The rest of the chain is unchanged
(alert bark → chase → scold → repeat-offence fight).

## Boundaries & Constraints

**Always:** Hearing radii, occlusion, cap and 360° rule stay as they are. Sneaking or standing still stays Silent, so
only the current vision formula applies. Asleep owners keep `hearingFillTime` (2 s). Hostile mode keeps `hearingFillTime`.
The investigation is **not combat** (`SetCombatState(true)` is not called) and walks at `BaseSpeed`.

**Ask First:** Changing vision fill rates, hearing radii, or making vision-only suspicion investigate too.

**Never:** New brain state; changes to witness mode, theft pursuit or the trespass chase / escalation rules.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Walk past owner | Awake owner, player walking in its area inside walk hearing radius, unseen | Suspicious at ~0.5 s, full awareness at ~1 s → `RespondToTrespasser` | N/A |
| Sneaking | Same, player sneaking | No hearing; vision-only detection as today | N/A |
| Asleep owner | Owner asleep in its area, player walking near | Wakes after ~2 s (unchanged) | N/A |
| Heard, then out of range | Suspicious after hearing, player runs out of hearing radius / sight | Walks (BaseSpeed) to last sensed in-area spot, turns `SearchDuration`, resumes routine | Unreachable spot → searches in place |
| Heard, then sneaks away | Suspicious after hearing, player starts sneaking unseen | Same investigation | N/A |
| Re-heard during search | Investigating, player walks in earshot again | Meter refills from `searchStartAwareness` → `RespondToTrespasser` | N/A |
| Vision-only suspicion | Suspicious from seeing a sneaking player, never heard | Unchanged: turns in place, resumes when meter drains | N/A |

</frozen-after-approval>

## Code Map

- `Assets/_Game/ScriptableObjects/Config/StealthConfigSO.cs` -- `[Header("Hearing")]` holds `hearingFillTime`
- `Assets/_Game/Data/Config/StealthConfig.asset` -- serialized config values
- `Assets/_Game/Scripts/Stealth/StealthDetection.cs` -- pure hearing math (`HearingFillPerSecond`)
- `Assets/_Game/Scripts/AI/EntityPerception.cs` -- `Tick` line ~255 picks the hearing fill time; `IsTrespassing`, `IsAsleep`, `CanHearTarget`, `LastSeenPosition`
- `Assets/_Game/Scripts/AI/EntityBrain.cs` -- `HandleSuspicious` (~703), `TransitionToSuspicious` (~2704), `TransitionToSearching` (~2721), `HandleSearching` (~919; already routes a re-detected trespasser to `RespondToTrespasser`)
- `Assets/Tests/EditMode/StealthDetectionTests.cs` -- pure math tests

## Tasks & Acceptance

**Execution:**
- [x] `StealthConfigSO.cs` + `StealthConfig.asset` -- add `[Min(0.05f)] trespassHearingFillTime = 1f` (tooltip: awake owner hearing a trespasser; asleep keeps `hearingFillTime`); set 1 in the asset -- tunable
- [x] `StealthDetection.cs` -- add pure `HearingFillTime(bool trespassing, bool asleep, float normal, float trespass)` → trespass value only when trespassing and awake -- testable selection
- [x] `EntityPerception.cs` -- use it in `Tick`; update class XML doc -- fast trespass hearing
- [x] `EntityBrain.cs` -- `_trespassHeardSpot` / `_hasTrespassHeardSpot`, cleared in `TransitionToSuspicious`. In `HandleSuspicious`, before the awareness-0 check: while `_perception.IsTrespassing` and sensed (seen or heard) and the target stands inside `TrespassArea` (`Contains`), after it has been heard once this stint, record its position (start recording only on the first `CanHearTarget`). Spot recorded and the target is neither seen nor heard (or the target / mode was dropped) → `TransitionToSearching(investigate: true)`: no `SetCombatState(true)`, speed `BaseSpeed`, destination = the spot, spot cleared -- investigation
- [x] `StealthDetectionTests.cs` -- cases for `HearingFillTime` (trespass awake → trespass, asleep → normal, hostile → normal)
- [x] `Scripts/AI/PERCEPTION.md`, `Scripts/Stealth/CLAUDE.md` -- document the 1 s trespass hearing and the investigation (Values table: `trespassHearingFillTime` 1 s)

**Acceptance Criteria:**
- Given an investigating owner, when the player talks to it, then dialogue opens (not in combat).
- Given the change, when EditMode tests run, then all pass, including the new `HearingFillTime` cases.

## Design Notes

The spot belongs to the brain's Suspicious stint, not to perception. A perception mode switch (the player leaves the area
and becomes a witness target) resets awareness to 0, so the check must run before `HandleSuspicious`'s
`Awareness <= 0` resume. `HandleSearching` already sends a re-detected trespasser to `RespondToTrespasser`. A hostile
goes to Engaging and a witness target is ignored.

## Verification

**Commands:**
- Unity MCP `run_tests` EditMode -- expected: all green
- `read_console` after compile -- expected: no errors

**Manual checks (if no CLI):**
- Play StartingTown, F3 overlay: walk into an owned area near an owner and watch the meter fill in ~1 s. Get heard, then sneak away: the owner walks to the spot, turns and resumes.

## Suggested Review Order

**Investigation of the last heard spot**

- Entry point: records the in-area spot once heard, investigates after 0.5 s of silence.
  [`EntityBrain.cs:734`](../../Assets/_Game/Scripts/AI/EntityBrain.cs#L734)

- Runs before the awareness-0 resume, since a mode switch zeroes the meter.
  [`EntityBrain.cs:721`](../../Assets/_Game/Scripts/AI/EntityBrain.cs#L721)

- investigate flag: no combat state, BaseSpeed, heard spot, awareness kept.
  [`EntityBrain.cs:2776`](../../Assets/_Game/Scripts/AI/EntityBrain.cs#L2776)

- Investigation joins fights and pauses while dialogue / menus are open.
  [`EntityBrain.cs:976`](../../Assets/_Game/Scripts/AI/EntityBrain.cs#L976)

- Review fix: an unrestricted-area trespasser re-sighted in a search is no longer engaged.
  [`EntityBrain.cs:970`](../../Assets/_Game/Scripts/AI/EntityBrain.cs#L970)

- Investigating still witnesses thefts and stays sneak-attackable.
  [`EntityBrain.cs:1169`](../../Assets/_Game/Scripts/AI/EntityBrain.cs#L1169)
  [`EntityBrain.cs:2311`](../../Assets/_Game/Scripts/AI/EntityBrain.cs#L2311)

**Fast trespass hearing**

- Awake trespass owners use the 1 s fill; asleep and hostile keep 2 s.
  [`EntityPerception.cs:255`](../../Assets/_Game/Scripts/AI/EntityPerception.cs#L255)
  [`StealthDetection.cs:115`](../../Assets/_Game/Scripts/Stealth/StealthDetection.cs#L115)

**Peripherals**

- Config value and asset.
  [`StealthConfigSO.cs:51`](../../Assets/_Game/ScriptableObjects/Config/StealthConfigSO.cs#L51)
  [`StealthConfig.asset:30`](../../Assets/_Game/Data/Config/StealthConfig.asset#L30)

- Selection tests.
  [`StealthDetectionTests.cs:263`](../../Assets/Tests/EditMode/StealthDetectionTests.cs#L263)
