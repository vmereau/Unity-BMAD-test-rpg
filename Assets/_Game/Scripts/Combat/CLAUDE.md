# CLAUDE.md — Assets/_Game/Scripts/Combat

> Loaded when Claude accesses files in this folder. Covers `WeaponHitbox`, hit resolution, how
> `PlayerCombat` binds its hitbox, and draw/sheathe. Namespace: `Game.Combat`.
>
> **Touching `PlayerCombat`'s attack/combo flow, `AnimationEventReceiver` or `SMB_AttackState`?**
> Read `PLAYER_COMBO.md` in this folder first.

---

## WeaponHitbox — Attacker-Side Swept Hitbox

`WeaponHitbox` is **owner-agnostic**: it sits on a weapon's `Drawn` child, on the Player's
`UnarmedHitbox`, or on an entity's `Hitbox_<id>`. No trigger callbacks, no Rigidbody, no
layer-collision-matrix dependency.

- **Shape = the Box/Sphere/Capsule collider on the same GO**, kept **permanently disabled** (shape
  definition only). Any other collider type → `GameLog.Error` in `Awake`, hitbox is a no-op.
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
- Pure logic (dedupe, owner/dead/null rejection, sub-step count, inflation math) is in `HitSweepTracker`
  — covered by `Tests/EditMode/HitSweepTrackerTests`.

### Hit resolution

Every hitbox owner resolves hits the same way: `target.TryReceiveHit(attacker)` → `TakeDamage(...)`
**only on `NotBlocked`**; other results deal no damage. Player: `PlayerCombat.OnWeaponHit`. AI:
`EntityMeleeAttacker` (see `Scripts/AI/CLAUDE.md`).

### Hit-window tooling

- `HitWindowEvents` (pure) — frame↔time conversion and add/replace/remove of `HitboxEnable/HitboxDisable`
  pairs by id. **FBX importer events are normalized over `firstFrame..lastFrame`; `.anim` events are
  seconds** — always convert through it. Covered by `HitWindowEventsTests`.
- **Hitbox Tuner** (`Tools/Combat/Hitbox Tuner`, `Assets/_Game/Editor/HitboxTunerWindow.cs`) — scrub a
  clip on a scene / Prefab-Mode entity, see posed hitboxes + swept trail, add `Hitbox_<id>` to a bone,
  write/remove events.
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
  hitbox → `BindUnarmedHitbox()` (activates `_unarmedHitbox`).
- `OnEnable` also calls `BindUnarmedHitbox()` directly — a startup safety net, because
  `GameEventSO<T>.Raise()` iterates listeners last-added-first and `EquipmentVisuals.OnEnable` may raise
  before `PlayerCombat` subscribed.
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

`CompleteWeaponSwitch` no-ops unless `IsSwitchingWeapon`. Unarmed runs the same flow (base-controller clips, no visual).
`SetDead(true)` keeps `IsSwitchingWeapon` (IsBusy blocks R; watchdog / load fix the visual). `PlayerSaveAdapter` forces the
sheathed stance when `IsInCombat || IsSwitchingWeapon`.

- R is gated by `CanSwitchWeapon()`: not busy, attacking, blocking, dodging, airborne, or already switching — the
  Attack-layer AnyState transitions for block / dodge / jump / fall are held bools that would yank the layer out of the
  clip. `CanAttack()` / `CanBlock()` also require `!IsSwitchingWeapon`.
- `CanAttack()` / `CanBlock()` require `IsInCombat` (gated in `PlayerStateManager`, see
  `Scripts/Player/CLAUDE.md`); `CanDodge()` does not. `IsInCombat` starts `false` (sheathed).
- The `DrawWeapon.started` unsubscribe goes inside the `if (_input == null) return;` guarded block of
  `OnDisable`, like Attack/Block.
- Socket and `Drawn`/`Sheathed` conventions: `Prefabs/Items/Weapons/CLAUDE.md`.

---

## Ranged — Bow (`BowSO` → `ArrowProjectile`)

With a `BowSO` equipped, `TryAttack` skips the combo/hitbox path and calls `TryFireBow` (no attack animation yet).
The bow is read from `EquipmentSystem.GetEquipped(Weapon)`, **not** `_currentWeaponSO` (only set by `OnVisualsRefreshed`,
null after a disable/enable). Ignored while a melee swing is still playing (`IsAttacking`).
Gate order: `CanAttack()` (drawn) → `fireCooldown` → stamina `HasEnough` → ammo `CountOf > 0` (else `OnCombatNotification`
"Out of arrows" toast) → `Consume` stamina (checked) → `TryConsume` ammo → fire. A refused shot spends nothing.

- Aim = crosshair ray (`ViewportPointToRay(0.5, 0.5)`) on Default + CharacterHitbox, skipping the player's own colliders,
  hits closer than the player along the ray (wall behind the OTS camera) and non-hurtbox triggers; no hit → `maxAimDistance`.
  Launch origin = `EquipmentVisuals.ActiveWeaponGO`.
- **Triggers count only on the CharacterHitbox layer** (aim and arrow) — `GetComponentInParent<IDamageable>()` on any trigger
  would let an NPC's perception / interaction sphere take the hit.
- Damage is snapshotted at fire time: `RangedDamage.Compute` = `attackDamage` + max(0, (Dex − baseDex) × `damagePerDexterity`)
  + bow `damageBonus` (no Strength, no Power Strike) — `RangedDamageTests`.
- `ArrowProjectile`: straight line, no Rigidbody, per-frame `SphereCastNonAlloc` (closest valid hit — results are unsorted).
  `IDamageable` → `PlayerCombat.ResolveHit` (same `TryReceiveHit` / sneak rule as melee), destroyed. Solid collider →
  spawns `ammoItem.worldItemPrefab` kinematic at the impact pose (a normal pickup, not saved), **moved into the hit
  collider's scene** — `Instantiate` targets the active scene (Core), where a reload would leave a duplicate arrow. **Prefab frame: root = arrow
  tip, shaft along −Z** — `Projectile_Arrow` and `Item_Arrow` share it so the stuck arrow lines up.

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
