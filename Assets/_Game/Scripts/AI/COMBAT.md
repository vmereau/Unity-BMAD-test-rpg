# Combat — EntityMeleeAttacker, tactics, blocking, potions, stance

> Read when touching `EntityMeleeAttacker`, `AttackComboPlan`, `EntityAttackStep`, `SMB_EntityAttackState`,
> `EntityCombatTactics` / `CombatTacticsRules` / `AttackTokenRegistry`, `EntityGuard` / `CombatBlockRules`,
> `EntityPotionDrinker`, `EntityStance`, or the `EntityBrain` Engaging / Attacking / Staggered / KnockedDown handling.
> Specs: `spec-ai-attack-root-and-step`, `spec-ai-combat-tactics-foundation`, `spec-ai-npc-blocking`,
> `spec-shield-offhand-blocking`, `spec-potion-drink-animation`, `spec-ai-stance-stagger-knockdown`,
> `spec-player-stance-stagger-knockdown`. Shared event / SMB contract: `.claude/rules/attack-pipeline.md`.

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
- **Swing rules = the player's, all entities:** while `EntityMeleeAttacker.IsInSwing`
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

## Combat tactics (EntityCombatTactics)

- **Opt-in by data:** `Entity._combatStyle` (`CombatStyleSO`, `Data/Entities/CombatStyles/`) + `EntityCombatTactics` on the
  root (`NPC_base Variant`). No style / no component → the plain chase-and-swing (monsters). Styles: `CombatStyle_CompanyGuard`
  (the seven Company guards, `BarnGuard` Venn included — patient, "competent but beatable") and `CombatStyle_Bandit` (pushier).
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
- Open ideas (blocking, potions and ranged are done): `deferred-work.md`.

---

## Blocking (EntityGuard)

- **Opt-in by data:** `CombatStyleSO` "Blocking" (`blockChance` 0 = never; guard 0.4, bandit 0.25) + `EntityGuard` on the root
  (`NPC_base Variant`). A parallel check, not a `CombatAction`: `Block_State` is UpperBody-only, so the NPC keeps circling /
  backing off with its guard up. The raise passes `NpcArmament.ShieldInHand` (`AIAnimationDriver.SetBlocking(true, shield)`),
  which picks the shield guard or the weapon's own guard (spec-one-hand-braced-block).
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
  dead / unconscious, `IsChangingWeapon`, drinking a potion, **guard broken** (`EntityStance.IsGuardBroken`: stance ≤ threshold),
  **ranged mode** (`NpcArmament.IsRanged`), or no ready `NpcArmament` (`IsReady`). Any melee mode guards
  (spec-unshielded-blocking): fists, one-hander, two-hander. A shield in hand (`NpcArmament.ShieldInHand` = a `ShieldSO` in
  the Offhand slot and the weapon drawn) keeps the full `perfectBlockChance`; without one it is × the style's
  `unshieldedPerfectBlockMultiplier` (0.5, `CombatBlockRules.PerfectBlockChance`) and no shield armor. Guards (`CompanyGuard_*`,
  Dock / Racket / Warehouse / Barn guards, Warden) carry `Shield_KiteWoodShield`, the bandit `Shield_RoundWoodShield`
  (`NPCEntity._startingEquipment`). The shield's `defenseBonus` adds to `Entity.Armor` in `EntityHealth.TakeDamage`
  (`NpcArmament.ShieldArmorBonus`; none in a fists fight — the shield stays on the back; body armor still doesn't count). Shield stamina / window are player-only — NPCs use the
  style's `perfectBlockChance` (halved without a shield, above). A bow swap bumps the shield into the inventory; `NpcArmament.EquipCarriedShield` re-equips it
  on the way back to melee and on wake-up — only the shield it stowed (`_stowedShield`), never trade stock. `GrantLoadout`
  equips shields after weapons (a loadout shield next to a two-hander is carried, warned). A restored living NPC with no
  offhand re-equips its starting shield only if its inventory still holds it (never granted anew: a looted shield stays
  gone). Going down (knockout / death) drops the shield into the lootable inventory with the weapon; wake-up re-equips it
  if not looted (`RANGED_EQUIPMENT.md` → Down, spec-npc-shield-loot-on-down).
- **Hit:** `EntityHealth.TryReceiveHit` → `EntityGuard.ResolveHit(attacker)`: raised + attacker in the front arc **and within
  `guardThreatRange`** → `Blocked`, or `PerfectBlock` on `perfectBlockChance` (× the unshielded multiplier without a shield); else `NotBlocked`. Any attacker in reach is
  blocked, not only the read target. Arrows share the check: a point-blank shot into a raised guard is blocked, a distant
  shooter is never blocked or parried (the parry would stagger the player at range).
- A blocked hit raises no damage reaction (no `HealthChanged`), like the player's own block — a guarding NPC doesn't retarget
  to a third party whose hits it blocks.
- **Guard budget = stance:** attackers apply `ApplyBlockedStanceDamage` on `Blocked` (`EntityMeleeAttacker`, and
  `PlayerCombat.ResolveHit`); `EntityStance` scales it by the style's `guardStanceMultiplier` (never knockdown) — crossing the
  threshold staggers (guard break), and the guard stays down until regen lifts stance above it. Without that gate the
  floored-hit rule would let a broken guard absorb blocked hits for free.
- **Parry:** NPC attacker → its own `Entity.PerfectBlockStanceDamage` (existing); player attacker →
  `PlayerStance.ApplyParriedStanceDamage(CombatConfigSO.parriedStanceDamage)` (60, no knockdown, skips attack resistance).

---

## Potions (EntityPotionDrinker)

- **Opt-in by data:** `CombatStyleSO` "Potions" (`drinkHealthThreshold` 0 = never; guard 0.35, bandit 0.25 but no potion) +
  `EntityPotionDrinker` on the root (`NPC_base Variant`) + a drinkable `PotionItemSO` (`IsDrunk`: kind `Potion`, not an ingredient) in the NPC `InventorySystem`
  (six of the seven Company guards: one `Item_Health_Potion` each, scene `_startingItems` overrides in StartingTown;
  `BarnGuard` (Venn) has none). Meals / raw
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

## Stance (EntityStance)

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
