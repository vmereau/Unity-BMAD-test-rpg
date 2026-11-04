# NPC Identity — Aldric Sorn (Camp Cook)

> This file is the canonical character sheet for this NPC. It is the instantiated version of
> `NPC_IDENTITY_TEMPLATE.md`. Read it before writing any dialogue, memory, or behavioural asset
> for this NPC. Keep it up to date as the character develops.

---

## Core Identity

| Field | Value |
|-------|-------|
| **Name** | Aldric Sorn |
| **Nickname / Known As** | "Old Aldric", "the Cook" |
| **Profession** | Camp cook (front); discreet fence and information broker (secret) |
| **Faction / Affiliation** | Unaffiliated (covert contacts among merchants and criminals) |
| **Location** | The canteen, Alderath Camp worker area |
| **Age** | Late 40s |

---

## Personality

| Field | Value |
|-------|-------|
| **Alignment** | Neutral self-serving — helps those who pay, harms those who threaten |
| **Traits** | Shrewd, amiable, patient, secretive, observant |
| **Fears / Flaws** | Old debts catching up; being exposed as more than a cook |
| **Desires / Goals** | Accumulate enough coin to disappear cleanly; keep his network intact |

---

## Speech Style

| Field | Value |
|-------|-------|
| **Tone** | Warm and welcoming on the surface; careful and measured underneath |
| **Vocabulary** | Common folk speech, occasionally too precise for a simple cook |
| **Quirks** | Wipes the serving table even when it is already clean; answers questions with questions; never volunteers information unprompted |
| **Taboo Topics** | His life before the camp; what he keeps under the pots; who he reports to |

**Sample line:**
> "You look like you've had a long road. Sit, eat — and tell me what's on your mind."

---

## Background

Aldric Sorn ran a modest inn in the capital before a failed deal left him owing debts to people who do not forgive. He signed on with the Company to disappear, took over the camp canteen, and quietly rebuilt — this time trading in information, lockpicks and discreet services rather than just ale. Friendly by habit and dangerous by necessity, he has survived long enough to know that patience and a warm smile outlast most weapons.

---

## Dialogue Writing Rules (NPC-specific)

> These rules extend `DIALOGUE.md` for this NPC specifically.

- Keep warmth on the surface even when discussing suspicious or dangerous topics
- Never directly accuse or threaten — Aldric deflects with hospitality
- Respond to personal questions about his past with a change of subject or a question back at the player
- Short sentences are fine; Aldric does not ramble

---

## Asset Reference

| Asset | Path |
|-------|------|
| NPCEntity | `Assets/_Game/Data/NPCs/Cook/NPC_Cook.asset` |
| Dialogues | `Assets/_Game/Data/NPCs/Cook/Dialogues/` |
| Teachings | `Assets/_Game/Data/NPCs/Cook/Teachings/` |
| Memories | `Assets/_Game/Data/NPCs/Cook/Memories/` |
| Shop | `Dialogues/Shop/` ("I need lockpicks") + `Mem_Cook_Shop` — unlocked by `SkillFact_BeginnerLockpicking_Learned`; stock (`Item_Lockpick` ×10) on the `Cook` instance in `StartingTown.unity` |
| Prefab | Scene instance of `Assets/_Game/Prefabs/Entities/Humanoids/NPC_base Variant.prefab` (no dedicated prefab) |
