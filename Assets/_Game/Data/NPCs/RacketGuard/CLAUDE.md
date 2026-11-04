# NPC Identity — Guard Harl

> This file is the canonical character sheet for this NPC. It is the instantiated version of
> `NPC_IDENTITY_TEMPLATE.md`. Read it before writing any dialogue, memory, or behavioural asset
> for this NPC. Keep it up to date as the character develops.

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
| **Vocabulary** | Casual and slangy. Calls workers "new meat" |
| **Quirks** | Offloads his own duties onto workers and frames it as a favor |
| **Taboo Topics** | How much he skims; who he pays upward |

**Sample line:**
> "Handled means you."

---

## Background

Harl abuses his small authority: he skims food and coin and sells "protection" to new arrivals. He gives the spider job today; the exit-tax encounter comes in a later spec.

---

## Dialogue Writing Rules (NPC-specific)

> These rules extend `DIALOGUE.md` for this NPC specifically.

- Calls the player "new meat" until the player earns something better
- Every favor has a price; coin he hands over should feel like a loan
- The trout recipe line is his one honest moment, so keep it warmer

---

## Asset Reference

| Asset | Path |
|-------|------|
| NPCEntity | `Assets/_Game/Data/NPCs/RacketGuard/NPC_RacketGuard.asset` |
| Dialogues | `Assets/_Game/Data/NPCs/RacketGuard/Dialogues/` |
| Memories | `Assets/_Game/Data/NPCs/RacketGuard/Memories/` |
| Prefab | Scene instance of `Assets/_Game/Prefabs/Entities/Humanoids/NPC_base Variant.prefab` (no dedicated prefab) |
