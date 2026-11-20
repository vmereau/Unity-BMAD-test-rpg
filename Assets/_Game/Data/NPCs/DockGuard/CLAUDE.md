# CLAUDE.md — Assets/_Game/Data/NPCs/DockGuard

> Loaded when Claude accesses this NPC's data. Character sheet for Dock Guard — read it before writing any dialogue, memory or behaviour for this NPC.
>
> **Draft (2026-11-09):** created as the night watch of `RestrictedAreas/RA_Docks`
> (`spec-restricted-areas-trespass`). Identity fields marked TBD are still open.

---

## Core Identity

| Field | Value |
|-------|-------|
| **Name** | Dock Guard (personal name TBD) |
| **Nickname / Known As** | <!-- TBD --> |
| **Profession** | Company guard of the dock compound |
| **Faction / Affiliation** | The Company (`Faction_Company`) |
| **Location** | Dock guard post by the compound gate (day); the docks (night) (npc-013) |
| **Age** | <!-- TBD --> |

---

## Personality

| Field | Value |
|-------|-------|
| **Alignment** | <!-- TBD --> |
| **Traits** | Watchful, rule-bound, tired |
| **Fears / Flaws** | <!-- TBD --> |
| **Desires / Goals** | <!-- TBD --> |

---

## Speech Style

| Field | Value |
|-------|-------|
| **Tone** | Short, official, tired of saying it |
| **Vocabulary** | Plain, regulations and hours |
| **Quirks** | Quotes the closing time; says every rule as if he has said it a thousand times, because he has |
| **Taboo Topics** | What comes in by boat at night |

**Sample line:**
> "Docks are closed after dark, same as every night, and I'm not opening them for you. So, out."

---

## Background

<!-- TBD — 2–4 sentences: origin, key event, current situation. -->

## Behaviour

- `NpcRoutine`: 06–21 stands at `NpcRoutineSpots/DockGuard_GuardPost` (in front of the dock guard post, facing the
  compound gate); 21–06 home = patrol `NpcPatrolRoutes/DockGuard` (shore end → west tip → east tip → warehouse ramp
  foot, 4 s waits).
- Owns `RA_Docks` (restricted 21–06) through `Faction_Company`, like every Company NPC.

## Dialogue Writing Rules (NPC-specific)

- Every line ends on the rule: closed, out, off-limits

---

## Asset Reference

| Asset | Path |
|-------|------|
| KilledFact | `Assets/_Game/Data/Enemies/StartingTown/Generic/KilledFact_DockGuard.asset` |
| Dialogues | none yet |
| Memories | none yet |
| Scene instance | `StartingTown.unity` → `DockGuard` (`NPC_base Variant` + `NpcRoutine`) |
