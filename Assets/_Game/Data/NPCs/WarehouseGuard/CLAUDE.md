# NPC Identity — Warehouse Guard

> This file is the canonical character sheet for this NPC. It is the instantiated version of
> `NPC_IDENTITY_TEMPLATE.md`. Read it before writing any dialogue, memory, or behavioural asset
> for this NPC. Keep it up to date as the character develops.
>
> **Draft (2026-11-09):** created as the test owner of `RestrictedAreas/RA_Warehouse`
> (`spec-restricted-areas-trespass`). Identity fields marked TBD are still open.

---

## Core Identity

| Field | Value |
|-------|-------|
| **Name** | Warehouse Guard (personal name TBD) |
| **Nickname / Known As** | "the warehouse man" |
| **Profession** | Company guard posted on the dock warehouse (Storage Hut) |
| **Faction / Affiliation** | The Company (`Faction_Company`) |
| **Location** | Dock warehouse, built into the north palisade above the docks (npc-012) |
| **Age** | <!-- TBD --> |

---

## Personality

| Field | Value |
|-------|-------|
| **Alignment** | <!-- TBD --> |
| **Traits** | Dutiful, territorial, bored |
| **Fears / Flaws** | <!-- TBD — e.g. a shortfall in the tally being blamed on him --> |
| **Desires / Goals** | <!-- TBD --> |

---

## Speech Style

| Field | Value |
|-------|-------|
| **Tone** | Curt, warning first |
| **Vocabulary** | Plain, orders rather than sentences |
| **Quirks** | <!-- TBD --> |
| **Taboo Topics** | What is stored in the warehouse |

**Sample line:**
> "This area is off-limits."

---

## Background

<!-- TBD — 2–4 sentences: origin, key event, current situation. -->

## Behaviour

- Patrols `NpcPatrolRoutes/WarehouseGuard`: camp ramp → camp door → west end → river landing → east end (4 s waits).
  The stops outside each door leave the interior unwatched — the intended sneak window.
- Sole entity owner of `RA_Warehouse` (always restricted). Warning / chase / scold lines come from
  `WitnessProfile_Humanoid` (`Barks_TrespassAlert`, `Barks_TrespassScold`). Being `Faction_Company`, he also owns
  `RA_Docks` at night.

## Dialogue Writing Rules (NPC-specific)

> These rules extend `DIALOGUE.md` for this NPC specifically.

- <!-- TBD -->

---

## Asset Reference

| Asset | Path |
|-------|------|
| NPCEntity | `Assets/_Game/Data/NPCs/WarehouseGuard/NPC_WarehouseGuard.asset` |
| KilledFact | `Assets/_Game/Data/Enemies/StartingTown/Generic/KilledFact_WarehouseGuard.asset` |
| Dialogues | `Assets/_Game/Data/NPCs/WarehouseGuard/Dialogues/` |
| Memories | `Assets/_Game/Data/NPCs/WarehouseGuard/Memories/` |
| Scene instance | `StartingTown.unity` → `WarehouseGuard` (`NPC_base Variant`) |
