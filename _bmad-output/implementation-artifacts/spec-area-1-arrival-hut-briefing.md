---
title: 'Area 1 arrival, part 2 — escort to the hut and Hobb''s briefing (event-001)'
type: 'feature'
created: '2026-11-13'
status: 'done'
baseline_commit: '992a5932b5d81e2251f1ed732ead9179d394b44a'
context: ['{project-root}/_bmad-output/project-context.md', '{project-root}/_bmad-output/implementation-artifacts/spec-area-1-arrival-escort-beating.md', '{project-root}/Assets/_Game/Data/NPCs/TentMate/CLAUDE.md']
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** After the beating, event-001 ends on the stage: the guards walk back to the gate and the player is left alone. The arrival never takes them to their hut, and Old Hobb (npc-001) never meets them.

**Approach:** After the beating, the two arrival guards escort the player from the stage to their hut in the Player's Zone, with the same free-walk rules as the stage escort. At the hut, Hobb opens a forced topic: he introduces himself and says where the player sleeps, then his normal topic list follows. Once that dialogue closes, the guards return to their gate posts and `WorldFact_Arrival_Done` is set.

## Boundaries & Constraints

**Always:** One completion fact, set at the very end (after the briefing closes); saving stays blocked for the whole sequence, so any load without the fact replays from the gate. The Warden's and Tam's routines resume right after the beating (Tam gets up and walks to the campfire; talk stays gated by the fact). Guards and Hobb stay suspended until completion. Escort-to-hut uses the part-1 rules: wait for a lagging player, warn then intercept + brawl a straying one, carry a knocked-out player to the hut. No player hold at the hut (the dialogue holds them). Dev options only in Editor / `Debug.isDebugBuild`.

**Ask First:** Any change to `IDamageable`, `KnockoutRules`, the Looting flow or `Core.unity`. Any change to Hobb's existing topics or memories.

**Never:** Stripped beat / empty starting inventory, camera direction, player auto-mover, new animations, saving mid-sequence, a second world fact.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Happy path | Beating done | Closing line → hold released, Warden / Tam routines resume → "Move." → guards walk the hut lanes → at the doorway, player within briefing distance of Hobb → Hobb's forced topic (2 lines) → his topic list → player closes → guards walk to gate posts, Hobb resumes routine, fact set, save unblocked | N/A |
| Lagging / straying | Same as the stage escort | Wait + bark / warn → intercept → brawl | N/A |
| Dragged | Player knocked out on the hut walk | Body moved to the hut watch spot, guards snapped to the doorway; briefing once the player is up | N/A |
| Player hangs back at the hut | Guards arrived, player > briefing distance from Hobb | Wait barks on cooldown; stray rules still apply | N/A |
| Hobb unavailable | Hobb down, or the topic can't open within the timeout (counted only while it could open) | Briefing skipped, sequence completes | Warning logged |
| Dev: Skip Arrival | Toggle at start / context menu | Fact set, everyone on routines (snap), player at `DevPlayerSpot` (now by the hut bed) | Ignored in release |
| Dev: Skip to Hut Escort | Toggle at start / context menu while not done | Warden / Tam on routines (snap), guards snapped to the stage, player at the watch spot, Hobb at his briefing spot, hut escort starts | Ignored in release |

</frozen-after-approval>

## Code Map

- `Scripts/World/Story/ArrivalSequence.cs` -- part 1 sequence; `TickEscort`, `Drag`, `TickClosing` → `Complete`, `Release`, `DevSkip`
- `Scripts/World/Story/ArrivalSequenceRules.cs` -- pure escort / stray / beating decisions
- `ScriptableObjects/World/ArrivalSequenceSO.cs` -- tunables + lines; asset `Data/World/ArrivalSequence_Event001`
- `ScriptableObjects/Dialogue/NPCDialogueRequestData.cs` -- `forcedStartNode`, `forcedLine`
- `Scripts/AI/NPC/NPCPresence.cs` -- `RequestForcedTopic(startNode)`
- `Scripts/World/DialogueSystem.cs:107` -- forced topic → `DialogueUI.OpenForcedTopic` + `StartTopic`
- `Scripts/UI/Dialogue/DialogueUI.cs:392` -- `RestoreTopics` closes a forced topic at chain end
- `Scenes/StartingTown.unity` -- stage lane ends (−9.5, 23.4–26.6), player hut `PlayersZone/worker-player` (−0.52, 47.97), bed (−0.50, 47.89), doorway ≈ (−2.2, 49.3); Hobb's hut `worker-mate` (−2.18, 44.56); Hobb's routine waypoints (5.95, 39.3) / (7.9, 38.2), night spot (4.4, 41)

## Tasks & Acceptance

**Execution:**
- [x] `ScriptableObjects/Dialogue/NPCDialogueRequestData.cs`, `Scripts/AI/NPC/NPCPresence.cs`, `Scripts/World/DialogueSystem.cs`, `Scripts/UI/Dialogue/DialogueUI.cs` -- add `forcedTopicThenTopics` (request flag, `RequestForcedTopic(node, thenTopics = false)`); `DialogueUI.OpenForcedTopic(npcName, thenTopics)` leaves `_forcedTopic` false so `RestoreTopics` re-queries the NPC's topics → chain end shows the normal topic list (Farewell included) instead of closing -- Hobb's follow-up topics; existing callers unchanged
- [x] `ScriptableObjects/World/ArrivalSequenceSO.cs` -- `hutBriefing` (StartDialogueNode), `tentMateCallBarks` (Hobb calls a player who stops short), `briefingDistance` (3), `briefingOpenTimeout` (8) -- data
- [x] `Scripts/World/Story/ArrivalSequenceRules.cs` -- `EvaluateBriefing(speakerDown, playerInRange, canOpen, waitTime, timeout)` → Wait / Open / Skip
- [x] `Scripts/World/Story/ArrivalSequence.cs` -- make the escort tick lane-pair generic (active lanes + drag spot + arrival action). Cast gains Hobb (routine suspended at `Begin`, snapped to `_tentMateSpot`). Closing → end Warden brawl, release hold, resume Warden / Tam routines, "Move." bark, start the hut escort (`_hutLaneLeft/Right`, `_hutWatchSpot`). Arrival → Briefing: Hobb's call barks while far, open `hutBriefing` with `thenTopics`, opened = cursor unlocked synchronously, closed = cursor locked again → `Complete`. Dev: `_devSkip` bool → `DevStart` enum (None / SkipAll / SkipToHutEscort) + `[ContextMenu("Dev: Skip to Hut Escort")]`
- [x] `Tests/EditMode/ArrivalSequenceRulesTests.cs` -- briefing decisions (Hobb down → Skip, out of range → Wait, timeout → Skip, in range + can open → Open)
- [x] `Data/NPCs/TentMate/Dialogues/Arrival/` -- `Start_TentMate_ArrivalBriefing` + 2 text nodes: "Easy. They've had their show. I'm Hobb. Next hut over's mine." / "This one's yours. Bed's against the back wall. Don't sleep anywhere else — the Corner behind us is where they put the ones who stopped working." Not in any memory; wire into `ArrivalSequence_Event001`
- [x] `StartingTown.unity` -- under `ArrivalSequence`: `HutLanes` (left / right, same count, first points by the stage, last flanking the hut doorway, NavMesh-reachable), `HutWatchSpot`, `TentMateSpot` (outside the doorway, facing the arriving escort); wire Hobb + spots; move `DevPlayerSpot` outside the hut's south door (the bed fills the hut); move Hobb's routine to his real hut: day Patrol between his hut door and the Player's Zone campfire, night spot at his hut door
- [x] Docs -- `Scripts/World/CLAUDE.md` (part 2, dev enum), `Scripts/AI/CLAUDE.md` (Hobb routine, `RequestForcedTopic` thenTopics), `UI/Dialogue/CLAUDE.md` (forced topic then topics), `TentMate/CLAUDE.md`, `Scenes/CLAUDE.md` if it lists the dev spot, `docs/World/world-index.md` / area-001 (event-001 complete), `deferred-work.md` (part 2 shipped)

**Acceptance Criteria:**
- Given the beating ends, when the closing delay passes, then Tam gets up and walks to the campfire while the guards lead the player toward the hut, and Tam still cannot be talked to.
- Given the player reaches Hobb at the hut, when his two lines end, then his normal topics and Farewell are shown and the dialogue only closes on the player's choice.
- Given the briefing closes, then both guards walk back to their gate posts, Hobb returns to his routine, F5 works and Tam can be talked to.
- Given any other forced topic (toll, Warden speech), then it still closes at its chain end.

## Spec Change Log

- 2026-11-13, review (acceptance audit, spec drift, no loopback): task text amended to what was built and verified in Play: `OpenForcedTopic(npcName, thenTopics)` (re-query in `RestoreTopics`) instead of passing start nodes; names `hutBriefing` / `_tentMateSpot` / `TentMateSpot` instead of `hobbBriefing` / `HobbBriefingSpot`; `DevPlayerSpot` outside the south door because the bed fills the hut (the frozen matrix row still says "by the hut bed"). Added `tentMateCallBarks` from review (a player stopping 3–7 m from Hobb only heard the guards' lag lines). KEEP: the cursor-based opened / closed detection and the shared escort tick.

## Design Notes

The hut walk is the stage walk with a different lane pair, watch spot and arrival action, so the escort tick is parameterised rather than duplicated. The Warden's speech already detects "opened / closed" by the cursor; the briefing reuses it, which works because the topic list keeps the cursor unlocked until the player leaves. Hobb is snapped to his spot at `Begin` (out of sight of the gate) so he is waiting when the escort arrives. User decision (2026-11-13): Hobb's routine moves to his real hut in this spec; his Greeting topic stays as is.

Found while implementing: the yard between the player hut's south door and Hobb's east door can only be reached from the east (the NavMesh path from the west runs through Hobb's hut), so the hut lanes cross the square and pass the campfire. Review fixes: the briefing timeout no longer counts while Hobb is fighting; a knockout during the briefing (which closes the dialogue) drags and restarts it instead of counting as "briefed"; Closing waits for a knocked-out player to get up before leaving the stage; the Warden no longer holds the hut escort; the dev skips refuse with a dialogue open and Skip to Hut Escort checks the fact.

## Verification

**Commands:**
- Unity MCP `run_tests` EditMode -- expected: all green incl. `ArrivalSequenceRulesTests`
- `read_console` after compile and in Play -- expected: no errors

**Manual checks:**
- New game end to end; stray and get knocked out on the hut walk; hang back at the hut; Dev: Skip to Hut Escort; Dev: Skip Arrival lands outside the hut door; a toll dialogue still closes at its end.

## Suggested Review Order

**Sequence flow (part 2)**

- Closing hands the stage cast back, then the same escort leads on to the hut.
  [`ArrivalSequence.cs:473`](../../Assets/_Game/Scripts/World/Story/ArrivalSequence.cs#L473)

- One escort tick for both legs; the leg picks lanes, drag spot and arrival action.
  [`ArrivalSequence.cs:99`](../../Assets/_Game/Scripts/World/Story/ArrivalSequence.cs#L99)

- Briefing: knockout restarts it, timeout counts only while it could open, cursor relock completes.
  [`ArrivalSequence.cs:495`](../../Assets/_Game/Scripts/World/Story/ArrivalSequence.cs#L495)

- Hobb calls over a player who stopped short of him.
  [`ArrivalSequence.cs:299`](../../Assets/_Game/Scripts/World/Story/ArrivalSequence.cs#L299)

- Hobb joins the owned cast at the start, waiting at his spot.
  [`ArrivalSequence.cs:201`](../../Assets/_Game/Scripts/World/Story/ArrivalSequence.cs#L201)

- Closing waits for a knocked-out player before leaving the stage.
  [`ArrivalSequence.cs:454`](../../Assets/_Game/Scripts/World/Story/ArrivalSequence.cs#L454)

- The Warden only holds the escort on the stage leg.
  [`ArrivalSequence.cs:391`](../../Assets/_Game/Scripts/World/Story/ArrivalSequence.cs#L391)

**Dialogue: forced topic, then topics**

- New flag: the forced chain ends on the NPC's topic list.
  [`DialogueUI.cs:231`](../../Assets/_Game/Scripts/UI/Dialogue/DialogueUI.cs#L231)

- Request plumbing; existing callers keep closing at the chain end.
  [`NPCPresence.cs:109`](../../Assets/_Game/Scripts/AI/NPC/NPCPresence.cs#L109)

  [`DialogueSystem.cs:115`](../../Assets/_Game/Scripts/World/DialogueSystem.cs#L115)

**Dev**

- Start toggle enum; Skip to Hut Escort checks the fact and an open dialogue.
  [`ArrivalSequence.cs:583`](../../Assets/_Game/Scripts/World/Story/ArrivalSequence.cs#L583)

**Peripherals**

- Pure briefing decision.
  [`ArrivalSequenceRules.cs:59`](../../Assets/_Game/Scripts/World/Story/ArrivalSequenceRules.cs#L59)

- Briefing topic, call barks, distance and timeout.
  [`ArrivalSequenceSO.cs:23`](../../Assets/_Game/ScriptableObjects/World/ArrivalSequenceSO.cs#L23)

- Briefing truth table.
  [`ArrivalSequenceRulesTests.cs:78`](../../Assets/Tests/EditMode/ArrivalSequenceRulesTests.cs#L78)
