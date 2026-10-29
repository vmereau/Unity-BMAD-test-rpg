---
title: 'Bow drawn in the left hand (off-hand weapon socket)'
type: 'bugfix'
created: '2026-10-29'
status: 'done'
baseline_commit: '118bde8'
context: []
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** When the bow is drawn, `EquipmentVisuals` parents it to `WeaponSocket` under `mixamorig:RightHand`, which is the sword's socket. The Mixamo archer clips hold the bow in the **left** hand and draw the string with the right, so the bow sits in the wrong hand. The rig has no left-hand socket.

**Approach:** Add a `LeftWeaponSocket` under `mixamorig:LeftHand` on the Player prefab. Add a per-archetype "drawn hand" setting (Right by default) that `EquipmentVisuals` uses to pick the drawn socket. Set `Archetype_Bow` to Left and re-derive its `drawnPose` for the left hand. Swords and axes stay on the right-hand `WeaponSocket`.

## Boundaries & Constraints

**Always:** The hand choice lives on the archetype (data), never hard-coded per weapon in `EquipmentVisuals`. Default = Right, so existing archetypes and weapons with a null archetype are unchanged. If the left socket is missing, fall back to the right-hand socket and warn once, matching the Back-socket fallback. The wiring tool (`Game/Dev/Wire EquipmentVisuals on Player Prefab`) finds or creates the new socket.

**Ask First:** Changing the sword/axe drawn poses, or moving the arrow launch origin off the bow (`ActiveWeaponGO`).

**Never:** Arrow-in-hand visuals, IK, or changes to `BowAimCorrection` calibration. No changes to the bow animation clips.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Bow drawn | archetype drawn hand = Left, left socket assigned | visual parented to `LeftWeaponSocket` | N/A |
| Sword drawn | archetype drawn hand = Right | visual on `WeaponSocket` (right hand) | N/A |
| Null archetype | legacy weapon | right-hand `WeaponSocket` | N/A |
| Left socket missing | drawn hand = Left, `_leftWeaponSocket` null | right-hand `WeaponSocket` | `GameLog.Warn` once per equipped weapon |

</frozen-after-approval>

## Code Map

- `Assets/_Game/ScriptableObjects/Items/Weapons/WeaponSheathSocket.cs` -- sheath enum pattern to mirror
- `Assets/_Game/ScriptableObjects/Items/Weapons/WeaponArchetypeSO.cs` -- archetype data (poses, sheath socket)
- `Assets/_Game/Scripts/Inventory/EquipmentVisuals.cs` -- picks the socket in `SetCombatState` / `RefreshWeapon`; `ResolveSheathSocket` static
- `Assets/_Game/Scripts/Editor/WireEquipmentVisuals.cs` -- wires the socket refs and creates `BackWeaponSocket`
- `Assets/_Game/Prefabs/Player/Player.prefab` -- `WeaponSocket` under RightHand at local `(0.002, 0.092, 0.011)`
- `Assets/_Game/Data/Items/Weapons/Archetypes/Archetype_Bow.asset` -- drawn pose copied from the sword
- `Assets/Tests/EditMode/WeaponArchetypeTests.cs` -- `ResolveSheathSocket` tests to mirror
- `Assets/_Game/Scripts/Combat/PlayerCombat.cs:640` -- launch origin = `ActiveWeaponGO` (now the left-hand bow)

## Tasks & Acceptance

**Execution:**
- [x] `Assets/_Game/ScriptableObjects/Items/Weapons/WeaponHand.cs` -- new enum `WeaponHand { Right = 0, Left = 1 }` in its own file -- serialized default stays Right
- [x] `WeaponArchetypeSO.cs` -- add `public WeaponHand drawnHand = WeaponHand.Right;` under Grip; fix the `drawnPose` tooltip ("drawn-hand socket space") -- data-driven hand
- [x] `EquipmentVisuals.cs` -- add `[SerializeField] Transform _leftWeaponSocket`. Add `public static Transform ResolveDrawnSocket(archetype, right, left)` and a private `CurrentDrawnSocket()` that warns once. Use it everywhere `_weaponSocket` is used as the drawn target. Keep `_weaponSocket == null` as the "no visual" gate -- single resolution point
- [x] `WireEquipmentVisuals.cs` -- find or create `LeftWeaponSocket` (layer 3) under `…/mixamorig:LeftShoulder/…/mixamorig:LeftHand`, mirroring the right socket offset `(-0.002, 0.092, 0.011)`, and assign `_leftWeaponSocket` -- reproducible wiring
- [x] Unity -- run the wiring menu, set `Archetype_Bow.drawnHand = Left`, and compute its `drawnPose` from `Aim Overdraw` sampled at 50 % on the Player prefab: grip at the socket, bow +Y ≈ UpperChest up (orthogonalized), +Z (belly) = right-hand → left-hand direction -- the bow faces the target with the string toward the drawing hand
- [x] `WeaponArchetypeTests.cs` -- `ResolveDrawnSocket` tests for the four matrix rows -- coverage
- [x] `Prefabs/Items/Weapons/CLAUDE.md`, `Prefabs/Player/CLAUDE.md` -- document `drawnHand` and `LeftWeaponSocket` -- docs

**Acceptance Criteria:**
- Given the bow is equipped and sheathed, when R is pressed, then the bow appears in the left hand, with the string toward the right hand during the aim pose.
- Given a sword is equipped, when R is pressed, then it is drawn in the right hand exactly as before.
- Given the bow is drawn, when an arrow is fired, then it launches from the left-hand bow position.

## Design Notes

Pose derivation (editor `execute_code`, nothing is shipped): load the Player prefab contents, sample the clip, read `L = LeftWeaponSocket`, `R = RightHand`, `up = UpperChest.up`. `fwd = (L.pos − R.pos).normalized`, `up' = Vector3.ProjectOnPlane(up, fwd).normalized`. Then `worldRot = LookRotation(fwd, up')` and `drawnPose.rotation = Inverse(L.rotation) * worldRot`, with position `(0,0,0)`. This is a first guess; fine-tune in play mode.

## Verification

**Commands:**
- `run_tests EditMode` (WeaponArchetypeTests) -- expected: all pass
- `read_console` -- expected: no compile errors

**Manual checks:**
- Play mode: draw the bow and aim. The bow is in the left hand and the string faces the right hand. The sword is still in the right hand.

## Suggested Review Order

**Drawn-hand resolution**

- Entry point: one static resolver, Left only when asked for and the socket exists.
  [`EquipmentVisuals.cs:181`](../../Assets/_Game/Scripts/Inventory/EquipmentVisuals.cs#L181)

- Instance wrapper warns once per equipped weapon, mirroring the Back-socket fallback.
  [`EquipmentVisuals.cs:170`](../../Assets/_Game/Scripts/Inventory/EquipmentVisuals.cs#L170)

- Both draw paths (R toggle and equip while drawn) go through the resolver.
  [`EquipmentVisuals.cs:73`](../../Assets/_Game/Scripts/Inventory/EquipmentVisuals.cs#L73)
  [`EquipmentVisuals.cs:110`](../../Assets/_Game/Scripts/Inventory/EquipmentVisuals.cs#L110)

**Data**

- Per-family hand choice, default Right so existing archetypes are unchanged.
  [`WeaponArchetypeSO.cs:14`](../../Assets/_Game/ScriptableObjects/Items/Weapons/WeaponArchetypeSO.cs#L14)

- Bow switched to Left; drawn pose derived from `Aim Overdraw` (first guess).
  [`Archetype_Bow.asset:15`](../../Assets/_Game/Data/Items/Weapons/Archetypes/Archetype_Bow.asset#L15)

**Rig wiring**

- Wiring tool finds or creates the left socket under LeftHand.
  [`WireEquipmentVisuals.cs:50`](../../Assets/_Game/Scripts/Editor/WireEquipmentVisuals.cs#L50)

- Side fix: the tool no longer nulls `_bodyRenderer` after the body swap.
  [`WireEquipmentVisuals.cs:103`](../../Assets/_Game/Scripts/Editor/WireEquipmentVisuals.cs#L103)

- The new socket on the prefab.
  [`Player.prefab:76`](../../Assets/_Game/Prefabs/Player/Player.prefab#L76)

**Tests**

- Resolver tests for the four I/O matrix rows plus the default.
  [`WeaponArchetypeTests.cs:144`](../../Assets/Tests/EditMode/WeaponArchetypeTests.cs#L144)
