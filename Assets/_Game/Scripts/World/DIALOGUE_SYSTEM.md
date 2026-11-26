# DialogueSystem — flow, forced lines, choices, close requests

> Read when touching `DialogueSystem`, `ChoiceItemRules`, or anything that opens / closes NPC dialogue
> (forced lines / topics, choice items and gold, give-item, close requests, trade window interplay).

---

## Flow

Drives NPC dialogue flow (start nodes, choices) using the NPC's memory/graph components.

- Requests arriving while a dialogue is open are **ignored** (warning).
- Closes on `OnPlayerKnockedOut`.
- `NotifyTopicCompleted` is silent for topics without a `dialogueFact`.

---

## Forced lines and topics (NPC-initiated)

- `NPCDialogueRequestData.forcedLine` non-empty → NPC-initiated dialogue: `DialogueUI.OpenLine(npcName, line)` (no
  topics; any advance / Escape closes) — used by theft confrontations (`EntityBrain` Confronting).
- `forcedStartNode` non-null → NPC-initiated topic (`DialogueUI.OpenForcedTopic` + `StartTopic`, full graph context);
  with `forcedTopicThenTopics` the chain ends on the NPC's topic list instead of closing.
- Both `forcedLine` and `forcedStartNode` (`NPCPresence.RequestForcedTopic(node, openingLine)`) → the line is shown
  first through one reused runtime `TextDialogueNode` (no id / seen state) whose next node is the start node's
  `nextNode`.

---

## Choices: items, gold, give-item

- `SelectChoice` checks `ChoiceOption.requiredItem` (`HasChoiceItem`, player `_playerInventory`, wired on
  `Player.prefab`), pays `ChoiceOption.goldCost` (player `GoldSystem` → `_goldItem` coins in the NPC inventory), then
  consumes the item (or, with `ChoiceOption.giveToNpc`, moves it into the NPC's `InventorySystem` through
  `GiveItemToNpc`), and raises `ChoiceOption.signal` on `OnDialogueSignal`.
- A worn requiredItem counts as held (`_playerEquipment`, wired on `Player.prefab`): when no unit is loose in the bag
  it is unequipped first, after the gold check (`ChoiceItemRules.Locate`, `ChoiceItemRulesTests`).
- A `ChoiceOption.requiredCategory` choice instead opens `_giveItemUI` (`SelectChoice` returns false, nothing
  applied); `CompleteGiveChoice(choice, node, item)` re-checks the item, pays the gold, moves one unit into the NPC's
  `InventorySystem` and runs the shared choice tail (`ApplyChoice`: seen, fact, signal, next node; chain end →
  `DialogueUI.RestoreTopics`).
- `hideWhenMissing` choices are filtered out in `AdvanceToNode` (`HasChoiceGoods`).

---

## Daily ration topic

- `DailyRationDialogueNode : StartDialogueNode` (Create → Game/Dialogue/Daily Ration Node): `item`, `perDay`, `noneLeftLine`,
  `notNowLine`. Row label = `DialogueSystem.TopicLabel` → `DailyRationRules.Label` ("My daily ration (2 left)"; `DialogueUI`
  asks the system for every topic label).
- `StartTopic` on it: NPC in its routine Sleep block (`NpcRoutine.CurrentActivity`, found from the requester; awake there =
  woken by the player) → `notNowLine`; none left → `noneLeftLine` (both through the runtime line node, ending on the topic
  list, nothing given); else one `item` into `_playerInventory` (refused → warning, nothing counted, topic list),
  `TakeDailyRation`, "Received: …" toast (`_onItemReceived` = `OnRewardItemReceived`) and the node's chain. Inventory
  full → `inventoryFullLine`, nothing counted. Empty node id / no item / no Core → warning, back to the topic list.
- Always repeatable: never marked seen (never dimmed / sorted down), its `dialogueFact` is ignored (`OnValidate` warns).
  As a forced topic its opening line is dropped so the checks still run. A suspended routine (story sequence) never refuses.
- The ration day starts at the NPC's wake hour (`NpcRoutine.TryGetWakeHour`; none → midnight, warned once), computed from
  `GameClock.Now` (`DailyRationRules.Period`), so a sleep skip, a load or an unloaded NPC reset it correctly. No clock = one
  endless day. Count keyed by the node id (one node per NPC), stored by `WorldStateManager` (`Data/Facts/CLAUDE.md`).

---

## Close requests and the trade window

- **Close request** (`OnDialogueCloseRequested`, `GameEventSO_DialogueCloseRequest` with the requesting
  `NPCPresence`): closes only when that NPC's request opened the current dialogue
  (`NPCDialogueRequestData.requester`, matched with `ReferenceEquals` — `ShouldCloseFor`,
  `DialogueCloseRequestTests`); forced lines have no requester.
- A trade window opened from a shop node outlives the dialogue (it closes before the trade opens); `NPCTradeUI`
  listens to the same close request itself and closes when it trades with the requester's `InventorySystem`.
