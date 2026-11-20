# CLAUDE.md — Assets/_Game/Data/NPCs/TimberBoss

> Loaded when Claude accesses this NPC's data. Character sheet for Gorm Holl — read it before writing any dialogue, memory or behaviour for this NPC.

---

## Core Identity

| Field | Value |
|-------|-------|
| **Name** | Gorm Holl |
| **Nickname / Known As** | TBD |
| **Profession** | Wood camp manager: worker gang boss of the Wood Camp (area-003) |
| **Faction / Affiliation** | None, a worker who runs the felling crew (`Faction_Neutral`) |
| **Location** | By the eastern shelter of the wood camp, all day |
| **Age** | Mid 40s |

---

## Personality

| Field | Value |
|-------|-------|
| **Alignment** | TBD |
| **Traits** | Gruff, protective of his crew, openly unimpressed by the Company |
| **Fears / Flaws** | The woods: trees his men have notched come down against the cut |
| **Desires / Goals** | Keep his crew alive |

---

## Speech Style

| Field | Value |
|-------|-------|
| **Tone** | Plain and blunt, with dry jokes; protective of his crew |
| **Vocabulary** | Felling words: notch, limb, sawpit, drag; "my lads" |
| **Quirks** | No titles, no flattery; jokes about the Company to its face; goes quiet about the man he lost |
| **Taboo Topics** | The man crushed last month: he states it, then won't say more |

**Sample line:**
> "Ugly work, honest work. It pays nothing, and nobody here pretends it does, which makes us the only honest men in the camp."

---

## Background

Holl came out as a worker like everyone else and earned the crew's trust by keeping them alive, not by any title. Lately
the woods frighten him: trees his men have notched come down against the cut, and one of them was crushed last month. He
reports to Warden Voss and does the bare minimum the notices ask. He is the way into the wood camp's work and its strange
falls (a later quest).

## Dialogue Writing Rules (NPC-specific)

- TBD

---

## Asset Reference

| Asset | Path |
|-------|------|
| Scene | Instance under the hand-owned `WorkSiteManagers` root; stands all day at `WorkSiteManagers/TimberBoss_Spot` |
| Dialogues | `Dialogues/Notice/` (one-time Warden's-errand delivery, needs `Item_Notice_Timber`), `Dialogues/Work/` (repeatable) |
| Head | Placeholder copy of the GuardRight mesh (`HeadAppearance_TimberBoss`) until `/characters:portrait-face` |
