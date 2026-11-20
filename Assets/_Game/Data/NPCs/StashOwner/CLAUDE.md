# CLAUDE.md — Assets/_Game/Data/NPCs/StashOwner

> Loaded when Claude accesses this NPC's data. Character sheet for Brann — read it before writing any dialogue, memory or behaviour for this NPC.

---

## Core Identity

| Field | Value |
|-------|-------|
| **Name** | Brann (npc-003) |
| **Nickname / Known As** | Stash Owner |
| **Profession** | Veteran laborer, gang house resident |
| **Faction / Affiliation** | Company-adjacent worker, not a Company guard (`Faction_Neutral`) |
| **Location** | His gang house stash 06:00–18:00, then the gang fire until morning |
| **Age** | Early 40s |

---

## Personality

| Field | Value |
|-------|-------|
| **Alignment** | TBD |
| **Traits** | Domineering, calculating, possessive |
| **Fears / Flaws** | Losing the comfortable position he carved out; treats other workers as potential thieves |
| **Desires / Goals** | Keep his supplies and his leverage |

---

## Speech Style

| Field | Value |
|-------|-------|
| **Tone** | Blunt, possessive, quietly threatening |
| **Vocabulary** | Plain; "mine" comes up a lot; thinks in debts and favours owed |
| **Quirks** | Says what's his before anything else; threats are practical, never theatrical; calls the player "newcomer", never adventurer |
| **Taboo Topics** | Who he sells information to |

**Sample line:**
> "My corner, my supplies and my business. You're new, so I'll say it once and slowly: keep walking, and keep your hands where they are."

---

## Background

Brann hoards better food and trades information about other workers to the Company. Tam knows about the stash and
regards Brann with casual contempt.

## Dialogue Writing Rules (NPC-specific)

- TBD

---

## Gameplay Notes

- Ordinary theft witnessing, pursuit and scolding apply; no quest-specific fight or debt.

---

## Asset Reference

| Asset | Path |
|-------|------|
| Scene | Instance and stash under hand-owned `HiddenStashQuest`, outside `Area1_Blockout` |
| Dialogues | `Dialogues/Greeting/`; `Dialogues/VennSentYou/` (`Mem_StashOwner_VennSentYou`: unlocked by `DialogueFact_BarnGuard_DealStruck`, invalidated by its own fact or `DialogueFact_FarmReeve_Shortfall_ReportedGuard`) |
| Stash | `BrannsStash` also holds `Item_BrannsTally` (TitheBarnShortfall proof; a second `ContainerTakeFact` sets `WorldFact_GangStash_TallyTaken`) |
