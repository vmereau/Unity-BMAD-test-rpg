# CLAUDE.md — Assets/_Game/Scripts/Combat

> Loaded when Claude accesses files in this folder. Covers `WeaponHitbox`, hit resolution, how
> `PlayerCombat` binds its hitbox, draw/sheathe and blocking. Namespace: `Game.Combat`.

## Topic files

| File | Read when |
|------|-----------|
| `PLAYER_COMBO.md` | Touching `PlayerCombat`'s attack/combo flow, `AnimationEventReceiver` or `SMB_AttackState` |
| `BOW.md` | Touching the bow: `BowSO`, aim / nock / fire in `PlayerCombat`, `SMB_BowAimState`, `BowAimCorrection`, spread (`BowSpreadMath`), `ArrowProjectile` / `ArrowFlightMath` |
| `EXECUTE.md` | Touching `PlayerExecution`, `IExecutable`, `ExecuteMath` or the `ExecuteImpact` / `ExecuteEnd` clip events |

---

## WeaponHitbox — Attacker-Side Swept Hitbox

`WeaponHitbox` is **owner-agnostic**: it sits on a weapon's `Drawn` child, on the Player's
`UnarmedHitbox`, or on an entity's `Hitbox_<id>`. No trigger callbacks, no Rigidbody, no
layer-collision-matrix dependency.

- **Shape = the Box/Sphere/Capsule collider on the same GO**, kept **permanently disabled** (shape
  definition only; `Awake` also disables it). Any other collider type → `GameLog.Error` in `Awake`, hitbox is a no-op.
- `Enable()` / `Disable()` open/close the hit window (driven by animation events + SMB safety nets).
  `IsWindowOpen` exposes it.
- While open, `LateUpdate` sweeps the shape from last frame's pose to this one —
  `ceil(travel / _maxStepDistance)` sub-steps, capped by `_maxSubSteps` — with `Physics.Overlap*NonAlloc`,
  explicit `_targetLayers` (empty → CharacterHitbox via `GameConstants.CHARACTER_HITBOX_LAYER_NAME`) and
  `QueryTriggerInteraction.Collide`.
- **Forgiveness (per hitbox):** `_reachPadding` inflates the shape; `_verticalReach` + `_verticalReachRadius`
  add a downward world-space capsule for low targets (spider). Tune per weapon/hitbox — **never inflate
  target hurtboxes instead**.
- **Owner:** the binder must call `SetOwner(transform)`. Colliders under the owner are skipped and the
  owner's own `IDamageable` is rejected. No faction filter here — AI hits are faction-filtered by
  `EntityMeleeAttacker` (`Game.AI`), keeping `Game.Combat` free of AI deps.
- Targets resolve via `GetComponentInParent<IDamageable>()`; `event Action<IDamageable, Vector3> OnHit`
  fires **once per target per window**.
- `[DefaultExecutionOrder(300)]`: the sweep runs after every bone / weapon pose writer (`BowAimCorrection` 200,
  `TwoHandGripConstraint` 250) — at the default order it would sweep last frame's grip on this frame's hands.
- Pure logic (dedupe, owner/dead/null rejection, sub-step count, inflation math) is in `HitSweepTracker`
  — covered by `Tests/EditMode/HitSweepTrackerTests`.

**Target side (hurtbox contract)** — consequences of the explicit-mask sweep for every hittable prefab:

- The hurtbox is a **trigger** collider on a child GO, Layer 7 **CharacterHitbox** — trigger so it
  never blocks movement or ragdolls.
- **No Rigidbody needed** on the entity or the weapon; the layer collision matrix is irrelevant
  (explicit-mask queries ignore it).
- **Size the hurtbox to the real body.** Low targets are reached by the attacker's `_verticalReach`
  (downward reach capsule), never by inflating the target's hurtbox.
- The target is resolved with `GetComponentInParent<IDamageable>()` from the hurtbox collider —
  `EntityHealth` (an `IDamageable`) must be on the root or any ancestor of the hurtbox. A sibling or
  unrelated GO → hits silently ignored.

### Hit resolution

Every hitbox owner resolves hits the same way: `target.TryReceiveHit(attacker)` → `TakeDamage(amount, attacker)`
**only on `NotBlocked`** (the attacker overload lets the victim choose knockout vs death — `Scripts/AI/KNOCKOUT.md` → Unconscious); other results deal no damage. Player: `PlayerCombat.OnWeaponHit`. AI:
`EntityMeleeAttacker` (see `Scripts/AI/COMBAT.md`).

**Stance** (player → AI; AI → player / NPC is in `Scripts/AI/COMBAT.md` → Stance): after `TakeDamage`, a live `IStanceTarget` gets `ApplyStanceDamage(amount, canKnockDown)`.
Melee = `StanceMath.ComputeMeleeStanceDamage`: weapon `ResolvedStanceDamage` (archetype `stanceDamage`, or the weapon's
own value when set; unarmed `CombatConfigSO.unarmedStanceDamage`) + Strength × `stancePerStrength`, × `finisherStanceMultiplier` on
`IsMaxCombo()` (unarmed: × the step's `UnarmedAttack.stanceMultiplier` instead — `PLAYER_COMBO.md`). `canKnockDown` = `WeaponSO.ResolvedCanKnockDown` (archetype flag, weapon
`KnockDownOverride`); unarmed never. Arrows snapshot the bow's resolved values at fire time (no Strength / finisher).
Archetypes: sword 25 / no KD, axe 35 / KD, two-handed 45 / KD, bow 10 / no KD. Rules and AI side: `Scripts/AI/COMBAT.md` → Stance.

**Melee stamina:** a swing costs `CombatConfigSO.attackStaminaCost` × `WeaponSO.ResolvedAttackStaminaMultiplier`
(`PlayerCombat.MeleeAttackStaminaCost`; unarmed × 1; two-handed × 2).

**Health damage (spec-stat-scaling-balance, tiers + worked examples in `_bmad-output/balance.md`)** — all through the pure
`DamageMath` (`DamageMathTests`):
- Raw player hit = `DamageMath.WeaponRaw(WeaponSO.damageBonus, stat)` where stat = the weapon's `scalingStat` (swords /
  axes STR, bows DEX), 1:1. Fists are a weapon: (`CombatConfigSO.unarmedDamage` 10 + STR) × the step's
  `UnarmedAttack.damageMultiplier`. × `sneakAttackDamageMultiplier` on an unaware target. AI raw = `Entity.AttackDamage` + wielded weapon `damageBonus` (no stat).
- Taken = `DamageMath.Mitigate(raw, armor, CombatConfigSO.minDamageFraction)` = `max(raw − armor, raw × 10 %)`, min 1, applied
  **in the victim's `TakeDamage`** (`PlayerHealth`: `PlayerStats.Defense`; `EntityHealth`: `Entity.Armor`) — callers pass raw
  damage, never pre-mitigate. New content: pick the tier first, then stay inside its band.

### Hit-window tooling

- `HitWindowEvents` (pure) — frame↔time conversion and add/replace/remove of `HitboxEnable/HitboxDisable`
  pairs by id. **FBX importer events are normalized over `firstFrame..lastFrame`; `.anim` events are
  seconds** — always convert through it. Covered by `HitWindowEventsTests`.
- **Hitbox Tuner** (`Tools/Combat/Hitbox Tuner`, `Assets/_Game/Editor/HitboxTunerWindow.cs`) — scrub a
  clip on a scene / Prefab-Mode entity, see posed hitboxes + swept trail, add `Hitbox_<id>` to a bone,
  write/remove events.
- **Weapon Pose Tuner** (`Tools/Combat/Weapon Pose Tuner`) — archetype drawn / sheathed poses and weapon grips:
  `Prefabs/Items/Weapons/WEAPON_TOOLS.md` (incl. the `AnimationMode` bone-revert gotcha).
- **Debug sweeps** — `Tools/Combat/Show Hitbox Sweeps` toggles `HitboxDebug.DrawSweeps` (editor-only,
  EditorPrefs). Play-mode gizmos: yellow samples fading over 1 s, red hits. Compiled out of builds.

---

## PlayerCombat Hitbox Binding — `OnVisualsRefreshed`

`PlayerCombat` (re)binds its hitbox when the `GameEventSO_Void _onVisualsRefreshed` channel
(`Data/Events/OnVisualsRefreshed.asset`) fires. `EquipmentVisuals` raises it at the **very end** of
`Refresh()`, after `_weaponVisual` is assigned, so `ActiveWeaponGO` is valid in the handler.

- Handler = `UnbindWeaponHitbox()` then `BindWeaponHitbox()`: cache the equipped `WeaponSO`, find
  `WeaponHitbox` under `ActiveWeaponGO` with **`GetComponentInChildren<WeaponHitbox>(true)`** (`Drawn` is
  inactive while sheathed), `SetOwner` + subscribe `OnHit`. No weapon visual, or a visual without a
  hitbox → `BindUnarmedHitbox()` (activates and binds **both** hands, `_unarmedHitbox` + `_unarmedHitboxLeft`; the hand of
  each step is picked in `OnAttackStateEntered` — `PLAYER_COMBO.md` → Unarmed Combo).
- `OnEnable` also calls `BindUnarmedHitbox()` directly — a startup safety net, because listener order is not
  guaranteed (`Scripts/Inventory/CLAUDE.md` → Rules) and `EquipmentVisuals.OnEnable` may raise before `PlayerCombat`
  subscribed.
- **Why not `_onEquipmentChanged`?** `PlayerCombat` would receive it before `EquipmentVisuals.Refresh()`
  ran → `ActiveWeaponGO` still stale/null.
- **Why not a C# event?** Cross-system (`Game.Inventory` → `Game.Combat`) communication must use
  `GameEventSO<T>` channels (project-context.md).

---

## Draw / Sheathe (R — `DrawWeapon` action)

Spec: `spec-player-draw-sheathe-animations.md`. `PlayerCombat.OnDrawWeaponStarted` flips
`PlayerStateManager.SetInCombat(...)` (state + animator bool) **immediately** and calls `BeginWeaponSwitch(draw, speed)`
(`IsSwitchingWeapon` + `DrawWeapon` / `SheatheWeapon` trigger, `WeaponSwitchSpeed` = `CombatConfigSO.weaponSwitchAnimSpeed`).
The weapon visual moves (`EquipmentVisuals.SetCombatState(IsInCombat)`) only in `CompleteWeaponSwitch()`, reached from:

| Path | Why |
|------|-----|
| `WeaponSwap` clip event → `AnimationEventReceiver.WeaponSwap` | timing (hand at the hilt / sheath) |
| `SMB_WeaponSwitchState.OnStateExit` → `OnWeaponSwitchStateExited(drawState)` | interrupt net (dodge / jump before the event) — **only if `drawState == _pendingDraw`**, or the previous state blending out after a quick re-press finishes the new switch early |
| `Update`: `IsDodging` / `IsAirborne` while switching | a dodge / jump on the same frame as R wins the AnyState order — `Draw_State` is never entered, so no exit net |
| `Update` watchdog (`weaponSwitchTimeout / weaponSwitchAnimSpeed`) | trigger never taken / controller swapped mid-clip |
| `HandleVisualsRefreshed`, `OnDisable` | equip change mid-clip; no watchdog while disabled |

`HandleVisualsRefreshed` also re-pushes `SetInCombat(IsInCombat)`: `Refresh()` reassigns `runtimeAnimatorController`,
which resets **every** animator parameter (a drawn player would sit in `LockOn Locomotion` and latch `Attack_1`).

`CompleteWeaponSwitch` no-ops unless `IsSwitchingWeapon`. Unarmed runs the same flow (base-controller clips `Draw unarmed` / `sheath unarmed`, no visual).
`SetDead(true)` keeps `IsSwitchingWeapon` (IsBusy blocks R; watchdog / load fix the visual). `PlayerSaveAdapter` forces the
sheathed stance when `IsInCombat || IsSwitchingWeapon`.

- R is gated by `CanSwitchWeapon()`: not busy, attacking, blocking, dodging, airborne, or already switching — block
  would pull the UpperBody layer out of the clip, and dodge / jump / fall play full-body (UpperBody weight 0, the
  draw / sheathe clip hidden). `CanAttack()` / `CanBlock()` also require `!IsSwitchingWeapon`.
- `CanAttack()` / `CanBlock()` require `IsInCombat` (gated in `PlayerStateManager`, see
  `Scripts/Player/CLAUDE.md`); `CanDodge()` does not. `IsInCombat` starts `false` (sheathed).
- The `DrawWeapon.started` unsubscribe goes inside the `if (_input == null) return;` guarded block of
  `OnDisable`, like Attack/Block.
- Socket and `Drawn`/`Sheathed` conventions: `Prefabs/Items/Weapons/CLAUDE.md`.

---

## Blocking (spec-shield-offhand-blocking, spec-unshielded-blocking)

Right click blocks in every melee mode — fists, one-hander, two-hander; a bow aims instead. Values come from the pure
`PlayerCombat.ResolveBlockValues(shield, config)` (`PlayerBlockValuesTests`): a shield **in hand**
(`EquipmentVisuals.ShieldInHand` = a `ShieldSO` in the Offhand slot and a drawn weapon visual — fists never draw it) gives
`ShieldSO.blockStaminaCost` / `perfectBlockWindow`; no shield gives `CombatConfigSO.unshieldedBlockStaminaCost` (25) /
`unshieldedPerfectBlockWindow` (0.2 s). Shield (or null) and the cost are snapshotted at the raise (`_blockShield`,
`_blockStaminaCost`). `BlockSourceChanged()` (held shield ≠ snapshot, or a bow equipped) lowers a raised block in
`HandleVisualsRefreshed` and fails `TryReceiveHit` closed — the next press snapshots the new values. Every lower goes through
`LowerBlock()`. Shields: Round 20 stamina / 0.35 s / +3 armor, Kite 12 / 0.2 s / +6. Clips (`Block_State` + overrides):
fists `Unarmed Block Guard`, two-hander `2hand blocking`, sword (shield or not) `Block Idle` (no shield clip yet —
`deferred-work.md`). NPC side: `Scripts/AI/COMBAT.md` → Blocking.

---

## Ranged — Bow

Aim, nock, shot, `BowAimCorrection`, spread and `ArrowProjectile`: `BOW.md`.

---

## Execute stomp (F)

`PlayerExecution` finishing off an unconscious NPC: `EXECUTE.md`.

---

## Code Review Checklist — Combat Scripts

| Severity | Pattern |
|----------|---------|
| HIGH | `WeaponHitbox` bound without `SetOwner(...)` — the owner's own hurtbox (Player `Hitbox`, layer 7) can be hit; `Enable()` logs a "no owner" warning |
| HIGH | `GetComponentInChildren<WeaponHitbox>()` without `includeInactive: true` — sheathed `Drawn` is inactive; attacks silently fall back to the unarmed hitbox |
| HIGH | `PlayerCombat` subscribing to `_onEquipmentChanged` for hitbox binding — use `_onVisualsRefreshed` (raised at the end of `Refresh()`) |
| HIGH | Plain C# event across the `Game.Inventory` → `Game.Combat` boundary — use `GameEventSO<T>` |
| MEDIUM | Hitbox shape collider left enabled, not Box/Sphere/Capsule, or on a child instead of the hitbox GO itself |
| MEDIUM | Low-target misses "fixed" by inflating the target's hurtbox — tune the attacker's `_verticalReach` / `_reachPadding` |
| MEDIUM | Weapon hit applied with `TakeDamage` directly — always `TryReceiveHit` first, damage only on `NotBlocked` |
| MEDIUM | `OnDrawWeaponStarted` not gated by `CanSwitchWeapon()` (attacking / blocking / airborne / switching) |
| MEDIUM | Code moving the weapon visual on R directly instead of through `CompleteWeaponSwitch` — breaks the event timing and the switch lock |

Combo/animation-event checklist rows: `PLAYER_COMBO.md`. `WeaponSO` subclassing rules:
`ScriptableObjects/Items/CLAUDE.md`.
