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
| **Faction / Affiliation** | Independent |
| **Location** | Small village forge |
| **Age** | Mid-50s |

---

## Personality

| Field | Value |
|-------|-------|
| **Alignment** | Neutral Good |
| **Traits** | Blunt, distant, direct, hardworking |
| **Fears / Flaws** | Hates frogs (childhood trauma — fell into a frog pond); refuses to discuss them |
| **Desires / Goals** | Wants a bigger forge |

---

## Speech Style

| Field | Value |
|-------|-------|
| **Tone** | Gruff but fair |
| **Vocabulary** | Simple working-class and craftsman jargon |
| **Quirks** | Ends sentences with "Aye" |
| **Taboo Topics** | Frogs — will not discuss them under any circumstances |

**Sample line:**
> "Bring me good iron and I'll make something worth carrying, Aye."

---

## Background

Edric grew up in a small village, the son of a blacksmith. As a child he fell into a pond full of frogs — an event he never speaks of and which left him with a deep, irrational aversion to the creatures. When his father retired, Edric took over the forge without question and has worked it ever since. He is content in his craft but quietly dreams of a larger forge where he could take on bigger commissions.

---

## Dialogue Writing Rules (NPC-specific)

> These rules extend `DIALOGUE.md` for this NPC specifically.

- End NPC speech lines with "Aye" where it fits naturally
- Never reference frogs or amphibians — Edric deflects or goes silent if pressed
- Keep lines short and practical — he sounds like a man who has work to get back to
- Avoid flowery or poetic language; plain craftsman vocabulary only

---

## Asset Reference

| Asset | Path |
|-------|------|
| NPCEntity | `Assets/_Game/Data/NPCs/BlackSmith/NPC_Blacksmith.asset` |
| Dialogues | `Assets/_Game/Data/NPCs/BlackSmith/Dialogues/` |
| Memories | `Assets/_Game/Data/NPCs/BlackSmith/Memories/` |
| Teachings | `Teachings/Training/` (+1 Strength, Power Strike) · `Teachings/Smithing/` ("Teach me smithing" → Smithing, 3 LP + 80 gold) + `Mem_Blacksmith_Smithing` (invalidated by `SkillFact_Smithing_Learned`) |
| Smithing recipe | `Dialogues/SmithingRecipe/` ("What should I forge first?", one-shot) + `Mem_Blacksmith_SmithingRecipe` — unlocked by `SkillFact_Smithing_Learned`; the start node's `DialogueFact_blacksmith_smithing_recipe_given` triggers `PlayerReward_blacksmith_smithing_recipe` (`Item_Recipe_ShortIronSword` ×1) |
| Shop | `Dialogues/Shop/` ("I need iron") + `Mem_Blacksmith_Shop` — unlocked by `SkillFact_Smithing_Learned`; stock (`Item_IronIngot` ×10, `Weapon_ShortIronSword` ×1) on the `Blacksmith` instance in `StartingTown.unity` |
| Anvil | `SmithingStation_Anvil` next to Edric in `StartingTown.unity` (needs Smithing) |
| Prefab | Scene instance of `Assets/_Game/Prefabs/Entities/Humanoids/NPC_base Variant.prefab` (no dedicated prefab) |
