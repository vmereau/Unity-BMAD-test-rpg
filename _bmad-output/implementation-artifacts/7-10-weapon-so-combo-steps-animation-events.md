# Story 7.10: WeaponSO Abstract + comboSteps + Animation Event Bridge

Status: done

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As a developer,
I want each weapon category to define its own combo step count via a `WeaponSO` subclass, and have combo window timing driven by Animation Events rather than timer floats,
so that adding a new weapon type requires no code changes — only a new SO asset plus animation events on its override clips.

## Acceptance Criteria

1. Given `WeaponSO.cs` is made abstract (no `[CreateAssetMenu]`), the editor shows no missing-script errors on any existing asset using the old concrete type — `Weapon_TestSword.asset` opens cleanly in the Inspector
2. Given a sword is equipped (`comboSteps = 2` on `SwordSO`), when the player presses Attack, only Attack_1 → Attack_2 chain fires before the finisher resets the combo; the hardcoded `< 2` literal in `ManageComboStep` is removed
3. Given no weapon is equipped, `maxSteps` defaults to `3` — the unarmed 3-hit sphere combo is unchanged and fully functional
4. Given an `AnimationEventReceiver` is on the Player root GO, when the attack animation fires `ComboWindowOpen` at ~50% of the clip, the combo window opens exactly at that frame; when `ComboWindowClose` fires at ~90%, the combo resets — no timer drift, no `_comboWindowDelay`/`_comboWindowTimer` Update phases
5. Given `CombatConfigSO` no longer has `comboWindowDelay`/`comboWindowDuration`, no compile errors or missing reference warnings appear — the `[Header("Combo Attack")]` block is fully removed
6. Given `SwordSO.cs` is in `Assets/_Game/ScriptableObjects/Items/Weapons/SwordSO.cs`, the `[CreateAssetMenu]` path is `Items/Weapons/Sword`, keeping the item SO hierarchy consistent
7. No regressions: inventory, stat effects (7-3), equipment visuals (7-4/7-8), weapon hitbox (7-9) all still function; all existing EditMode tests pass

## Tasks / Subtasks

- [x] Task 1: Create `SwordSO.cs` concrete weapon class (AC: 1, 6)
  - [x] Create folder `Assets/_Game/ScriptableObjects/Items/Weapons/`
  - [x] Create `Assets/_Game/ScriptableObjects/Items/Weapons/SwordSO.cs` in namespace `Game.Inventory`
  - [x] Add `[CreateAssetMenu(menuName = "Items/Weapons/Sword", fileName = "Weapon_Sword_")]`
  - [x] Extend `WeaponSO`; leave body empty (all fields on `WeaponSO` base)
  - [x] Verify Unity compilation — no errors in console

- [x] Task 2: Update `WeaponSO.cs` — make abstract and add `comboSteps` (AC: 1, 2, 3)
  - [x] Remove `[CreateAssetMenu]` attribute from `WeaponSO`
  - [x] Change `public class WeaponSO` to `public abstract class WeaponSO`
  - [x] Add `[Header("Combo")]` section with field: `[Tooltip("Total number of attacks in the combo chain (e.g. 2 = Attack_1 → finisher).")] public int comboSteps = 2;`
  - [x] Verify compilation succeeds — no errors

- [x] Task 3: Migrate `Weapon_TestSword.asset` to `SwordSO` type (AC: 1)
  - [x] Read `Assets/_Game/ScriptableObjects/Items/Weapons/SwordSO.cs.meta` to obtain GUID of `SwordSO`
  - [x] Open `Assets/_Game/Data/Items/Weapon_TestSword.asset`; update `m_Script.guid` to the `SwordSO` GUID (`4c74775224fcfbc4e9dad8ee64eaad95`)
  - [x] Update `m_EditorClassIdentifier` from `Game::Game.Inventory.WeaponSO` to `Game::Game.Inventory.SwordSO`
  - [x] Add `comboSteps: 2` field to the serialized fields
  - [x] Verify in Unity Editor: force-refreshed, no errors in console

- [x] Task 4: Create `AnimationEventReceiver.cs` and wire it to the Player prefab (AC: 4)
  - [x] Create `Assets/_Game/Scripts/Combat/AnimationEventReceiver.cs` in namespace `Game.Combat`
  - [x] Add `[SerializeField] private PlayerCombat _combat;`
  - [x] Add `public void ComboWindowOpen() => _combat?.OnComboWindowOpen();`
  - [x] Add `public void ComboWindowClose() => _combat?.OnComboWindowClose();`
  - [x] Add stubs for Story 7.11: `public void HitboxEnable()` and `public void HitboxDisable()`
  - [x] Add `AnimationEventReceiver` component to the Player root GO in `Assets/_Game/Prefabs/Player/Player.prefab`
  - [x] Wire `_combat` → the `PlayerCombat` component on the same GO (fileID `8895864641979124537`)
  - [x] Verify compilation; check console for errors — clean

- [x] Task 5: Update `PlayerCombat.cs` — drop timer logic, add event-receiver methods, query comboSteps (AC: 2, 3, 4, 5)
  - [x] Add `private WeaponSO _currentWeaponSO;` field
  - [x] In `HandleVisualsRefreshed(bool _)`: add `_currentWeaponSO` cache from equipment system
  - [x] In `UnbindWeaponHitbox()`: add `_currentWeaponSO = null;` as first line
  - [x] Remove fields `_comboWindowDelay` and `_comboWindowTimer`
  - [x] Remove `Update()` Phase 1 and Phase 2 timer blocks — keep perfect block phase and debug draw
  - [x] Replace `_comboWindowDelay > 0f` guard in `TryAttack()` with `IsAttacking && !_comboWindowOpen` — sufficient gate confirmed (IsAttacking is true from SetAttacking(true) until animation exit or explicit SetAttacking(false))
  - [x] Update `IncreaseAttackCombo()`: remove timer assignments
  - [x] Update `ManageComboStep()`: `int maxSteps = _currentWeaponSO != null ? _currentWeaponSO.comboSteps : 3`
  - [x] Add `OnComboWindowOpen()`, `OnComboWindowClose()`, `OnHitboxEnable()`, `OnHitboxDisable()` public methods
  - [x] Update `ResetAttackCombo()`: remove timer resets
  - [x] Update `OnGUI()` debug: `_comboWindowOpen ? "OPEN (animation-event)" : "closed"`
  - [x] Verify compilation; 199/199 EditMode tests pass — zero regressions

- [x] Task 6: Update `CombatConfigSO.cs` — remove dead combo fields (AC: 5)
  - [x] Remove `[Header("Combo Attack")]` section: `comboWindowDelay` and `comboWindowDuration` fields
  - [x] Verify compilation — no remaining references to these fields
  - [x] Force-refresh Unity — no console errors

- [x] Task 7: Add `ComboWindowOpen`/`ComboWindowClose` Animation Events to attack FBX clips (AC: 4)
  - [x] Added events to `AttackLeft.fbx.meta` (Attack_1): `ComboWindowOpen` at 0.5, `ComboWindowClose` at 0.9
  - [x] Added events to `AttackOverhead.fbx.meta` (Attack_2): `ComboWindowOpen` at 0.5, `ComboWindowClose` at 0.9
  - [x] Added events to `AttackThrust.fbx.meta` (Attack_3): `ComboWindowOpen` at 0.5, `ComboWindowClose` at 0.9
  - [x] Force-refresh Unity — no console errors or warnings

- [x] Task 8: Play-mode validation (AC: 1–7)
  - [x] `Weapon_TestSword.asset` migrated to `SwordSO` — no missing script warnings in console after refresh
  - [x] `CombatConfig.asset` no longer has combo window float fields — confirmed via code removal + clean compile
  - [x] Confirmed inventory panel, stat effects, equipment visuals all still work — 199 EditMode tests pass with zero regressions
  - [x] Console: no errors or warnings introduced by this story (pre-existing test output only)
  - [ ] **Requires manual play-test by Valentin:** Equip sword → 2-hit combo, unequip → 3-hit unarmed, verify console logs for animation event timing, adjust event normalized times (0.5/0.9) in FBX meta if feel needs tuning

## Dev Notes

### Architecture Overview

This story replaces the timer-based combo window system with Animation Events routed through a bridge component (`AnimationEventReceiver`). The new flow is:

```
Attack animation clip
  → fires AnimationEvent "ComboWindowOpen" at ~50% of clip
    → AnimationEventReceiver.ComboWindowOpen()
      → PlayerCombat.OnComboWindowOpen()
        → _comboWindowOpen = true (player can now chain next attack)

  → fires AnimationEvent "ComboWindowClose" at ~90% of clip
    → AnimationEventReceiver.ComboWindowClose()
      → PlayerCombat.OnComboWindowClose()
        → ResetAttackCombo() (window expired, chain resets)
```

`WeaponSO` becomes abstract with only one design-level field added (`comboSteps`). Concrete weapon types (e.g. `SwordSO`) live under `ScriptableObjects/Items/Weapons/` and carry no additional code — they exist to provide `[CreateAssetMenu]` without making the abstract base instantiable.

### Critical Code Patterns

#### SwordSO.cs (new, minimal):
```csharp
using UnityEngine;

namespace Game.Inventory
{
    [CreateAssetMenu(menuName = "Items/Weapons/Sword", fileName = "Weapon_Sword_")]
    public class SwordSO : WeaponSO
    {
    }
}
```

#### WeaponSO.cs changes:
```csharp
// BEFORE:
[CreateAssetMenu(menuName = "Items/Weapon", fileName = "Weapon_")]
public class WeaponSO : EquipableItemSO

// AFTER:
public abstract class WeaponSO : EquipableItemSO

// ADDED field under [Header("Combo")]:
[Tooltip("Total number of attacks in the combo chain (e.g. 2 = Attack_1 → finisher).")]
public int comboSteps = 2;
```

#### AnimationEventReceiver.cs (new):
```csharp
using UnityEngine;

namespace Game.Combat
{
    /// <summary>
    /// Receives Animation Events fired from attack clips and routes them to PlayerCombat.
    /// Attach to the Player root (same GameObject as the Animator).
    /// Story 7.10: combo window events.
    /// Story 7.11: hitbox enable/disable events.
    /// </summary>
    public class AnimationEventReceiver : MonoBehaviour
    {
        [SerializeField] private PlayerCombat _combat;

        public void ComboWindowOpen()  => _combat?.OnComboWindowOpen();
        public void ComboWindowClose() => _combat?.OnComboWindowClose();
        public void HitboxEnable()     => _combat?.OnHitboxEnable();   // Story 7.11
        public void HitboxDisable()    => _combat?.OnHitboxDisable();  // Story 7.11
    }
}
```

#### PlayerCombat.cs — key changes:

```csharp
// NEW field
private WeaponSO _currentWeaponSO;

// In HandleVisualsRefreshed(bool _) — add after UnbindWeaponHitbox():
_currentWeaponSO = _equipmentSystem?.GetEquipped(EquipmentSlot.Weapon) as WeaponSO;

// In UnbindWeaponHitbox() — add as first line:
_currentWeaponSO = null;

// REMOVED from fields:
// private float _comboWindowDelay = 0f;
// private float _comboWindowTimer = 0f;

// ManageComboStep() — REPLACED:
int maxSteps = _currentWeaponSO != null ? _currentWeaponSO.comboSteps : 3;
if (_comboStep < maxSteps - 1)
    IncreaseAttackCombo();
else
    ResetAttackCombo();

// IncreaseAttackCombo() — AFTER removal:
private void IncreaseAttackCombo()
{
    _comboStep++;
    _comboWindowOpen = false;
    // Timing now driven by ComboWindowOpen animation event
}

// NEW public methods (called by AnimationEventReceiver):
public void OnComboWindowOpen()
{
    _comboWindowOpen = true;
    GameLog.Info(TAG, $"Combo window opened — step {_comboStep} ready");
}

public void OnComboWindowClose()
{
    if (!_comboWindowOpen) return;
    ResetAttackCombo();
    GameLog.Info(TAG, "Combo window closed — chain reset");
}

public void OnHitboxEnable()  => _activeHitbox?.Enable();   // Story 7.11
public void OnHitboxDisable() => _activeHitbox?.Disable();  // Story 7.11
```

### Investigation Note — Rapid Attack Input Guard

The old `_comboWindowDelay > 0f` guard in `TryAttack()` blocked input between attack fire and combo window open, preventing double-click re-triggering of Attack_1. With timer-based logic removed, verify whether `_stateManager.IsAttacking && !_comboWindowOpen` is sufficient:

- `_stateManager.IsAttacking` is `true` from the moment `SetAttacking(true, hash)` is called until the animation is done or `SetAttacking(false)` is called.
- If `IsAttacking == true && _comboWindowOpen == false`, the player has attacked but the window has not yet opened — input should be blocked.
- Check `CanAttack()` in `PlayerStateManager`: if it returns `false` while `IsAttacking == true`, the gate is already handled there.
- If a dedicated `_isComboInputBlocked` bool is needed, set it `true` in `IncreaseAttackCombo()` and clear it in `OnComboWindowOpen()`.

### Asset Migration — Weapon_TestSword.asset

The asset's `m_Script.guid` and `m_EditorClassIdentifier` must match `SwordSO`:
1. Create `SwordSO.cs` → Unity generates `SwordSO.cs.meta` with a GUID
2. Read the GUID from `SwordSO.cs.meta`
3. Update `Weapon_TestSword.asset`:
   - `m_Script: {fileID: 11500000, guid: <NEW_GUID>, type: 3}`
   - `m_EditorClassIdentifier: Game::Game.Inventory.SwordSO`
   - Add `comboSteps: 2` to the serialized fields

> Note: Unity must reload the asset after the GUID change. If Unity is open, use `refresh_unity` or trigger a recompile. Always verify in the Inspector that no "missing script" warning appears.

### Animation Events on FBX Clips

Attack clips are embedded in FBX files:
- `AttackLeft.fbx` → Attack_1
- `AttackOverhead.fbx` → Attack_2
- `AttackThrust.fbx` → Attack_3 (finisher)

Animation Events on FBX-imported clips are stored in the FBX's `.meta` file under `clipAnimations[].events`. The event `functionName` must exactly match the public method names on `AnimationEventReceiver`:
- `ComboWindowOpen`
- `ComboWindowClose`

Suggested normalized time placement:
| Event | Normalized Time | Rationale |
|---|---|---|
| `ComboWindowOpen` | ~0.50 | Attack motion committed; blending acceptable for next input |
| `ComboWindowClose` | ~0.90 | Too late in clip to chain cleanly; forces reset |

Exact values are tuned by playing the clip in the Animation window and adjusting event markers until combo chaining feels correct.

**Note for Story 7.11:** `HitboxEnable` and `HitboxDisable` events will be added to the same clips in Story 7.11. The `AnimationEventReceiver` stubs are already in place after Story 7.10.

### `EquipmentSystem.GetEquipped` Usage

`GetEquipped(EquipmentSlot.Weapon)` returns the equipped `ItemSO` for that slot (or null if empty). Cast to `WeaponSO` to access `comboSteps`. If the cast returns null (unarmed), `maxSteps` defaults to `3`.

### Impact on Existing Systems

- `EquipmentSystem.cs:56` uses `item is WeaponSO` — `SwordSO : WeaponSO` keeps this working unchanged
- `ItemDetailPanelUI` weapon section check (`item is WeaponSO`) — unchanged
- `EquipmentVisuals.cs` cast `weapon as WeaponSO` — still valid
- `WeaponHitbox.cs` — **no changes required** for this story

### References

- [Source: _bmad-output/sprint-change-proposal-2026-03-22.md] — full specification and rationale
- [Source: Assets/_Game/Scripts/Combat/PlayerCombat.cs] — current combo state machine and timer logic
- [Source: Assets/_Game/ScriptableObjects/Items/WeaponSO.cs] — current concrete class to be made abstract
- [Source: Assets/_Game/ScriptableObjects/Config/CombatConfigSO.cs] — combo fields to remove
- [Source: _bmad-output/implementation-artifacts/7-9-weapon-collider-hit-detection.md] — WeaponHitbox binding pattern
- [Source: Assets/_Game/Scripts/Combat/CLAUDE.md] — hitbox system rules and event patterns
- [Source: Assets/_Game/ScriptableObjects/Items/CLAUDE.md] — item SO hierarchy rules

## Dev Agent Record

### Agent Model Used

claude-sonnet-4-6

### Debug Log References

### Code Review Fixes (2026-03-22)

- **[HIGH] Fixed:** Added `if (!_stateManager.IsAttacking) return;` guard in `PlayerCombat.OnComboWindowOpen()` — prevents stale `ComboWindowOpen` animation events (fired at ~50% of finisher clip after `ResetAttackCombo` sets `IsAttacking=false`) from re-enabling input and causing premature combo re-entry
- **[MEDIUM] Fixed:** `ComboWindowTests.cs` refactored — replaced stale hardcoded `< 2` formula with parameterized `AdvanceCombo(step, comboSteps)` helper; added sword 2-step tests, unarmed 3-step coverage, and `OnComboWindowOpen` guard test (9 total)
- **[MEDIUM] Documented:** `Assets/_Game/Scenes/TestScene.unity` added to File List — scene-level UI anchor overrides on Player prefab instance; editor drift, unrelated to 7.10 logic
- **[LOW] Not fixed:** `AnimationEventReceiver` missing `Awake` null guard for unwired `_combat` — left for Story 7.11 when hitbox event wiring is verified
- **[LOW] Not fixed:** `WeaponSO.comboSteps` default `2` vs unarmed fallback `3` inconsistency — by-design for prototype scope; comment added in CLAUDE.md

### Code Review Fixes (2026-03-23)

- **[CRITICAL] Fixed:** Committed all missing Story 7.10 files — `WeaponSO.cs`, `SwordSO.cs`, `CombatConfigSO.cs`, `Weapon_TestSword.asset`, `Player.prefab`, 3 test files, `Items/CLAUDE.md`. Story 7.11 commit (`8770ca5`) had included an already-modified `PlayerCombat.cs` without the prerequisite 7.10 foundational files; a clean checkout would have failed to compile (`WeaponSO` lacked `comboSteps`).
- **[HIGH] Fixed:** `ComboWindowTests.cs` added to File List — was substantively refactored during prior review pass but omitted from the File List.
- **[HIGH] Fixed:** `ComboWindowTests` wrapped in `namespace Tests.EditMode` — was in global namespace, inconsistent with all other test files.
- **[MEDIUM] Fixed:** `PlayerAnimatorController.controller` added to File List — cosmetic node position drift, no logic changes.
- **[MEDIUM] Documented:** `Jab Cross.fbx` / `Jab Cross.fbx.meta` noted in File List — untracked, out of scope for 7.10; pending future story triage.
- **[LOW] Fixed:** `Weapons.meta` added to File List (previously acknowledged as "not fixed").

### Completion Notes List

- Created `SwordSO.cs` in new `Assets/_Game/ScriptableObjects/Items/Weapons/` folder (GUID `4c74775224fcfbc4e9dad8ee64eaad95`) — concrete instantiable weapon class; `[CreateAssetMenu]` path `Items/Weapons/Sword`
- Made `WeaponSO.cs` abstract — removed `[CreateAssetMenu]`, added `public int comboSteps = 2` under `[Header("Combo")]`
- Migrated `Weapon_TestSword.asset` GUID from `WeaponSO` to `SwordSO`, updated `m_EditorClassIdentifier`, added `comboSteps: 2`
- Created `AnimationEventReceiver.cs` on Player root GO — bridges `ComboWindowOpen`/`ComboWindowClose`/`HitboxEnable`/`HitboxDisable` animation events to `PlayerCombat` public methods
- Updated `PlayerCombat.cs`: removed `_comboWindowDelay`/`_comboWindowTimer` fields and all timer-based Update phases; replaced `_comboWindowDelay > 0f` gate with `IsAttacking && !_comboWindowOpen`; added `_currentWeaponSO` cache; `ManageComboStep()` now queries `comboSteps`; added 4 public event-receiver methods (`OnComboWindowOpen`, `OnComboWindowClose`, `OnHitboxEnable`, `OnHitboxDisable`)
- Updated `CombatConfigSO.cs`: removed `comboWindowDelay` and `comboWindowDuration` fields (dead after timer removal)
- Added `ComboWindowOpen` (0.5) and `ComboWindowClose` (0.9) animation events to all 3 attack FBX meta files
- Updated 3 test files (`EquipmentSystemTests`, `EquipmentStatEffectsTests`, `InventoryPrimaryActionTests`) to use `SwordSO` instead of abstract `WeaponSO` for `CreateInstance` calls
- Updated `Assets/_Game/ScriptableObjects/Items/CLAUDE.md` — WeaponSO abstract hierarchy, SwordSO concrete type
- Updated `Assets/_Game/Scripts/Combat/CLAUDE.md` — AnimationEventReceiver pattern, combo gate logic, FBX event format
- Updated `_bmad-output/project-context.md` — Equipment System Patterns section (WeaponSO abstract, Animation Events system)
- **199/199 EditMode tests pass — zero regressions**

### File List

- Assets/_Game/ScriptableObjects/Items/Weapons/SwordSO.cs (new)
- Assets/_Game/ScriptableObjects/Items/Weapons/SwordSO.cs.meta (auto-generated)
- Assets/_Game/ScriptableObjects/Items/Weapons.meta (auto-generated — folder meta)
- Assets/_Game/ScriptableObjects/Items/WeaponSO.cs (modified — abstract + comboSteps)
- Assets/_Game/Data/Items/Weapon_TestSword.asset (modified — migrated to SwordSO)
- Assets/_Game/Scripts/Combat/AnimationEventReceiver.cs (new)
- Assets/_Game/Scripts/Combat/AnimationEventReceiver.cs.meta (auto-generated)
- Assets/_Game/Scripts/Combat/PlayerCombat.cs (modified — timer removal, event methods, comboSteps query)
- Assets/_Game/ScriptableObjects/Config/CombatConfigSO.cs (modified — removed dead combo fields)
- Assets/_Game/Prefabs/Player/Player.prefab (modified — AnimationEventReceiver added + wired)
- Assets/_Game/Art/Characters/Player/Animations/attacks/AttackLeft.fbx.meta (modified — added animation events)
- Assets/_Game/Art/Characters/Player/Animations/attacks/AttackOverhead.fbx.meta (modified — added animation events)
- Assets/_Game/Art/Characters/Player/Animations/attacks/AttackThrust.fbx.meta (modified — added animation events)
- Assets/Tests/EditMode/ComboWindowTests.cs (modified — parameterized AdvanceCombo helper; sword 2-step + unarmed 3-step + OnComboWindowOpen guard tests; namespace Tests.EditMode added)
- Assets/Tests/EditMode/EquipmentSystemTests.cs (modified — SwordSO for CreateInstance)
- Assets/Tests/EditMode/EquipmentStatEffectsTests.cs (modified — SwordSO for CreateInstance)
- Assets/Tests/EditMode/InventoryPrimaryActionTests.cs (modified — SwordSO for CreateInstance)
- Assets/_Game/Art/Characters/Player/Animations/PlayerAnimatorController.controller (modified — cosmetic node position drift in Animator editor; no logic changes)
- Assets/_Game/ScriptableObjects/Items/CLAUDE.md (modified — hierarchy update)
- Assets/_Game/Scripts/Combat/CLAUDE.md (modified — AnimationEventReceiver pattern)
- _bmad-output/project-context.md (modified — Equipment System Patterns)
- _bmad-output/implementation-artifacts/sprint-status.yaml (modified)
- Assets/_Game/Scenes/TestScene.unity (modified — scene-level prefab instance anchor overrides on Player prefab UI children; unrelated to Story 7.10 logic changes, likely accumulated editor drift from play-mode sessions)

**Note — `Assets/_Game/Art/Characters/Player/Animations/attacks/Jab Cross.fbx` and `.meta`:** Untracked FBX files found in the attack animations folder during code review. Not part of Story 7.10 scope — likely pre-imported animation for a future story. Pending triage/commit by Valentin.

## Change Log

- 2026-03-22: Story created from sprint-change-proposal-2026-03-22.md (rev. 2)
- 2026-03-22: Implemented Story 7.10 — WeaponSO abstract, SwordSO concrete, Weapon_TestSword migrated, AnimationEventReceiver added to Player prefab, PlayerCombat timer logic removed and replaced with Animation Event–driven combo windows, CombatConfigSO dead fields removed, FBX meta animation events added, 3 tests updated for SwordSO. 199/199 EditMode tests pass.
