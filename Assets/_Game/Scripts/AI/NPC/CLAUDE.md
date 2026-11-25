# CLAUDE.md — Assets/_Game/Scripts/AI/NPC

> Loaded when Claude accesses files in this folder. NPC-only components: daily routines and sleep (`NpcRoutine` →
> `EntityBrain`), `NPCPresence` dialogue, memories, toll gates. Namespace: `Game.AI`. Parent rules: `Scripts/AI/CLAUDE.md`.
> Read this too before touching the `EntityBrain` routine / Patrolling / Sleeping / Intercepting code.
> Specs: `spec-npc-routine-sleep`, `spec-racket-guard-exit-toll`.

---

## What's here

| File | Role |
|------|------|
| `NPCPresence` | `: EntityPresence` — adds dialogue. Alive **and hostile to the player** (`FactionSO.AreHostile(own, TargetRegistry.Player.Faction)`, read live; no player → not hostile) → no prompt, `CanInteract` false (bandits); corpse loot unchanged. |
| `NPCMemoryComponent`, `NPCDialogueGraphComponent` | An NPC's active memory set; resolve available dialogue start nodes / choices. |
| `NpcTalkOpener` | Data for `NPCPresence._talkOpeners` — see **NPCPresence** below. |
| `TollGate`, `TollGateRules` | Exit toll — see **Toll gates** below. `TollGateRules` is pure (`TollGateRulesTests`). Racket data: `Game.NPC.TollRacketSO` (`ScriptableObjects/Entities/NPC/`). |
| `NpcRoutine`, `NpcRoutineBlock` | Daily routine (Stand / Patrol / Sleep blocks — owns all patrol routes). `NpcRoutineSchedule` (`FindBlock`, `FindAllDayBlock`, `NearestIndex`) is pure (`NpcRoutineScheduleTests`). |
| `NpcSleepRules` | Pure sleep rules (`ShouldLieDown`, `IsQuiet`) — see **NPC sleep**. `NpcSleepRulesTests`. |
| `NpcTorchRules` | Pure night-torch rule `WantsTorch(flag, hour, sunrise, sunset, asleep)` — see **NPC routines**. `NpcNightTorchTests`. |

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
- A stored routine request is re-applied in `EntityBrain.OnEnable` (respawn).
- **Night torch** (spec-npc-night-torch): `_torchAtNight` + `_dayNight` (`DayNightConfig`; null = 6 / 22). Every poll (and on
  time skip) → `NpcArmament.SetTorchWanted(NpcTorchRules.WantsTorch(...))`: night = outside sunrise → sunset, not asleep
  (`EntityBrain.IsAsleep`), a clock loaded. Independent of the blocks and of `Suspend`. The offhand swap itself (shield back
  for fights, loot, grant) is `NpcArmament`'s: `Scripts/AI/RANGED_EQUIPMENT.md` → Night torch.

---

## NPC sleep

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

## NPCPresence (dialogue)

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
- **`NPCPresence`** overrides `Interact()`: dead / unconscious → `base.Interact()` (loot, never dialogue); alive and out of
  combat → raises `_onDialogueRequested`; in combat → blocked. Prompt `"Loot"` / `"Talk"` / `""`;
  `CanInteract => IsLootable || IsAliveAndOutOfCombat`.
- **Wiring gotcha:** `_onLootRequested` must be wired on **both** `Entity_base.prefab` (monsters) and the NPC
  variant's own `NPCPresence` — the NPC variant removes the inherited base component, so the base wiring
  doesn't carry over (`Prefabs/Entities/CLAUDE.md`). Base loot behaviour: `Scripts/AI/CLAUDE.md` → EntityPresence loot.

---

## Toll gates (TollGate → EntityBrain)

- `TollGate` (scene, under the StartingTown root `TollGates`): zone = child `BoxCollider`s (trigger, Ignore Raycast,
  containment on demand like `RestrictedArea`) spanning the whole gate; `_collector` (Harl's `EntityBrain`), `_post` (his
  routine spot), `_paidFact` (`WorldFact`), hours, `_racket` (`TollRacketSO`: shared deferred / beaten facts, forced openers,
  pay / defer / refuse `DialogueSignalSO`s, talk distance, duty radius, timeouts), `_onDialogueSignal`.
- Flow: player in zone + `CanCollect` (in hours, gate unpaid, racket not beaten, gate **armed**, collector on duty = on its
  routine, conscious, within `dutyRadius` of the post) → `EntityBrain.BeginIntercept` → **Intercepting** (non-combat walk at
  `EngageSpeed`, waits for a locked cursor, stops facing the player) → `Reached` → `NPCPresence.RequestForcedTopic` (the
  "other gate paid" opener when any sibling gate of the same racket is paid) → dialogue closes → last signal: pay → paid fact,
  defer → racket deferred fact, none / refuse → `EntityBrain.StartBrawl` (aggro + `ForceAware` + Engaging, same mechanism as
  trespass escalation fight — `Scripts/AI/PERCEPTION.md`). Brawl: player unconscious → this gate paid (the normal knockout **Looting** robs them — `Scripts/AI/KNOCKOUT.md`); collector
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

---

## StartingTown routine cast

- StartingTown (spots under `NpcRoutineSpots`), 06–21 stand at spawn / 21–06 spot: Storekeeper on the commute north of the Company store and
  Cook by the canteen (both spots date from the removed houses; move them when those NPCs move indoors), Blacksmith in his room at the Timberworks forge, Warden outside the Company
  office door on the outcrop (NPCs can't open doors). Old Hobb (`TentMate`): 06–21 Patrol between his hut door (`worker-mate`) and the Player's Zone campfire, 21–06
  **Sleep** in `PlayersZone/HobbsBed` inside his hut (his restricted area: see `Scenes/STARTING_TOWN.md` → Hobb's hut and bed; `Spot_TentMate_Night` is unused since). `DockGuard`: 06–21 spot `DockGuard_GuardPost` (in front of the dock guard post), 21–06 Patrol on
  its deck (`NpcPatrolRoutes/DockGuard`). `WarehouseGuard` and `bandit`: all-day Patrol. `CompanyGuard_ArrivalLeft` / `Right`: all-day stand at `Spot_ArrivalGuard_Left` / `Right` (arrival gate). `RacketGuard` (Harl): 06–14
  `Spot_RacketGuard_QuarryGate`, 14–21 `Spot_RacketGuard_WildernessGate` (his toll posts), 21–06 `Spot_RacketGuard_Night` outside
  The Short Measure. `BeatenWorker` (Tam): all-day stand at `Spot_BeatenWorker_Campfire`. TitheBarnShortfall cast (scene root `TitheBarnShortfall`): `BarnGuard` (Venn) 06–21 `Spot_BarnGuard_Gate`, 00–03 `Spot_BarnGuard_BarnDoor` (block label `Barn`), otherwise Sleep in `BarnGuardBed` (barracks `Bunk_0`); `FieldHand_Pim` / `BarnWorker_Gerrit` 06–21 `Spot_FieldHand_Strips` / `Spot_BarnWorker_Threshing`, 21–06 Sleep in `FieldHandBed` / `BarnWorkerBed` (tent camp, west of Hobb's hut).
- **Night torch on** (`_torchAtNight`): DockGuard, WarehouseGuard, CompanyGuard_ArrivalLeft / Right, RacketGuard (Harl),
  BarnGuard (Venn — asleep 21–00 / 03–06, so only at the barn door).

---

## Code Review Checklist — AI/NPC

| Severity | Pattern |
|----------|---------|
| HIGH | A story sequence calls `NpcRoutine.Suspend()` without a matching `Resume()` (also from `OnDisable`) — the NPC never returns to its routine |
| MEDIUM | `_onDialogueCloseRequested` unwired on an NPC variant's own `NPCPresence` or a non-prefab scene NPC — the base `Entity_base` wiring doesn't carry over (`_onLootRequested`: root AI checklist) |
| MEDIUM | `NPCPresence` code treating a `RaiseDialogueRequest` as opened without checking the cursor unlocked synchronously — `DialogueSystem` ignores a request while another dialogue is open |
