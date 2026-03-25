# CLAUDE.md — Assets/_Game/Scripts/Combat

> Loaded when Claude accesses files in this folder. Covers the WeaponHitbox system and PlayerCombat attack pipeline.

---

## WeaponHitbox System (Story 7.9)

### Overview

`WeaponHitbox` is placed on the **weapon mesh child GO** inside a weapon visual prefab (e.g. `SM_Sword_1` inside `SwordBase_Visual`). It manages trigger collider(s) that represent the weapon's hit zone.

- Colliders start **disabled** (dormant) — enabled only during an active attack frame window
- `Enable()` / `Disable()` are called by `PlayerCombat` at the start/end of each attack window
- `OnTriggerEnter` fires `OnEnemyHit` C# event if `EnemyHealth` is found in the collided object's parent hierarchy

### Binding Pattern — OnVisualsRefreshed GameEventSO

`PlayerCombat` subscribes to `GameEventSO_Void _onVisualsRefreshed` (`Assets/_Game/Data/Events/OnVisualsRefreshed.asset`). `EquipmentVisuals` raises this SO at the very end of `Refresh()`, after `_weaponVisual` is assigned — so `ActiveWeaponGO` is always valid when the callback fires.

Both components hold `[SerializeField] private GameEventSO_Void _onVisualsRefreshed;` wired to the same asset in the Inspector.

```csharp
// PlayerCombat OnEnable — also calls BindUnarmedHitbox() directly for startup state
_onVisualsRefreshed?.AddListener(HandleVisualsRefreshed);
BindUnarmedHitbox();

// PlayerCombat OnDisable (before _input null guard)
_onVisualsRefreshed?.RemoveListener(HandleVisualsRefreshed);
UnbindWeaponHitbox();

private void HandleVisualsRefreshed(bool _)  // GameEventSO_Void uses Action<bool>
{
    UnbindWeaponHitbox();
    BindWeaponHitbox();
}

private void BindWeaponHitbox()
{
    _currentWeaponSO = _equipmentSystem?.GetEquipped(EquipmentSlot.Weapon) as WeaponSO;
    var weaponGO = _equipmentVisuals?.ActiveWeaponGO;
    if (weaponGO == null) { BindUnarmedHitbox(); return; }  // no weapon visual — unarmed
    _activeHitbox = weaponGO.GetComponentInChildren<WeaponHitbox>(true);
    if (_activeHitbox != null)
        _activeHitbox.OnEnemyHit += OnWeaponHit;
    else
        BindUnarmedHitbox(); // weapon visual exists but has no WeaponHitbox — fallback
}

private void BindUnarmedHitbox()
{
    _unarmedHitbox.SetActive(true);
    _activeHitbox = _unarmedHitbox.GetComponent<WeaponHitbox>();
    if (_activeHitbox != null)
        _activeHitbox.OnEnemyHit += OnWeaponHit;
}

// EquipmentVisuals.Refresh() — end of method
_onVisualsRefreshed?.Raise(false);
```

**Why `BindUnarmedHitbox()` in `OnEnable`?** `EquipmentVisuals.OnEnable()` calls `Refresh()` which raises `OnVisualsRefreshed`. Because `GameEventSO<T>.Raise()` iterates `_listeners` in reverse (last-added first), component order determines who fires first. As a safety net, `PlayerCombat.OnEnable()` calls `BindUnarmedHitbox()` directly — this ensures a valid hitbox is always bound at startup regardless of listener order.

**Why not `_onEquipmentChanged`?** `PlayerCombat` (component index 7 on Player GO) subscribes before `EquipmentVisuals` (component index 22), so it would be called BEFORE `EquipmentVisuals.Refresh()` ran — making `ActiveWeaponGO` null. The dedicated `OnVisualsRefreshed` SO is raised from within `Refresh()` after `_weaponVisual` is set, eliminating the race entirely.

**Why not a plain C# event?** Plain `System.Action` events across `Game.Inventory` → `Game.Combat` violate the committed architecture rule: cross-system communication must use typed `GameEventSO<T>` channels only.

### Combo-End Disable Requirement

`_activeHitbox?.Disable()` must be called on **every** path that ends an attack combo:
1. `TryAttack` — stamina deny
2. `TryAttack` — `Consume()` fail
3. Finisher executed (last hit in combo) — via `ResetAttackCombo()`
4. `OnComboWindowClose()` animation event — combo window expired; `ResetAttackCombo()` called
5. `OnBlockStarted` — block interrupts combo; `ResetAttackCombo()` called
6. `OnAttackStateExited()` SMB callback — interrupt (dodge/stagger/death) where animation events never fired

Missing any of these paths leaves the hitbox enabled = phantom hits persist after the attack animation ends.

---

## AnimationEventReceiver System (Stories 7.10, 7.11, 7.13)

### Overview

`AnimationEventReceiver` is a bridge MonoBehaviour on the **Player root GO** (same GO as the `Animator`). Unity's animation event system calls public methods on components on the same GameObject — `AnimationEventReceiver` receives them and routes to `PlayerCombat`.

Full routing table:

| Method on `AnimationEventReceiver` | Routes to `PlayerCombat` | Source |
|---|---|---|
| `ComboWindowOpen()` | `OnComboWindowOpen()` | FBX animation event |
| `ComboWindowClose()` | `OnComboWindowClose()` | FBX animation event |
| `HitboxEnable()` | `OnHitboxEnable()` | FBX animation event |
| `HitboxDisable()` | `OnHitboxDisable()` | FBX animation event |
| `NotifyAttackEntered(int)` | `OnAttackStateEntered(int)` | `SMB_AttackState.OnStateEnter` |
| `NotifyAttackExited()` | `OnAttackStateExited()` | `SMB_AttackState.OnStateExit` |

The last two are called by code (SMB), not by the FBX clip — they fire even if the clip was interrupted before any animation event ran.

### Combo Window Gate in TryAttack()

```csharp
if (_stateManager.IsAttacking && !_comboWindowOpen)
{
    // Attack input ignored — waiting for combo window (animation event)
    return;
}
```
`IsAttacking` is `true` from `SetAttacking(true)` until `SetAttacking(false)` — this blocks rapid re-triggering of Attack_1 before the combo window opens.

### comboSteps Query

`PlayerCombat` caches `_currentWeaponSO` in `BindWeaponHitbox()`:
```csharp
_currentWeaponSO = _equipmentSystem?.GetEquipped(EquipmentSlot.Weapon) as WeaponSO;
```
`IsMaxCombo()` queries it:
```csharp
int maxSteps = _currentWeaponSO != null ? _currentWeaponSO.comboSteps : 2;
return _comboStep == maxSteps;
```
Unarmed fallback: `maxSteps = 2` (2-hit combo). `ManageComboStep()` calls `IncreaseAttackCombo()` only if not at max; if `_comboStep > 1` after increment, `_IsComboAttacking` is set `true` to guard the SMB exit path.

### Animation Events on FBX Clips (Story 7.11)

Events are stored in the FBX `.meta` file under `clipAnimations[].events`. The `time` field is **normalized time** (0.0–1.0). All four events must be on every attack clip.

> **Per-clip timing tables and `.meta` YAML format** → `Assets/_Game/Art/Characters/Player/Animations/Combat/CLAUDE.md`

Sword clips use uniform timings (0.25 / 0.50 / 0.50 / 0.90). Unarmed clips have custom timings tuned to their animation arcs — do not copy sword timings verbatim.

### Hitbox Pipeline — Fully Event-Driven (Story 7.11)

`ExecuteAttack()` does **not** call `_activeHitbox.Enable()` directly. The hitbox is entirely driven by animation events and SMB callbacks:

```
Attack input → ExecuteAttack() → animator trigger → clip plays
  → SMB OnStateEnter → NotifyAttackEntered() → OnAttackStateEntered()
      → _activeHitbox?.Disable()   ← safety: ensure hitbox off at state entry
      → _comboWindowOpen = false   ← re-arm gate for this state
  → HitboxEnable at ~25% → HitboxEnable() → OnHitboxEnable() → _activeHitbox?.Enable()
  → HitboxDisable at ~50% → HitboxDisable() → OnHitboxDisable() → _activeHitbox?.Disable()
  → SMB OnStateExit → NotifyAttackExited() → OnAttackStateExited()
      → if _IsComboAttacking: _IsComboAttacking = false; return  ← combo chain, skip cleanup
      → else: ResetAttackCombo() + ExitAttack()  ← interrupt/finisher cleanup
```

---

## SMB_AttackState — Guaranteed State Callbacks (Story 7.13)

`SMB_AttackState` (`SMB_AttackState.cs`) is a `StateMachineBehaviour` on each attack state in `PlayerAnimatorController`. It provides **guaranteed** enter/exit callbacks that complement animation events — animation events handle timing, SMB handles state transitions regardless of interrupt or crossfade.

```csharp
public class SMB_AttackState : StateMachineBehaviour
{
    [SerializeField] private int attackIndex; // 1, 2, or 3 — set per-state in Animator Inspector

    public override void OnStateEnter(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
        => GetReceiver(animator)?.NotifyAttackEntered(attackIndex);

    public override void OnStateExit(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
        => GetReceiver(animator)?.NotifyAttackExited();
}
```

**What each callback does in `PlayerCombat`:**

`OnAttackStateEntered(int attackIndex)`:
- Disables `_activeHitbox` (safety net if the previous state's `HitboxDisable` event was never reached)
- Clears `_comboWindowOpen = false` (re-arms the TryAttack gate for this state)

`OnAttackStateExited()`:
- If `_IsComboAttacking` is true → set it `false` and **return immediately** (combo chain in progress; next state already queued by `ManageComboStep`)
- Otherwise: call `ResetAttackCombo()` + `ExitAttack()` (interrupt or finisher cleanup)

**Why not `GetNextAnimatorStateInfo(layerIndex).IsTag("Attack")` to detect combo chains?**
Unity may have already completed the transition by the time `OnStateExit` fires — `GetNextAnimatorStateInfo` returns empty `AnimatorStateInfo` in that case. Always returns false, even when the next state is an attack state. Use `_IsComboAttacking` flag instead.

---

## `_IsComboAttacking` — Combo Chain Guard (Story 7.13)

A `private bool _IsComboAttacking` field in `PlayerCombat` prevents `SMB_AttackState.OnStateExit` from tearing down combo state mid-chain.

```
ManageComboStep() sets _IsComboAttacking = true  (when _comboStep > 1 after increment)
  ↓ animator transition begins (Attack_1 → Attack_2)
  ↓ SMB OnStateExit fires for Attack_1
    → _IsComboAttacking is true → set false, return, skip cleanup
  ↓ SMB OnStateEnter fires for Attack_2
    → _activeHitbox disabled (safety net)
    → _comboWindowOpen = false (re-armed)
```

**State machine:**
- `false` at rest (no combo chain in flight)
- Set `true` in `ManageComboStep()` when `_comboStep > 1` after incrementing — indicates a mid-combo transition is expected
- Cleared `false` in `OnAttackStateExited()` when it guards the early return — consumed once per transition

**Interrupt scenarios (dodge/stagger/death mid-combo):** `_IsComboAttacking` is not set by input alone — it is only set when `_comboStep > 1`. For first-hit interrupts or interrupts where `ManageComboStep` wasn't reached, `OnAttackStateExited()` runs the full cleanup path.

---

## DrawWeapon / Combat State Toggle (Story 7.12)

`PlayerCombat.OnDrawWeaponStarted` handles the R key to draw/sheathe the weapon:

```csharp
private void OnDrawWeaponStarted(InputAction.CallbackContext ctx)
{
    if (_stateManager.IsBusy) return;
    if (_stateManager.IsAttacking) return;  // guard — prevent mid-swing socket jump
    bool entering = !_stateManager.IsInCombat;
    _stateManager.SetInCombat(entering);       // updates IsInCombat + drives animator bool
    _equipmentVisuals?.SetCombatState(entering); // reparents weapon visual to correct socket
}
```

**Key rules:**
- `CanAttack()` and `CanBlock()` both require `IsInCombat == true` — attacking/blocking while sheathed is blocked at `PlayerStateManager` level, not in `PlayerCombat`
- `CanDodge()` is **unchanged** — dodge always works regardless of combat state
- R is ignored while `IsBusy` (cursor unlocked) **or** while `IsAttacking` — drawing mid-combo would jump the weapon visual to the hip socket while the `Drawn` child is deactivated, silencing the hit window for that swing
- `_equipmentVisuals` ref is reused from Story 7.9 — `SetCombatState()` was added to `EquipmentVisuals` in 7.12
- `DrawWeapon.started` subscription follows the same OnEnable/OnDisable pattern as Attack/Block; the unsubscribe must be inside the `if (_input == null) return;` guarded block
- `EquipmentVisuals._undrawnWeaponSocket` = `UndrawnWeaponSocket` GO under `mixamorig:Hips` in Player prefab (position left at `(0,0,0)` for manual tuning)
- On initial weapon equip (`EquipmentVisuals.RefreshWeapon()`), socket is chosen by `_isInCombat` — weapon always appears on hip by default

---

## Code Review Checklist — Combat Scripts

| Severity | Pattern |
|----------|---------|
| HIGH | `_activeHitbox` not disabled on all combo-end paths — phantom hits will persist between attacks |
| HIGH | Subscribing to `_onEquipmentChanged` directly in `PlayerCombat` — `ActiveWeaponGO` is null due to GameEventSO reverse-iteration order; use `_onVisualsRefreshed` SO raised at the end of `Refresh()` |
| HIGH | Using a plain C# event across `Game.Inventory` → `Game.Combat` boundary — architecture mandates `GameEventSO<T>` for all cross-system events |
| MEDIUM | `WeaponHitbox` placed on the weapon prefab root instead of the mesh child — root has the kinematic Rigidbody; collider must be on a child so `OnTriggerEnter` resolves the correct GameObject |
| HIGH | Weapon visual prefab missing a kinematic `Rigidbody` on its root — static trigger + static collider = `OnTriggerEnter` never fires (see Prefabs/Enemies/CLAUDE.md) |
| HIGH | `AnimationEventReceiver` function name mismatch — Unity finds receiver methods by exact string match on the same GO as the Animator; typo = silent no-op, not a compile error |
| HIGH | `GetComponentInChildren<WeaponHitbox>()` without `includeInactive: true` — `Drawn` child is inactive when weapon is equipped while sheathed; hitbox will not be found and attacks silently fall back to sphere overlap |
| HIGH | `ScriptableObject.CreateInstance<WeaponSO>()` in new code or tests — `WeaponSO` is abstract (Story 7.10); use a concrete subclass like `SwordSO` |
| MEDIUM | New weapon SO added without a concrete subclass (e.g. inheriting `WeaponSO` directly via `[CreateAssetMenu]`) — `WeaponSO` is abstract and has no `[CreateAssetMenu]`; all new weapon types need a concrete class in `ScriptableObjects/Items/Weapons/` |
| MEDIUM | `OnDrawWeaponStarted` missing `IsAttacking` guard — draw/sheathe during active combo deactivates `Drawn` child mid-swing, silencing the animation-event hit window; always check `if (_stateManager.IsAttacking) return;` before toggling combat state |
| HIGH | `GetNextAnimatorStateInfo(layerIndex).IsTag(...)` used in `SMB_AttackState.OnStateExit` — unreliable: Unity may complete the transition before SMB fires, returning empty `AnimatorStateInfo`; use `_IsComboAttacking` flag pattern instead |
| HIGH | `SMB_AttackState` missing from an attack state in the Animator — no enter/exit guarantee; interrupt path (dodge/stagger/death mid-combo) will leave `IsAttacking = true` and hitbox enabled |
| MEDIUM | `_IsComboAttacking` set without a matching `ExecuteAttack()` path — only `ManageComboStep()` should set this; do not set it in `TryAttack()` or `ResetAttackCombo()` paths |
