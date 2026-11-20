# CLAUDE.md — Assets/_Game/Data/NPCs/BarnWorker

> Loaded when Claude accesses this NPC's data. Character sheet for Gerrit — read it before writing any dialogue, memory or behaviour for this NPC.

---

## Core Identity

| Field | Value |
|-------|-------|
| **Name** | Gerrit |
| **Nickname / Known As** | Gerrit, "old Gerrit" |
| **Profession** | Field-gang hand: threshing floor and barn loading under Pell's eye |
| **Faction / Affiliation** | None (`Faction_Neutral`) |
| **Location** | Threshing floor by day, tent-camp pallet near Pim at night |
| **Age** | Early 60s |

---

## Personality

| Field | Value |
|-------|-------|
| **Alignment** | Shrewd survivor, quietly decent |
| **Traits** | Dry, unhurried, shrewd, patient, careful |
| **Fears / Flaws** | Naming names: in this camp it gets the worker killed, never the man named |
| **Desires / Goals** | Outlast one more reeve; keep the gang out of trouble |

---

## Speech Style

| Field | Value |
|-------|-------|
| **Tone** | Dry, unhurried, thinks out loud |
| **Vocabulary** | Threshing, sacks, the gang, reeves; plain and exact |
| **Quirks** | Reaches answers by ruling things out ("Think about it."); "mind" as a softener; stops just short of a name |
| **Taboo Topics** | Names. He describes the guard without ever naming him |

**Sample line:**
> "Thresh it, sack it, carry it, and let Pell count it. Third reeve I've done that for. They change, the sacks don't."

---

## Background

Gerrit has worked the Company farm longer than anyone on the gang and has outlasted three reeves. He loads the tithe
barn and knows its routine as well as Pell does, which is why he knows the thief isn't a worker.

## Dialogue Writing Rules (NPC-specific)

- Never names Venn or any guard; he describes and stops.

---

## Quest Role — TitheBarnShortfall

Lead (optional step 0): `BarnTalk` sets `DialogueFact_BarnWorker_BarnTalk` and points at a guard without naming him.

Topics: `Greeting`, `BarnTalk` (one-shot).

---

## Asset Reference

| Asset | Path |
|-------|------|
| Scene | StartingTown `TitheBarnShortfall/BarnWorker_Gerrit` (NPC_base Variant instance). Routine: 06–21 Stand `Spot_BarnWorker_Threshing`, 21–06 Sleep `TitheBarnShortfall/BarnWorkerBed` (next to Pim's) |
