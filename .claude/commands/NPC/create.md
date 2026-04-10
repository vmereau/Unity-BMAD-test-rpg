Your task is to scaffold a complete new NPC folder, including the NPCDataSO asset and the NPC identity file (CLAUDE.md), by walking the user through an interactive setup.

## Step 0 — Read the identity template

Read `Assets/_Game/Data/NPCs/NPC_IDENTITY_TEMPLATE.md` in full before proceeding. You will use it as the structure for the identity interview.

---

## Step 1 — NPC Name

If `$ARGUMENTS` contains a name, use it. Otherwise ask:

> "What is the NPC's name? (This becomes the folder name and asset prefix. Example: `Blacksmith`, `ElderMira`, `GuardCaptain`)"

- Strip spaces for the folder/asset name (e.g. "Elder Mira" → folder `ElderMira`, asset `NPC_ElderMira.asset`)
- Keep the display name with spaces for the `npcName` field in `NPCDataSO` and the identity file

Check whether `Assets/_Game/Data/NPCs/<NPCName>/` already exists. If it does, warn the user and ask whether to continue (edit existing) or cancel.

---

## Step 2 — Create the folder and NPCDataSO

Create the following in order:

1. Folder: `Assets/_Game/Data/NPCs/<NPCName>/`
2. Subfolders: `Dialogues/` and `Memories/` inside the NPC folder
3. Asset: `NPC_<NPCName>.asset` (type: `Game/NPC/NPC Data`) inside the NPC folder
   - Set `npcName` to the display name from Step 1
   - Leave other fields (`dayState`, `nightState`, `walkSpeed`, `prefab`, `memories`) at defaults for now

After creation, check Unity console for errors (`read_console`).

---

## Step 3 — NPC Identity Interview

Walk the user through each section of `NPC_IDENTITY_TEMPLATE.md`. Group the questions into logical rounds to avoid overwhelming the user. For each field, state what it is for and give a brief example.

### Round A — Core Identity
Ask all of these together (the user can answer in one message):

> "Let's fill in the core identity. Answer what you know — you can leave anything blank for now.
>
> 1. **Full name** (e.g. "Edric the Greysmith")
> 2. **Nickname / known as** (e.g. "Edric", "the Old Smith")
> 3. **Profession** (e.g. "Blacksmith", "Town Guard")
> 4. **Faction / affiliation** (e.g. "Merchant Guild", "Unaffiliated")
> 5. **Location** (where is this NPC found?)
> 6. **Age** (rough, e.g. "Mid-50s", "Young adult")"

### Round B — Personality
Ask together:

> "Now personality:
>
> 1. **Alignment** — moral/social disposition (e.g. "Neutral Good", "Lawful Gruff", or just a description)
> 2. **Traits** — 3–5 adjectives (e.g. "Proud, stubborn, fair, dry-witted")
> 3. **Fears / flaws** — what unsettles or limits them?
> 4. **Desires / goals** — what do they want from life?"

### Round C — Speech Style
Ask together:

> "How do they speak?
>
> 1. **Tone** (e.g. "Gruff but fair", "Cheerful and gossipy", "Cold, formal")
> 2. **Vocabulary** (e.g. "Simple working-class words", "Archaic and verbose", "Military jargon")
> 3. **Quirks** (e.g. "Ends sentences with 'Aye'", "Never says 'please'")
> 4. **Taboo topics** (things they refuse to discuss)
> 5. **Sample line** — write one sentence in their voice"

### Round D — Background
Ask as a single open question:

> "Give a short background (2–4 sentences): origin, the key event that shaped them, and their current situation."

### Round G — NPC-specific dialogue rules (optional)
Ask:

> "Any NPC-specific dialogue writing rules? These extend the general DIALOGUE.md rules.
> (e.g. 'Never use contractions', 'Avoid mentioning magic', 'Always sounds impatient')
> Press Enter / type 'none' to skip."

---

## Step 4 — Create the NPC identity file

Using the answers collected, generate the `CLAUDE.md` for this NPC by filling in `NPC_IDENTITY_TEMPLATE.md`.

- Replace `<NPC_NAME>` in the heading with the display name
- Fill every table row and section with the user's answers
- For fields the user left blank, keep the comment placeholder text so it is clear what belongs there
- Fill the Asset Reference table with correct paths for this NPC
- Save as `Assets/_Game/Data/NPCs/<NPCName>/CLAUDE.md`

---

## Step 5 — Confirm and summarise

Show the user a summary of everything created:

```
NPC scaffold complete — <DisplayName>

Created:
  Assets/_Game/Data/NPCs/<NPCName>/
  Assets/_Game/Data/NPCs/<NPCName>/CLAUDE.md       ← identity file
  Assets/_Game/Data/NPCs/<NPCName>/NPC_<NPCName>.asset
  Assets/_Game/Data/NPCs/<NPCName>/Dialogues/
  Assets/_Game/Data/NPCs/<NPCName>/Memories/

Next steps:
  - Use /NPC:dialogue to create the first dialogue topic for <DisplayName>
  - Add a prefab reference in NPC_<NPCName>.asset once the prefab exists
  - Fill any blank fields in CLAUDE.md as the character develops
```
