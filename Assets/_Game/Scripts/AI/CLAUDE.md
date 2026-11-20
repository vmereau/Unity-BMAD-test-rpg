# CLAUDE.md — Assets/_Game/Scripts/AI

> Loaded when Claude accesses files in this folder. Entity brains, health, faction targeting, melee
> attacks, and entity/NPC presence. Namespace: `Game.AI` — except `EntityPresence.cs`, which is in
> `Game.World` although the file lives here. Most system detail lives in the topic files below — read the
> matching one before editing that part of `EntityBrain` or the component.
> AI animation polymorphism (Brain/Health → Driver → Bridge): `Scripts/Core/Animations/CLAUDE.md`.
> Event/SMB contract shared with the player: `.claude/rules/attack-pipeline.md` (AI side: `COMBAT.md` → EntityMeleeAttacker).

---

## What's here

| File | Role |
|------|------|
| `EntityBrain` | State machine (flows + which topic file owns each state: **Topic files** below). Implements `ICombatStateProvider` and `Game.Combat.ISneakAttackTarget`. Targets from `TargetRegistry` (radius) and `EntityPerception` (stealth targets). `DebugStateName` for debug views. |
| `EntityPerception` | Vision-cone awareness (0..1) of **stealth targets** (the player), math in `Game.Stealth.StealthDetection`. Ticked by the brain; never changes brain state itself. Static `Active` list (F3 overlay); gizmos. On `Entity_base` root — `PERCEPTION.md`. |
| `EntityHealth` | Health for any entity; `IDamageable`. On death: stops NavMeshAgent (optional), `PersistentID.RegisterDeath(credited)`, death anim. Body stays (ragdoll, never `SetActive(false)`). Knockout / credit / execute: `KNOCKOUT.md`. |
| `KillCredit` | Pure player-credit rule for kills / knockouts (`KillCreditTests`) — `KNOCKOUT.md`. |
| `FactionMember` | Targetable faction participant; self-registers with `TargetRegistry` — see **Factions & registries**. |
| `TargetRegistry` | **Static** registry of live `FactionMember`s — use it instead of `FindGameObjectWithTag` — see **Factions & registries**. |
| `TheftPursuitRegistry` | **Static** claim set of theft ids (reset on play). `TryClaim(id)` — first catcher wins; `IsClaimed(id)` tells the other pursuers to give up. `TheftPursuitRegistryTests`. |
| `FactionSO.Defends` / `AidGroupSO` | Who defends whom in a fight — `RELATIONS.md` → Mutual defense. |
| `PlayerRelationship` | Static tier query `Of(npc)` → `RelationshipTier` Hostile / Neutral / Friendly. Pure `Resolve` (`PlayerRelationshipTests`) — `RELATIONS.md`. |
| `ICombatStateProvider` | Read-only "in combat?". Implemented by `EntityBrain`, polled by `NPCPresence`. |
| `EntityMeleeAttacker` | Entity-side owner of named `WeaponHitbox`es — `COMBAT.md`. |
| `AttackComboPlan` | Pure combo bookkeeping (`Begin`, `TryAdvance`, `Reset`) for `EntityMeleeAttacker`. `AttackComboPlanTests`. |
| `EntityAttackStep` | Optional per-state forward step during a swing (`NPC_base Variant`) — `COMBAT.md` → EntityMeleeAttacker. |
| `EntityAnimationEventReceiver` | On the **Animator GO**. Hit-window events by id → attacker; SMB notifications → attacker state counter; `ComboWindowOpen()` → `attacker.OnComboWindowOpen()`; `WeaponSwap` → `NpcArmament` (`RANGED_EQUIPMENT.md`). |
| `NpcArmament` | Equipped NPCs: loadout, weapon vs fists, draw / sheathe, weapon loot — `RANGED_EQUIPMENT.md`. |
| `NpcHeadAppearance` | NPC face: Head SMR mesh + material from `NPCEntity.Head` — see **NPC appearance**. |
| `NpcSkinTone` + `SkinToneMaterials` | NPC skin tone: toned copies of masked `SG_SkinLit` materials — see **NPC appearance**. |
| `SMB_EntityAttackState` | On attack states of `EntityBase.controller` and `Humanoid_Template`. Enter/exit → receiver → attacker counter. Receiver lookup cached; no-op on the Player. |
| `SMB_DeathState` | On the death animator state. |
| `EntityCombatTactics` + `CombatTacticsRules` + `AttackTokenRegistry` | Circling, attack tokens. Pure + tested (`CombatTacticsRulesTests`, `AttackTokenRegistryTests`). Profile: `CombatStyleSO` (`ScriptableObjects/Entities/`) — `COMBAT.md`. |
| `EntityGuard` + `CombatBlockRules` / `GuardPlan` | NPC blocking. Rules pure + tested (`CombatBlockRulesTests`) — `COMBAT.md` → Blocking. |
| `EntityArcher` | NPC bow shots. Decision in `CombatTacticsRules` (`CombatTacticsRangedTests`), lead math in `Game.Combat.RangedAimMath` (`RangedAimMathTests`) — `RANGED_EQUIPMENT.md`. |
| `EntityPotionDrinker` | Tactical potion drinking. Decision in `CombatTacticsRules` (`CombatTacticsDrinkTests`), timing in `Game.Combat.DrinkTimeline` (`DrinkTimelineTests`) — `COMBAT.md` → Potions. |
| `EntityStance` | Hidden stance meter (`IStanceTarget`) — stagger / knockdown, `COMBAT.md` → Stance. |
| `EntityPresence` (`Game.World`) | Base `IInteractable` for every entity — see **EntityPresence loot** below. |
| `NPC/` | `NPCPresence`, memory / dialogue graph components, routines, sleep, toll gates — `NPC/CLAUDE.md`. |

---

## Topic files

`EntityBrain` flows: Idle / Patrolling → (Suspicious) → Warning → Engaging ⇄ Attacking → (Searching) → back; witnesses:
(Suspicious) → **Watching** → back; theft witnesses / restricted-area owners: **Pursuing** → **Confronting** → back;
knockout winner over the player: **Looting** → back; toll collector: **Intercepting** (driven by `TollGate`) → back or
brawl; routine **Sleeping**; stance **Staggered / KnockedDown**; Dead. Warning / Engaging basics and the damage rules are
in **Rules** below.

| File | Read before touching |
|------|----------------------|
| `PERCEPTION.md` | `EntityPerception`, `TheftPursuitRegistry`; brain states **Suspicious / Searching / Watching / Pursuing / Confronting**; witness, theft pursuit + return, trespass (+ escalation fight), hearing, patrol detection, damage reaction (`NotifyHitBy`), sneak attack (`IsUnawareOf`) |
| `COMBAT.md` | `EntityMeleeAttacker`, `AttackComboPlan`, `EntityAttackStep`, `SMB_EntityAttackState`; **Engaging / Attacking** with tactics, attack tokens, blocking (`EntityGuard`), potions (`EntityPotionDrinker`), stance (`EntityStance`, **Staggered / KnockedDown**) |
| `RANGED_EQUIPMENT.md` | `EntityArcher`, NPC `BowAimCorrection`, `NpcArmament` (loadout, fists vs weapon, draw / sheathe, ranged switch, `WeaponSwap`, save) |
| `KNOCKOUT.md` | `EntityHealth` death / knockout / execute, `KillCredit`, brawl retaliation, **Looting** state |
| `RELATIONS.md` | `PlayerRelationship`, friendly assist / grace, relationship barks, `FactionSO.Defends` / `AidGroupSO`, `TryJoinNearbyFight` |
| `NPC/CLAUDE.md` | **Idle / Patrolling** routine targets (`SetRoutineTarget`, `NpcRoutine`), **Sleeping**, **Intercepting** (`TollGate`), `NPCPresence` (forced topics, talk openers, dialogue close), StartingTown routine cast |

---

## Factions & registries

- **`FactionMember` — one faction source per GO:** an entity always uses `PersistentID.Entity.Faction` (the `Entity` SO);
  the serialized `_faction` is only for a GO without an Entity — the Player (`Faction_Player`). `_faction` set on an entity →
  `GameLog.Error`, ignored. `IsPlayer` = an `IStealthTarget` component exists (even disabled). Brain / perception / attacker
  resolve the same-GO member in `Awake` (no inspector field). Consumers read `member.Faction` at query time — never copy it
  into their own field (future runtime faction change). **Only the player may implement `IStealthTarget`** (it is the
  player marker).
- **`TargetRegistry`:** reset on play-mode enter (`SubsystemRegistration`). `Player` = the registered `IsPlayer` member (null
  when absent; a second one is ignored with a warning, promoted if the first unregisters) — how non-player systems
  (`NPCPresence`) get the player's faction; Player-side systems (`LockOnSystem`, `SleepSystem`) read their own same-GO
  `FactionMember`, the same component. `NPCPresence` warns once if it is null. `FindClosestHostile(..., skipStealthTargets)` /
  `FindClosestHostileStealthTarget` / `FindClosestNonHostileStealthTarget` (witnesses).

---

## NPC appearance

- `NpcHeadAppearance`: in `Awake` swaps the model's Head SMR mesh + material to `NPCEntity.Head` (`HeadAppearanceSO`). An
  incompatible mesh (bind-pose count / values, bone indices) → warning, default head kept. Asset pipeline:
  `Art/Characters/Humanoids/HEADS_SKIN.md` → NPC head variants.
- `NpcSkinTone`: in `Start` swaps every masked `SG_SkinLit` material under the model for a cached toned copy
  (`NPCEntity.SkinTone`); re-tones a rebuilt armor by polling `EquipmentVisuals.ArmorVisual` in `LateUpdate` (no cross-system
  event). A skin material without a mask (legacy / unbaked head) is kept, one warning; every shipped `M_Head_*` has its own
  mask. Cache (`SkinToneMaterials`): one copy per source × tone, plain materials (SRP Batcher-safe), cleared on
  `SubsystemRegistration`. Pipeline: `Art/Characters/Humanoids/HEADS_SKIN.md` → NPC skin tones.

---

## EntityPresence loot

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
- `NPCPresence` (dialogue subclass, forced topics, talk openers, wiring gotcha): `NPC/CLAUDE.md`.

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
  Armed NPCs: `NpcArmament` (`RANGED_EQUIPMENT.md` → NPC equipment).
- One damage value per attack (`Entity.AttackDamage`, + the wielded weapon's `damageBonus` for an armed NPC). Two open hitboxes can each hit the same target once
  (dedupe is per `WeaponHitbox`).
- **Known gap:** spider `AttackRange` 0.8 is root-to-root, but agent avoidance holds it ≈ 1.0 m from an NPC →
  spiders engage NPCs but never bite them (they do bite the player). Retune the spider SO if needed.
- **`EntityBrain.IsAnyFighting(member)`** (static, over `_active`): some brain `IsFighting` that member — backs the player's `PlayerStateManager.IsAggroed` (aggro run / jump stamina costs, `Scripts/Player/PLAYER_STATES.md` → Aggro movement costs).
- **`EntityBrain.EndBrawl(target)`**: disengages a fight with that target back to the routine (story sequences calling off a
  scripted brawl); no-op otherwise.
- **`EntityBrain.Say(line, duration, priority)`**: scripted speech bubble over the NPC (story sequences); false without a
  line or `_onSpeechBubbleRequested`.

---

## Code Review Checklist — AI

| Severity | Pattern |
|----------|---------|
| HIGH | Attacking entity without `EntityMeleeAttacker` on the root or `EntityAnimationEventReceiver` on the Animator GO — attacks animate but deal no damage |
| HIGH | `EntityBrain` (or any AI code) calling `TakeDamage` directly |
| HIGH | A damage source calling `TakeDamage(amount)` with a known attacker — use `TakeDamage(amount, attacker)` or brawls kill |
| MEDIUM | Hitbox GO under a ragdoll bone carrying a `Rigidbody` — drivers collect every child Rigidbody for the ragdoll |
| MEDIUM | Clip event id (`HitboxEnable("Bite")`) not matching a registered `_hitboxes` id — one warning, then silent misses |
| MEDIUM | `_onLootRequested` unwired on a prefab that has its own `EntityPresence` / `NPCPresence` — corpse never opens |
