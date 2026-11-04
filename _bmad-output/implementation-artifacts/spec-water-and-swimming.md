---
title: 'Water volumes and player surface swimming'
type: 'feature'
created: '2026-11-04'
status: 'done'
baseline_commit: '25a06ce'
context: ['{project-root}/_bmad-output/project-context.md']
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** The game has no water: no URP water shader exists (`M_Water_01` is HDRP) and the player has no swim mode, so lakes and rivers can't be built. Swim clips are downloaded but not imported or wired.

**Approach:** A reusable `Water_Volume` prefab (trigger box + animated surface) on the existing `Water` layer. The player switches to a surface-only swim mode when deep enough: gravity off, held at the surface, swim clips, most actions gated. Hand-sculpted basins stay with Valentin.

## Boundaries & Constraints

**Always:** Normal swimming is free; Sprint swims faster and drains stamina. Gates go through `PlayerStateManager`. Animator writes go through `HumanoidAnimationBridge`. Tunables live in `PlayerConfigSO`. `Humanoid_Template` is edited through the `AnimatorController` API and clip imports through `ModelImporter.clipAnimations`, never raw YAML. NPC behaviour is unchanged.

**Ask First:** Any terrain edit; any change that makes swim gates alter the pure `Evaluate*` signatures tested today.

**Never:** Diving or underwater camera effects, breath meter, drowning or swim damage, wading slowdown, climb-out animations, NPCs or monsters swimming, buoyancy or a water physics simulation, HDRP assets, editing terrain by script.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Wade in | Feet depth reaches `swimEnterDepth` | Swimming starts, sneak ends, splash cue | N/A |
| Reach shore | Rising ground lifts feet, depth < `swimExitDepth` | Walking resumes, no fall clip, exit cue | N/A |
| Shallows | Depth stays below `swimEnterDepth` | Normal walking, footsteps | N/A |
| Fall or jump in | High-speed entry | Fall speed dropped, rises to float height at `swimRiseSpeed` | N/A |
| Enter with weapon drawn | `IsInCombat`, attacking, blocking, aiming or mid-switch | Attack / aim / block ended, instantly sheathed | N/A |
| Enter while dodging | `IsDodging` | Swim starts when the dodge ends | N/A |
| Locked on | Entering, or pressing lock-on, while swimming | Lock-on released / refused | N/A |
| Stamina empties | Sprint held | Normal swim speed until Sprint is released and pressed again | No log spam |
| Controller reset | Equip or load while swimming | Swim animation stays (bool pushed every frame) | N/A |
| Volume disappears | Region unloads or load teleports away | Swimming ends next frame | N/A |
| Death while swimming | `SetDead(true)` | Swim cleared, Death plays, body sinks | N/A |

</frozen-after-approval>

## Code Map

- `Scripts/Player/PlayerController.cs` -- gravity, jump, speed; swim branch goes here
- `Scripts/Player/PlayerStateManager.cs` -- gates, `IsAirborne`, `SuppressFootsteps`
- `Scripts/Player/PlayerAnimationDriver.cs`, `Scripts/Core/Animations/HumanoidAnimationBridge.cs` -- animator path
- `Scripts/Player/LockOnSystem.cs` -- lock acquisition
- `Scripts/Combat/PlayerCombat.cs` -- `InterruptForStance`, `Update` polling pattern; `DodgeController.cs` -- bypasses `CanDodge()`
- `Scripts/Player/PlayerSaveAdapter.cs:162` -- instant-sheathe pattern
- `ScriptableObjects/Config/PlayerConfigSO.cs` -- tunables
- `Art/Characters/Humanoids/Controllers/Humanoid_Template.controller` -- shared with NPCs

## Tasks & Acceptance

**Execution:**
- [x] `Assets/_Game/Art/Characters/Humanoids/Animations/swimming.fbx`, `Idle swimming.fbx` -- loop, bake root rotation / Y / XZ into pose, name clips -- in-place clips driven by code
- [x] `ScriptableObjects/Config/PlayerConfigSO.cs` -- add `[Header("Swimming")]`: `swimSpeed` 2, `swimSprintSpeed` 3.5, `swimSprintStaminaPerSecond` 8, `swimEnterDepth` 1.3, `swimExitDepth` 1.1, `swimFloatDepth` 1.45, `swimRiseSpeed` 2, `swimStrokeDistance` 2 -- no magic numbers
- [x] `Scripts/Player/SwimRules.cs` -- pure enter/exit/float-height logic -- testable
- [x] `Scripts/Player/PlayerSwimming.cs` -- new: detection, enter/exit, Sprint/stamina, swim cues -- single owner of swim mode
- [x] `Scripts/Player/PlayerStateManager.cs` -- `IsSwimming` / `SetSwimming`; instance `Can*` for attack, block, dodge, jump, sneak, aim, weapon switch return false while swimming; `IsAirborne` false while swimming; suppress footsteps; `SetDead(true)` clears it -- gating
- [x] `Scripts/Player/PlayerController.cs` -- swim branch replaces jump and gravity; uses swim speed
- [x] `HumanoidAnimationBridge.cs`, `PlayerAnimationDriver.cs` -- `SetSwimming`, pushed every frame in `Update`
- [x] `Humanoid_Template.controller` -- add `IsSwimming`; `Swim Locomotion` state tagged `FullBody`, 1D blend tree on `VelocityZ`; AnyState → it after Death and stance, before `Attack_1`; exit on `IsSwimming` false
- [x] `Scripts/Combat/PlayerCombat.cs`, `DodgeController.cs`, `Scripts/Player/LockOnSystem.cs` -- sheathe and interrupt on swim, refuse dodge, release lock-on
- [x] `Assets/_Game/Shaders/Water.shader` -- URP transparent: world-space procedural ripples, depth tint, shore foam, refraction, sun specular, fresnel, fog -- scale-independent
- [x] `Assets/_Game/Prefabs/World/Water_Volume.prefab`, `Art/Materials` material -- layer `Water`, trigger `BoxCollider`, surface mesh on top; `NavMeshModifier` ignore on the mesh, `NavMeshModifierVolume` Not Walkable -- scene bakes use render meshes, so the surface would otherwise be walkable
- [x] `Art/Sounds/SFX/Cue_WaterSplash`, `Cue_WaterExit`, `Cue_SwimStroke` + `Art/Sounds/CLAUDE.md` prompts -- empty cues, clips generated by Valentin
- [x] Player prefab -- add and wire `PlayerSwimming`
- [x] `Assets/_Game/Scenes/StartingTown.unity` -- one `Water_Volume` instance Valentin can move and scale
- [x] `Assets/Tests/EditMode/SwimRulesTests.cs` -- matrix enter/exit, hysteresis, highest surface
- [x] `Scripts/Player/CLAUDE.md`, `Controllers/CLAUDE.md`, `Prefabs/CLAUDE.md`, root index -- document swim mode, the state, and the basin / rebake recipe

**Acceptance Criteria:**
- Given a basin with a walkable slope, when the player walks in and out, then they transition without flicker, teleport or fall clips.
- Given swimming, when pressing jump, dodge, attack, block, aim, R or C, then nothing happens.
- Given the volume is rescaled, when viewed in game, then ripples don't stretch and the swim surface matches the visible surface.
- Given a NavMesh rebake with the prefab placed, when NPCs path nearby, then none enter or cross the water.
- Given day/night changes, when viewing the water, then it follows the lighting.

## Spec Change Log

- Review pass 1. Trigger: three reviewers found the Design Note "no camera change" false (looking up put the camera under the surface) and stance clips cut by the held `IsSwimming`. Amended: Camera note and Terrain note. Avoids: culled water surface while swimming, invisible reactions with input frozen. Handled as patches rather than a full re-derivation: the core design held up in Play Mode. KEEP: `SwimRules`, overlap-probe detection, instance `Can*` wrapping, animator bool pushed every frame, clip height offset -0.6.

## Design Notes

**No `WaterVolume` script:** `PlayerSwimming` runs one `OverlapSphereNonAlloc` per frame just above the feet (Water mask, triggers collide) and takes the highest `collider.bounds.max.y` as the surface. The box must reach below the basin floor. Trigger callbacks aren't used because they would also fire for child hitboxes.

**Hysteresis:** `swimExitDepth < swimEnterDepth <= swimFloatDepth`, otherwise floating re-triggers an exit. The shore works by itself: rising ground pushes the `CharacterController` up until depth passes the exit threshold.

**Gates:** wrap in the instance methods (`CanJump() => !IsSwimming && EvaluateCanJump(...)`) so `Evaluate*` signatures and tests stay intact.

**Animator bool:** pushed every frame because `Refresh()` and `Rebind()` reset all parameters. Blend tree thresholds are `swimSpeed / runSpeed` (0.33) and `swimSprintSpeed / runSpeed` (0.58, clip faster); update them if tunables change.

**Stamina:** accumulate the per-second cost and call `Consume` per whole point, so it doesn't log every frame.

**Pose:** the idle clip is upright but `swimming` is prone, so the clip has a height offset of -0.6 (negative raises it) to share the shoulder waterline at `swimFloatDepth` 1.45.

**Terrain:** the terrain can't go below y = 0, so basins need ground of at least ~2.5 m. The scene instance is at (75, 50), where Valentin is sculpting the test basin.

**Camera:** `CameraController` lowers the follow offset when looking up (up to `_lookDownHeightRise`), so that drop is skipped while swimming to keep the camera above the surface.

## Verification

**Commands:**
- Unity Test Runner EditMode, via MCP `run_tests` -- expected: all pass, including `SwimRulesTests`
- MCP `read_console` -- expected: no compile errors

**Manual checks (if no CLI):**
- Play Mode at the placed volume: tune `swimFloatDepth` so the waterline sits at the shoulders; verify the matrix.

## Suggested Review Order

**Swim mode**

- Entry point: finds the surface, then starts or ends the swim.
  [`PlayerSwimming.cs:85`](../../Assets/_Game/Scripts/Player/PlayerSwimming.cs#L85)

- One overlap probe, no water script; highest trigger top is the surface.
  [`PlayerSwimming.cs:117`](../../Assets/_Game/Scripts/Player/PlayerSwimming.cs#L117)

- Pure enter, exit and float rules, covered by tests.
  [`SwimRules.cs:9`](../../Assets/_Game/Scripts/Player/SwimRules.cs#L9)

- Replaces jump and gravity while swimming.
  [`PlayerController.cs:94`](../../Assets/_Game/Scripts/Player/PlayerController.cs#L94)

- Sprint drain in whole stamina points, with the exhausted lockout.
  [`PlayerSwimming.cs:146`](../../Assets/_Game/Scripts/Player/PlayerSwimming.cs#L146)

**Gating and interrupts**

- State owner; entering clears sneak.
  [`PlayerStateManager.cs:240`](../../Assets/_Game/Scripts/Player/PlayerStateManager.cs#L240)

- Instance gates wrap the pure `Evaluate*` methods, keeping their tests intact.
  [`PlayerStateManager.cs:395`](../../Assets/_Game/Scripts/Player/PlayerStateManager.cs#L395)

- Instant sheathe: the full-body swim state hides the sheathe clip.
  [`PlayerCombat.cs:487`](../../Assets/_Game/Scripts/Combat/PlayerCombat.cs#L487)

**Animation and camera**

- Pushed every frame; off during reactions so stagger clips play.
  [`PlayerAnimationDriver.cs:71`](../../Assets/_Game/Scripts/Player/PlayerAnimationDriver.cs#L71)

- Keeps the camera above the surface when looking up.
  [`CameraController.cs:176`](../../Assets/_Game/Scripts/Player/CameraController.cs#L176)

**Water surface**

- World-space shading, so scaling the prefab never stretches ripples.
  [`Water.shader:123`](../../Assets/_Game/Shaders/Water.shader#L123)

**Peripherals**

- Enforces exit < enter <= float.
  [`PlayerConfigSO.cs:50`](../../Assets/_Game/ScriptableObjects/Config/PlayerConfigSO.cs#L50)

- Rule and hysteresis tests.
  [`SwimRulesTests.cs:6`](../../Assets/Tests/EditMode/SwimRulesTests.cs#L6)
