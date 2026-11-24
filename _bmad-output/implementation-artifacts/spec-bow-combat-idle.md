---
title: 'Bow combat idle: Mixamo standing bow idle with an idle-only carry seat'
type: 'feature'
created: '2026-11-24'
status: 'done'
baseline_commit: '6848e913c39959b428d71810f4206a7fe3243b84'
context:
  - '{project-root}/Assets/_Game/Scripts/Combat/BOW.md'
  - '{project-root}/Assets/_Game/Art/Characters/Humanoids/Controllers/CLAUDE.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** With the bow drawn, `CombatIdle` plays the unarmed fist guard (`Bow_AnimatorOverride` maps no idle). The
user-supplied Mixamo `Combat/Bow/Standing Bow Idle.fbx` looks right with the bow seated in the hand as before
spec-bow-hand-grip (bow low across the front of the hips, string between the arm and the body). With the current
aiming seat (`drawnPose`, handle across the palm) the same clip puts the string outside the arm. Arm-only fits can't fix
that without bending the wrist (renegotiated with the user, 2026-11-24).

**Approach:** Play the untouched Mixamo clip as the bow `CombatIdle`. Give the bow a second in-hand seat, `carryPose`,
used while idling, with its own finger pose. Blend the bow to the aiming seat (`drawnPose`) while the UpperBody layer is
in (or heading into) a bow aim state or the draw / sheathe clip.

## Boundaries & Constraints

**Always:**
- Carry seat starts from the pre-spec-bow-hand-grip `drawnPose` (pos (0.0408, −0.0647, 0.0033), rot (298.92, 293.9,
  31.67)) and is nudged only as needed. Over the `Idle` / `Walking` / `Running` legs at 10 / 50 / 90 %, no bow vertex is
  more than 1 cm inside the body skin, and the string lies between the left forearm and the body.
- `BowCarry` fingers close on the riser: no fingertip more than 3 mm inside it, and middle / ring / little within 12 mm
  of its surface.
- Aim, nock, fire, the Draw Bow grab and the Sheathe Bow release keep the aiming seat (spec-bow-hand-grip numbers
  unchanged). The seat blend takes about 0.15 s, follows scaled time, and snaps when a visual is (re)attached.
- Same data and components serve the player and NPC archers. A weapon without a carry seat behaves exactly as today.

**Ask First:**
- If the carry seat needs more than about 3 cm or 15° of change from the old pose to meet the clearances (show renders).
- Any edit to the Mixamo clip's curves, the aim clips, `drawnPose` / `sheathedPose`, or other archetypes.

**Never:**
- IK, runtime bone corrections, changes to `BowAimCorrection`, the arrow origin or aim / fire logic.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Bow drawn, idle | `CombatIdle` | Mixamo idle, carry seat, `BowCarry` fingers | N/A |
| Start aiming | `IsAiming` → `BowDrawArrow_State` | Seat + fingers blend to aiming in ~0.15 s | N/A |
| Stop aiming | aim states → `CombatIdle` | Blend back to carry | N/A |
| Draw / sheathe | `Draw_State` / `Sheathe_State` | Aiming seat (grab / release unchanged) | N/A |
| Other weapon | no carry seat | `Drawn` stays at `drawnPose` | N/A |

</frozen-after-approval>

## Code Map

- `Assets/_Game/Art/Characters/Humanoids/Animations/Combat/Bow/Standing Bow Idle.fbx(.meta)` -- Mixamo source (untracked); clip `Standing Bow Idle`, loop, root baked (set)
- `.../Combat/Bow/Bow_AnimatorOverride.overrideController` -- maps Draw / Sheathe only; add `Unarmed Idle Guard` → idle
- `Assets/_Game/ScriptableObjects/Items/Weapons/WeaponArchetypeSO.cs`, `Data/Items/Weapons/Archetypes/Archetype_Bow.asset` -- `drawnPose` / `sheathedPose`
- `Assets/_Game/Scripts/Inventory/EquipmentVisuals.cs` -- `ApplyArchetypePoses`, visual (re)attach
- `Assets/_Game/Scripts/Core/Animations/HandPoseDriver.cs`, `HandPoseRule.cs` (`HandPose` enum), `HumanoidAnimationBridge.cs` (`DestinationIs`, `IsSwitchingWeapon`)
- `Assets/_Game/Editor/Characters/HandPoseBuilder.cs` (`Poses`, `Build Hand Poses`), `HandPoseCapture.cs` (`DefaultCases`)
- `Assets/Tests/EditMode/HandPoseRuleTests.cs`, `HumanoidTemplateControllerTests.cs`, `WeaponArchetypeTests.cs`

## Tasks & Acceptance

**Execution:**
- [x] `WeaponArchetypeSO.cs` -- `hasCarryPose` + `carryPose` (Drawn child pose while idling) -- data
- [x] `EquipmentVisuals.cs` -- `SetCarryWeight(float)`: Drawn = blend(drawnPose → carryPose) (lerp / slerp, pure static helper); no-op without a carry seat; reset to 0 on attach -- seat blend
- [x] `HumanoidAnimationBridge.cs` -- `IsBowAiming` (UpperBody destination = BowDrawArrow / BowAim / BowFire) -- state query
- [x] `HandPoseRule.cs` + `HandPoseDriver.cs` -- append `HandPose.BowCarry = 6`; carry = bow in hand and neither aiming nor switching; the driver steps the weight (rate 1 / 0.15 s) and picks `BowCarry` vs `BowGrip` -- one place drives seat + fingers
- [x] `HandPoseBuilder.cs` -- `Poses[BowCarry]` fitted to the carry seat; rebuild the hand layers -- fingers
- [x] `Archetype_Bow.asset` -- fit `carryPose` (start from the old seat, clearances above) -- seat values
- [x] `Bow_AnimatorOverride` -- `Unarmed Idle Guard` → `Standing Bow Idle` (no empty entries) -- idle clip
- [x] `HandPoseCapture.cs` -- `bowidle` case (idle clip, `BowCarry`, carry seat) -- verification
- [x] Tests -- rule (`BowCarry` cases), blend helper (0 / 1 / mid), override mapping + loop, LeftHand layer has `BowCarry` -- guards
- [x] Delete `Editor/Characters/TmpBowIdleFit.cs`; docs: `BOW.md`, `Controllers/CLAUDE.md`, `Scripts/Core/Animations/CLAUDE.md`, `Weapons/CLAUDE.md`, `deferred-work.md` (close the "drawn bow plays the unarmed guard" entry) -- cleanup

**Acceptance Criteria:**
- Given `HandPoseCapture bowidle`, when run, then the Always clearances and finger limits hold.
- Given StartingTown in play mode, when the player draws the bow, idles, aims, fires, stops aiming and sheathes, then screenshots show the Mixamo idle with the string inside, a clean aim, and no visible pop beyond the short seat blend.
- Given the EditMode suite, when run, then all tests pass.

## Spec Change Log

- 2026-11-24 (user renegotiation, before implementation): the "ready-low" arm-offset bake was dropped. With the
  aiming seat, the string sits on the back-of-hand side, so the string can only face the body if the arm twists the
  palm outward (wrist at ±0.9 in every fit). The user prefers the untouched Mixamo pose with the old seat, so the bow now
  re-seats in the hand while idling. KEEP: clip import settings already applied (loop, root baked like `Aim Overdraw`).

## Verification

**Commands:**
- MCP `run_tests` EditMode -- expected: all pass
- MCP `read_console` -- expected: no compile errors

**Manual checks:**
- Renders `D:/claude-scratch/bowidle/` (body + hand close-ups); in-game screenshots (idle, aim, stop aim, sheathe).

## Design Notes (implementation)

- **Carry seat:** `Archetype_Bow.carryPose` = pos (0.0288, −0.0767, 0.0033), rot (298.92, 293.9, 31.67): the old seat moved
  (−1.2, −1.2, 0) cm, no rotation (under the 3 cm / 15° Ask First). Hip / buttock clearance over Idle / Walking / Running
  at 10 / 50 / 90 % (left-hand skin excluded): worst 0.5 cm inside (old seat 1.7 cm). String faces the body (horizontal
  string direction · hand→hips = 0.63 at worst).
- **`BowCarry` fingers:** `HandPoseCapture bowidle`: no finger skin inside the palm; tips Th 14 / In 32 / Mi −2 / Ri 1 /
  Li 12 mm. Aim grip (`bow` case) unchanged from spec-bow-hand-grip.
- **Gate:** carry only in the combat idle (UpperBody heading into `CombatIdle`, Base not full-body, rigid left-hand
  archetype); everything else keeps `drawnPose`, as before this spec.
- **In game (StartingTown, player driven through `PlayerCombat` handlers, per-frame log):** draw → bow arrives at the
  aiming seat, eases to carry (0.15 s) as the idle starts; aim → aiming seat before `Bow Draw Arrow`; stop aim → carry in
  ~0.17 s; sheathe → aiming seat at 0.175 s, released to the back at 0.268 s (Sheathe Bow release 0.238 s into the
  clip). Firing was not exercised in play: `BowFire_State` isn't `CombatIdle`, so it keeps the aiming seat. Screenshots
  `Temp/bowidle/ingame_*.png`. The MCP positioned-camera screenshot doesn't render the bow; move `Camera.main` (Cinemachine
  brain off) and capture the Game view instead.
- **Evidence:** EditMode 1953 / 1953.

## Spec Change Log (review)

- 2026-11-24 review (patches, no loopback): carry gated positively (`CombatIdle` + not full-body) instead of "not aiming /
  switching" — a dodge, stagger, knockdown, death or drink kept the idle-only seat; snap when a weapon appears in the hand
  without a draw clip (NPCs) and keep the weight across a same-family `RefreshWeapon` (no one-frame pop); carry seat limited
  to rigid left-hand archetypes (`UsesCarrySeat`); misplaced XML doc restored; the empty-override test reads `m_Clips`.

## Suggested Review Order

**When the bow uses its idle seat**

- Entry point: the carry seat applies only while the combat idle shows.
  [`HandPoseRule.cs:65`](../../Assets/_Game/Scripts/Core/Animations/HandPoseRule.cs#L65)

- One driver picks fingers and seat from the same state, for player and NPCs.
  [`HandPoseDriver.cs:55`](../../Assets/_Game/Scripts/Core/Animations/HandPoseDriver.cs#L55)

- 0.15 s ease; snaps on a new visual or an NPC's clip-less draw.
  [`HandPoseDriver.cs:67`](../../Assets/_Game/Scripts/Core/Animations/HandPoseDriver.cs#L67)

- Read-only state query (destination state, like the switching check).
  [`HumanoidAnimationBridge.cs:179`](../../Assets/_Game/Scripts/Core/Animations/HumanoidAnimationBridge.cs#L179)

**Seat blend on the weapon visual**

- Writes the Drawn child between drawnPose and carryPose.
  [`EquipmentVisuals.cs:427`](../../Assets/_Game/Scripts/Inventory/EquipmentVisuals.cs#L427)

- Only rigid left-hand weapons: fingers and TwoHandGripConstraint stay consistent.
  [`EquipmentVisuals.cs:441`](../../Assets/_Game/Scripts/Inventory/EquipmentVisuals.cs#L441)

- Gear change mid-idle keeps the seat (no one-frame pop).
  [`EquipmentVisuals.cs:184`](../../Assets/_Game/Scripts/Inventory/EquipmentVisuals.cs#L184)

**Data**

- New archetype fields.
  [`WeaponArchetypeSO.cs:21`](../../Assets/_Game/ScriptableObjects/Items/Weapons/WeaponArchetypeSO.cs#L21)

- Bow carry seat values (old seat, 1.7 cm off the hip).
  [`Archetype_Bow.asset:22`](../../Assets/_Game/Data/Items/Weapons/Archetypes/Archetype_Bow.asset#L22)

- Bow combat idle mapped onto the base `Unarmed Idle Guard`.
  [`Bow_AnimatorOverride.overrideController:1`](../../Assets/_Game/Art/Characters/Humanoids/Animations/Combat/Bow/Bow_AnimatorOverride.overrideController#L1)

- Mixamo clip import: loop, root baked like the aim clips.
  [`Standing Bow Idle.fbx.meta:46`](../../Assets/_Game/Art/Characters/Humanoids/Animations/Combat/Bow/Standing%20Bow%20Idle.fbx.meta#L46)

**Fingers**

- New appended hand pose (Animator int 6).
  [`HandPoseRule.cs:15`](../../Assets/_Game/Scripts/Core/Animations/HandPoseRule.cs#L15)

- BowCarry finger values with measured gaps; rebuilt via Build Hand Poses.
  [`HandPoseBuilder.cs:93`](../../Assets/_Game/Editor/Characters/HandPoseBuilder.cs#L93)

**Peripherals**

- Capture case for the idle seat.
  [`HandPoseCapture.cs:58`](../../Assets/_Game/Editor/Characters/HandPoseCapture.cs#L58)

- Rule cases: idle only, full-body / aim / right-hand excluded.
  [`HandPoseRuleTests.cs:113`](../../Assets/Tests/EditMode/HandPoseRuleTests.cs#L113)

- Override mapping, loop, no empty entries.
  [`HumanoidTemplateControllerTests.cs:195`](../../Assets/Tests/EditMode/HumanoidTemplateControllerTests.cs#L195)

- Blend math and carry-seat eligibility.
  [`WeaponArchetypeTests.cs:315`](../../Assets/Tests/EditMode/WeaponArchetypeTests.cs#L315)
