# NPC Identity — Company Guard (arrival escort)

> This file is the canonical character sheet for this NPC. It is the instantiated version of
> `NPC_IDENTITY_TEMPLATE.md`. Read it before writing any dialogue, memory, or behavioural asset
> for this NPC. Keep it up to date as the character develops.
>
> **Draft (2026-11-12):** created for event-001 (`spec-area-1-arrival-escort-beating`). Two nameless rank-and-file
> guards share this identity; each has its own entity asset so each keeps its own `KilledFact`.

---

## Core Identity

| Field | Value |
|-------|-------|
| **Name** | Company Guard (nameless) |
| **Nickname / Known As** | "the gate men" |
| **Profession** | Rank-and-file Company guards posted at the arrival gate; they escort new arrivals to the stage |
| **Faction / Affiliation** | The Company (`Faction_Company`) |
| **Location** | Each side of the arrival gate, all day |
| **Age** | Twenties to forties |

---

## Personality

| Field | Value |
|-------|-------|
| **Alignment** | Lawful neutral — following orders, no more |
| **Traits** | Bored, curt, used to herding people |
| **Fears / Flaws** | The Warden noticing them doing a poor job |
| **Desires / Goals** | Get the new meat to the square and get back to standing still |

---

## Speech Style

| Field | Value |
|-------|-------|
| **Tone** | Flat orders |
| **Vocabulary** | Short imperatives; calls arrivals "new meat" |
| **Quirks** | Never explains anything |
| **Taboo Topics** | None — they don't talk |

**Sample line:**
> "Keep up."

---

## Dialogue Writing Rules (NPC-specific)

- Barks only (no topics yet): move / wait / stray lines in `Barks_Arrival_*`
- Never answer questions; the Warden does the talking

---

## Asset Reference

| Asset | Path |
|-------|------|
| NPCEntity (left / right) | `NPC_CompanyGuard_ArrivalLeft.asset`, `NPC_CompanyGuard_ArrivalRight.asset` (copied from `NPC_WarehouseGuard`) |
| KilledFacts | `Data/Enemies/StartingTown/Generic/KilledFact_CompanyGuard_Arrival{Left,Right}` |
| Barks | `Barks_Arrival_Move`, `Barks_Arrival_Wait`, `Barks_Arrival_Stray` (this folder; referenced by `Data/World/ArrivalSequence_Event001`) |
| Prefab | Scene instances `CompanyGuard_ArrivalLeft` / `Right` of `NPC_base Variant.prefab` — plain body (NPCs can't wear the Company armor visual yet) |
