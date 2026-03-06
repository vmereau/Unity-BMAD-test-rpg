# Echoes of the Fallen - Development Epics

| # | Epic Name | Dependencies |
|---|---|---|
| 1 | Foundation & Movement | None |
| 2 | Combat System | Epic 1 |
| 3 | Progression & Stats | Epic 2 |
| 4 | World & Exploration | Epic 1 |
| 5 | Quest & Dialogue | Epic 4 |
| 6 | Inventory & Economy | Epic 3, 5 |
| 7 | Crafting & Stealth | Epic 6 |
| 8 | Content & Polish | All |

---

## Epic 1: Foundation & Movement

### Goal
Establish the Unity project foundation and prove third-person movement feels good.

### Scope
**Includes:** Unity project setup, third-person character controller, camera
system (over-the-shoulder), basic collision, simple test scene, input system
(WASD + mouse look), basic animations (idle, walk, run).

**Excludes:** Combat, UI, NPCs, any game systems.

### Dependencies
None — first epic.

### Deliverable
Player can move through a test environment with responsive third-person
controls and camera. Movement feels good.

### Stories
- As a player, I can move my character with WASD so that I navigate the world
- As a player, I can look around with the mouse so that the camera follows my aim
- As a player, I can run and walk so that movement has speed variation
- As a player, I see basic idle/walk/run animations so that movement feels alive
- As a developer, the Unity project is structured for BMAD workflow so that
  future development follows consistent patterns

---

## Epic 2: Combat System

### Goal
Implement the full stamina-based directional combat system and validate the
combat feel.

### Scope
**Includes:** Stamina system (pool, consumption, recovery), timed combo attacks
(3-hit chain with animation windows), manual blocking (hold), perfect block window
(timed, no stamina cost, staggers attacker), dodge roll (directional, stamina cost,
i-frames), basic enemy AI (patrol, engage, attack), hit detection, health system, death.

**Excludes:** Character stats/progression, specific enemy types, UI beyond
debug overlays.

### Dependencies
Epic 1 (character controller and input system).

### Deliverable
Player can fight enemies with full combat mechanics. Stamina management is
meaningful. Perfect block feels rewarding. Combat loop is validated.

### Stories
- As a player, I have a stamina bar that depletes on attacks, blocks, and dodges
- As a player, I cannot attack, block, or dodge at zero stamina
- As a player, I can chain up to 3 attacks by pressing LMB within each combo window
- As a player, I hold right mouse button to block incoming damage
- As a player, I can perform a perfect block in a tight timing window that
  staggers the attacker and costs no stamina
- As a player, I can dodge roll in any direction with i-frames
- As a player, enemies patrol and engage me when I enter their range
- As a player, enemies and I have health that depletes on hit

---

## Epic 3: Progression & Stats

### Goal
Implement the XP → level → learning point → stat/skill progression system.

### Scope
**Includes:** Base stats (Strength, Dexterity, Endurance, Mana), XP gain from
combat and exploration, level-up system, learning points awarded on level-up,
trainer NPCs (spend LP + gold to improve stats/skills), tomes (spend LP to
learn skills), stat effects on combat (damage, stamina pool, etc.).

**Excludes:** Full skill tree content, crafting skill tiers, quest XP (Epic 5).

### Dependencies
Epic 2 (combat must exist to validate stat effects).

### Deliverable
Player earns XP, levels up, spends learning points at a trainer, and sees
meaningful improvement in combat capability.

### Stories
- As a player, I gain XP from defeating enemies
- As a player, I level up when my XP reaches the threshold
- As a player, I receive learning points on level-up
- As a player, I can spend learning points and gold at a trainer to raise stats
- As a player, I can read a tome to learn a skill using learning points
- As a player, my stats affect combat outcomes (Strength → damage, Endurance → stamina pool)

---

## Epic 4: World & Exploration

### Goal
Build the starting region and first dungeon with a living world feel.

### Scope
**Includes:** Starting town layout, wilderness region, one dungeon, NPC
placement with day/night schedules, basic NPC routines (sleep, work, patrol),
environmental assets (terrain, foliage, ruins), points of interest, monster
placement (no respawn), day/night cycle, ambient lighting.

**Excludes:** Quest content, shops, dialogue system, crafting stations.

### Dependencies
Epic 1 (movement required to navigate the world).

### Deliverable
Player can explore a handcrafted starting region and dungeon. World feels
alive with NPC routines and day/night cycle. Monsters don't respawn once killed.

### Stories
- As a player, I can explore a starting town with NPC characters present
- As a player, I can venture into a wilderness region with enemies and resources
- As a player, I can enter and clear a dungeon with a shortcut back to entrance
- As a player, I see NPCs follow daily routines (sleep at night, work by day)
- As a player, enemies I kill do not respawn
- As a player, the world transitions between day and night with dynamic lighting

---

## Epic 5: Quest & Dialogue

### Goal
Implement the Gothic-style quest and dialogue system.

### Scope
**Includes:** Topic-based NPC dialogue system, quest log (title, description,
past dialogue), main quest chain (3-5 quests), 2-3 side quests, quest rewards
(XP, gold, items), permanent quest failure (NPC death = quest closed), multiple
quest outcomes.

**Excludes:** Shop system (Epic 6), voice acting.

### Dependencies
Epic 4 (NPCs and world must exist).

### Deliverable
Player can talk to NPCs via topic menu, accept quests, complete them with
multiple outcomes, and see the world react to permanent failures.

### Stories
- As a player, I can talk to NPCs and select from a list of topics
- As a player, I receive quests through NPC dialogue
- As a player, I have a quest log that stores quest titles, descriptions,
  and past dialogue
- As a player, quests have multiple possible outcomes based on my actions
- As a player, if a quest NPC dies, that quest is permanently closed
- As a player, completing quests rewards me with XP, gold, or items
- As a player, completing story quests triggers world state changes

---

## Epic 6: Inventory & Economy

### Goal
Implement the inventory, equipment, and gold economy systems.

### Scope
**Includes:** Unlimited inventory system, equipment slots (weapon, armor set,
2 rings, necklace), stat-based item system (no rarity), shop NPCs (fixed
inventory, gold prices), looting enemies and containers, gold as currency,
selling items, gated content (gold bribes).

**Excludes:** Crafting (Epic 7).

### Dependencies
Epic 3 (stats), Epic 5 (NPC interaction framework).

### Deliverable
Player can loot the world, buy and sell at shops, equip items that affect
stats, and spend gold to access gated content.

### Stories
- As a player, I have an inventory that holds all items I collect
- As a player, I can equip weapons, armor pieces, rings, and a necklace
- As a player, equipped items modify my character stats
- As a player, I can buy and sell items at dedicated shop NPCs
- As a player, I can loot gold and items from defeated enemies and containers
- As a player, I can pay gold bribes to access gated areas or services

---

## Epic 7: Crafting & Stealth

### Goal
Implement the crafting and stealth systems as layered gameplay options.

### Scope
**Includes:** Crafting stations in the world, recipe system (must be learned),
crafting skill tiers (unlocked via learning points), resource gathering
(harvesting plants, enemy drops), craftable equipment and consumables,
visibility cone stealth, crouch mechanic, NPC pickpocketing, container
stealing, caught = NPC hostile.

**Excludes:** Full recipe content library (prototype subset only).

### Dependencies
Epic 6 (inventory and items must exist), Epic 3 (learning point tiers).

### Deliverable
Player can sneak past or steal from NPCs, gather resources, learn recipes,
and craft items at crafting stations.

### Stories
- As a player, I can crouch to enter stealth and reduce my detection radius
- As a player, NPCs have visibility cones — if I enter their cone, I am detected
- As a player, I can steal from NPCs (pickpocket) and containers
- As a player, getting caught stealing turns the NPC hostile
- As a player, I can harvest plants and collect enemy drops as crafting resources
- As a player, I can learn recipes from the world or shops
- As a player, I can craft items at crafting stations if I have the recipe,
  resources, and required crafting skill tier

---

## Epic 8: Content & Polish

### Goal
Integrate assets, audio, and polish systems. Prepare for prototype review.

### Scope
**Includes:** Free asset integration (characters, environments, animations),
ambient audio and combat SFX, background music (3-5 tracks), functional UI
(quest log, inventory screen, stat screen, HUD), save/load system, Steam
achievements (basic), Steam cloud saves, key rebinding, options menu,
performance optimization pass.

**Excludes:** New game systems — polish only.

### Dependencies
All previous epics.

### Deliverable
A complete, playable prototype with coherent assets, audio, and UI. Ready
for personal playtesting and BMAD workflow validation.

### Stories
- As a player, the world uses consistent free assets for characters and environments
- As a player, I hear ambient environmental audio and dynamic combat sounds
- As a player, background music adapts to context (exploration, combat, town)
- As a player, I have a functional HUD (stamina bar, health bar, gold)
- As a player, I can save and load my game
- As a player, I can rebind keys in the options menu
- As a developer, basic Steam achievements trigger at key milestones
- As a developer, the game runs at 60fps on mid-range hardware
