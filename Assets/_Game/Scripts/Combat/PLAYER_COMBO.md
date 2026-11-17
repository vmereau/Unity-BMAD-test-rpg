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
   (3: lead jab → hook → uppercut finisher) when unarmed. Not `WeaponSO.DEFAULT_COMBO_STEPS` (2), which is only the
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
  outgoing clip fires its close event during the crossfade (Hook's 0.76 inside the `Attack_2 → Attack_3`
  blend). Resetting there zeroed `_comboStep` mid-chain and looped Attack_1 instead of the finisher.
- **Stall watchdog** — every enter/exit/clip event refreshes the activity time; `IsAttacking` with no activity
  for `CombatConfigSO.attackStallTimeout` (1.5 s) force-ends the attack (trigger never taken, lost exit).
- **Cancels — dodge, block, jump / fall** end any melee swing at any moment through `PlayerCombat.CancelAttack()`
  (full reset, stamina not refunded), never `SetAttacking(false)` alone — a leftover `_comboStep` made the next press
  fire `Attack_2/3` from `CombatIdle`, which has no such transition. Dodge: `DodgeController`. Block: `OnBlockStarted`
  (after its stamina check; `CanBlock` / `CanJump` no longer exclude `IsAttacking`). Jump / fall: `Update` cancels
  when `IsAttacking && IsAirborne`. On the Animator, Base Layer AnyState dodge / jump / fall and each attack state's
  `IsBlocking` exit pull the clip (`Controllers/CLAUDE.md`). The GDD allows these instant cancels pending playtest.
- **Every attack is full-body** (Base Layer, spec-full-body-attacks-root-motion): `CanMove()` is false while
  `IsAttacking` — no move input during a swing. Movement comes from two things:
  - **Attack step** — `PlayerAttackStep` (Player root) moves the `CharacterController` along the player's forward,
    constant speed, during the step window. `OnAttackStateEntered(index)` calls `Begin(ResolveAttackStep(index))`:
    `WeaponArchetypeSO.attackSteps[index − 1]`, else `CombatConfigSO.defaultAttackStep`; unarmed:
    `CombatConfigSO.unarmedAttacks[index − 1].step`. `AttackStep` = distance (m, 0 = rooted) + start / duration in **seconds since
    the state entered** (not normalized). The step stops when `IsAttacking` clears (any cancel) or a dodge starts; a combo
    step's enter replaces the previous step. Weapon clips are in place (Mixamo, net root travel 0); the unarmed clips keep root XZ unbaked so their lunge is
    dropped (`Art/.../Animations/Combat/CLAUDE.md` → Unarmed) — no `OnAnimatorMove`.
  - **Wind-up steering** — `PlayerController` turns toward the raw camera-relative input at `PlayerConfigSO.attackSteerSpeed`
    while `CanSteerAttack()` (attacking, not `IsAttackCommitted`). `OnHitboxEnable` commits (`SetAttackCommitted(true)`);
    each attack-state enter re-opens it; `SetAttacking` clears it. Lock-on still always faces the target.
- **Recovery cancel** (spec-attack-recovery-cancel): a real (not ignored) `ComboWindowClose` while `IsAttacking` calls
  `_attackTracker.OpenRecovery()`; every finisher clip has one (Uppercut 0.70, `2hand attack 2 refined` 0.80, sword thrust).
  `IsRecoveryOpen` is cleared by any press, attack-state enter and `Reset`. While open:
  - **Move** (`Player/Move` ≥ `CombatConfigSO.recoveryCancelMoveThreshold`, `UpdateRecoveryMoveCancel`) →
    `SetAttackCancel(true)` + `CancelAttack()`: move input this frame, Base `Attack_N → LockOn Locomotion` (0.2 s).
  - **Attack press** (after the stamina gate) → `CancelAttack()`, step-1 `ExecuteAttack()`, `Press(isChainStep: true)` (the
    outgoing state is still active — a resync to 0 would let its exit end the new swing), then `SetAttackCancel(true)` (after
    `SetAttacking(true)`, which clears it) → Base `Attack_N → Attack_1_State`. `OnAttackStateEntered` clears `AttackCancel`
    (before its stale-enter guard). If Base is already blending out at exit time (`PlayerStateManager.IsAttackBlendingOut`),
    Attack_N's own transitions no longer run: the press `CancelAttack()`s and takes the fresh path instead — AnyState →
    `Attack_1` (gated on `AttackCancel` false) interrupts the blend-out.
  - **Fresh press while a cancelled swing still blends out** (move / block / dodge): `Press(isChainStep: ActiveStates > 0)` —
    the outgoing exit is still to come, and a resync to 0 would let it end the new attack.
  - `AttackCancel` is a **bool**, not a trigger: a trigger set during an exit-time blend would latch and cut the next swing.
  - Verified in play (2026-11-17, stepped 50 fps): jab move-cancel at its 1.33 s close (Animator out in 0.24 s); restart
    from Hook (0.15 s transition) and from the jab itself (self-transition) chains on normally; a press mid exit blend and a
    fresh press 0.1 s after a move-cancel both start a full jab (hit window opens); Uppercut recovery 0.93 s after its enter.
- `HandleVisualsRefreshed` cancels any attack: `Refresh()` swaps the controller, which resets the Animator without an
  SMB exit (equip / load mid-swing would otherwise stay `IsAttacking`, rooted, until the stall watchdog).
- `PlayerStateManager.SetAttacking(false)` clears the `Attack_1/2/3` triggers (latched triggers auto-chained).
- **Never detect chains with `GetNextAnimatorStateInfo(layer).IsTag(...)` in `OnStateExit`** — the
  transition may already be complete, so it returns an empty state.

---

## Unarmed Combo — per-step data (spec-unarmed-combo-rework)

Lead jab (left) → hook (right) → uppercut (left, finisher). `CombatConfigSO.unarmedAttacks` holds one `UnarmedAttack`
per attack state: forward `step`, `hand`, `damageMultiplier`, `stanceMultiplier`. A missing entry = `defaultAttackStep`,
right hand, × 1. `OnValidate` (editor) caps `unarmedComboSteps` at the entry count (skipped for an empty list) and turns
a 0 multiplier — a struct element just added in the Inspector — into 1.

| Step | Clip | Hand | Step (m, start s, duration s) | Damage × | Stance × |
|------|------|------|-------------------------------|----------|----------|
| 1 | `lead jab` | Left | 0.30, 0.07, 0.43 | 1 | 1 |
| 2 | `Hook` | Right | 0.35, 0.27, 0.37 | 1 | 1 |
| 3 | `Uppercut` | Left | 0.50, 0.10, 0.37 | 1.5 | 2 |

- **Hand:** `Player.prefab` has `UnarmedHitbox` (right, `_unarmedHitbox`) and `UnarmedHitbox_L` (left,
  `_unarmedHitboxLeft`). Unarmed binds both (owner + `OnHit`). `OnAttackStateEntered` closes the window, then
  `SelectUnarmedHand` points `_activeHitbox` at the step's hand, so `HitboxEnable` opens only that one. Every close path
  goes through `CloseHitWindows()`, which closes both hands. A missing hand hitbox warns once and uses the other.
- **Multipliers follow the attack state the Animator entered** (`_currentAttackIndex`), not `_comboStep`, which advances on
  the press. Health damage = `ComputeEffectiveDamage()` × `damageMultiplier`. Stance uses the step's `stanceMultiplier`
  **instead of** the global `finisherStanceMultiplier` (weapons still use the global one on `IsMaxCombo()`).
- "Unarmed" = no `WeaponSO` equipped, read from `EquipmentSystem` (`IsUnarmed()`), not `_currentWeaponSO`.
- The clips' hit events also carry a string id (`UnarmedLeft` / `Unarmed`) for NPCs. The player's receiver is
  parameterless and ignores it; **the player's hand comes from the config**. Keep both in sync when a step changes hand.
- Verified in play (2026-11-16, stepped at 60 fps, logged a frame or two after each event): left window 0.36–0.60 s
  after the Attack_1 enter (events at 0.33 / 0.57 s), then the right hand only in Attack_2 and the left only in Attack_3,
  the other hand never; a full chain travels 1.15 m and resets to step 1.

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
| MEDIUM | New melee attack clip (finisher included) without a `ComboWindowClose` event — that step never opens its recovery, the player stays rooted to the clip end |
| MEDIUM | Recovery restart pressed with `isChainStep: false` — the outgoing state's exit ends the restarted attack |
| MEDIUM | New attack clip event handler that doesn't call `_attackTracker.Event(...)` — long clips trip the stall watchdog |
| MEDIUM | Hit window closed with `_activeHitbox.Disable()` instead of `CloseHitWindows()` — unarmed has two hands bound |
| MEDIUM | Unarmed step changes hand in `CombatConfigSO.unarmedAttacks` but the clip's event id (`Unarmed` / `UnarmedLeft`) isn't updated — NPCs punch with the wrong hand |
