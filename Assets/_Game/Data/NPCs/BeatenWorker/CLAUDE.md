# NPC Identity — Tam

> This file is the canonical character sheet for this NPC. It is the instantiated version of
> `NPC_IDENTITY_TEMPLATE.md`. Read it before writing any dialogue, memory, or behavioural asset
> for this NPC. Keep it up to date as the character develops.

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
| **Vocabulary** | Simple, working-class, short sentences |
| **Quirks** | Trails off ("..."); worries about the player before himself; never asks outright for help |
| **Taboo Topics** | None. He just goes quiet when hurt |

**Sample line:**
> "It's not the first time. I'll be fine, I just need to sit a while."

---

## Background

Tam has been in the camp for years and is simply worn out. The beatings took his strength, not his decency: he is kind to anyone who is kind to him, and too tired to be cruel to anyone who isn't. Full design: `docs/World/NPCs/npc-002-beaten-worker.md`. The arrival beating that introduces him comes in a later spec, so for now he is simply present.

---

## Dialogue Writing Rules (NPC-specific)

> These rules extend `DIALOGUE.md` for this NPC specifically.

- Never dramatic, never sneers, never threatens; never asks outright for help
- Tired but friendly: short, warm sentences; he thanks people and worries about them
- When mocked or hurt he withdraws, sad and tired ("...Maybe. I'm too tired to argue."), never retaliates
- Covers the who and why of the camp; leaves logistics to Hobb
- Points toward things, never assigns them: he is not a quest giver

## Topic Flow (gating)

| Topic | Visible when |
|-------|--------------|
| `Greeting/` "Are you alright?" (opener) | until `DialogueFact_BeatenWorker_OfferedHelp` or `_Mocked` is set. Choices: help (sets OfferedHelp), mock (sets Mocked), leave (no fact) |
| `CampPeople/` "Who should I watch out for?" | OfferedHelp, not Mocked |
| `StashHint/` "Is there really nothing I can do for you?" | OfferedHelp, not Mocked, not `DialogueFact_HiddenStash_Asked`. The start node sets Asked at chain end (quest start, either answer); the "I don't have any money for food." choice also sets `DialogueFact_HiddenStash_Hint` (Brann's chest) |
| `Feed/` "I brought you something to eat." | Asked, not FedFood / FedMeal / Mocked. "Here, this should help." = Give window for ReadyFood (Apple / Bread) → `DialogueFact_HiddenStash_FedFood` (+50 XP); "Here, I have even better than what you were asking for." = Meal, hidden without a meal → `_FedMeal` (+100 XP, kept for later content); "Not yet."; "I don't have any money for food." → Hint + the chest lines. Either Fed fact completes the quest; the item ends in Tam's inventory |
| `StashReported/` "You were right about Brann's supplies..." | Asked + Hint + `WorldFact_HiddenStash_Taken`, not Reported / Mocked. Side line: +25 XP (`PlayerReward_HiddenStash_Reported`), does not complete the quest |
| `Cold/` "Got a moment?" → "...Not really. I'm tired. Leave me be." (repeatable) | Mocked |

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

---

## Asset Reference

| Asset | Path |
|-------|------|
| NPCEntity | `Assets/_Game/Data/NPCs/BeatenWorker/NPC_BeatenWorker.asset` |
| Dialogues | `Assets/_Game/Data/NPCs/BeatenWorker/Dialogues/` |
| Memories | `Assets/_Game/Data/NPCs/BeatenWorker/Memories/` |
| Prefab | Scene instance of `Assets/_Game/Prefabs/Entities/Humanoids/NPC_base Variant.prefab` (no dedicated prefab) |
