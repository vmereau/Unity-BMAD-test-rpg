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
| `EntityHealth` | Health for any entity; `IDamageable`. On death: stops NavMeshAgent (optional), `PersistentID.RegisterDeath(credited)` (player credit, see **Unconscious**), death anim. Body stays (ragdoll, never `SetActive(false)`). Knockout instead of death for a brawl — see **Unconscious** below. |
| `FactionMember` | Targetable faction participant; self-registers with `TargetRegistry`. **One faction source per GO:** an entity always uses `PersistentID.Entity.Faction` (the `Entity` SO); the serialized `_faction` is only for a GO without an Entity — the Player (`Faction_Player`). `_faction` set on an entity → `GameLog.Error`, ignored. `IsPlayer` = an `IStealthTarget` component exists (even disabled). Brain / perception / attacker resolve the same-GO member in `Awake` (no inspector field). Consumers read `member.Faction` at query time — never copy it into their own field (future runtime faction change). **Only the player may implement `IStealthTarget`** (it is the player marker). |
| `TheftPursuitRegistry` | **Static** claim set of theft ids (reset on play). `TryClaim(id)` — first catcher wins; `IsClaimed(id)` tells the other pursuers to give up. `TheftPursuitRegistryTests`. |
| `TargetRegistry` | **Static** registry of live `FactionMember`s — use it instead of `FindGameObjectWithTag`. Reset on play-mode enter (`SubsystemRegistration`). `Player` = the registered `IsPlayer` member (null when absent; a second one is ignored with a warning, promoted if the first unregisters) — how non-player systems (`NPCPresence`) get the player's faction; Player-side systems (`LockOnSystem`, `SleepSystem`) read their own same-GO `FactionMember`, the same component. `NPCPresence` warns once if it is null. `FindClosestHostile(..., skipStealthTargets)` / `FindClosestHostileStealthTarget` / `FindClosestNonHostileStealthTarget` (witnesses). |
| `FactionSO.Defends` / `AidGroupSO` | Who defends whom in a fight — see **Mutual defense** below. |
| `PlayerRelationship` | Static tier query `Of(npc)` → `RelationshipTier` Hostile / Neutral / Friendly — see **Player relationship** below. Pure `Resolve` (`PlayerRelationshipTests`). |
| `ICombatStateProvider` | Read-only "in combat?". Implemented by `EntityBrain`, polled by `NPCPresence`. |
| `EntityMeleeAttacker` | Entity-side owner of named `WeaponHitbox`es — see below. |
| `AttackComboPlan` | Pure combo bookkeeping (`Begin`, `TryAdvance`, `Reset`) for `EntityMeleeAttacker`. `AttackComboPlanTests`. |
| `EntityAnimationEventReceiver` | On the **Animator GO**. Hit-window events by id → attacker; SMB notifications → attacker state counter; `ComboWindowOpen()` → `attacker.OnComboWindowOpen()`; `WeaponSwap(AnimationEvent)` (draw / sheathe clips) → `NpcArmament.CompleteWeaponSwitch(fromDraw)` — draw vs sheathe read from the firing state name (`Draw_State` / `Sheathe_State` in `Humanoid_Template`; **renaming them** makes a superseded clip's event complete a reversed switch); no armament → no-op. |
| `NpcArmament` | Equipped NPCs: loadout, weapon vs fists, draw / sheathe, weapon loot — see **NPC equipment** below. |
| `NpcHeadAppearance` | NPC face: in `Awake` swaps the model's Head SMR mesh + material to `NPCEntity.Head` (`HeadAppearanceSO`). An incompatible mesh (bind-pose count / values, bone indices) → warning, default head kept. Asset pipeline: `Art/Characters/Humanoids/CLAUDE.md` → NPC head variants. |
| `NpcSkinTone` + `SkinToneMaterials` | NPC skin tone: in `Start` swaps every masked `SG_SkinLit` material under the model for a cached toned copy (`NPCEntity.SkinTone`); re-tones a rebuilt armor by polling `EquipmentVisuals.ArmorVisual` in `LateUpdate` (no cross-system event). A skin material without a mask (legacy / unbaked head) is kept, one warning; every shipped `M_Head_*` has its own mask. Cache: one copy per source × tone, plain materials (SRP Batcher-safe), cleared on `SubsystemRegistration`. Pipeline: `Art/Characters/Humanoids/CLAUDE.md` → NPC skin tones. |
| `SMB_EntityAttackState` | On attack states of `EntityBase.controller` and `Humanoid_Template`. Enter/exit → receiver → attacker counter. Receiver lookup cached; no-op on the Player. |
| `SMB_DeathState` | On the death animator state. |
| `EntityCombatTactics` + `CombatTacticsRules` + `AttackTokenRegistry` | Combat tactics (circling, attack tokens) — see **Combat tactics** below. Rules / tokens pure + tested (`CombatTacticsRulesTests`, `AttackTokenRegistryTests`). Profile: `CombatStyleSO` (`ScriptableObjects/Entities/`). |
| `EntityGuard` + `CombatBlockRules` / `GuardPlan` | NPC blocking — see **Blocking** below. Rules pure + tested (`CombatBlockRulesTests`). |
| `EntityArcher` | NPC bow shots (aim / nock / release, shooter-owned arrow hits) — see **Ranged** below. Decision in `CombatTacticsRules` (`CombatTacticsRangedTests`), lead math in `Game.Combat.RangedAimMath` (`RangedAimMathTests`). |
| `EntityPotionDrinker` | Tactical potion drinking — see **Potions** below. Decision in `CombatTacticsRules` (`CombatTacticsDrinkTests`), timing in `Game.Combat.DrinkTimeline` (`DrinkTimelineTests`). |
| `EntityStance` | Hidden stance meter (`IStanceTarget`) — stagger / knockdown, see **Stance** below. |
| `EntityPresence` (`Game.World`) | Base `IInteractable` for every entity — see below. |
| `NPC/NPCPresence` | `: EntityPresence` — adds dialogue. Alive **and hostile to the player** (`FactionSO.AreHostile(own, TargetRegistry.Player.Faction)`, read live; no player → not hostile) → no prompt, `CanInteract` false (bandits); corpse loot unchanged. |
| `NPC/NPCMemoryComponent`, `NPC/NPCDialogueGraphComponent` | An NPC's active memory set; resolve available dialogue start nodes / choices. |
| `NPC/TollGate`, `NPC/TollGateRules` | Exit toll (spec-racket-guard-exit-toll) — see **Toll gates** below. `TollGateRules` is pure (`TollGateRulesTests`). Racket data: `Game.NPC.TollRacketSO` (`ScriptableObjects/Entities/NPC/`). |
| `NPC/NpcRoutine`, `NPC/NpcRoutineBlock` | Daily routine (Stand / Patrol / Sleep blocks — owns all patrol routes) — see below. `NpcRoutineSchedule` (`FindBlock`, `FindAllDayBlock`, `NearestIndex`) is pure (`NpcRoutineScheduleTests`). |
| `NPC/NpcSleepRules` | Pure sleep rules (`ShouldLieDown`, `IsQuiet`) — see **NPC sleep** below. `NpcSleepRulesTests`. |

---

## NPC routines (NpcRoutine → EntityBrain)

- **Patrol routes live only in routines** — `EntityBrain` has no waypoint field. An entity without an `NpcRoutine`
  (monsters) idles / wanders around its spawn and returns there after a fight. A guard that patrols all day gets an
  `NpcRoutine` with one `0 → 0` Patrol block (WarehouseGuard, bandit).
- `NpcRoutine` (scene component on the NPC instance, `[DefaultExecutionOrder(100)]` → after `EntityBrain.Start`) holds
  `NpcRoutineBlock`s: `startHour` (incl.) → `endHour` (excl., wraps midnight, start == end = all day), `NpcActivity`
  **`Stand`** (`spot` = position + facing; empty = the spawn spot) or **`Patrol`** (`waypoints` list, looped,
  `PatrolWaitTime` at each; no non-null waypoint → stands instead, one warning) or **`Sleep`** (`bed`, a `Game.World.Bed` with
  NPC approach + sleep anchor; missing → stands at `spot`, one warning — see **NPC sleep**). First match wins; no match = stand at
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
- `CurrentBlock` / `CurrentActivity` are the hook for future activity animations and dialogue / trade gating. A block's optional
  `label` (no effect on the routine) is what gates match by name (`WorldFactTrigger` `InRoutineBlock`).
- **Story sequences own NPCs through `Suspend()` / `Resume(snap)`** (e.g. `Game.World.ArrivalSequence`): while
  `IsSuspended` no block is sent, so the sequence's own `SetRoutineTarget` calls stick; `Resume` re-sends the current
  block at once (snap = warp). Always resume what you suspend (also from `OnDisable`).
- StartingTown (spots under `NpcRoutineSpots`), 06–21 stand at spawn / 21–06 spot: Storekeeper on the commute north of the Company store and
  Cook by the canteen (both spots date from the removed houses; move them when those NPCs move indoors), Blacksmith in his room at the Timberworks forge, Warden outside the Company
  office door on the outcrop (NPCs can't open doors). Old Hobb (`TentMate`): 06–21 Patrol between his hut door (`worker-mate`) and the Player's Zone campfire, 21–06
  **Sleep** in `PlayersZone/HobbsBed` inside his hut (his restricted area: see `Scenes/CLAUDE.md`; `Spot_TentMate_Night` is unused since). `DockGuard`: 06–21 spot `DockGuard_GuardPost` (in front of the dock guard post), 21–06 Patrol on
  its deck (`NpcPatrolRoutes/DockGuard`). `WarehouseGuard` and `bandit`: all-day Patrol. `CompanyGuard_ArrivalLeft` / `Right`: all-day stand at `Spot_ArrivalGuard_Left` / `Right` (arrival gate). `RacketGuard` (Harl): 06–14
  `Spot_RacketGuard_QuarryGate`, 14–21 `Spot_RacketGuard_WildernessGate` (his toll posts), 21–06 `Spot_RacketGuard_Night` outside
  The Short Measure. `BeatenWorker` (Tam): all-day stand at `Spot_BeatenWorker_Campfire`. TitheBarnShortfall cast (scene root `TitheBarnShortfall`): `BarnGuard` (Venn) 06–21 `Spot_BarnGuard_Gate`, 00–03 `Spot_BarnGuard_BarnDoor` (block label `Barn`), otherwise Sleep in `BarnGuardBed` (barracks `Bunk_0`); `FieldHand_Pim` / `BarnWorker_Gerrit` 06–21 `Spot_FieldHand_Strips` / `Spot_BarnWorker_Threshing`, 21–06 Sleep in `FieldHandBed` / `BarnWorkerBed` (tent camp, west of Hobb's hut).

---

## NPC sleep (spec-npc-routine-sleep)

- A Sleep block sends `SetRoutineTarget(bed.NpcApproach, null, snap, bed)`. The NPC walks to the approach point like a
  stand spot; on arrival `HandleBedArrival` → `LieDown`: `Bed.TryOccupy` (taken → stands, one warning), agent
  `updatePosition` / `updateRotation` off, root set on `Bed.NpcSleepAnchor`, `SetSleeping(true)` (driver → `IsSleeping`),
  `EntityPerception.SetAsleep(true)`, state **`Sleeping`**. A load / time skip snaps to the approach and lies down at once.
  Nothing is saved.
- **Asleep perception:** no sight, no proximity circle, `CanSeeTheft` false (thefts ignored; `HandleTheftCommitted` doesn't
  list Sleeping either). Hearing only, in **every** mode incl. witness, with the normal radii / cap / occlusion — a sneaking
  or still player is Silent and never wakes it. `IsUnawareOf` includes Sleeping (sneak-attack bonus).
- **Waking** (`HandleSleeping`): full awareness → trespasser in its area → `RespondToTrespasser`; hostile → normal reaction;
  witnessed player, or one in its area during grace → `WakeGrumbling` (`WitnessProfileSO.WokenBarks` bubble, perception
  reset, routine). `WakeByPlayer` with the player inside an area it owns (Hobb's hut) → `RespondToTrespasser` too. `IsSleeping`
  is re-sent every Sleeping tick (an `NpcArmament` controller swap resets animator parameters). A
  radius hostile (monster), a hit (`HandleHealthChanged`), a stance break, a knockout, `StartBrawl` and `WakeByPlayer()`
  (`NPCPresence` "Wake" prompt, no dialogue) also wake it. Every exit goes through `ExitSleepPose(warp)`: animation /
  perception awake, bed released, agent updates back on, body warped to the approach. Death passes `warp: false` (the
  ragdoll takes the body where it lies). `OnDisable` exits too.
- **Back to sleep:** every wake (trespass and fights included) sets `_sleepWaitQuiet`; at the bed the NPC faces the player
  and lies down only after the player has stayed beyond its walk hearing radius (or is gone) for
  `StealthConfigSO.sleepResumeDelay` (10 s) (`NpcSleepRules`). A snap or a block change to another bed / no bed clears the
  wait. A death in bed leaves the agent updates off; `OnDisable` restores them (respawn).
- **Block change while asleep:** same bed → sleeps on; otherwise gets up and walks (`ApplyRoutineRequest`). No get-up clip:
  the animator blends `Sleeping → LockOn Locomotion`. `IsInNonCombatRoutine` excludes Sleeping (no toll intercepts).
- `ArrivalSequence`-style callers use the 3-arg `SetRoutineTarget` (no bed) — a sleeper gets up for them.

---

## EntityMeleeAttacker

- `_hitboxes` = `Id` + `WeaponHitbox`. Clip events open/close them by id (`""` = all). `RegisterHitbox` /
  `UnregisterHitbox` for runtime (equipped-weapon) hitboxes. Unknown id → one warning per id.
- `AggroTarget` (`SetAggroTarget`, set by the brain for brawls, incl. the trespass escalation fight): hits land on it even when its
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
- **Swing rules = the player's (spec-ai-attack-root-and-step), all entities:** while `EntityMeleeAttacker.IsInSwing`
  (attacking and in an attack state, or within 0.5 s of the trigger before the state enters; capped at 4 s since the last
  start / state enter so a leaked state count never freezes the entity), `EntityBrain.HandleAttack` keeps the agent stopped
  and returns before any range / sight check — the swing and its combo finish rooted even if the target backs off (a dead /
  invalid target still ends it). The brain turns toward the target only while `!IsAttackCommitted`: a hitbox actually
  opening (`OpenWindow`, known id) commits, every attack-state enter (`NotifyAttackStateEntered(index)`) re-opens it.
- **Forward step:** `EntityAttackStep` (optional, on `NPC_base Variant`; monsters have none) — `Begin(index)` from the
  state enter resolves `CombatConfigSO.ResolveAttackStep(WieldedWeapon, index)` (same rule as `PlayerCombat`) and moves
  through `NavMeshAgent.Move` (works while stopped, velocity stays 0; skips avoidance). `Stop()` on `BeginAttack` /
  `EndAttack`; stops off-mesh; never brings the bodies within `_contactMargin` of `AttackTarget` (agent radius + target
  NavMeshAgent / CharacterController / CapsuleCollider radius; target passed to `BeginAttack(damage, hits, target)`). The
  index comes from `SMB_EntityAttackState.attackIndex` (1/2/3 on `Humanoid_Template` Attack_1/2/3_State; 0 on `EntityBase` = no step).

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
- **Theft return (spec-theft-return-or-fight):** a pursuer keeps every theft id it adopts in one chase (`_pursuitTheftIds`,
  trespass ids excluded, cleared on a new chase / `EndPursuit`). At the catch, `TryOfferReturn` sums them from
  `Game.Core.StolenGoodsLedger` and asks the player (`OnStolenGoodsRequested`, query) what they still carry; something left →
  `NPCPresence.RequestForcedTopic(StealthConfigSO.theftReturnDialogue, scoldLine)` (scold bark first, then the shared
  give-back / refuse choices in `Data/NPCs/Shared/Dialogues/TheftReturn`), else the old lone scold line. `OnDialogueSignal`
  (own `entityName`, Confronting): `theftReturnSignal` → take request → items + gold (`CombatConfigSO.knockoutLootGold`
  coins) into its own inventory, incidents cleared. Dialogue closed without the return signal (refuse choice, Escape) →
  `RefuseReturn`: `StolenGoodsLedger.MarkRefused` + `StartBrawl(player)` (armed); can't fight (passive) → cleared, routine;
  thief knocked out mid-dialogue by someone else → marked refused, no brawl. A never-opened dialogue (timeout) is no
  refusal. The give-back signal is honoured even if a hit took the catcher out of Confronting (`_confrontThief`).
  Leaving combat (`SetCombatState(false)` → `EndRefusalBrawl`) or disabling clears its refused incidents unless the player
  lies unconscious — then **any** knockout winner recovers them at the grab (`RecoverRefusedGoods`, after the usual gold
  cut). Every looting exit and the no-loot branches (Friendly winner, missing setup) call `ClearRefused`. A catcher skips
  incidents already refused to another NPC (`IsRefused`). Gaps: equipped stolen items are never returned; Escape on the
  scold line already counts as a refusal.
  Setup (one warning, then scold only): config dialogue + return signal, `NPCPresence`, `InventorySystem`,
  `_onStolenGoodsRequested` + `_onDialogueSignal` (wired on `Entity_base`).
- **Trespass (restricted areas — `Game.World.RestrictedArea`):** perception has a third mode between hostile and witness
  (priority hostile > trespass > witness): a non-hostile stealth target standing inside an area **this entity owns**
  (`NPCEntity` listed, or its faction listed; area restricted now = hours window + not in grace) is perceived with the
  **hostile formula** — walking or sneaking, sneak-shortened — at `TrespassSightRange` (`DetectionRange`, or the witness
  profile's `TheftSightRange` for a passive owner). `IsTrespassing` / `TrespassArea`; F3 overlay + gizmo label
  "Trespass". The meter → Suspicious → full awareness → `RespondToTrespasser(area)`: `TrespassersSpotted++`, then
  - not warned enough yet (`!area.ShouldFightTrespasser`, or the owner can't brawl): `TrespassAlertBarks` bubble + **Pursuing** with `TheftKind.Trespass` on the area's open incident
    (`RestrictedArea.OpenIncidentId`, negative ids, reused until claimed → several owners share one chase, first catcher
    scolds). Confronting uses `TrespassScoldBarks`; `area.StartGrace(trespassGraceDuration)` at the catch (before the
    dialogue-channel check), **`area.RecordWarning` once the scold dialogue really opens** (one per catch; skipped when
    the area already warned enough), `HoldGrace` every frame while the scold is open, `StartGrace` again when it closes. An escape
    gives no grace. A theft while chasing a trespasser keeps `_pursuitArea` (the scold still starts its grace).
    **Leaving the area ends the chase:** every Pursuing tick, `TheftDetection.ShouldStopTrespassChase` (trespass kind +
    area, and the player outside every box or the area no longer `IsRestrictedNow`) → one `TrespassFledBarks` bubble
    (`Barks_TrespassFled`, own no-repeat index; null = silent) + `EndPursuit` → patrol / routine. Crossing into another
    area the owner also owns (`TryFindTrespassedArea`) hands the chase over to it instead. No LOS needed, no grace — walking back in
    refills the meter from 0. A theft seen mid-chase switches `_pursuitKind`, so that chase follows the theft rules.
    `IsTrespassTarget` re-checks `TrespassArea.IsRestrictedNow` (grace / hours can change between perception scans).
  - **Escalation (spec-trespass-repeat-offence-fight):** `area.ShouldFightTrespasser(trespassWarningExpiryHours)` (the
    area's active warnings ≥ its `_warningsBeforeFight`; 0 = attack on sight, the old instant aggression) →
    `StartBrawl(player)` (**armed**; passive / no attacker → false → the chase above) + `TrespassPunishBarks` bubble
    (`Barks_TrespassPunish`; `TrespassAlertBarks` when no warning was given), `_trespassFightArea = area`. The fight
    follows normal combat rules (leaving the area doesn't stop it). `SetCombatState(false)` → `EndTrespassFight`: the
    player lying unconscious (knocked out by anyone) → `area.ClearWarnings()`; won / fled → warnings kept. Knockout
    looting robs the gold as for any brawl. A theft scold that kept `_pursuitArea` also counts as a warning. StartBrawl's
    aggro target makes the faction filter accept the non-hostile player; `SetCombatState(false)` clears that aggro target
    (disengage, search end, death, routine return), and so does a damage retarget onto another attacker. Searching
    re-engages a re-sighted trespasser only if it is the current aggro target; otherwise it leaves combat and applies the
    area's rules (`RespondToTrespasser`); a witnessed target is ignored.
  - An owner already **Watching** (perception engaged, no rescans) checks `TryFindTrespassedArea` itself and responds.
  - Unknown-source hits never pick a trespassing (non-hostile) perception target. Lock-on accepts an aggro owner
    through `EntityBrain.IsFightingOrEngaging(player)` (`Scripts/Player/CLAUDE.md` → LockOnSystem).
- **Hearing:** hostile and trespass perception also hear a walking / running player (Entity walk / run hearing radii,
  360°, halved without LOS) — see `Scripts/Stealth/CLAUDE.md` → Detection formula. It fills the same meter, so the
  brain needs no change: Suspicious faces `LastSeenPosition` (= last *sensed*), alerted states keep tracking a heard
  target. Checks that mean "sees" (unknown-source hit retarget, Watching trespass check) read `CanSeeTarget`, which
  stays vision-only. Hearing radii are capped at the sight range, so Warning's `dist > DetectionRange` cancel can't loop.
- **Detection while patrolling:** perception ticks every frame in every state and `HandlePatrol` checks it before path
  handling; the cone follows `transform.forward`, which the NavMeshAgent turns along the path (`updateRotation` is only
  disabled by `EntityCombatTactics` while circling / backing off / closing in, and restored when tactics end) — a walking guard sees ahead, a waiting one keeps its arrival facing. Verified in play mode on the WarehouseGuard.
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
  `NPC_base Variant` and any non-prefab scene NPC. `CanInteract => IsLootable`,
  `InteractPrompt` = `"Loot"` when lootable, else `""`. `Interact()` → `OpenLoot()` raises
  `GameEventSO_ContainerOpenRequest _onLootRequested` with the entity's `InventorySystem` and
  `takeOnly = true` — reusing the container pipeline (`ContainerSystem` / `ContainerUI`). An emptied corpse
  is inert (no prompt, `Interact()` no-op).
- **`NPCPresence._availableAfter`** (optional `Fact`; an `ElapsedFact` delays it): no "Talk" prompt / dialogue until the fact is true (Tam before
  event-001). Loot and NPC-forced topics (`RequestForcedTopic`) ignore it. No `WorldStateManager` → available.
- **`NPCPresence.RequestForcedTopic(node, thenTopics = false)`**: `thenTopics` → the chain ends on the NPC's normal topic
  list (Farewell included) instead of closing (Hobb's arrival briefing). Toll and Warden topics keep the default.
- **`NPCPresence._talkOpeners`** (`NpcTalkOpener`: `condition` (any `Fact` — an `ElapsedFact` delays it, e.g. Hobb 2 h) / `topic` / `played` WorldFact + `cancelWhenWitnessed`): on the
  player's Talk, the first opener whose condition is true and played false opens its topic with
  `RaiseDialogueRequest(topic, thenTopics: true)`; `played` is set only if the dialogue really opened (cursor unlocked at once —
  `DialogueSystem` ignores a request while another dialogue is open); otherwise the normal topic list is requested. Skipped while the NPC's brain `IsChasingThief`
  (Pursuing / Confronting: its scold comes first). Incomplete opener → warning, skipped; no `WorldStateManager` → none.
  `MarkWitnessedOpenersPlayed()` spends every `cancelWhenWitnessed` opener **whose condition is already true** (an `ElapsedFact` condition is read without its delay — `ElapsedFact.WithoutDelay`): `EntityBrain`
  calls it (`SpendWitnessedOpeners`) when a chase over an item / container theft of its **own** goods ends by any exit:
  `EndPursuit` (scold, give-up, claimed by another catcher), the refused-return brawl, and a check at the top of `Update`
  (`_pursuitOwnGoods` while no longer `IsChasingThief`: a hit or knockout mid-chase, a hostile taking over).
  `_pursuitOwnGoods` is set at adoption from `TheftCommittedData.owner`; not saved, so a reload mid-chase keeps the opener. So an NPC never later says "someone went through my things" about a theft it saw, while a
  witnessed opening with nothing taken leaves the line for a later unseen theft. Openers aren't tied to a specific incident:
  any own-goods chase spends every flagged opener whose condition is true. Pure `TalkOpenerRules` (`TalkOpenerRulesTests`).
  Hobb's suspicion line (`Dialogues/Suspicion`) is the first user.
- **`NPCPresence.RequestDialogueClose()`** (`_onDialogueCloseRequested` → `OnDialogueCloseRequested`, wired on `NPC_base Variant`
  and any non-prefab scene NPC): closes the dialogue this NPC opened (forced topic or the player's Talk); ignored for another NPC's.
  `TollGate` calls it when Harl is disturbed mid-talk, `ArrivalSequence` on region unload mid speech / briefing.
- **`EntityBrain.IsAnyFighting(member)`** (static, over `_active`): some brain `IsFighting` that member — backs the player's `PlayerStateManager.IsAggroed` (aggro run / jump stamina costs, `Scripts/Player/CLAUDE.md`).
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
- **Humanoid NPCs without a weapon fight unarmed** (random 1–3 hits of lead jab → hook → uppercut; hitboxes `Unarmed` =
  right fist, `UnarmedLeft` = left fist, opened by the id on each clip's hit events — left, right, left).
  Armed NPCs: `NpcArmament` (see **NPC equipment**).
- One damage value per attack (`Entity.AttackDamage`, + the wielded weapon's `damageBonus` for an armed NPC). Two open hitboxes can each hit the same target once
  (dedupe is per `WeaponHitbox`).
- **Known gap:** spider `AttackRange` 0.8 is root-to-root, but agent avoidance holds it ≈ 1.0 m from an NPC →
  spiders engage NPCs but never bite them (they do bite the player). Retune the spider SO if needed.

---

## Combat tactics (EntityCombatTactics — spec-ai-combat-tactics-foundation)

- **Opt-in by data:** `Entity._combatStyle` (`CombatStyleSO`, `Data/Entities/CombatStyles/`) + `EntityCombatTactics` on the
  root (`NPC_base Variant`). No style / no component → the plain chase-and-swing (monsters). Styles: `CombatStyle_CompanyGuard`
  (the six guards — patient, "competent but beatable") and `CombatStyle_Bandit` (pushier).
- **Brain integration:** `HandleEngage` → `_tactics.Tick(target, distToTarget, IsAttackReady())` (the brain's **3D**
  distance — a flat one never matches the brain's range check on stairs) instead of the straight chase; enters Attacking
  only with a token on the **current** target (`HoldsAttackToken` → `WantsAttackOn(target)`) within `AttackRange`.
  `HandleAttack` swings once (`_tacticsSwingStarted`), then — after `IsInSwing` ends — `NotifySwingEnded()` (token released,
  fresh circle time) and back to Engaging (`ResumeEngageAgent`: unstopped, EngageSpeed / EngageStoppingDistance), instead of
  standing out the cooldown. Attacking **without** a token (stance reaction ended tactics, retarget) → straight back to
  Engaging — never a plain swing that would break the cap. Out of sight a tactical fighter only chases the last seen
  position (no swing); tactics handing back to the plain chase restore its agent settings. Skipped (plain chase) while chasing a last-seen position and against a
  **passive target** (`DetectionRange <= 0`, e.g. Tam in the arrival beating — circling a victim that never fights back
  reads wrong). **Single release point:** `Update` calls `EndTactics()` whenever the state is not Engaging / Attacking, and
  on death, unconscious and stance reactions — never add per-exit release calls.
- **Decision** every `decisionInterval` (`CombatTacticsRules.Decide`): held token → Attack; beyond ring + `approachMargin`
  → Approach (`updateRotation` on); circle time elapsed + attack ready + free token + `roll < aggression` → take token,
  Attack; closer than `AttackRange × backOffFraction` → BackOff; else Circle (flip on `directionChangeChance`). Ring =
  `AttackRange + ringMargin`. Circle point = rotate (self − target) around the target, leading 2 intervals, NavMesh-sampled;
  both directions blocked → Approach. While not Approaching, `updateRotation` is off and the component faces the target at
  `WarningTurnSpeed` — the strafe blend comes from `DriveLocomotion`'s local velocity (`LockOn Locomotion`).
- **Commit timeout:** a token held `attackCommitTimeout` (2.5 s) without a swing (unreachable / kiting target, avoidance)
  is given back and the entity circles again — otherwise two stuck holders starve every other attacker. Circle time only
  counts while circling / backing off. BackOff with no NavMesh point behind → `ResetPath` (holds), never a stale chase path.
- **Tokens:** `AttackTokenRegistry` (static, keyed by the target `FactionMember`, reset on `SubsystemRegistration`), capacity
  `CombatConfigSO.maxAttackersPerTarget` (2; no config → uncapped, one warning). One token per holder.
- `DebugStateName` shows `Engaging/Circle`, `Attacking/Attack`… (cached strings) while tactics drive.
- Roadmap done (blocking, potions, ranged — see the sections below); open ideas in `deferred-work.md`.

---

## Blocking (EntityGuard — spec-ai-npc-blocking)

- **Opt-in by data:** `CombatStyleSO` "Blocking" (`blockChance` 0 = never; guard 0.4, bandit 0.25) + `EntityGuard` on the root
  (`NPC_base Variant`). A parallel check, not a `CombatAction`: `Block_State` is UpperBody-only, so the NPC keeps circling /
  backing off with its guard up.
- **Brain:** `Update` ticks `_guard.Tick(_currentTarget)` after the state switch while Engaging / Attacking **and tactics are
  active** (else `End()`); `EndTactics()` also ends it (single release point). `IsAttackReady()` is false and `HandleAttack`
  won't `ExecuteAttack` while `IsRaised` — `IsBlocking` cancels attack states (block-cancel transitions). `DebugStateName`
  shows `Engaging/Guard` / `Attacking/Guard`.
- **Reading the swing:** the target's `Game.Combat.IMeleeThreat` (`PlayerCombat`: `IsAttacking` without a bow;
  `EntityMeleeAttacker`: `IsInSwing` / `IsAttackCommitted`). Wind-up = attacking and not committed; both sides re-open
  "committed" per combo step, so `GuardPlan` rolls **once per step** on the wind-up edge: pass while down → raise after
  `reactionDelayMin..Max` (too slow = that hit lands), pass while up → stays up, fail → guard lowered (opening). Only a target
  within `guardThreatRange` and the front arc (`guardArcAngle` half-angle) is read; the guard only rises / stays up while it
  remains there. Threat over (or out of arc / range) → `guardHoldTime`, then down. A late reaction still raises the guard for
  the rest of the swing (the hit already landed). Retarget → the plan resets, so a swing the new target is already winding up
  is read. The threat must be a live, enabled component (a disabled `PlayerCombat` keeps stale flags). A raised guard is
  re-sent to the animator every tick — an `NpcArmament` controller swap resets every parameter.
- **Can't guard** (drops at once; a step that started meanwhile is never read): own `IsInSwing`, staggered / knocked down,
  dead / unconscious, `IsSwitchingWeapon`, drinking a potion, **guard broken** (`EntityStance.IsGuardBroken`: stance ≤ threshold),
  or **no shield in hand** (spec-shield-offhand-blocking): `NpcArmament.ShieldInHand` = a `ShieldSO` in the Offhand slot and the
  weapon drawn (`EquipmentVisuals.ShieldInHand`) — so never with fists, a bow, or without `NpcArmament`. Guards (`CompanyGuard_*`,
  Dock / Racket / Warehouse guards, Warden) carry `Shield_KiteWoodShield`, the bandit `Shield_RoundWoodShield`
  (`NPCEntity._startingEquipment`). The shield's `defenseBonus` adds to `Entity.Armor` in `EntityHealth.TakeDamage`
  (`NpcArmament.ShieldArmorBonus`; body armor still doesn't count). Shield stamina / window are player-only — NPCs keep the
  style's `perfectBlockChance`. A bow swap bumps the shield into the inventory; `NpcArmament.EquipCarriedShield` re-equips it
  on the way back to melee and on wake-up — only the shield it stowed (`_stowedShield`), never trade stock. `GrantLoadout`
  equips shields after weapons (a loadout shield next to a two-hander is carried, warned). A restored living NPC with no
  offhand and no shield in its inventory gets its starting shield back (pre-shield saves). The shield stays equipped (not
  lootable) when the NPC goes down, like armor — unless a bow swap had stowed it in the (lootable) inventory.
- **Hit:** `EntityHealth.TryReceiveHit` → `EntityGuard.ResolveHit(attacker)`: raised + attacker in the front arc **and within
  `guardThreatRange`** → `Blocked`, or `PerfectBlock` on `perfectBlockChance`; else `NotBlocked`. Any attacker in reach is
  blocked, not only the read target. Arrows share the check: a point-blank shot into a raised guard is blocked, a distant
  shooter is never blocked or parried (the parry would stagger the player at range).
- A blocked hit raises no damage reaction (no `HealthChanged`), like the player's own block — a guarding NPC doesn't retarget
  to a third party whose hits it blocks.
- **Guard budget = stance:** attackers apply `ApplyBlockedStanceDamage` on `Blocked` (`EntityMeleeAttacker`, and now
  `PlayerCombat.ResolveHit`); `EntityStance` scales it by the style's `guardStanceMultiplier` (never knockdown) — crossing the
  threshold staggers (guard break), and the guard stays down until regen lifts stance above it. Without that gate the
  floored-hit rule would let a broken guard absorb blocked hits for free.
- **Parry:** NPC attacker → its own `Entity.PerfectBlockStanceDamage` (existing); player attacker →
  `PlayerStance.ApplyParriedStanceDamage(CombatConfigSO.parriedStanceDamage)` (60, no knockdown, skips attack resistance).

---

## Potions (EntityPotionDrinker — spec-potion-drink-animation)

- **Opt-in by data:** `CombatStyleSO` "Potions" (`drinkHealthThreshold` 0 = never; guard 0.35, bandit 0.25 but no potion) +
  `EntityPotionDrinker` on the root (`NPC_base Variant`) + a drinkable `PotionItemSO` (`IsDrunk`: kind `Potion`, not an ingredient) in the NPC `InventorySystem`
  (the six Company guards: one `Item_Health_Potion` each, scene `_startingItems` overrides in StartingTown). Meals / raw
  food are never drunk.
- **Decision** (`CombatTacticsRules.WantsDrink`, checked **first**, before the held-token rule — the token is given back):
  health ≤ threshold, has a potion, `DrinksUsed < maxDrinksPerFight`, off `drinkCooldown` → `BackOff` (destination past
  `drinkSafeDistance` + 0.5 m, not the ring) until distance ≥ `drinkSafeDistance` **or** `drinkBackOffMaxTime` backing off
  (cornered) → `CombatAction.Drink` (appended to the enum — `DebugStateName` arrays are built from it: `Engaging/Drink`).
- **Drink:** `TryBegin` takes the potion out of the inventory (**consumed at start**), `AIAnimationDriver.SetDrinking`
  (`IsDrinking`, re-sent every frame — controller swaps), heal at `CombatConfigSO.drinkHealTime` via
  `PotionItemSO.ApplyHeal(EntityHealth)` (instant, HoT collapsed, no meal bonus), done at `drinkDuration`. Tactics hold the
  agent still and face the target until it ends, then circle. No swing (`IsAttackReady`), no guard (`EntityGuard.CanGuard`).
- **Interrupt** (potion lost before the sip): `HealthChanged` decrease (a landed hit), `EndTactics` (the single release
  point — stagger, knockout, death, disengage, Searching), tactics retarget, disable. A drink in progress keeps tactics
  ticking through a brief loss of sight (`HandleEngage` skips the last-seen chase while `IsDrinking`).
- `DrinksUsed` / cooldown reset in `SetCombatState(false)` (`ResetFight`) and `OnEnable`. An undrunk potion stays loot.
- **Outside combat the brain ends it at once** (`EndTactics` every frame outside Engaging / Attacking) — a direct
  `TryBegin` test needs a running fight (`StartBrawl`).

---

## Ranged (EntityArcher — spec-ai-ranged-combat-style)

- **Opt-in by data:** a `BowSO` in `NPCEntity._startingEquipment` + a `CombatStyleSO` (its "Ranged" values) + ammo (the bow's
  `ammoItem`) in the NPC `InventorySystem` + `EntityArcher` and `BowAimCorrection` on the root (`NPC_base Variant`). Only the
  bandit today: Hunting Bow + Axe, 12 `Item_Arrow` (scene `_startingItems`), `DisengageRange` 18.
- **Weapon choice** (`CombatTacticsRules.WantsRanged`, at each decision; `ShouldDecideWeaponMode`: never while holding a
  token or while a switch plays out): `CanUseRanged` = a wieldable bow, ammo left, **faction-hostile** target (brawls /
  aggro-only fights stay melee), no fists request → bow from `rangedSwitchDistance`,
  melee below `meleeSwitchDistance`, kept inside the band and during `weaponSwitchCooldown`; losing the bow / ammo → melee at
  once. Requests `NpcArmament.SetRanged` (see **NPC equipment**). Spacing follows the **requested** mode during the switch clips.
- **Ranged branch of `Decide`** (after Drink — Drink outranks Shoot): held token → Shoot; beyond `preferredRange` +
  `approachMargin` → Approach; inside `rangedBackOffDistance` → BackOff (kite); `ShotReady` (bow in hand, off `shotCooldown`,
  ammo, clear line of fire) + free token → **Shoot**; else Circle on the `preferredRange` ring. Shots take a token from the
  **same pool** as melee swings (held only for the ~1.5 s shot).
- **Shoot:** tactics hold the agent still and face the target + `CombatConfigSO.npcAimStanceYaw` (side-on); `EntityArcher`
  sets `IsAiming` (re-pushed every frame), the nock comes from `SMB_BowAimState` → `EntityAnimationEventReceiver`
  (`IBowNockListener`) or the `bowNockTimeout` watchdog (a forced nock skips `BowFire`), the arrow leaves `aimTime` after the
  nock (the tell), the bow stays up `npcBowReleaseHold` for the Fire clip, then lowers; token released, circle. Watchdog =
  `npcBowNockTimeout`. Dropped with no arrow (ammo is spent at the release) by: a drink wish (checked at the decision interval
  — Drink outranks Shoot), `EndTactics` / retarget, the target dying / going down, a line of fire closed at the release (the
  clear-shot sweep runs again there).
- **Arrow:** `ArrowProjectile` (owner = the NPC) aimed at the target's body height × `npcAimHeightFraction` (CharacterController
  / NavMeshAgent), led by the smoothed target velocity × flight time × `leadFactor`, spread `shotSpreadAngle`
  (`BowSpreadMath.SampleCone`). Clear-shot sweep (`npcClearShotRadius`) before a shot: the first collider must be the target, a
  hostile body or nothing — a wall or a non-hostile body → circle. Hit = `EntityMeleeAttacker.IsHostileTarget` filter (a
  non-hostile absorbs it), then `TryReceiveHit(shooter)`: `NotBlocked` → `NotifyHitBy` + `TakeDamage(AttackDamage +
  bow.damageBonus, shooter)` + stance (`bow.ResolvedStanceDamage`); `Blocked` → blocker `ApplyBlockedStanceDamage`;
  `PerfectBlock` / `Dodged` → nothing for the shooter. Sound via `EntityMeleeAttacker.PlayHitSound`.
- **No guard while a bow is wielded** — the bow bumps the shield into the inventory, so `ShieldInHand` is false (`EntityGuard`); no melee swing (`IsAttackReady`; `HandleAttack` — a fight the tactics
  don't drive asks `SetRanged(false)` instead). Target lead velocity is smoothed with `npcTargetVelocitySmoothTime`
  (frame-rate independent). The clear-shot sweep skips corpses (the arrow does too) and holds the shot if its buffer fills.
- **Spine aim:** `BowAimCorrection` reads `EntityArcher` as its `IBowAimSource`; the player's calibration (`_arrowLineLocal` /
  `_arrowOriginLocal`) fits the `base_hero` NPC rig — measured ≈ 2° average error in play. Re-calibrate both if the rig changes.
- **Perception LOS ignores the entity's own colliders** (all entities) — the side-on aim swings the archer's own head (ragdoll
  collider) into the eye ray, which used to drop it into the last-seen chase mid-shot. A full hit buffer counts as blocked.
- `BowAimCorrection` sits on every `NPC_base Variant`; it returns before capturing the pose while idle and resets (no writes)
  once the animator is off (ragdoll at death mid-aim).

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
  `Blocked` (player: × `blockStanceMultiplier`; NPC: × its style's `guardStanceMultiplier`; never knockdown). Amount = `Entity.ResolveAttackStance(step, hits, wielded)`:
  monsters → fixed `_attackStanceDamage` / `_attackCanKnockDown` (rat 10, viper 15, spider 20, wolf 25 KD, grunt 30 KD);
  `NPCEntity` → `_attackStanceDamage` (15) + the **wielded** weapon (`EntityMeleeAttacker.WieldedWeapon`, set by
  `NpcArmament`; `WeaponSO.ResolvedStanceDamage`), × `_finisherStanceMultiplier` on the last hit of a multi-hit combo;
  knockdown = the wielded weapon's `ResolvedCanKnockDown` (fists = never). `ResolveAttackStance(step, hits, wielded, out kd)`. Step / hits come from `AttackComboPlan` (`CurrentStep` advances at `ComboWindowOpen`).
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
  always kill, even a downed target. No `RegisterDeath`, no kill, no kill XP. A knockout **credited to the player** calls
  `PersistentID.RegisterKnockout()` → one-time knockout XP per NPC (spec-knockout-xp-reward, `Player/Progression/CLAUDE.md`);
  an uncredited one leaves the claim free.
- **Player credit (`KillCredit`, spec-kill-xp-credit-assist):** resolved by `EntityHealth` at death / knockout from the
  killing blow's `GameObject` (execute = the executor): player → Friendly NPC (`PlayerRelationship.Of`) → assist
  (`EntityHealth.PlayerDamaged`: the player landed damage since `OnEnable`) → none. Unknown source → assist only. The kill
  fact is always recorded (`RegisterDeath(credited)`); only kill / knockout XP depends on it. Pure `Evaluate` (`KillCreditTests`).
- `EntityHealth` (needs `_combatConfig`, wired on `Entity_base` + any non-prefab scene NPC — null = always lethal):
  `IsUnconscious` from the knockout until the get-up clip ends. HP regains linearly to
  `CombatConfigSO.unconsciousRecoverHealthFraction` over `unconsciousDuration`, then `AIAnimationDriver.SetUnconscious(false)`
  (get-up), then `getUpDuration` later `RegainedConsciousness`. A knockout-pair hit while down is ignored — the only
  finishing move is the player's execute stomp (below). Same-GO events `KnockedOut` / `RegainedConsciousness`. `OnEnable` resets it (not saved).
- `EntityBrain`: `KnockedOut` → target dropped, combat off, attack ended, perception reset, agent held, state Idle;
  `Update` returns early while unconscious (no perception); `RegainedConsciousness` → `ResumeNonCombat()` (routine, never
  a re-attack). Routine requests / thefts / damage reactions are stored or ignored while down. `HasValidTarget` is false for
  an unconscious target this brain could only knock out → the winner disengages to its routine.
- **Brawl retaliation:** `HandleHealthChanged` with a known, live, conscious, **non-hostile** attacker forming a knockout
  pair with this entity (and `DetectionRange > 0`) → `EntityMeleeAttacker.SetAggroTarget(attacker)` + engage (same aggro
  mechanism as the trespass escalation fight). Passive NPCs still never fight back.
- `EntityStance` / `PlayerStance` treat unconscious like dead (meter refilled, `Apply` ignored); hit resolvers skip stance
  on an unconscious target. `EntityPresence.IsAliveAndOutOfCombat` is false while down.
- A loot screen open on an NPC that gets up closes (`OnLootSourceLost`, see EntityPresence below).
- **Execute (spec-execute-unconscious-npc):** `EntityHealth` implements `Game.Combat.IExecutable`. `CanBeExecuted` =
  unconscious, not getting up, not dead, `NPCEntity.CanBeExecuted` (per-NPC opt-out, default true). `ExecutePoint` = the
  body Animator's Chest bone (fallback Hips, then root). `Execute(executor)` → `Die(ragdollNow: true)`: a normal death
  (`RegisterDeath`, kill XP, lootable corpse) but straight to ragdoll — the standing death clip would lift the lying body.
  Player side: `Scripts/Combat/CLAUDE.md` → Execute.

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
  `EntityBrain._onGoldTheftRequested` (wired on `Entity_base` and any non-prefab scene NPC). Looting is reactive to hits (in
  `HandleHealthChanged`'s list), ignores thefts, stores routine requests; not unaware (no sneak bonus). Bubbles fall back
  to the default duration / priority without a stealth config.

---

## Player relationship (spec-player-relationship-tiers)

- **Tier** = `PlayerRelationship.Of(FactionMember)` — the one query other systems (dialogue, …) read; live, never cache it.
  Hostile = `FactionSO.AreHostile` with the player's faction (**faction-only**). Otherwise an active `NPCEntity` override
  (`_playerRelationship` Neutral / Friendly, active while **any** `_relationshipConditions` fact is true; empty = always; no
  `WorldStateManager` = inactive) wins; otherwise Friendly when either faction lists the other in `_alliedFactions`, else
  Neutral. No player → Neutral. Only Tam has an override today (Friendly once fed — `Data/NPCs/BeatenWorker/CLAUDE.md`).
- Tunables + barks: `CombatConfigSO` "Player relationship" (`friendlyAssistRange` 12, `friendlyGraceDuration` 8,
  `friendlyWarnLockout` 2, `relationshipBarkCooldown` 30 (was `hostileSightBarkCooldown`), `Barks_HostileSight` /
  `_FriendlyAssist` / `_FriendlyWarn` / `_FriendlyDisappointed`), read through `EntityHealth.CombatConfig`. No config →
  none of the reactions below. Bubbles use the theft bubble duration / priority; each set keeps its own no-repeat index;
  missing line / channel → silent, one warning (`RaiseRelationshipBark`).
- **Hostile:** `RespondToDetectedTarget` on the player → one `hostileSightBarks` bubble per cooldown (NPCs only, never
  monsters). Hit-triggered engages don't bark.
- **Friendly assist:** `TryAssistPlayer` (throttled by `_targetScanInterval`) runs from Idle / Patrolling (end of
  `TryDetectFromNonCombat`), Suspicious and Watching. Non-passive, has an attacker, player live and conscious → closest
  brain in the static `EntityBrain._active` list (OnEnable / OnDisable, reset on `SubsystemRegistration`) with
  `IsFighting(player)` that it **can fight**: live, conscious, not its own / an allied faction, faction-hostile (one-way
  `IsHostileTo`, same rule as the melee filter) or a knockout pair, and **not Friendly** to the player (friendlies never
  join against a friendly). Range = `min(friendlyAssistRange, 0.9 × DisengageRange)` — at 12 / 12 it would engage and
  drop the fight at once, then re-assist every scan. Faction-hostile → plain engage; knockout pair → aggro target
  (brawl). `friendlyAssistBarks` once per cooldown, perception reset.
- **Friendly grace:** `HandleHealthChanged`, before the passive early return: the player hits a Friendly NPC **not in
  combat** → `TryFriendlyWarn` — outside the window: `friendlyWarnBarks` bubble + window opens, no reaction; within
  `friendlyWarnLockout` of the warning: absorbed (the rest of the same combo must not start the brawl); later inside the
  window: window closes, the normal damage reaction runs (brawl). A passive friendly (Tam) keeps the window open — one
  bubble per window, never fights back. Grace resets in `OnEnable`; runtime only.
- **Friendly knockout winner:** `TryStartLooting` → Friendly → `friendlyDisappointedBarks` bubble, no loot,
  `DisengageFromCombat` there (returns true, so the caller doesn't log "target lost").
- Out of scope / later: bystanders watching / commenting a fight, hostile execute (hostile blows already kill), individual
  Hostile overrides, a tier dialogue condition.

## Mutual defense (spec-faction-mutual-defense)

- **Rule** = pure `FactionSO.Defends(self, selfGroup, other, otherGroup)` (`FactionTests`): a shared non-null
  `AidGroupSO` (`Entity._aidGroup`, faction-independent — e.g. a hidden crew who are workers on paper), **or** the
  defender's faction has `_membersDefendEachOther` and the other is the same faction or allied (either direction). The
  **defender's** flag decides. Flags: Company / Bandits / Monsters on, Neutral (workers) / Player off.
- **One faction per entity** stays the rule — sub-groups go through `AidGroupSO`, never multi-faction membership.
- **Scan:** `TryJoinNearbyFight` = `TryAssistPlayer() || TryDefendMember()` at the same three call sites (Idle /
  Patrolling, Suspicious, Watching); each has its own throttle timer. `TryDefendMember` → non-passive → closest
  opponent of an `_active` brain in **Engaging / Attacking** (a Warning also sets `IsInCombat` — the warned target keeps
  its grace) with a live, conscious `_currentTarget` (**whoever started the fight** — a trespass brawl counts) and is
  defended. A defender without a faction still defends its aid group (brawl only). First scan staggered in `OnEnable`. Range measured to the **opponent**:
  `min(memberDefendRange, 0.9 × DisengageRange)`.
- **Skips:** an opponent the defender also defends; the player when the defender is Friendly to the player (it stays out
  of a fight between its colleague and the player — the player is never on the defended side, that's the friendly
  assist's job); opponents neither faction-hostile nor a knockout pair.
- **Join:** perception reset (+ `ForceAware` for a stealth opponent), hostile → plain engage, else aggro target (brawl);
  fists when the defended colleague's brawl is a fistfight (`NpcArmament.FistsRequested`).
  `memberDefendBarks` (`Barks_MemberDefend`) once per `relationshipBarkCooldown`, `NPCEntity` only — monsters stay silent.
- Runtime only; no event-based call for help.

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
  trespass escalation fight). Brawl: player unconscious → this gate paid (the normal knockout **Looting** robs them); collector
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

## NPC equipment (NpcArmament — spec-npc-equipment)

- `NPC_base Variant` root: `EquipmentSystem` (`_npcOwned` = no warnings for the unwired player channels / `PlayerStats`),
  `EquipmentVisuals` (the base hero's 5 sockets, `base_hero` body, `mixamorig:Hips`, default controller `Humanoid_Base`,
  **`_onEquipmentChanged` / `_onVisualsRefreshed` unwired**) and `NpcArmament`, which refreshes the visuals directly.
- **Loadout:** `NPCEntity._startingEquipment` is added to the NPC inventory and equipped in `NpcArmament.Start` (skipped on a
  corpse). Equipped items leave the inventory → not lootable. Guards (all `Faction_Company`: CompanyGuard_ArrivalLeft /
  Right, DockGuard, WarehouseGuard, Harl, Warden) wear `Armor_CompanyGrunt` + `Weapon_ShortIronSword` + `Shield_KiteWoodShield`;
  the bandit carries `Weapon_HuntingBow` + `Weapon_Axe` + `Shield_RoundWoodShield` (shields equipped after weapons — see Blocking). A `BowSO` next to a melee weapon is **added to the inventory but not equipped** (fights
  start with the melee weapon).
- **Ranged switch** (spec-ai-ranged-combat-style): `SetRanged(bool)` → `AdvanceWeaponChange` in `Update`, one step at a time,
  while `IsSwitchingWeapon` covers the clips: waits (switching, attacking, staggered, aiming, down) → sheathes a drawn weapon →
  swaps the equipped weapon while sheathed (`Unequip` + `Equip`) → `Rebuild` (bow override controller, draw in combat). Bow
  wielded = **no hitbox registered** (`RemoveAllHitboxes`). `CarriedBow` / `AmmoCount` / `IsRanged` / `RangedRequested` /
  `IsChangingWeapon`. **`RangedRequested` re-syncs to the wielded weapon** in `ApplyMode` whenever no change is pending and no
  fists are requested (wake-up, save restore, end of a fists fight, a bow without a visual) — a stale request once left an
  NPC circling with a bow it couldn't swing. Waking with the dropped weapon looted → another carried weapon (melee first, else
  the bow), else fists. Leaving combat keeps the current weapon.
- **Mode:** armed (weapon equipped with a `WeaponHitbox` under its visual → registered as `Weapon`, weapon animator override)
  unless the current fight asked for fists — `EntityBrain.StartBrawl(target, useFists: true)` (only the arrival sequence's
  Warden → Tam beating today) — or there is no weapon: `Unarmed` + `UnarmedLeft` + the default controller. **Weapon and
  fists are never registered together** (sword clips fire `HitboxEnable` with an empty id = every hitbox). The fists request ends with the fight
  (`SetCombatState(false)`). Every fight is armed by default, brawls included.
- **Controller swaps** (`EquipmentVisuals.ForceDefaultController` / `Refresh`) reset every animator parameter and state:
  `NpcArmament` re-pushes `IsInCombat` and calls `EntityMeleeAttacker.ResetAttackStates()` (attack states restart without
  SMB exits — a stuck counter blocks every attack). Never swapped while unconscious / dead (it would stand the body up),
  attacking or in a stagger / knockdown — a mode change then waits (`_modePending`, applied in `Update`). A re-enabled
  NPC rebuilds its mode in `OnEnable` (the visual and its hitbox were recreated).
- **Draw / sheathe:** armed NPCs play the clips on combat entry / exit (`AIAnimationDriver.TriggerWeaponSwitch`); the visual
  moves on `WeaponSwap`, or after `CombatConfigSO.weaponSwitchTimeout / weaponSwitchAnimSpeed`. `HandleAttack` waits while
  `IsSwitchingWeapon`.
- **Down:** knockout / death → `OnDowned`: weapon unequipped into the inventory (lootable), visual removed with
  `Refresh(false)` (controller kept), armor stays equipped (not lootable). `RegainedConsciousness` → `OnWokeUp`: the weapon
  still in the inventory is re-equipped, a looted one leaves the NPC on fists for good; full refresh.
- **Saved** (spec-npc-equipment-save): `SaveableObject` captures `NpcArmament.Equipped` + `DroppedWeapon` (`ObjectSaveData.equipped` / `droppedWeaponId`, via the shared `EquipmentSaveConverter`) once `HasLoadout` (null before
  `Start`, so a corpse hidden at load keeps its authored armor). No catalog → restore skipped, authored loadout kept. Restore runs after `Start`, so
  `RestoreLoadout` replaces the granted loadout (`RestoreEquipped` never touches the already-restored inventory) and sets
  `_loadoutRestored` so a corpse whose `Start` runs later doesn't grant again. Knockout isn't saved: a living NPC saved while
  down loads standing and re-equips its dropped weapon if its inventory still holds it; a corpse keeps it as loot.
  `equipped == null` (old save / no armament) → the loadout is granted as before. Brann (`NPC_StashOwner`) has no loadout.

## Code Review Checklist — AI

| Severity | Pattern |
|----------|---------|
| HIGH | Attacking entity without `EntityMeleeAttacker` on the root or `EntityAnimationEventReceiver` on the Animator GO — attacks animate but deal no damage |
| HIGH | `EntityBrain` (or any AI code) calling `TakeDamage` directly |
| HIGH | A damage source calling `TakeDamage(amount)` with a known attacker — use `TakeDamage(amount, attacker)` or brawls kill |
| MEDIUM | Hitbox GO under a ragdoll bone carrying a `Rigidbody` — drivers collect every child Rigidbody for the ragdoll |
| MEDIUM | Clip event id (`HitboxEnable("Bite")`) not matching a registered `_hitboxes` id — one warning, then silent misses |
| MEDIUM | `_onLootRequested` unwired on a prefab that has its own `EntityPresence` / `NPCPresence` — corpse never opens |
