# NPC Identity — Dock Guard

> This file is the canonical character sheet for this NPC. It is the instantiated version of
> `NPC_IDENTITY_TEMPLATE.md`. Read it before writing any dialogue, memory, or behavioural asset
> for this NPC. Keep it up to date as the character develops.
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
| **Tone** | Short, official |
| **Vocabulary** | Plain, regulations |
| **Quirks** | <!-- TBD --> |
| **Taboo Topics** | <!-- TBD --> |

**Sample line:**
> "Docks are closed after dark. Out."

---

## Background

<!-- TBD — 2–4 sentences: origin, key event, current situation. -->

## Behaviour

- `NpcRoutine`: 06–21 stands at `NpcRoutineSpots/DockGuard_GuardPost` (in front of the dock guard post, facing the
  compound gate); 21–06 home = patrol `NpcPatrolRoutes/DockGuard` (shore end → west tip → east tip → warehouse ramp
  foot, 4 s waits).
- Owns `RA_Docks` (restricted 21–06) through `Faction_Company`, like every Company NPC.

## Dialogue Writing Rules (NPC-specific)

> These rules extend `DIALOGUE.md` for this NPC specifically.

- <!-- TBD -->

---

## Asset Reference

| Asset | Path |
|-------|------|
| NPCEntity | `Assets/_Game/Data/NPCs/DockGuard/NPC_DockGuard.asset` |
| KilledFact | `Assets/_Game/Data/Enemies/StartingTown/Generic/KilledFact_DockGuard.asset` |
| Dialogues | `Assets/_Game/Data/NPCs/DockGuard/Dialogues/` |
| Memories | `Assets/_Game/Data/NPCs/DockGuard/Memories/` |
| Scene instance | `StartingTown.unity` → `DockGuard` (`NPC_base Variant` + `NpcRoutine`) |
