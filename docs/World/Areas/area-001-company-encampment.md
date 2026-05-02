# Area 001 — Company Encampment

> id: area-001
> type: area
> name: Company Encampment
> status: draft
> region: Frontier (remote, sealed by mountain ranges)
> npcs: npc-001, npc-002, npc-003, npc-004, npc-005, npc-006, npc-007, npc-008
> quests: quest-001, quest-002
> events: event-001, event-002, event-003
> factions: faction-001, faction-002, faction-003
> brainstorm-source: `_bmad-output/brainstorming-area-1-company-encampment-2026-04-30.md`

---

## Overview

The Company Encampment is the player's arrival point and prologue zone. It is a remote frontier labor camp run by the Company — grim, barely maintained, and governed by debt bondage. The player arrives deceived and stripped of possessions, and must accumulate power, allies, and agency through any means available before choosing a path out.

This area serves as the game's tutorial, first demo, and prologue. It ends with a player-authored ending event that closes the prologue and opens the next region.

---

## Layout

### Natural Features
- Built along a **riverbank** — water and fishing are Company-controlled resources
- **Company house** sits on elevated ground above the river, physically looming over the worker area
- **Wilderness** surrounds the camp on all other sides — dense, frontier, where workers labor by day

### Structures
- **Company House** — behind the inner fence. Guards' quarters and warden's office. More maintained than the rest.
- **Storage Hut** — company-controlled warehouse. Accessible via warden task (hauling goods).
- **Workshop** — shared-use tool and repair facility, company-controlled.
- **Docks** — company-maintained fishing docks on the river. Off-limits at night officially.

### Fences
- **Outer fence** — low, practical. Not imposing. Message: the wilderness is worse than staying.
- **Inner fence** — separates worker area from company area. The real social divide.

### Camp Center
- Visible from both entrances and the company gate
- **Punishment post** (stocks / pillory) — slightly elevated. Used for punishments and warden announcements.
- Company flags visible at all angles
- Workers do not gather here voluntarily

### Tent Camp Zones (Worker Area)
| Zone | Description |
|------|-------------|
| Player's Zone | Neutral. Worn but not destitute. Card game, tool repair trunk, campfire, low conversation. |
| Wretches' Corner | Poorest workers. Minimal possessions. Social bottom of the camp. |
| Mafia Zone | Workers with leverage or muscle. Better-kept tents. Stash owner's territory. |
| Fight Club Hollow | Hidden by rock face or cave mouth. Not visible from company house. Runs at night. |

### Exits
| Exit | Direction | Status | Notes |
|------|-----------|--------|-------|
| Civilization Road | Back toward towns / Company camps | Gated | Guarded post, blocked until story conditions met |
| Wilderness Path | Into the unknown | Open | Curves behind a hill, disappears from sight. Reads as danger, then as freedom. |

---

## Atmosphere

**By day:** Grim and functional. Workers move in silence. Guards visible. Company flags everywhere. The elevated company house watches over everything. Ruins visible on a distant hillside.

**By night:** The camp breathes. Fewer guards, some asleep. Illegal fishing at the docks. Fight club in the hollow. Across the river — a warm light in the treeline. The independent hunter's fire.

**Tone:** Oppressive but not hopeless. The injustice is immediate and personal. Freedom is visible — literally — across the river.

---

## Wilderness Mystery Signals

| Signal | Time | Location | Meaning |
|--------|------|----------|---------|
| Ruins on hillside | Day | Visible from camp | Ancient civilization — to be designed. Quiet question mark. |
| Light in treeline | Night | Across the river | Independent hunter's camp. Proof freedom exists now. |

---

## NPCs

| ID | Name | Role | Zone |
|----|------|------|------|
| npc-001 | Tent Mate | Logistics informant. Knows where everything is. | Player's Zone |
| npc-002 | Beaten Worker | Social informant. Knows people and secrets. | Recovers in Player's Zone after event-001 |
| npc-003 | Stash Owner | Worker with leverage. Intimidation or snitch connections. | Mafia Zone |
| npc-004 | Independent Hunter | Free operator. Camp is across the river. | Wilderness / river crossing |
| npc-005 | Worker Leader | Hidden rebel leader. Appears as ordinary worker. | Worker area (indeterminate) |
| npc-006 | The Warden | Camp authority. Main antagonist. Issues tasks, enforces order. | Company house / camp center |
| npc-007 | Racket Guard | Extorts newcomers at exits. | Camp exits |
| npc-008 | The False Friend | Befriends player, leads them to robbery. | Neutral zone / camp |

---

## Events

| ID | Name | Trigger | Description |
|----|------|---------|-------------|
| event-001 | Arrival Beating | Scripted on player arrival | Player escorted by guards, forced to watch warden beat a worker (npc-002) in the camp center for underperformance. Worker left unconscious. No one helps. Player escorted to tent. |
| event-002 | Workers' Revolt | Player-triggered — worker path completion | Workers rise after preparation quests complete. Player fights alongside them. Camp becomes a free workers camp. |
| event-003 | Company Crackdown | Player-triggered — company path completion | Player reports the rebel network to the warden. Guards move on leaders. Camp locked down tighter. |

---

## Quests

| ID | Name | Giver | Type | Notes |
|----|------|-------|------|-------|
| quest-001 | The Hidden Stash | npc-002 | Stealth / social | Steal from npc-003. Teaches stealth and social consequence. |
| quest-002 | The Sealed Passage | npc-009 | Exploration / mystery | Scout the quarry passage. Tabled pending ruins/lore design. |

### Warden's Tasks (Non-quest work assignments)
| Task | Type | Hidden Value |
|------|------|-------------|
| Hauling goods to warehouse | Access task | Unlocks storage area, reveals guard positions |
| Delivering notices to work sites | Access task | Forces tour of all three work sites |
| Latrine cleaning | Degrading | None — atmospheric |
| Canteen cleanup | Degrading | None — atmospheric |

---

## Faction Paths

### Company Path
**Seeds:** Reporting rule-breakers, informing on the quarry passage, complying with warden tasks.
**Pivot moment:** Warden offers the player an insider role — spy on the suspected rebel ring using newcomer status as cover.
**Ending trigger:** Player accepts and reports the rebel network → event-003.

### Worker Path
**Seeds:** Helping npc-002, socializing in the neutral zone, completing worker quests, doing their assigned work.
**Entry:** Multiple accomplices (worker network members) across the camp. Whichever the player trusts first becomes their entry point.
**Reveal:** Mundane network quests eventually lead to an offer → introduction to npc-005 (worker leader).
**Preparation quests:** Steal weapons/armor, secure food reserves, recruit able-bodied workers.
**Ending trigger:** npc-005 judges readiness → player chooses when to begin → event-002.

### Independent Path
**Seeds:** Finding npc-004 (independent hunter), avoiding defining quests of either faction, exploring the wilderness.
**Ending:** Player builds enough wilderness foothold to leave. Camp fate unchanged. No ending event triggered.

---

## Economy

### Official (barely survivable)
- Wages from assigned work
- Warden payment for information

### Unofficial (where real money is)
- Gambling — card games in the neutral zone
- Fight club winnings
- Smuggling goods in or out
- Doing other workers' assigned labor for pay
- Selling stolen goods via black market
- Illegal fishing catches

---

## Day/Night Cycle

| Activity | Time | Location | Notes |
|----------|------|----------|-------|
| Illegal fishing | Night | Docks | Guards absent. Black market outlet. |
| Fight club | Night | Fight Club Hollow | Hidden from company sightlines. |
| Hunter's fire visible | Night | Across river | First contact incentive for independent path. |
| Black market | Night | Tent area (location TBD) | Different inventory than official shop. |
| Theft / sneaking | Night | Camp-wide | Reduced guard coverage. Some guards asleep. |

---

## Exit Conditions

No hard resource gate. Endings become available when player's actions and completed quests push a path far enough:
- **Company ending** → insider offer accepted and acted on
- **Worker ending** → preparation quests complete, player triggers revolt
- **Independent ending** → hunter questline far enough, player exits through wilderness

---

## Work Sites (Sub-areas)

| ID | Name | Flavor | Key Hook |
|----|------|--------|----------|
| area-002 | Stone Quarry | Hardest work, desperate workers | Sealed passage to ruins (quest-002) |
| area-003 | Wood Camp | Physical labor, forest edge | TBD |
| area-004 | Hunting Grounds | Tracker/ranger flavor | Lead hunter (npc-010) — independent-minded, speaks to player as equal |

---

## Open Threads

- **Quarry passage interior** — what's inside, civilization lore connection (needs separate ruins/lore session)
- **Ruins design** — hillside layout, what's inside, the vanished civilization's identity
- **Black market operator** — who runs it, what they sell, faction alignment
- **Camp official shop/trader** — who runs it, company employee or worker?
- **Fight club design** — rules, opponent roster, reward structure, reputation effects
- **NPC roster completion** — names for all stub NPCs, full dialogue seeds
- **Wood camp quest details** — beyond basic flavor
- **Work site guard/manager NPCs** — area-002 and area-003 managers unnamed
