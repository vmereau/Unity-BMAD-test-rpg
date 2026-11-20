# CLAUDE.md — Assets/_Game/Data/NPCs/QuarryOverseer

> Loaded when Claude accesses this NPC's data. Character sheet for Overseer Dace — read it before writing any dialogue, memory or behaviour for this NPC.

---

## Core Identity

| Field | Value |
|-------|-------|
| **Name** | Overseer Dace |
| **Nickname / Known As** | Dace |
| **Profession** | Quarry manager: Company overseer of the Stone Quarry (area-002) |
| **Faction / Affiliation** | The Company (`Faction_Company`) |
| **Location** | By the sledge on the quarry floor, all day |
| **Age** | Early 50s |

---

## Personality

| Field | Value |
|-------|-------|
| **Alignment** | TBD |
| **Traits** | Hard, practical, tired; no patience for fear |
| **Fears / Flaws** | Resents every quota increase; measures people in blocks cut and tallies met |
| **Desires / Goals** | Keep the men away from the sealed south-face cut |

---

## Speech Style

| Field | Value |
|-------|-------|
| **Tone** | Hard, practical, tired. All business |
| **Vocabulary** | Numbers and quotas, mason's words (face, cut, bed, block); calls the player "hands" or "newcomer" |
| **Quirks** | Talks in tallies; only warms up about good stone; shuts down any question about the south face with "Company business". Short sentences are his trait, but they are whole sentences |
| **Taboo Topics** | The south-face cut and the men he took into it |

**Sample line:**
> "Meet your tally and you eat. Miss it and you owe, and owing in this quarry is a hole you dig with your own hands, so cut straight and you won't need to."

---

## Background

Dace was a mason before the Company made him an overseer, and he still respects good stonework more than any person. He
opened a new cut behind the crack in the south face with his trusted cutters, then the Company sealed it with a locked
door. He keeps the men away from it as "Company business" and dismisses their stories about it, but never explains it.
He takes orders from Warden Voss. He is the way into the quarry's work and, later, the sealed passage (quest-002, with
npc-009).

## Dialogue Writing Rules (NPC-specific)

- TBD

---

## Asset Reference

| Asset | Path |
|-------|------|
| Scene | Instance under the hand-owned `WorkSiteManagers` root; stands all day at `WorkSiteManagers/QuarryOverseer_Spot` |
| Dialogues | `Dialogues/Notice/` (one-time Warden's-errand delivery, needs `Item_Notice_Quarry`), `Dialogues/Work/` (repeatable) |
| Head | Placeholder copy of the Bandit mesh (`HeadAppearance_QuarryOverseer`) until `/characters:portrait-face` |
