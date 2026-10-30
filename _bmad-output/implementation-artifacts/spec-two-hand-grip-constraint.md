---
title: 'Two-hand grip constraint'
type: 'feature'
created: '2026-10-30'
status: 'done'
baseline_commit: '6424ac59e53b084aed2a4f4be238484f4d4f862e'
context:
  - '{project-root}/_bmad-output/project-context.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** A drawn weapon is rigidly posed on the right-hand socket (`archetype.drawnPose`). The Mixamo two-handed clips (animated without a weapon) don't keep a constant hand relationship: the hand line drifts 12–64° from the pose within one `2hand attack` swing, and `2hand blocking` uses an opposite grip (127° off). No single pose keeps both hands on the handle.

**Approach:** For archetypes flagged `twoHandedGrip`, a Player component running after the Animator (and after `BowAimCorrection`) re-aims the drawn weapon every frame: grip on the right-hand socket, blade along the left-hand → right-hand line, roll from the archetype's tuned edge direction plus `gripRoll`, shifted `gripSlide` along the blade. The Weapon Pose Tuner previews the constrained result live; roll / slide are edited as archetype fields (user choice a).

## Boundaries & Constraints

**Always:** Pure solve math in a static class with EditMode tests. Only touches the `Drawn` child, only while drawn and the archetype is flagged — one-handed weapons, bows and sheathed poses unchanged. Degenerate input (hands < 5 cm apart, edge reference parallel to the blade, missing socket) falls back to the rigid `drawnPose` for that frame. The hitbox follows (it lives on `Drawn`). Constants in the archetype / class, `GameLog` for warnings.

**Ask First:** Changing `EquipmentVisuals` beyond read-only accessors; changing `BowAimCorrection`; adding left-hand IK.

**Never:** No IK on the arms. No animator / controller changes. No NPC support (NPCs have no `EquipmentVisuals`). No Play-mode tuner.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Swing | Flamberge drawn, `2hand attack` any frame | Grip at the right socket, blade along LH→RH, edge = archetype edge projected ⊥ blade, rotated `gripRoll` | N/A |
| Block | `2hand blocking` (opposite grip) | Blade follows the flipped hand line, both hands on the handle | N/A |
| Slide | `gripSlide` = 0.05 | Weapon moved 5 cm along +blade | N/A |
| Degenerate | Hands < 5 cm, or edge ref ∥ blade | Rigid `drawnPose` this frame | No log spam (warn once) |
| Other weapon | Sword / bow / sheathed 2H | No change to `Drawn` | N/A |

</frozen-after-approval>

## Code Map

- `Assets/_Game/Scripts/Inventory/EquipmentVisuals.cs` -- owns `_weaponVisual`, `_currentArchetype`, `_isInCombat`, hand sockets; `ApplyArchetypePoses`
- `Assets/_Game/ScriptableObjects/Items/Weapons/WeaponArchetypeSO.cs` -- family preset; new grip fields
- `Assets/_Game/Scripts/Combat/BowAimCorrection.cs` -- `[DefaultExecutionOrder(200)]` LateUpdate bone writes (spine) — the grip must run after it
- `Assets/_Game/Editor/WeaponPoseTunerWindow.cs` -- preview must show the constrained weapon
- `Assets/_Game/Prefabs/Player/Player.prefab` -- `WeaponSocket` (RightHand), `LeftWeaponSocket` (LeftHand, mirrored offset)

## Tasks & Acceptance

**Execution:**
- [x] `ScriptableObjects/Items/Weapons/WeaponArchetypeSO.cs` -- `[Header("Two-hand grip")] bool twoHandedGrip; float gripRoll (deg); float gripSlide (m)` -- per-family switch + tuning
- [x] `Scripts/Inventory/TwoHandGripMath.cs` -- `static bool TrySolve(Vector3 rightGrip, Vector3 leftGrip, Vector3 edgeReference, float rollDeg, float slide, out Vector3 position, out Quaternion rotation)`: blade = normalize(right − left); edge = edgeRef ⊥ blade, rotated `rollDeg` about blade; rotation = LookRotation(edge, blade); position = rightGrip + blade × slide; false when degenerate -- shared by runtime and tuner
- [x] `Scripts/Inventory/EquipmentVisuals.cs` -- read-only accessors: `CurrentArchetype`, `IsDrawn`, `DrawnSocket` (resolved hand), `LeftHandSocket` -- no behavior change
- [x] `Scripts/Inventory/TwoHandGripConstraint.cs` -- `[DefaultExecutionOrder(250)]` MonoBehaviour on the Player root: in LateUpdate, when drawn + flagged + `Drawn` child found, edge reference = `DrawnSocket.rotation × drawnPose rotation × forward`, solve with `DrawnSocket` / `LeftHandSocket` positions, write `Drawn` world pose; else no-op. Warn once on missing left socket -- runtime constraint
- [x] `Prefabs/Player/Player.prefab` -- add `TwoHandGripConstraint` (reference `EquipmentVisuals`) -- wiring
- [x] `Data/Items/Weapons/Archetypes/Archetype_TwoHanded.asset` -- `twoHandedGrip` true, roll / slide 0 -- enable for the family
- [x] `Editor/WeaponPoseTunerWindow.cs` -- Drawn mode + flagged archetype: after each sample and on every scene repaint, apply the same solve to the preview `Drawn`; disable Save Pose / Select Pose Node there with a hint ("edit gripRoll / gripSlide on the archetype"); Select Grip stays -- live preview
- [x] `Tests/EditMode/TwoHandGripMathTests.cs` -- blade along the hand line, edge ⊥ blade + roll, slide offset, degenerate cases -- I/O matrix
- [x] CLAUDE.md: `Prefabs/Items/Weapons`, `Prefabs/Player`, `Scripts/Inventory` -- constraint, order 250 vs `BowAimCorrection` 200, tuner behavior, "discard the Player stage after tuning" -- docs

**Acceptance Criteria:**
- Given the Flamberge drawn in Play mode, when attacking and blocking, then the handle passes through both hands at every frame and the edge leads the swing.
- Given the sword or the bow, then their drawn visuals are byte-for-byte driven by `drawnPose` as before.
- Given the tuner on the Flamberge (Drawn), when scrubbing `2hand blocking` and editing `gripRoll`, then the preview updates without saving anything to the Player.

## Design Notes

The archetype's tuned `drawnPose` keeps one role: its edge axis (rotation × +Z, in hand-socket space) is the roll reference, so the current tuning stays meaningful and `gripRoll` defaults to 0. Blade sign: right hand nearer the guard, so the blade extends past the right hand away from the left (right − left).

## Verification

**Commands:**
- Unity compile + `read_console` -- expected: no errors
- `run_tests` EditMode -- expected: all green incl. `TwoHandGripMathTests`

**Manual checks:**
- Play mode: Flamberge attack ×2 + block — both hands on the handle; sword / bow unchanged.

## Suggested Review Order

**Runtime constraint**

- Entry point: blend rigid drawnPose → two-hand solve by grip weight.
  [`TwoHandGripConstraint.cs:83`](../../Assets/_Game/Scripts/Inventory/TwoHandGripConstraint.cs#L83)

- Gates: flagged + drawn, Left-hand archetype and missing socket warn once.
  [`TwoHandGripConstraint.cs:45`](../../Assets/_Game/Scripts/Inventory/TwoHandGripConstraint.cs#L45)

- Solve: blade = right − left, edge from drawnPose rolled, slide along blade.
  [`TwoHandGripMath.cs:44`](../../Assets/_Game/Scripts/Inventory/TwoHandGripMath.cs#L44)

- Smooth hand-off: weight eases out as the left hand lets go or the solve degenerates.
  [`TwoHandGripMath.cs:29`](../../Assets/_Game/Scripts/Inventory/TwoHandGripMath.cs#L29)

**Ordering**

- Hitbox sweep moved after every pose writer (order 300).
  [`WeaponHitbox.cs:18`](../../Assets/_Game/Scripts/Combat/WeaponHitbox.cs#L18)

- Read-only accessors the constraint needs.
  [`EquipmentVisuals.cs:35`](../../Assets/_Game/Scripts/Inventory/EquipmentVisuals.cs#L35)

**Tooling, data, tests**

- Tuner preview re-solves live (10 Hz + repaint); pose saving disabled in 2H drawn mode.
  [`WeaponPoseTunerWindow.cs:432`](../../Assets/_Game/Editor/WeaponPoseTunerWindow.cs#L432)

- New archetype fields.
  [`WeaponArchetypeSO.cs:28`](../../Assets/_Game/ScriptableObjects/Items/Weapons/WeaponArchetypeSO.cs#L28)

- Math + weight tests (incl. reversed block grip).
  [`TwoHandGripMathTests.cs:1`](../../Assets/Tests/EditMode/TwoHandGripMathTests.cs#L1)
