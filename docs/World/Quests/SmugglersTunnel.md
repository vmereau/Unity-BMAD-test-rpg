# Quest Spec — SmugglersTunnel

> Template version: 1.1
> Status: `ready`
> Spec path: `docs/World/Quests/SmugglersTunnel.md`
> Design with `/quests:design`, build with `/quests:implement SmugglersTunnel`, check with `/quests:audit SmugglersTunnel`.

Umbrella quest of the smuggling chain, given by Warden Voss (npc-006) after Counted Twice
(`TitheBarnShortfall`). The gang smuggles goods (field grain and whatever else it sells on the black market)
**out** of the camp through the Company's own cargo. Nothing goes missing from the Company's stores. The goods
travel from the gang house basement through the shored tunnel into the warehouse cellar's side room, get packed
in among the legitimate crates, and leave by barge. Two Company guards and one gang man keep the route:

| Role (in Brann's dock tally) | Who | Part in it |
|---|---|---|
| "the dock man" | Dock Guard (npc-013, personal name to set: suggestion **Garrick Dunn**) | Night watch of the docks 21–06. Counts the barge cargo out and chalks it short. Holds a tunnel key copy |
| "the gate" | **New:** night guard of the wilderness gate (suggestion **Edwy Marl**, folder `GateGuard`) | The gate stands right above the tunnel. Lets the gang's boats pass and keeps the gate road clear at night. Holds a tunnel key copy |
| "the door" | **New:** gang doorman (suggestion **Ketch**, folder `GangDoorman`) | Guards the gang house deck, the hidden door and the basement by day. Carries the tunnel and basement keys |

The Warehouse Guard (npc-012) is honest. The **Warehouse Manager** (new, tally room, alcoholic, clean, holds the
warehouse keys) belongs to the Company trust quests, not to this spec.

## The chain

| Quest | Role | Status |
|---|---|---|
| `SmugglersTunnel` (this spec) | Started by Voss. Tracks the discovery (both ends of the tunnel, the three accomplices) and the side the player picks | ready |
| Company trust quests (1–2) | Earn the Warehouse Manager's trust → cellar access (`Key_WarehouseCellar`) | to design (`/quests:design`) |
| Gang trust quests (1–2) | Earn Brann's trust (after `DialogueFact_StashOwner_VennSentYou`, or another way in) → a crate run through the tunnel with the gang keys | to design |

All the ways in meet at the same physical triggers (cellar end, tunnel, gang end), so this quest doesn't care
which route the player took:
- **Company:** the manager's key opens the cellar. The tunnel door is then picked, or opened with a looted or stolen key.
- **Gang:** the crate run walks the whole route.
- **Quick way:** keys or lockpicks only. The basement evidence gives the names, and Voss or Brann then hear about it.

## Story beats

1. **Voss's hunch** (start). Pell sends the player up to the Warden (Counted Twice edit, below). Voss sums up
   what he knows (Venn's tally, or a field hand who "steals a sack to eat, he doesn't run a market"). He thinks
   it's bigger: his cargo leaves counted twice, and yet the gang never runs short. He has no men to spare, and
   the ones he has wear the uniform he's asking about. The player should look into it when they can, and make
   friends with the gang if that's what it takes.
2. **The cellar's other door.** In the warehouse cellar's side room a door stands in the south wall, with a gang
   lock, not a Company one.
3. **The tunnel.** Behind it, a shored tunnel runs under the wilderness gate to a basement under the gang house,
   with a hidden door onto the gang's pier. The player can find it from either end.
4. **Who's in on it.** Evidence, any of:
   - **Brann's dock tally**, in a crate in the basement. Its text is a run of chalked anecdotes:
     - *"Fog night. Dock man counted 12 out, chalked 10. 6 coin."*
     - *"Gate let the second boat through, 5 extra, his girl back home's sick again."*
     - *"The door slept on watch, docked him 2."*
   - **A tunnel key copy** in the Dock Guard's or the gate guard's chest, by their bed.
   - **The meeting:** 02–03 every night, the Dock Guard comes to the basement to settle the count with the
     doorman. A player who sneaks in and sees them sets `MeetingSeen`. Being spotted there is trespass on the
     gang's basement (warning, chase, fight).

   With evidence, the player confronts each man (one topic each). Every evidence choice names him, and the
   confrontations are optional.
5. **The side.**
   - **Company:** tell Voss about the tunnel (both ends found), with or without the names. Nobody is removed
     (they're kept for later quests). The guards get a slap on the wrist (a month's pay, the worst posts), and
     Brann's goods and money are seized.
   - **Gang:** tell Brann "I've been through your tunnel" and pick "I'm with you". Brann hands the player a cover
     story: he only took some field grain to feed his friends. The player tells Voss that story. Voss doesn't
     believe it's all, but it's all he can punish (Brann has ties with the guards and the Company): a fine and a
     morning on the stage. Opens more gang relations and opportunities.

   Either side closes the other. No double-cross in this quest.

No fail state. Killing an accomplice only removes his evidence path.

---

## Prerequisites (system / scene work — separate `gds-quick-dev` specs, before `/quests:implement`)

| # | Need | Why | Notes |
|---|---|---|---|
| P1 | **Locks and keys on the route** (done, `spec-smuggling-route-locks-and-keys`) | The user wants all three doors locked, lockpickable at Lockpicking 1 | `Lockable` (`_requiredSkill` = `Skill_Begginer_Lockpicking`, `_keyItem`) on `Warehouse/CellarDoor` (`Key_WarehouseCellar`), `GangHouse/HiddenDoor` (`Key_GangBasement`), and **two new tunnel doors**, one at the cellar side room's opening and one at the basement mouth (`Key_GangTunnel`, one key for both). Built: `GangHouse/TunnelDoor_Basement` on the tunnel's last shoring frame outside the basement's west wall, `Warehouse/TunnelDoor_Cellar` in the side room's opening (each builder sets its lock), out of the bake. Keys are `KeyItemSO`s |
| P2 | **Key holders** (done, `spec-smuggling-route-locks-and-keys`) | Keys come from inventories and chests (no pickpocketing yet, see `docs/IDEAS.md`) | `Key_WarehouseCellar`: Warehouse Guard's inventory (loot), Warehouse Manager (Company trust quest). `Key_GangTunnel` + `Key_GangBasement`: doorman's inventory, `BrannsStash`. `Key_GangTunnel` copy: a sleeping chest (owned container) at the Dock Guard's bed and at the gate guard's bed (garrison barracks), each with a `ContainerTakeFact` |
| P3 | **Dock tally + basement crate** (done, `spec-smuggling-route-locks-and-keys`) | Quick-way evidence | `Item_DockTally` (`ItemSO.description` carries the anecdotes, readable in the inventory), in an owned crate in the basement (owner: doorman or Brann), `ContainerTakeFact` → `WorldFact_Smuggling_DockTallyTaken` |
| P4 | **Route triggers** | Steps 0–1 and the meeting | `WorldFactTrigger`s: side room (`CellarEndFound`), mid-tunnel (`TunnelWalked`), basement (`GangEndFound`), and the meeting (`MeetingSeen`: basement box, hours 2→3, required NPCs doorman **and** Dock Guard near the meeting spot) |
| P5 | **NPCs reach the basement** | The 02–03 meeting | Today the hidden door and the tunnel are out of the bake on purpose (`NavMesh/TunnelStrip`). The basement (and the deck → hidden door way) must be walkable for agents, the tunnel stays not walkable, and an NPC must get through a locked door on its routine (needs P10). **Fallback:** hold the meeting on the gang deck in front of the hidden door, already reachable |
| P10 | **NPCs open and close doors** (future spec) | The meeting walks the Dock Guard and Ketch through the hidden door and the tunnel doors, and later routines will cross other doors | NPCs can't use doors today. A future `gds-quick-dev` spec: an NPC on a path opens a door (unlocking it when it holds the `Lockable._keyItem`), passes, then closes it, and locks it again if it was locked, so the player's locks stay meaningful. Includes NavMesh links across door leaves that are out of the bake. Until it ships, use P5's fallback |
| P6 | **Basement restricted area** | "Confront them, probably a fight" | `RA_GangBasement` (basement + tunnel's gang half), owned by the doorman (and Brann). The existing trespass flow (warn → chase → `ShouldFightTrespasser`) gives the fight with no new code |
| P7 | **Seizure of Brann's goods and money** | Company ending | A fact-driven way to empty `BrannsStash` (items + gold) and Brann's purse on `Reported` / `ReportedAll`, plus `FactRemovedObject` on the basement goods props. A small new component (e.g. `FactClearedContainer`). Nobody is removed |
| P8 | **New NPCs** | Accomplices | `/NPC:create` for the gate guard and the doorman; fill in the Dock Guard's identity (name, background). Routines: doorman 06–21 stands on the deck by the hidden door, 02–03 in the basement, sleeps in the gang house. Gate guard 21–06 at the wilderness gate (Harl holds it 14–21), sleeps by day in a barracks bed with his chest. Dock Guard leaves his patrol 02–03 for the meeting |
| P9 | **Counted Twice edits** | Pell sends the player to Voss | See Quest Dependencies → `edits` |

---

## Metadata

| Field | Value |
|---|---|
| `questId` | `SmugglersTunnel` (asset `Quest_SmugglersTunnel`) |
| `title` | Bigger Than It Looks |
| `description` | Warden Voss thinks Venn's grain was the small end of something. The Company's cargo leaves the camp clean and counted, and yet the gang never runs short of what it sells. Voss is short of men he can trust, so he wants the new face to find out how it's done. Ask around, earn a friend or two, and see where it leads. |

---

## Facts

### To create

| Asset name | Type | Key / trigger |
|---|---|---|
| `DialogueFact_Warden_Smuggling_Assigned` | `DialogueFact` | Start nodes `Start_Warden_Smuggling_Venn` and `Start_Warden_Smuggling_Pim` (both set it) |
| `WorldFact_Smuggling_CellarEndFound` | `WorldFact` | `WorldFactTrigger` in the warehouse cellar's side room, in front of the tunnel door (P4) |
| `WorldFact_Smuggling_TunnelWalked` | `WorldFact` | `WorldFactTrigger` mid-tunnel (P4) |
| `WorldFact_Smuggling_GangEndFound` | `WorldFact` | `WorldFactTrigger` in the gang house basement (P4) |
| `WorldFact_Smuggling_DockTallyTaken` | `WorldFact` | `ContainerTakeFact` on the basement crate, item `Item_DockTally` (P3) |
| `WorldFact_Smuggling_DockGuardKeyFound` | `WorldFact` | `ContainerTakeFact` on the Dock Guard's chest, item `Key_GangTunnel` (P2) |
| `WorldFact_Smuggling_GateGuardKeyFound` | `WorldFact` | `ContainerTakeFact` on the gate guard's chest, item `Key_GangTunnel` (P2) |
| `WorldFact_Smuggling_MeetingSeen` | `WorldFact` | Hour- and NPC-gated `WorldFactTrigger` in the basement, 02–03, doorman + Dock Guard there (P4) |
| `DialogueFact_DockGuard_Smuggling_Named` | `DialogueFact` | Any evidence choice in `Choice_DockGuard_Smuggling` |
| `DialogueFact_GateGuard_Smuggling_Named` | `DialogueFact` | Any evidence choice in `Choice_GateGuard_Smuggling` |
| `DialogueFact_GangDoorman_Smuggling_Named` | `DialogueFact` | Any evidence choice in `Choice_GangDoorman_Smuggling` |
| `DialogueFact_StashOwner_Smuggling_Joined` | `DialogueFact` | "I'm with you." in `Choice_StashOwner_Tunnel` |
| `DialogueFact_StashOwner_Smuggling_Refused` | `DialogueFact` | "I'm with the Warden." in `Choice_StashOwner_Tunnel` |
| `DialogueFact_Warden_Smuggling_ReportedAll` | `DialogueFact` | Full report choice (route + three names) in `Choice_Warden_SmugglingReport` |
| `DialogueFact_Warden_Smuggling_Reported` | `DialogueFact` | Route-only report choice |
| `DialogueFact_Warden_Smuggling_CoverStory` | `DialogueFact` | Brann's cover story choice (gated on `Joined`) |
| `KilledFact_GateGuard`, `KilledFact_GangDoorman` | `KilledFact` | New NPCs' `PersistentID` (generated with the NPC; not read by the quest) |
| `QuestFact_SmugglersTunnel_Started` | `QuestFact` | IsStarted |
| `QuestFact_SmugglersTunnel_Completed` | `QuestFact` | IsCompleted |

### Already exists (reference only)

| Asset name | Type | Where it lives |
|---|---|---|
| `DialogueFact_FarmReeve_Shortfall_ReportedGuard` | `DialogueFact` | Counted Twice: unlocks Voss's Venn version |
| `DialogueFact_FarmReeve_Shortfall_BlamedPim` | `DialogueFact` | Counted Twice: unlocks Voss's Pim version |
| `KilledFact_DockGuard` | `KilledFact` | `Data/Enemies/StartingTown/Generic/` (not read) |

---

## Quest Conditions

```
startPart:
  fact:  DialogueFact_Warden_Smuggling_Assigned
  entry: "Voss thinks Venn's grain was the small end of something bigger. He has no men to spare, so the new face gets to find out how it's done."

completedParts:     # ANY true → completed
  - fact:  DialogueFact_Warden_Smuggling_ReportedAll
    entry: "I told Voss about the tunnel and named the men who keep it. The two guards lose a month's pay, and Brann loses his goods and his purse."
  - fact:  DialogueFact_Warden_Smuggling_Reported
    entry: "I told Voss about the tunnel between his cellar and the gang house. Brann's goods are seized. The men who held the door are still nobody's business but mine."
  - fact:  DialogueFact_Warden_Smuggling_CoverStory
    entry: "I told Voss what Brann wanted him told: a bit of field grain for friends, nothing more. Brann paid a fine, stood a morning on the stage, and kept his tunnel."

failedParts: []     # no fail state
```

---

## Steps

```
steps:
  - title: "The cellar's other door"
    description: "Whatever the gang sells leaves the camp somehow, and the Company's cargo leaves from the warehouse."
    parts:
      - fact:  WorldFact_Smuggling_CellarEndFound
        entry: "In the cellar's side room there's a door with a gang lock on it, not a Company one."

  - title: "Where the tunnel goes"
    description: "Someone dug a way into the Company's cellar. Find where it starts."
    parts:
      - fact:  WorldFact_Smuggling_TunnelWalked
        entry: "Behind it, a shored tunnel runs under the wilderness gate."
      - fact:  WorldFact_Smuggling_GangEndFound
        entry: "It comes up in a basement under the gang house, with a hidden door onto the gang's pier."
    # Either end first: the step shows as soon as one part is true.

  - title: "Who's in on it"
    description: "A tunnel needs men who look the other way at both ends."
    parts:
      - fact:  DialogueFact_DockGuard_Smuggling_Named
        entry: "Garrick Dunn, the night dock guard, counts the barges out and chalks them short."
      - fact:  DialogueFact_GateGuard_Smuggling_Named
        entry: "Edwy Marl keeps the wilderness gate at night, and the gang's boats go by him."
      - fact:  DialogueFact_GangDoorman_Smuggling_Named
        entry: "Ketch keeps the gang's door, the one behind the nets."
    # Optional: the route-only report doesn't need it.

  - title: "Brann's version"
    description: "Brann has a smaller story he'd like the Warden to believe."
    parts:
      - fact:  DialogueFact_StashOwner_Smuggling_Joined
        entry: "Brann wants Voss told that he only took some field grain to feed his friends. Nothing else."
    # Hidden unless the player sides with Brann.

# DEFERRED steps (add when the trust quests exist; their facts don't exist yet):
#   - "A way into the warehouse": QuestFact_<CompanyTrustQuest>_Completed
#   - "A friend in the gang":     QuestFact_<GangTrustQuest>_Completed
# Adding steps before the existing ones shifts QuestFact step indices: add them at the end, or edit through
# Quest Explorer, which remaps them.
```

---

## Quest Dependencies

```
reads:
  - DialogueFact_FarmReeve_Shortfall_ReportedGuard / _BlamedPim: unlock Voss's start topic, one version per Counted Twice ending
edits:        # Counted Twice (TitheBarnShortfall), dialogue only; no fact, step or reward changes
  - Text_FarmReeve_Report_Guard and Text_FarmReeve_Report_Pim: add a closing sentence
      "And go up to the Warden. He asked to see whoever found my thief, and he doesn't ask twice."
  - Quest_TitheBarnShortfall completedParts entries (both): append " Pell says the Warden wants a word."
  - docs/World/Quests/TitheBarnShortfall.md: record the same edits
read_by:
  - Company trust quests (to design): unlock on QuestFact_SmugglersTunnel_Started; end with Key_WarehouseCellar
  - Gang trust quests (to design): unlock on QuestFact_SmugglersTunnel_Started and/or DialogueFact_StashOwner_VennSentYou; the crate run sets the route WorldFacts by walking it
  - Mem_StashOwner_SmugglingAfter_Joined: the hook for later gang quests
  - Later quests that use the accomplices (they are kept, not removed): read ReportedAll / Reported / CoverStory
```

---

## NPC Involvement

```
npcs:
  - npc: Warden (Warden Voss, npc-006)
    exists: true
    folder: Assets/_Game/Data/NPCs/Warden/

    memories:
      - asset: Mem_Warden_Smuggling_Venn
        unlock:     [DialogueFact_FarmReeve_Shortfall_ReportedGuard]
        invalidate: [DialogueFact_Warden_Smuggling_Assigned]
        dialogue_topic: Smuggling   # "Pell said you wanted to see me."
        # Start node sets DialogueFact_Warden_Smuggling_Assigned.
        dialogue:
          - NPC: "Pell brought me a slip of chalk with a guard's initials on it. Three sacks a week. A guard doesn't risk the fort for three sacks unless someone is buying them, and a buyer doesn't stop at three sacks."
          - → Text_Warden_Smuggling_Shared

      - asset: Mem_Warden_Smuggling_Pim
        unlock:     [DialogueFact_FarmReeve_Shortfall_BlamedPim]
        invalidate: [DialogueFact_Warden_Smuggling_Assigned]
        dialogue_topic: Smuggling   # "Pell said you wanted to see me."
        # Start node sets DialogueFact_Warden_Smuggling_Assigned.
        dialogue:
          - NPC: "Pell gave me a field hand. A field hand steals a sack to eat. He doesn't sell it, and the gang sells a great deal more than sacks."
          - → Text_Warden_Smuggling_Shared

        # Shared chain (both start nodes continue here):
          - NPC (Shared): "My cargo leaves this camp counted twice, by men I pay. And yet the gang never runs short of anything it sells. Either my count is wrong, or my counters are."
          - NPC (Shared_2): "I have no men to spare, and the men I have wear the uniform I'm asking about. You're new. Nobody has bought you yet. Find out how it's done, when your shifts allow. Make friends with the gang if that's what it takes. I won't ask how, only what."

      - asset: Mem_Warden_SmugglingReport
        unlock:     [DialogueFact_Warden_Smuggling_Assigned]
        invalidate: [DialogueFact_Warden_Smuggling_ReportedAll, DialogueFact_Warden_Smuggling_Reported, DialogueFact_Warden_Smuggling_CoverStory]
        dialogue_topic: SmugglingReport   # "About the cargo."
        dialogue:
          - NPC: "Well? I don't pay for news that there isn't any."
          - Player: "There's a tunnel from your cellar to the gang house. Your dock man, your gate man and the gang's doorman keep it."  → Text_Warden_Report_All  [sets: DialogueFact_Warden_Smuggling_ReportedAll]  [requires: Mem_Warden_GateAllNamed]
          - Player: "There's a tunnel from your warehouse cellar to the gang house."  → Text_Warden_Report_Route  [sets: DialogueFact_Warden_Smuggling_Reported]  [requires: Mem_Warden_GateRoute]
          - Player: "It's Brann. He takes field grain to feed his friends. That's all there is."  → Text_Warden_Report_Cover  [sets: DialogueFact_Warden_Smuggling_CoverStory]  [requires: Mem_Warden_GateJoined]
          - Player: "Not yet."  → END
          - NPC (All): "A hole in my own cellar, and two of my own men holding the lamp. They'll lose a month's pay and stand the worst posts I have, and from tonight they'll know someone is watching. Brann's goods are the Company's as of this evening, and so is his purse. Forty coin. Earned, for once."
          - NPC (Route): "A tunnel, and nobody to stand in front of me for it. Brann's goods are the Company's as of this evening. The men who held the door I'll find without you, in time. Twenty coin."
          - NPC (Cover): "Field grain, for friends. That's the story. Brann will pay for the grain and stand a morning on the stage, and he'll know as well as I do that it isn't the whole of it. It's what I can hang on him. Go."

      - asset: Mem_Warden_GateRoute          # gate memory, no start dialogue
        unlock:     [WorldFact_Smuggling_CellarEndFound, WorldFact_Smuggling_GangEndFound]
        invalidate: [DialogueFact_StashOwner_Smuggling_Joined]

      - asset: Mem_Warden_GateAllNamed       # gate memory
        unlock:     [WorldFact_Smuggling_CellarEndFound, WorldFact_Smuggling_GangEndFound, DialogueFact_DockGuard_Smuggling_Named, DialogueFact_GateGuard_Smuggling_Named, DialogueFact_GangDoorman_Smuggling_Named]
        invalidate: [DialogueFact_StashOwner_Smuggling_Joined]

      - asset: Mem_Warden_GateJoined         # gate memory
        unlock:     [DialogueFact_StashOwner_Smuggling_Joined]
        invalidate: []

  - npc: StashOwner (Brann, npc-003)
    exists: true
    folder: Assets/_Game/Data/NPCs/StashOwner/

    memories:
      - asset: Mem_StashOwner_Tunnel
        unlock:     [QuestFact_SmugglersTunnel_Started, WorldFact_Smuggling_GangEndFound]
        invalidate: [DialogueFact_StashOwner_Smuggling_Joined, DialogueFact_StashOwner_Smuggling_Refused, DialogueFact_Warden_Smuggling_Reported, DialogueFact_Warden_Smuggling_ReportedAll]
        dialogue_topic: Tunnel   # "I've been through your tunnel."
        dialogue:
          - NPC: "Have you, now. Then you know a thing men have gone into the lake for knowing. The tunnel's mine, newcomer, and so are the boats and the men at both ends of it. So you'll tell me, slowly: are you with me, or with the Warden?"
          - Player: "I'm with you."  → Text_StashOwner_Tunnel_Joined  [sets: DialogueFact_StashOwner_Smuggling_Joined]
          - Player: "I'm with the Warden."  → Text_StashOwner_Tunnel_Refused  [sets: DialogueFact_StashOwner_Smuggling_Refused]
          - NPC (Joined): "Good. Then you'll go up to Voss and tell him what he can afford to hear: Brann takes a bit of field grain to feed his friends, and nothing else. He'll fine me and stand me on the stage for a morning, and everyone goes back to work. Voss gets his thief, I keep my tunnel, and you get a friend who pays. Do it soon."
          - NPC (Refused): "Then go and tell him. He'll take my goods, dock two guards a month's pay and call it justice, because it's all he can afford. And I'll still be here after, newcomer. Remember that."

      - asset: Mem_StashOwner_SmugglingAfter_Joined
        unlock:     [DialogueFact_Warden_Smuggling_CoverStory]
        invalidate: []
        dialogue_topic: SmugglingAfter   # "About Voss."
        dialogue:
          - NPC: "A morning on the stage and a fine I'll have back in a week. Cheap. You kept your mouth shut and said the right thing, which is more than most. When you want proper work, you know where my door is."
          # Hook for the later gang quests.

      - asset: Mem_StashOwner_SmugglingAfter_Reported
        unlock:     [DialogueFact_Warden_Smuggling_Reported]   # also ReportedAll: needs a second memory (AND-only unlocks), same topic and dialogue
        invalidate: []
        dialogue_topic: SmugglingAfter
        dialogue:
          - NPC: "My chest is light, newcomer, and my purse is lighter. Voss's men carried it all up the hill, counted twice. I don't forget who showed them the way."
      # Mem_StashOwner_SmugglingAfter_ReportedAll: unlock [DialogueFact_Warden_Smuggling_ReportedAll], same dialogue.

  - npc: DockGuard (npc-013, name to set: Garrick Dunn)
    exists: true
    folder: Assets/_Game/Data/NPCs/DockGuard/
    identity_notes: >
      Fill in the draft sheet: night watch of the docks, in on the smuggling for the extra coin a guard's wage
      never gives. Counts barge cargo out and chalks it short. Keeps a tunnel key copy in the chest by his
      barracks bed. Leaves his patrol 02–03 for the basement meeting. Rule kept: every line ends on the rule.

    memories:
      - asset: Mem_DockGuard_Smuggling
        unlock:     [QuestFact_SmugglersTunnel_Started]
        invalidate: [DialogueFact_DockGuard_Smuggling_Named, DialogueFact_StashOwner_Smuggling_Joined, QuestFact_SmugglersTunnel_Completed]
        dialogue_topic: Smuggling   # "About the night barges."
        dialogue:
          - NPC: "The barges go out counted, signed and closed, same as every night. Nothing to ask about. So, off my dock."
          - Player: "Brann's tally says the dock man counts twelve and chalks ten."  → Text_DockGuard_Smuggling_Named  [sets: DialogueFact_DockGuard_Smuggling_Named]  [requires: Mem_DockGuard_GateTally]
          - Player: "Why does a Company guard keep a gang key in his chest?"  → Text_DockGuard_Smuggling_Named  [sets: DialogueFact_DockGuard_Smuggling_Named]  [requires: Mem_DockGuard_GateKey]
          - Player: "I saw you in the gang's basement last night."  → Text_DockGuard_Smuggling_Named  [sets: DialogueFact_DockGuard_Smuggling_Named]  [requires: Mem_DockGuard_GateMeeting]
          - Player: "Nothing."  → END
          - NPC (Named): "Twelve, ten. Every crate that leaves this dock leaves counted, and what's packed inside is somebody else's business. Tell Voss, and he docks my pay a month. Tell Brann you told Voss, and you'll learn to swim at night. Either way, the docks close at nine. Out."

      - asset: Mem_DockGuard_GateTally     # gate memories, no start dialogue
        unlock: [WorldFact_Smuggling_DockTallyTaken]
      - asset: Mem_DockGuard_GateKey
        unlock: [WorldFact_Smuggling_DockGuardKeyFound]
      - asset: Mem_DockGuard_GateMeeting
        unlock: [WorldFact_Smuggling_MeetingSeen]

  - npc: GateGuard (Edwy Marl)
    exists: false
    identity_notes: >
      Company guard (Faction_Company), late 20s, night post at the wilderness gate (21–06; Harl works it 14–21).
      A sick daughter back in the kingdom (no women or girls live in the Company encampment) and a wage that
      doesn't cover the healer there: he sends money home with the barges, and the gang pays the difference. Nervous, apologetic, too honest a face for it. Sleeps by day in a garrison barracks bed with
      a chest holding a tunnel key copy. Company armour + short sword (NpcArmament). Run /NPC:create.

    memories:
      - asset: Mem_GateGuard_Greeting
        unlock:     []
        invalidate: []
        dialogue_topic: Greeting
        dialogue:
          - NPC: "Gate's shut till dawn. I'm sorry, I don't make the hours. Back to the tents, please."

      - asset: Mem_GateGuard_Smuggling
        unlock:     [QuestFact_SmugglersTunnel_Started]
        invalidate: [DialogueFact_GateGuard_Smuggling_Named, DialogueFact_StashOwner_Smuggling_Joined, QuestFact_SmugglersTunnel_Completed]
        dialogue_topic: Smuggling   # "About the boats on the lake."
        dialogue:
          - NPC: "Boats? I watch the road, not the water. There's nothing on the lake at night but fog."
          - Player: "Brann's tally says the gate let the second boat through, for five extra."  → Text_GateGuard_Smuggling_Named  [sets: DialogueFact_GateGuard_Smuggling_Named]  [requires: Mem_GateGuard_GateTally]
          - Player: "There's a gang key in the chest by your bed."  → Text_GateGuard_Smuggling_Named  [sets: DialogueFact_GateGuard_Smuggling_Named]  [requires: Mem_GateGuard_GateKey]
          - Player: "Nothing."  → END
          - NPC (Named): "Five coin. You know what a healer back in the kingdom asks for a fever? More than the Company pays me in a month. I don't carry anything, I don't touch anything. I look at the road when the boats go by. That's all. Please. That's all it is."

      - asset: Mem_GateGuard_GateTally
        unlock: [WorldFact_Smuggling_DockTallyTaken]
      - asset: Mem_GateGuard_GateKey
        unlock: [WorldFact_Smuggling_GateGuardKeyFound]

  - npc: GangDoorman (Ketch)
    exists: false
    identity_notes: >
      Gang member (Faction_Neutral like Brann, owner of RA_GangBasement), early 30s, Brann's man. Big, slow to
      speak, loyal to whoever pays. By day stands on the gang house deck by the hidden door; 02–03 in the
      basement settling the count with the Dock Guard; sleeps in the gang house. Carries Key_GangTunnel and
      Key_GangBasement (lootable after a knockout). Run /NPC:create.

    memories:
      - asset: Mem_GangDoorman_Greeting
        unlock:     []
        invalidate: []
        dialogue_topic: Greeting
        dialogue:
          - NPC: "Nets. Drying. Nothing to see. Brann's inside if you want him. If he wants you, he'll call."

      - asset: Mem_GangDoorman_Smuggling
        unlock:     [QuestFact_SmugglersTunnel_Started]
        invalidate: [DialogueFact_GangDoorman_Smuggling_Named, DialogueFact_StashOwner_Smuggling_Joined, QuestFact_SmugglersTunnel_Completed]
        dialogue_topic: Smuggling   # "What's behind the nets?"
        dialogue:
          - NPC: "Wall. Behind the nets there's a wall. Walls are dull."
          - Player: "Brann docked you two for sleeping on watch. It's in his tally."  → Text_GangDoorman_Smuggling_Named  [sets: DialogueFact_GangDoorman_Smuggling_Named]  [requires: Mem_GangDoorman_GateTally]
          - Player: "I saw you and the dock guard counting coin in the basement."  → Text_GangDoorman_Smuggling_Named  [sets: DialogueFact_GangDoorman_Smuggling_Named]  [requires: Mem_GangDoorman_GateMeeting]
          - Player: "Nothing."  → END
          - NPC (Named): "Two coin. He wrote that down? Course he did. Fine. I keep the door, and the door doesn't talk. You've seen what you've seen. Now go and see something else, before I'm told to stop you."

      - asset: Mem_GangDoorman_GateTally
        unlock: [WorldFact_Smuggling_DockTallyTaken]
      - asset: Mem_GangDoorman_GateMeeting
        unlock: [WorldFact_Smuggling_MeetingSeen]
```

Gate memories have no start dialogue: each is used by a gated choice of the same NPC (V15, V17).

---

## Rewards

```
rewards:
  onStart: null

  onStepCompleted:
    - stepIndex: 1      # Where the tunnel goes
      xp: 100
    - stepIndex: 2      # Who's in on it
      xp: 100

  onCompleted:          # every ending
    xp: 150

  # Per-ending gold: PlayerRewardSO on the DialogueFact (RewardFactType = Dialogue)
  onDialogue:
    - fact: DialogueFact_Warden_Smuggling_ReportedAll
      gold: 40
    - fact: DialogueFact_Warden_Smuggling_Reported
      gold: 20
    - fact: DialogueFact_Warden_Smuggling_CoverStory
      gold: 40          # Brann's pay; mention it in Mem_StashOwner_SmugglingAfter_Joined ("here's your share")

  onFailed: null
```

---

## Implementation Checklist

- [ ] Prerequisites P1–P8 shipped (separate specs; P5 needs P10 or uses its deck fallback), P9 edits done
- [ ] Fact assets created
- [ ] QuestSO created — `Assets/_Game/Data/Quests/SmugglersTunnel/Quest_SmugglersTunnel.asset`
- [ ] NPCs created — `GateGuard`, `GangDoorman`; DockGuard sheet filled
- [ ] Dialogue chains + memories created (Warden, StashOwner, DockGuard, GateGuard, GangDoorman)
- [ ] PlayerRewardSO assets created
- [ ] Registered — `QuestEventsManager` (auto-synced) and `QuestLogUI._allQuests`
- [ ] `quest_report SmugglersTunnel` shows no Error / Warning
- [ ] Deferred trust steps added once the trust quests exist
