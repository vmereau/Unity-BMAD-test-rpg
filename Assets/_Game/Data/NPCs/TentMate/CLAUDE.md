# CLAUDE.md — Assets/_Game/Data/NPCs/TentMate

> Loaded when Claude accesses this NPC's data. Character sheet for Old Hobb — read it before writing any dialogue, memory or behaviour for this NPC.

---

## Core Identity

| Field | Value |
|-------|-------|
| **Name** | Old Hobb |
| **Nickname / Known As** | Hobb |
| **Profession** | Veteran laborer, the player's tent mate (npc-001) |
| **Faction / Affiliation** | Unaffiliated |
| **Location** | Player's Zone, his hut (`worker-mate`) next to the player's |
| **Age** | Early 60s |

---

## Personality

| Field | Value |
|-------|-------|
| **Alignment** | Neutral Good, worn down |
| **Traits** | Practical, tired, kind in small ways, cautious |
| **Fears / Flaws** | Too old for the quarry; afraid of being moved to the Wretches' Corner |
| **Desires / Goals** | Keep his place; see one newcomer survive their first season |

---

## Speech Style

| Field | Value |
|-------|-------|
| **Tone** | Low, plain, unsentimental; kind through what he does |
| **Vocabulary** | Working-class, simple words; names places by what happens there |
| **Quirks** | Gives directions by who to avoid; turns everything into a practical how-to; changes the subject when the past comes up |
| **Taboo Topics** | How he came to the camp |

**Sample line:**
> "Keep your head down and your bowl close. The first season's the worst, and after that you stop expecting anything, which helps more than you'd think."

---

## Background

Hobb signed the same false contract years ago. He knows where everything is, and covers the what and where of camp life. Social information belongs to Tam.

---

## Dialogue Writing Rules (NPC-specific)

- Practical logistics only: places, routines, rules
- Warm through actions, never through speeches
- Leaves judgments of people to the Beaten Worker

---

## Asset Reference

| Asset | Path |
|-------|------|
| Dialogues | `Assets/_Game/Data/NPCs/TentMate/Dialogues/` — `Arrival/Start_TentMate_ArrivalBriefing` is event-001's forced briefing (`ArrivalSequence_Event001.hutBriefing`), in no memory |
| Facts | `Facts/WorldFact_Hobb_BelongingsRummaged` (set by `HobbsBox`'s `ContainerTakeFact`), `WorldFact_Hobb_RummageOpenerPlayed`, `ElapsedFact_Hobb_BelongingsRummaged_2h` (the opener's condition: 2 game hours after the take), `KilledFact_HobbsBox` (the box's save key) |
| Suspicion opener | `Dialogues/Suspicion/Start_TentMate_Rummaged` → `Text_TentMate_Rummaged`: the scene `NPCPresence._talkOpeners` entry, pending 2 game hours after an unseen take (cancelled at once when Hobb witnessed the theft). In no memory, so Quest Explorer may list it as unattributed |
