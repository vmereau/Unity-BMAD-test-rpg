# NPC Identity — Overseer Dace

## Core Identity

- Name: Overseer Dace (quarry manager).
- Profession: Company overseer of the Stone Quarry (area-002).
- Affiliation: The Company (`Faction_Company`).
- Age: early 50s.

## Personality and Background

Hard, practical and tired. Dace measures people in blocks cut and tallies met, and has no patience for fear. He was a mason before the Company made him an overseer, and he still respects good stonework more than any person. He opened a new cut behind the crack in the south face with his trusted cutters, then the Company sealed it with a locked door. He keeps the men away from it as "Company business" and dismisses their stories about it, but never explains it.

## Speech Style

| Field | Value |
|-------|-------|
| **Tone** | Hard, practical, tired. All business |
| **Vocabulary** | Numbers and quotas, mason's words (face, cut, bed, block); calls the player "hands" or "newcomer" |
| **Quirks** | Talks in tallies; only warms up about good stone; shuts down any question about the south face with "Company business". Short sentences are his trait, but they are whole sentences |
| **Taboo Topics** | The south-face cut and the men he took into it |

**Sample line:**
> "Meet your tally and you eat. Miss it and you owe, and owing in this quarry is a hole you dig with your own hands, so cut straight and you won't need to."

## Relationships and Routine

Takes orders from Warden Voss and resents every quota increase. He is the way into the quarry's work and, later, the sealed passage (quest-002, with npc-009). He stands all day by the sledge on the quarry floor (`WorkSiteManagers/QuarryOverseer_Spot`).

## Assets

`NPC_QuarryOverseer.asset`, `Dialogues/Notice/` (one-time Warden's-errand delivery, needs `Item_Notice_Quarry`), `Dialogues/Work/` (repeatable), `Memories/`. Head: placeholder copy of the Bandit mesh (`HeadAppearance_QuarryOverseer`) until `/characters:portrait-face`. Scene instance under the hand-owned `WorkSiteManagers` root.
