---
title: 'Potion use + drink animation — player and tactical NPC drinking'
type: 'feature'
created: '2026-11-17'
status: 'done'
baseline_commit: '427e94b'
context:
  - '{project-root}/Assets/_Game/Scripts/AI/CLAUDE.md'
  - '{project-root}/Assets/_Game/Art/Characters/Humanoids/Controllers/CLAUDE.md'
  - '{project-root}/.claude/rules/attack-pipeline.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** Potions heal the player instantly with no animation, and styled NPCs never heal: a Company guard at 10 % HP fights to the end exactly like one at full health.

**Approach:** A drink is a timed UpperBody action (`drink potion.fbx`, new `IsDrinking` bool) shared by the player and humanoid NPCs: the potion is consumed when the drink starts, the heal lands at the sip time, and a landed hit or stance reaction interrupts it (potion lost). Styled NPCs get a `Drink` tactic: below a style health threshold, with a potion and drinks left, they back off, then drink — a clear opening.

## Boundaries & Constraints

**Always:**
- Only `ConsumableKind.Potion` plays the drink; meals / raw food keep the instant use. A drink can't start while one runs (second use rejected, not consumed).
- **Timing from `CombatConfigSO`** (`drinkHealTime`, `drinkDuration`, measured on the trimmed clip, like `knockoutLootGrabTime`), not clip events: identical for both actors and immune to override swaps.
- **Interrupt** = damage actually taken (`HealthChanged` decrease / `PlayerHealth` damage), stagger / knockdown / unconscious / death, swimming, tactics ending. Before `drinkHealTime` → no heal, potion gone; after → healed, the tail is just cut.
- **Player while drinking:** no attack, block, aim, dodge, jump, draw / sheathe, execute, sprint (walk speed, like aiming); movement and camera free. Start refused while attacking / blocking / dodging / airborne / swimming / switching weapon / aiming / incapacitated / dead / executing / in dialogue (inventory open is fine).
- **NPC heal path:** `PotionItemSO` heals an `EntityHealth` directly; a heal-over-time item on an NPC heals its full amount instantly (no `StatusEffectHost` on NPCs). No first-meal bonus for NPCs.
- **NPC drinking:** agent stopped, faces the target, token released, guard down and never raised, no swing (`IsAttackReady` false). The potion leaves its `InventorySystem` at the start. An undrunk potion stays lootable (existing corpse / knockout loot).
- **Pure + tested:** `CombatTacticsRules` decides Drink / BackOff-for-drink (threshold, has potion, drinks used < `maxDrinksPerFight`, `drinkCooldown`, safe distance or back-off timeout); `DrinkTimeline` (pure) owns sip / done / interrupt. Drinks-per-fight resets when the brain leaves combat.
- `IsDrinking` re-pushed to the animator every frame (controller swaps reset parameters). Tunables on `CombatStyleSO` / `CombatConfigSO`, no magic numbers.

**Ask First:** Changing any existing `Entity` / `NPCEntity` value; adding animator parameters beyond `IsDrinking`.

**Never:** Ranged style / bow, feints, punishing the player's recovery, NPCs drinking outside combat; a potion prop in the hand (deferred); touching `epics.md` / `sprint-status.yaml`.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Player drinks | Health potion from action bar, idle or walking | Potion −1, drink clip (UpperBody), +50 HP at sip, walk-speed until done | N/A |
| Player hit early | Damage before sip | Drink cut, no heal, potion gone | N/A |
| Player busy | Use while attacking / blocking / dodging | Rejected, potion kept | N/A |
| Meal | Use cooked fish | Instant / over-time heal as today, no clip | N/A |
| NPC low | Guard ≤ 35 % HP, 1 potion, 0 drinks | BackOff until ≥ safe distance (or timeout), then Drink; healed at sip | N/A |
| NPC punished | Player hits the drinker | Drink cut, potion gone, back to circling; no second drink (cap 1 / no potion) | N/A |
| NPC no potion | Bandit / potion looted | Fights on, never drinks | N/A |
| Down with potion | Guard killed / knocked out undrunk | Potion in the loot | N/A |

</frozen-after-approval>

## Code Map

- `ScriptableObjects/Items/PotionItemSO.cs` -- `OnUse` (player only today), food HoT via `StatusEffectHost`
- `Scripts/Inventory/ActionBarSystem.cs:175`, `Scripts/UI/Inventory/InventoryUI.cs:165` -- call `OnUse`, decrement on true
- `Scripts/Player/PlayerStateManager.cs` -- `Can*` gates (`CanAttack` `:446`, `CanBlock`, `CanDodge` `:495`, `CanJump`, `CanSwitchWeapon`, `CanAim`, `CanExecute`)
- `Scripts/Player/PlayerController.cs:215` -- sprint speed choice; `Scripts/Player/PlayerAnimationDriver.cs:76` -- per-frame bool push
- `Scripts/AI/CombatTacticsRules.cs`, `EntityCombatTactics.cs` -- decision + movement; `EntityBrain.cs` `Update` `:448`, `IsAttackReady` `:767`, `EndTactics` `:786`, `SetCombatState` `:99`
- `Scripts/AI/EntityGuard.cs` -- can't-guard list; `Scripts/AI/EntityHealth.cs:203` -- `Heal`, `HealthChanged`
- `Scripts/Core/Animations/HumanoidAnimationBridge.cs`, `AIAnimationDriver.cs`, `HumanoidAIAnimationDriver.cs`, `UpperBodyWeight.cs`
- `Art/Characters/Humanoids/Controllers/Humanoid_Template.controller`, `Animations/drink potion.fbx` (266 frames ≈ 8.9 s — trim)
- `ScriptableObjects/Entities/CombatStyleSO.cs`, `ScriptableObjects/Config/CombatConfigSO.cs`; styles `Data/Entities/CombatStyles/`
- `Scenes/StartingTown.unity` -- guard instances (`InventorySystem._startingItems` overrides); `Item_Health_Potion` (heal 50, guards 150 HP)

## Tasks & Acceptance

**Execution:**
- [x] `Animations/drink potion.fbx` -- trim to the raise → sip → lower window (~2–2.5 s), root rotation / Y / XZ baked, no loop; measure sip + end on a real humanoid -- clip
- [x] `Humanoid_Template.controller` -- UpperBody `Drink_State` (tag `UpperBodyAction`), AnyState → it on `IsDrinking` (no self-transition), exit → `LockOn Locomotion` on `IsDrinking` false; new bool `IsDrinking` -- walk while drinking
- [x] `ScriptableObjects/Config/CombatConfigSO.cs` -- `drinkHealTime`, `drinkDuration` (measured) -- timing
- [x] `Scripts/Combat/DrinkTimeline.cs` (new, pure) -- `Begin`, `Tick(dt) → Sip / Done`, `Interrupt → healed?` -- shared timing
- [x] `ScriptableObjects/Items/PotionItemSO.cs` -- `OnUse`: Potion kind + `PlayerPotionDrinker` → `TryBegin(this)`; else today's path. Extract `ApplyHeal(GameObject player)` (today's body) and add `ApplyHeal(EntityHealth)` (instant full amount) -- player + NPC heal paths
- [x] `Scripts/Player/PlayerPotionDrinker.cs` (new, Player root) -- timeline; `PlayerStateManager.SetDrinking`; heal at sip; interrupt on damage / incapacitated / dead / swimming -- player drinking
- [x] `Scripts/Player/PlayerStateManager.cs`, `PlayerController.cs`, `PlayerAnimationDriver.cs`, `HumanoidAnimationBridge.cs`, `DodgeController` -- `IsDrinking` flag + gates above + walk speed + per-frame push -- player lock-out
- [x] `ScriptableObjects/Entities/CombatStyleSO.cs` -- `[Header("Potions")]` `drinkHealthThreshold` (0 = never), `maxDrinksPerFight`, `drinkCooldown`, `drinkSafeDistance`, `drinkBackOffMaxTime` -- profile
- [x] `Scripts/AI/CombatTacticsRules.cs` -- `CombatAction.Drink` (appended); drink inputs; `WantsDrink`; Decide: wants drink → Drink if far enough / backed off long enough, else BackOff; checked before the held-token rule -- pure decision
- [x] `Scripts/AI/EntityPotionDrinker.cs` (new, `NPC_base Variant` root) -- finds a Potion-kind `PotionItemSO` in the inventory, `TryBegin` (removes it), timeline, heal at sip, `Interrupt`, `IsDrinking`, drinks count / cooldown, `ResetFight`; interrupt on `HealthChanged` decrease, `OnDisable` -- NPC drinking
- [x] `Scripts/AI/EntityCombatTactics.cs` -- fill drink inputs; Drink → release token, stop agent, begin drink; hold Drink until the drinker finishes, then circle -- execution
- [x] `Scripts/AI/EntityBrain.cs` -- `EndTactics` interrupts the drink; `IsAttackReady` false while drinking; `SetCombatState(false)` → `ResetFight`; `DebugStateName` `Engaging/Drink` -- integration
- [x] `Scripts/AI/EntityGuard.cs` -- can't guard while drinking -- clear opening
- [x] `AIAnimationDriver.cs` / `HumanoidAIAnimationDriver.cs` -- virtual `SetDrinking` (monsters no-op), re-pushed every frame -- animation
- [x] Player prefab + `NPC_base Variant.prefab` -- add the drinker components -- wiring
- [x] Styles -- guard: threshold 0.35, 1 drink, cooldown 10 s, safe 4.5 m, back-off max 2.5 s; bandit: threshold 0.25, 1 drink, same timings (no potion — a desperate brawler, not a supplied soldier) -- tuning
- [x] `StartingTown.unity` (+ the Warden's scene) -- one `Item_Health_Potion` in each of the six guards' `_startingItems` -- data
- [x] `Tests/EditMode/CombatTacticsRulesTests.cs`, `DrinkTimelineTests.cs` -- matrix rows at rule level (threshold, potion, cap, cooldown, back-off-then-drink, token priority) + sip / interrupt -- regression
- [x] `Scripts/AI/CLAUDE.md` (**Potions** section), `Scripts/Player/CLAUDE.md` (gate row), Controllers `CLAUDE.md`, `deferred-work.md` (potion prop, drink out of combat) -- docs

**Acceptance Criteria:**
- Given the player in a fight, when they drink from the action bar, then the upper body plays the drink while the legs can walk, attack / block / dodge / sprint are refused until it ends, and HP rises at the sip.
- Given a guard brought under 35 % HP, when it still carries its potion, then it steps away, drinks (debug `Engaging/Drink`, no guard), and resumes circling with more HP — or, if hit mid-drink, loses the potion and the heal.
- Given a spider, a wolf or the bandit, when hurt, then they never drink.

## Design Notes

Consume-on-start + heal-at-sip makes the interrupt a real gamble for both sides with no refund bookkeeping, and keeps the inventory UI flow (`OnUse` true → decrement). The drink tactic outranks a held token: a holder in Engaging gives it back the moment it wants to drink (mid-swing it doesn't decide). Back-off timeout = a cornered NPC drinks anyway. Sword stays in hand during the drink (clip may clip through it) — accepted until the prop spec.

## Verification

**Commands:**
- Unity Test Runner EditMode (`mcp__UnityMCP__run_tests`) -- expected: all pass incl. new tests
- `read_console` after compile -- expected: no errors

**Manual checks:**
- Play StartingTown: player drinks in and out of combat; get hit mid-drink. Bring the WarehouseGuard low: back-off, drink, interrupt by hitting; loot an undrunk potion from a downed guard.

## Suggested Review Order

**NPC drink decision (entry point)**

- Drink checked first: threshold, potion, cap, cooldown; back off, then drink.
  [`CombatTacticsRules.cs:49`](../../Assets/_Game/Scripts/AI/CombatTacticsRules.cs#L49)

- Drink decided: token given back, potion taken, agent held still.
  [`EntityCombatTactics.cs:206`](../../Assets/_Game/Scripts/AI/EntityCombatTactics.cs#L206)

- While drinking: hold and face; afterwards circle again.
  [`EntityCombatTactics.cs:88`](../../Assets/_Game/Scripts/AI/EntityCombatTactics.cs#L88)

- Inputs read only when low enough; no config → never plans a drink.
  [`EntityCombatTactics.cs:232`](../../Assets/_Game/Scripts/AI/EntityCombatTactics.cs#L232)

- Back-off aims past the safe distance, not just to the ring.
  [`EntityCombatTactics.cs:296`](../../Assets/_Game/Scripts/AI/EntityCombatTactics.cs#L296)

- Tactics ending mid-drink gives the agent its movement back.
  [`EntityCombatTactics.cs:250`](../../Assets/_Game/Scripts/AI/EntityCombatTactics.cs#L250)

**NPC drink execution**

- Potion leaves the inventory at start; refused over a swing, draw or stagger.
  [`EntityPotionDrinker.cs:78`](../../Assets/_Game/Scripts/AI/EntityPotionDrinker.cs#L78)

- Heal at the sip; NPC heal path is instant, never on a downed body.
  [`EntityPotionDrinker.cs:129`](../../Assets/_Game/Scripts/AI/EntityPotionDrinker.cs#L129)
  [`PotionItemSO.cs:96`](../../Assets/_Game/ScriptableObjects/Items/PotionItemSO.cs#L96)

- A landed hit interrupts; before the sip the potion is lost.
  [`EntityPotionDrinker.cs:140`](../../Assets/_Game/Scripts/AI/EntityPotionDrinker.cs#L140)

**Brain integration**

- Single release point also cuts the drink (stagger, knockout, death, disengage).
  [`EntityBrain.cs:802`](../../Assets/_Game/Scripts/AI/EntityBrain.cs#L802)

- A drink survives a brief loss of sight instead of the plain chase.
  [`EntityBrain.cs:732`](../../Assets/_Game/Scripts/AI/EntityBrain.cs#L732)

- No swing and no guard while drinking — the opening.
  [`EntityBrain.cs:782`](../../Assets/_Game/Scripts/AI/EntityBrain.cs#L782)
  [`EntityGuard.cs:117`](../../Assets/_Game/Scripts/AI/EntityGuard.cs#L117)

- Leaving combat cuts the drink before resetting the per-fight count.
  [`EntityBrain.cs:109`](../../Assets/_Game/Scripts/AI/EntityBrain.cs#L109)

**Player drinking**

- Potions hand off to the drinker; meals and raw food heal as before.
  [`PotionItemSO.cs:85`](../../Assets/_Game/ScriptableObjects/Items/PotionItemSO.cs#L85)

- Drink start: gate, timeline, IsDrinking, saving blocked until it ends.
  [`PlayerPotionDrinker.cs:38`](../../Assets/_Game/Scripts/Player/PlayerPotionDrinker.cs#L38)

- Interrupt reasons: hit, down, swim, airborne, dialogue, scripted, load.
  [`PlayerPotionDrinker.cs:76`](../../Assets/_Game/Scripts/Player/PlayerPotionDrinker.cs#L76)

- Gates: drink start allowed with the inventory open; IsDrinking closes the action gates.
  [`PlayerStateManager.cs:168`](../../Assets/_Game/Scripts/Player/PlayerStateManager.cs#L168)
  [`PlayerStateManager.cs:474`](../../Assets/_Game/Scripts/Player/PlayerStateManager.cs#L474)

- No sprint, no dodge, sneak not dropped by Shift while drinking.
  [`PlayerController.cs:216`](../../Assets/_Game/Scripts/Player/PlayerController.cs#L216)
  [`DodgeController.cs:128`](../../Assets/_Game/Scripts/Combat/DodgeController.cs#L128)
  [`PlayerSneak.cs:122`](../../Assets/_Game/Scripts/Player/PlayerSneak.cs#L122)

**Timing and animation**

- Shared pure timeline: one sip, then done; interrupt reports a lost heal.
  [`DrinkTimeline.cs:33`](../../Assets/_Game/Scripts/Combat/DrinkTimeline.cs#L33)

- Sip / duration measured on the trimmed clip at state speed 1.5.
  [`CombatConfigSO.cs:96`](../../Assets/_Game/ScriptableObjects/Config/CombatConfigSO.cs#L96)

- IsDrinking written only through the bridge, re-pushed every frame.
  [`HumanoidAnimationBridge.cs:115`](../../Assets/_Game/Scripts/Core/Animations/HumanoidAnimationBridge.cs#L115)
  [`PlayerAnimationDriver.cs:79`](../../Assets/_Game/Scripts/Player/PlayerAnimationDriver.cs#L79)

**Peripherals**

- Style tunables (guard 0.35, bandit 0.25 without a potion).
  [`CombatStyleSO.cs:72`](../../Assets/_Game/ScriptableObjects/Entities/CombatStyleSO.cs#L72)

- Rule, timeline and gate tests.
  [`CombatTacticsDrinkTests.cs:1`](../../Assets/Tests/EditMode/CombatTacticsDrinkTests.cs#L1)
  [`DrinkTimelineTests.cs:1`](../../Assets/Tests/EditMode/DrinkTimelineTests.cs#L1)
