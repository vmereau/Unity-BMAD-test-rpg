# CLAUDE.md — Assets/_Game/Data/NPCs/<NPCName>

> Loaded when Claude accesses this NPC's data. Character sheet for <NPC_NAME> — read it before writing any dialogue, memory or behaviour for this NPC.

---

## Core Identity

| Field | Value |
|-------|-------|
| **Name** | <!-- Full name, e.g. "Edric the Greysmith" --> |
| **Nickname / Known As** | <!-- How NPCs/players call them, e.g. "Edric", "the Old Smith" --> |
| **Profession** | <!-- What they do, e.g. "Blacksmith", "Town Guard", "Innkeeper" --> |
| **Faction / Affiliation** | <!-- Group they belong to, e.g. "Merchant Guild", "Iron Pact", "Unaffiliated" --> |
| **Location** | <!-- Where they are found, e.g. "Ironhold Forge, Starting Town" --> |
| **Age** | <!-- Rough age or range, e.g. "Mid-50s", "Young adult" --> |

---

## Personality

| Field | Value |
|-------|-------|
| **Alignment** | <!-- Moral/social disposition, e.g. "Neutral Good", "Lawful Gruff", or a short description --> |
| **Traits** | <!-- 3–5 adjectives, e.g. "Proud, stubborn, fair, dry-witted" --> |
| **Fears / Flaws** | <!-- What unsettles or limits them, e.g. "Afraid of the forest, holds grudges" --> |
| **Desires / Goals** | <!-- What they want, e.g. "To retire with enough coin, see his apprentice succeed" --> |

---

## Speech Style

| Field | Value |
|-------|-------|
| **Tone** | <!-- Overall register, e.g. "Gruff but fair", "Cheerful and gossipy", "Cold, formal" --> |
| **Vocabulary** | <!-- Word choice, e.g. "Working-class, simple words", "Archaic and verbose", "Military jargon" --> |
| **Quirks** | <!-- Speech habits, e.g. "Ends sentences with 'Aye'", "Rarely asks questions", "Never says 'please'" --> |
| **Taboo Topics** | <!-- Things they refuse to discuss or deflect, e.g. "Their past, the war, magic" --> |

**Sample line:**
> <!-- Write one example sentence in their voice, e.g. "Aye, I'll forge it — but don't expect it done before sundown." -->

---

## Background

<!-- 2–4 sentences. Keep it tight: origin, key event that shaped them, current situation.
     Example: "Edric spent thirty years at the capital forge before a falling beam crushed his
     left hand. He moved north to start over, cheaper land and fewer questions. He is good at
     his work and knows it." -->

## Dialogue Writing Rules (NPC-specific)

- <!-- e.g. "Never use contractions — Edric speaks in full sentences" -->
- <!-- e.g. "Do not reference magic or the gods — he is strictly secular" -->
- <!-- e.g. "Greeting topics should always feel slightly impatient, like he has work to do" -->

---

## Asset Reference

| Asset | Path |
|-------|------|
<!-- NPC-specific rows only (the folder layout and the NPC_base Variant scene instance are the default — Data/NPCs/CLAUDE.md).
     Typical rows: Scene (StartingTown path + NpcRoutine spots / hours), Shop, quest role, KilledFact, non-default prefab or entity name.
     Drop the section if there is nothing NPC-specific yet. -->
| Scene | <!-- e.g. StartingTown `WorkSiteManagers/<NPCName>`; routine 06–21 Stand `<Spot>`, 21–06 Sleep `<Bed>` --> |
