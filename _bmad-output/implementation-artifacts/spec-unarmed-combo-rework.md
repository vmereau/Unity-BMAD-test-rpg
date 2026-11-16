---
title: 'Unarmed combo rework (lead jab → hook → uppercut) + unarmed draw / sheathe'
type: 'feature'
created: '2026-11-16'
status: 'done'
baseline_commit: 'e42c8763bf7ddb8499394d7f4a6633bec5f7339d'
context:
  - '{project-root}/.claude/rules/attack-pipeline.md'
  - '{project-root}/Assets/_Game/Scripts/Combat/PLAYER_COMBO.md'
  - '{project-root}/Assets/_Game/Art/Characters/Humanoids/Controllers/CLAUDE.md'
  - '{project-root}/Assets/_Game/Art/Characters/Humanoids/Animations/Combat/CLAUDE.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** The unarmed combo is Jab → Uppercut → Jab: one clip twice, every hit from the right hand, no finisher
damage bonus, and unarmed draw / sheathe plays the sword clips as a placeholder.

**Approach:** Three distinct steps — `lead jab` (left hand) → `Hook` (right) → `Uppercut` (left, finisher) — driven by
per-step data in `CombatConfigSO` (forward step, hand, damage / stance multipliers), plus `draw unarmed` /
`sheath unarmed` as the base draw / sheathe clips. `(old) Jab.fbx` ends unreferenced but stays on disk.

## Boundaries & Constraints

**Always:**
- Existing pipeline only: clip events → receiver → `PlayerCombat` → `WeaponHitbox`; `TryReceiveHit` → `TakeDamage` →
  stance. No hardcoded tunables.
- Every step moves forward (distance > 0); the finisher's distance, health damage and stance damage are the largest.
- A step opens only its own hand's hitbox, only between its `HitboxEnable` / `HitboxDisable` events; every
  end-of-attack path closes both hands.
- Event times come from measured contact frames (hand-speed / reach sampling), never copied from another clip.
- Sword, two-handed and bow keep their current clips and behaviour after the override re-key.
- Controller / override / importer edits through the Unity API (`execute_code`), never raw YAML.
- Blender MCP only to fix the clips (trim, retime, in-place cleanup, end → start pose blending); re-export to the
  same FBX path, GUID unchanged.

**Ask First:**
- Changing a combo transition's duration / exit time on `Humanoid_Template` (shared with sword and two-handed).
- Any Blender edit beyond trim / retime / root cleanup / pose blending.
- Retuning NPC `AttackRange` / `EngageStoppingDistance` if the new clips change NPC reach.

**Never:**
- Delete `(old) Jab.fbx`. Animate from scratch. Commit before the user approves the feel.
- Implement hook / uppercut before the user approves the lead jab (checkpoint 2).
- Change weapon combos, the stance rules, or NPC damage values.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Full chain | 3 presses, each inside the previous step's combo window | jab → hook → uppercut, each cross-fading at the press; a 4th press is ignored; next press after the end starts step 1 | N/A |
| No chain | 1 press | lead jab plays out with its recovery, returns to unarmed idle, combo resets | N/A |
| Hand per step | step 1 / 2 / 3 hit window | only the left / right / left hitbox is open; the other hand never deals damage | hitbox for that hand missing → warn once, fall back to the other hand |
| Finisher | step 3 connects | health damage × step 3 `damageMultiplier`, stance × step 3 `stanceMultiplier`; steps 1–2 use ×1 | step entry missing in config → ×1, right hand, `defaultAttackStep` |
| Cancel mid-step | dodge / block / jump / equip during a window | both hand hitboxes closed, combo reset | N/A |
| Weapon equipped | sword / 2H / bow | weapon hitbox and archetype data only; unarmed step data ignored | N/A |
| Draw / sheathe | R while unarmed | `Draw unarmed` / `sheath unarmed` play on UpperBody; attacks unlock at `WeaponSwap` | event missing → state exit / watchdog completes the switch |
| NPC unarmed | NPC rolls a 1–3 hit combo | same three clips; each step hits with its own hand | unknown hitbox id → existing one-time warning |

</frozen-after-approval>

## Code Map

- `Assets/_Game/Art/Characters/Humanoids/Animations/Combat/Unarmed/` -- `lead jab` (1.80 s), `Hook` (2.17 s), `Uppercut` (1.33 s, 4 old events), `Draw unarmed` (1.00 s), `sheath unarmed` (1.07 s); new clips have no events
- `.../Controllers/Humanoid_Template.controller` -- Base `Attack_1/3_State` = `Unarmed Jab` ((old) Jab), `Attack_2_State` = `Uppercut`; UpperBody `Draw_State` / `Sheathe_State` = sword clips
- `.../Combat/one handed/Sword_AnimatorOverride`, `two handed/TwoHanded_AnimatorOverride`, `Bow/Bow_AnimatorOverride` -- keyed on the base clips being replaced
- `Assets/_Game/ScriptableObjects/Config/CombatConfigSO.cs` + `Data/Config/CombatConfig.asset` -- `unarmedComboSteps`, `unarmedAttackSteps`, `unarmedStanceDamage`, `finisherStanceMultiplier`
- `Assets/_Game/Scripts/Combat/PlayerCombat.cs` -- `BindUnarmedHitbox`, `_activeHitbox`, `OnAttackStateEntered`, `ResolveAttackStep`, `ComputeEffectiveDamage`, `ComputeMeleeStanceDamage`
- `Assets/_Game/Prefabs/Player/Player.prefab` -- `UnarmedHitbox` under `RightHand/WeaponSocket` only
- `Assets/_Game/Scripts/AI/NpcArmament.cs`, `Prefabs/Characters/Humanoid_BaseHero.prefab`, `Prefabs/Entities/Humanoids/NPC_base Variant.prefab` -- NPC `Unarmed` hitbox (right hand); events with id `""` open every hitbox
- `Assets/Tests/EditMode/` -- `ComboWindowTests`, `StanceMathTests`, `NpcModelPrefabTests` (asserts exactly 1 hitbox)

## Tasks & Acceptance

**Execution:**
- [x] GUID audit (done in planning) -- `Jab.fbx` → `(old) Jab.fbx` and `Uppercut Jab.fbx` → `Uppercut.fbx` are renames with GUIDs kept; controller and overrides still resolve. Nothing to repair; record in the spec
- [x] `ScriptableObjects/Config/UnarmedAttack.cs` (new) -- serializable struct: `AttackStep step`, `WeaponHand hand`, `damageMultiplier`, `stanceMultiplier` (default 1) + static `Resolve(array, attackIndex, fallback)` -- per-step data in one place
- [x] `CombatConfigSO.cs` + `CombatConfig.asset` -- replace `unarmedAttackSteps` with `unarmedAttacks` (3 entries: L / R / L, distances ≈ 0.25 / 0.35 / 0.5, finisher damage × 1.5, stance × 2); `OnValidate` keeps `unarmedComboSteps` ≤ entry count
- [x] `PlayerCombat.cs` -- second serialized hitbox `_unarmedHitboxLeft`; bind / unbind both; `OnAttackStateEntered` closes the current hitbox then selects the step's hand; damage and stance use the entered step's multipliers (unarmed stance uses the step multiplier instead of the global `finisherStanceMultiplier`)
- [x] `Player.prefab` -- `UnarmedHitbox_L` under `LeftHand/LeftWeaponSocket` (copy of the right one), wired
- [x] Lead jab clip -- import settings like the other attack clips, in-place check, Blender trim if needed, four events at measured times with string id `UnarmedLeft`; `Attack_1_State` motion → `lead jab`; step 1 timings in config. **CHECKPOINT 2: stop for approval**
- [x] Hook + Uppercut clips -- same treatment (ids `Unarmed` / `UnarmedLeft`; Uppercut loses its combo window); `Attack_2_State` → `Hook`, `Attack_3_State` → `Uppercut`; start poses blended to the previous step's window pose
- [x] Draw / sheathe -- `WeaponSwap` event on both clips; `Draw_State` / `Sheathe_State` motions → unarmed clips
- [x] Overrides -- re-key so each family keeps its clips: sword `lead jab`→`SwordAttackLeft`, `Hook`→`SwordAttackThrust`, `Uppercut`→`SwordAttackLeft`, draw / sheathe → sword clips; 2H likewise (`2hand attack refined` / `attack 2 refined` / `attack refined`, `2hand draw` / `sheath`); bow draw / sheathe
- [x] NPC -- left-hand `UnarmedHitbox_L` on the NPC model prefab, registered as `UnarmedLeft` by `NpcArmament` next to `Unarmed`; re-measure reach
- [x] `Assets/Tests/EditMode/` -- `UnarmedAttackTests` (resolve, fallback, multipliers), a `CombatConfig.asset` data test (3 steps, L / R / L, distances > 0 with the finisher largest, finisher multipliers > 1), update `NpcModelPrefabTests`
- [x] Docs -- `PLAYER_COMBO.md`, `Scripts/Combat/CLAUDE.md`, Combat animations `CLAUDE.md` (timing tables, folder list), `Controllers/CLAUDE.md`, `Scripts/AI/CLAUDE.md`, `Prefabs/Player` + `Prefabs/Entities` CLAUDE.md, `project-context.md` unarmed line

**Acceptance Criteria:**
- Given the project, when searching assets for GUID `fe9222d19fa70f74f9c660807a35fc5f`, then only `(old) Jab.fbx.meta` matches.
- Given a sword, two-handed weapon or bow equipped, when attacking and drawing / sheathing, then the same clips play as before this change.
- Given each of the three clips sampled on the Player rig, when its hit window is open, then the assigned hand is at or near full extension toward the target.
- Given play mode with the unarmed player, when running the full combo and R twice, then the console shows no errors.
- Given the EditMode suite, when run, then all tests pass.

## Design Notes

- **Two data sources for the hand, one per receiver.** The player picks the hand from `CombatConfigSO` (its receiver
  is parameterless; Unity still calls it when the event carries a string). NPCs share the clips and already route
  `HitboxEnable(string id)`, so the events carry the id. Without it an NPC's right-hand-only hitbox would whiff on
  steps 1 and 3.
- **Multipliers follow the entered attack state**, not `_comboStep` (which advances on the press).
- **Transitions are shared.** Offsets stay 0; pose matching is done in the clips, so sword and 2H blends are untouched.
  Each `ComboWindowClose` stays below its state's exit time (0.81 / 0.89).

## Verification

**Commands:**
- Unity MCP `run_tests` (EditMode) -- expected: all green
- `grep -rl fe9222d19fa70f74f9c660807a35fc5f Assets` -- expected: only `(old) Jab.fbx.meta`

**Manual checks (if no CLI):**
- `execute_code` dump of `Humanoid_Template` attack / draw states and the three overrides matches the task list.
- Play mode (stepped with `EditorApplication.Step()`): hit logs show one hit per window, from that step's hand; console clean.
- User feel test, then commit.

## Implementation Notes (2026-11-16)

- **GUID audit:** `Jab.fbx` → `(old) Jab.fbx` and `Uppercut Jab.fbx` → `Uppercut.fbx` were renames with GUIDs kept; nothing was
  broken. After the re-key, GUID `fe9222d1…` is referenced only by `(old) Jab.fbx.meta` (searched all of `Assets`).
- **No Blender edit.** In-place cleanup is done by import settings (root XZ not baked, rotation baked on Original
  orientation); the Hook's first 10 frames are trimmed in the importer, which also matches its start pose to the lead jab's
  combo-window pose (hip yaw 78° / 77°). **Deviation:** the Uppercut's start is not trimmed or pose-blended — the 0.42 s
  combo blend covers its drop-back. Left for the feel test.
- **Final values:** events and step data are in the timing tables of `PLAYER_COMBO.md` and the Combat animations `CLAUDE.md`.
  Lead jab step is 0.30 m (not the proposed 0.25): it matches the clip's lunge and keeps the rear foot planted.
- **Verified:** EditMode 1532 / 1532. Play mode (stepped): full chain, each step opens only its own hand, 1.15 m travelled,
  combo resets; draw / sheathe release the switch lock at `WeaponSwap`; no console errors; every unarmed NPC in StartingTown
  has `Unarmed` + `UnarmedLeft` registered.
- **Not verified in play:** finisher damage / stance multipliers on a live target; NPCs landing the new punches (reach was
  measured on the Player rig only — the uppercut has ~0.1 m margin at the 1.3 m stopping distance); sword / two-handed /
  bow clips (checked from the override listings and an EditMode test, not played).
- **Review (3 reviewers, no high-severity finding). Patched:** config code defaults = asset values; `OnValidate` turns a 0
  multiplier into 1; `_currentAttackIndex` reset in `EndAttack`; Loop Pose off on `Uppercut`; one-handed timing table
  matches the real override; tests for the Player prefab wiring and the override mapping. **Left as is (pre-existing):**
  `_currentWeaponSO` vs equipment disagreement in the startup race; `CombatConfig.asset` re-serialised the `execute*` fields.

## Suggested Review Order

**Per-step data**

- One entry per attack state: step, hand, damage and stance multipliers
  [`UnarmedAttack.cs:13`](../../Assets/_Game/ScriptableObjects/Config/UnarmedAttack.cs#L13)

- Replaces `unarmedAttackSteps`; validation caps the combo and repairs zero multipliers
  [`CombatConfigSO.cs:146`](../../Assets/_Game/ScriptableObjects/Config/CombatConfigSO.cs#L146)

- Authored values: L / R / L, 0.30 / 0.35 / 0.50 m, finisher ×1.5 / ×2
  [`CombatConfig.asset:72`](../../Assets/_Game/Data/Config/CombatConfig.asset#L72)

**Hand selection and hit windows**

- State enter closes the window, then picks the step's hand
  [`PlayerCombat.cs:954`](../../Assets/_Game/Scripts/Combat/PlayerCombat.cs#L954)

- The hand comes from config; a missing hitbox falls back with one warning
  [`PlayerCombat.cs:626`](../../Assets/_Game/Scripts/Combat/PlayerCombat.cs#L626)

- Both fists are bound; guarded against a double `OnHit` subscription
  [`PlayerCombat.cs:551`](../../Assets/_Game/Scripts/Combat/PlayerCombat.cs#L551)

- Every close path shuts both hands
  [`PlayerCombat.cs:608`](../../Assets/_Game/Scripts/Combat/PlayerCombat.cs#L608)

**Damage and stance**

- Health damage × the entered step's multiplier, unarmed only
  [`PlayerCombat.cs:643`](../../Assets/_Game/Scripts/Combat/PlayerCombat.cs#L643)

- Unarmed stance uses the step multiplier instead of the global finisher one
  [`PlayerCombat.cs:1081`](../../Assets/_Game/Scripts/Combat/PlayerCombat.cs#L1081)

- New overload: per-step multiplier, then Power Strike
  [`StanceMath.cs:65`](../../Assets/_Game/Scripts/Combat/StanceMath.cs#L65)

- Index cleared at attack end so stale events can't reuse finisher multipliers
  [`PlayerCombat.cs:471`](../../Assets/_Game/Scripts/Combat/PlayerCombat.cs#L471)

**NPCs (shared controller)**

- Left fist id, registered and unregistered together with the right one
  [`NpcArmament.cs:36`](../../Assets/_Game/Scripts/AI/NpcArmament.cs#L36)

**Clips, controller, overrides (docs hold the measured values)**

- Event times, measured contact frames, import settings and why XZ is unbaked
  [`CLAUDE.md:110`](../../Assets/_Game/Art/Characters/Humanoids/Animations/Combat/CLAUDE.md#L110)

- Base clips per state and the override re-key rule
  [`CLAUDE.md:85`](../../Assets/_Game/Art/Characters/Humanoids/Controllers/CLAUDE.md#L85)

- Player-side contract: hand, multipliers, two data sources to keep in sync
  [`PLAYER_COMBO.md:107`](../../Assets/_Game/Scripts/Combat/PLAYER_COMBO.md#L107)

**Tests**

- Player prefab has a wired fist hitbox per hand
  [`UnarmedAttackTests.cs:121`](../../Assets/Tests/EditMode/UnarmedAttackTests.cs#L121)

- Sword, two-handed and bow overrides map every replaced base clip
  [`UnarmedAttackTests.cs:144`](../../Assets/Tests/EditMode/UnarmedAttackTests.cs#L144)
