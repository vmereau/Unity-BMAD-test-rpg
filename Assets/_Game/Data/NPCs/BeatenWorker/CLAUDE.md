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
> "You get used to it. Well — most people do."

---

## Background

Tam worked out how little he can do before the Warden beats him, and stopped pretending otherwise. Full design: `docs/World/NPCs/npc-002-beaten-worker.md`. The arrival beating that introduces him comes in a later spec, so for now he is simply present.

---

## Dialogue Writing Rules (NPC-specific)

> These rules extend `DIALOGUE.md` for this NPC specifically.

- Never dramatic, never asks for help
- Covers the who and why of the camp; leaves logistics to Hobb
- Points toward things, never assigns them: he is not a quest giver

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
