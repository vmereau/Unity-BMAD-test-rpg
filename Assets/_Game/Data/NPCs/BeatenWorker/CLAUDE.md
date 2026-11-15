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
| **Alignment** | Resigned neutral |
| **Traits** | Sardonic, resigned, observant |
| **Fears / Flaws** | Admitting he still cares |
| **Desires / Goals** | The minimum viable existence |

---

## Speech Style

| Field | Value |
|-------|-------|
| **Tone** | Dry, deadpan, mildly amused by everything |
| **Vocabulary** | Simple, working-class, short sentences |
| **Quirks** | Underreacts to everything; asks for nothing in return |
| **Taboo Topics** | Anything that would make him admit he still cares |

**Sample line:**
> "Not my first time. I'll manage."

---

## Background

Tam worked out how little he can do before the Warden beats him, and stopped pretending otherwise. Full design: `docs/World/NPCs/npc-002-beaten-worker.md`. The arrival beating that introduces him comes in a later spec, so for now he is simply present.

---

## Dialogue Writing Rules (NPC-specific)

> These rules extend `DIALOGUE.md` for this NPC specifically.

- Never dramatic, never asks for help
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
| `Cold/` "Got a moment?" → "Not for you." (repeatable) | Mocked |

---

## Gameplay Notes (event-001)

- Beaten unconscious by the Warden on the stage during the arrival (`ArrivalSequence`). Not talkable until
  `WorldFact_Arrival_Done` (`NPCPresence._availableAfter`); then he gets up and walks to the campfire (all-day routine,
  `Spot_BeatenWorker_Campfire`).
- **Passive** (`_detectionRange` 0): he never fights back, against the Warden or the player. `_baseHealth` 25 so the
  Warden's unarmed hits (10) knock him out in 2–3 blows.

---

## Asset Reference

| Asset | Path |
|-------|------|
| NPCEntity | `Assets/_Game/Data/NPCs/BeatenWorker/NPC_BeatenWorker.asset` |
| Dialogues | `Assets/_Game/Data/NPCs/BeatenWorker/Dialogues/` |
| Memories | `Assets/_Game/Data/NPCs/BeatenWorker/Memories/` |
| Prefab | Scene instance of `Assets/_Game/Prefabs/Entities/Humanoids/NPC_base Variant.prefab` (no dedicated prefab) |
