# CLAUDE.md — Assets/_Game/Data/NPCs/RacketGuard

> Loaded when Claude accesses this NPC's data. Character sheet for Guard Harl — read it before writing any dialogue, memory or behaviour for this NPC.

---

## Core Identity

| Field | Value |
|-------|-------|
| **Name** | Guard Harl |
| **Nickname / Known As** | Harl |
| **Profession** | Armed camp guard and extortionist (npc-007) |
| **Faction / Affiliation** | The Company |
| **Location** | Camp exits and the Company yard |
| **Age** | Mid-30s |

---

## Personality

| Field | Value |
|-------|-------|
| **Alignment** | Neutral self-serving — a thief with a uniform |
| **Traits** | Lazy, friendly on the surface, greedy, petty |
| **Fears / Flaws** | Being caught skimming by the Warden; workers who aren't afraid of him |
| **Desires / Goals** | Personal enrichment at no risk |

---

## Speech Style

| Field | Value |
|-------|-------|
| **Tone** | Mock-friendly, then flat |
| **Vocabulary** | Casual and slangy; calls workers "new meat"; talks about protection like a shopkeeper selling it |
| **Quirks** | Dresses extortion up as a favour; jokes at your expense and enjoys his own jokes; turns wheedling once he has been beaten |
| **Taboo Topics** | How much he skims; who he pays upward |

**Sample line:**
> "Handled means you, new meat. I'd do it myself, but then what would you do all day?"

---

## Background

Harl abuses his small authority: he skims food and coin and sells "protection" to new arrivals. He works
two exit tolls (spec-racket-guard-exit-toll): the quarry opening by the tally post 06–14 and the wilderness opening
(bridge head) 14–21; nights outside The Short Measure. The fee is 10 gold per gate, paid once. "Can't pay" works once across
both gates. Refusing or walking off means a brawl: if the player loses, Harl robs them and that gate counts as paid. If Harl
loses any fight to the player, he never collects again.

---

## Dialogue Writing Rules (NPC-specific)

- Calls the player "new meat" until the player earns something better
- Every favor has a price; coin he hands over should feel like a loan

---

## Asset Reference

| Asset | Path |
|-------|------|
| Toll racket | `TollRacket_Harl.asset` (facts `WorldFact_Toll_*`, signals in `Dialogues/Toll/`); scene gates under `StartingTown/TollGates` |
| Toll dialogue | `Dialogues/Toll/` — **forced** topics (`Start_RacketGuard_Toll`, `_TollOtherGate`) opened by `TollGate`, not exposed by a memory (Quest Explorer may list them as unattributed — expected). `Memories/Toll/Mem_RacketGuard_TollCanDefer` gates the "can't pay" choice; `Mem_RacketGuard_TollBeaten` + `Dialogues/TollBeaten/` = his grudging line after losing |
