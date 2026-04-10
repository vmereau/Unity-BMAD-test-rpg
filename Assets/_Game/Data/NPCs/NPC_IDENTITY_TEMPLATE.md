# NPC Identity — <NPC_NAME>

> This file is the canonical character sheet for this NPC. It is the instantiated version of
> `NPC_IDENTITY_TEMPLATE.md`. Read it before writing any dialogue, memory, or behavioural asset
> for this NPC. Keep it up to date as the character develops.

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

> These rules extend `DIALOGUE.md` for this NPC specifically.

- <!-- e.g. "Never use contractions — Edric speaks in full sentences" -->
- <!-- e.g. "Do not reference magic or the gods — he is strictly secular" -->
- <!-- e.g. "Greeting topics should always feel slightly impatient, like he has work to do" -->

---

## Asset Reference

| Asset | Path |
|-------|------|
| NPCDataSO | `Assets/_Game/Data/NPCs/<NPCName>/NPC_<NPCName>.asset` |
| Dialogues | `Assets/_Game/Data/NPCs/<NPCName>/Dialogues/` |
| Memories | `Assets/_Game/Data/NPCs/<NPCName>/Memories/` |
| Prefab | `Assets/_Game/Prefabs/NPCs/<NPCName>.prefab` *(if exists)* |
