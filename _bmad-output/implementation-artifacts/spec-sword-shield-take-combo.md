---
title: 'One-handed sword: 3-hit combo from Mixamo sword-and-shield clips + combo press buffer + off-hand arm layer'
type: 'feature'
created: '2026-11-25'
status: 'done'
baseline_commit: '0b35d04c'
context:
  - '{project-root}/Assets/_Game/Scripts/Combat/PLAYER_COMBO.md'
  - '{project-root}/Assets/_Game/Art/Characters/Humanoids/Animations/Combat/CLAUDE.md'
  - '{project-root}/Assets/_Game/Art/Characters/Humanoids/Controllers/CLAUDE.md'
  - '{project-root}/_bmad-output/implementation-artifacts/spec-sword-three-hit-combo.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** The sword combo is two in-place clips from another source pushed by a scripted step; the Meshy replacement
(spec-sword-three-hit-combo) was rejected on look because weaponless motion carries no blade weight. Nothing poses the
left arm for an off-hand item other than a shield.

**Approach:** (A) Use three separate Mixamo sword-and-shield attacks — `slash` (down-cut), `slash (3)` (rising cut),
`slash (4)` (leaping overhead finisher) — each starting and ending in `sword and shield idle`, which becomes the
sword's combat idle. The player is moved by clip root motion; a combo press made during the swing is buffered and
fires when the window opens. (B) Add a `LeftArm` Animator layer that holds a torch pose when the off-hand item asks
for it. Shield and empty hand keep the clip's own arm.

## Boundaries & Constraints

**Always:**
- Whole clips, native travel, `rootMotionScale` 1 on all three steps. Hits 1–2 lunge and step back to the guard when
  the chain stops; a chained press fires before the step back.
- Import: Humanoid, avatar created from the model (as `AttackLeft.fbx`). Root rotation and Y baked on Original,
  root XZ **not** baked. Idle: Loop Time + Loop Pose.
- Events: hits 1–2 carry `HitboxEnable` / `HitboxDisable` / `ComboWindowOpen` / `ComboWindowClose`. Hit 3 carries no
  `ComboWindowOpen`. Hit windows are timed on the sword-hand speed peak on the Player rig, and a chained clip opens its
  hitbox no earlier than 0.30 s. The window opens after the hit and closes before the state's exit time.
- Combo press buffer (`CombatConfigSO.comboPressBufferTime`, all weapons): a press during the swing fires at the
  window open.
- The axe keeps today's combo: a new `Axe_AnimatorOverride` copies the current sword override, and the axe archetype
  points to it.
- `LeftArm` layer: index 4 (after the hand layers), Override, mask = left arm only (no fingers). Weight 1 only while
  the off-hand item's pose is not `None` and the Base state is not full-body (same tags as `UpperBodyWeight`, attacks
  included), stepped over 0.15 s. Shield, empty hand and NPCs today → weight 0.
- The torch pose (idle and locomotion) holds the hand above the hip and left of the body; attacks play the clip's own
  left arm, which swings clear of the blade (user decision 2026-11-25: no fixed pose cleared it).

**Ask First:**
- **After the chain first plays in game (before tuning windows or travel and before docs): show it to the user.**
  If rejected, stop and report.
- Recovery clips authored in Blender; scaling travel; adding an input buffer for the combo press; any change to the
  shared 0.42 s combo transitions, stamina, damage or stance values.

**Never:** Meshy generations; events on files still in `Assets/mixamo packs/`; a runtime IK for the left arm;
`applyRootMotion` or root motion for NPCs; a torch item, light or prefab; pack locomotion, block, impact or death clips.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Chain | Sword, presses at window open / close | 3 clips, moved by the clips, no pose pop (velocity jump in a crossfade ≤ the clip's own peak), sideways ≤ 8 cm | Move the seam ±3 f |
| Stop after hit 1 / 2 | No further press | Exit to the idle | Recovery clip → Ask First |
| Shield / empty | Round, kite or none | The clip's arm; shield-to-blade ≥ the idle's own value | Report |
| Torch stand-in | Off-hand item with `TorchHold` | Arm layer weight 1 in idle / locomotion, 0 in attacks (clip's arm), fist throughout | Re-bake the pose |
| Full-body | Attack, dodge, stagger, death with the torch | Arm layer weight → 0 | N/A |
| Axe / unarmed / 2H / bow | Any | Unchanged clips, steps and timings | N/A |
| NPC | Sword guard, 1–3 hits | New clips on scripted steps tuned to the player's travel | Tune `attackSteps` |

</frozen-after-approval>

## Code Map

- `Assets/mixamo packs/Sword and Shield Pack/` -- untracked source; `slash`, `slash (3)`, `slash (4)` and `idle` move out of it (FBX + `.meta`, GUID kept)
- `Art/Characters/Humanoids/Animations/Combat/one handed/` -- `Sword_AnimatorOverride` (keys `lead jab step` / `hook step` / `uppercut step` / `Unarmed Idle Guard`), `AttackLeft.fbx.meta` (Mixamo import template)
- `Data/Items/Weapons/Archetypes/Archetype_OneHandedSword.asset`, `Archetype_OneHandedAxe.asset` -- `attackSteps`, `defaultComboSteps` 2, override GUID `5e410d60…`; `Weapon_big sword.asset` keeps `comboSteps` 2 (plays hits 1–2)
- `Art/Characters/Humanoids/Controllers/Humanoid_Template.controller` -- Attack_1/2/3 on Base (combo blend 0.423 s, exit 0.81 / 0.89 / 0.90); layers Base / UpperBody / LeftHand / RightHand
- `Scripts/Core/Animations/HandPoseDriver.cs`, `HandPoseRule.cs`, `HumanoidAnimationBridge.cs`, `UpperBodyWeight.cs` -- per-frame equipment polling (player + NPC), layer weight by name
- `Editor/Characters/HandPoseBuilder.cs` -- pattern for building a mask and a layer in code; `BowIdleBaker` -- pattern for baking a pose clip
- `ScriptableObjects/Items/EquipableItemSO.cs`, `Scripts/Inventory/EquipmentSystem.cs` -- `Offhand` slot item
- `Tools/Blender/sword_combo_play_measure.cs`, `sword_blade_measure.cs`, `action_root_motion_analyse.py` -- measurement harness
- `Assets/Tests/EditMode/HumanoidTemplateControllerTests.cs` (layer list), `UnarmedAttackTests.cs` (override keys)

## Tasks & Acceptance

**Execution:**
- [x] `Combat/one handed/sword and shield/` -- `sword and shield slash`, `slash (3)`, `slash (4)`, `idle` (+ `.meta`, GUIDs kept) moved from the pack, Humanoid, clips `sword shield cut` / `rising cut` / `overhead` / `idle`, events in seconds from the sword hand on the Player rig -- the clips
- [x] `sword shield overhead.fbx` -- `Tools/Blender/mixamo_retime.py` + `configs/sword/sword_shield_overhead.json`: the finisher's rise slowed ×1.5 + 0.2 s hold (user: "ends abruptly") -- finisher ending
- [x] `one handed/Axe_AnimatorOverride.overrideController` (+ `.meta`), `Archetype_OneHandedAxe.asset` -- copy of the old sword override -- the axe is unchanged
- [x] `Sword_AnimatorOverride.overrideController` -- three attack keys and the idle key → new clips -- wiring
- [x] `Archetype_OneHandedSword.asset` -- `defaultComboSteps` 3; three steps, `rootMotionScale` 1, NPC distances = the player's buffered in-chain travel -- data
- [x] `Scripts/Combat/ComboPressBuffer.cs` + `PlayerCombat` + `CombatConfigSO.comboPressBufferTime` (0.4 s) -- buffered combo press (user decision) -- input
- [x] Play check → **Ask First checkpoint with the user** (take rejected, separate clips accepted)
- [x] `ScriptableObjects/Items/OffhandArmPose.cs` + `EquipableItemSO.offhandArmPose` + `EquipmentVisuals.OffhandArmPose` -- what the off-hand asks for
- [x] `Editor/Characters/OffhandArmLayerBuilder.cs` -- `Tools/Characters/Build Offhand Arm Layer`: `OffhandPoses/LeftArmMask.mask`, `Torch Hold.anim`, layer 4 -- reproducible layer
- [x] `Scripts/Core/Animations/OffhandArmRule.cs` + `HandPoseDriver` + `HumanoidAnimationBridge.SetOffhandArmWeight` -- runtime
- [x] `Assets/Tests/EditMode/` -- `SwordShieldComboTests`, `OffhandArmRuleTests`, `ComboPressBufferTests`, `HumanoidTemplateControllerTests` (layer 4), `UnarmedAttackTests` (axe override) -- regression net
- [x] Measure -- chains at earliest / latest / buffered press, round and kite shield, torch stand-in, one sword NPC -- report below
- [x] Docs -- `Combat/CLAUDE.md`, `BAKE_RECIPES.md`, `PLAYER_COMBO.md`, `Controllers/CLAUDE.md`, `Scripts/Core/Animations/CLAUDE.md`, `Items/CLAUDE.md`, `attack-pipeline.md`, `Tools/Blender/README.md`

**Acceptance Criteria:**
- Given a sword, when three attacks chain, then the three clips play in order, the player moves by the clips, and the finisher keeps the stance bonus and the recovery cancel.
- Given the sword drawn and idle, then `sword shield idle` loops, and hit 1 starts from its pose and hit 3 ends on it.
- Given an off-hand item with `TorchHold`, then the left arm holds the torch pose in idle and locomotion, and releases it in attacks and full-body states.
- Given the EditMode suite, when run, then it passes.

## Spec Change Log

- **2026-11-25, user renegotiation after the first in-game look:** the split take (`slash (2)`) was rejected ("transitions,
  combo endings and movement are rough"); replaced by the pack's separate attacks `slash` / `slash (3)` / `slash (4)`,
  which start and end in the idle. The ±3-frame seam rules went with it. KEEP: the press buffer, the import recipe.
- **2026-11-25, Ask First (input buffer):** the take's hit 1 → 2 seam fell after Attack_1's exit time (0.06 s window);
  the user chose the combo press buffer (all weapons).
- **2026-11-25, user request:** the finisher "ends abruptly" → its rise retimed in Blender (not a recovery clip).
- **2026-11-25, user decision (torch):** no fixed torch pose cleared the sword (6 cm low-front, 1.7 cm at shoulder
  height): attacks release the `LeftArm` layer and play the clip's arm.

## Implementation Notes (2026-11-25)

Measured in play (StartingTown open ground, `Weapon_ShortIronSword`, `PlayerCombat.TryAttack`, 80–140 fps):

| Run | Travel cut / rising / overhead (m) | Total | Sideways peak / end | Crossfade velocity jump (hips / sword hand) vs clip peak |
|-----|-----|-----|-----|-----|
| Single cut | 0.11 (peak 0.55, steps back) | 0.11 | 0.18 / 0.03 | — |
| Buffered chain (press re-sent every frame from 0 s, or from 0.45 s → fires at the 0.72 s window open) | 0.52 / 0.39 / 0.78 | 1.70 | 0.38 / 0.15 | 0.56 / 2.87 vs 0.80 / 5.04 m/s |
| Latest press (1.15 s / 1.42 s) | 0.42 / −0.33 / 0.96 | 1.04 | 0.27 / 0.04 | 0.41 / 2.00 vs 0.72 / 5.71 m/s |
| Round shield, buffered | 0.52 / 0.40 / 0.79 | 1.71 | 0.38 / 0.15 | 0.58 / 2.79 vs 0.73 / 4.91 m/s |
| Kite shield, latest | 0.42 / −0.36 / 0.96 | 1.03 | 0.27 / 0.04 | 0.26 / 0.97 vs 0.76 / 5.52 m/s |

- Planted-foot slide ≤ 2.3 cm. Root speed never 0 through the 1 → 2 crossfade; the 2 → 3 one passes 0 for 3–11 frames
  and goes backward (−0.87 m/s) as the rising cut steps back.
- The shield arm's frame-to-frame jump inside a crossfade (3.1–3.5 m/s) exceeds its own clip peak (2.6–3.4).
- Held shields touch the blade (0 m box-to-line) ~0.1 s: round at the rising cut's start, kite at the cut's end.
- Torch stand-in (in-memory `ShieldSO` with `TorchHold`, the placeholder cylinder stretched to a 0.5 m stick): weight 1
  in idle, 0 within 0.15 s of each attack start, back to 1 after; fist throughout. With the clip's arm: hand ≥ 0.16 m
  from the blade; the stick 0.26 / 0.12 m (cut / rising cut, early press) and 0 in the finisher's landing crouch.
- Finisher: 3.03 s, exits at 2.72 s; up to the landing it matches `slash (4)` within 0.2 cm.
- Sword NPC (RacketGuard, `BeginAttack(0, 3)` + `TriggerAttack`): cut → rising cut → overhead → Idle. Travel not measured.
- The harness re-sends its press every frame until the step advances (mashing), so the buffered rows are a mashed or
  held input. A **single** press is kept only if it is at most `comboPressBufferTime` (0.4 s) old when the window opens:
  from 0.32 s into the cut, from 0.57 s into the rising cut. Earlier single presses are dropped (not measured in play).
- Review patches: the `LeftArm` layer also releases in UpperBody actions (block, draw / sheathe, drink, bow aim); the
  retime script normalizes quaternions and reports sign flips (0 in `slash (4)`); tests pick the exit transition by its
  destination, check Loop Pose and the stall-watchdog gaps (finisher: 1.27 s from `ComboWindowClose` to the exit end,
  against 1.5 s).
- EditMode: 1991 / 1991 after the review patches.

**Deviations:** sideways ≤ 8 cm not met (0.38 m peak: the clips' own sway, judged fine on look); shield-to-blade below
the idle's value for ~0.1 s; the torch "≥ 0.25 m" rule replaced by the clip's arm (user); late presses lunge again after
a step back (−0.33 m on hit 2); the clip-exit leg snap (base `Idle` legs ≠ the pack's bladed stance) is not addressed.

## Verification

**Commands:**
- Unity MCP `run_tests` (EditMode) -- expected: all green
- `read_console` after the reimport and the layer build -- expected: no errors

**Manual checks:**
- `sword_combo_play_measure.cs` + `action_root_motion_analyse.py`: travel, drift, crossfade velocity jumps, cancels
- `sword_blade_measure.cs`: blade path, off-hand to blade per frame

## Suggested Review Order

**Combo press buffer (all weapons)**

- Entry point: a press before the window opens is stored instead of dropped.
  [`PlayerCombat.cs:455`](../../Assets/_Game/Scripts/Combat/PlayerCombat.cs#L455)

- The window-open event fires the stored press, so buffered chains start at the seam.
  [`PlayerCombat.cs:1038`](../../Assets/_Game/Scripts/Combat/PlayerCombat.cs#L1038)

- Cleared on every state enter and every reset: a press never outlives its swing.
  [`PlayerCombat.cs:1081`](../../Assets/_Game/Scripts/Combat/PlayerCombat.cs#L1081)

- Pure store / consume with an age limit.
  [`ComboPressBuffer.cs:27`](../../Assets/_Game/Scripts/Combat/ComboPressBuffer.cs#L27)

- Tunable 0.4 s; 0 restores the old drop.
  [`CombatConfigSO.cs:208`](../../Assets/_Game/ScriptableObjects/Config/CombatConfigSO.cs#L208)

**Sword clips and wiring**

- Clip table, timings and the lunge / step-back behaviour in one place.
  [`Combat/CLAUDE.md:69`](../../Assets/_Game/Art/Characters/Humanoids/Animations/Combat/CLAUDE.md#L69)

- Four override keys now point at the pack clips; the axe got its own copy.
  [`Sword_AnimatorOverride.overrideController:12`](../../Assets/_Game/Art/Characters/Humanoids/Animations/Combat/one%20handed/Sword_AnimatorOverride.overrideController#L12)

- Three clip-driven steps; NPC distances copied from the player's chain.
  [`Archetype_OneHandedSword.asset:32`](../../Assets/_Game/Data/Items/Weapons/Archetypes/Archetype_OneHandedSword.asset#L32)

- Finisher ending retimed: piecewise time map, quaternions normalized.
  [`mixamo_retime.py:23`](../../Tools/Blender/mixamo_retime.py#L23)

**Off-hand arm layer**

- The rule: pose only in idle / locomotion; every action plays the clip's arm.
  [`OffhandArmRule.cs:17`](../../Assets/_Game/Scripts/Core/Animations/OffhandArmRule.cs#L17)

- Driver steps the weight every frame, player and NPCs alike.
  [`HandPoseDriver.cs:65`](../../Assets/_Game/Scripts/Core/Animations/HandPoseDriver.cs#L65)

- Bridge owns the only `SetLayerWeight` call.
  [`HumanoidAnimationBridge.cs:114`](../../Assets/_Game/Scripts/Core/Animations/HumanoidAnimationBridge.cs#L114)

- Pose source: the Offhand item's field, read from the slot.
  [`EquipmentVisuals.cs:83`](../../Assets/_Game/Scripts/Inventory/EquipmentVisuals.cs#L83)

- Item field, append-only enum.
  [`EquipableItemSO.cs:20`](../../Assets/_Game/ScriptableObjects/Items/EquipableItemSO.cs#L20)

- Idempotent builder: mask, constant-muscle torch clip, layer 4.
  [`OffhandArmLayerBuilder.cs:33`](../../Assets/_Game/Editor/Characters/OffhandArmLayerBuilder.cs#L33)

**Tests**

- Clips: root motion import, event counts, windows before exit.
  [`SwordShieldComboTests.cs:112`](../../Assets/Tests/EditMode/SwordShieldComboTests.cs#L112)

- Stall-watchdog gaps (finisher margin 0.23 s).
  [`SwordShieldComboTests.cs:140`](../../Assets/Tests/EditMode/SwordShieldComboTests.cs#L140)

- Layer 4 shape: arm-only mask, weight 0, left-arm curves only.
  [`HumanoidTemplateControllerTests.cs:51`](../../Assets/Tests/EditMode/HumanoidTemplateControllerTests.cs#L51)
