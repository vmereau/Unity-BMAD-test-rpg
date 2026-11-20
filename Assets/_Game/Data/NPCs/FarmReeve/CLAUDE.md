# CLAUDE.md — Assets/_Game/Data/NPCs/FarmReeve

> Loaded when Claude accesses this NPC's data. Character sheet for Reeve Wendel Pell — read it before writing any dialogue, memory or behaviour for this NPC.

---

## Core Identity

| Field | Value |
|-------|-------|
| **Name** | Reeve Wendel Pell |
| **Nickname / Known As** | Reeve Pell (`entityName`), Pell |
| **Profession** | Farm manager: the Company's reeve of the farm and its tithe barn |
| **Faction / Affiliation** | The Company (`Faction_Company`) |
| **Location** | Outside the east door of the manager's house, all day |
| **Age** | Late 50s |

---

## Personality

| Field | Value |
|-------|-------|
| **Alignment** | TBD |
| **Traits** | Fussy, precise, suspicious |
| **Fears / Flaws** | Being blamed for the missing grain; happier with a tally stick than a person |
| **Desires / Goals** | Find whoever is skimming the tithe barn |

---

## Speech Style

| Field | Value |
|-------|-------|
| **Tone** | Prim, fussy and suspicious |
| **Vocabulary** | Tallies, sacks, tithe, strips; every number exact |
| **Quirks** | Corrects small things; counts out loud; treats every newcomer as a suspect in the barn shortfall |
| **Taboo Topics** | The idea that the shortfall is his own fault |

**Sample line:**
> "I write it all down, every sack, every strip and every pair of hands. Forty-one sacks in on Tuesday and forty in the barn, so you'll understand why I count twice."

---

## Background

Pell is a bookkeeper at heart. Every week he counts the tithe barn, and every week it is short. Not by much, just enough
that someone is being careful. He is determined to find them, and anyone new is a suspect. He answers to Warden Voss and
is the way into the farm's work and the barn.

## Dialogue Writing Rules (NPC-specific)

- TBD

---

## Quest Role — TitheBarnShortfall

The barn-theft quest `TitheBarnShortfall` (`Data/Quests/TitheBarnShortfall/`) starts with him. Topics:

- `Dialogues/Shortfall/` — quest start; the sighting choice, gated on `Mem_FarmReeve_GateSeenVenn`, starts the quest
  (sets `Accepted`) and gives the "real proof" warning; `Choice_FarmReeve_Shortfall_Venn` then sets `NeedProof` and ends
  on `Text_FarmReeve_Shortfall_VennAccept`.
- `Dialogues/ShortfallReport/` — open from quest start; name Venn by handing over `Item_BrannsTally`, the no-proof Venn
  line gated on `Mem_FarmReeve_GateCaught` sets `NeedProof`, Pim gated on `Mem_FarmReeve_GateDeal`.
- `Dialogues/ShortfallAfter/` — one repeatable aftermath per ending.

---

## Asset Reference

| Asset | Path |
|-------|------|
| Scene | Instance under the hand-owned `WorkSiteManagers` root; stands all day at `WorkSiteManagers/FarmReeve_Spot` |
| Dialogues | `Dialogues/Notice/` (one-time Warden's-errand delivery, needs `Item_Notice_Farm`), `Dialogues/Work/` (repeatable), quest topics above |
| Head | Placeholder copy of the WarehouseGuard mesh (`HeadAppearance_FarmReeve`) until `/characters:portrait-face` |
