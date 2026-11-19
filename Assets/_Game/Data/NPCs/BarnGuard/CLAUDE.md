# NPC Identity — Osric Venn

> This file is the canonical character sheet for this NPC. It is the instantiated version of
> `NPC_IDENTITY_TEMPLATE.md`. Read it before writing any dialogue, memory, or behavioural asset
> for this NPC. Keep it up to date as the character develops.

---

## Core Identity

| Field | Value |
|-------|-------|
| **Name** | Osric Venn |
| **Nickname / Known As** | Venn |
| **Profession** | Garrison guard, fields gate post; keeps the second key to the tithe barn for the fort's ration draws |
| **Faction / Affiliation** | The Company (`Faction_Company`); secretly sells barn grain to the gang through Brann (npc-003) |
| **Location** | Fields gate by day, barracks bunk at night, tithe barn door 00–03 |
| **Age** | Mid-30s |

---

## Personality

| Field | Value |
|-------|-------|
| **Alignment** | Corrupt but not cruel; practical |
| **Traits** | Charming, easy, reasonable, self-justifying, unhurried |
| **Fears / Flaws** | Being called a thief; believes everything is a fair arrangement if framed right |
| **Desires / Goals** | Keep his side income quiet, keep his post, stay friends with everyone who matters |

---

## Speech Style

| Field | Value |
|-------|-------|
| **Tone** | Friendly and reasonable, a salesman's ease |
| **Vocabulary** | Company and guard words (gate, post, draws, the fort), trade words (arrangement, cut, fair) |
| **Quirks** | Reframes everything as a fair arrangement; calls the player "friend"; answers a threat with a smile |
| **Taboo Topics** | Being called a thief (he moves grain, he doesn't steal it) |

**Sample line:**
> "Fields gate. Workers out at dawn, in at dusk, and nobody in between who hasn't got a reason. Have you got one? Good. Then we're friends."

---

## Background

Venn has stood the fields gate for two seasons and holds the fort's second key to the tithe barn. He sells two or
three sacks a week to the gang's black market through Brann, carefully enough that Pell's count is only ever a little
short. He thinks of it as a tax on a Company that taxes everyone.

## Dialogue Writing Rules (NPC-specific)

- Never sounds guilty or threatened in the moment. When refused he threatens calmly, as a friendly warning ("nobody's
  going to believe you", "the men who buy that grain remember faces"), never angrily.
- Never calls himself a thief and never says "steal" about himself.

---

## Quest Role — TitheBarnShortfall (`docs/World/Quests/TitheBarnShortfall.md`)

The thief. Caught at the barn door 00–03 (`WorldFact_TitheBarn_ThiefCaught`), he offers a deal (40 gold, name Pim) or
is refused. Removed 12 h after Pell hears his name (`ElapsedFact_FarmReeve_Shortfall_ReportedGuard_12h`, scene
`FactRemovedObject` wrapper). His death fails the quest (`KilledFact_BarnGuard`).

Topics: `Greeting` (until caught), `BarnTalk` (quest started, until caught: blames the workers, brush-off), `Caught` (deal / refuse; refuse = calm threat, dialogue only), `AfterRefused`, `AfterDeal`, `AfterReported`.

---

## Asset Reference

| Asset | Path |
|-------|------|
| NPCEntity | `Assets/_Game/Data/NPCs/BarnGuard/NPC_BarnGuard.asset` |
| Dialogues | `Assets/_Game/Data/NPCs/BarnGuard/Dialogues/` |
| Memories | `Assets/_Game/Data/NPCs/BarnGuard/Memories/` |
| Scene | StartingTown `TitheBarnShortfall/BarnGuard_Removal/BarnGuard` (NPC_base Variant instance; wrapper has the `FactRemovedObject`). Routine: 06–21 Stand `Spot_BarnGuard_Gate`, 00–03 Stand `Spot_BarnGuard_BarnDoor` (label `Barn`), 21–00 / 03–06 Sleep `TitheBarnShortfall/BarnGuardBed` (pallet on barracks `Bunk_0`). Barn trigger: `TitheBarnShortfall/BarnDoorTrigger` (hours 0–3, NearSpot r 3) |
