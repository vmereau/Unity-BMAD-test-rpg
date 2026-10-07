# CLAUDE.md — Assets/_Game/Scripts/AI

> Loaded when Claude accesses files in this folder. Entity brains, health, faction targeting, melee
> attacks, and entity/NPC presence. Namespace: `Game.AI` — except `EntityPresence.cs`, which is in
> `Game.World` although the file lives here.
> AI animation polymorphism (Brain/Health → Driver → Bridge): `Scripts/Core/Animations/CLAUDE.md`.
> Event/SMB contract shared with the player: `.claude/rules/attack-pipeline.md`.

---

## What's here

| File | Role |
|------|------|
| `EntityBrain` | State machine Idle → Patrolling → (Engaging → Attacking) → Dead. Implements `ICombatStateProvider`. Targets from `TargetRegistry`. |
| `EntityHealth` | Health for any entity; `IDamageable`. On death: stops NavMeshAgent (optional), `PersistentID.RegisterDeath()`, death anim. Body stays (ragdoll, never `SetActive(false)`). |
| `FactionMember` | Targetable faction participant; self-registers with `TargetRegistry`. Faction from `PersistentID.Entity.Faction`, or `_factionOverride` (the Player — no PersistentID). |
| `TargetRegistry` | **Static** registry of live `FactionMember`s — use it instead of `FindGameObjectWithTag`. Reset on play-mode enter (`SubsystemRegistration`). |
| `ICombatStateProvider` | Read-only "in combat?". Implemented by `EntityBrain`, polled by `NPCPresence`. |
| `EntityMeleeAttacker` | Entity-side owner of named `WeaponHitbox`es — see below. |
| `AttackComboPlan` | Pure combo bookkeeping (`Begin`, `TryAdvance`, `Reset`) for `EntityMeleeAttacker`. `AttackComboPlanTests`. |
| `EntityAnimationEventReceiver` | On the **Animator GO**. Hit-window events by id → attacker; SMB notifications → attacker state counter; `ComboWindowOpen()` → `attacker.OnComboWindowOpen()`. |
| `SMB_EntityAttackState` | On attack states of `EntityBase.controller` and `Humanoid_Template`. Enter/exit → receiver → attacker counter. Receiver lookup cached; no-op on the Player. |
| `SMB_DeathState` | On the death animator state. |
| `EntityPresence` (`Game.World`) | Base `IInteractable` for every entity — see below. |
| `NPC/NPCPresence` | `: EntityPresence` — adds dialogue. |
| `NPC/NPCMemoryComponent`, `NPC/NPCDialogueGraphComponent` | An NPC's active memory set; resolve available dialogue start nodes / choices. |

---

## EntityMeleeAttacker

- `_hitboxes` = `Id` + `WeaponHitbox`. Clip events open/close them by id (`""` = all). `RegisterHitbox` /
  `UnregisterHitbox` for runtime (equipped-weapon) hitboxes. Unknown id → one warning per id.
- `BeginAttack(damage, comboHits)` / `EndAttack()` bracket an attack; `OpenWindow` is ignored while
  `!IsAttacking`. Hits are faction-filtered (target's `FactionMember` must be hostile; none → ignored), then
  `TryReceiveHit → TakeDamage(CurrentDamage)` on `NotBlocked`.
- **Combo:** `comboHits` is clamped to `AIAnimationDriver.MaxComboSteps` (no driver → 1).
  `OnComboWindowOpen()` advances and calls `TriggerComboStep(next)`; each step's clip opens its own
  window → damage per hit.
- **Attack-state counter:** SMB enter/exit count active attack states; reaching 0 calls `EndAttack()`.
  `IsInAttackState => count > 0`. `EndAttack()` never touches the counter (it mirrors the animator) and calls
  `AIAnimationDriver.CancelAttack()` to drop queued triggers; `OnDisable` resets the counter.

---

## EntityPresence / NPCPresence (interaction & loot)

`EntityPresence` sits on the `Entity_base.prefab` root, so every entity is found by `InteractionSystem` and
shows its world-space `EntityUI` on hover. `NameTag` is null-guarded (`""` without `PersistentID.Entity`).

- **Loot:** `IsLootable = IsDead && HasLoot` (`HasLoot` = `InventorySystem.Count > 0`). `CanInteract => IsLootable`,
  `InteractPrompt` = `"Loot"` when lootable, else `""`. `Interact()` → `OpenLoot()` raises
  `GameEventSO_ContainerOpenRequest _onLootRequested` with the entity's `InventorySystem` and
  `takeOnly = true` — reusing the container pipeline (`ContainerSystem` / `ContainerUI`). An emptied corpse
  is inert (no prompt, `Interact()` no-op).
- **`NPCPresence`** overrides `Interact()`: dead → `base.Interact()` (loot, never dialogue); alive and out of
  combat → raises `_onDialogueRequested`; in combat → blocked. Prompt `"Loot"` / `"Talk"` / `""`;
  `CanInteract => IsLootable || IsAliveAndOutOfCombat`.
- **Wiring gotcha:** `_onLootRequested` must be wired on **both** `Entity_base.prefab` (monsters) and the NPC
  variant's own `NPCPresence` — the NPC variant removes the inherited base component, so the base wiring
  doesn't carry over (`Prefabs/Entities/CLAUDE.md`).

---

## Rules

- **No `event Action` across system boundaries** (project-context.md). `ICombatStateProvider` is polled via
  `GetComponent`; use a `GameEventSO<T>` channel if push is ever needed.
- `EntityHealth.MaxHealth` = `PersistentID.Entity.BaseHealth`, fallback `100f`.
- `PersistentID`, `AIAnimationDriver` and `NavMeshAgent` are **optional** — guard every access.
- **Passive entities** (e.g. `Entity_HumanoidNPC`) have `_detectionRange = 0`. Any `WarningRange` vs
  `DetectionRange` check (`Entity.OnValidate`, `EntityBrain` guard) must skip when `DetectionRange <= 0`.
- **AI damage flows only through `EntityMeleeAttacker` hit windows — `EntityBrain` never calls `TakeDamage`.**
  `ExecuteAttack` = `BeginAttack(Entity.AttackDamage, hits)` (hits rolled in `[ComboHitsMin, ComboHitsMax]`,
  default 1/1) + `TriggerAttack()` + cooldown. Range decides *when* to swing, the hitbox *if* it lands. No
  hitscan fallback: a brain without an attacker logs `GameLog.Error` once in `Awake` (only when
  `DetectionRange > 0`).
- `EntityBrain` calls `EndAttack()` in `TransitionToDead` and `DisengageFromCombat`; the SMB covers state exits.
  `EntityBrain.Start` warns once when `ComboHitsMax` exceeds the driver's `MaxComboSteps`.
- **Brain gate:** `HandleAttack` returns while `_meleeAttacker.IsInAttackState` (after `FaceTarget()`, before
  the cooldown) — a ~3.3 s combo outlasts the 2 s cooldown and must not be cut. Interval =
  `max(cooldown, attack duration)`.
- **The brain faces its target while Attacking** (`FaceTarget()` every tick, `Entity.WarningTurnSpeed`) — a
  non-facing attacker whiffs.
- **Humanoid NPCs fight unarmed** (`Unarmed` hitbox, right hand, random 1–3 hit Jab → Uppercut → Jab). Armed
  NPCs (future): `RegisterHitbox("Weapon", hitbox)`.
- One damage value per attack (`Entity.AttackDamage`). Two open hitboxes can each hit the same target once
  (dedupe is per `WeaponHitbox`).
- **Known gap:** spider `AttackRange` 0.8 is root-to-root, but agent avoidance holds it ≈ 1.0 m from an NPC →
  spiders engage NPCs but never bite them (they do bite the player). Retune the spider SO if needed.

---

## Code Review Checklist — AI

| Severity | Pattern |
|----------|---------|
| HIGH | Attacking entity without `EntityMeleeAttacker` on the root or `EntityAnimationEventReceiver` on the Animator GO — attacks animate but deal no damage |
| HIGH | `EntityBrain` (or any AI code) calling `TakeDamage` directly |
| MEDIUM | Hitbox GO under a ragdoll bone carrying a `Rigidbody` — drivers collect every child Rigidbody for the ragdoll |
| MEDIUM | Clip event id (`HitboxEnable("Bite")`) not matching a registered `_hitboxes` id — one warning, then silent misses |
| MEDIUM | `_onLootRequested` unwired on a prefab that has its own `EntityPresence` / `NPCPresence` — corpse never opens |
