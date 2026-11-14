# NPC Identity — Gorm Holl

## Core Identity

- Name: Gorm Holl (wood camp manager).
- Profession: worker gang boss of the Wood Camp (area-003).
- Affiliation: none, a worker who runs the felling crew (`Faction_Neutral`).
- Age: mid 40s.

## Personality and Background

Gruff, protective of his crew and openly unimpressed by the Company. Holl came out as a worker like everyone else and earned the crew's trust by keeping them alive, not by any title. Lately the woods frighten him: trees his men have notched come down against the cut, and one of them was crushed last month.

## Speech Style

Plain and blunt, a few dry jokes. No titles, no flattery. Sample: "Ugly work, honest work. Pays nothing, and nobody here pretends it does."

## Relationships and Routine

Reports to Warden Voss and does the bare minimum the notices ask. He is the way into the wood camp's work and its strange falls (a later quest). He stands all day by the eastern shelter of the wood camp (`WorkSiteManagers/TimberBoss_Spot`).

## Assets

`NPC_TimberBoss.asset`, `Dialogues/Notice/` (one-time Warden's-errand delivery, needs `Item_Notice_Timber`), `Dialogues/Work/` (repeatable), `Memories/`. Head: placeholder copy of the GuardRight mesh (`HeadAppearance_TimberBoss`) until `/characters:portrait-face`. Scene instance under the hand-owned `WorkSiteManagers` root.
