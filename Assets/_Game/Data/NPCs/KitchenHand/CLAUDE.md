# CLAUDE.md — Assets/_Game/Data/NPCs/KitchenHand

> Loaded when Claude accesses this NPC's data. Character sheet for Tob (Canteen Kitchen Hand) — read it before writing any dialogue, memory or behaviour for this NPC.

---

## Core Identity

| Field | Value |
|-------|-------|
| **Name** | Tob |
| **Nickname / Known As** | "Tob", "the kitchen hand" |
| **Profession** | Kitchen hand at the canteen — does most of the real cooking |
| **Faction / Affiliation** | Company worker (indentured, like the rest of the camp); works under Aldric Sorn (Cook) |
| **Location** | The canteen cookhouse, Alderath Camp worker area (fireplace, kitchen garden) |
| **Age** | Early twenties |

---

## Personality

| Field | Value |
|-------|-------|
| **Alignment** | Decent and pragmatic — helps people eat well, keeps out of trouble, loyal to Aldric |
| **Traits** | Eager, friendly, knowledgeable about food, observant, discreet |
| **Fears / Flaws** | Fears the guards and losing his place in the kitchen; gets carried away talking about food |
| **Desires / Goals** | Pay off his Company contract and one day run a kitchen of his own |

---

## Speech Style

| Field | Value |
|-------|-------|
| **Tone** | Lively, warm, a little cheeky |
| **Vocabulary** | Simple camp speech, precise kitchen words (herbs, cuts, what a dish does for the body) |
| **Quirks** | Talks while working (stirring, peeling, tasting); compares people's health to what they eat; goes vague and changes the subject when anything touches Aldric's affairs |
| **Taboo Topics** | Aldric's business, contacts and past — what Aldric keeps under the pots |

**Sample line:**
> "Same gruel every day and the body forgets how to be strong. Eat something new and it remembers — you'll feel it the first time, I promise."

---

## Background

Tob was signed into a Company contract young to cover a family debt and ended up scrubbing pots at the
canteen. Aldric saw he had a feel for food and taught him to cook. Now in his early twenties, Tob does most of
the real kitchen work while Aldric "sees to the guests" — and he has learned not to ask what that means.

## Dialogue Writing Rules (NPC-specific)

- He is the player's introduction to cooking: lines about food, recipes and meals are open, clear and helpful.
- Explain meal first-eat bonuses in-world, never in game terms: a varied diet is good for the body, the first
  time you eat a new dish it does you the most good. No "permanent bonus", no stat names.
- **Never explicit about Aldric's knowledge or business** — no confirmation, no hints, no pointing the player to
  Aldric for "other things". If asked, he deflects to food or work ("He cooks, I cook. That's all I know.").
- He encourages trying things: good cooks find dishes by mixing ingredients, not only from written recipes.
- Short, energetic sentences; no long speeches.

---

## Asset Reference

| Asset | Path |
|-------|------|
| Scene | `StartingTown.unity` root `KitchenHand` (NPC_base Variant) at (-10.6, 22.3), between the cookhouse counter and `CookingStation_Fireplace` (-9.4, 21.2) |
| Routine | 05–07 `NpcRoutineSpots/Spot_KitchenHand_Garden` (farm kitchen garden, (-31, -64)), 07–21 his spawn spot at the fire, 21–05 Sleep in `TentCamp/Canteen/KitchenHandBed` (pallet inside the cookhouse, `Ownership` = Tob). Wake hour 05:00 (daily ration reset) |
| Quest role | Cooking tutorial chain `WorkersRation` → `WorkersSandwich` → `CooksExperiment` (specs in `docs/World/Quests/`; `WorkersRation` implemented, the other two `ready`): first-meal effect, cooking a given recipe, discovering a dish by experimenting. Also hands out the daily soup ration (2 per day). Topics: `NewHere` (first topic, starts `WorkersRation`), `DailyRation` (permanent after the offer), `AboutTheSoup` (turn-in) |
| KilledFact | `Data/Enemies/StartingTown/Generic/KilledFact_KitchenHand.asset` (scene `PersistentID` and `NPC_KitchenHand._killedFact`) |
