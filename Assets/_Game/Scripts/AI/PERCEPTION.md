# Perception & stealth — EntityBrain + EntityPerception

> Read when touching `EntityPerception`, `TheftPursuitRegistry`, or the `EntityBrain` states Suspicious / Searching /
> Watching / Pursuing / Confronting: witness mode, theft pursuit and return, trespass, hearing, patrol detection,
> damage reaction (`NotifyHitBy`) and sneak attack (`IsUnawareOf`).
> Specs: `spec-theft-return-or-fight`, `spec-trespass-repeat-offence-fight`. Detection formula: `Scripts/Stealth/CLAUDE.md`.

---

## Perception & stealth

- **Perception is optional:** null or inactive (`DetectionRange <= 0`, no config) → exactly the plain radius
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
  Give up (`TheftDetection.ShouldGiveUp`): give-up timer ≥ `theftChaseLoseSightTime` (see below), beyond `theftChaseMaxDistance`, after
  `theftChaseMaxDuration`, thief dead, or the incident claimed by another witness. A hostile found by the throttled
  radius scan while Pursuing takes over (normal Warning / Engaging reaction, like Watching). **Sensed = seen (LOS) or heard**
  (`CanHearTarget` on the chased target — perception in trespass / hostile mode, so also a theft inside an area the witness
  owns; witness mode never hears): it refreshes `_pursuitLastSeen` and the agent paths to the target, otherwise to the last
  sensed spot (re-requested only when it moves). Give-up timer (`TheftDetection.NextNoSightTime`): sensed while making
  headway → 0; walking on unsensed → held; **standing still** (`IsWalkingToPursuitSpot` false: arrived, end of a partial
  path, blocked, < 0.2 m/s) → runs even if heard, so a guard over an unreachable cellar or under a ledge gives up. Catch (`CanCatch`: flat distance ≤ `theftCatchDistance`,
  height gap ≤ `theftCatchMaxHeight` 1.2 m, **LOS**, **and the cursor locked** — waits while a menu is open; a guard on the
  floor above a cellar, or behind a wall, walks round instead of scolding through it) + `TheftPursuitRegistry.TryClaim` →
  **Confronting**: stops, faces the thief, raises `OnNPCDialogueRequested` with `forcedLine` (item thefts use
  `ItemTheftScoldBarks`, else `TheftScoldBarks`, else a default line); resumes its routine when the dialogue closes
  (cursor locked again) or after `theftConfrontOpenTimeout` if it never opened. A hostile hit while Pursuing /
  Confronting triggers the normal combat reaction. Known gap: the player can still talk to a chasing NPC normally —
  it scolds right after.
- **Theft return:** a pursuer keeps every theft id it adopts in one chase (`_pursuitTheftIds`,
  trespass ids excluded, cleared on a new chase / `EndPursuit`). At the catch, `TryOfferReturn` sums them from
  `Game.Core.StolenGoodsLedger` and asks the player (`OnStolenGoodsRequested`, query) what they still carry; something left →
  `NPCPresence.RequestForcedTopic(StealthConfigSO.theftReturnDialogue, scoldLine)` (scold bark first, then the shared
  give-back / refuse choices in `Data/NPCs/Shared/Dialogues/TheftReturn`), else the lone scold line. `OnDialogueSignal`
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
  - **Escalation:** `area.ShouldFightTrespasser(trespassWarningExpiryHours)` (the
    area's active warnings ≥ its `_warningsBeforeFight`; 0 = attack on sight) →
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
  An **awake owner hearing a trespasser** fills at `StealthConfigSO.trespassHearingFillTime` (1 s; `StealthDetection.HearingFillTime`),
  hostile mode and asleep owners at `hearingFillTime` (2 s).
- **Trespass noise investigation** (spec-trespass-hearing-fast-detection): a Suspicious owner that has heard the trespasser
  records the last spot inside the area where it sensed them (`_trespassHeardSpot`, cleared on entering Suspicious). Once
  they are neither seen nor heard for 0.5 s (`TRESPASS_INVESTIGATE_DELAY`: out of range, sneaking, still, or the perception
  mode dropped them when they left the area; not when the target turned hostile) → `TransitionToSearching(investigate: true)`
  (`IsInvestigating`): **not combat** (dialogue opens; the search waits while the cursor is unlocked), `BaseSpeed`, awareness
  kept (no `searchStartAwareness`), walks to the spot, turns `SearchDuration`, `EndSearch` → routine. Unlike a combat search
  it still witnesses thefts, joins nearby fights (`TryJoinNearbyFight`) and counts as unaware for sneak attacks. Checked
  before Suspicious's `Awareness <= 0` resume (a mode switch zeroes the meter). Re-detected during any search →
  `RespondToTrespasser`; a trespasser whose area stopped being restricted before the rescan is ignored, not engaged (unless
  it is the aggro target). Vision-only suspicion never investigates.
- **Detection while patrolling:** perception ticks every frame in every state and `HandlePatrol` checks it before path
  handling; the cone follows `transform.forward`, which the NavMeshAgent turns along the path (`updateRotation` is only
  disabled by `EntityCombatTactics` while circling / backing off / closing in, and restored when tactics end) — a walking guard sees ahead, a waiting one keeps its arrival facing.
- **Sneak attack:** `IsUnawareOf` = Idle/Patrolling/Suspicious, not fully aware, and not damaged within
  `StealthConfigSO.damageAlertDuration` (so passive / neutral entities that never fight back only give the
  bonus once). `PlayerCombat` multiplies the hit by `CombatConfigSO.sneakAttackDamageMultiplier`.
