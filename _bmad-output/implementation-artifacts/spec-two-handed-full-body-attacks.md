---
title: 'Full-body two-handed attacks, melee cancels and the 2H finisher'
type: 'feature'
created: '2026-10-30'
status: 'done'
baseline_commit: '55cd536'
context: ['{project-root}/Assets/_Game/Scripts/Combat/PLAYER_COMBO.md', '{project-root}/Assets/_Game/Art/Characters/Humanoids/Controllers/CLAUDE.md']
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** The `2hand attack` clip twists the hips up to 112° and leans 38°. The upper-body-masked Attack layer keeps hips and legs on locomotion, so in-game the torso, arms and Flamberge are 30–112° off the clip: the blade points up mid-swing. The hand line relative to the chest matches at 0°, so `TwoHandGripConstraint` is not the cause. Attack_2 also replays attack 1, because the new finisher `2hand attack 2.fbx` is not wired.

**Approach:** Add an unmasked Animator layer synced to the Attack layer. Its weight fades to 1 only while a two-handed (archetype `fullBodyAttacks`) attack plays, and the player can't move or turn during that swing. Block, dodge and jump cancel any melee swing at any moment. Wire `2hand attack 2` as Attack_2 of the 2H override.

## Boundaries & Constraints

**Always:**
- 1H, unarmed and the bow keep upper-body attacks and free movement.
- The full-body weight is 0 on NPCs: only the player driver writes it.
- Every Animator write goes through `HumanoidAnimationBridge` → `PlayerAnimationDriver`.
- Every cancel goes through `PlayerCombat.CancelAttack()`, which ends the combo and closes the hitbox.
- Clip events fired twice in one frame (once by the Attack layer, once by the synced layer) reach `PlayerCombat` only once.
- Lock-on keeps facing the target during a 2H swing.

**Ask First:** a change to the 1H or unarmed clips, the 2H combo length, or the stamina and stance values.

**Never:**
- No stamina refund on cancel.
- No root motion.
- No new attack trigger parameters.
- No Base-layer attack states driven by triggers (a trigger is consumed by one layer).

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| 2H swing | Flamberge drawn, Attack | Hips and legs follow the clip (pose matches the Weapon Pose Tuner); WASD ignored; no turning unless locked on | N/A |
| 2H combo | Attack inside the step-1 combo window | `2hand attack 2` plays full-body as the finisher | N/A |
| Block cancel | Any melee swing, any frame, block pressed, enough stamina | Attack cancelled (hitbox closed, combo reset), block raised | Not enough stamina: no block, the swing continues |
| Jump / fall cancel | Swing, jump pressed or player leaves the ground | Jump happens, attack cancelled, full-body weight fades out | N/A |
| Dodge cancel | 2H swing, dodge | Same as today; the full-body weight fades out | N/A |
| 1H swing | Sword, Attack | Unchanged: upper body only, can move | N/A |
| Duplicate event | HitboxEnable from both layers in the same frame | One `OnHitboxEnable` | N/A |

</frozen-after-approval>

## Code Map

- `Assets/_Game/Art/Characters/Humanoids/Controllers/Humanoid_Template.controller` -- layers Base / Attack (UpperBodyMask, weight 1); attack states and AnyState block / dodge / jump transitions live on Attack.
- `Assets/_Game/Art/Characters/Humanoids/Animations/Combat/two handed/` -- `2hand attack.fbx` (re-downloaded by the user, meta lost its `human` mapping), `2hand attack 2.fbx` (new, 1.27 s), `TwoHanded_AnimatorOverride` (Jab and Uppercut both → `2hand attack`).
- `Assets/_Game/ScriptableObjects/Items/Weapons/WeaponArchetypeSO.cs` + `WeaponSO` resolved accessors -- per-family flags.
- `Assets/_Game/Scripts/Player/PlayerStateManager.cs` -- `CanMove / CanBlock / CanJump` gates.
- `Assets/_Game/Scripts/Combat/PlayerCombat.cs` -- `ExecuteAttack`, `OnBlockStarted`, `Update` watchdogs, `CancelAttack`.
- `Assets/_Game/Scripts/Combat/AnimationEventReceiver.cs` -- clip event entry point.
- `Assets/_Game/Scripts/Core/Animations/HumanoidAnimationBridge.cs`, `Scripts/Player/PlayerAnimationDriver.cs` -- Animator owner and player driver.

## Tasks & Acceptance

**Execution:**
- [x] `two handed/2hand attack.fbx.meta` -- verify the humanoid avatar after the user's re-download (`human: []` + `autoGenerateAvatarMappingIfUnspecified` = Unity auto-maps the Mixamo rig at import: avatar valid / human, clip `humanMotion`); reimport clears the stale event warning -- a broken avatar would import the clip as generic.
- [x] `two handed/2hand attack 2.fbx.meta` -- import it like `2hand attack` (bake root rotation / Y / XZ into pose, no loop). Add a HitboxEnable / HitboxDisable pair around the fastest part of the swing (measured hand speed), and no combo window (it is the last step).
- [x] `two handed/TwoHanded_AnimatorOverride.overrideController` -- map the Attack_2 base clip (`Unarmed Uppercut`) to `2hand attack 2`.
- [x] `WeaponArchetypeSO.cs`, `WeaponSO` -- add the `fullBodyAttacks` bool and `WeaponSO.ResolvedFullBodyAttacks` (false without an archetype); set it true on `Archetype_TwoHanded.asset`.
- [x] `Humanoid_Template.controller` -- add a `FullBody` layer: synced to Attack, no mask, Override, default weight 0, every state's motion = the Attack layer's motion, no SMBs.
- [x] `HumanoidAnimationBridge` / `PlayerAnimationDriver` -- add `SetFullBodyWeight(float)`. Each `Update`, the driver moves the weight toward `IsAttacking && IsFullBodyAttack ? 1 : 0` over `PlayerConfigSO.fullBodyAttackBlendTime` (0.15 s — the driver already holds PlayerConfigSO).
- [x] `PlayerStateManager` -- add `IsFullBodyAttack`, set together with `SetAttacking(true, …, fullBody)` and cleared when attacking ends. `CanMove()` = `EvaluateCanMove(IsBusy, IsAttacking, IsFullBodyAttack)` (pure). Remove `!IsAttacking` from `CanBlock()` and `CanJump()`.
- [x] `PlayerCombat` -- pass `ResolvedFullBodyAttacks` in `ExecuteAttack`. `OnBlockStarted` calls `CancelAttack()` when attacking (after the stamina check). `Update` calls `CancelAttack()` when `IsAttacking && IsAirborne`.
- [x] `AnimationEventReceiver` -- drop a clip event already forwarded this frame (same method, same `Time.frameCount`). SMB notifications are not deduplicated.
- [x] `Assets/Tests/EditMode/` -- tests for `EvaluateCanMove`, `ResolvedFullBodyAttacks`, and the event dedupe helper (pure).
- [x] Docs -- `Controllers/CLAUDE.md` (FullBody layer), `PLAYER_COMBO.md` (cancels, dedupe), `Animations/Combat/CLAUDE.md` (2H timings), `Scripts/Player/CLAUDE.md` (CanMove / CanBlock / CanJump).

**Acceptance Criteria:**
- Given the Flamberge drawn in edit mode, when the real Animator runs Attack_1 / Attack_2 with the full-body weight at 1, then the hips match the clip sampled by the tuner (≤ 2°).
- Given a sword attack, when the Animator runs, then the FullBody weight stays 0 and movement is allowed.
- Given an NPC on `Humanoid_Template`, then its FullBody weight is 0.
- Given EditMode tests, when run, then all pass.

## Design Notes

Synced layer rather than Base-layer twins: it reuses the Attack layer's states, transitions, timing and AnyState cancels without duplicating triggers. The weight follows the gameplay `IsAttacking`, so every existing end path (SMB exit, cancel, watchdog, stance interrupt) fades it out. The override controller swaps clips on every layer, so the synced motions need no extra mapping.

## Verification

**Commands:**
- Unity EditMode tests (MCP `run_tests`) -- expected: all pass.

**Manual checks (if no CLI):**
- Edit-mode Animator simulation on a Player instance: hips match the clip at weight 1; FullBody weight is 0 with the sword.
- Play mode: two Flamberge swings plus the finisher, a block / jump / dodge cancel mid-swing, a sword attack while moving.

## Suggested Review Order

**Full-body layer (the fix for the blade pointing up)**

- Entry point: the weight follows gameplay `IsAttacking`, so every end path fades it out.
  [`PlayerAnimationDriver.cs:72`](../../Assets/_Game/Scripts/Player/PlayerAnimationDriver.cs#L72)

- Synced, unmasked Attack copy at weight 0; legs follow the 2H clip only when weighted.
  [`Humanoid_Template.controller:1509`](../../Assets/_Game/Art/Characters/Humanoids/Controllers/Humanoid_Template.controller#L1509)

- Bridge owns the layer write; index cached only once found.
  [`HumanoidAnimationBridge.cs:52`](../../Assets/_Game/Scripts/Core/Animations/HumanoidAnimationBridge.cs#L52)

- Pure weight step, unit-tested.
  [`PlayerAnimationDriver.cs:81`](../../Assets/_Game/Scripts/Player/PlayerAnimationDriver.cs#L81)

**Rooting and the full-body flag**

- Flag read from the equipment (not the startup-racy cache) at swing start.
  [`PlayerCombat.cs:399`](../../Assets/_Game/Scripts/Combat/PlayerCombat.cs#L399)

- Flag set and cleared with `IsAttacking`.
  [`PlayerStateManager.cs:160`](../../Assets/_Game/Scripts/Player/PlayerStateManager.cs#L160)

- `CanMove` false only during a full-body swing; stops move and free-movement turning.
  [`PlayerStateManager.cs:390`](../../Assets/_Game/Scripts/Player/PlayerStateManager.cs#L390)

- Data: archetype flag, resolved on the weapon, enabled for two-handed.
  [`WeaponArchetypeSO.cs:36`](../../Assets/_Game/ScriptableObjects/Items/Weapons/WeaponArchetypeSO.cs#L36)
  [`WeaponSO.cs:58`](../../Assets/_Game/ScriptableObjects/Items/WeaponSO.cs#L58)
  [`Archetype_TwoHanded.asset:27`](../../Assets/_Game/Data/Items/Weapons/Archetypes/Archetype_TwoHanded.asset#L27)

**Cancels (block / jump / dodge, any melee, any moment)**

- Block-cancel after the stamina check, through `CancelAttack`.
  [`PlayerCombat.cs:294`](../../Assets/_Game/Scripts/Combat/PlayerCombat.cs#L294)

- Jump / fall cancel once airborne (coyote-smoothed).
  [`PlayerCombat.cs:222`](../../Assets/_Game/Scripts/Combat/PlayerCombat.cs#L222)

- Gates no longer exclude attacking; pure helpers for tests.
  [`PlayerStateManager.cs:337`](../../Assets/_Game/Scripts/Player/PlayerStateManager.cs#L337)
  [`PlayerStateManager.cs:376`](../../Assets/_Game/Scripts/Player/PlayerStateManager.cs#L376)

- Equip / load mid-swing resets the Animator without SMB exit — cancel there too.
  [`PlayerCombat.cs:482`](../../Assets/_Game/Scripts/Combat/PlayerCombat.cs#L482)

**Double clip events from the synced layer**

- Per-frame dedupe keyed on event + clip + time (crossfades still pass).
  [`ClipEventDeduper.cs:29`](../../Assets/_Game/Scripts/Combat/ClipEventDeduper.cs#L29)
  [`AnimationEventReceiver.cs:72`](../../Assets/_Game/Scripts/Combat/AnimationEventReceiver.cs#L72)

- NPCs share the controller: same dedupe keyed on hitbox id.
  [`EntityAnimationEventReceiver.cs:72`](../../Assets/_Game/Scripts/AI/EntityAnimationEventReceiver.cs#L72)

**2H finisher**

- `2hand attack 2` import + hit window 0.42–0.63, no combo window.
  [`2hand attack 2.fbx.meta:35`](../../Assets/_Game/Art/Characters/Humanoids/Animations/Combat/two%20handed/2hand%20attack%202.fbx.meta#L35)

- Uppercut slot → finisher.
  [`TwoHanded_AnimatorOverride.overrideController:15`](../../Assets/_Game/Art/Characters/Humanoids/Animations/Combat/two%20handed/TwoHanded_AnimatorOverride.overrideController#L15)

**Peripherals**

- Sprint no longer drops sneak while rooted.
  [`PlayerSneak.cs:90`](../../Assets/_Game/Scripts/Player/PlayerSneak.cs#L90)

- Blend time config.
  [`PlayerConfigSO.cs:28`](../../Assets/_Game/ScriptableObjects/Config/PlayerConfigSO.cs#L28)

- Tests: controller mirror, weight step, gates, dedupe, resolved flag.
  [`HumanoidTemplateControllerTests.cs`](../../Assets/Tests/EditMode/HumanoidTemplateControllerTests.cs)
  [`PlayerAnimationDriverTests.cs`](../../Assets/Tests/EditMode/PlayerAnimationDriverTests.cs)
  [`PlayerStateManagerTests.cs`](../../Assets/Tests/EditMode/PlayerStateManagerTests.cs)
  [`ClipEventDeduperTests.cs`](../../Assets/Tests/EditMode/ClipEventDeduperTests.cs)
  [`WeaponArchetypeTests.cs`](../../Assets/Tests/EditMode/WeaponArchetypeTests.cs)

- Docs: controller layer rules, combo pipeline, clip timings.
  [`Controllers/CLAUDE.md:21`](../../Assets/_Game/Art/Characters/Humanoids/Controllers/CLAUDE.md#L21)
  [`PLAYER_COMBO.md`](../../Assets/_Game/Scripts/Combat/PLAYER_COMBO.md)
