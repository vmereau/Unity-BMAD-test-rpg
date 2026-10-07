# PLAYER_COMBO.md — PlayerCombat attack & combo pipeline

> Read before changing `PlayerCombat`'s attack/combo code, `AnimationEventReceiver`, `SMB_AttackState`,
> or the player attack states/clips. Per-clip event timings: `Art/Characters/Humanoids/Animations/Combat/CLAUDE.md`.

---

## Routing

`AnimationEventReceiver` sits on the **Player root GO** (same GO as the `Animator` — Unity only delivers
clip events there) and forwards to `PlayerCombat`:

| `AnimationEventReceiver` | → `PlayerCombat` | Fired by |
|---|---|---|
| `ComboWindowOpen()` | `OnComboWindowOpen()` | clip event |
| `ComboWindowClose()` | `OnComboWindowClose()` | clip event |
| `HitboxEnable()` | `OnHitboxEnable()` | clip event |
| `HitboxDisable()` | `OnHitboxDisable()` | clip event |
| `NotifyAttackEntered(int)` | `OnAttackStateEntered(int)` | `SMB_AttackState.OnStateEnter` |
| `NotifyAttackExited()` | `OnAttackStateExited()` | `SMB_AttackState.OnStateExit` |

Clip events handle **timing**; the SMB callbacks are **guaranteed** (they fire even when the clip is
interrupted before its events ran). Unity matches event receivers by exact method name — a typo compiles
and silently does nothing.

---

## Timeline

```
Attack input → TryAttack() → ExecuteAttack() (SetAttacking + animator trigger) → ManageComboStep()
  → SMB OnStateEnter → OnAttackStateEntered()   hitbox Disable() (safety) + _comboWindowOpen = false
  → HitboxEnable  event → _activeHitbox.Enable()
  → HitboxDisable event → _activeHitbox.Disable()
  → ComboWindowOpen event → next press accepted
  → SMB OnStateExit → OnAttackStateExited()
        _IsComboAttacking ? consume flag, return (chain in flight)
                          : ResetAttackCombo() + ExitAttack()   (interrupt / finisher cleanup)
```

`ExecuteAttack()` never enables the hitbox itself — the hit window is entirely event/SMB-driven.

---

## Gates in `TryAttack()`

1. `PlayerStateManager.CanAttack()` (requires `IsInCombat`).
2. `IsMaxCombo()` — max steps = `_currentWeaponSO.ResolvedComboSteps`, or `CombatConfigSO.unarmedComboSteps`
   (3: Jab → Uppercut → Jab) when unarmed. Not `WeaponSO.DEFAULT_COMBO_STEPS` (2), which is only the
   fallback for weapons without an archetype.
3. `IsAttacking && !_comboWindowOpen` → ignore (prevents re-triggering Attack_1 before the window opens).
4. Stamina: not enough → `ResetAttackCombo()`; `Consume()` failing after `HasEnough()` → `ExitAttack()`.

---

## `_IsComboAttacking` — Combo Chain Guard

Unity fires `OnStateEnter(next)` at transition **start** and `OnStateExit(previous)` at transition **end**,
so the outgoing state's exit would otherwise tear down a chain in flight.

- Set `true` only in `ManageComboStep()`, when `_comboStep > 1` after incrementing.
- Consumed (set `false`) by `OnAttackStateExited()`, which then returns early — once per transition.
- First-hit interrupts (dodge/stagger/death) never set it → full cleanup runs.
- `OnComboWindowClose()` is **ignored while `_IsComboAttacking`**: the outgoing clip can fire its close
  event during the crossfade (Uppercut's 0.93 lands inside the 0.63 → 0.95 `Attack_2 → Attack_3` blend).
  Resetting there zeroed `_comboStep` mid-chain and looped Attack_1 instead of the finisher.
- **Never detect chains with `GetNextAnimatorStateInfo(layer).IsTag(...)` in `OnStateExit`** — the
  transition may already be complete, so it returns an empty state.

---

## Keeping the Hitbox Closed

Every way an attack can end must leave `_activeHitbox` disabled. `ExitAttack()` disables it;
`UnbindWeaponHitbox()` disables it on rebind; `OnAttackStateEntered` / `OnAttackStateExited` (SMB) are the
guaranteed nets for interrupts. Block (`OnBlockStarted`) and an expired combo window only call
`ResetAttackCombo()` and rely on the SMB exit for cleanup. A new end-of-attack path that bypasses both
leaves phantom hits after the animation.

---

## Code Review Checklist — Player Combo

| Severity | Pattern |
|----------|---------|
| HIGH | New attack-ending path that neither reaches `ExitAttack()` nor relies on an `SMB_AttackState` exit — hitbox stays open (phantom hits) |
| HIGH | Player attack state in the Animator without `SMB_AttackState` — no interrupt cleanup; `IsAttacking` and the hitbox stay on |
| HIGH | `AnimationEventReceiver` method renamed / event `functionName` typo — silent no-op |
| HIGH | `GetNextAnimatorStateInfo(...).IsTag(...)` used in `SMB_AttackState.OnStateExit` — use the `_IsComboAttacking` flag |
| MEDIUM | `_IsComboAttacking` set anywhere but `ManageComboStep()` |
| MEDIUM | `OnComboWindowClose()` losing its `_IsComboAttacking` early return — finisher loops back to Attack_1 |
