---
title: 'AI Melee Hitboxes — Animation-Driven Entity Attacks & Hitbox Tuner'
slug: 'ai-melee-hitboxes-tuner'
created: '2026-09-29'
status: 'completed'
stepsCompleted: [1, 2, 3, 4]
tech_stack: ['Unity 6000.6.2f1', 'C# (.NET Standard 2.1)', 'PhysX non-alloc overlap queries (via existing WeaponHitbox)', 'Animation Events (ModelImporter clipAnimations + AnimationUtility)', 'UnityEditor AnimationMode / Handles / UIElements EditorWindow', 'Unity Test Framework (EditMode, NUnit)']
files_to_modify:
  - 'Assets/_Game/Scripts/AI/EntityMeleeAttacker.cs (new) + .meta'
  - 'Assets/_Game/Scripts/AI/EntityAnimationEventReceiver.cs (new) + .meta'
  - 'Assets/_Game/Scripts/AI/SMB_EntityAttackState.cs (new) + .meta'
  - 'Assets/_Game/Scripts/AI/EntityBrain.cs'
  - 'Assets/_Game/Scripts/Combat/WeaponHitbox.cs (debug sweep recording, editor-only)'
  - 'Assets/_Game/Scripts/Combat/HitWindowEvents.cs (new, pure event-list logic) + .meta'
  - 'Assets/_Game/Scripts/Combat/HitboxDebug.cs (new, editor-only static toggle) + .meta'
  - 'Assets/_Game/Editor/HitboxTunerWindow.cs (new) + .meta'
  - 'Assets/_Game/Editor/HitboxDebugMenu.cs (new) + .meta'
  - 'Assets/_Game/Art/Characters/Monsters/EntityBase.controller (SMB on Attack state)'
  - 'Assets/_Game/Prefabs/Entities/Monsters/Monster_DarknessSpider Variant.prefab'
  - 'Assets/HEROIC FANTASY CREATURES FULL PACK VOL 1/Fantasy Animals Pack/Darkness Spider/FBX Files/DarknessSpider@CrawlBiteThreat.FBX.meta (events, written by the tuner)'
  - 'Assets/Tests/EditMode/HitWindowEventsTests.cs (new) + .meta'
  - 'Assets/Tests/EditMode/EntityMeleeAttackerTests.cs (new) + .meta'
  - 'Assets/_Game/Scripts/AI/CLAUDE.md'
  - 'Assets/_Game/Scripts/Combat/CLAUDE.md'
  - 'Assets/_Game/Scripts/Core/Animations/CLAUDE.md'
  - 'Assets/_Game/Prefabs/Entities/Monsters/CLAUDE.md'
code_patterns:
  - 'Animation-event-driven hit windows + StateMachineBehaviour exit safety net (mirrors player AnimationEventReceiver / SMB_AttackState)'
  - 'Owner-agnostic WeaponHitbox: SetOwner(Transform), Enable/Disable, OnHit(IDamageable, Vector3)'
  - 'Hit resolution: IDamageable.TryReceiveHit(attacker) → TakeDamage only on NotBlocked'
  - 'AI code references AIAnimationDriver only; Animator lives on the nested model child (CreatureVisual)'
  - 'Game.Editor asmdef for editor windows (UIElements, Tools/<Area>/ menu); #if UNITY_EDITOR only for editor utilities'
  - 'GameLog + TAG, [SerializeField] private, _camelCase, non-alloc physics, no per-frame allocations'
test_patterns:
  - 'EditMode NUnit in Assets/Tests/EditMode, assembly Tests.EditMode references Game (NOT Game.Editor) — testable logic must live in Game'
  - 'Pure static/plain-C# helpers tested directly; MonoBehaviours via new GameObject + AddComponent'
---

# Tech-Spec: AI Melee Hitboxes — Animation-Driven Entity Attacks & Hitbox Tuner

**Created:** 2026-09-29

## Overview

### Problem Statement

AI entities deal damage with a hitscan: `EntityBrain.ExecuteAttack()` triggers the attack animation
and, **on the same frame**, applies damage to the current target if it is within
`Entity.AttackRange`. Damage is disconnected from the animation (no wind-up, no real reach, cannot
be dodged spatially), and it is inconsistent with the player, whose attacks use the owner-agnostic
swept `WeaponHitbox` driven by `HitboxEnable` / `HitboxDisable` animation events (delivered by
`tech-spec-hit-detection-sweep-rework`).

There is also no tooling to place hitboxes on creature bones or to choose which frames of an attack
clip are active — today that means hand-editing FBX `.meta` clip events and eyeballing colliders.

### Solution

Introduce an owner-agnostic **AI melee attacker** component that owns one or more named
`WeaponHitbox`es (bone-attached for creatures, weapon-attached for future humanoids), opens/closes
them from animation events carrying the hitbox id, and resolves hits with the same
`TryReceiveHit → TakeDamage` sequence as the player (plus a faction-hostility filter).
`EntityBrain` stops applying damage directly and faces its target while attacking. Ship a
**Hitbox Tuner** editor window (scrub an attack clip on a prefab, see posed hitboxes and the swept
trail, add hitboxes to bones, write the enable/disable events) and **editor play-mode debug gizmos**
for live tuning.

### Scope

**In Scope:**
- `EntityMeleeAttacker` (shared, reusable later by humanoid NPCs wielding a weapon): named hitbox
  registry, window open/close by id, damage from `Entity.AttackDamage`, faction filter,
  `TryReceiveHit → TakeDamage`
- `EntityAnimationEventReceiver` on the Animator GO: `HitboxEnable(string id)` /
  `HitboxDisable(string id)` (empty id = all hitboxes), plus no-op `ComboWindowOpen/Close` so
  player attack clips can later be reused on humanoid NPCs without "no receiver" errors
- `SMB_EntityAttackState` on `EntityBase.controller`'s `Attack` state: exit → close all windows
- `EntityBrain`: hard switch (range check only starts the attack), face target while Attacking,
  close windows on death/disengage
- **DarknessSpider** migrated end to end: `Bite` hitbox on the fang bones + events on
  `DarknessSpider@CrawlBiteThreat`
- **Hitbox Tuner** EditorWindow (`Tools/Combat/Hitbox Tuner`)
- **Debug sweep gizmos** in editor play mode (`Tools/Combat/Show Hitbox Sweeps` toggle)
- EditMode tests for pure logic; CLAUDE.md updates

**Out of Scope:**
- Humanoid NPC combat implementation (`HumanoidAIAnimationDriver.TriggerAttack` stub, NPC weapon
  equipping/visuals, player-clip reuse) — follow-up spec that plugs into `EntityMeleeAttacker`
- **Wolf / Rat / Viper / Grunt:** their `EnemyType_*` SOs and override controllers are referenced by
  **no prefab or scene** (the TestScene wolf/rat/viper are pack demo prefabs without AI). Nothing to
  migrate; when their entity prefabs are created, the tuner authors their hitboxes/events.
  → The "hard switch" affects only the spider in practice; no live humanoid hostile exists.
- Clip/validation checker; monster block/dodge; hit reactions/hit-stop/VFX; runtime (build) debug
  overlay; AI attack selection between several attack types; new animations

## Context for Development

### Codebase Patterns

- User decisions (2026-09-29): architecture-only for humanoid AI; animation events (not a data
  asset) define hit windows; tooling = Hitbox Tuner + debug gizmos; hard switch (no hitscan fallback).
- **Player pipeline to mirror:** clip events → `AnimationEventReceiver` (same GO as Animator) →
  `PlayerCombat.OnHitboxEnable/Disable` → `WeaponHitbox.Enable/Disable`; `SMB_AttackState`
  enter/exit safety nets resolve the receiver via `animator.GetComponent<AnimationEventReceiver>()`.
- **`WeaponHitbox` (verified):** owner-agnostic; `SetOwner(Transform)` (also caches owner
  `IDamageable`); `Enable()` ignores re-enable while open; `Disable()`; sweeps in `LateUpdate`;
  `event Action<IDamageable, Vector3> OnHit` once per target per window; shape = disabled
  Box/Sphere/Capsule on the same GO; `_targetLayers` empty → CharacterHitbox (layer 7);
  forgiveness `_reachPadding` 0.1 / `_verticalReach` 0.6 / `_verticalReachRadius` 0.25;
  `OnDrawGizmosSelected` draws the vertical reach capsule. No faction filter (comment hook next to
  the owner check).
- **`EntityBrain` (verified):** `ExecuteAttack()` = `_animationDriver?.TriggerAttack()` +
  cooldown + immediate `TryReceiveHit` switch (`PerfectBlock`/`Blocked`/`Dodged` logs,
  `NotBlocked` → `TakeDamage(Entity.AttackDamage)`). `HandleAttack` stops the agent and **does not
  rotate toward the target** (only `HandleWarning` calls `FaceTarget()`, using
  `Entity.WarningTurnSpeed`). Once hits are spatial, a non-facing spider will whiff → must face.
  `TransitionToDead` / `DisengageFromCombat` are the exit paths. Required components are resolved in
  `Awake` with error + `enabled = false`.
- **Faction:** `FactionMember` (namespace `Game.AI`) on the entity root exposes `Faction`
  (`FactionSO`), `Damageable`; `FactionSO.IsHostileTo(FactionSO)`. Player uses `_factionOverride`.
- **Animator location:** `MonsterAnimationBridge._animator` → the Animator on `CreatureVisual`
  (nested `DarknessSpider_PBR` pack prefab root) — **not** the entity root. Animation events are
  delivered to MonoBehaviours on that GO → the receiver goes on `CreatureVisual`.
  `MonsterAnimationDriver.Awake` applies `Entity.AnimatorOverride` at runtime, so the prefab's
  Animator may show the base controller in edit mode.
- **Animator controller:** shared `Art/Characters/Monsters/EntityBase.controller` (states Idle,
  Walk, Run, Warning, Attack, GetHit, Death, Dead; params Speed, Attack, GetHit, Death, IsWarning);
  `Attack` state has no SMBs, exits at exit-time 1. Per-monster `AnimatorOverrideController`s in
  `Art/Characters/Monsters/Enemies/`.
- **Monster clips are FBX-embedded** (fileID 7400000 in the pack's `@Clip.FBX`), not `.anim`.
  Spider attack = `DarknessSpider@CrawlBiteThreat.FBX` (overrides base `FantasyWolf@Bite`), 21 frames
  (0–20), `loopTime: 1`, `clipAnimations` already populated, `events: []`. Events must be written
  through `ModelImporter.clipAnimations[i].events` + `SaveAndReimport()` (the tuner also supports
  `.anim` via `AnimationUtility.SetAnimationEvents` for player-style / future clips).
- **Spider bones** (under `CreatureVisual/.../DARKNESS_SPIDER_ROOT`): `cephalothorax`,
  `chelicere_L_a/b`, `chelicere_R_a/b`, `hook_L`, `hook_R` (fangs), `mandibul_*`, `leg_[LR]_[0-3]_[a-e]`,
  `Abdomen_0..3`. The variant already adds components on bones (ragdoll) as prefab-instance
  overrides — adding a child GO under a bone is the same mechanism.
  `MonsterAnimationDriver` collects `GetComponentsInChildren<Rigidbody>` under the Animator for the
  ragdoll → hitbox GOs must **not** carry a Rigidbody.
- `EnemyType_DarknessSpider`: `AttackDamage 10`, `AttackRange 2.2`, `AttackCooldown 2`.
- **Editor code:** `Assets/_Game/Editor/` = assembly `Game.Editor` (refs `Game`, editor-only),
  namespace `Game.Editor`, UIElements windows, menus under `Tools/<Area>/…`
  (`WeaponCreatorWindow` is the style reference). `Tests.EditMode` does **not** reference
  `Game.Editor` → testable logic goes in `Game` (runtime assembly, no `UnityEditor` usage).
- **Rules (project-context):** no C# `event Action` across system boundaries (WeaponHitbox.OnHit is
  consumed by the owning attacker on the same entity — same pattern as PlayerCombat, accepted);
  `#if UNITY_EDITOR` only for editor utilities; non-alloc physics; `Game.DevTools` namespace for
  `Scripts/Debug` (not used — debug gizmos are editor-only and live next to `WeaponHitbox`).

### Files to Reference

| File | Purpose |
| ---- | ------- |
| `Assets/_Game/Scripts/Combat/WeaponHitbox.cs` | Hitbox reused as-is (+ editor-only sweep recording). GUID `b3698d187bb02ff4f9d9218ca603f3fb` — edit in place |
| `Assets/_Game/Scripts/Combat/HitSweepTracker.cs` | Dedupe / sub-step logic (unchanged) |
| `Assets/_Game/Scripts/Combat/AnimationEventReceiver.cs`, `SMB_AttackState.cs` | Player-side patterns to mirror |
| `Assets/_Game/Scripts/Combat/PlayerCombat.cs` (`BindWeaponHitbox`, `OnWeaponHit`) | Reference bind + hit resolution |
| `Assets/_Game/Scripts/Combat/IDamageable.cs` | `IsDead`, `TakeDamage`, `TryReceiveHit(GameObject)` → `HitResult` |
| `Assets/_Game/Scripts/AI/EntityBrain.cs` | `ExecuteAttack` (L312-337), `HandleAttack`, `FaceTarget`, `TransitionToDead`, `DisengageFromCombat` |
| `Assets/_Game/Scripts/AI/FactionMember.cs`, `ScriptableObjects/Factions/FactionSO.cs` | Faction filter |
| `Assets/_Game/Scripts/AI/EntityHealth.cs` | `TakeDamage` → `TriggerGetHit`; death → `TriggerDeath` |
| `Assets/_Game/Scripts/Core/Animations/MonsterAnimationDriver.cs`, `MonsterAnimationBridge.cs` | Animator ownership, override application, ragdoll Rigidbody scan |
| `Assets/_Game/ScriptableObjects/Entities/Entity.cs` | `AttackDamage`, `AttackRange`, `AttackCooldown`, `WarningTurnSpeed`, `AnimatorOverride` |
| `Assets/_Game/Art/Characters/Monsters/EntityBase.controller` | `Attack` state gets the SMB |
| `Assets/_Game/Art/Characters/Monsters/Enemies/DarknessSpider.overrideController` | Attack clip → `DarknessSpider@CrawlBiteThreat` |
| `Assets/_Game/Prefabs/Entities/Monsters/Monster_DarknessSpider Variant.prefab` | Add attacker, receiver, Bite hitbox |
| `Assets/_Game/Editor/WeaponCreatorWindow.cs` | EditorWindow style reference (UIElements, TAG, menu path) |
| `Assets/Tests/EditMode/HitSweepTrackerTests.cs` | Test style reference |

### Technical Decisions

1. **New AI-side classes live in `Scripts/AI` / `Game.AI`** (`EntityMeleeAttacker`,
   `EntityAnimationEventReceiver`, `SMB_EntityAttackState`) — they need `FactionMember` and are
   AI-specific; `Game.Combat` stays free of `Game.AI` dependencies. `WeaponHitbox` is unchanged
   functionally.
2. **Named hitboxes.** `EntityMeleeAttacker` serializes `List<NamedHitbox { string Id; WeaponHitbox
   Hitbox; }>`; `OpenWindow(id)` / `CloseWindow(id)`; empty/null id = all. Runtime
   `RegisterHitbox(id, hitbox)` / `UnregisterHitbox(id)` for future equipped weapons (humanoids).
   Unknown id → `GameLog.Warn` once per id.
3. **Event signature:** `HitboxEnable(string)` / `HitboxDisable(string)` on a **separate receiver
   class** (no overload clash with the player's parameterless methods). A clip event without a
   string parameter delivers `""` → "all hitboxes", so the player's sword clips work unchanged on
   future humanoid NPCs.
4. **Damage flow:** `EntityBrain.ExecuteAttack()` → `_meleeAttacker.BeginAttack(Entity.AttackDamage)`
   (stores damage, closes any open window) → `TriggerAttack()`. Hits: faction filter (target's
   `FactionMember` must be hostile to owner faction; targets without `FactionMember` are ignored) →
   `TryReceiveHit(owner GO)` → `TakeDamage` on `NotBlocked`; other results logged (same messages as
   today). `EndAttack()` closes all windows.
5. **Safety nets:** `SMB_EntityAttackState.OnStateExit` → receiver → `EndAttack()` (covers GetHit
   interrupt, Death, normal end); `EntityBrain.TransitionToDead` and `DisengageFromCombat` also call
   `EndAttack()`. Hard switch: if `EntityMeleeAttacker` is missing, brain logs `GameLog.Error` once
   in `Awake` and attacks still animate but deal no damage (no hitscan fallback).
6. **Facing:** `HandleAttack` calls `FaceTarget()` each frame (existing helper, reuses
   `Entity.WarningTurnSpeed`).
7. **Hitbox Tuner** (`Game.Editor.HitboxTunerWindow`, `Tools/Combat/Hitbox Tuner`): target =
   selected GameObject in a scene or Prefab Mode that has an Animator in its hierarchy; clip list =
   `Entity.AnimatorOverride.animationClips` (via `PersistentID`) else
   `Animator.runtimeAnimatorController.animationClips`; frame slider sampled with
   `AnimationMode.StartAnimationMode` + `AnimationMode.SampleAnimationClip` (non-destructive,
   stopped on close/clip change); Scene-view `Handles` draw every `WeaponHitbox` shape at the
   current frame and the swept trail (shape centres per frame) across the authored window; window
   start/end frame + hitbox id fields; "Add Hitbox To Selected Bone" (child GO `Hitbox_<id>`,
   disabled trigger `SphereCollider` r=0.15, `WeaponHitbox`, registered in the root
   `EntityMeleeAttacker`, `Undo` registered); "Write Events" → `HitWindowEvents.Apply(...)` then
   writes via ModelImporter (FBX) or `AnimationUtility.SetAnimationEvents` (`.anim`).
8. **`HitWindowEvents` (pure, `Game.Combat`)**: frame↔time conversion from `frameRate`; builds the
   new event array by removing existing `HitboxEnable/HitboxDisable` events whose string == id and
   inserting the new pair, preserving all other events, sorted by time; validation (start < end,
   within clip length). Works on `AnimationEvent[]` for `.anim` and is mapped to/from
   `ClipAnimationInfoEvent` in the editor for FBX. → EditMode test surface.
9. **Debug sweeps:** `HitboxDebug.DrawSweeps` static bool (`Game.Combat`, `#if UNITY_EDITOR`),
   persisted via `EditorPrefs` by `HitboxDebugMenu` (`Tools/Combat/Show Hitbox Sweeps`, checkmark).
   `WeaponHitbox` (editor only) records sampled shape centres/radii + hit points into a fixed-size
   ring buffer while a window is open and draws them in `OnDrawGizmos` (open window = yellow,
   recent samples fade over ~1 s, hits = red spheres). Zero cost in builds.
10. **Spider:** one `Bite` hitbox (sphere) under `cephalothorax`, positioned between `hook_L` /
    `hook_R` via the tuner; events authored with the tuner on `CrawlBiteThreat` (window picked
    visually, expected around the lunge frames). `AttackRange` retune allowed if the bite reach
    can't cover 2.2 m (record final values).
11. **Vendor asset note:** events live in the pack's FBX `.meta` — re-importing/updating the
    HEROIC FANTASY pack would wipe them. Documented in Monsters CLAUDE.md.


## Implementation Plan

### Tasks

- [x] Task 1: Expose the hit-window state on `WeaponHitbox` and add editor-only sweep recording
  - File: `Assets/_Game/Scripts/Combat/WeaponHitbox.cs` (edit in place — keep GUID
    `b3698d187bb02ff4f9d9218ca603f3fb`)
  - Action:
    1. Add `public bool IsWindowOpen => _tracker.IsWindowOpen;` (used by `EntityMeleeAttacker` and tests).
    2. Add an editor-only debug recorder, entirely inside `#if UNITY_EDITOR`:
       ```csharp
       private struct DebugSample { public Vector3 Center; public float Radius; public float Time; public bool Hit; }
       private const int DEBUG_SAMPLE_CAPACITY = 64;
       private const float DEBUG_SAMPLE_LIFETIME = 1f;
       private readonly DebugSample[] _debugSamples = new DebugSample[DEBUG_SAMPLE_CAPACITY];
       private int _debugSampleHead;
       private void RecordDebugSample(Vector3 center, float radius, bool hit) { /* ring-buffer write, Time = UnityEngine.Time.time */ }
       ```
       Call `RecordDebugSample` at the end of `SampleAt` (centre + a bounding radius of the queried
       shape: sphere radius / capsule half-segment + radius / box half-extents magnitude) and in
       `ProcessOverlaps` right after `OnHit?.Invoke` (hit point, radius 0.05, `hit = true`). Guard
       both calls with `if (HitboxDebug.DrawSweeps)` (inside `#if UNITY_EDITOR`).
    3. Add `private void OnDrawGizmos()` inside `#if UNITY_EDITOR`: return unless
       `Application.isPlaying && HitboxDebug.DrawSweeps`. Draw each sample younger than
       `DEBUG_SAMPLE_LIFETIME` as a wire sphere; colour red for hits, yellow for samples, alpha
       fading linearly with age. If `IsWindowOpen`, draw the current shape centre as a small solid
       yellow sphere.
  - Notes: No behaviour change in builds. No allocations (fixed array). Do not touch sweep logic.

- [x] Task 2: Create the editor-only debug toggle
  - File: `Assets/_Game/Scripts/Combat/HitboxDebug.cs` (new, namespace `Game.Combat`)
  - Action: Whole file wrapped in `#if UNITY_EDITOR`:
    `public static class HitboxDebug { public static bool DrawSweeps; }` with an XML summary
    ("editor play-mode sweep gizmos, toggled from Tools/Combat/Show Hitbox Sweeps").
  - Notes: Editor-only static; reset on domain reload and restored by Task 9's
    `[InitializeOnLoadMethod]`.

- [x] Task 3: Create the pure event-list logic `HitWindowEvents`
  - File: `Assets/_Game/Scripts/Combat/HitWindowEvents.cs` (new, namespace `Game.Combat`,
    `public static class`, no `UnityEditor` usage)
  - Action: Implement:
    ```csharp
    public const string ENABLE_FUNCTION  = "HitboxEnable";
    public const string DISABLE_FUNCTION = "HitboxDisable";

    // Seconds for a frame of a .anim clip (AnimationEvent.time for .anim is in seconds). frameRate <= 0 → 0.
    public static float FrameToSeconds(int frame, float frameRate);
    // Normalized [0..1] time for an FBX clip (ModelImporterClipAnimation events are normalized over
    // firstFrame..lastFrame). frame is relative to firstFrame. lastFrame <= firstFrame → 0.
    public static float FrameToNormalized(int frame, float firstFrame, float lastFrame);
    public static int   NormalizedToFrame(float normalized, float firstFrame, float lastFrame); // rounded, relative
    public static int   SecondsToFrame(float seconds, float frameRate);                          // rounded

    // 0 <= startFrame < endFrame <= frameCount - 1. Returns false + human-readable reason.
    public static bool IsValidWindow(int startFrame, int endFrame, int frameCount, out string reason);

    // New array: every event of `existing` except HitboxEnable/HitboxDisable whose stringParameter
    // == hitboxId (null treated as ""), plus Enable at startTime and Disable at endTime
    // (stringParameter = hitboxId), stable-sorted by time. Other events copied untouched.
    public static AnimationEvent[] ApplyWindow(AnimationEvent[] existing, string hitboxId, float startTime, float endTime);

    // Removes the Enable/Disable events for hitboxId; others untouched. Null input → empty array.
    public static AnimationEvent[] RemoveWindow(AnimationEvent[] existing, string hitboxId);

    // First Enable for hitboxId and the first Disable for it at or after that time. False if either is absent.
    public static bool TryGetWindow(AnimationEvent[] events, string hitboxId, out float startTime, out float endTime);
    ```
  - Notes: Time-unit agnostic — callers convert (seconds for `.anim`, normalized for FBX). Copy
    `AnimationEvent`s field by field (`time`, `functionName`, `stringParameter`, `floatParameter`,
    `intParameter`, `objectReferenceParameter`, `messageOptions`) — never mutate the input array or
    its elements. No `GameLog` → no `TAG`.

- [x] Task 4: Create `EntityMeleeAttacker`
  - File: `Assets/_Game/Scripts/AI/EntityMeleeAttacker.cs` (new, namespace `Game.AI`)
  - Action: MonoBehaviour on the entity root (same GO as `EntityBrain` / `FactionMember`):
    ```csharp
    private const string TAG = "[AI]";
    [System.Serializable] public struct NamedHitbox { public string Id; public WeaponHitbox Hitbox; }
    [Tooltip("Hitboxes opened by HitboxEnable(id) animation events. An event with an empty id opens all.")]
    [SerializeField] private List<NamedHitbox> _hitboxes = new();
    [SerializeField] private FactionMember _selfFactionMember;   // auto-resolved in Awake if null

    public float CurrentDamage { get; private set; }
    public bool IsAttacking { get; private set; }
    public IReadOnlyList<NamedHitbox> Hitboxes => _hitboxes;
    private readonly HashSet<string> _warnedUnknownIds = new();
    ```
    - `Awake`: resolve `_selfFactionMember` via `GetComponent` if null (warn if still null). For each
      entry: skip null `Hitbox` with `GameLog.Warn` naming the id; `Hitbox.SetOwner(transform)`;
      `Hitbox.OnHit += HandleHit`. Warn on duplicate ids.
    - `OnDestroy`: unsubscribe every hitbox.
    - `public void RegisterHitbox(string id, WeaponHitbox hitbox)` / `public void UnregisterHitbox(string id)`:
      runtime add/remove (future equipped weapons) — register = `SetOwner` + subscribe + add;
      unregister = `Disable()` + unsubscribe + remove. Null guards; duplicate id on register → warn and ignore.
    - `public void BeginAttack(float damage)`: `CloseAllWindows()`; `CurrentDamage = damage`; `IsAttacking = true`.
    - `public void EndAttack()`: `CloseAllWindows()`; `IsAttacking = false`.
    - `public void OpenWindow(string id)`: if `!IsAttacking` → return (event outside an attack is
      ignored). Null/empty id → `Enable()` every hitbox; else the entry with that id; unknown id →
      `GameLog.Warn` once per id.
    - `public void CloseWindow(string id)`: same matching, `Disable()`. Unknown id: silent.
    - `private void HandleHit(IDamageable target, Vector3 hitPoint)`:
      ```csharp
      if (target == null || target.IsDead || !IsAttacking) return;
      if (!IsHostileTarget(target)) return;
      HitResult result = target.TryReceiveHit(gameObject);
      // PerfectBlock / Blocked / Dodged → GameLog.Info with today's EntityBrain.ExecuteAttack messages
      // NotBlocked → target.TakeDamage(CurrentDamage); GameLog.Info($"{name} hit landed at {hitPoint}")
      ```
    - `private bool IsHostileTarget(IDamageable target)`: `target as Component` null → false; get
      `FactionMember` on its GO (fallback `GetComponentInParent`); no member → false;
      `_selfFactionMember == null || _selfFactionMember.Faction == null` → true; else
      `_selfFactionMember.Faction.IsHostileTo(member.Faction)`.
    - `private void OnDisable()` → `EndAttack()`.
    - `#if UNITY_EDITOR public void EditorAddHitbox(string id, WeaponHitbox hitbox)` — appends to the
      serialized list (caller records Undo). Used by the tuner.
  - Notes: Damage comes from the brain (Entity SO), not the hitbox. Dedupe stays in `WeaponHitbox`
    (one hit per target per window; two hitboxes open at once can each hit the same target once —
    documented). XML summary explains the humanoid reuse path (register the equipped weapon's
    `WeaponHitbox`, e.g. id `Weapon`; player clips' parameterless events open all).

- [x] Task 5: Create `EntityAnimationEventReceiver`
  - File: `Assets/_Game/Scripts/AI/EntityAnimationEventReceiver.cs` (new, namespace `Game.AI`)
  - Action: MonoBehaviour placed on the **Animator's GameObject**:
    ```csharp
    private const string TAG = "[AI]";
    [SerializeField] private EntityMeleeAttacker _attacker;   // auto: GetComponentInParent in Awake
    public void HitboxEnable(string id)  { if (_attacker == null) return; _attacker.OpenWindow(id); }
    public void HitboxDisable(string id) { if (_attacker == null) return; _attacker.CloseWindow(id); }
    public void NotifyAttackExited()     { if (_attacker == null) return; _attacker.EndAttack(); }
    // Player attack clips also fire these — no-ops so reused clips don't log "no receiver".
    public void ComboWindowOpen()  { }
    public void ComboWindowClose() { }
    ```
    `Awake`: resolve `_attacker` via `GetComponentInParent<EntityMeleeAttacker>()` if null; `GameLog.Warn` if still null.
  - Notes: Separate class from the player's `AnimationEventReceiver` → no overload clash between
    `HitboxEnable()` and `HitboxEnable(string)`. An event with no string parameter delivers `""`.

- [x] Task 6: Create `SMB_EntityAttackState`
  - File: `Assets/_Game/Scripts/AI/SMB_EntityAttackState.cs` (new, namespace `Game.AI`)
  - Action: `StateMachineBehaviour`; cache `EntityAnimationEventReceiver` via
    `animator.GetComponent<>()` (lazy, like `SMB_AttackState`); `OnStateExit` → `NotifyAttackExited()`.

- [x] Task 7: Rewire `EntityBrain` (hard switch + facing + safety closes)
  - File: `Assets/_Game/Scripts/AI/EntityBrain.cs`
  - Action:
    1. Add `[Tooltip("Resolves attack hits from animation-driven hit windows.")] [SerializeField] private EntityMeleeAttacker _meleeAttacker;`
       below `_animationDriver`. In `Awake` (after the FactionMember block): if null →
       `GetComponent<EntityMeleeAttacker>()`; if still null →
       `GameLog.Error(TAG, $"{gameObject.name}: no EntityMeleeAttacker — attacks will deal no damage")`
       (do **not** disable the brain).
    2. Replace the `ExecuteAttack()` body:
       ```csharp
       if (_meleeAttacker != null) _meleeAttacker.BeginAttack(_persistentID.Entity.AttackDamage);
       _animationDriver?.TriggerAttack();
       _attackCooldownTimer = _persistentID.Entity.AttackCooldown;
       GameLog.Info(TAG, $"{gameObject.name} attacks {_currentTarget.Transform.name}");
       ```
       Remove the `TryReceiveHit` switch (moved to `EntityMeleeAttacker.HandleHit`).
    3. `HandleAttack()`: after the two range checks and before the cooldown check, call `FaceTarget()`.
    4. `TransitionToDead()` and `DisengageFromCombat()`: `if (_meleeAttacker != null) _meleeAttacker.EndAttack();`.
  - Notes: Keep `using Game.Combat;` (`IDamageable` still used). `FaceTarget` reuses
    `Entity.WarningTurnSpeed` — documented in AI CLAUDE.md.

- [x] Task 8: Add the SMB to the shared monster controller
  - File: `Assets/_Game/Art/Characters/Monsters/EntityBase.controller`
  - Action: Via Unity MCP `execute_code`: load the `AnimatorController`, find state `Attack` on
    `Base Layer`, `AddStateMachineBehaviour<Game.AI.SMB_EntityAttackState>()` (skip if already
    present), `EditorUtility.SetDirty` + `AssetDatabase.SaveAssets()`. Check `read_console`.
  - Notes: Shared by all four override controllers — every monster gets the safety net. Do not
    hand-edit the controller YAML.

- [x] Task 9: Debug sweeps menu toggle
  - File: `Assets/_Game/Editor/HitboxDebugMenu.cs` (new, namespace `Game.Editor`)
  - Action: `static class` with `const string MENU = "Tools/Combat/Show Hitbox Sweeps"`,
    `const string PREF_KEY = "Game.HitboxDebug.DrawSweeps"`; `[MenuItem(MENU)] Toggle()` flips
    `HitboxDebug.DrawSweeps` and saves `EditorPrefs.SetBool`; `[MenuItem(MENU, true)] Validate()`
    calls `Menu.SetChecked(MENU, HitboxDebug.DrawSweeps)` and returns true;
    `[InitializeOnLoadMethod] Restore()` reads the pref.

- [x] Task 10: Hitbox Tuner window
  - File: `Assets/_Game/Editor/HitboxTunerWindow.cs` (new, namespace `Game.Editor`, `EditorWindow`,
    UIElements like `WeaponCreatorWindow`, `private const string TAG = "[HitboxTuner]"`)
  - Action:
    1. `[MenuItem("Tools/Combat/Hitbox Tuner")]`. **Target root** = "Use Selection" on the current
       `Selection.activeGameObject` → walk up to the outermost parent that (a) is the prefab-stage
       root or scene root object and (b) has an `Animator` in children. Resolve: `Animator` (first
       in children), `EntityMeleeAttacker` on the root (optional — help box + "Add
       EntityMeleeAttacker" button with Undo), all `WeaponHitbox`es in children.
    2. **Clip picker:** `DropdownField` of distinct clips from `PersistentID.Entity.AnimatorOverride.animationClips`
       when present (via `root.GetComponent<PersistentID>()`), else
       `animator.runtimeAnimatorController.animationClips`; plus an `ObjectField` override for any
       `AnimationClip`. Label shows source kind: FBX (`AssetImporter.GetAtPath` is `ModelImporter`)
       or `.anim`.
    3. **Scrubbing:** `frameCount = Mathf.RoundToInt(clip.length * clip.frameRate) + 1`; `SliderInt`
       0..frameCount-1 + ◀/▶ step buttons + "Loop window" toggle (advances start→end on
       `EditorApplication.update` at clip frame rate). On change: start `AnimationMode` if needed,
       `AnimationMode.BeginSampling()`, `AnimationMode.SampleAnimationClip(animator.gameObject, clip, frame / clip.frameRate)`,
       `AnimationMode.EndSampling()`, `SceneView.RepaintAll()`. "Stop Preview" button + automatic
       `AnimationMode.StopAnimationMode()` in `OnDisable`, on target/clip change, on
       `EditorApplication.playModeStateChanged` (ExitingEditMode) and `AssemblyReloadEvents.beforeAssemblyReload`.
    4. **Window authoring:** hitbox id `DropdownField` (attacker ids + "(all)" = `""`); start/end
       `IntegerField`s + "Start = current" / "End = current" buttons; pre-filled from existing events
       via `HitWindowEvents.TryGetWindow` (converted with `NormalizedToFrame` for FBX,
       `SecondsToFrame` for `.anim`); `HitWindowEvents.IsValidWindow` → error help box and Write
       button disabled when invalid.
    5. **Write Events:**
       - FBX: `var importer = (ModelImporter)AssetImporter.GetAtPath(path)`;
         `var clips = importer.clipAnimations; if (clips.Length == 0) clips = importer.defaultClipAnimations;`
         find `clips[i].name == clip.name` (error help box if not found);
         `clips[i].events = HitWindowEvents.ApplyWindow(clips[i].events, id,
         HitWindowEvents.FrameToNormalized(start, clips[i].firstFrame, clips[i].lastFrame),
         HitWindowEvents.FrameToNormalized(end, clips[i].firstFrame, clips[i].lastFrame));`
         `importer.clipAnimations = clips; importer.SaveAndReimport();` then re-resolve the clip
         (`AssetDatabase.LoadAllAssetsAtPath(path)` by name).
       - `.anim`: `Undo.RecordObject(clip, "Write Hit Window")`;
         `AnimationUtility.SetAnimationEvents(clip, HitWindowEvents.ApplyWindow(AnimationUtility.GetAnimationEvents(clip), id,
         HitWindowEvents.FrameToSeconds(start, clip.frameRate), HitWindowEvents.FrameToSeconds(end, clip.frameRate)))`;
         `EditorUtility.SetDirty(clip); AssetDatabase.SaveAssets();`.
       - "Remove Window" → `HitWindowEvents.RemoveWindow` through the same two paths.
       - `GameLog.Info(TAG, ...)` on success with clip, id and frames.
    6. **Add Hitbox To Selected Bone:** enabled when `Selection.activeTransform` is inside the
       target, an attacker exists and a new non-empty id is typed (`TextField`). Creates child
       `Hitbox_<id>` (layer Default, local pos/rot zero, scale one) with
       `SphereCollider { isTrigger = true, radius = 0.15f, enabled = false }` + `WeaponHitbox`;
       `Undo.RegisterCreatedObjectUndo`; `Undo.RecordObject(attacker, ...)` +
       `attacker.EditorAddHitbox(id, hitbox)`; `PrefabUtility.RecordPrefabInstancePropertyModifications(attacker)`;
       select the new GO so it can be placed/resized with standard gizmos. Reject duplicate ids.
    7. **Scene view drawing** (`SceneView.duringSceneGui`, subscribed in `OnEnable`, removed in
       `OnDisable`): every `WeaponHitbox` under the target drawn at the current pose with `Handles`
       from its collider's local data × `lossyScale` (selected id = cyan, others = grey, label = id)
       + its `_verticalReach` capsule (orange; read via `SerializedObject`). When a valid window is
       set for the selected id and preview is active: compute the **swept trail** by sampling every
       frame in [start, end] and recording the hitbox shape centre (cache keyed by clip + id + start
       + end; invalidate on change), re-sample the current frame, then draw a yellow polyline with
       small discs and wire shapes at the start and end frames. With "(all)" selected, draw trails
       for every hitbox.
    8. Info help box: "Events on FBX clips from a vendor pack are stored in its .meta — re-importing
       the pack wipes them."
  - Notes: Editor only (`Game.Editor` asmdef). Null-check target/animator/clip on every GUI tick
    and reset gracefully (target deleted, prefab stage closed). Undo on every mutation.

- [x] Task 11: Wire the DarknessSpider
  - Files: `Assets/_Game/Prefabs/Entities/Monsters/Monster_DarknessSpider Variant.prefab`;
    `Assets/HEROIC FANTASY CREATURES FULL PACK VOL 1/Fantasy Animals Pack/Darkness Spider/FBX Files/DarknessSpider@CrawlBiteThreat.FBX.meta`
  - Action (Unity MCP / `PrefabUtility.LoadPrefabContents` + `SaveAsPrefabAsset` on the same path — never raw YAML):
    1. Add `EntityMeleeAttacker` on the prefab root; assign `_selfFactionMember`; assign it to
       `EntityBrain._meleeAttacker`.
    2. Add `EntityAnimationEventReceiver` on `CreatureVisual` (the Animator GO); assign `_attacker`.
    3. Create `Hitbox_Bite` under the `cephalothorax` bone: disabled trigger `SphereCollider`,
       `WeaponHitbox`; local position = midpoint of `hook_L` / `hook_R` in `cephalothorax` space;
       radius = max(0.15, half the hook spacing + 0.05) (compensate for bone `lossyScale`);
       `_reachPadding = 0.1`, `_verticalReach = 0.3`. Register as `Bite` in `_hitboxes`.
    4. Events on `CrawlBiteThreat` (frames 0–20): with the tuner, pick the lunge frames (fangs moving
       forward → recoil start) and write `HitboxEnable("Bite")` / `HitboxDisable("Bite")`. In an
       MCP-only session, write them via `execute_code` (same `ModelImporter` path as Task 10.5 using
       `HitWindowEvents.ApplyWindow`) with a provisional window of frames 8→14, and flag it for
       manual tuning in the review notes.
    5. Reach check: at the lunge frame, measure horizontal distance root → `Hitbox_Bite` centre +
       radius + padding + 0.3 (player hurtbox radius). If < `EnemyType_DarknessSpider.AttackRange`
       (2.2), set `AttackRange` = that reach − 0.1 and ensure `EngageStoppingDistance` ≤ `AttackRange`.
       Record final values.
  - Notes: No Rigidbody on the hitbox GO (ragdoll scan). Use `refresh_unity(mode="if_dirty")` only.

- [x] Task 12: EditMode tests
  - Files: `Assets/Tests/EditMode/HitWindowEventsTests.cs` (new), `Assets/Tests/EditMode/EntityMeleeAttackerTests.cs` (new)
  - Action — `HitWindowEventsTests`:
    - `FrameToSeconds_30fps_Frame15_ReturnsHalfSecond`; `FrameToSeconds_ZeroFrameRate_ReturnsZero`
    - `FrameToNormalized_MidFrame_ReturnsHalf` (range 0..20, frame 10 → 0.5); `FrameToNormalized_DegenerateRange_ReturnsZero`
    - `NormalizedToFrame_RoundTrip_ReturnsSameFrame`; `SecondsToFrame_RoundTrip_ReturnsSameFrame`
    - `IsValidWindow_StartNotBeforeEnd_False`; `IsValidWindow_EndBeyondClip_False`; `IsValidWindow_NegativeStart_False`; `IsValidWindow_Valid_True`
    - `ApplyWindow_NullInput_AddsSortedPair`
    - `ApplyWindow_ReplacesExistingPairForSameId`
    - `ApplyWindow_KeepsPairsOfOtherIds`
    - `ApplyWindow_KeepsUnrelatedEvents_WithAllParameters` (`ComboWindowOpen` with float/int params untouched)
    - `ApplyWindow_NullId_TreatedAsEmpty`
    - `ApplyWindow_DoesNotMutateInput`
    - `RemoveWindow_RemovesOnlyMatchingId`
    - `TryGetWindow_ReturnsTimes`; `TryGetWindow_Absent_ReturnsFalse`
  - Action — `EntityMeleeAttackerTests` (`[SetUp]` builds an attacker GO + child hitbox GOs, each
    with `SphereCollider` then `WeaponHitbox`; add hitboxes via `RegisterHitbox`;
    `Object.DestroyImmediate` in `[TearDown]`):
    - `OpenWindow_WhenNotAttacking_DoesNotOpen`
    - `OpenWindow_WithId_OpensOnlyThatHitbox`
    - `OpenWindow_EmptyId_OpensAll`
    - `OpenWindow_UnknownId_DoesNotThrow`
    - `EndAttack_ClosesAllWindows`
    - `BeginAttack_ClosesOpenWindows_AndStoresDamage`
    - `UnregisterHitbox_ClosesAndRemoves`
  - Notes: `WeaponHitbox.Enable/Disable/IsWindowOpen` depend only on the tracker, so they work
    without `Awake`. Use `LogAssert.Expect` for the expected warnings (no-owner / unknown id) or
    `LogAssert.ignoreFailingMessages = true` scoped to the test. Full EditMode suite must stay green
    (Unity MCP `run_tests`).

- [x] Task 13: Documentation
  - `Assets/_Game/Scripts/AI/CLAUDE.md`: rows for `EntityMeleeAttacker`,
    `EntityAnimationEventReceiver`, `SMB_EntityAttackState`; rules: "AI damage only through
    `EntityMeleeAttacker` hit windows — `EntityBrain` never calls `TakeDamage`", "receiver lives on
    the Animator GO", "brain faces target while Attacking (reuses `WarningTurnSpeed`)", humanoid reuse
    path. Checklist: HIGH "attacking entity without `EntityMeleeAttacker` or receiver → deals no
    damage"; MEDIUM "hitbox GO under a ragdoll bone carrying a Rigidbody".
  - `Assets/_Game/Scripts/Combat/CLAUDE.md`: `WeaponHitbox` shared with AI, `IsWindowOpen`,
    `HitWindowEvents`, debug sweeps toggle, event naming (`HitboxEnable(string id)` for entities,
    parameterless for the player).
  - `Assets/_Game/Scripts/Core/Animations/CLAUDE.md`: `EntityBase.controller` `Attack` state carries
    `SMB_EntityAttackState`; new monster base controllers must keep it.
  - `Assets/_Game/Prefabs/Entities/Monsters/CLAUDE.md`: monster attack recipe (attacker on root,
    receiver on Animator GO, `Hitbox_<id>` under a bone, events via Hitbox Tuner); vendor FBX
    `.meta` warning; spider Bite values (bone, local position, radius, window frames, AttackRange).

### Acceptance Criteria

- [ ] AC 1: Given a hostile spider in attack range facing the player, when its bite reaches the
  authored window and the fangs' swept volume overlaps the player's hurtbox, then the player loses
  `AttackDamage` HP exactly once for that bite.
- [ ] AC 2: Given the spider has started a bite, when the player leaves the fangs' swept volume
  before the window opens, then no damage is dealt.
- [ ] AC 3: Given the bite window is open, when the player is dodging (i-frames) or blocking, then
  `TryReceiveHit` returns Dodged/Blocked/PerfectBlock, no damage is dealt and the result is logged.
- [ ] AC 4: Given the spider is in Attacking and not facing the player, when frames pass, then it
  rotates toward the player at `WarningTurnSpeed`.
- [ ] AC 5: Given a bite is in progress, when the spider is hit (GetHit) or dies before the window
  closes, then all hit windows close and no damage is dealt afterwards.
- [ ] AC 6: Given a same-faction spider inside the bite volume, when the window is open, then it
  takes no damage.
- [ ] AC 7: Given `IsAttacking == false`, when a `HitboxEnable` event fires, then no hitbox opens.
- [ ] AC 8: Given no `Tail` hitbox is registered, when `HitboxEnable("Tail")` fires repeatedly, then
  exactly one warning is logged and nothing throws.
- [ ] AC 9: Given an entity with `EntityBrain` but no `EntityMeleeAttacker`, when it awakes, then
  `GameLog.Error` names it, the brain keeps running, and its attacks deal no damage.
- [ ] AC 10: Given the Hitbox Tuner targets the spider prefab in Prefab Mode, when I pick
  `CrawlBiteThreat` and move the frame slider, then the spider poses at that frame and the `Bite`
  hitbox is drawn at the posed fang position; Stop Preview restores the original pose and the
  prefab is not dirtied by the preview.
- [ ] AC 11: Given a valid window, when I press Write Events, then the FBX clip's importer events
  contain exactly one `HitboxEnable("Bite")` / `HitboxDisable("Bite")` pair at the matching
  normalized times, other events and clip settings are preserved, and reopening the tuner pre-fills
  the same frames.
- [ ] AC 12: Given start ≥ end or a frame outside the clip, when shown in the tuner, then an error
  help box explains it and Write Events is disabled.
- [ ] AC 13: Given a bone selected under the target and id `Tail`, when I press Add Hitbox To
  Selected Bone, then `Hitbox_Tail` (disabled trigger sphere + `WeaponHitbox`) is created and
  registered as `Tail`, and one Undo removes both the GO and the registration.
- [ ] AC 14: Given a valid window for the selected id and an active preview, when viewing the Scene,
  then the hitbox centre's swept trail across the window frames is drawn.
- [ ] AC 15: Given `Tools/Combat/Show Hitbox Sweeps` is checked, when player or spider attacks run in
  play mode, then sweep samples are drawn fading over ~1 s and hits are red; unchecked → nothing is
  drawn; the checkmark survives a domain reload.
- [ ] AC 16: Given the player's attacks, when played after this change, then they behave exactly as
  before (no `PlayerCombat` / `WeaponHitbox` regression).
- [ ] AC 17: Given the EditMode suite, when run, then `HitWindowEventsTests`,
  `EntityMeleeAttackerTests` and all existing tests pass.

## Additional Context

### Dependencies

- Builds on `tech-spec-hit-detection-sweep-rework` (implementation-complete, uncommitted in the
  working tree): `WeaponHitbox` sweeps, `HitSweepTracker`, the Player `Hitbox` hurtbox on layer 7 —
  required for AI hits on the player.
- No new packages. UnityEditor APIs: `AnimationMode`, `AnimationUtility`, `ModelImporter`,
  `Handles`, `Undo`, `PrefabUtility`, `EditorPrefs`.
- Unity Editor + MCP connection for Tasks 8, 11 and `run_tests`.
- Order: 1 → 2 → 3 (Combat, one compile) → 4 → 5 → 6 → 7 (AI, one compile) → 8 → 9 → 10 (editor)
  → 11 (needs 4–10) → 12 → 13.

### Testing Strategy

- **EditMode (automated):** Task 12 — event-list logic (conversions, replace/keep/remove,
  immutability, window lookup) and attacker window routing. Full suite via Unity MCP `run_tests`.
- **Manual play-mode checklist (spider in TestScene / StartingTown):**
  1. Let a spider engage; stand still → one hit per bite, timed with the fang lunge (AC 1).
  2. Back off during the wind-up → miss (AC 2). Dodge / block through the window (AC 3).
  3. Circle the spider → it turns to follow (AC 4).
  4. Hit / kill the spider mid-bite → no late damage (AC 5).
  5. Two spiders side by side → no spider-on-spider damage (AC 6).
  6. Toggle `Show Hitbox Sweeps`; watch player swings and spider bites (AC 15).
  7. Player combos on the spider unchanged (AC 16).
- **Tuner checklist:** AC 10–14 on the spider prefab in Prefab Mode; `git diff` of the FBX `.meta`
  only touches the `events:` block of `CrawlBiteThreat`.
- `read_console` after every domain reload, prefab save and reimport.

### Notes

- **High-risk items:**
  - **AnimationMode leaks:** a missed `StopAnimationMode` leaves the prefab posed and can dirty it —
    stop on every exit path (window close, target/clip change, entering play mode, assembly reload).
  - **Event time units:** FBX importer events are normalized over `firstFrame..lastFrame`; `.anim`
    events are seconds. Mixing them misplaces windows silently — isolated in `HitWindowEvents` and tested.
  - **Empty `clipAnimations`** must be seeded from `defaultClipAnimations`, or writing collapses the
    clip list.
  - **Vendor `.meta`:** a HEROIC FANTASY pack re-import/update wipes the events → re-author with the tuner.
  - **`CrawlBiteThreat` has `loopTime: 1`:** harmless (Attack exits at exit-time 1; events outside an
    attack are ignored by `IsAttacking`).
  - Hitbox under a nested pack prefab bone = added-GameObject override on the variant — expected;
    never apply it to the pack prefab.
  - Unity-null semantics: explicit `!= null` for `UnityEngine.Object` fields, never `?.`.
- **Known limitations:** tuner trail shows the hitbox centre path, not the full swept volume; one
  damage value per attack (`Entity.AttackDamage`); two simultaneously open hitboxes can each hit
  the same target once; `AttackRange` still decides *when* to swing, the hitbox decides *if* it lands.
- **Future (out of scope):** humanoid NPC combat (register the equipped weapon's `WeaponHitbox`,
  implement `HumanoidAIAnimationDriver.TriggerAttack`, reuse player clips — receiver already
  no-ops combo events); wolf/rat/viper entity prefabs (author with the tuner); per-attack damage
  multiplier via event `floatParameter`; clip validation checker; hit-stop / VFX at `hitPoint`.

## Review Notes

- Adversarial review completed (inline) — 7 findings: 3 fixed, 1 deferred to a new spec, 3 acknowledged.
  - F1 (fixed): Hitbox Tuner used the global `AnimationMode` — now scoped to a private `AnimationModeDriver`.
  - F2 (deferred → `tech-spec-humanoid-unarmed-combat.md`): the hard switch leaves every humanoid harmless.
    Neutral NPCs are hostile to Monsters/Bandits and the `bandit` is hostile to the player; all previously
    dealt invisible hitscan damage (`HumanoidAIAnimationDriver.TriggerAttack` is a stub).
  - F3 (fixed): bite events moved off the gitignored vendor FBX `.meta` onto a project copy
    `Art/Characters/Monsters/DarknessSpider/Animations/darknessSpider_bite.anim` (override `Bite` slot);
    vendor `.meta` reverted to `events: []`.
  - F4 (fixed): "no EntityMeleeAttacker" error gated on `DetectionRange > 0` (still logged by the 7
    StartingTown humanoids — they are genuinely hostile-capable).
  - F5 (ack): tuner allocates a `SerializedObject` per hitbox per Scene repaint (editor only).
  - F6 (ack): attacking spider turns at `WarningTurnSpeed` (540°/s) — sidestepping a bite is hard (AC 2).
  - F7 (noise): `TryGetWindow` uses earliest Enable time — identical for sorted lists.
- Resolution approach: user-directed (fix F1/F3/F4, new spec for F2).
- **Deviations from the plan:**
  - Spider fangs are `chelicere_[LR]_b` (under `cephalothorax/Dummy025`), not `hook_L/R` (those are under
    `Abdomen_3`, the abdomen tip). `Hitbox_Bite` under `cephalothorax` at local `(-0.27, 0, 0.01)`, r 0.15,
    `_reachPadding` 0.1, `_verticalReach` 0.3.
  - Bite window frames 7→13 (from sampled bone positions) instead of the provisional 8→14 — refine with the tuner.
  - `EnemyType_DarknessSpider`: `AttackRange` 2.2 → 0.8, `EngageStoppingDistance` 1.8 → 0.65 (reach ≈ 0.92 m).
  - Events authored on a project `.anim` copy, not the vendor FBX `.meta` (F3).
- **Verification:** EditMode 331/331 green; play-mode smoke check confirmed attacker/receiver/hitbox owner
  wired on all 3 StartingTown spiders. **Pending manual:** play-mode checklist (AC 1–6, 15, 16) and tuner
  checklist (AC 10–14).
