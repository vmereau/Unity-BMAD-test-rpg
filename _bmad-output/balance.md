# Balance Reference — Stats, Damage & Difficulty Scaling

> Source of truth for combat numbers (spec `implementation-artifacts/spec-stat-scaling-balance.md`).
> Gothic-style: the player starts weaker than almost everything, levels slowly, and steers the build with
> learning points; a completionist ends overpowered against everything except late-game challenges.
> **Authoring rule:** pick a creature / item's tier first, then keep every value inside that tier's band.

---

## Formulas

| What | Formula | Where |
|------|---------|-------|
| Player raw melee | `weapon damage + scaling stat` (1:1) | `DamageMath.WeaponRaw`, `PlayerCombat` |
| Unarmed (fists) | `(unarmedDamage 10 + STR) × combo-step damageMultiplier` | `CombatConfigSO.unarmedDamage`, `PlayerCombat` |
| Player raw arrow | `bow damage + DEX` | `PlayerCombat.ComputeRangedDamage` |
| Sneak attack | raw × 2 on an unaware target | `CombatConfigSO.sneakAttackDamageMultiplier` |
| AI raw | `Entity.AttackDamage + wielded weapon damage` | `EntityBrain` |
| Damage taken | `max(raw − armor, raw × 10 %)`, min 1 | `DamageMath.Mitigate`, `CombatConfigSO.minDamageFraction` |
| Player armor | sum of worn `defenseBonus` | `PlayerStats.Defense` |
| Creature armor | `Entity._armor` | `EntityHealth` |
| Kill XP | `level × 10` (`_xpOnKill` −1); an explicit value ≥ 0 overrides | `Entity.XpOnKill` |
| Level curve | cumulative `100·n(n+1)/2` → 100, 300, 600, 1000, 1500 … 46 500 (30 levels) | `ProgressionConfig.xpPerLevel` |
| Per level-up | +10 max HP, +10 LP | `ProgressionConfig` |

Weapons choose their stat with `WeaponSO.scalingStat` (swords / axes / two-handed: Strength, bows: Dexterity).
The armor floor (10 %) is what keeps late trash harmless but not zero, and bosses killable even with low damage.

## Player baseline

| | Start | Notes |
|---|---|---|
| Health | 40 | +10 per level → ~330 at level 30 |
| STR / DEX / END / INT | 10 | 1 LP per point at trainers (Blacksmith STR cap 30) |
| LP | 10 per level | Skills cost 10–15 LP |
| Origins | Blacksmith STR+5 END+5 · Joiner DEX+10 | ≈ one level of LP |

## Tier bands

| Tier | Creature level | HP | Damage | Armor | XP | Weapon dmg | Worn armor total |
|------|----------------|----|--------|-------|----|-----------|------------------|
| T0 vermin | 1–2 | 15–30 | 6–10 | 0 | 10–20 | 5–10 | 0–5 |
| T1 early | 3–6 | 40–90 | 12–25 | 5–15 | 30–60 | 10–20 | 10–20 |
| T2 mid | 7–14 | 100–200 | 25–55 | 20–40 | 70–140 | 25–40 | 30–45 |
| T3 late | 15–24 | 250–450 | 60–100 | 50–80 | 150–240 | 45–65 | 55–75 |
| T4 challenge | 25+ | 600–1500 | 120–200 | 90–150 | 250+ | 70–90 | 80–100 |

\* Band exception: Tam's HP is sized for the scripted arrival beating (3 Warden blows of 30), not for the player.

Humanoid damage bands include the wielded weapon. Worn-armor bands are the total of all pieces; a single piece
(e.g. Company Grunt armor 8) sits below its tier until the set is completed. Civilians sit at T1, guards at T2: picking a fight with the
town guard early should be a mistake, as in Gothic.

## Current content

| Entity | Tier | Lvl | HP | Dmg | Armor | XP |
|--------|------|-----|----|-----|-------|----|
| Giant Rat | T0 | 1 | 20 | 6 | 0 | 10 |
| Beaten Worker (Tam) | T0* | 2 | 75 | 8 | 0 | 20 |
| Darkness Spider / Grunt | T1 | 3 | 45 / 50 | 15 | 5 | 30 |
| Giant Viper | T1 | 4 | 50 | 20 | 5 | 40 |
| Wolf | T1 | 5 | 70 | 18 | 10 | 50 |
| Civilians (Cook, Storekeeper, TentMate, FarmReeve, StashOwner, TimberBoss, QuarryOverseer, HumanoidNPC template) | T1 | 5 | 80 | 12 + weapon | 10 | 50 |
| Bandit | T1 | 6 | 90 | 15 + weapon | 15 | 60 |
| Blacksmith | T2 | 7 | 110 | 20 + weapon | 20 | 70 |
| Lead Hunter | T2 | 8 | 120 | 20 + weapon | 20 | 80 |
| Guards (Company ×2, Dock, Racket, Warehouse) | T2 | 10 | 150 | 25 + weapon | 25 | 100 |
| Warden | T2 | 12 | 180 | 30 + weapon | 30 | 120 |

| Item | Damage / Defense | Scales with |
|------|------------------|-------------|
| Test Sword | 8 | STR |
| Axe, Short Iron Sword | 12 | STR |
| Short Iron Sword (forged) | 16 | STR |
| Big Sword | 30 | STR |
| Flamberge | 40 | STR |
| Hunting Bow | 12 | DEX |
| Company Grunt armor | 8 | — |
| Test Helmet / Test Armor | 5 / 15 | — |

| Skill | LP | Requirements |
|-------|----|--------------|
| Beginner / Expert Lockpicking | 10 / 15 | DEX 15 / 30 |
| Smithing | 10 | — |

## Worked examples

- **Fresh start vs Wolf:** STR 10 + short iron sword 12 = 22 raw − 10 armor = **12 per hit → 6 hits**. The wolf's 18 vs
  the Company Grunt armor (8) = 10 per bite → **4 bites** kill the 40 HP player. The player is underpowered.
- **Fresh start vs Guard:** 22 − 25 armor → floor 2.2 per hit, ~68 hits. Don't.
- **Fresh start fistfight vs a civilian (80 HP, armor 10):** fists (10 + STR 10) × 1 / 1 / 1.5 = 20 / 20 / 30 raw →
  10 / 10 / 20 per punch → 6 punches (2 combos). Hard but winnable; a guard (armor 25) stays out of reach.
- **Level 10, STR 30, Flamberge (40), 30 armor:** 70 raw vs Guard → 45 per hit (4 hits); the guard's ~37 raw → 7.
- **Level 25, STR 80, 60-dmg weapon, 70 armor, ~290 HP:** a wolf dies in 1 hit and bites for 1.8. Overpowered.
  A T4 boss (armor 110, raw 150): 30 per hit, 80 per bite → still a real fight.

## Out of scope (future)

Damage types (edge / blunt / missile), NPC armor from worn `ArmorSO`, rising LP cost per stat point, Endurance → max
health, knockout XP (`NPCEntity.XpOnKnockout`, still a flat 25).
