---
title: 'Area 1 arrival, part 1 — escort to the stage and the public beating (event-001)'
type: 'feature'
created: '2026-11-12'
status: 'done'
baseline_commit: 'c91fb9097f22fe687f5803c7df5a8dbf8a89a363'
context: ['{project-root}/_bmad-output/project-context.md', '{project-root}/.claude/rules/attack-pipeline.md', '{project-root}/Assets/_Game/Scripts/AI/CLAUDE.md', '{project-root}/docs/World/Areas/area-001-company-encampment.md']
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** A new game starts at the player's tent with nothing happening. event-001 (escorted arrival, the Warden publicly beating Tam) doesn't exist, and Tam can be talked to from the start.

**Approach:** A new game spawns the player outside the arrival gate between two new Company guards. The guards escort the player down the arrival lane to the camp square. There the Warden gives a short forced speech, then knocks Tam out in 2–3 unarmed hits while the player is held in place. The guards then return to their posts on each side of the arrival gate, the event fact is set, and Tam gets up and walks to the Player's Zone campfire, where he can now be talked to. Part 2 (escort to the tent, Hobb's briefing) is deferred.

## Boundaries & Constraints

**Always:** Free walk: the player keeps control during the escort; the camera stays free during the hold. Completion is one `WorldFact` (`WorldFact_Arrival_Done`) set with `SetWorldEvent`; the sequence's in-progress state is never saved. Saving and autosave are refused while the sequence runs. The brains stay the only movers; the sequence drives them through `SetRoutineTarget` / `StartBrawl` and suspends the participants' `NpcRoutine` while it runs. Tunables and lines live on a data asset. The beating is real combat: the Warden's unarmed hits, Tam doesn't fight back, the knockout is non-lethal. The dev skip only works in the Editor / development builds.

**Ask First:** Any change to `IDamageable`, `KnockoutRules` or the Looting flow. Any change to `Core.unity`.

**Never:** Escort to the tent, Hobb's briefing, an empty starting inventory (designed later), camera direction or a player auto-mover, new animations, Tam bruise visuals, saving mid-sequence.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Happy path | New game | Gate spawn → "Move." bubble → guards walk the lane → stage: player held, Warden speech → 2–3 hits, Tam down → Warden closing bubble → hold released, guards walk back to the gate posts, fact set | N/A |
| Lagging | Player > follow distance behind the guards | Guards stop at their next point and wait | N/A |
| Straying | Player > stray distance from the guards | Warning bubble; still away after the grace time → nearest guard brawls the player | N/A |
| Dragged | Player knocked out during the escort | Body moved to the watch spot, guards snapped to the stage; the stage phase starts once the player is up | Normal knockout looting may happen |
| Interrupted beating | Warden is knocked out, or the beat timeout is reached | Sequence completes without the closing line | N/A |
| Load done | Save with the fact set | No sequence; everyone on their routines; Tam talkable | N/A |
| Load not done | Save without the fact (pre-existing saves) | Sequence restarts: player and participants snapped to start | N/A |
| Save attempt | Quicksave / autosave during the sequence | Refused, "Can't save right now" | N/A |
| Dev skip | Inspector toggle at start, or context menu in Play | Fact set, routines resumed (snap), player warped to the dev spot by the tent | Ignored in release builds |

</frozen-after-approval>

## Code Map

- `Scripts/AI/EntityBrain.cs` -- `SetRoutineTarget(spot, route, snap)`, `StartBrawl(FactionMember)`, private `RaiseSpeechBubble`; brawl retaliation skips passive (`DetectionRange <= 0`) NPCs
- `Scripts/AI/NPC/NpcRoutine.cs` -- evaluates on `Start` and every second; re-sends only on a block change
- `Scripts/AI/NPC/NPCPresence.cs` -- `RequestForcedTopic`, `CanInteract`
- `Scripts/AI/EntityHealth.cs` -- `IsUnconscious`, `KnockedOut` / `RegainedConsciousness`
- `Scripts/Player/PlayerStateManager.cs` -- `IsBusy` gates movement / combat; `Scripts/Player/PlayerHealth.cs` -- player knockout
- `Scripts/Core/Save/SaveSystem.cs` -- `CanSave(out reason)`; `Scripts/Core/SceneLoader.cs` -- new game spawns at `PlayerSpawnPoint`
- `Scripts/AI/NPC/TollGate.cs` -- the pattern to copy for a scene-owned encounter that polls brains
- `Scenes/StartingTown.unity` -- arrival gate (−38.4, 36.3), stage / post (1.5, 25), Warden (−3, 12), `PlayerSpawnPoint` (11, 37.85)

## Tasks & Acceptance

**Execution:**
- [x] `Scripts/AI/NPC/NpcRoutine.cs` -- `Suspend()` / `Resume(bool snap)`: while suspended no evaluation; `Resume` forces a re-send -- lets the sequence own the participants
- [x] `Scripts/AI/EntityBrain.cs` -- public `Say(string line, float duration)` wrapper over `RaiseSpeechBubble` -- guard and Warden bubbles
- [x] `Scripts/AI/NPC/NPCPresence.cs` -- optional `_availableAfter` (`WorldFact`): no talk / prompt until it is true -- gates Tam
- [x] `Scripts/Player/PlayerStateManager.cs` -- `SetScriptedHold(bool)` / `IsScriptedHold`, included in `IsBusy` -- holds the player during the beating
- [x] `Scripts/Core/Save/SaveSystem.cs` -- static `BlockSaving(object owner)` / `UnblockSaving(object owner)`; `CanSave` refuses while any block is held -- Core doesn't depend on World
- [x] `ScriptableObjects/World/ArrivalSequenceSO.cs` (new) -- follow / stray distances, stray grace, beat timeout, closing delay, `WorldFact`, Warden speech start node, `BarkSetSO`s (move, wait, stray warning, Warden closing)
- [x] `Scripts/World/Story/ArrivalSequence.cs` (new) + pure `ArrivalSequenceRules.cs` -- phases Escort → Speech → Beating → Return → Done. Starts once the region is loaded and the player is registered, if the fact is false. Two parallel lane routes (one per guard): advance when both guards arrived and the player is within follow distance. Applies the stray and drag rules. At the stage: hold → `RequestForcedTopic` → after it closes `StartBrawl(Tam)` → Tam unconscious → closing bubble → release hold, resume routines, set fact, unblock save. `[ContextMenu]` + inspector dev skip gated on `Application.isEditor || Debug.isDebugBuild` (project rule; `DEVELOPMENT_BUILD` is deprecated)
- [x] `Tests/EditMode/ArrivalSequenceRulesTests.cs` -- advance, wait, stray warning / brawl and completion decisions from the matrix
- [x] NPC data -- `Data/NPCs/CompanyGuard/` (one `NPCEntity`, Company faction, identity `CLAUDE.md`); `Warden/Dialogues/Arrival/` speech topic + closing barks in Voss's voice; guard barks; `NPC_BeatenWorker` `_baseHealth` 25 and `_detectionRange` 0 (passive); `Data/Facts/WorldFact_Arrival_Done`; `Data/World/ArrivalSequence_Event001.asset`
- [x] `StartingTown.unity` -- `ArrivalSequence` root: lane routes (left / right), stage posts, watch spot, Warden / Tam stage spots, dev spot near the tent. Two `NPC_base Variant` guards with Company armor and an all-day Stand `NpcRoutine` at the gate posts. Tam: all-day Stand routine at the campfire, `_availableAfter` wired. Move `PlayerSpawnPoint` outside the arrival gate
- [x] Docs -- `Scripts/AI/CLAUDE.md` (Suspend/Resume, `Say`, `_availableAfter`), `Scripts/World/CLAUDE.md` (ArrivalSequence), `Scenes/CLAUDE.md` (spawn moved, Warden event spot), `Data/Facts/CLAUDE.md`, `BeatenWorker/CLAUDE.md`, `docs/World/world-index.md` (event-001 implemented)

**Acceptance Criteria:**
- Given a new game, when the player walks with the guards, then they reach the stage with no NavMesh path errors, and Tam cannot be talked to before the fact is set.
- Given the beating starts, when the Warden attacks, then Tam is unconscious after 2–3 landed hits and never strikes back.
- Given the sequence is done, when Tam gets up, then he walks to the campfire and his greeting is available.
- Given Play mode starts with the dev skip enabled, then the fact is set and the player starts by the tent.

## Design Notes

The sequence owns the event and only drives existing brain mechanics, like `TollGate`. Tam is passive for good: a resigned man who never hits back fits him, and it keeps the beating predictable. Instead of Tam being moved out of sight, he gets up and walks to the campfire (his routine), which needs no visibility check. A dragged player's body pops to the watch spot; there is no fade system, which is acceptable for the graybox.

Found while implementing: a guard can't brawl a player beyond its 12 m disengage range, so a straying player is first intercepted (`BeginIntercept`, like `TollGate`), and straying is measured from the current lane point (a chasing guard would drag the guards' centre along). The Warden's unarmed punches whiffed at the humanoid 1.3 m stop distance, so his entity uses 0.9 / 1.1. The guards use the plain NPC body: NPCs can't wear the Company armor visual yet (deferred). The two guards have one identity but two `NPCEntity` assets, because each scene NPC needs its own `KilledFact`.

## Verification

**Commands:**
- Unity MCP `run_tests` EditMode -- expected: all green, including `ArrivalSequenceRulesTests`
- `read_console` after compile and on entering Play -- expected: no errors

**Manual checks:**
- Play a new game through each row of the matrix (stray, lag, provoke a guard, provoke the Warden); F5 during the escort is refused; load a post-event save.

## Suggested Review Order

**Sequence flow (entry point)**

- Starts only on a settled, non-loading region without the fact; dev skip short-circuits.
  [`ArrivalSequence.cs:126`](../../Assets/_Game/Scripts/World/Story/ArrivalSequence.cs#L126)

- Takes ownership: save block, routines suspended, cast snapped, run state reset.
  [`ArrivalSequence.cs:149`](../../Assets/_Game/Scripts/World/Story/ArrivalSequence.cs#L149)

- Escort tick: drag on knockout, stray rules from the lane, no advancing mid-brawl.
  [`ArrivalSequence.cs:174`](../../Assets/_Game/Scripts/World/Story/ArrivalSequence.cs#L174)

- Straying guard walks up first (intercept), brawls only once within reach.
  [`ArrivalSequence.cs:233`](../../Assets/_Game/Scripts/World/Story/ArrivalSequence.cs#L233)

- Knocked-out player is carried to the watch spot; guards snap to the stage.
  [`ArrivalSequence.cs:264`](../../Assets/_Game/Scripts/World/Story/ArrivalSequence.cs#L264)

**Stage: hold, speech, beating**

- Hold applies only while calm — never freeze a player being hit or lying down.
  [`ArrivalSequence.cs:293`](../../Assets/_Game/Scripts/World/Story/ArrivalSequence.cs#L293)

- Speech retried at an interval; timeout counts only while it could open.
  [`ArrivalSequence.cs:304`](../../Assets/_Game/Scripts/World/Story/ArrivalSequence.cs#L304)

- Real brawl on Tam; failure closes at once instead of freezing the player.
  [`ArrivalSequence.cs:338`](../../Assets/_Game/Scripts/World/Story/ArrivalSequence.cs#L338)

- Every exit releases hold, routines, save block and the Warden's brawl.
  [`ArrivalSequence.cs:381`](../../Assets/_Game/Scripts/World/Story/ArrivalSequence.cs#L381)

**Generic hooks in other systems**

- Routines can be lent to a sequence; Resume forces a re-send.
  [`NpcRoutine.cs:72`](../../Assets/_Game/Scripts/AI/NPC/NpcRoutine.cs#L72)

- Calling off a scripted brawl returns the NPC to its routine.
  [`EntityBrain.cs:1353`](../../Assets/_Game/Scripts/AI/EntityBrain.cs#L1353)

- Scripted speech bubble wrapper.
  [`EntityBrain.cs:836`](../../Assets/_Game/Scripts/AI/EntityBrain.cs#L836)

- Talk gated on a world fact (Tam before event-001); loot / forced topics unaffected.
  [`NPCPresence.cs:59`](../../Assets/_Game/Scripts/AI/NPC/NPCPresence.cs#L59)

- Owner-keyed static save block; Core stays free of World references.
  [`SaveSystem.cs:70`](../../Assets/_Game/Scripts/Core/Save/SaveSystem.cs#L70)

- Scripted hold joins IsBusy; interaction also refuses while held.
  [`PlayerStateManager.cs:69`](../../Assets/_Game/Scripts/Player/PlayerStateManager.cs#L69)

**Peripherals**

- Pure escort / stray / beating decisions.
  [`ArrivalSequenceRules.cs:31`](../../Assets/_Game/Scripts/World/Story/ArrivalSequenceRules.cs#L31)

- Tunables and lines for the event.
  [`ArrivalSequenceSO.cs:35`](../../Assets/_Game/ScriptableObjects/World/ArrivalSequenceSO.cs#L35)

- Truth tables for the rules.
  [`ArrivalSequenceRulesTests.cs:1`](../../Assets/Tests/EditMode/ArrivalSequenceRulesTests.cs#L1)
