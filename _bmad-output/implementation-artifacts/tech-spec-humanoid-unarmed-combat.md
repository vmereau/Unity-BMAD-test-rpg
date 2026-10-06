---
title: 'Humanoid Unarmed Combat — AI NPCs Through Hit Windows'
slug: 'humanoid-unarmed-combat'
created: '2026-10-01'
status: 'completed'
stepsCompleted: [1, 2, 3, 4]
tech_stack: ['Unity 6000.6.2f1', 'C# (.NET Standard 2.1)', 'Mecanim (Humanoid_Template, upper-body Attack layer, triggers)', 'StateMachineBehaviours', 'Unity Test Framework (EditMode, NUnit)']
files_to_modify:
  - 'Assets/_Game/Scripts/AI/AttackComboPlan.cs (new, pure) + .meta'
  - 'Assets/_Game/Scripts/AI/EntityMeleeAttacker.cs'
  - 'Assets/_Game/Scripts/AI/EntityAnimationEventReceiver.cs'
  - 'Assets/_Game/Scripts/AI/SMB_EntityAttackState.cs'
  - 'Assets/_Game/Scripts/AI/EntityBrain.cs'
  - 'Assets/_Game/Scripts/Core/Animations/AIAnimationDriver.cs'
  - 'Assets/_Game/Scripts/Core/Animations/HumanoidAIAnimationDriver.cs'
  - 'Assets/_Game/Scripts/Core/Animations/HumanoidAnimationBridge.cs'
  - 'Assets/_Game/Scripts/Combat/SMB_AttackState.cs (explicit null check, cached missing receiver)'
  - 'Assets/_Game/Scripts/Combat/PlayerCombat.cs (unarmed combo from config; ComboWindowClose ignored mid-chain)'
  - 'Assets/_Game/ScriptableObjects/Config/CombatConfigSO.cs + Data/Config/CombatConfig.asset (unarmedComboSteps 3)'
  - 'Assets/_Game/Scripts/Combat/CLAUDE.md'
  - 'Assets/_Game/ScriptableObjects/Entities/Entity.cs (combo hit count fields)'
  - 'Assets/_Game/Art/Characters/Humanoids/Controllers/Humanoid_Template.controller (SMB on Attack_1/2/3_State; Attack_2 → Attack_3 interruptionSource None)'
  - 'Assets/_Game/Prefabs/Entities/Humanoids/NPC_base Variant.prefab'
  - 'Assets/_Game/Data/NPCs/**/NPC_*.asset (7 Entity SOs: range + combo)'
  - 'Assets/Tests/EditMode/AttackComboPlanTests.cs (new) + .meta'
  - 'Assets/Tests/EditMode/EntityMeleeAttackerTests.cs'
  - 'Assets/_Game/Scripts/AI/CLAUDE.md'
  - 'Assets/_Game/Scripts/Core/Animations/CLAUDE.md'
  - 'Assets/_Game/Prefabs/CLAUDE.md'
code_patterns:
  - 'AI hit-window pipeline: EntityBrain → EntityMeleeAttacker.BeginAttack → AIAnimationDriver.TriggerAttack; clip events → EntityAnimationEventReceiver → attacker; SMB_EntityAttackState safety net'
  - 'AI code references AIAnimationDriver only (never concrete drivers/bridges); Animator written only by its bridge'
  - 'Pure plain-C# logic class next to the MonoBehaviour for EditMode tests (HitSweepTracker / HitWindowEvents pattern)'
  - 'Tunables in Entity SO (no magic numbers); GameLog + TAG; [SerializeField] private _camelCase; explicit != null for UnityEngine.Object (never ?.)'
  - 'Prefab edits via PrefabUtility.LoadPrefabContents/SaveAsPrefabAsset; controller edits via AnimatorController API (never raw YAML)'
test_patterns:
  - 'EditMode NUnit in Assets/Tests/EditMode, assembly Tests.EditMode references Game only'
  - 'Pure classes tested directly; MonoBehaviours via new GameObject + AddComponent (Awake does not run in EditMode); LogAssert.ignoreFailingMessages scoped to the fixture'
---

# Tech-Spec: Humanoid Unarmed Combat — AI NPCs Through Hit Windows

**Created:** 2026-10-01

## Overview

### Problem Statement

`tech-spec-ai-melee-hitboxes-tuner` (completed 2026-10-01) made AI damage flow only through
`EntityMeleeAttacker` hit windows (hard switch, no hitscan fallback). No humanoid NPC has an
`EntityMeleeAttacker`, and `HumanoidAIAnimationDriver.TriggerAttack` is a warn-only stub (no attack
animation ever played). Result: every humanoid is harmless — the hostile `bandit` (Faction_Bandits,
hostile to Player/Monsters/Neutral) can't hurt the player, and neutral NPCs (Faction_Neutral, hostile
to Monsters/Bandits — Guard, Elder, Villager, Merchant, Blacksmith, Innkeeper) can't hurt spiders or
bandits. Before that spec they all dealt invisible hitscan damage. All 7 StartingTown humanoids log
`[AI] <name>: no EntityMeleeAttacker — attacks will deal no damage` every play session.

### Solution

Wire the generic humanoid `UnarmedHitbox` (already on `NPC_base Variant`, inactive) into the
hit-window pipeline: `EntityMeleeAttacker` on the NPC root with the hitbox registered,
`EntityAnimationEventReceiver` on the `Character` Animator GO, `HumanoidAIAnimationDriver.TriggerAttack`
plays the unarmed attack, and an NPC-side combo driver chains a random 1–3 hit combo
(Jab → Uppercut → Jab) through the existing `Attack_2` / `Attack_3` triggers on `ComboWindowOpen`, each
hit dealing damage through its own hit window. `SMB_EntityAttackState` is added to the humanoid attack
states as the exit safety net.

### Scope

**In Scope:**
- `HumanoidAIAnimationDriver.TriggerAttack` implemented (animator trigger via `HumanoidAnimationBridge`)
- Random 1–3 hit unarmed combo for NPCs (chained on `ComboWindowOpen`), damage per hit
- `NPC_base Variant.prefab` wiring (attacker + registered/activated `UnarmedHitbox` + receiver) — covers
  all 7 NPC variants
- `SMB_EntityAttackState` on `Humanoid_Template` `Attack_1/2/3_State` (no-op for the player)
- `AttackRange` / `EngageStoppingDistance` retuned on the 7 `NPC_*` Entity SOs from measured jab reach;
  `AttackDamage` 10 and `AttackCooldown` 2 unchanged
- EditMode tests for the pure combo logic; CLAUDE.md updates

**Out of Scope:**
- Armed NPCs (guard weapons, NPC weapon equipping/visuals)
- NPC block / dodge; civilians fleeing instead of fighting
- Per-NPC damage differentiation; hit reactions / hit-stop / VFX; new animations

## Context for Development

### Codebase Patterns

- User decisions (2026-10-01): random 1–3 hit combo; all hostile-capable NPCs fight (current faction
  behavior kept); retune range only (damage 10 / cooldown 2 unchanged).
- Built on the AI hit-window pipeline from `tech-spec-ai-melee-hitboxes-tuner`: `EntityBrain.ExecuteAttack`
  → `EntityMeleeAttacker.BeginAttack(damage)` → `AIAnimationDriver.TriggerAttack()`; clip events →
  `EntityAnimationEventReceiver.HitboxEnable(string id)` (parameterless clip events deliver `""` = all
  hitboxes) → `EntityMeleeAttacker.OpenWindow`; `SMB_EntityAttackState.OnStateExit` → `EndAttack()`.
- NPCs use `Humanoid_Base.overrideController` over `Humanoid_Template.controller` (same base controller as
  the Player). Attacks live on the upper-body-masked `Attack` layer (weight 1, `UpperBodyMask`):
  `CombatIdle → Attack_1_State` (trigger `Attack_1`), `Attack_1 → Attack_2` (trigger `Attack_2`),
  `Attack_2 → Attack_3` (trigger `Attack_3`), each `→ CombatIdle` at exit time. All three states carry the
  player's `SMB_AttackState`.
- Clips: `Unarmed Jab` (`Jab.fbx`, 1.97 s) events HitboxEnable 0.21 / HitboxDisable 0.33 /
  ComboWindowOpen 0.36 / ComboWindowClose 0.92 (normalized); `Unarmed Uppercut` (`Uppercut Jab.fbx`,
  1.33 s) 0.34 / 0.57 / 0.63 / 0.93. Attack_3 reuses Jab.
- `NPC_base Variant.prefab`: Animator on `Character`; `UnarmedHitbox` (SphereCollider + WeaponHitbox,
  **inactive**) at `Character/mixamorig:Hips/.../mixamorig:RightHand/WeaponSocket/UnarmedHitbox`.
- NPC Entity SOs (`NPCEntity`): all 7 have AttackDamage 10, AttackRange 1.8, AttackCooldown 2,
  EngageStoppingDistance 1.5, DetectionRange 8, WarningRange 5.
- Factions: Bandits hostile to Player/Monsters/Neutral; Neutral hostile to Monsters/Bandits; Monsters
  hostile to Player/Neutral/Bandits; Player hostile to Monsters/Bandits.
- Lessons from the spider spec: verify bone/prefab assumptions against the real assets; measure reach
  before setting `AttackRange`; never author events on vendor assets (pack folder is gitignored).

### Investigation Findings (verified 2026-10-01 in the editor)

**Runtime code**
- `AIAnimationDriver` (`Game.Animations`, abstract MonoBehaviour): virtual no-ops `DriveLocomotion`,
  `TriggerAttack`, `TriggerGetHit`, `TriggerDeath`, `EnableRagdoll`, `SetWarning`, `SetInCombat`. No combo API.
- `HumanoidAIAnimationDriver` (on the NPC **root**, `[RequireComponent(HumanoidAnimationBridge)]`):
  `TriggerAttack()` = `GameLog.Warn("humanoid AI attack not implemented yet")`. `Awake` caches **every
  child Rigidbody under the Animator** as ragdoll bodies → hitbox GOs must never carry a Rigidbody
  (`UnarmedHitbox` has none). `SetInCombat` → bridge `IsInCombat` bool.
- `HumanoidAnimationBridge` (NPC root; shared with the Player): `PlayAttack(int triggerHash)` = `SetTrigger`;
  hashes exist for IsGrounded/IsRising/Velocity/IsBlocking/IsDodging/IsInCombat/GetHit/Death — **no
  Attack_1/2/3 hashes and no ResetTrigger helper yet**. Uses `_animator?.` (Unity-null caveat — new code
  must use explicit checks).
- `EntityMeleeAttacker` (from the previous spec): `BeginAttack(float)`, `EndAttack()`, `OpenWindow/CloseWindow(id)`,
  `RegisterHitbox/UnregisterHitbox`, `IsAttacking`, `CurrentDamage`, `Hitboxes`; `OnDisable → EndAttack`.
  No combo or attack-state tracking.
- `EntityAnimationEventReceiver`: `HitboxEnable(string)`, `HitboxDisable(string)`, `NotifyAttackExited()`,
  no-op `ComboWindowOpen()` / `ComboWindowClose()`.
- `SMB_EntityAttackState`: `OnStateExit` only → `NotifyAttackExited()` → `EndAttack()`. **In a combo this
  would end the attack on the Attack_1 → Attack_2 transition** (Unity fires `OnStateEnter(next)` at
  transition start and `OnStateExit(previous)` at transition end) → an enter/exit counter is needed.
- `SMB_AttackState` (player, `Game.Combat`) sits on the same humanoid attack states and resolves
  `animator.GetComponent<AnimationEventReceiver>()` with `?.` — NPCs have no such receiver; in the editor
  `GetComponent` may return a fake-null object that `?.` does not short-circuit. Harmless today (the method
  body only touches a null managed field) but must become an explicit `!= null` check.
- `EntityBrain.HandleAttack`: range checks → `FaceTarget()` → cooldown → `ExecuteAttack()`
  (`BeginAttack(AttackDamage)` + `TriggerAttack()` + cooldown = `AttackCooldown`). **No gate on an attack in
  progress** — a 3-hit combo (~3.3 s) outlasts the 2 s cooldown, so `BeginAttack` would cut it mid-swing.
- `Entity` SO `[Header("Attack")]`: `_attackRange`, `_attackCooldown` (+ `_attackDamage` elsewhere); `OnValidate`
  under `#if UNITY_EDITOR`. `NPCEntity` derives from `Entity`.

**Animator (`Humanoid_Template.controller`, NPCs via `Humanoid_Base.overrideController`)**
- `Attack` layer: weight 1, `UpperBodyMask`, default state `LockOn Locomotion`;
  `LockOn Locomotion → CombatIdle` on `IsInCombat` (0.54 s), `CombatIdle → Attack_1_State` on trigger
  `Attack_1` (0.25 s); `Attack_1 → Attack_2` on trigger `Attack_2` (no exit time, 0.42 s, interruption Source);
  `Attack_2 → Attack_3` on `Attack_3` (same); each `→ CombatIdle` at exit time (0.81 / 0.89 / 0.90).
  Each attack state carries `SMB_AttackState`. Also an `Attack` trigger param (unused by NPCs).
- `GetHit` / `Death` are AnyState transitions on the **Base** layer → they do **not** interrupt the upper-body
  attack; the punch keeps playing while the NPC is hit. Death is covered by `EntityBrain.TransitionToDead →
  EndAttack` and the ragdoll disabling the Animator (an attack state may then never "exit").
- Clip events (normalized): `Unarmed Jab` (`Jab.fbx`, 1.97 s): HitboxEnable 0.21, HitboxDisable 0.33,
  ComboWindowOpen 0.36, ComboWindowClose 0.92. `Unarmed Uppercut` (`Uppercut Jab.fbx`, 1.33 s): 0.34 /
  0.57 / 0.63 / 0.93. Attack_3 reuses the Jab. Clips are project assets
  (`Art/Characters/Humanoids/Animations/Combat/attacks/Unarmed/`), not a vendor pack.

**Prefab (`NPC_base Variant.prefab`)**
- Root: PersistentID, EntityHealth, EntityBrain, NavMeshAgent (radius 0.5), FactionMember, EntityUI,
  InventorySystem, NPCPresence, NPCMemoryComponent, NPCDialogueGraphComponent, GoldSystem,
  HumanoidAnimationBridge, HumanoidAIAnimationDriver. No EntityMeleeAttacker.
- Animator on child `Character` (`applyRootMotion = true`), no other components.
- `Character/mixamorig:Hips/…/mixamorig:RightHand/WeaponSocket/UnarmedHitbox`: **inactive**, layer Default,
  SphereCollider r 0.25 (disabled, trigger), WeaponHitbox `_reachPadding` 0.1, `_verticalReach` 0.6, plus a
  stray MeshFilter (no renderer). No Rigidbody.
- Hurtbox `Hitbox` CapsuleCollider r 0.5, h 2.32 (layer 7). Player hurtbox r 0.3.
- **All 7 StartingTown NPCs are direct scene instances of `NPC_base Variant`** (no intermediate variants);
  only Villager/bandit carry 3 EntityBrain overrides (waypoints) — wiring the base prefab reaches everyone.

**Measured jab reach** (full-body `SampleAnimation` on the NPC rig — approximation, see risks)
- Hand (hitbox centre) root-local z: 0.45 (n 0.20) → 0.53 (0.25) → 0.94 (0.31) → peak 0.99–1.04 (0.36),
  y 1.19–1.36. Inside the Jab window (0.21–0.33) max forward ≈ 0.94.
- Reach vs player = 0.94 + 0.25 (radius) + 0.1 (padding) + 0.3 (player hurtbox) ≈ 1.59 → AttackRange ≈ 1.5,
  EngageStoppingDistance ≈ 1.3 (must stay ≤ AttackRange and > agent radius 0.5).
- **The Uppercut in the full-body sample moves the hand backward/sideways during its window** (z ≈ −0.17,
  x ±0.3; max forward 0.33) — likely body rotation that the upper-body mask partly removes. Must be
  verified in play mode with the debug sweeps.
- Spider hurtbox top ≈ y 0.54; jab hand y ≈ 1.2–1.3 → the 0.6 downward reach capsule (r 0.25) bottoms out
  ≈ y 0.35–0.5 → marginal overlap; raise the NPC `UnarmedHitbox._verticalReach` if NPC punches miss spiders.

### Files to Reference

| File | Purpose |
| ---- | ------- |
| `Assets/_Game/Scripts/AI/EntityMeleeAttacker.cs` | Attacker to extend (combo + attack-state counter) |
| `Assets/_Game/Scripts/AI/EntityAnimationEventReceiver.cs`, `SMB_EntityAttackState.cs` | Receiver / SMB to extend |
| `Assets/_Game/Scripts/AI/EntityBrain.cs` (`HandleAttack`, `ExecuteAttack`) | Attack gate + combo hit count |
| `Assets/_Game/Scripts/Core/Animations/AIAnimationDriver.cs`, `HumanoidAIAnimationDriver.cs`, `HumanoidAnimationBridge.cs`, `MonsterAnimationDriver.cs` | Driver seam |
| `Assets/_Game/Scripts/Combat/SMB_AttackState.cs`, `PlayerCombat.cs` (`ManageComboStep`, `_IsComboAttacking`) | Player combo pattern to mirror / not break |
| `Assets/_Game/ScriptableObjects/Entities/Entity.cs` | Attack config fields |
| `Assets/_Game/Art/Characters/Humanoids/Controllers/Humanoid_Template.controller` | Attack layer states |
| `Assets/_Game/Prefabs/Entities/Humanoids/NPC_base Variant.prefab` | Wiring target |
| `Assets/Tests/EditMode/EntityMeleeAttackerTests.cs`, `HitWindowEventsTests.cs` | Test style |
| `_bmad-output/implementation-artifacts/tech-spec-ai-melee-hitboxes-tuner.md` | Pipeline it builds on |

### Technical Decisions

1. **Combo plan is pure logic** — `AttackComboPlan` (`Game.AI`, plain C#): `Begin(int hits, int maxSteps)`
   (clamped to [1, maxSteps]), `CurrentStep`, `TotalHits`, `TryAdvance(out int nextStep)` (true while
   `CurrentStep < TotalHits`), `Reset()`. EditMode-tested.
2. **Hit count is data** — `Entity` gets `_comboHitsMin` / `_comboHitsMax` (int, `[Min(1)]`, default 1/1 so
   monsters are unchanged; `OnValidate` keeps max ≥ min). The brain rolls `Random.Range(min, max + 1)` and
   calls `BeginAttack(damage, hits)`. NPC SOs: 1 / 3.
3. **Driver seam** — `AIAnimationDriver` gains `virtual int MaxComboSteps => 1` and
   `virtual void TriggerComboStep(int step) { }`. Humanoid: `MaxComboSteps => 3`; `TriggerAttack()` resets
   the Attack_1/2/3 triggers then sets `Attack_1`; `TriggerComboStep(2|3)` sets `Attack_2|Attack_3`. Bridge
   gets the three hashes + `ResetAttackTriggers()` (explicit `_animator != null` checks). Monster driver unchanged.
4. **Attacker owns the combo** — `EntityMeleeAttacker` resolves `AIAnimationDriver` (serialized, auto
   `GetComponent`), clamps hits to `MaxComboSteps` (no driver → 1). `OnComboWindowOpen()`: ignored unless
   `IsAttacking`; `TryAdvance` → `TriggerComboStep(next)`. Each step's clip opens its own window →
   `WeaponHitbox` re-arms dedupe per window → **damage per hit** (`CurrentDamage` each).
5. **Attack-state counter instead of exit-only** — SMB `OnStateEnter → NotifyAttackEntered()`,
   `OnStateExit → NotifyAttackExited()`; the attacker counts active attack states (decrement clamped at 0)
   and calls `EndAttack()` when the count returns to 0. `IsInAttackState => count > 0`. Crossfades between
   combo states never drop the count to 0. Spider behaviour unchanged (enter 1 → exit 0 → EndAttack).
   `EndAttack()` does not touch the counter (it mirrors the animator).
6. **Brain gate** — `HandleAttack`: after `FaceTarget()`, `if (_meleeAttacker != null &&
   _meleeAttacker.IsInAttackState) return;` before the cooldown check → no new attack while one plays.
7. **Receiver** — `ComboWindowOpen()` → `attacker.OnComboWindowOpen()`; `ComboWindowClose()` stays a no-op;
   new `NotifyAttackEntered()`.
8. **Player safety** — `SMB_EntityAttackState` added to `Attack_1/2/3_State` next to `SMB_AttackState`; the
   Player has no `EntityAnimationEventReceiver` → no-op. `SMB_AttackState` switched to explicit null checks
   (behaviour unchanged for the player).
9. **Prefab** — `NPC_base Variant`: `UnarmedHitbox` set active (stays dormant: collider disabled, window
   closed); `EntityMeleeAttacker` on root (`_hitboxes = [{ "Unarmed", UnarmedHitbox }]`, faction member,
   driver); `EntityBrain._meleeAttacker` assigned; `EntityAnimationEventReceiver` on `Character`.
10. **Tuning** — 7 NPC SOs: `AttackRange` 1.8 → 1.5, `EngageStoppingDistance` 1.5 → 1.3, combo 1–3;
    `AttackDamage` 10 / `AttackCooldown` 2 unchanged. Re-verify reach in play mode with
    `Tools/Combat/Show Hitbox Sweeps` and record final values.

## Implementation Plan

### Tasks

- [x] Task 1: Create the pure combo logic `AttackComboPlan`
  - File: `Assets/_Game/Scripts/AI/AttackComboPlan.cs` (new, namespace `Game.AI`, `public sealed class`, no MonoBehaviour, no `GameLog` → no `TAG`)
  - Action:
    ```csharp
    public int TotalHits { get; private set; }   // 0 when idle
    public int CurrentStep { get; private set; } // 0 when idle, 1..TotalHits during an attack
    public bool IsActive => TotalHits > 0;
    // hits and maxSteps clamped: maxSteps < 1 → 1; hits clamped to [1, maxSteps]. Sets CurrentStep = 1.
    public void Begin(int hits, int maxSteps);
    // True (and CurrentStep++) while IsActive && CurrentStep < TotalHits; nextStep = new CurrentStep. Else false, nextStep = 0.
    public bool TryAdvance(out int nextStep);
    public void Reset(); // TotalHits = CurrentStep = 0
    ```
  - Notes: XML summary: "Step 1 is started by TriggerAttack; steps 2..N are requested on ComboWindowOpen."

- [x] Task 2: Add combo hit-count config to `Entity`
  - File: `Assets/_Game/ScriptableObjects/Entities/Entity.cs`
  - Action: Under `[Header("Attack")]` add
    `[Tooltip("Minimum hits per attack combo (1 = single attack).")] [SerializeField, Min(1)] private int _comboHitsMin = 1;`
    and `[Tooltip("Maximum hits per attack combo; clamped by the animation driver's MaxComboSteps.")] [SerializeField, Min(1)] private int _comboHitsMax = 1;`;
    properties `ComboHitsMin` / `ComboHitsMax`. In the existing `OnValidate`: `if (_comboHitsMax < _comboHitsMin) _comboHitsMax = _comboHitsMin;`.
  - Notes: Defaults 1/1 keep every monster SO (spider etc.) on single attacks with no asset change.

- [x] Task 3: Extend the animation driver seam + humanoid implementation
  - Files: `Assets/_Game/Scripts/Core/Animations/AIAnimationDriver.cs`, `HumanoidAnimationBridge.cs`, `HumanoidAIAnimationDriver.cs`
  - Action:
    1. `AIAnimationDriver`: add `public virtual int MaxComboSteps => 1;` and `public virtual void TriggerComboStep(int step) { }` (XML: step 2..MaxComboSteps; step 1 is `TriggerAttack`).
    2. `HumanoidAnimationBridge`: add `Attack1Hash/Attack2Hash/Attack3Hash = Animator.StringToHash("Attack_1"/"Attack_2"/"Attack_3")`, `public int AttackTriggerHash(int step)` (1→Attack1Hash, 2→Attack2Hash, 3→Attack3Hash, else 0) and `public void ResetAttackTriggers()` (`if (_animator == null) return;` then `ResetTrigger` ×3). Do not change existing methods.
    3. `HumanoidAIAnimationDriver`: `private const int MAX_COMBO_STEPS = 3;` `public override int MaxComboSteps => MAX_COMBO_STEPS;`
       `TriggerAttack()`: `if (_bridge == null) return; _bridge.ResetAttackTriggers(); _bridge.PlayAttack(_bridge.AttackTriggerHash(1));`
       `TriggerComboStep(int step)`: `if (_bridge == null || step < 2 || step > MAX_COMBO_STEPS) return; _bridge.PlayAttack(_bridge.AttackTriggerHash(step));`
       Update the class XML summary (attack implemented; unarmed combo via Attack_1/2/3 triggers).
  - Notes: Explicit `!= null` (Unity-null). `MonsterAnimationDriver` untouched (inherits 1 / no-op). Clearing stale `Attack_2/3` triggers in `TriggerAttack` prevents a leftover trigger from auto-chaining the next attack.

- [x] Task 4: Add combo + attack-state tracking to `EntityMeleeAttacker`
  - File: `Assets/_Game/Scripts/AI/EntityMeleeAttacker.cs`
  - Action:
    1. Fields: `[Tooltip("Plays attack/combo steps. Auto-resolved on the same GO if null.")] [SerializeField] private AIAnimationDriver _animationDriver;`
       (`using Game.Animations;`), `private readonly AttackComboPlan _combo = new();`, `private int _activeAttackStates;`.
       Properties: `public bool IsInAttackState => _activeAttackStates > 0;`, `public int ComboStep => _combo.CurrentStep;`, `public int ComboHits => _combo.TotalHits;`.
    2. `Awake`: `if (_animationDriver == null) _animationDriver = GetComponent<AIAnimationDriver>();` (no warning — tests and driverless setups are valid → single hits).
    3. `public void BeginAttack(float damage) => BeginAttack(damage, 1);` and
       `public void BeginAttack(float damage, int comboHits)`: existing body + `_combo.Begin(comboHits, _animationDriver != null ? _animationDriver.MaxComboSteps : 1);`.
    4. `EndAttack()`: existing body + `_combo.Reset();` (does **not** touch `_activeAttackStates`).
    5. `public void OnComboWindowOpen()`: `if (!IsAttacking) return; if (!_combo.TryAdvance(out int next)) return; if (_animationDriver != null) _animationDriver.TriggerComboStep(next);`
    6. `public void NotifyAttackStateEntered() => _activeAttackStates++;`
       `public void NotifyAttackStateExited() { _activeAttackStates = Mathf.Max(0, _activeAttackStates - 1); if (_activeAttackStates == 0) EndAttack(); }`
    7. `OnDisable`: `EndAttack(); _activeAttackStates = 0;`.
    8. Update the class XML summary: combo (hit count rolled by the brain, steps requested on ComboWindowOpen, damage per hit = per window) and the state counter (crossfades between combo states keep the attack alive).
  - Notes: Dedupe stays per `WeaponHitbox` window → each combo hit can damage the same target once (intended "damage per hit").

- [x] Task 5: Extend `EntityAnimationEventReceiver`
  - File: `Assets/_Game/Scripts/AI/EntityAnimationEventReceiver.cs`
  - Action: `ComboWindowOpen()` → `if (_attacker == null) return; _attacker.OnComboWindowOpen();` (keep `ComboWindowClose()` a no-op, update comment). Add `public void NotifyAttackEntered() { if (_attacker == null) return; _attacker.NotifyAttackStateEntered(); }`. `NotifyAttackExited()` now calls `_attacker.NotifyAttackStateExited()` (not `EndAttack()` directly). Update XML/comments.

- [x] Task 6: Extend `SMB_EntityAttackState`
  - File: `Assets/_Game/Scripts/AI/SMB_EntityAttackState.cs`
  - Action: Add `OnStateEnter` → `receiver.NotifyAttackEntered()`; `OnStateExit` unchanged call. Cache the lookup once per instance with a `_resolved` bool (avoid a `GetComponent` on every enter/exit on the Player, where the receiver is legitimately absent). Explicit `!= null` checks. Update XML: counter semantics, works on Player states as a no-op.

- [x] Task 7: `EntityBrain` — attack gate + combo roll
  - File: `Assets/_Game/Scripts/AI/EntityBrain.cs`
  - Action:
    1. `HandleAttack()`: right after `FaceTarget();` add `if (_meleeAttacker != null && _meleeAttacker.IsInAttackState) return; // never cut an attack/combo mid-swing`.
    2. `ExecuteAttack()`: replace `BeginAttack(AttackDamage)` with
       `int hits = Random.Range(entity.ComboHitsMin, entity.ComboHitsMax + 1);` (where `entity = _persistentID.Entity`) and `_meleeAttacker.BeginAttack(entity.AttackDamage, hits);`. Log line includes the hit count.
  - Notes: Cooldown still starts at `ExecuteAttack`; the gate makes the effective interval `max(cooldown, attack duration)`.

- [x] Task 8: Player SMB null-safety
  - File: `Assets/_Game/Scripts/Combat/SMB_AttackState.cs`
  - Action: Replace `GetReceiver(animator)?.X()` with `var r = GetReceiver(animator); if (r != null) r.X();` in both callbacks. Behaviour unchanged for the Player.
  - Notes: These states now also run on NPC Animators (no `AnimationEventReceiver`) — `?.` does not respect Unity-null.

- [x] Task 9: Add `SMB_EntityAttackState` to the humanoid attack states
  - File: `Assets/_Game/Art/Characters/Humanoids/Controllers/Humanoid_Template.controller`
  - Action: Via Unity MCP `execute_code`: load the `AnimatorController`, layer `Attack`, states `Attack_1_State`, `Attack_2_State`, `Attack_3_State` → `AddStateMachineBehaviour<Game.AI.SMB_EntityAttackState>()` if absent (keep `SMB_AttackState`), `SetDirty` + `SaveAssets`. `read_console`.
  - Notes: Never hand-edit the controller YAML.

- [x] Task 10: Wire `NPC_base Variant.prefab`
  - File: `Assets/_Game/Prefabs/Entities/Humanoids/NPC_base Variant.prefab`
  - Action (one `execute_code` with `PrefabUtility.LoadPrefabContents` / `SaveAsPrefabAsset` on the same path):
    1. `UnarmedHitbox` (`Character/.../mixamorig:RightHand/WeaponSocket/UnarmedHitbox`) → `SetActive(true)`; leave its collider disabled; no Rigidbody (ragdoll scan).
    2. Root: `AddComponent<EntityMeleeAttacker>()`; via `SerializedObject` set `_selfFactionMember` (root `FactionMember`) and `_animationDriver` (root `HumanoidAIAnimationDriver`); `EditorAddHitbox("Unarmed", unarmedHitbox)`.
    3. Root `EntityBrain._meleeAttacker` = the attacker.
    4. `Character`: `AddComponent<EntityAnimationEventReceiver>()`, `_attacker` assigned.
  - Notes: Verify afterwards that the 7 StartingTown scene instances resolve the attacker (play-mode query) and that no instance override blanks the new fields. `refresh_unity(mode="if_dirty")` only.

- [x] Task 11: Tune the NPC Entity SOs
  - Files: `Assets/_Game/Data/NPCs/Bandit/NPC_Bandit.asset`, `BlackSmith/NPC_Blacksmith.asset`, `Elder/NPC_Elder.asset`, `Innkeeper/NPC_Innkeeper.asset`, `Merchant/NPC_Merchant.asset`, `NPC_Guard/NPC_Guard.asset`, `Villager/NPC_Villager.asset`
  - Action: Via `SerializedObject`: `_attackRange` 1.5, `_engageStoppingDistance` 1.3, `_comboHitsMin` 1, `_comboHitsMax` 3. `AttackDamage` / `AttackCooldown` untouched.

- [x] Task 12: EditMode tests
  - Files: `Assets/Tests/EditMode/AttackComboPlanTests.cs` (new), `Assets/Tests/EditMode/EntityMeleeAttackerTests.cs`
  - Action — `AttackComboPlanTests`:
    `Begin_SetsStepOne`; `Begin_ClampsHitsToMaxSteps` (5 hits, max 3 → 3); `Begin_ClampsHitsToAtLeastOne` (0 → 1);
    `Begin_MaxStepsBelowOne_TreatedAsOne`; `TryAdvance_ThreeHits_AdvancesTwiceThenStops` (2, 3, then false);
    `TryAdvance_SingleHit_ReturnsFalse`; `TryAdvance_WhenIdle_ReturnsFalse`; `Reset_ClearsState`.
  - Action — `EntityMeleeAttackerTests` (add):
    `NotifyAttackStateExited_LastState_EndsAttack`;
    `NotifyAttackStateExited_WithOverlappingNextState_KeepsAttacking` (enter, enter, exit → still `IsAttacking`, window can open);
    `NotifyAttackStateExited_BelowZero_Clamps` (exit with no enter → `IsInAttackState` false, no throw);
    `EndAttack_DoesNotResetStateCounter`;
    `BeginAttack_NoDriver_ClampsComboToOneHit` (`BeginAttack(10, 3)` → `ComboHits == 1`);
    `OnComboWindowOpen_WhenNotAttacking_DoesNotAdvance`.
  - Notes: Driver-backed combo advancing is covered by `AttackComboPlanTests` + play mode (no test doubles for MonoBehaviour drivers). Full suite must stay green.

- [x] Task 13: Play-mode verification + reach retune
  - Action: StartingTown, `Tools/Combat/Show Hitbox Sweeps` on.
    1. Bandit vs player: jab sweeps reach the player at the engage distance; adjust `_attackRange` / `_engageStoppingDistance` (all 7 SOs) if the sweep falls short or overshoots; record final values.
    2. Uppercut (step 2): check where its sweep goes under the upper-body mask. If it never reaches forward, record it as a known limitation (no new animations in scope) — do **not** change the controller.
    3. Guard vs spider: punches land on the spider hurtbox; if not, raise the NPC `UnarmedHitbox._verticalReach` (prefab) and record.
    4. Root motion: after several combos, `Character.localPosition` stays ≈ (0,0,0) relative to the root. If it drifts, record it and set `Animator.applyRootMotion = false` on `Character` **only if** locomotion is unaffected (otherwise note as follow-up).
    5. Console clean (no "humanoid AI attack not implemented", no "no EntityMeleeAttacker" errors).

- [x] Task 14: Documentation
  - `Assets/_Game/Scripts/AI/CLAUDE.md`: `AttackComboPlan` row; attacker combo + state counter; brain gate; combo hits from `Entity.ComboHitsMin/Max`; remove the "every StartingTown humanoid logs this" note.
  - `Assets/_Game/Scripts/Core/Animations/CLAUDE.md`: `MaxComboSteps` / `TriggerComboStep` seam; replace the "Humanoid AI combat triggers are stubs" rule (attack implemented; GetHit/Death on the Base layer do not interrupt the upper-body attack); `Humanoid_Template` `Attack_1/2/3_State` carry both `SMB_AttackState` and `SMB_EntityAttackState`.
  - `Assets/_Game/Prefabs/CLAUDE.md` (Entities/NPC section): NPC attack wiring (attacker on root with `Unarmed`, receiver on `Character`, `UnarmedHitbox` active + dormant, no Rigidbody), final range values.

### Acceptance Criteria

- [ ] AC 1: Given the hostile bandit engaged with the player in range, when it attacks, then a jab animation plays and the player loses `AttackDamage` once per hit whose swept hand overlaps the player hurtbox.
- [ ] AC 2: Given a combo of N hits (1–3) rolled, when each step's ComboWindowOpen fires, then the next step plays until N steps have played, and each hit can damage the target at most once.
- [ ] AC 3: Given a 3-hit combo in progress, when the 2 s cooldown elapses mid-combo, then no new attack starts until the last attack state has exited.
- [ ] AC 4: Given the Attack_1 → Attack_2 crossfade, when `OnStateExit(Attack_1)` fires after `OnStateEnter(Attack_2)`, then the attack stays active and Attack_2's hit window opens.
- [ ] AC 5: Given the last combo state exits, when the attack-state count reaches 0, then all windows are closed and `IsAttacking` is false.
- [ ] AC 6: Given a neutral NPC (e.g. Guard) engaging a spider or the bandit, when its jab overlaps the target hurtbox, then the target takes damage; given a neutral NPC near another neutral NPC or the player, then they take no damage (faction filter).
- [ ] AC 7: Given the player blocking / dodging during an NPC hit window, then `TryReceiveHit` returns Blocked/PerfectBlock/Dodged, no damage is dealt and the result is logged.
- [ ] AC 8: Given an NPC dies or disengages mid-combo, then all windows close and no further damage or combo steps occur.
- [ ] AC 9: Given the DarknessSpider (combo 1/1, monster driver), when it attacks, then behaviour is unchanged from `tech-spec-ai-melee-hitboxes-tuner` (single bite, events, damage, gate harmless).
- [ ] AC 10: Given the player's attacks and combos, when played after this change, then they behave exactly as before (no new logs/errors from `SMB_EntityAttackState` or `SMB_AttackState`).
- [ ] AC 11: Given play mode in StartingTown, when the scene loads, then no `no EntityMeleeAttacker` error and no `humanoid AI attack not implemented` warning is logged.
- [ ] AC 12: Given a stale `Attack_2` trigger from a previous combo, when a new attack starts, then only Attack_1 plays first (triggers reset).
- [ ] AC 13: Given the EditMode suite, when run, then `AttackComboPlanTests`, the new `EntityMeleeAttackerTests` and all existing tests pass.

## Additional Context

### Dependencies

- Builds on `tech-spec-ai-melee-hitboxes-tuner` (completed 2026-10-01, uncommitted in the working tree at
  spec time): `EntityMeleeAttacker`, `EntityAnimationEventReceiver`, `SMB_EntityAttackState`,
  `WeaponHitbox.IsWindowOpen`, debug sweeps, Hitbox Tuner.
- No new packages. Unity Editor + MCP for Tasks 9–11, 13 and `run_tests`.
- Order: 1 → 2 → 3 (one compile) → 4 → 5 → 6 → 7 → 8 (one compile) → 9 → 10 → 11 → 12 → 13 → 14.

### Testing Strategy

- **EditMode (automated):** Task 12 — combo plan logic and the attacker's state counter / clamping. Full suite
  via Unity MCP `run_tests`.
- **Play mode (manual, Task 13 + ACs):** StartingTown with debug sweeps: bandit vs player (AC 1–3, 7, 12),
  combo crossfade (AC 4–5), guard vs spider / bandit and neutral-vs-neutral (AC 6), kill / outrun an NPC
  mid-combo (AC 8), spider bite unchanged (AC 9), player combos unchanged (AC 10), console on load (AC 11).
- `read_console` after every domain reload, controller save and prefab save.

### Implementation Results (2026-10-06)

Play mode, StartingTown (editor-side probes logging attacker/animator state per change):
- **Wiring:** all 7 humanoids resolve `EntityMeleeAttacker` (1 `Unarmed` hitbox), `HumanoidAIAnimationDriver`
  and the `Character` receiver; no scene override blanks the new fields. Console clean on load (AC 11).
- **Bandit vs player:** jab windows land at 0.6–1.0 m (one hit per window, 10 dmg each); the next attack waits
  for the last attack state to exit (AC 1–3, 5). Range values kept at 1.5 / 1.3.
- **Step 3 bug found & fixed (controller):** `Attack_2 → Attack_3` (0.42 s fixed blend, interruption `Source`)
  started at Uppercut 0.63 and was cancelled by `Attack_2 → CombatIdle` at exit time 0.89 — step 3 never played.
  Set that transition's `interruptionSource = None`; re-verified A1 → A2 → A3 with a window per step and the
  counter held across both crossfades (AC 2, 4). Player impact: only removes the same cut on 3-step weapon combos.
- **Uppercut reach (known limitation):** the step-2 window hit the player once at 0.9 m but whiffed at 1.0 m in
  every later combo; it does hit spiders at 1.0 m. No controller/animation change (out of scope).
- **Guard vs spider:** jab + uppercut hits land on the spider (`_verticalReach` 0.6 sufficient — unchanged);
  Merchant/Guard/Blacksmith gang up on spiders with no friendly fire (AC 6). Spiders never bite NPCs: spider
  `AttackRange` 0.8 vs ≈ 1.0 m agent separation — pre-existing tuning gap, recorded in `Scripts/AI/CLAUDE.md`.
- **Spider vs player:** single bite, hit lands, 1-hit combo (AC 9).
- **Death mid-combo:** killing the Innkeeper at step 2 → `IsAttacking` false, combo cleared, window closed, no
  further steps (AC 8).
- **Root motion:** `Character.localPosition` stayed (0,0,0) through every combo — `applyRootMotion` left on.
- **Not exercised automatically (needs manual play):** AC 7 (player block/dodge vs NPC hit — `TryReceiveHit`
  path unchanged from the previous spec) and AC 10 (player combos — `SMB_AttackState` change is an equivalent
  null-check; the new SMB is a no-op without a receiver; no errors logged).

### Notes

- **High-risk items:**
  - **Uppercut reach** under the upper-body mask is unverified (full-body sample shows the hand moving
    backward). Step 2 may whiff; recorded as a limitation if so — new animations are out of scope.
  - **Root motion on `Character`** (`applyRootMotion = true`) could drift the visual from the agent during
    attacks — verify in Task 13.
  - **Hits don't interrupt punches** (GetHit/Death on the Base layer, attack on the upper-body layer) — an NPC
    keeps punching while hit; death is still safe via `EndAttack` + ragdoll.
  - **SMBs run on both the Player and NPCs** (shared `Humanoid_Template`) — every receiver lookup must be
    explicit-null-safe and silent when absent.
  - **Counter drift:** if an attack state's `OnStateExit` never fires (Animator disabled by the ragdoll), the
    counter stays > 0 — harmless because the entity is dead; `OnDisable` resets it.
- **Known limitations:** one damage value per hit (`Entity.AttackDamage`); combo length random per attack, no
  reaction to blocks; civilians fight like guards; NPC punches on low targets (spider) rely on `_verticalReach`.
- **Future (out of scope):** armed NPCs (register the equipped weapon's `WeaponHitbox` as `Weapon`, weapon
  archetype clips); NPC block/dodge; civilians fleeing; per-NPC damage/combo tuning; hit reactions on the
  attack layer.

## Review Notes

- Adversarial review completed (2026-10-06)
- Findings: 10 total — 5 fixed (F2, F7, F8, F9 auto-fixed; F3/AC 10 player combos verified by the user), 5
  acknowledged (F1 controller change accepted, F4 spider range pre-existing, F5 off-screen culling, F6
  combo continues while chasing, F10 uppercut reach = known limitation)
- Resolution approach: auto-fix
- Fixes: `AIAnimationDriver.CancelAttack()` (humanoid resets Attack_1/2/3) called from
  `EntityMeleeAttacker.EndAttack` (F2); 5 driver-backed/lifecycle tests with a fake driver (F7);
  `SMB_AttackState` caches a missing receiver (F8); `EntityBrain.Start` warns when `ComboHitsMax` >
  driver `MaxComboSteps` (F9).
- **Scope addition (user request):** player unarmed combo is now 3 steps
  (`CombatConfigSO.unarmedComboSteps = 3`; `WeaponSO.DEFAULT_COMBO_STEPS` stays 2 for archetype-less
  weapons). Exposed a player bug: Uppercut's `ComboWindowClose` (0.93) fires inside the now-uninterruptible
  `Attack_2 → Attack_3` blend and reset `_comboStep`, letting a 4th press restart Attack_1. Fixed by ignoring
  `OnComboWindowClose` while `_IsComboAttacking`. Verified in play mode: A1 → A2 → A3, 4th press ignored
  (max combo), clean exit to CombatIdle; NPC 3-hit combos unchanged. AC 7 (block/dodge vs NPC) still
  manual-only.
