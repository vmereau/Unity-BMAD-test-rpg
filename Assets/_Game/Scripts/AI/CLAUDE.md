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
| `EntityBrain` | State machine Idle / Patrolling → (Suspicious) → Warning → Engaging ⇄ Attacking → (Searching) → back; witnesses: (Suspicious) → **Watching** → back; theft witnesses: **Pursuing** → **Confronting** → back; Dead. Implements `ICombatStateProvider` and `Game.Combat.ISneakAttackTarget`. Targets from `TargetRegistry` (radius) and `EntityPerception` (stealth targets). `DebugStateName` for debug views. |
| `EntityPerception` | Vision-cone awareness (0..1) of **stealth targets** (the player) — cone + LOS raycast + 360° proximity radius, math in `Game.Stealth.StealthDetection`. Ticked by the brain; never changes brain state itself. Static `Active` list for the F3 overlay; detection gizmos. On `Entity_base` root. |
| `EntityHealth` | Health for any entity; `IDamageable`. On death: stops NavMeshAgent (optional), `PersistentID.RegisterDeath()`, death anim. Body stays (ragdoll, never `SetActive(false)`). |
| `FactionMember` | Targetable faction participant; self-registers with `TargetRegistry`. Faction from `PersistentID.Entity.Faction`, or `_factionOverride` (the Player — no PersistentID). |
| `TheftPursuitRegistry` | **Static** claim set of theft ids (reset on play). `TryClaim(id)` — first catcher wins; `IsClaimed(id)` tells the other pursuers to give up. `TheftPursuitRegistryTests`. |
| `TargetRegistry` | **Static** registry of live `FactionMember`s — use it instead of `FindGameObjectWithTag`. Reset on play-mode enter (`SubsystemRegistration`). `FindClosestHostile(..., skipStealthTargets)` / `FindClosestHostileStealthTarget` / `FindClosestNonHostileStealthTarget` (witnesses). |
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

## Perception & stealth (EntityBrain + EntityPerception)

- **Perception is optional:** null or inactive (`DetectionRange <= 0`, no config) → exactly the old radius
  behaviour, no Suspicious / Searching. With it active, stealth targets (`FactionMember.StealthTarget != null`
  — the player) are skipped by the radius scan and acquired only through awareness; NPC-vs-NPC keeps the radius.
- Idle/Patrol: full awareness → `RespondToDetectedTarget()` (Warning/Engaging rules unchanged); awareness ≥
  `suspicionThreshold` → **Suspicious** (stopped, turns to `LastSeenPosition`, back to the same waypoint when
  awareness drains to 0). Alerted states tick perception with `engaged = true`: 360° tracking within
  `DisengageRange`, awareness held at 1.
- Warning/Engaging/Attacking: `TimeSinceSeen >= Entity.LoseSightTime` → **Searching** (walk to last seen,
  rotate in place `SearchDuration`, then resume). Engaging chases `LastSeenPosition` while out of sight.
  Re-sighted during search → Engaging with no warning. `CancelWarning`, `DisengageFromCombat` and death reset
  perception (otherwise the held awareness of 1 re-detects instantly).
- **Damage reaction:** every damage source (`PlayerCombat`, `EntityMeleeAttacker`) calls
  `ISneakAttackTarget.NotifyHitBy(attacker)` right before `TakeDamage`; `EntityHealth.HealthChanged` (same GO)
  then engages **that attacker** if it is hostile (never a bystander), while Idle/Patrolling/Suspicious/
  Searching/Warning. Unknown source → the stealth target only if in sight, else the closest radius hostile.
  Skipped for passive entities. **Any new damage source must call `NotifyHitBy`.**
- **Witness mode is per target, not per entity.** Every StartingTown NPC uses its own `NPCEntity` asset with
  `DetectionRange 8` (they fight monsters) — never gate witnessing on `DetectionRange <= 0`. `ScanForTarget`:
  closest **hostile** stealth target first (`DetectionRange > 0`); none and `CanWitness` (enabled
  `WitnessProfileSO` on the `NPCEntity`) → closest **non-hostile** one, `IsWitnessing = true`. Witnessing only
  counts while the target **sneaks** (walking drains awareness); sight range = profile `WitnessRange` (no sneak
  multiplier), proximity = the sneaking one. The bandit references the same profile harmlessly — the player is
  hostile to it, so the hostile branch always wins. Brain: `RespondToDetectedTarget` routes a witnessed target
  (`IsWitnessTarget()`) to **Watching** (agent stopped, faces the player at `witnessTurnSpeed`, ticked with
  `engaged = true` → tracks 360° within `WitnessWatchRange` even if the player stands up), a hostile one to
  Warning / Engaging. Exit: flat distance > `WitnessWatchRange`, no LOS for `witnessLoseSightTime`, or a hostile
  found by the throttled radius scan (→ normal reaction) → perception reset + `ResumeNonCombat()`. Being hit
  while Watching fights back like Idle; an unknown-source hit never picks the witnessed (non-hostile) target. Entry raises one speech bubble (`OnSpeechBubbleRequested`, anchored on the
  `SpeechAnchor` child of `Entity_base`, y 2.8 — above `EntityUICanvas`) unless inside `witnessWarnCooldown`;
  `WarnedCount` (runtime only, not saved; counts only bubbles actually raised) is the hook for future escalation.
  Opening a dialogue stands the player up, so a witness never starts watching mid-conversation. Watching is **not combat**
  (`IsInCombat` false → dialogue still opens) and **not unaware** (no sneak-attack bonus). Passive NPCs still
  never fight back (`HandleHealthChanged` early return) and never reach Warning / Searching.
- **Theft witnesses (Pursuing / Confronting):** every brain listens to `OnTheftCommitted` (wired on `Entity_base`
  with `_onDialogueRequested`). It reacts only while Idle / Patrolling / Suspicious / Watching / Pursuing, with an
  active perception that `CanWitness`, a live thief **not hostile** to it (bandit / spiders ignore thefts), and
  `EntityPerception.CanSeeTheft` (cone at profile `TheftSightRange`, sneak-shortened, or the sneak-adjusted proximity
  radius, + immediate LOS raycast). **Every** seeing witness goes **Pursuing** (non-combat — `IsInCombat` false; agent
  at `EngageSpeed`, `stoppingDistance = 0.8 × theftCatchDistance`), shouts one `TheftAlertBarks` bubble and increments
  `TheftsWitnessed` (runtime, escalation hook). A new theft while Pursuing adopts the new id and restarts the timers.
  Give up (`TheftDetection.ShouldGiveUp`): no LOS for `theftChaseLoseSightTime`, beyond `theftChaseMaxDistance`, after
  `theftChaseMaxDuration`, thief dead, or the incident claimed by another witness. A hostile found by the throttled
  radius scan while Pursuing takes over (normal Warning / Engaging reaction, like Watching). Catch (`CanCatch`: flat distance ≤
  `theftCatchDistance` **and the cursor locked** — waits while a menu is open) + `TheftPursuitRegistry.TryClaim` →
  **Confronting**: stops, faces the thief, raises `OnNPCDialogueRequested` with `forcedLine` (item thefts use
  `ItemTheftScoldBarks`, else `TheftScoldBarks`, else a default line); resumes its routine when the dialogue closes
  (cursor locked again) or after `theftConfrontOpenTimeout` if it never opened. A hostile hit while Pursuing /
  Confronting triggers the normal combat reaction. Known gap: the player can still talk to a chasing NPC normally —
  it scolds right after.
- **Sneak attack:** `IsUnawareOf` = Idle/Patrolling/Suspicious, not fully aware, and not damaged within
  `StealthConfigSO.damageAlertDuration` (so passive / neutral entities that never fight back only give the
  bonus once). `PlayerCombat` multiplies the hit by `CombatConfigSO.sneakAttackDamageMultiplier`.

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
- **Passive entities** (e.g. `Entity_HumanoidNPC` — only the `NPC_base Variant` default; every scene NPC overrides
  it with its own `NPCEntity` asset, `DetectionRange 8`) have `_detectionRange = 0`. Any `WarningRange` vs
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
