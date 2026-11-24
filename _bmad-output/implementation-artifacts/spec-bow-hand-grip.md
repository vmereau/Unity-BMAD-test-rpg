---
title: 'Bow held by its grip: left hand on the handle in hand and when drawn from the back'
type: 'bugfix'
created: '2026-11-24'
status: 'done'
baseline_commit: '2f3945caa93bf9e5e97c40ff1c0f24bbde0ec9ce'
context:
  - '{project-root}/Assets/_Game/Art/Characters/Humanoids/Animations/Combat/BAKE_RECIPES.md'
  - '{project-root}/Assets/_Game/Scripts/Combat/BOW.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** Measured on the v2 hero (`HandPoseCapture`, preview-scene renders): in hand, the bow's handle (Drawn-space
y ±0.075, z 0.065–0.107, x ±0.027) runs almost along the left fingers (82° off the palm's cross axis), so the middle
finger sinks 15 mm and the thumb 9 mm into the riser while the little finger misses it by 30 mm. On draw / sheathe the
left hand passes 19 cm from the bow's grip on the back at best (Draw Bow ~30 %, Sheathe Bow ~37 %), and `WeaponSwap` fires
at 45 % / 65 % when the hand is 44 / 54 cm away: the bow teleports. The bow's riser also sits inside the back.

**Approach:** Re-derive `Archetype_Bow.drawnPose` so the handle crosses the left palm, and retune the `BowGrip` fingers to
wrap it. Correct the left wrist with constant muscle offsets in baked copies of the three aim clips, so the bow stays
upright (string toward the right hand) while nocking, aiming and firing. Re-derive `sheathedPose` so that on the Draw
Bow grab frame the bow on the back sits exactly where it will be in the hand. Move both `WeaponSwap` events to the frame
where the hand is on the grip.

## Boundaries & Constraints

**Always:**
- The grab is seamless: at the Draw Bow `WeaponSwap` frame, the bow's world pose on the back equals its in-hand pose
  (≤ 1 cm, ≤ 5°). The Sheathe Bow event goes on the frame where the palm is closest to the back grip.
- The bow on the back does not cross the torso skin (riser and limbs outside the body, ≤ 1 cm graze tolerated).
- The baked clips are produced by a committed, idempotent editor tool (offsets in code). Every source curve and clip
  setting is copied, the offsets are constant and only on the left wrist / forearm-twist muscles, and each stays within ±1.
- The arrow line (`BowAimCorrection` calibration) and the launch origin stay valid. Wrist muscles don't move the hand
  bones' positions: confirm it numerically and re-calibrate only if the line moves by more than 1°.
- Same data serves the player and NPC archers (archetype + shared controller).

**Ask First:**
- If a handle-across-palm grip can't keep the aiming bow within ~15° of upright using wrist offsets alone (show the
  residual).
- If a seamless grab puts the bow on the back through the body or in an implausible spot (show renders first).
- Any change to the guard / unarmed clips, other archetypes, or `HandPoseRule`.

**Never:**
- IK, runtime bone corrections, a string bone / bending string, a right-hand string pose (deferred), or changes to arrow
  flight, aim spread or `BowAimCorrection` code.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Nock / aim / fire | bow drawn, aiming | Handle across the left palm. Fingers on it: no tip > 3 mm inside the riser, middle / ring / little ≤ 12 mm from its surface. Bow upright ±15°, string toward the right hand | N/A |
| Combat idle with the bow | `Unarmed Idle Guard` + bow | Same grip in hand (clip untouched) | N/A |
| Draw Bow | press R, bow on the back | Palm on the back grip at the event frame, swap with no visible jump | N/A |
| Sheathe Bow | press R, bow in hand | Bow leaves the hand at the back, near its sheathed pose | N/A |

</frozen-after-approval>

## Code Map

- `Assets/_Game/Data/Items/Weapons/Archetypes/Archetype_Bow.asset` -- `drawnPose` (LeftWeaponSocket space), `sheathedPose` (BackWeaponSocket, Spine2)
- `Assets/_Game/Editor/Characters/HandPoseBuilder.cs` -- `Poses[BowGrip]` finger muscles; rerun `Build Hand Poses`
- `Assets/_Game/Editor/Characters/HandPoseCapture.cs` -- grip / penetration measurement; `bow` case uses `Standing Aim Overdraw.fbx`
- `Assets/_Game/Art/Characters/Humanoids/Animations/Combat/Bow/*.fbx` -- Draw arrow / Aim Overdraw / Fire arrow (states `BowDrawArrow_State` / `BowAim_State` / `BowFire_State`), Draw Bow / Sheathe Bow (`WeaponSwap` in the importer `clipAnimations` events)
- `Assets/_Game/Art/Characters/Humanoids/Controllers/Humanoid_Template.controller` -- UpperBody bow states (SMB on `BowAim_State` must survive)
- `Assets/_Game/Scripts/Combat/BowAimCorrection.cs` -- `_arrowLineLocal` / `_arrowOriginLocal` (Player + NPC_base Variant)

## Tasks & Acceptance

**Execution:**
- [x] `Archetype_Bow.asset` -- fit `drawnPose` on the left socket: handle axis across the palm (~20–30° diagonal like a pistol grip), its string-side face (z 0.065) against the palm, string toward the thumb-side / right hand -- grip geometry
- [x] `HandPoseBuilder.cs` -- retune `BowGrip` fingers for the new placement, rebuild the hand poses -- fingers wrap the handle
- [x] `Assets/_Game/Editor/Characters/BowClipWristBaker.cs` -- `Tools/Characters/Bake Bow Wrist Clips`: bake `Draw arrow` / `Aim Overdraw` / `Fire arrow` into `Combat/Bow/*.anim` with constant left-wrist offsets (fitted so the bow is upright, string toward the right hand), and point the three UpperBody states at them -- wrist fix, reproducible
- [x] `Archetype_Bow.asset` -- derive `sheathedPose` from the Draw Bow grab frame (seamless), then check against the torso -- bow on the back where the hand takes it
- [x] `Standing Equip Bow.fbx.meta`, `Standing Disarm Bow.fbx.meta` -- move `WeaponSwap` to the grab frames -- no teleport
- [x] `HandPoseCapture.cs` -- bow case samples the baked `Aim Overdraw.anim` -- verification follows the shipped clip
- [x] `Assets/Tests/EditMode/HumanoidTemplateControllerTests.cs` -- bow states use the baked `.anim` clips (event-free, `Aim` loops), `SMB_BowAimState` still on `BowAim_State` -- structure guard
- [x] `BAKE_RECIPES.md`, `BOW.md`, `Prefabs/Items/Weapons/CLAUDE.md`, `deferred-work.md` -- bake recipe, grab frames, sheathed pose rule; right-hand string pose deferred -- docs

**Acceptance Criteria:**
- Given the bow clips sampled at 10 / 50 / 90 %, when `HandPoseCapture` measures the left hand, then the I/O grip limits hold and no finger skin is inside the palm.
- Given Draw Bow / Sheathe Bow sampled at their event frame, when the palm-to-grip distance is measured, then it is ≤ 4 cm, and at Draw Bow the back and hand bow poses match within 1 cm / 5°.
- Given StartingTown in play mode, when the player draws the bow, aims, fires and sheathes, then screenshots show the left hand closed on the handle at every step and no visible bow jump at the swap.
- Given the EditMode suite, when run, then all tests pass.

## Spec Change Log

## Design Notes

The bow is rigid in the left socket, so the in-hand grip is the same in every clip: one `drawnPose` + one finger pose
fix them all. The wrist bake only restores the bow's world orientation that a proper grip changes. Muscle names:
`Left Hand Down-Up`, `Left Hand In-Out`, `Left Forearm Twist In-Out`. Deriving `sheathedPose`:
`inverse(BackWeaponSocket) × LeftWeaponSocket × drawnPose` at the Draw Bow event frame (preview-scene sample, as
`HandPoseCapture`). Sampling frames: palm-to-back-grip minimum (current pose: Draw Bow 0.30, Sheathe Bow ~0.375); re-measure
with the new sheathed pose.

## Verification

**Commands:**
- MCP `run_tests` EditMode -- expected: all pass
- MCP `read_console` -- expected: no compile errors

**Manual checks:**
- Close-ups + body renders in `D:/claude-scratch/bowhands/after*` next to `before*`; in-game screenshots (draw, aim, fire, sheathe).

## Design Notes (implementation)

- **In-hand fit:** `drawnPose` = pos (0.0228, −0.0276, −0.0626), rot (347.22, 337.61, 300.23): handle axis = palm cross
  axis tilted 30° toward the fingers, centre 4.6 cm out along the palm normal. `BowGrip` re-fit with coupled joints
  (DIP ≈ 0.65 × PIP). `HandPoseCapture` on all three baked clips at 10 / 50 / 90 % and the guard: no finger skin inside
  the palm, palm 24 mm, tips Th 12 / In 7 / Mi 8 / Ri 2 / Li 3 mm, index distal joint −1 mm (before: Mi −15, Th −9, Li +30).
- **Wrist:** one offset set (Down-Up 0.819, In-Out 0.594, Twist 0.488) fitted on Aim Overdraw against the previous in-hand
  orientation: 11° (3° on the limb axis). Fire arrow mean 23° / max 33°, Draw arrow mid-clip up to 55° (bow lowered, as in
  the source): **user-accepted** (Ask First). Hand bone moves 0 mm relative to UpperChest → arrow line unchanged.
- **Back:** `sheathedPose` = pos (−0.038, −0.138, −0.083), rot (359.8, 0.1, 167.4) = in-hand pose at Draw Bow 0.30. Torso
  check (spine / hips / neck / shoulder skin vs bow vertices): 0 inside at the grab frame, Idle, Running. The bow hangs near
  vertical along the spine (**user-kept**). Sheathe Bow released at 0.325 (palm 2 cm, bow turns ~49°: **user-accepted,
  deferred**).
- **Evidence:** EditMode 1938 / 1938. Captures `D:/claude-scratch/bowhands/{before,after}` and `{before,after}_body`;
  in-game (StartingTown, player driven through `PlayerCombat` handlers at timeScale 0.03–0.1, other renderers hidden):
  `D:/claude-scratch/bowhands/ingame/` — draw (bow by the handle out of the back), nock, aim, fire (arrow spawned), sheathe.
- **Review patches:** broken doc splices fixed; test finds UpperBody by name; stale-bake guard test (source + offset per
  key, length, curve count); baker warns if a source gains events; deferred-work cleaned.

## Suggested Review Order

**Bow placement in the hand and on the back (entry point)**

- Handle across the palm (drawn) and seamless back pose derived from the grab frame.
  [`Archetype_Bow.asset:16`](../../Assets/_Game/Data/Items/Weapons/Archetypes/Archetype_Bow.asset#L16)

- Fingers re-fit to wrap the 5.5 × 4.2 cm handle.
  [`HandPoseBuilder.cs:81`](../../Assets/_Game/Editor/Characters/HandPoseBuilder.cs#L81)

**Wrist bake (keeps the aiming bow upright)**

- One offset set for the three clips: no wrist jump between bow states.
  [`BowClipWristBaker.cs:34`](../../Assets/_Game/Editor/Characters/BowClipWristBaker.cs#L34)

- Validates first, bakes, rewires the UpperBody states.
  [`BowClipWristBaker.cs:49`](../../Assets/_Game/Editor/Characters/BowClipWristBaker.cs#L49)

- Clamp rule: never pushes a muscle further out of range than the source.
  [`BowClipWristBaker.cs:83`](../../Assets/_Game/Editor/Characters/BowClipWristBaker.cs#L83)

- Copies every curve and the clip settings; offsets only the three wrist muscles.
  [`BowClipWristBaker.cs:89`](../../Assets/_Game/Editor/Characters/BowClipWristBaker.cs#L89)

**Grab timing**

- Draw Bow WeaponSwap 0.45 → 0.30 (hand on the back grip).
  [`Standing Equip Bow.fbx.meta:60`](../../Assets/_Game/Art/Characters/Humanoids/Animations/Combat/Bow/Standing%20Equip%20Bow.fbx.meta#L60)

- Sheathe Bow WeaponSwap 0.65 → 0.325 (palm closest to the grip).
  [`Standing Disarm Bow.fbx.meta:60`](../../Assets/_Game/Art/Characters/Humanoids/Animations/Combat/Bow/Standing%20Disarm%20Bow.fbx.meta#L60)

**Peripherals**

- Bow states on the bakes, SMB kept, stale-bake guard, swap frames.
  [`HumanoidTemplateControllerTests.cs:197`](../../Assets/Tests/EditMode/HumanoidTemplateControllerTests.cs#L197)

- Capture tool samples the shipped baked clip.
  [`HandPoseCapture.cs:54`](../../Assets/_Game/Editor/Characters/HandPoseCapture.cs#L54)

- Bake recipe and numbers.
  [`BAKE_RECIPES.md:169`](../../Assets/_Game/Art/Characters/Humanoids/Animations/Combat/BAKE_RECIPES.md#L169)

- Archetype rules: drawn = grip, sheathed = grab-frame pose.
  [`Weapons/CLAUDE.md:112`](../../Assets/_Game/Prefabs/Items/Weapons/CLAUDE.md#L112)
