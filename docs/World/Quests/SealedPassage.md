# Quest Spec — SealedPassage

> Template version: 1.1
> Status: `implemented` — 2026-11-18: quest data (Part A), the door route (Part B1, `spec-sealed-passage-door-route`) and the goat-track route (Part B2, `spec-sealed-passage-goat-track`).
> Spec path: `docs/World/Quests/SealedPassage.md`
> World id: quest-002 (The Sealed Passage) · area-002 Stone Quarry · giver npc-009 Bren
> Design with `/quests:design`, build with `/quests:implement SealedPassage`, check with `/quests:audit SealedPassage`.

---

## Metadata

| Field | Value |
|---|---|
| `questId` | `SealedPassage` |
| `title` | The Sealed Passage |
| `description` | The Company sealed the crack in the quarry's south face with a locked door overnight, without warning. Bren believes his lad Odo slipped in the night before and was walled in. The lock is simple, and Dace keeps the key in his hut. There is also an old goat track over the rock from the wilderness, but nobody goes out there. Get inside and find out what became of Odo. |

---

## Story

Last season the south-face crew broke into a crack wide enough for a man, with open rock behind it. Dace started a
new cut in there with a few skilled, trusted cutters; the rest of the workers were told to keep clear (not formally
forbidden, just "not for the likes of us"). Workers said you could hear something through the crack at night, slow,
like breathing. Odo, a young worker Bren has looked after since they came off the same cart, was drawn to it. The
quarry night watch is one sleepy guard by a brazier, so Odo slipped in one night. The next morning a Company door
with an iron lock closed the crack, put up overnight, and Odo's bedroll was cold. The hand-picked cutters were
"moved up to the fort", and Bren has never seen one of them since (unanswered hook).

Beyond the door: the abandoned new cut (Company layer per `Assets/_Game/Art/Environment/ANCIENT_CIVILIZATION.md`:
timber scaffolding, a few left tools, painted survey numbers on the ancient stone; the ancient stone itself is never
cut), then the Ruins 2 ring. Odo sits against the ancient stone, facing it, with no mark on him. He never tried the
door. Nothing answers what happened (canon: the ruins never explain themselves).

The cleft stays sealed for the world: `WorldFact_QuarryPassageOpened` is no longer part of this quest and is kept
for a later Company event (e.g. the Company reopening the cut).

---

## Facts

Who sets each type: `Assets/_Game/Data/Facts/CLAUDE.md`.

### To create

| Asset name | Type | Key / trigger |
|---|---|---|
| `DialogueFact_QuarryWorker_Friendly` | `DialogueFact` | Bren's opener, choice "Any advice for a newcomer?" |
| `DialogueFact_QuarryWorker_Rude` | `DialogueFact` | Bren's opener, choice "Mind your own business, old man." |
| `DialogueFact_QuarryWorker_PassageAccepted` | `DialogueFact` | "The south face", choice "I'll find out what happened to him." |
| `DialogueFact_QuarryWorker_ReportToken` | `DialogueFact` | "I found Odo", token choice (`requiredItem` = `Item_Odo_TallyToken`, given to Bren) |
| `DialogueFact_QuarryWorker_ReportTold` | `DialogueFact` | "I found Odo", choice without the token |
| `WorldFact_SealedPassage_RingEntered` | `WorldFact` | **New setter:** a reach trigger inside the sealed area, past both the door and the goat track's descent |
| `WorldFact_SealedPassage_TokenTaken` | `WorldFact` | `ContainerTakeFact` on Odo's remains (existing setter, HiddenStash pattern) |
| `KilledFact_QuarryWorker` | `KilledFact` | Bren's scene `PersistentID` (`Game/World/Generate Missing KilledFacts`); same asset on `NPC_QuarryWorker._killedFact` |
| `QuestFact_SealedPassage_Started` | `QuestFact` | computed — IsStarted |
| `QuestFact_SealedPassage_Completed` | `QuestFact` | computed — IsCompleted |

### Already exists (reference only)

| Asset name | Type | Where it lives |
|---|---|---|
| `WorldFact_Arrival_Done` | `WorldFact` | `Assets/_Game/Data/Facts/` (set by `ArrivalSequence`) |
| `Skill_Begginer_Lockpicking` | skill (not a fact) | `Assets/_Game/Data/Skills/Lockpicking/` — required by the door's `Lockable` |

---

## Quest Conditions

```
startPart:
  fact:  DialogueFact_QuarryWorker_PassageAccepted
  entry: "Bren's lad Odo slipped through the crack in the quarry's south face the night before the Company sealed it with a door. Bren wants to know what became of him."

completedParts:     # ANY true → completed
  - fact:  DialogueFact_QuarryWorker_ReportToken
    entry: "I gave Bren Odo's tally token. At least he knows now."
  - fact:  DialogueFact_QuarryWorker_ReportTold
    entry: "I told Bren what I found beyond the door."

failedParts:        # ANY true → failed
  - fact:  KilledFact_QuarryWorker
    entry: "Bren is dead. There is no one left to tell."
```

---

## Steps

```
steps:
  - title: "Past the sealed crack"
    description: "The crack is shut behind a Company door. I could pick its lock, take the key from Dace's hut, or brave the old goat track from the wilderness."
    parts:
      - fact:  WorldFact_SealedPassage_RingEntered
        entry: "I made it past the sealed crack."

  - title: "Find Odo"
    description: "Search the new cut and the old stones beyond it."
    parts:
      - fact:  WorldFact_SealedPassage_TokenTaken
        entry: "I found Odo, and took his tally token."
```

The report is the completion itself (two alternative facts), so it is not a step: a step needs all its parts true.

---

## Routes past the seal (any one works)

| Route | Requirement | Risk |
|---|---|---|
| **Pick the door** | `Skill_Begginer_Lockpicking` + 1 `Item_Lockpick` (existing `LockpickRules` gate) | Door owned by Dace (`Ownership`): a witnessed pick is a crime |
| **Dace's key** | `Item_Key_QuarrySouthDoor`, an owned pickup in Dace's hut | Taking it while seen is a theft |
| **Goat track** | None — a steep path from the forest edge south-east of the quarry, over the ring's rim and down into it on foot | A small group of existing hostile creatures along the track |

---

## Quest Dependencies

```
reads:
  - WorldFact_Arrival_Done: Bren only talks after the arrival (event-001)
read_by:
  - Mem_QuarryWorker_Waiting: unlocks on QuestFact_SealedPassage_Started
  - Mem_QuarryWorker_After: unlocks on QuestFact_SealedPassage_Completed
```

---

## NPC Involvement

Voice for Bren: **campfire storyteller** — plain and unhurried, long sentences joined with "and" / "so", work
vocabulary (benches, tally, cart, sledge), dry understatement, "the lads", "mind you". No ellipses or stage pauses.
See the Voice section of `Assets/_Game/Data/NPCs/DIALOGUE.md`. Nodes over 300 characters are split at a sentence
boundary during implementation.

```
npcs:
  - npc: QuarryWorker (Bren, npc-009)
    exists: false            # /NPC:create
    folder: Assets/_Game/Data/NPCs/QuarryWorker/
    identity_notes: >
      Bren, quarryman in his 40s, two years in the camp. Worn, patient, stubborn, dry humour. Has looked after Odo,
      a young worker, since they came off the same cart. No faction (worker). Works the quarry floor by day, sleeps
      in the tent camp. Voice: campfire storyteller (above). Calls the player "friend" once he warms up.

    memories:
      - asset: Mem_QuarryWorker_Greeting         # greeting opener (DIALOGUE.md pattern)
        unlock:     [WorldFact_Arrival_Done]
        invalidate: [DialogueFact_QuarryWorker_Friendly, DialogueFact_QuarryWorker_Rude]
        dialogue_topic: Greeting                  # label: "You look like you've been here a while."
        dialogue:
          - NPC: "Long enough that the dust doesn't wash out any more. You're one of the new ones off the last cart, I'd guess. You've got that look, like you're still waiting for someone to tell you it was all a mistake."
          - Player: "Any advice for a newcomer?"  → NPC: "Keep your tally up, keep your head down, and don't borrow from anyone who smiles when they lend. That's most of what I know, and it took me two years to learn it." END  [sets: DialogueFact_QuarryWorker_Friendly]
          - Player: "Mind your own business, old man."  → NPC: "Fair enough. There's plenty of business here to mind, and none of it's mine." END  [sets: DialogueFact_QuarryWorker_Rude]
          - Player: "I have to go."  → END  (no fact: opener stays available)

      - asset: Mem_QuarryWorker_SouthFace
        unlock:     [WorldFact_Arrival_Done, DialogueFact_QuarryWorker_Friendly]
        invalidate: [DialogueFact_QuarryWorker_PassageAccepted, DialogueFact_QuarryWorker_Rude]
        dialogue_topic: SouthFace                 # label: "What's behind that door in the south face?"
        dialogue:
          - NPC: "You'll have seen it, then. Everyone has, and everyone walks past it like it isn't there. Best way to keep your back in one piece around here is not to ask about things the Company builds overnight."
          - NPC: "Last season the lads on the south face broke into a crack wide enough for a man, and the rock opens up behind it. So Dace starts a new cut in there. Not with the likes of us, mind you. He took his best cutters, men he trusts, and told the rest of us to stay at our own benches."
          - NPC: "Odo, the lad I've looked after since we came off the same cart, couldn't leave it alone. He swore you could hear something through that crack at night, slow, like breathing. I told him it was the wind in the rock. He didn't believe me, and I'm not sure I did either."
          - NPC: "Night watch on the quarry is one guard and a brazier, and the guard's usually asleep before the brazier is, so it wouldn't have been hard for him. Then one morning there's a door in the crack, Company timber with an iron lock, put up while we slept. And Odo's bedroll was cold."
          - NPC (choice prompt): "He went in the night before, I'm sure of it, and they shut him in. I can't go and look myself. If Dace catches me near that door I'll be hauling till I drop, and I'm too old to run from his guards."
          - Player: "What happened to the men who worked in there?"  → NPC: "Gone up to the fort, they told us. Better work, better food. Funny thing, though: I carry stone up to that fort every week, and I've never once seen any of them up there." → back to the choice
          - Player: "How would I get in?"  → NPC: "The lock's nothing special. Anyone who's had a lesson or two with a pick could open it. Or there's Dace's hut down on the floor; he leaves his keys lying about like nobody would dare." → NPC: "There's the old goat track over the rock from the wilderness side too, but I wouldn't. Things out there have eaten better men than us." → back to the choice
          - Player: "I'll find out what happened to him."  → NPC: "You'd do that for a stranger? Then I owe you, and I don't forget what I owe. Whatever you find in there, good or bad, come and tell me. Not knowing is the worst of it." END  [sets: DialogueFact_QuarryWorker_PassageAccepted]
          - Player: "Not my business."  → NPC: "No, I suppose it isn't. It's nobody's business. That's the trouble with this place." END  (no fact: topic stays)

      - asset: Mem_QuarryWorker_Waiting           # repeatable reminder
        unlock:     [QuestFact_SealedPassage_Started]
        invalidate: [WorldFact_SealedPassage_TokenTaken]
        dialogue_topic: Waiting                   # label: "About Odo."
        dialogue:
          - NPC: "Any luck? No, I can see it on you. Pick the lock if you've the skill, or lift Dace's key from his hut while he's out on the floor. And if you go round by the goat track, take a proper weapon. I mean it." END

      - asset: Mem_QuarryWorker_Report
        unlock:     [WorldFact_SealedPassage_TokenTaken, QuestFact_SealedPassage_Started]
        invalidate: [DialogueFact_QuarryWorker_ReportToken, DialogueFact_QuarryWorker_ReportTold]
        dialogue_topic: Report                    # label: "I found Odo."
        dialogue:
          - NPC (choice prompt): "You found him? Then tell me straight. I've had enough of people being kind to me with half the story."
          - Player: "He sat against the old stones, facing them. Not a mark on him, and he never tried the door. Here's his tally token."  [requiredItem: Item_Odo_TallyToken, given]
              → NPC: "That's his tally, right enough. Forty-one blocks short. They'd have charged him for dying, if they'd ever admitted he was in there."
              → NPC: "Sitting there facing the rock, and never once tried the door. I keep turning that over. Here, take this. It's not much, but I'd rather it went to you than to Dace's ledger." END  [sets: DialogueFact_QuarryWorker_ReportToken]
          - Player: "He sat against the old stones, facing them. Not a mark on him, and he never tried the door. I brought nothing back."
              → NPC: "What was he listening to in there, that he'd sit and wait like that? Don't answer, I don't think I want to know. At least I know where he is now, and that's more than the Company ever gave me." END  [sets: DialogueFact_QuarryWorker_ReportTold]

      - asset: Mem_QuarryWorker_After             # repeatable
        unlock:     [QuestFact_SealedPassage_Completed]
        invalidate: []
        dialogue_topic: After                     # label: "How are you holding up?"
        dialogue:
          - NPC: "Cutting my tally, same as always. Some nights I still hear it through the rock, that slow breathing, and I lie there and tell myself it's the wind. I'm getting better at believing it." END

  - npc: QuarryOverseer (Overseer Dace, npc-014)
    exists: true
    folder: Assets/_Game/Data/NPCs/QuarryOverseer/
    role: no new dialogue. Owns the door (`Ownership`) and his hut's key pickup.
```

Note: a rude answer in the opener closes Bren's south-face topic for good, so the quest can be missed
(Consequence pillar). Intended.

---

## World Set-up (implemented with the quest)

- **Door in the cleft** — replace the `CleftBlocker` `Seal` with a planks-and-rubble wall holding a locked Company door:
  `DoorInteractable` + `Lockable` (`_requiredSkill` = `Skill_Begginer_Lockpicking`, new `_keyItem` =
  `Item_Key_QuarrySouthDoor`) + `Ownership` (Dace) + `SaveableObject`. From the inside it opens without the lock
  check (no trap). Keep the seal's `FactRemovedObject` / `WorldFact_QuarryPassageOpened` wiring and the fact asset
  for a later Company event. Keep the NavMesh closed while the door is shut, as the carving obstacle does today.
- **Key support (new code)** — `Lockable._keyItem`: a player holding the key opens the lock with no skill and no
  lockpick (resolved in `DoorSystem` and `ContainerSystem`, `LockpickRules` extended and tested).
- **`Item_Key_QuarrySouthDoor`** — new quest-flagged key item (placeholder icon), owned pickup in **Dace's hut**, a new
  small overseer's shed on the quarry floor near his sledge spot (`WorkSiteManagers/QuarryOverseer_Spot`).
- **Goat track** — steep but walkable path from the forest edge south-east of the quarry, over the ring's rim and down
  into it, with a gap in the invisible `Quarry/RuinsRing` colliders where it crosses; a small group of existing hostile
  creatures along it.
- **Ring-entry trigger (new setter)** — a trigger volume that sets `WorldFact_SealedPassage_RingEntered` when the player
  enters (reuse an existing fact-setting trigger if one exists; otherwise a small `WorldFactTrigger`).
- **The new cut** — Company layer dressing past the door: scaffolding, a few left tools, painted survey numbers.
- **Odo's remains** — a container at the foot of the ancient stone, seated and facing it, with `ContainerTakeFact` →
  `WorldFact_SealedPassage_TokenTaken`, `SaveableObject`. Contents: `Item_Odo_TallyToken` (new quest item, placeholder
  icon) and a few worthless belongings.

---

## Rewards

```
rewards:
  onStart: null

  onStepCompleted:
    - stepIndex: 0          # past the sealed crack
      xp: 50
      lp: 0
      gold: 0
      stats: []

  onCompleted: null         # rewards hang on the two report facts instead

  onFact:                   # PlayerRewardSO with FactType = Dialogue
    - fact: DialogueFact_QuarryWorker_ReportToken
      xp: 150
      gold: 10              # Bren's "it's not much"
    - fact: DialogueFact_QuarryWorker_ReportTold
      xp: 150
      gold: 0

  onFailed: null
```

Scale reference: WardensErrand completion 100 XP, HiddenStash meal ending 100 XP.

---

## Player Flow

1. After the arrival, Bren's opener ("You look like you've been here a while."). Friendly answer → "What's behind that
   door in the south face?" opens. Rude answer → Bren's quest is closed for good.
2. Bren tells the story; accepting sets `PassageAccepted` → quest starts; "About Odo." becomes his reminder.
3. The player gets past the seal by one of three routes (lockpick, Dace's key, goat track) → `RingEntered` → step 0
   (+50 XP).
4. Through the abandoned new cut to Odo's remains → takes the token → `TokenTaken` → step 1 done, "About Odo." closes,
   "I found Odo." opens.
5. Report with the token (+150 XP, +10 gold) or without it (+150 XP) → quest completed → "How are you holding up?".

## Design Checks

- No soft-lock: the goat track is always open without skill or key; the door opens from inside.
- The token can be sold or dropped: the "brought nothing back" report still completes the quest.
- Bren dead before accepting: the quest never appears. Bren dead after completion: `failedParts` would also be true —
  **verify `QuestSO` state priority during implementation**; if failed wins, gate the fail on "not completed".
- New setters required (V3 flags them until built): `RingEntered` trigger, and key support for the door.

---

## Implementation Checklist

**Part A — quest data (done 2026-11-18)**

- [x] Facts in `Assets/_Game/Data/Facts/`: `DialogueFact_QuarryWorker_Friendly` / `_Rude` / `_PassageAccepted` /
      `_ReportToken` / `_ReportTold`, `WorldFact_SealedPassage_RingEntered` / `_TokenTaken`,
      `QuestFact_SealedPassage_Started` / `_Completed` / `_Step0`
- [x] QuestSO — `Assets/_Game/Data/Quests/SealedPassage/Quest_SealedPassage.asset` (start, 2 steps, 2 completed parts; no failed part yet)
- [x] NPC data — `Assets/_Game/Data/NPCs/QuarryWorker/` (`NPC_QuarryWorker.asset` copied from Tam: Neutral faction,
      base head, no relationship override; identity `CLAUDE.md`), `docs/World/NPCs/npc-009-quarry-worker.md`
- [x] Dialogue chains — `Data/NPCs/QuarryWorker/Dialogues/{Greeting,SouthFace,Waiting,Report,After}/`
- [x] Memories — `Data/NPCs/QuarryWorker/Memories/Mem_QuarryWorker_{Greeting,SouthFace,Waiting,Report,After}.asset` (5 synced to `NPCEntity.memories`)
- [x] Items — `Assets/_Game/Data/Items/Quest/Item_Key_QuarrySouthDoor.asset`, `Item_Odo_TallyToken.asset` (data only,
      placeholder icon copied from the notices, no world prefab)
- [x] Rewards — `Assets/_Game/Data/Rewards/PlayerReward_SealedPassage_{Step0,ReportToken,ReportTold}.asset` (synced to `PlayerRewards`)
- [x] Registered — `QuestEventsManager` and `QuestLogUI._allQuests`
- [x] `world-index.md` / `area-001-company-encampment.md` — quest-002 and npc-009 rows updated
- [x] `quest_report SealedPassage`: only the two expected V3 errors (`RingEntered`, `TokenTaken` never set — their
      setters are scene objects built in Part B). `quest_report audit`: no new issues.

**Part B — code + world set-up (`gds-quick-dev`)**

- [x] Code — `Lockable._keyItem` (+ `DoorSystem` / `ContainerSystem` / `LockpickRules.Key`, tests); `WorldFactTrigger`
      (ring entry, indexed by the Quest Explorer); `QuestSO.IsFailed` returns false once completed (+ test); `DoorLatch`
      opens the door from inside
- [x] Bren placed in StartingTown (`WorkSiteManagers/QuarryWorker`, routine quarry floor 06–21 / Wretches' Corner fire),
      `Data/Enemies/StartingTown/Generic/KilledFact_QuarryWorker.asset` on `NPC_QuarryWorker._killedFact` and as the
      quest's failed part
- [x] Scene — door in the cleft (`Area1SitesBuilder`: `CleftBlocker/Seal/CompanyDoor` + `InsideLatch`), StartingTown
      root `SealedPassage` (Dace's hut + owned key pickup `Prefabs/Items/Item_Key_QuarrySouthDoor.prefab`, new-cut
      dressing, survey marks, Odo's remains + `OdoBundle` with `ContainerTakeFact`, `RingEntryTrigger`)
- [x] Goat track (B2, `spec-sealed-passage-goat-track`) — `layout.json` `path-goat-track` graded by `Tools/World/Grade Area 1 Goat Track`
      (sculpt captured, NavMesh rebaked), `RuinsRing` gap from `Area1SitesBuilder.BuildRuinsRing`, `SealedPassage/GoatTrack`
      (cairns + 3 darkness spiders on the shelf), `RingEntryTrigger/Shape_GoatTrack`; the cleft door's leaf defers to
      `InsideLatch` from inside (no theft); `Mem_QuarryWorker_Report` also needs `QuestFact_SealedPassage_Started`
- [x] `quest_report SealedPassage` clean (no issues); `quest_report audit` no new issues; status → `implemented`
