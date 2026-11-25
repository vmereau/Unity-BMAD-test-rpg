---
title: 'Bow combat idle: yaw-compensated bake of the Mixamo idle with a palm-true carry seat'
type: 'feature'
created: '2026-11-25'
status: 'done'
baseline_commit: '1b44c7812ccb26ce63d659631de121946d83a8b5'
context:
  - '{project-root}/Assets/_Game/Scripts/Combat/BOW.md'
  - '{project-root}/Assets/_Game/Art/Characters/Humanoids/Animations/Combat/BAKE_RECIPES.md'
  - '{project-root}/Assets/_Game/Scripts/Core/Animations/CLAUDE.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** A drawn bow idles in the unarmed fist guard. The Mixamo `Standing Bow Idle` is a side-on stance (hips 63°,
shoulders 55°, head 3°; locomotion `Idle` hips 5°), and `UpperBodyMask` takes the hips from locomotion: the chest ends
facing forward and the head ~52° left. The first attempt (944cead1, reverted) also seated the idle bow with its handle
along the fingers, so the carry seat and the fingers looked wrong (user, 2026-11-25).

**Approach:** Bake the idle into an UpperBody clip with constant spine and neck offsets that give back part of the lost
turn (chest bladed, head forward). Keep two in-hand seats for the bow (aiming, carry) with the 944cead1 state-driven
blend, but re-derive the carry seat so the handle stays across the palm, and refit the `BowCarry` fingers on it.

## Boundaries & Constraints

**Always:**
- Measured on the Player over `Idle` legs: shoulders yaw 25° ± 5° right of the root, head yaw within ± 5° of forward,
  constant over the clip. Offsets are constant, set in code, each resulting muscle within ± 1.
- Carry seat: the handle crosses the left palm like the aiming seat (handle axis ≤ 15° from the aiming seat's, palm
  face of the riser ≤ 1 cm from the palm). The string lies between the left forearm and the body. Over `Idle` /
  `Walking` / `Running` at 10 / 50 / 90 %, no bow vertex is more than 1 cm inside the body skin.
- `BowCarry` fingers: no fingertip more than 3 mm inside the riser; all five tips within 12 mm of its surface.
- The baked idle may add left wrist / forearm-twist offsets of at most ± 0.7 to make that seat possible.
- Aim, nock, fire, Draw Bow grab and Sheathe Bow release keep the aiming seat and the spec-bow-hand-grip numbers.
- Baked by a committed, idempotent editor tool. Same data and components for the player and NPC archers. A weapon
  without a carry seat behaves as today.

**Ask First:**
- If the seat, wrist or chest limits above cannot all be met (show renders and the residuals).
- Before wiring the clip into the override: show the user front / side / hand close-up renders of the fitted idle.
- Any edit to the aim clips, `drawnPose` / `sheathedPose`, other archetypes or the UpperBody mask.

**Never:**
- IK or runtime bone corrections, a new Base-layer state, bow-specific leg clips, changes to `BowAimCorrection`, the
  arrow origin or aim / fire logic.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Bow drawn, idle or moving | `CombatIdle` | Baked idle on the upper body, carry seat, `BowCarry` fingers | N/A |
| Start / stop aiming | `IsAiming` flips | Seat and fingers blend to / from the aiming seat in ~0.15 s | N/A |
| Draw / sheathe, drink, full-body states | not `CombatIdle` | Aiming seat | N/A |
| Other weapon | no carry seat | `Drawn` stays at `drawnPose` | N/A |
| Bake with a missing source or curve | tool run | Nothing written | Throws, logged with `GameLog.Error` |

</frozen-after-approval>

## Code Map

- `Assets/_Game/Art/Characters/Humanoids/Animations/Combat/Bow/Standing Bow Idle.fbx(.meta)` -- source, untracked, currently imported Generic; the Humanoid `.meta` is in 944cead1
- `Assets/_Game/Editor/Characters/BowClipWristBaker.cs` -- pattern for the bake (constant muscle offsets, GUID kept)
- `Assets/_Game/Art/Characters/Humanoids/Animations/Combat/Bow/Bow_AnimatorOverride.overrideController` -- maps Draw / Sheathe only
- 944cead1 (reverted) -- `WeaponArchetypeSO` carry fields, `EquipmentVisuals.SetCarryWeight`, `HandPoseRule` / `HandPoseDriver` `BowCarry`, `HumanoidAnimationBridge` query, `HandPoseBuilder` / `HandPoseCapture` cases, tests, hand-layer rebuild
- `Assets/_Game/Data/Items/Weapons/Archetypes/Archetype_Bow.asset` -- `drawnPose` (aiming seat), carry seat to add

## Tasks & Acceptance

**Execution:**
- [x] git -- `git cherry-pick -n 944cead1`, then drop its spec file and its `deferred-work.md` hunk -- restores the reviewed seat blend, rule, tests and the Humanoid import of the FBX
- [x] `Assets/_Game/Editor/Characters/BowIdleBaker.cs` -- `Tools/Characters/Bake Bow Idle Clip`: source FBX → `Combat/Bow/Bow Idle.anim` with constant spine / chest / upper-chest twist, neck / head turn and left-wrist offsets (values in code, clamped like `BowClipWristBaker.OffsetValue`) -- compensated idle, reproducible
- [x] `BowIdleBaker.cs` offsets -- fit on the Player prefab over the `Idle` legs through the real mask (layered sample), to the Always targets -- measured, not eyeballed
- [x] `Archetype_Bow.asset` -- re-derive `carryPose` from `drawnPose` (turn about the handle axis, small slide), to the Always clearances -- handle stays in the palm
- [x] `HandPoseBuilder.cs` -- refit `Poses[BowCarry]` on the new seat, rebuild the hand layers -- fingers closed on the riser
- [x] `HandPoseCapture.cs` -- `bowidle` case samples `Bow Idle.anim` over `Idle` legs; render front / side / hand close-up -- evidence for the Ask First render check
- [x] `Bow_AnimatorOverride.overrideController` -- `Unarmed Idle Guard` → `Bow Idle` (replaces the cherry-picked FBX mapping) -- idle clip
- [x] `Assets/Tests/EditMode/` -- baker offset clamp, override maps the baked looping clip, restored rule / blend / layer tests still pass -- guards
- [x] `BOW.md`, `BAKE_RECIPES.md`, `Controllers/CLAUDE.md`, `Scripts/Core/Animations/CLAUDE.md`, `Prefabs/Items/Weapons/CLAUDE.md`, `deferred-work.md` -- bake recipe, carry seat rule, close the two "bow plays the unarmed guard" entries -- docs

**Acceptance Criteria:**
- Given `HandPoseCapture bowidle`, when run, then the chest, head, seat and finger limits in Always hold.
- Given StartingTown in play mode, when the player draws the bow, idles, walks, runs, aims, fires, stops aiming and sheathes, then Game-view screenshots show the bladed idle with the string inside the arm and no pop beyond the seat blend.
- Given an NPC archer in combat, when it idles with the bow, then it shows the same idle and seat.
- Given the EditMode suite, when run, then all tests pass.

## Design Notes

- Only part of the 58° lost turn is given back (target 25°): the full turn over forward legs reads as a wrung torso.
- The seat blend stays state-driven (carry only while the UpperBody layer heads into `CombatIdle` and the Base layer is
  not full-body): the user reported no issue with it.
- The masked layer cannot be measured with `clip.SampleAnimation`; sample a two-layer playable (legs clip + masked idle).

## Verification

**Commands:**
- MCP `run_tests` EditMode -- expected: all pass
- MCP `read_console` -- expected: no compile errors

**Manual checks:**
- Renders from `HandPoseCapture bowidle` (front, side, hand) and in-game screenshots (idle, walk, aim, stop aim, sheathe).

## Implementation Notes

- **Root cause found during implementation:** `Standing Bow Idle.fbx` has no skin, so with `Create From This Model` Unity
  built its avatar from the idle pose and every muscle read ~0 (Head Turn 0.03 instead of −1.34). The 944cead1 version
  played that flat pose, and its carry seat and fingers were fitted on it. The import now copies the avatar from
  `Idle.fbx` (deviation from the cherry-picked `.meta`; pinned by a test).
- **Bake:** unbaked, through the mask: shoulders −6°, head −57°. Baked: shoulders 24.4–24.8°, head 0.1–0.6° at 10 / 50 /
  90 %. A turn-only fit left the torso and head leaning sideways, so the offsets cover Front-Back, Left-Right and Twist
  (spine) and Nod, Tilt and Turn (neck / head); all within ±1, 0 keys clamped. No wrist offset was needed.
- **Carry seat:** `carryPose` = `drawnPose` rolled −95° about the handle axis (`carryPivot` (0, 0, 0.0855)); handle axis
  deviation 0°. With the aiming seat the string was 17 cm outboard of the forearm. `HandPoseCapture bowidle`: string
  7.9 cm from the forearm on the body side, 0.0 cm inside the body over Idle / Walking / Running at 10 / 50 / 90 %; palm
  gap 17 mm (aiming seat: 24 mm); fingertips Th 7 / In 4 / Mi 5 / Ri 2 / Li 2 mm. The 944cead1 seat is 7.9 cm inside the
  body on the corrected pose.
- **Measured on `Humanoid_BaseHero`** (same sockets as the Player), not the Player prefab.
- **User render check (Ask First):** renders shown and approved 2026-11-25 before the override was wired.
- **In game (StartingTown, player driven through `PlayerCombat`):** idle = `Bow Idle`, carry seat, `BowCarry`; aim =
  `Bow Aim Overdraw`, aiming seat, `BowGrip`; stop aim = back to carry; sheathe = bow on the back, hand relaxed.
  **Not exercised in play:** walking / running, firing, an NPC archer.
- **Evidence:** EditMode 1958 / 1958.

## Spec Change Log

- 2026-11-25 review (patches, no loopback): the seat blend lerped the Drawn origin, moving the handle 28 mm off the palm
  mid-blend — `BlendPose` now turns about `carryPivot` (tested); a gear change mid-blend no longer snaps the seat (the
  driver tracks the archetype, not the visual); `BowIdleBaker.Rig` disposes on a failed build; the seat clearance sweep
  is part of `HandPoseCapture bowidle`; a clamped bake key fails the stale-bake test; stale doc lines fixed.

## Suggested Review Order

**The baked idle**

- Entry point: the offsets that turn and level the masked upper body.
  [`BowIdleBaker.cs:30`](../../Assets/_Game/Editor/Characters/BowIdleBaker.cs#L30)

- Bake validates first, then rewrites the clip in place.
  [`BowIdleBaker.cs:59`](../../Assets/_Game/Editor/Characters/BowIdleBaker.cs#L59)

- Two-layer playable: the only way to read a masked layer in edit mode.
  [`BowIdleBaker.cs:167`](../../Assets/_Game/Editor/Characters/BowIdleBaker.cs#L167)

- Shared copy-with-offsets, reused from the wrist baker.
  [`BowClipWristBaker.cs:110`](../../Assets/_Game/Editor/Characters/BowClipWristBaker.cs#L110)

- Import fix: avatar copied from the hero, not built from the idle pose.
  [`Standing Bow Idle.fbx.meta:894`](../../Assets/_Game/Art/Characters/Humanoids/Animations/Combat/Bow/Standing%20Bow%20Idle.fbx.meta#L894)

**The two seats**

- Blend turns the bow about the handle, so it stays in the palm.
  [`EquipmentVisuals.cs:451`](../../Assets/_Game/Scripts/Inventory/EquipmentVisuals.cs#L451)

- Seat written on the Drawn child; rigid left-hand weapons only.
  [`EquipmentVisuals.cs:427`](../../Assets/_Game/Scripts/Inventory/EquipmentVisuals.cs#L427)

- Driver eases the weight; snaps only for a new family or a clip-less draw.
  [`HandPoseDriver.cs:69`](../../Assets/_Game/Scripts/Core/Animations/HandPoseDriver.cs#L69)

- Carry only in the combat idle, never over full-body states.
  [`HandPoseRule.cs:65`](../../Assets/_Game/Scripts/Core/Animations/HandPoseRule.cs#L65)

- Read-only state query on the bridge.
  [`HumanoidAnimationBridge.cs:179`](../../Assets/_Game/Scripts/Core/Animations/HumanoidAnimationBridge.cs#L179)

**Data**

- Carry seat, pivot and the flag on the bow archetype.
  [`Archetype_Bow.asset:22`](../../Assets/_Game/Data/Items/Weapons/Archetypes/Archetype_Bow.asset#L22)

- New archetype fields.
  [`WeaponArchetypeSO.cs:26`](../../Assets/_Game/ScriptableObjects/Items/Weapons/WeaponArchetypeSO.cs#L26)

- `BowCarry` finger values for the rolled seat.
  [`HandPoseBuilder.cs:93`](../../Assets/_Game/Editor/Characters/HandPoseBuilder.cs#L93)

**Peripherals**

- Seat sweep: string side and body clearance over three leg clips.
  [`HandPoseCapture.cs:211`](../../Assets/_Game/Editor/Characters/HandPoseCapture.cs#L211)

- Guards the avatar import that caused the flat pose.
  [`HumanoidTemplateControllerTests.cs:240`](../../Assets/Tests/EditMode/HumanoidTemplateControllerTests.cs#L240)

- Pins chest 25° and head forward through the real mask.
  [`HumanoidTemplateControllerTests.cs:253`](../../Assets/Tests/EditMode/HumanoidTemplateControllerTests.cs#L253)

- Handle stays in place across the seat blend.
  [`WeaponArchetypeTests.cs:356`](../../Assets/Tests/EditMode/WeaponArchetypeTests.cs#L356)

- Bake recipe and the no-skin avatar gotcha.
  [`BAKE_RECIPES.md:184`](../../Assets/_Game/Art/Characters/Humanoids/Animations/Combat/BAKE_RECIPES.md#L184)
