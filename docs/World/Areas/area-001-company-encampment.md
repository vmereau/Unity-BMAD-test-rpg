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

> **In Unity (2026-11-07):** graybox in `StartingTown.unity`; zone map in `Assets/_Game/Scenes/CLAUDE.md`. The 1000 m valley terrain, river, lake, roads, forest and the camp's palisade ring are in, along with the quarry cliffs, the sealed Ruins 2 ring, the cleft blocker and the camp-side sites: fort wall and tower, docks, bridge rails, the Fight Pit and the hunter's fire. The remote sites are grayboxed too: the ridge ruins, quarry and Ruins 2 dressing, wood camp, hunting camp and tutorial cave, with the spiders at the cave and the bandit patrol near (−110, 30). Art replaces these one root at a time. The camp interior is grayboxed too: a tall inner palisade with one guarded gate, work-gang tents, latrine, the Timberworks by the wood opening, with Edric's forge and the Company store, the Stoneworks by the quarry opening, and a dock warehouse built into the north palisade. The dock compound has its guard post, tally shed, lockup, smokehouse and net racks. The lake side stands open behind a broken shore rail, with the sunken Fight Pit and the gang house and its dock. The Company house stays on the south outcrop, with the garrison quarter under its ramp.

> **Layout planning (2026-11-04):** [zone planner](https://claude.ai/artifact/FLxhSZ4NDN1EtunT4bukyv) (source in `area-001/planner.html`). The approved valley geography and camp interior are in `area-001/layout.json`.
>
> **Art decisions:** the Company house becomes an old stone fort with a tower on the outcrop, repaired with timber. Its walls now follow the whole plateau rim: stone facing the camp and the ramp, timber palisade on the back. The interior is still to be designed. The quarry passage stays a hidden crack that a worker sealed with rubble and planks, not a visible door. Reference images: `Assets/_Game/Art/Artworks/Starting area/`, with the camp under `Camp/` and the docks under `Camp/Docks/`. The new north-west arrival gate replaces the Civilization gate. A timber palisade rings the camp, with an opening wherever a road crosses, except on the lake side, which stands open. The Company emblem is still open.

The Company Encampment is the player's arrival point and prologue zone. It is a remote frontier labor camp run by the Company — grim, barely maintained, and governed by debt bondage. The player arrives deceived and stripped of possessions, and must accumulate power, allies, and agency through any means available before choosing a path out.

This area serves as the game's tutorial, first demo, and prologue. It ends with a player-authored ending event that closes the prologue and opens the next region.

---

## Layout

### Natural Features
- Built along a **riverbank** — water and fishing are Company-controlled resources
- **Company house** sits on elevated ground above the river, physically looming over the worker area
- **Wilderness** surrounds the camp on all other sides — dense, frontier, where workers labor by day

### Structures
- **Company House** — the fort on the outcrop. Officers' quarters and warden's office. More maintained than the rest. Voss stays here, out of the player's reach early on.
- **Storage Hut** — company-controlled warehouse, built into the north palisade above the docks. Its door faces the camp and its river door leads to the deck. Accessible via warden task (hauling goods).
- **Stoneworks** — the yard inside the quarry opening, on the Company side of the inner gate. It is the only part of that side workers cross every day. Stone comes in through the opening, is weighed and cut here, then goes up the hoists to the fort repairs or on through the inner gate to the warehouse.
  - **Stone mason's lodge** — half-finished stone building made from quarry offcuts, with an open cutting bay. It replaces the old shared workshop.
  - **Masons' row** — five huts against the inner palisade for skilled masons. Better than tents, worse than guards. They sleep behind the gate at night, so they can carry things the tent workers can't.
  - **Tool store** — tools are counted out through a hatch and back in.
  - **Tally post** — guard hut, tally board and wash trough beside the opening. A natural spot for the racket guard's exit tax.
  - **Sledge track and weighing beam** — ruts lead from the quarry road round the lodge to the foot crane. The beam shows how stone is counted.
  - **Fort hoists** — one timber crane at the foot of the Company cliff and one on the outcrop, roped to the rim. Scenery for now, with space kept for a later route up.
  - **Ox pen** — against the south-east wall. The oxen have a better roof than the workers.
  - **Rubble tip** — waste stone outside the east wall, north of the quarry road.
- **Docks** — company-maintained fishing docks on the river. Off-limits at night officially. They stand inside the Company compound, so they are reached through the warehouse, the compound gate on the delivery road, or the bridge path's dock gate. Both gates stand open in the blockout and will be restricted.
- **Company compound** — the north bank, closed on the west by a fence and on the north by the river. A staging yard with a cart shed, smokehouse, net racks and a gutting table stands by the delivery road.
- **Dock guard post** — a small enterable guard house in the palisade corner by the compound gate, with a lockup cage, a tally shed where goods are counted, and an alarm bell. It watches the gate and the delivery road, and with fishing moved away, workers have no reason to be at the docks at night.
- **Company Road** — the one clean surface in the camp: 5 m of cobbles from the warehouse through the tents and the inner gate to the fort's foot entrance, past the mud the workers walk in.
- **Timberworks** — the yard inside the wood opening, on the Company side of the inner gate, doing for wood what the Stoneworks does for stone. A lane runs from the wood road to the fort road.
  - **Timber yard** — log stacks, handcarts, a tally board, the tool shelter and a roofed sawpit by the opening.
  - **Seasoning shed** — a long open-sided roof over plank stacks. Wood dries here before use.
  - **Carpenters' shop** — a room and an open bay on the lane, turning out palisade stakes, fort beams and cart wheels. The cooper's lean-to at its east end makes barrels for the smokehouse.
  - **Charcoal clamp** — an earth mound behind the shed, burning offcuts into fuel for the forge.
  - **Forge** — Edric's bigger forge on the lane, stone-backed and open to it, with a small room for him. It replaces the open-air anvil in the yard.
- **Company store** — the Storekeeper's building at the lane's east end, by the commute. He still works outside it for now.
- **Garrison quarter** — the ground under the fort ramp, south of the Timberworks. One path leaves the fort road and runs through the drill yard, which serves as the quarter's square. It is built to guard the ramp gate. Its armoury and grain store are later theft targets for the worker path; the docks and warehouse hold the early ones, and the fort comes last.
  - **Barracks** — rank-and-file guards, along the palisade. The stone end nearest the ramp gate is the locked armoury.
  - **Company staff house** — shared lodging for lower employees: the Debt Manager, the steward under Voss and other low-ranking staff. The false ledgers are in its ledger room. Workers deal with the Debt Manager at a pay hatch on the road side, behind a queue rail, and never go inside.
  - **The Short Measure** — alehouse for guards and artisans at the start, selling food and ale. Workers eat at the canteen on their own side.
  - **Drill yard** — pells, a guards' well and straw butts with the bank behind them. An alarm bell stands where the path meets the fort road.
  - **Grain store** — on capped stilts beside the alehouse.
  - **Workers' graves** — on the low bank, with numbers on stakes and no names.
  - **Reserved ground** — plots are kept for kennels and a barber-surgeon.
- **Company farm** — the plain behind the fort hill, outside the palisade. It feeds the camp and the oxen. A field gang from the tents goes out each morning through the inner gate and the fields gate, the western of the twin gates at the foot of the fort ramp, under the fort's eye.
  - **Farm manager's house** — the Company reeve's house beside the track, just outside the palisade by the fields gate, with a kitchen garden behind it.
  - **Tithe barn** — the camp's main grain store, locked. It is the target for the worker path's "secure food reserves" preparation quest. The garrison's grain store holds only the guards' ration.
  - **Threshing floor, ox mill** — grain is threshed under an open roof, then ground by an ox walking the sweep of a round mill shed.
  - **Ox byre and paddock** — the farm's oxen turn the mill and are bred here, with cows and calves in the paddock. The Stoneworks pen keeps the team that hauls stone. Haystacks, a dung heap, the well and the gang's tally post fill the yard.
  - **Fields** — wheat (bread for the fort), barley (ale for The Short Measure and the workers' gruel), oats and a hay meadow (fodder), and the kitchen garden (the canteen). Low wattle fences keep the animals out.
- **Latrine** — trench behind Wretches' Corner.
- **Gang house** — a small timber house on the lake shore near the wilderness opening, with its own short dock behind it. The camp's top gang members are "allowed" a real roof, and it overlooks the night path and the bridge. npc-003 lives here.

### Fences
- **Outer fence** — tall timber palisade around the camp, with openings where roads cross. West of the fort it stands on top of the bank above the garrison quarter. The fort hill closes it on the south: the palisade runs up onto the hill and round its back, and the fort's stone walls take over on the camp side. An 8 m collapsed stretch on the north side keeps the hunter's fire in view from the tents; it can't be walked through. On the east side the palisade runs from the inner fence's end towards the lake and stops short of the shore, so it marks a line more than it closes one.
- **Fort entrances** — a narrow foot entrance through the ruin pillars above the Company Road, and a wide cart gate on the south-west of the plateau.
- **Fort ramp** — a railed 5 m cart ramp from the fort gate (the eastern of the twin ramp gates) up to the cart gate. It runs outside the palisade, under the fort's west wall, so anyone climbing it is exposed; the cart gate is the real defence.
- **Shore rail** — knee-high broken rail with gaps along the lake side. Workers can reach the water. The Company marks the line but doesn't defend it.
- **Inner fence** — tall palisade with one guarded gate, separating the worker area from the company area. The real social divide: everyone walks through it each morning.

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
| Mafia Zone | Workers with leverage or muscle. Better-kept tents. Stash owner's territory, run from the gang house. |
| Fight Pit | About (52.5, 24), by the lake: a round sunken floor inside a raised earth bank, with a gap to the north. A placeholder until the detailed pit is designed: mostly out of sight from the fort, but no secret. Runs at night. See **The Company's Tolerance**. |
| Lakeside | The open ground between the tents and the shore. Reserved: tents and other uses to be decided. |

### Exits
| Exit | Direction | Status | Notes |
|------|-----------|--------|-------|
| Civilization Road | Back toward towns / Company camps | Gated | Guarded post at the new north-west gate. The gate stands open; the pass at the valley's west end is what stays blocked until story conditions are met. |
| Wilderness Path | Into the unknown | Open | Curves behind a hill, disappears from sight. Reads as danger, then as freedom. |

---

## Atmosphere

**By day:** Grim and functional. Workers move in silence. Guards visible. Company flags everywhere. The elevated company house watches over everything. Ruins visible on a distant hillside.

**By night:** The camp breathes. Fewer guards, some asleep. Illegal fishing on the far side of the lake. Fights in the pit, with guards among the bettors. Across the river — a warm light in the treeline. The independent hunter's fire.

**Tone:** Oppressive but not hopeless. The injustice is immediate and personal. Freedom is visible — literally — across the river.

### The Company's Tolerance

The fights are illegal in name only. The Company knows about the pit and lets it run: as long as the workers can work, a little "entertainment" costs it nothing. The pit stays tucked out of sight, so nobody has to look at it, and some guards take bets on the fights.

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
| npc-003 | Stash Owner | Worker with leverage. Intimidation or snitch connections. | Gang house / Mafia Zone |
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
- Fight club winnings and bets, with some guards taking wagers
- Smuggling goods in or out
- Doing other workers' assigned labor for pay
- Selling stolen goods via black market
- Illegal fishing catches

---

## Day/Night Cycle

| Activity | Time | Location | Notes |
|----------|------|----------|-------|
| Illegal fishing | Night | Far side of the lake (spot TBD) | Out of the Company's sight. Black market outlet. |
| Fight club | Night | Fight Pit | Out of sight from the fort, tolerated by the Company. Some guards take bets. |
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
| — | Company farm | Field gang, under the fort's eye | Tithe barn: the food the revolt needs |
| area-004 | Hunting Grounds | Tracker/ranger flavor | Lead hunter (npc-010) — independent-minded, speaks to player as equal |

---

## Open Threads

- **Quarry passage interior** — what's inside, civilization lore connection (needs separate ruins/lore session)
- **Ruins design** — hillside layout, what's inside, the vanished civilization's identity
- **Black market operator** — who runs it, what they sell, faction alignment
- **Camp official shop/trader** — who runs it, company employee or worker?
- **Fight club design** — rules, opponent roster, reward structure, reputation effects, which guards take bets and where the Company's tolerance ends
- **NPC roster completion** — names for all stub NPCs, full dialogue seeds
- **Wood camp quest details** — beyond basic flavor
- **Farm manager** — the reeve who lives at the farm is unnamed, and the field gang has no roster yet
- **Work site guard/manager NPCs** — area-002 and area-003 managers unnamed
