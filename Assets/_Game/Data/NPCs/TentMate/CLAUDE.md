# NPC Identity — Old Hobb

> This file is the canonical character sheet for this NPC. It is the instantiated version of
> `NPC_IDENTITY_TEMPLATE.md`. Read it before writing any dialogue, memory, or behavioural asset
> for this NPC. Keep it up to date as the character develops.

---

## Core Identity

| Field | Value |
|-------|-------|
| **Name** | Old Hobb |
| **Nickname / Known As** | Hobb |
| **Profession** | Veteran laborer, the player's tent mate (npc-001) |
| **Faction / Affiliation** | Unaffiliated |
| **Location** | Player's Zone, the shared tent |
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
| **Tone** | Low, plain, unsentimental |
| **Vocabulary** | Working-class, simple words |
| **Quirks** | Gives directions by who to avoid; rarely finishes a thought about the past |
| **Taboo Topics** | How he came to the camp |

**Sample line:**
> "Keep your head down and your bowl close."

---

## Background

Hobb signed the same false contract years ago. He knows where everything is, and covers the what and where of camp life. Social information belongs to Tam.

---

## Dialogue Writing Rules (NPC-specific)

> These rules extend `DIALOGUE.md` for this NPC specifically.

- Practical logistics only: places, routines, rules
- Warm through actions, never through speeches
- Leaves judgments of people to the Beaten Worker

---

## Asset Reference

| Asset | Path |
|-------|------|
| NPCEntity | `Assets/_Game/Data/NPCs/TentMate/NPC_TentMate.asset` |
| Dialogues | `Assets/_Game/Data/NPCs/TentMate/Dialogues/` |
| Memories | `Assets/_Game/Data/NPCs/TentMate/Memories/` |
| Prefab | Scene instance of `Assets/_Game/Prefabs/Entities/Humanoids/NPC_base Variant.prefab` (no dedicated prefab) |
