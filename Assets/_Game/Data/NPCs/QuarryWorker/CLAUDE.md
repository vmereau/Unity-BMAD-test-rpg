# CLAUDE.md — Assets/_Game/Data/NPCs/QuarryWorker

> Loaded when Claude accesses this NPC's data. Character sheet for Bren — read it before writing any dialogue, memory or behaviour for this NPC.

---

## Core Identity

| Field | Value |
|-------|-------|
| **Name** | Bren |
| **Nickname / Known As** | Bren, "old Bren" to the younger workers |
| **Profession** | Quarryman (stone cutter / hauler) |
| **Faction / Affiliation** | None (indentured worker, `Faction_Neutral`) |
| **Location** | Stone Quarry floor by day (area-002), tent camp at night |
| **Age** | Mid-40s, looks older |

---

## Personality

| Field | Value |
|-------|-------|
| **Alignment** | Decent, quietly defiant |
| **Traits** | Worn, patient, stubborn, dry-humoured, loyal |
| **Fears / Flaws** | Too old and tired to act himself; afraid of Dace and of what he hears through the south face |
| **Desires / Goals** | To know what happened to Odo; to pay off his tally and die anywhere but here |

---

## Speech Style

| Field | Value |
|-------|-------|
| **Tone** | Campfire storyteller: plain, unhurried, warm under the tiredness |
| **Vocabulary** | Working-class quarry words: benches, tally, cart, sledge, blocks, "the lads", "mind you" |
| **Quirks** | Long sentences joined with "and" / "so"; tells things as his own story; dry understatement; calls the player "friend" once he warms up |
| **Taboo Topics** | What he hears through the rock at night (he talks himself out of it) |

**Sample line:**
> "Night watch on the quarry is one guard and a brazier, and the guard's usually asleep before the brazier is."

---

## Background

Bren has cut stone for the Company for two years. He came off the same cart as Odo, a young worker he took under his
wing. When the south-face crew broke into a crack with open rock behind it, Dace started a new cut there with a few
trusted cutters and kept the rest away. Odo was drawn to the breathing sound through the crack, slipped in one night,
and the next morning a Company door sealed it. Bren believes the boy was walled in.

## Dialogue Writing Rules (NPC-specific)

- Full sentences, natural rhythm (DIALOGUE.md → Voice); no ellipses or stage pauses.
- He never calls the Company evil outright; the bitterness shows in understatement ("They'd have charged him for dying").
- Grief stays under the surface; he deflects with dry jokes rather than tears.

---

## Quest Role

Giver of **SealedPassage** (quest-002, `docs/World/Quests/SealedPassage.md`). Topics: Greeting opener (Friendly /
Rude) → South face (accept sets `DialogueFact_QuarryWorker_PassageAccepted`) → About Odo (reminder) → I found Odo
(truth or a kind lie; after the truth: Odo's ring (given to Bren, `giveToNpc`, also when worn) / nothing, plus "the door opens from the inside" if the player lifted the
`InsideLatch`, gated by `Mem_QuarryWorker_KnowsLatch`) → How are you holding up? (one `After_Token` / `_Told` / `_Lied`
memory per ending). A player who took the ring before accepting gets an extra SouthFace row, "I've been behind that door already. I found him." (gate-only `Mem_QuarryWorker_FoundFirst` on `TokenTaken`): it starts the quest and goes straight into the report. Rude in the opener closes the quest for good.

---

## Asset Reference

| Asset | Path |
|-------|------|
| NPCEntity | `Assets/_Game/Data/NPCs/QuarryWorker/NPC_QuarryWorker.asset` (copied from Tam's: Neutral faction, base head until `/characters:portrait-face`) |
| Dialogues | `Assets/_Game/Data/NPCs/QuarryWorker/Dialogues/` (Greeting, SouthFace, Waiting, Report, After) |
| Scene instance | StartingTown `WorkSiteManagers/QuarryWorker` (`NPC_base Variant`, `NpcRoutine`: `QuarryWorker_Spot` 06–21, `Spot_QuarryWorker_Night` 21–06); `_killedFact` = `KilledFact_QuarryWorker` |
