---
title: 'Stat scaling & balance framework (Gothic-style)'
type: 'feature'
created: '2026-11-16'
status: 'done'
baseline_commit: '884cd2bf7bf40e054a1ba6462ce69c965698eb09'
context:
  - '{project-root}/_bmad-output/project-context.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** Health, damage and defense are flat placeholders (player hits for 35 from minute one, every creature is 40–80 HP and gives 25 XP, enemies have no armor, 5 levels total), so there is no difficulty arc to playtest against.

**Approach:** Adopt a Gothic-style model: weapon damage + its scaling stat (STR or DEX, 1:1), hybrid armor mitigation `max(raw − armor, raw × 10%)` on both player and entities, a creature `Level` that drives XP (level × 10), a ~30-level curve `100·n(n+1)/2` with 10 LP per level, then rebalance every existing asset to a written tier table so the player starts weak (40 HP) and ends overpowered. Fists are a weapon (base damage 10 + STR). The obsolete test skill Power Strike is removed with its tome, teaching and dialogue.

## Boundaries & Constraints

**Always:** One pure, tested damage helper used by player melee, player arrows, AI hits and both `TakeDamage` paths. Unarmed scales with STR; fists do not ignore armor. Entity armor is a single `Entity` value. Tier table lives in `_bmad-output/balance.md` and every rebalanced asset fits its band.

**Ask First:** Renaming/removing serialized fields beyond those listed; touching stance values; changing gold costs.

**Never:** Damage types (edge/blunt/missile); NPC armor from worn `ArmorSO`; rising LP cost per stat point; Endurance → max HP; level-scaled enemies; difficulty options.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Armor below raw | raw 20, armor 10 | 10 damage | N/A |
| Armor above raw | raw 18, armor 70, floor 0.1 | 1.8 damage (floor) | N/A |
| Tiny raw | raw 3, armor 50 | 1 damage (absolute minimum 1) | N/A |
| Zero raw | raw 0 | 0 damage | N/A |
| STR sword | weapon 12 (STR), STR 15 | raw 27 | N/A |
| DEX bow | bow 12 (DEX), DEX 14 | raw 26 | N/A |
| Unarmed | fists 10 + STR 10, step multiplier 1.5 | raw 30 | N/A |
| Derived XP | Entity level 5, `_xpOnKill` −1 | XpOnKill 50 | explicit ≥0 value overrides |

</frozen-after-approval>

## Code Map

- `Scripts/Combat/PlayerCombat.cs` -- `ComputeEffectiveDamage` (L1060), `ComputeRangedDamage` (L900), unarmed multiplier at `OnWeaponHit`
- `Scripts/Combat/RangedDamage.cs` -- old DEX formula, to be replaced
- `Scripts/Player/PlayerHealth.cs` -- `TakeDamage` flat `amount − Defense`, min 1
- `Scripts/AI/EntityHealth.cs` -- `TakeDamage` without mitigation; has nullable `_combatConfig`
- `Scripts/AI/EntityBrain.cs:1848` -- AI raw damage = `Entity.AttackDamage` + wielded `damageBonus` (unchanged)
- `ScriptableObjects/Entities/Entity.cs` -- stats + `_xpOnKill`
- `ScriptableObjects/Items/WeaponSO.cs`, `Weapons/BowSO.cs` -- `damageBonus`
- `ScriptableObjects/Config/CombatConfigSO.cs`, `ProgressionConfigSO.cs` -- `attackDamage`, `damagePerStrength/Dexterity`, base stats
- `Scripts/UI/Inventory/ItemDetailFormatter.cs` -- weapon detail lines
- `Data/Config/*.asset`, `Data/Entities|NPCs/**` entity assets, `Data/Items/**` weapons/armor, `Data/Skills/**`, `Data/Origins/*`, `Data/NPCs/BlackSmith/Teachings/Training/*`

## Tasks & Acceptance

**Execution:**
- [x] `Scripts/Combat/DamageMath.cs` -- new static: `WeaponRaw(weaponDamage, statValue)`, `Mitigate(raw, armor, minFraction)` per matrix; delete `RangedDamage.cs` -- single formula
- [x] `ScriptableObjects/Items/WeaponSO.cs` -- add `StatType scalingStat = Strength`; tooltip `damageBonus` as base weapon damage -- per-weapon scaling
- [x] `ScriptableObjects/Config/CombatConfigSO.cs` -- remove `attackDamage`, `powerStrikeStanceBonus`; add `[Range(0,1)] minDamageFraction = 0.1`, `unarmedDamage = 10` -- floor knob, fists as a weapon
- [x] `ScriptableObjects/Config/ProgressionConfigSO.cs` -- remove `damagePerStrength/Dexterity`; defaults base stats 10, LP 10, 30-entry curve -- new scale
- [x] `Scripts/Combat/PlayerCombat.cs` -- melee = `WeaponRaw(weapon, stat(scalingStat))` (unarmed: (`unarmedDamage` + STR) × step multiplier); arrows = `WeaponRaw(bow, stat(bow.scalingStat))` -- 1:1 stat scaling
- [x] `Scripts/Player/PlayerHealth.cs`, `Scripts/AI/EntityHealth.cs` -- apply `Mitigate` with Defense / `Entity.Armor`, log raw + armor -- symmetric mitigation
- [x] `ScriptableObjects/Entities/Entity.cs` -- add `[Min(1)] _level`, `[Min(0)] _armor`; `XpOnKill` = `_xpOnKill >= 0 ? _xpOnKill : _level * 10`, default −1 -- XP from level
- [x] `Scripts/UI/Inventory/ItemDetailFormatter.cs` -- add "Scales with" line (stat label) for weapons -- player-visible scaling
- [x] Data assets -- apply the Design Notes tables (configs, entities with `_xpOnKill: -1`, weapons + bow `scalingStat` Dexterity, armor, skills, origins, Blacksmith `limitStat`); fix `ProgressionConfig.asset` stale `baseMana` key to `baseIntelligence` -- rebalance
- [x] Power Strike removal -- delete `Skill_PowerStrike`, `SkillFact_Power_strike_learned`, `Item_Tome_PowerStrike` (+ prefab, icon), `Text_Blacksmith_Training_ConfirmPowerStrike`; strip it from `SkillCatalog`, `ItemCatalog`, Blacksmith training choices, StartingTown chests and TestScene; drop `ProgressionConfigSO.powerStrikeDamageBonus`, the `StanceMath` bonus parameter and `PlayerCombat._playerSkills` -- obsolete test skill
- [x] `_bmad-output/balance.md` -- tier table, formulas, worked examples; link from `gdd.md` Difficulty Curve and `Scripts/Combat/CLAUDE.md` (replace stale damage text) -- reference for future content
- [x] `Tests/EditMode/` -- `DamageMathTests` (matrix), entity XP derivation; update `StatCombatEffectTests`, `RangedDamageTests` (remove), `EquipmentStatEffectsTests`, `ItemDetailFormatterTests` -- grep old values

**Acceptance Criteria:**
- Given a fresh game, when the player (40 HP, STR 10, short iron sword) fights a wolf, then each hit deals 12 and the wolf takes ~6 hits.
- Given any entity asset, when inspected, then its level/HP/damage/armor/XP sit inside its tier band in `balance.md`.
- Given the project compiles, when EditMode tests run, then all pass.

## Spec Change Log

- **2026-11-16, review loop 1 (human-approved amendment, no revert).** Trigger: edge-case + blind reviews found unarmed raw = STR alone (10) equals civilian armor, so early fistfights dealt the 1-damage floor (~80 punches per civilian). Amended: frozen Intent / Always / matrix row for fists (base damage 10, still mitigated by armor); the human also asked to remove the obsolete Power Strike skill and its content. Blacksmith armor 15 → 20 (T2 band), Tam HP 75 recorded as a band exception. Avoids: unwinnable early brawls. KEEP: the `DamageMath` formulas, mitigation inside each victim's `TakeDamage`, level-derived XP, all other rebalanced values.

## Design Notes

**Tiers** (level · HP · dmg · armor · XP=lvl×10; weapon dmg; worn armor total): T0 1–2 · 15–30 · 6–10 · 0 (wpn 5–10, armor 0–5) · T1 3–6 · 40–90 · 12–25 · 5–15 (10–20, 10–20) · T2 7–14 · 100–200 · 25–55 · 20–40 (25–40, 30–45) · T3 15–24 · 250–450 · 60–100 · 50–80 (45–65, 55–75) · T4 25+ · 600–1500 · 120–200 · 90–150 (70–90, 80–100).

**Player:** baseHealth 40, +10 HP & 10 LP per level, base STR/DEX/END/INT 10, xpPerLevel[i] = 100·n(n+1)/2, n = 1..30.

**Entities (lvl/HP/dmg/armor):** Rat 1/20/6/0 · BeatenWorker 2/75/8/0 (band exception: 3 Warden fist blows in the arrival beating) · Spider 3/45/15/5 · Grunt 3/50/15/5 · Viper 4/50/20/5 · Wolf 5/70/18/10 · HumanoidNPC template & civilians (Cook, Storekeeper, TentMate, FarmReeve, StashOwner, TimberBoss, QuarryOverseer) 5/80/12/10 · Bandit 6/90/15/15 · Blacksmith 7/110/20/20 · LeadHunter 8/120/20/20 · guards (CompanyGuard ×2, Dock, Racket, Warehouse) 10/150/25/25 · Warden 12/180/30/30.

**Weapons (dmg, stat):** TestSword 8 STR · Axe 12 STR · ShortIronSword 12 STR · Forged 16 STR · big sword 30 STR · Flamberge 40 STR · HuntingBow 12 DEX. **Armor:** CompanyGrunt 8 · TestArmor 15 · TestHelmet 5.

**Fists:** `unarmedDamage` 10 → fresh character vs civilian (armor 10) = 10 / 10 / 20 per punch.

**LP economy:** Beginner Lockpicking 10 (DEX 15), Expert 15 (DEX 30), Smithing 10; Blacksmith STR training limit 30; origins Blacksmith STR+5 END+5, Joiner DEX+10. `StatFact_Strenght_8` is unreferenced — leave it.

## Verification

**Commands:**
- `mcp__UnityMCP__read_console` (errors) after `refresh_unity` -- expected: no compile errors
- `mcp__UnityMCP__run_tests` mode EditMode -- expected: all green

**Manual checks:**
- Play: player vs wolf / guard logs show `raw − armor` damage matching the AC.

## Suggested Review Order

**Damage formula (entry point)**

- The whole model in two pure functions: stat-scaled raw hit, armor with a 10 % floor.
  [`DamageMath.cs:22`](../../Assets/_Game/Scripts/Combat/DamageMath.cs#L22)

- Player melee raw: weapon + its scaling stat; fists are a weapon (10 + STR) × combo step.
  [`PlayerCombat.cs:1051`](../../Assets/_Game/Scripts/Combat/PlayerCombat.cs#L1051)

- Bow raw: bow damage + Dexterity, snapshotted at fire time.
  [`PlayerCombat.cs:895`](../../Assets/_Game/Scripts/Combat/PlayerCombat.cs#L895)

**Mitigation lives in the victim**

- Entities subtract `Entity.Armor`; callers always pass raw damage.
  [`EntityHealth.cs:173`](../../Assets/_Game/Scripts/AI/EntityHealth.cs#L173)

- Player subtracts equipment Defense through the same helper.
  [`PlayerHealth.cs:181`](../../Assets/_Game/Scripts/Player/PlayerHealth.cs#L181)

**Data model**

- Creature level anchors its tier; XP derives from it unless overridden.
  [`Entity.cs:120`](../../Assets/_Game/ScriptableObjects/Entities/Entity.cs#L120)

- Per-weapon scaling stat; bows default to Dexterity via constructor.
  [`WeaponSO.cs:18`](../../Assets/_Game/ScriptableObjects/Items/WeaponSO.cs#L18)
  [`BowSO.cs:14`](../../Assets/_Game/ScriptableObjects/Items/Weapons/BowSO.cs#L14)

- Tuning knobs: armor floor fraction and fist base damage.
  [`CombatConfigSO.cs:45`](../../Assets/_Game/ScriptableObjects/Config/CombatConfigSO.cs#L45)

- 30-level curve default `100·n(n+1)/2`; the asset holds the shipped values.
  [`ProgressionConfigSO.cs:14`](../../Assets/_Game/ScriptableObjects/Config/ProgressionConfigSO.cs#L14)

**Power Strike removal**

- Stance math loses its bonus parameter; nothing else read the skill.
  [`StanceMath.cs:52`](../../Assets/_Game/Scripts/Combat/StanceMath.cs#L52)

**Player-facing + reference**

- Item detail shows which stat a weapon scales with.
  [`ItemDetailFormatter.cs:101`](../../Assets/_Game/Scripts/UI/Inventory/ItemDetailFormatter.cs#L101)

- Tier bands, formulas, current content and worked examples for future authoring.
  [`balance.md:1`](../balance.md#L1)

**Tests**

- Matrix rows, clamps and level-derived XP.
  [`DamageMathTests.cs:14`](../../Assets/Tests/EditMode/DamageMathTests.cs#L14)
