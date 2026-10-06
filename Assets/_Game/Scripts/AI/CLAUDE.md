# CLAUDE.md — Assets/_Game/Scripts/AI

> Loaded when Claude accesses files in this folder. Entity brains, health, faction
> targeting, and NPC presence/memory components. Namespace: `Game.AI`.

---

## What's here

| File | Role |
|------|------|
| `EntityBrain` | Generic state machine: Idle → Patrolling → (Engaging → Attacking) → Dead. Implements `ICombatStateProvider`. Finds targets via `TargetRegistry`. |
| `EntityHealth` | Generic health for any entity (enemy/NPC/neutral). Implements `IDamageable`. On death: stops NavMeshAgent (optional), calls `PersistentID.RegisterDeath()`, triggers death anim. Body stays in scene (ragdoll, no `SetActive(false)`). |
| `FactionMember` | Tags a GO as a targetable faction participant. Self-registers with `TargetRegistry` on enable. Faction from `PersistentID.Entity.Faction`, or `_factionOverride` (Player uses override — no PersistentID). |
| `TargetRegistry` | **Static** runtime registry of live `FactionMember`s. Query this instead of `FindGameObjectWithTag`. Reset on play-mode enter via `SubsystemRegistration`. |
| `ICombatStateProvider` | Read-only "is this entity in combat?". Implemented by `EntityBrain`, polled by `NPCPresence`. |
| `NPCPresence` | `: EntityPresence` (base lives in `Game.World`, on `Entity_base.prefab`). Overrides `Interact()`: **dead → `base.Interact()` (loot the corpse via the shared container pipeline)**; alive & out-of-combat → raises `_onDialogueRequested`; in-combat → blocked. `InteractPrompt` = `"Loot"` when dead-with-loot, `"Talk"` when alive, empty when dead-and-empty; `CanInteract => IsLootable || IsAliveAndOutOfCombat` (`IsLootable = IsDead && HasLoot`). A dead NPC always routes to `base.Interact()` (never dialogue), so an emptied corpse is simply inert. Name-tag + alive/combat/dead gating helpers inherited from `EntityPresence`. Loot uses the inherited `_onLootRequested`, which must be wired on this variant's own `NPCPresence` (the inherited base `EntityPresence` is removed on the NPC variant, so the base prefab's wiring does NOT carry over). |
| `NPCMemoryComponent` / `NPCDialogueGraphComponent` | Hold an NPC's active memory set and resolve available dialogue start nodes / choices. |
| `SMB_DeathState` | StateMachineBehaviour on the death animator state. |
| `EntityMeleeAttacker` | On the entity root. Owns named `WeaponHitbox`es (`_hitboxes`: `Id` + `Hitbox`), opens/closes them by id from animation events, faction-filters hits (target's `FactionMember` must be hostile to ours; no `FactionMember` → ignored) and resolves `TryReceiveHit → TakeDamage(CurrentDamage)` on `NotBlocked`. `BeginAttack(damage)` / `EndAttack()` bracket an attack; `OpenWindow` is ignored while `!IsAttacking`. Unknown id → one warning per id. `RegisterHitbox` / `UnregisterHitbox` for runtime (equipped-weapon) hitboxes. **Combo:** `BeginAttack(damage, comboHits)` plans N hits (clamped to `AIAnimationDriver.MaxComboSteps`; no driver → 1); `OnComboWindowOpen()` advances and calls `TriggerComboStep(next)`; each step's clip opens its own window → damage per hit. **Attack-state counter:** `NotifyAttackStateEntered/Exited` (from the SMB) count active attack states; `EndAttack()` fires when the count returns to 0 — `IsInAttackState => count > 0`. `EndAttack()` never touches the counter (it mirrors the animator), and calls `AIAnimationDriver.CancelAttack()` to drop queued attack triggers; `OnDisable` resets the counter. |
| `AttackComboPlan` | Pure plain-C# combo bookkeeping (`Begin(hits, maxSteps)`, `TryAdvance`, `Reset`) owned by `EntityMeleeAttacker`. EditMode-tested (`AttackComboPlanTests`). |
| `EntityAnimationEventReceiver` | On the **Animator GO** (monsters: `CreatureVisual`). `HitboxEnable(string id)` / `HitboxDisable(string id)` → attacker (empty id = all hitboxes); `NotifyAttackEntered()` / `NotifyAttackExited()` from the SMB → attacker state counter; `ComboWindowOpen()` → `attacker.OnComboWindowOpen()`; `ComboWindowClose()` no-op. |
| `SMB_EntityAttackState` | On `EntityBase.controller`'s `Attack` state **and** `Humanoid_Template`'s `Attack_1/2/3_State`. `OnStateEnter`/`OnStateExit` → receiver → attacker counter (attack ends when the last attack state exits: normal end, GetHit interrupt, death). Receiver lookup cached once per instance; silent no-op on the Player (no receiver). |

---

## Rules

- **No `event Action` across system boundaries** (project-context.md). `ICombatStateProvider` is polled via `GetComponent`, not subscribed. Use a `GameEventSO<T>` channel if push is ever needed.
- `EntityHealth.MaxHealth` is driven by `PersistentID.Entity.BaseHealth` — falls back to `100f` when no `PersistentID`/Entity is assigned.
- **Passive entities** (e.g. `Entity_HumanoidNPC`) use `_detectionRange = 0` — they never detect targets. Any validation comparing `WarningRange` vs `DetectionRange` (`Entity.OnValidate`, `EntityBrain` runtime guard) must skip when `DetectionRange <= 0`, or it fires a false warning (`0 >= 0`) on every asset load.
- `PersistentID`, `AIAnimationDriver`, and `NavMeshAgent` are all **optional** on an entity — guard every access (`TryGetComponent` / null check).
- **AI damage flows only through `EntityMeleeAttacker` hit windows — `EntityBrain` never calls `TakeDamage`.** `ExecuteAttack` = `BeginAttack(Entity.AttackDamage, hits)` (hits rolled in `[Entity.ComboHitsMin, ComboHitsMax]`, default 1/1 = monsters unchanged) + `TriggerAttack()` + cooldown; the range check only decides *when* to swing, the hitbox decides *if* it lands. Hard switch, no hitscan fallback: a brain without an attacker logs `GameLog.Error` once in `Awake` (only when `DetectionRange > 0` — passive entities stay silent) and its attacks deal no damage.
- `EntityBrain` calls `EndAttack()` in `TransitionToDead` and `DisengageFromCombat`; `SMB_EntityAttackState` covers animator state exits.
- `EntityBrain.Start` warns once when `Entity.ComboHitsMax` exceeds the driver's `MaxComboSteps` (the attacker clamps silently).
- **Brain gate:** `HandleAttack` returns while `_meleeAttacker.IsInAttackState` (after `FaceTarget()`, before the cooldown check) — a combo (~3.3 s) outlasts the 2 s cooldown and must never be cut mid-swing. Effective interval = `max(cooldown, attack duration)`.
- **Combo crossfades need a counter, not exit-only:** Unity fires `OnStateEnter(next)` at transition start and `OnStateExit(previous)` at transition end, so an exit-only SMB would end the attack on every `Attack_1 → Attack_2` blend.
- **The brain faces its target while Attacking** (`FaceTarget()` every `HandleAttack` tick, reusing `Entity.WarningTurnSpeed`) — hits are spatial, a non-facing attacker whiffs.
- **Humanoid NPCs fight unarmed** (`Unarmed` hitbox on the right hand, random 1–3 hit Jab → Uppercut → Jab combo). Armed NPCs (future): register the equipped weapon's `WeaponHitbox` via `RegisterHitbox("Weapon", hitbox)`. Player sword clips' parameterless `HitboxEnable` events arrive as `""` → open every hitbox.
- **Spider vs humanoid NPC range gap (pre-existing):** spider `AttackRange` 0.8 is root-to-root, but NavMeshAgent avoidance holds it ≈ 1.0 m from an NPC (agent radius 0.5) → spiders engage NPCs but never bite them (they do bite the player). Retune the spider SO if NPC-vs-spider fights should be two-sided.
- One damage value per attack (`Entity.AttackDamage`). Two hitboxes open at once can each hit the same target once (dedupe is per `WeaponHitbox`).

> AI animation polymorphism (`AIAnimationDriver` base, Brain/Health → Driver → Bridge contract) → `Assets/_Game/Scripts/Core/Animations/CLAUDE.md`

---

## Code Review Checklist — AI

| Severity | Pattern |
|----------|---------|
| HIGH | Attacking entity without `EntityMeleeAttacker` on the root or `EntityAnimationEventReceiver` on the Animator GO — attacks animate but deal no damage |
| HIGH | `EntityBrain` (or any AI code) calling `TakeDamage` directly — damage goes through `EntityMeleeAttacker` hit windows only |
| MEDIUM | Hitbox GO under a ragdoll bone carrying a `Rigidbody` — `MonsterAnimationDriver` collects every child Rigidbody for the ragdoll |
| MEDIUM | Clip event id (`HitboxEnable("Bite")`) not matching a registered `_hitboxes` id — one warning, then silent misses |
