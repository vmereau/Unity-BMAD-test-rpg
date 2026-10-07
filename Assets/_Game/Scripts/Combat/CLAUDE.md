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

`PlayerCombat.OnDrawWeaponStarted` toggles `PlayerStateManager.SetInCombat(...)` (state + animator
bool) and `EquipmentVisuals.SetCombatState(...)` (reparents the weapon visual to hand / sheath socket).

- Ignored while `IsBusy` (cursor unlocked) **or** `IsAttacking` — drawing mid-combo would deactivate
  `Drawn` mid-swing and silence that swing's hit window.
- `CanAttack()` / `CanBlock()` require `IsInCombat` (gated in `PlayerStateManager`, see
  `Scripts/Player/CLAUDE.md`); `CanDodge()` does not. `IsInCombat` starts `false` (sheathed).
- The `DrawWeapon.started` unsubscribe goes inside the `if (_input == null) return;` guarded block of
  `OnDisable`, like Attack/Block.
- Socket and `Drawn`/`Sheathed` conventions: `Prefabs/Items/Weapons/CLAUDE.md`.

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
| MEDIUM | `OnDrawWeaponStarted` missing the `IsAttacking` guard |

Combo/animation-event checklist rows: `PLAYER_COMBO.md`. `WeaponSO` subclassing rules:
`ScriptableObjects/Items/CLAUDE.md`.
