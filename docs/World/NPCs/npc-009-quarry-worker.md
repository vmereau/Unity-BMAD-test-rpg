# NPC 009 — Quarry Worker

> id: npc-009
> type: npc
> name: Bren
> status: draft
> location: area-002 — Stone Quarry floor by day, tent camp at night
> faction: none
> role: quest-giver
> personality: worn, stubborn, dry-humoured
> unity-asset: Assets/_Game/Data/NPCs/QuarryWorker/
> quests-given: quest-002 (SealedPassage)
> quests-involved: quest-002
> events: none
> knows-of: npc-014 (Overseer Dace), Odo (missing worker, behind the south-face door), the hand-picked cutters "moved up to the fort"

---

## Identity

A quarryman two years into his Company debt. He came off the same cart as Odo, a young worker he looked after. When
Odo slipped through the crack in the south face the night before the Company sealed it with a door, Bren was left with
the boy's debt on his own tally and no answer.

---

## Role in Area

Works the quarry floor. The player's way into quest-002: he knows the three ways past the seal (pick the simple lock,
take Dace's key from his hut, or the goat track from the wilderness) but is too old and too watched to go himself.

---

## Personality

**Tone:** campfire storyteller, plain and unhurried
**Vocabulary:** quarry work words (benches, tally, cart, sledge), "the lads", "mind you"
**Quirks:** long sentences joined with "and" / "so"; dry understatement; grief hidden under jokes
**Taboo topics:** what he hears through the rock at night
**Sample line:** "Night watch on the quarry is one guard and a brazier, and the guard's usually asleep before the brazier is."

---

## Relationships

| NPC | Relationship |
|-----|-------------|
| Odo | Young worker he looked after; missing behind the south-face door |
| npc-014 Overseer Dace | Fears him; Dace wrote Odo off as a runaway and put his debt on Bren's tally |

---

## State Changes

| Condition | Change |
|-----------|--------|
| Rude answer in the greeting | Closes the south-face topic for good (quest missed) |
| Quest accepted | "About Odo." reminder topic |
| Odo's ring taken | "I found Odo." report topic; before accepting, also a SouthFace row "I found him already" that starts the quest and opens the report |
| Quest completed | "How are you holding up?" |

---

## Dialogue Topics

| Topic ID | Trigger | Summary |
|----------|---------|---------|
| Greeting | After the arrival | Opener; Friendly / Rude facts |
| SouthFace | Friendly | The new cut, Odo, the door; accept starts quest-002 |
| Waiting | Quest started | Reminder of the three routes |
| Report | Ring taken | Truth (ring given to Bren / nothing / latch line) or kind lie; completes quest-002 |
| After | Quest completed | Still hears the breathing |

---

## Open Threads

- Portrait face (`/characters:portrait-face`); placed in StartingTown as `WorkSiteManagers/QuarryWorker`
- The hand-picked cutters "moved up to the fort" — unanswered hook
