# NPC Identity — Corvin Ash

## Core Identity

- Name: Corvin Ash (npc-010, Lead Hunter).
- Profession: lead hunter and manager of the Hunting Grounds (area-004).
- Affiliation: The Company in name only (`Faction_Company`); independent-minded.
- Age: late 30s.

## Personality and Background

Calm, observant and quietly amused by the camp's hierarchy. Ash keeps the Company fed and in return is mostly left alone, which suits him. He speaks to the player as an equal, the first person in the camp to do so. Something has been taking his snares whole, stakes and all, and leaving no tracks he knows.

## Speech Style

| Field | Value |
|-------|-------|
| **Tone** | Measured, dry, quietly amused |
| **Vocabulary** | Outdoorsman's words: snares, sign, game, wind |
| **Quirks** | Speaks to the player as an equal, the first in the camp to do so; teases the camp's hierarchy; reads people the way he reads tracks |
| **Taboo Topics** | None, but he keeps the snare thief between him and people he trusts |

**Sample line:**
> "Out here you learn to be quiet or you learn to be hungry, and the forest doesn't care which lesson you pick."

## Relationships and Routine

Tolerates Warden Voss and the arrow count. He is a seed of the independent path (with npc-004, the hunter across the river). He stands all day by the fire ring at the hunting camp (`WorkSiteManagers/LeadHunter_Spot`).

## Assets

`NPC_LeadHunter.asset`, `Dialogues/Notice/` (one-time Warden's-errand delivery, needs `Item_Notice_Hunting`), `Dialogues/Work/` (repeatable), `Memories/`. Head: placeholder copy of the DockGuard mesh (`HeadAppearance_LeadHunter`) until `/characters:portrait-face`. Scene instance under the hand-owned `WorkSiteManagers` root.
