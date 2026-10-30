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
| `WeaponSwap()` | `OnWeaponSwapEvent()` | draw / sheathe clip event (see `CLAUDE.md` → Draw / Sheathe) |
| `NotifyWeaponSwitchExited(bool)` | `OnWeaponSwitchStateExited(bool)` | `SMB_WeaponSwitchState.OnStateExit` |

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
        attack states still active ? return (chain in flight)
                                   : EndAttack() = ResetAttackCombo() + ExitAttack()
  Update: no attack callback / clip event for attackStallTimeout while IsAttacking → forced EndAttack()
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

A press only *requests* a step: the Animator can ignore the trigger (e.g. a press after `Attack_n → CombatIdle`
started at exit time — that blend is uninterruptible, but `ComboWindowClose` fires later). Such a press is
dropped cleanly: the outgoing exit ends the attack and `SetAttacking(false)` clears the latched trigger.

---

## `PlayerAttackTracker` — Attack Lifecycle

Unity fires `OnStateEnter(next)` at transition **start** and `OnStateExit(previous)` at transition **end**,
so the outgoing state's exit would otherwise tear down a chain in flight. `PlayerAttackTracker` (pure,
`Tests/EditMode/PlayerAttackTrackerTests`) follows what the Animator actually plays:

- **Active attack states** — `Enter` +1, `Exit` −1 (clamped at 0). The attack ends (`EndAttack()`) only when
  the count returns to 0 — same model as the AI's `EntityMeleeAttacker`. Never end on input.
- **Queued step** — set by a combo press (`Press(isChainStep: true)`), cleared when the next state enters.
- **Resync** — a fresh Attack_1 press zeroes the count (no attack state can be active then), repairing drift
  from an `OnStateExit` Unity never delivered.
- `OnHitboxEnable()` / `OnComboWindowOpen()` are ignored while `!IsAttacking` — a cancelled / force-ended attack
  still blending out keeps firing clip events.
- `OnComboWindowClose()` is ignored while `ShouldIgnoreWindowClose` (step queued **or** 2 states active): the
  outgoing clip fires its close event during the crossfade (Uppercut's 0.93 inside the `Attack_2 → Attack_3`
  blend). Resetting there zeroed `_comboStep` mid-chain and looped Attack_1 instead of the finisher.
- **Stall watchdog** — every enter/exit/clip event refreshes the activity time; `IsAttacking` with no activity
  for `CombatConfigSO.attackStallTimeout` (1.5 s) force-ends the attack (trigger never taken, lost exit).
- **Cancels — dodge, block, jump / fall** end any melee swing at any moment through `PlayerCombat.CancelAttack()`
  (full reset, stamina not refunded), never `SetAttacking(false)` alone — a leftover `_comboStep` made the next press
  fire `Attack_2/3` from `CombatIdle`, which has no such transition. Dodge: `DodgeController`. Block: `OnBlockStarted`
  (after its stamina check; `CanBlock` / `CanJump` no longer exclude `IsAttacking`). Jump / fall: `Update` cancels
  when `IsAttacking && IsAirborne`. The Attack layer's AnyState block / dodge / jump / fall transitions pull the clip.
- **Full-body attacks** (`WeaponSO.ResolvedFullBodyAttacks`, two-handed): `ExecuteAttack` passes the flag to
  `SetAttacking(true, hash, fullBody)` → `IsFullBodyAttack` → `CanMove()` false (no move, no free-movement turn;
  lock-on still faces the target) and the FullBody layer fades in (`Controllers/CLAUDE.md`). It follows `IsAttacking`,
  so every end path fades it out.
- **Clip events are deduplicated per frame** (`ClipEventDeduper`): the synced FullBody layer replays the Attack clip and
  can fire each event a second time in the same frame. Key = event + clip + event time (handlers take `AnimationEvent`),
  so a crossfade's outgoing / incoming clips still both fire. `EntityAnimationEventReceiver` (NPCs share the controller)
  keys on event + hitbox id. SMB notifications are not deduplicated (the FullBody layer has no SMBs).
- `HandleVisualsRefreshed` cancels any attack: `Refresh()` swaps the controller, which resets the Animator without an
  SMB exit (equip / load mid-swing would otherwise stay rooted until the stall watchdog).
- `PlayerStateManager.SetAttacking(false)` clears the `Attack_1/2/3` triggers (latched triggers auto-chained).
- **Never detect chains with `GetNextAnimatorStateInfo(layer).IsTag(...)` in `OnStateExit`** — the
  transition may already be complete, so it returns an empty state.

---

## Keeping the Hitbox Closed

Every way an attack can end must leave `_activeHitbox` disabled. `ExitAttack()` disables it;
`UnbindWeaponHitbox()` disables it on rebind; `OnAttackStateEntered` / `OnAttackStateExited` (SMB) are the
guaranteed nets for interrupts. Cancels (dodge / block / jump) go through `CancelAttack()`; an expired combo window only
calls `ResetAttackCombo()` and relies on the SMB exit for cleanup. A new end-of-attack path that bypasses both
leaves phantom hits after the animation.

---

## Code Review Checklist — Player Combo

| Severity | Pattern |
|----------|---------|
| HIGH | New attack-ending path that neither reaches `ExitAttack()` nor relies on an `SMB_AttackState` exit — hitbox stays open (phantom hits) |
| HIGH | Player attack state in the Animator without `SMB_AttackState` — no interrupt cleanup; `IsAttacking` and the hitbox stay on |
| HIGH | `AnimationEventReceiver` method renamed / event `functionName` typo — silent no-op |
| HIGH | `GetNextAnimatorStateInfo(...).IsTag(...)` used in `SMB_AttackState.OnStateExit` — use `PlayerAttackTracker`'s state count |
| HIGH | Attack ended on input or by a one-shot flag instead of the tracker's active-state count — a trigger the Animator ignores locks `IsAttacking` |
| HIGH | Code clearing `IsAttacking` without resetting the combo (`SetAttacking(false)` from outside `PlayerCombat`) — use `CancelAttack()` |
| MEDIUM | `OnComboWindowClose()` losing its `ShouldIgnoreWindowClose` early return — finisher loops back to Attack_1 |
| MEDIUM | New attack clip event handler that doesn't call `_attackTracker.Event(...)` — long clips trip the stall watchdog |
