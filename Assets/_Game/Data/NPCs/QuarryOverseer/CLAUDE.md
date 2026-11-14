# NPC Identity — Overseer Dace

## Core Identity

- Name: Overseer Dace (quarry manager).
- Profession: Company overseer of the Stone Quarry (area-002).
- Affiliation: The Company (`Faction_Company`).
- Age: early 50s.

## Personality and Background

Hard, practical and tired. Dace measures people in blocks cut and tallies met, and has no patience for fear. He was a mason before the Company made him an overseer, and he still respects good stonework more than any person. He dismisses the workers' dread of the sealed crack in the south face as foolishness, yet nobody works that face, and he has never ordered them to.

## Speech Style

Clipped, all business, numbers and quotas. He calls the player "hands" or "newcomer". Sample: "Meet your tally and you eat. Miss it and you owe."

## Relationships and Routine

Takes orders from Warden Voss and resents every quota increase. He is the way into the quarry's work and, later, the sealed passage (quest-002, with npc-009). He stands all day by the sledge on the quarry floor (`WorkSiteManagers/QuarryOverseer_Spot`).

## Assets

`NPC_QuarryOverseer.asset`, `Dialogues/Notice/` (one-time Warden's-errand delivery, needs `Item_Notice_Quarry`), `Dialogues/Work/` (repeatable), `Memories/`. Head: placeholder copy of the Bandit mesh (`HeadAppearance_QuarryOverseer`) until `/characters:portrait-face`. Scene instance under the hand-owned `WorkSiteManagers` root.
