# Quest Spec — SealedPassage

> Template version: 1.1
> Status: `implemented` — 2026-11-18: quest data (Part A), the door route (Part B1, `spec-sealed-passage-door-route`), the goat-track route (Part B2, `spec-sealed-passage-goat-track`) the report rework (truth / lie / latch, `spec-sealed-passage-report-rework`) and Odo's ring, the goat-track drop and the found-him-first row (`spec-sealed-passage-odo-ring-drop`).
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
| `DialogueFact_QuarryWorker_ReportToken` | `DialogueFact` | "I found Odo", ring choice (`requiredItem` = `Armor_Odo_Ring`, `giveToNpc`: it goes into Bren's inventory, even when worn) |
| `DialogueFact_QuarryWorker_ReportTold` | `DialogueFact` | "I found Odo" → truth, "I couldn't bring anything back." |
| `DialogueFact_QuarryWorker_ReportLied` | `DialogueFact` | "I found Odo", the kind lie "He got out. He's up at the fort with the other cutters." (read by the Report invalidation, the quest's completed part, its reward and `Mem_QuarryWorker_After_Lied`; nothing in the world reacts to the lie yet, kept for a later beat) |
| `DialogueFact_QuarryWorker_ReportLatch` | `DialogueFact` | "I found Odo" → truth, "The door opens from the inside." (hides the line once said) |
| `WorldFact_SealedPassage_Unlatched` | `WorldFact` | `DoorLatch._unlatchedFact` on the cleft door's `InsideLatch`: the player lifted it from the inside |
| `WorldFact_SealedPassage_RingEntered` | `WorldFact` | **New setter:** a reach trigger inside the sealed area, past both the door and the goat track's descent |
| `WorldFact_SealedPassage_TokenTaken` | `WorldFact` | `ContainerTakeFact` on Odo's remains (existing setter, HiddenStash pattern); the name predates the ring |
| `KilledFact_QuarryWorker` | `KilledFact` | Bren's scene `PersistentID` (`Game/World/Generate Missing KilledFacts`); same asset on `NPC_QuarryWorker._killedFact` |
| `QuestFact_SealedPassage_Started` | `QuestFact` | computed — IsStarted |
| `QuestFact_SealedPassage_Completed` | `QuestFact` | computed — IsCompleted |
| `QuestFact_SealedPassage_Step0` | `QuestFact` | computed — step 0 completed (read by `PlayerReward_SealedPassage_Step0`) |

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
    entry: "I gave Bren Odo's ring. At least he knows now."
  - fact:  DialogueFact_QuarryWorker_ReportTold
    entry: "I told Bren what I found beyond the door."
  - fact:  DialogueFact_QuarryWorker_ReportLied
    entry: "I told Bren that Odo got out and went up to the fort. He wanted to believe it."

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
        entry: "I found Odo, and took the ring from his hand."
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
  - Mem_QuarryWorker_After_{Token,Told,Lied}: each unlocks on its report fact (QuestFact_SealedPassage_Completed has no reader now)
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
          - Player: "I'll find out what happened to him."  → NPC: "You'd do that for a stranger? Then I owe you, friend, and I don't forget what I owe. Whatever you find in there, good or bad, come and tell me. Not knowing is the worst of it." END  [sets: DialogueFact_QuarryWorker_PassageAccepted]
          - Player: "Not my business."  → NPC: "No, I suppose it isn't. It's nobody's business, and that's the whole trouble with this place." END  (no fact: topic stays)
          - Player [requiredMemory: Mem_QuarryWorker_FoundFirst]: "I've been behind that door already. I found him."  [sets: DialogueFact_QuarryWorker_PassageAccepted]
              → NPC: "You've been in? Before I'd even asked? Then you know more than I do, friend, and I've waited long enough." → Choice_QuarryWorker_Report (the report below, ring row included)

      - asset: Mem_QuarryWorker_Waiting           # repeatable reminder
        unlock:     [QuestFact_SealedPassage_Started]
        invalidate: [WorldFact_SealedPassage_TokenTaken]
        dialogue_topic: Waiting                   # label: "About Odo."
        dialogue:
          - NPC: "Any luck? No, I can see it on you. Pick the lock if you've the skill, or lift Dace's key from his hut while he's out on the floor. And if you go round by the goat track, take a proper weapon. I mean it." END

      - asset: Mem_QuarryWorker_Report
        unlock:     [WorldFact_SealedPassage_TokenTaken, QuestFact_SealedPassage_Started]
        invalidate: [DialogueFact_QuarryWorker_ReportToken, DialogueFact_QuarryWorker_ReportTold, DialogueFact_QuarryWorker_ReportLied]
        dialogue_topic: Report                    # label: "I found Odo."
        dialogue:
          - NPC (choice prompt): "You found him? Then tell me straight. I've had enough of people being kind to me with half the story."
          - Player: "He's dead. He sat against the old stones, facing them, without a mark on him."
              → NPC (choice prompt, Choice_QuarryWorker_Report_Truth): "Dead, then. I knew it the morning I saw that door, and it still lands like a dropped block. Facing the stones, like he was waiting on something. Go on, friend, tell me the rest."
              - Player [requiredMemory: Mem_QuarryWorker_KnowsLatch]: "The door opens from the inside. He could have walked out."  [sets: DialogueFact_QuarryWorker_ReportLatch]
                  → NPC: "Opens from the inside? Then nobody shut him in at all. He sat there with the way out at his back and stayed. I've been angry at the Company for a month, friend, and now I don't know who to be angry at."
                  → NPC (choice prompt, Choice_QuarryWorker_Report_Rest): "Well. That's one more thing to lie awake over. Go on, then, friend, was there anything of his left to bring back?" → the ring / nothing rows below
              - Player: "Here. His ring."  [requiredItem: Armor_Odo_Ring, giveToNpc (into Bren's inventory; a worn ring is unequipped), hideWhenMissing]
                  → NPC: "That's his ring. His grandfather's, he said, and he never once took it off, swore it kept the rock off his head. Didn't keep much off in the end, did it?"
                  → NPC: "I'll wear it for him, somebody should. Here, take this, friend. It's not much, but I'd rather it went to you than to Dace's ledger." END  [sets: DialogueFact_QuarryWorker_ReportToken]
              - Player: "I couldn't bring anything back."
                  → NPC: "What was he listening to in there, that he'd sit and wait like that? Don't answer, I don't think I want to know. At least I know where he is now, and that's more than the Company ever gave me." END  [sets: DialogueFact_QuarryWorker_ReportTold]
          - Player: "He got out. He's up at the fort with the other cutters."
              → NPC: "Up at the fort, with the lads I never see when I carry stone up there. Well, I'll look harder next week, then. Thank you for telling me, friend." END  [sets: DialogueFact_QuarryWorker_ReportLied]

      - asset: Mem_QuarryWorker_FoundFirst        # gate-only (no topic): the SouthFace "I found him already" row
        unlock:     [WorldFact_SealedPassage_TokenTaken]
        invalidate: [DialogueFact_QuarryWorker_PassageAccepted]

      - asset: Mem_QuarryWorker_KnowsLatch        # gate-only (no topic)
        unlock:     [WorldFact_SealedPassage_Unlatched]
        invalidate: [DialogueFact_QuarryWorker_ReportLatch]

      # One repeatable After topic per ending, label "How are you holding up?"
      - asset: Mem_QuarryWorker_After_Token
        unlock: [DialogueFact_QuarryWorker_ReportToken]
        dialogue:
          - NPC: "I wear his ring on my little finger now, it's all it'll fit. Some days I cut a few blocks over and tell myself they're his, and Dace's ledger doesn't know the difference." END
      - asset: Mem_QuarryWorker_After_Told
        unlock: [DialogueFact_QuarryWorker_ReportTold]
        dialogue:
          - NPC: "Cutting my tally, same as always. Some nights I still hear it through the rock, that slow breathing, and I lie there and tell myself it's the wind. I'm getting better at believing it." END
      - asset: Mem_QuarryWorker_After_Lied
        unlock: [DialogueFact_QuarryWorker_ReportLied]
        dialogue:
          - NPC: "I keep thinking of him up at the fort, eating better than the rest of us. Thank you for that, friend. I've not had a load to carry up since you told me, but next time I do, I'll ask around for him." END

  - npc: QuarryOverseer (Overseer Dace, npc-014)
    exists: true
    folder: Assets/_Game/Data/NPCs/QuarryOverseer/
    role: owns the door (`Ownership`) and his hut's key pickup. His Warden's-notice line (`Text_QuarryOverseer_Notice_3`)
      calls the door "Company business" and never denies the cut was worked.
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
- **Goat track** — steep but walkable path from the forest edge south-east of the quarry, over the ring's rim onto a
  ledge, then a drop through the gap in the invisible `Quarry/RuinsRing` colliders into a pit on the ring's
  floor. The drop is **not one-way** (the `DropBlockers` were removed by design on 2026-11-19): the player can climb
  back out of the pit, or leave through the cleft door's `InsideLatch`. A small group of existing hostile creatures along it.
- **Ring-entry trigger (new setter)** — a trigger volume that sets `WorldFact_SealedPassage_RingEntered` when the player
  enters (reuse an existing fact-setting trigger if one exists; otherwise a small `WorldFactTrigger`).
- **The new cut** — Company layer dressing past the door: scaffolding, a few left tools, painted survey numbers.
- **Odo's remains** — a container at the foot of the ancient stone, seated and facing it, with `ContainerTakeFact` →
  `WorldFact_SealedPassage_TokenTaken`, `SaveableObject`. Contents: `Armor_Odo_Ring` and a few worthless belongings.
- **Odo's ring** — `Data/Items/Quest/Armor_Odo_Ring.asset` (`ArmorSO`, Ring1, +1 defense, buy 6 / sell 2, not a quest
  item; the old token asset converted in place, same GUID and `itemId`): a thin worn copper heirloom with ODO scratched
  inside, worth too little to rob. Model `Art/Items/OdoRing/` (Blender), icon `Art/UI/Items/Item_OdoRing_Icon.png`
  (Blender render), world prefab `Prefabs/Items/Armor_Odo_Ring.prefab`.

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

  onCompleted: null         # rewards hang on the three report facts instead

  onFact:                   # PlayerRewardSO with FactType = Dialogue
    - fact: DialogueFact_QuarryWorker_ReportToken
      xp: 150
      gold: 10              # Bren's "it's not much"
    - fact: DialogueFact_QuarryWorker_ReportTold
      xp: 150
      gold: 0
    - fact: DialogueFact_QuarryWorker_ReportLied
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
4. Through the abandoned new cut to Odo's remains → takes the ring → `TokenTaken` → step 1 done, "About Odo." closes,
   "I found Odo." opens.
5. Report: the truth, then the ring (into Bren's inventory, +150 XP, +10 gold) or nothing (+150 XP), with "the door opens from
   the inside" as an extra row if the player left through the latch (after the goat-track drop, unless the door was already unlocked); or the kind lie (+150 XP,
   ring kept) → quest completed → the matching
   "How are you holding up?".

## Design Checks

- No soft-lock: the goat track is always open without skill or key; the pit can be climbed back out of (the one-way
  `DropBlockers` were removed by design on 2026-11-19), and the door opens from inside.
- The ring can be worn, sold or dropped: a worn ring still shows the ring row; a sold one leaves "brought nothing back".
- Explored first: entering the ring and taking the ring before accepting sets `RingEntered` / `TokenTaken` anyway. Bren's
  SouthFace then offers "I've been behind that door already. I found him.", which starts the quest (both steps already
  done) and goes straight into the report.
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

**Part C — report rework (`spec-sealed-passage-report-rework`, 2026-11-18)**

- [x] `DoorLatch._unlatchedFact` (+ `RecordUnlatch`, tests) → `WorldFact_SealedPassage_Unlatched` on the cleft `InsideLatch`
      (scene + `Area1SitesBuilder`); Quest Explorer indexes it (`FactLinkSource.DoorLatch`, `FactGuidInBlock`)
- [x] Report: `Choice_QuarryWorker_Report` (truth / lie) → `_Truth` (latch / token / nothing) → latch reaction →
      `_Rest` (token / nothing); gate-only `Mem_QuarryWorker_KnowsLatch`; `ReportLied` completion + reward
- [x] After topics split into `Mem_QuarryWorker_After_{Token,Told,Lied}`; Dace's `Text_QuarryOverseer_Notice_3` rewritten
- [x] `quest_report SealedPassage` clean; `quest_report audit` no new issues

**Part D — Odo's ring, goat-track drop, found him first (`spec-sealed-passage-odo-ring-drop`, 2026-11-18)**

- [x] `Item_Odo_TallyToken` → `Armor_Odo_Ring` (`ArmorSO`, same GUID / `itemId`), Blender model + icon + world prefab
- [x] `ChoiceOption.giveToNpc` + worn `requiredItem` support in `DialogueSystem` (`ChoiceItemRules`, tests); ring rows give to Bren
- [x] Bren's ring lines and After_Token rewritten; SouthFace found-first row + `Mem_QuarryWorker_FoundFirst`
- [x] Goat track: ledge + `drops` + drop pit (`Area1FortRamp`), invisible `SealedPassage/GoatTrack/DropBlockers` (a terrain
      face is jump-climbable at any steepness), `Tools/World/Check Goat Track Drop` (CharacterController simulation) passes,
      sculpt captured, NavMesh rebaked, `Shape_GoatTrackDrop` trigger over the pit
- [x] 2026-11-19 (`0e871d8`): `DropBlockers` removed by design — the drop is no longer one-way, so
      `Tools/World/Check Goat Track Drop` is expected to fail.
