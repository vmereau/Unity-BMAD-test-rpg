# Knockout — Unconscious, kill credit, execute, Looting

> Read when touching `EntityHealth` death / knockout / execute, `KillCredit`, brawl retaliation, or the `EntityBrain`
> **Looting** state (`TryStartLooting`).
> Specs: `spec-unconscious-knockout`, `spec-knockout-xp-reward`, `spec-kill-xp-credit-assist`, `spec-execute-unconscious-npc`,
> `spec-knockout-gold-looting`.

---

## Unconscious

- **Who decides:** the victim. Hits go through `IDamageable.TakeDamage(amount, attacker)` (`PlayerCombat.ResolveHit`,
  `EntityMeleeAttacker.HandleHit`); `TakeDamage(amount)` = unknown source = lethal. A 0-HP blow is a **knockout** when
  `FactionMember.IsKnockoutPair(attacker, victim)`: both `IsHumanoid` (`IsPlayer` or `Entity is NPCEntity`) and not
  `FactionSO.AreHostile` (pure rule `Game.Combat.KnockoutRules`, `KnockoutRulesTests`). Monsters / hostiles / unknown
  always kill, even a downed target. No `RegisterDeath`, no kill, no kill XP. A knockout **credited to the player** calls
  `PersistentID.RegisterKnockout()` → one-time knockout XP per NPC (`Scripts/Player/Progression/CLAUDE.md`);
  an uncredited one leaves the claim free.
- **Player credit (`KillCredit`):** resolved by `EntityHealth` at death / knockout from the
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
- A loot screen open on an NPC that gets up closes (`OnLootSourceLost`, `CLAUDE.md` → EntityPresence loot).
- **Execute:** `EntityHealth` implements `Game.Combat.IExecutable`. `CanBeExecuted` =
  unconscious, not getting up, not dead, `NPCEntity.CanBeExecuted` (per-NPC opt-out, default true). `ExecutePoint` = the
  body Animator's Chest bone (fallback Hips, then root). `Execute(executor)` → `Die(ragdollNow: true)`: a normal death
  (`RegisterDeath`, kill XP, lootable corpse) but straight to ragdoll — the standing death clip would lift the lying body.
  Player side: `Scripts/Combat/EXECUTE.md`.

---

## Knockout loot — `Looting` state

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
