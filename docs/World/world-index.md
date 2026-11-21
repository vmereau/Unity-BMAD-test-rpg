# World Index — Echoes of the Fallen

> Last updated: 2026-11-15
> Source of truth for all world entities. Read this first before any design or implementation task.
> Full docs at `docs/World/`. Quest specs at `docs/World/Quests/`. Unity assets at `Assets/_Game/Data/`.

---

## Areas

| ID | Name | Region | Status | Key NPCs | Key Quests |
|----|------|--------|--------|----------|------------|
| area-001 | Company Encampment | Frontier | draft | npc-001..019 | quest-001, quest-002, WardensErrand, TitheBarnShortfall |

---

## NPCs

| ID | Name | Location | Role | Faction | Status |
|----|------|----------|------|---------|--------|
| npc-001 | Tent Mate (Old Hobb) | area-001 | Logistics informant (existing narrative NPC) | none | graybox |
| npc-002 | Beaten Worker (Tam) | area-001 | Social informant | none | graybox |
| npc-003 | Brann (Stash Owner) | area-001 gang house | Worker with leverage | company-adjacent | implemented |
| npc-004 | Independent Hunter | wilderness / area-001 edge | Independent path contact | faction-003 | stub |
| npc-005 | Worker Leader | area-001 | Hidden rebel leader | faction-002 | stub |
| npc-006 | The Warden (Voss) | area-001 | Main antagonist / camp authority | faction-001 | graybox |
| npc-007 | Racket Guard (Harl) | area-001 exits (quarry gate 06–14, wilderness gate 14–21) | Antagonist / extortionist — one-time exit toll per gate | faction-001 | implemented |
| npc-008 | The False Friend | area-001 | Antagonist / betrayer | none | stub |
| npc-009 | Quarry Worker (Bren) | area-002 (quarry) | Quest giver — sealed passage | none | placed (StartingTown quarry floor) |
| npc-010 | Lead Hunter (Corvin Ash) | area-004 (hunting camp) | Work site manager; independent-minded | faction-001 (loose) | graybox |
| npc-011 | Camp Cook (Aldric Sorn) | area-001 — Canteen | Service NPC / information broker | none (covert) | draft |
| npc-012 | Warehouse Guard | area-001 — dock warehouse | Guard / restricted-area owner | faction-001 | draft |
| npc-013 | Dock Guard | area-001 — dock guard post / docks | Guard / restricted-area owner (night) | faction-001 | draft |
| npc-014 | Overseer Dace | area-002 (quarry floor) | Quarry manager | faction-001 | graybox |
| npc-015 | Gorm Holl | area-003 (wood camp) | Wood camp gang boss | none | graybox |
| npc-016 | Reeve Wendel Pell | Company farm (manager's house) | Farm manager | faction-001 | graybox |
| npc-017 | Osric Venn (Barn Guard) | area-001 fields gate / barracks / tithe barn (00–03) | Grain thief, TitheBarnShortfall; mafia-route hook | faction-001 (covert gang supplier) | placed (StartingTown) |
| npc-018 | Pim (Field Hand) | Company farm strips / tent camp | Lead + scapegoat, TitheBarnShortfall | none | placed (StartingTown) |
| npc-019 | Gerrit (Barn Worker) | Company farm threshing floor / tent camp | Lead, TitheBarnShortfall | none | placed (StartingTown) |
| npc-020 | Tob (Kitchen Hand) | area-001 — Canteen cookhouse | Cooking tutor, cooking quest chain | none | draft |

---

## Quests

| ID | Name | Giver | Area | Faction Path | Status |
|----|------|-------|------|-------------|--------|
| quest-001 | The Hidden Stash | npc-002 (informant) | area-001 | neutral | implemented |
| quest-002 | The Sealed Passage (`SealedPassage`) | npc-009 | area-002 | worker / independent | implemented (door route, Dace's key, goat track) |
| TitheBarnShortfall | Counted Twice (`TitheBarnShortfall`) | npc-016 | area-001 (farm) | Company / mafia hook | implemented |
| WardensErrand | The Warden's Errand | npc-006 (Voss) | area-001 + work sites | neutral | implemented |

---

## Events

| ID | Name | Area | Trigger | Status |
|----|------|------|---------|--------|
| event-001 | Arrival Beating | area-001 | Scripted — player escort on arrival | implemented: part 1 (`spec-area-1-arrival-escort-beating`: gate → stage escort, beating), part 2 (`spec-area-1-arrival-hut-briefing`: escort to the hut, Hobb's briefing); stripped beat redesigned as player origins (`docs/World/player-origins.md`, design) |
| event-002 | Workers' Revolt | area-001 | Player-triggered — worker path ending | stub |
| event-003 | Company Crackdown | area-001 | Player-triggered — company path ending | stub |

---

## Factions

| ID | Name | Alignment | Status |
|----|------|-----------|--------|
| faction-001 | The Company | Antagonist | stub |
| faction-002 | Workers' Ring (Rebels) | Allied / player-joinable | stub |
| faction-003 | Independents | Neutral / player-joinable | stub |

---

## Work Sites (Sub-Areas of Area 001)

| ID | Name | Type | Manager | Status |
|----|------|------|---------|--------|
| area-002 | Stone Quarry | Work site | npc-014 (Overseer Dace) | stub |
| area-003 | Wood Camp | Work site | npc-015 (Gorm Holl) | stub |
| area-004 | Hunting Grounds | Work site | npc-010 (Lead Hunter) | stub |
