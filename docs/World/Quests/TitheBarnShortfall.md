# Quest Spec — TitheBarnShortfall

> Template version: 1.1
> Status: `ready`
> Spec path: `docs/World/Quests/TitheBarnShortfall.md`
> Design with `/quests:design`, build with `/quests:implement TitheBarnShortfall`, check with `/quests:audit TitheBarnShortfall`.

Manager quest for Reeve Wendel Pell (npc-016), planted by the Warden's Errand notice. The tithe barn
comes up short every week. The thief is **Osric Venn**, a garrison guard who holds a barn key and sells
the grain to the gang's black market. The player can hand Venn to Pell (Company side), or take Venn's cut
and give Pell a scapegoat, Pim from the field gang. Siding with Venn opens the mafia route through
Brann (npc-003).

## Story beats

1. **Pell's shortfall** (start). Pell counts at dusk and the barn is right. He counts at dawn and it is
   short by two or three sacks. The lock is never forced. He'll pay for a name.
2. **Asking around the farm** (optional). Pim, a young field hand, says the barn isn't that secure:
   nobody watches it once the gang is out, and "someone who's really hungry" could slip in. His guess
   points at the workers, and it makes him easy to blame later. Gerrit, an old hand on the threshing
   floor, reasons the other way. It can't be a worker, because workers are the first ones Pell suspects.
   It has to be someone who knows the place well, though: when Pell counts, where the lamps aren't, and
   someone "nobody counts". That points at the guards.
3. **Aldric's tip** (optional). The Cook, an information broker, sells for 20 gold what he hears: barn
   grain turns up at the gang's tent after midnight, carried by "a man who didn't need to climb in".
4. **The stakeout.** Between 00:00 and 03:00, Venn walks from the barracks to the tithe barn and stands
   at the barn door with sacks. A player who reaches the barn while he is there catches him.
5. **The confrontation.** Venn offers a deal. The player refuses (he'll report), or takes 40 gold and
   agrees to name Pim.
6. **The report to Pell.** The player names Venn or names Pim (the second choice needs the deal). Pell
   pays either way. 12 game hours later, the named man is gone ("taken up to the fort").
7. **Aftermath.** If the player took the deal, Brann has a one-time "Venn says you keep your mouth shut"
   topic. It is the mafia route hook, and its quests come later. A player who took the deal and still
   named Venn keeps the 40 gold, and the Brann hook closes.

Fail: Venn dies before the report ("dead men keep their secrets").

---

## Prerequisites (system work — separate `gds-quick-dev` specs, before `/quests:implement`)

| # | Need | Why | Notes |
|---|---|---|---|
| P1 | **HUD time of day** (**done**, `HudClockUI`) | The stakeout is time-gated, and nothing on screen shows the hour today | Small, diegetic-friendly readout (e.g. sun/moon dial + hour) on the HUD; reads `GameClock` |
| P2 | **Sleep / wait to a chosen hour** | Reach 00:00 without guessing | **Done** (`spec-hud-clock-and-sleep-hour-picker`): bed hour picker in `SleepUI` (any target hour); stays bed-only |
| P3 | **Hour-gated, NPC-gated `WorldFactTrigger`** | `WorldFact_TitheBarn_ThiefCaught` must only set while Venn is actually at the barn | Optional hour window (00–03) + optional required NPC (`PersistentID`) that must be alive, conscious, and in a named routine block / at a spot |
| P4 | **Fact-driven NPC removal on an `ElapsedFact`** | Venn / Pim leave 12 h after the report | `FactRemovedObject._fact` is a `WorldFact` only → widen to `Fact` (stored or computed, incl. `ElapsedFact`), and check it saves / restores for an NPC root |
| P5 | **Venn's night routine** | He must walk to the barn and stand there in the window | Content on `NpcRoutine`: Sleep in a barracks bed, Stand at `Spot_BarnGuard_BarnDoor` 00–03, day post at the fields gate. A sack prop on the spot (or in hand) sells the theft |

P1–P2 were requested. P3–P5 are the smallest code changes I can see that make the stakeout and the
removals work. P5 is scene content and can go into `/quests:implement`.

---

## Metadata

| Field | Value |
|---|---|
| `questId` | `TitheBarnShortfall` (asset `Quest_TitheBarnShortfall`) |
| `title` | Counted Twice |
| `description` | Reeve Pell counts the tithe barn at dusk and again at dawn, and every week the dawn count comes up short. The lock is never forced. Pell wants the thief's name, and until he has it, everyone new is on his list. |

---

## Facts

### To create

| Asset name | Type | Key / trigger |
|---|---|---|
| `DialogueFact_FarmReeve_Shortfall_Accepted` | `DialogueFact` | "I'll find your thief." choice in `Choice_FarmReeve_Shortfall` |
| `DialogueFact_FieldHand_BarnTalk` | `DialogueFact` | `Start_FieldHand_BarnTalk` plays (Pim's "barn isn't that secure" topic) |
| `DialogueFact_BarnWorker_BarnTalk` | `DialogueFact` | `Start_BarnWorker_BarnTalk` plays (Gerrit's "not one of us" topic) |
| `KilledFact_BarnWorker_Gerrit` | `KilledFact` | Gerrit's `PersistentID` (generated with the NPC; not read by the quest) |
| `DialogueFact_Cook_BarnTip` | `DialogueFact` | "Twenty coin. Tell me." choice in `Choice_Cook_BarnTip` (`goldCost` 20) |
| `WorldFact_TitheBarn_ThiefCaught` | `WorldFact` | Hour- and NPC-gated `WorldFactTrigger` at the barn door (P3): player enters 00–03 while Venn is there, alive and up |
| `DialogueFact_BarnGuard_Refused` | `DialogueFact` | "Pell gets your name." choice (either Venn choice node) |
| `DialogueFact_BarnGuard_DealStruck` | `DialogueFact` | "Deal." choice in `Choice_BarnGuard_Deal` |
| `DialogueFact_FarmReeve_Shortfall_ReportedGuard` | `DialogueFact` | "It's Venn, a garrison man. He has a key." choice in `Choice_FarmReeve_ShortfallReport` |
| `DialogueFact_FarmReeve_Shortfall_BlamedPim` | `DialogueFact` | "It's Pim, from your field gang." choice (gated on the deal) |
| `DialogueFact_StashOwner_VennSentYou` | `DialogueFact` | `Start_StashOwner_VennSentYou` plays (mafia-route hook for later quests) |
| `KilledFact_BarnGuard` | `KilledFact` | Venn's `PersistentID` (`Game/World/Generate Missing KilledFacts`) |
| `KilledFact_FieldHand_Pim` | `KilledFact` | Pim's `PersistentID` (generated with the NPC; not read by the quest) |
| `ElapsedFact_FarmReeve_Shortfall_ReportedGuard_12h` | `ElapsedFact` | `ReportedGuard` + 12 h → removes Venn (P4) |
| `ElapsedFact_FarmReeve_Shortfall_BlamedPim_12h` | `ElapsedFact` | `BlamedPim` + 12 h → removes Pim (P4) |
| `QuestFact_TitheBarnShortfall_Started` | `QuestFact` | IsStarted |
| `QuestFact_TitheBarnShortfall_Completed` | `QuestFact` | IsCompleted |
| `QuestFact_TitheBarnShortfall_Failed` | `QuestFact` | IsFailed |

### Already exists (reference only)

| Asset name | Type | Where it lives |
|---|---|---|
| `WorldFact_Arrival_Done` | `WorldFact` | `Data/Facts/` (set by `ArrivalSequence`), unlocks Pell's shortfall topic |

---

## Quest Conditions

```
startPart:
  fact:  DialogueFact_FarmReeve_Shortfall_Accepted
  entry: "Reeve Pell's tithe barn is short every dawn, and the lock is never forced. He'll pay for the thief's name."

completedParts:     # ANY true → completed
  - fact:  DialogueFact_FarmReeve_Shortfall_ReportedGuard
    entry: "I gave Pell the thief's name: Osric Venn, a garrison guard with a key to the barn."
  - fact:  DialogueFact_FarmReeve_Shortfall_BlamedPim
    entry: "I gave Pell a name, just not the right one. Pim from the field gang will pay for Venn's sacks."

failedParts:
  - fact:  KilledFact_BarnGuard
    entry: "Venn is dead. Whatever he knew about the barn went into the ground with him."
```

Completion wins over failure (`QuestSO.IsFailed` is false once completed), so killing Venn after the
report changes nothing. Pell's report topic closes on `Failed` so a dead Venn can't flip a failed quest to
completed (deferred-work, sealed-passage review).

---

## Steps

```
steps:
  - title: "Ask around the farm"
    description: "The field gang works next to the barn every day. Someone there may have noticed something."
    parts:
      - fact:  DialogueFact_FieldHand_BarnTalk
        entry: "Pim says nobody watches the barn once the gang is out. Anyone hungry enough could slip in."
      - fact:  DialogueFact_BarnWorker_BarnTalk
        entry: "Gerrit doesn't think it's a worker, since they'd be the first suspected. It's someone who knows the place well, though. Someone nobody counts."
    # Optional: active once either talk happens, done when both have.

  - title: "A man with a key"
    description: "Aldric the cook hears what sells in the camp after dark. He might know where Pell's grain goes."
    parts:
      - fact:  DialogueFact_Cook_BarnTip
        entry: "Aldric says the barn grain reaches the gang's tent after midnight, carried by a man who didn't need to climb in."
    # Optional: a player who goes straight to the stakeout never activates this step (no part true → hidden).

  - title: "Watch the barn"
    description: "Whoever empties the barn does it at night. Wait by the tithe barn after midnight."
    parts:
      - fact:  WorldFact_TitheBarn_ThiefCaught
        entry: "I caught Osric Venn, a garrison guard, carrying sacks out of the tithe barn with his own key."
```

---

## Quest Dependencies

```
reads:
  - WorldFact_Arrival_Done: Pell's shortfall topic only opens after the arrival
read_by:
  - Mem_FarmReeve_ShortfallReport: unlocks on WorldFact_TitheBarn_ThiefCaught; closes on QuestFact_TitheBarnShortfall_Failed
  - Mem_FieldHand_BarnTalk / Mem_BarnWorker_BarnTalk: unlock on QuestFact_TitheBarnShortfall_Started
  - Mem_Cook_BarnTip: unlocks on QuestFact_TitheBarnShortfall_Started; closes on Completed / Failed
  - Mem_BarnGuard_Caught: closes on Completed / Failed
  - Mem_StashOwner_VennSentYou: mafia-route hook (DialogueFact_StashOwner_VennSentYou is the read point for future mafia quests)
  - Mem_FarmReeve_ShortfallAfter_Guard / _Pim: aftermath line per ending
  - Gang revenge follow-up (to design): reads ReportedGuard + DealStruck
```

---

## NPC Involvement

```
npcs:
  - npc: FarmReeve (Reeve Wendel Pell, npc-016)
    exists: true
    folder: Assets/_Game/Data/NPCs/FarmReeve/

    memories:
      - asset: Mem_FarmReeve_Shortfall
        unlock:     [WorldFact_Arrival_Done]
        invalidate: [DialogueFact_FarmReeve_Shortfall_Accepted]
        dialogue_topic: Shortfall   # topic text: "About the barn being short."
        dialogue:
          - NPC: "Short. Again. I count at dusk and the barn is right. I count at dawn and it's two sacks short, three some weeks. Never more, never less. Someone is being careful."
          - NPC: "And the lock is never forced. I check it myself. So either I've a ghost in my barn, or someone walks in through the door like he owns it."
          - Player: "I'll find your thief."  → Text_FarmReeve_Shortfall_Accept  [sets: DialogueFact_FarmReeve_Shortfall_Accepted]
          - Player: "Not my problem."  → END
          - NPC (Accept): "You? Well, you're new, so you're either my thief or the one man in this camp who isn't in on it yet. It goes at night. Bring me a name, a real one, and I'll pay for it. Twenty-five coin, counted twice."

      - asset: Mem_FarmReeve_ShortfallReport
        unlock:     [QuestFact_TitheBarnShortfall_Started, WorldFact_TitheBarn_ThiefCaught]
        invalidate: [DialogueFact_FarmReeve_Shortfall_ReportedGuard, DialogueFact_FarmReeve_Shortfall_BlamedPim, QuestFact_TitheBarnShortfall_Failed]
        dialogue_topic: ShortfallReport   # "I know who's taking your grain."
        dialogue:
          - NPC: "Well? A name. I've had a week of guesses, I don't need another."
          - Player: "It's Venn, a garrison man. He has a key."  → Text_FarmReeve_Report_Guard  [sets: DialogueFact_FarmReeve_Shortfall_ReportedGuard]
          - Player: "It's Pim, from your field gang."  → Text_FarmReeve_Report_Pim  [sets: DialogueFact_FarmReeve_Shortfall_BlamedPim]  [requires: Mem_FarmReeve_GateDeal]
          - Player: "Not yet."  → END
          - NPC (Guard): "A Company man, with a Company key. So it was never my count that was wrong. I'll take it up to the Warden myself, and I'll write down who brought it to me. Here, twenty-five, as promised."
          - NPC (Pim): "Pim? I had him down as slow, not careful. Then again, slow men count badly too. I'll have his blanket turned over before the gang goes out. Twenty-five, as promised."

      - asset: Mem_FarmReeve_GateDeal        # gate memory, no start dialogue (V15 via gated choice, V17 same NPC)
        unlock:     [DialogueFact_BarnGuard_DealStruck]
        invalidate: []

      # Aftermath: one repeatable memory per ending (a topic can't branch on a fact).
      - asset: Mem_FarmReeve_ShortfallAfter_Guard
        unlock:     [DialogueFact_FarmReeve_Shortfall_ReportedGuard]
        invalidate: []
        dialogue_topic: ShortfallAfter   # "How's the count?"
        dialogue:
          - NPC: "Right at dusk and right at dawn, every night since. They took Venn up to the fort. I didn't ask what for. I know what for, and I've written it down."

      - asset: Mem_FarmReeve_ShortfallAfter_Pim
        unlock:     [DialogueFact_FarmReeve_Shortfall_BlamedPim]
        invalidate: []
        dialogue_topic: ShortfallAfter   # "How's the count?"
        dialogue:
          - NPC: "Right at dawn since Pim went. Mostly. Funny, I never had him down as clever enough. Still, the sack was under his blanket, and a sack doesn't lie."
          # "Mostly": Venn is still stealing, just less. A seed for a later beat, not a mechanic.

  - npc: Cook (Aldric Sorn, npc-011)
    exists: true
    folder: Assets/_Game/Data/NPCs/Cook/

    memories:
      - asset: Mem_Cook_BarnTip
        unlock:     [QuestFact_TitheBarnShortfall_Started]
        invalidate: [DialogueFact_Cook_BarnTip, QuestFact_TitheBarnShortfall_Completed, QuestFact_TitheBarnShortfall_Failed, WorldFact_TitheBarn_ThiefCaught]
        dialogue_topic: BarnTip   # "Pell's barn is short. Heard anything?"
        dialogue:
          - NPC: "Pell's grain? Why would a cook hear about grain? Well. Supposing I did, what would hearing it be worth to you?"
          - Player: "Twenty coin. Tell me."  → Text_Cook_BarnTip  [sets: DialogueFact_Cook_BarnTip]  (goldCost 20)
          - Player: "Nothing. Forget it."  → END
          - NPC (Tip): "Barn sacks turn up at the gang's tent, late, after midnight. Good sacks, Company stitching. And the man who brings them doesn't come over the fence. He walks the fields road like he's on duty. Make of that what you like."

  - npc: BarnGuard (Osric Venn, npc-017)
    exists: false
    identity_notes: >
      Garrison guard (Faction_Company), mid 30s. Keeps the second key to the tithe barn for the fort's
      ration draws. Sells two or three sacks a week to the gang's black market through Brann. Easy
      charm, practical, not cruel. He thinks of it as a tax on a Company that taxes everyone.
      Voice: friendly and reasonable, a salesman's ease, reframes everything as a fair arrangement.
      Taboo: being called a thief. Routine: day post at the fields gate, sleeps in the barracks
      (garrison quarter), 00–03 Stand at Spot_BarnGuard_BarnDoor with sacks (P5). Company armor +
      short sword like the other guards (NpcArmament). Run /NPC:create.

    memories:
      - asset: Mem_BarnGuard_Greeting
        unlock:     []
        invalidate: [WorldFact_TitheBarn_ThiefCaught]
        dialogue_topic: Greeting
        dialogue:
          - NPC: "Fields gate. Workers out at dawn, in at dusk, and nobody in between who hasn't got a reason. Have you got one? Good. Then we're friends."

      - asset: Mem_BarnGuard_Caught
        unlock:     [WorldFact_TitheBarn_ThiefCaught]
        invalidate: [DialogueFact_BarnGuard_Refused, DialogueFact_BarnGuard_DealStruck, QuestFact_TitheBarnShortfall_Completed, QuestFact_TitheBarnShortfall_Failed]
        dialogue_topic: Caught   # "Those are Pell's sacks."
        dialogue:
          - NPC: "Easy. You didn't see a man stealing, you saw a man moving the Company's grain from one Company place to another. All right, you saw a bit more than that. So what happens now?"
          - Player: "Pell gets your name."  → Text_BarnGuard_Refused  [sets: DialogueFact_BarnGuard_Refused]
          - Player: "What's it worth to you?"  → Choice_BarnGuard_Deal
          - NPC (Refused): "My word against a worker's? Go on, then. See which one the fort believes."
          - Choice_BarnGuard_Deal NPC: "Forty now, and nothing said by me either. But Pell wants a name, and he'll keep counting till he gets one. So give him one. Pim, from the field gang. Slow lad, sleeps near the barn wall. I'll make sure a sack turns up under his blanket."
          - Player: "Deal."  → Text_BarnGuard_Deal  [sets: DialogueFact_BarnGuard_DealStruck]  (+40 gold, PlayerRewardSO on the fact)
          - Player: "No. Pell gets your name."  → Text_BarnGuard_Refused  [sets: DialogueFact_BarnGuard_Refused]
          - NPC (Deal): "Sensible. You'd be surprised how rare that is. If you ever want proper work, go and see Brann at the gang house. Tell him Venn sent you."

      # Follow-ups so Venn never opens an empty topic list after the confrontation.
      - asset: Mem_BarnGuard_AfterRefused
        unlock:     [DialogueFact_BarnGuard_Refused]
        invalidate: [DialogueFact_FarmReeve_Shortfall_ReportedGuard]
        dialogue_topic: AfterRefused   # "About the barn."
        dialogue:
          - NPC: "Still here? I'd have thought you'd be halfway to Pell by now. Go on, then. I'll still be standing at this gate tomorrow, and the day after, and you'll still be a worker. Think about which of us the fort keeps."

      - asset: Mem_BarnGuard_AfterDeal
        unlock:     [DialogueFact_BarnGuard_DealStruck]
        invalidate: [DialogueFact_FarmReeve_Shortfall_ReportedGuard]
        dialogue_topic: AfterDeal   # "About our arrangement."
        dialogue:
          - NPC: "Our arrangement? Never heard of it, friend, and neither have you. That's what makes it work. Seen Brann yet? Do. He likes people who can count without saying the numbers out loud."

      - asset: Mem_BarnGuard_AfterReported
        unlock:     [DialogueFact_FarmReeve_Shortfall_ReportedGuard]
        invalidate: []   # Venn is removed 12 h after the report (P4)
        dialogue_topic: AfterReported   # "Pell knows."
        dialogue:
          - NPC: "So you went to him. Fine. They'll walk me up the hill, and a sergeant will shout, and in a month I'll be on another gate. But the men I sold to don't get walked up any hill, worker. They don't forget a name either. Yours, for instance."
          # Same line for the double-cross (deal taken, then reported). It foreshadows the revenge follow-up below.

  - npc: FieldHand (Pim, npc-018)
    exists: false
    identity_notes: >
      Young field-gang worker, slow-spoken, trusting, tired. Sleeps in a tent-camp hut near the fields
      path (first field-gang roster entry). Day: works the strips (Stand / Patrol on the farm). No
      faction. He's the scapegoat in the deal ending and is removed 12 h after Pell hears his name (P4).
      Run /NPC:create.

    memories:
      - asset: Mem_FieldHand_Greeting
        unlock:     []
        invalidate: [DialogueFact_FarmReeve_Shortfall_BlamedPim]
        dialogue_topic: Greeting
        dialogue:
          - NPC: "Barley's coming in thin this year. Reeve says it's the soil. I think it's the rain. Nobody asks me, though. Nobody ever asks me anything."

      - asset: Mem_FieldHand_BarnTalk
        unlock:     [QuestFact_TitheBarnShortfall_Started]
        invalidate: [DialogueFact_FieldHand_BarnTalk, WorldFact_TitheBarn_ThiefCaught, QuestFact_TitheBarnShortfall_Completed, QuestFact_TitheBarnShortfall_Failed]
        dialogue_topic: BarnTalk   # "Pell says the barn keeps coming up short."
        # Start node sets DialogueFact_FieldHand_BarnTalk.
        dialogue:
          - NPC: "Short again? He'll have us all counted twice by supper. Thing is, the barn's not that secure, you know. Pell checks the lock morning and night, and that's all he checks. Once the gang's out on the strips there's nobody watching it at all. Someone could sneak in easy when no one's looking."
          - Player: "Any idea who it might be?"  → Text_FieldHand_BarnTalk_Who
          - Player: "Thanks, Pim."  → END
          - NPC (Who): "Me? No idea. Someone who's really hungry, perhaps? Only everyone here is hungry, so that's not much of a guess, is it. Don't tell the reeve I said that. He already looks at me funny."
        # Design intent: Pim is innocent and naive. His "hungry worker" guess points the wrong way and makes
        # him the easy name Venn picks later.

      - asset: Mem_FieldHand_BarnTalkAgain   # optional repeat after the first talk
        unlock:     [DialogueFact_FieldHand_BarnTalk]
        invalidate: [WorldFact_TitheBarn_ThiefCaught, QuestFact_TitheBarnShortfall_Completed, QuestFact_TitheBarnShortfall_Failed]
        dialogue_topic: BarnTalkAgain   # "About the barn again."
        dialogue:
          - NPC: "I told you all I know, honest. Nobody watches it, and everybody's hungry. If you find him, will you tell Pell it wasn't me? He'd believe you before he believed me."

  - npc: BarnWorker (Gerrit, npc-019)
    exists: false
    identity_notes: >
      Old field-gang hand, early 60s, works the threshing floor and loads the barn under Pell's eye. He has
      outlasted three reeves. Dry, unhurried, shrewd: he thinks out loud and reaches the right answer
      by ruling things out. No faction. Day: threshing floor (Stand). Night: tent-camp hut near Pim. He
      doesn't name names, because naming a guard gets a worker killed. Run /NPC:create.

    memories:
      - asset: Mem_BarnWorker_Greeting
        unlock:     []
        invalidate: []
        dialogue_topic: Greeting
        dialogue:
          - NPC: "Thresh it, sack it, carry it, and let Pell count it. Third reeve I've done that for. They change, the sacks don't."

      - asset: Mem_BarnWorker_BarnTalk
        unlock:     [QuestFact_TitheBarnShortfall_Started]
        invalidate: [DialogueFact_BarnWorker_BarnTalk, QuestFact_TitheBarnShortfall_Completed, QuestFact_TitheBarnShortfall_Failed]
        dialogue_topic: BarnTalk   # "Pell has me looking for his grain thief."
        # Start node sets DialogueFact_BarnWorker_BarnTalk.
        dialogue:
          - NPC: "Has he now. Well, it's not one of us, I'll tell you that for free. Think about it. The first morning a sack's gone, who does Pell look at? The gang. Us. A worker who took from that barn would be the first man suspected and the first man hanged. You'd have to be a fool, and the fools here don't last long enough to steal."
          - Player: "Then who?"  → Text_BarnWorker_BarnTalk_Who
          - Player: "Maybe one of you is a careful fool."  → Text_BarnWorker_BarnTalk_Fool
          - NPC (Who): "Someone who knows this place well, mind. Knows when Pell counts and when he sleeps, knows where the lamps aren't, and walks past that lock like it's nothing to him. Someone who's here a lot, and that nobody bothers counting. That's as far as I go. Names get men killed in this camp, and it's never the man named."
          - NPC (Fool): "Careful fools are still fools. Three sacks a week, every week, for months, and never a forced lock? That's not a hungry man. A hungry man takes too much once and gets caught. This one takes just enough, like he's being paid for it."
          # Both branches end; the start node already set the fact.

  - npc: StashOwner (Brann, npc-003)
    exists: true
    folder: Assets/_Game/Data/NPCs/StashOwner/

    memories:
      - asset: Mem_StashOwner_VennSentYou
        unlock:     [DialogueFact_BarnGuard_DealStruck]
        invalidate: [DialogueFact_StashOwner_VennSentYou, DialogueFact_FarmReeve_Shortfall_ReportedGuard]
        dialogue_topic: VennSentYou   # "Venn sent me."
        dialogue:
          - NPC: "Venn says you know when to keep your mouth shut. That's rarer than it should be, newcomer. I'll have work for you. Not today. When I do, you'll hear it from me and nobody else."
          # Sets DialogueFact_StashOwner_VennSentYou on the start node — read by the future mafia quests.
```

---

## Rewards

```
rewards:
  onStart: null

  onStepCompleted:
    - stepIndex: 2          # caught Venn at the barn
      xp: 50
      lp: 0
      gold: 0
      stats: []

  onCompleted:
    xp: 150
    lp: 0
    gold: 0
    stats: []

  onFact:                   # PlayerRewardSO, FactType = Dialogue
    - fact: DialogueFact_BarnGuard_DealStruck
      gold: 40              # Venn's cut
    - fact: DialogueFact_FarmReeve_Shortfall_ReportedGuard
      gold: 25              # Pell's fee
    - fact: DialogueFact_FarmReeve_Shortfall_BlamedPim
      gold: 25              # Pell's fee (paid for the wrong name)

  onFailed: null
```

Scale reference: SealedPassage endings 150 XP, WardensErrand completion 100 XP.

---

## Design notes / open items

- **Consequence:** the named man is removed 12 h after the report (P4). The report-Venn ending is the only
  one that leaves the barn "honest". The deal ending keeps a corrupt key-holder at the barn. The later
  worker-path "secure food reserves" quest can use Venn as its way in (deal) or face a new lock (report).
- **Follow-up needed: Venn takes the fall → gang revenge (to design, own spec/quest).** Reporting Venn
  cuts the gang's grain supply, and nothing in the world reacts to that yet. Planned direction: some time
  after `ElapsedFact_FarmReeve_Shortfall_ReportedGuard_12h` (Venn gone), gang members from the Mafia Zone
  jump the player at night, e.g. on the lakeside or fields path. This could be a beating that leaves the
  player knocked out and robbed (existing knockout + loot flow) rather than a fight to the death. Brann
  should know about it ("you cost us a supplier") and may offer a way to settle the debt, which turns the
  revenge into an entry to the mafia route from the other side. A double-cross (deal taken, then Venn
  named) should make it worse. Read: `DialogueFact_FarmReeve_Shortfall_ReportedGuard`,
  `DialogueFact_BarnGuard_DealStruck`. Venn's `Mem_BarnGuard_AfterReported` line already foreshadows it.
  Also tracked in `deferred-work.md`.
- **Double-cross:** taking the deal and then naming Venn is allowed. The player keeps the 40 gold, Venn is
  removed, and Brann's hook closes. Nothing punishes it yet. A later mafia beat can read
  `DialogueFact_BarnGuard_DealStruck` + `_ReportedGuard`.
- **Stakeout without the leads:** a player who skips Pim, Gerrit and Aldric can still catch Venn. Steps 0–1 then
  never show.
- **Clue design:** the three leads point at the guard from different angles. Pim (wrongly) points at hungry
  workers, Gerrit rules the workers out and describes the guard without naming him, and Aldric says where the
  grain goes and that the carrier walks the road "like he's on duty".
- **Venn attacked or knocked out:** a knockout doesn't fail the quest, and the trigger (P3) won't fire while
  he's down. Killing him fails the quest, unless the report has already been made.
- **Refused, then never reported:** the quest stays open with the report topic available. That's fine.
- **Sacks:** a theftable sack container at Venn's spot during the window is a nice extra (theft-return
  flow), not required.

---

## Implementation Checklist

- [ ] Prerequisites P1–P4 shipped (`gds-quick-dev`), P5 done in implement
- [ ] Fact assets created — Dialogue / World / Quest / Elapsed facts in `Assets/_Game/Data/Facts/`; KilledFacts via
      `Game/World/Generate Missing KilledFacts` (`Data/Enemies/StartingTown/`) and wired on each `PersistentID`
- [ ] QuestSO created — `Assets/_Game/Data/Quests/TitheBarnShortfall/Quest_TitheBarnShortfall.asset`
- [ ] NPCs created — `Data/NPCs/BarnGuard/` (Venn, npc-017), `Data/NPCs/FieldHand/` (Pim, npc-018),
      `Data/NPCs/BarnWorker/` (Gerrit, npc-019); world-index rows
- [ ] Dialogue chains created — FarmReeve (Shortfall, ShortfallReport, ShortfallAfter ×2), Cook (BarnTip), BarnGuard
      (Greeting, Caught, AfterRefused, AfterDeal, AfterReported), FieldHand (Greeting, BarnTalk, BarnTalkAgain), BarnWorker (Greeting, BarnTalk), StashOwner (VennSentYou)
- [ ] Memory entries created under each NPC's `Memories/` (auto-synced)
- [ ] PlayerRewardSO assets created — step 2 (caught), completed, and the three gold facts
- [ ] Scene: Venn, Pim and Gerrit placed with routines and beds, `Spot_BarnGuard_BarnDoor`, barn-door trigger (P3),
      removal objects (P4)
- [ ] Registered — `QuestEventsManager` (auto) and `QuestLogUI._allQuests`
- [ ] `quest_report TitheBarnShortfall` shows no Error / Warning
