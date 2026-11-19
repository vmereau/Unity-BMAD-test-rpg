# NPC Identity — Edric the Greysmith

> This file is the canonical character sheet for this NPC. It is the instantiated version of
> `NPC_IDENTITY_TEMPLATE.md`. Read it before writing any dialogue, memory, or behavioural asset
> for this NPC. Keep it up to date as the character develops.

---

## Core Identity

| Field | Value |
|-------|-------|
| **Name** | Edric the Greysmith |
| **Nickname / Known As** | Edric |
| **Profession** | Blacksmith |
| **Faction / Affiliation** | The Company (hired smith) |
| **Location** | Timberworks forge, Company side, Alderath Camp |
| **Age** | Mid-50s |

---

## Personality

| Field | Value |
|-------|-------|
| **Alignment** | Neutral Good |
| **Traits** | Blunt, distant, direct, hardworking |
| **Fears / Flaws** | Proud of his work to a fault; takes bad iron and cheap commissions as a personal insult |
| **Desires / Goals** | Got his bigger forge at the Timberworks; wants the commissions to fill it |

---

## Speech Style

| Field | Value |
|-------|-------|
| **Tone** | Gruff but fair; warms up when he talks shop |
| **Vocabulary** | Smith's words: heat, quench, temper, stock, anvil. Swears like a man who has burned his hands too often |
| **Quirks** | Closes a thought with "aye" now and then (at most once a line, never every line); talks about metal as if it has moods; grumbles about Company iron and Company prices |
| **Taboo Topics** | What the Company pays him for a blade |

**Sample line:**
> "Bring me good iron and I'll make you something worth carrying. Bring me Company iron and I'll still make something, aye, but I'll swear at it the whole way."

---

## Background

Edric grew up in a small village, the son of a blacksmith. When his father retired, Edric took over the forge without question and has worked it ever since. He is content in his craft. The Company has given him a larger forge beside the Timberworks, and he quietly hopes for the bigger commissions that should go with it.

---

## Dialogue Writing Rules (NPC-specific)

> These rules extend `DIALOGUE.md` for this NPC specifically.

- "aye" (lower case) closes a thought now and then, never on every line
- Practical, full sentences: he sounds like a man who has work to get back to, but likes talking about the work
- Avoid flowery or poetic language; plain craftsman vocabulary, and he swears when he means it

---

## Asset Reference

| Asset | Path |
|-------|------|
| NPCEntity | `Assets/_Game/Data/NPCs/BlackSmith/NPC_Blacksmith.asset` |
| Dialogues | `Assets/_Game/Data/NPCs/BlackSmith/Dialogues/` |
| Memories | `Assets/_Game/Data/NPCs/BlackSmith/Memories/` |
| Teachings | One topic, `Teachings/Training/` ("What can you teach me?", `Mem_Blacksmith_Training`): rows +1 Strength (50 gold, cap 30) and Smithing (10 LP + 80 gold). The Smithing row is gated by the gate-only `Mem_Blacksmith_Smithing` (no start dialogue, invalidated by `SkillFact_Smithing_Learned`), so it disappears once learned |
| Smithing recipe | `Dialogues/SmithingRecipe/` ("What should I forge first?", one-shot) + `Mem_Blacksmith_SmithingRecipe` — unlocked by `SkillFact_Smithing_Learned`; the start node's `DialogueFact_blacksmith_smithing_recipe_given` triggers `PlayerReward_blacksmith_smithing_recipe` (`Item_Recipe_ShortIronSword` ×1) |
| Shop | `Dialogues/Shop/` ("I need iron") + `Mem_Blacksmith_Shop` — unlocked by `SkillFact_Smithing_Learned`; stock (`Item_IronIngot` ×10, `Weapon_ShortIronSword` ×1) on the `Blacksmith` instance in `StartingTown.unity` |
| Anvil | `SmithingStation_Anvil` next to Edric in `StartingTown.unity` (needs Smithing) |
| Prefab | Scene instance of `Assets/_Game/Prefabs/Entities/Humanoids/NPC_base Variant.prefab` (no dedicated prefab) |
