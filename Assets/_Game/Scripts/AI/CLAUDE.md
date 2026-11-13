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
| `EntityBrain` | State machine Idle / Patrolling → (Suspicious) → Warning → Engaging ⇄ Attacking → (Searching) → back; witnesses: (Suspicious) → **Watching** → back; theft witnesses / restricted-area owners: **Pursuing** → **Confronting** → back; knockout winner over the player: **Looting** → back; toll collector: **Intercepting** (driven by `TollGate`) → back or brawl; Dead. Implements `ICombatStateProvider` and `Game.Combat.ISneakAttackTarget`. Targets from `TargetRegistry` (radius) and `EntityPerception` (stealth targets). `DebugStateName` for debug views. Patrolling only on an `NpcRoutine` Patrol route (`SetRoutineTarget`) — no waypoints of its own; without a routine it idles / wanders. A stored routine request is re-applied in `OnEnable` (respawn). |
| `EntityPerception` | Vision-cone awareness (0..1) of **stealth targets** (the player) — cone + LOS raycast + 360° proximity radius, math in `Game.Stealth.StealthDetection`. Ticked by the brain; never changes brain state itself. Static `Active` list for the F3 overlay; detection gizmos. On `Entity_base` root. |
| `EntityHealth` | Health for any entity; `IDamageable`. On death: stops NavMeshAgent (optional), `PersistentID.RegisterDeath()`, death anim. Body stays (ragdoll, never `SetActive(false)`). Knockout instead of death for a brawl — see **Unconscious** below. |
| `FactionMember` | Targetable faction participant; self-registers with `TargetRegistry`. **One faction source per GO:** an entity always uses `PersistentID.Entity.Faction` (the `Entity` SO); the serialized `_faction` is only for a GO without an Entity — the Player (`Faction_Player`). `_faction` set on an entity → `GameLog.Error`, ignored. `IsPlayer` = an `IStealthTarget` component exists (even disabled). Brain / perception / attacker resolve the same-GO member in `Awake` (no inspector field). Consumers read `member.Faction` at query time — never copy it into their own field (future runtime faction change). **Only the player may implement `IStealthTarget`** (it is the player marker). |
| `TheftPursuitRegistry` | **Static** claim set of theft ids (reset on play). `TryClaim(id)` — first catcher wins; `IsClaimed(id)` tells the other pursuers to give up. `TheftPursuitRegistryTests`. |
| `TargetRegistry` | **Static** registry of live `FactionMember`s — use it instead of `FindGameObjectWithTag`. Reset on play-mode enter (`SubsystemRegistration`). `Player` = the registered `IsPlayer` member (null when absent; a second one is ignored with a warning, promoted if the first unregisters) — how non-player systems (`NPCPresence`) get the player's faction; Player-side systems (`LockOnSystem`, `SleepSystem`) read their own same-GO `FactionMember`, the same component. `NPCPresence` warns once if it is null. `FindClosestHostile(..., skipStealthTargets)` / `FindClosestHostileStealthTarget` / `FindClosestNonHostileStealthTarget` (witnesses). |
| `ICombatStateProvider` | Read-only "in combat?". Implemented by `EntityBrain`, polled by `NPCPresence`. |
| `EntityMeleeAttacker` | Entity-side owner of named `WeaponHitbox`es — see below. |
| `AttackComboPlan` | Pure combo bookkeeping (`Begin`, `TryAdvance`, `Reset`) for `EntityMeleeAttacker`. `AttackComboPlanTests`. |
| `EntityAnimationEventReceiver` | On the **Animator GO**. Hit-window events by id → attacker; SMB notifications → attacker state counter; `ComboWindowOpen()` → `attacker.OnComboWindowOpen()`. |
| `SMB_EntityAttackState` | On attack states of `EntityBase.controller` and `Humanoid_Template`. Enter/exit → receiver → attacker counter. Receiver lookup cached; no-op on the Player. |
| `SMB_DeathState` | On the death animator state. |
| `EntityStance` | Hidden stance meter (`IStanceTarget`) — stagger / knockdown, see **Stance** below. |
| `EntityPresence` (`Game.World`) | Base `IInteractable` for every entity — see below. |
| `NPC/NPCPresence` | `: EntityPresence` — adds dialogue. Alive **and hostile to the player** (`FactionSO.AreHostile(own, TargetRegistry.Player.Faction)`, read live; no player → not hostile) → no prompt, `CanInteract` false (bandits); corpse loot unchanged. |
| `NPC/NPCMemoryComponent`, `NPC/NPCDialogueGraphComponent` | An NPC's active memory set; resolve available dialogue start nodes / choices. |
| `NPC/TollGate`, `NPC/TollGateRules` | Exit toll (spec-racket-guard-exit-toll) — see **Toll gates** below. `TollGateRules` is pure (`TollGateRulesTests`). Racket data: `Game.NPC.TollRacketSO` (`ScriptableObjects/Entities/NPC/`). |
| `NPC/NpcRoutine`, `NPC/NpcRoutineBlock` | Daily routine (Stand / Patrol blocks — owns all patrol routes) — see below. `NpcRoutineSchedule` (`FindBlock`, `FindAllDayBlock`, `NearestIndex`) is pure (`NpcRoutineScheduleTests`). |

---

## NPC routines (NpcRoutine → EntityBrain)

- **Patrol routes live only in routines** — `EntityBrain` has no waypoint field. An entity without an `NpcRoutine`
  (monsters) idles / wanders around its spawn and returns there after a fight. A guard that patrols all day gets an
  `NpcRoutine` with one `0 → 0` Patrol block (WarehouseGuard, bandit).
- `NpcRoutine` (scene component on the NPC instance, `[DefaultExecutionOrder(100)]` → after `EntityBrain.Start`) holds
  `NpcRoutineBlock`s: `startHour` (incl.) → `endHour` (excl., wraps midnight, start == end = all day), `NpcActivity`
  **`Stand`** (`spot` = position + facing; empty = the spawn spot) or **`Patrol`** (`waypoints` list, looped,
  `PatrolWaitTime` at each; no non-null waypoint → stands instead, one warning). First match wins; no match = stand at
  the spawn spot. **Stateless, not saved** — the block is derived from `GameClock.Active.Hour`, polled every
  `_pollInterval` (1 s).
- Block change → `EntityBrain.SetRoutineTarget(spot, route, snap)`; the brain stays the only mover. Snap (agent `Warp`)
  on the first evaluation (region load) and on `OnTimeSkipped` (sleep); otherwise walk. A **new route** starts at its
  **nearest waypoint** (flat distance, `NpcRoutineSchedule.NearestIndex`); a snap warps onto it and continues with the
  following one; resending the same route keeps the current index (post-combat resume). The nearest pick happens when the
  route is actually applied (after combat / dialogue / a stagger), not when requested. Destroyed waypoints are dropped;
  none left → stands (one warning). Applied at once while Idle /
  Patrolling, else on the next non-combat return (`ResumeNonCombat`, `CancelWarning`, `DisengageFromCombat`) — so combat
  / witness / theft flows need no routine checks. Idle on a stand target walks there, stops within
  `WaypointArrivalThreshold`, then turns to the spot's yaw.
- Walking changes wait while `CursorManager.IsLocked` is false (dialogue / trade / menus — the NPC never walks off
  mid-conversation); snaps don't. A snap while Suspicious / Watching drops the witness flow (perception reset) and warps;
  combat / Pursuing / Confronting keep the target for their exit (walk). Requests before `Start` completed, on a disabled
  brain or a corpse (`EntityHealth.IsDead`, incl. save-restored ones) are stored only. Pushed > 2× the arrival threshold
  off the spot → walks back; a partial path logs one warning.
- No clock (region played without Core) → the first all-day block (`FindAllDayBlock`) applies once (snap), else no
  routine (idle wander); one info log.
- `CurrentBlock` / `CurrentActivity` are the hook for future activity animations and dialogue / trade gating.
- **Story sequences own NPCs through `Suspend()` / `Resume(snap)`** (e.g. `Game.World.ArrivalSequence`): while
  `IsSuspended` no block is sent, so the sequence's own `SetRoutineTarget` calls stick; `Resume` re-sends the current
  block at once (snap = warp). Always resume what you suspend (also from `OnDisable`).
- StartingTown (spots under `NpcRoutineSpots`), 06–21 stand at spawn / 21–06 spot: Storekeeper on the commute north of the Company store and
  Cook by the canteen (both spots date from the removed houses; move them when those NPCs move indoors), Blacksmith in his room at the Timberworks forge, Warden outside the Company
  office door on the outcrop (NPCs can't open doors). Old Hobb (`TentMate`): 06–21 Patrol between his hut door (`worker-mate`) and the Player's Zone campfire, 21–06
  `Spot_TentMate_Night` outside his hut door. `DockGuard`: 06–21 spot `DockGuard_GuardPost` (in front of the dock guard post), 21–06 Patrol on
  its deck (`NpcPatrolRoutes/DockGuard`). `WarehouseGuard` and `bandit`: all-day Patrol. `CompanyGuard_ArrivalLeft` / `Right`: all-day stand at `Spot_ArrivalGuard_Left` / `Right` (arrival gate). `RacketGuard` (Harl): 06–14
  `Spot_RacketGuard_QuarryGate`, 14–21 `Spot_RacketGuard_WildernessGate` (his toll posts), 21–06 `Spot_RacketGuard_Night` outside
  The Short Measure. `BeatenWorker` (Tam): all-day stand at `Spot_BeatenWorker_Campfire`.

---

## EntityMeleeAttacker

- `_hitboxes` = `Id` + `WeaponHitbox`. Clip events open/close them by id (`""` = all). `RegisterHitbox` /
  `UnregisterHitbox` for runtime (equipped-weapon) hitboxes. Unknown id → one warning per id.
- `AggroTarget` (`SetAggroTarget`, set by the brain for instant-aggression trespass): hits land on it even when its
  faction isn't hostile.
- `BeginAttack(damage, comboHits)` / `EndAttack()` bracket an attack; `OpenWindow` is ignored while
  `!IsAttacking`. Hits are faction-filtered (target's `FactionMember` must be hostile; none → ignored), then
  `TryReceiveHit → TakeDamage(CurrentDamage, gameObject)` on `NotBlocked`.
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
- **Witness mode is per target, not per entity.** Every StartingTown NPC (except Tam, `NPC_BeatenWorker`: passive `DetectionRange 0` since event-001, so he never witnesses thefts or trespass) uses its own `NPCEntity` asset with
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
- **Trespass (restricted areas — `Game.World.RestrictedArea`):** perception has a third mode between hostile and witness
  (priority hostile > trespass > witness): a non-hostile stealth target standing inside an area **this entity owns**
  (`NPCEntity` listed, or its faction listed; area restricted now = hours window + not in grace) is perceived with the
  **hostile formula** — walking or sneaking, sneak-shortened — at `TrespassSightRange` (`DetectionRange`, or the witness
  profile's `TheftSightRange` for a passive owner). `IsTrespassing` / `TrespassArea`; F3 overlay + gizmo label
  "Trespass". The meter → Suspicious → full awareness → `RespondToTrespasser(area)`: `TrespassersSpotted++`, then
  - normal area: `TrespassAlertBarks` bubble + **Pursuing** with `TheftKind.Trespass` on the area's open incident
    (`RestrictedArea.OpenIncidentId`, negative ids, reused until claimed → several owners share one chase, first catcher
    scolds). Confronting uses `TrespassScoldBarks`; `area.StartGrace(trespassGraceDuration)` at the catch (before the
    dialogue-channel check), `HoldGrace` every frame while the scold is open, `StartGrace` again when it closes. An escape
    gives no grace. A theft while chasing a trespasser keeps `_pursuitArea` (the scold still starts its grace).
    **Leaving the area ends the chase:** every Pursuing tick, `TheftDetection.ShouldStopTrespassChase` (trespass kind +
    area, and the player outside every box or the area no longer `IsRestrictedNow`) → one `TrespassFledBarks` bubble
    (`Barks_TrespassFled`, own no-repeat index; null = silent) + `EndPursuit` → patrol / routine. Crossing into another
    area the owner also owns (`TryFindTrespassedArea`) hands the chase over to it instead. No LOS needed, no grace — walking back in
    refills the meter from 0. A theft seen mid-chase switches `_pursuitKind`, so that chase follows the theft rules.
    `IsTrespassTarget` re-checks `TrespassArea.IsRestrictedNow` (grace / hours can change between perception scans).
  - `InstantAggression` area: bubble + `EntityMeleeAttacker.SetAggroTarget(player)` + `ForceAware` + **Engaging** (no
    warning). The aggro target makes the faction filter accept the non-hostile player. `SetCombatState(false)` clears it
    (disengage, search end, death, routine return), and so does a damage retarget onto another attacker. Searching
    re-engages a re-sighted trespasser only if it is the current aggro target; otherwise it leaves combat and applies the
    area's rules (`RespondToTrespasser`); a witnessed target is ignored.
  - An owner already **Watching** (perception engaged, no rescans) checks `TryFindTrespassedArea` itself and responds.
  - Unknown-source hits never pick a trespassing (non-hostile) perception target. Known gap: lock-on can't target an
    aggro owner (it only takes hostile factions).
- **Hearing:** hostile and trespass perception also hear a walking / running player (Entity walk / run hearing radii,
  360°, halved without LOS) — see `Scripts/Stealth/CLAUDE.md` → Detection formula. It fills the same meter, so the
  brain needs no change: Suspicious faces `LastSeenPosition` (= last *sensed*), alerted states keep tracking a heard
  target. Checks that mean "sees" (unknown-source hit retarget, Watching trespass check) read `CanSeeTarget`, which
  stays vision-only. Hearing radii are capped at the sight range, so Warning's `dist > DetectionRange` cancel can't loop.
- **Detection while patrolling:** perception ticks every frame in every state and `HandlePatrol` checks it before path
  handling; the cone follows `transform.forward`, which the NavMeshAgent turns along the path (`updateRotation` is never
  disabled) — a walking guard sees ahead, a waiting one keeps its arrival facing. Verified in play mode on the WarehouseGuard.
- **Sneak attack:** `IsUnawareOf` = Idle/Patrolling/Suspicious, not fully aware, and not damaged within
  `StealthConfigSO.damageAlertDuration` (so passive / neutral entities that never fight back only give the
  bonus once). `PlayerCombat` multiplies the hit by `CombatConfigSO.sneakAttackDamageMultiplier`.

---

## EntityPresence / NPCPresence (interaction & loot)

`EntityPresence` sits on the `Entity_base.prefab` root, so every entity is found by `InteractionSystem` and
shows its world-space `EntityUI` on hover. `NameTag` is null-guarded (`""` without `PersistentID.Entity`).

- **Loot:** `IsLootable = IsDown && HasLoot` (`IsDown` = dead **or unconscious and not yet getting up**) (`HasLoot` = `InventorySystem.Count > 0`).
  When the get-up clip starts (`EntityHealth.GettingUp`), `EntityPresence` raises `GameEventSO_Inventory _onLootSourceLost`
  (`OnLootSourceLost`) with its inventory; `ContainerUI` closes if that inventory is the one open. Wired on `Entity_base`,
  `NPC_base Variant` and non-prefab scene NPCs (Brann). `CanInteract => IsLootable`,
  `InteractPrompt` = `"Loot"` when lootable, else `""`. `Interact()` → `OpenLoot()` raises
  `GameEventSO_ContainerOpenRequest _onLootRequested` with the entity's `InventorySystem` and
  `takeOnly = true` — reusing the container pipeline (`ContainerSystem` / `ContainerUI`). An emptied corpse
  is inert (no prompt, `Interact()` no-op).
- **`NPCPresence._availableAfter`** (optional `WorldFact`): no "Talk" prompt / dialogue until the fact is true (Tam before
  event-001). Loot and NPC-forced topics (`RequestForcedTopic`) ignore it. No `WorldStateManager` → available.
- **`NPCPresence.RequestForcedTopic(node, thenTopics = false)`**: `thenTopics` → the chain ends on the NPC's normal topic
  list (Farewell included) instead of closing (Hobb's arrival briefing). Toll and Warden topics keep the default.
- **`EntityBrain.EndBrawl(target)`**: disengages a fight with that target back to the routine (story sequences calling off a
  scripted brawl); no-op otherwise.
- **`EntityBrain.Say(line, duration, priority)`**: scripted speech bubble over the NPC (story sequences); false without a
  line or `_onSpeechBubbleRequested`.
- **`NPCPresence`** overrides `Interact()`: dead / unconscious → `base.Interact()` (loot, never dialogue); alive and out of
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

## Stance (EntityStance — spec-ai-stance-stagger-knockdown, spec-player-stance-stagger-knockdown)

- `EntityStance` (`IStanceTarget`, on the `Entity_base` root → every entity) holds a hidden meter (`Entity` SO
  `[Header("Stance")]`), state in the shared `Game.Combat.StanceMeter` (also used by the player's `PlayerStance`;
  `StanceMeterTests`). Rules are pure in `Game.Combat.StanceMath` (`StanceMathTests`): crossing the threshold
  (`_staggerThreshold` × max, from above to at/below) **staggers once per drain** (re-armed when regen brings stance
  back above it); reaching 0 **knocks down**; a hit with `canKnockDown = false` never takes stance below the threshold.
  Regen after `_stanceRegenDelay`, **never while staggered / knocked down** (no re-stagger stun-lock); refilled to max
  on getting up. A floored or fully absorbed hit changes nothing and does **not** reset the regen delay (otherwise a
  sword user pins stance at the threshold forever). Hyper-armor: `_attackStanceResistance` while
  `EntityMeleeAttacker.IsInAttackState`. Runtime only (not saved, reset in `OnEnable`). One info log per stance hit.
- Sources: `PlayerCombat.ResolveHit` → `ApplyStanceDamage` (after `TakeDamage`, skipped on a lethal hit) and
  `EntityMeleeAttacker` on `PerfectBlock` → `ApplyPerfectBlockStanceDamage(Entity.PerfectBlockStanceDamage)` (never
  knockdown, **skips hyper-armor** — the parried attacker is always mid-attack). By design a perfect block on an entity
  already at/below the threshold deals nothing.
- **AI hits deal stance damage** to any `IStanceTarget` (player or another NPC): `EntityMeleeAttacker.HandleHit` →
  `ApplyStanceDamage` after `TakeDamage` on `NotBlocked` (skipped on a lethal hit), `ApplyBlockedStanceDamage` on
  `Blocked` (player: × `blockStanceMultiplier`, never knockdown). Amount = `Entity.ResolveAttackStance(step, hits)`:
  monsters → fixed `_attackStanceDamage` / `_attackCanKnockDown` (rat 10, viper 15, spider 20, wolf 25 KD, grunt 30 KD);
  `NPCEntity` → `_attackStanceDamage` (15) + data-only `_weapon` (`WeaponSO.ResolvedStanceDamage`, no visual / hitbox),
  × `_finisherStanceMultiplier` on the last hit of a multi-hit combo; knockdown = the weapon's `ResolvedCanKnockDown`
  (no weapon = never). Step / hits come from `AttackComboPlan` (`CurrentStep` advances at `ComboWindowOpen`).
- Reaction: `AIAnimationDriver.TriggerStagger(variant 0/1)` / `TriggerKnockDown()` (base default = `TriggerGetHit`, so
  monsters fall back to GetHit). Timed by `_staggerDuration` / `_knockdownDuration` — **match the clip lengths**
  (humanoid 1.1 / 4.6 s, monsters 0.6 / 1.5 s).
- Brain: `Staggered` / `KnockedDown` → `EndAttack()` + `HoldAgentForReaction` (previous `isStopped` saved once; re-held
  every paused tick, so an agent off the NavMesh at the hit is stopped once back on it); `Update` skips state handling
  while `IsIncapacitated`; `Recovered` restores the agent and the state continues. **Event-driven entry points must
  respect the pause:** a damage reaction stores the target and engages on recovery, `SetRoutineTarget` stores the spot / route and
  walks to it on recovery (never snaps mid-reaction), `HandleTheftCommitted` ignores thefts. `ClearStanceReaction()`
  drops the bookkeeping on recovery, death (`TransitionToDead`) and `OnEnable` (respawn mid-reaction raises no
  `Recovered`). `EntityPresence.IsAliveAndOutOfCombat` is false while incapacitated (no dialogue with a downed NPC).
- `EntityStance.Recover` calls `AIAnimationDriver.ClearStanceReaction()` (resets unconsumed `Stagger` / `KnockDown`).
- `EntityHealth` skips `TriggerGetHit` while incapacitated (it would cut the clip); the bridge also resets a pending
  `GetHit` on stagger / knockdown (same-hit order: `TakeDamage` → GetHit, then stance).

---

## Unconscious (spec-unconscious-knockout)

- **Who decides:** the victim. Hits go through `IDamageable.TakeDamage(amount, attacker)` (`PlayerCombat.ResolveHit`,
  `EntityMeleeAttacker.HandleHit`); `TakeDamage(amount)` = unknown source = lethal. A 0-HP blow is a **knockout** when
  `FactionMember.IsKnockoutPair(attacker, victim)`: both `IsHumanoid` (`IsPlayer` or `Entity is NPCEntity`) and not
  `FactionSO.AreHostile` (pure rule `Game.Combat.KnockoutRules`, `KnockoutRulesTests`). Monsters / hostiles / unknown
  always kill, even a downed target. No `RegisterDeath`, no kill, no XP.
- `EntityHealth` (needs `_combatConfig`, wired on `Entity_base` + unpacked scene NPCs like Brann — null = always lethal):
  `IsUnconscious` from the knockout until the get-up clip ends. HP regains linearly to
  `CombatConfigSO.unconsciousRecoverHealthFraction` over `unconsciousDuration`, then `AIAnimationDriver.SetUnconscious(false)`
  (get-up), then `getUpDuration` later `RegainedConsciousness`. A knockout-pair hit while down is ignored (finishing blow
  deferred). Same-GO events `KnockedOut` / `RegainedConsciousness`. `OnEnable` resets it (not saved).
- `EntityBrain`: `KnockedOut` → target dropped, combat off, attack ended, perception reset, agent held, state Idle;
  `Update` returns early while unconscious (no perception); `RegainedConsciousness` → `ResumeNonCombat()` (routine, never
  a re-attack). Routine requests / thefts / damage reactions are stored or ignored while down. `HasValidTarget` is false for
  an unconscious target this brain could only knock out → the winner disengages to its routine.
- **Brawl retaliation:** `HandleHealthChanged` with a known, live, conscious, **non-hostile** attacker forming a knockout
  pair with this entity (and `DetectionRange > 0`) → `EntityMeleeAttacker.SetAggroTarget(attacker)` + engage (same aggro
  mechanism as instant-aggression trespass). Passive NPCs still never fight back.
- `EntityStance` / `PlayerStance` treat unconscious like dead (meter refilled, `Apply` ignored); hit resolvers skip stance
  on an unconscious target. `EntityPresence.IsAliveAndOutOfCombat` is false while down.
- A loot screen open on an NPC that gets up closes (`OnLootSourceLost`, see EntityPresence below).

### Knockout loot — `Looting` state (spec-knockout-gold-looting)

- **Who:** only the NPC whose blow knocked the **player** out. `EntityMeleeAttacker.HandleHit` records the victim when
  `IsUnconscious` flips inside its own `TakeDamage` call; `TakeKnockedOutVictim()` reads + clears it (also cleared by the
  next `BeginAttack`). The brain's `!HasValidTarget()` branch in `HandleEngage` / `HandleAttack` calls `TryStartLooting()`
  before disengaging. Other brawlers disengage as before.
- **Flow:** combat off (aggro cleared), perception reset, taunt bubble (`CombatConfigSO.knockoutLootTauntBarks`), walk at
  `BaseSpeed` → flat distance ≤ `knockoutLootReachDistance` → stop, `AIAnimationDriver.TriggerPickUp()` → at
  `knockoutLootGrabTime` raise `OnGoldTheftRequested` (`GoldTheftRequest`, base + random fraction in
  `[knockoutLootFractionMin, Max]`; the player's `GoldSystem` deducts with `Game.Economy.GoldTheftRules`) → `Taken` ×
  `knockoutLootGold` into its own `InventorySystem` → thanks bubble (had ≥ base) or poor bubble → `ResumeNonCombat()` at
  `knockoutLootClipDuration`. The gold is lootable back like any NPC inventory (and saved with it).
- **Timing:** the grab / end timers start when the animator is actually in `PickUp` (`AIAnimationDriver.IsPickingUp`;
  1.5 s fallback) — on arrival the Base Layer is often still blending out of the knockout punch.
- **Cancel:** the victim must stay "lying" = `IsUnconscious` and not `IKnockoutRecovery.IsGettingUp` (`PlayerHealth`).
  Up before the grab (walking or mid-clip) → no gold, `EndLooting` → routine. Every exit (end, cancel, hit reaction,
  hostile spotted by the throttled radius scan, own knockout, death, `OnDisable`) goes through `StopLooting()` →
  `AIAnimationDriver.CancelPickUp()` (resets the `PickUp` trigger, blends a playing clip back to locomotion — an
  unconsumed trigger would otherwise fire on the next locomotion entry). The end waits while the cursor is unlocked (the
  player may be up and talking to the robber). Unreachable body: invalid path, or a partial path ending > 2 × reach away
  → gives up. The attacker's knockout record is cleared on disengage / own knockout / death / disable, so a stale one
  can't make it loot someone else's knockout.
- Setup (warned once if missing): `EntityHealth._combatConfig` with `knockoutLootGold`, a same-GO `InventorySystem`,
  `EntityBrain._onGoldTheftRequested` (wired on `Entity_base` and unpacked Brann). Looting is reactive to hits (in
  `HandleHealthChanged`'s list), ignores thefts, stores routine requests; not unaware (no sneak bonus). Bubbles fall back
  to the default duration / priority without a stealth config.

---

## Toll gates (TollGate → EntityBrain — spec-racket-guard-exit-toll)

- `TollGate` (scene, under the StartingTown root `TollGates`): zone = child `BoxCollider`s (trigger, Ignore Raycast,
  containment on demand like `RestrictedArea`) spanning the whole gate; `_collector` (Harl's `EntityBrain`), `_post` (his
  routine spot), `_paidFact` (`WorldFact`), hours, `_racket` (`TollRacketSO`: shared deferred / beaten facts, forced openers,
  pay / defer / refuse `DialogueSignalSO`s, talk distance, duty radius, timeouts), `_onDialogueSignal`.
- Flow: player in zone + `CanCollect` (in hours, gate unpaid, racket not beaten, gate **armed**, collector on duty = on its
  routine, conscious, within `dutyRadius` of the post) → `EntityBrain.BeginIntercept` → **Intercepting** (non-combat walk at
  `EngageSpeed`, waits for a locked cursor, stops facing the player) → `Reached` → `NPCPresence.RequestForcedTopic` (the
  "other gate paid" opener when any sibling gate of the same racket is paid) → dialogue closes → last signal: pay → paid fact,
  defer → racket deferred fact, none / refuse → `EntityBrain.StartBrawl` (aggro + `ForceAware` + Engaging, same mechanism as
  instant-aggression trespass). Brawl: player unconscious → this gate paid (the normal knockout **Looting** robs them); collector
  knocked out by the player's blow (`EntityBrain.WasHitThisFrameBy`, any fight incl. a sneak hit) → racket beaten fact. Pay / defer
  are applied **on the pick** (`OnDialogueSignal`, routed to the last gate that opened the toll dialogue), so an interrupted talk
  can't take the gold without recording it. The dialogue counts as open only if the cursor unlocked synchronously on the request
  (else re-armed, never a refusal). While approaching, the player leaving the zone or leading him beyond `dutyRadius` = walked
  past = brawl; a timeout / hostile / third-party abort keeps the gate armed. A loss only pays when he was fighting the player.
  Other endings (answered, brawl over) disarm the gate until the player leaves its zone.
- `EntityBrain.Intercept` reports `Aborted` whenever the state left Intercepting another way (hit, hostile spotted, own knockout,
  timeout, target lost, disable) until the caller calls `ClearIntercept()`; `EndIntercept()` returns to the routine. Intercepting
  is in `HandleHealthChanged`'s reactive list, ignores thefts, stores routine requests, gives no sneak-attack bonus.
- Forced toll topics are not reachable from a memory, so Quest Explorer may list them as unattributed (expected).

## Code Review Checklist — AI

| Severity | Pattern |
|----------|---------|
| HIGH | Attacking entity without `EntityMeleeAttacker` on the root or `EntityAnimationEventReceiver` on the Animator GO — attacks animate but deal no damage |
| HIGH | `EntityBrain` (or any AI code) calling `TakeDamage` directly |
| HIGH | A damage source calling `TakeDamage(amount)` with a known attacker — use `TakeDamage(amount, attacker)` or brawls kill |
| MEDIUM | Hitbox GO under a ragdoll bone carrying a `Rigidbody` — drivers collect every child Rigidbody for the ragdoll |
| MEDIUM | Clip event id (`HitboxEnable("Bite")`) not matching a registered `_hitboxes` id — one warning, then silent misses |
| MEDIUM | `_onLootRequested` unwired on a prefab that has its own `EntityPresence` / `NPCPresence` — corpse never opens |
