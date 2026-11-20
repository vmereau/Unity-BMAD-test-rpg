# CLAUDE.md — Assets/_Game/Data/NPCs/StashOwner

> Loaded when Claude accesses this NPC's data. Character sheet for Brann — read it before writing any dialogue, memory or behaviour for this NPC.

## Core Identity

- Name: Brann (npc-003, Stash Owner).
- Profession: veteran laborer, gang house resident.
- Affiliation: company-adjacent worker; not a Company guard.
- Age: early 40s.

## Personality and Background

Domineering, calculating and possessive. Brann hoards better food and trades information about other workers to the Company. He fears losing the comfortable position he carved out. His aim is to keep his supplies and his leverage.

## Speech Style

| Field | Value |
|-------|-------|
| **Tone** | Blunt, possessive, quietly threatening |
| **Vocabulary** | Plain; "mine" comes up a lot; thinks in debts and favours owed |
| **Quirks** | Says what's his before anything else; threats are practical, never theatrical; calls the player "newcomer", never adventurer |
| **Taboo Topics** | Who he sells information to |

**Sample line:**
> "My corner, my supplies and my business. You're new, so I'll say it once and slowly: keep walking, and keep your hands where they are."

## Relationships and Routine

Tam knows about the stash and regards Brann with casual contempt. Brann treats other workers as potential thieves. He stands by his gang house stash from 06:00 to 18:00, then joins the gang fire until morning. Ordinary theft witnessing, pursuit and scolding apply; no quest-specific fight or debt.

## Assets

`NPC_StashOwner.asset`, `Dialogues/Greeting/`, `Memories/`. Scene instance and stash live under hand-owned `HiddenStashQuest`, outside `Area1_Blockout`. `BrannsStash` also holds `Item_BrannsTally` (TitheBarnShortfall proof; a second `ContainerTakeFact` sets `WorldFact_GangStash_TallyTaken`).
