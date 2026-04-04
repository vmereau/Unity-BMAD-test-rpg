---
title: 'Game Brainstorming Session — NPC Memory System'
date: '2026-04-03'
author: 'user'
version: '1.0'
stepsCompleted: [1, 2, 3, 4]
status: 'complete'
---

# Game Brainstorming Session — NPC Memory System

## Session Info

- **Date:** 2026-04-03
- **Facilitator:** Game Designer Agent
- **Topic:** NPC Memory System

---

## Brainstorming Approach

**Topic:** NPC Memory System
**Selected Mode:** Guided — techniques applied sequentially

**Technique Sequence:**
1. MDA Framework — design the system from mechanics to feeling
2. Emergence Engineering — find the simple rules that create complex behaviour
3. Player Agency Moments — which player actions matter and how
4. Ludonarrative Harmony — make it feel alive, not like a database
5. What If Scenarios — push to extremes to find the interesting edges
6. Verbs Before Nouns — what do NPCs *do* with memories?
7. Remix an Existing Game — learn from Morrowind / Witcher / Disco Elysium

**Focus Areas:**
- Memory data model (what is stored, how long, at what fidelity)
- Impact on: dialogue, daily routines, quests offered, shop behaviour
- Player-facing feedback (how does the player *know* NPCs remember?)
- Scope for a solo Unity dev (implementation realism)

---

## Core Design Decisions

### Aesthetic North Star
**Target player feeling:** Invested and immersed — the game world is reacting to their actions.
Not just response, but *aliveness*. The world knows the player.

The key moment: an NPC spontaneously references something the player did — unprompted, organic, in their own words. Not a system talking. A person remembering.

**Primary failure mode to avoid:** Stale dialogue — the NPC references an old event that the world has moved past. Something bigger happened since then, making the old memory feel out of place.

---

## Ideas Generated

### [Memory Model #1]: The Memory Unit
_Core Loop_: Each NPC holds a set of Memory entries. Each Memory has unlock conditions (world state keys that must be true) and invalidation conditions (world state keys that permanently close the memory). When the player engages an NPC, all valid memories surface as available effects.
_Novelty_: Memories are not timers or counters — they are world-state queries. Staleness is determined by world events, not time.

```
Memory {
  id: "cleared_mill_monster"
  unlock_conditions:   [player_completed_mill_quest == true]
  invalidation_conditions: [mill_destroyed == true, npc_hostile == true]
  effects: {
    dialogue:  ["Thank you for clearing the mill..."]
    shop:      { modifier: price -10%, first_open_dialogue: "Least I can do..." }
    routine:   [workers_return_to_mill_during_daytime]
    quest:     [special_dialogue_option → "miller_cousin_needs_help"]
  }
}
```

---

### [Memory Model #2]: Invalidation Always Supersedes Unlock
_Core Loop_: If both an unlock condition and an invalidation condition are true simultaneously, the invalidation wins. The memory is closed, permanently.
_Novelty_: No ambiguity, no conflict resolution needed. Negative world events always take precedence. An NPC can have a new memory unlocked by the same event that invalidated the old one — they evolve rather than go silent.

**Example — The Cousin:**
- Event: `mill_destroyed` + `player_joined_burning_faction`
- Invalidates: all mill-positive memories (gratitude dialogue, discount, cousin quest)
- Unlocks: new dialogue set — the cousin's hidden resentment surfaces, a more complex relationship begins

---

### [Memory Model #3]: Four Effect Types
_Core Loop_: A single memory entry can fan out into up to four distinct game system effects simultaneously. One world event, multiple systems react.
_Novelty_: The designer authors one data entry; four systems respond. No per-system scripting required.

| Effect | Player visibility | Reveal pattern |
|--------|------------------|----------------|
| Dialogue | Full | Always visible as dialogue option when valid |
| Shop | Partial | One-shot reveal dialogue on first shop open, then silent |
| Routine | None | Silently changes NPC behaviour in world |
| Quest | Full | New dialogue option that initiates the quest |

---

### [Architecture #4]: GlobalGameState + EventBus
_Core Loop_: Any game system can fire a world event. The event updates GlobalGameState (persistent key-value facts about the world). Memory conditions query GlobalGameState. Systems can react reactively via the EventBus or query proactively when needed.
_Novelty_: Open vocabulary — no predefined event list. As new systems are designed, they declare their own events. The memory system scales with the game.

```
AnySystem.Fire("world_event_key", { context })
     ↓
GlobalGameState.Set("world_event_key", true)
     ↓
MemorySystem evaluates unlock/invalidation conditions
NPC routines evaluate behaviour changes
QuestSystem evaluates new unlocks
ShopSystem queries GlobalGameState on open
```

---

### [Emergence #5]: Ripple Effects via Shared World State Keys
_Core Loop_: Multiple NPCs can hold memories that reference the same world state key. When that key changes, all affected NPCs react simultaneously — without explicit scripting between them.
_Novelty_: NPC relationship networks emerge from shared conditions, not explicit chains. The designer gives each NPC their own memories; the network emerges organically.

**Example — "mill_monster_cleared" fires:**
- Miller: unlocks gratitude dialogue + shop discount + workers return to mill
- Workers: unlock routine (go to work during daytime)
- Guard captain: unlocks dialogue ("heard you helped the miller")
- Rival merchant: unlocks *negative* dialogue (lost business to mill reopening)

Nobody scripted miller → guard captain. Same key, four different authored reactions.

---

### [Emergence #6]: One Event, Opposing Reactions
_Core Loop_: The same world state event can unlock positive memories on some NPCs and negative memories on others simultaneously.
_Novelty_: Moral complexity emerges from the authored NPC set, not from explicit branching. The player's one action divides the world naturally.

---

### [Scope Decision #7]: Memory Triggers Tied to Quest Outcomes
_Core Loop_: The primary vocabulary of world events will emerge from quest design. Quest completed, failed, and ignored states are the main triggers for memory unlock/invalidation.
_Novelty_: Keeps implementation focused. The event vocabulary grows organically as quests are authored, not as a predefined list.

---

### [Future Layer #8]: Reputation System
_Core Loop_: An aggregate layer built on top of memory effects. NPC memories contribute to faction or global reputation scores. High negative reputation → NPCs refuse dialogue, become hostile. High positive → new interactions unlocked.
_Novelty_: Reputation is a read of the memory system, not a separate parallel system. It emerges from the same data.

---

### [Reference #9]: What Existing Games Do — and What to Steal
| Game | What they do | Steal | Discard |
|------|-------------|-------|---------|
| Morrowind | Flat dialogue topic list, any NPC can have any topic | All valid options visible simultaneously | No world-state reactivity |
| Witcher 3 | Act-change events affect main quest NPCs | World events invalidating old dialogue | Side NPCs are mostly static |
| Disco Elysium | Every choice writes to persistent state | How the player engaged matters, not just what they did | Internal player psychology — this system is world/NPC state |

**This system is more systemic than all three** — data-driven memories across N NPCs reacting to a shared world state is more scalable.

---

## Firm Constraints (out of scope)

- **No memory forgetting** — once created, memories persist until invalidated by world events
- **No gossip** — NPCs don't share memories with each other; secondhand knowledge is not modelled
- **No memory degradation** — memories don't distort or fade over time
- **No player memory footprint UI** — player discovers effects through play, not a stats screen (reputation UI is a future consideration)
- **NPC backstory is static** — authored at game start, not affected by the memory system

---

## Themes and Patterns

1. **World state as the single source of truth** — everything reads from / writes to GlobalGameState
2. **Invalidation as world evolution** — closed doors are as important as opened ones
3. **Emergence from shared conditions** — network effects without scripted chains
4. **Reveal proportional to significance** — dialogue is explicit, shop has one reveal, routine is silent
5. **Quest outcomes as primary memory vocabulary** — the event list grows from game design, not the other way around

## Promising Next Steps

- Design the `NPCMemory` ScriptableObject schema (id, conditions, effects)
- Design the `GlobalGameState` service (key-value store, persistence, EventBus integration)
- Identify 3-5 NPCs in the starting town to pilot the system with
- Define how memory dialogue options are visually distinguished in the dialogue UI
- Scope out the reputation layer as a future Epic
