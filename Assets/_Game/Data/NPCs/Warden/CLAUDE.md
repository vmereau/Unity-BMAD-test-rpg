# CLAUDE.md — Assets/_Game/Data/NPCs/Warden

> Loaded when Claude accesses this NPC's data. Character sheet for Warden Voss — read it before writing any dialogue, memory or behaviour for this NPC.

---

## Core Identity

| Field | Value |
|-------|-------|
| **Name** | Warden Voss |
| **Nickname / Known As** | the Warden |
| **Profession** | Camp overseer for the Company (npc-006) |
| **Faction / Affiliation** | The Company |
| **Location** | Company house on the outcrop; camp center by day |
| **Age** | Late 40s |

---

## Personality

| Field | Value |
|-------|-------|
| **Alignment** | Lawful cruel — the rules are his tool, not his belief |
| **Traits** | Authoritarian, rude, calculating, publicly brutal |
| **Fears / Flaws** | Bigger Company men above him; needs the camp's numbers to look good |
| **Desires / Goals** | Maximize output, keep control, be noticed by the Company |

---

## Speech Style

| Field | Value |
|-------|-------|
| **Tone** | Contemptuous, calm, final |
| **Vocabulary** | The ledger's words: debt, output, cost. Talks about people as numbers |
| **Quirks** | Never says please or thanks; never repeats himself; states outcomes as facts; speaks in whole, unhurried sentences, a man who never needs to raise his voice |
| **Taboo Topics** | Any suggestion that the workers were deceived |

**Sample line:**
> "Medicine costs coin, and coin goes on your debt. If you'd rather not owe for it, don't bleed."

---

## Background

Voss runs Alderath Camp for the Company. He uses punishment as theater: every beating at the center stage is staged for the people watching. Not a final boss — a mid-tier bully who feels enormous when the player has nothing.

---

## Dialogue Writing Rules (NPC-specific)

- Orders, never requests. Refusing him gets contempt, not anger
- Refers to the player's debt whenever money comes up
- No exposition: he assumes workers already know their place

---

## Gameplay Notes

- event-001: snapped to `ArrivalSequence/WardenStageSpot` for the arrival; forced topic `Dialogues/Arrival/Start_Warden_ArrivalSpeech`,
  then beats Tam; closing bubble `Barks_Warden_ArrivalClosing`. His routine resumes afterwards.
- `_engageStoppingDistance` 0.9 / `_attackRange` 1.1 (not the humanoid 1.3 / 1.5): at the default range his unarmed punches
  whiffed (known humanoid reach gap, `deferred-work.md`).
- Stripped beat (spec-player-origins): one-shot topic `Dialogues/Belongings/Start_Warden_Belongings` ("Where are my
  belongings?") via `Mem_Warden_Belongings`: unlocks on `WorldFact_Arrival_Done` + `WorldFact_Origin_HadBelongings` (never for
  the Wretch), invalidated by `DialogueFact_Warden_Belongings`. He denies any baggage cart exists. No quest, no log entry.
