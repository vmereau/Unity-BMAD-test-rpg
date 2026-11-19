# NPC Identity — Reeve Wendel Pell

## Core Identity

- Name: Reeve Wendel Pell (farm manager).
- Profession: the Company's reeve of the farm and its tithe barn.
- Affiliation: The Company (`Faction_Company`).
- Age: late 50s.

## Personality and Background

Fussy, precise and suspicious. Pell is a bookkeeper at heart, happier with a tally stick than a person. Every week he counts the tithe barn, and every week it is short. Not by much, just enough that someone is being careful. He is determined to find them, and anyone new is a suspect.

## Speech Style

| Field | Value |
|-------|-------|
| **Tone** | Prim, fussy and suspicious |
| **Vocabulary** | Tallies, sacks, tithe, strips; every number exact |
| **Quirks** | Corrects small things; counts out loud; treats every newcomer as a suspect in the barn shortfall |
| **Taboo Topics** | The idea that the shortfall is his own fault |

**Sample line:**
> "I write it all down, every sack, every strip and every pair of hands. Forty-one sacks in on Tuesday and forty in the barn, so you'll understand why I count twice."

## Relationships and Routine

Answers to Warden Voss and fears being blamed for the missing grain. He is the way into the farm's work and the barn (later theft and food-reserve quests for the worker path). He stands all day outside the east door of the manager's house (`WorkSiteManagers/FarmReeve_Spot`).

## Assets

`NPC_FarmReeve.asset`, `Dialogues/Notice/` (one-time Warden's-errand delivery, needs `Item_Notice_Farm`), `Dialogues/Work/` (repeatable), TitheBarnShortfall topics `Dialogues/Shortfall/` (quest start), `ShortfallReport/` (open from quest start; name Venn by handing over `Item_BrannsTally`, the no-proof Venn line gated on `Mem_FarmReeve_GateCaught` sets `NeedProof`, Pim gated on `Mem_FarmReeve_GateDeal`), `ShortfallAfter/` (one repeatable aftermath per ending), `Memories/`. Head: placeholder copy of the WarehouseGuard mesh (`HeadAppearance_FarmReeve`) until `/characters:portrait-face`. Scene instance under the hand-owned `WorkSiteManagers` root.
