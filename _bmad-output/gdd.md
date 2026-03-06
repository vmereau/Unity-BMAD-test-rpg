---
stepsCompleted: [1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14]
inputDocuments: []
documentCounts:
  briefs: 0
  research: 0
  brainstorming: 0
  projectDocs: 0
workflowType: 'gdd'
lastStep: 2
project_name: 'Unity-BMAD-test-rpg'
user_name: 'Valentin'
date: '2026-02-28'
game_type: 'rpg'
game_name: 'Echoes of the Fallen'
---

# Echoes of the Fallen - Game Design Document

**Author:** Valentin
**Game Type:** RPG
**Target Platform(s):** PC (Windows) via Steam

---

## Executive Summary

### Game Name

Echoes of the Fallen

### Core Concept

Echoes of the Fallen is a third-person action RPG prototype built in Unity,
designed to explore and validate game development workflows using the BMAD
methodology. The player controls a single character navigating a world filled
with quests, enemies, and progression systems.

The core experience centers on third-person exploration, real-time combat, and
quest-driven progression — serving as a hands-on learning platform for Unity
development patterns within a structured BMAD AI-assisted pipeline.

### Game Type

**Type:** RPG
**Framework:** This GDD uses the RPG template with type-specific sections for
character progression, stats, inventory, quest systems, and combat mechanics.

### Target Audience

Adult hardcore RPG players (18+) who know genre conventions, expect system depth,
and play in long sessions (1-3 hours). Mature content expected.

### Unique Selling Points (USPs)

1. **Gothic-Inspired Atmosphere** — A dark, immersive world with environmental
   storytelling, NPC routines, and a tone that doesn't soften its edges.
2. **Brutal, Unforgiving Combat** — Combat that punishes mistakes and rewards
   mastery. No regenerating health, no easy mode. Death is meaningful.
3. **Player Agency with Real Consequences** — Choices permanently alter the
   world. NPCs remember, factions react, moral decisions have lasting ripple effects.
4. **Learning Point System** — No traditional XP leveling. Players spend
   learning points earned through exploration, quests, and combat to train with
   NPCs or study tomes.
5. **Completionist Power Fantasy** — Players who explore every corner, finish
   every quest, and kill every enemy become genuinely overpowered.
6. **Permanent World State + Story Acts** — Monsters do not respawn. Story Acts
   advance the world — new threats emerge, questlines open, and the world evolves
   with narrative progression.

### Competitive Positioning

Where most modern RPGs scale difficulty or respawn enemies indefinitely, Echoes
of the Fallen commits to a permanent world state. Power is earned, not scaled
away — targeting hardcore RPG players who feel modern games have abandoned
meaningful consequence and earned progression.

---

## Goals and Context

### Project Goals

1. **Creative:** Build a Gothic-inspired RPG that captures the atmosphere,
   weight, and unforgiving design philosophy of the Gothic series.
2. **Personal:** Learn Unity and the BMAD development workflow through a
   meaningful, passion-driven prototype project.

### Background and Rationale

Inspired by the Gothic series (Gothic 1 & 2 in particular), Echoes of the
Fallen is a personal attempt to recreate the design philosophy that made those
games special — a living, breathing world with real stakes, no hand-holding,
and a sense that the player earned every ounce of power they have.

The project is a prototype: a vehicle for learning Unity deeply while staying
creatively motivated through a genre and style the developer genuinely loves.

---

## Core Gameplay

### Game Pillars

1. **Immersion** — Every system, visual, and interaction reinforces the player's
   sense of being inside a living, believable world. No UI clutter, no
   hand-holding, no systems that break the fiction.

2. **Earned Power** — Strength is never given, always taken. Learning points are
   spent deliberately through trainers and tomes. Players who explore fully,
   complete all quests, and kill every enemy become genuinely overpowered —
   and deserve to be.

3. **Consequence** — Actions have lasting effects. NPCs who die stay dead.
   Quests that fail stay failed. Story Acts permanently reshape the world.
   Every decision carries weight.

4. **Living World** — The world exists independently of the player. NPCs have
   routines, factions have agendas, and the world changes through Story Acts
   rather than artificial respawns.

**Pillar Prioritization:** When pillars conflict, prioritize in this order:
Consequence → Earned Power → Living World → Immersion

### Core Gameplay Loop

The player enters the world, explores regions, engages enemies in real-time
combat, and takes on quests from NPCs. Victories — cleared areas, completed
quests, discovered secrets — award learning points. Those points are spent
with trainers or tomes to permanently improve the character. Stronger
capabilities open new areas, harder enemies, and deeper quest chains.
Story Acts punctuate the loop, reshaping the world and raising the stakes.

**Loop Diagram:**

```
Explore World
     ↓
Encounter (Combat / Quest / Secret)
     ↓
Overcome Challenge
     ↓
Earn Learning Points
     ↓
Spend on Skills (Trainers / Tomes)
     ↓
World State Updated (permanent kills, quest outcomes, Act triggers)
     ↓
Explore Deeper → (repeat)
```

**Loop Timing:** Variable — a single combat encounter is minutes; a full
quest chain or dungeon clearance is 30-90 minutes.

**Loop Variation:** Each iteration feels different because the world is
permanently changed. Cleared areas stay cleared. Dead NPCs stay dead.
Completed quests close chapters. The player is always moving forward,
never treading the same ground twice.

### Win/Loss Conditions

#### Victory Conditions

- **Primary:** Complete the main story by defeating the final boss.
- **Secondary (personal):** Full world completion — all quests done, all
  enemies killed, all secrets found, maximum character power achieved.

#### Failure Conditions

- **Player Death:** Reload last manual or autosave. No permadeath.
- **Quest Failure:** NPCs who die stay dead. Failed quests are permanently
  closed. Associated rewards and story branches are lost.
- **No global fail state** — the game cannot be put into an unwinnable
  condition through combat death alone.

#### Failure Recovery

Death returns the player to their last save with no additional penalty beyond
lost progress since that save. Quest failures are permanent and intended —
they are a feature of Consequence, not a punishment to be avoided via reload.
Players must live with the outcomes of their actions.

---

## Game Mechanics

### Primary Mechanics

#### 1. Exploration
Players advance through the world on foot via third-person movement. The world
is permanent — cleared areas stay cleared, killed enemies do not respawn.
Secrets, hidden paths, and undiscovered locations reward thorough exploration
with learning points and crafting resources.

- **Pillar:** Living World, Earned Power
- **Frequency:** Constant
- **Progression:** New areas unlock as character power increases

#### 2. Combat (Real-Time, Gothic-Style)
Third-person real-time combat with directional attacks driven by mouse movement.
Combat is weighty and deliberate — not button-mashing.

- **Stamina System:** All offensive and defensive actions consume stamina.
  At zero stamina the player cannot attack, dodge, or block. Stamina recovers
  when not performing actions.
- **Combo Attacks:** Attacks are chained in a timed combo sequence via LMB.
  The first press triggers the opening strike. A timing window opens during
  the animation — pressing LMB within that window continues the chain to a
  second strike. A second timing window during the follow-up animation allows
  a finishing blow. Missing a window resets the combo. All combo hits consume
  stamina; at zero stamina, the chain cannot continue.
- **Manual Blocking:** Hold a dedicated button to raise a block. Blocking
  costs stamina on each hit absorbed.
- **Perfect Block:** A tight timing window at the moment of impact negates
  stamina cost entirely and briefly staggers the attacker.
- **Dodge:** Directional dodge roll costs stamina. Invincibility frames
  reward timing over button-mashing.
- **Pillar:** Earned Power, Consequence
- **Skill tested:** Timing, stamina management, positioning

#### 3. Stealth
Visibility cone-based detection. NPCs have forward-facing vision cones;
crouching reduces the player's detection radius. Sound is not modelled
in this prototype.

- **Stealing:** Players can steal from NPC pickpockets and containers
  (chests, shelves, crates).
- **Detection:** Getting caught stealing turns the NPC hostile immediately.
  No faction reputation system in this prototype.
- **Pillar:** Immersion, Consequence

#### 4. Crafting
Players craft equipment (weapons, armor) and consumables (potions, food)
at crafting stations using gathered resources.

- **Resources:** Purchased from shops, harvested from world plants,
  looted from enemy drops.
- **Requirements:** Each recipe requires (a) a minimum crafting skill tier
  unlocked via learning points, and (b) the specific recipe learned
  (found in the world or purchased).
- **No recipe = no craft**, regardless of resources available.
- **Pillar:** Earned Power, Living World

#### 5. Questing
NPCs offer quests through dialogue. Quest outcomes are permanent — NPCs
who die during or before a quest stay dead; failed quests do not reset.
Shops are accessed through NPC dialogue.

- **Pillar:** Consequence, Living World

#### 6. Progression (Learning Point System)
Defeating enemies, completing quests, discovering secrets, and exploring
new areas awards learning points. Points are spent with trainers (NPCs)
or by reading tomes found in the world to permanently improve stats,
unlock abilities, or raise crafting skill tiers.

- **Pillar:** Earned Power
- **No automatic leveling** — all progression is deliberate player choice.

### Mechanic Interactions

- **Combat + Stamina:** Forces players to fight tactically, not recklessly.
  Running out of stamina mid-fight is a death sentence on harder enemies.
- **Stealth + Combat:** Players can initiate combat from stealth for a
  damage advantage, or avoid combat entirely to preserve resources.
- **Crafting + Exploration:** Harvesting resources and finding recipes
  rewards thorough world exploration.
- **Progression + All Systems:** Learning points flow from every activity —
  combat, questing, exploration — ensuring all playstyles contribute to
  character growth.

### Mechanic Progression

| Mechanic | How It Evolves |
|---|---|
| Combat | New attacks, higher stamina pool, stat improvements via learning points |
| Stealth | Reduced detection radius, faster steal speed via learning points |
| Crafting | Higher tier recipes unlocked via crafting skill tiers (learning points) |
| Exploration | Access to new zones gated by character power level |

### Controls and Input

#### Control Scheme (PC — Keyboard & Mouse)

| Action | Input |
|---|---|
| Move | WASD |
| Camera / Aim direction | Mouse look |
| Attack (combo) | Left Mouse Button (tap within window to chain) |
| Block | Right Mouse Button (hold) |
| Dodge | Space + WASD direction |
| Interact / Talk / Loot | E |
| Steal (pickpocket / container) | F (when near NPC or container) |
| Toggle Crouch (Stealth) | C |
| Open Inventory | I |
| Open Quest Log | J |
| Open Map | M |
| Open Crafting | K (at crafting station) |
| Quick consumable | Q |
| Pause / Menu | Escape |

#### Input Feel

Combat inputs should feel weighty and committed — no instant cancels.
Attacks have wind-up and recovery frames. The perfect block window is tight
enough to reward skill without being frame-perfect. Combo attacks should feel
weighty and committed — each hit in the chain has a clear animation window.
Timing feedback (visual or audio) communicates when the next input window is
open. Missing the window resets the combo naturally without punishment beyond
lost momentum.

#### Accessibility Controls

Key rebinding via options menu. No additional accessibility features planned
for this prototype.

---

## RPG Specific Design

### Character System

**Base Stats (Gothic-Style):**

| Stat | Effect |
|---|---|
| Strength | Melee damage, carrying interactions |
| Dexterity | Attack speed, stealth effectiveness, steal success |
| Endurance | Max health, stamina pool |
| Mana | Spellcasting pool (spells, teleportation) |

**Class System:** None — fully classless. Build identity emerges organically
from how the player spends learning points. A player who invests in Strength
and combat skills becomes a warrior; one who invests in Dexterity and stealth
becomes a thief. No gates, no locked paths.

**Progression Flow:**

```
Actions (combat wins, quest completions, exploration)
     ↓
XP gained
     ↓
Level Up (XP threshold met)
     ↓
Learning Points awarded
     ↓
Spent at Trainers (learning points + gold) or Tomes (learning points)
     ↓
Stats increase / Skills unlocked / Crafting tiers raised
```

### Inventory & Equipment

**Item Types:**
- **Weapons:** Melee weapons (swords, axes, hammers, etc.)
- **Armor:** Full sets (helmet, chest, legs, boots, gloves)
- **Accessories:** Two ring slots + one necklace slot

**No rarity system.** Items are defined entirely by their stats. A sword found
in a dungeon is better or worse based on its numbers, not its color or label.

**Inventory:** Unlimited slots, no weight system. Players carry what they find.

### Quest System

**Quest Types:**
- Main story quests — drive Act progression and world state changes
- Side quests — optional, permanently failable, often with multiple outcomes

**Quest Log:**
- Stores past dialogue for reference
- Shows quest title and description
- No map markers, no objective arrows, no waypoints
- Players navigate via NPC dialogue, exploration, and memory

**Quest Outcomes:** Quests can resolve in multiple ways depending on player
choices and actions. NPCs who die stay dead; quests tied to dead NPCs close
permanently.

**Quest Rewards:**
- XP (always)
- Gold (most quests)
- Items (select quests)
- World updates — area unlocks, new NPCs, Act triggers (story quests)
- Learning points (rare, significant quests only)

### World & Exploration

**Structure:** Open world divided into distinct regions. Each region has its
own atmosphere, enemy types, factions, and points of interest.

**Zone Organization:** By region (geographic/thematic), not by level gates.
Danger is communicated through world design and NPC warnings — players who
wander into a high-danger region early do so at their own risk.

**Fast Travel:** No free fast travel.
- **Teleportation Stones:** Consumable items found/purchased, teleport to
  a set destination.
- **Teleportation Spells:** Learned via learning points, require Mana.
  Reusable but resource-gated.

**Points of Interest:**
- Dungeons, caves, ruins, bandit camps, monster lairs, hidden shrines,
  NPC settlements, crafting stations, trainer locations.
- All defined by world-building and quest design — no procedural generation.

### NPC & Dialogue

**Dialogue System:** Gothic-style topic selection. Players choose from a list
of conversation topics rather than branching dialogue trees. Topics unlock
based on quest state, world events, and prior conversations.

**Companions:** None. Solo player only.

**Merchants:** Dedicated shop NPCs with fixed inventories. Shops may restock
between Story Acts. No bartering — fixed gold prices.

**NPC Schedules:** NPCs can have day/night routines (sleep, work, patrol,
eat). Schedules affect NPC availability — a trainer asleep at night cannot
teach. A merchant at the tavern after hours won't sell.

### Combat System

*Defined in Game Mechanics — real-time Gothic-style combat with directional
attacks, stamina system, manual blocking, perfect block window, and dodge
mechanics.*

---

## Progression and Balance

### Player Progression

Echoes of the Fallen combines four progression types working in concert:

| Type | Implementation |
|---|---|
| **Power** | XP → Levels → Learning Points → Stats & Skills |
| **Skill** | Combat mastery, stamina management, perfect blocks |
| **Narrative** | Story Acts unlock new world states, quests, and challenges |
| **Content** | New regions, dungeons, and areas unlock as character grows |

**Progression Flow:**

```
XP earned (combat, quests, exploration)
     ↓
Level Up → Learning Points awarded
     ↓
Trainers (LP + Gold) or Tomes (LP) → Stats raised / Skills unlocked
     ↓
Greater capability → Deeper regions accessible → Harder challenges → More XP
```

**Pacing:** Progress feels meaningful within each session. A single dungeon
clear or quest chain should yield at least one level-up or a significant
crafting haul. Full character mastery (completionist path) is the end-game,
not a mid-game milestone.

**Completionist Ceiling:** Players who do everything — all quests, all kills,
all secrets — become genuinely overpowered. This is intentional and rewarded.

### Difficulty Curve

**Pattern:** Regional exponential with Act-based spikes.

- **Starting region:** Forgiving enough to learn mechanics without being trivial.
  Early enemies teach stamina management and combo timing.
- **Further regions:** Danger scales with distance from the starting area.
  No level gates — danger is communicated through world design and NPC warnings.
- **Story Acts:** Each Act transition introduces tougher enemy variants,
  new spawns in previously safe regions, and escalating quest stakes.
- **Deep Exploration Spikes:** Dungeons, caves, and hidden areas contain
  enemies above the local region's baseline — high risk, high reward.
  These are optional but offer the best loot and learning point yields.

**Philosophy:** Challenge is skill-based, not stat-gated. A player with
lower stats who masters stamina management, directional attacks, and perfect
blocks can defeat enemies that would crush a careless overleveled character.
Skill expression always provides a path forward.

**If Stuck:** No hints, no UI assistance. Players are expected to explore
elsewhere, return later with more skills, or improve their mechanical play.
Getting beaten by an enemy is information — learn its patterns, manage stamina,
use the environment.

**Difficulty Options:** None. One experience for all players.

### Economy and Resources

**Gold:**

| Source | Notes |
|---|---|
| Enemy loot | Dropped on death, varies by enemy type |
| Quest rewards | Guaranteed for most quests |
| Selling items | Any item can be sold to merchants |
| Looting containers | Chests, crates, shelves in the world |

| Sink | Notes |
|---|---|
| Trainers | Primary gold sink — skills cost LP + gold |
| Shops | Equipment, consumables, crafting recipes, teleportation stones |
| Gated content | Bribes, toll payments, access fees to areas or services |

**Economy Design:** Gold is always in motion. Trainers are the primary drain;
players must balance skill investment against equipment purchases. Gated
content adds meaningful spending decisions.

**Crafting Resources:**

Resources (plants, enemy drops, shop purchases) are deliberately abundant.
Players should never feel unable to craft due to scarcity — the limiting
factors are the required recipe and crafting skill tier, not material
availability. This encourages experimentation and rewards explorers.

---

## Level Design Framework

### Structure Type

Open world divided into distinct regions. No level select, no loading screens
between regions (within Acts). The world is one continuous space that expands
and changes with each Story Act. Players navigate freely — there is no
prescribed path, only increasing danger as they move further from the start.

### Area Types

| Area Type | Description |
|---|---|
| **Starting Town** | The player's anchor point. Trainers, shops, quest givers, crafting stations. Safe zone. |
| **Wilderness Regions** | Large outdoor zones connecting points of interest. Enemies patrol, resources to harvest. |
| **Dungeons** | Underground or enclosed spaces. Higher enemy density, better loot, boss encounters. |
| **NPC Camps** | Smaller settlements outside the main town. Faction presence, unique traders, local quests. |
| **Ruins** | Abandoned structures. Environmental storytelling, hidden secrets, powerful loot. |
| **Boss Arenas** | Dedicated spaces for story or quest-driven boss encounters. Handcrafted for the fight. |

### Tutorial Integration

No explicit tutorial. The starting area is designed to guide players organically
through core mechanics via natural world encounter flow:

- Early enemies introduce stamina-limited combat
- A nearby cave teaches dungeon navigation and looting
- A local quest introduces the quest log and NPC dialogue
- A trainer introduces the learning point system

Players are never told what to do — they discover through play.

### Boss & Elite Encounters

**Dedicated Encounters:** Story and quest bosses are staged in handcrafted
arenas with deliberate pacing. These mark Act transitions or major quest
resolutions.

**World Elites:** Powerful enemies exist freely in the world, discoverable
through exploration. No special staging — they are part of the environment.
Defeating them yields exceptional loot and XP. Players stumble upon them;
the world does not warn them.

### Area Progression & Revisiting

Players can travel anywhere at any time. There are no hard locks on regions.

**Act Changes** actively incite re-exploration:
- New enemy types spawn in previously cleared regions
- New NPCs appear with fresh quests
- Previously inaccessible areas or secrets become reachable
- World lore deepens — NPCs react to story events

### Level Design Principles

Simple foundational principles for the prototype, to be refined through
playtesting:

1. **Danger is communicated, not blocked** — Players can go anywhere, but
   the world signals risk through NPC dialogue, environmental design, and
   enemy presence. No invisible walls, no level gates.
2. **Every area has a reason to exist** — Regions, dungeons, and camps
   contain at least one quest hook, trainer, unique enemy, or secret.
3. **Reward exploration visibly** — Hidden areas and caves off the beaten
   path always contain something worthwhile.
4. **Shortcuts matter** — Dungeons include at least one shortcut or loop
   back to the entrance once partially cleared.

*These principles will be expanded and refined through playtesting.*

---

## Art and Audio Direction

### Art Style

**Style:** Realistic 3D with a dark medieval fantasy aesthetic. Prioritizes
atmosphere and environmental believability over technical spectacle. The world
should feel grounded and lived-in, not polished or heroic.

**Visual References:** Gothic 1 & 2 (primary). Modern equivalents for
technical quality bar: Kingdom Come: Deliverance (environmental realism),
A Plague Tale: Innocence (dark atmosphere and lighting).

**Color Palette:** Muted and earthy — browns, mossy greens, stone grays,
dark blues at night. Warm firelight and torchlight provide contrast in
interior and nighttime scenes. No saturated or vibrant colors.

**Lighting:** Dynamic day/night cycle with dramatic directional lighting.
Sunlight filters through forest canopies. Interiors are lit by fire and
torches. Weather effects (overcast, rain, fog) shift the mood of outdoor
regions.

**Camera:** Third-person, over-the-shoulder. Close enough to feel the
world, far enough to read enemy positions in combat.

**Character Design:** Realistic proportions. NPCs look like they belong to
their role — a farmer looks weathered, a guard looks functional, not glamorous.

**Aesthetic Goals (Art):**
- No floating icons, no glowing quest items, no obvious gamification in world space
- Environmental details communicate history and ongoing life
- Act changes are visible — new enemy camps, opened gates, scorched areas

### Audio and Music

**Music Style:** Dark ambient orchestral with subtle electronic undertones —
inspired by Kai Rosenkranz's Gothic soundtrack. Music is atmospheric and
understated, never competing with world ambient sounds.

**Dynamic Music:** Adapts to context:
- Exploration: low, ambient, region-specific themes
- Combat: tension builds, rhythm intensifies
- Night: sparse, quieter, unsettling undertones
- Towns: warmer, muted social ambience

**Sound Design:** Weighty and impactful. Every sword swing has mass, every
block has a crunch, every perfect block has a distinct sharp ring. Stamina
depletion is audible — labored breathing signals exhaustion. Environmental
audio is rich: wind, water, fire, distant creature sounds.

**Voice/Dialogue:** No voice acting for this prototype. Text-based dialogue
with ambient vocal reactions (grunts, acknowledgment sounds) for interactions.

**Aesthetic Goals (Audio):**
- Ambient world sounds carry more weight than music
- Combat audio communicates stakes — hits hurt, blocks matter, stamina
  exhaustion is heard before it is felt

---

## Technical Specifications

### Performance Requirements

**Priority:** Performance over visual fidelity. Stable frame rate is
non-negotiable; graphical quality is secondary and will be tuned to maintain
targets on mid-range hardware.

| Target | Value |
|---|---|
| Frame Rate | 60fps locked (target), minimum 30fps floor |
| Resolution | 1080p native, windowed/borderless supported |
| Load Times | Under 10 seconds for initial load; seamless within regions |
| Visual Priority | Performance > fidelity — reduce draw distance, shadow quality, post-processing before dropping frames |

**Minimum Hardware Target (PC):**
- GPU: Mid-range from ~5 years ago (e.g., GTX 1060 / RX 580 class)
- RAM: 8GB
- Storage: Under 5GB for prototype build

### Platform-Specific Details

**Platform:** PC (Windows) via Steam — exclusive, no ports planned.

| Feature | Planned |
|---|---|
| Steam Achievements | Yes — basic milestone achievements |
| Steam Cloud Saves | Yes |
| Steam Overlay | Yes (default) |
| Mod Support | No — out of scope for prototype |
| Key Rebinding | Yes — via options menu |
| Fullscreen / Windowed | Both supported |

**Control Scheme:** Keyboard & Mouse only. No gamepad support planned.

### Asset Requirements

**Asset Strategy:** Free and open-licensed assets for the prototype phase.
Sources: Unity Asset Store (free tier), itch.io free assets, Mixamo
(animations), Sketchfab (CC-licensed 3D models).

**Prototype Scope — Small:**

| Category | Prototype Target |
|---|---|
| Regions | 1 starting region + 1 dungeon |
| NPCs | ~10-15 unique NPCs |
| Enemy Types | 3-5 enemy types |
| Quests | 1 main quest chain (3-5 quests) + 2-3 side quests |
| Items | ~20-30 items (weapons, armor, consumables) |
| Music Tracks | 3-5 ambient tracks |
| SFX | Core combat, UI, and ambient sounds |

**Art Assets:**
- 3D character models with basic animations (idle, walk, run, attack, death)
- Environment assets: terrain, foliage, rocks, ruins, dungeon tiles
- UI: minimal, functional — quest log, inventory, stat screen

**Audio Assets:**
- Free/CC licensed ambient and orchestral tracks
- Core SFX: combat hits, blocks, footsteps, UI feedback

### Technical Constraints

- Engine: Unity (established by project setup)
- Prototype will not be production-ready — polish deferred to post-validation
- Free asset quality may vary; visual consistency is aspirational, not required
- No localization for prototype (English only)

---

## Development Epics

### Epic Overview

| # | Epic Name | Scope | Dependencies | Est. Stories |
|---|---|---|---|---|
| 1 | Foundation & Movement | Unity setup, third-person controller, camera | None | 4-6 |
| 2 | Combat System | Stamina, directional attacks, block, dodge | Epic 1 | 6-8 |
| 3 | Progression & Stats | XP, leveling, learning points, stats, trainers | Epic 2 | 5-7 |
| 4 | World & Exploration | Starting region, dungeon, day/night, NPC schedules | Epic 1 | 6-8 |
| 5 | Quest & Dialogue | Quest system, topic dialogue, quest log | Epic 4 | 5-7 |
| 6 | Inventory & Economy | Inventory, equipment, shops, gold, looting | Epic 3, 5 | 5-7 |
| 7 | Crafting & Stealth | Crafting stations, recipes, skill tiers, stealth, stealing | Epic 6 | 5-7 |
| 8 | Content & Polish | Assets, audio, UI polish, save system, Steam integration | All | 6-8 |

### Recommended Sequence

1. **Epic 1** — Prove the foundation before anything else
2. **Epic 2** — Combat is the core gameplay loop; validate it early
3. **Epics 3 & 4** — Can run in parallel; progression and world are independent
4. **Epic 5** — Quests require the world to exist
5. **Epic 6** — Inventory and economy require quests and progression
6. **Epic 7** — Crafting and stealth are layered systems on top of the economy
7. **Epic 8** — Polish last, once all systems are validated

### Vertical Slice

**First playable milestone (Epics 1-2):** Player can move through a basic
scene, engage enemies with stamina-based directional combat, block, perfect
block, and dodge. Combat feel is validated before any content is built around it.

---

## Success Metrics

### Technical Metrics

| Metric | Target | Measurement Method |
|---|---|---|
| Frame Rate | Stable 60fps on GTX 1060 / RX 580 class GPU | Manual testing, Unity profiler |
| Crash Rate | Zero crashes during a full play session | Manual playtesting |
| Load Time | Under 10 seconds initial load | Stopwatch / Unity profiler |
| Build Size | Under 5GB | Build output |
| Memory Usage | No out-of-memory errors during session | Unity profiler |

### Gameplay Metrics

Since this is a personal prototype, gameplay metrics are self-assessed through
playtesting rather than analytics tooling.

| Metric | Target | Measurement Method |
|---|---|---|
| Combat Feel | Deaths feel like player error, not unfairness | Personal playtesting notes |
| Progression Impact | Noticeable power increase after spending LP on stats or skills | Self-assessment after each spend |
| World Reactivity | NPCs respond with new dialogue after player actions | Playtesting checklist |
| Exploration Reward | Hidden areas and secrets yield meaningful loot or LP | Content review during level design |
| Perfect Block | Used naturally without consciously thinking about it | Playtesting observation |

*Specific combat feel targets will be refined through iterative playtesting
during and after Epic 2.*

### Qualitative Success Criteria

1. **Earned Power feels real** — Spending LP on stats or skills produces a
   noticeable, satisfying difference in capability.
2. **Challenges overcome through skill** — Hard encounters beaten on a later
   attempt through better stamina management and timing, not just grinding.
3. **The world reacts** — NPCs acknowledge completed quests, cleared areas,
   and world events through dialogue.
4. **Exploration is rewarding** — Hidden areas reliably yield something worth
   finding. Thoroughness pays off tangibly.
5. **BMAD workflow validated** — The AI-assisted development process produced
   a functional, coherent prototype.

### Metric Review Cadence

- After Epic 2: Combat feel assessment
- After Epic 3: Progression satisfaction check
- After Epic 4: World reactivity check
- After Epic 8: Full qualitative review

---

## Out of Scope

The following features are explicitly not included in the prototype (v1.0):

- **Multiplayer** — Solo experience only
- **Console / Mobile ports** — PC via Steam exclusively
- **Full voice acting** — Text dialogue with ambient vocal reactions only
- **Mod support** — No modding tools or hooks planned
- **Companion system** — Solo player throughout
- **Sound-based stealth** — Visibility cones only; audio detection deferred
- **Additional Story Acts** — Prototype covers one region and one Act

### Deferred to Post-Launch

- **Faction Reputation System** — Planned for a later stage. Factions exist
  in the world but player reputation/standing with them is not tracked in v1.0.

---

## Assumptions and Dependencies

### Key Assumptions

- **Solo developer** — All development, design, and testing done by one person
- **Unity LTS stability** — Project built on Unity LTS; no breaking changes assumed
- **Free asset licensing** — All sourced assets are compatible with personal
  and potential commercial use (CC0, Unity Asset Store EULA)
- **No hard deadline** — Personal prototype with no external delivery pressure
- **Steam approval** — Standard Steam Direct submission process assumed

### External Dependencies

- **Unity Engine** — Core development platform
- **Unity Asset Store** — Free asset sourcing (characters, environments, animations)
- **Mixamo** — Free character animations
- **Steam SDK (Steamworks)** — Achievements and cloud saves integration
- **Free audio sources** — itch.io, freesound.org, or similar CC-licensed music and SFX

### Risk Factors

- Free asset visual inconsistency may affect prototype cohesion
- Solo developer scope creep — prototype boundaries must be respected
- Unity API changes between LTS versions may require migration work

---

## Document Information

**Document:** Echoes of the Fallen - Game Design Document
**Version:** 1.0
**Created:** 2026-02-28
**Author:** Valentin
**Status:** Complete

### Change Log

| Version | Date | Changes |
|---|---|---|
| 1.0 | 2026-02-28 | Initial GDD complete |
