# NPC Identity — Warden Voss

> This file is the canonical character sheet for this NPC. It is the instantiated version of
> `NPC_IDENTITY_TEMPLATE.md`. Read it before writing any dialogue, memory, or behavioural asset
> for this NPC. Keep it up to date as the character develops.

---

## Core Identity

| Field | Value |
|-------|-------|
| **Name** | Warden Voss |
| **Nickname / Known As** | the Warden |
| **Profession** | Camp overseer for the Company (npc-006) |
| **Faction / Affiliation** | The Company |
| **Location** | Company house on the outcrop; camp center by day |
| **Age** | Late 40s |

---

## Personality

| Field | Value |
|-------|-------|
| **Alignment** | Lawful cruel — the rules are his tool, not his belief |
| **Traits** | Authoritarian, rude, calculating, publicly brutal |
| **Fears / Flaws** | Bigger Company men above him; needs the camp's numbers to look good |
| **Desires / Goals** | Maximize output, keep control, be noticed by the Company |

---

## Speech Style

| Field | Value |
|-------|-------|
| **Tone** | Contemptuous, flat, final |
| **Vocabulary** | Short orders. Talks about people as costs and output |
| **Quirks** | Never says please or thanks; never repeats himself; states outcomes as facts |
| **Taboo Topics** | Any suggestion that the workers were deceived |

**Sample line:**
> "Medicine costs coin, and coin goes on your debt."

---

## Background

Voss runs Alderath Camp for the Company. He uses punishment as theater: every beating at the center stage is staged for the people watching. Not a final boss — a mid-tier bully who feels enormous when the player has nothing.

---

## Dialogue Writing Rules (NPC-specific)

> These rules extend `DIALOGUE.md` for this NPC specifically.

- Orders, never requests. Refusing him gets contempt, not anger
- Refers to the player's debt whenever money comes up
- No exposition: he assumes workers already know their place

---

## Asset Reference

| Asset | Path |
|-------|------|
| NPCEntity | `Assets/_Game/Data/NPCs/Warden/NPC_Warden.asset` |
| Dialogues | `Assets/_Game/Data/NPCs/Warden/Dialogues/` |
| Memories | `Assets/_Game/Data/NPCs/Warden/Memories/` |
| Prefab | Scene instance of `Assets/_Game/Prefabs/Entities/Humanoids/NPC_base Variant.prefab` (no dedicated prefab) |
