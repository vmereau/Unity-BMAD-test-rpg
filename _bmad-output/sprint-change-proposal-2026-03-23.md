# Sprint Change Proposal — 2026-03-23

**Project:** Echoes of the Fallen (Unity-BMAD-test-rpg)
**Author:** Valentin
**Date:** 2026-03-23
**Status:** Approved — pending implementation

---

## Section 1: Issue Summary

### Problem Statement

As the combat animation pipeline matured through Stories 7.10 (animation-event combo windows) and 7.11 (animation-event hitbox activation), the absence of a **weapon draw/sheathe state** became a design gap. Currently the player always holds the weapon in hand and attacking is gated only by stamina — there is no explicit "combat stance" entry. This conflicts with the Gothic-style design intent (Immersion pillar: "no systems that break the fiction") and prevents weapon-specific animation sets from being meaningfully layered.

### Context

- Stories 7.10 and 7.11 are **complete** — the animation-event pipeline is solid and proven
- Story 7.11 is currently in **review** (203/203 tests passing)
- This change is **additive** — it builds cleanly on top of the existing foundation without requiring rollback of any prior work

### Evidence / Motivation

1. The `PlayerAnimatorController` now supports `AnimatorOverrideController` per weapon (via `EquipmentVisuals.ApplyAnimatorOverride()`), and weapon-specific animation clips exist at `Combat/attacks/Sword/` and `Combat/attacks/Unarmed/`
2. The `PlayerStateManager` gate pattern is well established — adding `IsInCombat` follows the exact same pattern as `IsBlocking`, `IsAttacking`, `IsDodging`
3. `EquipmentVisuals` already holds a `_weaponSocket` reference — adding `_undrawnWeaponSocket` and a `SetCombatState()` method is minimal surface area

---

## Section 2: Impact Analysis

### Epic Impact

**Epic 7 (Equipment & Economy) — in-progress**
- Two new developer stories added: `7-12` and `7-13`
- Remaining backlog stories (7-5 shop, 7-6 looting, 7-7 gold bribe) are **independent** of combat state — no blocking dependency

**Epic 8 (Crafting & Stealth) — backlog**
- Stealth stories (crouch, visibility cone) should gate on `!IsInCombat` when written — minor guard condition addition, no structural change needed now

**Epic 9 (Content & Polish) — backlog**
- Animation polish stories will need to be written with awareness of the InCombat/Default animation layer split — minor context addition

### Story Impact

| Story | Status | Impact |
|---|---|---|
| 7-11 (hitbox animation events) | review | **None** — completes independently; 7-12/7-13 build on its foundation |
| 7-12 (combat state toggle) | new — backlog | New story; must complete before 7-13 |
| 7-13 (combat animation rewiring) | new — backlog | New story; depends on 7-12 |
| 7-5, 7-6, 7-7 | backlog | **None** — unaffected |

### Artifact Conflicts

| Artifact | Impact |
|---|---|
| `epics.md` | Add story descriptions for 7-12 and 7-13 to Epic 7 |
| `sprint-status.yaml` | Add `7-12-combat-state-toggle: backlog` and `7-13-combat-animation-rewiring: backlog` |
| `_bmad-output/project-context.md` | Add InCombat gate pattern to combat rules |
| `Assets/_Game/Scripts/Player/CLAUDE.md` | Add InCombat code review checklist entries |

### Technical Impact

**Scripts modified:** `PlayerStateManager.cs`, `PlayerAnimator.cs`, `PlayerCombat.cs`, `EquipmentVisuals.cs`
**Assets modified:** `Player.prefab`, `PlayerAnimatorController.controller`, `Weapon_TestSword.asset`, 3× sword FBX meta files, 1× unarmed FBX meta file
**Assets created:** `CombatAnimatorOverride_Sword.overrideController`
**Input:** `InputSystem_Actions` (both files) — `DrawWeapon` action added (R key, Player map)
**Tests:** Existing 203 EditMode tests expected to pass without modification; no new pure-logic unit tests required for socket swap or layer weight

---

## Section 3: Recommended Approach

**Selected path: Direct Adjustment**

Add two new stories (7-12, 7-13) to Epic 7's backlog and implement them in sequence after 7-11 exits review. No rollback of prior work. No MVP scope reduction.

**Rationale:**
- 7.10/7.11 are clean foundations — additive work only
- The `PlayerStateManager` gate pattern is battle-tested across 4 existing states; `IsInCombat` slots in naturally
- The `AnimatorOverrideController` mechanism already exists in `EquipmentVisuals`; weapon-specific animation sets are already in place in the project
- Risk is Low-Medium: `PlayerAnimatorController` changes are the most complex part, but the layer weight + `IsInCombat` bool approach is standard Unity practice

**Effort estimate:** Medium (2 dev stories, each ~1 session)
**Risk:** Low-Medium (Animator Controller wiring is the highest-friction task)
**Timeline impact:** Minimal — 7-5/7-6/7-7 are unblocked and can proceed in parallel or after

---

## Section 4: Detailed Change Proposals

### Story 7-12: InCombat State, R-Key Draw/Sheathe, UndrawnWeaponSocket

**User story:** As a player, I can press R to draw my weapon and enter combat stance (and R again to sheathe it and return to default), so my character visually reflects whether I'm ready to fight, and attacking/blocking are intentionally gated to when the weapon is drawn.

**Acceptance Criteria:**
1. Pressing R toggles `IsInCombat` on `PlayerStateManager` (true → false → true…); R is ignored while `IsBusy`
2. `CanAttack()` returns false when `!IsInCombat` — attacks are impossible with weapon sheathed
3. `CanBlock()` returns false when `!IsInCombat` — blocking is impossible with weapon sheathed
4. `CanDodge()` is unchanged — dodging always permitted regardless of combat state
5. When entering InCombat: `_weaponVisual` is reparented to `WeaponSocket` (hand)
6. When exiting InCombat: `_weaponVisual` is reparented to `UndrawnWeaponSocket` (hip/scabbard)
7. Player prefab has `UndrawnWeaponSocket` child GO under the hip bone (position left for manual adjustment by Valentin)
8. All 203 EditMode tests pass — 0 regressions

**Files to change:**

| File | Change |
|---|---|
| `InputSystem_Actions.inputactions` | Add `DrawWeapon` button action (R key) to Player map |
| `InputSystem_Actions.cs` | Add `DrawWeapon` to embedded JSON (both files required — project rule) |
| `Assets/_Game/Scripts/Player/PlayerStateManager.cs` | Add `IsInCombat` property; `SetInCombat(bool)` setter calling `_playerAnimator.SetInCombat()`; gate `CanAttack` and `CanBlock` on `IsInCombat` |
| `Assets/_Game/Scripts/Player/PlayerAnimator.cs` | Add `IsInCombatHash`; add `SetInCombat(bool)` driving the animator bool |
| `Assets/_Game/Scripts/Combat/PlayerCombat.cs` | Subscribe `DrawWeapon.started` in OnEnable/OnDisable; add `OnDrawWeaponStarted()` handler toggling `_stateManager.SetInCombat` and `_equipmentVisuals.SetCombatState` |
| `Assets/_Game/Scripts/Inventory/EquipmentVisuals.cs` | Add `_undrawnWeaponSocket` serialized field; `_isInCombat` private field; `SetCombatState(bool)` public method reparenting `_weaponVisual`; update `RefreshWeapon()` to attach to correct socket on initial equip |
| `Assets/_Game/Prefabs/Player/Player.prefab` | Add `UndrawnWeaponSocket` empty child GO under hip bone; wire `_undrawnWeaponSocket` on EquipmentVisuals |

**Critical patterns:**

```csharp
// PlayerStateManager.cs
public bool IsInCombat { get; private set; }

public void SetInCombat(bool value)
{
    IsInCombat = value;
    _playerAnimator.SetInCombat(value);
    GameLog.Info(TAG, $"Combat stance: {(value ? "DRAWN" : "sheathed")}");
}

// Gates updated:
public bool CanAttack() => !IsBusy && !IsAirborne && !IsBlocking && !IsDodging && IsInCombat;
public bool CanBlock()  => !IsBusy && !IsAirborne && !IsDodging && IsInCombat;
// CanDodge() — UNCHANGED
```

```csharp
// PlayerCombat.cs — new handler
private void OnDrawWeaponStarted(InputAction.CallbackContext ctx)
{
    if (_stateManager.IsBusy) return;
    bool entering = !_stateManager.IsInCombat;
    _stateManager.SetInCombat(entering);
    _equipmentVisuals?.SetCombatState(entering);
}
```

```csharp
// EquipmentVisuals.cs — socket swap
public void SetCombatState(bool isInCombat)
{
    _isInCombat = isInCombat;
    if (_weaponVisual == null) return;
    var targetSocket = isInCombat ? _weaponSocket : _undrawnWeaponSocket;
    if (targetSocket == null) return;
    _weaponVisual.transform.SetParent(targetSocket, worldPositionStays: false);
    _weaponVisual.transform.localPosition = Vector3.zero;
    _weaponVisual.transform.localRotation = Quaternion.identity;
    GameLog.Info(TAG, $"Weapon visual moved to {targetSocket.name}");
}
```

---

### Story 7-13: Combat Animation Rewiring (InCombat Upper Body Layer)

**User story:** As a player, when I draw my weapon (enter InCombat), my upper body shows a weapon-specific idle animation and my attacks use weapon-appropriate clips, so the visual feedback matches what I'm holding.

**Acceptance Criteria:**
1. When `IsInCombat=false`: upper body animations unchanged — base locomotion plays as before
2. When `IsInCombat=true` + sword equipped: upper body plays `SwordIdle.fbx` (`Combat/attacks/Sword/`)
3. When `IsInCombat=true` + unarmed: upper body plays `Unarmed Idle.fbx` (`Combat/attacks/Unarmed/`)
4. When `IsInCombat=true` + sword: Attack_1/2/3 use sword-specific clips from `Combat/attacks/Sword/`
5. When `IsInCombat=true` + unarmed: Attack_1/2/3 use `Jab Cross.fbx` (single clip reused for all 3 unarmed steps)
6. All three `Combat/attacks/Sword/*.fbx.meta` files have the 4 animation events (HitboxEnable @ 0.25, HitboxDisable @ 0.50, ComboWindowOpen @ 0.50, ComboWindowClose @ 0.90) — same format as root attack metas
7. `Combat/attacks/Unarmed/Jab Cross.fbx.meta` has ComboWindowOpen @ 0.50 and ComboWindowClose @ 0.90 (HitboxEnable/Disable included for consistency; no-ops when unarmed since `_activeHitbox` is null)
8. All EditMode tests pass — 0 regressions

**Depends on:** Story 7-12 (requires `IsInCombat` animator parameter)

**Files to change:**

| File | Change |
|---|---|
| `Assets/_Game/Scripts/Player/PlayerAnimator.cs` | Update `SetInCombat(bool)` to also set upper body layer weight (1.0 in combat, 0.0 default); cache `_upperBodyLayerIndex` via `Animator.GetLayerIndex("UpperBody")` in Awake |
| `Assets/_Game/Art/Characters/Player/Animations/PlayerAnimatorController.controller` | Add `IsInCombat` bool parameter; add `CombatIdle` state to UpperBody layer with `Unarmed Idle.fbx` as default clip; set layer default weight to 0 (controlled at runtime by `SetLayerWeight`); ensure attack states are reachable from `CombatIdle` |
| `CombatAnimatorOverride_Sword.overrideController` (NEW) | Path: `Assets/_Game/Art/Characters/Player/Animations/Combat/`; overrides CombatIdle → `SwordIdle.fbx`, Attack_1 → `AttackLeft.fbx`, Attack_2 → `AttackOverhead.fbx`, Attack_3 → `AttackThrust.fbx` (all from `Combat/attacks/Sword/`) |
| `Assets/_Game/Data/Items/Weapon_TestSword.asset` | Assign `CombatAnimatorOverride_Sword` to `animatorOverrideController` field |
| `Combat/attacks/Sword/AttackLeft.fbx.meta` | Add 4 animation events (same format as `attacks/AttackLeft.fbx.meta`) |
| `Combat/attacks/Sword/AttackOverhead.fbx.meta` | Add 4 animation events |
| `Combat/attacks/Sword/AttackThrust.fbx.meta` | Add 4 animation events |
| `Combat/attacks/Unarmed/Jab Cross.fbx.meta` | Add 4 animation events (ComboWindow events meaningful; HitboxEnable/Disable are harmless no-ops) |
| `Assets/_Game/Scripts/Player/CLAUDE.md` | Add InCombat review checklist entries |
| `Assets/_Game/Scripts/Combat/CLAUDE.md` | Document InCombat animation layer + sword override pattern |
| `_bmad-output/project-context.md` | Add InCombat gate pattern to combat rules |

**Animation event format (for all new FBX metas):**
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
      - time: 0.5
        functionName: ComboWindowOpen
        data:
        objectReferenceParameter: {fileID: 0}
        floatParameter: 0
        intParameter: 0
        messageOptions: 0
      - time: 0.9
        functionName: ComboWindowClose
        data:
        objectReferenceParameter: {fileID: 0}
        floatParameter: 0
        intParameter: 0
        messageOptions: 0
```

**PlayerAnimator layer weight pattern:**
```csharp
// Awake() — after _animator assignment:
_upperBodyLayerIndex = _animator.GetLayerIndex("UpperBody");

// SetInCombat() — updated:
public void SetInCombat(bool value)
{
    if (_animator == null) return;
    _animator.SetBool(IsInCombatHash, value);
    if (_upperBodyLayerIndex >= 0)
        _animator.SetLayerWeight(_upperBodyLayerIndex, value ? 1f : 0f);
}
```

---

## Section 5: Implementation Handoff

**Change scope: Minor** — direct implementation by dev agent

**Routing:** Development team (dev-story workflow)

**Implementation sequence:**
1. Complete Story 7-11 code review → merge
2. Implement Story 7-12 (`7-12-combat-state-toggle`)
3. Code review Story 7-12
4. Implement Story 7-13 (`7-13-combat-animation-rewiring`) — depends on 7-12
5. Code review Story 7-13

**Success criteria:**
- Story 7-12: R key toggles weapon between hip and hand; attacking/blocking impossible with weapon sheathed; dodging unaffected; all tests pass
- Story 7-13: InCombat upper body shows SwordIdle or UnarmedIdle; sword attacks use `Combat/attacks/Sword/` clips with correct animation events; unarmed attacks use `Jab Cross.fbx`; all tests pass

**Manual validation required (both stories):**
- Play-test: enter combat (R), attack with sword → verify SwordIdle + sword attack animations + hitbox timing
- Play-test: unequip sword → unarmed InCombat → UnarmedIdle + Jab Cross attacks + sphere overlap hit detection
- Play-test: exit combat (R) → weapon moves to hip socket visually
- Play-test: attempt attack while sheathed → blocked; attempt dodge while sheathed → permitted

---

## Change Log

| Date | Change |
|---|---|
| 2026-03-23 | Sprint Change Proposal created — two new stories (7-12, 7-13) added to Epic 7 for InCombat state, weapon socket swap, and combat animation rewiring |
