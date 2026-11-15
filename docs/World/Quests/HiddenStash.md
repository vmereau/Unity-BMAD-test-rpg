# The Hidden Stash

- ID: quest-001 / `HiddenStash`.
- Location: Company Encampment, gang house by the lake.
- Informant: Tam. Owner: Brann (npc-003).
- Status: implemented.

Tam's stash topic ("Is there really nothing I can do for you?") only appears after the player offers help in his opener, and never after mocking him. He says he is hungry and doesn't care where food comes from; picking "I don't have any money for food." makes him point at Brann's chest and starts the quest ("I'll find you something." leaves the topic open). Brann stands inside the gang house from 06:00 to 18:00 and goes to the gang fire from 18:00 to 06:00. The unlocked owned chest contains five bread, two apples and 25 gold coins, with no restock. Taking food or a coin advances the quest; merely opening the chest does not. Collected coins immediately increase the player's gold balance, one gold per coin, without occupying inventory slots. Tell Tam "I helped myself to Brann's supplies" to complete it. There is no extra turn-in payment or XP.

Taking before hearing the hint persists and permits the follow-up after the hint. Mocking Tam in his opener closes the stash topic for good, so the quest can then never start (a stash taken anyway stays unreported). Being seen uses the normal theft pursuit/scold and does not permanently fail the quest. Killing Brann frees his property using the normal ownership rule. Facts and remaining contents survive save/load.

## Authored data

`Assets/_Game/Data/Quests/HiddenStash/Quest_HiddenStash.asset` reads:

| Fact | Setter | Purpose |
|---|---|---|
| `Dialogue.Played.HiddenStash.Hint` | Tam's StashHint "no money" choice | Start and first objective |
| `World.HiddenStash.Taken` | `ContainerTakeFact` on `HiddenStashQuest/BrannsStash` | First successful allowed-item take |
| `Dialogue.Played.HiddenStash.Reported` | Tam's StashReported start node | Completion |

`HiddenStashQuest` is a hand-owned root outside `Area1_Blockout`. Its chest and Brann have distinct PersistentID facts. The chest's killed identity is also its save key; its explicit save ID is unique. Builders never own the encounter. A layout change to the gang house requires checking these hand-owned placements.

## Verification route

New game → talk to Tam, offer help → stash topic, "no money" → visit Brann by day → return after dusk → open without taking (no progress) → take one item → return to Tam. Repeat with taking before the hint. Save/load after each phase and confirm no restock, duplicate loot, or duplicate completion. Check ordinary containers and corpse loot do not advance this quest.
