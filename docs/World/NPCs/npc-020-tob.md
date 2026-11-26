# NPC 020 — Tob

> id: npc-020
> type: npc
> name: Tob
> status: draft
> location: area-001 — Canteen cookhouse
> faction: none (Company worker)
> role: cooking tutor / quest-giver
> personality: Eager, friendly, discreet
> unity-asset: Assets/_Game/Data/NPCs/KitchenHand/
> quests-given: WorkersRation → WorkersSandwich → CooksExperiment (docs/World/Quests/)
> quests-involved: TBD
> events: TBD
> knows-of: TBD

---

## Identity

Tob was signed into a Company contract young to cover a family debt and ended up scrubbing pots at the canteen.
Aldric saw he had a feel for food and taught him to cook. Now in his early twenties, Tob does most of the real
kitchen work while Aldric "sees to the guests" — and he has learned not to ask what that means.

---

## Role in Area

Cooks for the workers at the canteen. For the player, he is the introduction to cooking: discovering recipes by
experimenting with ingredients, learning them from recipe items, cooking at a station, and why eating varied meals matters (first-eat meal bonuses, explained in-world as "a varied diet is
good for the body"). He gives a short quest chain that teaches these systems. He keeps Aldric's fence business
entirely out of his talk.

---

## Personality

**Tone:** Lively, warm, a little cheeky
**Vocabulary:** Simple camp speech, precise kitchen words
**Quirks:** Talks while working; compares people's health to what they eat; goes vague about Aldric's affairs
**Taboo topics:** Aldric's business, contacts and past
**Sample line:** "Same gruel every day and the body forgets how to be strong. Eat something new and it remembers — you'll feel it the first time, I promise."

---

## Relationships

| NPC | Relationship |
|-----|-------------|
| Aldric Sorn (npc-011) | His master in the kitchen; loyal, asks no questions |

---

## State Changes

| Condition | Change |
|-----------|--------|
| (fill as known) | |

---

## Dialogue Topics (to be written)

| Topic ID | Trigger | Summary |
|----------|---------|---------|
| NewHere | First talk | Starts `WorkersRation`, opens the daily ration |
| DailyRation | After NewHere, permanent | Two bowls of Worker's Soup per day, reset when he wakes; refused if woken |
| AboutTheSoup | Soup eaten | Explains the first-meal effect in-world, completes `WorkersRation` |
| SomethingToCarry | `WorkersRation` done | Starts `WorkersSandwich`, gives his sandwich recipe (or "I already know this meal") |
| TheSandwich | Sandwich cooked | Completes `WorkersSandwich` |
| MixingThings | `WorkersSandwich` done | Starts `CooksExperiment` with a hint (or "I've already found a dish") |
| FoundADish | A meal discovered by experimenting | Completes `CooksExperiment`, mentions the written recipes he sells |
| Shop | `WorkersRation` done, permanent | "What do you sell?" — opens his shop: raw food (apple, bread, brown mushroom, trout, onion, salt pork) and the two sandwich recipe items (moved from the Storekeeper) |

---

## Open Threads

- Cooking quest chain designed (specs `ready`), not implemented: blocked on a code spec (meal-eaten / crafted
  facts, meal-discovered fact, daily ration give) and new items (soup, onion, salt pork, sandwich, forager's stew)
- Not placed in StartingTown; no `KilledFact`, head or skin tone yet
