# NPC Identity — Storekeeper

> This file is the canonical character sheet for this NPC. It is the instantiated version of
> `NPC_IDENTITY_TEMPLATE.md`. Read it before writing any dialogue, memory, or behavioural asset
> for this NPC. Keep it up to date as the character develops.

---

## Core Identity

| Field | Value |
|-------|-------|
| **Name** | Storekeeper |
| **Nickname / Known As** | the Storekeeper |
| **Profession** | Company storekeeper |
| **Faction / Affiliation** | The Company |
| **Location** | Company yard, at his table east of the Company store (he doesn't work inside it yet) |
| **Age** | Mid-40s |

---

## Personality

| Field | Value |
|-------|-------|
| **Alignment** | Lawful neutral — the ledger is the only morality |
| **Traits** | Exact, bored, unsympathetic, tidy |
| **Fears / Flaws** | Shortfalls in the ledger |
| **Desires / Goals** | Finish his posting and leave the frontier |

---

## Speech Style

| Field | Value |
|-------|-------|
| **Tone** | Dry, bored and transactional |
| **Vocabulary** | Clerk's words: debt, price, ledger, entry |
| **Quirks** | Quotes the price before anything else; reads out your debt the way other men talk about the weather |
| **Taboo Topics** | Where the stock really goes |

**Sample line:**
> "It goes on your debt or in my hand. Which is it to be?"

---

## Background

A Company employee who runs the official store. He doesn't enjoy the system or question it. Personal name still to be decided.

---

## Dialogue Writing Rules (NPC-specific)

> These rules extend `DIALOGUE.md` for this NPC specifically.

- Never says more than the transaction needs; complete sentences, no small talk
- Never haggles or sympathizes

---

## Asset Reference

| Asset | Path |
|-------|------|
| NPCEntity | `Assets/_Game/Data/NPCs/Storekeeper/NPC_Storekeeper.asset` |
| Dialogues | `Assets/_Game/Data/NPCs/Storekeeper/Dialogues/` |
| Memories | `Assets/_Game/Data/NPCs/Storekeeper/Memories/` |
| Prefab | Scene instance of `Assets/_Game/Prefabs/Entities/Humanoids/NPC_base Variant.prefab` (no dedicated prefab) |
