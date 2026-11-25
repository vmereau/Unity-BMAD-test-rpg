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
3. `IsAttacking && !_comboWindowOpen` → **buffered** while an attack state is playing (below), else ignored (prevents
   re-triggering Attack_1 before the window opens).
4. Stamina: not enough → `ResetAttackCombo()`; `Consume()` failing after `HasEnough()` → `ExitAttack()`.

### Combo press buffer (`ComboPressBuffer`, spec-sword-shield-take-combo, every weapon)

A press made while the swing's window is still closed is stored (`_pressBuffer.Store`); `OnComboWindowOpen` consumes
it and calls `TryAttack()` when it is at most `CombatConfigSO.comboPressBufferTime` (0.4 s) old. So an early press counts,
and every buffered chain starts the next state at the window open (the same seam each time). Cleared on every
attack-state enter (a press belongs to the swing it was made in), in `ResetAttackCombo` (window close, end, cancel,
stamina refusal) and by its own consume. Mashing works: each swing buffers its own press. A press **after** the window
opened fires at once, as before. `comboPressBufferTime` 0 = the old drop. Pure, `ComboPressBufferTests`.

A press only *requests* a step: the Animator can ignore the trigger (e.g. a press after `Attack_n → CombatIdle`
started at exit time — that blend is uninterruptible, but `ComboWindowClose` fires later). Such a press is
dropped cleanly: the outgoing exit ends the attack and `SetAttacking(false)` clears the latched trigger.

---

## `PlayerAttackTracker` — Attack Lifecycle

The outgoing state's exit would otherwise tear down a chain in flight (crossfade callback order:
`.claude/rules/attack-pipeline.md` → State callbacks across crossfades). `PlayerAttackTracker` (pure,
`Tests/EditMode/PlayerAttackTrackerTests`) follows what the Animator actually plays:

- **Active attack states** — `Enter` +1, `Exit` −1 (clamped at 0). The attack ends (`EndAttack()`) only when
  the count returns to 0 — same model as the AI's `EntityMeleeAttacker`. Never end on input.
- **Queued step** — set by a combo press (`Press(isChainStep: true)`), cleared when the next state enters.
- **Resync** — a fresh Attack_1 press zeroes the count (no attack state can be active then), repairing drift
  from an `OnStateExit` Unity never delivered.
- `OnHitboxEnable()` / `OnComboWindowOpen()` are ignored while `!IsAttacking` — a cancelled / force-ended attack
  still blending out keeps firing clip events.
- `OnComboWindowClose()` is ignored while `ShouldIgnoreWindowClose` (step queued **or** 2 states active): the
  outgoing clip fires its close event during the crossfade (`hook step`'s 0.70 inside the `Attack_2 → Attack_3`
  blend). Resetting there zeroed `_comboStep` mid-chain and looped Attack_1 instead of the finisher.
- **Stall watchdog** — every enter/exit/clip event refreshes the activity time; `IsAttacking` with no activity
  for `CombatConfigSO.attackStallTimeout` (1.5 s) force-ends the attack (trigger never taken, lost exit).
- **Cancels — dodge, block, jump / fall** end any melee swing at any moment through `PlayerCombat.CancelAttack()`
  (full reset, stamina not refunded), never `SetAttacking(false)` alone — a leftover `_comboStep` made the next press
  fire `Attack_2/3` from `CombatIdle`, which has no such transition. Dodge: `DodgeController`. Block: `OnBlockStarted`
  (after its stamina check; `CanBlock` / `CanJump` don't exclude `IsAttacking`). Jump / fall: `Update` cancels
  when `IsAttacking && IsAirborne`. On the Animator, Base Layer AnyState dodge / jump / fall and each attack state's
  `IsBlocking` exit pull the clip (`Controllers/CLAUDE.md`). The GDD allows these instant cancels pending playtest.
- **Every attack is full-body** (Base Layer, spec-full-body-attacks-root-motion): `CanMove()` is false while
  `IsAttacking` — no move input during a swing. Movement comes from two things:
  - **Attack step** — `PlayerAttackStep` (Player root) moves the `CharacterController` along the player's forward,
    constant speed, during the step window. `OnAttackStateEntered(index)` calls `Begin(ResolveAttackStep(index))`:
    `WeaponArchetypeSO.attackSteps[index − 1]`, else `CombatConfigSO.defaultAttackStep`; unarmed:
    `CombatConfigSO.unarmedAttacks[index − 1].step`. `AttackStep` = distance (m, 0 = rooted) + start / duration in **seconds since
    the state entered** (not normalized). The step stops when `IsAttacking` clears (any cancel) or a dodge starts; a combo
    step's enter replaces the previous step. Weapon clips are in place (Mixamo, net root travel 0) except the one-handed sword's; the sword and unarmed clips keep root XZ unbaked: the player reads that travel (next bullet) and NPCs
    drop it (`Art/.../Animations/Combat/CLAUDE.md` → Unarmed).
  - **Clip root motion (spec-action-root-motion-jab-trial, spec-action-root-motion-unarmed-combo)** — an attack whose
    `AttackStep.rootMotionScale` is > 0 moves by its clip instead: `PlayerCombat.BeginAttackMovement` starts
    `ActionRootMotion` (Player root) and leaves `PlayerAttackStep` idle; a step with scale 0 does the opposite, so each
    attack state has one mover. When `ActionRootMotion.Begin` returns false (component missing or disabled) the scripted
    step runs instead. The whole unarmed combo uses it (`lead jab step`, `hook step`, `uppercut step`, scale 1); weapons
    keep the scripted step. Details: *Action root motion* below.
  - **Wind-up steering** — `PlayerController` turns toward the raw camera-relative input at `PlayerConfigSO.attackSteerSpeed`
    while `CanSteerAttack()` (attacking, not `IsAttackCommitted`). `OnHitboxEnable` commits (`SetAttackCommitted(true)`);
    each attack-state enter re-opens it; `SetAttacking` clears it. Lock-on still always faces the target.
- **Recovery cancel** (spec-attack-recovery-cancel): a real (not ignored) `ComboWindowClose` while `IsAttacking` calls
  `_attackTracker.OpenRecovery()`; every finisher clip has one (`uppercut step` 0.70, `2hand attack 2 refined` 0.80, sword thrust).
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
- `HandleVisualsRefreshed` cancels any attack: `Refresh()` swaps the controller, which resets the Animator without an
  SMB exit (equip / load mid-swing would otherwise stay `IsAttacking`, rooted, until the stall watchdog).
- `PlayerStateManager.SetAttacking(false)` clears the `Attack_1/2/3` triggers (latched triggers auto-chained).
- Chain detection never uses `GetNextAnimatorStateInfo` in `OnStateExit` — `.claude/rules/attack-pipeline.md`.

---

## Unarmed Combo — per-step data (spec-unarmed-combo-rework)

Lead jab (left) → hook (right) → uppercut (left, finisher). `CombatConfigSO.unarmedAttacks` holds one `UnarmedAttack`
per attack state: forward `step`, `hand`, `damageMultiplier`, `stanceMultiplier`. A missing entry = `defaultAttackStep`,
right hand, × 1. `OnValidate` (editor) caps `unarmedComboSteps` at the entry count (skipped for an empty list) and turns
a 0 multiplier — a struct element just added in the Inspector — into 1.

| Step | Clip | Hand | Step (m, start s, duration s) | Damage × | Stance × |
|------|------|------|-------------------------------|----------|----------|
| 1 | `lead jab step` | Left | clip root motion (`rootMotionScale` 1, ≈ 0.40 m); scripted 0.40, 0.07, 0.43 for NPCs | 1 | 1 |
| 2 | `hook step` | Right | clip root motion (scale 1, 0.33–0.40 m in a chain); scripted 0.37, 0.17, 0.50 for NPCs | 1 | 1 |
| 3 | `uppercut step` | Left | clip root motion (scale 1, 0.45–0.52 m in a chain); scripted 0.48, 0.15, 0.50 for NPCs | 1.5 | 2 |

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

---

## Action root motion (`ActionRootMotion`, the unarmed and sword combos)

Clip-driven chains today: the unarmed combo (below) and the one-handed sword (`Archetype_OneHandedSword`, `rootMotionScale`
1 on its three steps: Mixamo sword-and-shield clips, `Art/.../Animations/Combat/CLAUDE.md` → One handed — sword). The
sword's cut / rising cut lunge and step back: a buffered chain fires before the step back (1.7 m over three hits), a
late press after it (1.0 m); the unarmed notes below hold for both.

Hybrid from `docs/IDEAS.md`: locomotion stays code-driven, an action clip carries its own travel. `ActionRootMotion`
implements `OnAnimatorMove` on the Player root (the Animator's GameObject), which makes Unity hand root motion to the
script — `applyRootMotion` stays off in the prefab and nothing moves unless the component is active.

- Active from `Begin(scale)` (attack-state enter) until the attack ends: same stop conditions as `PlayerAttackStep`
  (not attacking, dodging, busy, airborne, controller disabled), or a scripted attack state taking over (`Begin` with scale 0).
- Each frame: `animator.deltaPosition` → `ActionRootMotionMath.Filter` (horizontal only, × scale) → shared ledge probe
  (`PlayerAttackStep.HasGroundAhead`) → `CharacterController.Move`, then a separate 2 cm downward `Move`. The clip's
  rotation is never applied. A failed ledge probe skips that frame's travel only (the scripted step stops for good):
  a clip has small backward / sideways frames, and one of them near an edge must not cancel the lunge after it.
- **`Animator.feetPivotActive` must be 0** while it runs (`Begin` sets it, `Stop` puts the previous value back; default 1, runtime-only). At 1 the root pivots
  around the feet during every blend and adds movement of its own: + 0.07 m sideways and + 0.06 m forward on the
  idle → jab transition, − 0.04 m on the exit blend.
- **Keep the downward `Move` separate.** `isGrounded` reflects the last `Move`, and this one runs after
  `PlayerController`'s gravity move; a horizontal-only move reports "not grounded" for the whole swing, and a
  mixed horizontal + down move slides the capsule down slopes.
- **Attack → attack crossfades keep the travel when both attacks are clip-driven.** `deltaPosition` is the weighted mix
  of both clips, so jab → hook → uppercut and the recovery restart (uppercut → jab) carry on moving through the blend:
  measured, no frame of travel is dropped or doubled (the root speed changes by at most 0.12 m/s between frames). The
  speed follows the clips: at a late press both can be at rest, and the hook's settle back shows as 0.2 m/s backward
  for a few frames. `PlayerCombat.BeginAttackMovement` calls `Begin` for **every** attack state (scale 0 = scripted), so
  the component knows whether the attack entered before this one was clip-driven (`_lastAttackClipDriven`, kept across
  `Stop()`: a cancelled swing still blends out when a fresh press arrives — measured, the new jab keeps its 0.40 m);
  `ActionRootMotionMath.AppliesDuringBlend` then applies the mix. When
  the outgoing attack was moved by the scripted step (a weapon clip, or a clip with unbaked root XZ whose lunge is
  dropped on purpose), the mix carries travel that was never applied: the blend is waited out, as before.
- **Every step of a clip-driven chain must be clip-driven** (`UnarmedAttackTests.Config_EveryStepMovesThePlayerByItsClip`):
  a scripted step in the middle stops the component at its enter and the next crossfade is waited out.
- **A crossfade trades travel, it does not add it.** The outgoing clip's remaining travel fades out while the incoming
  clip's first 0.42 s fades in, so a chain covers less than the sum of its clips (three hits: 1.21–1.24 m against
  0.40 + 0.42 + 0.60 m of clip travel) and each step's share depends on the press time. Start a clip's lunge late in the
  blend when its distance matters (`uppercut step` moves from 0.13 s; starting at 0 s lost half of it) and tune
  `travel_m` against the travel measured in a chain.
- Locomotion, idle and dodge clips are XZ-baked (0). Not covered: `Landing` (unbaked) still blending out when the jab
  starts.
- **Unity moves the root by the centre of mass, not the hips.** A punch that leans sideways moves the capsule sideways
  and back (hook: 6 cm) while the feet stay planted; chaining cuts the return. Straighten it in the fit (`travel_yaw_deg`),
  never by filtering the delta in code: the pose is root-relative, so a dropped delta becomes foot slide.
- Measured in play (single jab, chains at the earliest / middle / latest press, recovery restart, half speed): numbers
  in `spec-action-root-motion-unarmed-combo` → Implementation Notes. Re-measure with
  `Tools/Blender/action_root_motion_play_measure.cs` + `action_root_motion_analyse.py`.
- NPCs do not have the component: they play the same clips with the scripted step (`EntityAttackStep`), so each
  `unarmedAttacks[i].step` stays close to the travel the player gets from that clip **in a chain** (measured: their
  path stays within 7 cm of the player's).
- A new root-motion clip: travel straight ahead in the clip, root XZ **not** baked, rotation and Y baked on Original
  (`Art/.../Animations/Combat/CLAUDE.md` → Unarmed, `Tools/Blender/README.md` → *Meshy motion fit*).

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
| MEDIUM | Attack given a `rootMotionScale` while its clip is in place or root-baked (XZ) — the player no longer moves on that swing |
| MEDIUM | One step of a clip-driven combo given `rootMotionScale` 0 (or the reverse) — the travel stops at that crossfade |
| MEDIUM | `ActionRootMotion` moving with one combined horizontal + downward `Move`, or with `feetPivotActive` left at 1 — slope slide / extra travel in blends |
| MEDIUM | Unarmed step changes hand in `CombatConfigSO.unarmedAttacks` but the clip's event id (`Unarmed` / `UnarmedLeft`) isn't updated — NPCs punch with the wrong hand |
