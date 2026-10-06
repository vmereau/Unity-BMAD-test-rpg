# CLAUDE.md — Assets/_Game/Scripts/Combat

> Loaded when Claude accesses files in this folder. Covers the WeaponHitbox system and PlayerCombat attack pipeline.

---

## WeaponHitbox System (Story 7.9, reworked by `tech-spec-hit-detection-sweep-rework`)

### Overview

`WeaponHitbox` is an **owner-agnostic, attacker-side swept hitbox** placed on the weapon's `Drawn`
child (or on a hand hitbox GO such as the Player's `UnarmedHitbox`). No trigger callbacks, no
Rigidbody, no layer-collision-matrix dependency.

- **Shape = the Box/Sphere/Capsule collider on the same GO**, kept **permanently disabled** — it is a
  shape definition only (center/size/radius + transform). Any other collider type → `GameLog.Error`
  in `Awake` and the hitbox is a no-op.
- `Enable()` / `Disable()` open/close the hit window (driven by `HitboxEnable`/`HitboxDisable`
  animation events + SMB safety nets — unchanged contract).
- While open, `LateUpdate` (after the Animator posed the hand) samples the shape between last
  frame's pose and this frame's pose — `ceil(travel / _maxStepDistance)` sub-steps, capped by
  `_maxSubSteps` — with `Physics.Overlap*NonAlloc`, explicit `_targetLayers` (empty → CharacterHitbox
  via `GameConstants.CHARACTER_HITBOX_LAYER_NAME`) and `QueryTriggerInteraction.Collide`.
- **Forgiveness (per hitbox, serialized):** `_reachPadding` inflates the query shape; `_verticalReach`
  + `_verticalReachRadius` add a downward world-space capsule from the shape centre so swings catch
  low targets (spider). Tune per weapon prefab — never inflate target hurtboxes instead.
- **Owner:** the controller must call `SetOwner(transform)` when binding. Colliders under `Owner`
  are skipped; the owner's own `IDamageable` is rejected. `WeaponHitbox` has no faction filter — AI
  hits are faction-filtered by `EntityMeleeAttacker` (`Game.AI`), keeping `Game.Combat` free of AI deps.
- `IsWindowOpen` exposes the hit-window state (used by `EntityMeleeAttacker` and tests).
- Targets resolve as `IDamageable` via `GetComponentInParent`; `event Action<IDamageable, Vector3> OnHit`
  (target, approximate hit point) fires **once per target per window**.
- Pure logic (dedupe, owner/dead/null rejection, sub-step count, shape inflation math) lives in
  `HitSweepTracker` (plain C#) — covered by `Tests/EditMode/HitSweepTrackerTests`.

### Hit Resolution

`PlayerCombat.OnWeaponHit(IDamageable target, Vector3 hitPoint)` mirrors `EntityBrain.ExecuteAttack()`:
`target.TryReceiveHit(gameObject)` → `TakeDamage(ComputeEffectiveDamage())` **only on `NotBlocked`**;
other results are logged and deal no damage.

**Shared with AI:** `EntityMeleeAttacker` (`Scripts/AI`, see its CLAUDE.md) owns named `WeaponHitbox`es
on entities, calls `SetOwner`, subscribes `OnHit` and resolves hits with the same
`TryReceiveHit → TakeDamage` sequence (plus a faction filter).

**Event naming:** the player's clips use parameterless `HitboxEnable()` / `HitboxDisable()`
(`AnimationEventReceiver`); entity clips use `HitboxEnable(string id)` / `HitboxDisable(string id)`
(`EntityAnimationEventReceiver`, a separate class — no overload clash). An event with no string
parameter delivers `""` = all hitboxes.

### Hit-window tooling

- `HitWindowEvents` (pure, `Game.Combat`) — frame↔time conversion and add/replace/remove of
  `HitboxEnable/HitboxDisable` pairs by id on `AnimationEvent[]`. **FBX importer events are
  normalized over `firstFrame..lastFrame`; `.anim` events are seconds** — always convert through it.
  Covered by `HitWindowEventsTests`.
- **Hitbox Tuner** (`Tools/Combat/Hitbox Tuner`, `Game.Editor.HitboxTunerWindow`) — scrub a clip on a
  scene/Prefab-Mode entity via `AnimationMode`, see posed hitboxes + swept trail, add `Hitbox_<id>` to
  a bone, write/remove events (FBX via `ModelImporter.clipAnimations`, `.anim` via `AnimationUtility`).
- **Debug sweeps** — `Tools/Combat/Show Hitbox Sweeps` toggles `HitboxDebug.DrawSweeps` (editor-only,
  persisted in EditorPrefs). `WeaponHitbox` records samples in a fixed ring buffer and draws them in
  `OnDrawGizmos` during play mode: yellow samples fading over 1 s, red hits. Compiled out of builds.

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
    {
        _activeHitbox.SetOwner(transform);
        _activeHitbox.OnHit += OnWeaponHit;
    }
    else
        BindUnarmedHitbox(); // weapon visual exists but has no WeaponHitbox — fallback
}

private void BindUnarmedHitbox()
{
    if (_unarmedHitbox == null) { /* warn, _activeHitbox = null */ return; }
    _unarmedHitbox.SetActive(true);
    _activeHitbox = _unarmedHitbox.GetComponent<WeaponHitbox>();
    if (_activeHitbox != null)
    {
        _activeHitbox.SetOwner(transform);
        _activeHitbox.OnHit += OnWeaponHit;
    }
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
int maxSteps = _currentWeaponSO != null ? _currentWeaponSO.ResolvedComboSteps : _config.unarmedComboSteps;
return _comboStep == maxSteps;
```
Unarmed: `CombatConfigSO.unarmedComboSteps` (3 — Jab → Uppercut → Jab via `Attack_1/2/3`). Not
`WeaponSO.DEFAULT_COMBO_STEPS` (2), which stays the fallback for weapons without an archetype. `ManageComboStep()` calls `IncreaseAttackCombo()` only if not at max; if `_comboStep > 1` after increment, `_IsComboAttacking` is set `true` to guard the SMB exit path.

### Animation Events on FBX Clips (Story 7.11)

Events are stored in the FBX `.meta` file under `clipAnimations[].events`. The `time` field is **normalized time** (0.0–1.0). All four events must be on every attack clip.

> **Per-clip timing tables and `.meta` YAML format** → `Assets/_Game/Art/Characters/Humanoids/Animations/Combat/CLAUDE.md`

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

**`OnComboWindowClose()` is ignored while `_IsComboAttacking`** — once the next step is queued, the
outgoing clip can still fire its `ComboWindowClose` during the crossfade (Uppercut's 0.93 lands inside
the 0.63 → 0.95 `Attack_2 → Attack_3` blend). Resetting there zeroed `_comboStep` mid-chain and let a
press in Attack_3's window restart Attack_1 (endless loop instead of a finisher).

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
| HIGH | `WeaponHitbox` bound without `SetOwner(...)` — the owner's own hurtbox (e.g. Player `Hitbox`, layer 7) can be hit; `Enable()` logs a "no owner" warning |
| MEDIUM | `WeaponHitbox` shape collider left enabled, or not Box/Sphere/Capsule, or placed on a child instead of the hitbox GO itself — the shape must be a disabled Box/Sphere/Capsule on the same GO |
| MEDIUM | Low-target misses "fixed" by inflating the target's hurtbox — tune the attacker's `_verticalReach` / `_reachPadding` instead |
| MEDIUM | Weapon hit applied with `TakeDamage` directly — always `TryReceiveHit` first, damage only on `NotBlocked` |
| HIGH | `AnimationEventReceiver` function name mismatch — Unity finds receiver methods by exact string match on the same GO as the Animator; typo = silent no-op, not a compile error |
| HIGH | `GetComponentInChildren<WeaponHitbox>()` without `includeInactive: true` — `Drawn` child is inactive when weapon is equipped while sheathed; hitbox will not be found and attacks silently fall back to the unarmed hitbox |
| HIGH | `ScriptableObject.CreateInstance<WeaponSO>()` in new code or tests — `WeaponSO` is abstract (Story 7.10); use a concrete subclass like `SwordSO` |
| MEDIUM | New weapon SO added without a concrete subclass (e.g. inheriting `WeaponSO` directly via `[CreateAssetMenu]`) — `WeaponSO` is abstract and has no `[CreateAssetMenu]`; all new weapon types need a concrete class in `ScriptableObjects/Items/Weapons/` |
| MEDIUM | `OnDrawWeaponStarted` missing `IsAttacking` guard — draw/sheathe during active combo deactivates `Drawn` child mid-swing, silencing the animation-event hit window; always check `if (_stateManager.IsAttacking) return;` before toggling combat state |
| HIGH | `GetNextAnimatorStateInfo(layerIndex).IsTag(...)` used in `SMB_AttackState.OnStateExit` — unreliable: Unity may complete the transition before SMB fires, returning empty `AnimatorStateInfo`; use `_IsComboAttacking` flag pattern instead |
| HIGH | `SMB_AttackState` missing from an attack state in the Animator — no enter/exit guarantee; interrupt path (dodge/stagger/death mid-combo) will leave `IsAttacking = true` and hitbox enabled |
| MEDIUM | `_IsComboAttacking` set without a matching `ExecuteAttack()` path — only `ManageComboStep()` should set this; do not set it in `TryAttack()` or `ResetAttackCombo()` paths |
