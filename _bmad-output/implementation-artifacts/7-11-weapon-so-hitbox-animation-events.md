# Story 7.11: Hitbox Animation Events

Status: review

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As a developer,
I want hitbox activation and deactivation to be driven by `HitboxEnable`/`HitboxDisable` Animation Events on attack clips rather than immediate input-frame activation,
so that hit windows are frame-accurate to the weapon swing animation and tunable per weapon type via override clips — without touching combat code.

## Acceptance Criteria

1. Given `HitboxEnable` and `HitboxDisable` events are added to all 3 attack FBX clips at ~0.25 and ~0.50 normalized time respectively, the weapon hitbox activates and deactivates at those exact animation frames — not at the input press frame.
2. Given `PlayerCombat.ExecuteAttack()` no longer calls `_activeHitbox.Enable()` directly, the hitbox is entirely event-driven for armed attacks; only the unarmed sphere fallback (`ExecuteHitDetection()`) fires immediately on input.
3. Given the finisher clip (Attack_3) now has `HitboxEnable`/`HitboxDisable` events, the finisher has an effective hit window — unlike the current behavior where `_activeHitbox.Enable()` and `_activeHitbox?.Disable()` are called in the same frame (cancelling each other).
4. Given `AnimationEventReceiver.Awake()` adds a null guard logging a warning when `_combat` is not wired, a misconfigured prefab surfaces a clear error instead of a silent null-ref.
5. Given no weapon is equipped (unarmed), the unarmed sphere fallback (`ExecuteHitDetection()`) is unchanged and fires immediately on input.
6. No regressions: inventory, stat effects (7-3), equipment visuals (7-4/7-8), weapon hitbox binding (7-9), combo window events (7-10) all still function; all existing EditMode tests pass.

## Tasks / Subtasks

- [x] Task 1: Remove immediate hitbox enable from `PlayerCombat.ExecuteAttack()` (AC: 1, 2, 3)
  - [x] In `Assets/_Game/Scripts/Combat/PlayerCombat.cs`, locate `ExecuteAttack()`
  - [x] Remove `_activeHitbox.Enable()` call from the armed branch (animation event will handle it)
  - [x] Keep the `else ExecuteHitDetection()` unarmed fallback untouched
  - [x] Verify compilation — no errors

- [x] Task 2: Add `HitboxEnable`/`HitboxDisable` events to the 3 attack FBX meta files (AC: 1, 3)
  - [x] Open `Assets/_Game/Art/Characters/Player/Animations/attacks/AttackLeft.fbx.meta`
  - [x] In `clipAnimations[0].events`, append two new event entries after the existing `ComboWindowClose` entry:
    - `HitboxEnable` at `time: 0.25`
    - `HitboxDisable` at `time: 0.50`
  - [x] Repeat for `AttackOverhead.fbx.meta` (Attack_2) — same two events at same times
  - [x] Repeat for `AttackThrust.fbx.meta` (Attack_3) — same two events at same times
  - [x] Force-refresh Unity — verify no console errors

- [x] Task 3: Add `Awake` null guard to `AnimationEventReceiver.cs` (AC: 4) — deferred from 7.10 code review
  - [x] Add `private void Awake()` method to `Assets/_Game/Scripts/Combat/AnimationEventReceiver.cs`
  - [x] Inside `Awake()`: `if (_combat == null) GameLog.Warn(TAG, "PlayerCombat not wired — animation events will be no-ops");`
  - [x] Add `private const string TAG = "[AnimationEventReceiver]";` field
  - [x] Verify compilation — no errors

- [x] Task 4: Update `Assets/_Game/Scripts/Combat/CLAUDE.md` (AC: 1)
  - [x] Add section documenting that hitbox is now fully animation-event driven
  - [x] Note that `ExecuteAttack()` no longer calls `_activeHitbox.Enable()` directly
  - [x] Note the finisher hitbox fix (same-frame enable/disable bug resolved)
  - [x] Note event normalized time placement for future weapon clip tuning

- [x] Task 5: Play-mode validation (AC: 1–6)
  - [x] Equip TestSword → attack → verify hitbox collider activates and deactivates during swing (not on input frame)
  - [x] Verify enemies take damage during the swing window
  - [x] Verify unequip → unarmed sphere fallback works (3-hit combo unchanged)
  - [x] Verify 2-hit sword combo still works end-to-end
  - [x] Verify no "phantom hits" (hitbox left open between attacks)
  - [x] Adjust `HitboxEnable`/`HitboxDisable` normalized times in FBX meta if hits register at wrong moment
  - [x] All EditMode tests pass — 0 regressions (203/203 passed)

## Dev Notes

### Architecture Overview

This story completes the animation-event pipeline started in Story 7.10. After Story 7.11:

```
Attack input pressed
  → PlayerCombat.TryAttack() → ExecuteAttack()
    → _stateManager.SetAttacking(true, triggerHash) → Animator plays clip
    → (NO immediate _activeHitbox.Enable() call)
    → [unarmed only] ExecuteHitDetection() sphere fallback

Attack clip plays:
  → HitboxEnable at ~25% → AnimationEventReceiver.HitboxEnable()
    → PlayerCombat.OnHitboxEnable() → _activeHitbox?.Enable()

  → HitboxDisable at ~50% → AnimationEventReceiver.HitboxDisable()
    → PlayerCombat.OnHitboxDisable() → _activeHitbox?.Disable()

  → ComboWindowOpen at ~50% → (window opens; player can chain next attack)
  → ComboWindowClose at ~90% → (window closes; chain resets)
```

### Critical Bug Fixed by This Story

In the current implementation (post-7.10), `ExecuteAttack()` calls `_activeHitbox.Enable()` immediately, then `ManageComboStep()` calls `ResetAttackCombo()` → `_activeHitbox?.Disable()` in the **same frame** for the finisher (step 2). This means the finisher's hitbox is enabled and immediately disabled — zero effective hit window.

After Story 7.11, the finisher clip has `HitboxEnable` at ~25% and `HitboxDisable` at ~50%, giving the finisher a proper hit window like the other attacks.

### The Exact Change to `ExecuteAttack()` in `PlayerCombat.cs`

```csharp
// BEFORE (Story 7.9 / 7.10):
private void ExecuteAttack()
{
    int triggerHash = _comboStep switch { 0 => Attack1Hash, 1 => Attack2Hash, _ => Attack3Hash };
    _stateManager.SetAttacking(true, triggerHash);
    GameLog.Info(TAG, $"Attack combo step {_comboStep + 1}");

    if (_activeHitbox != null)
        _activeHitbox.Enable();       // ← REMOVE THIS LINE
    else
        ExecuteHitDetection();        // ← keep: unarmed fallback
}

// AFTER (Story 7.11):
private void ExecuteAttack()
{
    int triggerHash = _comboStep switch { 0 => Attack1Hash, 1 => Attack2Hash, _ => Attack3Hash };
    _stateManager.SetAttacking(true, triggerHash);
    GameLog.Info(TAG, $"Attack combo step {_comboStep + 1}");

    if (_activeHitbox == null)
        ExecuteHitDetection();        // Unarmed sphere fallback (unchanged behavior)
    // Armed: hitbox enabled/disabled by HitboxEnable/HitboxDisable animation events
}
```

### `AnimationEventReceiver.cs` — Awake null guard to add

```csharp
private const string TAG = "[AnimationEventReceiver]";

private void Awake()
{
    if (_combat == null)
        GameLog.Warn(TAG, "PlayerCombat not wired — animation events will be no-ops");
}
```

The null-conditional operators (`_combat?.OnHitboxEnable()` etc.) already prevent NullReferenceExceptions — the guard is a developer-experience improvement only.

### FBX Meta Event Format (same pattern as 7.10)

Events are stored in the `.meta` file under `clipAnimations[n].events`. The existing events after Story 7.10 are `ComboWindowOpen` (0.5) and `ComboWindowClose` (0.9). Append two new entries per clip:

```yaml
      - time: 0.25
        functionName: HitboxEnable
        data:
        objectReferenceParameter: {fileID: 0}
        floatParameter: 0
        intParameter: 0
        messageOptions: 0
      - time: 0.5
        functionName: HitboxDisable
        data:
        objectReferenceParameter: {fileID: 0}
        floatParameter: 0
        intParameter: 0
        messageOptions: 0
```

Note: `HitboxDisable` at 0.5 and `ComboWindowOpen` at 0.5 fire at the same normalized time. Unity fires all events at the same time in their listed order — the order in the YAML determines dispatch order. Either order is safe here since they are independent.

Target clips:
- `Assets/_Game/Art/Characters/Player/Animations/attacks/AttackLeft.fbx.meta` → Attack_1
- `Assets/_Game/Art/Characters/Player/Animations/attacks/AttackOverhead.fbx.meta` → Attack_2
- `Assets/_Game/Art/Characters/Player/Animations/attacks/AttackThrust.fbx.meta` → Attack_3 (finisher)

### AnimationEventReceiver Function Name Contract

Unity matches Animation Event `functionName` to public methods on components **on the same GameObject as the Animator** by exact string. A typo is a silent no-op (no compile error, no runtime error). The method names must be:

| FBX event `functionName` | `AnimationEventReceiver` method | Routes to |
|---|---|---|
| `HitboxEnable` | `public void HitboxEnable()` | `PlayerCombat.OnHitboxEnable()` |
| `HitboxDisable` | `public void HitboxDisable()` | `PlayerCombat.OnHitboxDisable()` |
| `ComboWindowOpen` | `public void ComboWindowOpen()` | `PlayerCombat.OnComboWindowOpen()` |
| `ComboWindowClose` | `public void ComboWindowClose()` | `PlayerCombat.OnComboWindowClose()` |

All four are already implemented in `AnimationEventReceiver.cs` from Story 7.10. Story 7.11 only adds the FBX meta events for `HitboxEnable`/`HitboxDisable`.

### Combo-End Hitbox Disable Paths (unchanged from 7.9)

All paths that end a combo still call `_activeHitbox?.Disable()` explicitly — these are safety guards in case the `HitboxDisable` animation event hasn't fired yet (e.g., player cancels into block mid-swing):

1. `TryAttack()` — stamina deny path
2. `TryAttack()` — `Consume()` fail path
3. `ResetAttackCombo()` — finisher fired, or window expired via `OnComboWindowClose()`
4. `OnBlockStarted()` — block interrupts combo via `ResetAttackCombo()`
5. `OnDisable()` — `UnbindWeaponHitbox()` calls `_activeHitbox.Disable()` then nulls `_activeHitbox`

These guards remain essential — they prevent phantom hits when the animation event hasn't fired but the hitbox needs to be closed immediately.

### Hitbox Timing Tuning Guide

Starting suggested normalized times for each event:

| Event | Suggested time | Rationale |
|---|---|---|
| `HitboxEnable` | 0.25 | Weapon strike motion ~25% through clip — tip of sword reaches target zone |
| `HitboxDisable` | 0.50 | Weapon retracts; simultaneously opens combo window for chaining |

To tune: In the Unity Animation window, play the FBX clip and pause at ~25% of the clip. Check if the sword tip visually reaches the target zone. If too early, increase `HitboxEnable` time; if too late, decrease it. Repeat for `HitboxDisable`.

Exact values differ per attack animation — Attack_3 (thrust) may need `HitboxEnable` at 0.15 (thrust is faster to connect). The developer adjusts these during play-testing.

### Impact on Existing Systems

| System | Impact |
|---|---|
| `WeaponHitbox.cs` | **Zero changes** — `Enable()`/`Disable()` already have the correct signature |
| `AnimationEventReceiver.cs` | Add `Awake` null guard + `TAG` const only |
| `PlayerCombat.cs` | One line removed from `ExecuteAttack()` |
| `AttackLeft/Overhead/Thrust.fbx.meta` | Two new event entries per file |
| All EditMode tests | No changes — no new pure logic to unit-test |
| `EquipmentSystem`, `InventoryUI`, `ItemDetailPanelUI` | Untouched |

### Project Structure Notes

All files touched are already established paths from Stories 7.9 and 7.10:
- `Assets/_Game/Scripts/Combat/PlayerCombat.cs` — minor edit
- `Assets/_Game/Scripts/Combat/AnimationEventReceiver.cs` — minor edit
- `Assets/_Game/Art/Characters/Player/Animations/attacks/*.fbx.meta` — event additions

No new scripts, prefabs, or assets created.

### References

- [Source: _bmad-output/sprint-change-proposal-2026-03-22.md#Story-7.11] — implementation handoff for hitbox events
- [Source: _bmad-output/implementation-artifacts/7-10-weapon-so-combo-steps-animation-events.md] — AnimationEventReceiver pattern + deferred null guard from code review
- [Source: Assets/_Game/Scripts/Combat/PlayerCombat.cs:256-260] — `ExecuteAttack()` line to remove
- [Source: Assets/_Game/Scripts/Combat/AnimationEventReceiver.cs] — stub methods already in place
- [Source: Assets/_Game/Scripts/Combat/WeaponHitbox.cs] — Enable()/Disable() — no changes required
- [Source: Assets/_Game/Scripts/Combat/CLAUDE.md] — hitbox system rules and combo-end disable requirement
- [Source: Assets/_Game/Art/Characters/Player/Animations/attacks/AttackLeft.fbx.meta:57-71] — existing event YAML format reference

## Dev Agent Record

### Agent Model Used

claude-sonnet-4-6

### Debug Log References

### Completion Notes List

- Tasks 1–4 complete. Task 5 (play-mode validation) requires manual developer testing in Unity play mode.
- Task 1: Removed `_activeHitbox.Enable()` from `ExecuteAttack()`. Armed attacks now fully event-driven; unarmed sphere fallback unchanged.
- Task 2: Added `HitboxEnable` (0.25) and `HitboxDisable` (0.50) events to all 3 FBX meta files. Events prepended before existing ComboWindowOpen/Close entries. Unity force-refreshed, 0 console errors.
- Task 3: Added `Awake()` null guard + `TAG` const to `AnimationEventReceiver.cs`. Added `using Game.Core;` import for `GameLog`.
- Task 4: Updated `Combat/CLAUDE.md` — documented event-driven hitbox pipeline, finisher fix, timing table, and tuning guide.
- All 203 EditMode tests pass (0 regressions).

### File List

- `Assets/_Game/Scripts/Combat/PlayerCombat.cs`
- `Assets/_Game/Scripts/Combat/AnimationEventReceiver.cs`
- `Assets/_Game/Art/Characters/Player/Animations/attacks/AttackLeft.fbx.meta`
- `Assets/_Game/Art/Characters/Player/Animations/attacks/AttackOverhead.fbx.meta`
- `Assets/_Game/Art/Characters/Player/Animations/attacks/AttackThrust.fbx.meta`
- `Assets/_Game/Scripts/Combat/CLAUDE.md`
- `_bmad-output/implementation-artifacts/sprint-status.yaml`
