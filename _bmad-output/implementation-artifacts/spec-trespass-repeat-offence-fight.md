---
title: 'Trespass escalation — repeat trespassers of the same area get a fight'
type: 'feature'
created: '2026-11-17'
status: 'done'
baseline_commit: 'f0862f4b721b1f4a5074d0383af1b289baf11fca'
context: ['{project-root}/_bmad-output/implementation-artifacts/spec-restricted-areas-trespass.md', '{project-root}/.claude/rules/attack-pipeline.md']
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** A normal restricted area only ever warns, chases and scolds (then 15 s grace); nothing remembers past offences, so the player can trespass the same place forever at no cost.

**Approach:** Each `RestrictedArea` remembers how many times the player was scolded there (runtime, expiring after one in-game day). Once the count reaches the area's `Warnings Before Fight` (default 1), an owner who spots the player there again skips the chase / scold and starts an armed brawl — the existing knockdown + gold looting are the penalty. `Warnings Before Fight = 0` replaces the `_instantAggression` flag.

## Boundaries & Constraints

**Always:** A warning = one completed catch → scold (at the moment grace starts), counted on the area (shared by all its owners). Warnings expire `trespassWarningExpiryHours` (StealthConfigSO, default 24) in-game hours after the latest one; no `GameClock.Active` → they never expire. The player being knocked out during an escalation fight resets the area's warnings to 0. Escalation fight = `StartBrawl(player)` (armed), continues by normal combat / disengage rules even if the player leaves the area. Owner that can't brawl (passive / no attacker) → old warn / chase / scold flow. Pure rules in `RestrictedAreaRules` with EditMode tests. GameLog + TAG.

**Ask First:** Changing the knockout gold formula, the grace duration, or the theft (item / container) chase.

**Never:** Saving warnings; HUD / reputation / bounty; per-NPC warning memory; lethal escalation; changing the trespass-exit chase stop.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| First offence | 0 warnings, threshold 1 | Warn → chase → scold → grace; area warnings = 1 | N/A |
| Second offence | 1 warning, spotted again (after grace / re-entry) | Punish bark, armed brawl, no chase / scold | N/A |
| Knocked out | Player KO'd in the escalation fight | Existing looting (gold); area warnings = 0 | N/A |
| Player wins / flees fight | Escalation fight ends, player conscious | Warnings kept → next spotting fights again | N/A |
| Expired | Warning 25 in-game h old | Treated as 0 → normal scold | N/A |
| Clock went back | Load a save earlier than the warning | Warning ignored (treated as expired) | N/A |
| Threshold 0 | Area set to 0 | Fight on first spotting (old instant aggression) | N/A |
| Other area | Warned in warehouse, spotted on docks | Docks have own count → scold | N/A |
| Passive owner | DetectionRange 0, threshold reached | Old chase / scold flow | N/A |

</frozen-after-approval>

## Code Map

- `Scripts/World/RestrictedArea.cs` -- `_instantAggression` (to replace), `StartGrace`, `InstantAggression`, gizmo colour
- `Scripts/World/RestrictedAreaRules.cs` -- pure rules home (`RestrictedAreaRulesTests`)
- `Scripts/AI/EntityBrain.cs:775` -- `RespondToTrespasser` (instant-aggression branch → fight), `TransitionToConfronting:1107` (scold = grace start), `StartBrawl:1756`, `DisengageFromCombat`, Looting entry (player KO)
- `ScriptableObjects/Config/StealthConfigSO.cs:107` -- trespass tunables
- `ScriptableObjects/Entities/NPC/WitnessProfileSO.cs:39` -- trespass bark sets (fallback pattern)
- `Scripts/Core/Time/GameClock.cs` -- `Active`, `Now` (world seconds); `GameTime.SECONDS_PER_HOUR`
- `Scenes/StartingTown.unity` -- `RA_Warehouse`, `RA_Docks` (both `_instantAggression: 0` → default 1, no migration)

## Tasks & Acceptance

**Execution:**
- [x] `Scripts/World/RestrictedAreaRules.cs` -- add pure `ActiveWarnings(int count, bool hasClock, double now, double lastWarningAt, float expiryHours)` (0 when expired or `now < lastWarningAt`; no clock → count) and `ShouldFight(int activeWarnings, int warningsBeforeFight)` -- testable
- [x] `Scripts/World/RestrictedArea.cs` -- replace `_instantAggression` with `[Min(0)] _warningsBeforeFight = 1` (tooltip: 0 = attack on sight); runtime `_warnings` + `_lastWarningAt` (world seconds); `RecordWarning()` (called from `StartGrace` path only on a scold), `ClearWarnings()`, `ShouldFightTrespasser` (rules + expiry config passed in or read via property); gizmo red when threshold 0 or warnings active; update class doc -- area memory
- [x] `ScriptableObjects/Config/StealthConfigSO.cs` -- `trespassWarningExpiryHours = 24` (+ OnValidate clamp ≥ 0) -- tunable
- [x] `ScriptableObjects/Entities/NPC/WitnessProfileSO.cs` + `Data/NPCs/Barks/Barks_TrespassPunish.asset` (NEW + meta, assigned on `WitnessProfile_Humanoid`) -- `TrespassPunishBarks` (fallback to alert barks): "I warned you!", "You were told to stay out!", "Back again? Now you pay." -- data
- [x] `Scripts/AI/EntityBrain.cs` -- `RespondToTrespasser`: if `area.ShouldFightTrespasser` → bark (punish set when warnings > 0, alert set at threshold 0), remember `_trespassFightArea`, `StartBrawl(trespasser)`; false → fall through to chase. Scold (`TransitionToConfronting` with `_pursuitArea`) → `RecordWarning()` once per catch. When the player is knocked out while this brain fights for `_trespassFightArea` → `ClearWarnings()`; clear `_trespassFightArea` whenever the fight ends -- behaviour
- [x] `Assets/Tests/EditMode/RestrictedAreaRulesTests.cs` -- matrix cases: expiry edge (23.9 h / 24 h), clock back, no clock, threshold 0 / 1 / 2 -- edges
- [x] `Scripts/AI/CLAUDE.md`, `Scripts/World/CLAUDE.md` -- replace instant-aggression wording with warnings / escalation -- docs

**Acceptance Criteria:**
- Given the WarehouseGuard scolded the player once, when the player stays or returns after the grace and is spotted, then the guard shouts a punish line and attacks with its weapon.
- Given that fight, when the player is knocked out, then the guard loots gold and the next visit gets a scold again.
- Given EditMode tests, when run, then all pass and the console has no compile errors.

## Design Notes

Warnings live on the area (not the NPC) so every owner of the docks shares them. The reset hook sits where the brain already learns the knockout (Looting start or disengage with an unconscious player target) — whichever brain held `_trespassFightArea` clears it; a defending guard without it doesn't matter since the area's own owners are fighting. The scene keeps a stale `_instantAggression: 0` key until re-saved — harmless.

## Verification

**Commands:**
- Unity MCP `run_tests` (EditMode) -- expected: all green
- `read_console` after compile -- expected: no errors

**Manual checks:**
- StartingTown: get scolded in the warehouse, wait 15 s inside → fight; lose → gold looted; re-enter → scold.

## Suggested Review Order

**Escalation decision (warned enough → fight)**

- Entry point: already-warned trespasser gets an armed brawl; passive owners fall back to the chase.
  [`EntityBrain.cs:792`](../../Assets/_Game/Scripts/AI/EntityBrain.cs#L792)

- Area memory: active warnings filtered by in-game expiry, threshold replaces instant aggression.
  [`RestrictedArea.cs:126`](../../Assets/_Game/Scripts/World/RestrictedArea.cs#L126)

- Pure rules: expiry (no clock, clock rewound) and threshold.
  [`RestrictedAreaRules.cs:34`](../../Assets/_Game/Scripts/World/RestrictedAreaRules.cs#L34)

**Recording and resetting warnings**

- Warning recorded only once the scold dialogue really opens (review patch), capped at the threshold.
  [`EntityBrain.cs:1151`](../../Assets/_Game/Scripts/AI/EntityBrain.cs#L1151)

- Leaving combat with the player lying unconscious = punished → area warnings reset.
  [`EntityBrain.cs:1271`](../../Assets/_Game/Scripts/AI/EntityBrain.cs#L1271)

- Hooked on the single combat-state writer, like the theft refusal brawl.
  [`EntityBrain.cs:85`](../../Assets/_Game/Scripts/AI/EntityBrain.cs#L85)

- Retarget onto another attacker drops the trespass fight (review patch).
  [`EntityBrain.cs:1930`](../../Assets/_Game/Scripts/AI/EntityBrain.cs#L1930)

**Peripherals**

- Per-area threshold field (default 1, 0 = on sight).
  [`RestrictedArea.cs:38`](../../Assets/_Game/Scripts/World/RestrictedArea.cs#L38)

- Expiry tunable (24 in-game hours).
  [`StealthConfigSO.cs:110`](../../Assets/_Game/ScriptableObjects/Config/StealthConfigSO.cs#L110)

- Punish barks, falling back to the alert set.
  [`WitnessProfileSO.cs:59`](../../Assets/_Game/ScriptableObjects/Entities/NPC/WitnessProfileSO.cs#L59)

- Expiry / threshold tests.
  [`RestrictedAreaRulesTests.cs:100`](../../Assets/Tests/EditMode/RestrictedAreaRulesTests.cs#L100)
