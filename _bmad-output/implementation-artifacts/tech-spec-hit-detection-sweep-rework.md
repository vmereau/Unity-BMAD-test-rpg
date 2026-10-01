---
title: 'Hit Detection Rework — Attacker Sweeps & Shared Hitbox Pipeline'
slug: 'hit-detection-sweep-rework'
created: '2026-09-28'
status: 'implementation-complete'
stepsCompleted: [1, 2, 3, 4]
tech_stack: ['Unity 6000.6.2f1', 'C# (.NET Standard 2.1)', 'PhysX 3D queries (OverlapBox/Sphere/CapsuleNonAlloc)', 'Unity Test Framework (EditMode, NUnit)']
files_to_modify:
  - 'Assets/_Game/Scripts/Core/GameConstants.cs'
  - 'Assets/_Game/Scripts/Combat/WeaponHitbox.cs'
  - 'Assets/_Game/Scripts/Combat/HitSweepTracker.cs (new) + .meta'
  - 'Assets/_Game/Scripts/Combat/PlayerCombat.cs'
  - 'Assets/_Game/Editor/WeaponCreatorWindow.cs'
  - 'Assets/_Game/Prefabs/Player/Player.prefab'
  - 'Assets/_Game/Prefabs/Items/Weapons/Swords/SwordBase/SwordBase_Visual.prefab'
  - 'Assets/_Game/Prefabs/Items/Weapons/Swords/Axe/Axe_Visual.prefab'
  - 'Assets/_Game/Prefabs/Items/Weapons/OneHandedSword/big sword/big sword_Visual.prefab'
  - 'Assets/_Game/Prefabs/Entities/Monsters/Monster_DarknessSpider Variant.prefab'
  - 'Assets/Tests/EditMode/HitSweepTrackerTests.cs (new) + .meta'
  - 'Assets/_Game/Scripts/Combat/CLAUDE.md'
  - 'Assets/_Game/Prefabs/Items/Weapons/CLAUDE.md'
  - 'Assets/_Game/Prefabs/Entities/Monsters/CLAUDE.md'
  - 'Assets/_Game/Prefabs/CLAUDE.md'
code_patterns:
  - 'Animation-event-driven hit window (HitboxEnable/HitboxDisable) + SMB_AttackState safety nets'
  - 'IDamageable (Game.Combat) resolved via GetComponentInParent from a child hurtbox collider'
  - 'Non-alloc physics queries with explicit LayerMask (project-context Physics rules)'
  - 'GameLog + TAG, [SerializeField] private, _camelCase, no magic numbers (config/serialized)'
  - 'Keep script GUIDs stable: edit WeaponHitbox.cs in place, never recreate its .meta'
test_patterns:
  - 'EditMode NUnit in Assets/Tests/EditMode, class [System]Tests, assembly Tests.EditMode references Game'
  - 'Pure C# logic classes tested directly; MonoBehaviours via new GameObject + AddComponent + reflection (PlayerCombatGateTests)'
---

# Tech-Spec: Hit Detection Rework — Attacker Sweeps & Shared Hitbox Pipeline

**Created:** 2026-09-28

## Overview

### Problem Statement

Player hit detection relies on physics triggers (`WeaponHitbox.OnTriggerEnter`), which silently
depends on three fragile conditions: a kinematic Rigidbody on the weapon, the hitbox being on a
layer allowed to touch `CharacterHitbox` (7) in the layer collision matrix, and the target collider
setup. Unarmed attacks never connect today because `UnarmedHitbox` (in `Player.prefab`, child of
`WeaponSocket`) is on layer 3 `Player` — which the collision matrix excludes from `CharacterHitbox`
(layer 3 mask `0x77`, bit 7 off) — and has no Rigidbody (the Player root only has a
`CharacterController`, which does not count for child colliders), so the trigger pair is
static-vs-static. Swords work only because `SwordBase_Visual` / `Axe_Visual` sit on layer 0 with a
kinematic Rigidbody on the root.

Separately, human-height swing arcs pass over low monsters, which pushed the spider's `HitBox` to a
fake 1.5 m-tall trigger capsule (radius 0.4, center y 0.75) floating above the model.

Finally, the hitbox pipeline is player-only: `WeaponHitbox` is coupled to `EntityHealth` (not the
existing `IDamageable` interface) and is only driven by `PlayerCombat`, so AI entities cannot reuse it.

### Solution

Replace trigger-based detection with a **forgiving, attacker-side sweep component**: during the
`HitboxEnable` → `HitboxDisable` animation-event window, each frame it casts the weapon's shape
from its previous pose to its current pose with an explicit `LayerMask`, plus a forgiving reach
(vertical tolerance and optional arc/range assist) so low targets connect. The component is
owner-agnostic — it resolves `IDamageable`, excludes its owner's own colliders, dedupes one hit per
target per swing, and raises an event — so AI entities can adopt it later.

### Scope

**In Scope:**
- New owner-agnostic hit-detection component replacing `WeaponHitbox` trigger logic (resolves
  `IDamageable`, ignores the owner's hierarchy, event reports target + hit point)
- `PlayerCombat` rewired to the new component for weapons and unarmed; damage computation stays in
  `PlayerCombat`
- Per-weapon sweep shape + forgiveness settings; unarmed default
- Prefab migration: `SwordBase_Visual`, `Axe_Visual`, big sword visual, `UnarmedHitbox` in
  `Player.prefab` — kinematic Rigidbody / layer workarounds no longer required
- Spider `HitBox` resized to its real body
- EditMode tests for pure logic (dedupe, owner exclusion, forgiveness math)
- CLAUDE.md updates (Combat, Monsters — including the stale "HitBox must be non-trigger" rule)

**Out of Scope:**
- Switching AI attacks (`EntityBrain` range check) to sweeps — groundwork only
- Monster attack animations / animation events
- Block / perfect-block / dodge changes
- Hit reactions, VFX, hit-stop
- Directional or per-bone hurtboxes

## Context for Development

### Codebase Patterns

- User preferences: **forgiving hits** (target inside the swing's arc/reach should connect, low
  monsters included); design must allow **both player and AI** to own the component — no hardcoded
  player references.
- Hitbox window is fully animation-event driven (`AnimationEventReceiver` → `PlayerCombat.OnHitboxEnable/Disable`)
  with `SMB_AttackState` enter/exit safety nets — keep this pipeline intact.
- Cross-system communication must use `GameEventSO<T>`; `IDamageable` (`Game.Combat`) is already
  implemented by `EntityHealth` and `PlayerHealth`.
- Open design point: self-hit exclusion by owner root only (default) vs faction filter — place the
  check where a faction filter can be added later.

**Current pipeline (verified):**
- `AnimationEventReceiver` (Player root, same GO as Animator) routes `HitboxEnable`/`HitboxDisable` clip
  events → `PlayerCombat.OnHitboxEnable/OnHitboxDisable` → `_activeHitbox?.Enable()/Disable()`.
  `SMB_AttackState` enter/exit and all combo-end paths also call `_activeHitbox?.Disable()` — this
  contract stays unchanged; only `WeaponHitbox` internals change.
- `PlayerCombat.BindWeaponHitbox()` finds `WeaponHitbox` via
  `_equipmentVisuals.ActiveWeaponGO.GetComponentInChildren<WeaponHitbox>(true)`; falls back to
  `BindUnarmedHitbox()` (`_unarmedHitbox` GO, child of `WeaponSocket` in `Player.prefab`, toggled
  active/inactive on bind/unbind). Subscribes `OnEnemyHit += OnWeaponHit`.
- `PlayerCombat.OnWeaponHit(EntityHealth)` calls `health.TakeDamage(ComputeEffectiveDamage())` directly
  — bypasses `TryReceiveHit`. `EntityBrain.ExecuteAttack()` (AI) already uses the correct sequence:
  `target.TryReceiveHit(gameObject)` → switch → `TakeDamage` only on `NotBlocked`.
- `WeaponHitbox` today: caches `GetComponentsInChildren<Collider>(true)`, toggles `enabled`, filters
  `other.gameObject.layer == CharacterHitbox`, `GetComponentInParent<EntityHealth>()`, dedupes via
  `HashSet<EntityHealth>` cleared on Enable/Disable, raises `event Action<EntityHealth> OnEnemyHit`.
- `IDamageable` (`Game.Combat`): `IsDead`, `TakeDamage(float)`, `TryReceiveHit(GameObject attacker)`.
  Implemented by `EntityHealth` (always `NotBlocked`) and `PlayerHealth` (delegates to PlayerCombat
  block/dodge). `FactionMember` resolves `IDamageable` via `GetComponent` on the entity root.

**Current prefab state (verified from YAML):**
| Object | Layer | Collider | Rigidbody |
|---|---|---|---|
| `Player.prefab/.../WeaponSocket/UnarmedHitbox` (inactive) | 3 Player | Sphere r=0.25 trigger, disabled | none (root has CharacterController only) |
| `SwordBase_Visual/Drawn` | 0 | Box trigger size (0.029, 0.845, 0.146) center y 0.35 | kinematic on root |
| `Axe_Visual/Drawn` | 0 | Box trigger head-only size (0.06, 0.22, 0.22) center (0, 0.39, 0.095) | kinematic on root |
| `big sword_Visual/Drawn` | (Weapon Creator output) | Box trigger fitted | kinematic on root |
| `Monster_DarknessSpider Variant/HitBox` | 7 CharacterHitbox | Capsule **trigger** r=0.4 h=1.5 center y 0.75 (oversized workaround) | none |
| `NPC_base Variant/Hitbox` | 7 | Capsule trigger r=0.5 h=2.32 center y 0.66 | none |
| `NPC_base Variant/.../UnarmedHitbox` | 0 | Sphere r=0.25 trigger + `WeaponHitbox` — **orphan, nothing drives it** (future AI hand hitbox) | none |
| Player hurtbox on layer 7 | — | **does not exist** | — |

- Layer collision matrix: Player(3)×CharacterHitbox(7) OFF; Characters(6)×CharacterHitbox(7) OFF.
  Physics queries with an explicit `LayerMask` ignore the matrix entirely.
- `LockOnSystem._lockOnLayerMask` = bit 6 (Characters) only — a Player hurtbox on layer 7 will not
  affect lock-on.
- `WeaponCreatorWindow.SetupModelChild()` adds a trigger `BoxCollider` fitted to renderers +
  `WeaponHitbox` on `Drawn`; line 264 adds a kinematic `Rigidbody` to the `_Visual` root.
- `CombatConfigSO.attackHitRange` is only read inside `#if false` debug code in `PlayerCombat` (dead;
  leave untouched — out of scope).
- `SwordBase_Visual.prefab` has a hand-crafted `.meta` GUID — edit in place only
  (`PrefabUtility.LoadPrefabContents` / `SaveAsPrefabAsset`), never recreate.
- MCP gotcha (root CLAUDE.md): after raw YAML edits to a prefab use `refresh_unity(mode="if_dirty")`,
  never `force`. Prefer editing prefabs through Unity MCP / `PrefabUtility`.

### Files to Reference

| File | Purpose |
| ---- | ------- |
| `Assets/_Game/Scripts/Combat/WeaponHitbox.cs` | Component being rewritten (keep class name + `.meta` GUID `b3698d187bb02ff4f9d9218ca603f3fb`) |
| `Assets/_Game/Scripts/Combat/PlayerCombat.cs` | Binds hitbox, applies damage (`OnWeaponHit`, `BindWeaponHitbox`, `BindUnarmedHitbox`, `UnbindWeaponHitbox`) |
| `Assets/_Game/Scripts/Combat/AnimationEventReceiver.cs` | Event routing — unchanged |
| `Assets/_Game/Scripts/Combat/SMB_AttackState.cs` | Enter/exit safety nets — unchanged |
| `Assets/_Game/Scripts/Combat/IDamageable.cs` | Target contract the new hitbox resolves |
| `Assets/_Game/Scripts/AI/EntityHealth.cs`, `Scripts/Player/PlayerHealth.cs` | `IDamageable` implementations |
| `Assets/_Game/Scripts/AI/EntityBrain.cs` (~L312-340) | Reference hit-resolution sequence (TryReceiveHit → TakeDamage) for future AI adoption |
| `Assets/_Game/Scripts/Inventory/EquipmentVisuals.cs` | `ActiveWeaponGO`, Drawn/Sheathed toggling |
| `Assets/_Game/Editor/WeaponCreatorWindow.cs` (~L264, ~L330-346) | Generates `_Visual` prefab with RB + trigger box + `WeaponHitbox` |
| `Assets/_Game/Prefabs/Player/Player.prefab` | `UnarmedHitbox` (fileID GO `2908738489711061366`), `PlayerCombat._unarmedHitbox` |
| `Assets/_Game/Prefabs/Entities/Monsters/Monster_DarknessSpider Variant.prefab` | Spider `HitBox` capsule |
| `Assets/Tests/EditMode/PlayerCombatGateTests.cs` | Test style reference |
| `_bmad-output/project-context.md` | Physics: non-alloc queries, explicit layers, minimal Rigidbody |

### Technical Decisions

1. **Keep `WeaponHitbox` class name, file and GUID.** All prefabs (`SwordBase`, `Axe`, `big sword`,
   `Player`, `NPC_base Variant`) and `WeaponCreatorWindow` reference it; renaming would break script
   references. Rewrite internals only; keep public `Enable()` / `Disable()` so `PlayerCombat`,
   `SMB_AttackState` paths and combo-end cleanup are untouched.
2. **Attacker-side swept queries replace `OnTriggerEnter`.** While enabled, in `LateUpdate` (after the
   Animator has posed the hand bone) the hitbox samples its shape between last frame's pose and this
   frame's pose (sub-steps capped, spaced by a max step distance) with `Physics.Overlap*NonAlloc`,
   explicit `LayerMask` (default: CharacterHitbox) and `QueryTriggerInteraction.Collide` (hurtboxes
   are triggers). No Rigidbody and no collision-matrix dependency.
3. **Shape authoring = the existing collider on the hitbox GO** (Box / Sphere / Capsule), kept
   **permanently disabled** and used only as a shape definition (read its center/size/radius +
   transform). Zero data migration for existing weapons, editor wireframe for free, Weapon Creator's
   fitted box keeps working.
4. **Forgiveness is per-hitbox, serialized:** `_reachPadding` (inflates the query shape) and
   `_verticalReach` (extra downward world-space reach so swings catch low targets like the spider).
   Unarmed and each weapon prefab can tune independently; sensible defaults on the component.
5. **Owner-agnostic.** Hitbox exposes `Owner` (Transform) set by the controlling component
   (`PlayerCombat` today, `EntityBrain` later). Colliders inside the owner's hierarchy are skipped.
   Targets resolved as `IDamageable` via `GetComponentInParent`. Dedupe one hit per `IDamageable` per
   window. Event becomes `event Action<IDamageable, Vector3> OnHit` (target, hit point). Faction
   filtering is a later hook next to the owner check — not implemented now.
6. **Pure logic extracted to `HitSweepTracker`** (plain C#, `Game.Combat`): per-window dedupe,
   owner exclusion decision, sub-step count computation — the EditMode test surface.
7. **Damage resolution in `PlayerCombat` uses `TryReceiveHit` → `TakeDamage`**, mirroring
   `EntityBrain`, so future NPC blocking works without touching the hitbox.
8. **Remove kinematic Rigidbodies** from weapon `_Visual` roots and stop `WeaponCreatorWindow` from
   adding one; creator sets the fitted box `enabled = false`.
9. **Groundwork for AI → player hits:** add a `Hitbox` child (layer 7 CharacterHitbox, trigger capsule
   sized to the player body) to `Player.prefab`. Safe: player sweeps exclude their own hierarchy;
   lock-on only scans layer 6. `EntityBrain` keeps its range check (out of scope).
10. **Spider hurtbox resized to the model's real body** (flat capsule/box from renderer bounds); low-target
    reach now comes from the attacker's `_verticalReach`, not an inflated hurtbox.

## Implementation Plan

### Tasks

- [x] Task 1: Add the hurtbox layer-name constant
  - File: `Assets/_Game/Scripts/Core/GameConstants.cs`
  - Action: Add `public const string CHARACTER_HITBOX_LAYER_NAME = "CharacterHitbox";` (group it
    under a `// Physics layers` comment).
  - Notes: Used by `WeaponHitbox` as the fallback target mask; avoids a string literal in combat code.

- [x] Task 2: Create the pure-logic `HitSweepTracker`
  - File: `Assets/_Game/Scripts/Combat/HitSweepTracker.cs` (new, namespace `Game.Combat`, plain C#
    `public sealed class`, no `MonoBehaviour`)
  - Action: Implement:
    ```csharp
    private readonly HashSet<IDamageable> _hitThisWindow = new();
    public bool IsWindowOpen { get; private set; }
    public void BeginWindow()  { _hitThisWindow.Clear(); IsWindowOpen = true; }
    public void EndWindow()    { _hitThisWindow.Clear(); IsWindowOpen = false; }

    /// Returns true exactly once per target per window. Rejects: window closed, null target,
    /// dead target, target == owner.
    public bool TryRegisterHit(IDamageable target, IDamageable owner)
    {
        if (!IsWindowOpen || target == null || target.IsDead) return false;
        if (owner != null && ReferenceEquals(target, owner)) return false;
        return _hitThisWindow.Add(target);
    }

    /// Number of pose samples for a frame's travel: ceil(distance / maxStep), clamped [1, maxSubSteps].
    /// maxStepDistance <= 0 or maxSubSteps < 1 → 1.
    public static int ComputeSubSteps(float travelDistance, float maxStepDistance, int maxSubSteps)

    /// World half-extents of a BoxCollider: |size * lossyScale| * 0.5 + padding on every axis.
    public static Vector3 ComputeBoxHalfExtents(Vector3 size, Vector3 lossyScale, float padding)

    /// World radius: radius * max(|lossyScale.x|, |lossyScale.y|, |lossyScale.z|) + padding.
    public static float ComputeScaledRadius(float radius, Vector3 lossyScale, float padding)
    ```
  - Notes: No `GameLog` calls here, so no `TAG` constant (root checklist: dead TAG = LOW finding).

- [x] Task 3: Rewrite `WeaponHitbox` as an owner-agnostic swept hitbox
  - File: `Assets/_Game/Scripts/Combat/WeaponHitbox.cs` (edit in place — **keep the class name, file
    and `.meta` GUID `b3698d187bb02ff4f9d9218ca603f3fb`**)
  - Action:
    1. Remove `OnTriggerEnter`, the `EntityHealth` dependency and `using Game.AI`. Add `using Game.Core;`.
    2. Fields:
       ```csharp
       private const string TAG = "[Combat]";
       private const int MAX_OVERLAP_RESULTS = 16;
       [Tooltip("Layers treated as hurtboxes. Empty = CharacterHitbox.")]
       [SerializeField] private LayerMask _targetLayers;
       [Tooltip("Inflates the query shape on every side (forgiving reach), metres.")]
       [SerializeField, Min(0f)] private float _reachPadding = 0.1f;
       [Tooltip("Extra downward world-space reach from the shape centre so swings catch low targets, metres. 0 = off.")]
       [SerializeField, Min(0f)] private float _verticalReach = 0.6f;
       [Tooltip("Radius of the downward reach capsule, metres.")]
       [SerializeField, Min(0.01f)] private float _verticalReachRadius = 0.25f;
       [Tooltip("Max distance between swept samples within one frame, metres.")]
       [SerializeField, Min(0.01f)] private float _maxStepDistance = 0.1f;
       [Tooltip("Upper bound on samples per frame.")]
       [SerializeField, Min(1)] private int _maxSubSteps = 8;

       public event System.Action<IDamageable, Vector3> OnHit;   // target, approximate hit point
       public Transform Owner { get; private set; }

       private readonly HitSweepTracker _tracker = new();
       private readonly Collider[] _overlapBuffer = new Collider[MAX_OVERLAP_RESULTS];
       private Collider _shape;              // Box/Sphere/Capsule on this GO — shape definition only
       private IDamageable _ownerDamageable;
       private Vector3 _prevPosition;
       private Quaternion _prevRotation;
       ```
       Rename the old `OnEnemyHit` event to `OnHit` (only `PlayerCombat` subscribes — Task 4).
    3. `Awake()`: `_shape = GetComponent<Collider>()`; if null or not Box/Sphere/Capsule →
       `GameLog.Error(TAG, ...)` naming the GO, and every query becomes a no-op. Set
       `_shape.enabled = false` (shape-only, never a physics participant). If `_targetLayers == 0` →
       `_targetLayers = LayerMask.GetMask(GameConstants.CHARACTER_HITBOX_LAYER_NAME)`. Call `Disable()`.
    4. `public void SetOwner(Transform owner)`: store `Owner`; `_ownerDamageable = owner != null ?
       owner.GetComponent<IDamageable>() : null`.
    5. `public void Enable()`: if `Owner == null` → `GameLog.Warn(TAG, "... no owner — self-hits not filtered")`.
       `_tracker.BeginWindow()`; `_prevPosition = transform.position; _prevRotation = transform.rotation;`.
       Keep the public signature (called by `PlayerCombat.OnHitboxEnable`).
    6. `public void Disable()`: `_tracker.EndWindow()`. Keep the public signature (called on every
       combo-end path in `PlayerCombat`).
    7. `private void LateUpdate()` (runs after the Animator posed the hand bone): if
       `!_tracker.IsWindowOpen || _shape == null` return. `n = HitSweepTracker.ComputeSubSteps(
       Vector3.Distance(_prevPosition, transform.position), _maxStepDistance, _maxSubSteps)`; for
       `i = 1..n`: `t = (float)i / n`, `pos = Vector3.Lerp(_prevPosition, transform.position, t)`,
       `rot = Quaternion.Slerp(_prevRotation, transform.rotation, t)`, `SampleAt(pos, rot)`. Then store
       current pose into `_prev*`. Zero allocations (no LINQ, no `new` per frame).
    8. `private void SampleAt(Vector3 pos, Quaternion rot)`: compute the world shape from `_shape`'s
       local data + `transform.lossyScale` using the `HitSweepTracker` static helpers:
       - `BoxCollider`: center = `pos + rot * Vector3.Scale(box.center, lossyScale)`;
         `Physics.OverlapBoxNonAlloc(center, ComputeBoxHalfExtents(box.size, lossyScale, _reachPadding), _overlapBuffer, rot, _targetLayers, QueryTriggerInteraction.Collide)`.
       - `SphereCollider`: `Physics.OverlapSphereNonAlloc(center, ComputeScaledRadius(...), ...)`.
       - `CapsuleCollider`: endpoints along `direction` axis (0=X,1=Y,2=Z) at
         `±max(0, height*scaleAlongAxis*0.5 - scaledRadius)` from center, rotated by `rot`;
         `Physics.OverlapCapsuleNonAlloc(p0, p1, scaledRadius + _reachPadding, ...)`.
       - Process results (`ProcessOverlaps(count, center)`).
       - If `_verticalReach > 0`: `Physics.OverlapCapsuleNonAlloc(center, center + Vector3.down * _verticalReach, _verticalReachRadius, _overlapBuffer, _targetLayers, QueryTriggerInteraction.Collide)` → `ProcessOverlaps`.
    9. `private void ProcessOverlaps(int count, Vector3 queryCenter)`: for each collider:
       skip if `Owner != null && col.transform.IsChildOf(Owner)`; `var target =
       col.GetComponentInParent<IDamageable>()`; if `_tracker.TryRegisterHit(target, _ownerDamageable)`
       → `OnHit?.Invoke(target, col.ClosestPoint(queryCenter))`. If `count == MAX_OVERLAP_RESULTS`
       log `GameLog.Warn` once per window (buffer saturated).
    10. `OnDrawGizmosSelected()` (editor visual aid): draw the downward reach capsule as a wire line +
        spheres at `center` and `center + down * _verticalReach` using `_verticalReachRadius`. Use the
        collider's own gizmo for the main shape.
  - Notes: `GetComponentInParent<IDamageable>()` works with interfaces in Unity 6. Update the class XML
    summary (swept, owner-agnostic, no Rigidbody/matrix dependency). Faction filtering hook = a
    comment next to the owner check in `ProcessOverlaps` (not implemented).

- [x] Task 4: Rewire `PlayerCombat` to the new hitbox API and hit resolution
  - File: `Assets/_Game/Scripts/Combat/PlayerCombat.cs`
  - Action:
    1. `BindWeaponHitbox()` / `BindUnarmedHitbox()`: after resolving `_activeHitbox`, call
       `_activeHitbox.SetOwner(transform)` then `_activeHitbox.OnHit += OnWeaponHit`.
    2. `UnbindWeaponHitbox()`: `_activeHitbox.OnHit -= OnWeaponHit` (rename from `OnEnemyHit`).
    3. `BindUnarmedHitbox()`: add a null guard — if `_unarmedHitbox == null` →
       `GameLog.Warn(TAG, "Unarmed hitbox not assigned — unarmed attacks cannot hit")`, set
       `_activeHitbox = null`, return. (Today it NREs.) Also warn in `Awake` if unassigned.
    4. Replace `OnWeaponHit(EntityHealth health)` with:
       ```csharp
       private void OnWeaponHit(IDamageable target, Vector3 hitPoint)
       {
           if (target == null || target.IsDead) return;
           HitResult result = target.TryReceiveHit(gameObject);
           if (result != HitResult.NotBlocked)
           {
               GameLog.Info(TAG, $"Weapon hit {result} at {hitPoint}");
               return;
           }
           target.TakeDamage(ComputeEffectiveDamage());
           GameLog.Info(TAG, $"Weapon hit landed at {hitPoint}");
       }
       ```
    5. Remove `using Game.AI;` if nothing else in the file uses it. Update the stale comment in
       `ExecuteAttack()` ("Unarmed fallback (no hitbox): sphere overlap fires immediately") → the
       warning only means no hitbox is bound.
  - Notes: Do not touch the combo / SMB / animation-event methods — the `Enable()`/`Disable()` contract
    is unchanged.

- [x] Task 5: Stop the Weapon Creator from generating physics-dependent hitboxes
  - File: `Assets/_Game/Editor/WeaponCreatorWindow.cs`
  - Action: Delete the `visualRoot.AddComponent<Rigidbody>().isKinematic = true;` line (~L264). In
    `SetupModelChild` (~L340), after `FitBoxToRenderers(trigger)`, set `trigger.enabled = false;`
    (shape-only). Update any comment/dialog text that mentions the kinematic Rigidbody.
  - Notes: `_World` prefab keeps its non-kinematic Rigidbody (~L289) — unrelated (drop physics).

- [x] Task 6: Migrate weapon `_Visual` prefabs
  - Files: `Prefabs/Items/Weapons/Swords/SwordBase/SwordBase_Visual.prefab`,
    `Prefabs/Items/Weapons/Swords/Axe/Axe_Visual.prefab`,
    `Prefabs/Items/Weapons/OneHandedSword/big sword/big sword_Visual.prefab`
  - Action: For each (via Unity MCP / `PrefabUtility.LoadPrefabContents` + `SaveAsPrefabAsset` on the
    same path — never recreate `SwordBase_Visual`): remove the `Rigidbody` from the root; set the
    `Drawn` `BoxCollider.enabled = false` (keep size/center); leave `WeaponHitbox` defaults
    (`_targetLayers` empty → CharacterHitbox at runtime) unless play-testing calls for tuning.
  - Notes: If a raw YAML edit is used instead, refresh with `refresh_unity(mode="if_dirty")` only.
    `big sword_*` files are currently **untracked** in git — they are the user's in-progress weapon;
    migrate them but do not commit on their behalf unless asked.

- [x] Task 7: Migrate the Player prefab (unarmed hitbox + player hurtbox)
  - File: `Assets/_Game/Prefabs/Player/Player.prefab`
  - Action:
    1. `UnarmedHitbox` (child of `WeaponSocket`): set layer to `0 Default` (layer no longer matters;
       avoids implying Player-layer semantics). Keep the SphereCollider (r=0.25, disabled, trigger).
       Remove the leftover `MeshFilter` (no renderer — dead component). Keep the GO inactive by default
       (`PlayerCombat` toggles it). Tune `_verticalReach` ≈ 0.6 / `_reachPadding` ≈ 0.1 (defaults).
    2. Add child `Hitbox` under the Player root: layer `7 CharacterHitbox`, `CapsuleCollider`
       `isTrigger = true`, radius 0.3, height 1.7, center (0, 0.9, 0) (matches the
       `CharacterController`), no other components.
  - Notes: The player's own sweeps skip `Hitbox` via `IsChildOf(Owner)` and the owner-`IDamageable`
    check. `LockOnSystem` scans layer 6 only — unaffected. The CharacterController ignores trigger
    colliders for movement.

- [x] Task 8: Resize the spider hurtbox to its real body
  - File: `Assets/_Game/Prefabs/Entities/Monsters/Monster_DarknessSpider Variant.prefab`
  - Action: Open the prefab via Unity MCP, read the combined renderer bounds of the `Visual` child in
    prefab-root local space, then set the `HitBox` CapsuleCollider to wrap the body: `direction = 2`
    (Z, along body length), `radius ≈ half body height` (clamp so it covers the leg span width
    reasonably), `height ≈ body length`, `center = bounds center`. Keep `isTrigger = true`, layer 7.
  - Notes: If the body is much wider than tall, a `BoxCollider` fits better — acceptable only if
    replacing the component keeps the `HitBox` GO (layer 7) and nothing references the capsule's
    fileID (grep the prefab and scenes first). Record final values in the Monsters CLAUDE.md.

- [x] Task 9: EditMode tests for the pure logic
  - File: `Assets/Tests/EditMode/HitSweepTrackerTests.cs` (new, class `HitSweepTrackerTests`)
  - Action: Private nested `FakeDamageable : IDamageable` (settable `IsDead`, no-op `TakeDamage`,
    `TryReceiveHit` → `NotBlocked`). Tests:
    - `TryRegisterHit_FirstHitInWindow_ReturnsTrue`
    - `TryRegisterHit_SameTargetTwiceInWindow_ReturnsFalseSecondTime`
    - `TryRegisterHit_AfterEndAndBeginWindow_ReturnsTrueAgain`
    - `TryRegisterHit_WindowClosed_ReturnsFalse`
    - `TryRegisterHit_TargetIsOwner_ReturnsFalse`
    - `TryRegisterHit_DeadTarget_ReturnsFalse`
    - `TryRegisterHit_NullTarget_ReturnsFalse`
    - `TryRegisterHit_TwoDistinctTargets_BothReturnTrue`
    - `ComputeSubSteps_ZeroDistance_ReturnsOne`
    - `ComputeSubSteps_DistanceOverStep_ReturnsCeiling` (0.35 / 0.1 → 4)
    - `ComputeSubSteps_LargeDistance_ClampedToMax` (10 / 0.1, max 8 → 8)
    - `ComputeSubSteps_NonPositiveStep_ReturnsOne`
    - `ComputeBoxHalfExtents_AppliesScaleAndPadding` (size (1,2,4), scale (2,1,0.5), pad 0.1 → (1.1,1.1,1.1))
    - `ComputeBoxHalfExtents_NegativeScale_UsesAbsolute`
    - `ComputeScaledRadius_UsesMaxAxisScalePlusPadding` (r 0.25, scale (1,2,1), pad 0.1 → 0.6)
  - Notes: Assembly `Tests.EditMode` already references `Game`. Run via Unity MCP `run_tests`
    (EditMode); full EditMode suite must stay green.

- [x] Task 10: Update CLAUDE.md documentation
  - Files and actions:
    - `Assets/_Game/Scripts/Combat/CLAUDE.md`: rewrite the "WeaponHitbox System" section — swept
      `LateUpdate` queries, shape-only disabled collider, `SetOwner` requirement, `OnHit(IDamageable,
      Vector3)`, forgiveness fields, `HitSweepTracker`; update code snippets (`OnEnemyHit` →
      `OnHit`, `SetOwner`); hit resolution now `TryReceiveHit` → `TakeDamage`. Checklist: remove the
      "kinematic Rigidbody" HIGH row; add HIGH "`WeaponHitbox` bound without `SetOwner` — owner's own
      hurtbox can be hit"; add MEDIUM "hitbox shape collider left enabled or not Box/Sphere/Capsule";
      add note "AI adoption: EntityBrain owns a WeaponHitbox, calls SetOwner(transform), resolves hits
      with the same TryReceiveHit → TakeDamage sequence".
    - `Assets/_Game/Prefabs/Items/Weapons/CLAUDE.md`: remove the "Kinematic Rigidbody on Visual Root"
      section and its checklist row; update the two-prefab diagram (no RB on `_Visual`, `Drawn` =
      `WeaponHitbox` + **disabled** shape collider); update the Weapon Creator bullet list.
    - `Assets/_Game/Prefabs/Entities/Monsters/CLAUDE.md`: hurtbox is a **trigger** sized to the real
      body (fix the stale "non-trigger" rule); remove the Rigidbody pair table (obsolete); explain that
      low-target reach comes from the attacker's `_verticalReach`, never from inflating hurtboxes;
      record the spider's final HitBox values.
    - `Assets/_Game/Prefabs/CLAUDE.md` (~L98): NPC `Hitbox` is a trigger capsule used by `WeaponHitbox`
      sweeps; add the Player `Hitbox` child (layer 7) and note the NPC `UnarmedHitbox` is currently
      undriven (future AI hitbox).

### Acceptance Criteria

- [ ] AC 1: Given the player is unarmed with fists raised (combat state on) and a spider within
  reach, when the player performs a jab, then the spider loses HP exactly once for that swing.
- [ ] AC 2: Given the player wields SwordBase, Axe, or big sword, when a swing passes through a
  spider or NPC, then the target loses HP exactly once per swing even if the blade overlaps it on
  several frames or sub-steps.
- [ ] AC 3: Given a low target (spider) whose hurtbox lies below the swing arc but within
  `_verticalReach` of the hitbox centre, when the swing window is active, then the hit connects.
- [ ] AC 4: Given a target entirely outside the shape + `_reachPadding` + vertical-reach volume for
  the whole window, when the player swings, then no damage is dealt.
- [ ] AC 5: Given the weapon `_Visual` prefabs and `UnarmedHitbox` have no Rigidbody and the layer
  collision matrix is unchanged, when attacks connect, then detection still works (no physics
  callback dependency).
- [ ] AC 6: Given the player's own `Hitbox` (layer 7) is inside the swing volume, when the player
  attacks, then the player takes no damage and no hit is logged against the player.
- [ ] AC 7: Given the `HitboxDisable` event has fired or an attack is interrupted (dodge / block /
  combo end), when the weapon keeps moving through a target, then no damage is dealt.
- [ ] AC 8: Given a fast swing that moves the hitbox more than its own size in one frame, when a
  target lies between the previous and current pose, then the hit connects (sub-stepped sweep).
- [ ] AC 9: Given a dead target, when a swing overlaps it, then `TakeDamage` is not called.
- [ ] AC 10: Given a hit target whose `TryReceiveHit` returns anything other than `NotBlocked`, when
  the hit registers, then `TakeDamage` is not called and the result is logged.
- [ ] AC 11: Given `PlayerCombat._unarmedHitbox` is unassigned, when the player attacks unarmed,
  then no exception is thrown and a warning is logged.
- [ ] AC 12: Given a hitbox GO without a Box/Sphere/Capsule collider, when it awakes, then
  `GameLog.Error` names the GO and its sweeps are no-ops (no exception).
- [ ] AC 13: Given a new weapon created with the Weapon Creator, when inspected, then its `_Visual`
  root has no Rigidbody and the `Drawn` box collider is disabled, and the weapon hits in play mode.
- [ ] AC 14: Given the EditMode suite, when run, then all `HitSweepTrackerTests` and all previously
  existing tests pass.
- [ ] AC 15: Given the spider prefab, when viewed in the scene, then its `HitBox` wraps the visible
  body and no longer extends ~1.5 m above it.

## Additional Context

### Dependencies

- No new packages. Unity PhysX query API (`Physics.Overlap*NonAlloc`, `Collider.ClosestPoint`).
- Unity Editor with MCP connection for prefab edits (Tasks 6–8) and `run_tests` (Task 9).
- Task order: 1 → 2 → 3 → 4 (code compiles together) → 5 → 6/7/8 (prefabs, any order) → 9 → 10.
  Tasks 3 and 4 must land in the same compile (event rename).

### Testing Strategy

- **EditMode (automated):** `HitSweepTrackerTests` (Task 9) — dedupe, owner exclusion, dead/null
  targets, window gating, sub-step math, shape inflation math. Run the full EditMode suite.
- **Manual play-mode checklist (Core + StartingTown):**
  1. Unarmed jab + uppercut on the spider → HP bar drops once per swing (AC 1).
  2. SwordBase / Axe / big sword combos on spider and a hostile NPC → one hit per swing (AC 2).
  3. Stand next to the spider, swing high → still connects (AC 3); step out of reach → misses (AC 4).
  4. Dodge / block mid-swing near an enemy → no hit after interrupt (AC 7).
  5. Watch console: no self-hit logs, no "no owner" warnings, no buffer-saturation warnings (AC 6).
  6. Create a throwaway weapon with the Weapon Creator, check the prefab, delete it (AC 13).
- Check `read_console` for errors after domain reload and after each prefab edit.

### Notes

- **High-risk items:**
  - Script GUID break — editing `WeaponHitbox.cs` in place keeps it; do **not** delete/recreate the
    file or its `.meta`.
  - `SwordBase_Visual.prefab` has a hand-crafted `.meta` GUID referenced by `Weapon_TestSword` — edit
    in place only.
  - Serialized-field migration: existing prefab instances get the C# initializer defaults for the new
    fields; `_targetLayers` deserializes as 0 → resolved to CharacterHitbox in `Awake`.
  - `LateUpdate` ordering: animation events fire during the Animator update (before `LateUpdate`), so
    the first sample of a window uses the posed hand — `_prev*` is set in `Enable()` so the first
    frame samples once at the current pose.
  - Forgiveness tuning (`_verticalReach`, `_reachPadding`) is a feel decision — defaults are a start
    point; expect play-test tuning per weapon.
- **Known limitations:** hit point is approximate (`ClosestPoint` to the query centre). Sweep
  sampling interpolates the hand pose linearly between frames, so very wide arcs at low FPS are
  approximated by chords (mitigated by `_maxSubSteps`). Friendly fire on non-hostile NPCs is
  unchanged (still hittable, as today).
- **Future (out of scope):** `EntityBrain` owning a `WeaponHitbox` (NPC `UnarmedHitbox` already
  exists, undriven) with an AI animation-event receiver; faction filter next to the owner check;
  hit-stop / hit VFX via the `hitPoint`; removing the dead `CombatConfigSO.attackHitRange`.

## Review Notes

- Adversarial review completed (inline, 2026-09-28)
- Findings: 9 total, 2 fixed, 7 skipped
- Resolution approach: auto-fix (real findings only)
- Fixed: F1 `WeaponHitbox.Enable()` ignores a re-Enable while the window is open (no dedupe reset
  mid-swing); F3 spec file list now includes new `.meta` files
- Skipped: F2 play-mode ACs (1–8, 13, 15) still need the manual checklist; F4 repeated
  unassigned-unarmed warning (asked for by the spec); F5 `TestScene.unity` scene-level Rigidbody
  override on the Player's `UnarmedHitbox` (harmless, out of scope); F6–F9 noise / by design
- Spider `HitBox` became a trigger `BoxCollider` (body wider than tall, per Task 8 note): center
  `(0, 0.293, -0.064)`, size `(1.73, 0.498, 1.43)` under `CreatureVisual`
- Status stays `implementation-complete` until the manual play-mode checklist passes → then `completed`
