---
title: 'Ranged combat style — NPC archers that switch to melee up close'
type: 'feature'
created: '2026-11-17'
status: 'done'
baseline_commit: 'aa0f5cc'
context:
  - '{project-root}/Assets/_Game/Scripts/AI/CLAUDE.md'
  - '{project-root}/Assets/_Game/Scripts/Combat/CLAUDE.md'
  - '{project-root}/.claude/rules/attack-pipeline.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** Styled NPCs only fight in melee; bows are player-only (aim states, nock, arrow hits all go through `PlayerCombat`). Step 4 (last) of the combat AI roadmap.

**Approach:** An NPC whose loadout holds a `BowSO` next to its melee weapon fights at a preferred range with a new `Shoot` tactic (draw → nock → aim tell → release, token-capped, cooldown), switches weapons with hysteresis (melee close in, bow when far again), and fires shooter-owned arrows that the player can block / dodge and that can hit other NPCs. The bandit becomes the first archer: Hunting Bow + Axe, 12 arrows.

## Boundaries & Constraints

**Always:**
- **Loadout:** a `BowSO` in `NPCEntity._startingEquipment` goes into the inventory, the melee weapon (or fists if none) is equipped at start. `NpcArmament.SetRanged(bool)` = one locked switch chain (sheathe current → swap the equipped weapon → draw the new one; `IsSwitchingWeapon` the whole time; controller swap only when safe, `IsInCombat` re-pushed). Bow wielded → **no** melee hitbox registered (weapon and fists never together still holds).
- **Ranged gate (pure):** bow carried (equipped or in the inventory), ≥ 1 ammo item, target **faction-hostile** (brawls / aggro-only fights stay melee), no fists request. Mode hysteresis: melee below `meleeSwitchDistance`, bow above `rangedSwitchDistance` (> melee), never two switches within `weaponSwitchCooldown`.
- **Ammo is finite:** each shot consumes one `bow.ammoItem` from the NPC inventory; none left → melee for the rest of the fight. Down → bow, axe and leftover arrows are lootable; a looted bow = melee only after waking (like the sword today).
- **Shoot tactic** (`CombatAction.Shoot`, appended): needs an attack token (shared cap), shot cooldown elapsed, clear line of fire (no non-hostile body / wall first on the sweep to the aim point — else circle). Holds still facing the target, `IsAiming` on, waits for the nock + `aimTime` (the tell), fires, aim off, token released, circles. Ranged ring = `preferredRange`; approach beyond it + `approachMargin`; back off (kite) inside `rangedBackOffDistance`.
- **Priority:** Drink > Shoot (drink start ends the aim). No guard while a bow is wielded (`EntityGuard` can't guard). No melee swing in ranged mode.
- **Arrow hit (NPC shooter):** same faction filter as `EntityMeleeAttacker` (aggro target accepted); non-hostile hit → arrow absorbed, no damage. `TryReceiveHit(shooter)` → `NotBlocked`: `NotifyHitBy`, `TakeDamage(AttackDamage + bow.damageBonus, shooter)`, stance `bow.ResolvedStanceDamage` / `ResolvedCanKnockDown` (skipped on lethal / knockout); `Blocked` → blocker `ApplyBlockedStanceDamage`; `PerfectBlock` → no shooter stance; `Dodged` → nothing. Hit sound like melee.
- **Accuracy:** aim point = target chest; spread `shotSpreadAngle` via `BowSpreadMath.SampleCone`; lead = target velocity (measured from position deltas) × flight time × `leadFactor`.
- **Animation:** only through `AIAnimationDriver` → `HumanoidAnimationBridge` (`SetAiming` re-pushed every frame, `BowFire`); no new animator params / states. Nock = `SMB_BowAimState` enter via an `IBowNockListener` on the Animator GO (both receivers), with a timeout watchdog. Spine aim = `BowAimCorrection` generalized over an aim-source interface (player: crosshair, NPC: target chest); NPC body faces target + stance yaw while aiming.
- Decisions pure + EditMode-tested; tunables on `CombatStyleSO` / `CombatConfigSO`; no magic numbers. Unstyled / bowless entities unchanged.

**Ask First:** Adding animator states / parameters; any `Entity` / `NPCEntity` value change beyond the approved bandit edits (loadout bow + axe, `DisengageRange` 12 → 18, 12 arrows in its scene `_startingItems`).

**Never:** Feints, punishing the player's recovery, NPC shields, guard-vs-arrows rework (note only), new NPCs, ranged monsters; touching `epics.md` / `sprint-status.yaml`.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Kite & shoot | Bandit vs player at 9 m | Bow drawn, circles ~`preferredRange`, shoots every `shotCooldown` with a visible draw / aim tell | N/A |
| Closing in | Player runs to < `meleeSwitchDistance` | Bow sheathed, axe drawn, melee tactics (circle / token swing) | No switch while mid-shot / swinging / staggered — waits |
| Back out | Player retreats past `rangedSwitchDistance`, cooldown elapsed | Axe sheathed, bow drawn, shooting resumes | Inside the band → keeps current weapon |
| Defend | Player blocks / dodges an arrow | Blocked: no damage, player stance cost; dodge: miss | N/A |
| Ally in line | Non-hostile NPC between bandit and target | No shot; repositions | Arrow that still hits it: absorbed, no damage |
| Out of arrows | 12 shots fired | Melee for the rest of the fight | N/A |
| Token cap | 2 melee attackers hold tokens on the target | Archer circles at range, shoots when a token frees | N/A |
| Low HP | Health ≤ drink threshold with a potion | Drink beats Shoot | N/A |
| Brawl | Aggro-only (non-hostile) target | Melee only | N/A |
| Unstyled / no bow | Guards, monsters | Unchanged behaviour | N/A |

</frozen-after-approval>

## Code Map

- `Scripts/AI/NpcArmament.cs` -- loadout grant, `ApplyMode` (hitboxes / controller), switch chain, down / wake
- `Scripts/AI/CombatTacticsRules.cs`, `EntityCombatTactics.cs` -- pure decide + movement; add mode + Shoot
- `Scripts/AI/EntityBrain.cs:706` `HandleEngage`, `:777` `IsAttackReady`, `:797` `EndTactics` (single release point)
- `Scripts/AI/EntityMeleeAttacker.cs:314` `HandleHit` / `IsHostileTarget` -- faction filter + hit sound to reuse
- `Scripts/AI/EntityGuard.cs` -- can't-guard list; `EntityAnimationEventReceiver.cs` -- receiver on Animator GO
- `Scripts/Combat/ArrowProjectile.cs` (owner + onHit callback — reuse as is), `PlayerCombat.cs:861` `FireArrow` (reference), `BowSpreadMath.cs`, `SMB_BowAimState.cs`, `AnimationEventReceiver.cs:54`, `BowAimCorrection.cs` (player-bound fields `_stateManager`, `_playerCombat`)
- `Scripts/Core/Animations/AIAnimationDriver.cs`, `HumanoidAIAnimationDriver.cs`, `HumanoidAnimationBridge.cs:186` (`SetAiming`, `PlayBowFire`, `ResetBowFireTrigger`)
- `ScriptableObjects/Entities/CombatStyleSO.cs`, `ScriptableObjects/Config/CombatConfigSO.cs`
- `Data/NPCs/Bandit/NPC_Bandit.asset`, `Data/Entities/CombatStyles/CombatStyle_Bandit.asset`, `Scenes/StartingTown.unity` (bandit instance), `Prefabs/Entities/Humanoids/NPC_base Variant.prefab`, Player prefab (`BowAimCorrection`)

## Tasks & Acceptance

**Execution:**
- [x] `Scripts/Combat/IBowNockListener.cs` + `IBowAimSource.cs` (new) -- nock callback; `IsBowAiming` + `TryGetBowAimPoint` -- decouple SMB / correction from the player
- [x] `SMB_BowAimState.cs`, `AnimationEventReceiver.cs`, `EntityAnimationEventReceiver.cs` -- SMB resolves `IBowNockListener`; entity receiver forwards to `EntityArcher`
- [x] `BowAimCorrection.cs` + `PlayerCombat.cs` -- read an `IBowAimSource` on the same root (PlayerCombat implements it); Player prefab rewired; same calibration on the NPC rig (verify in play)
- [x] `AIAnimationDriver.cs`, `HumanoidAIAnimationDriver.cs` -- virtual `SetAiming`, `TriggerBowFire`, `ClearBowFire` (monsters no-op)
- [x] `CombatStyleSO.cs` -- `[Header("Ranged")]` `preferredRange`, `rangedBackOffDistance`, `meleeSwitchDistance`, `rangedSwitchDistance`, `weaponSwitchCooldown`, `shotCooldown`, `aimTime`, `shotSpreadAngle`, `leadFactor`; `OnValidate` ordering clamps
- [x] `CombatConfigSO.cs` -- `npcAimStanceYaw` (45), `npcBowNockTimeout` -- shared rig values
- [x] `Scripts/AI/CombatTacticsRules.cs` -- ranged inputs; `WantsRanged` (gate + hysteresis + cooldown); ranged branch of `Decide` (Drink first, then Approach / BackOff / Shoot / Circle); `Scripts/Combat/RangedAimMath.cs` (pure lead point)
- [x] `Scripts/AI/EntityArcher.cs` (new, NPC root) -- aim / nock / watchdog / fire / ammo / clear-shot sweep / target velocity / hit resolution; `IBowAimSource`; `IsAiming`, `CanShoot`, `End()`; `OnDisable → End`
- [x] `Scripts/AI/NpcArmament.cs` -- bow kept in inventory at grant; `HasRangedWeapon`, `IsRanged`, `SetRanged(bool)` chain; bow mode registers no hitbox; down / wake / save keep working with either weapon equipped
- [x] `Scripts/AI/EntityMeleeAttacker.cs` -- expose `IsHostileTarget` + hit sound for the archer
- [x] `Scripts/AI/EntityCombatTactics.cs` -- fill ranged inputs, request mode switches, run Shoot (token, hold, archer), ring = `preferredRange` in ranged mode
- [x] `Scripts/AI/EntityBrain.cs` -- no Attacking transition in ranged mode; `EndTactics` ends the archer; `DebugStateName` `Engaging/Shoot`
- [x] `Scripts/AI/EntityGuard.cs` -- can't guard while a bow is wielded
- [x] `NPC_base Variant.prefab` -- add `EntityArcher`
- [x] Data -- bandit: `_startingEquipment` = Hunting Bow + Axe, `DisengageRange` 18, 12 `Item_Arrow` (scene `_startingItems`); `CombatStyle_Bandit` ranged values (preferred 9, back-off 6, melee 3.5, ranged 6.5, switch cd 4 s, shot cd 2.5 s, aim 0.6 s, spread 2.5°, lead 0.6)
- [x] `Tests/EditMode/CombatTacticsRangedTests.cs`, `RangedAimMathTests.cs` -- every matrix row at rule level + hysteresis, cooldowns, gate, Drink / token priority, lead math
- [x] Docs -- `Scripts/AI/CLAUDE.md` (**Ranged** section, NPC equipment update), `Scripts/Combat/CLAUDE.md` (bow: shooter-owned NPC path, aim-source), Controllers `CLAUDE.md` (bow states no longer player only), `deferred-work.md` (roadmap done, guard-vs-arrows note)

**Acceptance Criteria:**
- Given the bandit spotting the player in open ground, when the player stays at range, then it draws the bow, keeps its distance and fires telegraphed arrows that hurt, can be blocked and dodged, with the bow pointing at the player (spine aim).
- Given the player closes in, when within the melee band, then the bandit swaps to the axe (sheathe + draw clips) and fights in melee; retreating far enough brings the bow back, without switch flicker at the band edges.
- Given the bandit is killed or knocked out, when looted, then the bow, axe and remaining arrows are in its inventory.
- Given a Company guard, spider or wolf, when it fights, then behaviour is unchanged.

## Spec Change Log

## Design Notes

Implementation additions (review pass): `EntityPerception` LOS now ignores the entity's own colliders for every entity — the
archer's side-on aim put its own head in the eye ray (mid-shot sight loss in play); `npcTargetVelocitySmoothTime` makes the lead
frame-rate independent; `RangedRequested` re-syncs to the wielded weapon (stale request after wake / restore / fists left a bow
user unable to fight); the gate and "decide the mode only between actions" are pure (`CanUseRanged`, `ShouldDecideWeaponMode`).
The Player prefab needed no rewiring: `PlayerCombat` is on the same GO as `BowAimCorrection` (resolved via `GetComponent`).

Arrows reuse `ArrowProjectile` unchanged — its `owner` + `onHit` callback already make hits shooter-owned; only the resolver is new. Tokens: one shared pool keeps "at most N threats at once"; an archer holds one only for ~1.5 s per shot. Ranged mode is skipped against non-hostile targets so knockout brawls (loot, toll, trespass) keep their melee rules.

## Verification

**Commands:**
- Unity Test Runner EditMode (`mcp__UnityMCP__run_tests`) -- expected: all pass incl. new tests
- `read_console` after compile -- expected: no errors

**Manual checks:**
- Play StartingTown vs the bandit: kiting, shots, block / dodge, melee swap and back, out-of-arrows, loot; player bow aim unchanged; a guard fight unchanged.

## Suggested Review Order

**Weapon choice (entry point)**

- Mode decided between actions only; requests the bow / melee switch on the armament.
  [`EntityCombatTactics.cs:297`](../../Assets/_Game/Scripts/AI/EntityCombatTactics.cs#L297)

- Pure gate + hysteresis + cooldown; brawls and empty quivers stay melee.
  [`CombatTacticsRules.cs:112`](../../Assets/_Game/Scripts/AI/CombatTacticsRules.cs#L112)
  [`CombatTacticsRules.cs:126`](../../Assets/_Game/Scripts/AI/CombatTacticsRules.cs#L126)

**Switch chain (NpcArmament)**

- One step per frame: wait, sheathe, swap while sheathed, rebuild + draw.
  [`NpcArmament.cs:286`](../../Assets/_Game/Scripts/AI/NpcArmament.cs#L286)

- Bow in hand registers no hitbox; request re-syncs to what is wielded.
  [`NpcArmament.cs:437`](../../Assets/_Game/Scripts/AI/NpcArmament.cs#L437)
  [`NpcArmament.cs:450`](../../Assets/_Game/Scripts/AI/NpcArmament.cs#L450)

- Bow next to a melee weapon is carried, not equipped; looted weapon falls back.
  [`NpcArmament.cs:249`](../../Assets/_Game/Scripts/AI/NpcArmament.cs#L249)
  [`NpcArmament.cs:377`](../../Assets/_Game/Scripts/AI/NpcArmament.cs#L377)

**Shoot tactic**

- Ranged branch: kite, keep the preferred ring, shoot on a free token.
  [`CombatTacticsRules.cs:95`](../../Assets/_Game/Scripts/AI/CombatTacticsRules.cs#L95)

- Shoot takes a token from the shared pool, then raises the bow.
  [`EntityCombatTactics.cs:274`](../../Assets/_Game/Scripts/AI/EntityCombatTactics.cs#L274)

- Held still mid-shot; a drink wish cuts the aim (Drink > Shoot).
  [`EntityCombatTactics.cs:111`](../../Assets/_Game/Scripts/AI/EntityCombatTactics.cs#L111)

- Side-on facing while aiming; the spine correction does the rest.
  [`EntityCombatTactics.cs:451`](../../Assets/_Game/Scripts/AI/EntityCombatTactics.cs#L451)

**Archer (shot timeline and hits)**

- Aim → nock (SMB or watchdog) → aim tell → release → hold → lower.
  [`EntityArcher.cs:150`](../../Assets/_Game/Scripts/AI/EntityArcher.cs#L150)

- Release re-checks the line, spends the arrow, leads + spreads the shot.
  [`EntityArcher.cs:204`](../../Assets/_Game/Scripts/AI/EntityArcher.cs#L204)

- Shooter-owned hit: faction filter, block / parry / dodge, NotifyHitBy, stance.
  [`EntityArcher.cs:258`](../../Assets/_Game/Scripts/AI/EntityArcher.cs#L258)

- Clear-shot sweep: walls and non-hostile bodies hold the shot.
  [`EntityArcher.cs:294`](../../Assets/_Game/Scripts/AI/EntityArcher.cs#L294)

**Brain, guard, perception**

- No melee swing with a bow; untactical fights ask for melee.
  [`EntityBrain.cs:779`](../../Assets/_Game/Scripts/AI/EntityBrain.cs#L779)
  [`EntityBrain.cs:869`](../../Assets/_Game/Scripts/AI/EntityBrain.cs#L869)

- A bow can't block.
  [`EntityGuard.cs:121`](../../Assets/_Game/Scripts/AI/EntityGuard.cs#L121)

- LOS ignores own colliders (archer's head swung into the eye ray).
  [`EntityPerception.cs:403`](../../Assets/_Game/Scripts/AI/EntityPerception.cs#L403)

**Animation and spine aim**

- Correction reads a same-GO aim source; idle NPCs skip it.
  [`BowAimCorrection.cs:50`](../../Assets/_Game/Scripts/Combat/BowAimCorrection.cs#L50)
  [`BowAimCorrection.cs:112`](../../Assets/_Game/Scripts/Combat/BowAimCorrection.cs#L112)

- Player aim source: crosshair point on the camera ray.
  [`PlayerCombat.cs:921`](../../Assets/_Game/Scripts/Combat/PlayerCombat.cs#L921)

- Nock via an interface on the Animator GO; NPC receiver forwards to the archer.
  [`SMB_BowAimState.cs:15`](../../Assets/_Game/Scripts/Combat/SMB_BowAimState.cs#L15)
  [`EntityAnimationEventReceiver.cs:73`](../../Assets/_Game/Scripts/AI/EntityAnimationEventReceiver.cs#L73)

- Driver hooks (monsters no-op).
  [`AIAnimationDriver.cs:37`](../../Assets/_Game/Scripts/Core/Animations/AIAnimationDriver.cs#L37)

**Peripherals**

- Lead math (frame-rate independent smoothing).
  [`RangedAimMath.cs:16`](../../Assets/_Game/Scripts/Combat/RangedAimMath.cs#L16)

- Style and config tunables.
  [`CombatStyleSO.cs:84`](../../Assets/_Game/ScriptableObjects/Entities/CombatStyleSO.cs#L84)
  [`CombatConfigSO.cs:153`](../../Assets/_Game/ScriptableObjects/Config/CombatConfigSO.cs#L153)

- Rule and math tests.
  [`CombatTacticsRangedTests.cs:1`](../../Assets/Tests/EditMode/CombatTacticsRangedTests.cs#L1)
  [`RangedAimMathTests.cs:1`](../../Assets/Tests/EditMode/RangedAimMathTests.cs#L1)
