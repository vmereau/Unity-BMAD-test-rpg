# CLAUDE.md — Assets/_Game/Data/NPCs/BeatenWorker

> Loaded when Claude accesses this NPC's data. Character sheet for Tam — read it before writing any dialogue, memory or behaviour for this NPC.

---

## Core Identity

| Field | Value |
|-------|-------|
| **Name** | Tam |
| **Nickname / Known As** | Tam |
| **Profession** | Long-time laborer, social informant (npc-002) |
| **Faction / Affiliation** | Unaffiliated |
| **Location** | Player's Zone, by the campfire |
| **Age** | Late 30s |

---

## Personality

| Field | Value |
|-------|-------|
| **Alignment** | Gentle neutral |
| **Traits** | Tired, gentle, observant, quietly kind |
| **Fears / Flaws** | Too worn out to stand up for himself; expects nothing from anyone |
| **Desires / Goals** | Get through the day; see decent people stay out of trouble |

---

## Speech Style

| Field | Value |
|-------|-------|
| **Tone** | Soft, weary, friendly. Grateful for small kindness |
| **Vocabulary** | Simple working words; talks about people more than places |
| **Quirks** | Worries about the player before himself; plays down his own hurt with a small tired joke; never asks outright for help, he says what he'd be grateful for; hesitates (one "...") only when he is hurt |
| **Taboo Topics** | None. He just goes quiet when hurt |

**Sample line:**
> "It's not the first time and I doubt it'll be the last, so don't fret over me. I'll sit here a while and let the ground stop moving, and then I'll be fine."

---

## Background

Tam has been in the camp for years and is simply worn out. The beatings took his strength, not his decency: he is kind to anyone who is kind to him, and too tired to be cruel to anyone who isn't. Full design: `docs/World/NPCs/npc-002-beaten-worker.md`. The arrival beating that introduces him comes in a later spec, so for now he is simply present.

---

## Dialogue Writing Rules (NPC-specific)

- Never dramatic, never sneers, never threatens; never asks outright for help
- Tired but friendly: plain, warm sentences that run on a little when he is grateful; he thanks people and worries about them
- When mocked or hurt he withdraws, sad and tired ("...Maybe. I'm too tired to argue with you."), never retaliates; that hesitation is his one ellipsis per topic
- Covers the who and why of the camp; leaves logistics to Hobb
- Points toward things, never assigns them: he is not a quest giver

## Topic Flow (gating)

| Topic | Visible when |
|-------|--------------|
| `Greeting/` "Are you alright?" (opener) | until `DialogueFact_BeatenWorker_OfferedHelp` or `_Mocked` is set. Choices: help (sets OfferedHelp), mock (sets Mocked), leave (no fact) |
| `CampPeople/` "Who should I watch out for?" | OfferedHelp, not Mocked |
| `StashHint/` "Is there really nothing I can do for you?" | OfferedHelp, not Mocked, not `DialogueFact_HiddenStash_Asked`. The start node sets Asked at chain end (quest start, either answer); the "I don't have any money for food." choice also sets `DialogueFact_HiddenStash_Hint` (Brann's chest) |
| `Feed/` "I brought you something to eat." | Asked, not FedFood / FedMeal / Mocked. "Here, this should help." = Give window for ReadyFood (Apple / Bread) → `DialogueFact_HiddenStash_FedFood` (+50 XP); "Here, I've got something even better than you hoped for." = Meal, hidden without a meal → `_FedMeal` (+100 XP, kept for later content); "Not yet."; "I don't have any money for food." → Hint + the chest lines. Either Fed fact completes the quest; the item ends in Tam's inventory |
| `StashReported/` "You were right about Brann's supplies..." | Asked + Hint + `WorldFact_HiddenStash_Taken`, not Reported / Mocked. Side line: +25 XP (`PlayerReward_HiddenStash_Reported`), does not complete the quest |
| `BarnTally/` "Pell's grain ends up with the gang. Know who buys it?" | OfferedHelp + TitheBarnShortfall `DialogueFact_FarmReeve_Shortfall_NeedProof`, not `_BeatenWorker_BarnTally` / `WorldFact_GangStash_TallyTaken` / quest Completed / Failed / Mocked. Free lead: Brann chalks every purchase on slips in his stash chest (he points, he warns "hands off") |
| `Cold/` "Got a moment?" → "...Not really, no. I'm tired. Leave me be." (repeatable) | Mocked |

---

## Gameplay Notes (event-001)

- Beaten unconscious by the Warden on the stage during the arrival (`ArrivalSequence`). Not talkable until
  `WorldFact_Arrival_Done` (`NPCPresence._availableAfter`); then he gets up and walks to the campfire (all-day routine,
  `Spot_BeatenWorker_Campfire`).
- **Passive** (`_detectionRange` 0): he never fights back, against the Warden or the player. `_baseHealth` 75 (a deliberate
  T0 band exception, `_bmad-output/balance.md`) so the Warden's unarmed hits (30) knock him out in 3 blows.
- **Player relationship:** Neutral (Faction_Neutral) until fed — `_playerRelationship` Friendly while
  `DialogueFact_HiddenStash_FedFood` or `_FedMeal` is set (spec-player-relationship-tiers). Friendly + passive: hitting
  him only raises the "What are you doing?!" warn bubble; he never assists or retaliates.
