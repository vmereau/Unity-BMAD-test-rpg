# CLAUDE.md — Assets/_Game/Data/NPCs/LeadHunter

> Loaded when Claude accesses this NPC's data. Character sheet for Corvin Ash — read it before writing any dialogue, memory or behaviour for this NPC.

---

## Core Identity

| Field | Value |
|-------|-------|
| **Name** | Corvin Ash (npc-010) |
| **Nickname / Known As** | Lead Hunter |
| **Profession** | Lead hunter and manager of the Hunting Grounds (area-004) |
| **Faction / Affiliation** | The Company in name only (`Faction_Company`); independent-minded |
| **Location** | By the fire ring at the hunting camp, all day |
| **Age** | Late 30s |

---

## Personality

| Field | Value |
|-------|-------|
| **Alignment** | TBD |
| **Traits** | Calm, observant, quietly amused by the camp's hierarchy |
| **Fears / Flaws** | TBD |
| **Desires / Goals** | To be left alone; find what has been taking his snares |

---

## Speech Style

| Field | Value |
|-------|-------|
| **Tone** | Measured, dry, quietly amused |
| **Vocabulary** | Outdoorsman's words: snares, sign, game, wind |
| **Quirks** | Speaks to the player as an equal, the first in the camp to do so; teases the camp's hierarchy; reads people the way he reads tracks |
| **Taboo Topics** | None, but he keeps the snare thief between him and people he trusts |

**Sample line:**
> "Out here you learn to be quiet or you learn to be hungry, and the forest doesn't care which lesson you pick."

---

## Background

Ash keeps the Company fed and in return is mostly left alone, which suits him. He tolerates Warden Voss and the arrow
count. Something has been taking his snares whole, stakes and all, and leaving no tracks he knows. He is a seed of the
independent path (with npc-004, the hunter across the river).

## Dialogue Writing Rules (NPC-specific)

- TBD

---

## Asset Reference

| Asset | Path |
|-------|------|
| Scene | Instance under the hand-owned `WorkSiteManagers` root; stands all day at `WorkSiteManagers/LeadHunter_Spot` |
| Dialogues | `Dialogues/Notice/` (one-time Warden's-errand delivery, needs `Item_Notice_Hunting`), `Dialogues/Work/` (repeatable) |
| Head | Placeholder copy of the DockGuard mesh (`HeadAppearance_LeadHunter`) until `/characters:portrait-face` |
