---
title: 'Drawn sword: Mixamo sword-and-shield locomotion (full body) + shield block hold'
type: 'feature'
created: '2026-11-25'
status: 'done'
baseline_commit: '2c4b872e'
context:
  - '{project-root}/Assets/_Game/Art/Characters/Humanoids/Controllers/CLAUDE.md'
  - '{project-root}/Assets/_Game/Art/Characters/Humanoids/Animations/Combat/CLAUDE.md'
  - '{project-root}/Assets/_Game/Scripts/Core/Animations/CLAUDE.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** With the sword drawn, the legs play the generic walk / idle (parallel feet) under the pack's bladed
`sword shield idle` upper body, so every sword attack ends with a leg snap and moving looks unrelated to the combo.
The shield block hold comes from a separate download of the pack's own `block idle`.

**Approach:** Base `LockOn Locomotion` blends (new float `CombatLocomotion`, follows `IsInCombat`) between today's tree
and a combat tree with the same 9 positions whose base clips are copies of the generic ones; the sword override maps
them to the pack (idle, walk, run, strafes). For archetypes flagged full-body, the UpperBody carry pose fades out
while Base is in `LockOn Locomotion`, so the pack's arms play. The sword override maps `Block Idle` → pack `block idle`.

## Boundaries & Constraints

**Always:**
- Sword override map: idle → `sword shield idle` (the existing CombatIdle clip), walk → `walk`, walk back → `walk (2)`,
  run → `run`, run back → `run (2)`, strafe walk L / R → `strafe (2)` / `strafe`, strafe run L / R → `strafe (3)` /
  `strafe (4)`; `Block Idle` → `block idle`. Positions and time scales as today's tree.
- Pack imports: Humanoid, avatar from the model (as `sword and shield idle.fbx`); loops with Loop Time + Loop Pose,
  root rotation and Y baked on Original, root XZ **not** baked (in place: `applyRootMotion` is off). FBX + `.meta`
  moved with GUIDs kept into `Combat/one handed/sword and shield/`.
- Unarmed, axe, two-handed, bow, sheathed sword and NPCs without the flag: unchanged clips and UpperBody weight.
- Full-body rule: UpperBody weight 0 only when in combat, flag set, Base heading into `LockOn Locomotion`, and no
  UpperBody action; draw / sheathe / block / drink still play over it. Same rule for the player and NPCs.
- Controller changes made by an idempotent editor builder, never by hand.

**Ask First:**
- **After the first in-game look (player walk / run / strafe / block / attack exit with the sword): show it to the
  user before tuning time scales, positions or the blend time.** If rejected, stop and report.
- Using the turn, 180-turn, block raise / lower, impact, death or kick clips.

**Never:** Overriding the generic locomotion clips (`Idle`, `Walking`, …) in the sword override (they also play
sheathed); a controller swap on draw; root motion for locomotion; changes to sneak, swim or attack states.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Drawn sword, idle / move | IsInCombat, sword | Pack idle / walk / run / strafes full body; attack exits blend into the pack idle legs | N/A |
| Sheathed sword | Not in combat | Generic locomotion, as today | N/A |
| Draw / sheathe / block / drink | UpperBody action | Action plays over the pack locomotion (weight 1) | N/A |
| Sneak in combat | Sneak states | Carry pose over sneak legs, as today | N/A |
| Other weapons drawn | Unarmed / axe / 2H / bow | Same clips and carry pose as today | N/A |
| Sword NPC in combat | Guard with sword | Pack locomotion full body | N/A |
| Sword + shield block | IsBlocking, shield | Pack `block idle` | N/A |

</frozen-after-approval>

## Code Map

- `Assets/mixamo packs/Sword and Shield Pack/` -- untracked source; 8 locomotion clips + `block idle` move out
- `Art/Characters/Humanoids/Controllers/Humanoid_Template.controller` -- Base `LockOn Locomotion` 2D tree (Idle, Walking, Running, Walking Backwards, Running Backwards, Left/Right strafe walking/running); UpperBody keeps its own copy
- `Combat/one handed/Sword_AnimatorOverride.overrideController` -- sword keys
- `Scripts/Core/Animations/UpperBodyWeight.cs`, `HumanoidAnimationBridge.cs`, `HandPoseDriver.cs` (reads archetype via `EquipmentVisuals`, player + NPC), `HumanoidAIAnimationDriver.cs`; `Scripts/Player/PlayerAnimationDriver.cs`
- `ScriptableObjects/Items/Weapons/WeaponArchetypeSO.cs`, `Data/Items/Weapons/Archetypes/Archetype_OneHandedSword.asset`
- `Editor/Characters/HandPoseBuilder.cs` / `OffhandArmLayerBuilder.cs` -- builder pattern
- `Tests/EditMode/UpperBodyWeightTests.cs`, `HumanoidTemplateControllerTests.cs`, `SwordShieldComboTests.cs`

## Tasks & Acceptance

**Execution:**
- [x] `Combat/one handed/sword and shield/` -- move + import the 9 pack clips (clip names `sword shield walk` …) -- clips
- [x] `Editor/Characters/CombatLocomotionBuilder.cs` -- `Tools/Characters/Build Combat Locomotion`: `.anim` copies of the 9 generic clips (`Animations/CombatLocomotion/`), combat 2D tree, Base `LockOn Locomotion` → 1D tree on `CombatLocomotion` (0 today's tree, 1 combat tree), param (Base locomotion is detected by state name, `HumanoidAnimationBridge.IsBaseLocomotion`: no new tag) -- reproducible controller change
- [x] `Sword_AnimatorOverride` -- 9 combat keys + `Block Idle` -- wiring
- [x] `WeaponArchetypeSO.fullBodyCombatLocomotion` + sword archetype true -- flag
- [x] `UpperBodyWeight` (new input), bridge (`SetCombatLocomotion` damped, `IsBaseLocomotion`, flag property written by `HandPoseDriver`), both drivers -- runtime
- [x] EditMode tests (`SwordShieldLocomotionTests`, `UpperBodyWeightTests`, `HumanoidTemplateControllerTests` block hold): weight rule, controller tree / param / tag, override keys, pack imports (loop, XZ not baked) -- regression net
- [x] Play check → **Ask First checkpoint** (approved 2026-11-25); then docs (`Player/CLAUDE.md`, `Controllers/CLAUDE.md`, `Combat/CLAUDE.md`, `Core/Animations/CLAUDE.md`, deferred-work)

**Acceptance Criteria:**
- Given the sword drawn, when the player stands, walks, runs and strafes, then the pack clips play on the whole body and a finished attack blends into the pack idle legs.
- Given the sword sheathed or another weapon drawn, then locomotion and the carry pose are unchanged.
- Given the EditMode suite, when run, then it passes.

## Design Notes

Measurements (headless Blender, pack clips): walk 1.52 m/s, walk back 1.15, run 4.19, run back 4.44, strafe walk
1.1–1.24, strafe run 3.4–3.5, all loop clean (0° first/last). Player blend: 0.5 = walkSpeed 3 m/s, 1.0 = runSpeed 6 — the
generic in-place clips already play below their speed; tune time scales only after the look. Existing `Block Idle.fbx`
= the same Mixamo animation as pack `block idle` (43 frames, same hip path, hands within 7 cm). Copies keep distinct
override keys: the sword override applies while sheathed too.

## Verification

**Commands:**
- Unity MCP `run_tests` (EditMode) -- expected: all green
- `read_console` after import and the builder run -- expected: no errors

**Manual checks:**
- Play: drawn sword idle / walk / run / strafe / block, attack exit, sheathe; one sword NPC in combat; axe and bow drawn

## Implementation Notes (2026-11-25)

- Play (StartingTown, `Weapon_ShortIronSword` + round shield): drawn idle = `sword shield idle` on Base at
  `CombatLocomotion` 1, UpperBody weight 0. Each of the 8 velocity points plays its pack clip at weight 1 (driver paused,
  floats set directly). Sheathed sword = generic `Idle`. Drawn axe = `Combat Idle` copy under `SwordIdle` (weight 1).
  User approved the look.
- Attack exits: `VelocityZ` stays 0 through every attack → locomotion blend, so the moving pack clips add no root travel
  to `ActionRootMotion`. Single cut 0.16 m, mashed chain 1.18 m at this spot (near the docks: not comparable with the
  combo spec's open-ground table).
- Sword NPC (RacketGuard, `SetInCombat` + `SetCombatState`): pack idle full body, flag true. Forced fists
  (`ForceDefaultController`): flag false, `Unarmed Idle Guard` at UpperBody weight 1.
- Review patches: flag also requires the weapon in hand and its own override (not NPC fists), reset in
  `HandPoseDriver.OnDisable`; `_combatLocomotion` reset on revive; builder validates every child (clip, unique and
  file-safe name) before writing anything and detects its wrapper by name; tests loop over every free clip and assert the
  archetypes exist. Rejected: foot slide (approved on look), shieldless pack arm (as the idle), carry pose during
  Landing / GetHit / sneak (intended), footstep events (distance-based, none on the clips).
- EditMode: 2015 / 2015.

## Suggested Review Order

**Controller: free / combat locomotion**

- Entry point: one builder makes the wrapper, the copies and the combat tree, idempotently.
  [`CombatLocomotionBuilder.cs:38`](../../Assets/_Game/Editor/Characters/CombatLocomotionBuilder.cs#L38)

- Validation runs before any asset is written, so a throw leaves nothing behind.
  [`CombatLocomotionBuilder.cs:94`](../../Assets/_Game/Editor/Characters/CombatLocomotionBuilder.cs#L94)

- Copies are distinct override keys; GUIDs kept on rebuild.
  [`CombatLocomotionBuilder.cs:127`](../../Assets/_Game/Editor/Characters/CombatLocomotionBuilder.cs#L127)

- Sword maps the copies and the block hold to the pack.
  [`Sword_AnimatorOverride.overrideController`](../../Assets/_Game/Art/Characters/Humanoids/Animations/Combat/one%20handed/Sword_AnimatorOverride.overrideController)

**Runtime: full-body rule**

- Carry pose off only in combat locomotion with the flag; actions still win.
  [`UpperBodyWeight.cs:21`](../../Assets/_Game/Scripts/Core/Animations/UpperBodyWeight.cs#L21)

- Flag needs the weapon in hand with its own override (NPC fists excluded).
  [`HandPoseDriver.cs:62`](../../Assets/_Game/Scripts/Core/Animations/HandPoseDriver.cs#L62)

- Bridge combines the flag with Base heading into `LockOn Locomotion`.
  [`HumanoidAnimationBridge.cs:176`](../../Assets/_Game/Scripts/Core/Animations/HumanoidAnimationBridge.cs#L176)

- Drivers step the float every frame, re-pushed after a controller reset.
  [`PlayerAnimationDriver.cs:89`](../../Assets/_Game/Scripts/Player/PlayerAnimationDriver.cs#L89)
  [`HumanoidAIAnimationDriver.cs:87`](../../Assets/_Game/Scripts/Core/Animations/HumanoidAIAnimationDriver.cs#L87)

- Data: sword-only archetype flag.
  [`WeaponArchetypeSO.cs:44`](../../Assets/_Game/ScriptableObjects/Items/Weapons/WeaponArchetypeSO.cs#L44)

**Tests and docs**

- Tree shape, override map, imports, flag ownership.
  [`SwordShieldLocomotionTests.cs:68`](../../Assets/Tests/EditMode/SwordShieldLocomotionTests.cs#L68)

- Controller rules for the new locomotion.
  [`Controllers/CLAUDE.md:68`](../../Assets/_Game/Art/Characters/Humanoids/Controllers/CLAUDE.md#L68)
