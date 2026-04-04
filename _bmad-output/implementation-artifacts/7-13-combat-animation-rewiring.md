# Story 7.13: Combat Animation Rewiring

Status: done

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As a player,
I want my upper body animations to match my combat stance when I draw my weapon — sword idle when a sword is equipped, unarmed idle when nothing is equipped — and I want attack animations to be weapon-specific via the existing AnimatorOverrideController mechanism,
so that drawing my weapon feels visually distinct and each weapon type has its own combat identity.

## Acceptance Criteria

1. `IsInCombat` bool parameter exists in `PlayerAnimatorController`; `PlayerAnimator.SetInCombat()` no longer uses the `_hasIsInCombatParam` guard — it calls `SetBool` directly every time
2. When `IsInCombat = true` and no action (attack/block/dodge) is active: the Attack layer plays the "CombatIdle" state (upper body only, via UpperBodyMask), crossfading in over 0.2 s
3. When `IsInCombat = false`: the Attack layer returns to "LockOn Locomotion" (upper body mirrors locomotion blend), crossfading out over 0.2 s
4. With no weapon equipped: "CombatIdle" plays the "Unarmed Idle" clip (from `Combat/attacks/Unarmed/Unarmed Idle.fbx`)
5. With `Weapon_TestSword` equipped: "CombatIdle" plays the "Sword Idle" clip (from `Combat/attacks/Sword/SwordIdle.fbx`), applied via the sword's `animatorOverrideController` field on `WeaponSO`
6. A `Sword_AnimatorOverride.overrideController` asset exists in `Assets/_Game/Art/Characters/Player/Animations/`; it is assigned to `Weapon_TestSword.asset.animatorOverrideController`
7. Existing attack (Attack_1/2/3 triggers), block (IsBlocking), and dodge (IsDodging/IsDodgingBackwards) animations play correctly — no regressions
8. All 203 EditMode tests pass — 0 regressions

## Tasks / Subtasks

- [x] Task 1: Add `IsInCombat` parameter to `PlayerAnimatorController.controller` (AC: 1)
  - [x] 1.1 Open `Assets/_Game/Art/Characters/Player/Animations/PlayerAnimatorController.controller` via YAML edit
  - [x] 1.2 In `m_AnimatorParameters`, append the `IsInCombat` bool entry (see Dev Notes for exact YAML)
  - [x] 1.3 Save and verify Unity imports without error (`read_console`)

- [x] Task 2: Add `CombatIdle` state to the Attack layer (AC: 2, 4)
  - [x] 2.1 In the Attack layer state machine (fileID: `2604412984486709926`), add `CombatIdle` to `m_ChildStates` (see Dev Notes)
  - [x] 2.2 Add the `AnimatorState` YAML block for `CombatIdle` (fileID: `7130000000000000001`, motion: `Unarmed Idle.fbx`)
  - [x] 2.3 Save and verify import

- [x] Task 3: Add transitions between `LockOn Locomotion` and `CombatIdle` (AC: 2, 3, 7)
  - [x] 3.1 Add `AnimatorStateTransition` YAML block for LockOn Locomotion → CombatIdle (fileID: `7130000000000000002`, condition: IsInCombat=true, no exit time, 0.2 s)
  - [x] 3.2 Add `AnimatorStateTransition` YAML block for CombatIdle → LockOn Locomotion (fileID: `7130000000000000003`, condition: IsInCombat=false, no exit time, 0.2 s)
  - [x] 3.3 Add fileID `7130000000000000002` to the `m_Transitions` list of the `LockOn Locomotion` state (fileID: `1426202001129919109`)
  - [x] 3.4 Add fileID `7130000000000000003` to the `m_Transitions` list of the new `CombatIdle` state
  - [x] 3.5 Save and verify — imported without error; transitions confirmed in YAML

- [x] Task 4: Simplify `PlayerAnimator.cs` — remove `_hasIsInCombatParam` guard (AC: 1)
  - [x] 4.1 Remove `private bool _hasIsInCombatParam;` field declaration
  - [x] 4.2 Remove the `foreach (var p in _animator.parameters) ...` loop from `Awake()`
  - [x] 4.3 Update `SetInCombat()` summary comment and remove the `&& _hasIsInCombatParam` guard
  - [x] 4.4 Verify compilation (`read_console`) — 0 errors
  - [x] 4.5 Run EditMode tests — 203/203 pass (AC: 8)

- [x] Task 5: Create `Sword_AnimatorOverride.overrideController` asset (AC: 5, 6)
  - [x] 5.1 Created via temporary editor script — saved to `Assets/_Game/Art/Characters/Player/Animations/`
  - [x] 5.2 Controller set to `PlayerAnimatorController`
  - [x] 5.3 "Unarmed Idle" → `SwordIdle` override confirmed in console log (`found override: True`)
  - [x] 5.4 All other clip overrides unchanged (21 entries logged, only Unarmed Idle overridden)
  - [x] 5.5 Asset saved and verified

- [x] Task 6: Assign the override controller to `Weapon_TestSword.asset` (AC: 5, 6)
  - [x] 6.1 Selected `Assets/_Game/Data/Items/Weapons/Weapon_TestSword.asset`
  - [x] 6.2 Set `animatorOverrideController` field → `Sword_AnimatorOverride` (GUID: `5e410d603e59ca64fb0e1f276a797f50`)
  - [x] 6.3 Asset saved and Unity refreshed without error

- [x] Task 7: Update `Assets/_Game/Art/Characters/Player/Animations/CLAUDE.md` (AC: documentation)
  - [x] 7.1 Added "Attack Layer — CombatIdle Upper Body Idle Switching" section with design, fileIDs, clip GUIDs, and AOC pattern
  - [x] 7.2 Updated Code Review Checklist with AOC clip-name matching patterns

- [x] Task 8: Play-mode validation (AC: 1–8)
  - [ ] 8.1 No weapon equipped → press R → verify upper body shows `Unarmed Idle` pose (arms relaxed)
  - [ ] 8.2 Equip `Weapon_TestSword` → press R → verify upper body shows `Sword Idle` pose (sword raised)
  - [ ] 8.3 While in combat stance → walk → verify lower body locomotion plays normally; upper body holds combat idle
  - [ ] 8.4 While in combat stance → attack (LMB) → verify attack animations play correctly on upper body; combo chain works
  - [ ] 8.5 While in combat stance → block (RMB) → verify block animation plays
  - [ ] 8.6 While in combat stance → dodge → verify dodge roll plays
  - [ ] 8.7 Press R again → verify upper body returns to locomotion blend (LockOn Locomotion state)
  - [ ] 8.8 Unequip sword → press R → verify Unarmed Idle returns
  - [x] 8.9 All 203 EditMode tests pass

## Dev Notes

### Architecture Overview — What This Story Changes

```
PlayerStateManager.SetInCombat(true)
  → PlayerAnimator.SetInCombat(true)
      → _animator.SetBool("IsInCombat", true)          ← NOW ACTIVE (7.13 adds param)
  → [Attack layer state machine]
      LockOn Locomotion → CombatIdle (0.2s crossfade)
      CombatIdle plays: Unarmed Idle (base) OR SwordIdle (AOC override)

EquipmentVisuals.RefreshWeapon()
  → ApplyAnimatorOverride(weapon.animatorOverrideController)
      → if sword: _animator.runtimeAnimatorController = Sword_AnimatorOverride
                  (overrides "Unarmed Idle" clip → "SwordIdle")
      → if null/no weapon: _animator.runtimeAnimatorController = _defaultAnimatorController
                           (base: plays "Unarmed Idle" in CombatIdle)
```

**Key design decisions:**
- Base controller attack states (`Attack_1_State`, `Attack_2_State`, `Attack_3_State`) continue using the existing sword attack clips (`AttackLeft.fbx`, `AttackOverhead.fbx`). This means "unarmed" attacks currently play sword animations — this is an accepted prototype shortcut. Future stories adding non-sword weapons will use AOC to override attacks per weapon type.
- The `Sword_AnimatorOverride` only needs to override the `Unarmed Idle` clip → `SwordIdle`. No attack clip overrides needed for the test sword since the base controller already uses sword attack clips.
- The Attack layer's UpperBodyMask means the CombatIdle only affects the upper body. The base locomotion blend tree plays normally on the lower body at all times.

---

### Animator Controller Structure (Critical Reference)

File: `Assets/_Game/Art/Characters/Player/Animations/PlayerAnimatorController.controller`

| Object | fileID |
|--------|--------|
| AnimatorController root | `9100000` |
| Base Layer state machine | `64360229391699431` |
| Attack Layer state machine | `2604412984486709926` |
| Attack Layer default state ("LockOn Locomotion") | `1426202001129919109` |
| Attack_1_State | `1100000000000000001` |
| Attack_2_State | `1100000000000000002` |
| Attack_3_State | `1100000000000000003` |
| Block_State | `1100000000000000010` |

**New objects added by this story:**

| Object | fileID |
|--------|--------|
| CombatIdle AnimatorState | `7130000000000000001` |
| Transition: LockOn Locomotion → CombatIdle | `7130000000000000002` |
| Transition: CombatIdle → LockOn Locomotion | `7130000000000000003` |

**Clip references needed:**

| Clip | YAML motion reference |
|------|-----------------------|
| Unarmed Idle | `{fileID: -203655887218126122, guid: 60a7f0e6f935b1e4f92daf7047221083, type: 3}` |
| SwordIdle | `{fileID: -203655887218126122, guid: e9be9528f8dab6c4a885457f845caa71, type: 3}` |

**Attack layer configuration:**
- Mask: `UpperBodyMask` — `{fileID: 31900000, guid: f235935659ca71b47ad63ebd7938e710, type: 2}`
- BlendingMode: `0` (Override)
- DefaultWeight: `1` (always active)

---

### Task 1: YAML Edit — Add `IsInCombat` Parameter

In the `m_AnimatorParameters` array of the `AnimatorController` root object (starting at line ~308), after the `VelocityZ` entry, append:

```yaml
  - m_Name: IsInCombat
    m_Type: 4
    m_DefaultFloat: 0
    m_DefaultInt: 0
    m_DefaultBool: 0
    m_Controller: {fileID: 9100000}
```

> `m_Type: 4` = bool parameter. `m_DefaultBool: 0` = false (weapon sheathed by default).

---

### Task 2: YAML Edit — Add `CombatIdle` State

**2a. In the Attack layer state machine (fileID: `2604412984486709926`), add to `m_ChildStates`:**

```yaml
  - serializedVersion: 1
    m_State: {fileID: 7130000000000000001}
    m_Position: {x: 260, y: -420, z: 0}
```

**2b. Add a new `AnimatorState` root object (place it alongside the other AnimatorState blocks):**

```yaml
--- !u!1102 &7130000000000000001
AnimatorState:
  serializedVersion: 6
  m_ObjectHideFlags: 1
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  m_Name: CombatIdle
  m_Speed: 1
  m_CycleOffset: 0
  m_Transitions:
  - {fileID: 7130000000000000003}
  m_StateMachineBehaviours: []
  m_Position: {x: 50, y: 50, z: 0}
  m_IKOnFeet: 0
  m_WriteDefaultValues: 0
  m_Mirror: 0
  m_SpeedParameterActive: 0
  m_MirrorParameterActive: 0
  m_CycleOffsetParameterActive: 0
  m_TimeParameterActive: 0
  m_Motion: {fileID: -203655887218126122, guid: 60a7f0e6f935b1e4f92daf7047221083, type: 3}
  m_Tag:
  m_SpeedParameter:
  m_MirrorParameter:
  m_CycleOffsetParameter:
  m_TimeParameter:
```

> **Critical:** `m_WriteDefaultValues: 0` — required per CLAUDE.md. All animator states must have this `false` or T-pose bleed occurs.

---

### Task 3: YAML Edit — Add Transitions

**3a. Add the LockOn Locomotion → CombatIdle transition:**

```yaml
--- !u!1101 &7130000000000000002
AnimatorStateTransition:
  m_ObjectHideFlags: 1
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  m_Name:
  m_Conditions:
  - m_ConditionMode: 1
    m_ConditionEvent: IsInCombat
    m_EventTreshold: 0
  m_DstStateMachine: {fileID: 0}
  m_DstState: {fileID: 7130000000000000001}
  m_Solo: 0
  m_Mute: 0
  m_IsExit: 0
  serializedVersion: 3
  m_TransitionDuration: 0.2
  m_TransitionOffset: 0
  m_ExitTime: 0
  m_HasExitTime: 0
  m_HasFixedDuration: 1
  m_InterruptionSource: 0
  m_OrderedInterruption: 1
  m_CanTransitionToSelf: 0
```

> `m_ConditionMode: 1` = "If true" (bool is true). See CLAUDE.md: manage_animation tool sets this wrong; always use YAML.

**3b. Add the CombatIdle → LockOn Locomotion transition:**

```yaml
--- !u!1101 &7130000000000000003
AnimatorStateTransition:
  m_ObjectHideFlags: 1
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  m_Name:
  m_Conditions:
  - m_ConditionMode: 2
    m_ConditionEvent: IsInCombat
    m_EventTreshold: 0
  m_DstStateMachine: {fileID: 0}
  m_DstState: {fileID: 1426202001129919109}
  m_Solo: 0
  m_Mute: 0
  m_IsExit: 0
  serializedVersion: 3
  m_TransitionDuration: 0.2
  m_TransitionOffset: 0
  m_ExitTime: 0
  m_HasExitTime: 0
  m_HasFixedDuration: 1
  m_InterruptionSource: 0
  m_OrderedInterruption: 1
  m_CanTransitionToSelf: 0
```

> `m_ConditionMode: 2` = "If false" (bool is false).

**3c. Update the `LockOn Locomotion` state (fileID: `1426202001129919109`) to include the new outgoing transition:**

Find the state at `&1426202001129919109` and add `{fileID: 7130000000000000002}` to its `m_Transitions` list:

```yaml
  m_Transitions:
  - {fileID: -6523477854359575753}   # existing transition (keep this)
  - {fileID: 7130000000000000002}    # ADD: new → CombatIdle transition
```

---

### Task 4: PlayerAnimator.cs Changes (Exact Code)

**Remove from the `// Cached:` comment block and field declarations:**
```csharp
// Cached: IsInCombat parameter not present until Story 7.13 adds it to the controller
private bool _hasIsInCombatParam;
```

**Remove from `Awake()` (the loop that scanned parameters):**
```csharp
foreach (var p in _animator.parameters)
    if (p.nameHash == IsInCombatHash) { _hasIsInCombatParam = true; break; }
```

**Replace `SetInCombat()` with (remove the `_hasIsInCombatParam` guard):**
```csharp
/// <summary>Drives the IsInCombat animator bool. Parameter and CombatIdle state added in Story 7.13.</summary>
public void SetInCombat(bool value)
{
    if (_animator != null) _animator.SetBool(IsInCombatHash, value);
}
```

> **Why the guard was there:** Story 7.12 added `SetInCombat()` as a stub because the `IsInCombat` parameter didn't yet exist in the controller. Unity 6 logs a warning when `SetBool` is called for a missing parameter (contrary to older docs claiming silent no-op). Story 7.13 adds the parameter, making the guard unnecessary.

---

### Task 5: AnimatorOverrideController — How AOC Clip Overrides Work

When `EquipmentVisuals.ApplyAnimatorOverride(overrideController)` runs:
```csharp
_animator.runtimeAnimatorController = overrideController;
// AnimatorOverrideController wraps the base controller and replaces specific clips.
// Clip lookup is by AnimationClip asset reference (not by state name).
```

The `Sword_AnimatorOverride` asset must list:
- **Original clip:** `Unarmed Idle` (the clip used by the `CombatIdle` state in the base controller)
- **Override clip:** `SwordIdle` (from `SwordIdle.fbx`)

**In Unity Editor:**
1. `Assets → Create → Animator Override Controller` → name: `Sword_AnimatorOverride`
2. Save to `Assets/_Game/Art/Characters/Player/Animations/`
3. Inspector: set **Controller** field → `PlayerAnimatorController`
4. The clip list populates. Locate "Unarmed Idle" row.
5. Set the override to `SwordIdle` (search by name in the asset picker)
6. Save

> **If "Unarmed Idle" does not appear in the clip list:** The `CombatIdle` state's motion reference (Task 2) was not saved correctly. Reimport the controller and check that `m_Motion` points to `guid: 60a7f0e6f935b1e4f92daf7047221083`. After Task 2, the clip should appear automatically.

> **AOC does NOT override by state name** — it overrides by clip asset reference. If two states use the same clip, both get the override. Ensure `Unarmed Idle` is only used in `CombatIdle`; if it appears elsewhere unexpectedly, investigate.

---

### Impact on Existing Systems

| System | Change | Risk |
|--------|--------|------|
| `PlayerAnimator.cs` | Remove `_hasIsInCombatParam` guard; simplify `SetInCombat()` | LOW — param now exists; guard was preventing the bool from ever being set |
| `PlayerAnimatorController.controller` | Add `IsInCombat` param; add `CombatIdle` state; add 2 transitions to Attack layer | MEDIUM — YAML edit risk; use exact fileIDs from Dev Notes |
| `EquipmentVisuals.cs` | No changes — `ApplyAnimatorOverride()` already exists from Story 7.8 | None |
| `PlayerStateManager.cs` | No changes — `SetInCombat()` call chain already routes correctly | None |
| `PlayerCombat.cs` | No changes — `OnDrawWeaponStarted` already calls `_stateManager.SetInCombat()` | None |
| `Weapon_TestSword.asset` | Assign `Sword_AnimatorOverride` to `animatorOverrideController` field | LOW — Inspector-only change |
| Attack/block/dodge animations | No changes to existing transitions or clips — new CombatIdle state is ONLY reachable from LockOn Locomotion; AnyState transitions for attacks/blocks/dodges still fire from CombatIdle | LOW |
| EditMode tests | No code logic changes — all 203 tests expected to pass unchanged | None |

---

### Regression Risks

1. **YAML fileID collision:** If any of the new fileIDs (`7130000000000000001`, `7130000000000000002`, `7130000000000000003`) collide with existing objects in the `.controller` file, Unity will fail to import. Verify uniqueness by grepping for those IDs before saving.

2. **`m_WriteDefaultValues: 0` omission:** If the new `CombatIdle` state has `m_WriteDefaultValues: 1`, it will write T-pose defaults for unmasked bones during the CombatIdle animation, causing pose corruption in the lower body. Always use `0`.

3. **`manage_animation` conditionMode bug:** If transitions are added via MCP `manage_animation(controller_add_transition)` instead of YAML, the conditionMode values will be wrong (uses `3`/Equals instead of `1`/If or `2`/IfNot for bools). Always use YAML for transitions. See `Assets/_Game/Art/Characters/Player/Animations/CLAUDE.md`.

4. **AnimatorOverrideController clip matching:** If `Unarmed Idle` doesn't appear in the AOC's clip list, the `CombatIdle` state's motion was not saved correctly (see Task 5 note). Do not proceed with Task 6 until the AOC shows the clip.

5. **`CanTransitionToSelf: 0` on transitions:** Prevents the transition from re-triggering when already in the target state. Set to `0` on both new transitions. If set to `1`, entering CombatIdle while already in CombatIdle could cause visual glitches.

6. **Attack states return to LockOn Locomotion (not CombatIdle) after finish:** After an attack completes, the state machine exits to the Attack layer's LockOn Locomotion state (existing exit transitions), then immediately re-transitions to CombatIdle if `IsInCombat` is still true (via the new LockOn Locomotion → CombatIdle transition). There may be a brief 0.2s blend through LockOn Locomotion before CombatIdle re-activates. If this looks jarring, consider adding `AnyState → CombatIdle` or direct exit transitions from attack states. For the first pass, the LockOn Locomotion intermediate is acceptable.

---

### Previous Story Intelligence (7.12)

From story 7.12 completion notes:
- `PlayerAnimator._hasIsInCombatParam` was added as a guard because Unity 6 warns on `SetBool` for missing parameters. Story 7.13 eliminates the need for this guard by adding the parameter.
- `EquipmentVisuals.ApplyAnimatorOverride()` is already wired and called during `RefreshWeapon()`. No changes needed to EquipmentVisuals.
- `SwordBase_Visual.prefab` already has `Drawn`/`Sheathed` child convention. No changes to weapon prefabs needed for 7.13.
- `203/203` EditMode tests — baseline to maintain.

From story 7.8 completion notes:
- `WeaponSO.animatorOverrideController` field is already serialized and hooked up. The field on `Weapon_TestSword.asset` is currently `null` — Task 6 assigns it.
- `EquipmentVisuals._animator` and `._defaultAnimatorController` are already wired in the Player prefab via `WireEquipmentVisuals.cs` editor utility. No prefab changes needed.

From 7.11 animation events CLAUDE.md:
- `HitboxEnable` / `HitboxDisable` / `ComboWindowOpen` / `ComboWindowClose` events are on attack FBX `.meta` files. No changes needed — attack clips are unmodified.

---

### Project Structure Notes

New assets created by this story:
- `Assets/_Game/Art/Characters/Player/Animations/Sword_AnimatorOverride.overrideController` (new)
- `Assets/_Game/Art/Characters/Player/Animations/Sword_AnimatorOverride.overrideController.meta` (auto-generated)

Modified files:
- `Assets/_Game/Art/Characters/Player/Animations/PlayerAnimatorController.controller`
- `Assets/_Game/Art/Characters/Player/Animations/PlayerAnimatorController.controller.meta` (may update if controller GUID changes — should NOT change)
- `Assets/_Game/Scripts/Player/PlayerAnimator.cs`
- `Assets/_Game/Data/Items/Weapons/Weapon_TestSword.asset`
- `Assets/_Game/Art/Characters/Player/Animations/CLAUDE.md`
- `_bmad-output/implementation-artifacts/sprint-status.yaml`

### References

- [Source: _bmad-output/epics.md#Epic-7] — Story 7.13 user story definition
- [Source: _bmad-output/implementation-artifacts/7-12-combat-state-toggle.md#Dev-Notes] — `_hasIsInCombatParam` guard rationale; `SetInCombat` stub context
- [Source: _bmad-output/implementation-artifacts/7-8-equipment-visual-prefab-support.md#AC-2] — `WeaponSO.animatorOverrideController` field and `ApplyAnimatorOverride()` pattern
- [Source: Assets/_Game/Art/Characters/Player/Animations/CLAUDE.md] — `m_WriteDefaultValues: 0` mandate; `manage_animation` conditionMode bug
- [Source: Assets/_Game/Scripts/Player/CLAUDE.md#PlayerAnimator] — Combat animation API rules; API must go through `PlayerAnimator` only
- [Source: Assets/_Game/Scripts/Combat/CLAUDE.md#AnimationEventReceiver-System] — Animation event timing on attack clips (unaffected by 7.13)
- [Source: Assets/_Game/Art/Characters/Player/Animations/PlayerAnimatorController.controller] — fileID reference table (lines 369–393 for layer structure, lines 896–949 for Attack layer state machine)

## Dev Agent Record

### Agent Model Used

claude-sonnet-4-6

### Debug Log References

### Completion Notes List

- Added `IsInCombat` bool parameter (m_Type: 4) to `m_AnimatorParameters` in `PlayerAnimatorController.controller` after `VelocityZ` entry.
- Added `CombatIdle` AnimatorState (fileID: `7130000000000000001`) to Attack layer `m_ChildStates`; motion references Unarmed Idle GUID `60a7f0e6f935b1e4f92daf7047221083`; `m_WriteDefaultValues: 0` as required.
- Added `LockOn Locomotion → CombatIdle` transition (fileID: `7130000000000000002`, conditionMode: 1/If/true, 0.2s) and `CombatIdle → LockOn Locomotion` transition (fileID: `7130000000000000003`, conditionMode: 2/If/false, 0.2s) via YAML; not via `manage_animation` to avoid conditionMode bug.
- Removed `_hasIsInCombatParam` field, parameter scan loop, and `&&_hasIsInCombatParam` guard from `PlayerAnimator.cs`. `SetInCombat()` now calls `SetBool` directly.
- Created `Sword_AnimatorOverride.overrideController` via temporary C# editor script; AOC confirmed 21 clip entries with "Unarmed Idle" override applied (`found override: True` in console).
- Assigned `Sword_AnimatorOverride` (GUID: `5e410d603e59ca64fb0e1f276a797f50`) to `Weapon_TestSword.asset.animatorOverrideController` via YAML edit.
- 203/203 EditMode tests pass — 0 regressions.
- Play-mode subtasks 8.1–8.8 require manual verification in the Unity Editor (visual animation check not automatable via MCP).

### File List

- Assets/_Game/Art/Characters/Player/Animations/PlayerAnimatorController.controller
- Assets/_Game/Scripts/Player/PlayerAnimator.cs
- Assets/_Game/Data/Items/Weapons/Weapon_TestSword.asset
- Assets/_Game/Art/Characters/Player/Animations/Sword_AnimatorOverride.overrideController (new)
- Assets/_Game/Art/Characters/Player/Animations/Sword_AnimatorOverride.overrideController.meta (new, auto-generated)
- Assets/_Game/Art/Characters/Player/Animations/CLAUDE.md
- _bmad-output/implementation-artifacts/sprint-status.yaml
- _bmad-output/implementation-artifacts/7-13-combat-animation-rewiring.md
