# Food for Tam (The Hidden Stash)

- ID: quest-001 / `HiddenStash` (id, asset and fact names kept from the original stash quest; the journal title is "Food for Tam").
- Location: Company Encampment. Optional theft path: the gang house by the lake.
- Giver: Tam (he points, never assigns). Optional stash owner: Brann (npc-003).
- Status: implemented.

Tam's stash topic ("Is there really nothing I can do for you?") only appears after the player offers help in his opener, and never after mocking him. He says he is hungry and doesn't care where food comes from. Either answer starts the quest (`DialogueFact_HiddenStash_Asked`, set when the topic ends): "I'll find you something." or "I don't have any money for food.", which also makes him point at Brann's chest (`DialogueFact_HiddenStash_Hint`).

The quest ends by handing Tam food through his "I brought you something to eat." topic, which opens the Give window:

| Ending | Choice | Accepted items | Fact | Reward |
|---|---|---|---|---|
| Base | "Here, this should help." | Ready-to-eat food: Apple, Bread (`PotionItemSO._readyToEat`) | `DialogueFact_HiddenStash_FedFood` | 50 XP |
| Better | "Here, I've got something even better than you hoped for." (only shown while the player holds a meal) | Any cooked meal | `DialogueFact_HiddenStash_FedMeal` | 100 XP, fact kept for later content |

The given item moves into Tam's inventory. "Not yet." leaves the topic open, and "I don't have any money for food." replays the chest hint from that topic too.

**Optional theft path:** Brann stands inside the gang house from 06:00 to 18:00 and goes to the gang fire from 18:00 to 06:00. The unlocked owned chest contains five bread, two apples and 25 gold coins, with no restock. Taking food or a coin sets `World.HiddenStash.Taken` (journal only); merely opening the chest does not. Collected coins immediately increase the player's gold balance. After the hint, telling Tam "You were right about Brann's supplies. They were just lying around, asking to disappear." gives 25 XP and does **not** complete the quest.

Mocking Tam closes every quest topic for good. Being seen stealing uses the normal theft pursuit/scold and does not fail the quest. Killing Brann frees his property using the normal ownership rule. Facts and remaining contents survive save/load.

## Authored data

`Assets/_Game/Data/Quests/HiddenStash/Quest_HiddenStash.asset` reads:

| Fact | Setter | Purpose |
|---|---|---|
| `Dialogue.Played.HiddenStash.Asked` | Tam's StashHint start node (topic end) | Start |
| `Dialogue.Played.HiddenStash.Hint` | Tam's "no money" choices (StashHint, Feed) | Optional step, part 1 |
| `World.HiddenStash.Taken` | `ContainerTakeFact` on `HiddenStashQuest/BrannsStash` | Optional step, part 2 |
| `Dialogue.Played.HiddenStash.FedFood` | Tam's Feed choice (ReadyFood) | Completion (base) |
| `Dialogue.Played.HiddenStash.FedMeal` | Tam's Feed choice (Meal) | Completion (better) |
| `Dialogue.Played.HiddenStash.Reported` | Tam's StashReported start node | Side reward only (not read by the quest) |

Rewards: `Data/Rewards/PlayerReward_HiddenStash_FedFood` / `_FedMeal` / `_Reported` (Dialogue fact type, on `Player.prefab` `PlayerRewards`).

`HiddenStashQuest` is a hand-owned root outside `Area1_Blockout`. Its chest and Brann have distinct PersistentID facts. The chest's killed identity is also its save key; its explicit save ID is unique. Builders never own the encounter. A layout change to the gang house requires checking these hand-owned placements.

## Verification route

New game → talk to Tam, offer help → stash topic, either answer → Feed topic with no food (food row red, meal row hidden), Cancel the Give window once, then give an apple (base) or a cooked meal (better) on another save. Separately: "no money" → take from Brann's chest after dusk → "You were right about Brann's supplies" (+25 XP, quest still open). Save/load after each phase and confirm no restock, duplicate loot, or duplicate rewards.
