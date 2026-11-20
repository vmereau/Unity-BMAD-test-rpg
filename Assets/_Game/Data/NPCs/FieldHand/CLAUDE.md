# CLAUDE.md — Assets/_Game/Data/NPCs/FieldHand

> Loaded when Claude accesses this NPC's data. Character sheet for Pim — read it before writing any dialogue, memory or behaviour for this NPC.

---

## Core Identity

| Field | Value |
|-------|-------|
| **Name** | Pim |
| **Nickname / Known As** | Pim |
| **Profession** | Field-gang worker on the Company farm strips |
| **Faction / Affiliation** | None (`Faction_Neutral`) |
| **Location** | Farm strips by day, tent-camp pallet near the north tarps at night |
| **Age** | Young adult (around 19) |

---

## Personality

| Field | Value |
|-------|-------|
| **Alignment** | Innocent, naive |
| **Traits** | Slow-spoken, trusting, tired, earnest, a little wounded |
| **Fears / Flaws** | Being blamed; Pell looks at him "funny"; nobody listens to him |
| **Desires / Goals** | To be asked his opinion once; to get through the season without trouble |

---

## Speech Style

| Field | Value |
|-------|-------|
| **Tone** | Plain, earnest, slightly plaintive |
| **Vocabulary** | Simple farm words: strips, barley, rain, the gang, the reeve |
| **Quirks** | Complains mildly that nobody asks him anything; worries out loud; asks the player for small favours |
| **Taboo Topics** | None; he says too much rather than too little |

**Sample line:**
> "Barley's coming in thin this year. Reeve says it's the soil. I think it's the rain. Nobody asks me, though."

---

## Background

Pim came to the camp on a debt cart and was put on the field gang because he was too slight for the quarry. He sleeps
near the barn wall side of the camp and works the strips under Pell's eye. He is the easy name Venn hands the player
in the deal ending of TitheBarnShortfall.

## Dialogue Writing Rules (NPC-specific)

- Never clever or guarded; his guesses point the wrong way honestly.

---

## Quest Role — TitheBarnShortfall

Lead (optional step 0): `BarnTalk` sets `DialogueFact_FieldHand_BarnTalk`. Scapegoat in the deal ending: removed 12 h
after Pell hears his name (`ElapsedFact_FarmReeve_Shortfall_BlamedPim_12h`, scene `FactRemovedObject` wrapper).

Topics: `Greeting`, `BarnTalk` (one-shot), `BarnTalkAgain` (repeat).

---

## Asset Reference

| Asset | Path |
|-------|------|
| Scene | StartingTown `TitheBarnShortfall/FieldHand_Removal/FieldHand_Pim` (NPC_base Variant instance; wrapper has the `FactRemovedObject`). Routine: 06–21 Stand `Spot_FieldHand_Strips` (wheat field edge), 21–06 Sleep `TitheBarnShortfall/FieldHandBed` (tent camp, west of Hobb's hut) |
